# Plan: reaching architectural-practice quality (Phase D)

**Status:** PLAN — written for approval before implementation
**Date:** 2026-04-23
**Author:** ToolBank AutoCAD
**Goal:** raise generated drawings from a "parametric MVP" (walls + room rectangles + bed
rectangles) to the level of a **construction drawing produced by an architectural practice**
(reference: a plan excerpt supplied by the user on 2026-04-23 — a hotel/apartment plan with
full furnishing, Y1/Y3 axes, stair cores, dimension chains, sanitary fittings and K1/K6/K10
profile callouts).

> **This plan has since been carried out.** Every category proposed below — `acad-hatches`,
> `acad-furniture`, `acad-plumbing`, `acad-openings`, `acad-verticals`, `acad-grids`,
> `acad-schedules`, `acad-sections`, `acad-plotstyles` and `acad-callouts` — now exists in
> `toolbank-manifests/`, and the bank has grown to **692 tools across 51 categories**. The
> document is kept as the reasoning behind that work, with its original figures intact; for
> what exists today see [`TOOLS-REFERENCE.md`](TOOLS-REFERENCE.md) and
> [`COVERAGE-ROADMAP.md`](COVERAGE-ROADMAP.md).

---

## 1. Executive summary

Where we are (Phase C — complete):
- The plan scores **12/12 on the safety axis** (WT 2022, MZ 2019, PN-EN 14644, LEAD/Faraday
  shielding).
- Geometry: 99 wall polylines + 61 doors + 36 beds + 53 room labels.
- It is clearly **missing ~17 elements typical of a construction drawing** (see §2).

The goal of Phase D — **a 1:50/1:100 construction drawing at practice level**:
- 7 new MCP categories (~48 new tools) and 3 extensions to existing ones.
- 10 new rules in `docs/engineering-rules/` (architectural fidelity, lineweights, sanitary
  fittings, furniture, stairs, structural axes, hatching, callouts, schedules, floor-finish
  schemes).
- A new Vision persona, `senior-architect-reviewer` (a 17-criterion rubric).
- A library of ~80 dynamic blocks (hospital furniture, sanitary fittings, doors and windows,
  stairs, lifts).
- A plot-style policy (`CTB`/`STB`) with 9-tier lineweights.

Estimated effort: **~45–55 person-days** (10 categories × 3–5 days of development, plus tests,
the block library, documentation, and regenerating the hospital plan).

---

## 2. Gap analysis — what a professional plan has and we do not

Reference: a plan excerpt from the reference material (a screenshot of a professional design
drawing).

| # | Construction-drawing element | Practice (ref.) | Our Phase C | Gap |
|---|---|---|---|---|
| 1 | **Wall hatching** (concrete, brick, insulation, plaster, stone) | ✓ per layer, per REI | missing — outlines only | **CRITICAL** |
| 2 | **Room furniture** (beds with bedding, nightstands, armchairs, desks, cabinets) | ✓ dynamic blocks, dimensioned | plain "bed" rectangles only | **CRITICAL** |
| 3 | **Sanitary fittings** (WC, basin, bath, shower enclosure, bidet, mirror) | ✓ per PN-EN 997/31 | missing — a WC is only a label | **CRITICAL** |
| 4 | **Construction-grade doors** (leaf + handle + swing zone + number + REI/EI type + width) | ✓ block with attributes | leaf line and arc only | **MAJOR** |
| 5 | **Windows** (frame + glazing + inner/outer sill + opening type + RC class) | ✓ block with attributes | missing — no window tool | **CRITICAL** |
| 6 | **Stairs / stair cores** (dimensioned treads, landing, handrail, shaft enclosure) | ✓ parametric blocks | missing | **MAJOR** |
| 7 | **Lifts** (shaft + car + landing doors + counter position) | ✓ blocks | missing | **MAJOR** |
| 8 | **Structural grid** (Y1/Y3/C/F bubble labels + continuous axes + 7200 mm spacing dimension) | ✓ on every plan | partial (2 grid bubbles) | **MAJOR** |
| 9 | **Dimension chains** (main chain + sub chain + cumulative + 45° tick marks) | ✓ 3 levels | 1 level only | **MAJOR** |
| 10 | **Door/window/room schedules** in paper space | ✓ | missing | **MAJOR** |
| 11 | **Profile callouts** (K1 column, K6 façade profiles, K10 treads) | ✓ leader + code | missing | **MINOR** |
| 12 | **Section lines** (A-A, B-B with cut-plane marker and direction arrow) | ✓ | missing | **MAJOR** |
| 13 | **Lineweight policy** (9 weights: heavy external outlines → fine hatching) | ✓ CTB/STB | one weight for everything | **MAJOR** |
| 14 | **Finish annotations** (floor, ceiling, wall — with a legend) | ✓ legend | missing | **MINOR** |
| 15 | **North arrow + scale bar + compass** | ✓ | missing — north as a symbol only | **MINOR** |
| 16 | **Reflected ceiling plan** (lighting, HVAC diffusers, fire detectors) | often a separate drawing | missing | **MINOR** |
| 17 | **Reveal / threshold / lintel details** | ✓ 1:10–1:20 detail | missing | **MINOR** |

