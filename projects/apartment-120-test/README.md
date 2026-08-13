# apartment-120-test

First live proof of `docs/engineering-rules/73-space-planning-method.md` — a ~123.5 m² gross
apartment built ZONE-FIRST (day cluster / buffer corridor / night cluster), not the single-row
"kulfon" pattern every earlier build in this repo used. Built by `scripts/build_apartment_120_test.py`.

## Design (decided before any tool call — rule 73 steps 1-5)

- Envelope: 13000 × 9500mm. `WALL_T` = 150mm uniformly (interior AND exterior — a documented
  simplification for this demo; a real exterior wall would be thicker). `INSET` = 75mm net-internal.
- **DAY zone** (y 0-4350, on the entry + south/daylight facade): Przedpokój (entry, 8.61 m²) |
  Kuchnia (11.97 m²) | Salon z jadalnią (32.13 m²).
- **BUFFER** (y 4350-5700, 1200mm net clear = WT §95 minimum exactly): Korytarz (15.42 m²) — the
  day/night separator rule 73 step 3 calls for.
- **NIGHT zone** (y 5700-9500, on the north facade, reached only via the corridor): Sypialnia
  rodziców (10.77 m²) | Łazienka 1 (6.39 m²) | Sypialnia dziecka 1 (10.59 m²) | Sypialnia dziecka 2
  (10.59 m²) | Łazienka 2 (6.39 m²).
- Every bedroom net footprint checked ≥ 2800×3600mm and kitchen/living checked against their own
  `populate_room` preset minimums (rule 64 §6) BEFORE any wall was drawn (rule 73 step 5).
  Bathrooms land at net 1750×3650mm — comfortably over the 1600×2200mm preset minimum but an
  elongated proportion; a known simplification (see script docstring), not hidden.
- Structural grid (3×2 non-uniform bays via `acad-grids.draw_grid`, 12× HEA140 columns + 1× IPE160
  facade beam) is deliberately independent of the partition layout — the partitions are
  non-load-bearing infill (normal multi-family slab+column construction), per rule 72 §3.
- 9 doors + 6 windows (the living room got two 1500mm windows flanking a structural column
  instead of one 3000mm picture window — see below), every opening with its own
  `insert_lintel`-sized RC lintel and `LINTEL_TYPE` tag (15 lintels total).

## Real defects found live and fixed (not caught by criteria 18-20 or audit_all_rooms)

A first pass of this build looked clean under every check above, but `acad.validators.check_overlaps`
(added to step 9 per rule 73's own new section on this) found real physical collisions between
independently-placed elements — the kind of thing an adjacency/area check can never catch because
each element was individually valid, just not cross-checked against the others:

1. **Two structural columns punched through windows.** The 3×2 grid and the window positions were
   designed separately; columns at x=4333 and x=8667 landed inside the kitchen window's and living
   room window's own spans. Fixed by moving the kitchen window and splitting the living room's
   single wide window into two windows flanking the column instead (also a more realistic facade
   treatment than one picture window straddling a structural member).
2. **Two bathroom doors swung into their own room's WC bowl.** `populate_bathroom`'s preset always
   places the WC at a fixed offset from the room corner; the doors were positioned without checking
   where that would land. Fixed by repositioning both doors toward the corner furthest from the WC.
3. **Fixing #1 created a new collision**: relocating the door in Łazienka 1 to clear the WC put it
   in the path of a shower fixture instead. Then rotating the fixture cluster 180° via
   `populate_bathroom`'s own `orientation` parameter to clear a *column* collision (below) put the
   shower back in the door's way — fixed by nudging the door position again. Three fix-then-recheck
   iterations total before this one room came back clean.
4. **A shower fixture collided with a north-wall column.** Same root cause as #1 (grid and fixture
   placement designed independently) — fixed with `populate_bathroom(orientation="south")`, which
   rotates the whole fixture cluster around the room centroid rather than requiring the room
   geometry itself to change.

Final state: `check_overlaps` across columns-vs-windows/doors/furniture/plumbing and
doors-vs-plumbing/furniture — **0 overlaps in every category**, re-verified after each fix, not
assumed clean after the first one.

## Verification results

