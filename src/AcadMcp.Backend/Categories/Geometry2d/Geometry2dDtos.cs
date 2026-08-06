// Typed DTOs for the acad-geometry-2d category.
// Mirrors plugin-side argument shapes 1-to-1. JsonPropertyName MUST match plugin readers.
// See rule 19-tool-implementation-pattern.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Geometry2d;

#region creation args

public sealed record DrawLineArgs(
    [property: JsonPropertyName("start")] Point2dDto Start,
    [property: JsonPropertyName("end")]   Point2dDto End,
    [property: JsonPropertyName("layer")] string? Layer = null);

public sealed record DrawPolylineArgs(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("closed")]   bool Closed = false,
    [property: JsonPropertyName("layer")]    string? Layer = null,
    [property: JsonPropertyName("globalWidth")] double? GlobalWidth = null);

public sealed record DrawCircleArgs(
    [property: JsonPropertyName("center")] Point2dDto Center,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("layer")]  string? Layer = null);

public sealed record DrawArcArgs(
    [property: JsonPropertyName("center")]      Point2dDto Center,
    [property: JsonPropertyName("radius")]      double Radius,
    [property: JsonPropertyName("startAngleDeg")] double StartAngleDeg,
    [property: JsonPropertyName("endAngleDeg")]   double EndAngleDeg,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record DrawEllipseArgs(
    [property: JsonPropertyName("center")]    Point2dDto Center,
    [property: JsonPropertyName("majorAxis")] Point2dDto MajorAxis,
    [property: JsonPropertyName("ratio")]     double Ratio,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawRectangleArgs(
    [property: JsonPropertyName("corner1")] Point2dDto Corner1,
    [property: JsonPropertyName("corner2")] Point2dDto Corner2,
    [property: JsonPropertyName("layer")]   string? Layer = null);

public sealed record DrawPolygonArgs(
    [property: JsonPropertyName("center")]    Point2dDto Center,
    [property: JsonPropertyName("sides")]     int Sides,
    [property: JsonPropertyName("radius")]    double Radius,
    [property: JsonPropertyName("inscribed")] bool Inscribed = true,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawSplineArgs(
    [property: JsonPropertyName("fitPoints")] IReadOnlyList<Point2dDto> FitPoints,
    [property: JsonPropertyName("closed")]    bool Closed = false,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawPointArgs(
    [property: JsonPropertyName("position")] Point2dDto Position,
    [property: JsonPropertyName("layer")]    string? Layer = null);

public sealed record DrawDonutArgs(
    [property: JsonPropertyName("center")]            Point2dDto Center,
    [property: JsonPropertyName("innerDiameter")]     double InnerDiameter,
    [property: JsonPropertyName("outerDiameter")]     double OuterDiameter,
    [property: JsonPropertyName("layer")]             string? Layer = null);

public sealed record DrawXLineArgs(
    [property: JsonPropertyName("basePoint")] Point2dDto BasePoint,
    [property: JsonPropertyName("direction")] Point2dDto Direction,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawRayArgs(
    [property: JsonPropertyName("basePoint")] Point2dDto BasePoint,
    [property: JsonPropertyName("direction")] Point2dDto Direction,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record DrawTextArgs(
    [property: JsonPropertyName("position")]    Point2dDto Position,
    [property: JsonPropertyName("text")]        string Text,
    [property: JsonPropertyName("height")]      double Height,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("style")]       string? Style = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record DrawMTextArgs(
    [property: JsonPropertyName("insertionPoint")] Point2dDto InsertionPoint,
    [property: JsonPropertyName("width")]          double Width,
    [property: JsonPropertyName("text")]           string Text,
    [property: JsonPropertyName("height")]         double Height,
    [property: JsonPropertyName("style")]          string? Style = null,
    [property: JsonPropertyName("layer")]          string? Layer = null);

public sealed record DrawHatchArgs(
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("pattern")]         string Pattern = "ANSI31",
    [property: JsonPropertyName("scale")]           double Scale = 1.0,
    [property: JsonPropertyName("angleDeg")]        double AngleDeg = 0.0,
    [property: JsonPropertyName("layer")]           string? Layer = null);

public sealed record DrawRevcloudArgs(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("arcMin")]   double ArcMin = 1.0,
    [property: JsonPropertyName("arcMax")]   double ArcMax = 2.0,
    [property: JsonPropertyName("layer")]    string? Layer = null);

#endregion

#region query args

public sealed record HandleArg(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record HandlesArg(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record WindowArg(
    [property: JsonPropertyName("corner1")] Point2dDto Corner1,
    [property: JsonPropertyName("corner2")] Point2dDto Corner2,
    [property: JsonPropertyName("crossing")] bool Crossing = false,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null);

public sealed record TwoPointsArg(
    [property: JsonPropertyName("a")] Point2dDto A,
    [property: JsonPropertyName("b")] Point2dDto B);

public sealed record PointAndHandleArg(
    [property: JsonPropertyName("point")]  Point2dDto Point,
    [property: JsonPropertyName("handle")] string Handle);

public sealed record TwoHandlesArg(
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B);

#endregion

#region modify args

public sealed record OffsetArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("side")]     string Side = "right");

public sealed record TrimExtendArgs(
    [property: JsonPropertyName("handleToModify")]  string HandleToModify,
    [property: JsonPropertyName("boundaryHandles")] IReadOnlyList<string> BoundaryHandles,
    [property: JsonPropertyName("pickPoint")]       Point2dDto? PickPoint = null);

public sealed record FilletArgs(
    [property: JsonPropertyName("handleA")] string HandleA,
    [property: JsonPropertyName("handleB")] string HandleB,
    [property: JsonPropertyName("radius")]  double Radius);

public sealed record ChamferArgs(
    [property: JsonPropertyName("handleA")] string HandleA,
    [property: JsonPropertyName("handleB")] string HandleB,
    [property: JsonPropertyName("distA")]   double DistA,
    [property: JsonPropertyName("distB")]   double DistB);

#endregion

#region results

public sealed record EntityResult(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

public sealed record EntitiesResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record ScalarResult(
    [property: JsonPropertyName("value")] double Value);

public sealed record PointsResult(
    [property: JsonPropertyName("points")] IReadOnlyList<Point2dDto> Points);

public sealed record EntityInfoResult(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("class")]     string ObjectClass,
    [property: JsonPropertyName("layer")]     string Layer,
    [property: JsonPropertyName("color")]     ColorDto? Color,
    [property: JsonPropertyName("linetype")]  string? Linetype,
    [property: JsonPropertyName("bbox")]      BoundingBoxDto? BoundingBox,
    [property: JsonPropertyName("length")]    double? Length,
    [property: JsonPropertyName("area")]      double? Area,
    [property: JsonPropertyName("isClosed")]  bool? IsClosed,
    [property: JsonPropertyName("startPoint")] Point2dDto? StartPoint,
    [property: JsonPropertyName("endPoint")]   Point2dDto? EndPoint);

public sealed record BoundingBoxResult(
    [property: JsonPropertyName("bbox")] BoundingBoxDto BoundingBox);

public sealed record OkResult(
    [property: JsonPropertyName("ok")] bool Ok);

#endregion

// draw_mline — pulled forward from roadmap 3.1 because acad-styles 2.3 defines MLINE styles and
// nothing in the bank could draw with one. A style no tool can apply is unusable by an agent and
// impossible to check by sight.
public sealed record DrawMlineArgs(
    [property: JsonPropertyName("vertices")]      IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("style")]         string? Style = null,
    [property: JsonPropertyName("scale")]         double Scale = 1.0,
    [property: JsonPropertyName("justification")] string Justification = "zero",
    [property: JsonPropertyName("closed")]        bool Closed = false,
    [property: JsonPropertyName("layer")]         string? Layer = null);

// ─────────── polyline vertex editing (roadmap 3.1) ───────────
//
// Every result carries `before`, and the edit tools carry the whole vertex as it was. A drawn
// polyline is a first draft; the alternative to editing one is deleting and redrawing it, which
// loses its handle, its layer and anything referencing it.

public sealed record PolylineRefArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("index")]  int? Index = null);

public sealed record PolylineVertexArgs(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("index")]      int? Index = null,
    [property: JsonPropertyName("point")]      Point2dDto? Point = null,
    [property: JsonPropertyName("bulge")]      double? Bulge = null,
    [property: JsonPropertyName("startWidth")] double? StartWidth = null,
    [property: JsonPropertyName("endWidth")]   double? EndWidth = null);

