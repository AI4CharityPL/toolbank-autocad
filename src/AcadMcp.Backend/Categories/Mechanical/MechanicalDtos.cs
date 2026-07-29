// Wire DTOs for the acad-mechanical domain category. Mirrors plugin readers
// 1-to-1 (rule 22). Every JsonPropertyName matches a field on a primitive
// plugin handler used by MechanicalProxy.
//
// Rules: 35-domain-categories-design.mdc, 37-mechanical-domain-traps.mdc.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Mechanical;

#region infrastructure

public sealed record LayerEnsureOutcome(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("status")]  string Status,
    [property: JsonPropertyName("color")]   int? AciColor,
    [property: JsonPropertyName("linetype")] string? Linetype,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm,
    [property: JsonPropertyName("error")]   string? Error = null);

public sealed record EnsureMechanicalLayersArgs(
    [property: JsonPropertyName("includeConstruction")] bool IncludeConstruction = true,
    [property: JsonPropertyName("includeRevision")]     bool IncludeRevision     = true);

public sealed record EnsureMechanicalLayersResult(
    [property: JsonPropertyName("layers")]   IReadOnlyList<LayerEnsureOutcome> Layers,
    [property: JsonPropertyName("createdCount")]  int CreatedCount,
    [property: JsonPropertyName("existingCount")] int ExistingCount);

#endregion

#region edge-class lines

public sealed record DrawCenterlineArgs(
    [property: JsonPropertyName("start")] Point2dDto Start,
    [property: JsonPropertyName("end")]   Point2dDto End,
    [property: JsonPropertyName("layer")] string Layer = MechanicalPalette.LayerCenter);

public sealed record DrawHiddenEdgeArgs(
    [property: JsonPropertyName("start")] Point2dDto Start,
    [property: JsonPropertyName("end")]   Point2dDto End,
    [property: JsonPropertyName("layer")] string Layer = MechanicalPalette.LayerHidden);

public sealed record DrawVisibleEdgeArgs(
    [property: JsonPropertyName("start")] Point2dDto Start,
    [property: JsonPropertyName("end")]   Point2dDto End,
    [property: JsonPropertyName("layer")] string Layer = MechanicalPalette.LayerVisible);

public sealed record DrawCenterlineCrossArgs(
    [property: JsonPropertyName("center")]      Point2dDto Center,
    [property: JsonPropertyName("featureRadiusMm")] double FeatureRadiusMm,
    [property: JsonPropertyName("extensionMm")] double ExtensionMm = 4.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]       string Layer = MechanicalPalette.LayerCenter);

