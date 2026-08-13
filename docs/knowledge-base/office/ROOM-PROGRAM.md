# Office — typical room program

## Zones

| Zone | Purpose | Typical share of gross area |
|---|---|---:|
| Public/reception | entrance, reception, waiting | ~5-10% |
| Open workstation area | desks, per the BHP §19 area-per-employee floor | ~50-60% |
| Private offices / meeting | enclosed offices, meeting rooms, phone booths | ~15-20% |
| Support | break room, storage, server/IT, WCs | ~10-15% |

## Rooms

| Nr | Nazwa | m² typowe | m² minimum (cytat) | Uwaga |
|---|---|---:|---:|---|
| — | Reception | 15-30 | none typology-specific; general habitable-room height applies | already an existing preset in this bank (rule 64 `reception`) |
| — | Open workstation area | per headcount | **≥ 2 m² free floor per employee** (BHP §19 ust. 2) | this is a per-person floor, not a per-room minimum — a validator rule checking a single room's area can't directly enforce it without knowing headcount; see AREA-CONVENTION.md |
| — | Private office (1 person) | 8-12 | derived: 1 person × ≥2 m² BHP floor, but realistically larger for furniture clearance | this bank's existing `office` preset (rule 64) already assumes 2400×2800mm = 6.72 m² min room |
| — | Meeting room (small) | 10-15 | none typology-specific | |
| — | Meeting room (large, >50 occupants) | varies | triggers **ZL I** classification for that specific room within an otherwise ZL III building | see STANDARDS.md |
| — | Break room / kitchen | 15-25 | none typology-specific | |
| — | Server/IT room | 6-12 | none typology-specific — HVAC/power requirements dominate sizing, not BHP | |
| — | WC / accessible WC | per rule 63 presets | **wc-public 1200×1400mm**, **wc-accessible 1500×1800mm (PN-EN 17210 §T.1)** | reused from existing `docs/engineering-rules/63-sanitary-fixtures-wt.md`, not re-researched |

## Adjacency / connectivity requirements

| Zone A | Zone B | Requirement | Why |
|---|---|---|---|
| Reception | Every other zone | reachable from reception without crossing a private office | visitor flow / access control |
| Open workstation area | Meeting rooms | directly adjacent | daily use pattern |
| Server/IT room | Open workstation area | NOT required to be adjacent — often isolated for HVAC/security | flagged as a deliberate exception to "every zone reachable," still must be reachable via corridor for maintenance access (rule 71 step 5's connectivity check still applies, just not via direct adjacency) |

## Wzorzec stref i cyrkulacji

Reception sits at the entry, public-facing, with every other zone reachable from it without
crossing a private office (the same access-control logic rule 60 §1a criterion 18 checks).
Open-plan workstation area occupies the largest, best-daylit floor plate (window walls), with
enclosed private offices and meeting rooms typically ringing its perimeter or occupying a
secondary daylight band — daylight is a workplace-quality driver here even though BHP does not
mandate it the way WT's residential §93 does. Support spaces (break room, server/IT, storage)
sit furthest from the public entry, server/IT often deliberately isolated from daily foot traffic
for security/HVAC reasons (see the Adjacency table's own exception note above).

## Sourcing note

Room list is a reasonable default combining this repo's already-existing `office`/`reception`/
`waiting`/`consult` furniture presets (rule 64) with the BHP area-per-employee figure researched
this session — not extracted from a real office reference drawing (none supplied for this
typology).
