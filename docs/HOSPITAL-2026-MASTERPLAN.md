# Szpital Kompaktowy 2026 — Masterplan Parter

> **Projekt:** Hospital-2026 Compact Tertiary Care — Ground Floor Masterplan
> **Skrzydło/faza:** Faza A · Parter · Rev. 01
> **Footprint:** 80 × 60 m (~4 320 m² netto brutto), z dziedzińcem wewnętrznym 18 × 12 m
> **Projektant (MCP agent):** generacja via `acad_design_iterate` 11 faz
> **Normy bazowe:** AIA CAD Layer Guidelines (2nd Ed.) · ISO 13567 · FGI Guidelines 2022 · HTM 00-01 · Rozp. MZ Dz.U. 2019 poz. 595 · WT 2022 (Dz.U. 2022 poz. 1225) · EN 17210 · IFC 4.3 / ISO 19650
> **Cel ćwiczenia:** zaadresować wszystkie niezgodności pliku referencyjnego `[REDACTED-REFERENCE-DWG]` (imperial units, 83 % prymitywnych Line, warstwa "0" aktywna, brak stylów, brak dynamic blocks, jeden layout PublishToWeb) i zademonstrować produkcyjny poziom dokumentacji.

---

## 1. Koncepcja i trendy 2026, które to kształtują

Projekt nie jest CAD-em dla CAD-u — każde pomieszczenie ma uzasadnienie w aktualnej praktyce 2026:

| Trend 2026 | Konsekwencja projektowa |
|---|---|
| **Post-COVID single-bed rooms** (100 % łóżka 1-osobowe, LDRP dla porodów) | Pokoje łóżkowe ≥ 15 m² netto + węzeł sanitarny prywatny ≥ 4,5 m² |
| **AIIR — Airborne Infection Isolation** (FGI 2022 § 2.1-3.3.2.2) | Min. 2 AIIR z anteroom (śluza), podciśnienie −2,5 Pa, 12 ACH, HEPA H14 |
| **Hybrid OR** (kardiochirurgia, neurochirurgia, endowaskularne) | 1 sala OR ≥ 70 m² z wbudowanym C-arm/angio + technical ceiling 800 mm |
| **Same-handed rooms** (ergonomia pielęgniarska) | Wszystkie pokoje łóżkowe leworęczne od headwall (standardyzacja szkolenia) |
| **Decentralized nursing** | Nurse pod co 4 pokoje zamiast centralnej nurse station |
| **Biophilic design** | Dziedziniec wewnętrzny 18×12 m, świetliki nad sterile corridor, curtain wall w poczekalni |
| **Telehealth hub** | Dedykowane pokoje "virtual consult" (akustyka, doświetlenie kamery, backdrop) |
| **IPC zoning — clean / dirty separation** | Podwójny korytarz OR (sterile core + dirty corridor), oddzielne windy czyste/brudne |
| **Adaptive re-use / modular** | Siatka 7,8 × 8,4 m kompatybilna z panelowymi ścianami demontowalnymi |
| **Embodied carbon tracking** | Ściany wewnętrzne CLT 120 mm (znak LAY `A-WALL-INT-MTMB`), stal struct recykling ≥ 90 % |
| **Sensor-ready** | Kabelkanały IoT w suficie każdego pokoju — warstwa `T-CABL-IOTN` |
| **Accessibility EN 17210** | Wszystkie drzwi ≥ 900 mm w świetle, WC dostępne 1 na każde 10 stanowisk |

Wymiarowanie stref wynika z połączenia FGI 2022 (amerykańskie, najnowsze globalne benchmarki) + PL Rozp. MZ 2019 (nasze obowiązkowe minimum). Gdzie się różnią, wybieramy **wyższy** wymóg.

---

## 2. Program funkcjonalno-użytkowy (Space Program)

Parter dzielimy na **5 stref** (A–E) + 2 zewnętrzne (F, G). Każdy pokój ma trzy pola: **numer AIA** · **nazwa** · **powierzchnia netto**.

### Strefa A · Public Zone (wejście, rejestracja, retail) — ~380 m²

| Nr | Nazwa | m² netto | Norma / uwaga |
|---|---|---:|---|
| A-101 | Wiatrołap główny (airlock) | 18 | automatyczne drzwi 2×1800 przesuwne |
| A-102 | Lobby / rejestracja centralna | 140 | curtain wall pn-wsch, 3 biurka rejestracji, kiosk samoobsługowy |
| A-103 | Poczekalnia główna | 90 | 60 miejsc siedzących, strefy rodzinne, biophilic zieleń |
| A-104 | Apteka ogólnodostępna (OTC) | 42 | wejście niezależne też z zewnątrz |
| A-105 | Kawiarnia / bistro | 35 | dla gości + personelu, godz. 6–21 |
| A-106 | WC publiczne damskie | 18 | 4 kabiny + 1 accessible |
| A-107 | WC publiczne męskie | 16 | 3 kabiny + 2 pisuary + 1 accessible |
| A-108 | WC universal / rodzinne | 8 | EN 17210, przewijak, mama-tata |
| A-109 | Biuro obsługi klienta | 12 | reklamacje / informacja / wyładowanie zasobów |

### Strefa B · Szpitalny Oddział Ratunkowy (SOR) — ~720 m²

| Nr | Nazwa | m² netto | Norma / uwaga |
|---|---|---:|---|
| B-201 | Podjazd karetek + wiatrołap SOR | 28 | zadaszony 3 stanowiska, EHR integration |
| B-202 | Triage (pielęgniarska segregacja) | 24 | 2 stanowiska, bezpośredni widok wejścia |
| B-203 | Rejestracja SOR | 12 | nocna izba przyjęć |
| B-204 | Poczekalnia SOR | 48 | 32 miejsca, split "Fast-track" / "Observation" |
| B-205 | Sala resuscytacyjna (Crash Room) | 45 | 2 stanowiska, widoczność z nurse pod, pre-programmed med gas |
| B-206 | Gabinet pediatryczny SOR | 22 | osobna poczekalnia, playground dostęp |
| B-207 | Safe room psychiatryczny | 14 | ligature-resistant, obserwacja video |
| B-208 | Konsultacyjny "Fast-track" A | 16 | lekarska decyzja < 15 min |
| B-209 | Konsultacyjny "Fast-track" B | 16 | j.w. |
| B-210 | Observation bay 1 | 12 | cubicle curtain, headwall med gas |
| B-211 | Observation bay 2 | 12 | j.w. |
| B-212 | Observation bay 3 | 12 | j.w. |
| B-213 | Observation bay 4 | 12 | j.w. |
| B-214 | Gabinet opatrunkowy (minor surgery) | 20 | scialytic lamp, sink pre-op |
| B-215 | Pokój lekarski SOR (on-call) | 14 | tapczan + biurko + WC prywatny |
| B-216 | Nurse pod SOR (central) | 22 | widok na observation 1-4, monitoring central |
| B-217 | Clean utility SOR | 10 | czyste materiały opatrunkowe |
| B-218 | Dirty utility SOR | 10 | brudny sprzęt, flush sink |
| B-219 | Magazyn sprzętu ratunkowego | 18 | defibrylatory, zestawy intubacji |
| B-220 | Stretcher bay (parking noszy) | 8 | 4 nosze + 2 wózki inwalidzkie |
| B-221 | WC pacjentów SOR | 6 | accessible |

### Strefa C · Diagnostyka obrazowa i laboratorium — ~580 m²

| Nr | Nazwa | m² netto | Norma / uwaga |
|---|---|---:|---|
| C-301 | Poczekalnia diagnostyki | 36 | 24 miejsca, split pediatryczny |
| C-302 | Rejestracja diagnostyki | 10 | |
| C-303 | Pracownia TK wielorzędowa 128-slice | 42 | control room 14 m² + sterownia 28 m² (bleed wall) |
| C-304 | Sterownia TK | 14 | okno ołowiane 40 × 30 cm |
| C-305 | Pracownia MR 3T | 52 | Faraday cage, strefa 4 Gauss zaznaczona, quench pipe do atmosfery |
| C-306 | Sterownia MR | 16 | Faraday penetration panel |
| C-307 | Kabina przygotowania MR | 6 | ferromagnetic screening |
| C-308 | RTG kostno-płucne I | 28 | ołów 2 mm w ścianach |
| C-309 | Sterownia RTG I | 8 | ołów 2 mm |
| C-310 | RTG mammograficzne | 18 | privacy ++, female-only access |
| C-311 | Sterownia RTG II | 6 | |
| C-312 | USG / pracownia ultrasonograficzna | 22 | 2 aparaty, printer, privacy |
| C-313 | Pracownia ECHO kardiologiczne | 20 | stress-test treadmill space |
| C-314 | Pokój interpretacji radiologicznej (reading room) | 24 | PACS workstation × 3, przyciemnienie |
| C-315 | Laboratorium przyjęć (point-of-care) | 28 | rapid tests, kolejka tubes pneumatic |
| C-316 | Pobrania krwi (phlebotomy) | 14 | 3 stanowiska |
| C-317 | WC pacjentów diagnostyki | 6 | accessible |
| C-318 | Magazyn kontrastów | 6 | kontrola temp. |
| C-319 | Pokój techników radiologii | 12 | |

