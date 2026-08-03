# AutoCAD Paperspace Viewports  (acad-viewports)

Paperspace viewport control: creation (rectangular and polygonal), scale, lock, shade plot, and the per-viewport layer overrides that let one model serve an architectural plan, a fire plan and a furniture plan from the same geometry.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `ViewportsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `ViewportsTools.cs` (or split per concern: `ViewportsLines.cs`, `ViewportsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "viewports"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category viewports --regenerate-manifest`
