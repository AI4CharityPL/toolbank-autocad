# Dental clinic (gabinet stomatologiczny) — legal & code standards

> **Reference standards:** WT 2024 (Rozp. MI 12.04.2002, tj. **Dz.U. 2022 poz. 1225**) · Rozp. MZ
> 26.03.2019 w sprawie pomieszczeń i urządzeń podmiotu leczniczego (tj. **Dz.U. 2022 poz. 402**)
> · Rozp. MZ 21.08.2006 w sprawie pracy z urządzeniami radiologicznymi (**Dz.U. 2006 nr 180
> poz. 1325**) · Ustawa 19.07.2019 o dostępności (**Dz.U. 2019 poz. 1696**)

Researched via web search this session against secondary legal-summary sources (romident.pl,
mbrk.pl, radiomed-radiologia.pl, promedus.pl, termedia.pl, serwiszoz.pl), cross-checked against
each other where more than one existed — NOT `isap.sejm.gov.pl` directly, which (per this bank's
own prior residential/office research) CAPTCHA-blocks automated fetch; a direct PDF fetch of the
WSSE Kraków sanepid guide was also attempted and failed (binary/undecodable, no PDF-rendering
tool in this environment — same failure mode residential hit with `eli.gov.pl`). Confidence is
marked per row; several secondary sources disagree with each other on which regulation is
currently in force (see Sourcing note) — that disagreement is recorded, not silently resolved.

A dental practice is a **podmiot wykonujący działalność leczniczą** under Ustawa 15.04.2011 o
działalności leczniczej (Dz.U. 2025 poz. 450), art. 22 — the same generic status a hospital or
outpatient clinic has. This means **Rozp. MZ 26.03.2019 (tj. Dz.U. 2022 poz. 402)** — the exact
regulation already cited in `docs/knowledge-base/hospital/STANDARDS.md` as "Rozp. MZ 2019" for
*Zał. 1 = szpitale* — also governs dental practices, but through its generic body text (no
dental-specific annex/załącznik was found; see below), not a hospital-specific annex.

## Legal-hierarchy table

| Skrót | Pełna nazwa | Rola w projekcie |
|---|---|---|
| **Pr. bud.** | Ustawa z 7.07.1994 Prawo budowlane (Dz.U. 2024 t.j.) | podstawa procesowa |
| **WT 2024** | Rozp. MI z 12.04.2002 (tj. **Dz.U. 2022 poz. 1225**) | parametry techniczne budynku jako całości: klasa odporności ogniowej, ewakuacja, wysokość pomieszczeń (§72) |
| **Rozp. MZ 2019** | Rozp. MZ z 26.03.2019 (tj. **Dz.U. 2022 poz. 402**, pierwotnie Dz.U. 2019 poz. 595) | ogólne wymagania dla pomieszczeń i urządzeń podmiotu leczniczego — **§16**: kształt i powierzchnia pomieszczeń muszą umożliwiać prawidłowe rozmieszczenie, zainstalowanie i użytkowanie aparatury/sprzętu. Generic functional test, not a fixed m² figure (see below) |
| **Ust. dział. lecz.** | Ustawa z 15.04.2011 o działalności leczniczej (tj. Dz.U. 2025 poz. 450), art. 22 ust. 1 i 3 | podstawa stosowania Rozp. MZ 2019 do gabinetu stomatologicznego jako podmiotu leczniczego |
| **Pr. atom.** | Ustawa z 29.11.2000 Prawo atomowe (t.j. Dz.U. 2024) | reżim zezwoleń/zgłoszeń dla aparatury RTG, wymóg Inspektora Ochrony Radiologicznej (IOR) od nowelizacji 23.09.2019 |
| **Rozp. MZ RTG 2006** | Rozp. MZ z 21.08.2006 w sprawie szczegółowych warunków bezpiecznej pracy z urządzeniami radiologicznymi (**Dz.U. 2006 nr 180 poz. 1325**) | powierzchnia gabinetu RTG, wentylacja, osłony — **the same citation already used in `docs/knowledge-base/hospital/STANDARDS.md`'s "Lead shielding (CT/RTG)" row**, reused here rather than re-derived |
| **Rozp. RM dawki graniczne** | Rozp. Rady Ministrów z 18.01.2005 (Dz.U. 2005 nr 20 poz. 168) | dawki graniczne promieniowania jonizującego — **currency uncertain**, see Sourcing note |
| **Ust. dostęp.** | Ustawa z 19.07.2019 (Dz.U. 2019 poz. 1696, tj. Dz.U. 2024) | dostępność dla osób ze szczególnymi potrzebami — same citation reused from `docs/knowledge-base/residential/STANDARDS.md` |
| **Ust. ppoż.** | Ustawa z 24.08.1991 o ochronie przeciwpożarowej (t.j. Dz.U. 2024) | klasyfikacja ZL |

