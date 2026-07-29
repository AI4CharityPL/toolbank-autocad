# AutoCAD Annotations  (acad-annotations)

AutoCAD text and annotation entities: single-line DBText, multi-line MText with inline formatting, leaders and multi-leaders (MLeader) with text or block content, basic Tables built from row/column data, points with point styles, and text style management. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `AnnotationsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `AnnotationsTools.cs` (or split per concern: `AnnotationsLines.cs`, `AnnotationsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "annotations"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category annotations --regenerate-manifest`