public sealed record PolylineWidthArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("width")]   double? Width = null,
    [property: JsonPropertyName("segment")] int? Segment = null);

public sealed record EntityRefArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record PolylineVertexInfo(
    [property: JsonPropertyName("index")]      int Index,
    [property: JsonPropertyName("point")]      IReadOnlyList<double> Point,
    [property: JsonPropertyName("bulge")]      double Bulge,
    [property: JsonPropertyName("startWidth")] double StartWidth,
    [property: JsonPropertyName("endWidth")]   double EndWidth);

public sealed record PolylineVertexListResult(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("vertices")]      IReadOnlyList<PolylineVertexInfo> Vertices,
    [property: JsonPropertyName("count")]         int Count,
    [property: JsonPropertyName("closed")]        bool Closed,
    [property: JsonPropertyName("length")]        double Length,
    // Nullable for the same reason as in PolylineWidthResult: the ConstantWidth getter
    // throws when segments differ, so "no single answer" is a real state a caller must see.
    [property: JsonPropertyName("constantWidth")] double? ConstantWidth,
    [property: JsonPropertyName("note")]          string Note);

public sealed record PolylineAddVertexResult(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("added")]  PolylineVertexInfo Added,
    [property: JsonPropertyName("count")]  int Count,
    [property: JsonPropertyName("before")] int Before,
    [property: JsonPropertyName("length")] double Length);

