# Residential (multi-family apartment building) — structural grid & layer conventions

## Typical structural grid

Multi-family residential buildings typically use a tighter bay than institutional buildings —
driven by apartment depth (habitable-room daylight requirement, WT §93, caps how deep a room
can be from its window wall) and party-wall/shear-wall spacing between units, not by large clear
spans the way an OR or an open office floor plate needs.

- Typical bay: **~3.0-6.0 m** structural spacing between load-bearing walls (much smaller than
  the hospital typology's 7.8×8.4m bay) — no specific project drove this figure this session; it
  is a general order-of-magnitude default, not a cited requirement. **Treat as a placeholder
  until a real residential reference drawing is available to derive it from** (rule 71 step 2).
- Habitable-room depth from the window wall is implicitly capped by the daylight requirement
  (§93) — deep, unlit rooms are non-compliant regardless of grid.

## This tool bank's own CAD layer key (unchanged)

Same AIA-style key as every other typology in this bank (`A-WALL`, `A-ROOM-BNDY`, `A-DOOR`, ...).
No residential-specific layer extensions exist yet (unlike hospital's `A-WALL-LEAD`/`A-WALL-FARA`)
because nothing researched this session required one — Polish residential WT doesn't have a
shielding-layer-equivalent requirement.

## Reading a REAL residential reference drawing

No real residential drawing has been supplied to this repo yet. When one is: extract grid,
layer convention, and unit-mix program the same way `docs/knowledge-base/hospital/GRID-AND-LAYERS.md`
did for the real outpatient-clinic drawing — via `acad.layers.list_layers` +
`acad.annotations.list_text_by_pattern`, and record the cross-reference table here.

## Sourcing note

This file is the weakest-grounded of the four residential knowledge-base files — no real
reference drawing exists for this typology yet, so the grid figure above is a placeholder, not a
researched default. Flagged explicitly rather than presented with false confidence.
