# Plan: Upgrade do poziomu biura architektonicznego (Phase D)

**Status:** PLAN — do akceptacji przed implementacją
**Data:** 2026-04-23
**Autor:** AutoCAD MCP Megasystem
**Cel:** Podnieść jakość generowanych rysunków z „parametrycznego MVP" (ściany + prostokąty pokoi + prostokąty-łóżka) do poziomu **rysunku wykonawczego biura architektonicznego** (referencja: wycinek planu dostarczony przez użytkownika 2026-04-23 — hotel/apartament z pełnym wyposażeniem, osiami Y1/Y3, klatkami schodowymi, łańcuchami wymiarowymi, sanitariatami, callout profili K1/K6/K10).

---

## 1. Executive summary

Obecny stan (Phase C — ukończony):
- Plan spełnia **12/12 na osi bezpieczeństwa** (WT 2022, MZ 2019, PN-EN 14644, osłony LEAD/Faraday).
- Geometria: 99 polilinii ścian + 61 drzwi + 36 łóżek + 53 etykiety pokoi.
- Wyraźnie **brakuje ~17 elementów typowych dla rysunku wykonawczego** (patrz §2).

Cel Phase D — **rysunek wykonawczy 1:50/1:100 na poziomie biura projektowego**:
- 7 nowych kategorii MCP (~48 nowych narzędzi), 3 rozszerzenia istniejących.
- 10 nowych zasad `docs/engineering-rules/` (architektoniczna wierność, lineweighty, sanitariaty, meble, schodowiska, osi strukturalne, hatching, callouts, schedule, schematy podłogowe).
- Nowa persona Vision `senior-architect-reviewer` (rubryka 17 kryteriów).
- Biblioteka ~80 bloków dynamicznych (meble szpitalne, sanitariaty, stolarka, schody, windy).
- Polityka plot-styles (`CTB`/`STB`) z 9-tier lineweightami.

Szacowany nakład: **~45-55 osobo-dni** (10 kategorii × 3-5 dni dev + testy + biblioteka bloków + dokumentacja + re-generacja planu szpitala).

---

## 2. Gap analysis — co ma profesjonalny plan, czego nam brakuje

Referencja: wycinek planu z materiału referencyjnego (zrzut ekranu profesjonalnego planu projektowego).

| # | Element rysunku wykonawczego | Biuro arch. (ref.) | Nasz Phase C | Gap |
|---|---|---|---|---|
| 1 | **Hatching ścian** (beton, cegła, izolacja, tynk, kamień) | ✓ per-layer per-REI | brak — tylko linie konturu | **CRITICAL** |
| 2 | **Meble w pokojach** (łóżka z pościelą, stoliki nocne, fotele, biurka, szafki) | ✓ bloki dynamiczne z wymiarem | tylko prostokąty „bed" | **CRITICAL** |
| 3 | **Sanitariaty** (WC, umywalka, wanna, kabina prysznica, bidet, lustro) | ✓ per PN-EN 997/31 | brak — WC oznaczone jedynie etykietą | **CRITICAL** |
| 4 | **Drzwi wykonawcze** (skrzydło + klamka + strefa swingu + numer + typ REI/EI + szerokość) | ✓ blok z atrybutami | tylko linia skrzydła + łuk | **MAJOR** |
| 5 | **Okna** (rama + szklenie + parapet wew./zew. + typ otwarcia + klasa RC) | ✓ blok z atrybutami | brak (brak tool do okien) | **CRITICAL** |
| 6 | **Schody / klatki schodowe** (stopnie z wymiarami, podest, poręcz, obudowa szybu) | ✓ bloki parametrische | brak | **MAJOR** |
| 7 | **Windy** (szyb + kabina + drzwi szybowe + pozycja licznika) | ✓ bloki | brak | **MAJOR** |
| 8 | **Siatka osiowa** (bubble-labels Y1/Y3/C/F + osi ciągłe + wymiar rozstawu 7200 mm) | ✓ na wszystkich planach | częściowo (tylko 2 grid bubbles) | **MAJOR** |
| 9 | **Łańcuchy wymiarowe** (main chain + sub chain + cumulative + tick marks 45°) | ✓ 3 poziomy | 1 poziom tylko | **MAJOR** |
| 10 | **Tabele stolarki** (drzwi/okien/pomieszczeń w paperspace) | ✓ | brak | **MAJOR** |
| 11 | **Callouts profili** (K1-słup, K6-profile elewacyjne, K10-stopnie) | ✓ leader + kod | brak | **MINOR** |
| 12 | **Linie przekrojów** (A-A, B-B z cut plane marker + strzałką) | ✓ | brak | **MAJOR** |
| 13 | **Polityka lineweight** (9 grubości: grube kontury zewn. → cienkie hatch) | ✓ CTB/STB | jedna grubość dla wszystkiego | **MAJOR** |
| 14 | **Oznaczenia materiałów wykończeniowych** (posadzki, sufit, ściana — legenda) | ✓ legenda | brak | **MINOR** |
| 15 | **Północ + pasek skali + kompas** | ✓ | brak — jedynie północ jako symbol | **MINOR** |
| 16 | **Reflected ceiling plan** (oświetlenie, diffusery HVAC, czujniki ppoż.) | Często osobny rysunek | brak | **MINOR** |
| 17 | **Szczegóły ościeża / progu / nadproża** | ✓ detal 1:10/1:20 | brak | **MINOR** |