### Strefa D · Blok operacyjny + PACU — ~890 m²

IPC zoning — obowiązkowe oddzielenie sterile core od dirty corridor (dwuobiegowy):

| Nr | Nazwa | m² netto | Norma / uwaga |
|---|---|---:|---|
| D-401 | Śluza personel męski | 12 | szatnia czysta + brudna |
| D-402 | Śluza personel żeński | 12 | j.w. |
| D-403 | Śluza pacjent (przygotowanie) | 8 | z systemem identyfikacji |
| D-404 | Pre-Op bay 1 | 10 | headwall, monitoring |
| D-405 | Pre-Op bay 2 | 10 | j.w. |
| D-406 | Pre-Op bay 3 | 10 | j.w. |
| D-407 | Pre-Op bay 4 | 10 | j.w. |
| D-408 | Pre-Op bay 5 | 10 | j.w. |
| D-409 | Pre-Op bay 6 | 10 | j.w. |
| D-410 | Nurse station Pre-Op | 14 | |
| D-411 | Korytarz sterylny (sterile corridor) | 78 | szer. 2,8 m, skylight 2 %, 25 ACH |
| D-412 | Sala operacyjna OR-1 (ogólna) | 48 | 7,0 × 7,0 m czysta, technical ceiling 800 mm, laminar flow |
| D-413 | Sala operacyjna OR-2 (ogólna) | 48 | j.w. |
| D-414 | Sala operacyjna OR-3 (endoskopowa / małe procedury) | 36 | niższy standard — 20 ACH |
| D-415 | **Hybrid OR-4 (kardio-neuro-endo)** | 72 | 8,4 × 8,4 m, wbudowany C-arm biplane, OR table radiolucent |
| D-416 | Sterile core (magazyn sterylny) | 40 | centralny między OR 1-2-4 |
| D-417 | Scrub station OR-1/2 | 6 | 2 baterie bezdotykowe |
| D-418 | Scrub station OR-3/4 | 6 | j.w. |
| D-419 | Clean utility OR | 14 | fartuchy sterylne, zestawy pre-prepared |
| D-420 | Dirty utility OR + flush sink | 14 | wydzielony od sterile |
| D-421 | Dirty corridor (komunikacja brudna) | 62 | 2,2 m × 28 m — min. szerokość dla noszy wg Wytycznych MZ (NIE 1,4 m) |
| D-422 | Anaesthesia storage | 10 | szafy blokowane |
| D-423 | Med-gas manifold room | 14 | O₂, N₂O, Vac, Air 4 bar, Air 8 bar (tool gases) |
| D-424 | PACU bay 1 | 12 | headwall + monitor |
| D-425 | PACU bay 2 | 12 | j.w. |
| D-426 | PACU bay 3 | 12 | j.w. |
| D-427 | PACU bay 4 | 12 | j.w. |
| D-428 | PACU bay 5 | 12 | j.w. |
| D-429 | PACU bay 6 | 12 | j.w. |
| D-430 | PACU bay 7 | 12 | j.w. |
| D-431 | PACU bay 8 | 12 | j.w. |
| D-432 | Nurse station PACU (central) | 24 | widok na 8 stanowisk |
| D-433 | Consult / family waiting PACU | 14 | private consult |
| D-434 | Pokój anestezjologa | 12 | on-call |

### Strefa E · Back-of-house — ~520 m²

| Nr | Nazwa | m² netto | Norma / uwaga |
|---|---|---:|---|
| E-501 | Apteka szpitalna — ekspedycja | 42 | bar ekspedycyjny, unit-dose |
| E-502 | Apteka szpitalna — clean room aseptic | 28 | klasa ISO 7, oddział cytostatyki klasa ISO 5 w izolatorze |
| E-503 | Apteka szpitalna — magazyn kontrolowany | 18 | leki kat. narkotyczne, sejf podwójny |
| E-504 | Sterylizatornia — strefa brudna | 26 | wejście materiału brudnego, mycie |
| E-505 | Sterylizatornia — strefa pakowania | 22 | laminary flow + foliarka |
| E-506 | Sterylizatornia — autoklawy | 18 | 2 autoklawy dwustronne (ściana-przepust) |
| E-507 | Sterylizatornia — strefa czysta + magazyn sterylny | 24 | wyjście materiału sterylnego do OR |
| E-508 | Magazyn czysty centralny | 48 | materiały opatrunkowe, textile |
| E-509 | Magazyn brudny + pralnia dzienna | 32 | chute szyb na brudną pościel |
| E-510 | Kuchnia pomocnicza / distribution | 38 | regeneracja posiłków, food-cart parking |
| E-511 | Pokój personelu / socjalny | 28 | kuchnia, lockers, sofa |
| E-512 | Szatnia personel damska | 22 | 24 szafki, prysznic × 2, WC × 2 |
| E-513 | Szatnia personel męska | 22 | j.w. |
| E-514 | Maszynownia AHU / technika | 84 | 3 centrale wentylacyjne (publiczna / SOR / OR-HEPA) |
| E-515 | Rozdzielnia elektryczna NN | 18 | UPS 400 kVA, diesel 800 kVA zewnętrzny |
| E-516 | Serwerownia / IT | 14 | PACS server room, klimatyzacja n+1 |
| E-517 | Pomieszczenie sprzątaczki (housekeeping) | 8 | flush sink, mop dryer |

### Strefy zewnętrzne (F, G)

- **F** — Podjazd karetek + teren manewrowy (40 × 10 m, dedykowany)
- **G** — Dziedziniec wewnętrzny 18 × 12 m (biophilic: trawnik, ławki, drzewo iglaste, punkt wodny); dostęp dla pacjentów walking, nie dla personelu medycznego

---

## 3. Konwencja warstw (AIA CAD Layer Guidelines 2nd Ed. + ISO 13567)

Struktura nazwy: `D-MJR-MIN-SUF` gdzie:
- **D** = dyscyplina (A = Architecture, S = Structural, M = Mechanical, E = Electrical, P = Plumbing, T = Telecom, C = Civil, L = Landscape)
- **MJR** = główny system
- **MIN** = podsystem (opcjonalnie)
- **SUF** = modyfikator (EXIST, NEWW, DEMO, REVA)

### Warstwy architektoniczne (A-)

| Warstwa | Kolor ACI | Linetype | Lineweight | Zastosowanie |
|---|---:|---|---|---|
| `A-GRID` | 8 (grey) | CENTER | 0.18 | osie strukturalne |
| `A-WALL-EXT` | 5 (blue) | CONTINUOUS | 0.50 | ściany zewnętrzne 400 mm |
| `A-WALL-INT` | 2 (yellow) | CONTINUOUS | 0.35 | ściany wewnętrzne 150 mm |
| `A-WALL-INT-MTMB` | 42 (orange) | CONTINUOUS | 0.35 | CLT 120 mm (embodied carbon low) |
| `A-WALL-PART` | 3 (green) | CONTINUOUS | 0.25 | ścianki działowe 100 mm |
| `A-WALL-LEAD` | 1 (red) | CONTINUOUS | 0.50 | ściany ołowiane RTG/TK/MR (2 mm Pb) |
| `A-WALL-FARA` | 210 (purple) | CONTINUOUS | 0.50 | Faraday cage MR |
| `A-DOOR` | 30 (orange) | CONTINUOUS | 0.25 | drzwi (ark + panel) |
| `A-DOOR-IDEN` | 30 | CONTINUOUS | 0.25 | numery drzwi |
| `A-GLAZ` | 4 (cyan) | CONTINUOUS | 0.25 | szyby / curtain wall |
| `A-FLOR-FIXT` | 6 (magenta) | CONTINUOUS | 0.18 | stałe wyposażenie |
| `A-FLOR-PLMB` | 140 | CONTINUOUS | 0.18 | fixtury sanitarne (WC, umywalki) |
| `A-FLOR-CASE` | 32 | CONTINUOUS | 0.18 | zabudowy meblowe (casework) |
| `A-EQPM-MED` | 190 | CONTINUOUS | 0.35 | sprzęt medyczny (CT, MR, C-arm, OR table) |
| `A-AREA` | 60 | HIDDEN2 | 0.18 | obrysy pomieszczeń (dla liczenia m²) |
| `A-AREA-IDEN` | 7 (white) | CONTINUOUS | 0.25 | nazwy + numery + powierzchnie pokoi |
| `A-ANNO-TEXT` | 7 | CONTINUOUS | 0.25 | opisy ogólne MText |
| `A-ANNO-DIMS` | 256 | CONTINUOUS | 0.18 | wymiary |
| `A-ANNO-NOTE` | 7 | CONTINUOUS | 0.25 | tekst uwagi + leadery |
| `A-ANNO-SYMB` | 7 | CONTINUOUS | 0.25 | symbole północy, skali |
| `A-ANNO-TTLB` | 7 | CONTINUOUS | 0.35 | title block |

