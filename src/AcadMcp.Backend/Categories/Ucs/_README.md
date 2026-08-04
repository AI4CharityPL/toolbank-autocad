# AutoCAD Coordinate Systems  (acad-ucs)

User Coordinate System management: create UCSs from points, objects, views or faces, rotate them about any axis, save and restore them by name. Every drawing tool in the bank works in WCS by default; a UCS is what makes rotated, inclined or sectioned work addressable. See docs/engineering-rules/43-coordinate-systems.md for the contract.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `UcsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `UcsTools.cs` (or split per concern: `UcsLines.cs`, `UcsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "ucs"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category ucs --regenerate-manifest`