**Podsumowanie:**
- 4× CRITICAL → bez nich plan NIE jest rysunkiem wykonawczym (gimnazjum/student I rok).
- 7× MAJOR → bez nich plan jest „koncepcyjny", nie nadaje się do przetargu / zgłoszenia pozwolenia.
- 6× MINOR → lukier, ale obowiązkowy dla rynku polskiego.

---

## 3. Mapping: obecny MCP vs wymagane możliwości

### Co już mamy (19 kategorii, 224 narzędzia)
- `Geometry2d`, `Geometry3d`, `Modify`, `Layers`, `Files`, `View`, `Selection`
- `Blocks` (12 tooli — insert, list, define, ale bez biblioteki)
- `Dimensions` (12 tooli — single-shot, bez chain/cumulative)
- `Annotations` (12 tooli — MText/MLeader, bez callout profili)
- `Architecture` (10 tooli — draw_room, draw_wall, ale bez schodów/windów/okien)
- `Validators`, `Parametric`, `Layouts`, `BooleanOps`
- Domeny: `Civil`, `Electrical`, `Mechanical`
- `Vision` (9 tooli — describe_image, review)

### Czego brakuje — 7 nowych kategorii + 3 rozszerzenia

| Kategoria | Typ | Liczba tooli | Priorytet |
|---|---|---|---|
| `acad-hatches` | NEW | 8 | **P0** (CRITICAL gap 1) |
| `acad-furniture` | NEW | 10 | **P0** (CRITICAL gap 2) |
| `acad-plumbing` | NEW | 8 | **P0** (CRITICAL gap 3) |
| `acad-openings` (drzwi-pro + okna) | NEW | 10 | **P0** (CRITICAL gap 5 + MAJOR 4) |
| `acad-verticals` (schody + windy + rampy) | NEW | 7 | **P1** (MAJOR gap 6+7) |
| `acad-grids` | NEW | 6 | **P1** (MAJOR gap 8) |
| `acad-schedules` | NEW | 5 | **P1** (MAJOR gap 10) |
| `acad-sections` | NEW | 4 | **P2** (MAJOR gap 12) |
| `acad-plotstyles` | NEW | 3 | **P2** (MAJOR gap 13) |
| `acad-callouts` | NEW | 4 | **P3** (MINOR gap 11,15,17) |
| **`Dimensions` extend** | EXTEND | +5 (chain, cumulative, baseline, tick-mark policy) | **P1** (MAJOR gap 9) |
| **`Blocks` extend** | EXTEND | +4 (library manifest, bulk-insert, swap-block, tag-atts) | **P0** |
| **`Architecture` extend** | EXTEND | +6 (draw_window, draw_stair, draw_elevator, draw_ramp, draw_ceiling_grid) | **P0/P1** |

**Razem: 7 nowych kategorii = 48 nowych narzędzi + 15 rozszerzeń = 63 nowych tooli (28% wzrostu).**

---

## 4. Szczegóły nowych kategorii

### 4.1 `acad-hatches` (P0)
Cel: tworzenie i zarządzanie hatches zgodnie z ISO 128 / PN-EN wzorami.

