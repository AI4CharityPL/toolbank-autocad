# Hospital — typical room program

Reorganized from `docs/HOSPITAL-2026-MASTERPLAN.md` §2. Full "uwaga" detail per room stays in
that document; this file is the navigable summary.

## Zones

| Zone | Purpose | Typical gross area |
|---|---|---:|
| A — Public | entrance, registration, retail | ~380 m² |
| B — Emergency (SOR) | triage through observation, incl. 2× AIIR + anteroom | ~720 m² |
| C — Diagnostics/lab | CT, MRI, X-ray, USG, ECHO, PACS, point-of-care lab | ~580 m² |
| D — OR + PACU | pre-op, sterile core, 4× OR (incl. hybrid), PACU | ~890 m² |
| E — Back-of-house | pharmacy, sterilization, laundry, plant rooms | ~520 m² |
| F/G — Exterior | ambulance apron (40×10m), biophilic courtyard (18×12m) | — |

## Room counts per zone (see masterplan §2 for the full per-room table)

- **Zone A**: 9 rooms (vestibule, lobby/reg, waiting, OTC pharmacy, cafe, 2× WC + universal WC,
  customer service).
- **Zone B**: 21 rooms incl. triage, registration, waiting, crash room (45 m²), pediatric ED,
  psychiatric safe room, 2× fast-track, 4× observation bay, minor-surgery room, on-call room,
  central nurse pod, clean/dirty utility, equipment store, stretcher bay, patient WC, **2× AIIR
  (16 m²) + 2× anteroom (4 m²)**.
- **Zone C**: 19 rooms incl. CT (42 m² + 14 m² control), MRI 3T (52 m² + 16 m² control + 6 m²
  prep), 2× X-ray + control (28/18 m²), USG, ECHO, PACS reading room, POC lab, phlebotomy,
  contrast store, tech room.
- **Zone D**: 34 rooms incl. 2× staff locker/airlock, patient airlock, 6× pre-op bay, pre-op
  nurse station, **sterile corridor (78 m², 2.8 m wide, 25 ACH)**, OR-1/OR-2 (48 m² general),
  OR-3 (36 m², endoscopic), **OR-4 hybrid (72 m², built-in biplane C-arm)**, sterile core, 2×
  scrub, clean/dirty utility, **dirty corridor (62 m², 2.2 m wide — stretcher clearance, not the
  generic 1.4 m minimum)**, anaesthesia store, med-gas manifold, 8× PACU bay, PACU nurse station,
  family consult, anaesthesiologist office.
- **Zone E**: 17 rooms incl. hospital pharmacy (dispensing + ISO-7 cleanroom + controlled store),
  4-room sterilization suite (dirty → packing → autoclave → clean/sterile store), central clean
  store, dirty store + laundry, kitchen distribution, staff social room, 2× staff locker room,
  AHU plant room (84 m², 3 air handlers), electrical room (UPS), server room, housekeeping.

## Adjacency / connectivity requirements

This is the check that would have caught the disconnected-zones defect (rule 71's incident)
immediately instead of after full elaboration:

| Zone A | Zone B | Requirement | Why |
|---|---|---|---|
| Public | SOR | direct door(s), public-facing | walk-in patients arrive from the public zone |
| Public | Diagnostics | direct corridor connection | outpatients referred to imaging |
| SOR | Diagnostics | direct, fast path | emergency imaging without crossing public zone |
| Diagnostics | OR+PACU | direct, staff-only | intraoperative imaging, pre-op diagnostics |
| OR+PACU | Back-of-house | direct, sterile-to-supply | sterile core resupply from E-zone sterilization |
| Every zone | Every other zone | at least one path via the corridor spine | fire egress requires ≥2 exits per ZL II zone (WT §236) reachable without crossing an unconnected zone |

Every zone in the program above must appear in a connected adjacency graph before detailed
geometry starts — a zone with zero planned connections to the rest of the building is a defect
to fix at the bubble-diagram stage, not something to discover in a finished drawing.

## Wzorzec stref i cyrkulacji

Linear zone spine with a strict clean/dirty and public/staff separation: **Public (A)** sits at
the building entry and feeds directly into **SOR/Emergency (B)** (walk-in access) and into
**Diagnostics (C)** (outpatient referrals) without either needing to cross the other. **OR+PACU
(D)** sits deepest in the plan, staff-only, reached from Diagnostics (intraoperative imaging) and
resupplied from **Back-of-house (E)**'s sterilization suite via a dedicated sterile corridor —
never via the public corridor. This is the pattern rule 73 step 3 generalizes: public/day-facing
zones near the entry, the most private/controlled zone (D) furthest from it, connected through
zones that mediate rather than directly.

## Sourcing note

Room list and areas are this repo's own prior research (`HOSPITAL-2026-MASTERPLAN.md` §1-2),
blending FGI Guidelines 2022 and Rozp. MZ 2019 minimums, taking the higher requirement where
they differ. Not re-verified against a real hospital reference drawing in this pass — the real
DWG the user supplied is a **specialist outpatient clinic** (trauma-orthopedic, lab, X-ray/USG/
EMG/ENG), not a full inpatient hospital with OR/ICU, so it grounds the diagnostics-zone program
better than the OR/inpatient zones; a future pass should reconcile Zone C against it directly.
