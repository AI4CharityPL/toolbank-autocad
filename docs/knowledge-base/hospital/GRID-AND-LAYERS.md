# Hospital — structural grid & layer conventions

## Typical structural grid

- Bay: **7.8 × 8.4 m** (`docs/HOSPITAL-2026-MASTERPLAN.md` — chosen for panelized demountable
  partition compatibility, an explicit 2026 trend the masterplan cites).
- Footprint: 80 × 60 m gross (~4,800 m²), with an 18 × 12 m internal courtyard.
- Building height class: **N (low-rise), H ≤ 12 m** — constrains the whole compliance posture
  (see `STANDARDS.md`); do not exceed this without re-deriving the fire-resistance-class
  requirement.

## This tool bank's own CAD layer key (unchanged)

Standard AIA-style key already used throughout this bank: `A-WALL`, `A-WALL-CTRL`, `A-ROOM-BNDY`,
`A-ROOM-IDEN`, `A-DOOR`, dozens of validator rules assume it (rule 02: no breaking changes).
Hospital-specific extensions stay prefix-based on top of it: `A-WALL-LEAD` (radiation shielding),
`A-WALL-FARA` (MRI Faraday cage) — see `hospital.walls.lead-shield-on-layer` /
`hospital.walls.faraday-on-layer` in `validators/architectural/`.

## Reading a REAL hospital/clinic reference drawing

The user supplied a real, licensed outpatient-clinic drawing
(`[a real, licensed reference DWG the user supplied]`, ArchiCAD export, 39,823 entities, 228 layers, 2,334
dimension entities) — extracted live via `acad.layers.list_layers` and
`acad.annotations.list_text_by_pattern`. It uses a Polish multi-branża numbered layer key, NOT
this bank's AIA-style key. Cross-reference so a future real-drawing import doesn't require
re-discovering this mapping:

| Real drawing's layer (pattern seen) | Meaning | Maps to this bank's layer |
|---|---|---|
| `<n>220-A Ściany Zewnętrzne` | exterior walls | `A-WALL` (exterior-flagged) |
| `<n>221-A Ściany Wewnętrzne` | interior/structural walls | `A-WALL` (interior, structural role) |
| `<n>230-A Ściany działowe` | partition walls | `A-WALL` (interior, non-structural — this bank does not yet distinguish structural vs. partition walls by layer; see gap note below) |
| `<n>210-A Strefy` | functional zones (department-level polygons + text, e.g. "PORADNIA URAZOWO-ORTOPEDYCZNA") | closest analogue: a zone-level grouping this bank doesn't have a dedicated layer for yet — currently only per-room `A-ROOM-BNDY`/`A-ROOM-IDEN` exist, no zone/department overlay |
| `Metryczki pomieszczeń ARCHICADa` | ArchiCAD room/zone stamps (number, name, area) | `A-ROOM-IDEN` |
| `Okna ARCHICADa` / `Znaczniki okien ARCHICADa` | windows / window tags | `A-WIN` / window schedule tag |
| `Drzwi ARCHICAD` / `Znaczniki drzwi ARCHICADa` | doors / door tags | `A-DOOR` / door schedule tag |
| `299-A Wymiary Opisy` | dimensions & descriptions | `A-ANNO-DIMS` |
| `410-K Osie` | structural grid axes (numbered, e.g. axis markers "01"-"11"+) | no direct equivalent yet — this bank has no dedicated grid-axis layer; `acad-grids` category draws grid bubbles as composite geometry without a fixed layer convention. **Gap: add one if a real-grid import becomes routine.** |
| `440-K Słupy` | columns | `S-COLS` |
| `450-K Stropy` / `450-K Belki` | slabs / beams | no direct equivalent (this bank is largely 2D-plan-focused for structure) |
| `445-K Grzybki` | flat-slab drop panels/mushroom heads | no equivalent |
| `499-K Otworowanie` | structural openings (symbol, plan) | overlaps `A-DOOR`/`A-WIN` conceptually, not layer-equivalent |
| `250-A Schody Pochylnie` | stairs/ramps | `acad-verticals` category, no fixed default layer found yet |
| `251-A Windy` | elevators | `acad-verticals` |
| `260-A Balustrady` | railings | no dedicated layer |
| `290-A Ściany kurtynowe` | curtain walls | no dedicated layer (this bank's exterior-envelope tooling doesn't distinguish curtain wall as a type yet) |
| `320-W Umeblowanie` | furniture | `A-FURN-*` (this bank's furniture layers, see rule 64) |
| `335-W wyposażenie TM` | technology/medical equipment | `A-FURN-EQP` equivalent (this bank's `FURN-EQP-*` blocks, added this session) |
| `224-A Obudowa urządzeń` | equipment enclosures | no dedicated layer |
| `520-IS-Went_Klim` | HVAC/ventilation | `M-HVAC` (mechanical category, schematic only in this bank — see `acad-mechanical` scope note) |
| `533-I wod ppoż` | fire water supply | no equivalent (this bank has no fire-suppression-specific plumbing layer) |
| `535-I Kan_deszcz` | storm drainage | `acad-civil` scope, no fixed layer confirmed |
| `811-G Budynki istniejące` | existing buildings (coordination) | no equivalent — this bank has no "existing structure to coordinate against" concept; every project in this bank starts from an empty document |
| `830-G Teksty Etykiety` | general text labels | generic annotation layer |
| `840-G Znaczniki ogólne` | general markers | generic annotation layer |

## Gaps this comparison surfaced

- **No zone/department overlay layer** — this bank tracks rooms individually
  (`A-ROOM-BNDY`/`A-ROOM-IDEN`) but has nothing matching the real drawing's `210-A Strefy`
  department-level polygon+label. Worth adding if multi-zone projects become routine.
- **No dedicated structural-grid-axis layer/tool output** — `acad-grids` draws grid bubbles but
  doesn't appear to commit to a fixed layer name the way `A-WALL` etc. do. Should be resolved
  before rule 71 step 4 ("fix the grid before any wall") can be followed mechanically rather than
  by convention.
- **No structural-vs-partition wall distinction** — the real drawing splits `221-A` (structural
  interior) from `230-A` (partition) by fire/structural role; this bank draws everything through
  one `draw_wall`/`draw_walls_chain` onto one `A-WALL` layer regardless of role. Not fixed here
  (would touch every architecture tool) — flagged for a future rule-02-compliant additive change.
- **No "existing structure to coordinate against" concept** — every project in this bank starts
  from an empty document; real projects (like the one just reviewed) are frequently
  extensions/renovations coordinated against an existing basement or structure. Out of scope for
  this pass.

## Sourcing note

The real-drawing cross-reference table was extracted live from the actual DWG this session
(`acad.layers.list_layers`, `acad.annotations.list_text_by_pattern`), not inferred — but the
"maps to" column for structure/MEP layers is this bank's best current approximation, not a
verified 1:1 equivalence; several rows are honestly gaps, not answers.