Narzędzia:
```
acad.hatches.draw_hatch(outline, pattern, scale, angle, layer, bgColor?)
acad.hatches.draw_hatch_by_boundary(point, pattern, scale, angle, layer)
acad.hatches.list_patterns()                         -> AR-CONC, AR-BRSTD, BATTING, EARTH, LINE, ANSI31...
acad.hatches.set_default_pattern_for_layer(layer, pattern, scale, angle)
acad.hatches.apply_material_preset(outline, material) -> "concrete"/"brick"/"insulation"/"plaster"/"stone"
acad.hatches.clip_hatch(handle, newOutline)
acad.hatches.delete_hatch(handle)
acad.hatches.regenerate_all(scope)                   -> hatches stają się niesynchroniczne po modyfikacji granicy
```

Technicznie: `Hatch` entity w AutoCAD Database (ObjectARX `Hatch` class).

### 4.2 `acad-furniture` (P0)
Cel: biblioteka dynamicznych bloków mebli szpitalnych, biurowych, mieszkaniowych, z atrybutami (numer inw., typ, wymiar).

Narzędzia:
```
acad.furniture.list_library(domain)                   -> "hospital"/"office"/"residential"
acad.furniture.insert_bed(position, orientation, type) -> "single"/"double"/"hospital"/"ICU"/"PACU"
acad.furniture.insert_chair(position, orientation, type)
acad.furniture.insert_desk(position, orientation, size)
acad.furniture.insert_cabinet(position, orientation, size, type)
acad.furniture.insert_sofa(position, orientation, seats)
acad.furniture.insert_table(position, orientation, shape, size)
acad.furniture.insert_equipment(position, type)       -> "ecg"/"defibrillator"/"ventilator"/"xray-c-arm"...
acad.furniture.set_attributes(handle, inv_id?, type?, note?)
acad.furniture.populate_room(room_handle, preset)    -> "single-bed-ward"/"office-workstation"/"OR-primary"
```

Biblioteka bloków: `assets/blocks/furniture/` (DWG per blok, insert via `WBLOCK REF`).

**Minimum 80 bloków dla szpitala** (lista w §6).

### 4.3 `acad-plumbing` (P0)
Cel: sanitariaty zgodne z PN-EN 997 (WC), PN-EN 31 (umywalki), PN-EN 232 (wanny), PN-EN 251 (kabiny).

Narzędzia:
```
acad.plumbing.insert_toilet(position, orientation, type)      -> "standard"/"wall-hung"/"disabled-pn-en-17210"
acad.plumbing.insert_sink(position, orientation, width)
acad.plumbing.insert_bathtub(position, orientation, length)
acad.plumbing.insert_shower(position, orientation, size, type) -> "walk-in"/"tray"/"disabled"
acad.plumbing.insert_bidet(position, orientation)
acad.plumbing.insert_urinal(position, orientation)
acad.plumbing.insert_medical_sink(position, type)            -> "scrub"/"flushing"/"hair-wash"
acad.plumbing.populate_bathroom(room_handle, preset)         -> "wc-standard"/"wc-disabled"/"ensuite"/"scrub-room"
```

Bloki: `assets/blocks/plumbing/`.

### 4.4 `acad-openings` (P0)
Cel: drzwi wykonawcze + okna z atrybutami, zastępują surowe `draw_line` + `draw_arc` w ścianie.

Narzędzia:
```
acad.openings.draw_door(wall_handle, offset, width, swing_side, hinge_side, type, rei, number?)
  - type = "single"/"double"/"slider"/"fold"/"rotating"
  - rei  = "EI30"/"EI60"/"EI120"/"REI120"/"none"
  - automatycznie wycina ścianę, rysuje ościeżnicę, skrzydło, klamkę, strefę swingu

acad.openings.draw_window(wall_handle, offset, width, height, sill_height, type, panes, rc?)
  - type = "single"/"double"/"tilt-turn"/"fixed"/"skylight"
  - rc   = "RC2"/"RC3"/"RC4" (PN-EN 1627)

acad.openings.list_doors(layer?)                  -> tabelka handles + width + type + REI + number
acad.openings.list_windows(layer?)
acad.openings.set_door_number(handle, number)     -> auto-numeracja D-001, D-002 per kondygnacja
acad.openings.set_window_number(handle, number)
acad.openings.renumber_all(prefix, start)
acad.openings.export_door_schedule(outputPath)    -> CSV dla acad-schedules
acad.openings.export_window_schedule(outputPath)
acad.openings.swap_door_type(handle, newType)
acad.openings.flip_hinge(handle)                  -> flip lewy/prawy
```

Ten tool usuwa dotychczasowy anty-wzorzec „rysuj drzwi jako line + arc bez wycinania ściany".

### 4.5 `acad-verticals` (P1)
Cel: klatki schodowe, schody kręte, rampy, windy.

