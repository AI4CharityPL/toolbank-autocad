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

// ─────────── breaking and dividing (roadmap 3.1) ───────────

public sealed record BreakAtPointArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("point")]  Point2dDto Point);

public sealed record BreakBetweenArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("point1")] Point2dDto Point1,
    [property: JsonPropertyName("point2")] Point2dDto Point2);

public sealed record DivideArgs(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("segments")]     int? Segments = null,
    [property: JsonPropertyName("block")]        string? Block = null,
    [property: JsonPropertyName("alignToCurve")] bool? AlignToCurve = null,
    [property: JsonPropertyName("layer")]        string? Layer = null);

public sealed record MeasureArgs(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("distance")]     double? Distance = null,
    [property: JsonPropertyName("block")]        string? Block = null,
    [property: JsonPropertyName("alignToCurve")] bool? AlignToCurve = null,
    [property: JsonPropertyName("layer")]        string? Layer = null);

public sealed record CurvePiece(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("length")] double Length);

public sealed record BreakAtPointResult(
    [property: JsonPropertyName("brokenAt")]            IReadOnlyList<double> BrokenAt,
    [property: JsonPropertyName("offsetFromRequested")] double OffsetFromRequested,
    [property: JsonPropertyName("lengthBefore")]        double LengthBefore,
    [property: JsonPropertyName("pieces")]              IReadOnlyList<CurvePiece> Pieces,
    [property: JsonPropertyName("count")]               int Count,
    [property: JsonPropertyName("note")]                string Note);

public sealed record BreakBetweenResult(
    [property: JsonPropertyName("from")]                IReadOnlyList<double> From,
    [property: JsonPropertyName("to")]                  IReadOnlyList<double> To,
    [property: JsonPropertyName("offsetFromRequested")] IReadOnlyList<double> OffsetFromRequested,
    [property: JsonPropertyName("lengthBefore")]        double LengthBefore,
    [property: JsonPropertyName("removedLength")]       double RemovedLength,
    [property: JsonPropertyName("pieces")]              IReadOnlyList<CurvePiece> Pieces,
    [property: JsonPropertyName("count")]               int Count,
    [property: JsonPropertyName("note")]                string Note);

public sealed record CurveMarker(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("point")]    IReadOnlyList<double> Point,
    [property: JsonPropertyName("distance")] double Distance);

public sealed record DivideResult(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("segments")]      int Segments,
    [property: JsonPropertyName("segmentLength")] double SegmentLength,
    [property: JsonPropertyName("curveLength")]   double CurveLength,
    [property: JsonPropertyName("markers")]       IReadOnlyList<CurveMarker> Markers,
    [property: JsonPropertyName("count")]         int Count,
    [property: JsonPropertyName("placed")]        string Placed,
    [property: JsonPropertyName("note")]          string Note);

public sealed record MeasureResult(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("distance")]    double Distance,
    [property: JsonPropertyName("curveLength")] double CurveLength,
    [property: JsonPropertyName("markers")]     IReadOnlyList<CurveMarker> Markers,
    [property: JsonPropertyName("count")]       int Count,
    [property: JsonPropertyName("remainder")]   double Remainder,
    [property: JsonPropertyName("placed")]      string Placed,
    [property: JsonPropertyName("note")]        string Note);

public sealed record PointStyleArgs(
    [property: JsonPropertyName("mode")]   string? Mode = null,
    [property: JsonPropertyName("pdmode")] int? Pdmode = null,
    [property: JsonPropertyName("size")]   double? Size = null);

public sealed record PointStyleResult(
    [property: JsonPropertyName("mode")]         string? Mode,
    [property: JsonPropertyName("pdmode")]       int Pdmode,
    [property: JsonPropertyName("beforePdmode")] int BeforePdmode,
    [property: JsonPropertyName("pdsize")]       double Pdsize,
    [property: JsonPropertyName("beforePdsize")] double BeforePdsize,
    [property: JsonPropertyName("note")]         string Note);

// ─────────── display order, transparency, wipeouts (roadmap 3.1) ───────────

public sealed record DrawOrderArgs(
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("position")]   string Position,
    [property: JsonPropertyName("relativeTo")] string? RelativeTo = null);

public sealed record TransparencyArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("percent")] double? Percent = null,
    [property: JsonPropertyName("mode")]    string? Mode = null);

public sealed record WipeoutArgs(
    [property: JsonPropertyName("vertices")]     IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("layer")]        string? Layer = null,
    [property: JsonPropertyName("bringToFront")] bool? BringToFront = null);

