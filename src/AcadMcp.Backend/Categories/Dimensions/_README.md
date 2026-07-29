# AutoCAD Dimensions  (acad-dimensions)

AutoCAD parametric dimension entities: linear (rotated and aligned), angular (3-point and 2-line), radial, diametric, arc-length, ordinate, plus baseline and continued chains derived from a prior dimension. Includes dimension style (DimStyle) lookup and assignment. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `DimensionsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `DimensionsTools.cs` (or split per concern: `DimensionsLines.cs`, `DimensionsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "dimensions"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category dimensions --regenerate-manifest`