public sealed record PolylineRemoveVertexResult(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("removed")] PolylineVertexInfo Removed,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("before")]  int Before,
    [property: JsonPropertyName("length")]  double Length);

public sealed record PolylineEditVertexResult(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("before")] PolylineVertexInfo Before,
    [property: JsonPropertyName("vertex")] PolylineVertexInfo Vertex,
    [property: JsonPropertyName("count")]  int Count,
    [property: JsonPropertyName("length")] double Length);

// constantWidth is NULLABLE, and that is not laziness. AutoCAD's ConstantWidth getter THROWS
// eInvalidInput when a polyline's segments have different widths - it is unanswerable rather
// than unset. Declaring it non-nullable would have this DTO drop a legitimate null, which is
// KNOWN-GAPS C0 all over again.
public sealed record PolylineWidthResult(
    [property: JsonPropertyName("handle")]              string Handle,
    [property: JsonPropertyName("width")]               double Width,
    [property: JsonPropertyName("segment")]             int? Segment,
    [property: JsonPropertyName("scope")]               string Scope,
    [property: JsonPropertyName("beforeConstantWidth")] double? BeforeConstantWidth,
    [property: JsonPropertyName("before")]              IReadOnlyList<PolylineVertexInfo> Before,
    [property: JsonPropertyName("vertices")]            IReadOnlyList<PolylineVertexInfo> Vertices,
    [property: JsonPropertyName("constantWidth")]       double? ConstantWidth,
    [property: JsonPropertyName("note")]                string? Note = null);

public sealed record ReverseCurveResult(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("startBefore")] IReadOnlyList<double> StartBefore,
    [property: JsonPropertyName("start")]       IReadOnlyList<double> Start,
    [property: JsonPropertyName("end")]         IReadOnlyList<double> End,
    [property: JsonPropertyName("note")]        string Note);
