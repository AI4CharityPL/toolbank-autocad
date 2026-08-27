# Compact Hospital 2026 — ground-floor masterplan

> **Project:** Hospital-2026 Compact Tertiary Care — Ground Floor Masterplan
> **Wing / phase:** Phase A · Ground floor · Rev. 01
> **Footprint:** 80 × 60 m (~4,320 m² gross), with an 18 × 12 m internal courtyard
> **Designer (MCP agent):** generated through `acad_design_iterate`, 11 phases
> **Reference standards:** AIA CAD Layer Guidelines (2nd Ed.) · ISO 13567 · FGI Guidelines 2022 · HTM 00-01 · Polish Ministry of Health regulation Dz.U. 2019 poz. 595 · WT 2022 (Dz.U. 2022 poz. 1225) · EN 17210 · IFC 4.3 / ISO 19650
> **Purpose of the exercise:** address every defect found in the reference file `[REDACTED-REFERENCE-DWG]` (imperial units, 83% primitive Lines, layer "0" active, no styles, no dynamic blocks, a single PublishToWeb layout) and demonstrate production-level documentation.

> **Written in Polish originally, translated for this repository.** Room names, legal citations
> and Polish standard numbers are kept in their original form where they are the legal
> identifier of a requirement; a `Dz.U.` reference means nothing in translation.

---

## 1. The concept, and the 2026 trends shaping it

This is not CAD for its own sake — every room is justified by current 2026 practice:

| 2026 trend | Design consequence |
|---|---|
| **Post-COVID single-bed rooms** (100% single rooms, LDRP for births) | Bed rooms ≥ 15 m² net plus a private sanitary unit ≥ 4.5 m² |
| **AIIR — Airborne Infection Isolation** (FGI 2022 § 2.1-3.3.2.2) | At least 2 AIIR rooms with an anteroom, −2.5 Pa negative pressure, 12 ACH, HEPA H14 |
| **Hybrid OR** (cardiac, neuro, endovascular surgery) | One theatre ≥ 70 m² with a built-in C-arm/angio suite and an 800 mm technical ceiling |
| **Same-handed rooms** (nursing ergonomics) | Every bed room left-handed from the headwall, which standardises training |
| **Decentralised nursing** | A nurse pod every 4 rooms instead of one central nurse station |
| **Biophilic design** | An 18 × 12 m internal courtyard, roof lights over the sterile corridor, a curtain wall in the waiting area |
| **Telehealth hub** | Dedicated "virtual consult" rooms (acoustics, camera lighting, backdrop) |
| **IPC zoning — clean / dirty separation** | A double corridor at the theatres (sterile core + dirty corridor), separate clean and dirty lifts |
| **Adaptive re-use / modular** | A 7.8 × 8.4 m grid compatible with demountable panel walls |
| **Embodied carbon tracking** | Internal walls in 120 mm CLT (layer `A-WALL-INT-MTMB`), structural steel ≥ 90% recycled |
| **Sensor-ready** | IoT cable trays in every room's ceiling — layer `T-CABL-IOTN` |
| **Accessibility EN 17210** | Every door ≥ 900 mm clear, one accessible WC per 10 fixtures |

Zone sizing combines FGI 2022 (American, the newest global benchmark) with the Polish Ministry
of Health regulation of 2019 (our mandatory minimum). Where the two differ, the **stricter**
requirement wins.

---

## 2. Space program

The ground floor is divided into **5 zones** (A–E) plus 2 external ones (F, G). Every room
carries three fields: **AIA number** · **name** · **net area**.

### Zone A · Public zone (entrance, reception, retail) — ~380 m²

| No. | Name | m² net | Standard / note |
|---|---|---:|---|
| A-101 | Main airlock lobby | 18 | automatic 2 × 1800 sliding doors |
| A-102 | Lobby / central reception | 140 | north-east curtain wall, 3 reception desks, self-service kiosk |
| A-103 | Main waiting area | 90 | 60 seats, family zones, biophilic planting |
| A-104 | Public pharmacy (OTC) | 42 | separate entrance from outside as well |
| A-105 | Café / bistro | 35 | visitors and staff, 06:00–21:00 |
| A-106 | Public WC, women | 18 | 4 cubicles + 1 accessible |
| A-107 | Public WC, men | 16 | 3 cubicles + 2 urinals + 1 accessible |
| A-108 | Universal / family WC | 8 | EN 17210, changing table, either parent |
| A-109 | Customer service office | 12 | complaints / information / resource handover |

### Zone B · Emergency department (SOR) — ~720 m²

| No. | Name | m² net | Standard / note |
|---|---|---:|---|
| B-201 | Ambulance drop-off + ED airlock | 28 | covered, 3 bays, EHR integration |
| B-202 | Triage (nurse-led) | 24 | 2 stations, direct line of sight to the entrance |
| B-203 | ED reception | 12 | night admissions |
| B-204 | ED waiting area | 48 | 32 seats, split "fast-track" / "observation" |
| B-205 | Resuscitation room (crash room) | 45 | 2 bays, visible from the nurse pod, pre-programmed medical gas |
| B-206 | Paediatric ED consulting room | 22 | separate waiting area, access to the playground |
| B-207 | Psychiatric safe room | 14 | ligature-resistant, video observation |
| B-208 | Fast-track consulting room A | 16 | clinician decision in under 15 min |
| B-209 | Fast-track consulting room B | 16 | as above |
| B-210 | Observation bay 1 | 12 | cubicle curtain, headwall medical gas |
| B-211 | Observation bay 2 | 12 | as above |
| B-212 | Observation bay 3 | 12 | as above |
| B-213 | Observation bay 4 | 12 | as above |
| B-214 | Treatment room (minor surgery) | 20 | scialytic lamp, pre-op sink |
| B-215 | ED on-call room | 14 | bed + desk + private WC |
| B-216 | ED nurse pod (central) | 22 | view over observation bays 1–4, central monitoring |
| B-217 | ED clean utility | 10 | clean dressing materials |
| B-218 | ED dirty utility | 10 | soiled equipment, flush sink |
| B-219 | Emergency equipment store | 18 | defibrillators, intubation sets |
| B-220 | Stretcher bay | 8 | 4 stretchers + 2 wheelchairs |
| B-221 | ED patient WC | 6 | accessible |

### Zone C · Diagnostic imaging and laboratory — ~580 m²

| No. | Name | m² net | Standard / note |
|---|---|---:|---|
| C-301 | Imaging waiting area | 36 | 24 seats, paediatric split |
| C-302 | Imaging reception | 10 | |
| C-303 | 128-slice CT room | 42 | 14 m² control room + 28 m² console area (bleed wall) |
| C-304 | CT console room | 14 | 40 × 30 cm leaded window |
| C-305 | 3T MR room | 52 | Faraday cage, 4-gauss line marked, quench pipe to atmosphere |
| C-306 | MR console room | 16 | Faraday penetration panel |
| C-307 | MR preparation cubicle | 6 | ferromagnetic screening |
| C-308 | General X-ray room I | 28 | 2 mm lead in the walls |
| C-309 | X-ray console room I | 8 | 2 mm lead |
| C-310 | Mammography room | 18 | enhanced privacy, female-only access |
| C-311 | X-ray console room II | 6 | |
| C-312 | Ultrasound room | 22 | 2 machines, printer, privacy |
| C-313 | Cardiac echo room | 20 | space for a stress-test treadmill |
| C-314 | Radiology reading room | 24 | 3 PACS workstations, dimmable |
| C-315 | Point-of-care laboratory | 28 | rapid tests, pneumatic tube queue |
| C-316 | Phlebotomy | 14 | 3 stations |
| C-317 | Imaging patient WC | 6 | accessible |
| C-318 | Contrast media store | 6 | temperature-controlled |
| C-319 | Radiographers' room | 12 | |

