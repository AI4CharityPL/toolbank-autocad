# 60. Architectural fidelity rubric

Architectural fidelity rubric — the 17-criterion scorecard that `senior-architect-reviewer` (Vision persona) applies to every floor-plan deliverable before it can be called a wykonawczy / tender-ready drawing, PLUS a separate 3-criterion spatial-quality gate (§1a, criteria 18-20) that is checked programmatically rather than by the vision LLM. READ BEFORE generating a full project, regenerating Hospital2026, or wiring a new persona/validator that scores architectural quality.

Every floor-plan that ships from this repo (Hospital2026, future clinic /
office / residential projects) is graded against a **17-criterion
scorecard**. A drawing under **15/17** is NOT a rysunek wykonawczy — it
is a concept sketch and MUST NOT be exported to PDF/A0 for tender,
submission or construction use. The rubric is identical to the gap
analysis in `docs/PLAN-PROFESSIONAL-UPGRADE-2026.md §2` and is the
authoritative contract between `senior-architect-reviewer` (rule 32 /
D11 Vision persona), `acad-validators` rule-engine, and every generator
in `acad-architecture` / `acad-openings` / `acad-hatches` / … .

Use this rule as the **exit gate** for any `design_iterate` session and
as the **entry contract** for any AI reviewer prompt.

## 1. The 17 criteria (canonical order)

Each criterion is scored **0 / 0.5 / 1** (fail / partial / full). Total
out of 17. Partial = the element exists but violates at least one
sub-rule (wrong scale, wrong layer, missing attribute, etc.).

| #  | Axis                  | Criterion                                                                 | Weight | Evidence tool |
|----|-----------------------|---------------------------------------------------------------------------|-------:|---------------|
| 1  | Material expression   | **Wall hatching** per material (concrete, brick, insulation, plaster, lead, faraday) matches rule 62 table | 1 | `acad.hatches.list_hatches` + vision |
| 2  | Interior furnishing   | **Furniture** in every inhabited room (beds w/ bedding, chairs, desks, cabinets) with correct density per room-type (rule 64) | 1 | `acad.furniture.list_library` + `acad.selection.get_entities_on_layer A-EQPM*` |
| 3  | Sanitary fixtures     | **Plumbing** (WC, basin, shower, bath) in every WC/bathroom room, accessible fixtures where PN-EN 17210 applies (rule 63) | 1 | `acad.plumbing.list_plumbing_catalog` + count per room |
| 4  | Door quality          | **Doors** with jamb ticks + leaf + swing arc + visible `NUMBER` attribute; REI / lead / RC tags per §4 of rule 65 | 1 | `acad.openings.list_openings_in_model kind=doors` |
| 5  | Window quality        | **Windows** with frame + glass lines + centre + sill attribute + sash/tilt marker + RC where required | 1 | `acad.openings.list_openings_in_model kind=windows` |
| 6  | Vertical circulation  | **Stairs / lifts / ramps** with tread lines, numbered treads, arrow, handrail, shaft outline (rule 67 companion) | 1 | `acad.verticals.list_*` |
| 7  | Structural grid       | **Grid axes** (Y1/Y3/C/F bubble-labels) + continuous grid lines + cumulative spacing dimensions (rule 67) | 1 | `acad.grids.list_grid_axes` |
| 8  | Dimensioning          | **Dimension chains** (main / sub / cumulative) on all four sides with 45° ticks (rule 66) | 1 | `acad.dimensions.list_chains` |
| 9  | Schedules             | **Door / window / room schedules** (paperspace Tables rendered via HOSPITAL-DEF TableStyle) linked to block attributes | 1 | `acad.schedules.list_tables` |
| 10 | Callouts              | **Profile / detail callouts** (K1/K6/K10 column-profile leaders, section bubbles, north arrow, scale bar, title block) — rule 69 | 1 | `acad.callouts.*` |
| 11 | Section lines         | **Section cut lines** (A-A, B-B) with cut-plane markers + direction arrows + layer A-ANNO-SECT (rule 70) | 1 | `acad.sections.list_section_lines` |
| 12 | Lineweight            | **Plot style** (CTB/STB) in use with ACI 1-9 → 0.13-0.70 mm tiering (rule 61) | 1 | `acad.plotstyles.list_plotstyles` + layout config |
| 13 | Finishes legend       | **LEGENDA WYKOŃCZEŃ** table mapping F-xx / W-xx / C-xx codes to materials (rule 65 + schedules) | 1 | `acad.schedules.find_schedule_tables title~='LEGENDA'` |
| 14 | Orientation + scale   | **North arrow + scale bar + compass** at bottom-right of sheet, rule 69 | 1 | `acad.callouts.*` + vision |
| 15 | Reflected ceiling plan| Optional sheet RCP with luminaires / HVAC diffusers / smoke detectors on layer E-LITE / M-HVAC | 1 | `acad.layouts.list_layouts name~='RCP'` |
| 16 | Jamb / sill / lintel details | Blow-up details at 1:10 / 1:20 in paperspace viewport with tag like `DET-01` | 1 | `acad.blocks.list_blocks name~='DET-*'` + rule 28 |
| 17 | Room program fidelity | **Every labelled room** exists in the programme checklist (e.g. Hospital2026 has OR × 4, PACU × 2, etc.) and has correct area within ±10% of brief | 1 | `acad.schedules.list_room_labels` + checklist |