### Warstwy strukturalne (S-)

| Warstwa | Kolor | Linetype | Lw | Zastosowanie |
|---|---:|---|---|---|
| `S-GRID` | 8 | CENTER | 0.18 | osie A-K / 1-10 |
| `S-GRID-IDEN` | 7 | CONTINUOUS | 0.35 | bubble markers |
| `S-COLS` | 92 | CONTINUOUS | 0.50 | słupy żelbetowe 450×450 |

### Warstwy mechaniczne / sanitarne / elektryczne / teletech

| Warstwa | Kolor | Zastosowanie |
|---|---:|---|
| `M-HVAC-DUCT` | 142 | kanały wentylacyjne main supply |
| `M-HVAC-RTRN` | 143 | kanały powrotu |
| `M-HVAC-HEPA` | 1 | stream HEPA do OR (czerwony critical) |
| `P-SANR` | 140 | piony sanitarne |
| `P-WATR-HOT` | 1 | CWU |
| `P-WATR-COLD` | 5 | ZWU |
| `P-GAS-MDCL` | 190 | med gas (O₂, N₂O, Vac, Air) |
| `E-LITE` | 50 | oświetlenie general |
| `E-LITE-EMER` | 1 | oświetlenie awaryjne PN-EN 1838 |
| `E-POWR-NORM` | 3 | gniazda standard (230 V) |
| `E-POWR-EMER` | 1 | gniazda UPS (czerwone) |
| `E-POWR-EMRG` | 2 | gniazda diesel (żółte) |
| `T-CABL-IOTN` | 251 | kabelkanały IoT / sensor-ready |
| `T-CABL-DATA` | 4 | structured cabling CAT 6A |
| `T-NURS-CALL` | 221 | nurse call system |

### Warstwy cywilne / landscape

| Warstwa | Zastosowanie |
|---|---|
| `C-TOPO` | granica działki, krawężniki |
| `C-ROAD-CURB` | podjazd karetek, chodniki |
| `L-PLNT` | zieleń (dziedziniec G) |
| `L-SITE-FURN` | ławki, kosze |

Łącznie **~35 warstw** (vs 17 w audytowanym pliku + z czego większość była "0", "8", "WALL 1 300" bez znaczenia).

---

## 4. Style tekstu i wymiarów (annotative)

Wszystkie style są **annotative** — skala renderu dobiera się automatycznie do skali viewportu (1:100, 1:50, 1:20).

### Text styles

| Nazwa | Font | Height (annotative) | Zastosowanie |
|---|---|---:|---|
| `ST-ANN-025` | arial.ttf | 2.5 mm | opisy małe (nr drzwi, detale) |
| `ST-ANN-035` | arial.ttf | 3.5 mm | standard opisy |
| `ST-ANN-050` | arial.ttf | 5.0 mm | nazwy pomieszczeń |
| `ST-ANN-070` | arialbd.ttf | 7.0 mm | nagłówki stref / numery pokoi |
| `ST-TITLE` | arialbd.ttf | 10.0 mm | title block headings |
| `ST-ISOP` | isocpeur.ttf | 2.5 mm | styl inżynierski do wymiarowania |

### Dimension styles

| Nazwa | Skala | Tekst | Zastosowanie |
|---|---|---|---|
| `DIM-100-ARCH` | 1:100 | ST-ISOP 2.5 mm | wymiary architektoniczne zewnętrzne |
| `DIM-50-DETL` | 1:50 | ST-ISOP 2.5 mm | wymiary per pokój |
| `DIM-20-PREC` | 1:20 | ST-ISOP 2.5 mm | detale (precision) |

Wszystkie DIMSTYLE: `DIMUNIT=2` (decimal), `DIMDEC=0` (mm całkowite), `DIMLUNIT=2`, `DIMTIH=0` (tekst poziomo), `DIMTOH=0`, `DIMEXE=1.25`, `DIMASZ=2.5`.

---

## 5. Siatka strukturalna 7,8 × 8,4 m

| Oś | Współrzędna X (mm) | Oś | Współrzędna Y (mm) |
|---|---:|---|---:|
| A | 0 | 1 | 0 |
| B | 7 800 | 2 | 8 400 |
| C | 15 600 | 3 | 16 800 |
| D | 23 400 | 4 | 25 200 |
| E | 31 200 | 5 | 33 600 |
| F | 39 000 | 6 | 42 000 |
| G | 46 800 | 7 | 50 400 |
| H | 54 600 | — | — |
| I | 62 400 | — | — |
| J | 70 200 | — | — |
| K | 78 000 | — | — |

11 osi × 7 osi = 77 pól siatki. 7,8 × 8,4 m = 65,5 m² / pole.

---

## 6. Bloki (target: dynamic blocks z atrybutami)

Wszystkie bloki zdefiniowane z **jednostką wstawienia mm** (INSUNITS=4) i punktem bazowym w środku geometrycznym lub na osi symetrii (dla drzwi — na osi zawiasu).

| Block Name | Typ | Parametry dynamic | Zastosowanie |
|---|---|---|---|
| `BLK-DOOR-SINGLE` | drzwi jednoskrzydłowe | szerokość 900/1100/1200/1400 mm; L/R hand | pokoje standard, gabinety |
| `BLK-DOOR-DOUBLE` | drzwi dwuskrzydłowe | szerokość 1600/1800/2100/2400 mm | SOR resus, OR, magazyny |
| `BLK-DOOR-SLIDE` | drzwi przesuwne | szerokość 1400/1800/2200 mm | sala OR (hands-free), wiatrołapy |
| `BLK-DOOR-AIRLOCK` | drzwi szczelne AIIR | 1100 mm, interlock indicator | AIIR anteroom |
| `BLK-WIN-CASEMENT` | okno uchylne | szer. 900/1200/1500/1800 mm | fasady elewacyjne |
| `BLK-WIN-CURTAIN` | segment curtain wall | szer. 1500 mm typ | lobby, curtain wall |
| `BLK-WIN-LEAD` | okno ołowiane | 600 × 400, 2 mm Pb | sterownie RTG/TK |
| `BLK-GRID-BUBBLE` | bubble ⌀ 900 mm | atrybut `GRID_ID` | oznaczenie osi |
| `BLK-NORTH` | strzałka północy | — | layout |
| `BLK-SCALE-BAR` | podziałka liniowa | atrybut `SCALE_RATIO` | layout |
| `BLK-ROOM-TAG` | tag pomieszczenia | atrybuty `ROOM_NO`, `ROOM_NAME`, `ROOM_AREA` | każdy pokój |
| `BLK-DOOR-TAG` | tag drzwi | atrybut `DOOR_NO` | każde drzwi |
| `BLK-MEDGAS-HEADWALL` | headwall O₂/Vac/Air | 1200 × 300 mm | pre-op, PACU, pokoje łóżkowe |
| `BLK-OR-TABLE` | stół OR radiolucent | 2200 × 560 mm | OR-1..4 |
| `BLK-CT-SCANNER` | CT 128-slice donut | ⌀ 2400 × 2100 mm | C-303 |
| `BLK-MR-SCANNER` | MR 3T bore | 2300 × 1950 mm | C-305 |
| `BLK-CARM-BIPLANE` | C-arm biplane | 3500 × 3500 mm footprint | D-415 Hybrid OR |
| `BLK-WC-ACCESSIBLE` | WC EN 17210 + uchwyty | 2200 × 1800 mm | accessible WCs |
| `BLK-SINK-SCRUB` | umywalka chirurgiczna | 800 × 600 mm | scrub station |
| `BLK-TITLEBLOCK-A0` | title block A0 | atrybuty `PROJECT`, `SHEET_NO`, `REV`, `DATE`, `SCALE`, `DESIGNER` | layout arkusza |

