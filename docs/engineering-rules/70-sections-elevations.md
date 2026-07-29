# 70. Sections + elevations policy (acad-sections)

Section line + view-title + elevation-marker policy — A-DETL-SECT/TITL/ELEV layers, DASHED2 cut linetype, step ticks, view direction semantics. READ BEFORE editing SectionsTools.cs, before drawing a section cut, before labelling a cross-section / elevation viewport.

Companion to rule 35 (domain-categories-design), rule 60 (architectural
fidelity — upcoming), rule 66 (dimension chains), rule 67 (grid axes),
rule 68 (plan-symbols — upcoming) and rule 69 (callouts/leaders). This rule
freezes the section-cut + view-title + elevation-marker geometry contract
for the `acad-sections` composite category.

The category deliberately does NOT render the cross-section view itself —
generating a literal section view from 3D geometry is outside scope for
2026-Q2. Instead, `acad-sections` provides the **symbology scaffolding** so
reviewers read a set of 2D plans as "these two points have a matching
section on sheet A-301 drawn at 1:50".

## §1. Layers (mandatory)

| Layer            | Purpose                                              |
|------------------|------------------------------------------------------|
| `A-DETL-SECT`    | Cut lines (DASHED2) + offset ticks                   |
| `A-DETL-TITL`    | Section / view titles + caption underlines           |
| `A-DETL-ELEV`    | Elevation-direction markers (triangle + baseline)    |
| `A-ANNO-SYMB`    | End-markers (circle + triangle) — inherited from r69 |

Composites MUST call `acad.layers.create_layer` idempotently via
`ArchitectureProxy.EnsureLayerAsync` before drawing. Do not draw onto `0`
or `Defpoints`.

## §2. Cut line contract (rule 70 §2)

1.  Cut lines are LINEWORK on `A-DETL-SECT` drawn as a single
    start-to-end `Line` (not a polyline with intermediate bend vertices —
    those are handled by a future step-line variant).
2.  The linetype MUST be `DASHED2` scaled by the plan scale factor
    (`ltscale = scaleFactor × 1.0`). `insert_section_line` calls
    `acad.modify.set_linetype` after `draw_line`; if the linetype is not
    loaded, the call is allowed to fail silently and the line plots
    Continuous — callers can pre-load DASHED2 via `acad.layers.set_layer_
    linetype` on a dedicated linetype layer.
3.  A 6 mm plotted perpendicular tick MUST be drawn at BOTH endpoints when
    `drawOffsetTicks=true` (the default). The ticks are the *offset*
    symbology — they signal that the cut path is schematic, not a literal
    metric distance.
4.  End markers are delegated to `acad-callouts.insert_section_callout`
    with `drawCutLine=false`, so that the circle-plus-triangle marker
    geometry stays in one place (rule 69 §4). `insert_section_line`
    supplies the same `label`, `scale`, `viewDirection`, `sheetReference`
    values as the cut-line so both ends read A-A on the printed sheet.

## §3. Section title contract (rule 70 §3)

`insert_section_title` places THREE text elements anchored on the
`position` (typically the centre-bottom of the section view):

1.  Caption + label — `PRZEKRÓJ A-A` at plotted 5 mm.
2.  Underline — 80 mm plotted horizontal line separating title from scale.
3.  Scale — `SKALA 1:50` at plotted 3.5 mm.

The caption defaults to `PRZEKRÓJ` (Polish "section") and is overridable
(`ELEWACJA`, `WIDOK`, `FRAGMENT`, `DETAL`, …). `viewScale` is distinct
from the plan scale — a 1:100 plan can host a 1:50 section's title.

## §4. Elevation marker contract (rule 70 §4)

Elevation markers differ from section callouts: elevations are always
*directional* (looking from a fixed viewpoint towards a building face),
so the marker is a filled triangle pointing in the compass direction
N/E/S/W (and the 4 intermediate NE/NW/SE/SW). `direction` accepts the
compass shorthand or bare degrees (0 = east, 90 = north, per AutoCAD
convention).

Geometry:

- Triangle — 8 mm plotted per side (equilateral-ish), tip in the compass
  direction.
- Baseline — 30 mm plotted horizontal line below the triangle.
- Label — `ELEWACJA <dir>` to the right of the baseline at plotted 3.5 mm.
- Optional sheet reference — one line below the label at plotted 2.8 mm.

Do not combine section-callout geometry with elevation-marker geometry;
they serve different reviewer questions ("which way does the cut go?" vs
"which building face is this?").

## §5. Inventory contract (rule 70 §5)

`list_section_lines` MUST:

1.  Default to `layerFilter=A-DETL-SECT`, override allowed.
2.  Return each entity's `handle`, `layer`, `objectClass`, and — when the
    entity is a `Curve` — its `lengthMm`. `lengthMm` is `null` for
    non-curve entities (e.g. annotations that stray onto the layer).
3.  Return an empty list gracefully when the layer is absent, never throw.

This inventory underpins the planned `update_sections` composite (D9
phase 2) which will rebuild section views after plan mutations.

## §6. Composite-of-composite pattern (rule 70 §6)

`acad-sections` composites MAY call `CalloutsTools.*` directly because
both categories live inside `AcadMcp.Backend`. This is a deliberate
exception to the "primitives only" rule in rule 35 §2: end-marker
geometry is authoritative in `acad-callouts` (rule 69) and duplicating it
in `acad-sections` would drift. Cross-category calls MUST:

1.  Pass the same `IPluginGateway` + `CancellationToken` parameters.
2.  Use the caller's `layer`/`scale`/`label` values rather than redefault.
3.  Propagate the returned handle list back into the composite's own
    `Summary.Handles` so callers can rollback the whole insertion with
    one `acad.modify.erase` call.

## §7. Test coverage expectations

Unit tests should at minimum pin:

- `SectionsPalette.ResolveDirectionDeg` returns correct degrees for the
  8 compass names and for bare numeric strings.
- Every composite's `[McpTool]` attribute binds to `category="sections"`
  (rule 24 — category binding).
- The sections category is discoverable via `acad_load_category` and
  returns exactly 4 tools.
- `A-DETL-SECT/TITL/ELEV` layer constants follow the `A-DETL-` prefix.
