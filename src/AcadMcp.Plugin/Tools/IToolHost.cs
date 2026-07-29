// Plugin-side tool dispatcher contract.
// Phase 1: in-process registry of plugin handlers (one per tool name).
// Each category MCP server (Backend) calls these by name over the pipe.

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

/// <summary>Result of a plugin-side tool dispatch. Mirrors <see cref="ToolResponse"/>.</summary>
public sealed record ToolDispatchResult(bool Ok, JsonObject? Result, ErrorInfo? Error);

/// <summary>Dispatcher for tool calls arriving on the pipe. Looks up the handler and invokes it.</summary>
public interface IToolHost
{
    /// <summary>True if the tool name is registered in the plugin.</summary>
    bool HasTool(string toolName);

    /// <summary>Invoke a tool by name. Implementation MUST honor cancellation token.</summary>
    Task<ToolDispatchResult> DispatchAsync(string toolName, JsonObject args, int timeoutMs, CancellationToken ct);

    /// <summary>List all registered tool names (for diagnostic / status command).</summary>
    System.Collections.Generic.IReadOnlyCollection<string> RegisteredTools { get; }
}
