# Backend host & plugin gateway contract

Backend stdio host - one process per category, one IPluginGateway per process, no direct AutoCAD calls

`AcadMcp.Backend.exe` is a thin shell. One process exposes ONE category over MCP stdio, plus a single shared client to the AutoCAD plugin. Tool implementations live in `src/AcadMcp.Categories/Acad.<Category>/` and MUST go through `IPluginGateway` for any AutoCAD work — never reach for `Application.DocumentManager` or `Database` directly from the Backend process. Direct AutoCAD types are not even loaded in this process; they live in the plugin AppDomain inside `acad.exe`.

## Process model (mandatory)

- One `AcadMcp.Backend.exe` per loaded MCP category. The MCP client / MCPBank starts/stops them on demand.
- Each process has exactly one `PluginPipeClient` instance, registered as singleton, lazy-connected on first tool call (NOT on `initialize`).
- `--category router` is the only special case: starts `RouterServer`, no plugin connection at all.
- Stdio is reserved for MCP JSON-RPC. Logs go to stderr only (rule 03).

## IPluginGateway abstraction (mandatory)

All tool methods that need AutoCAD MUST take `IPluginGateway` (or a higher-level domain service that wraps it) by parameter or by static service locator on the catalog instance — NEVER instantiate a `PluginPipeClient` themselves.

```csharp
public interface IPluginGateway
{
    Task<JsonNode?> InvokeAsync(string tool, JsonObject args, int timeoutMs, CancellationToken ct);
    HandshakeResponse? Handshake { get; } // null until first call succeeds
    bool IsConnected { get; }
}
```

Behaviour requirements:

- `InvokeAsync` lazy-connects on first call. Connection failure surfaces as `PluginUnavailableException` with the exact pipe name and remediation hint ("NETLOAD AcadMcp.Plugin.dll inside an open AutoCAD session").
- After a dropped pipe, the next `InvokeAsync` MUST attempt one reconnect before failing.
- `timeoutMs` is per-call, default 30 000 ms for write operations and 5 000 ms for read-only (`McpToolAttribute.ReadOnly == true`). The dispatcher passes the right value automatically.
- The gateway is thread-safe: many `tools/call` requests can be in flight in parallel; `PluginPipeClient` already de-multiplexes by correlation id.
- The gateway MUST translate `ToolResponse.Error` into a typed `PluginToolException` so the dispatcher in `CategoryServer` can map it to MCP `isError: true` content without unwrapping `JsonNode` manually.

## Forbidden patterns

- ❌ `using Autodesk.AutoCAD.*;` anywhere under `src/AcadMcp.Backend/` or under any `Acad.<Category>` project. The Backend has no AutoCAD assemblies in its bin folder.
- ❌ `new PluginPipeClient(...)` outside `Program.cs` and `Pipe/` infrastructure. Always inject `IPluginGateway`.
- ❌ Blocking the JSON-RPC dispatch thread with `.Result` or `.Wait()`. Always `await`.
- ❌ Holding the gateway's pipe stream open for streaming results inside a single tool call. Streaming uses MCP `progress` notifications, not raw pipe.
- ❌ Logging tool arguments at Information level when they could contain user file paths or coordinates that the user did not opt-in to share.

## Required patterns

- Tools that don't touch AutoCAD (pure compute, validators, schema lookups) take only their typed args + `CancellationToken`. They MUST NOT depend on `IPluginGateway`.
- Tools that touch AutoCAD declare `[McpTool(..., RequiresPlugin = true)]` and accept `IPluginGateway gateway` as the FIRST parameter (or as a constructor dep on a non-static catalog if we move to instance-style tools later).
- Errors from the plugin propagate up with `tie.InnerException` unwrapped — NEVER swallow `PluginToolException`.
- Every Backend process writes a one-line startup banner to stderr (`AcadMcp.Backend starting. Category=... Transport=... Mode=...`) and a one-line shutdown banner. No other stdout writes outside the JSON-RPC frames.

## Diagnostics

Backend exposes operator-only flags (NOT visible to MCP clients):

- `--ping-plugin` connect, run `_echo` + `acad_status`, exit. Used by smoke tests.
- `--regenerate-manifest` rewrite `mcpbank-manifests/acad-<category>.json` from `[McpTool]` metadata, exit.
- `--verbose` raise log level to `Debug`.

If `--ping-plugin` succeeds but real tool calls fail, suspect the plugin's UI thread dispatcher (rule 10) or a missing `[McpTool]` registration (rule 20).
