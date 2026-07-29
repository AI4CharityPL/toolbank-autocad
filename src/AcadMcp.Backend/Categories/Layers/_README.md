# AutoCAD Layers  (acad-layers)

AutoCAD layer (LayerTableRecord) management: create, rename, delete and list layers; query and set per-layer state (color by ACI or RGB, linetype, lineweight, plot style, plottable, frozen, locked, on/off, transparency); pick the current layer; bulk move entities between layers; and basic layer-state save / restore via AutoCAD's layer state manager. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `LayersTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `LayersTools.cs` (or split per concern: `LayersLines.cs`, `LayersCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "layers"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category layers --regenerate-manifest`
