# AutoCAD Architecture (plan-view)  (`acad-architecture`)

High-level architectural plan-view domain category. Composes primitives from
`acad-geometry-2d`, `acad-layers`, `acad-annotations`, `acad-dimensions` —
the agent never has to remember which layer or which linetype. Pairs with
`acad-validators` rules under `validators/architectural/`.

> Read **rule 35-domain-categories-design.mdc** and **rule 36-architecture-domain-traps.mdc** before changing anything in this folder.

## Tools (v1, 10)

| tool                          | what it produces                                    |
| ----------------------------- | --------------------------------------------------- |
| `ensure_architectural_layers` | AIA layer key (16 layers, idempotent)               |
| `draw_wall`                   | centreline + 2 face lines                           |
| `draw_walls_chain`            | centreline polyline + 2 mitred face polylines       |
| `insert_door`                 | door panel + swing arc (no wall cut yet)            |
| `insert_window`               | sill + glass + header + 2 jambs (no wall cut yet)   |
| `insert_rect_column`          | rectangular column on S-COLS + crosshair on -CTRL   |
| `insert_round_column`         | circular column on S-COLS + crosshair on -CTRL      |
| `define_room`                 | closed boundary + 3 text labels (number, name, m²)  |
| `dimension_wall`              | linear-or-aligned dimension picked per wall angle   |
| `architecture_health`         | read-only metadata (layer key + planned blocks)     |

## Layer key (canonical, see rule 36 §11)

| layer            | colour | linetype     |
| ---------------- | ------ | ------------ |
| `A-WALL`         | 7      | Continuous   |
| `A-WALL-CTRL`    | 8      | CENTER       |
| `A-DOOR`         | 30     | Continuous   |
| `A-DOOR-SWING`   | 30     | DASHED       |
| `A-GLAZ`         | 4      | Continuous   |
| `A-ROOM-BNDY`    | 8      | DASHED       |
| `A-ROOM-IDEN`    | 7      | Continuous   |
| `A-CLNG`         | 5      | Continuous   |
| `A-ROOF`         | 5      | Continuous   |
| `A-STRS`         | 6      | Continuous   |
| `A-ANNO-DIMS`    | 2      | Continuous   |
| `A-ANNO-NOTE`    | 2      | Continuous   |
| `S-COLS`         | 1      | Continuous   |
| `S-COLS-CTRL`    | 8      | CENTER       |
| `S-SLAB`         | 7      | Continuous   |
| `S-SLAB-HATCH`   | 8      | Continuous   |

The constant strings live in `ArchitecturePalette.cs` — single source of truth.

## v1 limitations (deliberate, called out in tool descriptions)

1. `insert_door` / `insert_window` do **not** cut the host wall opening yet —
   that ships in Phase 7 along with `connect_walls` and the bundled DWG block
   library under `blocks/architectural/`.
2. `draw_walls_chain` mitres at vertices but doesn't auto-clean wall T-junctions
   between two independently-drawn chains.
3. Text in `define_room` is plain `DBText` stacked at the centroid; the bundled
   `ROOM_TAG.dwg` block reference variant comes with the block library.

## How to regenerate the manifest from code

```powershell
dotnet build src/AcadMcp.Backend/AcadMcp.Backend.csproj -c Release
src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe --category architecture --regenerate-manifest
```

## Paired validators

- `validators/architectural/walls-on-walls-layer.yaml`
- `validators/architectural/wall-centerlines-on-a-wall-ctrl.yaml`
- `validators/architectural/columns-on-s-cols-layer.yaml`
- `validators/architectural/walls-min-length.yaml`
- `validators/architectural/door-blocks-have-room-tag.yaml`
- `validators/architectural/titleblock-must-exist.yaml`

Bundled in the `polish-arch-baseline` standard.