- `audit_all_rooms(cellMm=50, marginMm=1700, tolerancePct=10)`: 9/9 rooms measured via real
  flood-fill (no raycast fallback). 7/9 rooms flagged `labelMismatch` (8.5-18.5% measured-vs-
  declared) — **root-caused, not a construction defect**: `RoomRegionSolver`'s opening-sealing
  disc (`r = max(widthMm/2 + 1.5*cellMm, 2*cellMm)`) legitimately shrinks the flood-filled area
  near every door/window, worse for door-dense small rooms (the corridor, 6 doors, lost 18.5%;
  the bathrooms, 1 door each, stayed under the 10% tolerance). Full writeup in rule 73's own
  section on this. 0 `leakSuspected`, 0 `emptyOpenings` once `marginMm` was raised past the
  widest opening's own seal radius (1575mm for the 3000mm living-room window) — an earlier pass
  with `marginMm=400` produced a false 0-doors-detected reading on every room, also documented
  in rule 73.
- Rule 60 §1a criterion 18 (public/day zone reachable from entry without crossing a private
  zone): **PASS** — `EXT→0.1→0.2→0.3` stays entirely within the day/entry zone; every night-zone
  door hangs off the corridor (`0.4`), never directly off the entry.
- Criterion 19 (daylight-declared rooms actually sit on an exterior wall with a window):
  **PASS** — windows recorded in rooms `{0.2, 0.3, 0.5, 0.7, 0.8}`, exactly the daylight-required
  set (kitchen, living, all 3 bedrooms). Bathrooms deliberately windowless (mechanical
  ventilation), matching common Polish apartment practice.
