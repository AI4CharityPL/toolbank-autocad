# AutoCAD View Control  (acad-view)

Control model-space view: zoom window/extents/all, set current view by name, list named views. Used as a pre-step for acad.files.export_file scope=Display and for AI-driven visual inspection loops.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `ViewTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `ViewTools.cs` (or split per concern: `ViewLines.cs`, `ViewCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "view"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category view --regenerate-manifest`
