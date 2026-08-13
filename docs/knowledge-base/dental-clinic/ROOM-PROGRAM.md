# Dental clinic (gabinet stomatologiczny) — typical room program

Scope: a small-to-medium standalone dental practice, ~150-250 m² gross, 2-3 treatment rooms —
the typical build-out inside an existing building shell or ground-floor commercial unit (per
`romident.pl`'s own area bands: "single-station up to ~60 m²", "two-station 60-80 m²",
"three-station 80-100 m² **for the clinical area alone**", before adding waiting/back-of-house —
consistent with the 150-250 m² total this file scopes to). NOT a hospital-scale outpatient
department — see `docs/knowledge-base/hospital/ROOM-PROGRAM.md` for that scale instead.

## Zones

| Zone | Purpose | Typical share of gross area |
|---|---|---:|
| Public / reception | entrance, waiting, registration, patient WC | ~20-25% |
| Treatment | gabinety zabiegowe (2-3), point RTG room | ~45-55% |
| Sterile / back-of-house | sterylizacja, storage | ~10-15% |
| Staff | staff WC, pom. socjalne (social/break room) | ~10-15% |

## Rooms per zone

| Nr | Nazwa | m² typowe | m² minimum (cytat) | Uwaga |
|---|---|---:|---:|---|
| PUB-001 | Poczekalnia (waiting room) | 10-20 | none found | size scales with chair count; accessibility circulation per Ust. dostęp. 2019 |
| PUB-002 | Rejestracja (reception) | 4-8 | none found | often combined with waiting room as one open area, not a separate enclosed room |
| PUB-003 | WC dla pacjentów | 2-4 | fixture spacing per `docs/engineering-rules/63-sanitary-fixtures-wt.md` (bathroom-residential preset, WT §82) | at least one must meet accessibility requirements (Ust. dostęp. 2019) |
| TRT-001..003 | Gabinet zabiegowy (treatment room) ×2-3 | 12-20 typical, 8-9 treated as an ergonomic floor by some sources | **none found as a code minimum** — Rozp. MZ 2019 §16 functional test only, see STANDARDS.md; a 12 m² + 8 m²/additional-chair figure is **historical/superseded**, not current | unit dentystyczny + workstation + sink + cabinetry; daylight not mandated the way residential living rooms are (WT §93 doesn't apply to non-residential rooms) |
| TRT-004 | Gabinet RTG punktowe (point dental X-ray room) | 8-12 | **≥ 8 m²** for one apparatus, **+4 m²** per additional apparatus (Rozp. MZ RTG 2006) — see STANDARDS.md, **Confirmed** | wall bearing the primary beam direction and the door need lead shielding per project-specific calculation — draw the shielded wall on `A-WALL-LEAD` (reused from hospital, see GRID-AND-LAYERS.md); ventilation ≥1.5 ACH |
| STR-001 | Sterylizacja | 4-8 | **≥ 4 m²** per sterilization workstation (industry convention, no § pinned — **Probable**, see STANDARDS.md) | dirty-to-clean one-way flow (brudne → mycie/dezynfekcja → pakowanie → autoklaw → czysty magazyn); U- or L-shaped counter recommended; equipment ≥1.5 m from any treatment surface |
| STR-002 | Magazyn / schowek (storage) | 3-6 | none found | materials, consumables |
| STF-001 | WC dla personelu | 2-3 | fixture spacing per `docs/engineering-rules/63-sanitary-fixtures-wt.md` | separate from patient WC where floor area allows |
| STF-002 | Pomieszczenie socjalne (staff social/break room) | 6-10 | **≥ 0.3 m² per employee** (romident.pl industry figure, no § pinned — **Probable**) | break/changing space for staff |

## Adjacency / connectivity requirements

| Room A | Room B | Connection requirement | Why |
|---|---|---|---|
| Poczekalnia | Rejestracja | direct, open or immediately adjacent | patient check-in flow |
| Poczekalnia | Gabinety zabiegowe | direct corridor access, NOT through sterylizacja or staff zone | patient-facing circulation stays separate from clinical/back-of-house flow |
| Every gabinet zabiegowy | Sterylizacja | direct, reachable **without crossing the poczekalnia** | instrument resupply between patients must not route dirty/clean instrument traffic through the public waiting area — same logic as the hospital typology's sterile-corridor requirement, scaled down |
| Gabinet RTG | Treatment-zone corridor | direct, staff/patient-escorted access, not opening directly onto poczekalnia | radiation-safety corridor discipline — patients are escorted in, not self-routed past the shielded door |
| Sterylizacja | Magazyn | direct or immediately adjacent | consumable resupply to sterilization workflow |
| Poczekalnia | WC dla pacjentów | direct, public-facing | patient-facing fixture must not require crossing the treatment zone |
| Pomieszczenie socjalne | WC dla personelu | direct or same back-of-house corridor | staff-only zone, kept separate from the patient-facing zone |
| Rejestracja | Every gabinet zabiegowy | reachable via one shared corridor (not a direct door) | reception coordinates patient flow into treatment rooms without needing a private connection to each |

## Wzorzec stref i cyrkulacji (zone/circulation pattern)

Typical 2D layout logic for this typology: **wejście → poczekalnia → rejestracja → korytarz
zabiegowy, z gabinetami zabiegowymi rozmieszczonymi wokół centralnie ulokowanej sterylizacji tak,
by każdy gabinet miał do niej bezpośredni dostęp bez przechodzenia przez poczekalnię; gabinet RTG
przy tym samym korytarzu, z odsuniętą od ciągu pieszego ścianą osłonową; strefa socjalna
personelu (WC personelu + pomieszczenie socjalne) odseparowana od strefy pacjenta.** This mirrors,
at a much smaller scale, the public-vs-sterile-vs-back-of-house separation the hospital typology's
own adjacency table already establishes — the same principle, one corridor loop instead of a
multi-zone spine.

## Sourcing note

Room list and typical areas are this session's own reasonable defaults, grounded in the confirmed
and probable figures in `STANDARDS.md` (Rozp. MZ RTG 2006's 8 m²/+4 m² RTG-room figures are the
only ones with a firm § behind them; the sterylizacja and gabinet-zabiegowy figures are industry
convention, explicitly flagged as such, not code minimums) — NOT extracted from a real dental
clinic reference drawing (none was supplied for this typology; unlike hospital, which had a real
outpatient-clinic DWG to extract from). If a real dental-clinic DWG becomes available, re-derive
this table from it per rule 71 step 2 rather than trusting these typical values as more than a
starting point.
