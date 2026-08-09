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

// ─────────── roadmap 4.1, first tranche: sweep, loft, helix ───────────

internal sealed record SweepArgsDto(
    [property: JsonPropertyName("profileHandle")] string? ProfileHandle,
    [property: JsonPropertyName("pathHandle")]    string? PathHandle,
    [property: JsonPropertyName("align")]         string? Align,
    [property: JsonPropertyName("bank")]          bool? Bank,
    [property: JsonPropertyName("twistDeg")]      double? TwistDeg,
    [property: JsonPropertyName("scale")]         double? Scale,
    [property: JsonPropertyName("eraseSources")]  bool? EraseSources,
    [property: JsonPropertyName("layer")]         string? Layer);

internal sealed record LoftArgsDto(
    [property: JsonPropertyName("profileHandles")] List<string>? ProfileHandles,
    [property: JsonPropertyName("guideHandles")]   List<string>? GuideHandles,
    [property: JsonPropertyName("pathHandle")]     string? PathHandle,
    [property: JsonPropertyName("closed")]         bool? Closed,
    [property: JsonPropertyName("ruled")]          bool? Ruled,
    [property: JsonPropertyName("eraseSources")]   bool? EraseSources,
    [property: JsonPropertyName("layer")]          string? Layer);

internal sealed record HelixArgsDto(
    [property: JsonPropertyName("center")]     Point3dDto? Center,
    [property: JsonPropertyName("baseRadius")] double? BaseRadius,
    [property: JsonPropertyName("topRadius")]  double? TopRadius,
    [property: JsonPropertyName("height")]     double? Height,
    [property: JsonPropertyName("turns")]      double? Turns,
    [property: JsonPropertyName("clockwise")]  bool? Clockwise,
    [property: JsonPropertyName("layer")]      string? Layer);

// ─────────── roadmap 4.1, second tranche: slicing and interference ───────────

internal sealed record SliceSolidArgsDto(
    [property: JsonPropertyName("handle")]      string? Handle,
    [property: JsonPropertyName("planePoint")]  Point3dDto? PlanePoint,
    [property: JsonPropertyName("planeNormal")] Point3dDto? PlaneNormal,
    [property: JsonPropertyName("keepBoth")]    bool? KeepBoth,
    [property: JsonPropertyName("layer")]       string? Layer);

internal sealed record InterfereArgsDto(
    [property: JsonPropertyName("handle1")]       string? Handle1,
    [property: JsonPropertyName("handle2")]       string? Handle2,
    [property: JsonPropertyName("createSolid")]   bool? CreateSolid,
    [property: JsonPropertyName("layer")]         string? Layer);

internal sealed record ImprintArgsDto(
    [property: JsonPropertyName("solidHandle")]  string? SolidHandle,
    [property: JsonPropertyName("curveHandle")]  string? CurveHandle,
    [property: JsonPropertyName("eraseSource")]  bool? EraseSource);

// ─────────── roadmap 4.1: the face/edge family ───────────

internal sealed record SolidQueryArgsDto(
    [property: JsonPropertyName("handle")] string? Handle);

/// <summary>Shared by fillet_edge and chamfer_edge: which edges, and by how much.</summary>
internal sealed record EdgeOpArgsDto(
    [property: JsonPropertyName("handle")]        string? Handle,
    [property: JsonPropertyName("edgeIndexes")]   List<int>? EdgeIndexes,
    [property: JsonPropertyName("nearPoints")]    List<Point3dDto>? NearPoints,
    [property: JsonPropertyName("radius")]        double? Radius,
    [property: JsonPropertyName("distance")]      double? Distance,
    [property: JsonPropertyName("distance2")]     double? Distance2,
    [property: JsonPropertyName("baseFaceIndex")] int? BaseFaceIndex,
    [property: JsonPropertyName("allowFaceLoss")] bool? AllowFaceLoss);

/// <summary>Shared by every SOLIDEDIT face operation: which faces, and what to do to them.</summary>
internal sealed record FaceOpArgsDto(
    [property: JsonPropertyName("handle")]        string? Handle,
    [property: JsonPropertyName("faceIndexes")]   List<int>? FaceIndexes,
    [property: JsonPropertyName("nearPoints")]    List<Point3dDto>? NearPoints,
    [property: JsonPropertyName("facing")]        Point3dDto? Facing,
    [property: JsonPropertyName("distance")]      double? Distance,
    [property: JsonPropertyName("taperAngleDeg")] double? TaperAngleDeg,
    [property: JsonPropertyName("pathHandle")]    string? PathHandle,
    [property: JsonPropertyName("from")]          Point3dDto? From,
    [property: JsonPropertyName("to")]            Point3dDto? To,
    [property: JsonPropertyName("axisStart")]     Point3dDto? AxisStart,
    [property: JsonPropertyName("axisEnd")]       Point3dDto? AxisEnd,
    [property: JsonPropertyName("angleDeg")]      double? AngleDeg,
    [property: JsonPropertyName("basePoint")]     Point3dDto? BasePoint,
    [property: JsonPropertyName("direction")]     Point3dDto? Direction,
    [property: JsonPropertyName("thickness")]     double? Thickness);
