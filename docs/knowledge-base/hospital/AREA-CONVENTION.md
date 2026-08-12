# Hospital — area-measurement convention

## Decision

- **Convention: net internal area (to face-of-finish)**, not gross/centreline. Every m² figure
  in `docs/HOSPITAL-2026-MASTERPLAN.md` §2 (e.g. "single-bed room ≥ 15 m² netto") is explicitly
  labeled "netto" in the source — the masterplan itself already commits to net area, this file
  just makes the CAD-construction consequence explicit and mandatory.
- **Citation:** PN-ISO 9836 governs net/gross area definitions for Polish practice generally;
  the specific ≥15 m² / ≥4.5 m² / ≥36 m² figures in this typology come from Rozp. MZ 2019 and
  this repo's own `hospital.rooms.*` validator rules, both of which mean net clear floor area.

## Practical consequence for `define_room` (this is the part that was learned the hard way)

- Typical wall thickness in this program: **200 mm** (`draw_wall`'s own default).
- `define_room`'s boundary polygon vertices **must be inset by half the wall thickness (100 mm
  for a 200 mm wall) from the wall centreline on every side**, not drawn at the centreline.
- **What happens if you don't**: this session built ~90 rooms across zones A-D with boundary
  vertices at wall centrelines. `define_room`'s own declared area (shoelace formula over those
  vertices) came out systematically **15-25% larger** than what `audit_all_rooms`/
  `get_room_data`'s flood-fill measured (which correctly measures to the wall face). One
  operating room (`hospital.rooms.or-min-area`, ≥36 m² floor) was declared at 36.0 m² by its
  centreline polygon but measured at 30.0 m² once corrected — a real, physical
  under-compliance that only surfaced after `correct_all_room_areas(syncBoundary=true)` was run
  near the end of the build, at which point fixing it required stealing floor area from a
  neighboring room and re-cutting several walls.
- **The fix costs nothing if done up front**: draw `define_room`'s vertices at
  `(wall_centreline ± 100mm)` from the start. The declared area then already matches what
  `audit_all_rooms` will measure, and a validator rule checking `area_at_least` against the
  boundary polygon is checking a number that means what it says.

## Validator interaction

`hospital.rooms.patient-room-min-area` / `icu-room-min-area` / `or-min-area` /
`ensuite-min-area` (`validators/architectural/hospital-*.yaml`) all use the `area_at_least`
check against the room's own boundary-polygon `Polyline` entity — NOT the flood-fill
measurement. Get the polygon right at construction time; `correct_room_area(syncBoundary=true)`
exists as a repair tool for drawings that already have this problem, not as a substitute for
drawing it correctly the first time.

## Sourcing note

The masterplan's "netto" labeling is the source of the convention decision; the failure mode and
its cost are this session's own direct experience, recorded here (and in
`docs/engineering-rules/71-project-intake-protocol.md`) so it isn't repeated.