**Summary:**
- 4× CRITICAL → without them the plan is not a construction drawing at all.
- 7× MAJOR → without them the plan stays conceptual and cannot go to tender or a permit
  application.
- 6× MINOR → polish, but expected on the Polish market.

---

## 3. Mapping: what the MCP has today versus what is required

### What already exists (19 categories, 224 tools)
- `Geometry2d`, `Geometry3d`, `Modify`, `Layers`, `Files`, `View`, `Selection`
- `Blocks` (12 tools — insert, list, define, but no library)
- `Dimensions` (12 tools — single-shot, no chains or cumulative)
- `Annotations` (12 tools — MText/MLeader, no profile callouts)
- `Architecture` (10 tools — draw_room, draw_wall, but no stairs, lifts or windows)
- `Validators`, `Parametric`, `Layouts`, `BooleanOps`
- Domains: `Civil`, `Electrical`, `Mechanical`
- `Vision` (9 tools — describe_image, review)

### What is missing — 7 new categories plus 3 extensions

| Category | Kind | Tools | Priority |
|---|---|---|---|
| `acad-hatches` | NEW | 8 | **P0** (CRITICAL gap 1) |
| `acad-furniture` | NEW | 10 | **P0** (CRITICAL gap 2) |
| `acad-plumbing` | NEW | 8 | **P0** (CRITICAL gap 3) |
| `acad-openings` (pro doors + windows) | NEW | 10 | **P0** (CRITICAL gap 5 + MAJOR 4) |
| `acad-verticals` (stairs + lifts + ramps) | NEW | 7 | **P1** (MAJOR gaps 6, 7) |
| `acad-grids` | NEW | 6 | **P1** (MAJOR gap 8) |
| `acad-schedules` | NEW | 5 | **P1** (MAJOR gap 10) |
| `acad-sections` | NEW | 4 | **P2** (MAJOR gap 12) |
| `acad-plotstyles` | NEW | 3 | **P2** (MAJOR gap 13) |
| `acad-callouts` | NEW | 4 | **P3** (MINOR gaps 11, 15, 17) |
| **`Dimensions` extend** | EXTEND | +5 (chain, cumulative, baseline, tick-mark policy) | **P1** (MAJOR gap 9) |
| **`Blocks` extend** | EXTEND | +4 (library manifest, bulk-insert, swap-block, tag-atts) | **P0** |
| **`Architecture` extend** | EXTEND | +6 (draw_window, draw_stair, draw_elevator, draw_ramp, draw_ceiling_grid) | **P0/P1** |

**Total: 7 new categories = 48 new tools + 15 extension tools = 63 new tools (a 28% increase).**

---

## 4. The new categories in detail

### 4.1 `acad-hatches` (P0)
Purpose: create and manage hatches following ISO 128 / PN-EN patterns.