Łącznie **20 bloków** (vs 11 nonsensownych w pliku audytowanym, bez ani jednego dynamic).

**Nazwy bloków przestrzegają ISO 13567 + wewnętrznego prefixu `BLK-`**, kategoria po drugim segmencie (`DOOR`, `WIN`, `EQPM` itp.). Plik audytowany miał `02ETRX29` — bez kategorii, bez czytelności.

---

## 7. Wytyczne przeciw-awaryjne (2026 — IPC + Resilience)

| Wymóg | Realizacja w projekcie |
|---|---|
| **12 ACH w AIIR, −2,5 Pa** | anteroom + HEPA H14 recirculation, wskaźnik ciśnienia przy drzwiach |
| **20 ACH w OR 1-4 przy 100 % fresh air** | dedykowane AHU OR-HEPA w maszynowni E-514 |
| **Sterile / dirty separation** | dwa korytarze fizycznie oddzielone (D-411 vs D-421) |
| **Dual power (normal + EPSS diesel)** | każda OR/AIIR zasilona z dwóch feederów + UPS 15 min |
| **Red power outlets** (UPS) + **żółte** (diesel EPSS) | wizualne oznaczenie, osobne warstwy CAD |
| **Seismic Category III** (kraje zagrożenia; PL nieobligatoryjne) | słupy 450×450 + stężenia stężone (nie rysujemy w parterze, ale rezerwujemy) |
| **Passive survivability 96 h** | rezerwa paliwa diesel 3 500 l + zbiornik wody ppoż 200 m³ (obiekt towarzyszący) |
| **Mass casualty surge capacity** | poczekalnia SOR konwertowalna na 12 cots overflow |
| **Wayfinding universal (ADA/EN 17210)** | kolor-coded zones, symbols > 100 mm, kontrast ≥ 70 % |
| **Lockdown / active threat** | drzwi oddziałowe z centralnym elektrozamkem, 3 strefy |

---

## 8. Layouts i sheet set

| Arkusz | Skala | Zawartość | Tytuł |
|---|---|---|---|
| **A-101** | 1:100 | Rzut parteru — cały obrys + strefy + nazwy + wymiary zewnętrzne | Ground Floor Plan — Overall |
| **A-102** | 1:50 | SOR powiększony | SOR Enlarged Plan |
| **A-103** | 1:50 | Blok operacyjny powiększony | OR Suite Enlarged Plan |
| **A-104** | 1:100 | Rzut warstw stropodachu | Roof Plan |
| **A-201** | 1:100 | Przekrój A-A (przez OR + lobby) | Section A-A |
| **A-202** | 1:100 | Przekrój B-B (przez SOR + AHU) | Section B-B |
| **A-301** | 1:100 | Elewacja południowa + północna | Elevations S/N |
| **A-401** | 1:50 | Detale (węzeł ściana-strop, detal MR Faraday) | Details |
| **M-101** | 1:100 | HVAC — parter | Mechanical Ground Floor |
| **E-101** | 1:100 | Elektryka — parter | Electrical Ground Floor |
| **P-101** | 1:100 | Sanitarka + med gas | Plumbing & Med Gas |

Minimum do wykonania w fazie A tego ćwiczenia: **A-101** (overall plan) + **A-102** (SOR enlarged) + **A-103** (OR suite enlarged) — reszta zostaje jako sheet templates.

---

## 9. Plan wykonania (fazy `acad_design_iterate`)

Każda faza jest jednym wywołaniem `acad_design_iterate` z własnym checkpointem, żeby można było cofnąć jedną fazę bez utraty poprzednich:

| # | Faza | Czas szac. | Entities target | Sprawdzenie |
|---|---|---:|---:|---|
| 0 | Setup (warstwy, text styles, dim styles, limits, INSUNITS=4) | ~40 s | ~0 geom | `list_layers` (≥ 30) |
| 1 | Siatka strukturalna 11 × 7 + bubble markers | ~30 s | ~100 | bubble blocks obecne |
| 2 | Obrys zewnętrzny + dziedziniec + ściany zewn. 400 mm | ~40 s | ~200 | `A-WALL-EXT` > 20 pline |
| 3 | Strefy A-E — ściany główne podziału | ~60 s | ~300 | `A-WALL-INT` > 50 |
| 4 | Pokoje — ściany działowe per pomieszczenie | ~120 s | ~800 | liczba zamkniętych polilinii ~115 |
| 5 | Drzwi — dynamic blocks insercja | ~90 s | ~180 inserts | `list_blocks` → `BLK-DOOR-*` refs |
| 6 | Okna + curtain wall | ~60 s | ~90 inserts | `BLK-WIN-*` refs |
| 7 | Fixtury + sprzęt medyczny (CT, MR, C-arm, WC, scrubs, headwalls) | ~90 s | ~80 inserts | `BLK-EQPM-*` refs |
| 8 | Opisy pomieszczeń (ROOM-TAG × 115) + numery | ~90 s | ~115 mtext + 115 blocks | `A-AREA-IDEN` count |
| 9 | Wymiarowanie zewn. + wewn. + osiowe | ~120 s | ~250 dims | `A-ANNO-DIMS` count |
| 10 | Layout A-101 + title block A0 + viewport 1:100 | ~40 s | layout + title | `list_layouts` ≥ 4 |
| 11 | Walidacja (`doc_summary` + listing) | ~20 s | — | raport OK |

Łącznie ~13 minut "zegarka projektanta-agenta" (realnie pewnie 30-40 min kalendarzowych z uwzględnieniem retry przy LockDocument).

---

## 10. Kryteria zgodności (acceptance)

Po zakończeniu wszystkich faz, plik **musi** przejść następujący self-check:

| Kryterium | Target | Sposób weryfikacji |
|---|---|---|
| `INSUNITS` | `4` (millimeters) | `acad.validators.doc_summary` |
| `MEASUREMENT` | `1` (metric) | j.w. |
| Liczba warstw | ≥ 30 | `acad.layers.list_layers` |
| Liczba text styles | ≥ 6 | `acad.annotations.list_text_styles` |
| Liczba dim styles | ≥ 3 | `acad.dimensions.list_dim_styles` |
| Liczba bloków definicji | ≥ 15 (w tym ≥ 5 BLK-DOOR*/BLK-WIN*) | `acad.blocks.list_blocks` |
| Liczba pokoi (ROOM-TAG insertów) | ≥ 110 | filter `list_blocks` → insert count |
| Liczba drzwi (DOOR-TAG insertów) | ≥ 80 | j.w. |
| Liczba wymiarów | ≥ 200 | `A-ANNO-DIMS` entity count |
| Liczba layoutów ≠ Model | ≥ 3 (A-101, A-102, A-103) | `acad.layouts.list_layouts` |
| Proporcja encji typu `Line` | **< 15 %** (vs 83 % w pliku audytowanym) | `doc_summary.typeHistogram` |
| Proporcja encji typu `DBText` / `MText` / `Dimension` | > 10 % łącznie | j.w. |
| Aktywna warstwa na koniec | **NIE `0`** (np. `A-WALL-INT`) | `acad_status.activeLayer` |

Jeśli projekt nie przechodzi któregoś kryterium — regeneracja odpowiedniej fazy z checkpointu.

---

## 11. Rozszerzenia poza tym ćwiczeniem

Tego **nie** robimy w tej iteracji (żeby nie przedłużać), ale masterplan wskazuje miejsce:

- **BIM export** — IFC 4.3 via `acad.files.export_file` (wymaga AutoCAD Architecture, nie robimy na vanilla AutoCAD)
- **3D realistic** — konceptualna wizualizacja zrobiona w poprzedniej sesji; tutaj skupiamy się na dokumentacji 2D
- **Piętra 1-3** — identyczna siatka + oddziały łóżkowe 5 × 24 single-bed rooms = 120 łóżek; layout kopiowalny z parteru w jednym rzucie
- **Walidator `hospital-2026-baseline.yaml`** — dedykowany plik YAML w `validators/rules/` z regułami na: unit check, layer naming AIA, minimum room areas, AIIR compliance, OR ACH implied requirement
- **Egress / fire** — plan ewakuacji jako A-105 (path of egress, exit signs, F-E extinguishers), regulacja PL WT 2022 § 211-237
- **Clash detection** — gdy dołączą M/E/P, `acad.validators.clash` (future Phase 8)