public sealed record EdgeLineResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record CenterlineCrossResult(
    [property: JsonPropertyName("horizontal")]    EntityHandle Horizontal,
    [property: JsonPropertyName("vertical")]      EntityHandle Vertical,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region section views

public sealed record DrawSectionCutLineArgs(
    [property: JsonPropertyName("start")]              Point2dDto Start,
    [property: JsonPropertyName("end")]                Point2dDto End,
    [property: JsonPropertyName("label")]              string Label,                  // "A", "B-B", ...
    [property: JsonPropertyName("arrowSizeMm")]        double ArrowSizeMm = 6.0,
    [property: JsonPropertyName("labelTextHeightMm")]  double LabelTextHeightMm = 5.0,
    [property: JsonPropertyName("sectionLayer")]       string SectionLayer = MechanicalPalette.LayerSection,
    [property: JsonPropertyName("textLayer")]          string TextLayer    = MechanicalPalette.LayerText);

public sealed record DrawSectionCutLineResult(
    [property: JsonPropertyName("cuttingPlane")]  EntityHandle CuttingPlane,
    [property: JsonPropertyName("arrowStart")]    EntityHandle ArrowStart,
    [property: JsonPropertyName("arrowEnd")]      EntityHandle ArrowEnd,
    [property: JsonPropertyName("labelStart")]    EntityHandle LabelStart,
    [property: JsonPropertyName("labelEnd")]      EntityHandle LabelEnd,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region holes

public sealed record DrawThroughHoleArgs(
    [property: JsonPropertyName("center")]            Point2dDto Center,
    [property: JsonPropertyName("diameterMm")]        double DiameterMm,
    [property: JsonPropertyName("centerlineExtensionMm")] double CenterlineExtensionMm = 4.0,
    [property: JsonPropertyName("profileLayer")]      string ProfileLayer = MechanicalPalette.LayerVisible,
    [property: JsonPropertyName("centerLayer")]       string CenterLayer  = MechanicalPalette.LayerCenter);

public sealed record DrawCounterboreHoleArgs(
    [property: JsonPropertyName("center")]            Point2dDto Center,
    [property: JsonPropertyName("throughDiameterMm")] double ThroughDiameterMm,
    [property: JsonPropertyName("counterboreDiameterMm")] double CounterboreDiameterMm,
    [property: JsonPropertyName("centerlineExtensionMm")] double CenterlineExtensionMm = 4.0,
    [property: JsonPropertyName("profileLayer")]      string ProfileLayer = MechanicalPalette.LayerVisible,
    [property: JsonPropertyName("centerLayer")]       string CenterLayer  = MechanicalPalette.LayerCenter);

public sealed record DrawThreadedHoleArgs(
    [property: JsonPropertyName("center")]            Point2dDto Center,
    [property: JsonPropertyName("majorDiameterMm")]   double MajorDiameterMm,
    [property: JsonPropertyName("minorDiameterMm")]   double MinorDiameterMm,
    [property: JsonPropertyName("threadGapDeg")]      double ThreadGapDeg = 90.0,    // rule 37 §4a — 270° arc default
    [property: JsonPropertyName("threadGapStartDeg")] double ThreadGapStartDeg = 0.0,
    [property: JsonPropertyName("centerlineExtensionMm")] double CenterlineExtensionMm = 4.0,
    [property: JsonPropertyName("profileLayer")]      string ProfileLayer = MechanicalPalette.LayerVisible,
    [property: JsonPropertyName("threadLayer")]       string ThreadLayer  = MechanicalPalette.LayerThread,
    [property: JsonPropertyName("centerLayer")]       string CenterLayer  = MechanicalPalette.LayerCenter);

public sealed record DrawHoleResult(
    [property: JsonPropertyName("profile")]       EntityHandle Profile,
    [property: JsonPropertyName("centerH")]       EntityHandle CenterHorizontal,
    [property: JsonPropertyName("centerV")]       EntityHandle CenterVertical,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record DrawCounterboreHoleResult(
    [property: JsonPropertyName("throughCircle")]    EntityHandle ThroughCircle,
    [property: JsonPropertyName("counterboreCircle")] EntityHandle CounterboreCircle,
    [property: JsonPropertyName("centerH")]          EntityHandle CenterHorizontal,
    [property: JsonPropertyName("centerV")]          EntityHandle CenterVertical,
    [property: JsonPropertyName("createdLayers")]    IReadOnlyList<string> CreatedLayers);

public sealed record DrawThreadedHoleResult(
    [property: JsonPropertyName("majorCircle")]   EntityHandle MajorCircle,
    [property: JsonPropertyName("minorArc")]      EntityHandle MinorArc,
    [property: JsonPropertyName("centerH")]       EntityHandle CenterHorizontal,
    [property: JsonPropertyName("centerV")]       EntityHandle CenterVertical,
    [property: JsonPropertyName("threadArcSpanDeg")] double ThreadArcSpanDeg,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

// Side-view (vertical cross-section) hole profiles -- Phase 7.3. Y axis runs
// DOWN into the material from the top surface (top = origin.y, bottom = origin.y - depthMm).
// "through" has no bottom line (open both ends); the other three kinds are closed at
// the bottom (blind = drill-point V, counterbore/countersink share the stepped-wall shape).
public sealed record DrawHoleSideViewArgs(
    [property: JsonPropertyName("kind")]                string Kind, // "through" | "blind" | "counterbore" | "countersink"
    [property: JsonPropertyName("topCenter")]           Point2dDto TopCenter,
    [property: JsonPropertyName("diameterMm")]          double DiameterMm,
    [property: JsonPropertyName("depthMm")]             double DepthMm,
    [property: JsonPropertyName("drillPointAngleDeg")]  double DrillPointAngleDeg = 118.0,      // "blind" only
    [property: JsonPropertyName("counterboreDiameterMm")] double? CounterboreDiameterMm = null, // "counterbore" only
    [property: JsonPropertyName("counterboreDepthMm")] double? CounterboreDepthMm = null,       // "counterbore" only
    [property: JsonPropertyName("headDiameterMm")]      double? HeadDiameterMm = null,          // "countersink" only
    [property: JsonPropertyName("countersinkAngleDeg")] double CountersinkAngleDeg = 90.0,      // "countersink" only, INCLUDED angle
    [property: JsonPropertyName("centerlineExtensionMm")] double CenterlineExtensionMm = 4.0,
    [property: JsonPropertyName("profileLayer")]        string ProfileLayer = MechanicalPalette.LayerVisible,
    [property: JsonPropertyName("centerLayer")]         string CenterLayer  = MechanicalPalette.LayerCenter);

public sealed record DrawHoleSideViewResult(
    [property: JsonPropertyName("kind")]          string Kind,
    [property: JsonPropertyName("profile")]       IReadOnlyList<EntityHandle> Profile,
    [property: JsonPropertyName("centerline")]    EntityHandle Centerline,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record DrawSectionHatchArgs(
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("material")]        string Material,   // key into MechanicalPatterns.ByMaterial
    [property: JsonPropertyName("scaleOverride")]   double? ScaleOverride = null,
    [property: JsonPropertyName("angleOverrideDeg")] double? AngleOverrideDeg = null,
    [property: JsonPropertyName("layer")]           string Layer = MechanicalPalette.LayerHatch);

public sealed record DrawSectionHatchResult(
    [property: JsonPropertyName("hatch")]         EntityHandle Hatch,
    [property: JsonPropertyName("material")]      string Material,
    [property: JsonPropertyName("pattern")]       string Pattern,
    [property: JsonPropertyName("scale")]         double Scale,
    [property: JsonPropertyName("angleDeg")]      double AngleDeg,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region fasteners

public sealed record DrawBoltHeadTopViewArgs(
    [property: JsonPropertyName("center")]        Point2dDto Center,
    [property: JsonPropertyName("flatToFlatMm")]  double FlatToFlatMm,
    [property: JsonPropertyName("rotationDeg")]   double RotationDeg = 0.0,
    [property: JsonPropertyName("includeShankCircle")] bool IncludeShankCircle = true,
    [property: JsonPropertyName("nominalDiameterMm")] double? NominalDiameterMm = null,
    [property: JsonPropertyName("centerlineExtensionMm")] double CenterlineExtensionMm = 4.0,
    [property: JsonPropertyName("profileLayer")]  string ProfileLayer = MechanicalPalette.LayerVisible,
    [property: JsonPropertyName("centerLayer")]   string CenterLayer  = MechanicalPalette.LayerCenter);

public sealed record DrawBoltHeadTopViewResult(
    [property: JsonPropertyName("hexagon")]       EntityHandle Hexagon,
    [property: JsonPropertyName("shankCircle")]   EntityHandle? ShankCircle,
    [property: JsonPropertyName("centerH")]       EntityHandle CenterHorizontal,
    [property: JsonPropertyName("centerV")]       EntityHandle CenterVertical,
    [property: JsonPropertyName("acrossCornersMm")] double AcrossCornersMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region revisions

public sealed record DrawRevisionTriangleArgs(
    [property: JsonPropertyName("position")]       Point2dDto Position,
    [property: JsonPropertyName("revision")]       string Revision,            // "1" | "A" | ...
    [property: JsonPropertyName("sideMm")]         double SideMm = 8.0,
    [property: JsonPropertyName("rotationDeg")]    double RotationDeg = 0.0,
    [property: JsonPropertyName("textHeightMm")]   double TextHeightMm = 3.5,
    [property: JsonPropertyName("triangleLayer")]  string TriangleLayer = MechanicalPalette.LayerRev,
    [property: JsonPropertyName("textLayer")]      string TextLayer     = MechanicalPalette.LayerRev);

public sealed record DrawRevisionTriangleResult(
    [property: JsonPropertyName("triangle")]      EntityHandle Triangle,
    [property: JsonPropertyName("text")]          EntityHandle Text,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region introspection

public sealed record MechanicalHealthArgs();

public sealed record MechanicalLayerSpec(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("aciColor")]     int AciColor,
    [property: JsonPropertyName("linetype")]     string Linetype,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm,
    [property: JsonPropertyName("plottable")]    bool Plottable,
    [property: JsonPropertyName("purpose")]      string Purpose);

public sealed record MechanicalMaterialSpec(
    [property: JsonPropertyName("material")] string Material,
    [property: JsonPropertyName("pattern")]  string Pattern,
    [property: JsonPropertyName("scale")]    double Scale,
    [property: JsonPropertyName("angleDeg")] double AngleDeg);

public sealed record MechanicalHealthResult(
    [property: JsonPropertyName("layerKey")]      IReadOnlyList<MechanicalLayerSpec> LayerKey,
    [property: JsonPropertyName("materials")]     IReadOnlyList<MechanicalMaterialSpec> Materials,
    [property: JsonPropertyName("bundledBlocks")] IReadOnlyList<string> BundledBlocks,
    [property: JsonPropertyName("category")]      string Category,
    [property: JsonPropertyName("version")]       string Version);

#endregion
