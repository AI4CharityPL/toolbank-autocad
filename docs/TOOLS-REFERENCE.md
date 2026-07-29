# MCP Nexus AutoCAD — Full Tool Reference

Every MCP tool exposed by every category backend (`AcadMcp.Backend.exe --category <name>`), generated directly from the manifests in [`mcpbank-manifests/`](../mcpbank-manifests) so it can't drift from what's actually registered. Regenerate after adding/renaming tools.

**Total: 30 categories, 337 tools.**

## Categories

- [`annotations`](#annotations) — 12 tool(s)
- [`architecture`](#architecture) — 16 tool(s)
- [`blocks`](#blocks) — 16 tool(s)
- [`boolean-ops`](#boolean-ops) — 8 tool(s)
- [`callouts`](#callouts) — 5 tool(s)
- [`civil`](#civil) — 10 tool(s)
- [`dimensions`](#dimensions) — 17 tool(s)
- [`electrical`](#electrical) — 12 tool(s)
- [`files`](#files) — 11 tool(s)
- [`furniture`](#furniture) — 10 tool(s)
- [`geometry-2d`](#geometry-2d) — 32 tool(s)
- [`geometry-3d`](#geometry-3d) — 15 tool(s)
- [`grids`](#grids) — 6 tool(s)
- [`hatches`](#hatches) — 8 tool(s)
- [`layers`](#layers) — 14 tool(s)
- [`layouts`](#layouts) — 10 tool(s)
- [`mechanical`](#mechanical) — 12 tool(s)
- [`modify`](#modify) — 18 tool(s)
- [`openings`](#openings) — 10 tool(s)
- [`parametric`](#parametric) — 12 tool(s)
- [`plotstyles`](#plotstyles) — 3 tool(s)
- [`plumbing`](#plumbing) — 9 tool(s)
- [`router`](#router) — 10 tool(s)
- [`schedules`](#schedules) — 9 tool(s)
- [`sections`](#sections) — 4 tool(s)
- [`selection`](#selection) — 12 tool(s)
- [`validators`](#validators) — 11 tool(s)
- [`verticals`](#verticals) — 8 tool(s)
- [`view`](#view) — 8 tool(s)
- [`vision`](#vision) — 9 tool(s)

## annotations

AutoCAD text and annotation entities: single-line DBText, multi-line MText with inline formatting, leaders and multi-leaders (MLeader) with text or block content, basic Tables built from row/column data, points with point styles, and text style management. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

**12 tools:**

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

## architecture

High-level architectural plan-view operations: walls (with centreline + two faces), doors (with wall opening + swing), windows (sill, glass, opening), columns, rooms (boundary + tag with computed area), floor slabs, stairs, and intelligent dimensioning. Composes primitives from acad-geometry-2d, acad-blocks, acad-layers, acad-annotations, and acad-dimensions while auto-creating the AIA-style architectural layer key. Pairs with acad-validators rules under validators/architectural/.

**16 tools:**

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
| `insert_door` | Insert a door at a hinge point. Draws the door panel (rectangle width × frameThicknessMm on A-DOOR) at the requested opening angle plus a swing arc (default quarter-circle, on A-DOOR-SWING). swingDirection='left' (default) hinges on the LEFT side of the wall axis; 'right' hinges on the RIGHT. NOTE v1: this tool DOES NOT cut a hole in the host wall — that step ships in Phase 7. Until then, use a follow-up acad-geometry2d.trim_curve / boolean op against the wall faces to punch the opening. |
| `insert_elevator` | Draw an elevator shaft on A-STRS as a rectangle with two diagonal lines (X) plus a centred label on A-ANNO-NOTE. No cab / mechanical details — use this as a plan-view placeholder for lifts/verticals. For more detail use acad-verticals in Phase D7. |
| `insert_ramp` | Draw a simple rectangular ramp outline on A-STRS plus a slope arrow (shaft + head) along the travel direction and a text label reporting the gradient as 'N% RAMP' on A-ANNO-NOTE. widthMm runs perpendicular to directionDeg, lengthMm runs along it. |
| `insert_rect_column` | Insert a rectangular structural column profile on layer S-COLS plus a small crosshair centre-mark on S-COLS-CTRL. width = X-axis, depth = Y-axis (before rotation). Column is auto-centered on the supplied point. |
| `insert_round_column` | Insert a circular structural column on layer S-COLS plus a small crosshair centre-mark on S-COLS-CTRL. |
| `insert_stair` | Draw a simple straight-run stair on A-STRS: outline rectangle (widthMm × runLengthMm), treadCount-1 perpendicular tread lines at equal spacing, and a travel-direction arrow (shaft + head). The arrow ends with an 'UP' label (configurable) on A-ANNO-NOTE. directionDeg points along the run (0 = +X). For multi-flight or spiral stairs use acad-verticals in Phase D7. |
| `insert_window` | Insert a window centred at a point along a wall axis. Draws 5 entities on A-GLAZ: the sill line (wall side closer to exterior), the glass line (in the middle of the wall), the header line (wall side closer to interior), and two perpendicular jamb lines closing the opening. NOTE v1: this tool DOES NOT cut the host wall — see insert_door note. rotationDeg is the wall's heading in degrees (0 = horizontal, +90 = vertical going up). |
| `split_wall_at_opening` | Cut a hole for a door/window in a wall entity — wrapper around acad.openings.cut_wall_for_opening. Workflow: (1) call split_wall_at_opening(wallHandle, jamb1, jamb2) BEFORE insert_door / insert_window so the wall faces are trimmed at the jambs; (2) then call the opening tool. v1 inherits the wrapped primitive's limitation (Line + 2-vertex Polyline walls); multi-vertex polyline walls will be supported once acad-verticals lands in Phase D7. |

## blocks

AutoCAD block (BlockTableRecord) authoring and instancing: define a block from existing entities, list / inspect / rename / purge block definitions, insert BlockReference instances with explicit attribute values, list and update attributes on existing references, explode references back to entities, and import block definitions across DWG files via WblockCloneObjects. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

**16 tools:**

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

## boolean-ops

Boolean (Constructive Solid Geometry) operations on AutoCAD 3D solids and 2D regions: union / subtract / intersect on Solid3d entities, the same set on Region entities, plus utilities for building Regions from closed planar curves and probing whether two entities intersect (boolean test or actual intersection points). All operations consume the 'tool' entity (it is removed from ModelSpace) just like the AutoCAD UNION / SUBTRACT / INTERSECT commands, and require the AcadMcp .NET plugin loaded inside an open AutoCAD session. Not supported on AutoCAD LT.

**8 tools:**

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

## callouts

Profile callouts (K1 column / K6 elevation profile / K10 stair step), north arrows (simple/compass/ISO-129), scale bars (1:50/1:100/1:200), finish callouts (floor/wall/ceiling codes). Optional per-project for architectural detail depth.

**5 tools:**

| Tool | Description |
|---|---|
| `insert_detail_callout` | Mark a rectangular/circular area for a detail on a separate sheet. Draws a detail circle of radius radiusMm around center, a dashed-style leader line to leaderEndPoint, and a callout bubble containing the label and target scale. If leaderEndPoint is null, the bubble is positioned at radiusMm*2 to the upper-right of the centre. |
| `insert_north_arrow` | Insert a north arrow symbol (ISO 5455) at the given model-space position. The symbol is a circle plus an inscribed north-pointing arrow plus a "N" label above. Plotted diameter is PlotNorthDiameterMm (30 mm) scaled by the requested drawing scale (1:100 → 3000 mm drawing-unit diameter). Layer A-ANNO-NORT, colour inherited from the layer. |
| `insert_scale_bar` | Insert a horizontal graphic scale bar at the given position. The bar is a chequered 5-segment rectangle (alternating black/white segments 1-by-4 mm plotted high) with numeric labels under each segment and a "1:100" scale caption centred above. Segment meters auto-scale to the plan scale (1:50 → 1 m segments, 1:200 → 2 m segments). |
| `insert_section_callout` | Insert a section cut-line plus two end markers (circle + label letter) plus two view-direction arrows. Optional drawCutLine=true draws the dashed cut polyline between startPoint and endPoint; set it to false if the plan already has a cut line. label defaults to "A" and creates markers reading "A" on both ends (A-A section). viewDirection controls which side the triangle arrows point (right\|left relative to the start→end vector). |
| `insert_title_block` | Draw an ISO 7200 sheet border plus a 12-row project title block in the lower-right corner. sheetSize accepts A0/A1/A2/A3/A4; the block is scaled so that the plotted paper size matches. Pass fields=[{key, value}, ...] to fill the standard rows (PROJEKT, INWESTOR, ADRES, BRANŻA, FAZA, STADIUM, RYSUNEK, SKALA, NR RYS., DATA, PROJEKTANT, SPRAWDZAJĄCY); missing rows are left empty. Shorthand projectName/sheetNumber/author/date/titleText populate the most common rows if fields is not supplied. |

## civil

High-level civil-engineering drafting: road alignments (tangent + circular curve segments on C-ROAD-CNTR with CENTER linetype), road corridor edges (C-ROAD-EDGE Continuous), stationing tick marks + labels in metric (0+020) or US (0+20) format perpendicular to the alignment, parcel polylines built from surveyor (bearing, distance) legs with closure tolerance check, major / minor topographic contours with elevation labels, spot elevations as cross + signed +XX.XX text, and a true-north arrow that respects drawing rotation. Composes primitives from acad-geometry-2d, acad-layers, acad-annotations and ships a 12-layer Polish PN + US NCS hybrid civil layer key. Pairs with acad-validators rules under validators/civil/.

**10 tools:**

| Tool | Description |
|---|---|
| `civil_health` | Report the 12-layer civil engineering key, the parcel-closure tolerance presets (residential / commercial / agricultural / forest), the supported stationing systems ('metric_km' / 'us_feet'), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which closure tolerance applies to a residential lot — without making a real call to AutoCAD. |
| `draw_alignment_curve` | Draw a single circular curve segment of a road horizontal alignment as an Arc on layer C-ROAD-CNTR (default). Spirals / clothoid transitions are NOT in v1 — only tangents and circular curves. The arc spans from startAngleDeg to endAngleDeg around the centre with the given radius (in metres, in the drawing's current units). |
| `draw_alignment_tangent` | Draw a single straight (tangent) segment of a road horizontal alignment as a line on layer C-ROAD-CNTR (default) — picks up CENTER linetype because the layer carries it. Per rule 38 §6 the road centreline MUST be a CENTER linetype on C-ROAD-CNTR; agents who reach for acad-geometry2d.draw_line directly bypass the linetype assignment. |
| `draw_contour_line` | Draw a topographic contour line as a polyline on layer C-TOPO-MAJR (when isMajor=true, default) or C-TOPO-MINR (when isMajor=false). When isMajor=true, also drops a labelled DBText with the elevation (formatted to 2 decimals) at the labelEvery-th vertex. Per rule 38 §4 minor contours are unlabelled; major contours MUST be labelled — agents who set isMajor=true on a 1 m contour break the visual hierarchy. |
| `draw_north_arrow` | Draw a basic north arrow at `position`: an isoceles triangle pointing toward TRUE north (rotated by trueNorthDegFromPageNorth from the page +Y axis per rule 38 §8) with optional 'N' letter above the tip. The triangle apex is sizeM tall, the base is 0.4 × sizeM wide, drawn on layer C-NORTH (Continuous, default). Per rule 38 §8 a north arrow with the default 0° rotation when the drawing is rotated ruins all bearings on the plan — agents MUST pass the drawing rotation explicitly. The COMPASS variant ships with the Phase-7 block library. |
| `draw_parcel` | Build a parcel polyline by walking from `start` along a list of (bearing, distance) legs and draw it on layer C-PROP (PHANTOM2 linetype, default). Bearings MUST be surveyor textual form: 'N 45 30 15 E' / 'N 45° 30\' 15" E' / 'S 30 W'. Computes the closure error (distance from the last vertex back to the start) and reports it in metres along with `closureStatus = 'in_tolerance' \| 'out_of_tolerance'`. Tolerance is set by `kind` ('residential' < 0.02 m, 'commercial' < 0.05 m, 'agricultural' < 0.20 m, 'forest' < 0.50 m per rule 38 §3) or via `toleranceMOverride`. Setting autoClose=true closes the polyline geometrically (last vertex snapped to first) but the original closure error is still reported. |
| `draw_road_corridor` | Given a road centreline polyline + a total widthM, draws the centreline on C-ROAD-CNTR (CENTER linetype) PLUS two parallel edge polylines on C-ROAD-EDGE (Continuous), each offset by widthM/2 to either side at every vertex (mitred at internal vertices using the average of the incoming and outgoing tangent normals). Per rule 38 §6 the edges are Continuous, NOT CENTER — the layer assignment is what makes the plan readable. Returns all 3 entity handles + the widthM used. |
| `ensure_civil_layers` | Idempotently create the 12-layer civil-engineering key (C-ROAD-CNTR, C-ROAD-EDGE, C-ROAD-LANE, C-PROP, C-ESMT, C-ROW, C-TOPO-MAJR, C-TOPO-MINR, C-TOPO-SPOT, C-STAT, C-ANNO, C-NORTH) per rule 38 §9, with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. C-ROAD-CNTR = 0.30 mm CENTER, C-ROAD-EDGE = 0.50 mm Continuous, C-PROP = 0.50 mm PHANTOM2, C-TOPO-MAJR = 0.35 mm, C-TOPO-MINR = 0.13 mm). Existing layers are left alone, never overwritten. includeRoad / includeProperty / includeTopo flags skip the corresponding sub-set so a survey-only drawing does not get road layers it never uses. |
| `place_spot_elevation` | Place a survey spot elevation at `position`: a small + cross (two perpendicular short lines on C-TOPO-SPOT) AND a signed elevation text formatted '+102.45' / '-1.23' (Polish PN-EN ISO 6709 conventional 2-decimal precision) offset by textOffsetM to the upper-right. Returns BOTH the cross handles and the text handle. Per rule 38 §5 drawing only the text breaks downstream takeoffs because the actual point is missing. |
| `place_station_labels` | Walk the centreline polyline and at every interval (default 20 m) drop: (1) a small perpendicular tick mark on layer C-STAT and (2) a labelled DBText with the station notation parallel to the alignment, offset to one side. Notation respects the system flag: 'metric_km' → '0+020' (Polish / EU, default), 'us_feet' → '0+20' (US, where 1 station = 100 ft). Per rule 38 §7 ticks are perpendicular to the LOCAL tangent, recomputed at every vertex, NOT to the global +X axis. |

## dimensions

AutoCAD parametric dimension entities: linear (rotated and aligned), angular (3-point and 2-line), radial, diametric, arc-length, ordinate, plus baseline and continued chains derived from a prior dimension. Includes dimension style (DimStyle) lookup and assignment. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

**17 tools:**

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

## electrical

High-level electrical schematic + ladder-logic drafting: ladder rails (L1 / N labelled at top), numbered horizontal rungs, NO and NC contacts as TWO different tools (the slash matters per rule 39 §2), IEC-style coils with optional contact-rung cross-reference text, IEC vs ANSI resistor symbols (rectangle vs zig-zag), wire segments + explicit wire-junction dots (rule 39 §3), terminal blocks as numbered rectangles in a row, and IEC 81346 device tags with prefix validation (-K / -Q / -F / -S / -B / -M / -T / -G / -X / -W / -H). Composes primitives from acad-geometry-2d, acad-layers, acad-annotations and ships an IEC + JIC hybrid 12-layer electrical key. Pairs with acad-validators rules under validators/electrical/.

**12 tools:**

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
| `place_resistor` | Place a resistor symbol at `position`, rotated by rotationDeg (0° = horizontal, terminals at left/right). style='iec' (default, Polish/EU) draws a rectangle of width 4×unitSize × height 1.5×unitSize; style='ansi' draws a zig-zag of 6 zags spanning the same width. Both styles expose two terminals named '1' (left / start) and '2' (right / end) with their EXACT coordinates so subsequent draw_wire calls snap to them (rule 39 §7). Default unitSize = 5 mm (rule 39 §10). |
| `place_terminal_block` | Place a terminal block as `count` numbered rectangles in a horizontal row starting at `origin` (top-left corner), each rectangle of width pitchMm × height heightMm, with sequential numbers (startNumber, startNumber+1, …) labelled below. Per rule 39 §11 terminals live on layer E-TERM (ACI 6, 0.40 mm) and labels on E-LBL-WIRE. Returns each slot's body handle, label handle, AND its top + bottom centre points so wires can snap to either side of the block. |

## files

AutoCAD drawing file lifecycle and conversion: open / save / save-as (with chosen DwgVersion) / close the active drawing, import DWG and DXF, export to DXF and downgraded DWG versions, plot the active layout to PDF or DWF via PlotEngine, render images, and run drawing maintenance (purge unused symbols, audit for corruption with optional fix). All operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session.

**11 tools:**

| Tool | Description |
|---|---|
| `audit_database` | Run AUDIT on the active document database. Reports the number of errors found and (if fix=true) fixed. |
| `close_document` | Close a document by its file path (or the active document if path is null). Set save=true to save before closing. |
| `export_file` | Export the active document to the given path. Format is one of "DWG", "DXF", "PDF", "DWF", "DWFX", "IMAGE" (PNG). Optional layout name (default: current). Scope is "Display" / "Extents" / "Limits" / "Window" (PDF/DWF only). |
| `get_active_document` | Return descriptor of the currently active document (path, name, modified flag, DWG version, entity count). |
| `import_file` | Import a .dwg / .dxf file into the currently active document at the optional insertion point (default: 0,0,0). DWG files are merged into model space; DXF respects its own units. |
| `list_documents` | List every open AutoCAD document with its file path, modified flag, read-only flag and entity count, plus the active document name. |
| `new_document` | Create a brand new empty document based on the default template (acad.dwt) and make it active. |
| `open_document` | Open an existing .dwg / .dxf file in the AutoCAD UI as a new document and make it active. Optional readOnly flag and password for encrypted DWG. |
| `purge_database` | Run a full database purge: removes every unused symbol-table record (blocks, layers, linetypes, text/dimstyle, mlinestyle, registered apps). Returns the count of records purged. |
| `save_document` | Save the currently active document to its existing path (no-op if it has no path yet — call save_document_as instead). |
| `save_document_as` | Save the currently active document to a new path. Optional dwgVersion is one of "AC1027" (2013), "AC1032" (2018), "AC1024" (2010), etc. Defaults to current AutoCAD's native format. |

## furniture

Insert and manage parametric furniture blocks for hospitals, offices and residential interiors. Covers beds, chairs, desks, cabinets, sofas, tables, and medical equipment with inventory attributes (inv_id, type, note) and room-preset populators.

**10 tools:**

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

## geometry-2d

AutoCAD 2D geometry primitives: create lines, polylines, circles, arcs, ellipses, rectangles, polygons, splines, points, donuts, xlines, rays, single- and multi-line text, hatches and revision clouds; query entities (entity descriptor, bounding box, length, area, intersections, distances) inside a rectangular window; modify with offset, trim, extend, join, explode, fillet, chamfer and delete. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session (NETLOAD or auto-bundle). Read-only tools are flagged ReadOnly = true.

**32 tools:**

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

## geometry-3d

AutoCAD 3D solids and surfaces: create primitive solids (box, sphere, cylinder, cone, wedge, torus, pyramid), build solids by extruding or revolving closed planar curves, build planar surfaces (Region) from boundary curves, and query 3D mass properties (volume, surface area via Brep, centroid, principal moments and radii of gyration) plus axis-aligned 3D bounding boxes. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session (NETLOAD or auto-bundle); read-only query tools are flagged ReadOnly = true. Not supported on AutoCAD LT.

**15 tools:**

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

## grids

Axis grids with bubble labels (alpha A..Z + numeric 1..N), axis spacings (e.g. 7200 mm ISO default), per-axis spacing, grid snapping and rename/remove operations. Every professional plan requires a structural grid per PN-B-01025.

**6 tools:**

| Tool | Description |
|---|---|
| `add_grid_axis` | Add one labeled grid-axis line. Optionally attaches bubbles (circle + text) at the start and/or end. Axis extends by extendMm past the provided endpoints before the bubble so the bubble sits outside the grid box. Rule 67 §3. |
| `add_grid_bubble` | Add a single grid bubble: a circle of the given radius plus a centred label. Used to retro-fit bubbles onto existing axis lines or for section / detail callouts. |
| `delete_grid` | Erase grid axes + bubbles. If handles is provided, only those handles are erased; otherwise every entity on axisLayer + bubbleLayer is erased. Use list_grid_axes first to preview. |
| `draw_grid` | Draw an orthogonal column grid from two lists of spacings (mm). X-axes get letter labels (A, B, C, …), Y-axes get numeric labels (1, 2, 3, …). Bubbles (circle + label) are drawn on the configured sides (default: north + west). Lines and bubbles live on A-GRID / A-GRID-BUB. Rule 67 §1 (grid policy). |
| `list_grid_axes` | Enumerate handles of all entities living on the grid axis and bubble layers. Read-only; used by validators + callouts to find grid bubbles for intersection queries. |
| `snap_to_grid` | Snap a point to the nearest grid intersection given an origin + two spacing lists. Returns snapped XY, axis labels (A, B, 1, 2…) and distance from the input point. PURE maths, no plugin call — use before drawing to align entities to structural axes. Rule 67 §5. |

## hatches

Draw, manage and regenerate hatches (material fills) on architectural and engineering drawings per ISO 128 and PN-EN patterns. Covers boundary-based hatching, pattern presets (concrete/brick/insulation/plaster/stone), material-to-layer mapping, and regeneration after boundary edits.

**8 tools:**

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

## layers

AutoCAD layer (LayerTableRecord) management: create, rename, delete and list layers; query and set per-layer state (color by ACI or RGB, linetype, lineweight, plot style, plottable, frozen, locked, on/off, transparency); pick the current layer; bulk move entities between layers; and basic layer-state save / restore via AutoCAD's layer state manager. All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

**14 tools:**

| Tool | Description |
|---|---|
| `create_layer` | Create a new layer. Accepts color (RGB or ACI), linetype name, lineweight in mm, plottable flag and description. |
| `delete_layer` | Delete a layer (only if no entities reference it). Layer 0 and Defpoints cannot be deleted. |
| `get_layer` | Get full descriptor of one layer by name. |
| `list_layer_states` | List every saved named layer state in the active drawing. |
| `list_layers` | List every layer in the active drawing with color, linetype, lineweight, plottable/frozen/locked/off flags, plus the current layer name. |
| `purge_unused_layers` | Purge every layer that has no entity references and is not protected (0 / Defpoints / current). Returns number of layers removed. |
| `rename_layer` | Rename a layer. Layer 0 cannot be renamed; new name must be a valid AutoCAD symbol name. |
| `restore_layer_state` | Restore a previously saved named layer state. |
| `save_layer_state` | Save the current visibility/lock/color/linetype state of every layer under a named layer state (LAS). |
| `set_current_layer` | Set the active ("current") layer; subsequent draw operations default to this layer. |
| `set_layer_color` | Set a layer's color (true RGB or ACI 1..255). |
| `set_layer_linetype` | Set a layer's linetype (must already be loaded; returns LayerNotFound if linetype is missing). |
| `set_layer_lineweight` | Set a layer's lineweight in millimeters; snaps to nearest standard AutoCAD value (e.g. 0.13, 0.18, 0.25, 0.5, 0.7, 1.0 mm). |
| `set_layer_state` | Toggle one or more layer state flags: frozen, locked, off, plottable. null = leave unchanged. Cannot freeze the current layer. |

## layouts

AutoCAD paper-space layout management: create, list, rename, delete and switch the current layout; create and configure floating Viewport entities (size, center, scale, layer, on/off, locked, frozen layers) on a layout; configure plot settings (page size, plotter, orientation, plot area). All write operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock.

**10 tools:**

| Tool | Description |
|---|---|
| `configure_plot` | Configure a layout's plot settings: plotter / device name, paper size (accepts canonical 'ISO_full_bleed_A0_(1189.00_x_841.00_MM)', locale 'ISO A0 (841.00 x 1189.00 MM)', or fuzzy alias 'A0' / 'ISO A0' / 'a0' — all three resolve to the plotter's canonical name), named plot style table, and 0/90/180/270 plot rotation. Pass null on any field to leave it untouched. Call layouts.list_paper_sizes first if you're unsure which media strings the installed plotter accepts. |
| `create_layout` | Create a new paper-space layout (tab). Optionally make it the current/active layout right after creation. |
| `create_viewport` | Create a paper-space Viewport entity on the named layout: a rectangular window of width × height centred at 'center' (paper-space coords). Optionally set a custom standard scale (e.g. 0.02 for 1:50, 0.01 for 1:100). Returns the Viewport entity handle. |
| `delete_layout` | Delete a paper-space layout. Cannot delete the Model tab; cannot delete the last remaining paper-space tab. |
| `get_layout` | Return descriptor of a single paper-space layout by name. |
| `list_layouts` | List every paper-space layout (tab) in the active drawing with its tab order, current flag, and configured plotter / paper size. |
| `list_paper_sizes` | Enumerate every paper size supported by a plotter (plotter=null -> the current layout's plotter or the first registered device). Returns the canonical media names (what configure_plot needs) plus the locale-facing display name. Call this before configure_plot if you're unsure which media strings the installed plotter accepts — especially for non-standard devices (e.g. 'DWG To PDF.pc3', 'Microsoft Print to PDF', pen plotters, PublishToWeb PNG). configure_plot accepts canonical / locale / fuzzy names (e.g. 'A0', 'ISO A0'); use this tool when even fuzzy resolution fails. |
| `rename_layout` | Rename a paper-space layout. The Model tab cannot be renamed; the new name must be unique and valid as an AutoCAD symbol name. |
| `set_current_layout` | Switch the active layout to the named tab (use "Model" to return to model space). All subsequent draw / viewport tools target this layout. |
| `set_viewport_scale` | Set the model-space-to-paper scale factor of an existing Viewport entity (e.g. 0.02 → 1:50, 0.01 → 1:100, 0.001 → 1:1000). |

## mechanical

High-level mechanical drafting operations: visible / hidden / centre line classes (each pinned to its own layer + linetype), section cutting plane lines with arrow heads + labels, plan-view holes (through, counterbore, threaded with the canonical 3/4 minor-Ø arc), bolt-head top views as flat-to-flat hexagons, and revision triangles + tags. Composes primitives from acad-geometry-2d, acad-layers, acad-annotations and ships an ISO-mechanical 11-layer key + ME-25 dim style. Pairs with acad-validators rules under validators/mechanical/.

**12 tools:**

| Tool | Description |
|---|---|
| `draw_bolt_head_top_view` | Draw the top view of a hex-head bolt per rule 37 §5: a regular hexagon with two flats parallel to the X axis (rotated by rotationDeg) sized by flatToFlatMm, optionally a Continuous shank circle inside (rotation matters for the hexagon, not the circle), and a centreline crosshair on ME-CENTER sized to the across-corners radius. Pass nominalDiameterMm for documentation only — it's echoed back in the result for traceability but does not affect geometry. The across-corners diameter is reported. |
| `draw_centerline` | Draw an axis / centreline as a CENTER line on layer ME-CENTER (default). For round features prefer draw_centerline_cross which sizes the extension automatically per ISO 128. |
| `draw_centerline_cross` | Draw the canonical round-feature centreline crosshair: TWO perpendicular CENTER-linetype lines on layer ME-CENTER (default), each extending featureRadiusMm + extensionMm beyond the centre point in both directions, rotated by rotationDeg. Per rule 37 §2 this is what a circle's centreline SHOULD look like — agents who try to do it with two raw draw_centerline calls usually forget the extension and the drawing looks like a `+` glued to the circle. |
| `draw_counterbore_hole` | Draw a plan-view counterbore hole: outer counterbore circle on layer ME-VISIBLE plus an inner through-hole circle on the same layer plus a centreline crosshair on ME-CENTER sized to the counterbore radius. counterboreDiameterMm MUST be greater than throughDiameterMm — the tool fails fast otherwise. |
| `draw_hidden_edge` | Draw an occluded edge as a HIDDEN line on layer ME-HIDDEN (default). Per rule 37 §1 hidden geometry MUST live on its own layer — drafting it on ME-VISIBLE is the #1 'looks fine, fails inspection' bug. |
| `draw_revision_triangle` | Draw the canonical revision marker per rule 37 §6: a filled equilateral triangle (closed polyline + SOLID hatch) on layer ME-REV with the revision letter or number drawn as DBText centred on the triangle. Returns BOTH the triangle handle and the text handle so the agent can later move them together. The triangle pointer sits at `position`; rotationDeg orients its tip (default 0° = pointing UP). |
| `draw_section_cut_line` | Draw a section cutting plane line per ISO 128 type H: thick PHANTOM polyline on layer ME-SECTION (lineweight 0.70 mm by default via the ensured layer), arrow heads on each end pointing in the viewing direction (perpendicular to the cut, pointing OUTWARD from the start→end direction by rotating +90°), and a label DBText on layer ME-TEXT placed at each end. Returns all 5 entity handles. Per rule 37 §3 the sectioned hatch is NOT drawn here — call acad-geometry2d.draw_hatch on the resulting sectioned-view boundary separately. |
| `draw_threaded_hole` | Draw a plan-view threaded (tapped) hole per rule 37 §4 + §4a: a FULL outer circle at majorDiameterMm on layer ME-VISIBLE, an INNER 3/4 ARC at minorDiameterMm on layer ME-THREAD (HIDDEN linetype) — the gap demonstrates that the inner circle is the thread minor diameter, not a true geometric circle — plus a centreline crosshair on ME-CENTER. The arc gap is threadGapDeg wide (default 90°, so the arc spans 270°) starting at threadGapStartDeg (default 0° = +X axis). minorDiameterMm MUST be smaller than majorDiameterMm. |
| `draw_through_hole` | Draw a plan-view through hole: profile circle on layer ME-VISIBLE (default) at the requested diameter PLUS a centreline crosshair on ME-CENTER (default) extending centerlineExtensionMm past the circle on each axis (rule 37 §4). Returns the profile circle and both centreline handles in one call. |
| `draw_visible_edge` | Draw a visible feature edge as a Continuous line on layer ME-VISIBLE (default). Use this rather than acad-geometry2d.draw_line whenever the line has semantic meaning — the layer assignment is what makes the drawing readable per ISO 128. |
| `ensure_mechanical_layers` | Idempotently create the ISO-mechanical 11-layer key (ME-VISIBLE, ME-HIDDEN, ME-CENTER, ME-DIMS, ME-TEXT, ME-SECTION, ME-HATCH, ME-THREAD, ME-CONSTRUCTION, ME-TITLE, ME-REV) per rule 37 §9, with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. ME-VISIBLE = 0.50 mm Continuous, ME-HIDDEN = 0.25 mm HIDDEN, ME-CENTER = 0.18 mm CENTER, ME-SECTION = 0.70 mm PHANTOM). Existing layers are left alone, never overwritten. ME-CONSTRUCTION is non-plottable. includeConstruction=false skips it; includeRevision=false skips ME-REV. |
| `mechanical_health` | Report the ISO-mechanical layer key, the material → hatch pattern lookup table (rule 37 §8), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which pattern to pass to acad-geometry2d.draw_hatch when sectioning steel — without making a real call to AutoCAD. |

## modify

AutoCAD entity edit and transform operations: rigid transforms (move, rotate, scale, mirror, align two-point), copy and array (rectangular and polar), erase, undo / redo, common-property updates (layer, color, linetype, lineweight) including bulk match-properties from a source entity, and AutoCAD Group management (create / add / remove / rename / dissolve / list members). All operations require the AcadMcp .NET plugin loaded inside an open AutoCAD session and run inside a single transaction with a document lock so the AutoCAD UI stays consistent.

**18 tools:**

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
| `redo` | Redo the most recently undone action via SENDCOMMAND "_REDO". |
| `rotate` | Rotate entities around a center by angle (degrees, CCW). Optional axis vector for 3D rotations (default Z). |
| `scale` | Uniformly scale entities about a center point by a positive factor. |
| `set_color` | Set the entity color to a true RGB color or an ACI index (1..255). |
| `set_layer` | Move entities to the given layer (creates the layer if missing). |
| `set_linetype` | Set the linetype (by name) and optional linetype scale on entities. The linetype must already be loaded. |
| `set_lineweight` | Set entity lineweight in millimeters. Common values: 0.13, 0.18, 0.25, 0.5, 0.7, 1.0 mm. |
| `undo` | Undo the last user/AI action by sending a SENDCOMMAND "_U". Counts the number of undo steps performed. |
| `ungroup` | Delete a named Group (the underlying entities remain in the drawing). |

## openings

Professional-grade door and window tools that cut the wall, draw frame/leaf/swing-arc, attach attributes (number, width, REI/EI fire class, RC burglary class), and emit schedules. Replaces raw line+arc door hacks with a single atomic call.

**10 tools:**

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

## parametric

Parametric drafting: geometric constraints via native -GEOMCONSTRAINT (Horizontal, Vertical, Parallel, Perpendicular, Coincident, Fix), DELCONSTRAINT cleanup, inventory of constraint proxy entities in model space, and dynamic BlockReference property get/set (visibility, lookup, distance, angle) without opening the Block Editor. Ships a dedicated P-* layer key for parametric annotation hygiene. Pairs with acad-validators under validators/parametric/. Full dimensional DIMCONSTRAINT workflows and Block Editor-only geometric constraints ship in Phase 7.

**12 tools:**

| Tool | Description |
|---|---|
| `apply_geom_coincident` | Apply a Coincident geometric constraint between two picks (handles a and b) via transparent -GEOCONSTRAINT. Works best on endpoints / points the solver can merge; whole-entity picks may fail depending on AutoCAD build. If the command rejects the pick set, constrain boundary polylines instead of hatch (rule 42 §8). |
| `apply_geom_fix` | Apply a Fix geometric constraint to anchor one entity (datum behaviour per rule 42 §2). Call once per sketch for the construction corner — do not Fix every entity or the drawing becomes over-constrained. |
| `apply_geom_horizontal` | Apply a Horizontal geometric constraint to one line-like entity in the current space using AutoCAD transparent -GEOCONSTRAINT. The entity handle must reference a Line, polyline segment, or other object the solver accepts for Horizontal. Runs outside an MCP transaction — AutoCAD owns the command transaction. |
| `apply_geom_parallel` | Apply a Parallel geometric constraint between two curve entities (handles a and b) via transparent -GEOCONSTRAINT. Both entities must live in the same current space; mixed paper-space / block-context picks are undefined — resolve handles from the active viewport context first. |
| `apply_geom_perpendicular` | Apply a Perpendicular geometric constraint between two curves via transparent -GEOCONSTRAINT. Common pitfall: picking two lines that are already parallel to the UCS axes — the solver may report redundant constraints (rule 42 §3). |
| `apply_geom_vertical` | Apply a Vertical geometric constraint to one line-like entity in the current space using transparent -GEOCONSTRAINT. Complements apply_geom_horizontal; do not stack both on the same line unless the office standard requires it. |
| `delete_entity_constraints` | Run transparent -DELCONSTRAINT on one entity handle to strip geometric/dimensional constraints attached to that object. Use before explode-freeze workflows or when rebuilding a sketch (rule 42 §4 — explode orphans constraints differently). |
| `ensure_parametric_layers` | Idempotently create the 6-layer parametric sketch key (P-CONSTRUCTION, P-SKETCH, P-CONSTRAINED, P-DYNAMIC, P-PARAM-LBL, P-NOTE) per rule 42 §9 with prescribed ACI colour, Continuous linetype, and lineweight. Existing layers are never overwritten. |
| `get_dynamic_block_properties` | Read all DynamicBlockReferenceProperty entries from a BlockReference handle: names, read-only flags, UnitsType, CLR type, and current Value. isDynamicBlock=false returns an empty list — the handle is still a block insert but not dynamic. Use the reference handle, never hard-code anonymous *U block names (rule 42 §6). |
| `list_constraint_entities` | Scan model space for database objects whose runtime class name contains 'Constraint' (constraint proxy / glyph entities). Optional layerFilter narrows results. Read-only with respect to geometry — still requires the plugin for DB access. |
| `parametric_health` | Return the 6-layer P-* parametric key, planned Phase-7 block roster, and the dynamic-block angle value policy string. Does not open AutoCAD. |
| `set_dynamic_block_property` | Write one DynamicBlockReferenceProperty on a BlockReference by name. Pass JSON booleans as true/false, numbers as JSON numbers. For Angle-typed properties the numeric value is interpreted as degrees and converted to radians in the plugin (see parametric_health.dynamicAnglePolicy). Strings are for lookup / text parameters. Read-only properties throw. |

## plotstyles

Manage CTB (color-dependent) and STB (named) plot-style tables. Apply 9-tier lineweight policy (0.05 mm hatches -> 1.4 mm outer building outline), per-layer plot color, screen percentage and plot flag. Ships AIA-2017 and PN-B-01025 presets.

**3 tools:**

| Tool | Description |
|---|---|
| `apply_plotstyle_to_layout` | Apply a named plot-style (CTB/STB) to a paperspace layout. Optionally runs ensure_ctb first so the sheet is copied into AutoCAD before being assigned (ensure=true, default). Under the hood dispatches acad.layouts.configure_plot { layoutName, plotStyle }. |
| `ensure_ctb` | Ensure a colour-dependent plot-style (CTB) is installed in AutoCAD's Plot Styles directory. Queries acad.layouts.list_plot_styles to resolve the target directory, then copies from sourcePath (caller override) or the repo asset folder <repo>/assets/plotstyles/<name>. If the CTB already exists and overwrite=false (default), reports existedBefore=true, copied=false. Calls list_plot_styles a second time to verify the refresh picked up the new sheet. Use this before apply_plotstyle_to_layout so the target sheet is guaranteed loaded. |
| `list_plotstyles` | Enumerate all plot-styles currently visible to AutoCAD (CTB + STB). filter='ctb' or 'stb' narrows the returned names. Also returns repo presets (HOSPITAL-ISO, ISO-Standard, monochrome), the AutoCAD Plot Styles directory, and the backend asset directory so the caller can prep ensure_ctb calls. |

## plumbing

Insert sanitary fixtures (WC, sinks, bathtubs, showers, bidets, urinals, medical sinks) compliant with PN-EN 997, PN-EN 31, PN-EN 232, PN-EN 251 and PN-EN 17210 (accessibility). Includes bathroom populators for standard, disabled, ensuite and scrub-room presets.

**9 tools:**

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

## router

AutoCAD MCP router - the single permanent entry point to ~30 specialist AutoCAD MCP categories (geometry-2d/3d, modify, layers, blocks, annotations, dimensions, layouts, files, parametric, architecture, mechanical, civil, electrical, vision, validators, workflows, livestream). Exposes 9 meta-tools: status, find_tools, load_category, recommend_categories, explain_capabilities, describe_drawing, undo_checkpoint, restore_checkpoint, design_iterate. The actual category MCPs are loaded on demand via MCP Nexus's mcpd_find / mcpd_connect flow. Backend talks to the AutoCAD .NET plugin (NETLOAD'ed) over a single named pipe.

**10 tools:**

| Tool | Description |
|---|---|
| `acad_call` | UNIVERSAL dispatch: invoke any backend composite (e.g. 'schedules/generate_door_schedule') OR any plugin primitive (e.g. tool='acad.annotations.add_table', category left empty). Routes in-process, no subprocess spawn. |
| `acad_describe_drawing` | Vision shortcut (Phase 4): screenshot active viewport + OCR + LLM-describe in one call. |
| `acad_design_iterate` | Auto-design loop (Phase 7.0): create a checkpoint, execute a planned sequence of tool calls, validate against a named standard, auto-fix fixable violations or roll back on failure. Closes the 12-of-10 agent loop. |
| `acad_explain_capabilities` | Returns a compact catalog of all known acad-* categories with one-line summaries. |
| `acad_find_tools` | Semantic search across all acad-* MCP servers via MCP Nexus find_tools, filtered to our namespace. Returns ranked candidates with category and tool name. |
| `acad_load_category` | Shortcut: connect to a single acad-<name> MCP server in lazy mode. Returns its tool list summary so you can pick the next call. |
| `acad_recommend_categories` | Suggest the 1-3 most relevant categories for a free-text task description. Saves tokens by avoiding indiscriminate loading. |
| `acad_restore_checkpoint` | Roll back to a previously created checkpoint (Phase 7). Used by the auto-design loop on validation failure. |
| `acad_status` | Lightweight health-check: AutoCAD alive, version, vertical (vanilla/civil3d/mechanical/architecture/MEP/plant3d), active document, layer, entity count, mode banner (full vs com-only). |
| `acad_undo_checkpoint` | Create a named undo checkpoint so subsequent operations can be rolled back atomically (Phase 7). |

## schedules

Parametric AutoCAD Table entities in paperspace: door schedule, window schedule, room schedule (number/name/area/floor/wall/ceiling finish), finish legend. Pulls data from acad-openings attributes and room labels, supports update-in-place.

**9 tools:**

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

## sections

Section cut lines (A-A, B-B) with cut-plane markers, directional arrows and depth ranges. Links to layout tabs for each section view. Conforms to PN-EN-ISO-128 and PN-B-01025 section symbol conventions.

**4 tools:**

| Tool | Description |
|---|---|
| `insert_elevation_marker` | Insert an elevation-direction marker at the given model-space position. Geometry: a filled triangle pointing in the requested compass direction (N / E / S / W / NE / NW / SE / SW or bare degrees) on top of a short horizontal baseline, with the caption "ELEWACJA <direction>" to the right. Differs from acad-callouts.insert_section_callout in that elevations have a directional triangle rather than a circle-in-triangle pair. |
| `insert_section_line` | Draw a section cut-line from startPoint to endPoint on layer A-DETL-SECT, apply DASHED2 linetype, add 90-degree offset ticks at both ends, then place labelled end markers with view-direction arrows via acad-callouts (unless drawEndMarkers=false). The offset ticks (6 mm plotted) signal that the cut-line's path is symbolic, not literal. sheetReference optional, viewDirection in {left\|right} relative to the start→end vector. |
| `insert_section_title` | Place a section/view title beneath a drawn section (e.g. "PRZEKRÓJ A-A" with "SKALA 1:50" under it and an underline). position is the insertion point — typically the centre-bottom of the view. Customise caption (defaults to "PRZEKRÓJ") for elevations / axonometric views. drawUnderline=true adds an 80 mm plotted horizontal rule between the caption and the scale line. |
| `list_section_lines` | Inventory all entities on the A-DETL-SECT layer (or a caller-supplied layer via layerFilter). For every handle that is a curve, also queries acad.geometry2d.get_curve_length so the caller can sanity-check drawing-unit cut lengths against the target scale. |

## selection

Non-interactive entity selection over the active drawing's ModelSpace: pick by criteria (layer, color, type/DXF name, handle list), by geometry (rectangular window, fence polyline, polygon, all-in-modelspace), apply free-form filter predicates, build named selection sets stored as Xrecords in the NamedObjectsDictionary, and load/list/delete those sets later. Returns lists of entity handles that downstream tools (acad-modify, acad-geometry-2d, acad-geometry-3d, etc.) can consume directly. Never opens an interactive 'Select objects:' prompt - rule 14-acad-no-blocking-prompts. All read paths are flagged ReadOnly = true; only saving a named set writes to the database.

**12 tools:**

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

## validators

Rule-based validator for AutoCAD drawings. Loads YAML rules (id, severity, discipline, scope, checks, optional auto-fix) from a bundled library plus user contributions, runs them against the active document via the AcadMcp .NET plugin, and returns a structured ValidationReport: per-rule counts and per-violation entityHandle/dxfType/layer/expected/observed/fixAvailable. Supports primitive checks (layer_equals, layer_matches, color_equals, linetype_equals, lineweight_at_least, length/area/radius_at_least, text_matches, attribute_present, bbox_inside, ...) and composite ones (not / any_of / all_of), plus document-level checks (entity_count_at_least, layer_must_exist, units_must_be). Auto-fix recipes (move_to_layer, set_color, set_linetype, set_lineweight, delete_entity, set_attribute) run in a single grouped transaction so failures roll back atomically. Includes bundled "standards" (iso-cad-baseline, polish-arch-baseline) and an add_validator_rule tool for live extension. Architecture: rules 33-validators-rule-format and 34-validators-engine-traps.

**11 tools:**

| Tool | Description |
|---|---|
| `add_validator_rule` | Persist a brand-new validator rule to the user-rules directory (%LOCALAPPDATA%/AcadMcp/validators/_user/<discipline>/<id>.yaml) and reload the registry. The 'yaml' argument is the full YAML document text. Returns the assigned id and on-disk path. |
| `auto_fix_violations` | Apply the fix recipe for every fixable violation in the cached report (or only those for the supplied ruleIds). All fixes run inside a SINGLE plugin transaction (rule 34 §3); failure rolls back the whole batch. Set dryRun=true to preview the planned actions without writing. |
| `check_overlaps` | Purely geometric overlap / intersection scanner. Finds pairs of entities whose bounding boxes (or curves, for mode=polyline_crosses_polyline) overlap or cross. Use cases: doors actually piercing walls, labels stacked on top of each other, notes touching geometry. Args: layersA (required), layersB (default layersA), mode bbox_intersect\|centroid_in_bbox\|polyline_crosses_polyline, tolerance mm, optional window rectangle. Result is sorted by severity then overlap area. |
| `explain_rule` | Return the full definition of one validator rule by id - severity, discipline, description, references, scope, list of checks and the optional fix recipe. Pure local query - does not touch AutoCAD. |
| `list_standards` | List every bundled standard preset (id, human name, ordered list of rule ids it expands to). Standards are convenience presets for validate_against_standard. Pure local query. |
| `list_validators` | List every available validator rule (id, name, severity, discipline, fix-available, description). Optional filters: discipline (general\|architectural\|mechanical\|electrical\|civil\|mep) and minSeverity (info\|warning\|error). Pure local query - does not touch AutoCAD. |
| `list_violations` | Return the most recent ValidationReport produced for the active document. The cache is keyed by document name + path so opening a different drawing returns 'no report yet' (rule 34 §9). |
| `reload_validator_rules` | Re-scan embedded resources, <repo>/validators and %LOCALAPPDATA%/AcadMcp/validators for rule YAML files and rebuild the in-process registry. Returns the new total rule count and any load errors. |
| `validate_against_standard` | Resolve a standard id (e.g. 'iso-cad-baseline') to its rule set and run validate_drawing for that bundle. Use list_standards first to discover available presets. |
| `validate_drawing` | Run a set of validator rules against the active document and return a structured report (per-rule counts, every violation with handle/dxfType/layer/expected/observed/fixAvailable). Optional filters: ruleIds (explicit list, overrides everything else), discipline, minSeverity, includePaperspace. |
| `validate_with_rule` | Run exactly one validator rule (by id) against the active document and return a focused report. Throws if the ruleId is unknown. |

## verticals

Stairs (straight, spiral, U/L), ramps, elevators (passenger/bed/goods), escalators, platform lifts and handrails per PN-EN 13374 and WT §54. Bed-lift minimum 160x260 with 1600 kg capacity enforced for hospitals.

**8 tools:**

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

## view

Model-space view control for the active AutoCAD document: zoom to an arbitrary drawing-unit rectangle (zoom_window), zoom to drawing extents / limits / named view, zoom by scale factor or around a named center point, and list / activate / describe stored named views. Used as a pre-step for acad.files.export_file scope=Display when the AI agent needs to frame a specific region before capturing a PNG + running describe_image on the result, and as a reset tool between regional captures in multi-tile visual review loops.

**8 tools:**

| Tool | Description |
|---|---|
| `get_current_view` | Return the currently active view's center, width, height and paper-space flag. |
| `list_views` | List all named views stored in the drawing's VIEW table with center, width, height and paper-space flag. |
| `set_current_view` | Restore a named view by name. |
| `zoom_all` | Zoom the active view to the drawing limits + extents (ZOOM _A). |
| `zoom_center` | Zoom to a specific center point with a requested view height in drawing units. |
| `zoom_extents` | Zoom the active model-space view to fit the bounding box of all entities (ZOOM _E). Use as a reset between regional captures. |
| `zoom_scale` | Zoom the active view by a relative scale factor. |
| `zoom_window` | Zoom the active model-space view to the axis-aligned rectangle defined by two corner points (drawing units). Use before acad.files.export_file scope="Display" to capture a specific region as PNG, or to frame an area for visual inspection. |

## vision

Vision pipeline for AutoCAD: OCR (PaddleOCR / EasyOCR / Tesseract) on raster drawings and PDF pages, custom YOLO CAD-symbol detection per discipline (arch / mech / elec / pid), title-block field extraction with per-discipline templates, dimension-callout OCR with mm normalisation, drawing-type classification via vision LLM, free-form vision-LLM image description (Anthropic Claude or OpenAI GPT-4o), and OCR-vs-DXF cross validation. Powered by the AcadMcp.Vision Python sidecar (FastAPI, localhost-only HTTP). Tools work without AutoCAD running and degrade gracefully with installHint when an ML dep is missing.

**9 tools:**

| Tool | Description |
|---|---|
| `classify_drawing` | Use a vision LLM (Anthropic Claude or OpenAI GPT-4o, whichever has an API key) to classify a drawing's discipline (arch / mech / elec / pid / civil / unknown) and sheet type (plan / section / detail / schedule / title / isometric / unknown). Returns a JSON verdict with confidence and a one-line rationale. Cached by image content hash. |
| `cross_validate_with_dxf` | Compare a list of OCR'd strings against a list of DXF text strings (the latter typically harvested by exporting the active document via acad.files.export_file -> DXF and walking the entity stream). Returns matched / only-in-OCR / only-in-DXF buckets after case + whitespace normalisation. Optional numericTolerance (>0) treats numeric tokens within the tolerance as matched (e.g. 12.5 vs 12.6). |
| `describe_image` | Free-form vision LLM description of any image (defaults to a CAD-reviewer prompt). Provider is "auto" (prefer Anthropic if ANTHROPIC_API_KEY is set, else OpenAI), "anthropic" or "openai". Image is downscaled to <=1568 px long side and JPEG-compressed q85 before sending. Cached by content hash + provider + first 64 chars of prompt. |
| `detect_symbols` | Run a custom YOLO CAD-symbol detector on a raster image. Discipline picks the per-discipline weights file: "arch" / "mech" / "elec" / "pid". Returns labelled bounding boxes (pixel coords) with confidence. If weights are missing returns 503 with an installHint pointing to scripts/setup-vision-models.ps1. |
| `extract_dimensions` | Extract dimension callouts from a raster drawing. Filters OCR tokens that look like dimensions (e.g. 1234, 12.5 mm, 12'-6"), parses them to a numeric value in millimetres when possible, and returns each token with its pixel box and confidence. Units may be "mm", "cm", "m", "in", "ft" or "auto" (rely on the OCR'd unit suffix). |
| `extract_titleblock` | Extract title-block fields (drawing_no, title, scale, date, drawn_by, checked_by, project, rev, sheet, ...) from a raster drawing. Pass a discipline hint ("architectural-eu" default, "architectural-us", "mechanical", "electrical", "civil") so the right field-alias dictionary is used. Returns canonical field keys + raw OCR labels + values with confidence and the panel rectangle. |
| `ocr_image` | Run OCR on a raster image or a single PDF page. Returns recognised text tokens with per-token confidence and pixel bounding boxes (top-left origin). Engine is one of "paddleocr" (default, best on CAD), "easyocr", "tesseract". Image is referenced by absolute file path or base64 data URL; for PDFs supply page (1-based) and dpi. Uses on-disk cache keyed by content hash + engine + version. |
| `vision_health` | Probe the AcadMcp.Vision Python sidecar at its discovered base URL (env ACADMCP_VISION_PORT, then %LOCALAPPDATA%\AcadMcp\vision.port, then default 50062). Returns status, version, phase and uptime. Use this to confirm the sidecar is reachable before calling other vision tools. |
| `vision_version` | Return the AcadMcp.Vision sidecar version, phase, the availability flags of every optional ML dep (paddleocr, easyocr, tesseract, ultralytics, torch, sam2, anthropic, openai, pypdfium2) and whether vision-LLM API keys are present. Use this to tell the user exactly which install command they need. |
