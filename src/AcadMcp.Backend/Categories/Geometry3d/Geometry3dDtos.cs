// Typed DTOs for the acad-geometry-3d category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Geometry3d;

public sealed record DrawBoxArgs(
    [property: JsonPropertyName("corner1")] Point3dDto Corner1,
    [property: JsonPropertyName("corner2")] Point3dDto Corner2,
    [property: JsonPropertyName("layer")]   string? Layer = null);

public sealed record DrawSphereArgs(
    [property: JsonPropertyName("center")] Point3dDto Center,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("layer")]  string? Layer = null);

public sealed record DrawCylinderArgs(
    [property: JsonPropertyName("basePoint")] Point3dDto BasePoint,
    [property: JsonPropertyName("radius")]    double Radius,
    [property: JsonPropertyName("height")]    double Height,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawConeArgs(
    [property: JsonPropertyName("basePoint")] Point3dDto BasePoint,
    [property: JsonPropertyName("radius")]    double Radius,
    [property: JsonPropertyName("height")]    double Height,
    [property: JsonPropertyName("topRadius")] double TopRadius = 0.0,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawTorusArgs(
    [property: JsonPropertyName("center")]      Point3dDto Center,
    [property: JsonPropertyName("majorRadius")] double MajorRadius,
    [property: JsonPropertyName("minorRadius")] double MinorRadius,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record DrawPyramidArgs(
    [property: JsonPropertyName("basePoint")]  Point3dDto BasePoint,
    [property: JsonPropertyName("sides")]      int Sides,
    [property: JsonPropertyName("baseRadius")] double BaseRadius,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("topRadius")]  double TopRadius = 0.0,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record DrawWedgeArgs(
    [property: JsonPropertyName("corner1")] Point3dDto Corner1,
    [property: JsonPropertyName("corner2")] Point3dDto Corner2,
    [property: JsonPropertyName("layer")]   string? Layer = null);

public sealed record ExtrudeCurveArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("height")]        double Height,
    [property: JsonPropertyName("taperAngleDeg")] double TaperAngleDeg = 0.0,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record RevolveCurveArgs(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("axisStart")] Point3dDto AxisStart,
    [property: JsonPropertyName("axisEnd")]   Point3dDto AxisEnd,
    [property: JsonPropertyName("angleDeg")]  double AngleDeg = 360.0,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record PlanarSurfaceArgs(
    [property: JsonPropertyName("boundaryHandles")] System.Collections.Generic.IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("layer")]           string? Layer = null);

public sealed record HandleArg3(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record VolumeResult(
    [property: JsonPropertyName("volume")] double Volume);

public sealed record AreaResult3(
    [property: JsonPropertyName("area")] double Area);

public sealed record CentroidResult(
    [property: JsonPropertyName("centroid")] Point3dDto Centroid);

public sealed record BoundingBox3Result(
    [property: JsonPropertyName("bbox")] BoundingBoxDto BoundingBox);

public sealed record MassPropertiesResult(
    [property: JsonPropertyName("volume")]       double Volume,
    [property: JsonPropertyName("surfaceArea")]  double SurfaceArea,
    [property: JsonPropertyName("centroid")]     Point3dDto Centroid,
    [property: JsonPropertyName("momentsOfInertia")] double[]? MomentsOfInertia,
    [property: JsonPropertyName("radiiOfGyration")]  double[]? RadiiOfGyration);

public sealed record EntityResult3(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

// ─────────── roadmap 4.1, first tranche: sweep, loft, helix ───────────

public sealed record SweepArgs(
    [property: JsonPropertyName("profileHandle")] string ProfileHandle,
    [property: JsonPropertyName("pathHandle")]    string PathHandle,
    [property: JsonPropertyName("align")]         string? Align = null,
    [property: JsonPropertyName("bank")]          bool? Bank = null,
    [property: JsonPropertyName("twistDeg")]      double? TwistDeg = null,
    [property: JsonPropertyName("scale")]         double? Scale = null,
    [property: JsonPropertyName("eraseSources")]  bool? EraseSources = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record SweepResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("volume")]        double Volume,
    [property: JsonPropertyName("profileArea")]   double ProfileArea,
    [property: JsonPropertyName("pathLength")]    double PathLength,
    [property: JsonPropertyName("areaTimesLength")] double AreaTimesLength,
    [property: JsonPropertyName("ratioToAreaTimesLength")] double? RatioToAreaTimesLength,
    [property: JsonPropertyName("sourcesErased")] bool SourcesErased,
    [property: JsonPropertyName("note")]          string Note);

public sealed record LoftArgs(
    [property: JsonPropertyName("profileHandles")] IReadOnlyList<string> ProfileHandles,
    [property: JsonPropertyName("guideHandles")]   IReadOnlyList<string>? GuideHandles = null,
    [property: JsonPropertyName("pathHandle")]     string? PathHandle = null,
    [property: JsonPropertyName("closed")]         bool? Closed = null,
    [property: JsonPropertyName("ruled")]          bool? Ruled = null,
    [property: JsonPropertyName("eraseSources")]   bool? EraseSources = null,
    [property: JsonPropertyName("layer")]          string? Layer = null);

public sealed record LoftResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("volume")]        double Volume,
    [property: JsonPropertyName("crossSections")] int CrossSections,
    [property: JsonPropertyName("guides")]        int Guides,
    [property: JsonPropertyName("hasPath")]       bool HasPath,
    [property: JsonPropertyName("sectionAreas")]  IReadOnlyList<double> SectionAreas,
    [property: JsonPropertyName("closed")]        bool Closed,
    [property: JsonPropertyName("ruled")]         bool Ruled,
    [property: JsonPropertyName("sourcesErased")] bool SourcesErased,
    [property: JsonPropertyName("note")]          string Note);

public sealed record HelixArgs(
    [property: JsonPropertyName("center")]     Point3dDto? Center = null,
    [property: JsonPropertyName("baseRadius")] double? BaseRadius = null,
    [property: JsonPropertyName("topRadius")]  double? TopRadius = null,
    [property: JsonPropertyName("height")]     double? Height = null,
    [property: JsonPropertyName("turns")]      double? Turns = null,
    [property: JsonPropertyName("clockwise")]  bool? Clockwise = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record HelixResult(
    [property: JsonPropertyName("entity")]         EntityHandle Entity,
    [property: JsonPropertyName("baseRadius")]     double BaseRadius,
    [property: JsonPropertyName("topRadius")]      double TopRadius,
    [property: JsonPropertyName("height")]         double Height,
    [property: JsonPropertyName("turns")]          double Turns,
    [property: JsonPropertyName("turnHeight")]     double TurnHeight,
    [property: JsonPropertyName("clockwise")]      bool Clockwise,
    [property: JsonPropertyName("length")]         double Length,
    [property: JsonPropertyName("expectedLength")] double? ExpectedLength,
    [property: JsonPropertyName("note")]           string Note);