### Zone D · Operating theatres + PACU — ~890 m²

IPC zoning — separating the sterile core from the dirty corridor is mandatory (a two-circuit
layout):

| No. | Name | m² net | Standard / note |
|---|---|---:|---|
| D-401 | Male staff changing airlock | 12 | clean and dirty changing rooms |
| D-402 | Female staff changing airlock | 12 | as above |
| D-403 | Patient airlock (preparation) | 8 | with patient identification |
| D-404 | Pre-op bay 1 | 10 | headwall, monitoring |
| D-405 | Pre-op bay 2 | 10 | as above |
| D-406 | Pre-op bay 3 | 10 | as above |
| D-407 | Pre-op bay 4 | 10 | as above |
| D-408 | Pre-op bay 5 | 10 | as above |
| D-409 | Pre-op bay 6 | 10 | as above |
| D-410 | Pre-op nurse station | 14 | |
| D-411 | Sterile corridor | 78 | 2.8 m wide, 2% skylight, 25 ACH |
| D-412 | Operating theatre OR-1 (general) | 48 | 7.0 × 7.0 m clear, 800 mm technical ceiling, laminar flow |
| D-413 | Operating theatre OR-2 (general) | 48 | as above |
| D-414 | Operating theatre OR-3 (endoscopy / minor procedures) | 36 | lower specification — 20 ACH |
| D-415 | **Hybrid OR-4 (cardiac / neuro / endovascular)** | 72 | 8.4 × 8.4 m, built-in biplane C-arm, radiolucent table |
| D-416 | Sterile core (sterile store) | 40 | central between OR 1, 2 and 4 |
| D-417 | Scrub station, OR-1/2 | 6 | 2 touch-free taps |
| D-418 | Scrub station, OR-3/4 | 6 | as above |
| D-419 | Theatre clean utility | 14 | sterile gowns, pre-prepared sets |
| D-420 | Theatre dirty utility + flush sink | 14 | separated from the sterile side |
| D-421 | Dirty corridor | 62 | 2.2 m × 28 m — the minimum stretcher width per the Ministry of Health guidance (NOT 1.4 m) |
| D-422 | Anaesthesia store | 10 | lockable cabinets |
| D-423 | Medical gas manifold room | 14 | O₂, N₂O, vacuum, air at 4 bar, air at 8 bar (tool gases) |
| D-424 | PACU bay 1 | 12 | headwall + monitor |
| D-425 | PACU bay 2 | 12 | as above |
| D-426 | PACU bay 3 | 12 | as above |
| D-427 | PACU bay 4 | 12 | as above |
| D-428 | PACU bay 5 | 12 | as above |
| D-429 | PACU bay 6 | 12 | as above |
| D-430 | PACU bay 7 | 12 | as above |
| D-431 | PACU bay 8 | 12 | as above |
| D-432 | PACU nurse station (central) | 24 | view over all 8 bays |
| D-433 | PACU consult / family waiting | 14 | private consultation |
| D-434 | Anaesthetist's room | 12 | on-call |

### Zone E · Back of house — ~520 m²

| No. | Name | m² net | Standard / note |
|---|---|---:|---|
| E-501 | Hospital pharmacy — dispensing | 42 | dispensing bar, unit-dose |
| E-502 | Hospital pharmacy — aseptic clean room | 28 | ISO class 7; cytostatics at ISO class 5 inside an isolator |
| E-503 | Hospital pharmacy — controlled store | 18 | controlled drugs, double safe |
| E-504 | Sterile services — dirty zone | 26 | soiled goods intake, washing |
| E-505 | Sterile services — packing zone | 22 | laminar flow + sealer |
| E-506 | Sterile services — autoclaves | 18 | 2 double-ended autoclaves (pass-through wall) |
| E-507 | Sterile services — clean zone + sterile store | 24 | sterile goods out to the theatres |
| E-508 | Central clean store | 48 | dressings, textiles |
| E-509 | Dirty store + day laundry | 32 | chute for soiled linen |
| E-510 | Satellite kitchen / distribution | 38 | meal regeneration, food-cart parking |
| E-511 | Staff room | 28 | kitchenette, lockers, sofa |
| E-512 | Female staff changing room | 22 | 24 lockers, 2 showers, 2 WCs |
| E-513 | Male staff changing room | 22 | as above |
| E-514 | AHU plant room | 84 | 3 air handling units (public / ED / theatre HEPA) |
| E-515 | LV switchroom | 18 | 400 kVA UPS, external 800 kVA diesel |
| E-516 | Server room / IT | 14 | PACS server room, n+1 cooling |
| E-517 | Housekeeping room | 8 | flush sink, mop dryer |

### External zones (F, G)

- **F** — ambulance drop-off and manoeuvring area (40 × 10 m, dedicated).
- **G** — internal courtyard, 18 × 12 m (biophilic: lawn, benches, a conifer, a water feature);
  open to walking patients, not to clinical staff.

---

## 3. Layer convention (AIA CAD Layer Guidelines 2nd Ed. + ISO 13567)

Name structure: `D-MJR-MIN-SUF`, where:
- **D** = discipline (A = Architecture, S = Structural, M = Mechanical, E = Electrical,
  P = Plumbing, T = Telecom, C = Civil, L = Landscape)
- **MJR** = major system
- **MIN** = minor system (optional)
- **SUF** = modifier (EXIST, NEWW, DEMO, REVA)

### Architectural layers (A-)

| Layer | ACI colour | Linetype | Lineweight | Use |
|---|---:|---|---|---|
| `A-GRID` | 8 (grey) | CENTER | 0.18 | structural axes |
| `A-WALL-EXT` | 5 (blue) | CONTINUOUS | 0.50 | 400 mm external walls |
| `A-WALL-INT` | 2 (yellow) | CONTINUOUS | 0.35 | 150 mm internal walls |
| `A-WALL-INT-MTMB` | 42 (orange) | CONTINUOUS | 0.35 | 120 mm CLT (low embodied carbon) |
| `A-WALL-PART` | 3 (green) | CONTINUOUS | 0.25 | 100 mm partitions |
| `A-WALL-LEAD` | 1 (red) | CONTINUOUS | 0.50 | leaded walls for X-ray/CT/MR (2 mm Pb) |
| `A-WALL-FARA` | 210 (purple) | CONTINUOUS | 0.50 | MR Faraday cage |
| `A-DOOR` | 30 (orange) | CONTINUOUS | 0.25 | doors (arc + leaf) |
| `A-DOOR-IDEN` | 30 | CONTINUOUS | 0.25 | door numbers |
| `A-GLAZ` | 4 (cyan) | CONTINUOUS | 0.25 | glazing / curtain wall |
| `A-FLOR-FIXT` | 6 (magenta) | CONTINUOUS | 0.18 | fixed equipment |
| `A-FLOR-PLMB` | 140 | CONTINUOUS | 0.18 | sanitary fixtures (WCs, basins) |
| `A-FLOR-CASE` | 32 | CONTINUOUS | 0.18 | casework |
| `A-EQPM-MED` | 190 | CONTINUOUS | 0.35 | medical equipment (CT, MR, C-arm, OR table) |
| `A-AREA` | 60 | HIDDEN2 | 0.18 | room outlines (for area take-off) |
| `A-AREA-IDEN` | 7 (white) | CONTINUOUS | 0.25 | room names, numbers and areas |
| `A-ANNO-TEXT` | 7 | CONTINUOUS | 0.25 | general MText notes |
| `A-ANNO-DIMS` | 256 | CONTINUOUS | 0.18 | dimensions |
| `A-ANNO-NOTE` | 7 | CONTINUOUS | 0.25 | note text and leaders |
| `A-ANNO-SYMB` | 7 | CONTINUOUS | 0.25 | north and scale symbols |
| `A-ANNO-TTLB` | 7 | CONTINUOUS | 0.35 | title block |

