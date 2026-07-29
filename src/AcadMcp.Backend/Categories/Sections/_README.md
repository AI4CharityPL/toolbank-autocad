# AutoCAD Section Lines  (acad-sections)

Section cut lines (A-A, B-B) with cut-plane markers, directional arrows and depth ranges. Links to layout tabs for each section view. Conforms to PN-EN-ISO-128 and PN-B-01025 section symbol conventions.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `SectionsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `SectionsTools.cs` (or split per concern: `SectionsLines.cs`, `SectionsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "sections"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category sections --regenerate-manifest`
