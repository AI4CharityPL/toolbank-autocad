// Plugin-side DTOs for the acad-geometry-3d category.
// Mirrors src/AcadMcp.Backend/Categories/Geometry3d/Geometry3dDtos.cs wire shape.
// Kept local to AcadMcp.Plugin to avoid circular dependencies on AcadMcp.Backend.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record DrawBoxArgsDto(
    [property: JsonPropertyName("corner1")] Point3dDto Corner1,
    [property: JsonPropertyName("corner2")] Point3dDto Corner2,
    [property: JsonPropertyName("layer")]   string? Layer = null);

internal sealed record DrawSphereArgsDto(
    [property: JsonPropertyName("center")] Point3dDto Center,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("layer")]  string? Layer = null);

internal sealed record DrawCylinderArgsDto(
    [property: JsonPropertyName("basePoint")] Point3dDto BasePoint,
    [property: JsonPropertyName("radius")]    double Radius,
    [property: JsonPropertyName("height")]    double Height,
    [property: JsonPropertyName("layer")]     string? Layer = null);

internal sealed record DrawConeArgsDto(
    [property: JsonPropertyName("basePoint")] Point3dDto BasePoint,
    [property: JsonPropertyName("radius")]    double Radius,
    [property: JsonPropertyName("height")]    double Height,
    [property: JsonPropertyName("topRadius")] double TopRadius = 0.0,
    [property: JsonPropertyName("layer")]     string? Layer = null);

internal sealed record DrawTorusArgsDto(
    [property: JsonPropertyName("center")]      Point3dDto Center,
    [property: JsonPropertyName("majorRadius")] double MajorRadius,
    [property: JsonPropertyName("minorRadius")] double MinorRadius,
    [property: JsonPropertyName("layer")]       string? Layer = null);

internal sealed record DrawPyramidArgsDto(
    [property: JsonPropertyName("basePoint")]  Point3dDto BasePoint,
    [property: JsonPropertyName("sides")]      int Sides,
    [property: JsonPropertyName("baseRadius")] double BaseRadius,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("topRadius")]  double TopRadius = 0.0,
    [property: JsonPropertyName("layer")]      string? Layer = null);

internal sealed record DrawWedgeArgsDto(
    [property: JsonPropertyName("corner1")] Point3dDto Corner1,
    [property: JsonPropertyName("corner2")] Point3dDto Corner2,
    [property: JsonPropertyName("layer")]   string? Layer = null);

internal sealed record ExtrudeCurveArgsDto(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("height")]        double Height,
    [property: JsonPropertyName("taperAngleDeg")] double TaperAngleDeg = 0.0,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record RevolveCurveArgsDto(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("axisStart")] Point3dDto AxisStart,
    [property: JsonPropertyName("axisEnd")]   Point3dDto AxisEnd,
    [property: JsonPropertyName("angleDeg")]  double AngleDeg = 360.0,
    [property: JsonPropertyName("layer")]     string? Layer = null);

internal sealed record PlanarSurfaceArgsDto(
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("layer")]           string? Layer = null);

internal sealed record HandleArg3Dto(
    [property: JsonPropertyName("handle")] string Handle);