Tools:
```
acad.hatches.draw_hatch(outline, pattern, scale, angle, layer, bgColor?)
acad.hatches.draw_hatch_by_boundary(point, pattern, scale, angle, layer)
acad.hatches.list_patterns()                         -> AR-CONC, AR-BRSTD, BATTING, EARTH, LINE, ANSI31...
acad.hatches.set_default_pattern_for_layer(layer, pattern, scale, angle)
acad.hatches.apply_material_preset(outline, material) -> "concrete"/"brick"/"insulation"/"plaster"/"stone"
acad.hatches.clip_hatch(handle, newOutline)
acad.hatches.delete_hatch(handle)
acad.hatches.regenerate_all(scope)                   -> hatches fall out of sync once their boundary is edited
```

Technically: the `Hatch` entity in the AutoCAD database (the ObjectARX `Hatch` class).

### 4.2 `acad-furniture` (P0)
Purpose: a library of dynamic blocks for hospital, office and residential furniture, carrying
attributes (inventory number, type, size).

Tools:
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

Block library: `assets/blocks/furniture/` (one DWG per block, inserted via `WBLOCK REF`).

**At least 80 blocks for the hospital** (list in §6).

### 4.3 `acad-plumbing` (P0)
Purpose: sanitary fittings per PN-EN 997 (WCs), PN-EN 31 (basins), PN-EN 232 (baths), PN-EN 251
(enclosures).

Tools:
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

Blocks: `assets/blocks/plumbing/`.

### 4.4 `acad-openings` (P0)
Purpose: construction-grade doors and windows with attributes, replacing raw `draw_line` plus
`draw_arc` inside a wall.

Tools:
```
acad.openings.draw_door(wall_handle, offset, width, swing_side, hinge_side, type, rei, number?)
  - type = "single"/"double"/"slider"/"fold"/"rotating"
  - rei  = "EI30"/"EI60"/"EI120"/"REI120"/"none"
  - cuts the wall automatically, then draws frame, leaf, handle and swing zone

acad.openings.draw_window(wall_handle, offset, width, height, sill_height, type, panes, rc?)
  - type = "single"/"double"/"tilt-turn"/"fixed"/"skylight"
  - rc   = "RC2"/"RC3"/"RC4" (PN-EN 1627)

acad.openings.list_doors(layer?)                  -> handles + width + type + REI + number
acad.openings.list_windows(layer?)
acad.openings.set_door_number(handle, number)     -> auto-numbering D-001, D-002 per storey
acad.openings.set_window_number(handle, number)
acad.openings.renumber_all(prefix, start)
acad.openings.export_door_schedule(outputPath)    -> CSV for acad-schedules
acad.openings.export_window_schedule(outputPath)
acad.openings.swap_door_type(handle, newType)
acad.openings.flip_hinge(handle)                  -> flips left/right
```

This category retires the current antipattern of "draw a door as a line plus an arc without
cutting the wall".

### 4.5 `acad-verticals` (P1)
Purpose: stair cores, spiral stairs, ramps and lifts.

Tools:
```
acad.verticals.draw_straight_stair(start, direction, flight_count, step_count, step_depth, step_width, landing_depth?)
acad.verticals.draw_spiral_stair(center, outer_radius, inner_radius, step_count, rotation_deg, direction)
acad.verticals.draw_ramp(start, end, width, slope_pct, surface_type)
acad.verticals.draw_elevator_shaft(position, size, type, capacity)   -> "passenger"/"bed"/"goods"/"car-lift"
acad.verticals.draw_escalator(position, direction, length, width)
acad.verticals.draw_platform_lift(position, size, type)
acad.verticals.add_handrail(stair_handle, side, type)                -> "both"/"left"/"right", "round"/"flat"/"pn-en-13374"
```

For the hospital: a bed lift is mandatory (160 cm × 260 cm, 1600 kg capacity, WT §54).

### 4.6 `acad-grids` (P1)
Purpose: a structural axis grid with bubble labels.

Tools:
```
acad.grids.draw_grid(origin, x_spacings, y_spacings, bubble_style?) -> auto-labels A,B,C... and 1,2,3...
acad.grids.add_axis(direction, position, label)
acad.grids.remove_axis(label)
acad.grids.rename_axis(old, new)
acad.grids.list_grid()                 -> axes + spacings + origin
acad.grids.snap_to_grid(handle, tolerance)
```