Narzędzia:
```
acad.verticals.draw_straight_stair(start, direction, flight_count, step_count, step_depth, step_width, landing_depth?)
acad.verticals.draw_spiral_stair(center, outer_radius, inner_radius, step_count, rotation_deg, direction)
acad.verticals.draw_ramp(start, end, width, slope_pct, surface_type)
acad.verticals.draw_elevator_shaft(position, size, type, capacity)   -> "passenger"/"bed"/"goods"/"car-lift"
acad.verticals.draw_escalator(position, direction, length, width)
acad.verticals.draw_platform_lift(position, size, type)
acad.verticals.add_handrail(stair_handle, side, type)                -> "both"/"left"/"right", "round"/"flat"/"pn-en-13374"
```

Dla szpitala: wymagana winda dla łóżek (160 cm × 260 cm, nośność 1600 kg wg WT §54).

### 4.6 `acad-grids` (P1)
Cel: siatka osi strukturalnych z bubble-labels.

Narzędzia:
```
acad.grids.draw_grid(origin, x_spacings, y_spacings, bubble_style?) -> auto-label A,B,C... i 1,2,3...
acad.grids.add_axis(direction, position, label)
acad.grids.remove_axis(label)
acad.grids.rename_axis(old, new)
acad.grids.list_grid()                 -> axes + spacings + origin
acad.grids.snap_to_grid(handle, tolerance)
```

Styl bubble: kółko D=8 mm (w skali 1:100 = 800 mm w model space), tekst 5 mm = 500 mm.

### 4.7 `acad-schedules` (P1)
Cel: parametryczne tabele (drzwi/okien/pomieszczeń/materiałów) w paperspace.

Narzędzia:
```
acad.schedules.generate_door_schedule(layout_name, position, columns?)
acad.schedules.generate_window_schedule(layout_name, position, columns?)
acad.schedules.generate_room_schedule(layout_name, position, columns?)
  - columns: ["number", "name", "area", "floor_finish", "wall_finish", "ceiling_finish", "height"]
acad.schedules.generate_finish_legend(layout_name, position)
acad.schedules.update_schedule(handle)   -> re-run query z aktualnych danych
```

Technicznie: AutoCAD `Table` entity z `TableStyle` + kolumny-formuły / cell contents.

### 4.8 `acad-sections` (P2)
Cel: linie przekrojowe A-A, B-B z cut plane marker + strzałką kierunkową.

Narzędzia:
```
acad.sections.draw_section_line(start, end, label, view_direction, depth_mm?)
acad.sections.draw_section_marker(position, label, orientation)
acad.sections.list_sections()
acad.sections.set_section_view(label, target_layout)
```

### 4.9 `acad-plotstyles` (P2)
Cel: zarządzanie CTB/STB per-layer lineweighty i kolory do wydruku.

Narzędzia:
```
acad.plotstyles.apply_ctb(ctb_name)                   -> "AIA 2017"/"PN-B-01025"/"Hospital-ISO"
acad.plotstyles.set_layer_policy(layer, lineweight_mm, plot_color, screen_pct?, plot?)
acad.plotstyles.export_ctb(outputPath)
```

Polityka 9-tier lineweight (patrz `61-lineweight-policy.md` niżej).

### 4.10 `acad-callouts` (P3)
Cel: leader lines z kodem profilu (K1, K6, K10) + paski skali + strzałka północy.

Narzędzia:
```
acad.callouts.insert_profile_callout(position, profile_code, description?)
acad.callouts.insert_north_arrow(position, rotation_deg, style)   -> "simple"/"compass"/"pn-en-iso-129"
acad.callouts.insert_scale_bar(position, scale, units, segments)
acad.callouts.insert_finish_callout(position, floor_code, wall_code, ceiling_code)
```

---

## 5. Rozszerzenia istniejących kategorii

### 5.1 `Architecture` extend (+6 tooli)
```
acad.architecture.draw_window(...)                 -> duplikat z openings, ale wygodny shortcut
acad.architecture.draw_stair(...)                  -> duplikat z verticals
acad.architecture.draw_ceiling_grid(room, grid_size, pattern, fixture_positions?)
acad.architecture.set_wall_material(handle, material, rei?)
acad.architecture.split_wall_at_opening(wall_handle, offset, width)   -> już używane ręcznie w Phase C, trzeba sformalizować
acad.architecture.offset_wall_with_insulation(wall_handle, thickness_out, thickness_in, insulation_thickness)
```

