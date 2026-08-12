# Hospital — legal & code standards

> **Normy bazowe:** AIA CAD Layer Guidelines (2nd Ed.) · ISO 13567 · FGI Guidelines 2022 ·
> HTM 00-01 · Rozp. MZ Dz.U. 2019 poz. 595 · WT 2024 (Dz.U. 2022 poz. 1225) · EN 17210 ·
> IFC 4.3 / ISO 19650

Reorganized from `docs/HOSPITAL-2026-MASTERPLAN.md` §13 (own prior research for this repo, not
re-researched here) — that document remains the authoritative deep-dive; this file is the
navigable summary rule 71 step 2 reads first.

Classification: **budynek użyteczności publicznej, zakład opieki zdrowotnej, kategoria
zagrożenia ludzi ZL II**, budynek niski (N, H ≤ 12 m).

## Legal-hierarchy table

| Skrót | Pełna nazwa | Rola w projekcie |
|---|---|---|
| **Pr. bud.** | Ustawa z 7.07.1994 Prawo budowlane (Dz.U. 2024 t.j.) | podstawa procesowa |
| **WT 2024** | Rozp. MI z 12.04.2002 (tj. **Dz.U. 2022 poz. 1225**), nowelizacje Dz.U. 2023 poz. 2442, Dz.U. 2024 poz. 474, **Dz.U. 2024 poz. 726** | parametry techniczne, bezpieczeństwo pożarowe |
| **Rozp. MZ 2019** | Rozp. MZ z 26.03.2019 (tj. **Dz.U. 2022 poz. 402**, Dz.U. 2019 poz. 595) | wymagania dla pomieszczeń podmiotu leczniczego (Zał. 1 = szpitale) |
| **Ust. dostęp.** | Ustawa z 19.07.2019 (Dz.U. 2019 poz. 1696, tj. Dz.U. 2024) | dostępność |
| **Ust. ppoż.** | Ustawa z 24.08.1991 (t.j. Dz.U. 2024) + Rozp. MSWiA z 7.06.2010 (Dz.U. 2023 poz. 822 t.j.) | instalacje ppoż. |
| **Wytyczne MZ HVAC** | MZ 2018 z aktualizacjami | klasy S1-S4, ACH, HEPA |
| **Pr. atom.** | Ustawa z 29.11.2000 (t.j. Dz.U. 2024) + Rozp. MZ z 21.08.2006 (Dz.U. 2006 nr 180 poz. 1325) | ochrona radiologiczna RTG/TK |
| **Pr. farm.** | Ustawa z 6.09.2001 + EU GMP Annex 1 (2022) | apteka szpitalna, clean room |

## PN-EN / PN-ISO / PN-B table

| Standard | Subject | Layer/system |
|---|---|---|
| PN-EN 13501-1 | klasyfikacja ogniowa wyrobów | materiały wykończeniowe |
| PN-EN 1838 | oświetlenie awaryjne | `E-LITE-EMER` |
| PN-EN 12464-1 | oświetlenie miejsc pracy | `E-LITE` |
| PN-EN 13779 / PN-EN 16798-3 | wentylacja niemieszkalna | `M-HVAC` |
| PN-EN ISO 14644-1:2016 | czystość pyłowa (ISO 5 OR, ISO 7 PACU) | S1/S2 |
| PN-EN 1822 | filtry HEPA H13/H14 | `M-HVAC-HEPA` |
| PN-EN ISO 16890-1 | filtry wstępne | `M-HVAC` |
| PN-B-02151-02/-03/-04 | akustyka | izolacja akustyczna |

## Key checkable numbers (validator-relevant, `hospital-baseline.yaml`)

| Requirement | Value | Citation |
|---|---:|---|
| Fire-resistance class | **B** | WT § 212 ust. 2 (ZL II + niski N) |
| Max fire-zone area | **3 500 m²** | WT § 227 ust. 1 |
| Single-bed room min area | **≥ 15 m²** net + ensuite **≥ 4.5 m²** | Rozp. MZ 2019, this repo's `hospital.rooms.patient-room-min-area` / `ensuite-min-area` |
| ICU bay min area | **≥ 15 m²** | rule 64 preset floor, `hospital.rooms.icu-room-min-area` |
| OR min area (general/hybrid) | **≥ 36 m²** floor (endoscopic), 48 m² general, 72 m² hybrid | `hospital.rooms.or-min-area` (36 m² is the checked floor — see AREA-CONVENTION.md for why) |
| Evacuation: exits per ZL II zone | **≥ 2** | WT § 236 |
| Evacuation: max travel, 2 directions | **30 m** | WT § 237 ust. 6 pkt 2 |
| Public corridor width | **2.8 m** (revised up from 2.4 m per this repo's own calc) | WT § 242 |
| Sterile corridor width | **2.8 m**, 25 ACH | Wytyczne MZ HVAC |
| Dirty corridor width | **2.2 m** | Wytyczne MZ HVAC (stretcher clearance, not 1.4 m) |
| OR cleanroom class | **ISO 5** (S1a, general/hybrid), ISO 7 (S1c, endoscopic) | PN-EN ISO 14644-1 |
| AIIR pressure / ACH | **−5 Pa / ≥ 12 ACH**, mandatory anteroom | Wytyczne MZ |
| Lead shielding (CT/RTG) | **Pb ≥ 1-2 mm** depending on modality | Pr. atom. + Rozp. MZ 2006 — `A-WALL-LEAD`, `hospital.walls.lead-shield-on-layer` |
| MRI Faraday cage | copper ≥0.5mm or RF-shielded steel | `A-WALL-FARA`, `hospital.walls.faraday-on-layer` |
| Room height, patient/OR | **3.3 m net** (OR: 4.2 m structural w/ technical ceiling) | WT § 72 |

Full detail (fire-zone geometry, evacuation door widths, acoustics table, lighting table,
accessibility corrections) stays in `docs/HOSPITAL-2026-MASTERPLAN.md` §13.2-13.11 — not
duplicated here to avoid the citation drifting out of sync in two places.

## Sourcing note

All citations above trace to this repo's own `docs/HOSPITAL-2026-MASTERPLAN.md`, itself
researched in an earlier session (not verified against isap.sejm.gov.pl primary text in this
pass — flag for a future citation-accuracy audit if this typology's numbers are ever load-bearing
for a real, non-demonstration project).
