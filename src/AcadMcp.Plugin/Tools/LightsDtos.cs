// Plugin-side DTOs for the acad-lights category.
// Mirrors src/AcadMcp.Backend/Categories/Lights/LightsDtos.cs wire shape.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record LightArgsDto(
    [property: JsonPropertyName("name")]         string? Name,
    [property: JsonPropertyName("position")]     Point3dDto? Position,
    [property: JsonPropertyName("target")]       Point3dDto? Target,
    [property: JsonPropertyName("direction")]    Point3dDto? Direction,
    [property: JsonPropertyName("intensity")]    double? Intensity,
    [property: JsonPropertyName("color")]        ColorDto? Color,
    [property: JsonPropertyName("on")]           bool? On,
    [property: JsonPropertyName("hotspotAngle")] double? HotspotAngle,
    [property: JsonPropertyName("falloffAngle")] double? FalloffAngle,
    [property: JsonPropertyName("layer")]        string? Layer);

internal sealed record SunArgsDto(
    [property: JsonPropertyName("on")]              bool? On,
    [property: JsonPropertyName("intensity")]       double? Intensity,
    [property: JsonPropertyName("dateTime")]        string? DateTime,
    [property: JsonPropertyName("skyIllumination")] bool? SkyIllumination,
    [property: JsonPropertyName("haze")]            double? Haze);