Bubble style: an 8 mm circle (800 mm in model space at 1:100), text 5 mm = 500 mm.

### 4.7 `acad-schedules` (P1)
Purpose: parametric tables (doors, windows, rooms, finishes) in paper space.

Tools:
```
acad.schedules.generate_door_schedule(layout_name, position, columns?)
acad.schedules.generate_window_schedule(layout_name, position, columns?)
acad.schedules.generate_room_schedule(layout_name, position, columns?)
  - columns: ["number", "name", "area", "floor_finish", "wall_finish", "ceiling_finish", "height"]
acad.schedules.generate_finish_legend(layout_name, position)
acad.schedules.update_schedule(handle)   -> re-runs the query against current data
```

Technically: the AutoCAD `Table` entity with a `TableStyle`, formula columns and cell contents.

### 4.8 `acad-sections` (P2)
Purpose: A-A and B-B section lines with a cut-plane marker and a direction arrow.

Tools:
```
acad.sections.draw_section_line(start, end, label, view_direction, depth_mm?)
acad.sections.draw_section_marker(position, label, orientation)
acad.sections.list_sections()
acad.sections.set_section_view(label, target_layout)
```

### 4.9 `acad-plotstyles` (P2)
Purpose: managing CTB/STB per-layer lineweights and plot colours.

Tools:
```
acad.plotstyles.apply_ctb(ctb_name)                   -> "AIA 2017"/"PN-B-01025"/"Hospital-ISO"
acad.plotstyles.set_layer_policy(layer, lineweight_mm, plot_color, screen_pct?, plot?)
acad.plotstyles.export_ctb(outputPath)
```

The 9-tier lineweight policy is described in `61-lineweight-policy.md` below.

### 4.10 `acad-callouts` (P3)
Purpose: leader lines carrying a profile code (K1, K6, K10), plus scale bars and the north
arrow.

Tools:
```
acad.callouts.insert_profile_callout(position, profile_code, description?)
acad.callouts.insert_north_arrow(position, rotation_deg, style)   -> "simple"/"compass"/"pn-en-iso-129"
acad.callouts.insert_scale_bar(position, scale, units, segments)
acad.callouts.insert_finish_callout(position, floor_code, wall_code, ceiling_code)
```

---

## 5. Extensions to existing categories

### 5.1 `Architecture` extend (+6 tools)
```
acad.architecture.draw_window(...)                 -> duplicates openings, but a convenient shortcut
acad.architecture.draw_stair(...)                  -> duplicates verticals
acad.architecture.draw_ceiling_grid(room, grid_size, pattern, fixture_positions?)
acad.architecture.set_wall_material(handle, material, rei?)
acad.architecture.split_wall_at_opening(wall_handle, offset, width)   -> already done by hand in Phase C; needs formalising
acad.architecture.offset_wall_with_insulation(wall_handle, thickness_out, thickness_in, insulation_thickness)
```

### 5.2 `Blocks` extend (+4 tools)
```
acad.blocks.library_register(manifest_path)        -> points at assets/blocks/<domain>/manifest.json
acad.blocks.library_list(domain)
acad.blocks.bulk_insert(block_name, positions[], orientations[], scale?)
acad.blocks.swap_block(handle, newBlockName, preserveAtts?)
```

### 5.3 `Dimensions` extend (+5 tools)
```
acad.dimensions.draw_chain_dimension(points[], side, offset, textHeight, tickStyle)  -> main chain + sub chain in one action
acad.dimensions.draw_cumulative_dimension(basePoint, points[], side, offset)
acad.dimensions.draw_baseline_dimension(basePoint, points[], side, offset)
acad.dimensions.set_tick_policy(style)  -> "45deg"/"dot"/"arrow" per PN-EN-ISO-129
acad.dimensions.auto_dim_walls(layer, offset, levels)  -> dimensions every external wall across 3 levels (cumulative + sub + main)
```

---

## 6. The block library — minimum list (~80 blocks)

Folder structure:
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

**One manifest per folder:** `manifest.json` listing `{ name, path, attributes[], anchor,
preview_png }`.

---

