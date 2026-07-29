# AutoCAD Mechanical (drafting) — `acad-mechanical`

High-level mechanical drafting tools that turn `"draw a tapped hole at (40, 40),
M10"` into the right combination of primitives (full-circle outer profile, 3/4
arc inner thread, centreline crosshair) on the right layers (`ME-VISIBLE`,
`ME-THREAD`, `ME-CENTER`) with the right linetypes (`Continuous`, `HIDDEN`,
`CENTER`) and the right lineweights (0.50 / 0.25 / 0.18 mm).

Read **rule 35** (universal domain-category contract) and **rule 37**
(mechanical-domain traps) BEFORE you change anything in this folder.

## Tools (12)

| tool                       | what it does                                                                                                                  |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `ensure_mechanical_layers` | Idempotently create the ISO-mechanical 11-layer key (rule 37 §9) with full colour + linetype + lineweight metadata            |
| `draw_visible_edge`        | Continuous line on `ME-VISIBLE`                                                                                               |
| `draw_hidden_edge`         | HIDDEN line on `ME-HIDDEN` (rule 37 §1)                                                                                       |
| `draw_centerline`          | CENTER line on `ME-CENTER`                                                                                                    |
| `draw_centerline_cross`    | Two perpendicular CENTER lines extending past a round feature by `extensionMm` (rule 37 §2)                                   |
| `draw_section_cut_line`    | Thick PHANTOM polyline on `ME-SECTION` + two arrow-head triangles + two labels on `ME-TEXT` (rule 37 §3)                      |
| `draw_through_hole`        | Profile circle + centreline crosshair on the round-feature axes (rule 37 §4)                                                  |
| `draw_counterbore_hole`    | Outer counterbore + inner through circle + crosshair                                                                          |
| `draw_threaded_hole`       | Major-Ø Continuous circle + minor-Ø **3/4 arc** on `ME-THREAD` (HIDDEN) + crosshair (rule 37 §4a)                             |
| `draw_bolt_head_top_view`  | Regular hexagon flat-to-flat + optional shank circle + crosshair (rule 37 §5)                                                 |
| `draw_revision_triangle`   | Closed triangle polyline + SOLID hatch + centred DBText on `ME-REV` (rule 37 §6)                                              |
| `mechanical_health`        | ReadOnly diagnostic: layer key + ISO 128-50 material→hatch table + planned bundled-block list                                 |

## Layer key (ISO mechanical, 11 layers — rule 37 §9)

| layer            | colour | linetype     | weight | content                           |
| ---------------- | ------ | ------------ | ------ | --------------------------------- |
| `ME-VISIBLE`     | 7      | Continuous   | 0.50   | visible feature edges             |
| `ME-HIDDEN`      | 8      | HIDDEN       | 0.25   | hidden edges                      |
| `ME-CENTER`      | 4      | CENTER       | 0.18   | centrelines, axes                 |
| `ME-DIMS`        | 2      | Continuous   | 0.18   | dimensions                        |
| `ME-TEXT`        | 2      | Continuous   | 0.18   | notes, labels                     |
| `ME-SECTION`     | 1      | PHANTOM      | 0.70   | section cutting plane             |
| `ME-HATCH`       | 8      | Continuous   | 0.18   | section hatching                  |
| `ME-THREAD`      | 8      | HIDDEN       | 0.25   | thread minor-Ø arcs               |
| `ME-CONSTRUCTION`| 9      | Continuous   | 0.13   | construction (non-plottable)      |
| `ME-TITLE`       | 7      | Continuous   | 0.50   | title block geometry              |
| `ME-REV`         | 1      | Continuous   | 0.50   | revision triangles + tags         |

The single source of truth is `MechanicalPalette.cs`. Mirror **rule 37 §9** if
you change either side.

## Material → hatch lookup (ISO 128-50 — rule 37 §8)

`MechanicalPatterns.ByMaterial` maps `"steel"`, `"cast_iron"`, `"aluminium"`,
`"bronze"`, `"glass"`, `"concrete"`, `"soil"`, etc. to the correct
`(pattern, scale, angle)` triplet. Agents call `mechanical_health` to discover
the table, then pass the chosen pattern to `acad-geometry2d.draw_hatch`.

## Conventions

- All tools live in `MechanicalTools.cs`. DTOs in `MechanicalDtos.cs`. Layer
  / pattern catalogues in `MechanicalPalette.cs`. IPC composition in
  `MechanicalProxy.cs`.
- Every tool calls `MechanicalProxy.EnsureLayerAsync` BEFORE it draws — the
  ISO layer key must always exist before the first stroke (rule 35 §3).
- `Category = "mechanical"` on every `[McpTool]`; the source generator
  validates this matches the folder.

## v1 limitations (also in the manifest `metadata.v1_limitations`)

1. **Side-view holes** (counterbore depth, blind-hole drill point,
   countersink flare lines) ship in **Phase 7** along with the bundled DWG
   library.
2. `draw_section_cut_line` emits the cutting plane + arrow heads + labels but
   **NOT the sectioned hatch**. Call `acad-geometry2d.draw_hatch` separately
   with a pattern obtained from `mechanical_health.materials`.
3. **Bundled blocks** under `blocks/mechanical/` (BOLT_HEX_M*, WASHER_FLAT_M*,
   BEARING_RADIAL_*, SURFACE_FINISH_BASIC, WELD_SYMBOL_BASIC) ship in
   **Phase 7**; v1 tools synthesise geometry inline.
4. The `mech.threads.minor-arc-not-full-circle` paired validator waits on the
   validator engine growing an `entity_class_equals` check primitive. Until
   then `draw_threaded_hole` itself enforces the 3/4-arc convention by always
   producing an `Arc`, never a `Circle`.

## Paired validators

Under `validators/mechanical/`:

- `mech.hidden.must-be-dashed`           (HIDDEN linetype on hidden geometry)
- `mech.hidden.on-me-hidden-layer`       (rule 37 §1, layer placement)
- `mech.centerlines.must-be-dashed`      (CENTER linetype on centrelines)
- `mech.centerlines.on-me-center-layer`  (rule 37 §1, layer placement)

Bundled into the standard `iso-mechanical-baseline.yaml`.

## How to regenerate the manifest from code

```powershell
dotnet build src/AcadMcp.Backend -c Release
src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe --category mechanical --regenerate-manifest
```
