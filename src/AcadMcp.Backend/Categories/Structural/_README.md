# AutoCAD Structural  (acad-structural)

Steel column/beam profiles from a real EN 10365 catalog subset, and span-based lintel sizing with an explicit engineering-heuristic disclaimer.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `StructuralTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `StructuralTools.cs` (or split per concern: `StructuralLines.cs`, `StructuralCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "structural"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category structural --regenerate-manifest`
