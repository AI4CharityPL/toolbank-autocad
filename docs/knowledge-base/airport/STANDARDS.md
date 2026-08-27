# Airport terminal — legal & code standards

> **Reference standards:** Ustawa z 3.07.2002 Prawo lotnicze (**Dz.U. 2002 nr 130 poz. 1112**, t.j. Dz.U.
> 2024) · WT 2024 (Rozp. MI 12.04.2002, **Dz.U. 2022 poz. 1225**) · Ustawa z 24.08.1991 o ochronie
> ppoż. (t.j. Dz.U. 2024) · Ustawa z 19.07.2019 o dostępności (Dz.U. 2019 poz. 1696, t.j. Dz.U.
> 2024) · ICAO Annex 14 (aerodrome design) · ICAO Annex 9 (facilitation) · IATA ADRM (Level of
> Service planning guidance)

**This is explicitly the weakest-grounded typology in this knowledge base.** Unlike hospital
(Rozp. MZ 2019 sets per-room minimums directly) or residential (WT Rozdział 7 sets per-apartment
and per-room figures directly), **no Polish or international source found this session sets a
prescriptive minimum area for a terminal-building room** — check-in hall, security screening
area, gate holdroom, baggage claim. Airport terminal sizing is governed in practice by IATA's
Level of Service (LoS) *planning* methodology (a design target chosen by the airport operator,
not a legal floor) and by airport-specific master planning against forecast passenger volumes —
not by a building-code minimum-area table. **Every numeric value in this file below the
legal-hierarchy table is flagged explicitly as either a generic-building-code figure that happens
to still apply (fire safety, evacuation, accessibility) or an industry-typical planning figure —
"typowe wartości branżowe, nie zweryfikowane normatywnie" — never as a terminal-specific code
minimum, because no such minimum was found.** Do not build a `validators/_standards/airport-*`
area-minimum rule on any figure in this file without first finding and citing a real source for
it; the confidence tags below say explicitly which figures are even candidates for that.

## Legal-hierarchy table

| Skrót | Pełna nazwa | Rola w projekcie |
|---|---|---|
| **Pr. bud.** | Ustawa z 7.07.1994 Prawo budowlane (Dz.U. 2024 t.j.) | podstawa procesowa dla budynku terminala jako obiektu budowlanego |
| **Pr. lotnicze** | Ustawa z 3.07.2002 Prawo lotnicze (**Dz.U. 2002 nr 130 poz. 1112**, t.j. Dz.U. 2024) | reżim lotniska jako całości (rejestracja, służby ratownicze, otoczenie lotniska) — **nie zawiera przepisów o wymiarowaniu pomieszczeń budynku terminala**, patrz niżej |
| **Rozp. MI 25.06.2003 (poz. 1191)** | Rozporządzenie w sprawie przepisów techniczno-budowlanych dla lotnisk cywilnych (Dz.U. 2003 nr 130 poz. 1191), wyd. na podst. Pr. lotnicze | pole wzlotów, drogi startowe, płyty postojowe, infrastruktura airside — z tego co udało się ustalić bez dostępu do pełnego tekstu, **dotyczy strony airside/lotniskowej, nie wnętrza budynku terminala** — patrz Sourcing note |
| **Rozp. MI 25.06.2003 (poz. 1192)** | Rozporządzenie w sprawie warunków, jakie powinny spełniać obiekty budowlane i naturalne w otoczeniu lotniska (Dz.U. 2003 nr 130 poz. 1192) | powierzchnie ograniczające wysokość zabudowy WOKÓŁ lotniska (przeszkody lotnicze) — dotyczy budynku terminala tylko jako obiektu podlegającego ograniczeniu wysokości, nie jego wewnętrznego programu |
| **WT 2024** | Rozp. MI z 12.04.2002 (t.j. **Dz.U. 2022 poz. 1225**) | budynek terminala jako budynek użyteczności publicznej: klasa odporności ogniowej, drogi ewakuacyjne, dostępność, wysokości pomieszczeń — te same ogólne przepisy co dla każdego dużego budynku publicznego, nie przepisy lotniskowe |
| **Ust. ppoż.** | Ustawa z 24.08.1991 (t.j. Dz.U. 2024) | klasyfikacja ZL, wymogi instalacji ppoż. |
| **Ust. dostęp.** | Ustawa z 19.07.2019 (Dz.U. 2019 poz. 1696, t.j. Dz.U. 2024) | dostępność części ogólnodostępnych terminala |
| **ICAO Annex 14** | Aerodromes, Vol. I — Aerodrome Design and Operations | fizyczne parametry lotniska (pas startowy, drogi kołowania, płyty, powierzchnie ograniczające przeszkody, RFF) — **nie obejmuje wewnętrznego układu budynku terminala** (jawnie wyłączone z zakresu, patrz ICAO Doc 9184 dla planowania ogólnego) |
| **ICAO Annex 9** | Facilitation | wymaga sprawnej obsługi celno-imigracyjnej na lotniskach międzynarodowych — nakłada wymóg *funkcjonalny* (odprawa musi istnieć i być sprawna), nie podaje metrażu pomieszczeń |
| **IATA ADRM** | Airport Development Reference Manual (obecnie 12th ed.), rozdział Level of Service (LoS) | branżowa metodyka planowania powierzchni terminala (m²/pasażera wg poziomu LoS A-F) — **wytyczna projektowa operatora lotniska, nie przepis prawa** |

