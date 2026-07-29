// Listener loop. One server instance, many concurrent NamedPipeServerStream connections
// (one per Backend / category MCP). Each connection runs in its own PipeSession.

using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;
using AcadMcp.Plugin.Tools;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Pipe;

internal sealed class NamedPipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly IToolHost _toolHost;
    private readonly Func<HandshakeRequest, HandshakeResponse> _handshakeFactory;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, PipeSession> _sessions = new();
    private Task? _acceptLoop;
    private int _maxConcurrent;

    public string PipeName => _pipeName;
    public int ActiveSessions => _sessions.Count;
    public int MaxConcurrentObserved => _maxConcurrent;

    public NamedPipeServer(string pipeName, IToolHost toolHost, Func<HandshakeRequest, HandshakeResponse> handshakeFactory)
    {
        _pipeName = pipeName;
        _toolHost = toolHost;
        _handshakeFactory = handshakeFactory;
    }

    public void Start()
    {
        if (_acceptLoop is not null) throw new InvalidOperationException("Already started");
        _acceptLoop = Task.Run(AcceptLoopAsync);
        Log.Info($"NamedPipeServer accept loop started on \\\\.\\pipe\\{_pipeName}");
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    pipeName: _pipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                Log.Error("AcceptLoop: WaitForConnection failed", ex);
                server?.Dispose();
                await Task.Delay(500, _cts.Token).ContinueWith(_ => { }).ConfigureAwait(false);
                continue;
            }

            var session = new PipeSession(server, _toolHost, _handshakeFactory, _cts.Token);
            _sessions[session.SessionId] = session;
            int now = _sessions.Count;
            if (now > _maxConcurrent) _maxConcurrent = now;

            _ = Task.Run(async () =>
            {
                try { await session.RunAsync().ConfigureAwait(false); }
                catch (Exception ex) { Log.Error($"Session {session.SessionId} crashed", ex); }
                finally
                {
                    _sessions.TryRemove(session.SessionId, out _);
                    session.Dispose();
                }
            });
        }

        Log.Info("NamedPipeServer accept loop exited");
    }

    public async Task StopAsync(TimeSpan drainTimeout)
    {
        try { _cts.Cancel(); } catch { }
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { }
        }

        var deadline = DateTime.UtcNow + drainTimeout;
        while (_sessions.Count > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        foreach (var s in _sessions.Values)
        {
            try { s.Dispose(); } catch { }
        }
        _sessions.Clear();
        Log.Info($"NamedPipeServer stopped (max concurrent observed: {_maxConcurrent})");
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