## 7. New rules (docs/engineering-rules/)

Every file is written according to rule `53-rules-update-mandate.md`.

| # | File | `alwaysApply` | Scope | What it says |
|---|---|---|---|---|
| 60 | `60-architectural-fidelity.md` | true | whole project | Minimum detail per room type (bedroom → at least a bed, nightstand, wardrobe and light; WC → at least a pan and a basin) |
| 61 | `61-lineweight-policy.md` | true | whole project | 9-tier lineweights (0.05 → 1.4 mm) and the layer→lw→pen mapping |
| 62 | `62-hatching-policy.md` | true | whole project | Default hatch per layer (A-WALL-EXT → AR-CONC @ scale=100, A-WALL-INT → AR-BRSTD, A-WALL-LEAD → ANSI38 in red, …) |
| 63 | `63-sanitary-fixtures-wt.md` | true | Architecture/Plumbing | Minimum WC and bathroom fittings per WT §78 and PN-EN 17210 (accessibility) |
| 64 | `64-furniture-density-per-room.md` | true | Furniture | Minimum furniture per function (an operating theatre → table, light, anaesthesia cart, workstation) |
| 65 | `65-door-window-schedule.md` | true | Openings/Schedules | Every door and window MUST carry a number, type, REI rating and width as block attributes |
| 66 | `66-dimension-chains.md` | true | Dimensions | At least 3 dimension levels (main/sub/cumulative), 45° tick marks per PN-EN-ISO-129 |
| 67 | `67-grid-axes.md` | true | Grids/Architecture | Every plan MUST carry a structural axis grid with bubble labels |
| 68 | `68-plan-symbols-standard.md` | true | Callouts | Every drawing: north arrow, scale bar and title block per PN-B-01025 |
| 69 | `69-callouts-leaders.md` | false (opt-in per project) | Callouts | The K1/K6/K10 convention and the elevation callout system |

Skeleton for `60-architectural-fidelity.md`:
```markdown
---
description: Minimum architectural detail per room type. Without it the plan reads as a student exercise.
alwaysApply: true
---

# Minimum architectural detail per room type

A CONSTRUCTION-level plan MUST carry, for every room:
- Walls with hatching (per `62-hatching-policy.md`)
- Every door and window opening drawn with `acad-openings` (NOT raw line + arc)
- Furniture per the table below
- A label with number, name and area (generated by `acad-schedules.generate_room_schedule`)

## Minimum furniture per room type

| Type | Minimum required blocks |
|---|---|
| Single bedroom | bed + nightstand + wardrobe + desk + chair |
| Single hospital room | hospital bed + headwall + patient wardrobe + visitor armchair + table |
| Standard WC | pan + basin + mirror + bin + paper holder |
| Accessible WC | wall-hung pan + accessible basin + grab rails + alarm + tilting mirror (PN-EN 17210) |
| Operating theatre | operating table + light + anaesthesia cart + workstation + IV pumps + backup light |
| ER box | trolley bed + defibrillator + suction + monitor + clinician chair + table |
...
```

---

## 8. A new Vision persona — `senior-architect-reviewer`

Location: `src/AcadMcp.Vision/personas/senior-architect-reviewer.yaml`.