### 5.2 `Blocks` extend (+4 toole)
```
acad.blocks.library_register(manifest_path)        -> wskazuje assets/blocks/<domain>/manifest.json
acad.blocks.library_list(domain)
acad.blocks.bulk_insert(block_name, positions[], orientations[], scale?)
acad.blocks.swap_block(handle, newBlockName, preserveAtts?)
```

### 5.3 `Dimensions` extend (+5 tooli)
```
acad.dimensions.draw_chain_dimension(points[], side, offset, textHeight, tickStyle)  -> main chain + sub chain w jednej akcji
acad.dimensions.draw_cumulative_dimension(basePoint, points[], side, offset)
acad.dimensions.draw_baseline_dimension(basePoint, points[], side, offset)
acad.dimensions.set_tick_policy(style)  -> "45deg"/"dot"/"arrow" per PN-EN-ISO-129
acad.dimensions.auto_dim_walls(layer, offset, levels)  -> automatyczne wymiarowanie wszystkich ścian zewnętrznych na 3 poziomach (cumulative + sub + main)
```

---

## 6. Biblioteka bloków — minimalna lista (~80 bloków)

Struktura folderu:
```
assets/blocks/
  furniture-hospital/
    bed-standard.dwg, bed-hospital.dwg, bed-icu.dwg, bed-pacu.dwg, bed-pediatric.dwg,
    chair-patient.dwg, chair-visitor.dwg, chair-wheelchair.dwg,
    cabinet-medication.dwg, cabinet-linen.dwg, cabinet-instrument.dwg,
    cart-crash.dwg, cart-medication.dwg, cart-linen.dwg,
    desk-nurse.dwg, desk-doctor.dwg, desk-reception.dwg,
    examination-table.dwg, operating-table.dwg, delivery-bed.dwg,
    ... (30+)
  furniture-office/
    desk-single.dwg, desk-double.dwg, desk-L.dwg,
    chair-office.dwg, chair-executive.dwg, chair-conference.dwg,
    cabinet-file.dwg, cabinet-shelves.dwg,
    table-meeting-4.dwg, table-meeting-8.dwg, table-meeting-12.dwg,
    sofa-2.dwg, sofa-3.dwg, armchair.dwg,
    ... (15+)
  plumbing/
    wc-standard.dwg, wc-wall-hung.dwg, wc-disabled-pn-en-17210.dwg,
    sink-standard.dwg, sink-wash-hand.dwg, sink-disabled.dwg,
    bathtub-standard.dwg, bathtub-disabled.dwg,
    shower-walk-in.dwg, shower-tray.dwg, shower-disabled.dwg,
    bidet.dwg, urinal.dwg,
    medical-sink-scrub.dwg, medical-sink-flushing.dwg,
    ... (20+)
  openings/
    door-single-D90-REI60.dwg, door-double-D180-REI60.dwg, door-slider-S120.dwg,
    window-fixed-W120-H150.dwg, window-tilt-turn-W120-H150.dwg,
    ... (10+)
  verticals/
    stair-straight.dwg (parametric), stair-spiral.dwg,
    elevator-passenger-110x140.dwg, elevator-bed-160x260.dwg, elevator-goods-200x300.dwg,
    ramp-handicap.dwg,
    ... (8+)
  symbols/
    north-arrow-iso.dwg, north-arrow-compass.dwg,
    scale-bar-100.dwg, scale-bar-50.dwg, scale-bar-20.dwg,
    grid-bubble-alpha.dwg, grid-bubble-numeric.dwg,
    callout-profile.dwg, callout-finish.dwg,
    section-marker.dwg, elevation-marker.dwg, detail-marker.dwg,
    ... (12+)
```

**Manifest per folder:** `manifest.json` z listą `{ name, path, attributes[], anchor, preview_png }`.

---

## 7. Nowe zasady (docs/engineering-rules/)

Wszystkie pliki tworzone zgodnie z rule `53-rules-update-mandate.md`.