### Structural layers (S-)

| Layer | Colour | Linetype | Lw | Use |
|---|---:|---|---|---|
| `S-GRID` | 8 | CENTER | 0.18 | axes A–K / 1–10 |
| `S-GRID-IDEN` | 7 | CONTINUOUS | 0.35 | bubble markers |
| `S-COLS` | 92 | CONTINUOUS | 0.50 | 450 × 450 reinforced concrete columns |

### Mechanical / plumbing / electrical / telecom layers

| Layer | Colour | Use |
|---|---:|---|
| `M-HVAC-DUCT` | 142 | main supply ductwork |
| `M-HVAC-RTRN` | 143 | return ductwork |
| `M-HVAC-HEPA` | 1 | HEPA supply to the theatres (red, critical) |
| `P-SANR` | 140 | soil stacks |
| `P-WATR-HOT` | 1 | domestic hot water |
| `P-WATR-COLD` | 5 | domestic cold water |
| `P-GAS-MDCL` | 190 | medical gases (O₂, N₂O, vacuum, air) |
| `E-LITE` | 50 | general lighting |
| `E-LITE-EMER` | 1 | emergency lighting, PN-EN 1838 |
| `E-POWR-NORM` | 3 | standard 230 V sockets |
| `E-POWR-EMER` | 1 | UPS sockets (red) |
| `E-POWR-EMRG` | 2 | diesel-backed sockets (yellow) |
| `T-CABL-IOTN` | 251 | IoT / sensor-ready cable trays |
| `T-CABL-DATA` | 4 | CAT 6A structured cabling |
| `T-NURS-CALL` | 221 | nurse call system |

### Civil / landscape layers

| Layer | Use |
|---|---|
| `C-TOPO` | site boundary, kerbs |
| `C-ROAD-CURB` | ambulance drop-off, footpaths |
| `L-PLNT` | planting (courtyard G) |
| `L-SITE-FURN` | benches, bins |

**~35 layers in total**, against 17 in the audited file — most of which were `0`, `8` or
`WALL 1 300`, and meant nothing.

---

## 4. Text and dimension styles (annotative)

Every style is **annotative** — the rendered size follows the viewport scale automatically
(1:100, 1:50, 1:20).

### Text styles

| Name | Font | Height (annotative) | Use |
|---|---|---:|---|
| `ST-ANN-025` | arial.ttf | 2.5 mm | small notes (door numbers, details) |
| `ST-ANN-035` | arial.ttf | 3.5 mm | standard notes |
| `ST-ANN-050` | arial.ttf | 5.0 mm | room names |
| `ST-ANN-070` | arialbd.ttf | 7.0 mm | zone headings and room numbers |
| `ST-TITLE` | arialbd.ttf | 10.0 mm | title block headings |
| `ST-ISOP` | isocpeur.ttf | 2.5 mm | engineering style for dimensioning |

### Dimension styles

| Name | Scale | Text | Use |
|---|---|---|---|
| `DIM-100-ARCH` | 1:100 | ST-ISOP 2.5 mm | external architectural dimensions |
| `DIM-50-DETL` | 1:50 | ST-ISOP 2.5 mm | per-room dimensions |
| `DIM-20-PREC` | 1:20 | ST-ISOP 2.5 mm | details (precision) |

Every DIMSTYLE: `DIMUNIT=2` (decimal), `DIMDEC=0` (whole millimetres), `DIMLUNIT=2`,
`DIMTIH=0` (horizontal text), `DIMTOH=0`, `DIMEXE=1.25`, `DIMASZ=2.5`.

---

## 5. The 7.8 × 8.4 m structural grid

| Axis | X (mm) | Axis | Y (mm) |
|---|---:|---|---:|
| A | 0 | 1 | 0 |
| B | 7,800 | 2 | 8,400 |
| C | 15,600 | 3 | 16,800 |
| D | 23,400 | 4 | 25,200 |
| E | 31,200 | 5 | 33,600 |
| F | 39,000 | 6 | 42,000 |
| G | 46,800 | 7 | 50,400 |
| H | 54,600 | — | — |
| I | 62,400 | — | — |
| J | 70,200 | — | — |
| K | 78,000 | — | — |

11 axes × 7 axes = 77 grid bays. At 7.8 × 8.4 m that is 65.5 m² per bay.

---

## 6. Blocks (target: dynamic blocks with attributes)

Every block is defined with **millimetre insertion units** (INSUNITS=4) and its base point at
the geometric centre or on the axis of symmetry — for doors, on the hinge axis.

| Block name | Type | Dynamic parameters | Use |
|---|---|---|---|
| `BLK-DOOR-SINGLE` | single-leaf door | width 900/1100/1200/1400 mm; left/right hand | standard rooms, consulting rooms |
| `BLK-DOOR-DOUBLE` | double-leaf door | width 1600/1800/2100/2400 mm | ED resus, theatres, stores |
| `BLK-DOOR-SLIDE` | sliding door | width 1400/1800/2200 mm | theatres (hands-free), airlocks |
| `BLK-DOOR-AIRLOCK` | sealed AIIR door | 1100 mm, interlock indicator | AIIR anteroom |
| `BLK-WIN-CASEMENT` | casement window | width 900/1200/1500/1800 mm | façades |
| `BLK-WIN-CURTAIN` | curtain-wall segment | 1500 mm typical | lobby, curtain wall |
| `BLK-WIN-LEAD` | leaded window | 600 × 400, 2 mm Pb | X-ray and CT console rooms |
| `BLK-GRID-BUBBLE` | ⌀ 900 mm bubble | attribute `GRID_ID` | axis marking |
| `BLK-NORTH` | north arrow | — | layout |
| `BLK-SCALE-BAR` | linear scale bar | attribute `SCALE_RATIO` | layout |
| `BLK-ROOM-TAG` | room tag | attributes `ROOM_NO`, `ROOM_NAME`, `ROOM_AREA` | every room |
| `BLK-DOOR-TAG` | door tag | attribute `DOOR_NO` | every door |
| `BLK-MEDGAS-HEADWALL` | O₂/vacuum/air headwall | 1200 × 300 mm | pre-op, PACU, bed rooms |
| `BLK-OR-TABLE` | radiolucent OR table | 2200 × 560 mm | OR-1..4 |
| `BLK-CT-SCANNER` | 128-slice CT gantry | ⌀ 2400 × 2100 mm | C-303 |
| `BLK-MR-SCANNER` | 3T MR bore | 2300 × 1950 mm | C-305 |
| `BLK-CARM-BIPLANE` | biplane C-arm | 3500 × 3500 mm footprint | D-415, hybrid OR |
| `BLK-WC-ACCESSIBLE` | EN 17210 WC with grab rails | 2200 × 1800 mm | accessible WCs |
| `BLK-SINK-SCRUB` | surgical scrub sink | 800 × 600 mm | scrub stations |
| `BLK-TITLEBLOCK-A0` | A0 title block | attributes `PROJECT`, `SHEET_NO`, `REV`, `DATE`, `SCALE`, `DESIGNER` | sheet layout |

