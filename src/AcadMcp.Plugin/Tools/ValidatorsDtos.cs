// Plugin-side DTOs for the acad-validators category.
// Mirror Backend/Categories/Validators/ValidatorsDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record CollectEntitiesArgsDto(
    [property: JsonPropertyName("entityTypes")]  string[]? EntityTypes = null,
    [property: JsonPropertyName("layerPattern")] string? LayerPattern = null,
    [property: JsonPropertyName("layerIn")]      string[]? LayerIn = null,
    [property: JsonPropertyName("inPaperspace")] bool? InPaperspace = null);

internal sealed record EntitySnapshotPluginDto(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("dxfType")]     string DxfType,
    [property: JsonPropertyName("className")]   string? ClassName,
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("colorAci")]    int? ColorAci,
    [property: JsonPropertyName("colorRgb")]    int[]? ColorRgb,
    [property: JsonPropertyName("linetype")]    string Linetype,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm,
    [property: JsonPropertyName("length")]      double? Length,
    [property: JsonPropertyName("area")]        double? Area,
    [property: JsonPropertyName("radius")]      double? Radius,
    [property: JsonPropertyName("textValue")]   string? TextValue,
    [property: JsonPropertyName("textHeight")]  double? TextHeight,
    [property: JsonPropertyName("blockName")]   string? BlockName,
    [property: JsonPropertyName("attributes")]  Dictionary<string, string>? Attributes,
    [property: JsonPropertyName("vertices")]    double[][]? Vertices,
    [property: JsonPropertyName("bboxMin")]     double[] BboxMin,
    [property: JsonPropertyName("bboxMax")]     double[] BboxMax,
    [property: JsonPropertyName("inPaperspace")] bool InPaperspace);

internal sealed record CollectEntitiesResultDto(
    [property: JsonPropertyName("entities")]    List<EntitySnapshotPluginDto> Entities,
    [property: JsonPropertyName("scannedTotal")] int ScannedTotal);

internal sealed record DocSummaryArgsDto();

internal sealed record DocSummaryResultDto(
    [property: JsonPropertyName("documentName")]        string DocumentName,
    [property: JsonPropertyName("documentPath")]        string? DocumentPath,
    [property: JsonPropertyName("units")]               string Units,
    [property: JsonPropertyName("layerNames")]          List<string> LayerNames,
    [property: JsonPropertyName("blockNames")]          List<string> BlockNames,
    [property: JsonPropertyName("textStyleNames")]      List<string> TextStyleNames,
    [property: JsonPropertyName("dimStyleNames")]       List<string> DimStyleNames,
    [property: JsonPropertyName("entityCountsByType")]  Dictionary<string, int> EntityCountsByType);

internal sealed record EntityFixPluginDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("fixType")]  string FixType,
    [property: JsonPropertyName("params")]   JsonObject? Params = null);

internal sealed record ApplyFixesArgsDto(
    [property: JsonPropertyName("fixes")] List<EntityFixPluginDto> Fixes);

internal sealed record FixOutcomePluginDto(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("fixType")] string FixType,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("message")] string Message);

internal sealed record ApplyFixesResultDto(
    [property: JsonPropertyName("requestedCount")] int RequestedCount,
    [property: JsonPropertyName("appliedCount")]   int AppliedCount,
    [property: JsonPropertyName("outcomes")]       List<FixOutcomePluginDto> Outcomes);

// ───── check_overlaps ─────

internal sealed record OverlapsWindowPluginDto(
    [property: JsonPropertyName("xMin")] double XMin,
    [property: JsonPropertyName("yMin")] double YMin,
    [property: JsonPropertyName("xMax")] double XMax,
    [property: JsonPropertyName("yMax")] double YMax);

internal sealed record CheckOverlapsArgsDto(
    [property: JsonPropertyName("layersA")]   string[]? LayersA = null,
    [property: JsonPropertyName("layersB")]   string[]? LayersB = null,
    [property: JsonPropertyName("mode")]      string? Mode = null,
    [property: JsonPropertyName("tolerance")] double Tolerance = 0.0,
    [property: JsonPropertyName("window")]    OverlapsWindowPluginDto? Window = null,
    [property: JsonPropertyName("maxResults")] int MaxResults = 500);

internal sealed record OverlapPairPluginDto(
    [property: JsonPropertyName("handleA")]     string HandleA,
    [property: JsonPropertyName("handleB")]     string HandleB,
    [property: JsonPropertyName("layerA")]      string LayerA,
    [property: JsonPropertyName("layerB")]      string LayerB,
    [property: JsonPropertyName("dxfTypeA")]    string DxfTypeA,
    [property: JsonPropertyName("dxfTypeB")]    string DxfTypeB,
    [property: JsonPropertyName("bboxA")]       double[] BboxA,
    [property: JsonPropertyName("bboxB")]       double[] BboxB,
    [property: JsonPropertyName("overlapArea")] double OverlapArea,
    [property: JsonPropertyName("intersectionCount")] int IntersectionCount,
    [property: JsonPropertyName("severity")]    string Severity,
    [property: JsonPropertyName("mode")]        string Mode);

internal sealed record CheckOverlapsResultDto(
    [property: JsonPropertyName("overlaps")]   List<OverlapPairPluginDto> Overlaps,
    [property: JsonPropertyName("scannedA")]   int ScannedA,
    [property: JsonPropertyName("scannedB")]   int ScannedB,
    [property: JsonPropertyName("mode")]       string Mode,
    [property: JsonPropertyName("truncated")]  bool Truncated);
