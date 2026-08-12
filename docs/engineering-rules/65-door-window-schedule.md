# 65. Door + window schedule policy (acad-openings)

Doors + windows (acad-openings) policy — parametric block library, unified attribute contract (NUMBER, REI, RC, FIRE_CLASS, LEAF/SWING direction, SILL, ACOUSTIC, LEAD, ROOM_FROM/TO), numbering D-001 / W-001, schedule export, wall-cut semantics. READ BEFORE editing OpeningsPluginTools.cs, adding new door/window families, or calling insert_door / insert_window / cut_wall_for_opening from architecture or validators.

Companion to rule 19 (tool impl pattern), rule 28 (block / layer traps),
rule 63 (sanitary fixtures) and rule 64 (furniture density). Defines the
parametric opening block catalog, attribute contract, layer split,
numbering semantics, wall-cutting rules and schedule export format.

This rule exists because doors + windows are the **compliance-critical**
category in a hospital / public-building project: REI fire ratings,
evacuation widths, accessibility, burglary class and radiological shielding
all ride on these blocks' attributes. A bad attribute contract here
invalidates every schedule, every validator and every safety audit.

## 1. Drawing unit

Millimetres. All factory geometries use literal opening widths (e.g. 900 =
90 cm leaf). Callers MUST call `files.set_units('mm')` before `insert_door`
or `insert_window`; metric-inch drawings produce silently-wrong schedules.

## 2. Block naming convention

All opening blocks start with prefix `DOOR-` or `WIN-` — these prefixes are
how `list_openings_in_model`, `renumber_openings` and `export_schedule`
distinguish openings from furniture, plumbing or architecture blocks.

- **Sized families** (the only mode supported in D5):
  `<FAMILY>-<W>-<H>`  with `<W>` and `<H>` in mm.

  Examples:
    - `DOOR-SINGLE-900-2100`
    - `DOOR-DOUBLE-1600-2100`
    - `DOOR-SLIDING-1000-2100`
    - `DOOR-FIRE-1200-2100`
    - `DOOR-HOSP-1800-2100`
    - `DOOR-LEAD-1200-2100`
    - `WIN-FIXED-1200-1500`
    - `WIN-CASE-1200-1500`
    - `WIN-TILT-1500-1500`
    - `WIN-HOSP-1800-1500`
    - `WIN-FIRE-1500-1500`

Rule: if you add a family, `<FAMILY>` tokens MUST match regex
`^(DOOR|WIN)-[A-Z]{2,10}$` and MUST be registered in
`OpeningsPluginTools.s_families` with `SupportsFire` / `SupportsBurglary`
/ `SupportsLead` flags set correctly, because `list_opening_catalog`
surfaces those flags to callers (validators rely on them when deciding
whether `rei > 0` or `rc > 0` makes sense on that family).

`insert_door` chooses the family via `type` (single/double/sliding/fire/
hospital/lead) and `leadShielded=true` (which forces `DOOR-LEAD`).
`insert_window` chooses via `type` (fixed/casement/tilt/hospital/fire).

## 3. Block origin & geometry

- Block origin is at the **geometric centre of the opening on the wall
  axis**. That means the `-Y` half represents one face of the wall and the
  `+Y` half represents the swing / interior side.
- Doors draw the jamb tick marks + leaf line + swing arc(s). Arcs open into
  `+Y` by default (swing = IN). When `swingDirection = OUT`, the caller
  must rotate the block by 180° (arcs don't flip internally — fewer moving
  parts in the factory).
- Windows draw two parallel glass lines ±`GLASS_OFFSET` (40 mm) around the
  axis plus a centre line. Sash / tilt / fire markers decorate the centre.
- No separate "opening gap" rectangle is drawn — the gap in the wall is
  created **separately** via `cut_wall_for_opening` (which operates on the
  actual wall entity). The block only draws the leaf + arcs + jamb ticks.

## 4. Attribute contract

Every opening `BlockReference` carries THESE EXACT 15 TAGS (empty string
when irrelevant for the kind):