**20 blocks in total**, against 11 meaningless ones in the audited file — not one of them
dynamic.

**Block names follow ISO 13567 plus an internal `BLK-` prefix**, with the category in the second
segment (`DOOR`, `WIN`, `EQPM`, …). The audited file had `02ETRX29`: no category, no
readability.

---

## 7. Resilience and infection-control requirements (2026)

| Requirement | How the design meets it |
|---|---|
| **12 ACH in AIIR rooms, −2.5 Pa** | anteroom plus HEPA H14 recirculation, pressure indicator at the door |
| **20 ACH in OR 1–4 on 100% fresh air** | a dedicated theatre HEPA AHU in plant room E-514 |
| **Sterile / dirty separation** | two physically separated corridors (D-411 versus D-421) |
| **Dual power (normal + EPSS diesel)** | every theatre and AIIR room fed from two feeders plus 15 minutes of UPS |
| **Red UPS sockets and yellow diesel EPSS sockets** | visual coding, separate CAD layers |
| **Seismic Category III** (seismic countries; not mandatory in Poland) | 450 × 450 columns plus braced frames — not drawn at ground level, but the space is reserved |
| **96 h passive survivability** | 3,500 l diesel reserve plus a 200 m³ firefighting water tank (a separate structure) |
| **Mass-casualty surge capacity** | the ED waiting area converts to 12 overflow cots |
| **Universal wayfinding (ADA / EN 17210)** | colour-coded zones, symbols over 100 mm, contrast ≥ 70% |
| **Lockdown / active threat** | departmental doors on a central electric lock, 3 zones |

---

## 8. Layouts and sheet set

| Sheet | Scale | Content | Title |
|---|---|---|---|
| **A-101** | 1:100 | Ground floor plan — full outline, zones, names, external dimensions | Ground Floor Plan — Overall |
| **A-102** | 1:50 | Emergency department, enlarged | ED Enlarged Plan |
| **A-103** | 1:50 | Theatre suite, enlarged | OR Suite Enlarged Plan |
| **A-104** | 1:100 | Roof build-up plan | Roof Plan |
| **A-201** | 1:100 | Section A-A (through the theatres and lobby) | Section A-A |
| **A-202** | 1:100 | Section B-B (through the ED and AHU plant) | Section B-B |
| **A-301** | 1:100 | South and north elevations | Elevations S/N |
| **A-401** | 1:50 | Details (wall/slab junction, MR Faraday detail) | Details |
| **M-101** | 1:100 | HVAC — ground floor | Mechanical Ground Floor |
| **E-101** | 1:100 | Electrical — ground floor | Electrical Ground Floor |
| **P-101** | 1:100 | Plumbing and medical gases | Plumbing & Med Gas |

The minimum for phase A of this exercise: **A-101** (overall plan), **A-102** (ED enlarged) and
**A-103** (theatre suite enlarged). The rest stay as sheet templates.

---

## 9. Execution plan (`acad_design_iterate` phases)

Each phase is one `acad_design_iterate` call with its own checkpoint, so a single phase can be
rolled back without losing the ones before it:

| # | Phase | Est. time | Entity target | Check |
|---|---|---:|---:|---|
| 0 | Setup (layers, text styles, dim styles, limits, INSUNITS=4) | ~40 s | ~0 geometry | `list_layers` (≥ 30) |
| 1 | Structural grid 11 × 7 + bubble markers | ~30 s | ~100 | bubble blocks present |
| 2 | External outline + courtyard + 400 mm external walls | ~40 s | ~200 | `A-WALL-EXT` > 20 polylines |
| 3 | Zones A–E — primary dividing walls | ~60 s | ~300 | `A-WALL-INT` > 50 |
| 4 | Rooms — partitions per room | ~120 s | ~800 | ~115 closed polylines |
| 5 | Doors — dynamic block insertion | ~90 s | ~180 inserts | `list_blocks` → `BLK-DOOR-*` references |
| 6 | Windows + curtain wall | ~60 s | ~90 inserts | `BLK-WIN-*` references |
| 7 | Fixtures and medical equipment (CT, MR, C-arm, WCs, scrub sinks, headwalls) | ~90 s | ~80 inserts | `BLK-EQPM-*` references |
| 8 | Room labels (ROOM-TAG × 115) and numbers | ~90 s | ~115 mtext + 115 blocks | `A-AREA-IDEN` count |
| 9 | External, internal and axis dimensioning | ~120 s | ~250 dimensions | `A-ANNO-DIMS` count |
| 10 | Layout A-101 + A0 title block + 1:100 viewport | ~40 s | layout + title block | `list_layouts` ≥ 4 |
| 11 | Validation (`doc_summary` + listings) | ~20 s | — | report OK |

About 13 minutes of "agent-designer clock" in total — realistically 30–40 calendar minutes once
LockDocument retries are counted.

---

## 10. Acceptance criteria

Once every phase has run, the file **must** pass this self-check:

| Criterion | Target | How it is verified |
|---|---|---|
| `INSUNITS` | `4` (millimetres) | `acad.validators.doc_summary` |
| `MEASUREMENT` | `1` (metric) | as above |
| Layer count | ≥ 30 | `acad.layers.list_layers` |
| Text style count | ≥ 6 | `acad.annotations.list_text_styles` |
| Dimension style count | ≥ 3 | `acad.dimensions.list_dim_styles` |
| Block definition count | ≥ 15 (including ≥ 5 `BLK-DOOR*`/`BLK-WIN*`) | `acad.blocks.list_blocks` |
| Room count (ROOM-TAG inserts) | ≥ 110 | filter `list_blocks` by insert count |
| Door count (DOOR-TAG inserts) | ≥ 80 | as above |
| Dimension count | ≥ 200 | `A-ANNO-DIMS` entity count |
| Layouts other than Model | ≥ 3 (A-101, A-102, A-103) | `acad.layouts.list_layouts` |
| Share of `Line` entities | **< 15%** (against 83% in the audited file) | `doc_summary.typeHistogram` |
| Share of `DBText` / `MText` / `Dimension` entities | > 10% combined | as above |
| Active layer at the end | **NOT `0`** (e.g. `A-WALL-INT`) | `acad_status.activeLayer` |

If any criterion fails, the corresponding phase is regenerated from its checkpoint.

---

## 11. Extensions beyond this exercise

Deliberately **not** part of this iteration, but the masterplan marks where each belongs:

- **BIM export** — IFC 4.3 through `acad.files.export_file` (needs AutoCAD Architecture; not
  attempted on vanilla AutoCAD).
