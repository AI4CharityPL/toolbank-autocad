// Wire DTOs for the acad-civil domain category. Mirrors plugin readers 1-to-1
// (rule 22). Every JsonPropertyName matches a field on a primitive plugin
// handler used by CivilProxy.
//
// Rules: 35-domain-categories-design.mdc, 38-civil-domain-traps.mdc.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Civil;

#region infrastructure

public sealed record LayerEnsureOutcome(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("status")]  string Status,
    [property: JsonPropertyName("color")]   int? AciColor,
    [property: JsonPropertyName("linetype")] string? Linetype,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm,
    [property: JsonPropertyName("error")]   string? Error = null);

public sealed record EnsureCivilLayersArgs(
    [property: JsonPropertyName("includeRoad")]      bool IncludeRoad      = true,
    [property: JsonPropertyName("includeProperty")]  bool IncludeProperty  = true,
    [property: JsonPropertyName("includeTopo")]      bool IncludeTopo      = true);

public sealed record EnsureCivilLayersResult(
    [property: JsonPropertyName("layers")]        IReadOnlyList<LayerEnsureOutcome> Layers,
    [property: JsonPropertyName("createdCount")]  int CreatedCount,
    [property: JsonPropertyName("existingCount")] int ExistingCount);

#endregion

#region road alignment

public sealed record DrawAlignmentTangentArgs(
    [property: JsonPropertyName("start")]      Point2dDto Start,
    [property: JsonPropertyName("end")]        Point2dDto End,
    [property: JsonPropertyName("layer")]      string Layer = CivilPalette.LayerRoadCntr);

public sealed record DrawAlignmentCurveArgs(
    [property: JsonPropertyName("center")]        Point2dDto Center,
    [property: JsonPropertyName("radiusM")]       double RadiusM,
    [property: JsonPropertyName("startAngleDeg")] double StartAngleDeg,
    [property: JsonPropertyName("endAngleDeg")]   double EndAngleDeg,
    [property: JsonPropertyName("layer")]         string Layer = CivilPalette.LayerRoadCntr);

public sealed record AlignmentSegmentResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record DrawRoadCorridorArgs(
    [property: JsonPropertyName("centerline")]   IReadOnlyList<Point2dDto> Centerline,
    [property: JsonPropertyName("widthM")]       double WidthM,
    [property: JsonPropertyName("centerlineLayer")] string CenterlineLayer = CivilPalette.LayerRoadCntr,
    [property: JsonPropertyName("edgeLayer")]    string EdgeLayer = CivilPalette.LayerRoadEdge);

public sealed record DrawRoadCorridorResult(
    [property: JsonPropertyName("centerline")]    EntityHandle Centerline,
    [property: JsonPropertyName("leftEdge")]      EntityHandle LeftEdge,
    [property: JsonPropertyName("rightEdge")]     EntityHandle RightEdge,
    [property: JsonPropertyName("widthM")]        double WidthM,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region stationing

public sealed record PlaceStationLabelsArgs(
    [property: JsonPropertyName("centerline")]      IReadOnlyList<Point2dDto> Centerline,
    [property: JsonPropertyName("intervalM")]       double IntervalM = 20.0,
    [property: JsonPropertyName("startStationM")]   double StartStationM = 0.0,
    [property: JsonPropertyName("system")]          string System = "metric_km", // "metric_km" | "us_feet"
    [property: JsonPropertyName("tickLengthM")]     double TickLengthM = 2.0,
    [property: JsonPropertyName("labelOffsetM")]    double LabelOffsetM = 1.5,
    [property: JsonPropertyName("textHeightM")]     double TextHeightM = 1.2,
    [property: JsonPropertyName("tickLayer")]       string TickLayer  = CivilPalette.LayerStation,
    [property: JsonPropertyName("labelLayer")]      string LabelLayer = CivilPalette.LayerStation);

public sealed record PlacedStation(
    [property: JsonPropertyName("stationM")] double StationM,
    [property: JsonPropertyName("label")]    string Label,
    [property: JsonPropertyName("position")] Point2dDto Position,
    [property: JsonPropertyName("tick")]     EntityHandle Tick,
    [property: JsonPropertyName("text")]     EntityHandle Text);

public sealed record PlaceStationLabelsResult(
    [property: JsonPropertyName("stations")]       IReadOnlyList<PlacedStation> Stations,
    [property: JsonPropertyName("createdLayers")]  IReadOnlyList<string> CreatedLayers);

#endregion

#region parcel

public sealed record ParcelLeg(
    [property: JsonPropertyName("bearing")]   string BearingText,    // "N 45 30 15 E" form
    [property: JsonPropertyName("distanceM")] double DistanceM);

public sealed record DrawParcelArgs(
    [property: JsonPropertyName("start")]            Point2dDto Start,
    [property: JsonPropertyName("legs")]             IReadOnlyList<ParcelLeg> Legs,
    [property: JsonPropertyName("kind")]             string Kind = "residential",  // "residential"|"commercial"|"agricultural"|"forest"
    [property: JsonPropertyName("toleranceMOverride")] double? ToleranceMOverride = null,
    [property: JsonPropertyName("autoClose")]        bool AutoClose = false,
    [property: JsonPropertyName("layer")]            string Layer = CivilPalette.LayerProperty);

public sealed record DrawParcelResult(
    [property: JsonPropertyName("parcel")]           EntityHandle Parcel,
    [property: JsonPropertyName("vertices")]         IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("closureErrorM")]    double ClosureErrorM,
    [property: JsonPropertyName("toleranceM")]       double ToleranceM,
    [property: JsonPropertyName("closureStatus")]    string ClosureStatus,        // "in_tolerance"|"out_of_tolerance"
    [property: JsonPropertyName("autoClosed")]       bool AutoClosed,
    [property: JsonPropertyName("createdLayers")]    IReadOnlyList<string> CreatedLayers);

