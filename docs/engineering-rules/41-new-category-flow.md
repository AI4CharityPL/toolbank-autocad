# Adding a new MCP category

How to add a new acad-<category> MCP server. Use scripts/new-category.ps1, never copy-paste.

A "category" = one MCP microserver = one folder under `src/AcadMcp.Backend/Categories/<Name>/` + one manifest under `toolbank-manifests/acad-<name>.json` + one launcher under `bin-launchers/acad-<name>.cmd`.

The number of these will reach 30+. They MUST be created identically every time. Use `scripts/new-category.ps1`. Hand-rolled categories drift and break `check-manifests.ps1`.

## Mandatory flow

```powershell
pwsh scripts/new-category.ps1 -Name geometry-2d -DisplayName "AutoCAD 2D Geometry" -Description "Create and edit 2D geometry: lines, circles, arcs, polylines, splines."
```

What the script generates:

1. `src/AcadMcp.Backend/Categories/Geometry2d/` with:
   - `Geometry2dTools.cs` containing one stub `[McpTool]` so the source generator emits a non-empty catalog and the build stays green
   - `_README.md` listing the planned tools (TODO list)
2. `toolbank-manifests/acad-geometry-2d.json` populated with:
   - `id = "acad-geometry-2d"`, `name`, `description`, `transport.command` pointing at the launcher
   - `lazy_mode = true`, `tags`, **placeholder** `intent_examples` (≥5, marked `// TODO replace`)
   - `tools_summary` regenerated from the stub
3. `bin-launchers/acad-geometry-2d.cmd` calling the Release Backend exe with `--category geometry-2d`
4. `tests/AcadMcp.Tests/Categories/Geometry2dTests.cs` smoke test (initialize → tools/list)

## What you must do AFTER the script

1. Replace placeholder `intent_examples` with real PL+EN phrases per `31-toolbank-discovery-hygiene.md`.
2. Refine `tags` to match agent-search vocabulary.
3. Implement actual tools (delete the stub).
4. Re-run `dotnet AcadMcp.Backend --category <name> --regenerate-manifest` so `tools_summary` matches reality.
5. Add the category to the router's "known categories" allowlist if it should appear in `acad_recommend_categories`.

## What you MUST NOT do

- Never copy an existing category folder by hand. The script keeps things uniform - paths, namespaces, csproj wiring, manifest fields, test naming. Diverging from this is how we end up with broken `check-manifests.ps1`.
- Never give a category a name with `_`, capital letters, or `acad-` prefix in its folder name. Folder = `Geometry2d` (PascalCase). Manifest id = `acad-geometry-2d`. Routing key = `geometry-2d`. The script enforces this mapping.
- Never add a category without a ToolBank manifest. The router CANNOT discover it otherwise (per `00-architecture-invariants.md` invariant #4).
- Never edit `tools_summary` or `intent_examples` count entries by hand after tools exist - always use `--regenerate-manifest`.

## Naming map (single source of truth)

| Concept              | Format                                       | Example                            |
| -------------------- | -------------------------------------------- | ---------------------------------- |
| Folder name          | PascalCase, no spaces                        | `Geometry2d`                       |
| Namespace            | `AcadMcp.Backend.Categories.<Folder>`        | `AcadMcp.Backend.Categories.Geometry2d` |
| `[McpTool(Category)]`| `<name>` (matches launcher arg, kebab-case)  | `geometry-2d`                      |
| Launcher arg         | `--category <name>`                          | `--category geometry-2d`           |
| Manifest filename    | `toolbank-manifests/acad-<name>.json`         | `toolbank-manifests/acad-geometry-2d.json` |
| Manifest `id`        | `acad-<name>`                                | `acad-geometry-2d`                 |
| Test class           | `Categories/<Folder>Tests.cs`                | `Geometry2dTests.cs`               |

If any one of these is out of sync, `check-manifests.ps1` fails and the build does too (via `CheckManifestSync` MSBuild target).


## The measured recipe — one AutoCAD restart per category

Written from the numbers of 2026-08-10, where the same person built four categories two ways.
The scarce resource is **AutoCAD restart cycles**, not tokens: every deploy needs the user to
restart AutoCAD, so a category costing four restarts costs four interruptions.

| category | order of work | restarts | result |
| --- | --- | ---: | --- |
| `acad-lisp` | built first, probed after | 3 | 5 of 12 shipped, six withdrawn |
| `acad-sections-3d` | probed, but assumptions unchecked | 3 | 9 shipped, 3 defects found live |
| `acad-data` ×3 tranches | probed first, every time | 1 each | 13, 5 and 5 shipped, first-run clean |

**The order that works:**

1. **Probe the whole category against the compiler BEFORE writing any tool.** Probe builds need
   no AutoCAD, so they are free of restarts. Guess names generously and in bulk; one round is not
   evidence of absence (§12c). Read `CS0219 assigned but never used` as *the name exists*.
2. **Write the entire category in one pass** — plugin, DTOs, backend, tests — not tranche by
   tranche. The compiler catches the shape errors, and it costs no restarts.
3. **Write the verification with its CONTROLS before deploying**, and pick test shapes that can
   fail: an asymmetric grid, a sphere cut off-centre, a filter that must match nothing. A cube
   cannot tell a cut from a silhouette; a symmetric grid cannot tell a transposition.
4. **Deploy once. Verify once.** Fold the docs, roadmap, changelog and commit into the same turn
   as the passing run.

**What burns a cycle every time:** varying arguments against a failing API instead of shrinking
the experiment. When several tools that share no code fail with the same status, stop and write a
five-line `[CommandMethod]` (rule 26 §15) — it answered in one round what two rebuilds could not.
