# 71. Project intake protocol — ground every new building project before drawing

Mandatory sequence before the FIRST `draw_wall` / `draw_walls_chain` / `define_room` call of a
new building project (any typology: residential, hospital, office, industrial, ...). Triggered by
rule 53 §"Triggers" #4 (same kind of mistake made repeatedly in one session) and #7 (the user
pointed out the mistake) — see the "What went wrong" section below for the incident.

## The six steps, in order

1. **Identify the typology.** Residential / hospital / office / industrial / other. Determines
   which `docs/knowledge-base/<typology>/` folder and which validator standard bundle apply.

2. **Check the grounding source, before inventing anything.**
   - If the user supplied a real reference drawing: open it and extract the structural grid
     (axis positions/spacing), the real room program (zone/room labels), and the layer
     convention it actually uses — via `acad.validators.collect_entities` filtered by layer/type,
     the same method documented in `docs/HOSPITAL-2026-GEOMETRY-TARGET.md`. Do not paraphrase
     the drawing from memory after a first look; re-query it for numbers.
   - If no reference drawing exists: read `docs/knowledge-base/<typology>/`. If it's missing or
     thin, research it (web search against authoritative sources + any existing project docs)
     and **write the findings into the knowledge base before proceeding** — a room program or
     a code citation invented from general knowledge and never written down gets re-invented
     differently next time, and can't be checked by anyone.

3. **Declare the area-measurement convention explicitly, before the first `define_room` call.**
   Net internal (to face-of-finish) or gross (to structural axis) — pick one, per
   `docs/knowledge-base/<typology>/AREA-CONVENTION.md` (PN-ISO 9836 for Polish projects).
   `define_room`'s boundary vertices must already be inset from the wall centreline to match
   that convention. Fixing this after the fact means resyncing every room's boundary polygon
   AND re-checking every areas-based validator rule against the corrected geometry — see
   "What went wrong" below for how expensive that got.

4. **Structural grid first.** Fix axis coordinates before any wall exists. Every wall endpoint
   lands on a grid line or a documented fixed offset from one — never an accumulated running
   total of neighbouring room widths with no shared skeleton. Once the grid is fixed, size
   columns/beams from `acad-structural` (`list_steel_profiles`, `insert_steel_column`,
   `insert_beam`) rather than a generic rectangle when a real profile matters — see rule 72.
   Lintels over openings (`insert_lintel`, rule 72 §8) are heuristic sizing, not a structural
   calculation; say so wherever the result is surfaced.

5. **Adjacency / bubble diagram before detailed geometry.** List every zone/department and
   confirm the adjacency graph is connected — every zone reachable from every other, generally
   through at least one real door, before elaborating any single zone in detail. This is the one
   check that would have caught the connectivity gap in one query instead of after ~130 rooms
   existed.

6. **Verification parameter discipline.** `audit_all_rooms`'s default `marginMm` (250mm) can be
   too tight for doors placed on a wall centreline rather than a face — pick and justify a value
   for the drawing's own convention, don't take the default on faith. And check any *own*
   verification script against the tool's actual response schema (real field names, e.g. a
   `flags: string[]` array, not assumed top-level booleans) before trusting what it reports —
   a verification script is code too, and it fails silently in the "everything looks fine"
   direction, which is the worst direction to fail in.

## What went wrong (the incident this rule codifies)

Building a hospital ground floor from scratch, entirely from an invented room-program document
(no real reference drawing, no grid discipline, no area-convention decision):

- `define_room` boundaries were drawn on wall **centrelines**; the flood-fill measurement
  (used by `audit_all_rooms` / `get_room_data`) measures to the wall **face**. Declared vs.
  measured area diverged by 15–25% across ~90 rooms, undetected until a `correct_all_room_areas`
  pass was run near the end — one operating room came out physically below its own code minimum
  once measured honestly, and had to be widened by stealing space from a neighbouring room.
- Four "bands" (sub-corridors) inside one zone were built with different total widths, sharing
  a boundary wall drawn to only the narrower band's extent — leaving a real, un-walled gap in
  the building envelope for the wider band's excess width. `audit_all_rooms` correctly flagged
  the resulting flood-fill leak once actually checked — but it took a second, correct pass to
  notice, because:
- The build's own verification script checked `row.get("leakSuspected")` /
  `row.get("emptyOpenings")` as top-level fields. The real schema puts them inside a
  `flags: string[]` array. Every check silently evaluated to "no problem" — 4 zones were
  reported "0 leaks" when the true count, once the bug was found, was dozens of flagged rows.
- Five zones were each built as an independently closed block with no connecting doors between
  them, discovered only when the user asked "did you check whether the floors connect to each
  other?" after the ground floor was substantially complete.
- The user then supplied a real, licensed reference DWG for comparison (39,823 entities, a real
  numbered structural grid, three separate wall-type layers, a real department program) — none
  of which the invented drawing had used or checked against, because nothing in this tool bank's
  process required checking a real reference before drawing.

## Why this rule exists

Every individual defect above was independently fixable and got fixed. The actual failure was
upstream of all of them: nothing in the process forced grounding, a grid, an area convention, or
a connectivity check before geometry accumulated to the point where finding out was expensive.
Steps 1-6 above are cheap, ordered, and each one is exactly the check that would have caught one
of these for a fraction of the cost of finding it 100 rooms later.
