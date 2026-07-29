# AutoCAD Files  (acad-files)

AutoCAD drawing file lifecycle and conversion: open / save / save-as (with chosen DwgVersion) / close the active drawing, import DWG and DXF, export to DXF and downgraded DWG versions, plot the active layout to PDF or DWF via PlotEngine, render images, and run drawing maintenance (purge unused symbols, audit for corruption with optional fix). All operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `FilesTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `FilesTools.cs` (or split per concern: `FilesLines.cs`, `FilesCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "files"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category files --regenerate-manifest`
