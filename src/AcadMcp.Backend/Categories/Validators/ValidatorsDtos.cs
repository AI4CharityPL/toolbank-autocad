// Wire DTOs for the acad-validators category.
// Mirrored on the plugin side in AcadMcp.Plugin.Tools.ValidatorsDtosPlugin.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Backend.Validators;

namespace AcadMcp.Backend.Categories.Validators;

// ─────────── plugin ↔ backend wire shapes ───────────

public sealed record EntitySnapshotDto(
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

public sealed record CollectEntitiesArgs(
    [property: JsonPropertyName("entityTypes")]  string[]? EntityTypes = null,
    [property: JsonPropertyName("layerPattern")] string? LayerPattern = null,
    [property: JsonPropertyName("layerIn")]      string[]? LayerIn = null,
    [property: JsonPropertyName("inPaperspace")] bool? InPaperspace = null);

public sealed record CollectEntitiesResult(
    [property: JsonPropertyName("entities")]    IReadOnlyList<EntitySnapshotDto> Entities,
    [property: JsonPropertyName("scannedTotal")] int ScannedTotal);

public sealed record DocSummaryArgs();

public sealed record DocSummaryDto(
    [property: JsonPropertyName("documentName")]        string DocumentName,
    [property: JsonPropertyName("documentPath")]        string? DocumentPath,
    [property: JsonPropertyName("units")]               string Units,
    [property: JsonPropertyName("layerNames")]          IReadOnlyList<string> LayerNames,
    [property: JsonPropertyName("blockNames")]          IReadOnlyList<string> BlockNames,
    [property: JsonPropertyName("textStyleNames")]      IReadOnlyList<string> TextStyleNames,
    [property: JsonPropertyName("dimStyleNames")]       IReadOnlyList<string> DimStyleNames,
    [property: JsonPropertyName("entityCountsByType")]  Dictionary<string, int> EntityCountsByType);

public sealed record EntityFixDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("fixType")] string FixType,
    [property: JsonPropertyName("params")]  Dictionary<string, object?>? Params = null);

public sealed record ApplyFixesArgs(
    [property: JsonPropertyName("fixes")] IReadOnlyList<EntityFixDto> Fixes);

public sealed record FixOutcomeDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("fixType")] string FixType,
    // outcome: applied | already_satisfied | manual_only | rolled_back | error
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("message")] string Message);

public sealed record ApplyFixesResult(
    [property: JsonPropertyName("requestedCount")] int RequestedCount,
    [property: JsonPropertyName("appliedCount")]   int AppliedCount,
    [property: JsonPropertyName("outcomes")]       IReadOnlyList<FixOutcomeDto> Outcomes);

// ─────────── tool args ───────────

public sealed record ListValidatorsArgs(
    [property: JsonPropertyName("discipline")]   string? Discipline = null,
    [property: JsonPropertyName("minSeverity")]  string? MinSeverity = null);

public sealed record ListValidatorsResult(
    [property: JsonPropertyName("rules")] IReadOnlyList<RuleDescriptor> Rules);

public sealed record RuleDescriptor(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("severity")]    string Severity,
    [property: JsonPropertyName("discipline")]  string Discipline,
    [property: JsonPropertyName("hasFix")]      bool HasFix,
    [property: JsonPropertyName("description")] string Description);

public sealed record ValidateArgs(
    [property: JsonPropertyName("ruleIds")]      string[]? RuleIds = null,        // null/empty => all
    [property: JsonPropertyName("discipline")]   string? Discipline = null,
    [property: JsonPropertyName("minSeverity")]  string? MinSeverity = null,
    [property: JsonPropertyName("includePaperspace")] bool IncludePaperspace = false);

public sealed record ValidateOneArgs(
    [property: JsonPropertyName("ruleId")] string RuleId);

public sealed record ExplainRuleArgs(
    [property: JsonPropertyName("ruleId")] string RuleId);

public sealed record ExplainRuleResult(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("severity")]    string Severity,
    [property: JsonPropertyName("discipline")]  string Discipline,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("references")]  IReadOnlyList<string> References,
    [property: JsonPropertyName("hasFix")]      bool HasFix,
    [property: JsonPropertyName("source")]      string Source,
    [property: JsonPropertyName("scope")]       object? Scope,
    [property: JsonPropertyName("checks")]      IReadOnlyList<object> Checks,
    [property: JsonPropertyName("fix")]         object? Fix);

public sealed record AddRuleArgs(
    [property: JsonPropertyName("yaml")] string Yaml);

public sealed record AddRuleResult(
    [property: JsonPropertyName("id")]   string Id,
    [property: JsonPropertyName("path")] string Path);

public sealed record AutoFixArgs(
    [property: JsonPropertyName("ruleIds")] string[]? RuleIds = null,        // null => every rule that fired
    [property: JsonPropertyName("dryRun")]  bool DryRun = false);

public sealed record AutoFixResult(
    [property: JsonPropertyName("dryRun")]   bool DryRun,
    [property: JsonPropertyName("planned")]  int Planned,
    [property: JsonPropertyName("applied")]  int Applied,
    [property: JsonPropertyName("outcomes")] IReadOnlyList<FixOutcomeDto> Outcomes);

public sealed record StandardArgs(
    [property: JsonPropertyName("standardId")] string StandardId);

public sealed record StandardDescriptor(
    [property: JsonPropertyName("id")]      string Id,
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("ruleIds")] IReadOnlyList<string> RuleIds);

public sealed record ListStandardsResult(
    [property: JsonPropertyName("standards")] IReadOnlyList<StandardDescriptor> Standards);

// ─────────── check_overlaps ───────────

public sealed record OverlapsWindow(
    [property: JsonPropertyName("xMin")] double XMin,
    [property: JsonPropertyName("yMin")] double YMin,
    [property: JsonPropertyName("xMax")] double XMax,
    [property: JsonPropertyName("yMax")] double YMax);

public sealed record CheckOverlapsArgs(
    [property: JsonPropertyName("layersA")]    string[] LayersA,
    [property: JsonPropertyName("layersB")]    string[]? LayersB = null,
    [property: JsonPropertyName("mode")]       string? Mode = null,
    [property: JsonPropertyName("tolerance")]  double Tolerance = 0.0,
    [property: JsonPropertyName("window")]     OverlapsWindow? Window = null,
    [property: JsonPropertyName("maxResults")] int MaxResults = 500);

public sealed record OverlapPair(
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

public sealed record CheckOverlapsResult(
    [property: JsonPropertyName("overlaps")]   IReadOnlyList<OverlapPair> Overlaps,
    [property: JsonPropertyName("scannedA")]   int ScannedA,
    [property: JsonPropertyName("scannedB")]   int ScannedB,
    [property: JsonPropertyName("mode")]       string Mode,
    [property: JsonPropertyName("truncated")]  bool Truncated);
