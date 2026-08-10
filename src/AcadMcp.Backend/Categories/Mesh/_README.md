# AutoCAD Meshes  (acad-mesh)

TODO: replace - one-paragraph description of the acad-mesh category. Lists what kinds of operations live here, which AutoCAD APIs they wrap, and any plugin/COM constraints.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `MeshTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `MeshTools.cs` (or split per concern: `MeshLines.cs`, `MeshCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "mesh"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category mesh --regenerate-manifest`