| # | Plik | `alwaysApply` | Zakres | Krótki opis |
|---|---|---|---|---|
| 60 | `60-architectural-fidelity.md` | true | cały projekt | Minimum detail per room-type (sypialnia → min. łóżko + stolik + szafa + światło; WC → min. WC + umywalka) |
| 61 | `61-lineweight-policy.md` | true | cały projekt | 9-tier lineweights (0.05 → 1.4 mm) + mapping layer→lw→pen |
| 62 | `62-hatching-policy.md` | true | cały projekt | Per-layer default hatch (A-WALL-EXT → AR-CONC @ scale=100, A-WALL-INT → AR-BRSTD, A-WALL-LEAD → ANSI38 + red...) |
| 63 | `63-sanitary-fixtures-wt.md` | true | Architecture/Plumbing | Min. wyposażenie WC / łazienki per WT §78 + PN-EN 17210 (dostępność) |
| 64 | `64-furniture-density-per-room.md` | true | Furniture | Min. meble per funkcja (sala OR → stół operacyjny + lampa + anestezjolog + stacja robocza) |
| 65 | `65-door-window-schedule.md` | true | Openings/Schedules | Każde drzwi/okno MUSI mieć numer + typ + REI + szerokość (atrybuty bloku) |
| 66 | `66-dimension-chains.md` | true | Dimensions | Min. 3 poziomy wymiarów (main/sub/cumulative), tick-marks 45° per PN-EN-ISO-129 |
| 67 | `67-grid-axes.md` | true | Grids/Architecture | Wszystkie plany MUSZĄ mieć siatkę osi z bubble-labels |
| 68 | `68-plan-symbols-standard.md` | true | Callouts | Każdy rysunek: północ + pasek skali + ramka tytułowa per PN-B-01025 |
| 69 | `69-callouts-leaders.md` | false (opt-in per projekt) | Callouts | Konwencja K1/K6/K10 i system elevacji callouts |

Szablon pliku `60-architectural-fidelity.md` (skelet):
```markdown
---
description: Minimalny poziom szczegółu architektonicznego per typ pomieszczenia. Bez tego plan jest "studencki".
alwaysApply: true
---

# Minimum architectural detail per room type

Plan NA POZIOMIE wykonawczym MUSI zawierać dla każdego pomieszczenia:
- Ściany z hatchingiem (per `62-hatching-policy.md`)
- Wszystkie otwory drzwiowe i okienne z `acad-openings` (NIE raw line+arc)
- Meble per tabela poniżej
- Etykietę z numerem + nazwą + powierzchnią (auto z `acad-schedules.generate_room_schedule`)

## Tabela minimum mebli per typ pomieszczenia

| Typ | Minimum wymagane bloki |
|---|---|
| Sypialnia 1-os | łóżko + stolik nocny + szafa + biurko + krzesło |
| Sypialnia szpitalna 1-os | łóżko szpitalne + headwall + szafa pacjenta + fotel dla odwiedzającego + stolik |
| WC standard | muszla + umywalka + lustro + kosz + papiernica |
| WC niepełnosprawny | muszla wall-hung + umywalka dostępna + poręcze + alarm + lustro nachylne (PN-EN 17210) |
| Sala OR | stół operacyjny + lampa + wózek anestezjologiczny + stacja robocza + pompki IV + lampa backup |
| SOR Box | łóżko transportowe + defibrylator + ssak + monitor + krzesło lekarza + stolik |
...
```

---

## 8. Nowa persona Vision — `senior-architect-reviewer`

Lokalizacja: `src/AcadMcp.Vision/personas/senior-architect-reviewer.yaml`.

