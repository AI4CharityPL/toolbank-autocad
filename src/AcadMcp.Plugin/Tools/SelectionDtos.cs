// Plugin-side DTOs for the acad-selection category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record EmptyArgsDto();

internal sealed record ByLayerArgsDto(
    [property: JsonPropertyName("layer")]   string Layer,
    [property: JsonPropertyName("frozen")]  bool? Frozen = null);

internal sealed record ByColorArgsDto(
    [property: JsonPropertyName("color")]    ColorDto Color,
    [property: JsonPropertyName("matchAci")] bool MatchAci = true);

internal sealed record ByTypeArgsDto(
    [property: JsonPropertyName("dxfType")] string DxfType);

internal sealed record ByHandleArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

internal sealed record WindowArgsDto(
    [property: JsonPropertyName("min")]      Point3dDto Min,
    [property: JsonPropertyName("max")]      Point3dDto Max,
    [property: JsonPropertyName("crossing")] bool Crossing = false);

internal sealed record FenceArgsDto(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point3dDto> Vertices);

internal sealed record PolygonArgsDto(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point3dDto> Vertices,
    [property: JsonPropertyName("crossing")] bool Crossing = false);

internal sealed record SaveSetArgsDto(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

internal sealed record LoadSetArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record FilterEntitiesDto(
    [property: JsonPropertyName("layer")]   string? Layer = null,
    [property: JsonPropertyName("dxfType")] string? DxfType = null,
    [property: JsonPropertyName("color")]   ColorDto? Color = null,
    [property: JsonPropertyName("handles")] IReadOnlyList<string>? Handles = null);