System prompt (skeleton):
```yaml
name: senior-architect-reviewer
model: claude-4.5-sonnet-thinking
max_tokens: 8000
system: |
  You are a senior architect with 20 years of experience designing public buildings
  (hospitals, schools, offices) on the Polish market. You assess construction drawings
  against:
  - Polish Building Act (consolidated text, Dz.U. 2024 poz. 725)
  - WT: Dz.U. 2022 poz. 1225 (technical conditions for buildings)
  - Ministry of Health regulation on hospital requirements (Dz.U. 2019 poz. 595)
  - PN-B-01025 (graphical symbols on architectural and building drawings)
  - PN-EN-ISO-129 (dimensioning)
  - PN-EN-ISO-5457 (drawing formats)
  - PN-EN-ISO-128 (line types)
  - AIA CAD Layer Guidelines (layering)

  You score against a 17-point rubric (below) and ALWAYS return JSON:
  { "score": <int 0-17>, "grade": "<A+/A/.../F>", "findings": [{"id":"...","severity":"critical/major/minor","description":"...","location":"..."}], "overall":"..." }

rubric:
  - id: hatches
    weight: critical
    question: "Do the walls carry correct hatches (concrete/brick/insulation), legible at 1:100?"
  - id: furniture
    weight: critical
    question: "Does every room carry the minimum furniture required for its type?"
  - id: plumbing
    weight: critical
    question: "Are sanitary rooms drawn in full (pan + basin + …) rather than labelled?"
  - id: openings
    weight: major
    question: "Do doors show a handle, swing direction and number; do windows show glazing?"
  - id: verticals
    weight: major
    question: "Are stairs and lifts drawn with tread and car dimensions?"
  - id: grids
    weight: major
    question: "Is there a structural axis grid with bubble labels and spacing dimensions?"
  - id: dimensions
    weight: major
    question: "Are there at least 3 dimension levels (main/sub/cumulative) with 45° ticks?"
  - id: schedules
    weight: major
    question: "Are door/window/room schedules present in paper space?"
  - id: sections
    weight: major
    question: "Are A-A / B-B section lines present?"
  - id: lineweights
    weight: major
    question: "Do lineweights vary per layer (heavy outlines, fine hatching)?"
  - id: callouts
    weight: minor
    question: "Are profile callouts (K1/K10) and finish callouts present?"
  - id: symbols
    weight: minor
    question: "Is there a north arrow, a scale bar and a title block?"
  - id: legend
    weight: minor
    question: "Is there a legend of materials, layers and symbols?"
  - id: reflected_ceiling
    weight: minor
    question: "Is there a reflected ceiling plan with lighting (may be a separate drawing)?"
  - id: details
    weight: minor
    question: "Are reveal/threshold/lintel details drawn at 1:10–1:20?"
  - id: compliance
    weight: critical
    question: "Do the symbols follow PN-B-01025? Is the drawing compliant with WT and MZ?"
  - id: readability
    weight: critical
    question: "Is every label legible at 1:100 without overlaps?"
```

---

## 9. Phase D — execution plan (12 steps)

```
D0  Backup + branch feature/phase-D-architectural-upgrade
D1  Scaffold the 7 new categories (scripts/new-category.ps1 x 7)
D2  Implement acad-hatches (P0 - 8 tools)
D3  Implement acad-furniture (P0 - 10 tools) + the hospital/office block library (50+ blocks)
D4  Implement acad-plumbing (P0 - 8 tools) + the plumbing library (20+ blocks)
D5  Implement acad-openings (P0 - 10 tools) + the openings library (10+ blocks)
D6  Extend Architecture (+6 tools), Dimensions (+5 tools), Blocks (+4 tools)
D7  Implement acad-verticals and acad-grids (P1 - 13 tools) + library (15+ blocks)
D8  Implement acad-schedules (P1 - 5 tools) + the "HOSPITAL-DEF" and "OFFICE-DEF" TableStyles
D9  Implement acad-sections, acad-plotstyles, acad-callouts (P2-P3 - 11 tools) + the symbols library
D10 Write the 10 new .md rules and update the existing ones (62 hatching, 26 API traps)
D11 Add the senior-architect-reviewer persona + the Vision sidecar endpoint + manifest refresh
D12 Regenerate the hospital plan from the Phase C checkpoint:
     - ckpt-phaseD-start
     - apply hatches per rule 62
     - populate_room across all 53 rooms (furniture + plumbing)
     - swap the 61 "raw" doors for acad-openings (preserving numbers)
     - grid + dimension chains (3 levels)
     - schedules (61 doors, windows TBD, 53 rooms)
     - 2 sections (A-A through ER-OR-MR, B-B through the ward)
     - lineweight policy + the HOSPITAL-ISO.ctb plot style
     - profile callouts + symbols
     - senior-architect-reviewer pass x 3 (overview + 6 tiles + details)
     - final DWG -> Hospital2026_PRO_A0-001.dwg
     - final multi-page PDF (20 pages: 1 cover + 1 overview + 8 zone zooms + 3 schedules
       + 2 sections + 2 RCP + 3 details)
```