---

## 12. Punkt referencyjny vs plik audytowany

| Cecha | `[REDACTED-REFERENCE-DWG]` (audyt) | Hospital-2026 (ten plan) |
|---|---|---|
| Jednostki | imperial `in` | metric `mm` |
| INSUNITS | 1 (inches) | 4 (millimeters) |
| Liczba warstw | 17 (większość useless: "0","8","WALL 1 300") | ~35 (AIA + ISO 13567) |
| Warstwa aktywna na koniec | `0` | `A-WALL-INT` (lub inna celowa) |
| Liczba text styles | 2 | ≥ 6 (annotative) |
| Liczba dim styles | 1 | ≥ 3 |
| Liczba bloków definicji | 11 (nonsensowne nazwy) | ≥ 20 (ISO-named, z czego ≥ 10 dynamic) |
| Dynamic blocks | 0 | ≥ 10 |
| Hatch fills | 0 (używano 2D Solid — 14 591 szt.) | Hatch ANSI31 / AR-CONC / AR-BRELM |
| 3DFace | 1 020 (legacy) | 0 |
| Proporcja `Line` | 83 % (exploded) | < 15 % |
| Layouty | 1 (`Projects1200x1100` PublishToWeb PNG) | ≥ 4 (A-101, A-102, A-103, A-104) |
| Title block | brak | BLK-TITLEBLOCK-A0 z atrybutami |
| Opisy pomieszczeń | losowe teksty | ROOM-TAG block × 115 z `ROOM_NO`, `ROOM_NAME`, `ROOM_AREA` |
| BIM-ready | NIE | TAK (LOD 200, gotowe do IFC Space + IfcWallStandardCase) |

---

---

## 13. Zgodność z polskim prawem 2026 — macierz compliance

Sekcja ta **zastępuje** wszelkie hand-wavy odwołania do "normy" w sekcjach 1–12 konkretnymi numerami Dz.U./paragrafami/PN. Projekt jest kwalifikowany jako **budynek użyteczności publicznej, zakład opieki zdrowotnej, kategoria zagrożenia ludzi ZL II**, budynek niski (N, H ≤ 12 m — parter + 2 piętra techniczne).

### 13.1 Hierarchia aktów prawnych obowiązujących kwiecień 2026

| Skrót | Pełna nazwa | Rola w projekcie |
|---|---|---|
| **Pr. bud.** | Ustawa z 7.07.1994 Prawo budowlane (Dz.U. 2024 t.j.) | podstawa procesowa: projekt budowlany, pozwolenie |
| **WT 2024** | Rozp. MI z 12.04.2002 (tj. **Dz.U. 2022 poz. 1225**), nowelizacje Dz.U. 2023 poz. 2442, Dz.U. 2024 poz. 474, **Dz.U. 2024 poz. 726** (obowiązuje od 15.08.2024) | parametry techniczne budynku, bezpieczeństwo pożarowe |
| **Rozp. MZ 2019** | Rozp. MZ z 26.03.2019 (tj. **Dz.U. 2022 poz. 402**, Dz.U. 2019 poz. 595) | wymagania dla pomieszczeń i urządzeń podmiotu leczniczego, Załącznik nr 1 = szpitale |
| **Ust. dostęp.** | Ustawa z 19.07.2019 o zapewnianiu dostępności (Dz.U. 2019 poz. 1696, tj. Dz.U. 2024) | dostępność dla osób ze szczególnymi potrzebami |
| **Ust. ppoż.** | Ustawa z 24.08.1991 o ochronie ppoż. (t.j. Dz.U. 2024) + Rozp. MSWiA z 7.06.2010 (Dz.U. 2023 poz. 822 t.j.) | instalacje ppoż., IBP |
| **Wytyczne MZ HVAC** | Wytyczne projektowania, wykonania, odbioru i eksploatacji systemów wentylacji i klimatyzacji dla podmiotów leczniczych (MZ 2018 z aktualizacjami) | klasy S1–S4, ACH, HEPA |
| **Pr. atom.** | Ustawa z 29.11.2000 Prawo atomowe (t.j. Dz.U. 2024) + Rozp. MZ z 21.08.2006 (Dz.U. 2006 nr 180 poz. 1325 z późn. zm.) | ochrona radiologiczna RTG/TK |
| **Pr. farm.** | Ustawa z 6.09.2001 Prawo farmaceutyczne + EU GMP Annex 1 (2022) | apteka szpitalna, clean room aseptyczny |
| **PN-EN 13501-1** | Klasyfikacja ogniowa wyrobów budowlanych | materiały wykończeniowe |
| **PN-EN 1838** | Oświetlenie awaryjne | E-LITE-EMER |
| **PN-EN 12464-1** | Oświetlenie miejsc pracy wewnątrz | E-LITE |
| **PN-EN 13779 / PN-EN 16798-3** | Wentylacja budynków niemieszkalnych | M-HVAC |
| **PN-EN ISO 14644-1:2016** | Klasyfikacja czystości pyłowej (ISO 5 — OR, ISO 7 — PACU) | sale klasy S1/S2 |
| **PN-EN 1822** | Filtry HEPA (H13/H14) | M-HVAC-HEPA |
| **PN-EN ISO 16890-1** | Filtry wstępne (ePM1, ePM2.5) | M-HVAC |
| **PN-B-02151-02, -03, -04** | Akustyka budynków | izolacja akustyczna |

### 13.2 Kategoria zagrożenia ludzi i klasa odporności pożarowej

**Kategoria ZL II** — WT 2024 § 209 ust. 1 pkt 2: "budynki przeznaczone przede wszystkim do użytku ludzi o ograniczonej zdolności poruszania się, takich jak szpitale, żłobki, przedszkola, domy dla osób starszych".

Przy wysokości budynku **H ≤ 12 m** (niski, N), WT § 212 ust. 2 tabela wymaga:

> **ZL II + budynek niski (N) → wymagana klasa odporności pożarowej "B"**

**⚠ Uwaga kluczowa:** WT § 214 explicite wyłącza ZL II z możliwości obniżenia klasy odporności pożarowej nawet przy zastosowaniu tryskaczy — **tryskacze i tak instalujemy** (§ 27 Rozp. MSWiA 2010 oraz wytyczne ubezpieczyciela), ale klasa pozostaje **B**.

Klasa B oznacza wg § 216 WT:

| Element | Odporność ogniowa |
|---|---|
| Główna konstrukcja nośna (słupy, stropodach nośny) | **R 120** |
| Stropy międzykondygnacyjne | **REI 60** |
| Ściany zewnętrzne (strefa pożarowa) | **EI 60** |
| Ściany wewnętrzne oddzielenia pp | **EI 60** |
| Ściany wewnętrzne działowe (zwykłe) | **EI 30** |
| Drzwi w ścianach oddzielenia pp | **EI 60** (z samozamykaczem, § 232 WT) |
| Konstrukcja dachu | **R 30** |
| Przekrycie dachu | **RE 30** |

Konsekwencje projektowe dla warstw CAD:

- `A-WALL-STRUCT` — słupy 450×450 żelbet R 120 (okładziny ognioodporne jeżeli stalowe rdzenie)
- `A-WALL-EXT` 400 mm — warstwy: tynk 20 + żelbet/silikat 240 + wełna mineralna 120 + tynk cienkowarstwowy 20 → REI 60+ z zapasem
- `A-WALL-FIRE` — **nowa warstwa, dopisać do § 3 masterplanu** — ściany oddzielenia pp REI 60, kolor 11 (magenta przełam), hatch skośny ANSI31 + tekst "EI60"
- `A-DOOR-FIRE` — drzwi ppoż. EI 60, symbol rombu z numerem, warstwa kolor 1 (red)

### 13.3 Strefy pożarowe — krytyczne, bo parter 80 × 60 > max strefy

WT § 227 ust. 1: **max powierzchnia strefy pożarowej ZL II w budynku niskim = 3 500 m²**.

Powierzchnia parteru brutto = 80 × 60 = **4 800 m²** → **musimy podzielić parter na co najmniej 2 strefy pożarowe oddzielone ścianą REI 60**.

**Podział stref pożarowych (rysunek schematyczny, oś Y = 0 m, osie X w mm):**

