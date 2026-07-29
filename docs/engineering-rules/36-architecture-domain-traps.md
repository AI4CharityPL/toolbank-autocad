# acad-architecture domain traps

Architectural domain traps — wall geometry, doors with swing, windows with sill/header, columns, slabs, rooms, stairs, plan-view dimensions. Read BEFORE adding a tool to acad-architecture or a validator under validators/architectural/.

These are the lessons learned drawing **plan-view architectural** content the
"right" way for AEC offices. They sit on top of the universal domain rule 35.

## 1. A wall is **not** a single line

The naive `wall = (a, b)` only works for visualisation. A real architectural
wall has **two faces** (parallel polylines) at offset `±thickness/2` from a
**centreline**. The MCP tool MUST store BOTH the centreline (on layer
`A-WALL-CTRL`, frozen by default) AND the two faces (on layer `A-WALL`) so
that downstream operations (offset, intersection cleanup, area takeoff) have
something to work with. Returning only the centreline is a regression.

The `draw_wall` result MUST include `centerlineHandle`, `leftFaceHandle`,
`rightFaceHandle` — the agent will need them for joins.

## 2. Wall ends — square, butt, or mitre

Two walls meeting at a corner default to **mitre** (faces extended/trimmed to
the angle bisector). Two walls meeting in a T-junction use **butt** (the
through-wall faces are continuous, the perpendicular wall faces stop at the
through-wall outer face). Free wall ends are **square** (a perpendicular cap
between the two faces).

The `connect_walls` tool MUST take an explicit `joinType: mitre|butt|square`.
Auto-detection is a Phase-7 concern; v1 demands the agent decides.

## 3. Doors have a leaf, a swing, AND a wall opening

A door insert is **3 things** in one tool call:
- a wall opening (gap in both wall faces, length = door width + frame
  clearance, default 5 mm/side);
- a door leaf (rectangle representing the panel in its open or closed
  position);
- a swing arc (90° default, both directions allowed).

`insert_door` punches the wall opening AND draws the swing WHEN called with
`wallHandle` (fixed 2026-07-29 — earlier builds always skipped the cut and
left a note admitting it). `wallHandle` is opt-in, not auto-detected: the
tool has no way to know which entity is "the wall" without being told, so a
call without `wallHandle` still only draws the door primitives — that's a
legitimate call shape (e.g. door not modeled against a specific wall
entity yet), not the bug the tool used to have. Any agent placing a door
into a real wall entity MUST pass `wallHandle` — returning a door block
reference without attempting to cut its host wall, when the wall handle was
available, is the bug this rule is about.

## 4. Windows have a sill and a header line

A window in plan is a wall opening + 3 parallel lines (sill, glass,
internally-divided panes) on layer `A-GLAZ`. Without the sill line the
plan looks like a hole in the wall. The `insert_window` tool MUST draw the
sill line and the glass line (on layer `A-GLAZ`); pass `wallHandle` to also
cut the wall (same opt-in rationale as `insert_door` above).

## 5. Columns are NEVER on the wall layer

