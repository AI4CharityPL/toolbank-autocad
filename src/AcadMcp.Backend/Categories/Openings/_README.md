# AutoCAD Openings — Doors + Windows  (`acad-openings`)

Professional-grade doors and windows with fire (REI), burglary (RC),
acoustic and lead-shield ratings, automatic numbering (`D-001` / `W-001`),
wall-cutting, quick-sketch alternatives and CSV/JSON schedule export.
Replaces line+arc door hacks with atomic BlockReference + attribute calls.

Phase D **D5** deliverable. See `docs/engineering-rules/65-door-window-schedule.md`
for the full attribute contract, layer split, numbering semantics and
performance targets. Read it before adding new door / window families.

## Tools (10)

| Tool                       | Purpose |
|----------------------------|---------|
| `list_opening_catalog`     | Read-only enumeration of the 11 door + window families with capability flags (fire / burglary / lead shield). |
| `insert_door`              | Insert a sized door block. Types: `single`, `double`, `sliding`, `fire`, `hospital`, `lead`. Auto-numbered `D-001`. REI, acoustic dB, lead-shield, leaf/swing direction, room codes, layer override. |
| `insert_window`            | Insert a sized window block. Types: `fixed`, `casement`, `tilt`, `hospital`, `fire`. Auto-numbered `W-001`. RC (PN-EN 1627), fire class, sill height, layer override. |
| `insert_opening_generic`   | Escape-hatch: insert any `DOOR-*` / `WIN-*` block by its canonical name with an explicit attribute map. |
| `draw_door_by_points`      | Quick-sketch: draw a line (leaf) + 90° arc (swing) between hinge and leaf-end. No block, no attributes — concept studies only. |
| `draw_window_by_points`    | Quick-sketch: two parallel lines + centre glass line between two jamb points. |
| `cut_wall_for_opening`     | Split a wall (Line / 2-vertex Polyline) into two segments with a gap between projected jamb points. Returns new wall handles + gap length. |
| `renumber_openings`        | Rewrite `NUMBER` attribute across doors + windows in `insertion` or `spatial` (Y↓ X→) order; overridable prefixes & pad width. |
| `list_openings_in_model`   | Read-only enumeration of every `DOOR-*` / `WIN-*` BlockReference with full attribute decoding and kind filter. |
| `export_schedule`          | Read-only CSV or JSON schedule (18 columns). Optional write-to-disk. Sorted by NUMBER. |

## Block families

All blocks are **generated at runtime on first use** — the library ships
zero `.dwg` assets. Sized naming: `<FAMILY>-<W>-<H>` (mm).

| Family         | Kind   | Default W×H | Fire | Burglary | Lead |
|----------------|--------|------------:|:----:|:--------:|:----:|
| `DOOR-SINGLE`  | door   |   900×2100  |      |          |      |
| `DOOR-DOUBLE`  | door   |  1600×2100  |      |          |      |
| `DOOR-SLIDING` | door   |  1000×2100  |      |          |      |
| `DOOR-FIRE`    | door   |  1200×2100  |  ✓   |          |      |
| `DOOR-HOSP`    | door   |  1800×2100  |  ✓   |          |      |
| `DOOR-LEAD`    | door   |  1200×2100  |      |          |  ✓   |
| `WIN-FIXED`    | window |  1200×1500  |      |    ✓     |      |
| `WIN-CASE`     | window |  1200×1500  |      |    ✓     |      |
| `WIN-TILT`     | window |  1500×1500  |      |    ✓     |      |
| `WIN-HOSP`     | window |  1800×1500  |  ✓   |    ✓     |      |
| `WIN-FIRE`     | window |  1500×1500  |  ✓   |    ✓     |      |

## Attribute contract (every BlockReference carries all 14)

```
NUMBER (visible)  TYPE  WIDTH_MM  HEIGHT_MM
REI  RC  FIRE_CLASS  LEAF_DIR  SWING_DIR
SILL_MM  ACOUSTIC_DB  LEAD  ROOM_FROM  ROOM_TO
```

Only `NUMBER` is visible. See rule 65 §4 for the full semantics and §5 for
numbering rules.

## Layer split

- `A-DOOR`, `A-DOOR-FIRE`, `A-DOOR-HOSP`, `A-DOOR-LEAD`
- `A-GLAZ`, `A-GLAZ-FIRE`

Default layer is inferred from the block family; callers can override.

## Typical usage flow

```text
1. cut_wall_for_opening(wallHandle, jamb1, jamb2)        ; carve the hole
2. insert_door/window at midpoint of (jamb1, jamb2)       ; auto-numbered
3. [draw furniture / plumbing / hatches around it]
4. renumber_openings(order='spatial') at end of session
5. export_schedule(kind='doors', format='csv',
                   outputPath='...\\doors.csv')
```

## Standards compliance

- **REI** per PN-EN 1634-1 (doors) / PN-EN 1364-1 (walls).
- **RC** per PN-EN 1627 (burglary resistance; 1 = residential minimum,
  4 = public, 6 = maximum).
- **Accessibility** door widths per PN-EN 17210 / WT-2019 §60: 90 cm
  clear for accessible, 110–120 cm for ward doors in hospitals.
- **Fire compartmentation** per WT-2019 §208–234 (ZL / PM / IN class).

## How to regenerate the manifest from code

```
dotnet run --project src/AcadMcp.Backend -c Release --no-build -- \
  --category openings --regenerate-manifest
```

## Conventions for this category

- All MCP-exposed tools live in `OpeningsTools.cs`; DTOs in
  `OpeningsDtos.cs`; one-line proxy in `OpeningsProxy.cs`.
- Plugin handlers live in `src/AcadMcp.Plugin/Tools/OpeningsPluginTools.cs`
  (keyed `acad.openings.<verb>`).
- Every tool MUST follow rules 20–25 (`[McpTool]` shape, naming, args /
  results, idempotency, category binding, tests).
- `Category = "openings"` on every tool; the source generator validates
  this matches the folder.
- When you add a family: update `s_families`, add a draw method, branch in
  `BuildBlockGeometry`, extend `ResolveDoorFamily` / `ResolveWindowFamily`,
  add an xUnit test, regenerate the manifest (see above) and verify
  `check-manifests` reports 0 problems.
