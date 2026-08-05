# AutoCAD Parametric (`acad-parametric`)

High-level **geometric constraints**, **constraint cleanup**, **constraint
inventory**, and **dynamic block reference** property I/O. Implements rule
**42** (`docs/engineering-rules/42-parametric-domain-traps.md`).

## Tools (12)

| tool | purpose |
| ---- | ------- |
| `ensure_parametric_layers` | Idempotent 6-layer P-* key (rule 42 §9) |
| `apply_geom_horizontal` | `-GEOCONSTRAINT` Horizontal, one entity |
| `apply_geom_vertical` | `-GEOCONSTRAINT` Vertical, one entity |
| `apply_geom_parallel` | Parallel, two handles |
| `apply_geom_perpendicular` | Perpendicular, two handles |
| `apply_geom_coincident` | Coincident, two handles |
| `apply_geom_fix` | Fix anchor, one handle |
| `delete_entity_constraints` | `-DELCONSTRAINT` on one handle |
| `list_constraint_entities` | Model-space scan, class name contains `Constraint` |
| `get_dynamic_block_properties` | Read `DynamicBlockReferenceProperty` list |
| `set_dynamic_block_property` | Write one dynamic property (angles: JSON **degrees** → radians in plugin when units look angular) |
| `parametric_health` | Read-only metadata + angle policy string |

## Plugin primitives (`acad.parametric.*`)

The plugin closes its transaction before `Editor.Command` runs native
`-GEOCONSTRAINT` / `-DELCONSTRAINT` (AutoCAD owns command transactions).

## v1 limitations

See `toolbank-manifests/acad-parametric.json` → `metadata.v1_limitations`.

## Paired validators

- `validators/parametric/sketch-on-p-sketch.yaml`
- `validators/parametric/constrained-on-p-constrained.yaml`

Bundled as `validators/_standards/parametric-baseline.yaml`.

## Regenerate manifest

```powershell
dotnet run --project src/AcadMcp.Backend -- --category parametric --regenerate-manifest
```
