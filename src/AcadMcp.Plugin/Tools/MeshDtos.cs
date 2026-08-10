// Plugin-side DTOs for the acad-mesh category.
// Mirrors src/AcadMcp.Backend/Categories/Mesh/MeshDtos.cs wire shape.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record MeshBoxArgsDto(
    [property: JsonPropertyName("corner1")]     Point3dDto? Corner1,
    [property: JsonPropertyName("corner2")]     Point3dDto? Corner2,
    [property: JsonPropertyName("smoothLevel")] int? SmoothLevel,
    [property: JsonPropertyName("layer")]       string? Layer);

internal sealed record MeshHandleArgsDto(
    [property: JsonPropertyName("handle")] string? Handle);

internal sealed record MeshSmoothArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("level")]  int? Level,
    [property: JsonPropertyName("by")]     int? By);

internal sealed record MeshConvertArgsDto(
    [property: JsonPropertyName("handle")]      string? Handle,
    [property: JsonPropertyName("smooth")]      bool? Smooth,
    [property: JsonPropertyName("optimize")]    bool? Optimize,
    [property: JsonPropertyName("eraseSource")] bool? EraseSource,
    [property: JsonPropertyName("layer")]       string? Layer);

// ─────────── roadmap 4.3, second tranche ───────────

internal sealed record MeshCreaseArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("level")]  double? Level);

internal sealed record MeshCylinderArgsDto(
    [property: JsonPropertyName("basePoint")]   Point3dDto? BasePoint,
    [property: JsonPropertyName("radius")]      double? Radius,
    [property: JsonPropertyName("height")]      double? Height,
    [property: JsonPropertyName("sides")]       int? Sides,
    [property: JsonPropertyName("smoothLevel")] int? SmoothLevel,
    [property: JsonPropertyName("layer")]       string? Layer);
