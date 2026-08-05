// Backend-side client for the AutoCAD plugin's named pipe.
// Owns one persistent connection. Reused across many ToolRequest calls per process.
// Thread-safe: a single read loop demuxes responses by correlationId.
//
// See rule 17-pipe-protocol.md.

using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Shared;
using AcadMcp.Shared.Pipe;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Pipe;

public sealed class PluginPipeClient : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly string _clientId;
    private readonly string _category;
    private readonly ILogger<PluginPipeClient> _log;
    private readonly TimeSpan _connectTimeout;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ToolResponse>> _pending = new();
    private readonly CancellationTokenSource _disposed = new();

    /// <summary>
    /// Extra time the client waits beyond a tool's declared timeout, so the plugin's own refusal -
    /// which names the tool and the reason - wins the race whenever it can produce one.
    /// </summary>
    private const int TimeoutGraceMs = 2_000;
    private NamedPipeClientStream? _stream;
    private Task? _readLoop;
    private HandshakeResponse? _handshake;
    private TaskCompletionSource<HandshakeResponse>? _handshakeTcs;

    public PluginPipeClient(
        ILogger<PluginPipeClient> log,
        string clientId,
        string category,
        string? pipeName = null,
        TimeSpan? connectTimeout = null)
    {
        _log = log;
        _clientId = clientId;
        _category = category;
        _pipeName = pipeName ?? Environment.GetEnvironmentVariable("ACADMCP_PIPE") ?? PipeProtocol.PipeName;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
    }

    public bool IsConnected => _stream?.IsConnected == true;
    public HandshakeResponse? Handshake => _handshake;

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_stream is not null) throw new InvalidOperationException("Already connected");

        _stream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await _stream.ConnectAsync((int)_connectTimeout.TotalMilliseconds, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                $"AutoCAD plugin not reachable on pipe '\\\\.\\pipe\\{_pipeName}' within {_connectTimeout.TotalSeconds}s. " +
                $"Make sure AcadMcp.Plugin.dll is NETLOAD'ed inside an open AutoCAD session.");
        }

        _readLoop = Task.Run(() => ReadLoopAsync(_disposed.Token));

        _handshakeTcs = new TaskCompletionSource<HandshakeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hs = new HandshakeRequest(
            ClientId: _clientId,
            Category: _category,
            ProtocolVersion: PipeProtocol.CurrentVersion,
            BackendVersion: GetBackendVersion());
        await SendAsync(MessageKind.Handshake, hs, ct).ConfigureAwait(false);

        using var to = new CancellationTokenSource(_connectTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, to.Token);
        var hsResult = await _handshakeTcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);

        if (!hsResult.Ok)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Plugin rejected handshake: {hsResult.Error?.Code} {hsResult.Error?.Message}");
        }
        _handshake = hsResult;
        _log.LogInformation("Connected to AutoCAD plugin v{V} acad={Acad} (pipe={Pipe})",
            hsResult.PluginVersion, hsResult.AcadVersion, _pipeName);
    }

    public async Task<ToolResponse> CallToolAsync(string tool, JsonObject args, int timeoutMs, CancellationToken ct)
    {
        if (_stream is null || _handshake is null) throw new InvalidOperationException("Not connected");

        var correlationId = Guid.NewGuid().ToString("N");
        var req = new ToolRequest(correlationId, tool, args, timeoutMs);
        var tcs = new TaskCompletionSource<ToolResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        try
        {
            await SendAsync(MessageKind.Tool, req, ct).ConfigureAwait(false);

            // The timeout has to be enforced HERE as well as inside the plugin.
            //
            // It was previously only packed into the request above and left to the plugin to
            // honour, and the plugin honours it faithfully - right up until it cannot. A handler
            // blocked on a MODAL AutoCAD dialog never reaches its own timeout check, never
            // replies, and this await had nothing but `ct` to wake it. So every timeout in the
            // bank was advisory: publish_sheets declares T_LONG = 300 s and hung for ten minutes
            // before the caller was killed by hand, taking the backend process with it.
            //
            // A grace margin on top of the declared timeout keeps the plugin's own, more
            // informative refusal winning the race whenever it is able to produce one.
            using var timeout = new CancellationTokenSource(
                timeoutMs > 0 ? timeoutMs + TimeoutGraceMs : Timeout.Infinite);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            using var reg = linked.Token.Register(() =>
            {
                _ = SendAsync(MessageKind.Cancel, new CancelRequest(correlationId), CancellationToken.None);
            });

            try
            {
                return await tcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Plugin tool '{tool}' did not answer within {timeoutMs} ms. The request was " +
                    "sent and a cancel was sent after it, but AutoCAD never replied - which most " +
                    "often means a handler is blocked on a modal dialog inside AutoCAD. Look at " +
                    "the AutoCAD window; the tool cannot see what is on screen.");
            }
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                var env = await PipeFraming.ReadEnvelopeAsync(_stream, ct).ConfigureAwait(false);
                if (env is null) break;

                switch (env.Kind)
                {
                    case MessageKind.HandshakeResponse:
                        var resp = PipeFraming.Unwrap<HandshakeResponse>(env);
                        if (resp is not null) _handshakeTcs?.TrySetResult(resp);
                        break;
                    case MessageKind.ToolResponse:
                        var tr = PipeFraming.Unwrap<ToolResponse>(env);
                        if (tr is not null && _pending.TryRemove(tr.CorrelationId, out var tcs))
                        {
                            tcs.TrySetResult(tr);
                        }
                        break;
                    default:
                        _log.LogWarning("Ignoring unknown message kind {Kind}", env.Kind);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogError(ex, "Pipe read loop crashed");
        }
        finally
        {
            FailAllPending(new InvalidOperationException("Pipe connection closed"));
        }
    }

    private async Task SendAsync<T>(MessageKind kind, T payload, CancellationToken ct) where T : class
    {
        if (_stream is null) throw new InvalidOperationException("Not connected");
        var env = PipeFraming.Wrap(kind, payload);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PipeFraming.WriteEnvelopeAsync(_stream, env, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            kv.Value.TrySetException(ex);
        }
        _pending.Clear();
        _handshakeTcs?.TrySetException(ex);
    }

    private static string GetBackendVersion()
    {
        var v = typeof(PluginPipeClient).Assembly.GetName().Version;
        return v?.ToString(3) ?? "0.1.0";
    }

    public async ValueTask DisposeAsync()
    {
        try { _disposed.Cancel(); } catch { }
        try { if (_readLoop is not null) await _readLoop.ConfigureAwait(false); } catch { }
        try { _stream?.Dispose(); } catch { }
        _writeLock.Dispose();
        _disposed.Dispose();
    }
}