Threshold policy:

- **< 10/17** — concept sketch, not to be exported.
- **10-13/17** — technical study, OK for internal review but NOT for
  tender / pozwolenie na budowę.
- **14-15/17** — executive-grade but missing optional axis; sign-off
  allowed with remark.
- **16-17/17** — full rysunek wykonawczy; clear for export.

## 1a. Spatial-quality gate — criteria 18-20 (programmatic, PASS/FAIL, not vision-scored)

Added 2026-08-12, triggered by the "kulfon" incident: a Zone-0.2 test build scored well against
the 17-criterion rubric above (correct hatching, doors, dimensions, schedules...) while being a
single row of boxes along one corridor — no day/night split, no daylight logic, no room sized
from its furniture. **The 17-criterion rubric above evaluates drafting-standard compliance; it
does not evaluate whether the LAYOUT ITSELF is any good.** A drawing can score 17/17 and still be
a "kulfon."

Criteria 18-20 close that gap. They are deliberately kept OUT of the vision-LLM's 17-point score
(`ARCHITECT_REVIEW_CRITERIA` in `acadmcp_vision/schemas.py`) rather than appended as an 18th row
to it: unlike hatching-correctness or dimension-chain-presence, all three are objectively
checkable by a tool call against the drawing's own data — asking a vision LLM to eyeball them
would add guessing noise where a deterministic check already exists. They are a separate
PASS/FAIL gate, checked the same session a project claims to follow rule 73's space-planning
method, not scored 0/0.5/1 and not blended into the 17-criterion total.