public sealed record WipeoutFrameArgs(
    [property: JsonPropertyName("mode")] string Mode);

public sealed record DrawOrderResult(
    [property: JsonPropertyName("affected")]   int Affected,
    [property: JsonPropertyName("position")]   string Position,
    [property: JsonPropertyName("relativeTo")] string? RelativeTo,
    [property: JsonPropertyName("note")]       string Note);

public sealed record TransparencyEntity(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("alpha")]  int Alpha);

public sealed record TransparencyResult(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("mode")]     string Mode,
    [property: JsonPropertyName("percent")]  double? Percent,
    [property: JsonPropertyName("entities")] IReadOnlyList<TransparencyEntity> Entities,
    [property: JsonPropertyName("note")]     string Note);

public sealed record WipeoutResult(
    [property: JsonPropertyName("entity")]         EntityHandle Entity,
    [property: JsonPropertyName("vertices")]       int Vertices,
    [property: JsonPropertyName("broughtToFront")] bool BroughtToFront,
    [property: JsonPropertyName("note")]           string Note);

public sealed record WipeoutFrameResult(
    [property: JsonPropertyName("mode")]         string Mode,
    [property: JsonPropertyName("wipeoutframe")] int Wipeoutframe,
    [property: JsonPropertyName("before")]       int Before,
    [property: JsonPropertyName("note")]         string Note);

// ─────────── splines (roadmap 3.1) ───────────

public sealed record SplineCvArgs(
    [property: JsonPropertyName("controlPoints")] IReadOnlyList<Point2dDto> ControlPoints,
    [property: JsonPropertyName("degree")]        int? Degree = null,
    [property: JsonPropertyName("closed")]        bool? Closed = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record SplineFitPointArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("index")]  int? Index = null,
    [property: JsonPropertyName("point")]  Point2dDto? Point = null);

public sealed record SplineConvertArgs(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("keepOriginal")] bool? KeepOriginal = null,
    [property: JsonPropertyName("layer")]        string? Layer = null);

public sealed record SplineCvResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("controlPoints")] int ControlPoints,
    [property: JsonPropertyName("degree")]        int Degree,
    [property: JsonPropertyName("closed")]        bool Closed,
    [property: JsonPropertyName("length")]        double Length,
    [property: JsonPropertyName("note")]          string Note);

public sealed record SplineFitPointResult(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("index")]        int Index,
    [property: JsonPropertyName("before")]       IReadOnlyList<double> Before,
    [property: JsonPropertyName("point")]        IReadOnlyList<double> Point,
    [property: JsonPropertyName("fitPoints")]    int FitPoints,
    [property: JsonPropertyName("lengthBefore")] double LengthBefore,
    [property: JsonPropertyName("length")]       double Length,
    [property: JsonPropertyName("note")]         string Note);

// `vertices` and `length` are NULLABLE: ToPolyline may hand back a Polyline2d rather than a
// lightweight Polyline, and a non-Curve has no length. Declaring them non-nullable would drop a
// legitimate null, which is KNOWN-GAPS C0.
public sealed record SplineToPolylineResult(
    [property: JsonPropertyName("entity")]         EntityHandle Entity,
    [property: JsonPropertyName("type")]           string Type,
    [property: JsonPropertyName("vertices")]       int? Vertices,
    [property: JsonPropertyName("lengthBefore")]   double LengthBefore,
    [property: JsonPropertyName("length")]         double? Length,
    [property: JsonPropertyName("originalKept")]   bool OriginalKept,
    [property: JsonPropertyName("originalHandle")] string? OriginalHandle,
    [property: JsonPropertyName("note")]           string Note);

// ─────────── lengthening and elliptical arcs (roadmap 3.1) ───────────

public sealed record LengthenArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("mode")]    string? Mode = null,
    [property: JsonPropertyName("value")]   double? Value = null,
    [property: JsonPropertyName("atStart")] bool? AtStart = null);

public sealed record EllipseArcArgs(
    [property: JsonPropertyName("center")]        Point2dDto Center,
    [property: JsonPropertyName("majorAxis")]     Point2dDto MajorAxis,
    [property: JsonPropertyName("ratio")]         double? Ratio = null,
    [property: JsonPropertyName("startAngleDeg")] double? StartAngleDeg = null,
    [property: JsonPropertyName("endAngleDeg")]   double? EndAngleDeg = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record LengthenResult(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("type")]         string Type,
    [property: JsonPropertyName("mode")]         string Mode,
    [property: JsonPropertyName("lengthBefore")] double LengthBefore,
    [property: JsonPropertyName("length")]       double Length,
    [property: JsonPropertyName("changedBy")]    double ChangedBy,
    [property: JsonPropertyName("atStart")]      bool AtStart,
    [property: JsonPropertyName("note")]         string Note);