```
      A   B   C   D   E   F   G   H   I   J   K
      0  7.8 15.6 23.4 31.2 39.0 46.8 54.6 62.4 70.2 78.0  [m]
  ┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐
1 │ Strefa A — STREFA PUBLICZNO-AMBULATORYJNA │   (A + B + C)
  │    lobby + poczekalnie + SOR + diagnostyka │    ~2 400 m²
  │  ~poprzez oś F (x = 39 m) ściana FIRE WALL │
  ├═══════════════════════════════════════════┤  ← ściana oddzielenia pp EI 60
  │ Strefa B — STREFA OPERACYJNO-TECHNICZNA    │   (D + E)
7 │    blok operacyjny + PACU + back-of-house  │    ~2 400 m²
  └───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘
```

Obie strefy ≤ 3 500 m² — zgodność ✓ (z zapasem ~31 %).

**Drzwi ppoż. w ścianie oddzielenia** (oś F, y = 0…50,4):
- przejście główne publiczne (lobby → korytarz diagnostyki): EI 60 1400 mm dwuskrzydłowe
- przejście sterile corridor (łóżkowe): EI 60 1800 mm przesuwne z interlockiem
- przejście dirty corridor: EI 60 1400 mm
- przejście service: EI 60 1100 mm

**Ściana wydzielająca maszynownię E-514** — wg WT § 212 ust. 9 pomieszczenia techniczne stanowią **odrębną strefę pożarową**; konieczność ściany REI 60 wokół AHU + odrębny dostęp z dachu lub korytarza service.

### 13.4 Drogi ewakuacji (WT rozdz. 4 Dz. VI)

| Parametr | Wymóg WT | Realizacja projektu |
|---|---|---|
| Min. liczba wyjść ewakuacyjnych ze strefy ZL II | **2** (§ 236 WT) | 3 wyjścia na Strefę A (główne, SOR, diagnostyczne), 3 na Strefę B (OR, service, dock) ✓ |
| Max długość przejścia w pomieszczeniu | **40 m** (§ 237 ust. 1) — 75 m z oddymianiem | wszystkie pomieszczenia < 15 m → ✓ |
| Max długość dojścia z **1 kier.** | **10 m** (§ 237 ust. 6 pkt 1) | wszystkie drogi mają 2 kierunki → ✓ |
| Max długość dojścia z **2 kier.** | **30 m** (§ 237 ust. 6 pkt 2) | najdłuższy dojście z OR-4 (D-415) do najbliższego wyjścia: ~28 m → ✓ |
| Szerokość drogi ewakuacyjnej (korytarz) | min **1,4 m** + 0,6 m na każde 100 osób (§ 242) | sterile 2,8 m, dirty 2,2 m, publiczne 2,4 m (przy 180-osobowej strefie: wymóg ok. 1,4 + 1,2 = 2,6 m — **mój publiczny korytarz 2,4 m to ZA MAŁO, poprawić na 2,8 m dla strefy publicznej**) |
| Szerokość drzwi na drogę ewakuacyjną | min **0,9 m** w świetle (§ 239) | drzwi standardowe 1,1 m ✓ |
| Szerokość drzwi ewakuacyjnych głównych | **1,2 m / 100 osób** | lobby A-101 wiatrołap 2× 1800 mm = 3,6 m dla 300 osób (max) → ✓ |
| Oddymianie klatek schodowych | wymagane w ZL II (§ 245 ust. 5) | nie dotyczy parteru, ale zarezerwowane miejsce na klapy dymowe w dachu sterile corridor |
| Oświetlenie awaryjne wg PN-EN 1838 | min **1 lx** na podłodze drogi ewakuacyjnej, min **1 h** backup | `E-LITE-EMER` na UPS 2 h + EPSS diesel |
| Znaki ewakuacyjne | PN-ISO 7010, PN-EN 1838, fotoluminescencyjne | warstwa `A-ANNO-SYMB-EGRS` (dopisać) |

**Decyzja projektowa:** korytarz w Strefie A (publiczny) rozszerzam z 2,4 m do **2,8 m** (zgodnie z obliczeniem szerokości dla 180 osób). Warstwa `A-WALL-PART` — repozycja o 400 mm w kierunku pomieszczeń rejestracji.

### 13.5 Wysokości pomieszczeń (WT § 72)

| Pomieszczenie | Min. wysokość netto |
|---|---|
| Pokój chorego / łóżkowy | **3,3 m** (§ 72 tab., kolumna "szpitale") |
| Sala operacyjna | **3,3 m** (+ strop techniczny, realnie **4,2 m konstrukcyjne**) |
| Pomieszczenia pomocnicze (magazyny, socjalne) | **2,5 m** |
| Korytarze | **2,5 m** (nad sufitem podwieszanym: 2,8 m) |
| AHU / maszynownia | **2,2 m** (§ 72 ust. 1 pkt 3) — projekt: 3,0 m ze względu na gabaryt central |

**Projektowa wysokość konstrukcyjna parteru: 4 500 mm** (3 300 netto + 1 200 na instalacje HVAC/strop techniczny OR).

### 13.6 Sale operacyjne — klasa S1 (Wytyczne MZ HVAC + PN-EN ISO 14644-1)

| Parametr | Wymóg | Realizacja |
|---|---|---|
| Klasa czystości pyłowej obszaru chronionego | **ISO 5** (S1a/S1b — zabiegi ortopedyczne, implanty) | OR-1, OR-2, **OR-4 Hybrid** → S1a |
| Klasa czystości (zabiegi niższego ryzyka) | ISO 7 (S1c) | OR-3 endoskopowa → S1c |
| Filtracja powietrza nawiewanego — 3 stopnie | ePM1 ≥ 50 % (F7) + ePM1 ≥ 80 % (F9) + **HEPA H13/H14** | wszystkie OR: H14 w nawiewnikach stropowych |
| Wydajność wentylacji | LAF 0,2–0,3 m/s, ok. **2 400 m³/h świeżego powietrza** na salę | dedykowane AHU OR-HEPA w E-514 |
| Krotność wymian ACH | **≥ 20/h** (S1), typowo 25/h | 25/h przyjęte — spadek ciśnienia filtrów wymaga zapasu wentylatorów |
| Nadciśnienie względem korytarza sterylnego | **min +15 Pa** (S1 vs sąsiednie) | czujnik różnicy ciśnień przy drzwiach każdej OR |
| Temperatura | **24 °C** (§ 134 WT tabela dla sali operacyjnej); ad-hoc 17–26 °C regulowane | AHU z nagrzewnicą wodną + chłodnica DX |
| Wilgotność względna | **30–60 %** (Wytyczne MZ) | nawilżanie parowe higieniczne |
| Obszar chroniony (LAF) | **co najmniej 3,2 × 3,2 m = 10,24 m²** (typowo 3,0 × 3,0 m minimum) | OR-1, OR-2: LAF 3,0 × 3,0; OR-4: LAF 3,6 × 3,6 |
| Strop laminarny (LAF panel) | nawiew z HEPA H14 w całej powierzchni | blok CAD `BLK-OR-LAF-PANEL` (dopisać) |
| Odrzut materiału PN-EN 1822 | wg testów MPPS | dokumentacja przy odbiorze |

### 13.7 AIIR (izolatki zakaźne) — klasa S3

| Parametr | Wymóg | Realizacja |
|---|---|---|
| Podciśnienie względem sąsiednich | **min −5 Pa** (Wytyczne MZ, S3) | 2× AIIR: D-490A, D-490B (dopisać do programu Strefy D) |
| ACH | **≥ 12/h** | 15/h + HEPA H13 w wywiewie |
| Anteroom (śluza) | **obowiązkowy**, z interlockiem drzwi | `BLK-DOOR-AIRLOCK` |
| Wywiew | na zewnątrz budynku, nie do recyrkulacji | pion odrębny w maszynowni |
| Okno obserwacyjne | obowiązkowe z sąsiedniego pomieszczenia | szyba bezszprosowa 900×600 mm |

**Program pomieszczeń AIIR — DOPISANE do Strefy B (SOR):**

| Nr | Nazwa | m² | Uwaga |
|---|---|---:|---|
| B-222 | AIIR 1 — izolatka zakaźna | 16 | podciśnienie −5 Pa, 15 ACH, HEPA wywiew |
| B-223 | Anteroom AIIR 1 | 4 | interlock, drop-zone PPE |
| B-224 | AIIR 2 — izolatka zakaźna | 16 | j.w. |
| B-225 | Anteroom AIIR 2 | 4 | j.w. |

