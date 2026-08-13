# Airport terminal — structural grid & layer conventions

Scope note: per the task that produced this file, this pass is **program/zone/grid level only,
not a full wall build** — kept deliberately light. It records a default structural bay to reach
for if a massing/zoning exercise needs one, and confirms the layer key needs no typology-specific
extension. It is not a structural design document and should not be read as engineering guidance.

## Typical structural grid

Airport terminals are the largest-span typology in this knowledge base by a wide margin — driven
by the need for column-free gate holdrooms, departure halls, and concourse spines where columns
would obstruct sightlines, queuing, or aircraft-adjacent operations.

- **Departure hall / concourse spine / large public halls:** wide-span steel truss or space-frame
  roof structures. Web research this session found real built examples ranging from ~18-45 m
  standard truss spans up to 100+ m for landmark/hub terminals (one cited example: 36 m column
  grid with a 108 m × 216 m overall span using concrete-filled steel columns) — **industry-typical
  figures from built-project reporting, not a code-mandated span.** For a small-to-medium regional
  terminal (the scope this knowledge base folder targets), a **~18-30 m clear-span bay** for the
  main public halls is a reasonable order-of-magnitude default — an order of magnitude larger than
  this bank's other typologies (hospital ~7.8×8.4 m, office comparable, residential ~3.0-6.0 m).
- **Gate piers / holdroom wings:** typically a tighter secondary grid than the main hall, closer to
  the office typology's bay spacing, since holdrooms are subdivided spaces rather than one
  continuous column-free volume.
- **Back-of-house (baggage handling, back-office, staff areas):** conventional smaller-bay
  structure, similar order of magnitude to the office typology — no large-span driver applies here.
- **No specific project drove any of these figures this session** — they are secondary-source
  reporting on built examples, not a cited design standard. **Treat as a placeholder pending a real
  terminal reference drawing**, per rule 71 step 2, exactly as the residential typology's grid file
  already flags for its own (much smaller-span) default.

## This tool bank's own CAD layer key (unchanged, no new layer needed this pass)

Same AIA-style key as every other typology in this bank (`A-WALL`, `A-ROOM-BNDY`, `A-DOOR`, ...).
**No airport-specific layer extension is introduced in this pass** — this file stays at
program/zone/grid scope only, deliberately not attempting a full wall build that might surface a
real need (e.g. a curtain-wall-specific layer, or an airside/landside security-line annotation
layer) before one is actually justified by real geometry work. If a future pass does full wall
construction for a terminal project and finds a genuine gap in the existing key, extend it the
same way hospital's `A-WALL-LEAD`/`A-WALL-FARA` extended the base key — a typology-prefixed
addition, not a fork (rule 02).

## Reading a REAL reference drawing in this typology

No real airport terminal drawing has been supplied to this repo yet. When one is: extract grid,
layer convention, and zone program the same way `docs/knowledge-base/hospital/GRID-AND-LAYERS.md`
did for the real outpatient-clinic drawing — via `acad.layers.list_layers` +
`acad.annotations.list_text_by_pattern` — and record the cross-reference table here.

| Real drawing's layer name (example seen) | Meaning | Maps to this bank's layer |
|---|---|---|
| — none seen yet — | — | — |

## Sourcing note

This is the weakest-grounded `GRID-AND-LAYERS.md` in the bank, by construction: no real terminal
reference drawing exists, and the span figures above come from generic secondary reporting on
built airport-terminal structures (used only to establish the right *order of magnitude* — "much
larger than any other typology here" — not a specific bay dimension to build to). Flagged
explicitly rather than presented with false confidence, matching this typology's overall honesty
framing in `STANDARDS.md`.