public sealed record EllipseArcResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("startAngleDeg")] double StartAngleDeg,
    [property: JsonPropertyName("endAngleDeg")]   double EndAngleDeg,
    [property: JsonPropertyName("ratio")]         double Ratio,
    [property: JsonPropertyName("majorLength")]   double MajorLength,
    [property: JsonPropertyName("length")]        double Length,
    [property: JsonPropertyName("closed")]        bool Closed,
    [property: JsonPropertyName("note")]          string Note);

// ─────────── boundaries from a point (roadmap 3.1) ───────────

public sealed record BoundaryArgs(
    [property: JsonPropertyName("point")]         Point2dDto Point,
    [property: JsonPropertyName("detectIslands")] bool? DetectIslands = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

// `length` and `closed` are nullable because a trace can return something that is not a Curve.
public sealed record BoundaryEntity(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("type")]   string Type,
    [property: JsonPropertyName("length")] double? Length,
    [property: JsonPropertyName("closed")] bool? Closed);

public sealed record BoundaryResult(
    [property: JsonPropertyName("seed")]          IReadOnlyList<double> Seed,
    [property: JsonPropertyName("boundaries")]    IReadOnlyList<BoundaryEntity> Boundaries,
    [property: JsonPropertyName("count")]         int Count,
    [property: JsonPropertyName("detectIslands")] bool DetectIslands,
    [property: JsonPropertyName("note")]          string Note);

public sealed record RegionEntity(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("area")]      double Area,
    [property: JsonPropertyName("perimeter")] double Perimeter);

public sealed record RegionFromBoundaryResult(
    [property: JsonPropertyName("seed")]    IReadOnlyList<double> Seed,
    [property: JsonPropertyName("regions")] IReadOnlyList<RegionEntity> Regions,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("note")]    string Note);

// ─────────── blending two curves (roadmap 3.1) ───────────

public sealed record BlendArgs(
    [property: JsonPropertyName("handle1")]    string Handle1,
    [property: JsonPropertyName("handle2")]    string Handle2,
    [property: JsonPropertyName("continuity")] string? Continuity = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

/// <summary>Which ends were joined. Reported because nothing was picked.</summary>
public sealed record BlendJoin(
    [property: JsonPropertyName("handle1")] string Handle1,
    [property: JsonPropertyName("end1")]    string End1,
    [property: JsonPropertyName("point1")]  IReadOnlyList<double> Point1,
    [property: JsonPropertyName("handle2")] string Handle2,
    [property: JsonPropertyName("end2")]    string End2,
    [property: JsonPropertyName("point2")]  IReadOnlyList<double> Point2);

public sealed record BlendResult(
    [property: JsonPropertyName("entity")]     EntityHandle Entity,
    [property: JsonPropertyName("continuity")] string Continuity,
    [property: JsonPropertyName("gap")]        double Gap,
    [property: JsonPropertyName("length")]     double Length,
    [property: JsonPropertyName("joinedAt")]   BlendJoin JoinedAt,
    [property: JsonPropertyName("note")]       string Note);

// ─────────── multiline editing (roadmap 3.1) ───────────

public sealed record MlineVertexArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("index")]  int? Index = null,
    [property: JsonPropertyName("point")]  Point2dDto? Point = null);

public sealed record MlineJoinArgs(
    [property: JsonPropertyName("handle1")]   string Handle1,
    [property: JsonPropertyName("handle2")]   string Handle2,
    [property: JsonPropertyName("tolerance")] double? Tolerance = null);

public sealed record MlineVertexResult(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("index")]    int Index,
    [property: JsonPropertyName("before")]   IReadOnlyList<double> Before,
    [property: JsonPropertyName("point")]    IReadOnlyList<double> Point,
    [property: JsonPropertyName("vertices")] int Vertices,
    [property: JsonPropertyName("note")]     string Note);

public sealed record MlineJoinResult(
    [property: JsonPropertyName("handle")]         string Handle,
    [property: JsonPropertyName("erased")]         string Erased,
    [property: JsonPropertyName("direction")]      string Direction,
    [property: JsonPropertyName("verticesBefore")] IReadOnlyList<int> VerticesBefore,
    [property: JsonPropertyName("vertices")]       int Vertices,
    [property: JsonPropertyName("joinedAt")]       IReadOnlyList<double> JoinedAt,
    [property: JsonPropertyName("note")]           string Note);
