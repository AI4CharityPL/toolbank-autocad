# 69. Plan-symbol + callout policy (acad-callouts)

Plan-symbol + leader policy — north arrow, scale bar, section/detail callouts, title block, leader hierarchy. READ BEFORE editing CalloutsTools.cs, before inserting any callout composite, before adding a new plan symbol to the library.

Companion to rule 35 (domain-categories-design), rule 60 (architectural
fidelity — upcoming), rule 61 (lineweight policy — upcoming), rule 66
(dimension chains), rule 67 (grid axes) and rule 68 (plan-symbols standard —
upcoming). This rule freezes the geometry + layer + plotted-size contract
for every callout and leader emitted by the `acad-callouts` composite
category so that Polish architectural practice (ISO 5455 + ISO 7200 + PN-EN
ISO 128) is respected end-to-end.

## §1. Layers (mandatory)

Every composite tool in `acad-callouts` MUST draw onto one of the following
layers. Create the layer on demand via `acad.layers.create_layer`
(idempotent); DO NOT draw on `0`, `Defpoints` or the layer of the referenced
host entity.

| Layer         | Purpose                                             |
|---------------|-----------------------------------------------------|
| `A-ANNO-NORT` | North arrow                                         |
| `A-ANNO-SBAR` | Graphic scale bar                                   |
| `A-ANNO-SYMB` | Section + detail markers (circles, triangles)        |
| `A-ANNO-TTLB` | Title block (stamp + inner field divisions)         |
| `A-ANNO-BORD` | Sheet paper border + drawing-area frame             |
| `A-ANNO-TEXT` | Free-floating explanatory text outside any callout  |

Layer colours are inherited from the layer, not set on the entity. Composite
tools MUST NOT stamp an ACI colour on individual primitives.

## §2. Plotted sizes (not drawing units!)

All callouts are sized in **plotted millimetres** and then multiplied by the
user-declared scale factor (1:50 → 50, 1:100 → 100…). The `CalloutsPalette`
table is authoritative. Do not hard-code drawing-unit sizes.

| Symbol                | Plotted base size | Plotted text size |
|-----------------------|-------------------|-------------------|
| North arrow           | Ø30 mm            | 3.5 mm ("N")      |
| Scale bar             | 50 mm long × 4 mm | 2.5 mm numbers / 3.5 mm caption |
| Section marker        | Ø10 mm            | 3.5 mm (letter), 2.5 mm (sheet ref) |
| Detail marker         | Ø12 mm            | 3.5 mm (number)   |
| Title block field     | —                 | 2.5 mm value / 3.5 mm key / 5 mm drawing title |

Callers override plotted sizes via the `*PlotMm` fields in every DTO. DO NOT
override them by multiplying manually — let `ResolveScaleFactor` do it.

## §3. Title block (ISO 7200 tailored to PL practice)

The default 12-row title block is (top→bottom): `PROJEKT`, `INWESTOR`,
`ADRES`, `BRANŻA`, `FAZA`, `STADIUM`, `RYSUNEK`, `SKALA`, `NR RYS.`, `DATA`,
`PROJEKTANT`, `SPRAWDZAJĄCY`. The block is anchored to the bottom-right
corner of the inner margin, 180 mm wide (plotted). Cell heights follow the
`fieldHeightPlotMm × 2.4` formula. The `RYSUNEK` row's value is typeset at
`titleHeightPlotMm` so the drawing title reads 5 mm on paper.

`insert_title_block` accepts both an explicit `fields` list AND shorthand
parameters (`projectName`, `sheetNumber`, `author`, `date`, `titleText`).
Shorthand fields are only filled if the row is not already present in
`fields`. `SKALA` is always seeded from the `scale` argument.

## §4. Section + detail markers (PN-EN ISO 128)

1.  A section callout is two symbols at the ends of the cut line: a circle
    with the section letter (e.g. `A`) plus a triangular arrow pointing in
    the view direction. Optionally a second line below the letter carries
    the target sheet reference (`1/5` for "sheet A-101 detail 5").
2.  The cut line is drawn on the same layer as the markers (`A-ANNO-SYMB`)
    when `drawCutLine=true`; callers may disable it and supply their own
    dashed line on a dedicated linetype (typical: `DASHED2` @ 2× scale).
3.  A detail callout has TWO circles connected by a short leader: (a) the
    area circle encompassing the plan feature, (b) the tag bubble naming
    the detail. The bubble's top half carries the detail number, the bottom
    half the target scale, and (optional) an offset text right of the bubble
    carries the sheet reference.

View-direction arrows are mandatory on section markers — plan reviewers rely
on them to tell which side of the cut is elevation and which is background.
The default is `viewDirection="right"` (right-hand side of the start→end
vector). Flip via `"left"`.

## §5. Scale bar (ISO 5455)

A scale bar MUST:

1.  Be a chequered bar with ≥5 equal segments totalling ≥50 mm plotted
    length.
2.  Label every segment divider with its metre value (0, 1, 2, 3, 4, 5).
3.  Carry a caption above the bar of the form `SKALA 1:100` (Polish) or
    `SCALE 1:100` (English); never just the ratio.
4.  Use drawing-unit-correct metres — `segmentMeters * 1000` per segment
    at whatever scale was declared, never "looks about right" fudging.

Callers who need unusual scales (1:25, 1:500) override `segmentMeters`
explicitly; the default preset table auto-selects 0.5 m / 1 m / 2 m / 5 m
segments depending on the scale factor.

## §6. North arrow

A north arrow is: a filled diamond arrow head pointing up, inscribed in a
circle, with the letter `N` centred above the circle. The composite draws
the diamond as a closed polyline so that:

- B&W plots still read as a filled arrow because the closed polyline's
  lineweight merges the fill.
- Callers who want a SOLID hatch fill must follow up with
  `acad.geometry2d.draw_hatch { pattern:"SOLID", boundaryHandles:[handle] }`
  on the returned arrow handle.

`rotationDeg` rotates the whole symbol around the insertion point; useful
when the plan's true north is not straight up. `label` is always "N" unless
the caller explicitly overrides it (e.g. for a compass-rose variant).

## §7. Leader hierarchy

When composites draw leader lines (detail callouts, future K-profile
callouts), follow this priority:

1.  Leader endpoint chosen by caller (`leaderEndPoint` in the DTO) wins.
2.  If absent, place the bubble at `(center.x + radiusMm*2, center.y +
    radiusMm*2)` — north-east of the feature, away from plan density.
3.  The leader line starts on the area-circle's edge (not its centre) and
    ends on the tag-bubble's edge, not inside it. Use the unit vector to
    trim both endpoints.

## §8. Test coverage expectations

Unit tests should at minimum pin:

- `CalloutsPalette.ResolveScaleFactor` returns 100 for empty / unknown
  inputs and the raw number for `1:N` inputs.
- `CalloutsPalette.ResolveScaleBarPreset` returns 0.5 m segments for scales
  ≤ 1:25, 1 m for 1:50-1:100, 2 m for 1:200, 5 m for 1:500+.
- Every composite's `[McpTool]` attribute binds to `category="callouts"`
  (rule 24 — category binding).
- The callouts category is discoverable via `acad_load_category { category:
  "callouts" }` and returns exactly 5 tools.