Structural columns (rectangular or circular) live on layer `S-COLS` (yes,
**S**, not A — they're structural). The architectural plan REFERENCES them
but doesn't own them. `insert_column` MUST create the `S-COLS` layer if
missing, draw the column profile there, and put a small `+` centre-mark on
layer `S-COLS-CTRL`.

## 6. Rooms are closed boundaries + a tag

A room is a closed polyline on layer `A-ROOM-BNDY` PLUS a block reference
`ROOM_TAG` (with attributes `ROOM_NUMBER`, `ROOM_NAME`, `AREA_M2`) inserted
at the polyline's centroid. The `define_room` tool MUST compute the area
itself (`Polyline.GetArea() / 1e6` for mm² → m²) and write it into the
attribute. Areas computed by the AI agent from raw points are off by a few
mm² and downstream schedule tools choke on the rounding drift.

## 7. Floor slabs go on `S-SLAB`, not on the wall layer

A floor slab outline is a closed polyline on layer `S-SLAB` with optional
hatch `ANSI31` on layer `S-SLAB-HATCH`. Slabs MUST NOT be confused with
ceiling outlines (`A-CLNG`) or roof outlines (`A-ROOF`).

## 8. Stairs need a stringer, treads, AND a direction arrow

A plan-view stair is the most error-prone primitive. It needs:
- the outline (left and right stringers as parallel polylines on `A-STRS`);
- the tread lines (perpendicular polylines, count = numTreads);
- a centreline arrow indicating "up" with an "UP" or "DN" label;
- a break line at the cut plane (for floors above the cut).

If `insert_stair` skips any of these four, it's incomplete.

## 9. Dimensions are linear, NOT aligned, for orthogonal walls

For orthogonal architectural walls (axis-aligned), use linear dimensions
(`dim_linear`), NOT aligned (`dim_aligned`). Aligned looks fine on paper
until the wall rotates 0.001° during a parametric edit and the dimension
suddenly jumps to a non-axis number. Pin orthogonal dims as linear.

For non-orthogonal walls (anything > 1° off axis), use aligned. The
`dimension_walls` batch tool MUST inspect each wall's angle and pick the
right primitive — don't make the agent decide per-wall.

## 10. Hatches break boolean operations — apply LAST

Wall fill (`hatch_walls`) MUST run AFTER all wall geometry is finalised
(opens cut, joins resolved). Hatching first then trimming destroys the
hatch's associative boundary and you end up with floating hatches that
require `BHATCH` repair. Order: walls → openings → join cleanup → hatches.

## 11. Layer key (the office standard we ship)

| layer            | colour | linetype     | content                              |
| ---------------- | ------ | ------------ | ------------------------------------ |
| `A-WALL`         | 7      | Continuous   | wall faces (visible)                 |
| `A-WALL-CTRL`    | 8      | CENTER       | wall centrelines (frozen by default) |
| `A-DOOR`         | 30     | Continuous   | door leaves and frames               |
| `A-DOOR-SWING`   | 30     | DASHED       | door swing arcs                      |
| `A-GLAZ`         | 4      | Continuous   | glazing, sills, headers              |
| `A-ROOM-BNDY`    | 8      | DASHED       | room boundary polylines              |
| `A-ROOM-IDEN`    | 7      | Continuous   | room tags (block inserts)            |
| `A-CLNG`         | 5      | Continuous   | ceiling outlines                     |
| `A-ROOF`         | 5      | Continuous   | roof outlines                        |
| `A-STRS`         | 6      | Continuous   | stair outlines and treads            |
| `S-COLS`         | 1      | Continuous   | structural columns                   |
| `S-COLS-CTRL`    | 8      | CENTER       | column centre-marks                  |
| `S-SLAB`         | 7      | Continuous   | floor slab outlines                  |
| `S-SLAB-HATCH`   | 8      | Continuous   | slab fill hatch                      |
| `A-ANNO-DIMS`    | 2      | Continuous   | dimensions                           |
| `A-ANNO-NOTE`    | 2      | Continuous   | text notes, leaders                  |

`ensure_architectural_layers` is THE entry point that creates this layer
key idempotently. Every other tool calls it first.

## 12. Bundled blocks under `blocks/architectural/`

Ship at least these:
- `DOOR_SINGLE_900.dwg` (single-leaf 900 mm internal door, includes the
  swing arc as a separate `A-DOOR-SWING` polyline so we can hide it later);
- `DOOR_DOUBLE_1800.dwg` (double-leaf 1800 mm);
- `WINDOW_1200x600.dwg` (single-pane);
- `ROOM_TAG.dwg` (block with `ROOM_NUMBER`, `ROOM_NAME`, `AREA_M2`
  attributes);
- `TITLEBLOCK_A1.dwg`, `TITLEBLOCK_A3.dwg` (matches validator
  `arch.titleblock.must-be-defined`).

A tool that needs a block calls `acad.blocks.define_block_from_file` against
the bundled DWG ONCE per drawing, then `insert_block`. Sanitise the file
name — never pass an agent-supplied string into `Path.Combine`.

## 13. Cross-reference with validators

Every architectural draw tool has a paired validator rule. If `insert_window`
puts the sill on the wrong layer, `arch.glazing.sill-on-glaz-layer` will
catch it on the next `validate_drawing` run. When you add a domain tool,
add or update the matching validator rule (rule 35 §8).
