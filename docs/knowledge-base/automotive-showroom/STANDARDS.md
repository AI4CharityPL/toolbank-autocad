# Automotive showroom (salon samochodowy / salon sprzedaży samochodów) — legal & code standards

> **Reference standards:** WT 2024 (Rozp. MI z 12.04.2002, tj. **Dz.U. 2022 poz. 1225**) · Ustawa z
> 24.08.1991 o ochronie ppoż. (t.j. Dz.U. 2024) — kategoria zagrożenia ludzi **ZL III** · Rozp.
> MPiPS BHP (Dz.U. 2003 nr 169 poz. 1650) dla stałych stanowisk pracy w hali/biurach · Ustawa z
> 19.07.2019 (Dz.U. 2019 poz. 1696) · PN-ISO 9836 (powierzchnie) · PN-EN 17210 (dostępność)

Researched via web search this session against secondary legal-summary sources (lexlege.pl,
arslege.pl, architektura.info, cross-checked against each other), NOT isap.sejm.gov.pl directly —
same access failure as residential/office (`isap.sejm.gov.pl` returns a CAPTCHA page to automated
fetch). Confidence is marked per row. No real showroom reference drawing has been supplied to this
repo yet, so every figure here is web-research-derived, not extracted from a project DWG.

## Legal-hierarchy table

| Skrót | Pełna nazwa | Rola w projekcie |
|---|---|---|
| **Pr. bud.** | Ustawa z 7.07.1994 Prawo budowlane (Dz.U. 2024 t.j.) | podstawa procesowa |
| **WT 2024** | Rozp. MI z 12.04.2002 (tj. **Dz.U. 2022 poz. 1225**) | budynek jako całość: klasa odporności ogniowej, strefy pożarowe, ewakuacja, wysokość pomieszczeń |
| **Ust. ppoż.** | Ustawa z 24.08.1991 (t.j. Dz.U. 2024) | klasyfikacja ZL, podstawa dla WT Dział VI |
| **Rozp. MPiPS BHP** | Rozp. Ministra Pracy i Polityki Socjalnej z 26.09.1997 (tj. **Dz.U. 2003 nr 169 poz. 1650**) | powierzchnia/wysokość dla stałych stanowisk pracy sprzedawców w hali i biurach sprzedaży — reused from `docs/knowledge-base/office/STANDARDS.md`, not re-researched |
| **Ust. dostęp.** | Ustawa z 19.07.2019 (Dz.U. 2019 poz. 1696, tj. Dz.U. 2024) | dostępność części publicznej (klienci z niepełnosprawnością) |

## PN-EN / PN-ISO standards table

| Standard | Subject | Which layer/system it governs |
|---|---|---|
| **PN-ISO 9836** | definicje powierzchni (netto/brutto) | area-measurement convention, see AREA-CONVENTION.md |
| **PN-EN 17210** | dostępność budynków, wymiary WC dostosowanego | reused `wc-accessible` preset (rule 63), 1500×1800mm |
| **PN-EN 13501-2** | klasyfikacja odporności ogniowej przegród i przeszkleń (E/EI) | relevant only if the display hall is fire-separated from an attached service workshop — see glazing note below |

## Fire classification

Classification: **budynek użyteczności publicznej, ZL III** — "użyteczności publicznej,
niezakwalifikowane do ZL I i ZL II" (WT § 209 ust. 2 pkt 3). **Confirmed** — quoted verbatim from
a direct fetch of the regulation text (lexlege.pl) this session; the general ZL III catch-all
description is independently corroborated by 3 secondary summary sites (fireconsult.pl, seka.pl,
safetydream.pl).

- A car showroom is explicitly **not** a "garaż" under WT Rozdział 10 (Dział III) — that chapter
  governs vehicle *parking and routine non-professional maintenance*, whereas a showroom's purpose
  is display/sale. **Probable** — this distinction is stated by industry secondary sources
  (znakowo.pl, aspolska.pl) describing garaże's scope, not by a direct statutory sentence naming
  showrooms; treat as a reasonable inference, not a quoted exemption clause.
- If a single room within the showroom (e.g. a large launch/event space) is designed for **>50
  simultaneous non-permanent occupants**, that room individually triggers **ZL I** classification
  even inside an otherwise-ZL III building (WT § 209 ust. 2 pkt 1). **Confirmed** definition text,
  **Probable** applicability judgment — same pattern already flagged in
  `docs/knowledge-base/office/STANDARDS.md` for large conference rooms; a typical dealership
  showroom floor (customers browsing, not a seated audience) will usually stay ZL III, but a
  launch-event hall should be checked against this threshold per project.
