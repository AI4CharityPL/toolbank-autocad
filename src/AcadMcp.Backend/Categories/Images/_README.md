# AutoCAD Raster Images  (acad-images)

Attach, clip, adjust and detach raster image references (PNG/JPG/TIFF/BMP). RasterImage/RasterImageDef entities, not underlays (DGN/DWF/PDF) which are a separate mechanism. set_draworder in acad-geometry-2d already reorders any entity including images; there is no separate reorder tool here.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `ImagesTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `ImagesTools.cs` (or split per concern: `ImagesLines.cs`, `ImagesCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "images"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category images --regenerate-manifest`
