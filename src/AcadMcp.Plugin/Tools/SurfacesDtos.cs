// Plugin-side DTOs for the acad-surfaces category.
// Mirrors src/AcadMcp.Backend/Categories/Surfaces/SurfacesDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record SurfaceExtrudeArgsDto(
    [property: JsonPropertyName("handle")]        string? Handle,
    [property: JsonPropertyName("height")]        double? Height,
    [property: JsonPropertyName("direction")]     Point3dDto? Direction,
    [property: JsonPropertyName("taperAngleDeg")] double? TaperAngleDeg,
    [property: JsonPropertyName("layer")]         string? Layer);

internal sealed record SurfaceRevolveArgsDto(
    [property: JsonPropertyName("handle")]    string? Handle,
    [property: JsonPropertyName("axisStart")] Point3dDto? AxisStart,
    [property: JsonPropertyName("axisEnd")]   Point3dDto? AxisEnd,
    [property: JsonPropertyName("angleDeg")]  double? AngleDeg,
    [property: JsonPropertyName("layer")]     string? Layer);

internal sealed record SurfaceSweepArgsDto(
    [property: JsonPropertyName("profileHandle")] string? ProfileHandle,
    [property: JsonPropertyName("pathHandle")]    string? PathHandle,
    [property: JsonPropertyName("bank")]          bool? Bank,
    [property: JsonPropertyName("twistDeg")]      double? TwistDeg,
    [property: JsonPropertyName("layer")]         string? Layer);

internal sealed record SurfaceOffsetArgsDto(
    [property: JsonPropertyName("handle")]   string? Handle,
    [property: JsonPropertyName("distance")] double? Distance,
    [property: JsonPropertyName("layer")]    string? Layer);

internal sealed record ConvertArgsDto(
    [property: JsonPropertyName("handle")]      string? Handle,
    [property: JsonPropertyName("eraseSource")] bool? EraseSource,
    [property: JsonPropertyName("layer")]       string? Layer);
