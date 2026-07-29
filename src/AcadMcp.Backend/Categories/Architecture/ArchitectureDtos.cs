// Wire DTOs for the acad-architecture domain category. Every tool lands in this
// file so the JSON shape is reviewable in one place. Naming follows rule 22:
// args end in *Args, results end in *Result, JsonPropertyName matches plugin readers.
//
// Rules: 35-domain-categories-design.mdc, 36-architecture-domain-traps.mdc,
// 22-mcp-tool-args-results.mdc.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Architecture;

#region infrastructure

/// <summary>Result of <c>ensure_architectural_layers</c> — the per-layer outcome.</summary>
public sealed record LayerEnsureOutcome(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("status")]  string Status,   // "created" | "already_exists" | "failed"
    [property: JsonPropertyName("color")]   int? AciColor,
    [property: JsonPropertyName("linetype")] string? Linetype,
    [property: JsonPropertyName("error")]   string? Error = null);

public sealed record EnsureArchitecturalLayersArgs(
    [property: JsonPropertyName("includeStructural")] bool IncludeStructural = true);

public sealed record EnsureArchitecturalLayersResult(
    [property: JsonPropertyName("layers")]   IReadOnlyList<LayerEnsureOutcome> Layers,
    [property: JsonPropertyName("createdCount")] int CreatedCount,
    [property: JsonPropertyName("existingCount")] int ExistingCount);

#endregion

#region walls

/// <summary>One straight wall segment between two centreline endpoints.</summary>
public sealed record DrawWallArgs(
    [property: JsonPropertyName("start")]      Point2dDto Start,
    [property: JsonPropertyName("end")]        Point2dDto End,
    [property: JsonPropertyName("thicknessMm")] double ThicknessMm = 200.0,
    [property: JsonPropertyName("centerlineLayer")] string CenterlineLayer = ArchitecturePalette.LayerWallCtrl,
    [property: JsonPropertyName("faceLayer")]  string FaceLayer = ArchitecturePalette.LayerWall);

/// <summary>Result of drawing one wall: centreline + the two parallel faces.</summary>
public sealed record DrawWallResult(
    [property: JsonPropertyName("centerline")] EntityHandle Centerline,
    [property: JsonPropertyName("leftFace")]   EntityHandle LeftFace,
    [property: JsonPropertyName("rightFace")]  EntityHandle RightFace,
    [property: JsonPropertyName("lengthMm")]   double LengthMm,
    [property: JsonPropertyName("thicknessMm")] double ThicknessMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

/// <summary>A polyline-based chain of walls. Endpoints connect mitre-style at the centreline.</summary>
public sealed record DrawWallsChainArgs(
    [property: JsonPropertyName("vertices")]   IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("thicknessMm")] double ThicknessMm = 200.0,
    [property: JsonPropertyName("closed")]     bool Closed = false,
    [property: JsonPropertyName("centerlineLayer")] string CenterlineLayer = ArchitecturePalette.LayerWallCtrl,
    [property: JsonPropertyName("faceLayer")]  string FaceLayer = ArchitecturePalette.LayerWall);

