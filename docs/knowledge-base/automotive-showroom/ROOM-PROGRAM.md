# Automotive showroom (salon samochodowy) — typical room program

## Scope note — service workshop excluded from this pass

A real car dealership is frequently one building with two very different halls: the **display
showroom** (this file's subject) and a **service/repair workshop** (hala serwisowa / warsztat).
The workshop is very likely a separate PM-classified fire zone (see STANDARDS.md), typically
needs a much larger clear span for lifts/bays and vehicle circulation, has its own ventilation
requirements (industry sources found incidentally during this research cite 6-10 ACH and mandatory
exhaust-gas extraction for a workshop hall — not researched further here because it's out of
scope), and is often built as effectively a separate structure abutting the showroom. **This
ROOM-PROGRAM.md covers only the showroom side** — reception/sales/exhibition/back-office/support —
plus a single "service reception" room that is the *customer-facing handoff point* into the
workshop, not the workshop itself. A future pass should add a dedicated `service-workshop`
typology (or an addendum to this one) before this bank attempts to draw a full dealership with a
real workshop hall.

## Zones

| Zone | Purpose | Typical share of gross area |
|---|---|---:|
| Public / exhibition | the glazed display hall where cars are shown | ~50-65% |
| Sales & customer service | reception, sales/negotiation offices, waiting/cafe | ~10-15% |
| Service reception (customer-facing only) | handoff point into the (out-of-scope) workshop | ~5% |
| Back-office / staff | staff offices, break room, staff WC | ~10-15% |
| Support | storage, plant/utility, customer WC | ~5-10% |

## Rooms per zone

| Nr | Nazwa | m² typowe | m² minimum (cytat) | Uwaga |
|---|---|---:|---:|---|
| — | Hala wystawowa (display/exhibition hall) | 300-800+ (varies with # of displayed vehicles; informal industry rule of thumb ~50-80 m²/car incl. circulation) | none found — see STANDARDS.md "Confirmed absent" row | large clear-span space; ceiling height driven by design convention (5-6 m), not code — see GRID-AND-LAYERS.md |
| — | Reception / recepcja klienta | 10-20 | none typology-specific | first customer touchpoint, usually just inside the entrance |
| — | Sales / negotiation office (biuro sprzedaży / pokój negocjacyjny) — typically 1-2 | 8-12 each | derived: reuse this bank's existing `office` preset (rule 64, 2400×2800mm = 6.72 m² min), same reasoning `docs/knowledge-base/office/ROOM-PROGRAM.md` used | often glazed-partition "pods" bordering the hall so the cars stay visible during negotiation — a deliberate design convention, not a code requirement |
| — | Customer waiting / cafe area | 15-30 | none typology-specific | coffee point, seating; frequently open to or overlooking the hall |
| — | Service reception (przyjęcie serwisowe) | 10-20 | none found | customer-facing desk only — the workshop hall itself is out of scope (see scope note above); this room is the boundary between showroom and workshop scopes |
| — | Back-office / staff offices | 8-15 each | reuse `office` preset (rule 64), same as sales offices | management, finance/leasing admin, not customer-facing |
| — | Break room / staff kitchen | 10-15 | none typology-specific | |
| — | Staff WC | per rule 63 presets | **wc-public 1200×1400mm** | reused, not re-researched |
| — | Customer WC / accessible WC | per rule 63 presets | **wc-public 1200×1400mm**, **wc-accessible 1500×1800mm (PN-EN 17210 §T.1)** | reused from `docs/engineering-rules/63-sanitary-fixtures-wt.md`, same as hospital/office |
| — | Storage (magazyn materiałów POS / dokumentacji) | 10-20 | none typology-specific | not vehicle-parts storage — that belongs to the out-of-scope workshop's own parts warehouse |

## Adjacency / connectivity requirements

| Zone A | Zone B | Connection requirement | Why |
|---|---|---|---|
| Exhibition hall | Main entrance / street frontage | direct, fully glazed | showroom visibility from the road is the primary design driver — see "Wzorzec stref i cyrkulacji" below |
| Exhibition hall | Sales/negotiation offices | direct, perimeter adjacency | customers move from browsing to negotiating without leaving sight of the hall |
| Reception | Exhibition hall + Waiting/cafe | direct, public-facing | reception is the routing point for all visitor traffic |
| Sales offices | Back-office / staff zone | direct | staff need to move between customer-facing and admin work without crossing the public hall repeatedly |
| Exhibition hall | Service reception | reachable, but NOT required to be visually open — typically a distinct desk near a side/rear entrance | customers arriving for service should not have to cross the sales floor; a deliberate separation of customer journeys, not a code requirement |
| Storage | Exhibition hall | reachable via back-of-house circulation, NOT public-facing | stock/POS materials moved without crossing the customer path |
| Customer WC | Waiting/cafe area | nearby, public accessible | convenience + accessibility (Ust. dostęp. 2019) |

## Wzorzec stref i cyrkulacji (typical 2D layout logic)

The dominant real-world pattern, corroborated by multiple dealership-design sources
(mix.waw.pl, gigaarchitekci.pl, nukastudio.pl) and by actual built-project descriptions
(newsteelconstruction.com's BMW/MINI/JLR showroom examples):

1. **The exhibition hall fronts the street / main road**, fully glazed on the public-facing
   elevation(s) — this is the single strongest, most consistently repeated design driver across
   every source found. The hall is deliberately the largest, most visually prominent volume.
2. **Sales and negotiation offices sit around the hall's perimeter**, often as glazed "pods" or
   partial-height partitions rather than solid enclosed rooms, so vehicles on display stay visible
   from inside the offices — a showroom-specific variant of the general office-adjacency pattern.
   Some larger showrooms add a mezzanine overlooking the hall for exactly this reason.
3. **Reception sits at or near the main public entrance**, routing visitors either into the hall
   (sales) or toward the service-reception desk (service), which is usually accessed from a
   **separate side or rear entrance/driveway** so the two customer journeys don't cross the
   showroom floor.
4. **Back-office, staff break room, and storage sit furthest from the public entrance** — typically
   at the rear of the building, away from the street frontage, mirroring the same "back-of-house
   farthest from public zone" pattern already used in hospital/office.
5. **The service workshop (out of scope here)**, when present, is generally a separate
   larger-span hall attached at the rear or side, with its own vehicle-only entrance distinct from
   the customer entrance.

## Sourcing note

Room list, typical areas, and the zoning/circulation pattern are this session's own reasonable
defaults, combining (a) this bank's already-existing `office`/`reception` furniture presets (rule
64) for the sales/back-office rooms, (b) rule 63's WC presets, and (c) web research on car-
dealership design practice (mix.waw.pl, gigaarchitekci.pl, nukastudio.pl, newsteelconstruction.com,
showhoobuilding.com) for the hall-fronts-the-street / offices-around-the-perimeter pattern — NOT
extracted from a real showroom reference drawing (none supplied for this typology). None of the
typical-area figures above carry a WT/BHP minimum-area citation, matching STANDARDS.md's
"Confirmed absent" finding that Polish code does not set a showroom floor-area minimum; treat
every m² figure in this file as a design default, not a code requirement, until a real reference
drawing or a validator-relevant citation is found. If a real dealership DWG becomes available,
re-derive this table from it per rule 71 step 2, and use that pass to also scope in the service
workshop this file deliberately leaves out.