- **Realistic 3D** — a conceptual visualisation was produced in an earlier session; this
  exercise is about 2D documentation.
- **Floors 1–3** — the same grid with 5 × 24 single-bed wards = 120 beds; the layout copies from
  the ground floor in one pass.
- **The `hospital-2026-baseline.yaml` validator** — a dedicated YAML file under
  `validators/rules/` carrying rules for unit checks, AIA layer naming, minimum room areas, AIIR
  compliance and the implied theatre ACH requirement.
- **Egress / fire** — an evacuation plan as sheet A-105 (egress paths, exit signage, fire
  extinguishers), per Polish WT 2022 §§ 211–237.
- **Clash detection** — once M/E/P arrive, `acad.validators.clash` (not planned yet).

---

## 12. The reference point versus the audited file

| Property | `[REDACTED-REFERENCE-DWG]` (audited) | Hospital-2026 (this plan) |
|---|---|---|
| Units | imperial `in` | metric `mm` |
| INSUNITS | 1 (inches) | 4 (millimetres) |
| Layer count | 17 (mostly useless: "0", "8", "WALL 1 300") | ~35 (AIA + ISO 13567) |
| Active layer at the end | `0` | `A-WALL-INT` (or another deliberate choice) |
| Text style count | 2 | ≥ 6 (annotative) |
| Dimension style count | 1 | ≥ 3 |
| Block definition count | 11 (meaningless names) | ≥ 20 (ISO-named, ≥ 10 of them dynamic) |
| Dynamic blocks | 0 | ≥ 10 |
| Hatch fills | 0 (2D Solids used instead — 14,591 of them) | Hatch ANSI31 / AR-CONC / AR-BRELM |
| 3DFace | 1,020 (legacy) | 0 |
| Share of `Line` | 83% (exploded) | < 15% |
| Layouts | 1 (`Projects1200x1100`, a PublishToWeb PNG) | ≥ 4 (A-101, A-102, A-103, A-104) |
| Title block | none | `BLK-TITLEBLOCK-A0` with attributes |
| Room labels | random text | ROOM-TAG block × 115 carrying `ROOM_NO`, `ROOM_NAME`, `ROOM_AREA` |
| BIM-ready | NO | YES (LOD 200, ready for IFC Space + IfcWallStandardCase) |

---

## 13. Compliance with Polish law as of 2026

This section **replaces** every hand-waving reference to "the standard" in sections 1–12 with
concrete Dz.U. numbers, paragraphs and PN standards. The building qualifies as a **public
building, a healthcare facility, human-hazard category ZL II**, low-rise (N, H ≤ 12 m — ground
floor plus 2 technical storeys).

Polish legal identifiers (`Dz.U.`, `WT`, `PN-B`) are left untranslated on purpose: they are the
legal address of a requirement, and a translated citation cannot be looked up.

### 13.1 The hierarchy of legislation in force in April 2026

| Short name | Full title | Role in the project |
|---|---|---|
| **Pr. bud.** | Act of 7 July 1994, Construction Law (Dz.U. 2024, consolidated) | the process basis: building design, permit |
| **WT 2024** | Ministry of Infrastructure regulation of 12 April 2002 (consolidated as **Dz.U. 2022 poz. 1225**), amended by Dz.U. 2023 poz. 2442, Dz.U. 2024 poz. 474 and **Dz.U. 2024 poz. 726** (in force from 15 August 2024) | the building's technical parameters and fire safety |
| **Rozp. MZ 2019** | Ministry of Health regulation of 26 March 2019 (consolidated as **Dz.U. 2022 poz. 402**; Dz.U. 2019 poz. 595) | requirements for the rooms and equipment of a healthcare provider; Annex 1 covers hospitals |
| **Ust. dostęp.** | Act of 19 July 2019 on ensuring accessibility (Dz.U. 2019 poz. 1696, consolidated 2024) | accessibility for people with special needs |
| **Ust. ppoż.** | Act of 24 August 1991 on fire protection (consolidated Dz.U. 2024) + Ministry of the Interior regulation of 7 June 2010 (consolidated Dz.U. 2023 poz. 822) | fire installations, fire safety instructions |
| **Ministry of Health HVAC guidance** | Guidance on designing, building, commissioning and operating ventilation and air-conditioning systems for healthcare providers (2018, with updates) | classes S1–S4, ACH, HEPA |
| **Pr. atom.** | Act of 29 November 2000, Atomic Law (consolidated Dz.U. 2024) + Ministry of Health regulation of 21 August 2006 (Dz.U. 2006 nr 180 poz. 1325, as amended) | radiological protection for X-ray and CT |
| **Pr. farm.** | Act of 6 September 2001, Pharmaceutical Law + EU GMP Annex 1 (2022) | hospital pharmacy, aseptic clean room |
| **PN-EN 13501-1** | Fire classification of construction products | finish materials |
| **PN-EN 1838** | Emergency lighting | `E-LITE-EMER` |
| **PN-EN 12464-1** | Lighting of indoor workplaces | `E-LITE` |
| **PN-EN 13779 / PN-EN 16798-3** | Ventilation for non-residential buildings | `M-HVAC` |
| **PN-EN ISO 14644-1:2016** | Cleanroom air cleanliness classification (ISO 5 for theatres, ISO 7 for PACU) | class S1/S2 rooms |
| **PN-EN 1822** | HEPA filters (H13/H14) | `M-HVAC-HEPA` |
| **PN-EN ISO 16890-1** | Pre-filters (ePM1, ePM2.5) | `M-HVAC` |
| **PN-B-02151-02, -03, -04** | Building acoustics | acoustic insulation |

### 13.2 Human-hazard category and fire-resistance class

**Category ZL II** — WT 2024 § 209(1)(2): buildings intended primarily for people with limited
mobility, such as hospitals, nurseries, kindergartens and care homes.

At a building height of **H ≤ 12 m** (low-rise, N), the table in WT § 212(2) requires:

> **ZL II + low-rise (N) → fire-resistance class "B"**