| Tag           | Visible | Doors | Windows | Notes                                       |
|---------------|---------|-------|---------|---------------------------------------------|
| `NUMBER`      | YES     | D-001 | W-001   | Auto-assigned unless caller passes `number` |
| `TYPE`        | no      | variant name                                  |
| `WIDTH_MM`    | no      | integer mm                                    |
| `HEIGHT_MM`   | no      | integer mm                                    |
| `REI`         | no      | "0","30","60","90","120" (doors); "0" on windows (FIRE_CLASS covers windows) |
| `RC`          | no      | "0"..."6" per PN-EN 1627 — windows only      |
| `FIRE_CLASS`  | no      | "", "E30", "EI30", "EI60", "EI120"           |
| `LEAF_DIR`    | no      | "L" / "R" (doors only)                       |
| `SWING_DIR`   | no      | "IN" / "OUT" (doors only)                    |
| `SILL_MM`     | no      | windows only, integer mm                     |
| `ACOUSTIC_DB` | no      | Rw dB (doors only, typically 30 / 35 / 40)   |
| `LEAD`        | no      | "0" / "1" (doors only)                       |
| `ROOM_FROM`   | no      | room code the door passes FROM               |
| `ROOM_TO`     | no      | room code the door passes TO                 |
| `LINTEL_TYPE` | no      | lintel/beam-over-opening type tag (e.g. `RC-150x250`, `HEB160`), blank if not set - see rule 72. Written by passing `lintelType` to `insert_door`/`insert_window`; `acad-structural.insert_lintel` computes the tag but never writes it itself (that tool never touches an opening block) |

Rules:
- `NUMBER` is the ONLY visible attribute so plans stay readable at 1:100.
- Validators query attributes by uppercase tag; always write uppercase.
- Adding a new semantic attribute REQUIRES a rule + spec update here and
  a matching entry in `s_attrTags` inside `OpeningsPluginTools`.

## 5. Numbering semantics

- `insert_door` auto-assigns `D-001`, `D-002`, … by scanning model-space
  for the max `NUMBER` starting with `D-` and incrementing.
- `insert_window` does the same with the `W-` prefix.
- Padding is 3 digits by default (`D-001`). Callers can override via
  `number=` to pin a specific code (e.g. `D-EVAC-01` for evacuation route).
- `renumber_openings` rewrites NUMBERs in either `insertion` order (the
  order `ObjectId`s come out of ModelSpace) or `spatial` order (Y desc →
  X asc, which reads "top-left to bottom-right"). Kind = `doors` / `windows`
  / `all`. Prefix and pad width are overridable.

**Never** re-use a number without renumber — `export_schedule` trusts
uniqueness. Validators (future rule 66+) will fail when two doors share
the same `NUMBER`.

## 6. Layer split

Default layer is inferred from block family:

| Block prefix              | Default layer  | Usage                                    |
|---------------------------|----------------|------------------------------------------|
| `DOOR-FIRE-*`             | `A-DOOR-FIRE`  | Fire-rated doors                         |
| `DOOR-LEAD-*`             | `A-DOOR-LEAD`  | Radiological shielded doors              |
| `DOOR-HOSP-*`             | `A-DOOR-HOSP`  | Hospital double-swing doors              |
| `DOOR-*` (other)          | `A-DOOR`       | Generic doors                            |
| `WIN-FIRE-*`, `WIN-HOSP-*`| `A-GLAZ-FIRE`  | Fire-rated windows                       |
| `WIN-*` (other)           | `A-GLAZ`       | Generic glazing                          |

Callers MAY override `layer=` but SHOULD follow AIA-2017 where possible.
The fire-separated layers exist so `CTB/STB` plot styles (D9) can highlight
fire compartmentation differently.

## 7. Wall cutting semantics

`cut_wall_for_opening` operates on a **single wall entity** (Line or
2-vertex Polyline) referenced by handle, and two **jamb points**:

- Both jambs are **projected onto the wall axis**; the projection parameter
  must lie in `[0, 1]` or the tool fails (jamb outside segment).
- The tool then erases the original wall and replaces it with zero, one,
  or two Line segments:
   - **left** = from `start` to `cut1`     (omitted if gap starts at start)
   - **right** = from `cut2` to `end`       (omitted if gap ends at end)
- `gapLengthMm` is the distance between the two projected points.
- Multi-segment polyline walls are NOT supported here — use the D6 tool
  `split_wall_at_opening` when it lands.