## Key checkable numbers, with confidence

| Requirement | Value | Citation | Confidence |
|---|---:|---|---|
| Fire category | **ZL I** likely (rooms/spaces designed for simultaneous occupancy of >50 non-permanent people — check-in hall, security hall, gate holdrooms, baggage claim all plausibly qualify) | WT § 209 | **Probable** — the ZL I definition itself (>50 non-permanent occupants, not primarily mobility-impaired) is corroborated by multiple secondary sources; its specific application to "dworzec lotniczy" was not found stated verbatim in a primary or secondary source this session, it is this session's own reasonable inference from the definition, not a confirmed classification |
| Evacuation passage length (within a ZL fire zone) | **≤ 40 m** | WT — reported as governing ZL zones generally (§ 237/§256 area, exact sub-paragraph not independently re-verified this session) | **Probable** — same generic-ZL figure used for every other public-building typology in this bank, not terminal-specific |
| Room/floor >300 m² in a ZL zone | requires **≥ 2** evacuation exits | WT, exact § not independently re-verified this session | **Probable** — generic ZL rule, would apply directly to any terminal hall over 300 m² (virtually guaranteed for check-in hall, security hall, departures concourse) |
| Accessibility of public-facing areas | required — step-free routes, accessible WCs, tactile guidance where applicable | Ust. dostęp. 2019, generic public-building requirement | **Confirmed** applies (it's a generic public-building law with no typology carve-out), **not independently re-verified in full text this session** |
| International-flight customs/immigration facility | required to exist and be "efficient" at international airports | ICAO Annex 9, general obligation | **Confirmed** the obligation exists in principle (widely corroborated); **no room-count or area figure** accompanies it in Annex 9 itself — sizing is left to national/airport-level planning |
| Terminal-building room-area minimums (check-in desk frontage, security lane count, holdroom m², baggage claim m²) | **none found** | — | **Confirmed absent** from every Polish source checked this session (Prawo lotnicze, its two executive regulations, WT) and from ICAO Annex 14/9 — these instrument sizing through LoS planning (IATA ADRM) and passenger-forecast-driven master planning, not a code minimum table |
| Check-in area, industry-typical planning figure | **~1.2-2.0 m²/departing pax at peak hour** (≈12.9-21.5 ft²/pax depending on bag-cart mix, IATA LoS bands) | IATA ADRM LoS space-standard tables (secondary source: National Academies ACRP synthesis of the same tables) | **industry-typical, nie zweryfikowane normatywnie** — a planning target chosen per project, not a minimum any code enforces |
| Security screening area, industry-typical planning figure | **~1.0 m²/pax** (≈10.8 ft²/pax, pre-security side) | IATA ADRM LoS | **industry-typical, nie zweryfikowane normatywnie** |
| Baggage claim area, industry-typical planning figure | **~1.3-1.7 m²/pax** (≈14.0-18.3 ft²/pax depending on cart-use assumption) | IATA ADRM LoS | **industry-typical, nie zweryfikowane normatywnie** |
| Gate holdroom area, industry-typical planning figure | order-of-magnitude **~1.0-1.5 m²/seated+standing pax** | general industry planning practice, not independently pinned to a specific IATA LoS table row this session | **Unconfirmed** — reported as typical practice in secondary aviation-planning literature, weakest-sourced figure in this table; flagged, do not treat as even industry-typical without further verification |

## Sourcing note

Direct fetch of `isap.sejm.gov.pl`/`eli.gov.pl` primary text for Prawo lotnicze succeeded once
(the base act text) and was searched for "terminal"/room-sizing content — **none found**, the act
governs airport establishment, registration, rescue/fire services, and airport surroundings, not
terminal-building interior design. Direct fetch of the two executive regulations under it
(Dz.U. 2003 nr 130 poz. 1191 techniczno-budowlane dla lotnisk cywilnych, and poz. 1192 obstacle
limitation surfaces) failed — `isap.sejm.gov.pl` returned a CAPTCHA verification page, the same
access failure the residential and office typologies hit this repo. Their scope was inferred from
secondary-source titles/descriptions only (airfield technical-construction rules; obstacle
limitation surfaces around the airport) — **not independently confirmed to exclude terminal
interior content**, flagged as a gap rather than asserted with false confidence. ICAO Annex 14 was
confirmed by multiple sources to explicitly exclude "overall planning of aerodromes" (referring
that instead to ICAO Doc 9184, Airport Planning Manual) — neither Annex 14 nor Doc 9184 was found
to set prescriptive terminal room minimums either. IATA ADRM LoS figures were not fetched from the
IATA document itself (paywalled/licensed) — they come from secondary summaries (an ACRP/National
Academies synthesis report citing the same IATA tables) cross-checked against each other for the
check-in, security, and baggage-claim rows; the holdroom figure could not be cross-checked and is
marked **Unconfirmed** rather than industry-typical. **Before this typology's figures are used for
anything beyond a demonstration project, (a) obtain the actual IATA ADRM 12th ed. LoS tables
directly rather than relying on secondary summaries, and (b) get a primary-source read of Dz.U.
2003 nr 130 poz. 1191 to confirm it truly has no terminal-building content.**