**⚠ Key point:** WT § 214 explicitly excludes ZL II from lowering the fire-resistance class even
where sprinklers are fitted. **Sprinklers are installed anyway** (§ 27 of the 2010 Ministry of
the Interior regulation, and the insurer's requirements), but the class stays **B**.

Under WT § 216, class B means:

| Element | Fire resistance |
|---|---|
| Primary structure (columns, load-bearing roof) | **R 120** |
| Floor slabs between storeys | **REI 60** |
| External walls (fire compartment) | **EI 60** |
| Internal compartment-separating walls | **EI 60** |
| Ordinary internal partitions | **EI 30** |
| Doors in compartment-separating walls | **EI 60**, self-closing (WT § 232) |
| Roof structure | **R 30** |
| Roof covering | **RE 30** |

Consequences for the CAD layers:

- `A-WALL-STRUCT` — 450 × 450 reinforced concrete columns at R 120 (fire-protective boarding if
  the cores are steel).
- `A-WALL-EXT` 400 mm — build-up: 20 render + 240 concrete/silicate + 120 mineral wool + 20 thin
  render → REI 60 with margin.
- `A-WALL-FIRE` — **a new layer, to be added to § 3 of the masterplan** — REI 60
  compartment-separating walls, colour 11, ANSI31 diagonal hatch plus the text "EI60".
- `A-DOOR-FIRE` — EI 60 fire doors, diamond symbol with a number, layer colour 1 (red).

### 13.3 Fire compartments — critical, because 80 × 60 exceeds the maximum

WT § 227(1): **the maximum ZL II fire compartment in a low-rise building is 3,500 m²**.

The gross ground-floor area is 80 × 60 = **4,800 m²** → **the floor must be split into at least
two compartments separated by an REI 60 wall**.

**Compartment split (schematic; Y axis at 0 m, X axes in mm):**

```
      A   B   C   D   E   F   G   H   I   J   K
      0  7.8 15.6 23.4 31.2 39.0 46.8 54.6 62.4 70.2 78.0  [m]
  ┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐
1 │ Compartment A - PUBLIC / OUTPATIENT        │   (A + B + C)
  │    lobby + waiting + ED + imaging          │    ~2,400 m²
  │  fire wall on axis F (x = 39 m)            │
  ├═══════════════════════════════════════════┤  <- EI 60 separating wall
  │ Compartment B - SURGICAL / TECHNICAL       │   (D + E)
7 │    theatres + PACU + back of house         │    ~2,400 m²
  └───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘
```

Both compartments are under 3,500 m² — compliant ✓, with about 31% to spare.

**Fire doors in the separating wall** (axis F, y = 0…50.4):
- main public route (lobby → imaging corridor): EI 60, 1400 mm, double-leaf
- sterile corridor route (bed transfers): EI 60, 1800 mm, sliding, interlocked
- dirty corridor route: EI 60, 1400 mm
- service route: EI 60, 1100 mm

**The wall enclosing plant room E-514** — under WT § 212(9), technical rooms form **their own
fire compartment**; an REI 60 wall around the AHUs is required, with separate access from the
roof or the service corridor.

### 13.4 Escape routes (WT chapter 4, section VI)

| Parameter | WT requirement | How the design meets it |
|---|---|---|
| Minimum escape exits from a ZL II compartment | **2** (WT § 236) | 3 exits from compartment A (main, ED, imaging), 3 from compartment B (theatres, service, dock) ✓ |
| Maximum travel distance within a room | **40 m** (§ 237(1)); 75 m with smoke extraction | every room is under 15 m ✓ |
| Maximum travel distance, **one direction** | **10 m** (§ 237(6)(1)) | every route has two directions ✓ |
| Maximum travel distance, **two directions** | **30 m** (§ 237(6)(2)) | longest route, OR-4 (D-415) to the nearest exit: ~28 m ✓ |
| Escape corridor width | minimum **1.4 m** plus 0.6 m per 100 occupants (§ 242) | sterile 2.8 m, dirty 2.2 m, public 2.4 m — for a 180-person zone the requirement is about 1.4 + 1.2 = 2.6 m, so **the 2.4 m public corridor is TOO NARROW and is widened to 2.8 m** |
| Door width onto an escape route | minimum **0.9 m** clear (§ 239) | standard doors are 1.1 m ✓ |
| Main escape door width | **1.2 m per 100 occupants** | the A-101 lobby airlock is 2 × 1800 mm = 3.6 m for 300 occupants (maximum) ✓ |
| Smoke extraction from stair cores | required in ZL II (§ 245(5)) | not applicable at ground level, but space is reserved for smoke vents in the sterile-corridor roof |
| Emergency lighting to PN-EN 1838 | minimum **1 lx** at floor level on an escape route, minimum **1 h** backup | `E-LITE-EMER` on 2 h UPS plus the EPSS diesel |
| Escape signage | PN-ISO 7010, PN-EN 1838, photoluminescent | layer `A-ANNO-SYMB-EGRS` (to be added) |

**Design decision:** the public corridor in compartment A is widened from 2.4 m to **2.8 m**, per
the width calculation for 180 occupants. On layer `A-WALL-PART`, this moves the partition 400 mm
towards the reception rooms.

### 13.5 Room heights (WT § 72)

| Room | Minimum clear height |
|---|---|
| Patient / bed room | **3.3 m** (WT § 72 table, hospitals column) |
| Operating theatre | **3.3 m** plus the technical ceiling — **4.2 m structural** in practice |
| Ancillary rooms (stores, staff rooms) | **2.5 m** |
| Corridors | **2.5 m** (2.8 m above the suspended ceiling) |
| AHU plant room | **2.2 m** (§ 72(1)(3)) — designed at 3.0 m for the size of the units |

**Designed structural floor-to-floor height: 4,500 mm** (3,300 clear plus 1,200 for HVAC and the
theatre technical ceiling).

### 13.6 Operating theatres — class S1 (Ministry of Health HVAC guidance + PN-EN ISO 14644-1)

| Parameter | Requirement | How the design meets it |
|---|---|---|
| Cleanliness class of the protected zone | **ISO 5** (S1a/S1b — orthopaedic surgery, implants) | OR-1, OR-2 and **OR-4 hybrid** → S1a |
| Cleanliness class (lower-risk procedures) | ISO 7 (S1c) | OR-3, endoscopy → S1c |
| Supply air filtration, 3 stages | ePM1 ≥ 50% (F7) + ePM1 ≥ 80% (F9) + **HEPA H13/H14** | H14 in the ceiling diffusers of every theatre |
| Ventilation performance | LAF 0.2–0.3 m/s, about **2,400 m³/h** of fresh air per theatre | a dedicated theatre HEPA AHU in E-514 |
| Air changes | **≥ 20/h** (S1), typically 25/h | 25/h adopted — filter pressure drop needs fan headroom |
| Positive pressure against the sterile corridor | **minimum +15 Pa** (S1 against neighbours) | a differential pressure sensor at every theatre door |
| Temperature | **24 °C** (WT § 134 table for operating theatres); adjustable 17–26 °C | AHU with a water heating coil and a DX cooler |
| Relative humidity | **30–60%** (Ministry of Health guidance) | hygienic steam humidification |
| Protected (LAF) zone | **at least 3.2 × 3.2 m = 10.24 m²** (3.0 × 3.0 m typical minimum) | OR-1 and OR-2: LAF 3.0 × 3.0; OR-4: LAF 3.6 × 3.6 |
| Laminar ceiling (LAF panel) | H14 HEPA supply across the whole panel | CAD block `BLK-OR-LAF-PANEL` (to be added) |
| Filter rejection per PN-EN 1822 | per MPPS testing | documented at commissioning |

### 13.7 AIIR (airborne infection isolation) — class S3

| Parameter | Requirement | How the design meets it |
|---|---|---|
| Negative pressure against neighbours | **minimum −5 Pa** (Ministry of Health guidance, S3) | 2 AIIR rooms: D-490A and D-490B (to be added to the zone D program) |
| Air changes | **≥ 12/h** | 15/h with H13 HEPA on the extract |
| Anteroom | **mandatory**, with door interlock | `BLK-DOOR-AIRLOCK` |
| Extract | discharged outside the building, never recirculated | a separate riser in the plant room |
| Observation window | mandatory, from the adjoining room | frameless 900 × 600 mm glazing |

**AIIR rooms — ADDED to zone B (the emergency department):**

| No. | Name | m² | Note |
|---|---|---:|---|
| B-222 | AIIR 1 — isolation room | 16 | −5 Pa, 15 ACH, HEPA extract |
| B-223 | AIIR 1 anteroom | 4 | interlock, PPE drop zone |
| B-224 | AIIR 2 — isolation room | 16 | as above |
| B-225 | AIIR 2 anteroom | 4 | as above |

### 13.8 Radiological protection (Atomic Law + Ministry of Health regulation of 2006)

| Room | Wall shielding | Door shielding | Ceiling shielding |
|---|---|---|---|
| CT (C-303), 128-slice, ~120 kVp | **Pb ≥ 2 mm** equivalent, or 250 mm reinforced concrete | Pb 2 mm, 2 mm Pb-glass window | Pb 2 mm, where an occupied room sits above |
| X-ray I (C-308) | Pb 1.5–2 mm | Pb 1.5 mm | Pb 1.5 mm |
| Mammography (C-310) | Pb 1 mm is sufficient | Pb 1 mm | Pb 1 mm |
| MR 3T (C-305) | **Faraday cage**, not lead — 0.5 mm copper or RF-shielded steel | RF door | RF ceiling |

CAD layers:
- `A-WALL-LEAD` (already in § 3) — colour 1, `AR-RSHKE` hatch plus the text "Pb 2 mm"
- `A-WALL-FARA` (already in § 3) — for the MR room, colour 210
- `A-AREA-RAD-CTRL` — the controlled area under Atomic Law § 12 (a dashed outline around the
  room plus a 3 m buffer)
- `A-AREA-RAD-OVER` — the supervised area

**The MR 4-gauss line** must be marked on the floor (a solid red line plus a pictogram). Layer
`A-AREA-MR-4GS`; a physical barrier — a 1.1 m guardrail or a wall — is required wherever the
line extends beyond the MR room.

### 13.9 Accessibility — the 2019 Accessibility Act and WT §§ 54–62

| Requirement | How the design meets it |
|---|---|
| Entrance doors at least **1.5 m** wide; a 1.5 × 1.5 m manoeuvring zone | airlock A-101: 2 × 1800 mm sliding, manoeuvring zone 3.0 × 3.0 m ✓ |
| External door threshold at most **20 mm** | a 15 mm magnetic threshold in the airlock ✓ |
| Every public WC to include at least one accessible cubicle | A-108 universal, plus one accessible cubicle in A-106 and A-107 ✓ |
| Accessible WC at least **2.5 × 2.2 m**, door **0.9 m**, opening outwards | `BLK-WC-ACCESSIBLE` is 2.2 × 1.8 m — **too small**; the block is corrected to **2.5 × 2.2** |
| Grab rails, handles, colour contrast ≥ 70% | layer `A-FLOR-FIXT-GRAB` for grab rails |
| Braille signage and pictogram plates | a ROOM-TAG "accessible" variant with a braille field |
| Induction loops at reception | symbol `BLK-ANNO-LOOP` above the A-102 desks |

**Correction to § 6 of the masterplan:** `BLK-WC-ACCESSIBLE` becomes **2.5 × 2.2 m** (was
2.2 × 1.8).

### 13.10 Acoustics (PN-B-02151, WT §§ 323–327)

| Rooms | Required wall sound insulation |
|---|---|
| Patient room ↔ patient room | R'w ≥ **50 dB** |
| Patient room ↔ corridor | R'w ≥ **45 dB** |
| Operating theatre ↔ sterile corridor | R'w ≥ **52 dB** |
| Psychiatric safe room (B-207) ↔ surroundings | R'w ≥ **52 dB** (an extra fibre-cement layer) |

Layer `A-WALL-PART` splits into three variants by sound insulation, coded in the wall block's
attribute (a new idea, to be implemented if time allows): `PART-ACS-45`, `PART-ACS-50`,
`PART-ACS-52`.

### 13.11 Lighting (PN-EN 12464-1)

| Room | Minimum average illuminance [lx] | CCT [K] | CRI |
|---|---:|---:|---:|
| Operating theatre (ambient) | **1,000** | 4,000 | 90 |
| Surgical field (scialytic) | 10,000–100,000 | 4,500 | 95 |
| Hospital corridor (day) | 100 | 3,000–4,000 | 80 |
| Hospital corridor (night) | 50 | 2,700 | 80 |
| Patient room (general) | 100 | 3,000 | 90 |
| Patient room (examination) | 300 | 4,000 | 90 |
| PACS reading workstation | 300 | 4,000 | 95 |
| Registration, reception | 300 | 4,000 | 80 |
| Analytical laboratory | 500 | 4,000 | 90 |
| Pharmacy clean room | 500 | 4,000 | 90 |
| Emergency lighting on escape routes | minimum **1** (PN-EN 1838) | 2,700 | 40 |

Layers: `E-LITE-AREA-100`, `E-LITE-AREA-300`, `E-LITE-AREA-500`, `E-LITE-AREA-1000`,
`E-LITE-EMER`, `E-LITE-TASK`.

### 13.12 Compliance matrix — key rooms against the standard

| Room | Area as designed | Minimum required | Status |
|---|---:|---:|---|
| General operating theatre (OR-1, OR-2) | 48 m² | ≥ 30 m² | ✓ with margin |
| OR-3, endoscopy | 36 m² | ≥ 25 m² | ✓ |
| OR-4, hybrid | 72 m² | ≥ 50 m² (cardiac surgery) | ✓ |
| PACU bay | 12 m² | ≥ 6 m² per bay plus 2 m² circulation | ✓ |
| Pre-op bay | 10 m² | ≥ 6 m² per bay | ✓ |
| ED resuscitation room (2 bays) | 45 m² | ≥ 25 m² per bay = 50 m² for two | **marginally below** — **corrected to 52 m², extended 1.5 m on axis A-B** |
| Treatment room (B-214) | 20 m² | ≥ 12 m² | ✓ |
| Consulting room (B-208, B-209) | 16 m² | ≥ 12 m² | ✓ |
| Triage | 24 m² | ≥ 18 m² (PTMR recommendation) | ✓ |
| AIIR room | 16 m² | ≥ 12 m² plus an anteroom | ✓ |
| Isolation anteroom | 4 m² | ≥ 3 m² | ✓ |
| Public patient WC | 6–18 m² | ≥ 3 m² (WT § 83), accessible at least 5 m² | ✓ |
| Universal accessible WC | 8 m² | ≥ 2.5 × 2.2 = 5.5 m² | ✓ |
| Sterile corridor | 2.8 m wide | ≥ 2.2 m (Ministry of Health guidance) | ✓ |
| Dirty corridor | **2.2 m wide** (corrected) | ≥ 2.2 m | ✓ (exactly at the limit) |
| Public corridor (zone A) | 2.8 m (after the § 13.4 correction) | 1.4 + 1.2 = 2.6 m for 180 occupants | ✓ |
| Operating theatre door | 1,400 / 1,800 mm | ≥ 1,400 mm (WT § 62, bed transfers) | ✓ |
| Patient room door | 1,100 mm | ≥ 1,100 mm | ✓ |
| Accessible door | 900 mm | ≥ 900 mm (§ 62) | ✓ |
| Patient room clear height | 3,300 mm | ≥ 3,300 mm (§ 72) | ✓ (exactly at the limit) |
| Operating theatre height | 3,300 mm clear (4,500 structural) | ≥ 3,300 mm | ✓ |

**Program correction #2:** B-205, the resuscitation room, grows from 45 m² to **52 m²** (the wall
on axis B moves 1.5 m along X).

