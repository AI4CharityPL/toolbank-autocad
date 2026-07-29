# Plugin lifecycle

Lifecycle of the AutoCAD .NET plugin AcadMcp.Plugin - load, init, named pipe, shutdown.

`AcadMcp.Plugin` is the SINGLE binary `NETLOAD`-ed inside AutoCAD. It owns the named pipe server, the UI thread dispatcher, and all access to AutoCAD APIs. Backend MCP processes connect to it as clients. There is exactly one of these per AutoCAD instance.

## Hard sequence on load

1. AutoCAD calls `IExtensionApplication.Initialize()`.
2. Plugin captures the AutoCAD UI thread `SynchronizationContext` into `UiThreadDispatcher.Capture()`.
3. Plugin starts `NamedPipeServer` on `PipeProtocol.PipeName` (`acadmcp` by default; override via `ACADMCP_PIPE` env var BEFORE NETLOAD).
4. Server logs "AcadMcp plugin online" to AutoCAD command line + writes a heartbeat file at `%LOCALAPPDATA%\AcadMcp\plugin.alive` so detect scripts can prove liveness without IPC.
5. Server accepts connections in a background thread loop. Per-connection request handlers run on background threads but ALL AutoCAD API calls go through `UiThreadDispatcher` (rule 10).

## Hard sequence on Terminate / unload

1. AutoCAD calls `IExtensionApplication.Terminate()` (called on AutoCAD exit OR explicit `NETUNLOAD`).
2. Stop accepting new pipe connections.
3. Cancel in-flight handlers via shared `CancellationTokenSource`. Wait up to 5 s for graceful drain.
4. Dispose the pipe stream and the listener thread.
5. Delete the heartbeat file. Log "AcadMcp plugin offline".
6. Never throw out of `Terminate` - swallow + log; an exception here can crash AutoCAD on exit.

## Hard rules

- The plugin MUST be safe to NETLOAD twice. Re-init is detected by checking `_initialized` and returning early.
- The plugin MUST NOT show any modal dialog, MessageBox, or AcadApplication popup at any point. Failures go to the command-line log + structured pipe error response.
- The plugin currently targets ONLY `net8.0-windows` (AutoCAD 2025+). Multi-targeting `net48` for AutoCAD 2020-2024 is deferred until a parallel test install of AutoCAD 2024 is available; the user-facing impact is "older AutoCAD versions need to wait for the legacy build phase". Do NOT re-introduce `net48` to `AcadMcp.Plugin.csproj` without also providing `$(AcadInstallPath2024)` MSBuild prop and corresponding HintPath. Shared code (DTOs in `AcadMcp.Shared`) DOES multi-target `net48;net8.0` so a future net48 plugin can reference it without source changes.
- The plugin MUST NOT load `AcadMcp.Backend.dll` or any backend code. The backend talks to it ONLY via the pipe. (Architectural invariant #2.)
- The plugin MUST tolerate the pipe client disconnecting mid-request. Cancel any in-flight UI thread work on disconnect.
- The plugin MUST register a `CommandMethod("ACADMCP_STATUS")` that prints pipe state, request count since load, last error - so the user can verify health from the AutoCAD command line.

## Forbidden in the plugin

- `Console.WriteLine` (no console attached).
- Synchronous waits on the UI thread that block the dispatcher.
- Anywhere holding a reference to `Document` or `Database` outside a single dispatched lambda - they can be invalidated between calls.
- Reading from app config / appsettings - the plugin runs hosted in AutoCAD, not as a .NET host.

## Logging

- Use `Editor.WriteMessage($"\nAcadMcp: {msg}\n")` for command-line output (only what the user should see).
- Use `System.Diagnostics.Trace` or write to `%LOCALAPPDATA%\AcadMcp\logs\plugin-yyyymmdd.log` for verbose diagnostics. Rolling: 7 days, then prune.
- Never write secrets or full request bodies to logs.
