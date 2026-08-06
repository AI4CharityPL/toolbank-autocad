// Plugin-side DTOs mirroring the wire shape sent by AcadMcp.Backend.Categories.Geometry2d.
// Kept here (not in Shared) because they are category-internal — Shared stays small.
// JsonPropertyName MUST match the Backend serializer (camelCase).

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record DrawLineArgsDto(
    [property: JsonPropertyName("start")] Point2dDto Start,
    [property: JsonPropertyName("end")]   Point2dDto End,
    [property: JsonPropertyName("layer")] string? Layer);

internal sealed record DrawPolylineArgsDto(
    [property: JsonPropertyName("vertices")]    List<Point2dDto> Vertices,
    [property: JsonPropertyName("closed")]      bool Closed,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("globalWidth")] double? GlobalWidth);

internal sealed record DrawCircleArgsDto(
    [property: JsonPropertyName("center")] Point2dDto Center,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("layer")]  string? Layer);

internal sealed record DrawArcArgsDto(
    [property: JsonPropertyName("center")]        Point2dDto Center,
    [property: JsonPropertyName("radius")]        double Radius,
    [property: JsonPropertyName("startAngleDeg")] double StartAngleDeg,
    [property: JsonPropertyName("endAngleDeg")]   double EndAngleDeg,
    [property: JsonPropertyName("layer")]         string? Layer);

internal sealed record DrawEllipseArgsDto(
    [property: JsonPropertyName("center")]    Point2dDto Center,
    [property: JsonPropertyName("majorAxis")] Point2dDto MajorAxis,
    [property: JsonPropertyName("ratio")]     double Ratio,
    [property: JsonPropertyName("layer")]     string? Layer);

internal sealed record DrawRectangleArgsDto(
    [property: JsonPropertyName("corner1")] Point2dDto Corner1,
    [property: JsonPropertyName("corner2")] Point2dDto Corner2,
    [property: JsonPropertyName("layer")]   string? Layer);

internal sealed record DrawPolygonArgsDto(
    [property: JsonPropertyName("center")]    Point2dDto Center,
    [property: JsonPropertyName("sides")]     int Sides,
    [property: JsonPropertyName("radius")]    double Radius,
    [property: JsonPropertyName("inscribed")] bool Inscribed,
    [property: JsonPropertyName("layer")]     string? Layer);

internal sealed record DrawSplineArgsDto(
    [property: JsonPropertyName("fitPoints")] List<Point2dDto> FitPoints,
    [property: JsonPropertyName("closed")]    bool Closed,
    [property: JsonPropertyName("layer")]     string? Layer);

internal sealed record DrawPointArgsDto(
    [property: JsonPropertyName("position")] Point2dDto Position,
    [property: JsonPropertyName("layer")]    string? Layer);

internal sealed record DrawDonutArgsDto(
    [property: JsonPropertyName("center")]        Point2dDto Center,
    [property: JsonPropertyName("innerDiameter")] double InnerDiameter,
    [property: JsonPropertyName("outerDiameter")] double OuterDiameter,
    [property: JsonPropertyName("layer")]         string? Layer);

internal sealed record DrawXLineArgsDto(
    [property: JsonPropertyName("basePoint")] Point2dDto BasePoint,
    [property: JsonPropertyName("direction")] Point2dDto Direction,
    [property: JsonPropertyName("layer")]     string? Layer);

internal sealed record DrawRayArgsDto(
    [property: JsonPropertyName("basePoint")] Point2dDto BasePoint,
    [property: JsonPropertyName("direction")] Point2dDto Direction,
    [property: JsonPropertyName("layer")]     string? Layer);

internal sealed record DrawTextArgsDto(
    [property: JsonPropertyName("position")]    Point2dDto Position,
    [property: JsonPropertyName("text")]        string Text,
    [property: JsonPropertyName("height")]      double Height,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("style")]       string? Style,
    [property: JsonPropertyName("layer")]       string? Layer);

internal sealed record DrawMTextArgsDto(
    [property: JsonPropertyName("insertionPoint")] Point2dDto InsertionPoint,
    [property: JsonPropertyName("width")]          double Width,
    [property: JsonPropertyName("text")]           string Text,
    [property: JsonPropertyName("height")]         double Height,
    [property: JsonPropertyName("style")]          string? Style,
    [property: JsonPropertyName("layer")]          string? Layer);

internal sealed record DrawHatchArgsDto(
    [property: JsonPropertyName("boundaryHandles")] List<string> BoundaryHandles,
    [property: JsonPropertyName("pattern")]         string Pattern,
    [property: JsonPropertyName("scale")]           double Scale,
    [property: JsonPropertyName("angleDeg")]        double AngleDeg,
    [property: JsonPropertyName("layer")]           string? Layer);