| # | Criterion | Check | Evidence tool |
|---|---|---|---|
| 18 | Public/day-zone rooms reach the entry without crossing a private/night zone | Walk the adjacency graph (see #20) from the entry room; every room tagged `zone=public` or `zone=day` in the project's `ROOM-PROGRAM.md` must be reachable without transiting a room tagged `zone=private`/`zone=night` | `acad.openings.list_openings_in_model` (`roomFrom`/`roomTo` per door) + the project's own zone tags |
| 19 | Every room the programme declares as requiring daylight actually sits on an exterior wall with a window | For each room in `ROOM-PROGRAM.md` marked "wymaga światła dziennego" / daylight-required, confirm at least one of its boundary edges coincides with an exterior wall carrying ≥1 window | `acad.openings.list_openings_in_model kind=windows` matched against room boundary geometry |
| 20 | Built adjacency graph matches the declared `ROOM-PROGRAM.md` Adjacency table | Every "MUST be directly connected" pair in the typology's `ROOM-PROGRAM.md` Adjacency table has a real door between those two rooms in the drawing (`roomFrom`/`roomTo` on some door) | `acad.openings.list_openings_in_model` compared row-by-row against `docs/knowledge-base/<typology>/ROOM-PROGRAM.md`'s Adjacency table |

**Gate policy:** all three must PASS before a drawing is considered rule-73-compliant, independent
of its 17-criterion score. A 17/17 drawing that fails #18-20 is drafted correctly but planned
badly — do not let a high vision-rubric score substitute for actually checking these three. A
failure on any one is a build defect to fix (rearrange the zone/room in question), not a rubric
technicality to argue about.

See `docs/engineering-rules/73-space-planning-method.md` step 9 for when in the build sequence
this gate runs, and rule 71 for the broader project-intake process it fits into.

## 2. How the persona uses this rubric

The `senior-architect-reviewer` Vision persona (D11) receives the 17
criteria as a system prompt, scores each, and returns JSON:

```json
{
  "score": 15.5,
  "criteria": [
    { "id": 1, "label": "hatching", "score": 1.0, "note": "..." },
    { "id": 2, "label": "furniture", "score": 0.5, "note": "..." },
    ...
  ],
  "fatal_gaps": ["criterion 4 < 1.0", "criterion 8 < 1.0"],
  "verdict": "executive-grade"
}
```

Callers MUST treat `score < 15` as a blocker and re-run the corresponding
generator (`acad-hatches`, `acad-openings`, …) with fixes before
re-exporting PDF. This is the loop enforced by `acad_design_iterate`
when `qualityTarget >= 15.0` is set.

## 3. Mapping rubric criterion → generator → validator

For each criterion, the generator that FIXES a gap and the validator
rule that DETECTS a gap are named so the coding agent never has to
guess where to start:

| # | Fix with                                        | Detect with                                         |
|---|-------------------------------------------------|-----------------------------------------------------|
| 1 | `acad.hatches.apply_material_preset_by_point`   | `acad.validators.check_overlaps` + vision           |
| 2 | `acad.furniture.populate_room`                  | count `A-EQPM*` handles per room polygon            |
| 3 | `acad.plumbing.populate_bathroom`               | count `A-EQPM-SAN*` handles per WC room             |
| 4 | `acad.openings.insert_door` + `renumber_openings`| `acad.openings.list_openings_in_model kind=doors`  |
| 5 | `acad.openings.insert_window`                   | `acad.openings.list_openings_in_model kind=windows` |
| 6 | `acad.verticals.insert_stair` / `_elevator`     | `acad.verticals.list_*`                             |
| 7 | `acad.grids.draw_grid_from_spacings`            | `acad.grids.list_grid_axes`                         |
| 8 | `acad.dimensions.dim_chain_linear`              | `acad.validators.rule "dims.chain-present"`         |
| 9 | `acad.schedules.generate_*_schedule`            | `acad.schedules.find_schedule_tables`               |
| 10| `acad.callouts.insert_title_block / _north_arrow / _scale_bar / _section_callout / _detail_callout` | vision persona |
| 11| `acad.sections.insert_section_line`             | `acad.sections.list_section_lines`                  |
| 12| `acad.plotstyles.apply_plotstyle_to_layout`     | `acad.layouts.list_plot_styles`                     |
| 13| `acad.schedules.generate_finish_legend`         | `find_schedule_tables title~='LEGENDA'`             |
| 14| `acad.callouts.insert_north_arrow / _scale_bar` | vision persona                                      |
| 15| `acad.layouts.new_layout name='RCP'` + E/M layers| `acad.layouts.list_layouts`                        |
| 16| `acad.blocks.library_register` + viewport       | `acad.blocks.list_blocks name~='DET-*'`             |
| 17| schedule + programme checklist                  | `acad.schedules.list_room_labels` + external rubric |

## 4. Relation to other rules

- Rule **32** (`acad-vision-traps`): technical rules for the vision
  sidecar & persona pipeline. Rule 60 defines WHAT the persona scores.
- Rule **62 / 63 / 64 / 65**: per-category rubrics referenced by
  criterion 1 (hatches), 2 (furniture), 3 (plumbing), 4-5 (openings).
- Rule **66 / 67 / 69 / 70**: dimensioning, grids, callouts, sections —
  referenced by criterion 7, 8, 10, 11.
- Rule **61**: lineweight policy — referenced by criterion 12.
- Rule **50** (task flow): architectural tasks that touch ≥ 5 criteria
  MUST cite this rule in the task description ("target ≥ 15/17").
- Rule **73** (space-planning method): defines the 9-step sequence that criteria 18-20 (§1a) are
  the exit check for — a project that skipped rule 73's zone-first steps is exactly the shape of
  project #18-20 are designed to catch.

## 5. Additions / removals

DO NOT add an 18th criterion without:

1. Extending `docs/PLAN-PROFESSIONAL-UPGRADE-2026.md §2` with the new
   gap (priority + evidence).
2. Updating the Vision persona prompt file (`src/AcadMcp.Vision/
   personas/senior-architect-reviewer.{md,json}`).
3. Regenerating the rubric JSON used by `/v1/architect-review` endpoint.
4. Updating the threshold table above (new denominator, new cut-offs).

A criterion can be **removed** only when its generator + validator are
both deleted (e.g. if Poland permanently drops a compliance rule).

Criteria 18-20 (§1a) are exempt from steps 2-3 above (they are not in
`ARCHITECT_REVIEW_CRITERIA` / the vision persona prompt / the `/v1/architect-review` JSON schema
by design — see §1a's own rationale) but still require step 1 (record the gap) and step 4 (this
document) when changed. A 21st spatial-quality criterion follows §1a's own pattern, not the
vision-rubric's steps 1-4.