- The **service/repair workshop**, if physically part of the same building, is very likely a
  separate **PM** (produkcyjno-magazynowa) fire zone with its own area-limit table (WT § 228, a
  *different* table from § 227's ZL table) rather than ZL III — this typology's ROOM-PROGRAM.md
  explicitly puts the workshop hall out of scope for this pass, so PM-table figures are not
  researched here. Flagged as a gap for a future pass, not silently assumed.

## Key checkable numbers, with confidence

| Requirement | Value | Citation | Confidence |
|---|---:|---|---|
| Max fire-zone area, ZL III, single-storey building | **10 000 m²** | WT § 227 ust. 1 | **Confirmed** — 3 independent secondary sources (architektura.info, lexlege.pl, arslege.pl) reproduce an identical table |
| Max fire-zone area, ZL III, budynek niski (N) | **8 000 m²** | WT § 227 ust. 1 | **Confirmed** — same 3-source cross-check |
| Max fire-zone area, ZL III, średniowysoki (SW) | **5 000 m²** | WT § 227 ust. 1 | **Confirmed** — same 3-source cross-check |
| Max fire-zone area, ZL III, wysoki/wysokościowy (W/WW) | **2 500 m²** | WT § 227 ust. 1 | **Confirmed** — same 3-source cross-check (a dealership showroom is essentially always single-storey or niski in practice, so this row is unlikely to be load-bearing) |
| Underground portion of a ZL fire zone | **≤ 50%** of the above-ground allowance | WT § 227 ust. 2 | **Confirmed** — 2 independent sources agree |
| Fire-zone area enlargement with sprinklers / smoke removal / both | **+100% / +100% / +200%** | WT § 227 ust. 4 | **Confirmed** — 3 sources agree on the combined figure, minor wording variance on the individual figures across fetches |
| Room requiring ≥2 evacuation exits (≥5 m apart) | area **> 300 m²** (or **>50** simultaneous occupants, general ZL threshold; >30 for ZL II specifically) | WT § 238 | **Probable** — 2 independent fetches quote matching text; one explicitly ties the 300 m² figure to ZL III, the other reproduces the same rule under a generic "ZL" heading without repeating the ZL III label — directly relevant to the display hall, which will almost always exceed 300 m² |
| Max evacuation approach length (dojście ewakuacyjne), ZL III, one direction available | **30 m** (of which ≤20 m may be a horizontal route) | WT § 256 ust. 3 | **Confirmed** — 2 independent fetches (architektura.info, lexlege.pl) reproduce identical figures |
| Max evacuation approach length, ZL III, two or more directions available | **60 m** | WT § 256 ust. 3 | **Confirmed** — same 2-source cross-check |
| Evacuation-length extension with sprinklers / smoke removal / both | **+50% / +50% / +100%** | WT § 256 | **Confirmed** — same 2-source cross-check |
| Horizontal evacuation route width | **≥ 0.6 m per 100 people**, never **< 1.4 m** clear (reducible to **1.2 m** for routes serving ≤20 people) | WT § 242 ust. 1 | **Confirmed** — reproduced verbatim, matches the general figure already used in `docs/knowledge-base/office/STANDARDS.md`'s "Probable" passage-width rows but here quoted directly from § 242 itself |
| Evacuation route height | **≥ 2.2 m** clear (local reduction to 2.0 m over ≤1.5 m run) | WT § 242 | **Confirmed** — quoted verbatim |
| Habitable/public room height, general baseline | **≥ 2.5 m** clear | WT § 72 | **Confirmed** — same figure already Confirmed in `docs/knowledge-base/residential/STANDARDS.md` and `docs/HOSPITAL-2026-MASTERPLAN.md`, applies to the showroom envelope as a public-use room absent a typology-specific override |
| Room height for a permanent employee workstation (sales desk on the hall floor / sales office), no harmful factors | **≥ 3 m** clear | Rozp. MPiPS BHP § 20 ust. 1 pkt 1 | **Confirmed** — reused directly from `docs/knowledge-base/office/STANDARDS.md`, not re-derived; applies wherever a salesperson has a fixed desk, whether in the hall or a back office |
| Showroom-specific (large-exhibition-hall) minimum ceiling height beyond the § 72 / BHP baselines above | none found | — | **Confirmed absent** — no WT provision sets a height floor specific to a display/exhibition hall use. Industry sources (showhoobuilding.com, mabo.info.pl, smarthalls.com) commonly recommend **5-6 m clear** for a car-showroom hall, but this is a design convention driven by vehicle sightlines/tailgate clearance/lighting, not a legal minimum — do not cite it as code |
| Retail/showroom floor-area minimum (e.g. m² per displayed vehicle) | none found in WT | — | **Confirmed absent** — Polish building code sets no per-vehicle or total floor-area minimum for a dealership showroom. Any such figure (frequently seen as an informal "~50-80 m² per displayed car including circulation" rule of thumb in industry material) is a design heuristic, not a statute |
| Retail/showroom glazing-ratio minimum (curtain-wall storefront) | none found in WT | — | **Confirmed absent** — glazing extent for a showroom frontage is set by vehicle-manufacturer corporate-identity / dealer-standard manuals (a private brand requirement, not Polish law), not by WT. Flagged so it isn't mistaken for a code citation |
| Glazing in a fire-separation wall (ściana oddzielenia przeciwpożarowego), if the hall is fire-separated from an attached workshop | **≤ 10%** of the wall's surface, glazing meeting the required fire-resistance class (PN-EN 13501-2) | general WT fire-separation rule, exact § not independently pinned this session | **Probable** — single-source figure (muratorplus.pl / fireproof24.pl summaries), not cross-checked against a second independent source or the primary text |
| Accessible WC dimensions | **1500×1800mm** | already documented `docs/engineering-rules/63-sanitary-fixtures-wt.md`, preset `wc-accessible`, PN-EN 17210 §T.1 | **Confirmed** — reused, not re-researched |

## Sourcing note

Primary-source access to `isap.sejm.gov.pl` was not attempted this session because residential's
and office's prior sessions already established it CAPTCHA-blocks automated fetch — same tooling
gap, not re-tested. All figures above come from secondary legal-reference sites (lexlege.pl,
arslege.pl, architektura.info), cross-checked against each other where the table shows 2- or
3-source agreement; single-source rows are marked **Probable** rather than **Confirmed**. The ZL
III definition itself (§ 209 ust. 2 pkt 3) was fetched and quoted directly, giving it the highest
confidence in this file. **Before any of the "Probable" rows are used for anything beyond a
demonstration project, verify against the actual Dz.U. 2022 poz. 1225 consolidated text**, and
before the fire-separation glazing figure is used for anything, find a second corroborating
source. The service/repair-workshop hall (PM classification, § 228 area table, workshop-specific
ventilation/ACH figures found incidentally during this research) is deliberately **not**
researched here — see ROOM-PROGRAM.md for why it's out of scope for this pass.
