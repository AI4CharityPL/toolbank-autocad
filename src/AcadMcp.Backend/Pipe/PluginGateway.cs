// Singleton gateway. Owns a single PluginPipeClient per Backend process.
// Lazy-connects on first call, attempts ONE reconnect on dropped pipe.
// See rule 18-backend-host-and-gateway.mdc.

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Shared;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Pipe;

public sealed class PluginGateway : IPluginGateway, IAsyncDisposable
{
    private readonly ILoggerFactory _lf;
    private readonly ILogger<PluginGateway> _log;
    private readonly StartupOptions _options;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private PluginPipeClient? _client;

    public PluginGateway(ILoggerFactory lf, StartupOptions options)
    {
        _lf = lf;
        _log = lf.CreateLogger<PluginGateway>();
        _options = options;
    }

    public HandshakeResponse? Handshake => _client?.Handshake;
    public bool IsConnected => _client?.IsConnected == true;

    public async Task<JsonNode?> InvokeAsync(string tool, JsonObject args, int timeoutMs, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            await EnsureConnectedAsync(ct).ConfigureAwait(false);
            try
            {
                var resp = await _client!.CallToolAsync(tool, args, timeoutMs, ct).ConfigureAwait(false);
                if (!resp.Ok)
                {
                    var code = resp.Error?.Code.ToString() ?? "Unknown";
                    var msg  = resp.Error?.Message ?? "(no message)";
                    throw new PluginToolException(tool, code, msg);
                }
                return resp.Result;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Pipe connection closed", StringComparison.OrdinalIgnoreCase))
            {
                _log.LogWarning("Pipe dropped while calling '{Tool}' (attempt {Attempt}/2). Reconnecting...", tool, attempt);
                await DropClientAsync().ConfigureAwait(false);
                if (attempt == 2) throw new PluginUnavailableException(_options.PipeName,
                    $"Pipe dropped twice while calling '{tool}'. Plugin may have crashed; check %LOCALAPPDATA%\\AcadMcp\\logs.", ex);
            }
        }
        throw new InvalidOperationException("unreachable"); // satisfies compiler; loop always returns or throws
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client?.IsConnected == true) return;
        await _connectGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_client?.IsConnected == true) return;
            await DropClientAsync().ConfigureAwait(false);

            var c = new PluginPipeClient(
                _lf.CreateLogger<PluginPipeClient>(),
                clientId: $"acad-{_options.Category}/{Environment.ProcessId}",
                category: _options.Category,
                pipeName: _options.PipeName);
            try
            {
                await c.ConnectAsync(ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await c.DisposeAsync().ConfigureAwait(false);
                throw new PluginUnavailableException(_options.PipeName, ex.Message, ex);
            }
            _client = c;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async Task DropClientAsync()
    {
        if (_client is null) return;
        try { await _client.DisposeAsync().ConfigureAwait(false); } catch { }
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DropClientAsync().ConfigureAwait(false);
        _connectGate.Dispose();
    }
}
