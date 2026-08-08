# Coverage roadmap — from 340 tools to the whole of AutoCAD

What this repository does **not** yet expose, organised into buildable phases.

Written after a tool-by-tool live verification of all 340 existing tools against a running
AutoCAD 2025 (see [PHASE-7-STATUS.md](PHASE-7-STATUS.md) and the `fix:` commits from that
sweep). The gaps below were established by matching every one of the 337 unique tool names
in `toolbank-manifests/` against AutoCAD's command and API surface, not from memory.

**Target: complete coverage.** Tool count is not a constraint — the discovery layer costs a
fixed number of tokens whether the registry holds 340 tools or 1,400, which is the entire
reason this architecture exists. The constraint is build effort and verification effort, so
the phases below are ordered by what unblocks real work first.

---

## Where the bank stands today

**546 tools across 39 categories** (updated 2026-08-08; this document was written at 337 across
31). Coverage is close to complete for one thing: *drawing and annotating a 2D production
sheet*, and after Phase 1 also for referencing, coordinate systems and sheet control — see
[Totals](#totals) for exactly how far Phase 1 actually got, which is less than "done".

| Solid | |
|---|---|
| 2D geometry, modify/transform | 33 + 19 |
| 3D primitives + booleans | 16 + 8 |
| Layers (incl. states), blocks, selection | 14 + 17 + 12 |
| Text, annotations, dimensions | 12 + 18 |
| Hatches, layouts, viewports (basic), files | 8 + 10 + 11 |
| 13 discipline packs (arch, mech, civil, elec, plumbing, furniture, openings, sections, callouts, schedules, verticals, grids, validators) | ~150 |

What is missing is not "more of the same". It is five distinct capability classes: **reference
management, coordinate systems, style authoring, advanced 3D, and automation escape hatches** —
plus the sheet-production machinery that turns model geometry into an issued drawing set.

---

## Phase 1 — Blocking a real project (≈105 tools)

Without these, an agent cannot produce a multi-discipline drawing set at all, regardless of how
good the geometry tools are. Highest value per unit of work in the whole roadmap.

### 1.1 `acad-xrefs` — external references (≈26)

No real building project exists in one file. Today the bank cannot open one.

```
attach_xref                 attach_xref_overlay        detach_xref
reload_xref                 unload_xref                bind_xref
bind_xref_insert            list_xrefs                 get_xref_info
set_xref_path               repath_all_xrefs           find_missing_xrefs
clip_xref_rect              clip_xref_polygonal        clip_xref_by_object
invert_xref_clip            delete_xref_clip           set_xref_clip_display
list_nested_xrefs           list_xref_dependent_symbols
set_xref_layer_override     reset_xref_layer_overrides
refedit_begin               refedit_save               refedit_discard
set_xref_demand_load
```

**Traps to expect:** `refedit_*` is a modal, stateful command sequence — the family most likely
to hit the queued-command deadlock documented in rule 15 and PHASE-7. Prefer the managed
`XrefGraph` / `Database.ResolveXrefs` API and treat `REFEDIT` as a last resort. Binding rewrites
symbol table names (`file|LAYER` → `file$0$LAYER`); every layer/block tool must keep working
afterwards, so this needs a regression test.

### 1.2 `acad-ucs` — coordinate systems (≈18)

Every tool in the bank today works in WCS. Anything rotated, inclined or sectioned is
unreachable.

```
create_ucs_3point           create_ucs_origin          create_ucs_zaxis
create_ucs_from_object      create_ucs_from_view       create_ucs_from_face
rotate_ucs_x                rotate_ucs_y               rotate_ucs_z
set_ucs_world               set_ucs_previous           save_named_ucs
restore_named_ucs           list_ucs                   delete_named_ucs
get_current_ucs             transform_point            set_ucs_per_viewport
```

**Design decision needed up front:** every existing drawing tool takes WCS points. Adding a UCS
means deciding whether they gain an optional `coordinateSystem` argument or whether UCS is
purely a view-level concept. Doing this *after* the phase is built would be a breaking change
across 340 tools — decide first, in `docs/engineering-rules/`.

### 1.3 `acad-viewports` — sheet control (≈24)

`create_viewport` and `set_viewport_scale` exist. Everything that makes a viewport *useful on an
issued sheet* does not.

```
set_viewport_layer_freeze       set_viewport_layer_thaw
list_viewport_layer_overrides   clear_viewport_overrides
set_vp_layer_color_override     set_vp_layer_linetype_override
set_vp_layer_lineweight_override set_vp_layer_transparency_override
lock_viewport                   unlock_viewport
create_polygonal_viewport       clip_viewport_by_object
set_viewport_ucs                set_viewport_view_direction
set_viewport_twist              set_viewport_shade_plot
set_viewport_annotation_scale   sync_viewport_to_annotation_scale
list_viewports_on_layout        delete_viewport
set_viewport_visual_style       set_viewport_render_mode
maximize_viewport               get_viewport_extents_in_model
```

**Why it matters:** per-viewport layer freeze is how one model serves an architectural plan, a
fire plan and a furniture plan. Without it every "view" needs its own geometry.

### 1.4 `acad-fields` — dynamic content (≈16)

A title block whose date, sheet number and areas are static text is wrong the moment anything
changes.

```
insert_field_date               insert_field_filename
insert_field_sheet_number       insert_field_sheet_set_property
insert_field_object_property    insert_field_area
insert_field_formula            insert_field_system_variable
insert_field_plot_info          insert_field_block_attribute
list_fields                     update_fields
convert_field_to_text           set_field_format
get_field_expression            set_field_evaluation_mode
```

**Direct payoff:** `callouts.insert_title_block` and the `schedules.*` family currently write
frozen strings. Fields make them self-maintaining, and make
`schedules.correct_all_room_areas` largely unnecessary.

### 1.5 `acad-annotative` — annotation scaling (≈14)

```
set_annotative                  add_annotation_scale
remove_annotation_scale         list_object_annotation_scales
set_current_annotation_scale    sync_scale_positions
set_scale_position_for_scale    list_scale_list
add_scale_to_list               delete_scale_from_list
reset_scale_list                set_annotation_visibility
set_paperspace_scale_link       list_objects_by_annotation_scale
```

**Phase 1 total: ≈98 tools.**

---

## Phase 2 — Issuing the set (≈80 tools)

Turning a finished model into deliverables.

### 2.1 `acad-sheetsets` — sheet set manager (≈24 → **23 planned**, **23 built and verified**) — COMPLETE

**Revised 2026-08-04 after checking the API rather than assuming it.** The original list was
written from the AutoCAD feature set, not from what is reachable, and three things about it were
wrong.

**It is COM, and the roadmap never said so.** There is no `DatabaseServices` API for sheet sets.
`acmgd.dll` exposes `IAcSmSheetSetMgr` / `IAcSmSheetSet` and `AcSmComponents.Interop.dll` sits
next to it, so this *is* reachable — but only through COM interop, which means a new assembly
reference on `$(AcadInstallPath)`, COM lifetime management, and a different error model from
every other category in this bank.

That matters more here than it would elsewhere. This repository already forbids
`Marshal.GetActiveObject` in its pre-commit gate, keeps a whole `AcadMcp.ComBridge` project for
the COM path it could not avoid, and has a documented history of the command and COM layers
failing opaquely. **Sheet sets should be the first category built with a supervised contract for
COM, written before the code** — the same way rule 43 preceded `acad-ucs` and rule 44 preceded
page setups.

**That contract now exists: [rule 45](engineering-rules/45-sheet-sets-com.md), written 2026-08-05
from `AcSmComponents.Interop` metadata rather than from documentation.** The COM surface is
confirmed present — 134 types, all six interfaces this needs. Two things the contract settles that
change the plan:

* **`open_sheet_set` / `close_sheet_set` are not built.** `IAcSmSheetSetMgr.FindOpenDatabase(path)`
  means every tool can take the `.DST` path and resolve it per call, so no handle is held across
  calls and the "what if a second client opens a different set" question never arises. 23 tools,
  not 25 — and the two that go are the two the roadmap was most worried about.
* **First tranche is read-only and unblocks something already shipped.**
  `fields.insert_field_sheet_set_property` exists and is blocked on `get_sheet_property`, so the
  six read tools come first: they need none of the `Save()` discipline and they turn a dead field
  live.

**Four entries are somebody else's job:**

| Entry | Belongs to |
|---|---|
| `publish_sheet_set` | 2.2 — it is a publish operation that happens to take a sheet set |
| `create_sheet_list_table`, `update_sheet_list_table` | `acad-schedules` already generates AutoCAD Table entities; a sheet list is one more schedule, not a new mechanism |
| `archive_sheet_set`, `etransmit_sheet_set` | eTransmit is a separate subsystem with its own packaging rules; it deserves its own tranche rather than two tools smuggled in here |

**Two are session state, and this bank has been burned by that shape before:**
`open_sheet_set` / `close_sheet_set` hold a handle across calls. Every stateful thing attempted
so far — `refedit_*`, the plot queue, `undo` — either failed or had to be withdrawn. If they are
built, the contract has to say what happens when a second client opens a different set.

**Missing, and they are not optional:** `rename_sheet`, `set_sheet_number`, `list_subsets`,
`delete_subset`, `get_sheet_set_path`. A sheet set whose sheets cannot be renumbered is not
usable on a real project, and renumbering is the single most common sheet-set operation there is.

**Cross-dependency the original list did not record:** `fields.insert_field_sheet_set_property`
is blocked on this category and is the reason title blocks cannot yet carry live sheet-set data.
Whatever subset of 2.1 gets built first must include `get_sheet_property`, or that field stays
blocked for nothing.

```
create_sheet_set            open_sheet_set             close_sheet_set
get_sheet_set_info          get_sheet_set_path         set_sheet_set_template
list_sheets                 add_sheet                  remove_sheet
rename_sheet                set_sheet_number           reorder_sheet
create_subset               list_subsets               delete_subset
move_sheet_to_subset        set_sheet_property         get_sheet_property
list_custom_properties      define_custom_property     resave_all_sheets
list_sheet_views            add_sheet_view             set_sheet_view_category
```

### 2.2 `acad-publish` — batch output (≈16 → **9 planned**, 4 built)

**Revised 2026-08-04.** Built and verified: `create_page_setup`, `list_page_setups`,
`apply_page_setup`, `delete_page_setup`. Withheld: `import_page_setup` (see
[KNOWN-GAPS.md](KNOWN-GAPS.md) section B — `WblockCloneObjects` reports success and clones
nothing).

**The good news the original list did not know:** `Publisher`, `DsdData`, `DsdEntry`,
`PlotProgressDialog`, `PlotConfigManager` and `PlotStamp` are all in `accoremgd.dll`. The
multi-sheet publish family is plain managed API, not COM. Nothing here needs the treatment 2.1
needs.

**Four entries collapse into two.** `publish_layouts`, `publish_to_pdf_multisheet`,
`publish_to_dwf` and `publish_to_plotter` are one mechanism with a parameter. Worse,
`files.export_file` already plots a single layout to PDF, DWF, DWFX, PNG and JPG and does it
well. The only thing genuinely missing is **many layouts into one file**, which `export_file`
cannot do — its `layout` argument is singular. So:

- `publish_sheets` — many layouts, one output file or one device, format as an argument
- `set_publish_options` — the DSD-level settings that publish reads

**`export_layout_to_model` is misfiled.** It is the EXPORTLAYOUT command: it turns a layout's
paper space into model space in a *new drawing*. That is a file operation, not a publish one,
and it belongs in `acad-files` or `acad-layouts`.

**Two entries contradict a decision this bank already made.** `get_plot_status` and
`cancel_plot` only mean anything with background plotting on — and `files.export_file` forces
`BACKGROUNDPLOT` **off** deliberately, because the background worker writes the file *after*
`EndPlot` returns (measured: PNG ~10 s late, PDF ~5 s late) and holds the engine long enough to
reject the next export. Building a status/cancel pair for a mode the bank switches off would be
building tools for a situation that does not arise. Withdrawn unless background plotting is ever
deliberately re-enabled, which would need its own contract.

**`plot_preview_extents` as specced is interactive.** A preview is something a human looks at.
The useful agent-shaped question is "what area will this plot cover", which is answerable from
the page setup and the layout without rendering anything — kept, but as
`get_plot_area` rather than as a preview.

```
create_page_setup           list_page_setups           apply_page_setup      (built)
delete_page_setup           import_page_setup                                (built / withheld)
publish_sheets              set_publish_options        set_plot_stamp
get_plot_area
```

### 2.3 `acad-styles` — style authoring — **COMPLETE**. 32 built + `draw_mline`, one withheld.

Today: dimension, multileader and table style authoring (18 tools, all live-verified). Text
styles live in `acad-annotations`, plot styles in `acad-plotstyles`.

**Revised 2026-08-05 against the managed API**, the same way 2.1 and 2.2 were. Every type below
was confirmed present or absent by reading `acdbmgd` / `acmgd` / `accoremgd` metadata directly,
not from memory. Three entries in the original list were wrong.

| Group | Tools | API |
|---|---|---|
| Dimension styles | `apply_dimstyle_override` `list_dimstyle_overrides` `import_dimstyle_from_dwg` | `Dimension.GetDimstyleData()` / `SetDimstyleData()`; `IdMapping` + `DuplicateRecordCloning` |
| Table styles | `set_table_cell_style` — alignment and colours **only** | `TableStyle.SetAlignment` / `SetColor` / `SetBackgroundColor`, per `RowType`. Text height is deliberately excluded: `modify_tablestyle` already owns it as `titleTextHeight` / `headerTextHeight` / `dataTextHeight`, and two tools writing one property is how they drift apart. |
| Multiline styles | `create_mlinestyle` `modify_mlinestyle` `list_mlinestyles` | `MlineStyle` — `Elements`, `StartAngle`, `EndAngle`, `Filled`, `FillColor`, caps, `ShowMiters` |
| Layer filters | `create_layer_filter` `create_layer_group_filter` `list_layer_filters` `delete_layer_filter` — **`apply_layer_filter` withheld** | `LayerManager.LayerFilter`, `LayerFilterTree`, `LayerGroup`. `LayerFilterTree.Current` is get-only and no type sets it: which filter the palette shows is UI state. |
| Visual styles | `list_visual_styles` `create_visual_style` | `DBVisualStyle` — see the caveat below |
| Point display | `set_point_display` | PDMODE / PDSIZE system variables |

**What the original list got wrong.**

* **`create_point_style` and `set_point_display_mode` are one tool, not two, and neither is a
  style.** There is no `PointStyle` type in the managed API — point display is the PDMODE and
  PDSIZE system variables, which are global to the drawing. Shipping a `create_point_style` would
  over-promise exactly the way `set_xref_clip_display` does in KNOWN-GAPS (A3). One honest tool:
  `set_point_display(mode, size)`. Note both are **Int16** sysvars, so they need the `(short)`
  cast or they throw `eInvalidInput` — see rule 26.
* **`list_dimstyles` is already built — in `acad-dimensions`.** It was briefly added to this list
  as a gap, wrongly: a grep across every manifest found it, alongside `set_entity_dimstyle` and
  `ensure_architectural_dimstyle`. Leaving it here would have shipped a second tool doing the
  same job under the same name in a different category, which is the one thing a discovery layer
  cannot disambiguate for an agent. **Check the whole bank, not just the category you are working
  in, before calling something missing.**
* **Visual styles are already half-covered too.** `acad-viewports` has `set_viewport_visual_style`
  — applying one. What is absent is authoring and enumeration, which is what this section adds.
* **`draw_mline` is pulled forward from 3.1 into this tranche.** The roadmap put the MLINE style
  tools in phase 2.3 and the tool that draws with them in phase 3.1, which would have shipped a
  style nothing in the bank can apply — unusable by an agent and, worse, impossible to verify by
  sight, since there would be no way to put one on screen. One tool moves; the rest of 3.1 stays
  where it is. **A phase boundary that separates a definition from its only consumer is a
  sequencing error, not a scope decision.**
* **`create_visual_style` is not equivalent to its siblings.** `DBVisualStyle` exposes only
  `Name`, `Description`, `Type` and an untyped `SetTrait` / `GetTrait` / `SetTraitFlag` pair —
  there is no property surface to author against, unlike `MlineStyle` or `TableStyle`. Build
  `list_visual_styles` first (safe: enumerate the dictionary), and treat `create_visual_style` as
  preset-based — derive from a `VisualStyleType` and override named traits — rather than
  pretending to expose the whole trait model. If the trait API does not behave, withhold it with
  a reason rather than shipping something that returns success and changes nothing.

### 2.4 CAD management — **COMPLETE**. 9 built, no `acad-standards` category, 5 struck.

**Revised 2026-08-05 against the managed API and against the rest of this bank.** This section
was the worst-planned in Phase 2: ten of its fourteen tools are unbuildable, and eight of those
ten already exist under different names.

**The DWS half does not exist in managed code, and is already covered anyway.** `StandardsAudit`,
`AcStMgr`, `StandardsPlugin` and `DwsFile` are all absent from `acdbmgd` / `acmgd` / `accoremgd` —
AutoCAD's CAD Standards feature (DWS files, STANDARDS, CHECKSTANDARDS) is ObjectARX and COM only.
That alone would make these eight a supervised-COM project like 2.1. But they should not be built
at all, because **`acad-validators` already does every one of them**, in managed code, with rules
an agent can read:

| Planned here | Already in `acad-validators` |
|---|---|
| `create_dws_standard` | YAML rule files under `validators/_standards/` |
| `associate_standard` / `list_associated_standards` | `list_standards` |
| `check_standards` / `batch_standards_audit` | `validate_drawing`, `validate_against_standard` |
| `list_standards_violations` | `list_violations` |
| `fix_standards_violation` | `auto_fix_violations` |
| `configure_standards_plugin` | `add_validator_rule`, `reload_validator_rules` |

A second standards system reachable only through COM, duplicating one that already ships and
already carries `polish-arch-baseline`, would be worse than nothing: an agent would have two
tools for one question and no way to choose. **Struck.**

**Layer translation is unreachable too.** `LayerTranslation` is absent from the managed
assemblies; LAYTRANS is a command with no exposed mapping API. Two tools struck.

**What is real, and where it belongs.** Nothing here justifies a new category — the four
surviving tools are layer-state and document-level work, and both already have owners:

| Category | Tools | API |
|---|---|---|
| `acad-layers` (has 4 layer-state tools) | `export_layer_state` `import_layer_state` `delete_layer_state` `rename_layer_state` `set_layer_state_description` `compare_layer_state` | `LayerStateManager.ExportLayerState` / `ImportLayerState` / `DeleteLayerState` / `RenameLayerState` / `SetLayerStateDescription` / `CompareLayerStateToDb` |
| `acad-files` | `list_drawing_properties` `set_drawing_properties` `set_drawing_custom_property` | `DatabaseSummaryInfo`, `DatabaseSummaryInfoBuilder` — title, subject, author, keywords, comments, revision, hyperlink base, plus arbitrary custom properties |

Nine tools, not fourteen, and no new category. The extra layer-state tools beyond the two
planned come from the same `LayerStateManager` the export/import pair lives on: an agent that can
save and restore a layer state but cannot delete, rename or describe one is stuck with whatever
it created first.

**Phase 2 total: ≈84 tools.**

---

## Phases 3–5 — API review, 2026-08-05

**Every phase so far had planning errors that only a check against the managed API found**, so
phases 3, 4 and 5 were reviewed the same way before any of them starts: every type each group
needs was looked up in `acdbmgd` / `acmgd` / `accoremgd` metadata, and every planned tool name was
grepped against all 38 manifests. Two findings change the numbers.

### Finding 1 — six of Phase 4.1's tools already exist, and `acad-modify` is already 3D

The roadmap lists `move_3d`, `rotate_3d`, `mirror_3d`, `align_3d`, `array_3d_rectangular` and
`array_3d_polar` as new 3D work. They are not new. `acad-modify` shipped 3D-capable:

| Planned in 4.1 | Already in `acad-modify` | What it says |
|---|---|---|
| `move_3d` | `move` | "by the vector from→to (**WCS**)" |
| `rotate_3d` | `rotate` | "**Optional axis vector for 3D rotations** (default Z)" |
| `mirror_3d` | `mirror` | "through a **plane defined by point + normal (3D)**" |
| `align_3d` | `align` | source point pair onto target point pair, optional scale |
| `array_3d_rectangular` | `array_rectangular` | "rows × cols × **levels** … optional **Z level spacing**" |
| `array_3d_polar` | `array_polar` | polar around a centre — the only one where a 3D axis argument may genuinely be missing |

This is the 2.4 pattern again: a phase planned from the AutoCAD *feature list* rather than from
what this bank already has. Five struck outright; `array_polar` gets an axis argument if it turns
out to lack one, which is an argument, not a tool.

### Finding 2 — what has no managed API at all

Confirmed absent from all three assemblies, so unbuildable without COM or the command channel:

| Tool | Why |
|---|---|
| `arc_aligned_text` (3.3) | Express Tools object, not in the core API |
| `spell_check` (3.3) | no `SpellCheck` type of any kind |
| `import_pdf_as_geometry`, `set_pdf_import_options` (3.5) | no importer type; PDFIMPORT is a command. `PdfReference` handles *attaching* an underlay, which is a different thing |
| `run_data_extraction`, `create_data_extraction_template` (5.2) | DATAEXTRACTION is a wizard |
| `define_property_set`, `attach_property_set`, `list_property_sets` (5.2) | **AEC/Architecture vertical only.** This bank targets vanilla AutoCAD; a tool that works on one machine and not the next is worse than an absent one |

That is **10 struck**, plus the 5 duplicates: **15 fewer than planned**.

### What the review confirmed as buildable

Almost everything else, and more solidly than expected. `Solid3d` alone covers most of 4.1 in
managed code — `ExtrudeFaces`, `TaperFaces`, `OffsetFaces`, `RemoveFaces`, `TransformFaces`,
`CopyFace`, `CopyEdge`, `ShellBody`, `SeparateBody`, `CleanBody`, `CheckInterference`,
`ImprintEntity`, `FilletEdges`, `ChamferEdges`, `Slice`, `CreateSweptSolid`, `CreateLoftedSolid`,
`CreateSculptedSolid` — so the SOLIDEDIT family does **not** need the command channel, which was
the main risk in Phase 4. `NurbSurface`, `SubDMesh`, `Section` + `SectionSettings`, `PointCloudEx`,
`Xrecord`, `RegAppTable`, `DataLink`, `GeoLocationData`, `ResultBuffer` + `TypedValue` (for 5.1)
and `RasterImage` / `UnderlayReference` + `PdfReference` / `DgnReference` / `DwfReference` are all
present.

### One naming collision to settle before 4.4 starts

`acad-sections` already exists and is **2D drafting symbols** — section lines, elevation markers,
titles. Phase 4.4 is **3D `SECTIONPLANE` objects**, a completely different mechanism. Both are
worth having, but `list_section_lines` next to `list_section_planes` is exactly the pair a
discovery layer cannot disambiguate for an agent. Name the 3D group explicitly — `acad-sections-3d`
with every tool carrying `_section_plane` — or merge the two under one category with unmistakable
prose. Decide before writing, not after.

### Revised totals

| Phase | Planned | After review | Note |
|---|---:|---:|---|
| 3 | 96 | **90** | 6 struck: arc_aligned_text, spell_check, 2 PDF-import, and 2 that need re-scoping against `filter_entities` |
| 4 | 92 | **86** | 5 duplicates of `acad-modify`, 1 argument rather than a tool |
| 5 | 66 | **61** | 5 struck: 2 data-extraction, 3 AEC property sets |
| | **254** | **237** | |

---

## Phase 3 — Filling the 2D gaps (≈95 → **90** after review)

The existing 2D coverage is broad but not complete. These are commands a draughtsman uses daily.

### 3.1 `acad-geometry-2d` extensions (≈30 → **29 built, 2 struck**) — COMPLETE

```
draw_mline ✔                edit_mline_vertex ✔        mline_join ✔
draw_spline_cv ✔            edit_spline_fit_point ✔    spline_to_polyline ✔
fit_polyline ✔              edit_polyline_vertex ✔     polyline_add_vertex ✔
polyline_remove_vertex ✔    set_polyline_width ✔       reverse_curve ✔
break_at_point ✔            break_between_points ✔     lengthen_curve ✔
stretch_window ✔            align_objects ✘            scale_by_reference ✔
rotate_by_reference ✔       divide_object ✔            measure_object ✔
blend_curves ✔              boundary_from_point ✔      region_from_boundary ✔
create_wipeout ✔            set_wipeout_frame ✔        set_draworder ✔
set_object_transparency ✔   draw_ellipse_arc ✔         draw_construction_geometry ✘
```

Two tools were added that this list did not plan for, both because a visual check showed the
planned ones were unusable without them: `set_point_style`, because `divide_object` and
`measure_object` place DBPoints that draw as a single pixel at the default PDMODE and therefore
looked like nothing had happened; and `list_polyline_vertices`, because every other polyline
tool takes a vertex index and nothing could report what the indices were.

`scale_by_reference` and `rotate_by_reference` went into `acad-modify` rather than here, next to
the plain `scale` and `rotate` they are the reference-driven forms of.

**Two entries are struck as duplicates**, not deferred — both were listed here by a name the
bank does not use, and a scan for the literal name reported them missing when the capability
was already there.

- `draw_construction_geometry`: AutoCAD has exactly two construction entities, XLINE and RAY,
  and `draw_xline` and `draw_ray` already draw both. A third tool over the same two classes
  would give the router two ways to spell one action. What is genuinely NOT covered is the
  XLINE command's Bisect and Offset modes — ways of computing a base point and direction, not
  new geometry — recorded in [KNOWN-GAPS](KNOWN-GAPS.md) §B rather than left implied by a tick.
- `align_objects`: **`modify.align` has existed since the original 18** and maps a source point
  pair onto a target pair with optional scaling, which is the whole of ALIGN. Writing the
  replacement is what found a real bug in it: the rotation axis came from the cross product of
  the two directions, which vanishes when they are PARALLEL — covering both "already aligned"
  and "exactly reversed". A 180° align therefore moved nothing, turned nothing and returned
  `affected: 1`. Measured against a 90° control, fixed, and `align` now reports the angle, the
  factor and where source B landed instead of only a count. Verified live 32/32.

Three tools from this phase were **withdrawn after measurement** rather than shipped
approximate — see KNOWN-GAPS §B for the numbers: `blend_curves` `continuity=smooth`,
`set_object_transparency` `byLayer`/`byBlock`, and (from 2.1) `add_sheet_view`.

### 3.2 `acad-dimensions` extensions (≈14 → **8 built, 6 blocked by the API**) — COMPLETE

```
dimension_jogged_radius ✔   dimension_jog_linear ✘     dimension_break ✘
dimension_space ✔           dimension_inspect ✘        dimension_reassociate ✘
dimension_oblique ✔         dimension_center_mark ✘    dimension_centerline ✘
edit_dimension_text ✔       dimension_update ✔         quick_dimension ✔
dimension_arc_symbol ✔      dimension_tolerance ✔
```

`quick_dimension` is the one that could not be reduced to the chain tools already here: those
are handed a list of points, and this works them out from the geometry. Its trap is duplicates —
three walls laid end to end give **six** key points of which only **four** are distinct, and a
chain built without merging them carries zero-length dimensions that draw as nothing and still
count. Both `pointsFound` and `pointsUsed` are reported, and in continuous mode the measurements
are checked to sum to the geometry's own span, so a dropped or doubled dimension fails instead
of leaving a plausible list of numbers.

Before any of this was written, the 2025 managed assemblies were **asked what they contain**,
by compiling a throwaway file that names every type and property the phase would need. That is
the authoritative answer and it splits the list in two — the four marked ✘ are not a question of
effort:

| present | absent |
|---|---|
| `RadialDimensionLarge`, `RotatedDimension.Oblique`, `AlignedDimension.Oblique`, `DimensionText`, `TextPosition`, `TextRotation`, `Dimtol`/`Dimlim`/`Dimtp`/`Dimtm`/`Dimtdec`, `ArcDimension.ArcSymbolType`, `DimLinePoint`, `GetDimstyleData`/`SetDimstyleData` | `CenterMark`, `CenterLine` (types), `Inspection*` (4), `JogSymbolHeight`/`JogSymbolPosition`, `Dimbreak`, `Dimassoc` and the `DimAssoc` type |

`dimension_update` was nearly struck as a second name for `set_entity_dimstyle`. It is not, and
the difference was measured rather than argued: a tolerance override was put on two identical
dimensions, one sent through each tool. `set_entity_dimstyle` **left the override standing** —
it only assigns `DimensionStyle` — while `dimension_update` re-applies the style's own values
through `SetDimstyleData` and clears it. The tool reports `toleranceOverrideBefore` and
`toleranceOverrideAfter` for every dimension so that difference stays checkable from outside.

The first run of that experiment was **void and nearly passed anyway**: it hardcoded the style
name `Standard`, which a metric template does not have, so the control arm failed and only the
`dimension_update` arm ran. "The override is gone" then looked like proof of a difference while
demonstrating nothing about the other tool.

`Dimension.Oblique` does not exist on the base class either — only the two linear kinds carry
it, which is why `dimension_oblique` refuses radial, diametric and angular dimensions by name.
The four absent ones are recorded in [KNOWN-GAPS](KNOWN-GAPS.md) §B with the compiler errors.

A first attempt at reading the assembly through PowerShell reported everything as absent. That
output was **discarded, not used**: the assembly had failed to load, so every "absent" was an
artefact of the failure rather than a finding.

### 3.3 `acad-annotations` extensions (≈18 → **14 built, 3 blocked, 1 struck**) — COMPLETE

```
find_replace_text ✔         spell_check ✘              text_to_mtext ✔
mtext_column_settings ✔     arc_aligned_text ✘         set_text_justification ✔
scale_text_in_place ✔       justify_text ✘             background_mask_mtext ✔
mtext_bullets_numbering ✔   insert_symbol ✔            stack_fraction ✔
set_paragraph_format ✔      text_fit ✔                 list_text_by_pattern ✔
export_text_content ✔       set_mtext_frame ✘          explode_mtext_to_text ✔
```

Asked of the compiler before anything was written, as in 3.2. **`ArcAlignedText` and
`SpellChecker` do not exist** in the 2025 managed assemblies — both are express-tool / UI
features — so those two are struck rather than deferred. Everything else on the list resolves:
`DBText.Justify`, `AlignmentPoint`, `WidthFactor`, `Oblique`, `AdjustAlignment`; `MText.Attachment`,
`BackgroundFill`, `BackgroundScaleFactor`, `ColumnType`, `ColumnCount`, `ColumnWidth`,
`ColumnGutterWidth`, `LineSpacingFactor`. `MText.ExplodeFragments` exists but returns `void` —
it walks fragments rather than exploding, so `explode_mtext_to_text` will use `Explode()`.

The first tranche is the three that share a scanner, because that is where the difficulty is:
**text lives in six places**, and one reading only DBText and MText misses most of a real sheet.
A `Table` derives from `BlockReference`, so its case has to be taken first or schedule text is
never scanned at all — caught by the compiler calling the table branch unreachable.

`justify_text` is **struck as a duplicate** of `set_text_justification` — the roadmap listed the
same operation twice.

The second tranche's three tools share a failure mode: the obvious implementation MOVES THE TEXT
and reports success anyway. `set_text_justification` proved it. A first version computed where
the anchor ought to go from the extents box, and `BottomRight` moved the text by exactly the
descender depth while `BaseLeft` threw `eNotApplicable` — the default justification uses
`Position`, not `AlignmentPoint`. The box says where the ink is, not where a justification line
is. The displacement is now measured and undone rather than predicted.


### 3.4 `acad-selection` extensions (≈12)

```
quick_select_by_property    create_selection_filter    apply_saved_filter
select_similar              select_previous            select_last
isolate_objects             hide_objects               unisolate_objects
select_by_area_range        select_by_length_range     select_duplicates
```

### 3.5 `acad-images` / `acad-underlays` (≈22)

```
attach_image                list_images                detach_image
clip_image                  set_image_transparency     set_image_adjust
set_image_frame             reorder_image_draworder    set_image_path
attach_pdf_underlay         attach_dgn_underlay        attach_dwf_underlay
list_underlays              detach_underlay            clip_underlay
set_underlay_contrast       set_underlay_monochrome    list_underlay_layers
set_underlay_layer_visibility import_pdf_as_geometry   set_pdf_import_options
bind_underlay
```

**Phase 3 total: ≈96 tools.**

---

## Phase 4 — Real 3D (≈95 → **86** after review)

Today: 7 primitives, extrude, revolve, one planar surface, 6 booleans, 5 queries. That is the
beginning of 3D, not 3D.

### 4.1 `acad-solids-advanced` (≈34)

```
sweep_curve                 loft_curves                loft_with_guides
loft_with_path              draw_helix                 draw_polysolid
presspull                   slice_solid                separate_solids
shell_solid                 clean_solid                check_solid
imprint_edges               extract_edges              interfere_solids
fillet_edge                 chamfer_edge
extrude_face                move_face                  rotate_face
offset_face                 taper_face                 delete_face
copy_face                   color_face
align_3d                    move_3d                    rotate_3d
mirror_3d                   array_3d_rectangular       array_3d_polar
array_path                  convert_to_solid           convert_to_surface
```

### 4.2 `acad-surfaces` (≈18)

```
create_nurbs_surface        surface_blend              surface_patch
surface_network             surface_trim               surface_untrim
surface_extend              surface_fillet             surface_offset
surface_sculpt              project_geometry_to_surface set_surface_associativity
convert_to_nurbs            rebuild_nurbs              show_cv
edit_cv                     surface_curvature_analysis surface_draft_analysis
```

### 4.3 `acad-mesh` (≈16)

```
create_mesh_box             create_mesh_sphere         create_mesh_cylinder
create_mesh_cone            create_mesh_torus          create_mesh_pyramid
create_mesh_wedge           smooth_mesh_more           smooth_mesh_less
refine_mesh                 add_mesh_crease            remove_mesh_crease
split_mesh_face             extrude_mesh_face          convert_mesh_to_solid
convert_mesh_to_surface
```

### 4.4 `acad-sections-3d` (≈12)

```
create_section_plane        create_section_from_object create_section_orthographic
set_section_state           set_section_live           toggle_live_section
generate_section_block      generate_section_2d        generate_section_3d
set_section_settings        list_section_planes        add_section_jog
```

### 4.5 `acad-pointclouds` (≈12)

```
attach_point_cloud          list_point_clouds          detach_point_cloud
clip_point_cloud            invert_pointcloud_clip     set_pointcloud_density
set_pointcloud_colormap     set_pointcloud_stylization extract_pointcloud_section
pointcloud_to_geometry      set_pointcloud_crop_state  get_pointcloud_info
```

**Phase 4 total: ≈92 tools.**

---

## Phase 5 — Data, extensibility, escape hatches (≈65 → **61** after review)

### 5.1 `acad-lisp` — the highest-leverage small phase (≈12)

```
eval_lisp                   load_lisp_file             list_loaded_lisp
run_script_file             run_command_sequence       define_command_alias
netload_assembly            list_loaded_applications   get_system_variable
set_system_variable         list_system_variables      purge_regapps
```

**Why this one first within Phase 5:** `AcadMcp.Lisp` already exists in the solution and is not
exposed by a single MCP tool. One `eval_lisp` gives an agent a fallback route to almost every
gap in this document while the dedicated tools are still being written.

**Serious caveat:** this is the same command layer that produced `eInvalidInput` in
`zoom_extents` / `zoom_all` and the silent queueing in `undo` / `redo`. It must run through a
supervised channel with a timeout and an explicit "the result of a queued command cannot be
observed" contract — see the honest-result pattern now used by `modify.undo`.

### 5.2 `acad-data` — xdata, dictionaries, data links (≈28)

```
attach_xdata                get_xdata                  delete_xdata
register_app_name           list_registered_apps
create_extension_dictionary list_dictionaries          get_dictionary_entry
set_dictionary_entry        delete_dictionary_entry
create_xrecord              read_xrecord               update_xrecord
run_data_extraction         create_data_extraction_template
create_data_link            update_data_link           list_data_links
link_table_to_excel         unlink_table               export_table_to_csv
import_csv_to_table         define_property_set        attach_property_set
list_property_sets          query_by_property          tag_entities
list_tagged_entities
```

**Note:** `selection.save_selection_set` already persists to an xrecord dictionary internally.
That mechanism is used but not exposed — this phase makes it a first-class capability.

### 5.3 `acad-geolocation` (≈12)

```
set_geographic_location     get_geographic_location    remove_geolocation
list_coordinate_systems     set_coordinate_system      convert_geo_to_wcs
convert_wcs_to_geo          insert_map_image           set_map_image_type
set_north_direction         place_geo_marker           list_geo_markers
```

### 5.4 `acad-views-cameras` (≈14)

```
create_named_view           create_view_from_window    create_view_from_layout
restore_view_in_viewport    delete_named_view          set_view_category
set_view_ucs_association    export_named_view
create_camera               list_cameras               set_camera_target
set_camera_lens             set_view_background        set_perspective_mode
```

**Phase 5 total: ≈66 tools.**

---

## Phase 6 — Visualisation (≈40 tools)

Lowest priority for a production-drawing agent; highest for anything client-facing.

### 6.1 `acad-render` (≈26)

```
create_material             modify_material            assign_material
list_materials              load_material_library      set_material_map
set_material_mapping        remove_material
create_point_light          create_spot_light          create_distant_light
create_web_light            list_lights                set_light_properties
delete_light                set_sun_properties         set_sky_illumination
set_geographic_sun          render_view                render_region
render_to_file              set_render_preset          set_render_quality
set_exposure                set_shadow_display         list_render_history
```

### 6.2 `acad-animation` (≈14)

```
create_motion_path          set_camera_path            set_target_path
set_animation_settings      preview_animation          record_animation
create_walkthrough          set_frame_rate             set_animation_duration
create_flythrough           export_animation           set_view_transition
create_showmotion_shot      play_showmotion
```

**Phase 6 total: ≈40 tools.**

---

## Phases 3–6 reviewed against the SDK, 2026-08-04

Phases 2.1 and 2.2 turned out to be planned from AutoCAD's feature set rather than from what is
reachable, so the rest got the same treatment: every type family below was looked for in
`acdbmgd` / `acmgd` / `accoremgd` metadata before the numbers were touched.

**The counts move, and not all in the same direction.**

### Phase 3 — the numbers were never checked against what exists

The plan reads "`acad-geometry-2d` extensions (≈30)", "`acad-dimensions` extensions (≈14)" and
so on, written when those categories were smaller. They are not small now:

| Category | Tools today | Plan said "extensions" |
|---|---:|---:|
| `acad-geometry-2d` | 31 | +30 |
| `acad-dimensions` | 17 | +14 |
| `acad-annotations` | 12 | +18 |
| `acad-selection` | 12 | +12 |

Doubling a 31-tool category is not an "extension", and nobody has checked how much of the
proposed 30 is already there. **This subphase needs a tool-by-tool diff against the existing
manifests before its number means anything** — the same diff that turned Phase 1's "13 missing"
into "4 missing, 2 that were never separate tools".

`acad-images` / `acad-underlays` (≈22) is the one part of Phase 3 that is genuinely new
capability, and it checks out: `RasterImage`, `RasterImageDef`, `UnderlayReference`,
`DgnReference`, `PdfReference` and `DwfReference` are all in `acdbmgd`.

### Phase 4 — sound, with two names that do not exist

`Solid3d`, `LoftOptions`, `SweepOptions`, `Brep`, the whole surface family
(`PlaneSurface`, `LoftedSurface`, `SweptSurface`, `NurbSurface`, `ExtrudedSurface`), the mesh
family (`SubDMesh`, `PolygonMesh`, `PolyFaceMesh`), `SectionSettings`/`SectionManager` and
`PointCloudEx`/`PointCloudDefEx` are all present. Phase 4 is buildable as planned.

Two entries name types that do not exist: **`ShellSolid` and `SliceSolid`**. Those are
*operations* on `Solid3d`, not classes, so the capability is there and the roadmap's naming is
not. A reminder that a plausible-looking name in a plan is not evidence the API has it — which
is exactly how `set_viewport_render_mode`, `AlignSpace` and `flowDirection` each cost a build
cycle.

`acad-geometry-3d` (15) and `acad-boolean-ops` (8) already exist, so Phase 4 is an extension of
those two rather than five new categories, and 92 is likely high for the same reason Phase 3's
numbers are.

### Phase 5 — the LISP subphase is already half-built and nobody wrote it down

`ResultBuffer` and `TypedValue` are present, and **`src/AcadMcp.Lisp/LispScriptLibrary.cs`
already exists in this repository**. The roadmap plans `acad-lisp` at 12 tools as though from
nothing. Whatever that project already does has to be diffed against the plan first.

`XData`, `RegAppTable`, `Xrecord`, `DataLink` and `DataTable` are all present, so 5.2 is sound.
`GeoLocationData`, `GeoPositionMarker`, `GeoMap` are present — 5.3 is sound.
`ViewTableRecord`, `Camera`, `ViewBorder` are present — 5.4 is sound, and part of it is already
covered by `acad-view`.

### Phase 6.2 — **there is no managed API at all**

`MotionPath`, `AnimationSettings` and `AnimPath` are **absent from all three assemblies**.
ANIPATH is a command, and its settings live in a dialog. So the 14 animation tools are not
"not yet built" — they are unreachable by the route every other category in this bank uses, and
would have to go through the command layer, which this project has repeatedly found unreliable
(`eInvalidInput` from `Editor.Command`, silent queueing in `undo`, the parametric tools).

**Phase 6.2 drops from 14 to 0** unless somebody wants command-layer animation badly enough to
write a supervised contract for that channel first.

Phase 6.1 is fine: `Render`, `RenderEnvironment`, `Material`, `MaterialMap`, `Sun` and `Light`
are all present.

### What this means for the totals

The honest position is that **only Phase 1 and Phase 2 have numbers anyone has checked**. Phases
3–5 are plausible but their counts are guesses that ignore what already exists; Phase 6.2 is
wrong outright.

| Phase | Was | Now | Confidence |
|---|---:|---:|---|
| 1 | 98 | 98 | verified, complete |
| 2 | 84 | **71** | verified against the API |
| 3 | 96 | **needs a diff** | low — ignores 72 existing tools in the four categories it extends |
| 4 | 92 | **likely lower** | medium — API confirmed, but extends two existing categories |
| 5 | 66 | **needs a diff** | medium — 5.1 ignores an existing project |
| 6 | 40 | **26** | 6.1 confirmed; 6.2 has no managed API |

Doing those diffs is cheap — it is the same script that produced Phase 1's real gap list — and
should happen before any of phases 3–5 is started, not while it is being built.

## Totals

| Phase | Focus | Planned | Built | Status |
|---|---|---:|---:|---|
| — | Pre-existing at the time this was written | 337 | 337 | — |
| 1 | Blocking a real project — xrefs, UCS, viewports, fields, annotative | 98 | **75** | **partial** |
| 2 | Issuing the set — sheet sets, publish, styles, standards | 84 → **71** | **71** | **Complete.** 2.1 finished 2026-08-06: 23 tools, 194 live checks. `add_sheet_view` is deliberately not built (see KNOWN-GAPS B) and `open_sheet_set`/`close_sheet_set` were dropped by rule 45; `resave_all_sheets` ships with a plan-first design, being the only tool here that writes .DWG files |
| 3 | 2D completeness — geometry, dimensions, text, selection, images | 96 → **90** | 1 | 2 struck as unbuildable, 2 needing re-scope; `draw_mline` already pulled forward |
| 4 | Real 3D — solids, surfaces, mesh, sections, point clouds | 92 → **86** | 0 | 5 already exist in `acad-modify`, which shipped 3D-capable |
| 5 | Data + escape hatches — LISP, xdata, geolocation, views | 66 → **61** | 0 | 5 struck: data extraction is a wizard, property sets are AEC-only |
| 6 | Visualisation — render, animation | 40 → **26** | 0 | 6.2 has no managed API |
| | **Total** | **813** | **411** | **51 %** |

**Corrected 2026-08-04.** This table previously read "Total ≈713". The phases sum to 476 and
the pre-existing bank was 337, which is 813. The old figure was 100 short and made the
remaining work look smaller than it is.

### Phase 1 is not finished

Verification and coverage were being conflated. Every tool built in Phase 1 passed its live
check — 21/21, 13/13, 14/14, 12/12, 15/15 — and that was reported as "Phase 1 complete". It
is not: those ratios say *everything built works*, not *everything planned was built*.

| Category | Planned | Built | Withheld with reason | Simply not built |
|---|---:|---:|---:|---:|
| `acad-xrefs` | 26 | 22 | 4 | 0 |
| `acad-ucs` | 18 | 15 | 2 | 0 |
| `acad-viewports` | 24 | 19 | 5 | 0 |
| `acad-fields` | 16 | 17 | 0 | 0 |
| `acad-annotative` | 14 | 15 | 2 | −1 |
| **Total** | **98** | **88** | **13** | **0** |

## Phase 1 is complete

All five categories are built out: 88 tools shipped and verified against live AutoCAD, 13
withheld with a recorded reason each in [KNOWN-GAPS.md](KNOWN-GAPS.md) section B, nothing left
unattempted. The one genuinely blocked item, `insert_field_sheet_set_property`, needs
`acad-sheetsets` and moves to Phase 2 where it belongs.

Two roadmap bullets turned out not to be tools at all - `bind_xref_insert` is `bind_xref` with
`insertMode: true`, `rotate_ucs_x/y/z` is one `rotate_ucs` with an axis argument - which is why
88 built against 98 planned is closure rather than a shortfall.

**Updated 2026-08-04 (second pass).** Four categories are now closed out. Two roadmap entries
turned out not to be missing tools at all: `bind_xref_insert` is `bind_xref` with
`insertMode: true`, and `rotate_ucs_x/y/z` is one `rotate_ucs` with an axis argument — the
roadmap was sketching capability, not naming an API.

The 4 remaining are all in `acad-fields`: `insert_field_area`, `insert_field_formula`,
`insert_field_plot_info`, `insert_field_block_attribute` and `set_field_format` — five names
against four slots because `insert_field_area` overlaps `insert_field_object_property`. They
need the same kind of controlled experiment the field-expression syntax needed the first time
(ten candidate expressions in one run), not a guess each.

`insert_field_sheet_set_property` stays blocked on `acad-sheetsets` in Phase 2.

The 10 withheld are recorded in [KNOWN-GAPS.md](KNOWN-GAPS.md) section B with the reason for
each — modal command sequences, missing SDK accessors, display-only operations with nothing an
API caller can observe. They are decisions, not omissions.

The 13 remaining are ordinary unbuilt work.

### Reaching 1,000+

Still achievable, and still mostly a question of granularity rather than new capability areas:
splitting composite tools into per-parameter variants (as `openings` and `furniture` already do
with typed entry points beside a generic one), adding per-discipline wrappers over the generic
primitives, and expanding the validator rule set — which is data, not code, and where several
hundred rules would be entirely reasonable for PL/EU/ISO coverage.

**That is a reason for caution, not enthusiasm.** Every tool added is a tool that must be
described, schema'd, live-verified and kept correct. The sweep behind this document found 20
real defects in 340 tools — roughly one per seventeen. Phase 1 then added 75 tools and the work
around them turned up a further handful, including two that every automated run had reported as
a success. Tripling the bank without tripling the verification machinery would triple the
defect count.

---

## What deliberately stays out of scope

- **Drafting settings** — osnap modes, grid/snap, polar tracking, dynamic input. These configure
  *interactive* drawing; an API places geometry at exact coordinates and has no use for them.
- **UI/ribbon/workspace customisation** — CUI, tool palettes, workspaces.
- **Anything already better served by an existing tool.** `EXPLODE`, `TRIM`, `JOIN` etc. are
  covered; adding command-level duplicates would only create two ways to do one thing.
- **Express Tools** as a block. Individually a few (`OVERKILL`, `TXT2MTXT`, `FLATTEN`,
  `BURST`) are worth wrapping and appear above; the rest are interactive conveniences.

---

## Build order recommendation

1. **Decide the UCS contract before anything else** (§1.2). It is the only item here that
   changes the signature of tools that already exist. Everything else is additive.
2. **`acad-lisp` early** (§5.1), out of phase order. Twelve tools that make every later gap
   survivable in the meantime.
3. **Phase 1 in full.** Until xrefs and viewport overrides exist, "production-grade drawings
   without human intervention" is not reachable no matter what else is added.
4. **Build the missing test machinery alongside Phase 1, not after it.** Three gaps found in the
   sweep will get worse with scale:
   - a catalogue-vs-consumer contract test (four defects were a discovery tool advertising what
     the action tool would not accept — and it cannot live in `AcadMcp.Tests`, because the
     catalogues are static data inside the plugin assembly; a plugin-level test project is
     needed and does not exist)
   - a test that every tool answers on *invalid* arguments, not only on empty ones — the blind
     spot that hid both the empty input schemas and the server-killing exception
   - a lint forbidding bare `catch { }` — two defects in the sweep survived precisely because a
     silent catch swallowed the signal
5. Phases 2–4 in any order, driven by which disciplines the project actually serves.
6. Phase 6 last, or never, depending on whether visualisation is a goal at all.