**Phase D acceptance gate:** `senior-architect-reviewer` returns `score ≥ 15/17` on the overview
and `score ≥ 13/17` on every zoom tile (mean). No critical findings.

---

## 10. The 12/10 rubric — final definition

| Axis | Max | Weight | What it counts |
|---|---|---|---|
| Safety (Phase C) | 12 | 40% | Already PASS (12/12) |
| Architectural fidelity (Phase D) | 17 | 40% | The `senior-architect-reviewer` rubric |
| Legal compliance | 10 | 15% | WT + MZ + PN-B + the Building Act |
| Documentation hygiene | 5 | 5% | CHANGELOG, docs, rules, tests |

**Total: 44 points. Target 12/10 = ≥ 92% = 41 points.**

Today: 12 (safety) + 4 (architectural fidelity, estimated) + 9 (compliance) + 5 (docs) =
**30/44 = 68% = 8.2/10**.

After Phase D, the target is 12 + 16 + 10 + 5 = **43/44 = 97.7% = 11.7/10**.

The remaining +0.3 towards "12/10" is 3D rendering, elevation visualisation and BIM export —
outside Phase D, in Phase E.

---

## 11. Effort estimate

| Step | Dev days | QA days | Block days |
|---|---|---|---|
| D1 scaffold | 0.5 | 0 | 0 |
| D2 hatches | 2 | 0.5 | 0 |
| D3 furniture + hospital blocks | 3 | 1 | **5** |
| D4 plumbing + blocks | 2 | 0.5 | **2** |
| D5 openings + blocks | 4 | 1 | **1.5** |
| D6 extensions (Arch, Dim, Blocks) | 3 | 1 | 0 |
| D7 verticals + grids | 3 | 1 | **2** |
| D8 schedules | 2 | 0.5 | 0 |
| D9 sections + plotstyles + callouts | 3 | 1 | **1** |
| D10 rules | 1.5 | 0 | 0 |
| D11 vision persona | 1 | 0.5 | 0 |
| D12 regenerate the hospital plan | 2 | 2 | 0 |
| **Total** | **27** | **9** | **11.5** |

**≈ 47.5 person-days** solo. With two people, roughly 25 calendar days.

---

## 12. Risks and rollback strategy

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| The AutoCAD `Hatch` boundary association breaks after an edit | High | Major | The `regenerate_all(scope)` tool plus an end-to-end test |
| The block library grows into gigabytes | Medium | Minor | A separate `autocad-mcp-blocks` repository with sparse checkout |
| ToolBank manifests drift from the code | High | Major | `check-manifests.ps1` in CI; the `CheckManifestSync` MSBuild target already enforces this |
| Per-layer lineweights break existing drawings | Medium | Major | The `assets/migrate-lineweights.ps1` migration script plus a `respect_existing_lw` flag |
| The architect-reviewer persona hallucinates | Medium | Major | A deterministic rubric, JSON schema validation, 3 passes and a majority vote |
| Phase C safety regressions during the D12 regeneration | Low | CRITICAL | A checkpoint before D12 and 5 post-D12 `check_overlaps` scans → abort on any regression |

**Rollback plan:** every D step has its own checkpoint (`ckpt-phaseD-<step>`) and a gate at the
end. If QA fails, `acad.checkpoint.restore` and a root-cause analysis.

---

## 13. References

- `docs/HOSPITAL-2026-REVIEW-FINDINGS.md` §4e (Phase C finish)
- `docs/engineering-rules/00-architecture-invariants.md` (the invariants)
- `docs/engineering-rules/41-new-category-flow.md` (how to add a category)
- `docs/engineering-rules/53-rules-update-mandate.md` (how to add a rule)
- AIA CAD Layer Guidelines 2017
- PN-B-01025:2004
- PN-EN-ISO 129, 128, 5457
- Polish WT: Dz.U. 2022 poz. 1225
- Ministry of Health, hospitals: Dz.U. 2019 poz. 595

---

**Next action, once approved:** the user says "green light Phase D" and D0–D12 are executed as a
todo-driven run.
