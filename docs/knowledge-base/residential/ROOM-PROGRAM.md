# Residential (multi-family apartment building) — typical room program

## Zones

Unlike hospital/office, an individual apartment isn't usually subdivided into "zones" the way a
department-based building is — the zone concept here applies at the BUILDING level (one
building = many apartments + shared circulation), and at the APARTMENT level each apartment is
itself the basic repeating unit.

| Zone | Purpose | Notes |
|---|---|---|
| Apartment units | the repeating residential unit | see room list below, per WT § 92 apartment must have living rooms, kitchen/kitchenette, bathroom, WC, storage, washing-machine space, internal circulation |
| Shared circulation | stairs, elevators, corridors serving multiple units | WT accessibility requirements apply (Ust. dostęp. 2019) |
| Building services | plant rooms, waste, parking (if below-grade) | not detailed here — out of scope for this pass |

## Rooms per apartment (typical, per WT §92 + §94)

| Nr | Nazwa | m² typowe | m² minimum (cytat) | Uwaga |
|---|---|---:|---:|---|
| — | Apartment total | 25-70+ | **≥ 25 m²** (WT § 94) | whole-apartment floor, not a per-room figure |
| — | Living room (pokój dzienny) | 18-28 | none found (see STANDARDS.md — no generic per-room minimum in WT) | typically the largest room; daylight required (§93) |
| — | Bedroom(s) | 10-16 | width ~2.2/2.6m reported, unconfirmed (see STANDARDS.md) | daylight required if classified as a living room (pokój mieszkalny) |
| — | Kitchen / kitchenette | 5-10 | **≥ 1.8 m²** (1-room apt) / ≥ 2.4 m² (multi-room apt), probable | windowless only in a 1-room apartment, with mechanical/gravitational exhaust (§93 ust. 2) |
| — | Bathroom | 4-6 | **3.52 m²** (1600×2200mm preset `bathroom-residential`, WT-2019 §82 — already in `docs/engineering-rules/63-sanitary-fixtures-wt.md`) | WC may be separate or combined |
| — | Separate WC (if not combined with bathroom) | 1.5-2.5 | none confirmed | optional per §92 ("separate WC or WC-in-bathroom") |
| — | Storage | 1-4 | none confirmed, only "space" required by §92 | can be a closet, not necessarily a room |
| — | Hallway / internal circulation | varies | width **≥ 1.2 m** clear (§95) | connects all rooms |

## Adjacency / connectivity requirements

| Room A | Room B | Requirement | Why |
|---|---|---|---|
| Entry hallway | Every room | direct or one-step access | §95 circulation requirement; no room should require passing through another bedroom |
| Kitchen | Living room | adjacent or combined (aneks kuchenny) permitted | §93 ust. 4 explicitly allows a kitchen annex opening onto a living room in multi-room apartments |
| Bathroom | Bedroom corridor | reachable without crossing the living room in larger units (typical practice, not a cited requirement) | privacy convention, not a WT citation — flagged as a convention, not a code minimum |

## Wzorzec stref i cyrkulacji

Within a single apartment: a day/night cluster split, not a single-row layout. The **day
cluster** (living room, kitchen/kitchenette, entry hall) sits nearest the entry and the facade
with the best daylight exposure, since §93 requires daylight for living rooms and kitchens
anyway. The **night cluster** (bedrooms, bathroom) sits deeper in the plan, reached through a
short internal corridor that acts as the buffer rule 73 step 3 calls for — bedrooms should not be
directly off the entry hall or require crossing the living room. This is the pattern a
"kulfon"-style single-row layout violates by construction: everything on one corridor with no
day/night distinction at all.

## Sourcing note

Room list and typical areas are this session's own reasonable defaults grounded in the confirmed
WT figures (§92 composition list, §94 total-area floor, §93 daylight) — NOT extracted from a
real residential reference drawing (none was supplied for this typology). If a real apartment
building DWG becomes available, re-derive this table from it per rule 71 step 2 rather than
trusting these typical values as more than a starting point.
