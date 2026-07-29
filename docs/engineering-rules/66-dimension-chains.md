# 66. Dimension chains policy (acad-dimensions)

Dimension chains policy — 3-level hierarchy (overall / axis / opening), ARCH-ISO dimstyle semantics, architectural tick vs arrow, cumulative chain math, auto_dim_walls wall-projection semantics. READ BEFORE editing DimensionsTools.cs / DimensionsPluginTools.cs, before calling ensure_architectural_dimstyle / cumulative_chain / auto_dim_walls / dimension_overall from architecture or verification layers.

Companion to rule 19 (tool impl pattern), rule 27 (text-and-table traps),
rule 65 (opening numbering) and rule 60 (architectural fidelity — upcoming).
Defines how architectural drawings are dimensioned so every plan produced
by the MCP is schedule-consistent, print-predictable and compliant with
PN-EN ISO 129 / ISO 13567 drawing conventions.

A drawing without a correct 3-level chain is a drawing that cannot be
built from. A-E architect reviews deduct **≥2 points** the moment the
overall / axis / opening hierarchy is missing, and our `senior-architect-
reviewer` persona flags this as a **hard fail** (criterion #7).

## 1. Drawing unit and scale

Millimetres. Dimensions never appear in metres or centimetres on Polish
architectural plans. Callers MUST `files.set_units('mm')` before any
dimension call. `DIMLFAC` stays at `1.0`; scaling to the sheet is done by
`DIMSCALE` (= plot-scale denominator, typically `100` for 1:100).

`ensure_architectural_dimstyle(scale=100.0)` is the canonical way to
prepare a style; do **not** override the individual `DIM*` vars in C#
outside that tool.

## 2. Three-level chain hierarchy

Every architectural plan at scale 1:50, 1:100 or 1:200 carries **three
parallel chains** on every orthogonal façade, stacked away from the
building outline in this order (closest → furthest):

| Level | Content | Typical offset | Tool |
|-------|---------|----------------|------|
| **L1 — opening** | Clear openings + piers: door / window widths and the brickwork between them. | 1 × text-height past wall face | `continued_chain` or `cumulative_chain` |
| **L2 — axis** | Centre-to-centre distance between structural axes (grid bubbles A-B-C… or 1-2-3…). | 2 × text-height past L1 | `baseline_chain` or `cumulative_chain` |
| **L3 — overall** | Single segment: exterior face → exterior face along that façade. | 3 × text-height past L2 | `dimension_overall` or `linear` |

Rule of thumb: **L1 segments MUST sum to the L2 segment they sit under,
and L2 segments MUST sum to L3.** A validator that finds a mismatch ≥1mm
flags it as a geometry bug, never a dimension bug.

Do **not** interleave opening dimensions inside the axis chain or the
overall dimension; keep the three chains on distinct dim-line offsets so
the reader's eye can walk down L1 → L2 → L3.

## 3. ARCH-ISO dimstyle

`ensure_architectural_dimstyle(styleName='ARCH-ISO', scale=100)` creates
(or updates) a dimstyle calibrated for Polish architectural output:

| DIM var | Value | Meaning |
|---------|-------|---------|
| DIMSCALE | `scale` (e.g. 100) | Global scale for the whole style; plot-mm values below are multiplied by it on screen. |
| DIMTXT | 2.5 mm | Text height (ISO A0-A3, 1:100 plan). |
| DIMASZ | 2.5 mm | Arrow / tick size. |
| DIMTAD | 1 | Text placed **above** the dim line (never inline). |
| DIMTIH / DIMTOH | `false` | Text rotates with dim line (never forced horizontal). |
| DIMGAP | 0.625 mm | Gap between text and dim line. |
| DIMEXE | 1.25 mm | Extension past dim line. |
| DIMEXO | 0.625 mm | Origin offset. |
| DIMDLI | 7.0 mm | Baseline spacing (auto for `baseline_chain`). |
| DIMRND | 1.0 | Round displayed distance to 1 mm. |
| DIMDEC | 0 | Decimal places. |
| DIMZIN | 8 | Suppress trailing zeros. |
| DIMBLK/DIMBLK1/DIMBLK2 | `_ArchTick` | Architectural tick marks (slash) instead of arrows. |

**Ticks, not arrows, are mandatory for 1:50 / 1:100 / 1:200 architectural
plans.** Arrows appear only on mechanical / civil details (use built-in
`ISO-25` there). Use `apply_arch_tick_style(layer='A-ANNO-DIMS')` to
retrofit existing dimensions onto ARCH-ISO in one pass.

## 4. Continued vs baseline vs cumulative

Three chain modes live in this category — pick the right one:

- **`continued_chain`** — each segment reads its own length (3200 → 1800
  → 6400 …). This is the default for opening (L1) and axis (L2) chains.
- **`baseline_chain`** — each segment reads the running total from the
  same baseline point (3200 → 5000 → 11400 …), stacked by `DIMDLI`. Used
  only when clients explicitly request cumulative-stacked dimensions
  (typical for site-plan setting-out).
- **`cumulative_chain`** — like `baseline_chain` but **all segments share
  the same dim-line point** (no stacking). Each segment still reads the
  cumulative distance from the baseline. Used for formwork setting-out
  and for wall-opening L1 when the architect wants running totals on a
  single line.

Mixing continued + cumulative dimensions on the same dim line is a bug;
the reader cannot tell which rule applies.

## 5. `auto_dim_walls` semantics

`auto_dim_walls(wallHandles, baselinePoint, rotationDeg, dimLinePoint)`
is a composite convenience tool that:

1. Queries every wall's end points via `acad.geometry2d.get_entity`.
   Walls may be `Line` or 2-vertex `Polyline`; multi-vertex polyline
   walls are rejected (split them first with `split_wall_at_opening`).
2. Projects every end-point onto the infinite line defined by
   `baselinePoint` + `rotationDeg`. The projection axis is that line;
   all distances below are measured along it.
3. Sorts the projected points, then **merges** any two points closer
   than `mergeToleranceMm` (default 2 mm) into one — this collapses
   T-junctions where two walls share an endpoint.
4. Invokes `acad.dimensions.continued_chain` with the merged points as
   the chain. Resulting dimensions are written to
   `layer='A-ANNO-DIMS'` and dimstyle `'ARCH-ISO'` unless overridden.

Use `auto_dim_walls` for L1 (opening) and L2 (axis) chains only; for L3
(overall) call `dimension_overall` instead — it reports the bounding
box along the projection axis as a single segment.

### Known limitations

- `get_entity` does **not** return polyline intermediate vertices.
  Composite call sites assume 2-vertex walls; document this contract
  in the prompt when calling the tool.
- Angular dimensions on non-orthogonal walls are not projected; callers
  must issue `dimension_aligned` manually for those.

## 6. `apply_arch_tick_style` sweep

Existing legacy drawings often mix `ISO-25` arrow dimensions with newer
`ARCH-ISO` ticks. `apply_arch_tick_style(layer, dimStyle, ensureStyle)`
walks every `Dimension` on `layer` and reassigns `DimensionStyle` to
the named style, creating the style with `ensureStyle=true` if missing.
Run it once per migration, not per drawing edit.

Do **not** change `DimensionStyle` directly in bulk from C# callers;
always route through `apply_arch_tick_style` so the change stays
traceable in tool logs.

## 7. Layer + colour

| Layer | Purpose | Colour |
|-------|---------|--------|
| `A-ANNO-DIMS` | All architectural dimensions (L1/L2/L3). | `6` (magenta) |
| `A-ANNO-GRID` | Grid bubbles (rule 67). | `1` (red) |
| `S-ANNO-DIMS` | Structural dimensions on structural-engineer sheets. | `5` (blue) |

Hospital-specific overlays (CLEAR-WIDTH audit dims, evacuation-route
measurements) go onto `A-ANNO-DIMS-EGRESS` with colour `2` (yellow) so
they survive a `dwgexport` without A-REGAN / A-EQPM layers.

## 8. Compliance tie-in

PN-B-01025 (architectural drawing format) + PN-EN ISO 129-1 mandate:

- tick or arrow size = text height;
- text above dim line, never inline;
- extension lines offset ≥0.5 mm from the object;
- rounding per drawing scale (1 mm at 1:100, 5 mm at 1:500).

`ensure_architectural_dimstyle` defaults satisfy all four bullets.
`senior-architect-reviewer` (rule 70 — upcoming) cross-checks the live
`Dimension` objects against these exact values and deducts on any drift.

## 9. Do NOT

- do NOT set `DIMSCALE=1` and call it a day; the print will read 0.3mm-
  tall text at 1:100 and fail IFC acceptance.
- do NOT mix continued + baseline on the same dim line — rule 66 §4.
- do NOT dimension openings by end-of-jamb when the schedule expects
  clear-leaf width (acad-openings rule 65 §6). Use the `CLEAR_MM`
  attribute instead.
- do NOT route dimension extension lines across rooms / through
  furniture — move the dim line further out or split the chain.
- do NOT place overall (L3) dimensions inside paper-space viewports.
  L3 goes on model space so modifications propagate to every sheet.
