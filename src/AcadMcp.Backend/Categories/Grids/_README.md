# AutoCAD Structural Grids  (acad-grids)

Axis grids with bubble labels (alpha A..Z + numeric 1..N), axis spacings (e.g. 7200 mm ISO default), per-axis spacing, grid snapping and rename/remove operations. Every professional plan requires a structural grid per PN-B-01025.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `GridsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `GridsTools.cs` (or split per concern: `GridsLines.cs`, `GridsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "grids"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category grids --regenerate-manifest`
