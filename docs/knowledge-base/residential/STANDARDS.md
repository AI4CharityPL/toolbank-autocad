# Residential (multi-family apartment building) — legal & code standards

> **Normy bazowe:** Rozp. MI z 12.04.2002 w sprawie warunków technicznych (tekst jednolity
> **Dz.U. 2022 poz. 1225**, "WT 2024") · PN-ISO 9836 (powierzchnie)

Researched via web search this session against secondary legal-summary sources
(architektura.info, arslege.pl cross-checked against each other), NOT the raw isap.sejm.gov.pl
PDF directly (it returned a CAPTCHA page and an undecodable binary to automated fetch — see
sourcing note). Confidence is marked per row; anything not independently confirmed by 2+ sources
is flagged rather than presented as settled.

## Legal-hierarchy table

| Skrót | Pełna nazwa | Rola w projekcie |
|---|---|---|
| **Pr. bud.** | Ustawa z 7.07.1994 Prawo budowlane (Dz.U. 2024 t.j.) | podstawa procesowa |
| **WT 2024** | Rozp. MI z 12.04.2002 (tj. **Dz.U. 2022 poz. 1225**) | parametry techniczne mieszkań, Dział III Rozdział 5 (pomieszczenia na pobyt ludzi) i Rozdział 7 (mieszkania w budynkach wielorodzinnych) |
| **Ust. dostęp.** | Ustawa z 19.07.2019 (Dz.U. 2019 poz. 1696, tj. Dz.U. 2024) | dostępność części wspólnych |

## Key checkable numbers, with confidence

| Requirement | Value | Citation | Confidence |
|---|---:|---|---|
| Habitable room height | **≥ 2.5 m** (2.5m clear; sloped ceiling avg, not below 1.9m) | WT § 72 | **Confirmed** — matches the figure already cited in `docs/HOSPITAL-2026-MASTERPLAN.md` §13.5 for non-medical rooms, and directly fetched from Rozdział 5 this session |
| Apartment total usable area | **≥ 25 m²** | WT § 94 (Rozdział 7) | **Confirmed** — 2 independent sources agree on both the value and the §94 numbering in the current consolidated text |
| Living rooms / kitchen / kitchenette daylight | direct natural daylight required | WT § 93 | **Confirmed** — 2 independent sources agree |
| Windowless kitchen | permitted **only** in a single-room apartment, with mechanical exhaust (gas) or gravitational (electric) ventilation | WT § 93 ust. 2 | **Confirmed** |
| Kitchen minimum area | **≥ 1.8 m²** (single-room apartment) / ≥ 2.4 m² (multi-room apartment) | WT (Rozdział 7, exact § not independently pinned) | **Probable** — area figures found via secondary sources; the exact paragraph number could not be confirmed against primary text in this pass |
| Internal corridor width | **≥ 1.2 m** clear (narrowing to 0.9 m permitted over a short run) | WT § 95 | **Confirmed** — 2 independent sources agree |
| Apartment composition | kitchen/kitchenette, bathroom, separate WC or WC-in-bathroom, storage space, washing-machine space, internal circulation | WT § 92 | **Confirmed** |
| Bedroom minimum width (1-person / 2-person) | **~2.2 m / ~2.6 m** (figures vary slightly by source, 2.2/2.6 most common) | reported, unconfirmed § | **Unconfirmed** — appears in one early search summary, not reproduced when the actual chapter text was fetched directly; may be an outdated/superseded provision. Do not build a validator rule on this number without primary-source verification. |
| "At least one room ≥ 16 m²" | as stated | reported, unconfirmed | **Unconfirmed** — same status as the width figures above; two direct chapter fetches did not reproduce this requirement. Flagged, not used. |
| Generic habitable-room minimum floor area | none found | — | **Confirmed absent** — Rozdział 5 (which governs habitable rooms generally) specifies only height (§72), floor level (§73), accessibility (§74) and door size (§75); no area figure appears anywhere in it. Polish WT appears to constrain apartment size through the total-apartment floor (§94) and room widths, not a per-room area minimum the way hospital regs (Rozp. MZ 2019) do for patient rooms. |

Bathroom minimum size is already documented and cited in this repo's `docs/engineering-rules/63-sanitary-fixtures-wt.md` — preset `bathroom-residential`, **1600×2200mm, WT-2019 §82** — reused here rather than re-researched.

## Sourcing note

Primary-source access failed twice this session: `isap.sejm.gov.pl` returned a CAPTCHA
verification page to automated fetch, and the raw PDF from `eli.gov.pl` fetched but could not be
decoded by the available tooling (no PDF-rendering binary in this environment). All figures
above come from secondary legal-reference sites (architektura.info, arslege.pl,
architekturaibiznes.pl), cross-checked against each other where possible. **Before this
typology's citations are used for anything beyond a demonstration project, verify the
"Probable"/"Unconfirmed" rows against the actual Dz.U. 2022 poz. 1225 consolidated text.**