Callers SHOULD:
1. Call `cut_wall_for_opening` FIRST (removes the original wall in a write
   transaction and returns new handles).
2. Then call `insert_door` / `insert_window` at the centre of the cut.
3. Optionally call `renumber_openings(order='spatial')` at the end.

This order is important because the door block does NOT itself cut the
wall — if the wall is still there, the door will appear to "pass through"
the wall polyline in validator scans.

## 8. Schedule export format

`export_schedule` enumerates every `DOOR-*` / `WIN-*` BlockReference,
reads attributes and emits one row per opening. The canonical CSV header
(lexicographic order **not** used — order matches the table in §4 plus
`BLOCK`, `LAYER`, `HANDLE`):

```
NUMBER,KIND,BLOCK,TYPE,WIDTH_MM,HEIGHT_MM,REI,RC,FIRE_CLASS,LEAF_DIR,SWING_DIR,SILL_MM,ACOUSTIC_DB,LEAD,ROOM_FROM,ROOM_TO,LINTEL_TYPE,LAYER,HANDLE
```

JSON format is the same fields as an array of objects. `outputPath` is
optional; when omitted the content is only returned in the result and the
caller writes it (e.g. Python harness). UTF-8 encoding, LF-or-CRLF as the
host produces.

`kind` filter: `doors` / `windows` / `all`. The tool is read-only.

## 9. Interaction with other categories

- **Architecture**: after drawing a `wall_line` or `room_rectangle`, call
  `cut_wall_for_opening` THEN `insert_door` / `insert_window`. Do not
  punch doors by drawing a shorter wall manually — the resulting visual is
  correct but the door has no attribute linkage and won't appear in the
  schedule.
- **Hatches (rule 62)**: hatches applied to rooms with `apply_material_preset_by_point`
  DO NOT need to be re-applied after cutting walls; hatch boundary detection
  uses the new geometry automatically on regenerate.
- **Furniture / plumbing**: PopulateRoom presets assume doors are already
  placed. A future validator will flag beds/fixtures that obstruct door
  swing arcs.
- **Schedules (D8 future)**: `generate_door_schedule` calls
  `export_schedule(kind='doors', format='csv')` under the hood and then
  imports it into an AutoCAD `Table` entity via `TableStyle` HOSPITAL-DEF.
- **Sections (D9 future)**: section cuts transect doors/windows; the
  schedule is the source of truth for `HEIGHT_MM` and `SILL_MM`.

## 10. Performance budget (per call, idle AutoCAD)

| Tool                       | Target p95 | Hard cap |
|----------------------------|-----------:|---------:|
| `list_opening_catalog`     |     10 ms  |    200 ms |
| `insert_door`              |     60 ms  |    500 ms |
| `insert_window`            |     60 ms  |    500 ms |
| `insert_opening_generic`   |     50 ms  |    400 ms |
| `draw_door_by_points`      |     25 ms  |    200 ms |
| `draw_window_by_points`    |     25 ms  |    200 ms |
| `cut_wall_for_opening`     |     50 ms  |    400 ms |
| `renumber_openings` (200)  |    150 ms  |  1 500 ms |
| `list_openings_in_model`   |    120 ms  |  1 000 ms |
| `export_schedule` (200)    |    300 ms  |  2 000 ms |

If you add a family, re-measure in the Hospital2026 master file
(≈ 180 openings) and update this table.

## 11. Testing checklist for new families

When adding a new `DOOR-<X>` / `WIN-<X>` family:

1. Register in `s_families` with correct capability flags.
2. Add a `DrawX` method drawing the block at origin (centred).
3. Add a `case "X":` branch in `BuildBlockGeometry`.
4. If `insert_door` / `insert_window` should dispatch to it, extend
   `ResolveDoorFamily` / `ResolveWindowFamily`.
5. Add an xUnit test inserting and listing one instance, asserting
   attributes propagate (`WIDTH_MM`, `HEIGHT_MM`, and any fire-rating).
6. Regenerate manifest: `dotnet AcadMcp.Backend -- --category openings --regenerate-manifest`.
7. Verify `check-manifests` = 0 problems.
