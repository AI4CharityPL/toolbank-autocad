# automotive-showroom-test

Third live proof of `docs/engineering-rules/73-space-planning-method.md`, and the first typology
in this bank built straight to `docs/engineering-rules/74-construction-document-readiness.md`
level in one pass rather than a separate retrofit after the fact. A ~495 m² gross car dealership
showroom (salon samochodowy): one big column-free public exhibition hall, a small public-amenities
block, two glazed sales-office pods, and a back-of-house staff wing. Built by
`scripts/build_automotive_showroom_test.py`.

## Design (grounded in `docs/knowledge-base/automotive-showroom/*.md` — rule 73 steps 1-5)

- **Three attached blocks**, not a plain box: a main 18000×21500mm block, a 3500×5500mm east
  bump-out (the two sales-office pods), and the same main block continues north into the rear
  (staff) wing. `WALL_T`=120mm interior partitions, `EXT_WALL_T`=150mm exterior/bearing walls — a
  genuinely two-thickness building, which `AREA-CONVENTION.md` explicitly calls for ("pick the
  right thickness per wall, not one default") unlike apartment/dental's uniform partitions.
- **Hala wystawowa (the hall) touches the south/street wall directly for 12 of the 18m frontage
  (67%)** — the single strongest, most-repeated finding in this typology's own research ("the
  exhibition hall fronts the street, fully glazed on the public-facing elevation... the single
  strongest design driver"). An earlier draft put a full-width front amenities band between the
  hall and the street, which would have hidden the hall entirely behind Recepcja/Poczekalnia —
  rejected before any tool call for directly contradicting the research's own top finding. Instead
  only a small SW-corner block (Recepcja/Poczekalnia/2×WC, 9000×4500mm) is carved out of the
  hall's own footprint — one notch, hand-computed as a 6-vertex inward-offset polygon (see the
  build script's own comment for the vertex-by-vertex derivation), not five.
- **East bump-out**: two glazed sales-office pods (Biuro sprzedaży 1/2) bordering the hall's own
  east wall, each with its own window onto the exterior — "sales offices sit around the hall's
  perimeter... so vehicles stay visible from inside the offices."
- **Rear wing**: a corridor (Korytarz personelu) off the hall, then Przyjęcie serwisowe / Biuro
  administracji ×2 / Pomieszczenie socjalne / WC personelu / Magazyn off that corridor. Reachable
  from the hall through exactly one door — back-of-house, not visually open to the sales floor,
  matching the adjacency table.
- **Structural grid**: 4 bays × 4500mm (x) and [16000, 5500]mm (y) — deliberately **no**
  intermediate column inside the 16000mm hall span, since interior columns would break sightlines
  to displayed vehicles, the #1 constraint `GRID-AND-LAYERS.md`'s own research found. HEB200
  columns on the main grid, lighter HEA160 at the 2 bump-out corners.
- **Windows, unlike dental-clinic-test's zero-by-design**: this typology's core driver IS glazing,
  so 7 rooms are declared daylight-required and each gets a real `acad.openings.insert_window` —
  Hala wystawowa's own 3500mm shopfront (floor-to-ceiling, `sillHeightMm=0`), both sales offices,
  both back-offices, Przyjęcie serwisowe, Pomieszczenie socjalne. Recepcja and Poczekalnia were
  **deliberately excluded** — see "Real defects found live and fixed" below.
- **Materials**: exterior/bearing walls hatch `steel` (see the confirmed catalog bug below) except
  the hall's direct-frontage segment, which hatches `glass` — the first build in this bank to
  differentiate curtain-wall glazing from opaque cladding on the *same* exterior wall rather than
  one material for every exterior wall.
- **Furniture**: unlike dental-clinic-test (where almost nothing had a matching preset), every
  workplace room here fits an existing preset comfortably — Recepcja gets `reception` (this bank's
  own 3000×4500mm reference minimum, which is exactly this room's own footprint), Poczekalnia gets
  `waiting`, both sales offices and both back-offices get `office` (2400×2800mm minimum,
  comfortably exceeded everywhere). Przyjęcie serwisowe, Pomieszczenie socjalne, Magazyn, Korytarz
  get none — no matching preset exists for a service desk or bulk storage, an honest gap, same
  discipline as dental-clinic-test's own docstring.

## Known, documented gap vs. the knowledge base's own ideal

The adjacency table in `ROOM-PROGRAM.md` calls for "Sales offices ↔ Back-office/staff zone:
direct... staff need to move between customer-facing and admin work **without crossing the public
hall repeatedly**." This build's only path from Biuro sprzedaży 1/2 to the rear wing is
`OFF → HALL → COR` — it *does* cross a corner of the public hall. A dedicated back corridor
connecting the bump-out to the rear wing without crossing the hall would need a 4th structural
wing, out of scope for this pass. Flagged here and in the build script's own docstring rather than
silently shipped as if it matched the ideal.

## Real defects found live and fixed (check_overlaps + rule 74 pipeline, one build session)

Unlike apartment/dental (retrofitted after an initial "logically valid" build), this project ran
the full rule-74 pipeline — hatching, dimensions, schedules, callouts, windows — in the SAME
build pass that placed rooms/doors, so `check_overlaps` caught every collision in a tighter loop.
Two rounds, 10 → 0 overlaps:

1. **A structural column sat exactly on the hall↔corridor door (D-09, x=9000)** and on **3
   windows** whose centres happened to land on grid columns (a 4500mm-wide window at x=4500, a
   1500mm window at x=9000, and the original 6000mm shopfront centred on x=13500, straddling
   BOTH the 9000 and 13500 grid lines). Fixed by moving D-09 to x=6750, and by re-sizing the
   shopfront from 6000mm to 3500mm so it sits cleanly WITHIN one 4500mm structural bay instead of
   straddling a column — architecturally the more correct choice too (glazing panels sit between
   mullions, not through them).
2. **Recepcja's own furniture ate its own door and window.** The `reception` preset's 2400mm desk,
   centred in a 2865mm-net room, left no real clearance for the south-wall entrance door OR a
   window on the same wall — `check_overlaps` (`A-DOOR`/`A-GLAZ` vs `A-FURN-DSK`) found both. Two
   fixes, not one: the main entrance was moved off Recepcja's wall entirely and repointed to open
   directly into **Hala wystawowa** instead (x=15000) — architecturally the better call anyway,
   matching the KB's own "hall fronts the street" driver — and Recepcja's own dedicated window was
   dropped. The same conflict (a 2200mm sofa vs. a 1500mm window) forced the identical call for
   Poczekalnia. Both rooms now borrow light through their own door into the hall's shopfront
   instead of having a dedicated window — a real design resolution, not a workaround: both already
   open directly onto the one room in the building with the most glazing.
3. **The `office` preset's desk, centred on the room, left no clearance for a door on the same
   (corridor-facing) wall** in Biuro administracji 1/2 — a 1600mm desk in a 2880mm-net room left
   only ~640mm on each side, not enough for an 800-900mm door with jambs. Fixed by widening both
   rooms from 3000mm to 3800mm (rebalanced from Przyjęcie serwisowe/Pomieszczenie
   socjalne/Magazyn, all of which had slack), giving ~940mm clearance on each side of the desk.
4. **A door's swing arc clipped a reception chair**; two corridor doors clipped their own room's
   desk at its default centred position. Fixed by repositioning each door/window individually,
   re-verified after every change.

Final state: **0 overlaps in every category** — columns×doors, columns×windows, columns×furniture,
columns×plumbing, doors×plumbing, doors×furniture, windows×furniture.

## Two real tool bugs found live and root-caused (not guessed around)

**1. `HatchCatalog.cs`'s `insulation` material preset is broken — fixed at the source.**
`material="insulation"` (pattern name `"BATTING"`) throws a native AutoCAD `eInvalidInput` from
`Hatch.EvaluateHatch` every time, confirmed live and isolated: on an identical boundary polyline,
`concrete`/`glass`/`plaster`/`steel` all succeed and `insulation` alone always fails. `"BATTING"`
is not a real predefined AutoCAD hatch pattern name; `"INSUL"` is, confirmed live
(`draw_hatch_by_boundary` with `pattern="INSUL"` succeeds). **Fixed at the source**
(`src/AcadMcp.Shared/Catalogs/HatchCatalog.cs`, `BATTING` → `INSUL`) — this project itself still
uses `steel` for the opaque envelope rather than re-running the whole build for a cosmetic swap,
since the source fix doesn't take effect in THIS already-built drawing without a plugin redeploy +
AutoCAD restart, and `steel` is an equally defensible material for a steel-portal-frame envelope.
Future builds get the real `insulation` pattern once the plugin is next redeployed.

**2. `RoomRegionSolver.SolveFlood`'s opening-seal is far wider than the wall it's sealing — found,
root-caused, NOT yet fixed (needs design work, not a one-line change).** `audit_all_rooms` (method
`flood`) measures a room's true area by rasterizing walls and sealing each door/window gap with a
disc of radius `max(widthMm/2 + 1.5×cell, 2×cell)`. For a typical 900mm door at `cellMm=50`, that's
a 525mm-radius disc **centred on the wall**, reaching ~465mm past the wall face into the room —
eating real floor area near every opening. Confirmed by direct proof, not inference: `get_entity`
on the actual wall face lines (e.g. Biuro administracji 1's walls) matches this project's own
declared net-internal inset **exactly to the millimetre** — the rooms and walls are geometrically
correct; only the audit's own re-measurement undercounts them. Rooms with several doors/windows on
their boundary (common here, given this project has real windows unlike the earlier two proof
builds) lose 10-20% of their true area in the flood-fill measurement and get falsely flagged
`labelMismatch`; large rooms barely notice (Hala wystawowa, 283 m², isn't flagged at all — the
same fixed per-opening loss is a rounding error against its own size). **This is why 9 of 14 rooms
below show `labelMismatch` — a verified false positive, not a build defect.** It ALSO affects
`generate_room_schedule`'s own area column (it pulls the same measured value), so the room
schedule on the exported sheet under-reports area for those same 9 rooms — a real, visible defect
in the deliverable, not just the audit report. Root-caused precisely (`RoomRegionSolver.cs` line
~120) but not fixed here: a correct fix needs the seal to be an oriented rectangle/ellipse aligned
with the wall (reaching only ~wall-thickness perpendicular to it, while still spanning the
opening's full width along it), which needs a wall-direction angle threaded into `OpeningSeed` —
real design work, flagged as its own follow-up task rather than rushed.

**A third thing that looked like a bug and wasn't: `tagPosition` reproduced apartment-120-test's
own known phantom-room bug, live, and was reverted.** Four narrow rooms (WC klienci=1080mm,
WC dostosowana=1165mm, and 6 similarly narrow rear-wing rooms) have auto-centroid tags whose
3-line text is wider than the room itself, overlapping the neighbouring room's tag illegibly on
the exported sheet (visible in the PNG below). Staggering each tag's Y position — strictly inside
its own room's real interior, nowhere near a shared wall — was tried as a fix. Live re-verification
caught the same defect apartment-120-test's own history already flagged: `audit_all_rooms` went
from 14 rows to **17**, with 2 spurious `leakSuspected` duplicate rows (`"query": "ADM.1"` /
`"query": "SOC"`, no area) alongside each room's own correct row —
`FetchGroupedRoomsAsync`'s point-in-polygon sibling grouping got confused even though every
tagPosition stayed inside its own room. **Reverted rather than ship a project whose own audit data
is wrong.** The resulting narrow-room tag-text overlap is real and visible on the exported sheet —
acknowledged here, not hidden, and not worth risking data integrity to paper over.

## A third, more consequential tool bug — caught by the user's own Print Preview, fixed at the source

The first exported sheet looked wrong in a way none of the checks above catch: the plan rendered
tiny and off-centre, and the schedule stack ran hundreds of millimetres past the bottom of the
physical A1 sheet. Root-caused, not patched around:

**`create_viewport` never set the viewport's model-space pan target.** `Viewport.ViewCenter`
determines which point in the drawing appears at the centre of the viewport; the plugin handler
set `CenterPoint` (paper position), `Width`/`Height` (paper size) and `CustomScale`, but never
`ViewCenter`. Confirmed live via `get_viewport_extents_in_model`: a viewport created over this
project's own building (spanning x0-21500/y0-21500) was actually showing a 55000×45000mm model
window centred near **(148, 105)** — essentially the world origin — leaving the real building
crowded into one corner of a mostly-empty frame. This is not project-specific: `apartment-120-test`
and `dental-clinic-test` reused the exact same `center`/`width`/`height` values, so the same defect
is very likely present there too, just less visible on their smaller buildings. **Fixed at the
source**: added an optional `modelCenter` parameter to `create_viewport` (both the Backend
`CreateViewportArgs` and the plugin's own `CreateRectViewportArgsDto` — the two-hop DTO trap, rule
35 §11), which sets `vp.ViewCenter` when supplied. This project now computes its own real content
bounding box (building + zone tags in the west margin + north arrow/scale bar to the east +
section line) and sizes/centres the viewport to it, rather than reusing a fixed number from an
earlier, differently-sized project. Confirmed live end-to-end: rebuilt, redeployed the plugin,
restarted AutoCAD, re-exported — the plan now fills a proportionate share of the sheet, correctly
centred, with both grid-bubble axes visible (only the Y-axis showed before, since the X-axis
bubbles sat outside the old, badly-centred viewport's own frame).

**The schedule stack overflow was two separate causes, both fixed.** The single-column stack
(door → window → room schedule → finish legend) measured a real bottom at **y=-826.75mm — about
765mm past the A1 sheet's own y=0 edge**, confirmed by `get_entity` on each table (not just the
tool's own printed note, which earlier passes in this bank had dismissed as a reporting quirk —
it was not). Two real causes: (1) stacking 4 tables in one column when `Table.GenerateLayout`
clamps every row well above its requested height (already known from apartment/dental) simply
needs more vertical room than one column of an A1 sheet has; (2) `generate_finish_legend` always
includes 11 hardcoded **hospital**-specific default rows ("Sale operacyjne", "SOR"...) with no way
to suppress them — both wrong content for a car showroom and, at ~643mm for that one table alone,
the single biggest contributor to the overflow. Fixed by splitting the 4 tables into 2 columns
(door+window / room+finish-legend) and adding `generate_finish_legend`'s new `includeDefaultRows`
parameter (default `true`, preserving existing callers), set `false` here so the legend carries
only this project's own 3 automotive-specific rows. Confirmed live: both columns now bottom out
at y≈150-177mm, comfortably above the title block's own top edge (y=82mm).

## Room-tag text overlap, root-caused precisely and closed with a permanent, re-runnable gate

A user's own screenshot of the corrected sheet still showed three narrow neighbouring rooms'
NAME text running together illegibly (`Poczekalnia / KawiarniaWC klienciWC dostosowana`,
`Pomieszczenie socjalneWC personeluMagazyn`), with an explicit instruction to fix this for
**every** label in the project and confirm it visually, not just claim it fixed.

**Root cause, confirmed by direct measurement, not guessed**: `get_entity` on every
`A-ROOM-IDEN` text handle showed the room tag's NAME line is left-justified **from the room's
own centroid**, not centred — so a sufficiently long name always reaches rightward into
whichever room sits next door, independent of that neighbour's own tag. The first fix attempt
(shortening only the two longest names, "Poczekalnia / Kawiarnia" → "Poczekalnia" and
"Pomieszczenie socjalne" → "Socjalne") closed the two worst cases but a systematic re-sweep
found the fix was incomplete: `check_overlaps`' `bbox_intersect` mode reported 0 remaining
room-tag overlaps, yet a direct gap measurement found "Biuro administracji 2" ending only
**5.56mm** before "Socjalne" began — 0.06mm on a 1:100-plotted page, visually indistinguishable
from touching, and "WC.C" and "WC.A" (their fixed 4-character room codes, not shortenable) only
**58.5mm** apart. **"Zero flagged overlaps" was not, on its own, a strong enough bar** — a
same-order-of-magnitude lesson to the `RoomRegionSolver` opening-seal bug above: a validator
that answers a narrower question than "is this actually fine" will pass things that aren't.

Fixed properly, not by moving `tagPosition` (which had already reproduced
apartment-120-test's own phantom-room audit bug earlier on this same project — see above):

1. Shortened "Biuro administracji 1"/"2" → both "Administracja" (the room NUMBER, `ADM.1`/
   `ADM.2`, already disambiguates them — the digit in the name was pure repetition), and
   "WC klienci"/"WC personelu" → "Klienci"/"Personelu" (same reasoning: the number already
   says "WC").
2. Widened WC klienci by 200mm (stolen from Poczekalnia, which had slack), shifting its own
   centroid — and therefore all three of its text lines — 100mm further from WC dostosowana's.

**A permanent, re-runnable gate was added to the build script itself** (not just an ad-hoc
check run once by hand): every `A-ROOM-IDEN` pair on the same text row must clear a **150mm
real-world minimum gap** (not merely "does not bbox-intersect"), checked by direct
`get_entity` measurement and enforced with `SystemExit` if violated, plus a re-run of
`audit_all_rooms` confirming the room count is still exactly 14 (the same check that would
catch a `tagPosition`-style regression). Confirmed live end-to-end after every one of these
fixes: 0 room-tag overlaps, 0 pairs under the 150mm floor, 14 rooms — and confirmed **visually**
by exporting and cropping the two previously-affected sheet regions, not by trusting the
measurement alone.

## A fourth tool bug — the layout's own on-screen view, root-caused and fixed at the source

A user's own screenshot of AutoCAD itself (not an export) showed A-101 opening to a badly-fitted
view: lots of wasted grey space above the sheet, more to the right, the plan looking "small" for
a reason unrelated to the viewport-centring fix earlier in this README. Root cause, found through
live testing rather than guessed: **`Editor.SetCurrentView` throws a native `eNullObjectPointer`
whenever a paperspace layout is the active tab** — confirmed by isolating the variable (the
identical call succeeds fine in Model space) across all four `acad.view.zoom_*` tools
(`zoom_extents`, `zoom_window`, `zoom_center`, `zoom_scale`), all of which share this bug.

The first fix attempt (re-syncing `CVPORT`, the current-viewport system variable, on the theory
that `LayoutManager.CurrentLayout`'s own programmatic switch leaves it stale) was a reasoned
attempt, not a guess — and it did **not** work; tested live, same error. Rather than keep
permuting C# changes against a live AutoCAD session, a web search of the Autodesk Community
forums (cross-checked across 2 independent threads) identified the actual missing piece: a
freshly constructed `ViewTableRecord` defaults `IsPaperspaceView` to `false`, and
`SetCurrentView` needs it explicitly `true` when the current viewport is the paperspace
background. Fixed in `ViewPluginTools.cs` (`ConfigureViewForCurrentSpace`, called from all four
zoom tools) — confirmed live: `zoom_extents`/`zoom_window`/`zoom_center`/`zoom_scale` all now
succeed in paperspace with no exception.

One caveat found along the way, left as-is rather than over-fixed: `zoom_extents` itself now
*runs* in paperspace but frames the wrong thing — it reads `Database.Extmin`/`Extmax`, which are
always **model-space** extents, not the paperspace-specific `Pextmin`/`Pextmax`, so calling it
on a layout still produces a nonsensical, oversized window. Not needed for this project (the
sheet's own bounds are known exactly, 841×594mm for A1), so `zoom_window` with those explicit
corners was used instead and added as a permanent build step (right before switching back to
Model space and saving) rather than the buggier `zoom_extents`. The `Extmin`/`Extmax`-vs-space
issue is real but narrower in scope than the crash itself; not spawned as its own follow-up since
`zoom_window` is a complete, correct substitute for any caller who knows what to frame.

Confirmed live end-to-end after a plugin redeploy + AutoCAD restart: `get_current_view` on A-101
reports `centerX=420.5, centerY=297` — exactly the sheet's own true centre (841/2, 594/2) — both
immediately after the build and after a fresh reopen, so the fitted view persists in the saved
file, not just in the live session that created it.

## Verification results

- `audit_all_rooms(cellMm=50, marginMm=700, tolerancePct=10)`: **14/14 rooms**, correct count
  (confirms the `tagPosition` revert above worked). 9/14 flagged `labelMismatch` — the
  `RoomRegionSolver` opening-seal bug described above, verified false (wall-face geometry proven
  correct via direct `get_entity` measurement), not a build defect.
- Criterion 18 (public zone reachable from entry without crossing a private/back zone): **PASS** —
  every door edge from a `REC/WAIT/WC.C/WC.A/HALL/OFF.1/OFF.2` room to an
  `SRV/ADM.1/ADM.2/SOC/STF.WC/MAG` room routes through `COR`, the declared neutral circulation
  zone; no direct public↔back edge exists.
- Criterion 19 (daylight-declared rooms have a window): **PASS** — all 7 daylight-required rooms
  (`HALL, OFF.1, OFF.2, ADM.1, ADM.2, SRV, SOC`) carry ≥1 real window entity, confirmed via
  `list_openings_in_model(kind="windows")`.
- Criterion 20 (built adjacency graph matches this project's own declared table): **PASS** — all
  16 built door edges match the declared table exactly (which itself documents the known
  sales-office↔back-office gap above, rather than claiming a direct connection that isn't there).
- `scripts/verify_construction_readiness.py`: **14 PASS, 0 FAIL, 2 SKIP** (Vision sidecar not
  running this session; no `.ctb` file supplied — both expected, matching apartment/dental).

## Vision review — rule 60 §1 17-criterion scorecard (2026-08-14, Path B)

Scored directly by the driving Claude Code session against the rendered `A-101` export (rule 74
item 9, Path B — the sidecar wasn't running this session).

| # | Criterion | Score | Note |
|---|---|---|---|
| 1 | Wall hatching | 0.5 | All 12 exterior/bearing zones hatched, correctly differentiated `glass` vs opaque — but the opaque material is `steel`, not the more literally-correct `insulation`, due to the confirmed catalog bug above |
| 2 | Furniture | 0.5 | Present and well-matched in 6 of 14 rooms (every room with a genuine preset); Przyjęcie serwisowe/Pomieszczenie socjalne/Magazyn have none — an honest catalog gap |
| 3 | Sanitary fixtures | 1.0 | All 3 WCs fixtured, accessible preset correctly used for WC dostosowana |
| 4 | Doors | 1.0 | 16 doors, jamb+leaf+swing+NUMBER+`LINTEL_TYPE`, visible in the door schedule |
| 5 | Windows | 1.0 | 7 windows, frame+glass+jamb rendered, in the window schedule (new vs. dental-clinic-test, which had none) |
| 6 | Vertical circulation | 1.0 (vacuous) | Single-story showroom, no stair/lift in scope, matches criterion 19 §1a's own vacuous-satisfaction logic |
| 7 | Structural grid | 1.0 | Grid + dimensions present; both axes now show bubble labels (A-E and 1-3) after the viewport-centring fix below — the X-axis bubbles sat outside the frame of the earlier, badly-centred viewport, not actually missing from the drawing |
| 8 | Dimensioning | 0.5 | Chain + 3 linear dimensions present and correct; not on all 4 sides |
| 9 | Schedules | 0.5 | Door/window/room schedules present as real paperspace Tables, now correctly fitted within the sheet in 2 columns (see the viewport/schedule-overflow fix below) — but the room schedule's own area column still inherits the `RoomRegionSolver` under-measurement bug for 9 of 14 rows, a real visible defect in the deliverable |
| 10 | Callouts | 0.5 | Title block (real project metadata) + north arrow + scale bar present; no separate profile/detail callout leaders |
| 11 | Section lines | 1.0 | 1 section (A-A) through the hall/corridor/rear wing |
| 12 | Lineweight/CTB | 0.0 | No `.ctb` supplied (rule 61 §3, opt-in, documented) |
| 13 | Finishes legend | 1.0 | `generate_finish_legend` called with 3 typology-specific extra rows (polished concrete floor, painted plaster, suspended ceiling) — new vs. dental-clinic-test, which skipped this entirely |
| 14 | Orientation + scale | 1.0 | North arrow + scale bar present, model-space placement (same accepted convention as apartment/dental) |
| 15 | RCP (optional) | 0.0 | Not built — explicitly optional per the rubric |
| 16 | Jamb/sill/lintel blow-ups | 0.0 | No `DET-XX` detail viewports |
| 17 | Room program fidelity | 1.0 | All 14 declared rooms present, correctly labelled, areas within the typical ranges this typology's own `ROOM-PROGRAM.md` cites |

**Score: 11.5 / 17 — "technical study" band (10-13/17): OK for internal review, NOT
tender/pozwolenie-ready.** (Up from an initial 11.0 after the viewport-centring fix below made
criterion 7's grid bubbles fully visible.) `fatal_gaps` (score 0): CTB/lineweight (12), RCP (15,
optional), detail blow-ups (16). Scored honestly rather than rounded up — this is a single-pass
build of a structurally more complex typology than apartment/dental (one big irregular hall
instead of a grid of similar rooms), not a multi-round-polished result; the real tool bugs found
along the way (hatch pattern, opening-seal, viewport centring, schedule overflow) cost real
points here (criteria 1, 7, 9) that a bug-free tool chain would not have — and, unlike the first
two, were caught not by this session's own checks but by the user's own review of the actual
exported sheet, which is exactly the kind of defect an automated PASS/FAIL gate cannot catch on
its own.

## Files

- `AutomotiveShowroomTest.dwg` — the built drawing.
- `../../scripts/build_automotive_showroom_test.py` — the build script (re-runnable; starts from
  `new_document`).
- `automotive-showroom-test-A101.png` (not tracked here — see `artifacts/architect-review/`) — the
  rendered A-101 sheet export used for the visual verification above.