public sealed record DrawWallsChainResult(
    [property: JsonPropertyName("centerline")] EntityHandle Centerline,
    [property: JsonPropertyName("leftFace")]   EntityHandle LeftFace,
    [property: JsonPropertyName("rightFace")]  EntityHandle RightFace,
    [property: JsonPropertyName("segmentCount")] int SegmentCount,
    [property: JsonPropertyName("totalLengthMm")] double TotalLengthMm,
    [property: JsonPropertyName("thicknessMm")] double ThicknessMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region doors and windows

public sealed record InsertDoorArgs(
    [property: JsonPropertyName("hinge")]      Point2dDto Hinge,
    [property: JsonPropertyName("widthMm")]    double WidthMm = 900.0,
    [property: JsonPropertyName("frameThicknessMm")] double FrameThicknessMm = 40.0,
    [property: JsonPropertyName("openingDeg")] double OpeningDeg = 90.0,
    [property: JsonPropertyName("hingeAngleDeg")] double HingeAngleDeg = 0.0,
    [property: JsonPropertyName("swingDirection")] string SwingDirection = "left",   // "left" | "right"
    [property: JsonPropertyName("doorLayer")]  string DoorLayer = ArchitecturePalette.LayerDoor,
    [property: JsonPropertyName("swingLayer")] string SwingLayer = ArchitecturePalette.LayerDoorSwing,
    // Optional: handle of the wall (Line or 2-vertex Polyline) this door sits in.
    // When supplied, the wall is cut at the door's own jambs (hinge -> hinge +
    // widthMm along hingeAngleDeg) BEFORE the door panel is drawn, via the same
    // acad.openings.cut_wall_for_opening primitive split_wall_at_opening wraps --
    // rule 36 §3 requires insert_door to punch the opening itself rather than
    // leaving it as a manual two-step flow.
    [property: JsonPropertyName("wallHandle")] string? WallHandle = null);

public sealed record InsertDoorResult(
    [property: JsonPropertyName("panel")]      EntityHandle Panel,
    [property: JsonPropertyName("swingArc")]   EntityHandle SwingArc,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("openingDeg")] double OpeningDeg,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers,
    [property: JsonPropertyName("wallOpening")] SplitWallAtOpeningResult? WallOpening,
    [property: JsonPropertyName("notes")]      string Notes);

public sealed record InsertWindowArgs(
    [property: JsonPropertyName("center")]     Point2dDto Center,
    [property: JsonPropertyName("widthMm")]    double WidthMm = 1200.0,
    [property: JsonPropertyName("wallThicknessMm")] double WallThicknessMm = 200.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]      string Layer = ArchitecturePalette.LayerGlazing,
    // Optional: handle of the wall (Line or 2-vertex Polyline) this window sits
    // in. When supplied, the wall is cut at the window's own axis span (the sill/
    // glass/header line endpoints projected onto the wall axis) BEFORE the window
    // primitives are drawn -- see InsertDoorArgs.WallHandle for the same rationale.
    [property: JsonPropertyName("wallHandle")] string? WallHandle = null);

public sealed record InsertWindowResult(
    [property: JsonPropertyName("sillLine")]   EntityHandle SillLine,
    [property: JsonPropertyName("glassLine")]  EntityHandle GlassLine,
    [property: JsonPropertyName("headerLine")] EntityHandle HeaderLine,
    [property: JsonPropertyName("leftJamb")]   EntityHandle LeftJamb,
    [property: JsonPropertyName("rightJamb")]  EntityHandle RightJamb,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers,
    [property: JsonPropertyName("wallOpening")] SplitWallAtOpeningResult? WallOpening,
    [property: JsonPropertyName("notes")]      string Notes);

#endregion

#region columns

public sealed record InsertRectColumnArgs(
    [property: JsonPropertyName("center")]     Point2dDto Center,
    [property: JsonPropertyName("widthMm")]    double WidthMm = 400.0,
    [property: JsonPropertyName("depthMm")]    double DepthMm = 400.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("centerMarkSizeMm")] double CenterMarkSizeMm = 100.0,
    [property: JsonPropertyName("columnLayer")] string ColumnLayer = ArchitecturePalette.LayerColumns,
    [property: JsonPropertyName("centerMarkLayer")] string CenterMarkLayer = ArchitecturePalette.LayerColumnsCtrl);

public sealed record InsertRoundColumnArgs(
    [property: JsonPropertyName("center")]     Point2dDto Center,
    [property: JsonPropertyName("diameterMm")] double DiameterMm = 400.0,
    [property: JsonPropertyName("centerMarkSizeMm")] double CenterMarkSizeMm = 100.0,
    [property: JsonPropertyName("columnLayer")] string ColumnLayer = ArchitecturePalette.LayerColumns,
    [property: JsonPropertyName("centerMarkLayer")] string CenterMarkLayer = ArchitecturePalette.LayerColumnsCtrl);

