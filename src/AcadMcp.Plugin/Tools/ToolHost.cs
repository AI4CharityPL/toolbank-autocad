// In-process registry of plugin-side tool handlers. Phase 1: only built-in diagnostic tools
// (acad_status, _echo). Real tools (draw_circle, ...) register themselves here in Phase 2+.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed class ToolHost : IToolHost
{
    private readonly ConcurrentDictionary<string, Func<JsonObject, CancellationToken, Task<ToolDispatchResult>>> _handlers = new();

    public IReadOnlyCollection<string> RegisteredTools => (IReadOnlyCollection<string>)_handlers.Keys;

    public bool HasTool(string toolName) => _handlers.ContainsKey(toolName);

    public void Register(string toolName, Func<JsonObject, CancellationToken, Task<ToolDispatchResult>> handler)
    {
        if (string.IsNullOrWhiteSpace(toolName)) throw new ArgumentException("toolName empty", nameof(toolName));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (!_handlers.TryAdd(toolName, handler))
        {
            Log.Warn($"ToolHost.Register: tool '{toolName}' already registered, ignoring duplicate.");
        }
        else
        {
            Log.Debug($"ToolHost registered tool '{toolName}' (total={_handlers.Count})");
        }
    }

    public async Task<ToolDispatchResult> DispatchAsync(string toolName, JsonObject args, int timeoutMs, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(toolName, out var handler))
        {
            return new ToolDispatchResult(
                Ok: false,
                Result: null,
                Error: new ErrorInfo(
                    Code: AcadErrorCode.UnknownTool,
                    Message: $"Plugin has no tool registered with name '{toolName}'.",
                    Hint: $"Plugin currently knows: {string.Join(", ", _handlers.Keys)}"));
        }

        using var timeoutCts = new CancellationTokenSource();
        if (timeoutMs > 0) timeoutCts.CancelAfter(timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await handler(args, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return new ToolDispatchResult(false, null, new ErrorInfo(
                AcadErrorCode.Timeout,
                $"Tool '{toolName}' exceeded its {timeoutMs} ms timeout."));
        }
        catch (OperationCanceledException)
        {
            return new ToolDispatchResult(false, null, new ErrorInfo(
                AcadErrorCode.Timeout,
                $"Tool '{toolName}' was cancelled by the caller."));
        }
        catch (Exception ex)
        {
            Log.Error($"ToolHost dispatch '{toolName}' failed", ex);
            return new ToolDispatchResult(false, null, new ErrorInfo(
                AcadErrorCode.InternalError,
                $"Plugin handler for '{toolName}' threw {ex.GetType().Name}: {ex.Message}"));
        }
    }
}