### 13.8 Ochrona radiologiczna (Pr. atom. + Rozp. MZ 2006)

| Pracownia | Osłona ścienna | Osłona drzwi | Osłona stropu |
|---|---|---|---|
| TK (C-303) 128-slice, ~120 kVp | **Pb ≥ 2 mm** równoważnik, lub żelbet 250 mm | Pb 2 mm, okno z Pb-glass 2 mm | Pb 2 mm (jeśli nad pomieszczeniem stałego pobytu) |
| RTG I (C-308) | Pb 1,5–2 mm | Pb 1,5 mm | Pb 1,5 mm |
| RTG mammograf (C-310) | Pb 1 mm wystarczy | Pb 1 mm | Pb 1 mm |
| MR 3T (C-305) | **Klatka Faradaya** (nie ołów) — miedź 0,5 mm lub stal RF-shielded | drzwi RF | strop RF |

Warstwy CAD:
- `A-WALL-LEAD` (już w § 3) — kolor 1, hatch `AR-RSHKE` + tekst "Pb 2 mm"
- `A-WALL-FARA` (już w § 3) — dla MR, kolor 210
- `A-AREA-RAD-CTRL` — strefa kontrolowana wg Pr. atom. § 12 (linia przerywana po obrysie pracowni + 3 m pas buforowy)
- `A-AREA-RAD-OVER` — strefa nadzorowana

**Strefa 4 Gauss MR** — obowiązkowe oznakowanie podłogi (linia ciągła czerwona + piktogram). Warstwa `A-AREA-MR-4GS`, fizyczna bariera (balustrada 1,1 m lub ściana) wymagana tam, gdzie strefa wychodzi poza pracownię MR.

### 13.9 Dostępność — Ust. dostęp. 2019 + WT § 54–62

| Wymóg | Realizacja |
|---|---|
| Drzwi wejściowe min. **1,5 m** szerokości; strefa manewrowa 1,5 × 1,5 m | wiatrołap A-101: 2× 1800 mm przesuwne, strefa manewrowa 3,0 × 3,0 m ✓ |
| Próg drzwi zewnętrznych max **20 mm** | próg magnetyczny 15 mm w wiatrołapie ✓ |
| Każda WC publiczna z co najmniej 1 kabiną dostępną | A-108 universal + jedna dostępna w A-106, A-107 ✓ |
| WC dostępne min **2,5 × 2,2 m**, drzwi **0,9 m** otwierane na zewnątrz | `BLK-WC-ACCESSIBLE` 2,2 × 1,8 m — **za mały**, korekta blocku do **2,5 × 2,2** |
| Poręcze, uchwyty, kontrast kolorystyczny ≥ 70 % | warstwa `A-FLOR-FIXT-GRAB` dla uchwytów |
| Oznakowanie w alfabecie Braille'a + tabliczki piktogramowe | ROOM-TAG wariant "accessible" z polem braille |
| Pętle indukcyjne w rejestracji | symbol `BLK-ANNO-LOOP` nad biurkami A-102 |

**Korekta do § 6 masterplanu:** blok `BLK-WC-ACCESSIBLE` ma wymiary **2,5 × 2,2 m** (było 2,2 × 1,8).

### 13.10 Akustyka (PN-B-02151, WT § 323–327)

| Pomieszczenie | Wymóg dźwiękoizolacyjny ściany |
|---|---|
| Pokój chorego ↔ pokój chorego | R'w ≥ **50 dB** |
| Pokój chorego ↔ korytarz | R'w ≥ **45 dB** |
| Sala operacyjna ↔ korytarz sterylny | R'w ≥ **52 dB** |
| Psychiatric safe room (B-207) ↔ otoczenie | R'w ≥ **52 dB** (dodatkowa warstwa włókno-cement) |

Warstwa `A-WALL-PART` rozwidlona na trzy warianty na podstawie dźwiękoizolacyjności — kodowanie w atrybucie bloku ściany (nowa koncepcja, do wdrożenia jeżeli czas pozwoli): `PART-ACS-45`, `PART-ACS-50`, `PART-ACS-52`.

### 13.11 Oświetlenie (PN-EN 12464-1)

| Pomieszczenie | Min. natężenie średnie [lx] | Tmin [K] | CRI |
|---|---:|---:|---:|
| Sala operacyjna (otoczenie) | **1 000** | 4 000 | 90 |
| Pole operacyjne (scialityczne) | 10 000–100 000 | 4 500 | 95 |
| Korytarz szpitalny (dzień) | 100 | 3 000–4 000 | 80 |
| Korytarz szpitalny (noc) | 50 | 2 700 | 80 |
| Pokój chorego (ogólne) | 100 | 3 000 | 90 |
| Pokój chorego (badanie) | 300 | 4 000 | 90 |
| Stanowisko pracy — czytelnia PACS | 300 | 4 000 | 95 |
| Rejestracja, recepcja | 300 | 4 000 | 80 |
| Pracownia analityczna | 500 | 4 000 | 90 |
| Apteka — clean room | 500 | 4 000 | 90 |
| Oświetlenie awaryjne drogi ewak. | min **1** (PN-EN 1838) | 2 700 | 40 |

Warstwy: `E-LITE-AREA-100`, `E-LITE-AREA-300`, `E-LITE-AREA-500`, `E-LITE-AREA-1000`, `E-LITE-EMER`, `E-LITE-TASK`.

### 13.12 Macierz zgodności — kluczowe pomieszczenia vs norma

| Pomieszczenie | Moja projektowana pow. | Minimum wg wytycznych | Status |
|---|---:|---:|---|
| Sala operacyjna ogólna (OR-1, OR-2) | 48 m² | ≥ 30 m² | ✓ z zapasem |
| Sala OR-3 endoskopowa | 36 m² | ≥ 25 m² | ✓ |
| Sala OR-4 Hybrid | 72 m² | ≥ 50 m² (kardiochirurgia) | ✓ |
| PACU bay (stanowisko) | 12 m² | ≥ 6 m² / stanowisko + komunikacja 2 m² | ✓ |
| Pre-Op bay | 10 m² | ≥ 6 m² / stanowisko | ✓ |
| Sala resuscytacyjna SOR (2 stan.) | 45 m² | ≥ 25 m² / 1 stanowisko = 50 m² / 2 | **marginalnie poniżej** — **korekta do 52 m² (rozszerzenie o 1,5 m w osi A-B)** |
| Gabinet zabiegowy opatrunkowy (B-214) | 20 m² | ≥ 12 m² | ✓ |
| Gabinet konsultacyjny (B-208, B-209) | 16 m² | ≥ 12 m² | ✓ |
| Triage | 24 m² | ≥ 18 m² (rekomendacja PTMR) | ✓ |
| AIIR pokój | 16 m² | ≥ 12 m² + anteroom | ✓ |
| Izolatka anteroom | 4 m² | ≥ 3 m² | ✓ |
| WC pacjentów publiczne | 6–18 m² | ≥ 3 m² (§ 83 WT) + dostępny min 5 m² | ✓ |
| WC dostępne uniwersalne | 8 m² | ≥ 2,5 × 2,2 = 5,5 m² | ✓ |
| Sterile corridor | 2,8 m szer. | ≥ 2,2 m (Wytyczne MZ) | ✓ |
| Dirty corridor | **2,2 m szer.** (poprawione) | ≥ 2,2 m | ✓ (na styku normy) |
| Public corridor (strefa A) | 2,8 m (po korekcie § 13.4) | 1,4 + 1,2 = 2,6 m dla 180 osób | ✓ |
| Drzwi sala operacyjna | 1 400 / 1 800 mm | ≥ 1 400 mm (WT § 62 dla łóżek) | ✓ |
| Drzwi sala chorego | 1 100 mm | ≥ 1 100 mm | ✓ |
| Drzwi dostępne dla niepełnosprawnych | 900 mm | ≥ 900 mm (§ 62) | ✓ |
| Wysokość pokoju chorego (netto) | 3 300 mm | ≥ 3 300 mm (§ 72) | ✓ (na styku normy) |
| Wysokość sali operacyjnej | 3 300 mm netto (4 500 konstr.) | ≥ 3 300 mm | ✓ |

**Korekta programowa #2:** B-205 Sala resuscytacyjna — rozszerzenie z 45 m² do **52 m²** (ściana w osi B przesunięta o 1,5 m w osi X).

### 13.13 Charakterystyka energetyczna (WT Dz. X § 328–331, od 1.01.2021, EP_budynek)

