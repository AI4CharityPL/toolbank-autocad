# Airport terminal — typical room program

Scope: a small-to-medium regional terminal (single or few piers, mixed domestic/limited
international traffic) — not a hub-scale multi-terminal complex. All areas in this file are
**industry-typical planning figures, not code minimums** — see `STANDARDS.md`, which found no
Polish or ICAO/IATA source that sets a prescriptive minimum area for any terminal-building room.
Where a table cell would normally carry a "m² minimum (cytat)" per this bank's template, it says
so explicitly rather than presenting a typical value as if it were a floor.

## Zones

| Zone | Purpose | Typical share of gross area |
|---|---|---|
| Landside processing (check-in hall, ticketing) | passenger and baggage entry into the air-travel process, still publicly accessible, non-ticketed | ~15-20% |
| Security screening | the one-way landside→airside checkpoint | ~5-8% |
| Airside concourse / gate holdrooms | boarding-side circulation and gate waiting areas | ~25-35% |
| Arrivals hall / baggage claim | inbound passenger and bag reclaim, pre- or post-customs depending on route mix | ~10-15% |
| Immigration / customs (international only) | border control, only present if the terminal handles international flights | ~3-6% if present, 0% if domestic-only |
| Retail / F&B concessions | airside and landside commercial space | ~8-12% |
| Airline back-office | check-in supervisor offices, baggage handling system rooms, ramp-side operations | ~5-8% |
| Staff areas | crew rest, staff circulation, break rooms, security/ops staff facilities | ~3-5% |
| Public WCs | distributed landside and airside | ~2-4% |

Percentages above are this session's own reasonable order-of-magnitude estimate for a
regional-terminal massing exercise, not extracted from a real terminal reference project (none
was supplied) and not backed by an IATA ADRM total-building-efficiency citation independently
verified this session — treat as a starting point for massing only.

## Rooms per zone (typical m² — no confirmed minimums exist, see STANDARDS.md)

