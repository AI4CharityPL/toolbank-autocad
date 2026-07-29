// Plugin-side DTOs for the acad-boolean-ops category.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record SolidBooleanArgsDto(
    [property: JsonPropertyName("targetHandle")] string TargetHandle,
    [property: JsonPropertyName("toolHandles")]  IReadOnlyList<string> ToolHandles,
    [property: JsonPropertyName("eraseTools")]   bool EraseTools = true);

internal sealed record RegionBooleanArgsDto(
    [property: JsonPropertyName("targetHandle")] string TargetHandle,
    [property: JsonPropertyName("toolHandles")]  IReadOnlyList<string> ToolHandles,
    [property: JsonPropertyName("eraseTools")]   bool EraseTools = true);

internal sealed record CreateRegionArgsDto(
    [property: JsonPropertyName("curveHandles")] IReadOnlyList<string> CurveHandles,
    [property: JsonPropertyName("eraseSource")]  bool EraseSource = false,
    [property: JsonPropertyName("layer")]        string? Layer = null);

internal sealed record CheckIntersectArgsDto(
    [property: JsonPropertyName("handleA")] string HandleA,
    [property: JsonPropertyName("handleB")] string HandleB);
