// One pipe connection = one session = one Backend process.
// Owns: handshake state, in-flight CancellationTokenSource per correlationId, request loop.
//
// Threading: read loop runs on a worker thread (kicked off from NamedPipeServer.Accept).
// Tool dispatch is async; long-running work goes through ToolHost which itself dispatches
// to the AutoCAD UI thread when needed (rule 10).

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;
using AcadMcp.Plugin.Tools;
using AcadMcp.Shared;
using AcadMcp.Shared.Pipe;

namespace AcadMcp.Plugin.Pipe;

internal sealed class PipeSession : IDisposable
{
    private readonly NamedPipeServerStream _stream;
    private readonly IToolHost _toolHost;
    private readonly Func<HandshakeRequest, HandshakeResponse> _handshakeFactory;
    private readonly CancellationTokenSource _sessionCts;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight = new();
    private readonly string _sessionId;
    private long _requestCount;
    private bool _handshakeComplete;
    private string _clientId = "<unknown>";
    private string _category = "<unknown>";

    public PipeSession(
        NamedPipeServerStream stream,
        IToolHost toolHost,
        Func<HandshakeRequest, HandshakeResponse> handshakeFactory,
        CancellationToken serverToken)
    {
        _stream = stream;
        _toolHost = toolHost;
        _handshakeFactory = handshakeFactory;
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
        _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public string SessionId => _sessionId;
    public long RequestCount => Interlocked.Read(ref _requestCount);
    public string ClientId => _clientId;
    public string Category => _category;

    public async Task RunAsync()
    {
        Log.Info($"[{_sessionId}] Pipe session started");
        try
        {
            while (!_sessionCts.IsCancellationRequested)
            {
                MessageEnvelope? env;
                try
                {
                    env = await PipeFraming.ReadEnvelopeAsync(_stream, _sessionCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (IOException ex)
                {
                    Log.Warn($"[{_sessionId}] Pipe read failed: {ex.Message}");
                    break;
                }

                if (env is null)
                {
                    Log.Info($"[{_sessionId}] Peer closed connection (clean EOF)");
                    break;
                }

                _ = HandleEnvelopeAsync(env);
            }
        }
        finally
        {
            CancelAllInFlight();
            Log.Info($"[{_sessionId}] Pipe session ended (handled {RequestCount} requests)");
        }
    }

    private async Task HandleEnvelopeAsync(MessageEnvelope env)
    {
        try
        {
            switch (env.Kind)
            {
                case MessageKind.Handshake:
                    await HandleHandshakeAsync(env).ConfigureAwait(false);
                    break;
                case MessageKind.Tool:
                    await HandleToolAsync(env).ConfigureAwait(false);
                    break;
                case MessageKind.Cancel:
                    HandleCancel(env);
                    break;
                default:
                    Log.Warn($"[{_sessionId}] Unknown message kind={env.Kind} - ignoring");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[{_sessionId}] HandleEnvelopeAsync failed for kind={env.Kind}", ex);
        }
    }

    private async Task HandleHandshakeAsync(MessageEnvelope env)
    {
        var req = PipeFraming.Unwrap<HandshakeRequest>(env);
        if (req is null)
        {
            await SendErrorHandshakeAsync(AcadErrorCode.InvalidArgument, "Handshake payload missing").ConfigureAwait(false);
            return;
        }

        if (req.ProtocolVersion < PipeProtocol.MinSupportedVersion || req.ProtocolVersion > PipeProtocol.CurrentVersion)
        {
            await SendErrorHandshakeAsync(
                AcadErrorCode.ProtocolMismatch,
                $"Protocol version {req.ProtocolVersion} not supported. Plugin supports [{PipeProtocol.MinSupportedVersion}..{PipeProtocol.CurrentVersion}].").ConfigureAwait(false);
            _sessionCts.Cancel();
            return;
        }

        _clientId = req.ClientId ?? "<unset>";
        _category = req.Category ?? "<unset>";
        _handshakeComplete = true;

        var response = _handshakeFactory(req);
        await SendAsync(MessageKind.HandshakeResponse, response).ConfigureAwait(false);
        Log.Info($"[{_sessionId}] Handshake OK clientId={_clientId} category={_category} backend={req.BackendVersion}");
    }

    private async Task HandleToolAsync(MessageEnvelope env)
    {
        var req = PipeFraming.Unwrap<ToolRequest>(env);
        if (req is null)
        {
            await SendToolErrorAsync(correlationId: "<missing>",
                new ErrorInfo(AcadErrorCode.InvalidArgument, "Tool payload missing")).ConfigureAwait(false);
            return;
        }

        if (!_handshakeComplete)
        {
            await SendToolErrorAsync(req.CorrelationId,
                new ErrorInfo(AcadErrorCode.ProtocolMismatch, "Tool call before handshake")).ConfigureAwait(false);
            return;
        }

        Interlocked.Increment(ref _requestCount);
        using var perRequestCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
        if (!_inFlight.TryAdd(req.CorrelationId, perRequestCts))
        {
            await SendToolErrorAsync(req.CorrelationId,
                new ErrorInfo(AcadErrorCode.InvalidArgument, $"Duplicate correlationId '{req.CorrelationId}'")).ConfigureAwait(false);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log.Info($"[{_sessionId}] --> tool={req.Tool} corr={req.CorrelationId} timeoutMs={req.TimeoutMs}");
        try
        {
            var result = await _toolHost.DispatchAsync(req.Tool, req.Args ?? new JsonObject(), req.TimeoutMs, perRequestCts.Token).ConfigureAwait(false);
            var response = new ToolResponse(
                CorrelationId: req.CorrelationId,
                Ok: result.Ok,
                Result: result.Result,
                Error: result.Error);
            await SendAsync(MessageKind.ToolResponse, response).ConfigureAwait(false);
            sw.Stop();
            if (result.Ok)
                Log.Info($"[{_sessionId}] <-- tool={req.Tool} corr={req.CorrelationId} ok elapsedMs={sw.ElapsedMilliseconds}");
            else
                Log.Warn($"[{_sessionId}] <-- tool={req.Tool} corr={req.CorrelationId} FAILED code={result.Error?.Code} msg={result.Error?.Message} elapsedMs={sw.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error($"[{_sessionId}] <-- tool={req.Tool} corr={req.CorrelationId} THREW elapsedMs={sw.ElapsedMilliseconds}", ex);
            try
            {
                await SendToolErrorAsync(req.CorrelationId,
                    new ErrorInfo(AcadErrorCode.InternalError, $"{ex.GetType().Name}: {ex.Message}")).ConfigureAwait(false);
            }
            catch { }
        }
        finally
        {
            _inFlight.TryRemove(req.CorrelationId, out _);
            perRequestCts.Dispose();
        }
    }

    private void HandleCancel(MessageEnvelope env)
    {
        var req = PipeFraming.Unwrap<CancelRequest>(env);
        if (req is null) return;
        if (_inFlight.TryGetValue(req.CorrelationId, out var cts))
        {
            try { cts.Cancel(); } catch { }
            Log.Debug($"[{_sessionId}] Cancel signaled for {req.CorrelationId}");
        }
    }

    private async Task SendAsync<T>(MessageKind kind, T payload) where T : class
    {
        var env = PipeFraming.Wrap(kind, payload);
        await _writeLock.WaitAsync(_sessionCts.Token).ConfigureAwait(false);
        try
        {
            await PipeFraming.WriteEnvelopeAsync(_stream, env, _sessionCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn($"[{_sessionId}] Send failed kind={kind}: {ex.Message}");
            _sessionCts.Cancel();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private Task SendToolErrorAsync(string correlationId, ErrorInfo error)
    {
        var resp = new ToolResponse(correlationId, Ok: false, Result: null, Error: error);
        return SendAsync(MessageKind.ToolResponse, resp);
    }

    private Task SendErrorHandshakeAsync(AcadErrorCode code, string message)
    {
        var resp = new HandshakeResponse(
            Ok: false,
            PluginVersion: "0.1.0",
            AcadVersion: "<unknown>",
            AcadVertical: null,
            IsLT: false,
            NegotiatedProtocolVersion: 0,
            Error: new ErrorInfo(code, message));
        return SendAsync(MessageKind.HandshakeResponse, resp);
    }

    private void CancelAllInFlight()
    {
        foreach (var kv in _inFlight)
        {
            try { kv.Value.Cancel(); } catch { }
        }
        _inFlight.Clear();
    }

    public void Dispose()
    {
        try { _sessionCts.Cancel(); } catch { }
        try { _stream.Dispose(); } catch { }
        try { _writeLock.Dispose(); } catch { }
        try { _sessionCts.Dispose(); } catch { }
    }
}