#endregion

#region topography

public sealed record DrawContourLineArgs(
    [property: JsonPropertyName("vertices")]       IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("elevationM")]     double ElevationM,
    [property: JsonPropertyName("isMajor")]        bool IsMajor = true,
    [property: JsonPropertyName("labelEvery")]     int LabelEvery = 1,             // emit label this many vertices in
    [property: JsonPropertyName("textHeightM")]    double TextHeightM = 1.0,
    [property: JsonPropertyName("majorLayer")]     string MajorLayer = CivilPalette.LayerTopoMajr,
    [property: JsonPropertyName("minorLayer")]     string MinorLayer = CivilPalette.LayerTopoMinr,
    [property: JsonPropertyName("labelLayer")]     string LabelLayer = CivilPalette.LayerTopoMajr);

public sealed record DrawContourLineResult(
    [property: JsonPropertyName("contour")]        EntityHandle Contour,
    [property: JsonPropertyName("label")]          EntityHandle? Label,
    [property: JsonPropertyName("layer")]          string Layer,
    [property: JsonPropertyName("createdLayers")]  IReadOnlyList<string> CreatedLayers);

public sealed record PlaceSpotElevationArgs(
    [property: JsonPropertyName("position")]       Point2dDto Position,
    [property: JsonPropertyName("elevationM")]     double ElevationM,
    [property: JsonPropertyName("crossSizeM")]     double CrossSizeM = 0.5,
    [property: JsonPropertyName("textHeightM")]    double TextHeightM = 1.0,
    [property: JsonPropertyName("textOffsetM")]    double TextOffsetM = 1.0,
    [property: JsonPropertyName("crossLayer")]     string CrossLayer = CivilPalette.LayerTopoSpot,
    [property: JsonPropertyName("textLayer")]      string TextLayer  = CivilPalette.LayerTopoSpot);

public sealed record PlaceSpotElevationResult(
    [property: JsonPropertyName("crossH")]         EntityHandle CrossHorizontal,
    [property: JsonPropertyName("crossV")]         EntityHandle CrossVertical,
    [property: JsonPropertyName("text")]           EntityHandle Text,
    [property: JsonPropertyName("formatted")]      string Formatted,
    [property: JsonPropertyName("createdLayers")]  IReadOnlyList<string> CreatedLayers);

#endregion

#region north arrow

public sealed record DrawNorthArrowArgs(
    [property: JsonPropertyName("position")]                  Point2dDto Position,
    [property: JsonPropertyName("sizeM")]                     double SizeM = 5.0,
    [property: JsonPropertyName("trueNorthDegFromPageNorth")] double TrueNorthDegFromPageNorth = 0.0,
    [property: JsonPropertyName("includeLetter")]             bool IncludeLetter = true,
    [property: JsonPropertyName("layer")]                     string Layer = CivilPalette.LayerNorth);

public sealed record DrawNorthArrowResult(
    [property: JsonPropertyName("arrow")]          EntityHandle Arrow,
    [property: JsonPropertyName("letter")]         EntityHandle? Letter,
    [property: JsonPropertyName("createdLayers")]  IReadOnlyList<string> CreatedLayers);

#endregion

#region introspection

public sealed record CivilHealthArgs();

public sealed record CivilLayerSpec(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("aciColor")]     int AciColor,
    [property: JsonPropertyName("linetype")]     string Linetype,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm,
    [property: JsonPropertyName("plottable")]    bool Plottable,
    [property: JsonPropertyName("purpose")]      string Purpose);

public sealed record CivilParcelToleranceSpec(
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("toleranceM")]  double ToleranceM);

public sealed record CivilHealthResult(
    [property: JsonPropertyName("layerKey")]            IReadOnlyList<CivilLayerSpec> LayerKey,
    [property: JsonPropertyName("parcelTolerances")]    IReadOnlyList<CivilParcelToleranceSpec> ParcelTolerances,
    [property: JsonPropertyName("stationingSystems")]   IReadOnlyList<string> StationingSystems,
    [property: JsonPropertyName("bundledBlocks")]       IReadOnlyList<string> BundledBlocks,
    [property: JsonPropertyName("category")]            string Category,
    [property: JsonPropertyName("version")]             string Version);

#endregion