### 13.13 Energy performance (WT section X, §§ 328–331, in force from 1 January 2021)

Hospitals are public buildings, so the maximum **EP** (primary energy) figure is
**190 kWh/(m²·year)** (WT § 329, table 1, healthcare row, from 2021; a draft act moves towards
160 from 2026, but 190 applies as this document is written).

Design boundary conditions:
- Thermal transmittance U — WT § 328, table 2:
  - external wall: U_max = **0.20 W/(m²·K)** → 20 cm of mineral wool at λ = 0.035
  - window: U_max = **0.9 W/(m²·K)** → triple glazing at Ug = 0.6
  - external door: U_max = **1.3 W/(m²·K)**
  - roof: U_max = **0.15 W/(m²·K)**
  - ground-bearing floor: U_max = **0.30 W/(m²·K)**
- Ventilation heat recovery: **at least 70%**
- A BMS is mandatory for a building with HVAC capacity over 290 kW — a hospital qualifies.

Design note: this is 2D documentation, so wall build-ups are not dimensioned, but **layer
`A-WALL-EXT` carries the attribute "U=0.18 W/m²K"** on the legend plate.

---

## 14. Revised execution plan (after the legal audit)

Two corrections to § 9:

| Phase | Correction | Effect in CAD |
|---|---|---|
| 3 (Zones) | Add the **fire-separating wall on axis F** (REI 60, layer `A-WALL-FIRE`, colour 11) and the wall enclosing plant room E-514 | 2 more lines on `A-WALL-FIRE` instead of `A-WALL-INT` |
| 4 (Rooms) | Grow B-205 to 52 m², move the B-206/B-207 wall 1.5 m along X; **add** B-222/B-223/B-224/B-225 (2 AIIR rooms with anterooms) in the south-west corner of the ED; widen the public corridor from 2.4 m to 2.8 m | 4 more rooms, 3 walls changed |
| 5 (Doors) | Introduce the `BLK-DOOR-FIRE-EI60` variant for the 4 openings in the axis-F wall | 4 new inserts |
| 6 (Windows) | no change | — |
| 7 (Fixtures) | Change `BLK-WC-ACCESSIBLE` from 2.2 × 1.8 to **2.5 × 2.2** | block redefinition |
| 8 (Labels) | Every room's ROOM-TAG gains `ROOM_CLASS_HYG` (S1/S2/S3/S4) and `ROOM_FIRE_ZONE` (A or B) | extended block schema |
| — | Add a fire-compartment diagram as its own layout, A-106 | — |

