# Automotive showroom — structural grid & layer conventions

## Typical structural grid

The display hall is the opposite design driver from residential's tight, daylight-capped bay
(GRID-AND-LAYERS.md residential) or hospital's OR-driven 7.8×8.4m bay — it needs the **largest
practical column-free clear span**, because interior columns break vehicle sightlines and
constrain how cars can be arranged/rotated on the floor. Steel portal-frame construction is the
dominant system found in every source researched this session.

- **Typical clear span for the exhibition hall: ~18-24 m**, single or double bay, as a reasonable
  default for a mid-size dealership showroom. **Probable** — this is a synthesis of real-project
  figures found this session, not a single authoritative source:
  - A documented JLR dealership showroom used **two 21 m-wide portal-frame spans** with a central
    row of prop columns forming a 2 m corridor (i.e. ~44 m total width across two 21 m bays) —
    (newsteelconstruction.com).
  - The same source's BMW/MINI workshop portion used **two 20 m-long spans**.
  - General steel-construction industry sources (showhoobuilding.com) claim clear spans of
    **30-60 m** are achievable with heavier steel trusses for larger flagship showrooms — an
    upper bound, not a typical default.
  - A multi-storey BMW showroom (with office/mezzanine floors above the ground-floor hall) used a
    much tighter **6 m × 8 m column grid** for the storeyed portion — this figure applies to the
    ancillary multi-storey volume (offices/mezzanine), NOT the column-free single-storey hall
    itself; don't conflate the two.
- **Driven by:** unobstructed sightlines to displayed vehicles from the street frontage and from
  within the hall, plus large-vehicle turning/repositioning clearance — not room depth or daylight
  penetration the way residential's grid is, and not a fixed clinical-equipment footprint the way
  hospital's OR bay is.
- **No specific project drove this figure this session** — treat the 18-24 m default as a
  reasonable starting point pending a real showroom reference drawing, same caveat residential's
  and office's grid files already carry for their own placeholder figures. Given this typology's
  primary constraint (large clear span) is architecturally more consequential than residential's
  or office's grid choice, this default should be confirmed against an actual structural
  engineer's design or a real reference drawing before being treated as load-bearing for anything
  beyond a demonstration project.
- **Sales/back-office wing**, if built as a conventional smaller-bay structure attached to the
  hall (rather than continuing the hall's long-span frame over the offices), can reasonably reuse
  the office typology's own grid default (~7.2-8.4 m) — itself already flagged as an unverified
  placeholder there.

## This tool bank's own CAD layer key (unchanged, do not fork per typology)

Same AIA-style key as every other typology in this bank (`A-WALL`, `A-ROOM-BNDY`, `A-DOOR`, ...).
No new layer constant is being added by this documentation pass — this file is research/
documentation only, not a code change.

- **Possible future extension, documented but NOT implemented here:** the exhibition hall's
  glazed street-facing curtain-wall frontage (see AREA-CONVENTION.md) is a structurally and
  visually distinct wall type from the hall's opaque envelope and from interior partitions. If a
  future pass finds a real, checkable need to distinguish it (e.g. a validator rule about
  minimum glazed-frontage extent, or a rendering/schedule reason to separate it), the existing
  precedent is hospital's `A-WALL-LEAD` / `A-WALL-FARA` pattern — a typology-prefixed **extension**
  of the existing `A-WALL` layer (e.g. an `A-WALL-GLZ` naming convention), not a wholesale
  replacement. **This is a documentation note only — do not add an `A-WALL-GLZ` constant to
  catalog/layer code as part of this pass**; nothing researched this session established an actual
  checkable requirement that would justify it, matching the same restraint office's and
  residential's GRID-AND-LAYERS.md files already showed (no new layer added there either, for the
  same reason: nothing researched required one).

## Reading a REAL showroom reference drawing (if supplied)

No real automotive-showroom drawing has been supplied to this repo yet. When one is: extract grid,
layer convention, and program the same way `docs/knowledge-base/hospital/GRID-AND-LAYERS.md` did
for the real outpatient-clinic drawing — via `acad.layers.list_layers` +
`acad.annotations.list_text_by_pattern` — and record the cross-reference table here.

| Real drawing's layer name (example seen) | Meaning | Maps to this bank's layer |
|---|---|---|
| *(none yet — no real drawing supplied for this typology)* | | |

## Sourcing note

The clear-span figures are web-research industry defaults synthesized from a handful of real
built-project descriptions (newsteelconstruction.com) plus general steel-construction marketing
material (showhoobuilding.com) — not derived from a real showroom reference drawing supplied to
this repo, and not a code-mandated figure (WT does not set a structural span requirement). This is
the weakest-grounded claim in this typology's four files in the sense that it's a structural-
engineering default rather than a legal citation, but it is better-corroborated than residential's
and office's own placeholder grid figures (which had zero real-project data behind them) — flagged
as **Probable**, not **Confirmed**, throughout.
