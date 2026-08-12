// Wire DTOs for the acad-structural category.
// Mirrors plugin-side argument shapes 1-to-1 where a plugin call is made. Most tools in
// this category compose from already-deployed generic acad.geometry2d.* / acad.layers.*
// verbs via ArchitectureProxy rather than a dedicated acad.structural.* plugin handler -
// see rule 72 §2. list_steel_profiles makes no plugin call at all - it is a pure in-memory
// catalog read and works even with no AutoCAD document open.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Shared;
using AcadMcp.Shared.Catalogs;

namespace AcadMcp.Backend.Categories.Structural;

#region list_steel_profiles

public sealed record ListSteelProfilesArgs(
    [property: JsonPropertyName("seriesFilter")] string? SeriesFilter = null);

public sealed record SteelProfileDto(
    [property: JsonPropertyName("designation")]   string Designation,
    [property: JsonPropertyName("series")]        string Series,
    [property: JsonPropertyName("heightMm")]      double HeightMm,
    [property: JsonPropertyName("widthMm")]       double WidthMm,
    [property: JsonPropertyName("webThicknessMm")] double WebThicknessMm,
    [property: JsonPropertyName("flangeThicknessMm")] double FlangeThicknessMm,
    [property: JsonPropertyName("weightKgPerM")]  double WeightKgPerM,
    [property: JsonPropertyName("areaCm2")]       double AreaCm2,
    [property: JsonPropertyName("standard")]      string Standard,
    [property: JsonPropertyName("description")]   string Description);

public sealed record ListSteelProfilesResult(
    [property: JsonPropertyName("profiles")] IReadOnlyList<SteelProfileDto> Profiles,
    [property: JsonPropertyName("count")]    int Count);

#endregion

#region insert_steel_column

public sealed record InsertSteelColumnArgs(
    [property: JsonPropertyName("designation")]  string Designation,
    [property: JsonPropertyName("center")]       Point2dDto Center,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("centerMarkSizeMm")] double CenterMarkSizeMm = 100.0,
    [property: JsonPropertyName("columnLayer")]  string ColumnLayer = ArchitecturePalette.LayerColumns,
    [property: JsonPropertyName("centerMarkLayer")] string CenterMarkLayer = ArchitecturePalette.LayerColumnsCtrl);

public sealed record InsertSteelColumnResult(
    [property: JsonPropertyName("profile")]      EntityHandle Profile,
    [property: JsonPropertyName("centerMarkH")]  EntityHandle CenterMarkHorizontal,
    [property: JsonPropertyName("centerMarkV")]  EntityHandle CenterMarkVertical,
    [property: JsonPropertyName("designation")]  string Designation,
    [property: JsonPropertyName("weightKgPerM")] double WeightKgPerM,
    [property: JsonPropertyName("areaCm2")]      double AreaCm2,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region insert_beam

public sealed record InsertBeamArgs(
    [property: JsonPropertyName("start")]        Point2dDto Start,
    [property: JsonPropertyName("end")]          Point2dDto End,
    [property: JsonPropertyName("designation")]  string? Designation = null,
    [property: JsonPropertyName("widthMm")]      double? WidthMm = null,
    [property: JsonPropertyName("label")]        string? Label = null,
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerBeam,
    [property: JsonPropertyName("centerlineLayer")] string CenterlineLayer = ArchitecturePalette.LayerBeamCtrl);

public sealed record InsertBeamResult(
    [property: JsonPropertyName("outline")]      EntityHandle Outline,
    [property: JsonPropertyName("centerline")]   EntityHandle Centerline,
    [property: JsonPropertyName("label")]        EntityHandle? LabelText,
    [property: JsonPropertyName("lengthMm")]     double LengthMm,
    [property: JsonPropertyName("widthMm")]      double WidthMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region insert_lintel

public sealed record InsertLintelArgs(
    [property: JsonPropertyName("position")]     Point2dDto? Position = null,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("jamb1")]        Point2dDto? Jamb1 = null,
    [property: JsonPropertyName("jamb2")]        Point2dDto? Jamb2 = null,
    [property: JsonPropertyName("spanMm")]       double? SpanMm = null,
    [property: JsonPropertyName("wallThicknessMm")] double WallThicknessMm = 250.0,
    [property: JsonPropertyName("bearingMm")]    double BearingMm = 200.0,
    [property: JsonPropertyName("materialHint")] string MaterialHint = "rc",
    [property: JsonPropertyName("drawPlanSymbol")] bool DrawPlanSymbol = true,
    [property: JsonPropertyName("layer")]        string Layer = ArchitecturePalette.LayerLintel,
    [property: JsonPropertyName("mark")]         string? Mark = null);

public sealed record InsertLintelResult(
    [property: JsonPropertyName("lintelTypeTag")] string LintelTypeTag,
    [property: JsonPropertyName("computedDepthMm")] double ComputedDepthMm,
    [property: JsonPropertyName("totalLengthMm")] double TotalLengthMm,
    [property: JsonPropertyName("suggestedSteelProfile")] string? SuggestedSteelProfile,
    [property: JsonPropertyName("planSymbol")]   EntityHandle? PlanSymbol,
    [property: JsonPropertyName("mark")]         EntityHandle? MarkText,
    [property: JsonPropertyName("disclaimer")]   string Disclaimer,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region ensure_structural_layers

public sealed record EnsureStructuralLayersArgs();

public sealed record EnsureStructuralLayersResult(
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion
