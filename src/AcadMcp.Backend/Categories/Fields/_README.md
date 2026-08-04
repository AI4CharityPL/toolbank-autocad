# AutoCAD Fields  (acad-fields)

Dynamic text fields: date, filename, layout/sheet name, object properties, computed areas, system variables and formulas. A title block or schedule built from fields stays correct when the drawing changes; one built from plain text is wrong the moment anything moves.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `FieldsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `FieldsTools.cs` (or split per concern: `FieldsLines.cs`, `FieldsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "fields"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category fields --regenerate-manifest`