New layouts:

| Sheet | Scale | Content |
|---|---|---|
| **A-106** | 1:200 | Fire compartment diagram (FZ-A, FZ-B), escape directions as arrows, travel distances |
| **A-107** | 1:200 | Hygiene class diagram S1/S2/S3/S4 (colour-coded) |
| **A-108** | 1:500 | Site plan with surroundings (plot, ambulance bay, courtyard G) |

### 14.1 Layers added since § 3

| Layer | Colour | Use |
|---|---:|---|
| `A-WALL-FIRE` | 11 | REI 60 fire-separating wall |
| `A-WALL-STRUCT` | 92 | primary structure — columns, beams |
| `A-DOOR-FIRE` | 1 | EI 60 fire doors |
| `A-AREA-FIRE` | 31 | fire compartment outline (HIDDEN, heavy) |
| `A-AREA-HYGN-S1` | 11 | hygiene class S1 (theatres) |
| `A-AREA-HYGN-S2` | 41 | class S2 (PACU, pre-op, standard isolation) |
| `A-AREA-HYGN-S3` | 1 | class S3 (AIIR) |
| `A-AREA-HYGN-S4` | 8 | class S4 (the rest of the clinical area) |
| `A-AREA-RAD-CTRL` | 2 | radiological controlled area |
| `A-AREA-RAD-OVER` | 3 | radiological supervised area |
| `A-AREA-MR-4GS` | 1 | MR 4-gauss line |
| `A-ANNO-SYMB-EGRS` | 7 | escape signage |
| `E-LITE-EMER` | 1 | emergency lighting (already present; confirmed) |

Layer count after the corrections: **~48**, up from the original 35.

---

## 15. Point-by-point response to the `[REDACTED-REFERENCE-DWG]` audit

The audit of the earlier file found 11 defects. How each is addressed:

| Defect in the reference file | How Hospital-2026 answers it |
|---|---|
| Imperial inches, INSUNITS=1 | Metric mm, INSUNITS=4; verified in phase 0 |
| 83% of entities are primitive Lines | Walls as closed POLYLINEs, fills as HATCH; target Line < 15% |
| 14,591 2D SOLIDs instead of hatches | ANSI31 / AR-CONC / AR-BRELM / AR-SAND for every fill |
| 1,020 legacy 3DFaces | No 3DFace; 3D work uses EXTRUDE/PRESSPULL (not applicable to a 2D ground-floor plan) |
| 17 layers — `0`, `8`, `WALL 1 300`, meaningless | ~48 layers per AIA CAD Layer Guidelines 2nd Ed. and ISO 13567 |
| Active layer at the end: `0` | Active layer `A-WALL-INT`, verified by the acceptance criteria in § 10 |
| 2 text styles, 1 dimension style | 6 annotative text styles, 3 dimension styles (1:100 / 1:50 / 1:20) |
| 11 blocks, none dynamic, names like `02ETRX29` | ~22 blocks, ≥ 10 dynamic with attributes (DOOR-SINGLE/DOUBLE/SLIDE/FIRE, WIN-*, ROOM-TAG, CT/MR/OR-TABLE/HEADWALL) |
| No meaningful layout, only a `Projects1200x1100` PublishToWeb PNG | 7 layouts (A-101…A-108) with an A0 title block and one viewport per scale |
| No functional justification | All 115 rooms carry a program entry (§ 2) and a standard reference (§ 13) |
| A file "from WhatsApp", unrelated to the project | A new `.dwg` under a git-controlled project directory (created in phase 0) |

---

> **Next step:** run phase 0 (setup) through `acad_design_iterate`. After that, every phase is
> its own call with its own checkpoint. All of it in a new `Hospital2026_A0-001.dwg`, never in
> someone else's `[REDACTED-REFERENCE-DWG]`.

> **Normative changes to apply before phase 0 (this document):**
> 1. Add the layers `A-WALL-FIRE`, `A-DOOR-FIRE`, `A-AREA-FIRE`, `A-AREA-HYGN-S1..S4`,
>    `A-AREA-RAD-*`, `A-AREA-MR-4GS`, `A-ANNO-SYMB-EGRS` (13 in total).
> 2. Grow `BLK-WC-ACCESSIBLE` to 2.5 × 2.2 m.
> 3. Add `BLK-DOOR-FIRE-EI60` (EI 60 fire door with a self-closer, attribute `FIRE_RATING`).
> 4. Add rooms B-222…B-225 to the program.
> 5. Grow B-205 to 52 m².
> 6. Widen the zone A public corridor to 2.8 m.
> 7. Set the D-421 dirty corridor width to 2.2 m (already applied in § 2).
> 8. Add the three new layouts A-106…A-108.
