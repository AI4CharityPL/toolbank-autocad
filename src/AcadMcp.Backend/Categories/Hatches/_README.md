# AutoCAD Hatches  (acad-hatches)

Draw, manage and regenerate hatches (material fills) on architectural and engineering drawings per ISO 128 and PN-EN patterns. Covers boundary-based hatching, pattern presets (concrete/brick/insulation/plaster/stone), material-to-layer mapping, and regeneration after boundary edits.

## Tools

| # | tool                                 | summary                                                    | read-only | notes |
|---|--------------------------------------|------------------------------------------------------------|-----------|-------|
| 1 | `draw_hatch`                         | Boundary-handle hatch, full property control (pattern / scale / angle / color / bg / associative / annotative). | no  | |
| 2 | `draw_hatch_by_boundary`             | Seed-point auto-boundary hatch using `Editor.TraceBoundary`. | no | persists temp boundaries on `A-BNDRY-TEMP` |
| 3 | `list_patterns`                      | Enumerate built-in ANSI / ISO / AR / material / solid patterns with default scale/angle. | **yes** | |
| 4 | `apply_material_preset`              | Apply a named material (concrete, brick, insulation, plaster, stone, steel, glass, wood-cross/grain, parquet, tile, lead-shield, faraday, earth, sand, cork, gravel, grass) to boundary handles. | no | see rule 62 |
| 5 | `apply_material_preset_by_point`     | Same as #4 but seed-point based.                            | no | |
| 6 | `clip_hatch`                         | Replace the boundary of an existing hatch.                 | no | |
| 7 | `regenerate_hatches`                 | Re-evaluate hatches by handle list, layer filter, or allInModelSpace after wall edits. | no | |
| 8 | `list_hatches`                       | Enumerate all hatches in model space, optionally filtered by layer / pattern. | **yes** | |

## Conventions

- All tools live in `HatchesTools.cs` (backend proxy) and `HatchesPluginTools.cs` (plugin side).
- `Category = "hatches"` on every `[McpTool]`; the source generator validates this matches the folder.
- Material -> (pattern, scale, angle, color) mapping is pinned by rule **62-hatching-policy.md**. To add a new material edit the rule AND `HatchesPluginTools.s_materialPresets` in the same commit.
- Drawing unit assumption: **millimeters**. Preset scales are baked in accordingly.

## Typical flows

### Hatch a single wall polyline with brick preset

```json
{
  "tool": "apply_material_preset",
  "args": {
    "boundaryHandles": ["1A4"],
    "material": "brick",
    "layer": "A-WALL-EXT-HATCH"
  }
}
```

### Auto-hatch an entire room from a seed point

```json
{
  "tool": "apply_material_preset_by_point",
  "args": {
    "seedPoint": { "x": 12500, "y": 17300 },
    "material": "tile",
    "layer": "A-FLOR-HATCH",
    "detectIslands": true
  }
}
```

### Regenerate all hatches after a bulk wall edit

```json
{ "tool": "regenerate_hatches", "args": { "allInModelSpace": true } }
```

## Regenerate manifest after editing tools

```powershell
dotnet run --project src/AcadMcp.Backend -- --category hatches --regenerate-manifest
```