| Nr | Nazwa | m² typowe | m² minimum (cytat) | Uwaga |
|---|---|---:|---:|---|
| LS-001 | Check-in hall | 400-1200 (scales with peak-hour departing pax × ~1.2-2.0 m²/pax IATA LoS planning figure) | none found | landside, publicly accessible, ticketing counters + self-service kiosks line one wall |
| LS-002 | Ticketing / airline counters (individual desk) | 4-6 per desk position | none found | typically arrayed in a linear or island check-in bank |
| SEC-001 | Security screening hall (pre-checkpoint queuing + lanes) | 150-400 (scales with ~1.0 m²/pax IATA LoS planning figure) | none found | the one hard landside/airside boundary — see Adjacency below |
| AIR-001 | Departures concourse (circulation spine) | varies with pier length | none found | connects security exit to all gate holdrooms |
| AIR-002 | Gate holdroom (typical single narrow-body gate) | 150-350 | none found | seated + standing capacity for one aircraft's boarding group; unconfirmed planning figure, see STANDARDS.md |
| ARR-001 | Baggage claim hall | 300-900 (scales with ~1.3-1.7 m²/pax IATA LoS planning figure) | none found | arrivals-side, one or more reclaim carousels |
| ARR-002 | Arrivals hall (post-claim, meeters/greeters) | 150-400 | none found | public, landside again once past customs (if any) |
| IMM-001 | Immigration control hall (int'l only) | 100-300 | none found | present only if terminal handles international arrivals; ICAO Annex 9 requires the function to exist and be efficient, no area figure |
| IMM-002 | Customs hall (int'l only) | 100-250 | none found | typically combined circulation with immigration, after baggage claim |
| RET-001 | Retail/F&B units (individual) | 20-150 per unit | none found | airside units require security-side access only |
| BO-001 | Airline back-office / ops room | 15-40 per unit | none found | staff-only, adjacent to check-in and/or ramp |
| BO-002 | Baggage handling system room / make-up area | 200-600+ | none found | staff-only, ground floor, adjacent to apron-side building face |
| ST-001 | Staff break room / crew rest | 20-60 | none found | staff-only |
| WC-001 | Public WC cluster (landside or airside) | 30-80 per cluster | none found | distributed per zone, accessibility per Ust. dostęp. 2019 |

## Adjacency / connectivity requirements

The landside→security→airside sequence is not a design preference — it is a **hard, one-way,
sequential requirement**: no route may allow a person to reach an airside space without passing
through the security checkpoint, and the checkpoint itself has no code-cited area minimum but its
*existence and one-way function* is non-negotiable for any terminal handling scheduled air
traffic. This is the one adjacency rule in this file with legal weight behind it (general aviation
security regulation, not sized here) rather than only planning convention.

| Zone A | Zone B | Connection requirement | Why |
|---|---|---|---|
| Landside processing (check-in) | Security screening | direct, one-way in (landside → security) | passengers must check in before presenting for screening |
| Security screening | Airside concourse | direct, one-way out (security → airside) | the hard landside/airside boundary — no bypass route permitted |
| Airside concourse | Gate holdrooms | direct, multiple | one or more holdrooms per pier, off the concourse spine |
| Gate holdrooms | Arrivals / baggage claim | **not directly connected** | arriving and departing passenger flows are kept separate; arrivals routes to baggage claim independently of the departures concourse |
| Baggage claim | Immigration/customs (int'l only) | sequence depends on route: immigration typically precedes baggage claim, customs follows it | matches typical international arrivals sequencing; domestic-only terminals omit both |
| Arrivals hall | Landside (public) | direct, one-way out (post-claim/post-customs → public landside) | arriving passengers rejoin the public landside zone after processing |
| Airline back-office | Check-in zone AND ramp/apron side | direct to both | supervises check-in operations and coordinates with ramp/baggage handling |
| Baggage handling system room | Check-in zone (bag drop) AND apron/ramp | direct to both, ground floor | bags move mechanically from check-in bag-drop to the aircraft, back-of-house only |
| Retail/F&B (airside) | Airside concourse only | airside-facing, no landside access | airside concessions must not create a walk-around-security route |
| Public WCs | every public zone | at least one cluster reachable without crossing a zone boundary | general public-building circulation expectation, not separately cited |

## Wzorzec stref i cyrkulacji (typical 2D layout logic)

The typical layout for this scale of terminal is a **linear concourse pattern**, matching the
plan already established for this repo's other typologies (a sequential zone chain rather than a
department cluster around a core):

1. **Landside processing band** — a wide, shallow band along the terminal's public (kerbside)
   frontage: check-in hall with ticketing counters facing the entrance, bag-drop adjacent.
2. **Security checkpoint as the boundary line** — a single controlled pinch-point spanning the
   full width (or a dedicated portion) of the plan, physically separating landside from airside.
   This is drawn as a distinct zone, not folded into either neighbor, precisely because it is the
   one adjacency in this program with a hard one-way rule.
3. **Airside concourse spine** — a long circulation corridor running perpendicular to (or
   continuing past) the security line, from which one or more **gate piers** branch off. Each pier
   carries a row of gate holdrooms along its outer (apron-facing) wall, with retail/F&B lining the
   inner side of the spine.
4. **Arrivals band, kept separate from the departures spine** — typically a lower level or a
   parallel band that does not intersect the departures concourse, leading arriving passengers
   from the aircraft/jet-bridge to baggage claim and out to the public landside zone independently.
5. **Back-of-house strip** along the apron-facing (airside/ground) edge — baggage handling system,
   airline back-offices, staff areas — positioned for direct ramp access without crossing
   passenger-facing zones.

This pattern is this session's own reasonable synthesis of standard airport-planning literature
(the linear/pier concourse type as commonly described in aviation planning sources), not extracted
from a real terminal reference drawing — no such drawing was supplied for this typology.

## Sourcing note

Zone list and adjacency logic follow directly from the two confirmed structural facts in this
typology: (1) security screening is a mandatory one-way landside/airside boundary (general
aviation-security practice, not separately area-cited here), and (2) IATA ADRM's LoS framework is
the actual industry mechanism for sizing terminal spaces, expressed per-passenger rather than as
fixed room minimums. Typical m² figures are derived from IATA LoS per-passenger planning ranges
(see `STANDARDS.md` for confidence tags on each) applied to an assumed small-to-medium regional
terminal's approximate peak-hour passenger volume — **not from a real reference drawing** (none
supplied) **and not from a confirmed code minimum** (none exists). If a real terminal reference
drawing becomes available, re-derive this entire file from it per rule 71 step 2 rather than
trusting these figures as more than an order-of-magnitude starting point.
