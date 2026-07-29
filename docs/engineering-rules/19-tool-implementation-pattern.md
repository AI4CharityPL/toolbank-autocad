# Backend ↔ Plugin tool implementation pattern

Backend↔Plugin tool split. Backend declares typed contract+gateway proxy. Plugin owns AutoCAD work.

Every domain tool exists in TWO places. Don't fight the split — it's what lets us scale to 1000+ tools, swap the plugin transport, mock in tests, and survive AutoCAD crashes without losing the MCP host.

## Backend side (`src/AcadMcp.Backend/Categories/<Folder>/...Tools.cs`)

- Pure declaration. NO `Autodesk.AutoCAD.*` imports — the assembly isn't even loaded here.
- Each tool is a `static` method on a `public static class` named `<Folder>Tools`.
- Decorated with `[McpTool(name, description, category, ...)]`.
- Signature: `(IPluginGateway gw, <TypedArgsRecord> args, CancellationToken ct) -> Task<TypedResultRecord>`.
- The body is ~5 lines: serialize args → `gw.InvokeAsync("acad.<cat>.<verb>", args, timeout, ct)` → deserialize result.
- Use the helper `Geometry2dProxy.CallAsync<TArgs,TResult>(gw, "acad.geometry2d.draw_line", args, timeout, ct)` (or per-category equivalent) to keep boilerplate to one line.
- Group DTOs in a sibling `<Folder>Dtos.cs` file. Argument records use `init` properties + `[JsonPropertyName]`.
- `RequiresPlugin = true` on every tool that calls the gateway. `ReadOnly = true` for queries that don't mutate the database.
- Default per-call timeouts: read-only 5 000 ms, mutating 30 000 ms, batch ops 60 000 ms. Document the choice in the C# `// timeout: ...` comment.

## Plugin side (`src/AcadMcp.Plugin/Tools/<Cat>PluginTools.cs`)

- Where the real AutoCAD code lives.
- `internal static class <Cat>PluginTools { public static void Register(ToolHost host) { ... } }`.
- Plugin-side tool keys are dotted, lowercase: `acad.geometry2d.draw_line`. ONE-to-ONE with backend tool names.
- Wrap EVERY handler body in `await UiThreadDispatcher.Run(() => { ... }, ct)` (rule 10).
- Wrap every database write in `using var docLock = doc.LockDocument()` + `using var tr = doc.TransactionManager.StartTransaction()`. Always `tr.Commit()` on success (rule 11).
- On exceptions: catch `Autodesk.AutoCAD.Runtime.Exception` separately, map to typed `AcadErrorCode` via the helper `AcadErrorMapper` (rule 12).
- Layer creation MUST go through `AcadEnv.EnsureLayer(tr, db, layerName)` so we don't crash on missing layers.
- All entity returns expose the AutoCAD `Handle.ToString()` as `EntityHandle.Handle`. Never return raw `ObjectId.Handle` integers.
- Register tools from `PluginEntryPoint.Initialize()` AFTER `BuiltinTools.Register(host)` and BEFORE `_pipeServer.Start()`.

## Naming map (mandatory)

| Backend tool name (rule 21) | Plugin tool key                  | Example handle in registration  |
|------------------------------|----------------------------------|---------------------------------|
| `draw_line`                  | `acad.geometry2d.draw_line`      | `host.Register("acad.geometry2d.draw_line", LineTools.DrawAsync)` |
| `draw_circle`                | `acad.geometry2d.draw_circle`    |                                 |
| `get_entity`                 | `acad.geometry2d.get_entity`     |                                 |

Why dotted on plugin side: plugin keys are not user-facing, but they are emitted in error messages and logs; the dotted form makes routing/grep trivially correct ("which category owns this tool?").

## Forbidden

- ❌ Tool method that does ANY AutoCAD work in the Backend assembly (it would `TypeLoadException` at runtime).
- ❌ Returning raw `ObjectId` over the wire — always return `EntityHandle` (handle string + class + layer).
- ❌ Calling `Application.DocumentManager.MdiActiveDocument` from a worker thread (rule 10).
- ❌ Catching `Exception` and rethrowing without mapping to `AcadErrorCode` (rule 12).

## Check list before adding a new tool

1. Backend: typed args record + result record in `<Folder>Dtos.cs`.
2. Backend: `[McpTool]` method that proxies via `gateway.InvokeAsync`.
3. Plugin: real handler registered under matching `acad.<cat>.<verb>` key.
4. Plugin: handler is fully `UiThreadDispatcher.Run`-wrapped.
5. Run `dotnet AcadMcp.Backend --category <cat> --regenerate-manifest` and commit the manifest delta.
6. Run `dotnet build` — `CheckManifestSync` must stay green.
7. Add a smoke test in `tests/AcadMcp.Tests/Categories/<Folder>Tests.cs` if the tool is non-trivial.
