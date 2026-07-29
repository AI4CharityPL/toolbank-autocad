# Adding a new MCP category

How to add a new acad-<category> MCP server. Use scripts/new-category.ps1, never copy-paste.

A "category" = one MCP microserver = one folder under `src/AcadMcp.Backend/Categories/<Name>/` + one manifest under `mcpbank-manifests/acad-<name>.json` + one launcher under `bin-launchers/acad-<name>.cmd`.

The number of these will reach 30+. They MUST be created identically every time. Use `scripts/new-category.ps1`. Hand-rolled categories drift and break `check-manifests.ps1`.

## Mandatory flow

```powershell
pwsh scripts/new-category.ps1 -Name geometry-2d -DisplayName "AutoCAD 2D Geometry" -Description "Create and edit 2D geometry: lines, circles, arcs, polylines, splines."
```

What the script generates:

1. `src/AcadMcp.Backend/Categories/Geometry2d/` with:
   - `Geometry2dTools.cs` containing one stub `[McpTool]` so the source generator emits a non-empty catalog and the build stays green
   - `_README.md` listing the planned tools (TODO list)
2. `mcpbank-manifests/acad-geometry-2d.json` populated with:
   - `id = "acad-geometry-2d"`, `name`, `description`, `transport.command` pointing at the launcher
   - `lazy_mode = true`, `tags`, **placeholder** `intent_examples` (≥5, marked `// TODO replace`)
   - `tools_summary` regenerated from the stub
3. `bin-launchers/acad-geometry-2d.cmd` calling the Release Backend exe with `--category geometry-2d`
4. `tests/AcadMcp.Tests/Categories/Geometry2dTests.cs` smoke test (initialize → tools/list)

## What you must do AFTER the script

1. Replace placeholder `intent_examples` with real PL+EN phrases per `31-mcpbank-discovery-hygiene.md`.
2. Refine `tags` to match agent-search vocabulary.
3. Implement actual tools (delete the stub).
4. Re-run `dotnet AcadMcp.Backend --category <name> --regenerate-manifest` so `tools_summary` matches reality.
5. Add the category to the router's "known categories" allowlist if it should appear in `acad_recommend_categories`.

## What you MUST NOT do

- Never copy an existing category folder by hand. The script keeps things uniform - paths, namespaces, csproj wiring, manifest fields, test naming. Diverging from this is how we end up with broken `check-manifests.ps1`.
- Never give a category a name with `_`, capital letters, or `acad-` prefix in its folder name. Folder = `Geometry2d` (PascalCase). Manifest id = `acad-geometry-2d`. Routing key = `geometry-2d`. The script enforces this mapping.
- Never add a category without an MCPBank manifest. The router CANNOT discover it otherwise (per `00-architecture-invariants.md` invariant #4).
- Never edit `tools_summary` or `intent_examples` count entries by hand after tools exist - always use `--regenerate-manifest`.

## Naming map (single source of truth)

| Concept              | Format                                       | Example                            |
| -------------------- | -------------------------------------------- | ---------------------------------- |
| Folder name          | PascalCase, no spaces                        | `Geometry2d`                       |
| Namespace            | `AcadMcp.Backend.Categories.<Folder>`        | `AcadMcp.Backend.Categories.Geometry2d` |
| `[McpTool(Category)]`| `<name>` (matches launcher arg, kebab-case)  | `geometry-2d`                      |
| Launcher arg         | `--category <name>`                          | `--category geometry-2d`           |
| Manifest filename    | `mcpbank-manifests/acad-<name>.json`         | `mcpbank-manifests/acad-geometry-2d.json` |
| Manifest `id`        | `acad-<name>`                                | `acad-geometry-2d`                 |
| Test class           | `Categories/<Folder>Tests.cs`                | `Geometry2dTests.cs`               |

If any one of these is out of sync, `check-manifests.ps1` fails and the build does too (via `CheckManifestSync` MSBuild target).
