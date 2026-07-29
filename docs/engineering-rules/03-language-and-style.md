# Code style

Language conventions, code style, naming. C# and Python.

## Languages

- **Code identifiers, types, methods, files, folders, branches:** English. Always.
- **Comments and doc-strings explaining DOMAIN concepts** (Polish CAD norms, building code, BIM nomenclature): Polish OK and often clearer.
- **Comments explaining CODE mechanics:** English.
- **`[McpTool] Description` field:** English (LLM-facing).
- **`[McpTool] Intent` examples:** ALWAYS BOTH PL and EN (5+ each minimum).
- **MCPBank manifest `description_pl` and `description_en`:** both required.
- **README/CHANGELOG/docs/:** English with Polish allowed where it clarifies CAD norm references.

## C# style

- `.NET 8` (Backend, Tests, ComBridge). `.NET Framework 4.8` (Plugin for AutoCAD 2020-2024) or `net8.0-windows` (Plugin for AutoCAD 2025+).
- File-scoped namespaces: `namespace AcadMcp.Backend.Categories.Geometry2D;`
- Primary constructors where readable
- `record` for DTOs and immutable value types
- `sealed class` by default unless inheritance is intentional
- `using var` for `IDisposable`
- Nullable reference types: **enabled** in every project (`<Nullable>enable</Nullable>`)
- LangVersion: `latest`
- Async suffix: `XxxAsync` for async methods, return `Task` or `ValueTask`
- No `async void` except event handlers
- Cancellation: every async public method takes `CancellationToken ct = default`
- Logging: `ILogger<T>` from `Microsoft.Extensions.Logging`, never `Console.WriteLine` outside `Program.cs`
- JSON: `System.Text.Json` only. No `Newtonsoft`.

## C# naming

- Types/methods/properties: `PascalCase`
- Locals/parameters: `camelCase`
- Private fields: `_camelCase`
- Constants: `PascalCase` (not `ALL_CAPS`)
- Interface: `ILayerService` (with `I` prefix)
- DTO records suffix: `Args` for tool inputs, `Result` for tool outputs (e.g. `DrawCircleArgs`, `DrawCircleResult`)

## File layout

- One public type per file (record DTOs in same file as the tool that uses them is OK if local-only)
- Filename matches main type name
- `Categories/Geometry2D/LineTools.cs` is a static class with multiple `[McpTool]` methods grouped by entity type
- Pure helpers (no `[McpTool]`) live in `Categories/<X>/_Helpers/`

## Python style (Vision sidecar)

- Python 3.11+
- Formatter: `ruff format` (replaces black)
- Linter: `ruff check` with rules: `E,F,I,N,UP,B,C4,SIM,PTH`
- Type hints **mandatory** on public functions
- `pyproject.toml` for config, no `setup.py`
- Async: `asyncio` + `grpc.aio`
- Logging: `structlog`
- No `print()` except `__main__` startup banner

## Forbidden everywhere

- `dynamic` in C# unless interop with COM (then quarantined to `ComBridge`)
- `eval`, `exec` in Python
- Magic numbers: extract to named constants with units in name (`MinLineWeightMm = 0.13`)
- Catching `Exception` without rethrow or `AcadErrorCode` mapping
- `TODO` without a tracking todo id (use format `// TODO(phase4): ...`)
