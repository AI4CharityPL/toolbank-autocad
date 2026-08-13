# Airport terminal — area-measurement convention

## Decision

- **Convention: net internal area (to face-of-finish)**, the same convention applied to every
  other typology in this bank (hospital, residential, office) — kept consistent here for the same
  reason: `define_room` boundary polygons, `audit_all_rooms` flood-fill measurement, and any future
  validator rule all need to agree on one convention across the whole tool bank, not per typology.
- **Citation:** PN-ISO 9836 for the general Polish net/gross area definition — the same citation
  the residential and hospital typologies use. **No terminal-specific area-convention source was
  found or searched for with any real effort this session** — IATA ADRM's LoS m²/passenger figures
  (see `STANDARDS.md`) are themselves ambiguous about net-vs-gross in the secondary sources
  consulted; ACRP/National-Academies planning literature is not a Polish or ICAO normative source
  for this decision either way.

**This is the lowest-confidence application of the net-area convention in this bank.** Every other
typology's convention decision is anchored to a specific code provision that uses the term
"powierzchnia użytkowa" (residential §94, or the hospital regulation's own area language). No
equivalent terminal-specific provision was found here — this file applies the repo-wide default
for consistency, not because a terminal-specific source was confirmed to require it.

## Practical consequence for `define_room`

- Wall thickness assumption: no terminal-specific structural/partition system was researched this
  session. Given the very large clear spans typical of terminal concourses (see
  `GRID-AND-LAYERS.md`), primary structure is likely steel/composite columns rather than
  load-bearing masonry — informally, **treat perimeter/structural walls similarly to the office
  typology's assumption (curtain wall / steel frame, not thick load-bearing masonry) until a real
  project confirms an actual `draw_wall(thicknessMm=...)` value.**
- `define_room`'s boundary polygon vertices must be drawn **inset by half the wall thickness from
  the wall centreline**, matching every other typology in this bank — not at the centreline.
- Any future validator rule checking a minimum area for this typology would be checking the
  boundary polygon's own area, not the flood-fill measurement — same mechanical caveat as every
  other typology's `AREA-CONVENTION.md` in this bank. Moot for now: **no terminal-specific
  area-minimum validator rule exists or is recommended yet**, because `STANDARDS.md` found no
  citable minimum to check against — see Validator interaction below.

## Validator interaction

This typology currently has **no area-based validator rule** in
`validators/_standards/` (no `airport-baseline.yaml` exists as of this pass). Given that
`STANDARDS.md` found zero confirmed per-room area minimums for a terminal building — every
candidate figure is either a generic ZL-classification/evacuation rule (unrelated to room area) or
an IATA LoS *planning target*, not an enforceable minimum — building an `area_at_least` validator
rule for this typology now would mean encoding an industry-typical planning figure as if it were a
code requirement, exactly the failure mode this knowledge base exists to prevent (see the README's
incident). **Do not add an airport area-minimum validator rule until a real citable source is
found**; if one is added anyway using the "industry-typical" figures in `ROOM-PROGRAM.md`, its
`description` must say explicitly that it enforces a planning convention, not a code minimum.

## Sourcing note

The net-area convention decision here is a **consistency choice, not an independently re-derived
one** — it follows the rest of this bank's pattern rather than a terminal-specific citation, because
no terminal-specific area-definition source was found. Flagged as a gap per this typology's overall
honesty framing (see `STANDARDS.md`'s opening note): if a real terminal reference drawing or a
primary ICAO/IATA area-definition source becomes available, re-derive this decision from it rather
than treating the repo-wide default as confirmed for this typology.
