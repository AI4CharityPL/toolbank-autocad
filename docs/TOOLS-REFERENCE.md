# ToolBank AutoCAD — Full Tool Reference

Auto-generated from `toolbank-manifests/acad-*.json` by `scripts/generate-tools-reference.py`. 39 categories, 490 tools total.

## Categories

- [acad-annotations](#acad-annotations) (12 tools)
- [acad-annotative](#acad-annotative) (15 tools)
- [acad-architecture](#acad-architecture) (16 tools)
- [acad-blocks](#acad-blocks) (16 tools)
- [acad-boolean-ops](#acad-boolean-ops) (8 tools)
- [acad-callouts](#acad-callouts) (5 tools)
- [acad-civil](#acad-civil) (12 tools)
- [acad-dimensions](#acad-dimensions) (17 tools)
- [acad-electrical](#acad-electrical) (15 tools)
- [acad-fields](#acad-fields) (17 tools)
- [acad-files](#acad-files) (14 tools)
- [acad-furniture](#acad-furniture) (10 tools)
- [acad-geometry-2d](#acad-geometry-2d) (33 tools)
- [acad-geometry-3d](#acad-geometry-3d) (15 tools)
- [acad-grids](#acad-grids) (6 tools)
- [acad-hatches](#acad-hatches) (8 tools)
- [acad-layers](#acad-layers) (20 tools)
- [acad-layouts](#acad-layouts) (8 tools)
- [acad-livestream](#acad-livestream) (3 tools)
- [acad-mechanical](#acad-mechanical) (14 tools)
- [acad-modify](#acad-modify) (16 tools)
- [acad-openings](#acad-openings) (10 tools)
- [acad-parametric](#acad-parametric) (5 tools)
- [acad-plotstyles](#acad-plotstyles) (3 tools)
- [acad-plumbing](#acad-plumbing) (9 tools)
- [acad-publish](#acad-publish) (6 tools)
- [acad-router](#acad-router) (10 tools)
- [acad-schedules](#acad-schedules) (9 tools)
- [acad-sections](#acad-sections) (4 tools)
- [acad-selection](#acad-selection) (12 tools)
- [acad-sheetsets](#acad-sheetsets) (18 tools)
- [acad-styles](#acad-styles) (32 tools)
- [acad-ucs](#acad-ucs) (15 tools)
- [acad-validators](#acad-validators) (11 tools)
- [acad-verticals](#acad-verticals) (8 tools)
- [acad-view](#acad-view) (8 tools)
- [acad-viewports](#acad-viewports) (19 tools)
- [acad-vision](#acad-vision) (9 tools)
- [acad-xrefs](#acad-xrefs) (22 tools)

---

## acad-annotations

| Tool | Description |
|---|---|
| `add_dbtext` | Add a single-line text entity (DBText / DTEXT) at the given position. Height defaults to 2.5 mm; alignment is one of Left, Center, Right, Middle, BaseLeft, BaseCenter, BaseRight, TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight. |
| `add_mleader_block` | Add a multi-leader (MLeader) whose content is a block reference (e.g. detail bubble). The block must already be defined in the drawing. |
| `add_mleader_text` | Add a multi-leader (MLeader) with MText content. Single segment from arrowTip to textPosition; the dogleg between the leader and the text block is enabled by default. |
| `add_mtext` | Add a multi-line text (MText) entity at the given position. widthFactor=0 disables word-wrap (auto width); attachmentPoint is e.g. TopLeft / MiddleCenter / BottomRight (defaults to TopLeft). Inline MText formatting codes are passed through (\\Pnewline, \\Lunderline, \\C2red). |
| `add_table` | Insert an AutoCAD Table at the given position with rows × cols cells. Optional 2D data array fills cell text top-to-bottom, left-to-right. rowHeight/colWidth are in current units. |
| `create_text_style` | Create a new text style (TextStyleTableRecord) by name with the given font (.shx or TTF face name). height=0 makes the style annotative-friendly (text height set per-entity). |
| `delete_text_style` | Delete a text style by name. Standard cannot be deleted; the style must be unused (no DBText/MText/Dim references). |
| `list_text_styles` | List every text style defined in the active drawing plus the current style name. |
| `set_current_text_style` | Set the active text style for new DBText / MText entities; subsequent text creation defaults to it. |
| `set_table_cell` | Set the text content of a single Table cell by (row, col), 0-based. |
| `update_dbtext` | Replace the contents of an existing DBText entity by handle. |
| `update_mtext` | Replace the contents string of an existing MText entity by handle. Inline formatting codes are preserved as written. |

## acad-annotative

| Tool | Description |
|---|---|
| `add_annotation_scale` | Give annotative objects a representation at one or more scales, so they appear in viewports set to those scales. An annotative object is invisible in a viewport whose scale it has no representation for - this is the tool that fixes 'my text disappeared on the 1:100 sheet'. |
| `add_scale_to_list` | Add an annotation scale to the drawing, e.g. name '1:25' with paperUnits 1 and drawingUnits 25. Idempotent - an existing name is updated rather than duplicated. |
| `delete_scale_from_list` | Remove an annotation scale from the drawing's list. Fails if the scale is in use by an annotative object or a viewport, rather than silently orphaning them. |
| `get_annotation_settings` | Report ANNOALLVISIBLE and ANNOAUTOSCALE, decoded. Read-only. |
| `get_current_annotation_scale` | Return the current annotation scale (CANNOSCALE) with its ratio. Read-only. |
| `list_annotative_objects` | Enumerate every annotative object in model space with the scales it carries. Pass scale to list only objects having a representation at that scale. Read-only. |
| `list_object_annotation_scales` | List which scale representations each given entity carries, plus whether it is annotative at all. Read-only. Call this before wondering why an object is missing from a sheet. |
| `list_scale_list` | List every annotation scale defined in this drawing, with its paper:drawing ratio and which one is current. Read-only. These are the scales available to add_annotation_scale and to viewports. |
| `remove_annotation_scale` | Remove scale representations from annotative objects. The object stops appearing in viewports at those scales. An object's last remaining representation cannot be removed - use set_annotative false instead. |
| `reset_scale_list` | Reset the drawing's scale list to AutoCAD's defaults, dropping custom entries that are not in use. Scales still referenced by objects or viewports are kept and reported. |
| `set_annotation_visibility` | ANNOALLVISIBLE: show every annotative object regardless of whether it has a representation at the current scale, or show only those that do. Turning it OFF is how you check a sheet for annotation that will be missing at that scale. |
| `set_annotative` | Turn the annotative flag on or off for one or more entities. Turning it ON gives the object a representation at the CURRENT annotation scale only - use add_annotation_scale for the others. Turning it OFF collapses it back to a single fixed-size object. |
| `set_auto_add_scale` | ANNOAUTOSCALE: whether annotative objects automatically gain a representation when the current annotation scale changes. On is convenient while drafting and dangerous on an issued set, because objects silently acquire scales nobody asked for. |
| `set_current_annotation_scale` | Set CANNOSCALE - the scale new annotative objects are created at, and the one model space displays. Does NOT retroactively add this scale to existing objects; use add_annotation_scale for that. |
| `sync_scale_positions` | Reset every scale representation of the given objects back to the position of the current scale's representation. Use after moving annotation at one scale and wanting the others to follow rather than drift. |

## acad-architecture

| Tool | Description |
|---|---|
| `architecture_health` | Report the architectural layer key + planned bundled block library used by this category. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults without making a real call to AutoCAD. |
| `attach_room_tag` | Attach a compact room tag built as a 3-line MTEXT-style stack (number / name / area) at a centroid. When areaM2 is null the third line is omitted. Implementation uses 3 stacked DBText rows on A-ROOM-IDEN because MText creation runs through acad-annotations in a later phase. |
| `define_room` | Define a room: closed boundary polyline on A-ROOM-BNDY plus three text labels on A-ROOM-IDEN (room number, room name, computed area in m²). Area is computed from the polyline using the shoelace formula and reported in m² (assuming the drawing is in millimetres). tagPosition defaults to the polygon's centroid. The whole result is one transactional 'room' the agent can later reference by handle. |
| `dimension_wall` | Place ONE dimension along a wall segment between two endpoints. Auto-picks linear vs aligned per rule 36 §9: walls within 1° of horizontal/vertical use linear (rotation locked); anything else uses aligned. forceLinear / forceAligned override the heuristic. offsetMm is the perpendicular distance from the wall axis to the dimension line. |
| `draw_ceiling_grid` | Draw a T-bar suspended ceiling grid inside a rectangular bounding box. Creates a closed border polyline plus N vertical and M horizontal interior lines spaced by tileWidthMm × tileDepthMm. All entities land on A-CLNG (configurable). rotationDeg rotates the whole grid around the bbox centre (0 = axis-aligned). Returns the border handle plus separate lists of vertical/horizontal tile gridlines. |
| `draw_wall` | Draw one straight wall segment as a centreline on A-WALL-CTRL plus two parallel face polylines (offset ±thickness/2) on A-WALL. Returns all three entity handles plus the segment length and the list of layers auto-created on demand. Wall ends are square (perpendicular cap) by default — connect mitres with acad-geometry2d.fillet_corner or use draw_walls_chain for connected runs. |
| `draw_walls_chain` | Draw a continuous run of walls from a list of vertices in one call. Generates a single centreline polyline on A-WALL-CTRL and two offset face polylines on A-WALL (built by stitching together the perpendicular offsets at each vertex — joints are mitred at the angle bisector). Set closed=true to close the run back to the first vertex (e.g. for a room outline). MUCH cheaper than draw_wall × N because it issues 3 polyline calls instead of 3·N line calls. |
| `ensure_architectural_layers` | Idempotently create the AIA-style architectural + structural layer key (A-WALL, A-WALL-CTRL, A-DOOR, A-DOOR-SWING, A-GLAZ, A-ROOM-BNDY, A-ROOM-IDEN, A-CLNG, A-ROOF, A-STRS, A-ANNO-DIMS, A-ANNO-NOTE, plus structural S-COLS, S-COLS-CTRL, S-SLAB, S-SLAB-HATCH when includeStructural=true). Existing layers are left alone, never overwritten. Returns one outcome per layer (created \| already_exists \| failed). |
| `insert_door` | Insert a door at a hinge point. Draws the door panel (rectangle width × frameThicknessMm on A-DOOR) at the requested opening angle plus a swing arc (default quarter-circle, on A-DOOR-SWING). swingDirection='left' (default) hinges on the LEFT side of the wall axis; 'right' hinges on the RIGHT. Pass wallHandle to also cut the host wall at the door's jambs (hinge -> hinge + widthMm along hingeAngleDeg) before drawing the panel -- omit it to only draw the door primitives without touching any wall (e.g. when the wall was already cut separately via split_wall_at_opening). |
| `insert_elevator` | Draw an elevator shaft on A-STRS as a rectangle with two diagonal lines (X) plus a centred label on A-ANNO-NOTE. No cab / mechanical details — use this as a plan-view placeholder for lifts/verticals. For more detail use acad-verticals in Phase D7. |
| `insert_ramp` | Draw a simple rectangular ramp outline on A-STRS plus a slope arrow (shaft + head) along the travel direction and a text label reporting the gradient as 'N% RAMP' on A-ANNO-NOTE. widthMm runs perpendicular to directionDeg, lengthMm runs along it. |
| `insert_rect_column` | Insert a rectangular structural column profile on layer S-COLS plus a small crosshair centre-mark on S-COLS-CTRL. width = X-axis, depth = Y-axis (before rotation). Column is auto-centered on the supplied point. |
| `insert_round_column` | Insert a circular structural column on layer S-COLS plus a small crosshair centre-mark on S-COLS-CTRL. |
| `insert_stair` | Draw a simple straight-run stair on A-STRS: outline rectangle (widthMm × runLengthMm), treadCount-1 perpendicular tread lines at equal spacing, and a travel-direction arrow (shaft + head). The arrow ends with an 'UP' label (configurable) on A-ANNO-NOTE. directionDeg points along the run (0 = +X). For multi-flight or spiral stairs use acad-verticals in Phase D7. |
| `insert_window` | Insert a window centred at a point along a wall axis. Draws 5 entities on A-GLAZ: the sill line (wall side closer to exterior), the glass line (in the middle of the wall), the header line (wall side closer to interior), and two perpendicular jamb lines closing the opening. Pass wallHandle to also cut the host wall at the window's own axis span before drawing -- omit it to only draw the window primitives without touching any wall. rotationDeg is the wall's heading in degrees (0 = horizontal, +90 = vertical going up). |
| `split_wall_at_opening` | Cut a hole for a door/window in a wall entity — wrapper around acad.openings.cut_wall_for_opening. Workflow: (1) call split_wall_at_opening(wallHandle, jamb1, jamb2) BEFORE insert_door / insert_window so the wall faces are trimmed at the jambs; (2) then call the opening tool. v1 inherits the wrapped primitive's limitation (Line + 2-vertex Polyline walls); multi-vertex polyline walls will be supported once acad-verticals lands in Phase D7. |

## acad-blocks

| Tool | Description |
|---|---|
| `bulk_insert` | Insert many BlockReferences in one pass. For each item: if the block name is already defined it is reused; otherwise (autoImport=true) the plugin searches every registered library (or only libraryName if given) for a matching <blockName>.dwg and imports it as a new definition before inserting. Attributes / scale / rotation / layer per item. |
| `define_block` | Define a new BlockTableRecord (BTR) in the active drawing from a list of existing entity handles. The 'origin' point becomes the block's insertion (base) point. By default the source entities are erased after copying into the BTR. |
| `define_block_from_file` | Import an external .dwg file as a new block definition; the entire model space of the source DWG becomes the block. |
| `delete_block_definition` | Delete a block definition (BTR). Only succeeds if no BlockReference uses it. Use purge_unused_blocks to remove every unused block in one call. |
| `explode_block_reference` | Explode a BlockReference into its constituent entities in model space. Attributes are converted into DBText. Returns the list of newly created entity handles. |
| `extract_block_references` | Find every BlockReference (insert) of a given block definition (or all blocks if name is omitted). Returns each insert's position, scale, rotation, layer and attributes. |
| `get_block_reference_attributes` | Return all attribute reference (AttributeReference) tag/value pairs of a single BlockReference by handle. |
| `insert_block` | Insert a BlockReference of a defined block at the given position with optional non-uniform scale and rotation. Attributes are populated from the {tag: value} dictionary; missing tags fall back to defaults. |
| `library_list` | List every registered block library. When libraryName is given, only that library is returned and its .dwg file list is enumerated (when includeFiles=true, default). |
| `library_register` | Register a filesystem folder as a named block library. The path is scanned for .dwg files (recursively by default) and persisted to a user-scoped catalog so subsequent sessions remember it. Libraries are consumed by bulk_insert and swap_block (auto-import). |
| `list_blocks` | List every block definition in the drawing (excluding *Model_Space, *Paper_Space and other AutoCAD-internal records). Reports anonymous, dynamic and Xref flags. |
| `purge_unused_blocks` | Purge every block definition that has no BlockReference and is not an internal record (Model_Space, Paper_Space, anonymous *D / *E records). Returns the count removed. |
| `redefine_block` | Replace the geometry of an existing block definition with a new entity set; existing block references are updated automatically. Source entities are erased by default. |
| `rename_block` | Rename a block definition. Cannot rename Model_Space / Paper_Space, anonymous blocks (starting with '*') or Xrefs. |
| `set_block_reference_attributes` | Update one or more attribute reference text strings on an existing BlockReference. Tags not present in the BlockReference are silently skipped. Returns the number of attributes that were actually updated. |
| `swap_block` | Globally replace every BlockReference of oldName with newName, preserving position, rotation, scale and layer. When keepAttributes=true, compatible attribute tag/value pairs are copied onto the new BlockReference. If newName is not defined yet and autoImport=true, it is imported from the registered libraries first. |

## acad-boolean-ops

| Tool | Description |
|---|---|
| `check_intersection` | Check whether two entities (solids, regions or curves) intersect, and report a coarse spatial relation tag. |
| `create_region` | Build one or more 2D Region entities from closed planar boundary curves. Returns the list of created regions. |
| `intersect_regions` | Boolean intersect for 2D regions (common area of target and tools). |
| `intersect_solids` | Boolean intersect: replace target with the common volume of target and every tool 3D solid. |
| `subtract_regions` | Boolean subtract for 2D regions (target − tools). |
| `subtract_solids` | Boolean subtract: remove every tool 3D solid from the target solid. Tool solids are erased by default. |
| `union_regions` | Boolean union of 2D regions (target + tools). |
| `union_solids` | Boolean union: merge one or more tool 3D solids into the target solid. Tool solids are erased by default. |

## acad-callouts

| Tool | Description |
|---|---|
| `insert_detail_callout` | Mark a rectangular/circular area for a detail on a separate sheet. Draws a detail circle of radius radiusMm around center, a dashed-style leader line to leaderEndPoint, and a callout bubble containing the label and target scale. If leaderEndPoint is null, the bubble is positioned at radiusMm*2 to the upper-right of the centre. |
| `insert_north_arrow` | Insert a north arrow symbol (ISO 5455) at the given model-space position. The symbol is a circle plus an inscribed north-pointing arrow plus a "N" label above. Plotted diameter is PlotNorthDiameterMm (30 mm) scaled by the requested drawing scale (1:100 → 3000 mm drawing-unit diameter). Layer A-ANNO-NORT, colour inherited from the layer. |
| `insert_scale_bar` | Insert a horizontal graphic scale bar at the given position. The bar is a chequered 5-segment rectangle (alternating black/white segments 1-by-4 mm plotted high) with numeric labels under each segment and a "1:100" scale caption centred above. Segment meters auto-scale to the plan scale (1:50 → 1 m segments, 1:200 → 2 m segments). |
| `insert_section_callout` | Insert a section cut-line plus two end markers (circle + label letter) plus two view-direction arrows. Optional drawCutLine=true draws the dashed cut polyline between startPoint and endPoint; set it to false if the plan already has a cut line. label defaults to "A" and creates markers reading "A" on both ends (A-A section). viewDirection controls which side the triangle arrows point (right\|left relative to the start→end vector). |
| `insert_title_block` | Draw an ISO 7200 sheet border plus a 12-row project title block in the lower-right corner. sheetSize accepts A0/A1/A2/A3/A4; the block is scaled so that the plotted paper size matches. Pass fields=[{key, value}, ...] to fill the standard rows (PROJEKT, INWESTOR, ADRES, BRANŻA, FAZA, STADIUM, RYSUNEK, SKALA, NR RYS., DATA, PROJEKTANT, SPRAWDZAJĄCY); missing rows are left empty. Shorthand projectName/sheetNumber/author/date/titleText populate the most common rows if fields is not supplied. |

## acad-civil

| Tool | Description |
|---|---|
| `civil_health` | Report the 12-layer civil engineering key, the parcel-closure tolerance presets (residential / commercial / agricultural / forest), the supported stationing systems ('metric_km' / 'us_feet'), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which closure tolerance applies to a residential lot — without making a real call to AutoCAD. |
| `draw_alignment_curve` | Draw a single circular curve segment of a road horizontal alignment as an Arc on layer C-ROAD-CNTR (default). Spirals / clothoid transitions are NOT in v1 — only tangents and circular curves. The arc spans from startAngleDeg to endAngleDeg around the centre with the given radius (in metres, in the drawing's current units). |
| `draw_alignment_spiral` | Draw a clothoid (Euler spiral) transition segment of a road horizontal alignment on layer C-ROAD-CNTR (default) -- the piece the v1 alignment tools were missing between a tangent and a circular curve. Approximated with the standard 2-term power-series clothoid expansion (drafting-grade accuracy, not survey-grade) sampled into `segments` points and drawn as a polyline. startBearingDeg is the tangent direction at Start (0 = +X, counter-clockwise); turnDirection picks which way it curves; endRadiusM is the circular-curve radius the spiral transitions INTO at its far end (the clothoid parameter A is derived as sqrt(endRadiusM * lengthM)). Returns the end point and end bearing so the next draw_alignment_curve call can continue tangent-to-curve without the agent doing the clothoid math itself. |
| `draw_alignment_tangent` | Draw a single straight (tangent) segment of a road horizontal alignment as a line on layer C-ROAD-CNTR (default) — picks up CENTER linetype because the layer carries it. Per rule 38 §6 the road centreline MUST be a CENTER linetype on C-ROAD-CNTR; agents who reach for acad-geometry2d.draw_line directly bypass the linetype assignment. |
| `draw_contour_line` | Draw a topographic contour line as a polyline on layer C-TOPO-MAJR (when isMajor=true, default) or C-TOPO-MINR (when isMajor=false). When isMajor=true, also drops a labelled DBText with the elevation (formatted to 2 decimals) at the labelEvery-th vertex. Per rule 38 §4 minor contours are unlabelled; major contours MUST be labelled — agents who set isMajor=true on a 1 m contour break the visual hierarchy. |
| `draw_north_arrow` | Draw a basic north arrow at `position`: an isoceles triangle pointing toward TRUE north (rotated by trueNorthDegFromPageNorth from the page +Y axis per rule 38 §8) with optional 'N' letter above the tip. The triangle apex is sizeM tall, the base is 0.4 × sizeM wide, drawn on layer C-NORTH (Continuous, default). Per rule 38 §8 a north arrow with the default 0° rotation when the drawing is rotated ruins all bearings on the plan — agents MUST pass the drawing rotation explicitly. The COMPASS variant ships with the Phase-7 block library. |
| `draw_parcel` | Build a parcel polyline by walking from `start` along a list of (bearing, distance) legs and draw it on layer C-PROP (PHANTOM2 linetype, default). Bearings MUST be surveyor textual form: 'N 45 30 15 E' / 'N 45° 30\' 15" E' / 'S 30 W'. Computes the closure error (distance from the last vertex back to the start) and reports it in metres along with `closureStatus = 'in_tolerance' \| 'out_of_tolerance'`. Tolerance is set by `kind` ('residential' < 0.02 m, 'commercial' < 0.05 m, 'agricultural' < 0.20 m, 'forest' < 0.50 m per rule 38 §3) or via `toleranceMOverride`. Setting autoClose=true closes the polyline geometrically (last vertex snapped to first) but the original closure error is still reported. |
| `draw_road_corridor` | Given a road centreline polyline + a total widthM, draws the centreline on C-ROAD-CNTR (CENTER linetype) PLUS two parallel edge polylines on C-ROAD-EDGE (Continuous), each offset by widthM/2 to either side at every vertex (mitred at internal vertices using the average of the incoming and outgoing tangent normals). Per rule 38 §6 the edges are Continuous, NOT CENTER — the layer assignment is what makes the plan readable. Returns all 3 entity handles + the widthM used. |
| `draw_vertical_profile` | Draw a road vertical alignment (profile view) grade line from a list of PVI points (station, elevation, and an optional parabolic vertical-curve length centred on that PVI). Interior PVIs with a curveLengthStation get a sampled symmetric parabola instead of a sharp grade break; PVIs without one (or the first/last point) stay a straight grade line to their neighbour. Drawn as ONE polyline on C-ROAD-CNTR (default) in a local station/elevation coordinate frame: drawing X = origin.X + (station - firstStation) * horizontalScale, drawing Y = origin.Y + (elevation - datumElevation) * verticalScale -- pass datumElevation close to (but below) the lowest PVI so the profile doesn't end up thousands of drawing units above origin, exactly like a real profile sheet's datum line. verticalScale defaults to 10x (a common profile exaggeration) since 1:1 road grades read as nearly flat lines otherwise. |
| `ensure_civil_layers` | Idempotently create the 12-layer civil-engineering key (C-ROAD-CNTR, C-ROAD-EDGE, C-ROAD-LANE, C-PROP, C-ESMT, C-ROW, C-TOPO-MAJR, C-TOPO-MINR, C-TOPO-SPOT, C-STAT, C-ANNO, C-NORTH) per rule 38 §9, with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. C-ROAD-CNTR = 0.30 mm CENTER, C-ROAD-EDGE = 0.50 mm Continuous, C-PROP = 0.50 mm PHANTOM2, C-TOPO-MAJR = 0.35 mm, C-TOPO-MINR = 0.13 mm). Existing layers are left alone, never overwritten. includeRoad / includeProperty / includeTopo flags skip the corresponding sub-set so a survey-only drawing does not get road layers it never uses. |
| `place_spot_elevation` | Place a survey spot elevation at `position`: a small + cross (two perpendicular short lines on C-TOPO-SPOT) AND a signed elevation text formatted '+102.45' / '-1.23' (Polish PN-EN ISO 6709 conventional 2-decimal precision) offset by textOffsetM to the upper-right. Returns BOTH the cross handles and the text handle. Per rule 38 §5 drawing only the text breaks downstream takeoffs because the actual point is missing. |
| `place_station_labels` | Walk the centreline polyline and at every interval (default 20 m) drop: (1) a small perpendicular tick mark on layer C-STAT and (2) a labelled DBText with the station notation parallel to the alignment, offset to one side. Notation respects the system flag: 'metric_km' → '0+020' (Polish / EU, default), 'us_feet' → '0+20' (US, where 1 station = 100 ft). Per rule 38 §7 ticks are perpendicular to the LOCAL tangent, recomputed at every vertex, NOT to the global +X axis. |

## acad-dimensions

| Tool | Description |
|---|---|
| `apply_arch_tick_style` | Scan every Dimension entity on a given layer and re-assign its dimension style to the target (default ARCH-ISO). If ensureStyle=true the target style is auto-created via ensure_architectural_dimstyle first. Useful for retrofitting legacy drawings to rule 66's architectural tick convention in one call. |
| `auto_dim_walls` | Automatically build a continued dimension chain across a list of wall handles. Each wall's start/end endpoints are projected onto the baseline direction through 'origin' + 'baselineDeg'; duplicates within mergeToleranceMm are collapsed. Remaining projected points are sorted and fed to continued_chain. Walls whose endpoints cannot be resolved (non-Curve entities) are returned in skippedHandles instead of aborting. Combined with ensure_architectural_dimstyle this replaces 90%+ of manual DIMLINEAR / DIMCONT chains when dimensioning a facade. |
| `dimension_aligned` | Place an aligned dimension parallel to the segment p1->p2 at dimLinePoint. |
| `dimension_angular_2l` | Place an angular dimension between two existing line entities. The arc passes through arcPoint. |
| `dimension_angular_3p` | Place an angular dimension defined by a vertex (center) and two rays through 'first' and 'second'. The arc passes through arcPoint. |
| `dimension_arc_length` | Place an arc-length dimension on an Arc; arcPoint locates the dimension arc. |
| `dimension_baseline_chain` | Build a baseline chain of linear dimensions from a common baseline point to N subsequent points; spacing is taken from the dimstyle's DIMDLI. |
| `dimension_continued_chain` | Build a continued chain of linear dimensions: each new dimension's first extension line is the previous dimension's second extension line. |
| `dimension_cumulative_chain` | Cumulative dimension chain: N dimensions sharing a SINGLE dim line, each reporting the distance from baselinePoint to point_i (running total). Use for exterior overall/axis/opening dimension rows per rule 66 §1. Differs from baseline_chain (staggered by DIMDLI) and continued_chain (end-to-end). |
| `dimension_diametric` | Place a diametric dimension on a Circle through farPoint (the leader anchor on the far side of the curve). |
| `dimension_linear` | Place a linear (rotated) dimension between p1 and p2 with the dim line passing through dimLinePoint at the given rotation in degrees (0 = horizontal). |
| `dimension_ordinate` | Place an ordinate (X or Y datum) dimension at definingPoint with leader endpoint at leaderEnd. useXAxis=true measures the X distance from UCS origin. |
| `dimension_overall` | Place a single linear dimension spanning the overall bbox of one or more entities projected onto rotationDeg. Useful for 'outer-most' exterior dimensions (rule 66 level 1). rotationDeg 0° measures along X (horizontal), 90° along Y. Uses the bounding boxes fetched via acad.geometry2d.get_entity. |
| `dimension_radial` | Place a radial dimension on a Circle or Arc; chordPoint specifies which side of the curve the leader exits. |
| `ensure_architectural_dimstyle` | Idempotently create (or update) the ARCH-ISO dimension style used by the 3-level architectural dimension hierarchy (rule 66). Sets architectural tick arrowheads ('ArchTick'), text height scaled to plot scale, mm precision, no trailing zeros. Safe to call multiple times; returns whether the style was created, updated or left untouched. |
| `list_dimstyles` | List all dimension styles defined in the active drawing plus the current Dimstyle. |
| `set_entity_dimstyle` | Assign a dimension style to one or more existing dimension entities (by handle). |

## acad-electrical

| Tool | Description |
|---|---|
| `draw_ladder_rails` | Draw the two vertical power rails of a ladder diagram on layer E-WIRE-PWR (default, ACI 1, 0.50 mm Continuous), spaced widthMm apart and heightMm tall starting from topLeft. Place rail labels (default 'L1' and 'N' per the Polish/IEC convention, rule 39 §9) above each rail on layer E-LBL-WIRE. Returns both rail handles and both label handles. To draw the rungs, follow with `draw_ladder_rung` calls; the rails do NOT auto-create rungs. |
| `draw_ladder_rung` | Draw one horizontal rung between the left and right ladder rails at vertical position y on layer E-WIRE (default), and place its rung-number label (rungNumber) on the LEFT side at offset labelOffsetMm to the left of the left rail on layer E-LBL-RUNG. Per rule 39 §4 rung numbers go on the LEFT rail and are sequential. Place the contacts to the LEFT of the coil on the rung; the coil itself sits at the RIGHT end (use place_coil for that). The rung is just the conductor — devices are added separately. |
| `draw_wire` | Draw a wire (poly-line) between symbol terminals or rung devices. Routes to the right layer per `kind`: 'signal' → E-WIRE (default, ACI 7, 0.30 mm), 'power' → E-WIRE-PWR (ACI 1, 0.50 mm), 'control' → E-WIRE-CTRL (ACI 4, 0.25 mm). Pass `layer` directly to override the routing. Per rule 39 §7 wires MUST connect at SYMBOL TERMINALS (use the `terminals` returned by place_resistor / place_contact_* / place_coil / place_terminal_block); a wire drawn to 'wherever the symbol body happens to start' breaks netlist extraction. |
| `draw_wire_junction` | Draw a filled junction dot at a wire intersection — the visual marker that the two wires ARE electrically connected (rule 39 §3). Without a dot, two crossing wires are conventionally NOT connected. Implementation: a small filled Circle on layer E-WIRE (default) — agents who skip this on T or + intersections produce ambiguous schematics that the inspector flags. |
| `electrical_health` | Report the 12-layer electrical-schematic key, the IEC 81346 device-tag prefix lookup table (rule 39 §6), the supported symbol styles ('iec' default, 'ansi') with the office default (5 mm unit size, rule 39 §10), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which prefix letter to use for a contactor — without making a real call to AutoCAD. |
| `ensure_electrical_layers` | Idempotently create the 12-layer electrical-schematic key (E-WIRE, E-WIRE-PWR, E-WIRE-CTRL, E-SYMBOL, E-TERM, E-LBL-WIRE, E-LBL-DEV, E-LBL-RUNG, E-XREF, E-TITLE, E-PANEL, E-NOTE) per rule 39 §11 with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. E-WIRE-PWR = 0.50 mm Continuous ACI 1, E-WIRE-CTRL = 0.25 mm ACI 4, E-LBL-RUNG = 0.25 mm ACI 2). Existing layers are left alone, never overwritten. includePanel=true also creates E-PANEL for cross-sheet drawings; default false because v1 ships only the schematic side. |
| `place_coil` | Place a relay / contactor coil symbol at `position`, rotated by rotationDeg. style='iec' (default) draws an empty rectangle of width 3×unitSize × height 2×unitSize with the device tag inside; style='ansi' draws a circle of radius unitSize with the tag inside. Optional `tag` (e.g. '-K1') is placed inside the symbol on layer E-LBL-DEV. Optional `contactRungs` (a JSON array of rung numbers like 12, 14, 18) emits the cross-reference text below the coil on layer E-XREF (rule 39 §5) — agents who omit this leave maintenance hunting through the drawing for K1's contacts. Exposes terminals 'A1' (top, IEC) and 'A2' (bottom, IEC). |
| `place_contact_nc` | Place a Normally-Closed contact symbol at `position`, rotated by rotationDeg. NC contact opens only when its controlling coil is energised. Geometry: identical to NO (rule 39 §2) PLUS a horizontal slash through the angled lever — the slash IS the NC marker. Exposes terminals 'in' (left) and 'out' (right). For NO use the SEPARATE place_contact_no tool. |
| `place_contact_no` | Place a Normally-Open contact symbol at `position`, rotated by rotationDeg. NO contact bridges only when its controlling coil is energised. Geometry: a horizontal bottom terminal line plus a short angled lever pointing up-and-away from the right terminal — NO horizontal slash (rule 39 §2: the slash is what distinguishes NC from NO). Exposes terminals 'in' (left) and 'out' (right). For NC use the SEPARATE place_contact_nc tool — never call this with a `kind` flag. |
| `place_device_tag` | Place an IEC 81346 device tag as DBText on layer E-LBL-DEV. Accepts the short form ('K1' / '-K1'), the location-qualified form ('+CAB1-K1') or the fully-qualified form ('=PWR+CAB1-K1') per rule 39 §6a. The PREFIX letter is validated against the IEC 81346-2 set (-K / -Q / -F / -S / -B / -M / -T / -G / -X / -W / -H per rule 39 §6) — agents who invent prefixes ('-A1' for a contactor) get a fail-fast error with the allowed list. Returns the canonical string + the prefix character + a one-line description of what that prefix means. |
| `place_din_rail` | Draw a DIN rail (EN 50022 top-hat rail, 35 mm wide by default) as a rectangle on layer E-PANEL (default) from start, lengthMm long, at rotationDeg. Pass slotPitchMm to also draw perpendicular tick marks every slotPitchMm along the rail as a visual device-spacing reference (omit for a plain rail outline). Returns the end point so a device outline or the next rail segment can be placed flush against it. |
| `place_panel_device_outline` | Draw a rectangular physical device footprint (breaker, contactor, relay body, etc.) on layer E-PANEL (default) for panel-layout drawings -- the physical counterpart to the schematic symbols above (place_coil etc. draw the SCHEMATIC symbol; this draws the PHYSICAL footprint you'd mount on a DIN rail). origin is the top-left corner. Pass tag to also place a device tag label centred below the outline on E-LBL-DEV. |
| `place_resistor` | Place a resistor symbol at `position`, rotated by rotationDeg (0° = horizontal, terminals at left/right). style='iec' (default, Polish/EU) draws a rectangle of width 4×unitSize × height 1.5×unitSize; style='ansi' draws a zig-zag of 6 zags spanning the same width. Both styles expose two terminals named '1' (left / start) and '2' (right / end) with their EXACT coordinates so subsequent draw_wire calls snap to them (rule 39 §7). Default unitSize = 5 mm (rule 39 §10). |
| `place_terminal_block` | Place a terminal block as `count` numbered rectangles in a horizontal row starting at `origin` (top-left corner), each rectangle of width pitchMm × height heightMm, with sequential numbers (startNumber, startNumber+1, …) labelled below. Per rule 39 §11 terminals live on layer E-TERM (ACI 6, 0.40 mm) and labels on E-LBL-WIRE. Returns each slot's body handle, label handle, AND its top + bottom centre points so wires can snap to either side of the block. |
| `route_wireway` | Draw a wireway / trunking channel along `path` on layer E-PANEL (default) as a centreline plus two parallel edge lines offset ±widthMm/2 (mitred at interior vertices, same offset approach as acad-civil.draw_road_corridor / acad-architecture.draw_walls_chain). Use this for the physical cable-management channel between panel devices, distinct from the schematic wire routing of draw_wire. |

## acad-fields

| Tool | Description |
|---|---|
| `convert_field_to_text` | Freeze a field into plain text at its current value. One-way and deliberate: use it when issuing a drawing that must not change afterwards, never as a way to 'fix' a field showing the wrong thing. |
| `get_field_evaluation_mode` | Report when fields currently re-evaluate (the FIELDEVAL bitmask, decoded). Read-only. |
| `get_field_expression` | Return the raw AcVar expression and the evaluated value behind one text entity. Read-only. Use it to see what a field is actually bound to before trusting the number it shows. |
| `insert_field_area` | Place a live area label for a closed shape, converted to the units you actually annotate in. AutoCAD reports Area in square DRAWING units, so a raw area field on a room drawn in millimetres reads 24000000 - correct and useless on a plan. This divides inside the field, so the number stays live: edit the room and the label follows. units accepts mm2, cm2, m2 (default) or ha, each with a sensible precision and unit suffix you can override. |
| `insert_field_block_attribute` | Place a field bound to one attribute of an inserted block, found by its tag. Use it to echo a door number, an equipment ID or a title-block value somewhere else on the sheet without retyping it - change the attribute and every echo follows. An unknown tag is refused with the list of tags that block actually has. |
| `insert_field_date` | Place a date field that re-evaluates rather than freezing. format is a .NET/AutoCAD date pattern (default yyyy-MM-dd). Use this for the date cell of a title block instead of writing today's date as text. |
| `insert_field_expression` | Place a field from a raw AcVar expression, for anything the typed tools do not cover. Escape hatch - the expression is passed through unvalidated, and the evaluated result is returned so a wrong one is visible immediately rather than at plot time. |
| `insert_field_filename` | Place a field showing this drawing's file name, optionally with its full path and extension. Survives Save As, which a typed file name does not. |
| `insert_field_formula` | Place a field that computes an arithmetic expression, optionally over other fields. '2+3' evaluates to 5; nest a property reference to compute from live geometry, such as a room area divided by an occupancy factor. The expression is passed through unvalidated and the evaluated result comes back with the handle, so a mistake shows up now rather than as #### at plot time. |
| `insert_field_layout_name` | Place a field showing the name of the layout the field sits on. This is the sheet-number cell of a title block: rename the tab and every sheet updates itself. |
| `insert_field_object_property` | Place a field bound to a property of an existing entity by handle - Area, Length, Radius, Layer, Color and so on. The text follows the object: edit the geometry and the number changes with it. This is what makes a room-area label self-maintaining. |
| `insert_field_plot_info` | Place a field showing how this sheet will plot: PaperSize, DeviceName, PlotScale, PlotOrientation, PlotDate, PlotStyleTable or LoginName. These are the title-block cells that are wrong most often, because they are usually typed once and then the page setup changes. Several evaluate to ---- until the layout has the setting, which is AutoCAD saying 'not set' rather than a broken field; the evaluated value is returned so you can tell. |
| `insert_field_system_variable` | Place a field showing an AutoCAD system variable (DWGNAME, LOGINNAME, CTAB, DWGPREFIX, ...). Useful for drawn-by and drawing-status cells. |
| `list_fields` | List every text entity in model space that contains a field, with its raw expression and its currently evaluated value. Read-only. Call this to find title-block cells that are still frozen text. |
| `set_field_evaluation_mode` | Control when fields re-evaluate: on open, save, plot and/or regen (the FIELDEVAL bitmask). Turning them all off is how a drawing is frozen for issue without converting every field to text. |
| `set_field_format` | Change how an existing field displays its value - decimal places, unit format, thousands separator - without rebuilding what it is bound to. Format strings are AutoCAD's field format codes, e.g. "%lu2%pr2" for two decimal places. Edits the field code rather than the text, because the text of a field is its answer, and writing that back would freeze the field into plain text. |
| `update_fields` | Re-evaluate fields now. Pass handles to update specific ones, or omit them to update every field in the drawing. Returns how many were evaluated. |

## acad-files

| Tool | Description |
|---|---|
| `audit_database` | Run AUDIT on the active document database. Reports the number of errors found and (if fix=true) fixed. |
| `close_document` | Close a document by its file path (or the active document if path is null). Set save=true to save before closing. |
| `export_file` | Export the active document to the given path. Format is one of "DWG", "DXF", "PDF", "DWF", "DWFX", "IMAGE" / "PNG". Optional layout name (default: current). Scope is "Display" / "Extents" / "Limits" / "Window" / "View" / "Layout". When scope="Window" you MUST supply the model-space rectangle in `window`: { xMin, yMin, xMax, yMax } in drawing units. For raster (PNG/IMAGE) and vector plots you may supply `widthPx` / `heightPx` to request an output resolution (PNG only; ignored for DWG/DXF). Typical usage for AI visual review: { format:"PNG", scope:"Window", window:{xMin:0,yMin:0,xMax:80000,yMax:60000}, widthPx:4000, heightPx:3000 }. |
| `get_active_document` | Return descriptor of the currently active document (path, name, modified flag, DWG version, entity count). |
| `import_file` | Import a .dwg / .dxf file into the currently active document at the optional insertion point (default: 0,0,0). DWG files are merged into model space; DXF respects its own units. |
| `list_documents` | List every open AutoCAD document with its file path, modified flag, read-only flag and entity count, plus the active document name. |
| `list_drawing_properties` | Read the drawing's own properties - title, subject, author, keywords, comments, last saved by, revision number, hyperlink base - plus every custom name/value pair on it. Read-only. Worth knowing: acad-fields can bind a field to any of these, so a title block that reads its project name from here updates itself instead of being retyped on every sheet. |
| `new_document` | Create a brand new empty document based on the default template (acad.dwt) and make it active. |
| `open_document` | Open an existing .dwg / .dxf file in the AutoCAD UI as a new document and make it active. Optional readOnly flag and password for encrypted DWG. |
| `purge_database` | Run a full database purge: removes every unused symbol-table record (blocks, layers, linetypes, text/dimstyle, mlinestyle, registered apps). Returns the count of records purged. |
| `save_document` | Save the currently active document to its existing path (no-op if it has no path yet — call save_document_as instead). |
| `save_document_as` | Write the active drawing to a new path. IMPORTANT: this writes a COPY. The open document keeps its own name and its own unsaved state - this is the managed Database.SaveAs, not AutoCAD's SAVEAS command, and there is no managed way to re-point an open document. So save_document afterwards still writes to the ORIGINAL path, and DBMOD still reports unsaved changes, both correctly. The result gives savedTo and the document's own path side by side, because confusing the two is the entire trap. Optional dwgVersion is one of "AC1027" (2013), "AC1032" (2018), "AC1024" (2010); defaults to native. |
| `set_drawing_custom_property` | Add, replace or remove one custom drawing property - an arbitrary name/value pair such as PROJECT-NUMBER or CLIENT. Pass value:null to remove it. These are the properties worth binding a title-block field to, because unlike the standard set they can be named after whatever the project actually tracks. The result says which of add, replace or remove happened. |
| `set_drawing_properties` | Set any of the drawing's standard properties, leaving the rest alone. Omitting a field leaves it unchanged; passing an empty string clears it deliberately - the two are different and both are supported. Custom name/value pairs go through set_drawing_custom_property instead. The result reports every property afterwards, not just the changed ones, so a caller can see that nothing else moved. |

## acad-furniture

| Tool | Description |
|---|---|
| `insert_bed` | Insert a hospital/residential bed. Types: 'standard' (900x2000), 'icu' (1000x2200 + head-monitor strip), 'bariatric' (1200x2200), 'pediatric' (700x1500), 'or' (operating table 550x2100 + trendelenburg), 'labour' (1050x2300 + stirrups). Defaults to layer A-FURN-BED + attributes {inv_id, type, room}. |
| `insert_cabinet` | Insert a cabinet / storage unit. Types: 'storage' (generic), 'medical' (with glass-door indicator), 'file' (with drawer lines), 'wardrobe' (with hanger-rail indicator). Configurable width/depth. Defaults width=800 depth=400 layer=A-FURN-CBT. |
| `insert_chair` | Insert a chair. Types: 'office' (550x550 swivel), 'armchair' (800x800), 'stool' (450x450 round), 'examination' (600x600 medical stool), 'wheelchair' (700x1100). Defaults to layer A-FURN-CHR. |
| `insert_desk` | Insert a desk at given position with configurable width/depth. Types: 'office', 'reception' (L-shaped counter 2400x800 + 1200x400 overhang), 'nurse-station' (3000x900 with raised edge). Defaults width=1600 depth=800 layer=A-FURN-DSK. |
| `insert_furniture` | Insert any catalog furniture block by its canonical name (e.g. 'FURN-BED-STD', 'FURN-CHAIR-OFFICE'). Generic entry-point used after list_furniture_catalog; most callers prefer the specialised insert_bed/insert_chair/... tools that infer a type. |
| `insert_sofa` | Insert a sofa. Types: 'lounge' (cushioned), 'clinic' (waiting-room, vinyl). Seats: 2, 3. Defaults seats=3 type=lounge layer=A-FURN-SFA. |
| `insert_table` | Insert a table. Shape: 'rectangle' / 'round' / 'square'. Types: 'meeting', 'coffee', 'dining', 'exam' (medical exam table - height strip + roll paper slot). Configurable width/depth. Defaults rectangle 1200x800 meeting, layer=A-FURN-TBL. |
| `list_furniture_catalog` | Enumerate the built-in furniture block catalog (hospital + office + residential). Read-only. Returns name, category (bed/chair/desk/cabinet/sofa/table/misc), domain (hospital/office/residential), default width/depth in mm, and a one-line description. |
| `list_furniture_in_model` | Enumerate all furniture BlockReferences currently in model-space (block names starting with 'FURN-'). Optionally filter by layer or by exact block name. Returns handle, block name, layer, position, rotation and any {inv_id, type, note} attribute values. Read-only. |
| `populate_room` | Auto-populate a room with a furniture preset. Room is identified either by a closed polyline handle OR explicit bbox (min+max). Presets: 'ward-room' (2 beds + 2 nightstands + 1 armchair), 'icu-room' (1 ICU-bed + monitor cabinet + visitor chair), 'or-room' (OR-table + anaesthesia + instrument trolley), 'office' (desk + chair + file cabinet), 'reception' (reception-desk + 3 waiting chairs), 'waiting' (3 sofas + 1 coffee-table), 'consult' (desk + 2 chairs + exam table + cabinet). Returns handles of inserted items plus per-item layer assignment warnings. |

## acad-geometry-2d

| Tool | Description |
|---|---|
| `chamfer_corner` | Chamfer two curves at their intersection with two distances; returns the new chamfer line. |
| `delete_entities` | Erase entities by handle. Pass multiple in one batch for atomicity. |
| `draw_arc` | Draw an arc by center, radius, and start/end angle in degrees (CCW). |
| `draw_circle` | Draw a circle by center point and radius. |
| `draw_donut` | Draw a donut (filled annulus) at a center, with inner and outer diameters. |
| `draw_ellipse` | Draw an ellipse by center, major-axis end point and minor-to-major ratio (0 < ratio <= 1). |
| `draw_hatch` | Apply an associative hatch over closed boundaries identified by handle. |
| `draw_line` | Draw a 2D straight line segment between two points on the active drawing. |
| `draw_mline` | Draw a multiline (MLINE) through the given vertices using a named multiline style - the way a wall of a defined type is drawn in one call rather than as two offset polylines that must be kept parallel by hand. style defaults to the drawing's current one; create one first with create_mlinestyle. justification is 'top', 'zero' or 'bottom' and decides which of the style's parallel lines the vertices you pass actually lie on, so it changes where the wall sits relative to your points. scale multiplies every element offset, so a 200mm style drawn at scale 1.5 is 300mm wide. |
| `draw_mtext` | Draw multiline text (MTEXT) with wrap-width and height. |
| `draw_point` | Draw a single point entity at a 2D position. |
| `draw_polygon` | Draw a regular polygon (3..1024 sides), inscribed or circumscribed. |
| `draw_polyline` | Draw a 2D lightweight polyline through the given vertex list, optionally closed. |
| `draw_ray` | Draw a half-infinite ray from a base point in a given direction. |
| `draw_rectangle` | Draw an axis-aligned rectangle as a closed polyline by two opposite corners. |
| `draw_revcloud` | Draw a revision cloud polyline through the given vertices with arc-length min/max. |
| `draw_spline` | Draw a 2D spline interpolated through the given fit points, optionally closed. |
| `draw_text` | Draw single-line text (DTEXT) at a 2D position with given height/rotation/style. |
| `draw_xline` | Draw an infinite construction line through a base point in a given direction. |
| `explode_entity` | Explode a polyline/block/hatch into its component primitives. |
| `extend_curve` | Extend a curve until it reaches one of the boundary entities. |
| `fillet_corner` | Fillet two curves at their intersection with the given radius; returns the new fillet arc. |
| `get_area` | Return enclosed area for a closed curve (circle, ellipse, closed polyline, hatch). |
| `get_bounding_box` | Return axis-aligned bounding box of an entity by handle (XY plane). |
| `get_curve_length` | Return curve length (perimeter) of a line/polyline/arc/spline by handle. |
| `get_distance_points` | Return Cartesian distance between two 2D points. |
| `get_distance_to_entity` | Return shortest distance from a 2D point to a curve entity by handle. |
| `get_entity` | Return full descriptor (class, layer, color, bbox, length, area, endpoints) of an entity by handle. |
| `get_intersections` | Return XY intersection points between two curves identified by handle. |
| `join_curves` | Join multiple coincident curves into a single polyline if topology allows. |
| `list_entities_in_window` | List handles of all entities whose bounding box intersects the rectangular window. |
| `offset_curve` | Offset a curve by a signed distance, returning the new curve handle. |
| `trim_curve` | Trim a curve at intersections with the boundary list, keeping the side opposite the pick point. |

## acad-geometry-3d

| Tool | Description |
|---|---|
| `draw_box` | Create a 3D solid box defined by two opposite corner points (axis-aligned in WCS). |
| `draw_cone` | Create a 3D solid cone or frustum (set topRadius>0 for frustum). |
| `draw_cylinder` | Create a 3D solid cylinder by base center, radius, and height (Z+). |
| `draw_planar_surface` | Create a planar surface entity from one or more closed planar boundary curves (handles). |
| `draw_pyramid` | Create a 3D solid pyramid or frustum with N sides (3..32). Use topRadius>0 for frustum. |
| `draw_sphere` | Create a 3D solid sphere by center point and radius. |
| `draw_torus` | Create a 3D solid torus by center, major (tube path) radius and minor (tube) radius. |
| `draw_wedge` | Create a 3D solid wedge (right-angle prism) defined by two opposite corners. |
| `extrude_curve` | Extrude a closed planar curve (Polyline / Region / Circle) into a 3D solid by given height with optional taper angle in degrees. |
| `get_3d_bounding_box` | Return the axis-aligned 3D bounding box of an entity (min and max points). |
| `get_3d_centroid` | Return the centroid (center of mass) of a 3D solid in WCS coordinates. |
| `get_mass_properties` | Return full mass properties of a 3D solid: volume, surface area, centroid, principal moments and radii of gyration. |
| `get_surface_area` | Return total surface area of a 3D solid or surface. |
| `get_volume` | Return the volume of a 3D solid (single value, current units). |
| `revolve_curve` | Revolve a closed planar curve around an arbitrary axis (axisStart, axisEnd) by angle in degrees (default 360). |

## acad-grids

| Tool | Description |
|---|---|
| `add_grid_axis` | Add one labeled grid-axis line. Optionally attaches bubbles (circle + text) at the start and/or end. Axis extends by extendMm past the provided endpoints before the bubble so the bubble sits outside the grid box. Rule 67 §3. |
| `add_grid_bubble` | Add a single grid bubble: a circle of the given radius plus a centred label. Used to retro-fit bubbles onto existing axis lines or for section / detail callouts. |
| `delete_grid` | Erase grid axes + bubbles. If handles is provided, only those handles are erased; otherwise every entity on axisLayer + bubbleLayer is erased. Use list_grid_axes first to preview. |
| `draw_grid` | Draw an orthogonal column grid from two lists of spacings (mm). X-axes get letter labels (A, B, C, …), Y-axes get numeric labels (1, 2, 3, …). Bubbles (circle + label) are drawn on the configured sides (default: north + west). Lines and bubbles live on A-GRID / A-GRID-BUB. Rule 67 §1 (grid policy). |
| `list_grid_axes` | Enumerate handles of all entities living on the grid axis and bubble layers. Read-only; used by validators + callouts to find grid bubbles for intersection queries. |
| `snap_to_grid` | Snap a point to the nearest grid intersection given an origin + two spacing lists. Returns snapped XY, axis labels (A, B, 1, 2…) and distance from the input point. PURE maths, no plugin call — use before drawing to align entities to structural axes. Rule 67 §5. |

## acad-hatches

| Tool | Description |
|---|---|
| `apply_material_preset` | Apply a named material preset (e.g. 'concrete', 'brick', 'insulation', 'plaster', 'stone', 'steel', 'glass', 'wood-cross', 'wood-grain', 'lead-shield', 'faraday', 'earth', 'tile', 'reinforced-concrete') to the supplied boundary handles. Each preset maps material -> (pattern, scale, angle, color) per rule 62-hatching-policy. |
| `apply_material_preset_by_point` | Same as apply_material_preset but takes a seed point instead of boundary handles. Auto-detects enclosing boundary, then applies the material preset. Preferred for batch populating walls/floors once the geometry is in place. |
| `clip_hatch` | Replace the boundary of an existing hatch with a new set of closed polyline/region handles. Use after editing geometry so the hatch follows the new edges. Returns the re-evaluated hatch. |
| `draw_hatch` | Fill one or more closed boundaries (identified by handle) with a named hatch pattern. Supports pattern/scale/angle, layer override, foreground+background colors, associative/annotative modes. Preferred over geometry-2d draw_hatch when you need color or background. |
| `draw_hatch_by_boundary` | Fill the closed region enclosing the given seed point, auto-detecting the boundary using AutoCAD's TraceBoundary (with optional island detection). Ideal when you only know a point inside a room rather than its edges. |
| `list_hatches` | Enumerate all hatch entities currently in model-space (optionally filtered by layer and/or pattern). Returns handle, layer, pattern, scale, angle, area, loop-count, associativity for each. Read-only. |
| `list_patterns` | Enumerate available hatch patterns with their category (ANSI, ISO, AR-architectural, PN-EN) and recommended default scale/angle. Read-only. Use to discover what patterns are installed before drawing. |
| `regenerate_hatches` | Re-evaluate one or more associative hatches after their boundaries have been edited. Scope: explicit handles, layer filter, or entire model-space. Returns the count of successfully regenerated hatches plus a list of handles that failed (e.g. open boundaries). |

## acad-layers

| Tool | Description |
|---|---|
| `compare_layer_state` | Answer whether restoring this state would change anything, without restoring it, and list the layers it covers. Read-only. This is the call to make before restore_layer_state in a drawing somebody is working in: the difference between a no-op and a restore that silently reorganises their view. |
| `create_layer` | Create a new layer. Accepts color (RGB or ACI), linetype name, lineweight in mm, plottable flag and description. |
| `delete_layer` | Delete a layer (only if no entities reference it). Layer 0 and Defpoints cannot be deleted. |
| `delete_layer_state` | Delete a named layer state. THE LAYERS ARE NOT TOUCHED - a layer state is a recording of visibility and properties, so deleting it removes the recording and nothing else. The result says so explicitly, because this is a tool name an agent could reasonably fear means something more destructive. |
| `export_layer_state` | Write one named layer state out to a .las file so it can be reused in other drawings or kept under version control alongside the project. It writes a file, not the DWG, so the drawing is unchanged. The result reports the byte count, because an export that produced an empty file is otherwise indistinguishable from one that worked. |
| `get_layer` | Get full descriptor of one layer by name. |
| `import_layer_state` | Read a .las file into this drawing as a named layer state. The name comes from inside the file rather than from you, so the result reports which states actually appeared - established by comparing the drawing before and after rather than by assuming. AutoCAD refuses to import over an existing name; delete or rename the local one first. |
| `list_layer_states` | List every saved named layer state in the active drawing. |
| `list_layers` | List every layer in the active drawing with color, linetype, lineweight, plottable/frozen/locked/off flags, plus the current layer name. |
| `purge_unused_layers` | Purge every layer that has no entity references and is not protected (0 / Defpoints / current). Returns number of layers removed. |
| `rename_layer` | Rename a layer. Layer 0 cannot be renamed; new name must be a valid AutoCAD symbol name. |
| `rename_layer_state` | Rename a named layer state. Refuses a name that is already taken rather than merging into it, and confirms both halves of the rename in the result - the old name gone and the new one present - since a rename that half happened is worse than one that failed outright. |
| `restore_layer_state` | Restore a previously saved named layer state. |
| `save_layer_state` | Save the current visibility/lock/color/linetype state of every layer under a named layer state (LAS). |
| `set_current_layer` | Set the active ("current") layer; subsequent draw operations default to this layer. |
| `set_layer_color` | Set a layer's color (true RGB or ACI 1..255). |
| `set_layer_linetype` | Set a layer's linetype (must already be loaded; returns LayerNotFound if linetype is missing). |
| `set_layer_lineweight` | Set a layer's lineweight in millimeters; snaps to nearest standard AutoCAD value (e.g. 0.13, 0.18, 0.25, 0.5, 0.7, 1.0 mm). |
| `set_layer_state` | Toggle one or more layer state flags: frozen, locked, off, plottable. null = leave unchanged. Cannot freeze the current layer. |
| `set_layer_state_description` | Attach or replace the description on a named layer state - what it is for, which sheet it belongs to, which discipline it serves. Pass an empty description to clear it. Worth doing: a drawing holding six states called PLAN-1 through PLAN-6 with no descriptions is one an agent cannot choose between. |

## acad-layouts

| Tool | Description |
|---|---|
| `configure_plot` | Configure a layout's plot settings: plotter / device name, paper size (accepts canonical 'ISO_full_bleed_A0_(1189.00_x_841.00_MM)', locale 'ISO A0 (841.00 x 1189.00 MM)', or fuzzy alias 'A0' / 'ISO A0' / 'a0' — all three resolve to the plotter's canonical name), named plot style table, and 0/90/180/270 plot rotation. Pass null on any field to leave it untouched. Call layouts.list_paper_sizes first if you're unsure which media strings the installed plotter accepts. |
| `create_layout` | Create a new paper-space layout (tab). Optionally make it the current/active layout right after creation. |
| `delete_layout` | Delete a paper-space layout. Cannot delete the Model tab; cannot delete the last remaining paper-space tab. |
| `get_layout` | Return descriptor of a single paper-space layout by name. |
| `list_layouts` | List every paper-space layout (tab) in the active drawing with its tab order, current flag, and configured plotter / paper size. |
| `list_paper_sizes` | Enumerate every paper size supported by a plotter (plotter=null -> the current layout's plotter or the first registered device). Returns the canonical media names (what configure_plot needs) plus the locale-facing display name. Call this before configure_plot if you're unsure which media strings the installed plotter accepts — especially for non-standard devices (e.g. 'DWG To PDF.pc3', 'Microsoft Print to PDF', pen plotters, PublishToWeb PNG). configure_plot accepts canonical / locale / fuzzy names (e.g. 'A0', 'ISO A0'); use this tool when even fuzzy resolution fails. |
| `rename_layout` | Rename a paper-space layout. The Model tab cannot be renamed; the new name must be unique and valid as an AutoCAD symbol name. |
| `set_current_layout` | Switch the active layout to the named tab (use "Model" to return to model space). All subsequent draw / viewport tools target this layout. |

## acad-livestream

| Tool | Description |
|---|---|
| `clear_events` | Discard all currently buffered events (does not affect future capture, only the backlog). Use this to reset state, e.g. at the start of a design_iterate loop so old noise doesn't show up in the first poll. |
| `livestream_status` | Report the event ring buffer's current size, capacity (2000), the highest sequence number issued so far (headSeq), how many older events have been dropped to stay within capacity, and how many documents currently have event hooks attached. |
| `poll_events` | Return entity-change and command-lifecycle events captured since sinceSeq (default 0 = from the start of the current buffer, which holds the most recent 2000 events). Each event has a monotonic seq -- pass the previous response's nextSeq back in as sinceSeq to get only what happened since your last poll. maxCount caps how many are returned in one call (default 200). Events are captured live via AutoCAD Database.ObjectAppended/Modified/Erased and Document.CommandWillStart/CommandEnded hooks, not by re-scanning the drawing. |

## acad-mechanical

| Tool | Description |
|---|---|
| `draw_bolt_head_top_view` | Draw the top view of a hex-head bolt per rule 37 §5: a regular hexagon with two flats parallel to the X axis (rotated by rotationDeg) sized by flatToFlatMm, optionally a Continuous shank circle inside (rotation matters for the hexagon, not the circle), and a centreline crosshair on ME-CENTER sized to the across-corners radius. Pass nominalDiameterMm for documentation only — it's echoed back in the result for traceability but does not affect geometry. The across-corners diameter is reported. |
| `draw_centerline` | Draw an axis / centreline as a CENTER line on layer ME-CENTER (default). For round features prefer draw_centerline_cross which sizes the extension automatically per ISO 128. |
| `draw_centerline_cross` | Draw the canonical round-feature centreline crosshair: TWO perpendicular CENTER-linetype lines on layer ME-CENTER (default), each extending featureRadiusMm + extensionMm beyond the centre point in both directions, rotated by rotationDeg. Per rule 37 §2 this is what a circle's centreline SHOULD look like — agents who try to do it with two raw draw_centerline calls usually forget the extension and the drawing looks like a `+` glued to the circle. |
| `draw_counterbore_hole` | Draw a plan-view counterbore hole: outer counterbore circle on layer ME-VISIBLE plus an inner through-hole circle on the same layer plus a centreline crosshair on ME-CENTER sized to the counterbore radius. counterboreDiameterMm MUST be greater than throughDiameterMm — the tool fails fast otherwise. |
| `draw_hidden_edge` | Draw an occluded edge as a HIDDEN line on layer ME-HIDDEN (default). Per rule 37 §1 hidden geometry MUST live on its own layer — drafting it on ME-VISIBLE is the #1 'looks fine, fails inspection' bug. |
| `draw_hole_side_view` | Draw a hole's SIDE view (vertical cross-section through the hole axis) -- the plan-view hole tools (draw_through_hole etc.) only draw the top-down circle; this is the companion detail/section view. kind='through': two parallel wall lines, open at both ends (a through hole has no bottom). kind='blind': walls stepping down to a drill-point V at drillPointAngleDeg (118° standard). kind='counterbore': wider walls for counterboreDepthMm then narrower walls to depthMm (requires counterboreDiameterMm + counterboreDepthMm). kind='countersink': an angled flare from headDiameterMm down to diameterMm over countersinkAngleDeg (requires headDiameterMm), then straight walls to depthMm. Y runs downward from topCenter (the top surface) into the material. A centreline is always drawn on ME-CENTER (default) extending centerlineExtensionMm past both ends. |
| `draw_revision_triangle` | Draw the canonical revision marker per rule 37 §6: a filled equilateral triangle (closed polyline + SOLID hatch) on layer ME-REV with the revision letter or number drawn as DBText centred on the triangle. Returns BOTH the triangle handle and the text handle so the agent can later move them together. The triangle pointer sits at `position`; rotationDeg orients its tip (default 0° = pointing UP). |
| `draw_section_cut_line` | Draw a section cutting plane line per ISO 128 type H: thick PHANTOM polyline on layer ME-SECTION (lineweight 0.70 mm by default via the ensured layer), arrow heads on each end pointing in the viewing direction (perpendicular to the cut, pointing OUTWARD from the start→end direction by rotating +90°), and a label DBText on layer ME-TEXT placed at each end. Returns all 5 entity handles. Per rule 37 §3 the sectioned hatch is NOT drawn here — call acad-geometry2d.draw_hatch on the resulting sectioned-view boundary separately. |
| `draw_section_hatch` | Apply a material-appropriate section hatch (ISO 128 §6 / rule 37 §8 convention -- steel ANSI31, cast iron ANSI32, aluminium ANSI37, etc., see mechanical_health.materials) over an existing closed boundary. This is the tool the header comment on this file used to say didn't exist in v1 -- it now looks up pattern/scale/angle from the same material table mechanical_health reports, so an agent doesn't have to hardcode hatch parameters per material. scaleOverride/angleOverrideDeg let you deviate from the table default for an unusual drawing scale. |
| `draw_threaded_hole` | Draw a plan-view threaded (tapped) hole per rule 37 §4 + §4a: a FULL outer circle at majorDiameterMm on layer ME-VISIBLE, an INNER 3/4 ARC at minorDiameterMm on layer ME-THREAD (HIDDEN linetype) — the gap demonstrates that the inner circle is the thread minor diameter, not a true geometric circle — plus a centreline crosshair on ME-CENTER. The arc gap is threadGapDeg wide (default 90°, so the arc spans 270°) starting at threadGapStartDeg (default 0° = +X axis). minorDiameterMm MUST be smaller than majorDiameterMm. |
| `draw_through_hole` | Draw a plan-view through hole: profile circle on layer ME-VISIBLE (default) at the requested diameter PLUS a centreline crosshair on ME-CENTER (default) extending centerlineExtensionMm past the circle on each axis (rule 37 §4). Returns the profile circle and both centreline handles in one call. |
| `draw_visible_edge` | Draw a visible feature edge as a Continuous line on layer ME-VISIBLE (default). Use this rather than acad-geometry2d.draw_line whenever the line has semantic meaning — the layer assignment is what makes the drawing readable per ISO 128. |
| `ensure_mechanical_layers` | Idempotently create the ISO-mechanical 11-layer key (ME-VISIBLE, ME-HIDDEN, ME-CENTER, ME-DIMS, ME-TEXT, ME-SECTION, ME-HATCH, ME-THREAD, ME-CONSTRUCTION, ME-TITLE, ME-REV) per rule 37 §9, with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. ME-VISIBLE = 0.50 mm Continuous, ME-HIDDEN = 0.25 mm HIDDEN, ME-CENTER = 0.18 mm CENTER, ME-SECTION = 0.70 mm PHANTOM). Existing layers are left alone, never overwritten. ME-CONSTRUCTION is non-plottable. includeConstruction=false skips it; includeRevision=false skips ME-REV. |
| `mechanical_health` | Report the ISO-mechanical layer key, the material → hatch pattern lookup table (rule 37 §8), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which pattern to pass to acad-geometry2d.draw_hatch when sectioning steel — without making a real call to AutoCAD. |

## acad-modify

| Tool | Description |
|---|---|
| `align` | Align entities so that source point pair (A,B) maps onto target point pair (A,B). Optional uniform scale to make distances match. |
| `array_polar` | Polar (circular) array around a center, distributing N items over the given total angle. Optionally rotate items along the path. |
| `array_rectangular` | Rectangular array (rows × cols × levels) by row, column and optional Z level spacing. |
| `copy` | Copy entities by translation from→to. Set count > 1 for an evenly stepped chain of copies. |
| `create_group` | Create a named AutoCAD Group containing the given entities. Selectable=true makes the group pickable as a unit. |
| `erase` | Erase entities (soft delete – AutoCAD keeps them in the undo stack until purged). |
| `match_properties` | Copy generic properties (layer, color, linetype, lineweight, ltscale) from source entity onto target entities. |
| `mirror` | Mirror entities through a plane defined by point + normal (3D); optionally erase the source entities. |
| `move` | Translate one or more entities by the vector from→to (WCS). |
| `rotate` | Rotate entities around a center by angle (degrees, CCW). Optional axis vector for 3D rotations (default Z). |
| `scale` | Uniformly scale entities about a center point by a positive factor. |
| `set_color` | Set the entity color to a true RGB color or an ACI index (1..255). |
| `set_layer` | Move entities to the given layer (creates the layer if missing). |
| `set_linetype` | Set the linetype (by name) and optional linetype scale on entities. The linetype must already be loaded. |
| `set_lineweight` | Set entity lineweight in millimeters. Common values: 0.13, 0.18, 0.25, 0.5, 0.7, 1.0 mm. |
| `ungroup` | Delete a named Group (the underlying entities remain in the drawing). |

## acad-openings

| Tool | Description |
|---|---|
| `cut_wall_for_opening` | Split an existing wall (Line or 2-vertex Polyline, referenced by handle) into two segments with a gap between jamb1 and jamb2. Projects both jamb points onto the wall axis; the leftHandle / rightHandle returned identify the surviving pieces. The original wall entity is erased. Fails (tool error) for closed polylines or walls with >2 vertices; use D6 'split_wall_at_opening' for polyline walls. |
| `draw_door_by_points` | Quick-sketch a door leaf + swing arc without creating a BlockReference. Provide hingePoint (p1) and leafEnd (p2); plugin draws a line p1->p2 plus a 90-deg arc centered at p1. Useful when precise block library is overkill (concept studies, mark-ups). Layer defaults to A-DOOR. |
| `draw_window_by_points` | Quick-sketch a window as 2 parallel lines (inner + outer wall face) + a center glass line between jamb1 and jamb2. wallThickness (mm, default 250) controls offset. Layer defaults to A-GLAZ. |
| `export_schedule` | Export a door or window schedule to CSV or JSON string (optionally write to disk). kind='doors'\|'windows'\|'all'. format='csv'\|'json'. CSV columns: NUMBER,TYPE,WIDTH_MM,HEIGHT_MM,REI,RC,FIRE_CLASS,ACOUSTIC_DB,LEAD,ROOM_FROM,ROOM_TO,LAYER,HANDLE. Returns the rendered content (also written to outputPath when supplied). Read-only. |
| `insert_door` | Insert a door block at a wall opening. Types: 'single' (900x2100 hinged), 'double' (1600x2100 two-leaf), 'sliding' (1000x2100), 'fire' (REI 30/60/90/120 EI marker), 'hospital' (double-swing with trajectory arrows), 'lead' (radiological Pb-marker). Auto-assigns number D-001, D-002... (skipped by number= or autoNumber=false). Attributes: NUMBER, TYPE, WIDTH_MM, HEIGHT_MM, REI, LEAF_DIR, SWING_DIR, ROOM_FROM, ROOM_TO, ACOUSTIC_DB, LEAD. Layer defaults to A-DOOR (or A-DOOR-FIRE / A-DOOR-LEAD depending on type). |
| `insert_opening_generic` | Insert any opening block (door or window) by its canonical name (e.g. 'DOOR-FIRE-1200-2100', 'WIN-HOSP-1800-1500'). Generic escape-hatch after list_opening_catalog; most callers prefer insert_door / insert_window. |
| `insert_window` | Insert a window block at a wall opening. Types: 'fixed' (non-opening), 'casement' (side-hung), 'tilt' (tilt & turn), 'hospital' (fire-rated E/EI30/EI60), 'fire' (EI30/EI60/EI120). Burglary rating (RC 1..6 per PN-EN 1627) and fire class supported per type. Auto-assigns W-001, W-002... Attributes: NUMBER, TYPE, WIDTH_MM, HEIGHT_MM, SILL_MM, RC, FIRE_CLASS, ROOM. Layer defaults to A-GLAZ. |
| `list_opening_catalog` | Enumerate the built-in doors + windows block catalog. Read-only. Covers: single/double/sliding/fire/hospital/lead-lined doors and fixed/casement/tilt/hospital/fire windows. Returns family name, default width/height, kind (door\|window), and capability flags (supportsFire, supportsBurglary, supportsLeadShield). |
| `list_openings_in_model` | Enumerate all opening BlockReferences currently in model-space (block names starting with 'DOOR-' or 'WIN-'). kind='doors'\|'windows'\|'all'. Optional layerFilter. Returns handle, blockName, kind, number, type, width/height, rei, rc, fireClass, acousticDb, leadShielded, roomFrom, roomTo, position, rotation, layer. Read-only. |
| `renumber_openings` | Rewrite NUMBER attribute across all doors and/or windows in model-space. kind='doors'\|'windows'\|'all'. order='insertion' (creation order) \| 'spatial' (sort by Y descending then X ascending so numbering reads 'room-by-room'). startAt starts sequence (default 1). Returns change log per entity. |

## acad-parametric

| Tool | Description |
|---|---|
| `ensure_parametric_layers` | Idempotently create the 6-layer parametric sketch key (P-CONSTRUCTION, P-SKETCH, P-CONSTRAINED, P-DYNAMIC, P-PARAM-LBL, P-NOTE) per rule 42 §9 with prescribed ACI colour, Continuous linetype, and lineweight. Existing layers are never overwritten. |
| `get_dynamic_block_properties` | Read all DynamicBlockReferenceProperty entries from a BlockReference handle: names, read-only flags, UnitsType, CLR type, and current Value. isDynamicBlock=false returns an empty list — the handle is still a block insert but not dynamic. Use the reference handle, never hard-code anonymous *U block names (rule 42 §6). |
| `list_constraint_entities` | Scan model space for database objects whose runtime class name contains 'Constraint' (constraint proxy / glyph entities). Optional layerFilter narrows results. Read-only with respect to geometry — still requires the plugin for DB access. |
| `parametric_health` | Return the 6-layer P-* parametric key and the dynamic-block angle value policy string. Does not open AutoCAD. |
| `set_dynamic_block_property` | Write one DynamicBlockReferenceProperty on a BlockReference by name. Pass JSON booleans as true/false, numbers as JSON numbers. For Angle-typed properties the numeric value is interpreted as degrees and converted to radians in the plugin (see parametric_health.dynamicAnglePolicy). Strings are for lookup / text parameters. Read-only properties throw. |

## acad-plotstyles

| Tool | Description |
|---|---|
| `apply_plotstyle_to_layout` | Apply a named plot-style (CTB/STB) to a paperspace layout. Optionally runs ensure_ctb first so the sheet is copied into AutoCAD before being assigned (ensure=true, default). Under the hood dispatches acad.layouts.configure_plot { layoutName, plotStyle }. |
| `ensure_ctb` | Ensure a colour-dependent plot-style (CTB) is installed in AutoCAD's Plot Styles directory. Queries acad.layouts.list_plot_styles to resolve the target directory, then copies from sourcePath (caller override) or the repo asset folder <repo>/assets/plotstyles/<name>. If the CTB already exists and overwrite=false (default), reports existedBefore=true, copied=false. Calls list_plot_styles a second time to verify the refresh picked up the new sheet. Use this before apply_plotstyle_to_layout so the target sheet is guaranteed loaded. |
| `list_plotstyles` | Enumerate all plot-styles currently visible to AutoCAD (CTB + STB). filter='ctb' or 'stb' narrows the returned names. Also returns repo presets (HOSPITAL-ISO, ISO-Standard, monochrome), the AutoCAD Plot Styles directory, and the backend asset directory so the caller can prep ensure_ctb calls. |

## acad-plumbing

| Tool | Description |
|---|---|
| `insert_basin` | Insert a wash basin (umywalka). Types: 'standard' (600x450), 'double' (1200x450 — two faucet positions). Set accessible=true for PN-EN 17210 700x550 with knee-clearance marker. Width is configurable. Layer A-PLMB-BSN. |
| `insert_bathtub` | Insert a bathtub (wanna). Types: 'standard' (1700x700), 'mini' (1500x700), 'corner' (1400x1400 quarter-round + splash wall). Configurable width/depth. Draws bathtub outline + drain + faucet-end indicator. Layer A-PLMB-BT. |
| `insert_plumbing` | Insert any catalog sanitary block by fully-qualified name (e.g. 'PLMB-WC-FS', 'PLMB-BSN-ACC-700-550'). Generic entry-point; most callers prefer specialised insert_wc / insert_basin / insert_shower / ... which map type + size to the canonical name. |
| `insert_shower` | Insert a shower (prysznic). Shape: 'square' / 'rectangle'. walkIn=true draws a walk-in (open-side curtain indicator, no raised tray). Standard sizes 800x800, 900x900, 1200x900 walk-in. Drain indicator always at the geometric centre. Layer A-PLMB-SHW. |
| `insert_urinal` | Insert a urinal (pisuar). Standard 380x340 at sprayer height 650 mm (PLMB-UR-STD). Set accessible=true for lower-rim 380x450 variant at 450 mm height (PLMB-UR-ACC, PN-EN 17210 §U4.3). Layer A-PLMB-UR. |
| `insert_wc` | Insert a toilet / WC. Types: 'floor-standing' (PLMB-WC-FS 370x650), 'wall-hung' (PLMB-WC-WH 370x540 — lower footprint), 'bidet-combo' (PLMB-WC-BID 370x550 with bidet spray). Set accessible=true for PN-EN 17210 compliant unit (PLMB-WC-ACC 800x800 with grab-bar indicators). Defaults floor-standing. Layer A-PLMB-WC. |
| `list_plumbing_catalog` | Enumerate the built-in sanitary-fixture catalog (WCs, basins, showers, bathtubs, urinals, sinks + accessible variants per PN-EN 17210). Returns name, category, domain (hospital/office/residential), default width/depth in mm, accessible flag, Polish/EN normative reference, and description. Read-only. |
| `list_plumbing_in_model` | Enumerate all sanitary BlockReferences currently in model-space (block names starting with 'PLMB-'). Filter by layer or exact block name. Returns handle, block name, layer, position, rotation, INV_ID / TYPE attribute values + ACCESSIBLE flag. Read-only. |
| `populate_bathroom` | Auto-populate a bathroom/WC with a sanitary preset. Room identified by closed-polyline handle OR bbox. Presets: 'wc-public' (WC + basin, single cubicle), 'wc-accessible' (PN-EN 17210 accessible WC + accessible basin + grab-bar markers, min 1500x1800), 'bathroom-residential' (WC + basin + bathtub OR shower), 'bathroom-hospital-patient' (wall-hung WC + basin + walk-in shower + grab bars), 'shower-room' (shower + basin), 'wc-block-staff' (2x WC + 2x basin + urinal). accessible=true overrides with accessible variants. |

## acad-publish

| Tool | Description |
|---|---|
| `apply_page_setup` | Apply a named page setup to layouts, so a whole set plots identically. Name the layouts explicitly, or pass allLayouts true - there is no 'all layouts' default, because applying a page setup to every tab in a drawing because an argument was omitted is precisely the accident worth designing out. Reports the outcome per layout rather than a count, so a partial success reads as one. |
| `create_page_setup` | Define a NAMED, reusable page setup in this drawing - device, paper size, plot style table, rotation - that can then be applied to many layouts at once. Either snapshot a layout you already configured by hand (fromLayout), or state the settings explicitly; passing both is an error rather than a precedence rule nobody remembers. Refuses to overwrite an existing name unless overwrite is true, because a firm's standard page setups should not be redefined by accident. |
| `delete_page_setup` | Remove a named page setup from this drawing. Layouts previously configured from it keep their settings - applying a page setup copies it rather than linking to it - and the result says so, so nobody expects issued sheets to revert. |
| `get_plot_area` | Report what a layout would plot: paper size, margins, the plot window, rotation, centring and scale. Read-only. This is the agent-shaped half of a plot preview - a preview is something a human looks at, while this is the part that can be checked before committing to output. Omit layoutName for the current layout. |
| `list_page_setups` | List the named page setups defined in this drawing, with the device, paper size, plot style table and rotation each one carries. Read-only. Call this before apply_page_setup - the names are per-drawing, so an agent cannot know them in advance. |
| `publish_sheets` | Publish several layouts into ONE file - a multi-sheet PDF, DWF or DWFX. This is the thing files.export_file cannot do: its layout argument is singular, so it produces one file per sheet. Name the layouts explicitly; there is no all-layouts default, for the same reason apply_page_setup has none. Optionally names a page setup to plot every sheet through, which is how a set comes out consistent. Reports the byte count of the file that actually appeared, and fails if none did. |

## acad-router

| Tool | Description |
|---|---|
| `acad_call` | UNIVERSAL dispatch: invoke any backend composite (e.g. 'schedules/generate_door_schedule') OR any plugin primitive (e.g. tool='acad.annotations.add_table', category left empty). Routes in-process, no subprocess spawn. |
| `acad_describe_drawing` | Vision shortcut (Phase 4): screenshot active viewport + OCR + LLM-describe in one call. |
| `acad_design_iterate` | Auto-design loop (Phase 7.0): create a checkpoint, execute a planned sequence of tool calls, validate against a named standard, auto-fix fixable violations or roll back on failure. Closes the 12-of-10 agent loop. |
| `acad_explain_capabilities` | Returns a compact catalog of all known acad-* categories with one-line summaries. |
| `acad_find_tools` | Semantic search across all acad-* MCP servers via ToolBank find_tools, filtered to our namespace. Returns ranked candidates with category and tool name. |
| `acad_load_category` | Shortcut: connect to a single acad-<name> MCP server in lazy mode. Returns its tool list summary so you can pick the next call. |
| `acad_recommend_categories` | Suggest the 1-3 most relevant categories for a free-text task description. Saves tokens by avoiding indiscriminate loading. |
| `acad_restore_checkpoint` | Roll back to a previously created checkpoint (Phase 7). Used by the auto-design loop on validation failure. |
| `acad_status` | Lightweight health-check: AutoCAD alive, version, vertical (vanilla/civil3d/mechanical/architecture/MEP/plant3d), active document, layer, entity count, mode banner (full vs com-only). |
| `acad_undo_checkpoint` | Create a named undo checkpoint so subsequent operations can be rolled back atomically (Phase 7). |

## acad-schedules

| Tool | Description |
|---|---|
| `audit_all_rooms` | READ-ONLY batch audit of every room label in the drawing. For each label, measures the REAL area (get_room_region), compares to the stated m² on the label, counts doors/windows/furniture in the detected region, and flags leaks, label mismatches, missing doors and furniture type conflicts. Optional exportCsvPath (use 'auto' for %LOCALAPPDATA%\AcadMcp\reports\). Does NOT modify the drawing. |
| `correct_all_room_areas` | Batch wrapper around correct_room_area: scans every room label and rewrites the m² token when the measured area diverges from the label by more than tolerancePct. Use apply=false for dry-run. Modifies the drawing only when apply=true. |
| `correct_room_area` | Verify and, if needed, CORRECT a space's stated area on its label. Measures the REAL area with the wall-aware boundary detector (get_room_region) and compares it to the area written on the label (e.g. '200 m²'). When they differ by more than tolerancePct (or when explicitAreaM2 is supplied), it rewrites the 'N m²' token on the label text in place (reusing update_dbtext/update_mtext). Use apply=false for a dry-run that only reports the discrepancy. Works on ALL layers and is space-agnostic (room, office, garden, …). Modifies the drawing only when a correction is applied. |
| `generate_door_schedule` | Build a door schedule table (ZESTAWIENIE STOLARKI DRZWIOWEJ) from every opening of kind=door currently in model space. Columns: NR, TYP, SZER., WYS., REI, OGNIOOCH., RC, DB, POM. OD, POM. DO. Uses attribute data written by acad-openings (rule 65). Applies the HOSPITAL-DEF or OFFICE-DEF TableStyle preset via ensure_table_style. Table anchored at the given position, layer A-ANNO-TBLS. |
| `generate_finish_legend` | Emit a finish legend (LEGENDA WYKOŃCZEŃ) mapping finish codes (F-xx floor, W-xx walls, C-xx ceiling) to descriptions, RAL colors and locations. The default rows cover typical hospital PVC/epoxy/HPL finishes; pass extraRows to append project-specific codes. Layer A-ANNO-LEGN by default. |
| `generate_room_schedule` | Build a room schedule table (ZESTAWIENIE POMIESZCZEŃ) from every DBText/MText label on the configured room-label layers (default A-ROOM-IDEN / A-ANNO-ROOM). If boundaryLayer is set, each room's area (m²) is computed from the closed polyline on that layer that contains the label. Auto-numbers rows 101, 102, … when autoNumber=true. |
| `generate_window_schedule` | Build a window schedule table (ZESTAWIENIE STOLARKI OKIENNEJ) from every opening of kind=window currently in model space. Columns: NR, TYP, SZER., WYS., PARAPET, SZYBA, RC, DB, POM. Values come from the acad-openings attribute contract (rule 65). Uses HOSPITAL-DEF/OFFICE-DEF TableStyle. Table anchored at the given position, layer A-ANNO-TBLS. |
| `get_room_data` | READ-ONLY, universal. Locate ONE space (room, office, apartment room, classroom, yard/garden, hall, …) by its number or name (substring, case-insensitive) and return a full dossier: number + name, MEASURED area (m², computed from a wall-aware flood-fill of the actual boundary) plus the labelled area (labelAreaM2, parsed from the label text) when present, bounding-box dimensions (width × depth in mm), the boundary detection method, and every door, window and furniture/equipment item that lies inside the room (furniture tested against the traced outline, openings against the perimeter). Scans labels on ALL layers by default (not just A-ROOM-*). Use this to gather accurate data BEFORE drawing a schedule or generating a visualization. Does NOT modify the drawing. |
| `update_schedules` | Find existing schedule tables by their title cell (ZESTAWIENIE STOLARKI / POMIESZCZEŃ / LEGENDA WYKOŃCZEŃ) and rebuild each one from current drawing content. Old tables are erased and replaced at the same insertion point. Useful after inserting/removing openings or rooms; one call keeps every schedule in sync. |

## acad-sections

| Tool | Description |
|---|---|
| `insert_elevation_marker` | Insert an elevation-direction marker at the given model-space position. Geometry: a filled triangle pointing in the requested compass direction (N / E / S / W / NE / NW / SE / SW or bare degrees) on top of a short horizontal baseline, with the caption "ELEWACJA <direction>" to the right. Differs from acad-callouts.insert_section_callout in that elevations have a directional triangle rather than a circle-in-triangle pair. |
| `insert_section_line` | Draw a section cut-line from startPoint to endPoint on layer A-DETL-SECT, apply DASHED2 linetype, add 90-degree offset ticks at both ends, then place labelled end markers with view-direction arrows via acad-callouts (unless drawEndMarkers=false). The offset ticks (6 mm plotted) signal that the cut-line's path is symbolic, not literal. sheetReference optional, viewDirection in {left\|right} relative to the start→end vector. |
| `insert_section_title` | Place a section/view title beneath a drawn section (e.g. "PRZEKRÓJ A-A" with "SKALA 1:50" under it and an underline). position is the insertion point — typically the centre-bottom of the view. Customise caption (defaults to "PRZEKRÓJ") for elevations / axonometric views. drawUnderline=true adds an 80 mm plotted horizontal rule between the caption and the scale line. |
| `list_section_lines` | Inventory all entities on the A-DETL-SECT layer (or a caller-supplied layer via layerFilter). For every handle that is a curve, also queries acad.geometry2d.get_curve_length so the caller can sanity-check drawing-unit cut lengths against the target scale. |

## acad-selection

| Tool | Description |
|---|---|
| `count_entities` | Count entities in model space (optionally filtered by DXF type). |
| `filter_entities` | Apply an additional layer/type/color filter to a candidate set (or to all of model space if no handles supplied). |
| `load_selection_set` | Load a previously saved named selection set and return its handles. Validates each handle still exists. |
| `save_selection_set` | Save a list of entity handles under a named selection set (stored in the AcadMcp xrecord dictionary on the drawing). |
| `select_all` | Select every entity currently in model space (no filtering). |
| `select_by_color` | Select all entities by color (true RGB or ACI index). |
| `select_by_handle` | Resolve a list of entity handles into a single selection result. Validates each handle exists. |
| `select_by_layer` | Select all entities on the given layer. Optionally restrict by frozen/thawed state. |
| `select_by_type` | Select entities by AutoCAD DXF entity name (e.g. "LINE", "LWPOLYLINE", "CIRCLE", "3DSOLID", "INSERT"). |
| `select_fence` | Select entities crossing a polyline fence defined by an ordered vertex list. |
| `select_polygon` | Select entities inside (crossing=false) or intersecting (crossing=true) a closed polygonal region. |
| `select_window` | Select entities fully inside (or, with crossing=true, intersecting) the WCS axis-aligned window from min to max. |

## acad-sheetsets

| Tool | Description |
|---|---|
| `add_sheet` | Add an existing paper-space layout to the sheet set as a new sheet - AutoCAD's "Import Layout as Sheet". Takes the .DWG holding the layout and the layout's name; the drawing itself is NOT modified, the set gains a reference to it. Checks the layout exists before touching the sheet set and lists the drawing's layouts if it does not, and refuses a layout already in the set, since one layout belongs to one sheet. Give number and title to control how it reads in the drawing list, and subset to file it under a discipline. |
| `create_subset` | Create a subset in a sheet set - the discipline or phase folder a real project organises its sheets into, such as Architectural or Phase 2. Nests inside another subset when parent names one, otherwise sits at the top level of the set. Writes to the shared .DST under a lock. Refuses a name already in use, because subset names are how move_sheet_to_subset addresses them and a duplicate would make that ambiguous. |
| `define_custom_property` | Define a custom property on the sheet set - the project-wide data a title block binds to, such as client, project number or issue date. scope='sheetSet' (the default) means one value shared by the whole project; scope='sheet' means every sheet carries its own, which set_sheet_property then fills in per sheet. Writes to the shared .DST under a lock. Setting an existing property updates its value and keeps the scope it already had, so an update never silently re-scopes it. |
| `delete_subset` | Delete an EMPTY subset from a sheet set. Refuses while it still holds sheets and reports how many, because what AutoCAD does with the sheets inside a removed subset is not documented and could include deleting them - move them out with move_sheet_to_subset first. Sheets are never removed by this tool. Writes to the shared .DST under a lock. |
| `get_sheet_property` | Read the properties of ONE sheet - name, number, title, description, plus every custom property on it. Read-only. Identify the sheet by its NAME or by its NUMBER, since on a real project people say 'A-101' at least as often as they say a sheet's name. Name a single property to get just that one, with whether it was built in or custom; omit it to get all of them. This is the tool fields.insert_field_sheet_set_property has been waiting for. |
| `get_sheet_set_info` | Summarise a sheet set file: its name, description, how many sheets it holds and how many subsets. Read-only. Takes the .DST path - every tool in this category does, because none of them hold a sheet set open between calls. Start here to confirm a path is a readable sheet set before asking it anything else. |
| `get_sheet_set_path` | Confirm a .DST path resolves to a readable sheet set and report its name and description. Read-only. Cheaper than get_sheet_set_info because it does not walk the sheet tree, so it is the call to make when all you need is to validate a path before passing it to the other tools. |
| `list_custom_properties` | List the custom properties defined at SHEET SET level - the project-wide values a title block binds to, such as client or project number. Read-only. Per-sheet custom properties are reported by get_sheet_property instead, because a sheet can override the set and reporting both here would hide which value actually applies. |
| `list_sheets` | List every sheet in a sheet set - number, name, title, description, the subset it sits under, and whether it is marked do-not-plot. Read-only. Subsets are walked recursively and each sheet reports its full subset path, so a nested set reads as a flat list an agent can act on rather than a tree it has to traverse. |
| `list_subsets` | List the subsets of a sheet set with their full paths and how many sheets each holds directly. Read-only. Subsets are how a real set is organised by discipline or by phase, and a subset path is what move_sheet_to_subset will take once the write half of this category exists. |
| `move_sheet_to_subset` | Move one sheet into a subset, or back to the top level of the sheet set when subset is omitted. The sheet is re-parented rather than copied, so the set's total sheet count does not change. Identify the sheet by its name or its number, and the subset by its bare name or its full 'Parent / Child' path. Writes to the shared .DST under a lock. |
| `remove_sheet` | Remove a sheet from the sheet set. This removes the set's REFERENCE to a layout - the layout itself, and the drawing file holding it, are left exactly as they were, so nothing is destroyed and the sheet can be added back. Identify it by name or number. Answers with how many sheets remain. Writes to the shared .DST under a lock. |
| `rename_sheet` | Rename and renumber a sheet in one locked write - AutoCAD's own "Rename & Renumber Sheet". Pass number, title, or both; at least one is required. A sheet has NO separately stored name: what the Sheet Set Manager displays is its number and title composed together, so those two are what renaming a sheet actually sets. Answers with all three fields as they were and as they now are. Pass "" as the title to clear it. |
| `reorder_sheet` | Move a sheet up or down the drawing list, placing it before or after another sheet. Exactly one of before or after is required; both name a sheet by its name or number. Ordering happens WITHIN one subset - if the two sheets sit in different subsets the tool refuses and points at move_sheet_to_subset, so that 'put A-102 after A-101' can never quietly relocate a sheet. Writes to the shared .DST under a lock. |
| `set_sheet_do_not_plot` | Mark one sheet do-not-plot, or clear that mark. The Publisher skips a do-not-plot sheet rather than failing the job, so this is how a sheet is held back from an issue without being removed from the set. Writes to the shared .DST under a lock. Pass doNotPlot=false to put the sheet back into the next publish. |
| `set_sheet_number` | Renumber one sheet in a sheet set - the 'A-101' that appears in the title block and orders the drawing list. Writes to the shared .DST: it is locked for the call, saved, and unlocked. Addresses the sheet by its current name OR its current number, and answers with both the old and the new number so the edit can be undone from the result alone. |
| `set_sheet_property` | Set a custom property on ONE sheet - the per-sheet value a title block prints, such as its revision or who checked it. Writes to the shared .DST under a lock and creates the property on that sheet if it does not exist yet. Refuses the built-in fields name, number, title and description, naming the tool that sets each, because writing one here would create a second property sharing the name and only one of them would mean anything. Answers with the previous value and whether it was created. |
| `set_sheet_title` | Set a sheet's title - the descriptive line a title block prints under the number, such as 'Ground Floor Plan'. Writes to the shared .DST under a lock. The sheet's displayed name is composed from its number and title, so setting the title moves that name too. Pass "" to clear the title. AutoCAD itself rejects an empty title, so the tool sends a space and the file stores it as empty - the result reports the "" that will be on disk, not the space that is briefly in memory. |

## acad-styles

| Tool | Description |
|---|---|
| `apply_dimstyle_override` | Override chosen properties on ONE dimension entity, leaving its named style and every other dimension untouched. Use this when a single dimension needs a smaller text or a different arrow without inventing a whole style for it. Property names are the same as create_dimstyle - see list_dimstyle_properties. The result reports every override the dimension carries afterwards, not just the ones just set, so a caller sees the accumulated state. |
| `copy_dimstyle` | Duplicate a dimension style under a new name, optionally overriding some properties in the same call. This is how a 1:100 style is made from a 1:50 one: copy it, override scale, done - and the two changes stay atomic instead of leaving a half-made style behind if the second call fails. |
| `create_dimstyle` | Create a named dimension style with chosen properties - text height, arrow size, decimal places, overall scale and the rest. Pass properties as a name-to-value map; use list_dimstyle_properties for the names and ranges. An unknown property name or an out-of-range value is an error, never silently skipped, because skipping would report success over a style that is not what was asked for. Refuses an existing name unless overwrite is true. |
| `create_layer_filter` | Create a PROPERTY layer filter from an expression, so layers created later that match it join automatically. Expressions look like NAME=="A-*" or COLOR=="1", combined with AND / OR / NOT. Nest it under an existing filter with parent. The result reports matchCount - check it, because a valid expression that selects nothing is stored and listed exactly like one that works. |
| `create_layer_group_filter` | Create a GROUP layer filter holding a fixed list of named layers. Unlike a property filter this never changes on its own - a layer added to the drawing afterwards does not join it. Use this when the set is a decision rather than a pattern. Every named layer must already exist; naming one that does not is an error rather than a silently smaller group. |
| `create_mleaderstyle` | Create a named multileader style with chosen properties - text height, arrow size, dogleg length, whether the leader has a landing, how many points it may have. Pass properties as a name-to-value map; an unknown name or an out-of-range value is an error rather than a silent skip. Refuses an existing name unless overwrite is true. |
| `create_mlinestyle` | Define a named multiline (MLINE) style from a list of parallel line elements, each given an offset from the centreline plus an optional colour and linetype. This is how a wall type is defined once and drawn many times: a 200mm wall is two elements at +100 and -100. Offsets are in drawing units and may be negative. Refuses an existing name unless overwrite is true, and refuses to redefine a style that entities already use, because AutoCAD does not allow that and reporting success would be a lie. |
| `create_tablestyle` | Create a named table style with chosen properties - cell margins, flow direction, and text height per row type. This is what makes a generated door or room schedule match the rest of the set instead of arriving at AutoCAD's defaults. Refuses an existing name unless overwrite is true. |
| `create_visual_style` | Create a named visual style derived from one of AutoCAD's presets - Conceptual, Realistic, Shaded, Hidden, Wireframe2D and the rest. Call list_visual_styles first for the full preset list. This deliberately does NOT expose per-trait authoring: DBVisualStyle offers only an untyped trait API with no property catalogue to advertise, so a tool promising arbitrary edits could not tell a caller what it accepts. Apply the result to a viewport with set_viewport_visual_style. |
| `delete_dimstyle` | Delete a dimension style. Refuses to delete 'Standard', refuses to delete the current style, and refuses a style still in use - with the reason, and a pointer to dimensions.set_entity_dimstyle for moving the dimensions off it first. |
| `delete_layer_filter` | Delete a layer filter. Deleting one that has nested filters takes those with it, and the result names them, because a filter count that dropped further than expected is otherwise a mystery. Refuses AutoCAD's built-in filters. Layers themselves are never touched - a filter is a view of them, not a container. |
| `delete_mleaderstyle` | Delete a multileader style. Refuses to delete 'Standard', refuses to delete the current style, and refuses one still in use - with the reason rather than a bare AutoCAD error code. |
| `delete_tablestyle` | Delete a table style. Refuses to delete 'Standard', refuses to delete the current style, and refuses one still in use - with the reason rather than a bare AutoCAD error code. |
| `import_dimstyle_from_dwg` | Copy dimension styles out of another drawing file into this one - the practical way to adopt an office standard without rebuilding it property by property. Name the styles to take, or omit names to take every non-Standard one. Existing names are SKIPPED unless overwrite is true, and the result lists imported and skipped separately, read back from the clone mapping rather than from what was asked for, because 'imported' that quietly meant 'did nothing' is the exact failure this reports around. |
| `list_dimstyle_overrides` | Report the properties on which ONE dimension differs from the named style it carries, with both values side by side. Read-only. This is the tool for the question 'why does this dimension look different from the others' - AutoCAD stores no list of overrides, so it is worked out by comparing the dimension's effective values against its style. count 0 means it matches on every property this bank authors, which the note says explicitly rather than leaving as an empty answer. |
| `list_dimstyle_properties` | List every dimension-style property this bank can set, with the AutoCAD DIMVAR behind it, what it does, and its valid range. Read-only. Call this first: the property names here are plain (textHeight, arrowSize, decimalPlaces) rather than DIMVAR spellings, because nobody should have to know that a text height is called DIMTXT in order to set one. |
| `list_layer_filters` | List every layer filter in the drawing - both kinds - with the expression or layer list behind it and how many layers it currently selects. Read-only. matchCount is the field to read after creating one: an expression can be perfectly valid, be stored, be listed, and select nothing, which no return code can tell you. |
| `list_mleaderstyle_properties` | List every multileader-style property this bank can set, with the API member behind it, what it does and its valid range. Read-only. Booleans travel as 0 or 1 so the whole properties argument stays one map of names to numbers - two value types in one dictionary would be two ways to be wrong about it. |
| `list_mleaderstyles` | List the multileader styles defined in this drawing with all their properties, and which one is current. Read-only. |
| `list_mlinestyles` | List every multiline (MLINE) style in the drawing with its parallel line elements, total width, end caps and whether anything is currently drawn with it. Read-only. inUse matters before deleting or redefining one: AutoCAD refuses to change a style that existing MLINE entities reference, so this is the call that tells you why a redefinition would fail. |
| `list_tablestyle_properties` | List every table-style property this bank can set, with the API member behind it, which row it applies to, what it does and its range. Read-only. Text heights are per row - titleTextHeight, headerTextHeight, dataTextHeight - because a schedule's caption, its column headings and its content are three different sizes and pretending otherwise is how tables end up unreadable. |
| `list_tablestyles` | List the table styles defined in this drawing with all their properties, and which one is current. Read-only. The schedules family draws into whichever style is current, so this is what tells you what a generated door or room schedule will look like before you generate it. |
| `list_visual_styles` | List every visual style in the drawing with the preset it derives from, plus the full set of preset names available to create_visual_style. Read-only. Styles AutoCAD keeps for its own rendering passes are flagged internalUseOnly rather than hidden, because omitting them would misreport what the drawing contains. |
| `modify_dimstyle` | Change properties on an existing dimension style, leaving the rest alone. The stored style changes immediately; dimensions already placed pick it up on the next regen, and the result says so - an unchanged screen after this call is not a failed call. |
| `modify_mleaderstyle` | Change properties on an existing multileader style, leaving the rest alone. The stored style changes immediately; multileaders already placed pick it up on the next regen, and the result says so. |
| `modify_mlinestyle` | Change an existing multiline style, leaving anything you do not pass alone. Passing elements REPLACES the whole element list rather than merging into it - a partial merge has no meaning when the elements are an ordered geometric set. Refuses a style that MLINE entities already reference, which is an AutoCAD restriction and not a choice made here. |
| `modify_tablestyle` | Change properties on an existing table style, leaving the rest alone. The stored style changes immediately; tables already placed pick it up on the next regen, and the result says so. |
| `set_current_dimstyle` | Make a dimension style the current one, so dimensions placed afterwards use it. Returns the style with all its properties, so the caller can confirm what they just switched to rather than trusting the name. |
| `set_current_mleaderstyle` | Make a multileader style the current one, so leaders placed afterwards use it. Returns the style with all its properties, so the caller can confirm what they switched to rather than trusting the name. |
| `set_current_tablestyle` | Make a table style the current one, so tables created afterwards use it - including the ones the schedules family generates. Returns the style with all its properties so the caller can confirm what they switched to. |
| `set_point_display` | Set how POINT entities are drawn, drawing-wide. Give a plain glyph name - dot, none, plus, cross, tick - with an optional surround of circle, square or both, and this works out the PDMODE bit code for you; pass mode instead if you already know it. size sets PDSIZE: positive is absolute drawing units, negative is a percentage of the viewport. This is NOT a style object - AutoCAD has no per-point style, only these two system variables, so the change applies to every point in the drawing. |
| `set_table_cell_style` | Set the per-cell properties of a table style - text height, alignment, text colour and background - for one cell class. A table style keeps a separate set of these for each class, typically _TITLE, _HEADER and _DATA, which is why the style-wide create/modify tools cannot reach them. Pass backgroundColorIndex as -1 to clear a background rather than set one. The result reports the cell's full state afterwards, so a caller sees what the other properties still are. |

## acad-ucs

| Tool | Description |
|---|---|
| `create_ucs_3point` | Define a UCS from three points: origin, a point on the positive X axis, and a point on the positive-Y side. This is the general case - it fixes origin, rotation and plane in one call. Pass a name to save it; makeCurrent defaults to true. |
| `create_ucs_from_entity` | Align the UCS to an existing entity's own plane and orientation, given its handle. The fastest way to start drawing on something already in the model - a wall face, a sloped polyline, a rotated block. |
| `create_ucs_from_view` | Set the current UCS so its XY plane faces the screen - AutoCAD's UCS View. This is what makes text and dimensions placed on an isometric or a rotated view read straight instead of lying flat on the model's own plane. The origin stays where the current UCS has it, because changing what you are looking at should not move where coordinates are measured from. |
| `create_ucs_origin` | Move the UCS origin without changing its axis directions. The cheapest useful UCS: work near a building corner in small local coordinates instead of large absolute ones. |
| `create_ucs_zaxis` | Define a UCS from an origin and a Z-axis direction; X and Y are derived. Use this for work on an inclined plane - a roof slope, a ramp, a sloped section. |
| `delete_ucs` | Delete a named UCS. The current UCS is unaffected even if it happens to match the deleted definition. |
| `get_current_ucs` | Return the current UCS: origin and the three axis vectors in WCS, plus whether it is the world system. Read-only. |
| `list_ucs` | List every named UCS in the drawing with its origin and axes, plus the current one. Read-only. Call this before restore_ucs to see what names exist. |
| `rename_ucs` | Rename a saved UCS. The new name must be a valid AutoCAD symbol name and must not already exist. |
| `restore_ucs` | Make a previously saved named UCS current. Errors if the name does not exist rather than silently leaving the current UCS in place. |
| `rotate_ucs` | Rotate the current UCS about its own X, Y or Z axis by an angle in degrees. axis is 'x', 'y' or 'z'. Rotating about Z is the usual case: aligning to a wall that does not run north-south. |
| `save_ucs` | Save the current UCS under a name so it can be restored later. Named UCSs are how a multi-storey or multi-wing project keeps its local coordinate systems addressable. |
| `set_ucs_previous` | Step back to the UCS in use before the last change, like AutoCAD's UCS Previous. The history covers changes made through these tools in this session only - a UCS changed by hand in AutoCAD is not in it, and the tool says so rather than silently doing nothing. Reports how many steps remain. |
| `set_ucs_world` | Reset the current UCS to WCS. Every tool in the bank interprets coordinates in WCS by default, so this returns the drawing to the state those tools assume. |
| `transform_point` | Convert one point between coordinate systems. 'from' and 'to' each accept 'world', 'current', or a saved UCS name. Use this to work out WCS coordinates for the drawing tools while they are still WCS-only. |

## acad-validators

| Tool | Description |
|---|---|
| `add_validator_rule` | Persist a brand-new validator rule to the user-rules directory (%LOCALAPPDATA%/AcadMcp/validators/_user/<discipline>/<id>.yaml) and reload the registry. The 'yaml' argument is the full YAML document text. Returns the assigned id and on-disk path. |
| `auto_fix_violations` | Apply the fix recipe for every fixable violation in the cached report (or only those for the supplied ruleIds). All fixes run inside a SINGLE plugin transaction (rule 34 §3); failure rolls back the whole batch. Set dryRun=true to preview the planned actions without writing. |
| `check_overlaps` | Find pairs of entities whose bounding boxes (or curves, for mode="polyline_crosses_polyline") overlap or intersect. Purely geometric - schema-free, does NOT need a validator rule. Intended for the AI visual-review pipeline: e.g. "which A-DOOR entities actually pierce an A-WALL-* polyline" or "which A-ANNO-TEXT labels stack on top of each other". Args: layersA (required, e.g. ["A-DOOR"]), layersB (optional, defaults to layersA for self-overlap), mode in { "bbox_intersect" (default), "centroid_in_bbox", "polyline_crosses_polyline" }, tolerance (mm, default 0), optional window rectangle to restrict to a region, maxResults (default 500). Result is sorted by severity (critical=2+ curve intersections, major=1 intersection or overlap>10000 sq-mm, minor=smaller overlap) then by overlap area descending. Handles are order-stable across calls. |
| `explain_rule` | Return the full definition of one validator rule by id - severity, discipline, description, references, scope, list of checks and the optional fix recipe. Pure local query - does not touch AutoCAD. |
| `list_standards` | List every bundled standard preset (id, human name, ordered list of rule ids it expands to). Standards are convenience presets for validate_against_standard. Pure local query. |
| `list_validators` | List every available validator rule (id, name, severity, discipline, fix-available, description). Optional filters: discipline (general\|architectural\|mechanical\|electrical\|civil\|mep) and minSeverity (info\|warning\|error). Pure local query - does not touch AutoCAD. |
| `list_violations` | Return the most recent ValidationReport produced for the active document. The cache is keyed by document name + path so opening a different drawing returns 'no report yet' (rule 34 §9). |
| `reload_validator_rules` | Re-scan embedded resources, <repo>/validators and %LOCALAPPDATA%/AcadMcp/validators for rule YAML files and rebuild the in-process registry. Returns the new total rule count and any load errors. |
| `validate_against_standard` | Resolve a standard id (e.g. 'iso-cad-baseline') to its rule set and run validate_drawing for that bundle. Use list_standards first to discover available presets. |
| `validate_drawing` | Run a set of validator rules against the active document and return a structured report (per-rule counts, every violation with handle/dxfType/layer/expected/observed/fixAvailable). Optional filters: ruleIds (explicit list, overrides everything else), discipline, minSeverity, includePaperspace. |
| `validate_with_rule` | Run exactly one validator rule (by id) against the active document and return a focused report. Throws if the ruleId is unknown. |

## acad-verticals

| Tool | Description |
|---|---|
| `draw_handrail` | Draw a handrail run as a polyline on A-RAIL with an optional mm-height annotation. Warns when heightMm falls outside WT §298 public stair range 900..1100 mm. |
| `draw_ramp` | Draw an accessibility ramp: rectangle outline, slope arrow (up or down) along length, percentage label. Emits compliance warnings against WT §66 (accessible ramp max 6% for rise > 500 mm). |
| `draw_stair_spiral` | Draw a spiral stair in plan view: outer arc, inner arc (column), radial tread lines, centre dot + label. Rule 67 §3 (spiral). |
| `draw_stair_straight` | Draw a straight-run stair in plan view: outline rectangle, tread lines, UP/DN direction arrow with label, all on layers A-STRS and A-STRS-DIR. Computes tread depth from runLengthMm / treadCount and emits compliance warnings against WT §54 (public stair: 150-175 mm riser, 250-350 mm tread, 1200 mm min clear width). |
| `draw_stair_u_shaped` | Draw a U-shaped (two-run, 180° return) stair: two straight runs separated by gapMm + one landing rectangle. Each run returns its own compliance warnings. |
| `insert_elevator_v` | Draw an elevator shaft in plan: rectangle outline, two diagonal lines (indicates lift symbol per ISO 7001), centred kind label. Kind must be 'passenger', 'bed' or 'goods'; the tool warns when cabin size falls below the kind-specific minimum (bed-lift 1600×2600 in hospitals per WT §193; passenger lift 1100×1400). |
| `insert_escalator` | Draw an escalator run: rectangle outline, evenly-spaced step lines, direction arrow, UP/DN label. Rotation locates the lower landing at origin. |
| `insert_platform_lift` | Draw a platform lift (wheelchair lift / stair-chair): rectangle outline + PL label. Default 1100×1400 mm matches PN-EN 81-41. |

## acad-view

| Tool | Description |
|---|---|
| `get_current_view` | Return the currently active view's center point, width, height and paper-space flag. Use to confirm a zoom actually took effect before capturing. |
| `list_views` | List all named views stored in the drawing's VIEW table with their center point and size. Use to pick a saved architectural view (e.g. "FLOOR-1", "SITE", "DETAIL-A") before capture. |
| `set_current_view` | Restore a named view by name (equivalent to the VIEW _R <name> command). Fails with a clear error if the name doesn't exist in the VIEW table. |
| `zoom_all` | Zoom the active view to the drawing limits + extents (ZOOM _A). Shows every drawing limit rectangle as well as the entity extent. |
| `zoom_center` | Zoom to a specific center point with a requested view height in drawing units (ZOOM _C <center> <height>). Useful to frame a named fixture at a known scale. |
| `zoom_extents` | Zoom the active model-space view to fit the bounding box of all entities (ZOOM _E). Use as a reset between regional captures. |
| `zoom_scale` | Zoom the active view by a relative scale factor (ZOOM _S <factor>x). factor>1 zooms in, 0<factor<1 zooms out. |
| `zoom_window` | Zoom the active model-space view to the axis-aligned rectangle defined by two corner points (drawing units). Use before acad.files.export_file scope="Display" to capture a specific region as PNG, or to frame an area for visual inspection. Corners can be in any order; the tool normalises them. This changes what the user sees in AutoCAD and what PlotType.Display would capture. |

## acad-viewports

| Tool | Description |
|---|---|
| `clip_viewport_by_object` | Clip a viewport to an existing closed shape - a closed polyline, circle or ellipse already in paper space. Use this when create_polygonal_viewport's vertex list is the wrong tool because the outline already exists, for instance a site boundary traced earlier. The shape must be closed; an open polyline is refused rather than assigned and left to corrupt the viewport later. |
| `create_polygonal_viewport` | Create a non-rectangular paperspace viewport from an ordered vertex list in paper-space coordinates. Needs at least 3 vertices; the outline is closed automatically. Use this for L-shaped or angled sheet windows. |
| `create_viewport` | Create a rectangular paperspace viewport on the named layout: a window width x height centred at 'center' in paper-space coordinates. Switches to that layout and back on its own, so it works from model space. Optional scale is the model-to-paper factor (0.02 = 1:50). |
| `delete_viewport` | Delete a paperspace viewport by handle. The model geometry it showed is untouched - only the window is removed. |
| `get_viewport_extents_in_model` | Return the model-space rectangle a viewport is currently showing, derived from its centre, paper size and scale. Use this to work out what geometry a sheet window actually covers before annotating it. |
| `get_viewport_info` | Full descriptor of one viewport by handle, including its frozen layers and which layers carry property overrides. |
| `list_viewports` | List paperspace viewports with handle, layout, paper geometry, scale, lock state and how many layer overrides each carries. Pass layoutName to restrict to one tab, omit it for the whole drawing. Read-only. |
| `set_viewport_annotation_scale` | Set the annotation scale of a paperspace viewport, which decides what size annotative text and dimensions plot at in that window, and which annotative objects appear in it at all. By default the viewport's zoom scale is set to match, the way AutoCAD's own UI keeps the two linked; pass syncViewScale false to set annotation scale alone. The scale must already be in the drawing's list - use annotative.add_scale_to_list first if it is not. |
| `set_viewport_layer_freeze` | Freeze layers in ONE viewport only. This is the mechanism that lets a single model produce an architectural plan and a fire plan: freeze the layers each sheet must not show, in that sheet's viewport, without touching the model or any other viewport. |
| `set_viewport_layer_thaw` | Thaw layers that were frozen in one viewport, so they display there again. Layers not frozen in that viewport are left alone. |
| `set_viewport_lock` | Lock or unlock a viewport. A locked viewport cannot have its zoom or scale changed by panning inside it, which is the single most common way an issued sheet silently ends up at the wrong scale. |
| `set_viewport_on_off` | Turn a viewport's display on or off. An off viewport keeps its position, size and scale but renders nothing - useful for sheets under construction without deleting the window. |
| `set_viewport_scale` | Set the model-to-paper scale of a viewport (0.02 = 1:50, 0.01 = 1:100, 0.001 = 1:1000). Locking the viewport afterwards is what stops an accidental zoom changing the drawn scale of an issued sheet. |
| `set_viewport_shade_plot` | Set how a viewport plots: 'AsDisplayed', 'Wireframe', 'Hidden' or 'Rendered'. Hidden is what removes obscured 3D edges on a plotted sheet without changing the model. |
| `set_viewport_twist` | Rotate the view inside a viewport by an angle, without rotating the model. Use it to put a wing of a building square on the sheet when it sits at an angle in the model - the drawing reads straight while the geometry stays where the survey put it. |
| `set_viewport_ucs` | Give one paperspace viewport its own coordinate system, independent of the drawing's current UCS. This is what lets one sheet annotate a rotated wing of a building in that wing's own coordinates while the neighbouring viewport stays on the world axes. Pass a saved UCS name, or 'world' to clear it. Also sets UCSVP so the setting is stored with the viewport instead of vanishing at the next layout switch. |
| `set_viewport_view_direction` | Set which way a viewport looks at the model: a named preset (top, bottom, front, back, left, right, sw-iso, se-iso, ne-iso, nw-iso) or an explicit direction vector. This is what turns one 3D model into a plan, an elevation and an isometric on the same sheet without duplicating geometry. |
| `set_viewport_visual_style` | Set the visual style a viewport displays and plots with - 2dWireframe, Hidden, Realistic, Conceptual, Shaded and whatever else the drawing defines. Unknown names are refused with the list of what this drawing actually has, since visual styles are per-drawing rather than fixed. |
| `sync_viewport_to_annotation_scale` | Set a viewport's zoom scale to match the annotation scale it already carries. set_viewport_annotation_scale does this by default; this tool is for repairing a viewport where the two have drifted apart - text sized for 1:50 on a window drawn at 1:100. Reports the scale before and after and whether anything changed. |

## acad-vision

| Tool | Description |
|---|---|
| `classify_drawing` | Use a vision LLM (Anthropic Claude or OpenAI GPT-4o, whichever has an API key) to classify a drawing's discipline (arch / mech / elec / pid / civil / unknown) and sheet type (plan / section / detail / schedule / title / isometric / unknown). Returns a JSON verdict with confidence and a one-line rationale. Cached by image content hash. |
| `cross_validate_with_dxf` | Compare a list of OCR'd strings against a list of DXF text strings (the latter typically harvested by exporting the active document via acad.files.export_file -> DXF and walking the entity stream). Returns matched / only-in-OCR / only-in-DXF buckets after case + whitespace normalisation. Optional numericTolerance (>0) treats numeric tokens within the tolerance as matched (e.g. 12.5 vs 12.6). |
| `describe_image` | Free-form vision LLM description of any image (defaults to a CAD-reviewer prompt). Provider is "auto" (prefer Anthropic if ANTHROPIC_API_KEY is set, else OpenAI), "anthropic" or "openai". Image is downscaled to <=1568 px long side and JPEG-compressed q85 before sending. OPTIONAL persona argument selects a curated review template: "architect-reviewer" (EN, Polish licensed architect reviewing a hospital floor plan at 1:100, structured output under walls-and-openings / doors / labels / code-compliance / visual-craft, with severity critical\|major\|minor and suggested MCP fix), "architect-reviewer-pl" (same in Polish), "delta-compare" (before/after regression check). When persona is set and prompt is left at default, the persona template replaces the prompt; otherwise the prompt is appended as "User focus: ...". Cached by content hash + provider + persona + first 64 chars of the composed prompt. |
| `detect_symbols` | Run a custom YOLO CAD-symbol detector on a raster image. Discipline picks the per-discipline weights file: "arch" / "mech" / "elec" / "pid". Returns labelled bounding boxes (pixel coords) with confidence. If weights are missing returns 503 with an installHint pointing to scripts/setup-vision-models.ps1. |
| `extract_dimensions` | Extract dimension callouts from a raster drawing. Filters OCR tokens that look like dimensions (e.g. 1234, 12.5 mm, 12'-6"), parses them to a numeric value in millimetres when possible, and returns each token with its pixel box and confidence. Units may be "mm", "cm", "m", "in", "ft" or "auto" (rely on the OCR'd unit suffix). |
| `extract_titleblock` | Extract title-block fields (drawing_no, title, scale, date, drawn_by, checked_by, project, rev, sheet, ...) from a raster drawing. Pass a discipline hint ("architectural-eu" default, "architectural-us", "mechanical", "electrical", "civil") so the right field-alias dictionary is used. Returns canonical field keys + raw OCR labels + values with confidence and the panel rectangle. |
| `ocr_image` | Run OCR on a raster image or a single PDF page. Returns recognised text tokens with per-token confidence and pixel bounding boxes (top-left origin). Engine is one of "paddleocr" (default, best on CAD), "easyocr", "tesseract". Image is referenced by absolute file path or base64 data URL; for PDFs supply page (1-based) and dpi. Uses on-disk cache keyed by content hash + engine + version. |
| `vision_health` | Probe the AcadMcp.Vision Python sidecar at its discovered base URL (env ACADMCP_VISION_PORT, then %LOCALAPPDATA%\AcadMcp\vision.port, then default 50062). Returns status, version, phase and uptime. Use this to confirm the sidecar is reachable before calling other vision tools. |
| `vision_version` | Return the AcadMcp.Vision sidecar version, phase, the availability flags of every optional ML dep (paddleocr, easyocr, tesseract, ultralytics, torch, sam2, anthropic, openai, pypdfium2) and whether vision-LLM API keys are present. Use this to tell the user exactly which install command they need. |

## acad-xrefs

| Tool | Description |
|---|---|
| `attach_xref` | Attach an external drawing as an XREF (attachment). Attachments are carried into any drawing that in turn references this one - use attach_xref_overlay when you do not want that. Block name defaults to the file name; insertion, scale and rotation default to origin/1/0. |
| `attach_xref_overlay` | Attach an external drawing as an OVERLAY. Overlays are NOT carried through when this drawing is itself referenced elsewhere, which is what stops circular and duplicated references in a multi-discipline set. Prefer this for cross-discipline backgrounds. |
| `bind_xref` | Bind an XREF into this drawing, making it a permanent local block. Default (bind mode) renames dependent symbols to blockName$0$LAYER; insertMode=true merges them into existing local symbols instead, which is usually what you want for issue-ready files but can collide with local names. |
| `clip_xref_by_object` | Clip an external reference to an outline that already exists in the drawing - a closed polyline or a circle - instead of retyping its coordinates. On a real project the boundary is usually already drawn: a site outline, a fire compartment, a lease line. Set inverted to clip away the inside instead of the outside. A circle is approximated with 64 segments, finer than any plotted line width. |
| `clip_xref_polygonal` | Clip one XREF insert to an arbitrary closed polygon given as an ordered vertex list in WCS. Needs at least 3 vertices; the polygon is closed automatically. |
| `clip_xref_rect` | Clip one XREF insert to a rectangle given by two opposite corners in WCS. inverted=true hides what is inside the rectangle instead of outside. Clipping is per-insert, so pass a handle from get_xref_info, not a block name. |
| `delete_xref_clip` | Remove the clip boundary from an XREF insert so the whole reference displays again. Succeeds quietly when there was no clip. |
| `detach_xref` | Detach an XREF completely: removes the definition and every insert of it. Fails if the xref is nested under another reference - detach the parent instead. |
| `find_missing_xrefs` | List every XREF whose file cannot be resolved at its saved path. Read-only; pair with set_xref_path or repath_all_xrefs to fix them. |
| `get_xref_info` | Full descriptor of one XREF by block name, plus the handles of every BlockReference insert of it. Use the returned handles for clipping, which is per-insert. |
| `invert_xref_clip` | Flip an existing clip boundary inside-out: what was hidden becomes visible and the reverse. Errors if the insert has no clip. |
| `list_nested_xrefs` | List XREFs that are referenced by another XREF rather than directly by this drawing, with their parent. Nested references cannot be detached or repathed here - do that in the parent drawing. |
| `list_xref_dependent_symbols` | List the layers, linetypes, text styles, dimension styles and blocks that arrive in this drawing through one XREF. These are the names that get renamed on bind - check here before binding to predict collisions. |
| `list_xrefs` | List every XREF in the drawing with its path, resolution status, overlay/attachment kind, nesting and insert count. Read-only. This is the tool to call first when a drawing does not look right. |
| `reload_all_xrefs` | Reload every resolved XREF in the drawing. Reports per-xref status so partial failures are visible. |
| `reload_xref` | Reload one XREF from disk, picking up changes another author has saved. Returns the resolved status so a failure to find the file is visible rather than silent. |
| `repath_all_xrefs` | Bulk path repair: replace oldPrefix with newPrefix in every XREF path. dryRun=true reports what would change, including whether each new path actually resolves, without writing anything. Run the dry run first. |
| `reset_xref_layer_overrides` | Drop layer overrides for one XREF, returning its layers to the properties defined in the source drawing. Pass a layer name to reset just that one, or omit it to reset all of them. |
| `set_clip_frame_display` | Show or hide clip boundary frames, for the WHOLE DRAWING. This does not change what any clip hides and it is not scoped to one reference: XCLIPFRAME is a drawing-wide system variable, so every clipped xref and block follows it together. Three modes, and the middle one is the useful one while laying out: 'hidden', 'display' (visible on screen, never plotted) and 'displayAndPlot'. The result reports the previous mode as well, so a caller can put it back. |
| `set_xref_layer_override` | Override colour, linetype, lineweight, on/off or frozen state for one layer coming from an XREF, without touching the source drawing. This is how a background reference is greyed back on a plan. |
| `set_xref_path` | Point one XREF at a different file. relativePath=true stores it relative to this drawing, which is what survives moving the project folder. Reloads by default so the result is immediately verifiable. |
| `unload_xref` | Unload an XREF: its geometry stops displaying and stops loading, but the definition and inserts stay so it can be reloaded later. Use this rather than detach for temporarily hiding a heavy reference. |
