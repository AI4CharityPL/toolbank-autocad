// Typed DTOs for the acad-selection category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Selection;

public sealed record EmptyArgs();

public sealed record ByLayerArgs(
    [property: JsonPropertyName("layer")]   string Layer,
    [property: JsonPropertyName("frozen")]  bool? Frozen = null);

public sealed record ByColorArgs(
    [property: JsonPropertyName("color")]    ColorDto Color,
    [property: JsonPropertyName("matchAci")] bool MatchAci = true);

public sealed record ByTypeArgs(
    [property: JsonPropertyName("dxfType")] string DxfType);

public sealed record ByHandleArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record WindowArgs(
    [property: JsonPropertyName("min")]      Point3dDto Min,
    [property: JsonPropertyName("max")]      Point3dDto Max,
    [property: JsonPropertyName("crossing")] bool Crossing = false);

public sealed record FenceArgs(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point3dDto> Vertices);

public sealed record PolygonArgs(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point3dDto> Vertices,
    [property: JsonPropertyName("crossing")] bool Crossing = false);

public sealed record SaveSetArgs(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record LoadSetArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record FilterEntities(
    [property: JsonPropertyName("layer")]   string? Layer = null,
    [property: JsonPropertyName("dxfType")] string? DxfType = null,
    [property: JsonPropertyName("color")]   ColorDto? Color = null,
    [property: JsonPropertyName("handles")] IReadOnlyList<string>? Handles = null);

public sealed record SelectionResult(
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record EntitiesListResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record SetMembersResult(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record CountResult(
    [property: JsonPropertyName("count")] int Count);