System prompt (skelet):
```yaml
name: senior-architect-reviewer
model: claude-4.5-sonnet-thinking
max_tokens: 8000
system: |
  Jesteś starszym architektem z 20-letnim doświadczeniem w projektowaniu obiektów
  użyteczności publicznej (szpitale, szkoły, biurowce) na rynku polskim. Oceniasz
  rysunki wykonawcze zgodnie z:
  - Ustawa Prawo budowlane (t.j. Dz.U.2024 poz. 725)
  - WT: Dz.U.2022 poz. 1225 (WT dla budynków)
  - Rozp. MZ ws. szczegółowych wymagań ws. szpitali (Dz.U.2019 poz. 595)
  - PN-B-01025 (oznaczenia graficzne na rysunkach architektoniczno-budowlanych)
  - PN-EN-ISO-129 (oznaczenia wymiarów)
  - PN-EN-ISO-5457 (formaty rysunków)
  - PN-EN-ISO-128 (rodzaje linii)
  - AIA CAD Layer Guidelines (layering)

  Wystawiasz ocenę według 17-punktowej rubryki (patrz poniżej) i ZAWSZE zwracasz JSON:
  { "score": <int 0-17>, "grade": "<A+/A/.../F>", "findings": [{"id":"...","severity":"critical/major/minor","description":"...","location":"..."}], "overall":"..." }

rubric:
  - id: hatches
    weight: critical
    question: "Czy ściany mają poprawne hatches (beton/cegła/izolacja) i czy są widoczne w skali 1:100?"
  - id: furniture
    weight: critical
    question: "Czy pomieszczenia mają minimum mebli wymagane per typ?"
  - id: plumbing
    weight: critical
    question: "Czy sanitariaty są narysowane pełno (WC + umywalka + etc.) a nie jako etykieta?"
  - id: openings
    weight: major
    question: "Czy drzwi mają klamkę, kierunek swingu i numer; okna mają szklenie?"
  - id: verticals
    weight: major
    question: "Czy schody i windy są narysowane z wymiarami stopni i kabiny?"
  - id: grids
    weight: major
    question: "Czy jest siatka osi strukturalnych z bubble-labels i wymiarem rozstawu?"
  - id: dimensions
    weight: major
    question: "Czy są min. 3 poziomy wymiarów (main/sub/cumulative) z tick-marks 45°?"
  - id: schedules
    weight: major
    question: "Czy w paperspace są tabele drzwi/okien/pomieszczeń?"
  - id: sections
    weight: major
    question: "Czy są linie przekrojowe A-A/B-B?"
  - id: lineweights
    weight: major
    question: "Czy lineweighty są zróżnicowane per-layer (grube kontury, cienkie hatch)?"
  - id: callouts
    weight: minor
    question: "Czy są callouts profili (K1/K10) i wykończeniowe?"
  - id: symbols
    weight: minor
    question: "Czy jest strzałka północy + pasek skali + ramka tytułowa?"
  - id: legend
    weight: minor
    question: "Czy jest legenda materiałów/warstw/symboli?"
  - id: reflected_ceiling
    weight: minor
    question: "Czy jest odzwierciedlenie sufitu z oświetleniem (może być osobny rysunek)?"
  - id: details
    weight: minor
    question: "Czy są detale ościeża/progu/nadproża w skali 1:10-1:20?"
  - id: compliance
    weight: critical
    question: "Czy oznaczenia są per PN-B-01025? Czy zgodne z WT + MZ?"
  - id: readability
    weight: critical
    question: "Czy napisy są czytelne w skali 1:100 bez nakładek?"
```

---

## 9. Phase D — plan wykonania (12 kroków)

```
D0  Backup + branch feature/phase-D-architectural-upgrade
D1  Scaffold 7 nowych kategorii (scripts/new-category.ps1 × 7)
D2  Zaimplementować acad-hatches (P0 — 8 tooli)
D3  Zaimplementować acad-furniture (P0 — 10 tooli) + biblioteka bloków hospital/office (50+ bloków)
D4  Zaimplementować acad-plumbing (P0 — 8 tooli) + biblioteka plumbing (20+ bloków)
D5  Zaimplementować acad-openings (P0 — 10 tooli) + biblioteka openings (10+ bloków)
D6  Rozszerzyć Architecture (+6 tooli), Dimensions (+5 tooli), Blocks (+4 tooli)
D7  Zaimplementować acad-verticals, acad-grids (P1 — 13 tooli) + biblioteka (15+ bloków)
D8  Zaimplementować acad-schedules (P1 — 5 tooli) + TableStyle "HOSPITAL-DEF" + "OFFICE-DEF"
D9  Zaimplementować acad-sections, acad-plotstyles, acad-callouts (P2-P3 — 11 tooli) + biblioteka symbols
D10 Napisać 10 nowych rules `.md` + uzupełnić istniejące (62 hatching, 26 API traps)
D11 Dodać persona senior-architect-reviewer + endpoint w Vision sidecar + manifest refresh
D12 Re-generacja planu szpitala z Phase C checkpointu:
     - ckpt-phaseD-start
     - apply hatches per 62
     - populate_room dla 53 pokoi (furniture + plumbing)
     - swap 61 "raw" drzwi na acad-openings (preserve numery)
     - grid + dimensions chains (3 levels)
     - schedules (drzwi 61, okna TBD, pomieszczenia 53)
     - 2 sections (A-A przez SOR-OR-MR, B-B przez oddział łóżkowy)
     - lineweight policy + plotstyle HOSPITAL-ISO.ctb
     - callouts profili + symbols
     - senior-architect-reviewer pass × 3 (overview + 6 tiles + details)
     - final DWG → Hospital2026_PRO_A0-001.dwg
     - final PDF multi-page (20 stron: 1 cover + 1 overview + 8 zone zooms + 3 schedules + 2 sections + 2 RCP + 3 detali)
```

