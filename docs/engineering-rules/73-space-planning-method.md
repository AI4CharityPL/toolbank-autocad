# 73. Space-planning method — from zoning to walls, for every typology

The mandatory 9-step sequence for turning a functional programme into a floor plan, for ANY
building typology and ANY scale. READ BEFORE the first `draw_wall` / `draw_walls_chain` /
`define_room` call of a new project — this is the step that sits between rule 71 step 2
(grounding the programme) and rule 71 step 4 (grid before walls), and it did not exist when the
Zone-0.2 test build produced a "kulfon."

## Why this rule exists

Every build in this bank's history up to and including the Zone-0.2 Administration test
(11 rooms from the reference drawing's own zone-stamp names) used the same layout algorithm: line
up room widths left-to-right along a single corridor, in programme order, done. It is mechanically
correct — walls close, doors connect, `audit_all_rooms` comes back clean — and it is
architecturally poor: no day/night (or public/private) split, no relationship to entry or
daylight, room WIDTHS chosen arbitrarily rather than from what has to fit inside. The user's own
words: *"nasz system... nie [ma] tworzyć takie kulfoy"* (our system should not produce these
crude/clunky things).

Two Explore-agent investigations, run specifically to check whether this was a tool-bank gap or a
process gap, found it is a **process gap**:

- `acad-grids.draw_grid` already accepts arbitrary, non-uniform bay spacing in BOTH axes
  (`xSpacingsMm`/`ySpacingsMm`, each an independent array — see `GridsDtos.cs`'s `DrawGridArgs`).
  A real, non-linear 2D structural grid was always possible; nothing in this bank's history had
  ever called it with more than a single uniform spacing in one direction.
- `acad-architecture.define_room` already accepts `boundaryLayer`/`tagLayer` overrides
  (`ArchitectureDtos.cs`). Nothing stops using it to represent a ZONE rather than a room — see
  §9 below.
- Rule 60's 17-criterion rubric scores drafting-standard compliance (hatching, dimension chains,
  schedules...) — it has no criterion that would catch a single-row layout with no zoning logic.
  A "kulfon" can score 17/17. Rule 60 §1a (criteria 18-20) closes that specific gap; this rule is
  the process that produces a layout that PASSES those criteria in the first place, not just a
  checklist that catches it after the fact.

## The 9 steps, in order

Steps 1-2 and 4 (partially) already exist elsewhere in this bank's process; they are listed here
for completeness because skipping the ORDER is exactly what produced the kulfon, even though each
individual step already existed.

### 1. Functional programme + zone list

Already covered by rule 71 step 2 + the typology's `docs/knowledge-base/<typology>/ROOM-PROGRAM.md`.
Every room has a zone tag (which functional group it belongs to — public/day, private/night,
back-of-house, ...) before this step is considered done. A room with no zone tag cannot be placed
in step 3.

### 2. Zone adjacency diagram (bubble diagram)

Already exists as the "Adjacency" table in `ROOM-PROGRAM.md`, but until this rule it was only used
to check connectivity AFTER geometry existed (rule 71 step 5), never to DERIVE the 2D layout.
Read it BEFORE step 3, not just to verify AFTER step 6. If two zones are marked "MUST be directly
connected," their footprints in step 3 must actually be adjacent, or step 3's placement is wrong
and needs redoing before any wall exists — cheap now, expensive after step 6.

### 3. 2D zone placement relative to entry and daylight

The step this bank has always skipped, collapsing straight to a single row. For each zone:

1. Compute an approximate footprint (width × depth) from the sum of its rooms' typical areas —
   a rough rectangle, not yet room-by-room.
2. Place **public/day zones** near the entry and the facade with the best daylight exposure
   (south-facing in the northern hemisphere, absent a stated site orientation).
3. Place **private/night zones** deeper in the plan, separated from the public/day zone by a
   short buffer corridor or hall — not immediately adjacent, and not on the direct entry path.
4. Zones that must connect (per step 2) share a boundary or a short direct corridor segment;
   zones with no adjacency requirement are free to be laid out in whichever arrangement keeps the
   footprint compact.

This is the step that turns "11 rooms in a row" into an actual plan — a day cluster, a night
cluster, a real corridor topology, not a hallway with doors on one side.

**Step 3a — draw the zone as an entity, MANDATORY, not optional (rule 74 C.3).** Every zone
footprint fixed in step 3 gets a real `define_room` call using `boundaryLayer="A-ZONE-BNDY"` /
`tagLayer="A-ZONE-IDEN"` (see "Zone as an entity" below for the exact pattern) — this is no
longer a nice-to-have "you COULD do this" note, it is a required deliverable of step 3. A build
that has zone footprints only on paper (in the agent's own working numbers) and never draws them
as a queryable entity has not actually finished step 3, even if step 6's walls end up in the
right place — a future `check_overlaps`/audit pass, or a different agent picking up the project
later, has nothing to query for "which zone is this room in" without it.

### 4. Structural grid fitted to zone boundaries

`acad-grids.draw_grid` with `xSpacingsMm`/`ySpacingsMm` arrays matched to the zone footprints from
step 3 — non-uniform bay spacing where the zone boundaries call for it, not a uniform grid
overlaid after the fact and hoped to line up. This is rule 71 step 4, made concrete: fix the grid
from step 3's zone geometry, not from an arbitrary default bay size.

### 5. Room size derived from furniture, before wall placement

For every room, look up its minimum size from the (corrected) preset table in
`docs/engineering-rules/64-furniture-density-per-room.md` §6 for the closest matching
`populate_room` preset, BEFORE deciding the room's wall dimensions. A room sized first and
furnished second routinely comes out too small for its own furniture — checking the preset
minimum first is one table lookup, checking it after wall placement means moving a wall.

### 6. Detailed walls and openings

Only now, with steps 3-5 fixed: exact wall coordinates from the grid (step 4) and zone/room
boundaries (steps 3+5), doors placed per the adjacency requirements from step 2.

### 7. Structural elements at grid intersections

`acad-structural` (`insert_steel_column`, `insert_beam`, `insert_lintel` — rule 72) at the
intersections fixed in step 4. A lintel over every opening from step 6, tagged onto that opening's
own `LINTEL_TYPE` attribute (rule 72 §6 sizing-then-recording split).

### 8. Furniture placement with fit verification

`populate_room` / `populate_bathroom` per rule 64. After placement, confirm the actual room
dimensions meet the preset minimum checked in step 5 — step 5 is a lookup before geometry exists,
this is the closing check that the geometry that got built still matches it (a wall moved for an
unrelated reason between step 5 and step 8 would otherwise go unnoticed).

### 9. Zoning-quality verification

Rule 60 §1a, criteria 18-20 (public-zone entry access, daylight-declared rooms actually on an
exterior wall with a window, built adjacency graph matches the declared table) — checked live
against the finished drawing, not assumed from having "followed the steps." Alongside
`audit_all_rooms` (rule 71 step 6: read the actual `flags: string[]` array, not assumed top-level
booleans).

**Logical checks are necessary but not sufficient — run a geometric coordination pass too.**
Criteria 18-20 and `audit_all_rooms` both operate on *declared* data (room numbers, labels,
adjacency) — none of them notice that a structural column, a window, a door swing and an
auto-placed plumbing fixture were each positioned correctly *on their own* but collide with each
other, because each was placed by a different tool call that has no knowledge of the others. Both
proof builds in this rule's own history passed every criteria-18-20 check and every
`audit_all_rooms` flag was already explained, and STILL shipped a structural column punched
through a window, three columns floating outside the building envelope, and three doors swinging
straight into WC fixtures — found only when `acad.validators.check_overlaps` was run afterward
across every cross-category pair that independently-placed elements can plausibly collide on. Run
it as part of step 9, not as an afterthought once a user asks "did you actually check this":

```python
for layersA, layersB in [
    (["S-COLS"], ["A-GLAZ"]),                                    # structural vs windows
    (["S-COLS"], ["A-DOOR"]),                                    # structural vs doors
    (["S-COLS"], ["A-FURN-*", "A-PLMB-*"]),                      # structural vs furniture/fixtures
    (["A-DOOR"], ["A-PLMB-WC", "A-PLMB-BSN", "A-PLMB-BT", "A-PLMB-SHW"]),  # door swing vs fixtures
    (["A-DOOR"], ["A-FURN-*"]),                                  # door swing vs furniture
]:
    check_overlaps(layersA=layersA, layersB=layersB, mode="bbox_intersect")
```

Fixing one collision can create another — moving a door clear of a column can put it in a sofa's
footprint; rotating a bathroom fixture cluster clear of a column (`populate_bathroom`'s own
`orientation` param, rule 64 §8, is the right lever for this — cheaper than hand-shifting room
geometry) can put a different fixture in the door's swing path. Re-run the full check-overlaps
battery after EVERY fix, not just the one pair that was failing — both proof builds needed two to
three iterations of fix-then-recheck before every pair came back clean, exactly the pattern this
whole rule is built around: check before declaring done, not after being asked to.

## Zone as an entity — MANDATORY (step 3a), no new tool needed (see also rule 72 §3 for the boundary)

**Updated 2026-08-13 (rule 74 C.3): this is a required deliverable of step 3, not an optional
pattern a build may or may not use.** The apartment-120-test and dental-clinic-test proof builds
(this rule's own §"Real defects found live" history) skipped it entirely — every zone existed
only as coordinates in the build script, never as a drawing entity — and nothing caught that,
because until now this section only said a zone COULD be represented this way.

A "zone" is not a new concept for the tool bank to implement — reuse `define_room` with
`boundaryLayer="A-ZONE-BNDY"` and `tagLayer="A-ZONE-IDEN"` to draw a zone's own boundary/label,
distinct from the per-room contract (`A-ROOM-BNDY`/`A-ROOM-IDEN`, the defaults). This gives step 3
a real, queryable entity in the drawing (a zone boundary polygon + a zone label) without adding a
sixth tool to `acad-architecture`. Do not reuse `A-ROOM-BNDY` for a zone boundary — mixing the two
on one layer breaks any future validator that counts rooms by scanning that layer, since a zone
boundary is not a room.

```python
call("architecture", "define_room", {
    "vertices": zone_footprint_vertices,   # step 3's approximate zone rectangle
    "number": "ZONE-DAY", "name": "Strefa dzienna",
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
})
```

## `audit_all_rooms` and small zone-adjacent entities: a known false positive

`audit_all_rooms`'s flood-fill measurement falls back to a `"raycast"` method for very small
polygons (~1.6m² and below observed) — including a zone boundary drawn as a thin buffer strip, or
a genuinely tiny room. The audit's own logic flags `leakSuspected` on ANY row whose method is not
`"flood"`, even when the raycast measurement matches the declared area exactly (0% delta observed
in the Zone-0.2 test). Read the row's OWN `deltaPct`/measured-vs-declared numbers before treating
a `leakSuspected` flag on a small polygon as a real leak — it may just be the fallback path,
correctly measuring a small shape by a different method. This is a real `audit_all_rooms`
usability gap (conflating "couldn't flood-fill, used a fallback" with "this room is leaking"), not
yet fixed in the tool itself — noted here so it isn't mistaken for a defect in the build.

## `audit_all_rooms`: the opening-sealing disc shrinks measured area near every door/window, root-caused

Found and root-caused during the apartment-120-test build (rule 73's own C.1 proof), this was the
"not yet root-caused ~12%" residual mismatch flagged as an open question in the Zone-0.2 test.
`RoomRegionSolver.SolveFlood` blocks a DISC around every opening centre before flood-filling, so
the fill cannot leak through the doorway gap into the neighbouring room:

```
r = max(widthMm * 0.5 + 1.5 * cellMm, 2.0 * cellMm)
```

This disc is centred on the opening's position — which sits on the WALL CENTRELINE, i.e. already
at the edge of the room's own net-internal boundary — and its radius (e.g. 525mm for a 900mm
door, 1575mm for a 3000mm window, at `cellMm=50`) bites INTO the room interior from there. Two
separate, both-legitimate consequences follow, and neither means the build is wrong:

1. **`measuredAreaM2` comes out systematically smaller than a correctly-drawn net-internal
   `labelAreaM2`**, worse for small rooms with several/wide openings relative to their footprint
   (a corridor with 6 doors along a 1200mm-deep run lost ~18.5% in the apartment-120-test build;
   a bathroom with 1 door in a deeper room lost only ~8.5%, under the default 10% tolerance).
   This is the flood-fill being conservative about doorway space, not a construction defect — do
   not "fix" it by fudging the declared boundary to chase the flood-fill number down; the boundary
   is correct per the area convention, the measurement is just conservative near openings.
2. **The opening's own centre point can end up MORE than a small `marginMm` away from the
   (now-notched) traced outline**, since it sits at the middle of the very disc that was carved
   out of the fill. `InsideOrNearBoundary` then reports doorCount/windowCount **0** for a room that
   visibly has doors in the drawing, and the audit adds a false `emptyOpenings` flag on top of the
   real `labelMismatch`. Confirmed live: `marginMm=400` produced 0/9 rooms with any detected
   opening; raising it to `marginMm=1700` (safely above `1500+1.5*cellMm` for the widest 3000mm
   window in that build) produced correct door/window counts on every room and dropped every
   `emptyOpenings` flag, leaving only the (separately explained) `labelMismatch` rows.

**Practical rule:** before calling `audit_all_rooms`, compute `marginMm` from your own widest
opening (`max(widthMm) * 0.5 + 1.5 * cellMm`, plus slack) rather than reusing rule 71's already-
noted "250mm is often too tight" guidance verbatim — 250mm and even 1000mm (the Zone-0.2 test's
choice) are both too small once a wide window is in the same room. A `labelMismatch` flag that
remains after fixing `marginMm` is expected and explainable exactly like this, not a defect to
chase to zero — read `doorCount`/`windowCount` alongside `deltaPct` before deciding which.

## Relation to other rules

- Rule **71** (project intake protocol): steps 1-2 of THIS rule are rule 71 step 2's continuation;
  step 4 of this rule is rule 71 step 4 made concrete (grid fitted to zones, not a generic
  default); step 9 of this rule feeds rule 71 step 6's verification discipline.
- Rule **60 §1a** (criteria 18-20): the exit gate this rule's step 9 runs.
- Rule **64** (furniture density): the preset table this rule's step 5 and step 8 both check
  against.
- Rule **72** (acad-structural domain traps): the tools this rule's step 7 calls.
- Rule **36**: per-room `A-ROOM-BNDY`/`A-ROOM-IDEN` contract — the zone-entity pattern above
  deliberately does NOT touch that rule, using its own `A-ZONE-*` pair instead, so the two
  concepts (room vs. zone) never collide on one layer.
