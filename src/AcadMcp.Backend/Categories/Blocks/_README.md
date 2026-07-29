# AutoCAD Blocks  (acad-blocks)

AutoCAD block (BlockTableRecord) authoring and instancing: define a block from existing entities, list / inspect / rename / purge block definitions, insert BlockReference instances with explicit attribute values, list and update attributes on existing references, explode references back to entities, and import block definitions across DWG files via WblockCloneObjects. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `BlocksTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `BlocksTools.cs` (or split per concern: `BlocksLines.cs`, `BlocksCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "blocks"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category blocks --regenerate-manifest`