## PN-EN / PN-ISO / PN-B standards table

| Standard | Subject | Which layer/system it governs |
|---|---|---|
| **PN-ISO 9836** | definicje powierzchni netto/brutto | `A-ROOM-BNDY` area convention — reused from residential/hospital, see AREA-CONVENTION.md |
| **PN-EN 12464-1** | oświetlenie miejsc pracy (w tym oświetlenie zabiegowe) | `E-LITE` — cited by one secondary source (romident.pl) for gabinet zabiegowy lighting; not independently cross-checked against a second source this session, **Probable** only |
| **EN 13060** | autoklawy parowe klasy B (mała sterylizacja) | sterylizacja room equipment spec, not a layer — cited for completeness since it constrains the sterilization room's equipment footprint |

## Key checkable numbers, with confidence

| Requirement | Value | Citation | Confidence |
|---|---:|---|---|
| Building fire category | **ZL III** (public-use building not otherwise classified ZL I/ZL II — patients are ambulatory, not bedridden, unlike hospital's ZL II) | WT / Ust. ppoż. classification practice | **Probable** — the ZL III catch-all definition is confirmed by multiple secondary sources (same pattern `docs/knowledge-base/office/STANDARDS.md` already flagged for its own ZL III row), exact WT §209 sub-point not independently re-verified against primary text |
| Gabinet zabiegowy (treatment room) minimum area | **none found** — Rozp. MZ 2019 §16 sets only a functional test ("umożliwia prawidłowe rozmieszczenie urządzeń"), no numeric floor | Rozp. MZ 2019 §16 | **Confirmed absent** — 3 independent secondary sources (mbrk.pl, romident.pl ×2) agree the current regulation dropped the fixed m² figure that a predecessor regulation once had |
| Gabinet zabiegowy — historical (superseded) minimum | 12 m² for the first dental chair + 8 m² for each additional chair | reported as a since-repealed rule, traced to Dz.U. 2000 nr 20 poz. 254 (an earlier, no-longer-in-force predecessor regulation) | **Unconfirmed as still in force — explicitly historical.** Recorded because two independent sources (mbrk.pl, one romident.pl page) both cite it as the *old* rule; do not use as a current validator minimum |
| Gabinet zabiegowy — practical/industry convention | **12-20 m²** typical, ~8-9 m² treated by some sources as an ergonomic floor even though not a legal minimum | industry convention, no § citation | **Probable as a design convention, NOT a code minimum** — flagged exactly that way per this bank's rule against presenting a typical value as a requirement |
| Room height (gabinet zabiegowy, generic habitable/workplace) | **≥ 2.5 m** (WT generic) vs **≥ 3 m** (BHP workplace rule, reducible to 2.5 m with AC for ≤4 workers) — two different source acts give two different figures, same ambiguity `docs/knowledge-base/office/STANDARDS.md` already flagged for office workrooms | WT §72 (generic) / Rozp. MPiPS BHP §20 (workplace) — see `docs/knowledge-base/office/STANDARDS.md` for the BHP citation this reuses | **Probable** — which act actually governs a dental treatment room's height was not independently resolved this session; recorded as an open question, not silently picked |
| Gabinet RTG punktowe (point dental X-ray room), one apparatus | **≥ 8 m²** | Rozp. MZ RTG 2006 | **Confirmed** — 2 independent sources agree on both the value and the citation (initial search-engine summary + a dedicated radiomed-radiologia.pl shielding-design page) |
| Gabinet RTG, each additional apparatus in the same room | **+ 4 m²** | Rozp. MZ RTG 2006 | **Confirmed** — same 2 sources; a "5% reduction permissible" caveat also appears in one source, not independently corroborated, flagged separately as **Unconfirmed** |
| RTG room ventilation | **≥ 1.5× air changes per hour** | Rozp. MZ RTG 2006 | **Confirmed** — 2 independent sources agree on both value and citation |
| RTG room lead shielding — gonad shield equivalent | **≥ 1.0 mm Pb** | Rozp. MZ RTG 2006 (personal-protection provisions) | **Confirmed** — 2 independent sources agree |
| RTG room lead shielding — wall/door (structural) | **not a fixed mm figure** — Rozp. MZ RTG 2006 requires a project-specific shielding calculation ("projekt osłon stałych") submitted for and approved by the provincial sanitary inspector before commissioning, sized to the apparatus's kV/workload, not a universal Pb-mm number | Rozp. MZ RTG 2006 | **Confirmed that no universal figure exists** — do not reuse hospital's CT/general-RTG "1-2 mm Pb" figure for a dental point apparatus; dental intraoral units are lower-dose and commonly need thinner or no supplementary lead over standard masonry, but this bank has no verified default — flag as project-specific, not a validator constant |
| Intraoral dental X-ray — permit exemption | Units performing **only** intraoral radiographs with equipment designed exclusively for that purpose are exempt from the provincial sanitary inspector's operating permit otherwise required for RTG units; a "zgłoszenie"/notification and shielding-project approval is still required | Pr. atom. + Rozp. MZ RTG 2006, per a NIL (Naczelna Izba Lekarska) summary | **Probable** — the exemption itself is corroborated by a professional-body source (NIL) and a second industry source (termedia.pl), exact Prawo atomowe article number not independently pinned |
| Inspektor Ochrony Radiologicznej (IOR) required | Yes, since the Prawo atomowe amendment effective 23.09.2019, every facility operating X-ray apparatus must designate one | Pr. atom. (as amended 2019) | **Probable** — reported by 2 industry sources, exact article not independently verified |
| Dose limits, ionizing radiation | worker **20 mSv/year**, general population **1 mSv/year** | Rozp. RM 18.01.2005 (Dz.U. 2005 nr 20 poz. 168) — **or a later implementing regulation post the 2019 Prawo atomowe amendment; sources disagree** | **Unconfirmed which regulation is currently in force** — search results surfaced both the 2005 regulation (Dz.U. 2005 nr 20 poz. 168) and a 2021 Rozp. RM (Dz.U. 2021 poz. 1657) governing dose-*assessment indicators* (not limits per se); which one is the current controlling text for the limit values themselves was not resolved this session. The **values** (20/1 mSv) are corroborated by 2 sources; the **citation** is not. |
| Sterylizacja — minimum stanowisko area | **≥ 4 m²** per sterilization workstation | reported by 2 industry sources (promedus.pl, and a search-engine blend of solutiomedica.pl/romident.pl), no specific § cited by either | **Probable** — industry-practice figure, not traced to a specific paragraph of Rozp. MZ 2019 or any other act; treat as a design convention until a primary-text § is found |
| Sterylizacja — equipment clearance from treatment area | **≥ 1.5 m** from any point where tissue-invasive procedures are performed | same sources as above | **Probable**, same caveat |
| Accessibility (dostępność) | Public entrance, WC, and circulation must meet Ustawa 19.07.2019 minimum requirements — same generic applicability already established for residential shared circulation and hospital public zones | Ust. dostęp. 2019 | **Confirmed applicable by the same reasoning `docs/knowledge-base/residential/STANDARDS.md` already used** — not independently re-derived this session beyond confirming a dental clinic is a public-facing usługi premises the act covers |

Bathroom/WC fixture minimums are already documented and cited in this repo's
`docs/engineering-rules/63-sanitary-fixtures-wt.md` (preset `bathroom-residential`,
1600×2200mm, WT-2019 §82) — reused here for the clinic's patient/staff WC rather than
re-researched, same as residential does for its own WC rows.

## Sourcing note

Primary-source access to `isap.sejm.gov.pl` was not attempted directly this session, on the
basis of residential's and office's own prior experience recorded in this repo (CAPTCHA-blocked
automated fetch); a direct PDF fetch of the WSSE Kraków official sanepid guide for dental clinics
was attempted and also failed (binary/undecodable content, no PDF-rendering tool available in
this environment — the same failure class residential hit with an `eli.gov.pl` PDF). All figures
above come from secondary legal/industry sources (romident.pl, mbrk.pl, radiomed-radiologia.pl,
promedus.pl, termedia.pl/NIL, serwiszoz.pl), cross-checked against each other where more than one
existed.

**A specific discrepancy worth flagging rather than silently resolving:** two romident.pl pages
cite "Rozporządzenie Ministra Zdrowia z dnia 26 czerwca 2012 r. (Dz.U. 2012 poz. 739)" as the
*currently governing* facility-requirements regulation, while a third source (mbrk.pl) cites the
regulation this bank already trusts elsewhere — Rozp. MZ 26.03.2019, consolidated text
**Dz.U. 2022 poz. 402** (the same one `docs/knowledge-base/hospital/STANDARDS.md` cites as
"Rozp. MZ 2019"). The 2012 regulation was itself superseded by the 2019 one; this file follows
the hospital typology's already-established citation for consistency across this bank, but the
disagreement between secondary sources on which act is current is itself evidence that this
citation should be verified against primary text (`isap.sejm.gov.pl` or `dziennikustaw.gov.pl`)
before being load-bearing for a non-demonstration project. **Before this typology's citations are
used for anything beyond a demonstration project, verify every "Probable"/"Unconfirmed" row above
against the actual consolidated Dz.U. text.**
