// Plugin-side DTOs for the acad-selection category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record EmptyArgsDto();

internal sealed record ByLayerArgsDto(
    [property: JsonPropertyName("layer")]     string Layer,
    [property: JsonPropertyName("frozen")]    bool? Frozen = null,
    // Default false: model-space only, unchanged from before this field existed. true also scans
    // every paper-space layout's own block (rule 74 C.4 - acad-selection was, like AcadEnv.Persist
    // before its own fix, hardcoded to *Model_Space; a title block or schedule table correctly
    // routed into paperspace via layoutName was invisible to select_by_layer, which made
    // verify_construction_readiness.py report a false FAIL for content confirmed present by
    // direct get_entity/bbox inspection).
    [property: JsonPropertyName("anySpace")]  bool AnySpace = false);

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