**Gate Phase D → accept:** senior-architect-reviewer zwraca `score ≥ 15/17` na overview + `score ≥ 13/17` na każdym zoom tile (mean). Brak critical findings.

---

## 10. Rubryka 12/10 — finalna definicja

| Oś | Max | Waga | Co liczy |
|---|---|---|---|
| Bezpieczeństwo (Phase C) | 12 | 40% | Już PASS (12/12) |
| Wierność architektoniczna (Phase D) | 17 | 40% | Rubryka `senior-architect-reviewer` |
| Compliance prawne | 10 | 15% | WT + MZ + PN-B + Prawo budowlane |
| Czystość dokumentacji | 5 | 5% | CHANGELOG, docs, rules, testy |

**Total: 44 pkt max. Target 12/10 = ≥ 92% = 41 pkt.**

Obecnie: 12 (safety) + 4 (arch fid szacowany) + 9 (compliance) + 5 (docs) = **30/44 = 68% = 8.2/10**.

Po Phase D: target 12 + 16 + 10 + 5 = **43/44 = 97.7% = 11.7/10**.

Pozostały +0.3 do „12/10" = renderowanie 3D + vizualizacja elewacji + BIM-export (poza scope Phase D → Phase E).

---

## 11. Estymacja nakładu

| Krok | Dni dev | Dni QA | Dni bloków |
|---|---|---|---|
| D1 scaffold | 0.5 | 0 | 0 |
| D2 hatches | 2 | 0.5 | 0 |
| D3 furniture + bloki hospital | 3 | 1 | **5** |
| D4 plumbing + bloki | 2 | 0.5 | **2** |
| D5 openings + bloki | 4 | 1 | **1.5** |
| D6 extensions (Arch, Dim, Blocks) | 3 | 1 | 0 |
| D7 verticals + grids | 3 | 1 | **2** |
| D8 schedules | 2 | 0.5 | 0 |
| D9 sections + plotstyles + callouts | 3 | 1 | **1** |
| D10 rules | 1.5 | 0 | 0 |
| D11 vision persona | 1 | 0.5 | 0 |
| D12 regeneracja planu szpitala | 2 | 2 | 0 |
| **Razem** | **27** | **9** | **11.5** |

**≈ 47.5 osobo-dni** (solo dev). Przy 2-osobowym zespole = ~25 dni kalendarzowych.

---

## 12. Ryzyka + strategia rollback

| Ryzyko | Prawdopod. | Wpływ | Mitygacja |
|---|---|---|---|
| AutoCAD API `Hatch` wiązanie z boundary breaks po edycji | Wysokie | Major | Tool `regenerate_all(scope)` + test e2e |
| Biblioteka bloków rośnie do GB | Średnie | Minor | Oddzielne repo `autocad-mcp-blocks` + sparse checkout |
| Manifesty ToolBank rozjeżdżają się | Wysokie | Major | `check-manifests.ps1` w CI + `CheckManifestSync` MSBuild target już wymusza |
| Per-layer lineweight łamie istniejące rysunki | Średnie | Major | Migration script `assets/migrate-lineweights.ps1` + flag `respect_existing_lw` |
| Persona arch-reviewer halucynuje | Średnie | Major | Determined rubric + JSON schema validation + 3× pass + majority vote |
| Phase C safety regression podczas D12 regenerating | Niskie | CRITICAL | Checkpoint przed D12 + post-D12 `check_overlaps` × 5 scans → abort if regresja |

**Rollback plan:** każde D-krok ma checkpoint (`ckpt-phaseD-<step>`). Gate na końcu każdego kroku — jeśli QA failed, `acad.checkpoint.restore` + RCA.

---

## 13. Referencje

- `docs/HOSPITAL-2026-REVIEW-FINDINGS.md` §4e (Phase C finish)
- `docs/engineering-rules/00-architecture-invariants.md` (invarianty)
- `docs/engineering-rules/41-new-category-flow.md` (jak dodawać kategorie)
- `docs/engineering-rules/53-rules-update-mandate.md` (jak dodawać rules)
- AIA CAD Layer Guidelines 2017
- PN-B-01025:2004
- PN-EN-ISO 129, 128, 5457
- Polska WT: Dz.U.2022 poz. 1225
- MZ Szpitale: Dz.U.2019 poz. 595

---

**Next action (po akceptacji):** użytkownik mówi "green light Phase D" → todo-driven wykonanie D0-D12.
