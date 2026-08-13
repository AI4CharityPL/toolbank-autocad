# dental-clinic-test

Second live proof of `docs/engineering-rules/73-space-planning-method.md` — a small
(~139 m² gross) dental practice, built zone-first with a genuinely 2D T-shaped corridor system
(not a single row), and a direct test that hospital-derived capability (`A-WALL-LEAD` radiation
shielding) transfers to a much smaller, different typology rather than being hospital-only. Built
by `scripts/build_dental_clinic_test.py`.

## Design (grounded in `docs/knowledge-base/dental-clinic/*.md` — rule 73 steps 1-5)

- Envelope: an 8-vertex L/Z-shaped footprint (not a plain rectangle) — narrower at the public
  front (0-9500mm wide), full width (0-14000mm) through the treatment zone, narrower again
  (0-7750mm) at the back staff extension. `WALL_T` = 120mm (typical lightweight fit-out
  partition per this typology's own `AREA-CONVENTION.md`), `INSET` = 60mm net-internal.
- **PUBLIC row** (y0-4000): WC pacjentów | Poczekalnia (centred — borders BOTH neighbours, so
  both `Poczekalnia-WC` and `Poczekalnia-Rejestracja` adjacency requirements are satisfiable as
  direct doors, not just one of them) | Rejestracja.
- **Korytarz-H** (y4000-5500, the "korytarz zabiegowy" the room program's own circulation-pattern
  section names) connects the public zone to the treatment zone.
- **Treatment row** (y5500-9500): Gabinet zabiegowy 1 | Sterylizacja + Magazyn (stacked) | Gabinet
  zabiegowy 2 | Gabinet RTG punktowe — with **Korytarz-V**, a vertical corridor spine (x
  6250-7750), running through the middle and OPEN to Korytarz-H at y=5500 (no wall in that gap —
  a real T-junction, not a doored connection). This is what makes the layout genuinely 2D rather
  than one more single row.
- **Staff row** (y9500-11500, west portion only): Pomieszczenie socjalne | WC personelu, reached
  via Korytarz-V's own continuation north — kept off the patient-facing corridor per the
  room program's adjacency table.
- **Gabinet RTG's primary-beam wall is shielded**: 200mm, drawn on layer `A-WALL-LEAD` — the
  SAME layer the hospital typology already defined (`docs/knowledge-base/hospital/GRID-AND-LAYERS.md`),
  reused rather than reinvented, exactly as `dental-clinic/GRID-AND-LAYERS.md` says to. Only the
  one wall bearing the primary beam direction is shielded (not all 4 walls) — a deliberate,
  less-conservative-but-more-accurate choice per `STANDARDS.md`, with the RTG room's `define_room`
  boundary using an ASYMMETRIC inset (100mm on that one wall, 60mm on the other three) rather
  than a uniform-inset shortcut.
- **No windows at all** — this typology's own `STANDARDS.md` confirms WT §93's daylight
  requirement is a residential-room provision that does not apply here. A deliberate, documented
  choice: rule 60 §1a criterion 19 is checked against an EMPTY daylight-required set.
- Furniture/plumbing (`populate_room`/`populate_bathroom`) called ONLY where this bank has a
  genuinely matching preset: Poczekalnia (`waiting`), WC pacjentów (`wc-accessible`), WC personelu
  (`wc-public`). Rejestracja, both Gabinety, Sterylizacja, Magazyn, RTG and Socjalne got **no**
  furniture call — a real, honest catalog gap (no dental-chair or small-reception preset exists
  yet; forcing the oversized hospital-scale `consult` (min 3500×4500mm) or `reception` (min
  3000×4500mm) presets onto much smaller rooms would be a worse fit than leaving them unfurnished).

## A real construction bug caught mid-build (rule 73 step 5 doing its job)

The first pass tried a door from Gabinet 1 (x0-3125) directly onto the corridor spine's own wall
(x=6250) — but Gabinet 1 doesn't actually border that wall; the Sterylizacja/Magazyn column
(x3125-6250) sits between them. `insert_door` correctly rejected it live
(`cut_wall_for_opening: jambs project outside the wall segment`), which is exactly what a door
placed on a wall the room doesn't touch looks like from the tool's side — caught and fixed by
routing Gabinet 1's door onto its real shared wall (Korytarz-H) instead, before the room labels
or furniture went in.

## Real defects found live and fixed (not caught by criteria 18-20 or audit_all_rooms)

Same lesson as the apartment build, found by adding `acad.validators.check_overlaps` to step 9
(now codified in rule 73's own section on this) — a build that passes every logical/adjacency
check can still have real physical collisions between elements that were each individually valid:

1. **3 of the 12 grid columns landed outside the building.** The structural grid was placed on a
   plain rectangular 4×3 product, but this building's own envelope is L/Z-shaped (narrower at the
   public front and the staff extension) — columns at (9333,11500), (14000,0) and (14000,11500)
   were floating in the exterior notch cut away from those rows. Fixed with an `in_building(x,y)`
   filter applied before insertion, not discovered after.
2. **A structural column punched through the front door.** The column at (4667,0) sat inside
   `D-01`'s own 1000mm-wide opening span. Fixed by moving the door to x=2800.
3. **Fixing #2 put the door in the waiting room's sofa instead.** The `waiting` preset places its
   sofa centred on the room; moving the door away from the column (first to x=3200) put it
   straight into the sofa's own footprint — caught on the SAME rebuild's overlap pass, moved again
   to x=2800 to clear both independently-placed elements.
4. **Two doors swung into their own room's plumbing fixtures.** `D-02` (into WC pacjentów) clipped
   the accessible-WC preset's basin; `D-08` (into WC personelu) clipped the wc-public preset's
   basin and WC bowl. Fixed by repositioning both doors into the gap between where each preset
   places its fixtures.

Final state: `check_overlaps` across columns-vs-doors/furniture/plumbing and
doors-vs-plumbing/furniture — **0 overlaps in every category**, re-verified after each fix.

## Verification results

- `audit_all_rooms(cellMm=50, marginMm=700, tolerancePct=10)`: 11/11 rooms measured via real
  flood-fill. 5/11 flagged `labelMismatch` (10.2-26.1%) — same root cause as the apartment build
  (rule 73's dedicated section): the opening-sealing disc shrinks measured area near every door,
  worst for the most door-dense room (Korytarz-H, 7 doors touching it, 26.1% loss) and mildest
  for single-door rooms (~10-11%). Not a construction defect.
- **New finding, flagged separately** (not part of rule 73's own scope — a background task was
  spawned for it): the audit's own `query` display field showed each room's AREA text (e.g.
  "18,93 m²") instead of its declared number/name for every room in this build, unlike the
  apartment build where purely-numeric room numbers ("0.1", "0.2"...) displayed correctly. The
  underlying per-row measurements (`doorCount`, `deltaPct`, `flags`) are unaffected — verified
  independently via `list_openings_in_model` — this is a display/grouping-key issue in
  `FetchGroupedRoomsAsync`'s number/name parsing for alphabetic room numbers, not a measurement
  bug. Root-cause investigation flagged as a follow-up, not blocking this build.
- Rule 60 §1a criterion 18 (public zone reachable from entry without crossing a private/back
  zone): **PASS** — no door edge jumps directly from a public room (`PUB.*`) into a
  treatment/staff room; every public-to-back connection routes through `COR.H`/`COR.V`.
- Criterion 19 (daylight-declared rooms have a window): **PASS**, vacuously — this typology
  declares no room daylight-required, confirmed against `STANDARDS.md`, and the build correctly
  has zero windows as a result.
- Criterion 20 (built adjacency graph matches this project's own declared table): **PASS** — all
  12 built door edges match the declared table exactly, no missing or unexpected edges.

## Rule 74 retrofit — construction-document pipeline (2026-08-13)

Extended to the full `docs/engineering-rules/74-construction-document-readiness.md` checklist,
applying every lesson learned live on `apartment-120-test`'s own retrofit (see that project's
README for the full defect writeups — `AcadEnv.Persist`'s model-space-only routing,
`Table.GenerateLayout`'s row-height clamping, the phantom-viewport auto-creation, the `SKALA`
field double-duty — every one of those fixes applied here too, not rediscovered): 8-segment
material hatching (every L/Z perimeter wall, not a representative subset — this footprint has 8
exterior segments where the apartment had 4), dimension chains, 1 section line, 4 zone entities
(PUBLIC / CORRIDOR-H / TREATMENT / STAFF — TREATMENT's own bounding rect includes the COR.V
spine, matching how this project's own design docstring already described it), and a paperspace
sheet (`A-101`, A1, learned directly from the apartment retrofit rather than re-discovering the
A3→A2→A1 sizing problem) with a locked 1:100 viewport, title block, and 2 schedule tables
(room + door — no window schedule, this typology has no windows by design).

No new defect classes beyond what apartment-120-test's own retrofit already found and fixed —
confirms those were real, general capability gaps (not apartment-specific), not something to
re-diagnose per project.

## Vision review — rule 60 §1 17-criterion scorecard (2026-08-13, Path B)

Scored directly by the driving Claude Code session against the rendered `A-101` export (rule 74
item 9, Path B — the sidecar wasn't running this session; see Known limitations).

| # | Criterion | Score | Note |
|---|---|---|---|
| 1 | Wall hatching | 1.0 | All 8 L/Z perimeter segments, concrete, handle-based |
| 2 | Furniture | 0.0 | Only Poczekalnia furnished (`waiting` preset); Rejestracja, both Gabinety, Sterylizacja, Magazyn, RTG, Socjalne have zero furniture — a real, already-documented catalog gap (no dental-chair/small-reception preset exists yet), not hidden here either |
| 3 | Sanitary fixtures | 0.5 | Both WCs fully fixtured; treatment rooms carry no plumbing at all (a real dental chair setup needs at minimum a basin — same catalog gap as #2) |
| 4 | Doors | 1.0 | 12 doors, jamb+swing+NUMBER+`LINTEL_TYPE` |
| 5 | Windows | 1.0 (vacuous) | Zero windows by declared design (WT §93 doesn't apply to this typology) — nothing present to be wrong, matches criterion 19 §1a's own vacuous-satisfaction logic |
| 6 | Vertical circulation | 1.0 (vacuous) | Single-story clinic, no shared stair/lift in scope |
| 7 | Structural grid | 1.0 | Axes present, dimensioned, columns filtered to the real L-shaped envelope |
| 8 | Dimensioning | 0.5 | Main perimeter/band chains present; no sub-tier dimensioning of individual door widths |
| 9 | Schedules | 1.0 | Room + door — real paperspace Tables (correctly no window schedule) |
| 10 | Callouts | 0.5 | Title block/north arrow/scale bar correct; no profile/detail callout leaders |
| 11 | Section lines | 1.0 | 1 section (A-A) with cut-plane markers |
| 12 | Lineweight/CTB | 0.0 | No `.ctb` supplied (rule 61 §3, opt-in) |
| 13 | Finishes legend | 0.0 | `generate_finish_legend` never called |
| 14 | Orientation + scale | 1.0 | North arrow + scale bar present, on-plan |
| 15 | RCP (optional) | 0.0 | Not built — explicitly optional per the rubric |
| 16 | Jamb/sill/lintel blow-ups | 0.0 | No `DET-XX` detail viewports |
| 17 | Room program fidelity | 1.0 | All 12 declared rooms present, correctly labelled; the `labelMismatch` flags are the same flood-fill measurement artifact as the apartment build, not a fidelity defect |

**Score: 10.5 / 17 — bottom of the "technical study" band (10-13/17): OK for internal review,
NOT tender/pozwolenie-ready.** Lower than `apartment-120-test`'s 12.0/17, and honestly so — the
furniture/plumbing catalog gap (criteria 2-3) bites harder here, where most of the building's own
purpose-rooms (Gabinety, Sterylizacja, RTG) sit completely empty, than in the apartment where
every inhabited room got a real preset. `fatal_gaps`: furniture (2), sanitary fixtures (3),
CTB/lineweight (12), finishes legend (13), profile/detail callouts (10), sub-tier dimensioning
(8), blow-up details (16) — the concrete list of what "wykonawczy" status would still need, led
by closing the dental-fixture catalog gap first since it affects the most criteria at once.

## Known limitations (documented honestly, not hidden — same items as apartment-120-test)

- `verify_construction_readiness.py` now reports a clean **13/13 PASS** (3 SKIP: no windows in
  this typology by design, no `.ctb` supplied, Vision sidecar not running — all expected). Needed
  a second, independent fix beyond the plugin's `anySpace` parameter — see
  `apartment-120-test`'s own README for the full writeup (a Backend-side DTO hop was silently
  dropping the new field before it ever reached the plugin).
- `configure_plot(paperSize="A1")` resolves to `psk:NorthAmericaNumber10Envelope` instead of an
  ISO A1 media name (same as apartment-120-test) — cosmetic to the plotter's own media table only,
  the drawn geometry is sized from this bank's own `CalloutsPalette.Sheets["A1"]` constant and is
  unaffected.
- Vision review and plot-style CTB: same best-effort/skipped state as apartment-120-test, same
  reasons (sidecar not running this session; no `.ctb` file supplied).

## Files

- `DentalClinicTest.dwg` — the built drawing.
- `../../scripts/build_dental_clinic_test.py` — the build script (re-runnable; starts from
  `new_document`).
- `dental-clinic-test-A101.png` (not tracked here — see `artifacts/architect-review/`) — the
  rendered A-101 sheet export used for the visual verification above.