public sealed record InsertColumnResult(
    [property: JsonPropertyName("profile")]    EntityHandle Profile,
    [property: JsonPropertyName("centerMarkH")] EntityHandle CenterMarkHorizontal,
    [property: JsonPropertyName("centerMarkV")] EntityHandle CenterMarkVertical,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region rooms

public sealed record DefineRoomArgs(
    [property: JsonPropertyName("vertices")]   IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("number")]     string Number,
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("tagPosition")] Point2dDto? TagPosition = null,   // null => auto-centroid
    [property: JsonPropertyName("tagTextHeightMm")] double TagTextHeightMm = 250.0,
    [property: JsonPropertyName("boundaryLayer")] string BoundaryLayer = ArchitecturePalette.LayerRoomBoundary,
    [property: JsonPropertyName("tagLayer")]   string TagLayer = ArchitecturePalette.LayerRoomIdentification);

public sealed record DefineRoomResult(
    [property: JsonPropertyName("boundary")]   EntityHandle Boundary,
    [property: JsonPropertyName("numberText")] EntityHandle NumberText,
    [property: JsonPropertyName("nameText")]   EntityHandle NameText,
    [property: JsonPropertyName("areaText")]   EntityHandle AreaText,
    [property: JsonPropertyName("areaM2")]     double AreaM2,
    [property: JsonPropertyName("centroid")]   Point2dDto Centroid,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region dimensioning

public sealed record DimensionWallArgs(
    [property: JsonPropertyName("start")]      Point2dDto Start,
    [property: JsonPropertyName("end")]        Point2dDto End,
    [property: JsonPropertyName("offsetMm")]   double OffsetMm = 500.0,
    [property: JsonPropertyName("layer")]      string Layer = ArchitecturePalette.LayerAnnoDims,
    [property: JsonPropertyName("forceLinear")] bool ForceLinear = false,
    [property: JsonPropertyName("forceAligned")] bool ForceAligned = false);

public sealed record DimensionWallResult(
    [property: JsonPropertyName("dimension")]  EntityHandle Dimension,
    [property: JsonPropertyName("primitive")]  string Primitive,    // "linear" | "aligned"
    [property: JsonPropertyName("measureMm")]  double MeasureMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region ceiling / stair / ramp / elevator / tagging (D6)

/// <summary>Draw a T-bar suspended ceiling grid inside a rectangular area on A-CLNG.</summary>
public sealed record DrawCeilingGridArgs(
    [property: JsonPropertyName("bboxMin")]      Point2dDto BboxMin,
    [property: JsonPropertyName("bboxMax")]      Point2dDto BboxMax,
    [property: JsonPropertyName("tileWidthMm")]  double TileWidthMm = 600.0,
    [property: JsonPropertyName("tileDepthMm")]  double TileDepthMm = 600.0,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerCeiling);

public sealed record DrawCeilingGridResult(
    [property: JsonPropertyName("borderHandles")] IReadOnlyList<EntityHandle> BorderHandles,
    [property: JsonPropertyName("verticalHandles")] IReadOnlyList<EntityHandle> VerticalHandles,
    [property: JsonPropertyName("horizontalHandles")] IReadOnlyList<EntityHandle> HorizontalHandles,
    [property: JsonPropertyName("tileCountX")]   int TileCountX,
    [property: JsonPropertyName("tileCountY")]   int TileCountY,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

/// <summary>Simple rectangular-run stair with tread lines + travel-direction arrow.</summary>
public sealed record InsertStairArgs(
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("widthMm")]      double WidthMm = 1200.0,
    [property: JsonPropertyName("runLengthMm")]  double RunLengthMm = 3000.0,
    [property: JsonPropertyName("treadCount")]   int TreadCount = 10,
    [property: JsonPropertyName("directionDeg")] double DirectionDeg = 0.0,
    [property: JsonPropertyName("upLabel")]      string UpLabel = "UP",
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 250.0,
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerStairs,
    [property: JsonPropertyName("annoLayer")]    string AnnoLayer = ArchitecturePalette.LayerAnnoNotes);

public sealed record InsertStairResult(
    [property: JsonPropertyName("outline")]      EntityHandle Outline,
    [property: JsonPropertyName("treads")]       IReadOnlyList<EntityHandle> Treads,
    [property: JsonPropertyName("arrow")]        IReadOnlyList<EntityHandle> Arrow,
    [property: JsonPropertyName("label")]        EntityHandle Label,
    [property: JsonPropertyName("treadDepthMm")] double TreadDepthMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

/// <summary>Rectangular ramp outline with slope arrow and percent-gradient label.</summary>
public sealed record InsertRampArgs(
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("widthMm")]      double WidthMm = 1200.0,
    [property: JsonPropertyName("lengthMm")]     double LengthMm = 6000.0,
    [property: JsonPropertyName("slopePercent")] double SlopePercent = 6.0,
    [property: JsonPropertyName("directionDeg")] double DirectionDeg = 0.0,
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 250.0,
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerStairs,
    [property: JsonPropertyName("annoLayer")]    string AnnoLayer = ArchitecturePalette.LayerAnnoNotes);

public sealed record InsertRampResult(
    [property: JsonPropertyName("outline")]      EntityHandle Outline,
    [property: JsonPropertyName("arrow")]        IReadOnlyList<EntityHandle> Arrow,
    [property: JsonPropertyName("label")]        EntityHandle Label,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

/// <summary>Elevator shaft rectangle with diagonal X and label (no mechanical details).</summary>
public sealed record InsertElevatorArgs(
    [property: JsonPropertyName("center")]       Point2dDto Center,
    [property: JsonPropertyName("widthMm")]      double WidthMm = 1800.0,
    [property: JsonPropertyName("depthMm")]      double DepthMm = 1800.0,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("label")]        string Label = "WINDA",
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 250.0,
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerStairs,
    [property: JsonPropertyName("annoLayer")]    string AnnoLayer = ArchitecturePalette.LayerAnnoNotes);

public sealed record InsertElevatorResult(
    [property: JsonPropertyName("shaft")]        EntityHandle Shaft,
    [property: JsonPropertyName("diagonal1")]    EntityHandle Diagonal1,
    [property: JsonPropertyName("diagonal2")]    EntityHandle Diagonal2,
    [property: JsonPropertyName("label")]        EntityHandle Label,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

/// <summary>Attach a compact room tag (number + name + area) at a centroid — MTEXT-based.</summary>
public sealed record AttachRoomTagArgs(
    [property: JsonPropertyName("center")]       Point2dDto Center,
    [property: JsonPropertyName("number")]       string Number,
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("areaM2")]       double? AreaM2 = null,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 200.0,
    [property: JsonPropertyName("widthMm")]      double WidthMm = 0.0,            // 0 = auto
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerRoomIdentification);

public sealed record AttachRoomTagResult(
    [property: JsonPropertyName("tag")]          EntityHandle Tag,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

/// <summary>
/// High-level convenience that cuts a hole for an opening in a wall. v1 wraps
/// <c>acad.openings.cut_wall_for_opening</c> and inherits its current limitation
/// (Line + 2-vertex Polyline walls only). Agents should call this BEFORE inserting
/// a door/window block so the wall faces are trimmed at the jambs.
/// </summary>
public sealed record SplitWallAtOpeningArgs(
    [property: JsonPropertyName("wallHandle")]   string WallHandle,
    [property: JsonPropertyName("jamb1")]        Point2dDto Jamb1,
    [property: JsonPropertyName("jamb2")]        Point2dDto Jamb2);

public sealed record SplitWallAtOpeningResult(
    [property: JsonPropertyName("originalHandle")] string OriginalHandle,
    [property: JsonPropertyName("leftHandle")]   string? LeftHandle,
    [property: JsonPropertyName("rightHandle")]  string? RightHandle,
    [property: JsonPropertyName("gapLengthMm")]  double GapLengthMm,
    [property: JsonPropertyName("notes")]        string Notes);

#endregion

#region introspection

public sealed record ArchitectureHealthArgs();

public sealed record ArchitectureHealthResult(
    [property: JsonPropertyName("layerKey")]   IReadOnlyList<ArchitecturalLayerSpec> LayerKey,
    [property: JsonPropertyName("bundledBlocks")] IReadOnlyList<string> BundledBlocks,
    [property: JsonPropertyName("category")]   string Category,
    [property: JsonPropertyName("version")]    string Version);

public sealed record ArchitecturalLayerSpec(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("aciColor")]   int AciColor,
    [property: JsonPropertyName("linetype")]   string Linetype,
    [property: JsonPropertyName("purpose")]    string Purpose);

#endregion
