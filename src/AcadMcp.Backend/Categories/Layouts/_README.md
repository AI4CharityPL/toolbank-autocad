# AutoCAD Layouts  (acad-layouts)

AutoCAD paper-space layout management: create, list, rename, delete and switch the current layout; create and configure floating Viewport entities (size, center, scale, layer, on/off, locked, frozen layers) on a layout; configure plot settings (page size, plotter, orientation, plot area). All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `LayoutsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `LayoutsTools.cs` (or split per concern: `LayoutsLines.cs`, `LayoutsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "layouts"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category layouts --regenerate-manifest`
