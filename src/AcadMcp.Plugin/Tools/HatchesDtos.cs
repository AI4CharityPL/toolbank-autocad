// Plugin-side DTOs mirroring the wire shape sent by AcadMcp.Backend.Categories.Hatches.
// Kept here (not in Shared) because they are category-internal — Shared stays small.
// JsonPropertyName MUST match the Backend serializer (camelCase).

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record HatchesDrawHatchArgsDto(
    [property: JsonPropertyName("boundaryHandles")] List<string> BoundaryHandles,
    [property: JsonPropertyName("pattern")]         string Pattern,
    [property: JsonPropertyName("scale")]           double Scale,
    [property: JsonPropertyName("angleDeg")]        double AngleDeg,
    [property: JsonPropertyName("layer")]           string? Layer,
    [property: JsonPropertyName("color")]           ColorDto? Color,
    [property: JsonPropertyName("backgroundColor")] ColorDto? BackgroundColor,
    [property: JsonPropertyName("associative")]     bool Associative,
    [property: JsonPropertyName("annotative")]      bool Annotative);

internal sealed record HatchesDrawByBoundaryArgsDto(
    [property: JsonPropertyName("seedPoint")]       Point2dDto SeedPoint,
    [property: JsonPropertyName("pattern")]         string Pattern,
    [property: JsonPropertyName("scale")]           double Scale,
    [property: JsonPropertyName("angleDeg")]        double AngleDeg,
    [property: JsonPropertyName("layer")]           string? Layer,
    [property: JsonPropertyName("detectIslands")]   bool DetectIslands,
    [property: JsonPropertyName("color")]           ColorDto? Color);

internal sealed record HatchesApplyPresetArgsDto(
    [property: JsonPropertyName("boundaryHandles")] List<string> BoundaryHandles,
    [property: JsonPropertyName("material")]        string Material,
    [property: JsonPropertyName("layer")]           string? Layer,
    [property: JsonPropertyName("scaleMultiplier")] double ScaleMultiplier);

internal sealed record HatchesApplyPresetByPointArgsDto(
    [property: JsonPropertyName("seedPoint")]       Point2dDto SeedPoint,
    [property: JsonPropertyName("material")]        string Material,
    [property: JsonPropertyName("layer")]           string? Layer,
    [property: JsonPropertyName("scaleMultiplier")] double ScaleMultiplier,
    [property: JsonPropertyName("detectIslands")]   bool DetectIslands);

internal sealed record HatchesClipArgsDto(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("boundaryHandles")] List<string> BoundaryHandles);

internal sealed record HatchesRegenerateArgsDto(
    [property: JsonPropertyName("handles")]         List<string>? Handles,
    [property: JsonPropertyName("layers")]          List<string>? Layers,
    [property: JsonPropertyName("allInModelSpace")] bool AllInModelSpace);

internal sealed record HatchesListHatchesArgsDto(
    [property: JsonPropertyName("layerFilter")]     string? LayerFilter,
    [property: JsonPropertyName("patternFilter")]   string? PatternFilter);

internal sealed record HatchesListPatternsArgsDto(
    [property: JsonPropertyName("categoryFilter")]  string? CategoryFilter);
