// Typed DTOs for the acad-hatches category.
// Mirrors plugin-side argument shapes 1-to-1. JsonPropertyName MUST match plugin readers.
// See rule 19-tool-implementation-pattern.md and rule 62-hatching-policy.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Hatches;

#region creation args

public sealed record DrawHatchArgs(
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("pattern")]         string Pattern = "ANSI31",
    [property: JsonPropertyName("scale")]           double Scale = 1.0,
    [property: JsonPropertyName("angleDeg")]        double AngleDeg = 0.0,
    [property: JsonPropertyName("layer")]           string? Layer = null,
    [property: JsonPropertyName("color")]           ColorDto? Color = null,
    [property: JsonPropertyName("backgroundColor")] ColorDto? BackgroundColor = null,
    [property: JsonPropertyName("associative")]     bool Associative = true,
    [property: JsonPropertyName("annotative")]      bool Annotative = false);

public sealed record DrawHatchByBoundaryArgs(
    [property: JsonPropertyName("seedPoint")]       Point2dDto SeedPoint,
    [property: JsonPropertyName("pattern")]         string Pattern = "ANSI31",
    [property: JsonPropertyName("scale")]           double Scale = 1.0,
    [property: JsonPropertyName("angleDeg")]        double AngleDeg = 0.0,
    [property: JsonPropertyName("layer")]           string? Layer = null,
    [property: JsonPropertyName("detectIslands")]   bool DetectIslands = true,
    [property: JsonPropertyName("color")]           ColorDto? Color = null);

public sealed record ApplyMaterialPresetArgs(
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("material")]        string Material,
    [property: JsonPropertyName("layer")]           string? Layer = null,
    [property: JsonPropertyName("scaleMultiplier")] double ScaleMultiplier = 1.0);

public sealed record ApplyMaterialPresetByPointArgs(
    [property: JsonPropertyName("seedPoint")]       Point2dDto SeedPoint,
    [property: JsonPropertyName("material")]        string Material,
    [property: JsonPropertyName("layer")]           string? Layer = null,
    [property: JsonPropertyName("scaleMultiplier")] double ScaleMultiplier = 1.0,
    [property: JsonPropertyName("detectIslands")]   bool DetectIslands = true);

public sealed record ClipHatchArgs(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles);

public sealed record RegenerateHatchesArgs(
    [property: JsonPropertyName("handles")]         IReadOnlyList<string>? Handles = null,
    [property: JsonPropertyName("layers")]          IReadOnlyList<string>? Layers = null,
    [property: JsonPropertyName("allInModelSpace")] bool AllInModelSpace = false);

public sealed record ListHatchesArgs(
    [property: JsonPropertyName("layerFilter")]     string? LayerFilter = null,
    [property: JsonPropertyName("patternFilter")]   string? PatternFilter = null);

public sealed record ListPatternsArgs(
    [property: JsonPropertyName("categoryFilter")]  string? CategoryFilter = null);

#endregion

#region results

public sealed record HatchEntityResult(
    [property: JsonPropertyName("entity")]  EntityHandle Entity,
    [property: JsonPropertyName("pattern")] string Pattern,
    [property: JsonPropertyName("scale")]   double Scale,
    [property: JsonPropertyName("angleDeg")] double AngleDeg,
    [property: JsonPropertyName("material")] string? Material);

public sealed record RegenerateHatchesResult(
    [property: JsonPropertyName("regenerated")] int Regenerated,
    [property: JsonPropertyName("failed")]      int Failed,
    [property: JsonPropertyName("failedHandles")] IReadOnlyList<string> FailedHandles);

public sealed record HatchInfoDto(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("layer")]     string Layer,
    [property: JsonPropertyName("pattern")]   string Pattern,
    [property: JsonPropertyName("scale")]     double Scale,
    [property: JsonPropertyName("angleDeg")]  double AngleDeg,
    [property: JsonPropertyName("area")]      double? Area,
    [property: JsonPropertyName("loopCount")] int LoopCount,
    [property: JsonPropertyName("associative")] bool Associative);

public sealed record ListHatchesResult(
    [property: JsonPropertyName("hatches")] IReadOnlyList<HatchInfoDto> Hatches,
    [property: JsonPropertyName("count")]   int Count);

public sealed record HatchPatternDto(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("category")]     string Category,
    [property: JsonPropertyName("description")]  string Description,
    [property: JsonPropertyName("defaultScale")] double DefaultScale,
    [property: JsonPropertyName("defaultAngleDeg")] double DefaultAngleDeg);

public sealed record ListPatternsResult(
    [property: JsonPropertyName("patterns")] IReadOnlyList<HatchPatternDto> Patterns,
    [property: JsonPropertyName("count")]    int Count);

#endregion
