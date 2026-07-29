# AutoCAD Plot Styles (CTB/STB)  (acad-plotstyles)

Manage CTB (color-dependent) and STB (named) plot-style tables. Apply 9-tier lineweight policy (0.05 mm hatches -> 1.4 mm outer building outline), per-layer plot color, screen percentage and plot flag. Ships AIA-2017 and PN-B-01025 presets.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `PlotstylesTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `PlotstylesTools.cs` (or split per concern: `PlotstylesLines.cs`, `PlotstylesCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "plotstyles"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category plotstyles --regenerate-manifest`