internal sealed record DrawRevcloudArgsDto(
    [property: JsonPropertyName("vertices")] List<Point2dDto> Vertices,
    [property: JsonPropertyName("arcMin")]   double ArcMin,
    [property: JsonPropertyName("arcMax")]   double ArcMax,
    [property: JsonPropertyName("layer")]    string? Layer);

internal sealed record HandleArgDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record HandlesArgDto(
    [property: JsonPropertyName("handles")] List<string> Handles);

internal sealed record WindowArgDto(
    [property: JsonPropertyName("corner1")]     Point2dDto Corner1,
    [property: JsonPropertyName("corner2")]     Point2dDto Corner2,
    [property: JsonPropertyName("crossing")]    bool Crossing,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter);

internal sealed record TwoPointsArgDto(
    [property: JsonPropertyName("a")] Point2dDto A,
    [property: JsonPropertyName("b")] Point2dDto B);

internal sealed record PointAndHandleArgDto(
    [property: JsonPropertyName("point")]  Point2dDto Point,
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record TwoHandlesArgDto(
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B);

internal sealed record OffsetArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("side")]     string Side);

internal sealed record TrimExtendArgsDto(
    [property: JsonPropertyName("handleToModify")]  string HandleToModify,
    [property: JsonPropertyName("boundaryHandles")] List<string> BoundaryHandles,
    [property: JsonPropertyName("pickPoint")]       Point2dDto? PickPoint);

internal sealed record FilletArgsDto(
    [property: JsonPropertyName("handleA")] string HandleA,
    [property: JsonPropertyName("handleB")] string HandleB,
    [property: JsonPropertyName("radius")]  double Radius);

internal sealed record ChamferArgsDto(
    [property: JsonPropertyName("handleA")] string HandleA,
    [property: JsonPropertyName("handleB")] string HandleB,
    [property: JsonPropertyName("distA")]   double DistA,
    [property: JsonPropertyName("distB")]   double DistB);

internal sealed record DrawMlineArgsDto(
    [property: JsonPropertyName("vertices")]      List<Point2dDto>? Vertices,
    [property: JsonPropertyName("style")]         string? Style,
    [property: JsonPropertyName("scale")]         double? Scale,
    [property: JsonPropertyName("justification")] string? Justification,
    [property: JsonPropertyName("closed")]        bool Closed,
    [property: JsonPropertyName("layer")]         string? Layer);

// ─────────── polyline vertex editing (roadmap 3.1) ───────────

internal sealed record PolylineRefArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("index")]  int? Index);

internal sealed record PolylineVertexArgsDto(
    [property: JsonPropertyName("handle")]     string? Handle,
    [property: JsonPropertyName("index")]      int? Index,
    [property: JsonPropertyName("point")]      Point2dDto? Point,
    [property: JsonPropertyName("bulge")]      double? Bulge,
    [property: JsonPropertyName("startWidth")] double? StartWidth,
    [property: JsonPropertyName("endWidth")]   double? EndWidth);

internal sealed record PolylineWidthArgsDto(
    [property: JsonPropertyName("handle")]  string? Handle,
    [property: JsonPropertyName("width")]   double? Width,
    [property: JsonPropertyName("segment")] int? Segment);

internal sealed record EntityRefArgsDto(
    [property: JsonPropertyName("handle")] string? Handle);

// ─────────── breaking and dividing (roadmap 3.1) ───────────

internal sealed record BreakAtPointArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("point")]  Point2dDto? Point);

internal sealed record BreakBetweenArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("point1")] Point2dDto? Point1,
    [property: JsonPropertyName("point2")] Point2dDto? Point2);

internal sealed record DivideArgsDto(
    [property: JsonPropertyName("handle")]       string? Handle,
    [property: JsonPropertyName("segments")]     int? Segments,
    [property: JsonPropertyName("block")]        string? Block,
    [property: JsonPropertyName("alignToCurve")] bool? AlignToCurve,
    [property: JsonPropertyName("layer")]        string? Layer);

internal sealed record MeasureArgsDto(
    [property: JsonPropertyName("handle")]       string? Handle,
    [property: JsonPropertyName("distance")]     double? Distance,
    [property: JsonPropertyName("block")]        string? Block,
    [property: JsonPropertyName("alignToCurve")] bool? AlignToCurve,
    [property: JsonPropertyName("layer")]        string? Layer);

internal sealed record PointStyleArgsDto(
    [property: JsonPropertyName("mode")]   string? Mode,
    [property: JsonPropertyName("pdmode")] int? Pdmode,
    [property: JsonPropertyName("size")]   double? Size);