Szpitale są budynkami użyteczności publicznej — max wskaźnik **EP** (energia pierwotna) = **190 kWh/(m²·rok)** (WT § 329 tab. 1 wiersz "ZOZ" od 2021 r.; od 2026 projekt ustawy dąży do 160 — na dzień pisania dokumentu obowiązuje 190).

Warunki brzegowe projektu:
- współczynnik przenikania ciepła U — WT § 328 tab. 2:
  - ściana zewnętrzna: U_max = **0,20 W/(m²·K)** → warstwa wełny 20 cm λ=0,035
  - okno: U_max = **0,9 W/(m²·K)** → trzyszybowe Ug = 0,6
  - drzwi zewnętrzne: U_max = **1,3 W/(m²·K)**
  - dach/stropodach: U_max = **0,15 W/(m²·K)**
  - podłoga na gruncie: U_max = **0,30 W/(m²·K)**
- odzysk ciepła wentylacji: **min 70 %** (rekuperator)
- system BMS obowiązkowy dla budynku o mocy HVAC > 290 kW (szpital — tak)

Uwaga projektowa: to dokumentacja 2D, więc nie wymiarujemy przekrojów ścian, ale **warstwa `A-WALL-EXT` jest oznakowana atrybutem "U=0.18 W/m²K"** na tabliczce legendy.

---

## 14. Skorygowany plan wykonania (po audycie prawnym)

Dwie korekty do § 9:

| Faza | Korekta | Efekt w CAD |
|---|---|---|
| 3 (Strefy) | Dodać **ścianę oddzielenia ppoż. osi F** (REI 60, warstwa `A-WALL-FIRE` kolor 11) + ścianę wydzielenia maszynowni E-514 | +2 linie na `A-WALL-FIRE` zamiast `A-WALL-INT` |
| 4 (Pokoje) | Rozszerzyć B-205 do 52 m², przesunąć ścianę B-206/B-207 o 1,5 m w osi X; **dodać** B-222/B-223/B-224/B-225 (2× AIIR + anteroom) w południowo-zachodnim narożniku SOR; skorygować szerokość public corridor z 2,4 m na 2,8 m | +4 pomieszczenia, zmiana 3 ścian |
| 5 (Drzwi) | Wprowadzić wariant `BLK-DOOR-FIRE-EI60` dla 4 przejść w ścianie osi F | +4 nowe insercje |
| 6 (Okna) | bez zmian | — |
| 7 (Fixtury) | Zmiana bloku `BLK-WC-ACCESSIBLE` z 2,2×1,8 na **2,5×2,2** | redefinicja bloku |
| 8 (Opisy) | Każdy pokój ma ROOM-TAG z dodatkowym atrybutem `ROOM_CLASS_HYG` (S1/S2/S3/S4) i `ROOM_FIRE_ZONE` (A lub B) | rozszerzony schemat bloku |
| — | Dodać rysunek schematu stref pożarowych jako osobny layout A-106 | — |

Nowe layouty:

| Arkusz | Skala | Zawartość |
|---|---|---|
| **A-106** | 1:200 | Schemat stref pożarowych (FZ-A, FZ-B), kierunki ewakuacji strzałkami, długości dojść |
| **A-107** | 1:200 | Schemat stref higienicznych S1/S2/S3/S4 (kolor-coded) |
| **A-108** | 1:500 | Plan sytuacyjny z otoczeniem (działka, ambulance bay, dziedziniec G) |

### 14.1 Warstwy dopisane vs § 3

| Warstwa | Kolor | Zastosowanie |
|---|---:|---|
| `A-WALL-FIRE` | 11 | ściana oddzielenia ppoż. REI 60 |
| `A-WALL-STRUCT` | 92 | konstrukcja nośna — słupy, rygle |
| `A-DOOR-FIRE` | 1 | drzwi ppoż. EI 60 |
| `A-AREA-FIRE` | 31 | obrys strefy pożarowej (HIDDEN, grubo) |
| `A-AREA-HYGN-S1` | 11 | obszar klasy higienicznej S1 (OR) |
| `A-AREA-HYGN-S2` | 41 | obszar S2 (PACU, preOp, izolatki standardowe) |
| `A-AREA-HYGN-S3` | 1 | obszar S3 (AIIR) |
| `A-AREA-HYGN-S4` | 8 | obszar S4 (reszta medical) |
| `A-AREA-RAD-CTRL` | 2 | strefa kontrolowana radiologiczna |
| `A-AREA-RAD-OVER` | 3 | strefa nadzorowana radiologiczna |
| `A-AREA-MR-4GS` | 1 | strefa 4 Gauss MR |
| `A-ANNO-SYMB-EGRS` | 7 | znaki ewakuacyjne |
| `E-LITE-EMER` | 1 | oświetlenie awaryjne (już było, potwierdzenie) |

Łącznie warstw po korektach: **~48** (z oryginalnych 35).

---

## 15. Odniesienie punkt-po-punkcie do audytu `[REDACTED-REFERENCE-DWG]`

Audyt poprzedniego pliku stwierdził 11 wad. Macierz tego, jak adresujemy każdą:

| Wada w pliku ref. | Rozwiązanie w Hospital-2026 |
|---|---|
| Imperial inches, INSUNITS=1 | Metric mm, INSUNITS=4; weryfikacja w Fazie 0 |
| 83 % encji to prymitywne Line | Ściany jako zamknięte POLYLINE, fills jako HATCH, cel: Line < 15 % |
| 14 591× 2D SOLID zamiast Hatch | ANSI31 / AR-CONC / AR-BRELM / AR-SAND dla wszystkich fillów |
| 1 020× 3DFace (legacy) | Brak 3DFace; w 3D używamy EXTRUDE/PRESSPULL (nie dotyczy parteru-2D) |
| 17 warstw — "0", "8", "WALL 1 300" bez znaczenia | ~48 warstw wg AIA CAD Layer Guidelines 2nd Ed. + ISO 13567 |
| Aktywna warstwa na koniec: `0` | Aktywna: `A-WALL-INT` (weryfikacja acceptance-criteria §10) |
| 2 text styles, 1 dim style | 6 text styles (annotative), 3 dim styles (1:100 / 1:50 / 1:20) |
| 11 bloków, 0 dynamic, nonsensy typu `02ETRX29` | ~22 bloki, ≥ 10 dynamic z atrybutami (DOOR-SINGLE/DOUBLE/SLIDE/FIRE, WIN-*, ROOM-TAG, CT/MR/OR-TABLE/HEADWALL) |
| Brak layoutu merytorycznego, tylko `Projects1200x1100` PublishToWeb PNG | 7 layoutów (A-101..108) z title blockiem A0 i viewportami per skala |
| Brak uzasadnienia funkcjonalnego | Każde z 115 pomieszczeń opisane programem (§ 2) + normą (§ 13) |
| Plik "z WhatsAppa", niezwiązany z projektem | Nowy `.dwg` w `C:\Users\DELL\Dev\autocad-mcp-projects\Hospital2026\Hospital2026_A0-001.dwg`, katalog pod kontrolą git (struktura zostanie założona w Fazie 0) |

---

> **Następny krok:** wykonanie Fazy 0 (setup) przez `acad_design_iterate`. Po niej — każda kolejna faza jako osobne wywołanie z własnym checkpointem. Wszystko w nowym pliku `Hospital2026_A0-001.dwg` (nie w cudzym [REDACTED-REFERENCE-DWG]).

> **Lista normatywnych zmian do wdrożenia przed Fazą 0 (ten dokument):**
> 1. Dopisanie warstw `A-WALL-FIRE`, `A-DOOR-FIRE`, `A-AREA-FIRE`, `A-AREA-HYGN-S1..S4`, `A-AREA-RAD-*`, `A-AREA-MR-4GS`, `A-ANNO-SYMB-EGRS` (łącznie +13).
> 2. Rozszerzenie `BLK-WC-ACCESSIBLE` do 2,5 × 2,2 m.
> 3. Dodanie `BLK-DOOR-FIRE-EI60` (drzwi ppoż. EI 60 z samozamykaczem, atrybut `FIRE_RATING`).
> 4. Dodanie pomieszczeń AIIR B-222..B-225 do programu.
> 5. Rozszerzenie B-205 do 52 m².
> 6. Korekta szerokości public corridor Strefy A do 2,8 m.
> 7. Korekta szerokości dirty corridor D-421 do 2,2 m (już wprowadzone w § 2).
> 8. Dodanie 3 nowych layoutów A-106..A-108.

