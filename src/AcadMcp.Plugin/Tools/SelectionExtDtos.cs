// Plugin-side DTOs for the phase 3.4 selection extensions.
// Mirrors src/AcadMcp.Backend/Categories/Selection/SelectionExtDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record SelExtArgsDto(
    [property: JsonPropertyName("handle")]        string? Handle,
    [property: JsonPropertyName("handles")]       List<string>? Handles,
    [property: JsonPropertyName("name")]          string? Name,
    [property: JsonPropertyName("layer")]         string? Layer,
    [property: JsonPropertyName("objectClass")]   string? ObjectClass,
    [property: JsonPropertyName("colorIndex")]    int? ColorIndex,
    [property: JsonPropertyName("min")]           double? Min,
    [property: JsonPropertyName("max")]           double? Max,
    [property: JsonPropertyName("rangeKind")]     string? RangeKind,
    [property: JsonPropertyName("tolerance")]     double? Tolerance,
    [property: JsonPropertyName("count")]         int? Count,
    [property: JsonPropertyName("matchLayer")]    bool? MatchLayer,
    [property: JsonPropertyName("matchColor")]    bool? MatchColor,
    [property: JsonPropertyName("matchLinetype")] bool? MatchLinetype);