- Criterion 20 (built adjacency graph matches this project's own declared table): **PASS** —
  the 9 built door edges match the declared table exactly, no missing or unexpected edges.

## Rule 74 retrofit — construction-document pipeline (2026-08-13)

Extended to the full `docs/engineering-rules/74-construction-document-readiness.md` checklist:
material hatching (4 exterior walls, concrete), dimension chains (`auto_dim_walls` ×2 +
`dimension_linear` ×2, all explicitly on `A-ANNO-DIMS`), 1 section line, zone entities (DAY/NIGHT,
now mandatory per rule 73 step 3a), and a real paperspace sheet (layout `A-101`) with a locked
1:100 viewport, title block, and 3 schedule tables (room/door/window).

**Real defects found live and fixed, not caught by the first-pass build:**

1. **Duplicate/phantom viewports.** `layouts.create_layout` auto-generates its own default
   viewport(s) the first time a layout is activated (AutoCAD's own behaviour) — a fresh `A-101`
   carried 2 phantom viewports (scale 1:1 "fit" + a second stray one) in addition to the one this
   script explicitly created and locked. Left alone, the plotted sheet showed the floor plan
   through 3 overlapping viewports at 3 different scales/pans — this is what the disconnected,
   oversized "frame" the user first flagged from a screenshot actually was. Fixed: `list_viewports`
   right after creating the intentional one, `delete_viewport` on every other handle found.
2. **`AcadEnv.Persist` (the plugin's single "append entity" choke point, used by every
   `acad.geometry2d.*`/`acad.annotations.*` primitive) was hardcoded to `*Model_Space`, regardless
   of which layout was current.** `insert_title_block`/`generate_*_schedule` calls made after
   switching to the paperspace layout still silently landed their entities in the BUILDING's own
   model-space coordinate system — invisible at 1:100 plot scale (confirmed live via
   `check_overlaps` between `A-ANNO-TTLB` and `A-WALL-BEAR`: real, non-zero bbox overlap near the
   building's own origin). This was a real, previously-undiscovered capability gap, not a script
   bug — this bank had never drawn anything into paperspace before this session. Fixed properly:
   added an optional `layoutName` parameter threaded through `AcadEnv.Persist` →
   `draw_line`/`draw_polyline`/`add_dbtext`/`add_table` plugin handlers → `ArchitectureProxy` →
   `insert_title_block`/`generate_room_schedule`/`generate_door_schedule`/`generate_window_schedule`
   backend args, defaulting to the old model-space behaviour everywhere so no other caller's
   behaviour changed. `verify_construction_readiness.py`'s own `select_by_layer` check inherited
   the same model-space-only scope (`acad-selection`'s long-standing, otherwise-correct design) —
   given a matching opt-in `anySpace` parameter, same additive pattern. **This surfaced a second,
   independent bug once the plugin was redeployed and `anySpace=true` still had zero effect**:
   this bank has a two-hop DTO architecture (an MCP tool call deserializes into a Backend-side
   args record, which `SelectionProxy` generically re-serializes to forward to the plugin over
   the named pipe) — a field added only to the plugin-side `ByLayerArgsDto` is silently dropped
   at the Backend hop before it ever reaches the plugin, no error, no warning. Fixed by adding
   the matching `AnySpace` field to the Backend's own `ByLayerArgs` record
   (`AcadMcp.Backend/Categories/Selection/SelectionDtos.cs`) — confirmed live, `select_by_layer`
   now finds 31 title-block entities and 3 schedule tables where it found 0 before this second
   fix, and `verify_construction_readiness.py` reports a clean 14/14 PASS.
3. **Schedule tables silently 46-92% taller than `SchedulesTools`' own row-count × row-height
   math predicts.** AutoCAD's `Table.GenerateLayout` clamps every row to a TableStyle-driven
   minimum regardless of the requested `RowHeight` — confirmed live: window schedule (8 rows,
   80mm requested) measured 123.5mm actual, door (11 rows, 110mm) measured 161mm, room (11 rows,
   88mm) measured 153.5mm. Widening the tightest columns changed nothing (ruled out wrapping as
   the cause). A first attempt stacking 3 schedules by the nominal formula on an A3 sheet, then an
   A2 sheet, both produced overlapping tables — fixed by measuring each table's real bbox via
   `get_entity` immediately after creation and positioning the next one from that real bottom
   edge, on an A1 sheet (the nominal-vs-real gap needs ~478mm of stacked height, more than A2's
   own 420mm total height can hold after the title block).
4. **`insert_title_block`'s `scale` argument does double duty** — it sizes the sheet (correctly
   `"1:1"` for literal paperspace mm) AND auto-fills the title block's own `SKALA` field via
   `values.TryAdd`, so left alone the sheet printed "SKALA 1:1" instead of the actual plan scale.
   Fixed with an explicit `fields: [{"key": "SKALA", "value": "1:100"}]` override (an explicit
   field wins over the auto-fill since `TryAdd`'s first write sticks).
5. **A systematic bbox sweep (2026-08-13, third pass) — every annotation-bearing layer checked
   pairwise against every other, not one export eyeballed for "does it look right"** — found two
   more real collisions the earlier visual-only passes missed: `insert_north_arrow`'s `position`
   is the CENTER of a 3000mm-diameter circle (1500mm radius), not a corner, a detail invisible
   from the tool's own args and only found by measuring the placed entity's real bbox — at the
   original spacing the circle's own left edge landed 800mm INSIDE the building, overlapping
   room 0.8/0.9's tags, and the scale bar placed only 600mm below sat well within the same
   circle. Both repositioned with real clearance computed from the measured radius. Separately,
   the section line (originally routed through the building's exact geometric centre, `X1/2`)
   sat directly under the corridor's own 3-line tag, which shares that same centre by construction
   (the corridor spans the full width) — fixed by moving the section line to `x=5900`, the one
   gap clear of every room's tag on both the day and night rows at once (checked against real
   bbox data, not re-guessed after another collision).

This is now checked with a **generic script** (any two annotation layers, pairwise, cross-space
pairs filtered out since paperspace and model-space coordinates only relate through the
viewport's own transform, not raw coordinate equality) rather than by eye — 0 real overlaps
remain across `A-ANNO-NORT`/`A-ANNO-SBAR`/`A-ROOM-IDEN`/`A-ZONE-IDEN`/`A-DETL-SECT`.

## Vision review — rule 60 §1 17-criterion scorecard (2026-08-13, Path B)

Scored directly by the driving Claude Code session against the rendered `A-101` export (rule 74
item 9, Path B — the sidecar wasn't running this session; see Known limitations). Cross-checked
against the build script's own tool calls where the image alone doesn't settle it.

| # | Criterion | Score | Note |
|---|---|---|---|
| 1 | Wall hatching | 1.0 | All 4 exterior walls, concrete, handle-based |
| 2 | Furniture | 1.0 | Every inhabited room populated (kitchen/living/3 bedrooms/2 bathrooms) |
| 3 | Sanitary fixtures | 1.0 | Both bathrooms fully fixtured via `bathroom-residential` |
| 4 | Doors | 1.0 | 9 doors, jamb+swing+NUMBER+`LINTEL_TYPE`, no REI declared (not required for interior residential doors) |
| 5 | Windows | 1.0 | 6 windows, frame+glass+sill+centre |
| 6 | Vertical circulation | 1.0 (vacuous) | Single ground-floor unit, no shared stair/lift in scope — nothing present to be wrong |
| 7 | Structural grid | 1.0 | Lettered/numbered axes, continuous grid lines, dimensioned |
| 8 | Dimensioning | 0.5 | Main perimeter/partition chains present; no sub-tier dimensioning of individual door/window widths |
| 9 | Schedules | 1.0 | Room/door/window — real paperspace Tables |
| 10 | Callouts | 0.5 | Title block/north arrow/scale bar correct; no profile/detail (column) callout leaders |
| 11 | Section lines | 1.0 | 1 section (A-A) with cut-plane markers |
| 12 | Lineweight/CTB | 0.0 | No `.ctb` supplied (rule 61 §3, opt-in) |
| 13 | Finishes legend | 0.0 | `generate_finish_legend` never called |
| 14 | Orientation + scale | 1.0 | North arrow + scale bar present, on-plan rather than sheet-corner — valid professional convention |
| 15 | RCP (optional) | 0.0 | Not built — rubric marks this optional, weighted accordingly in the read below |
| 16 | Jamb/sill/lintel blow-ups | 0.0 | No `DET-XX` detail viewports |
| 17 | Room program fidelity | 1.0 | All 9 declared rooms present, correctly labelled; `labelMismatch` flags are a flood-fill measurement artifact (documented above), not a fidelity defect |

**Score: 12.0 / 17 — "technical study" band (10-13/17): OK for internal review, NOT tender/pozwolenie-ready.**

`fatal_gaps` (score < 1.0, real remaining work before "wykonawczy" status): CTB/lineweight (12),
finishes legend (13), profile/detail callouts (10), sub-tier dimensioning (8), blow-up details
(16). RCP (15) is explicitly optional per the rubric and carries less weight than the others.
This is an honest result for a project whose own stated purpose is proving rule 73/74's
*process*, not shipping a tender-ready package — the gaps above are the concrete list of what
that would still take, not a hidden shortfall.

## Known limitations (documented honestly, not hidden)

- `verify_construction_readiness.py` now reports a clean **14/14 PASS** (2 SKIP: no `.ctb`
  supplied, Vision sidecar not running — both expected, see below). This needed BOTH the plugin
  `anySpace` fix and the second, independent Backend-DTO fix it surfaced (see the defect list
  above) — the plugin redeploy alone was not enough, confirmed live before concluding it worked.
- ~~`configure_plot(layoutName="A-101", paperSize="A1")` silently resolved to
  `NorthAmericaNumber10Envelope`~~ — **fixed** (2026-08-13, second pass): the correct locale
  string is `"ISOA1"`, confirmed via `list_paper_sizes` — `"A2"`/`"A3"` matching their own plain
  names earlier was luck, not a working convention. `configure_plot` now receives `PLOT_MEDIA =
  "ISOA1"` (kept as a separate constant from `SHEET = "A1"`, which is a different namespace —
  this bank's own `CalloutsPalette.Sheets` key, not the plotter's media name).
- **New, still open**: `acad.files.export_file` renders a BLANK area where the viewport's own
  content should be, for any layout whose locked viewport has been through a genuine save+reload
  — confirmed reproducible on THIS project too (not dental-clinic-test-specific), including after
  a full AutoCAD process restart. The viewport's own properties (scale, center, width, lock) read
  back correctly immediately before every failing export — this is a rendering-pipeline bug, not
  data corruption. See `dental-clinic-test`'s own README for the full investigation (scope
  Window vs. Extents, locked vs. unlocked, a native `REGENALL` — none fixed it) and follow-up
  task `task_65805cb0`. **Practical effect on this project**: earlier exports in this session
  were captured in the same unbroken AutoCAD session as their own build (before any close/reopen)
  and are trustworthy; a fresh export of this saved file should not be trusted until that task
  lands.
- Vision review (rule 74 item 9): sidecar port file present but the sidecar itself was not
  running this session — health-check skipped, scored `/v1/architect-review` call not attempted.
  Per the user's standing instruction this session, the API key configuration is left alone.
- Plot-style CTB (rule 74 item 8): no `.ctb` file supplied under `assets/plotstyles/` — best-effort
  step skipped, matching rule 61 §3's own opt-in design.

## Files

- `Apartment120Test.dwg` — the built drawing.
- `../../scripts/build_apartment_120_test.py` — the build script (re-runnable; starts from
  `new_document`).
- `apartment-120-test-A101.png` (not tracked here — see `artifacts/architect-review/`) — the
  rendered A-101 sheet export used for the visual verification above.
