// Typed DTOs for the acad-surfaces category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Surfaces;

public sealed record SurfaceExtrudeArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("height")]        double? Height = null,
    [property: JsonPropertyName("direction")]     Point3dDto? Direction = null,
    [property: JsonPropertyName("taperAngleDeg")] double? TaperAngleDeg = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record SurfaceRevolveArgs(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("axisStart")] Point3dDto? AxisStart = null,
    [property: JsonPropertyName("axisEnd")]   Point3dDto? AxisEnd = null,
    [property: JsonPropertyName("angleDeg")]  double? AngleDeg = null,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record SurfaceSweepArgs(
    [property: JsonPropertyName("profileHandle")] string ProfileHandle,
    [property: JsonPropertyName("pathHandle")]    string PathHandle,
    [property: JsonPropertyName("bank")]          bool? Bank = null,
    [property: JsonPropertyName("twistDeg")]      double? TwistDeg = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record SurfaceOffsetArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("distance")] double? Distance = null,
    [property: JsonPropertyName("layer")]    string? Layer = null);

public sealed record SurfaceConvertArgs(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("eraseSource")] bool? EraseSource = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record SurfaceResult(
    [property: JsonPropertyName("entity")]            EntityHandle Entity,
    [property: JsonPropertyName("area")]              double Area,
    [property: JsonPropertyName("type")]              string Type,
    [property: JsonPropertyName("curveLength")]       double? CurveLength,
    [property: JsonPropertyName("height")]            double? Height,
    [property: JsonPropertyName("lengthTimesHeight")] double? LengthTimesHeight,
    [property: JsonPropertyName("taperAngleDeg")]     double? TaperAngleDeg,
    [property: JsonPropertyName("angleDeg")]          double? AngleDeg,
    [property: JsonPropertyName("radiusToMidpoint")]  double? RadiusToMidpoint,
    [property: JsonPropertyName("pappusArea")]        double? PappusArea,
    [property: JsonPropertyName("profileLength")]     double? ProfileLength,
    [property: JsonPropertyName("pathLength")]        double? PathLength,
    [property: JsonPropertyName("profileTimesPath")]  double? ProfileTimesPath,
    [property: JsonPropertyName("distance")]          double? Distance,
    [property: JsonPropertyName("sourceArea")]        double? SourceArea,
    [property: JsonPropertyName("note")]              string Note);

public sealed record ToSurfaceResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("area")]          double Area,
    [property: JsonPropertyName("wasType")]       string WasType,
    [property: JsonPropertyName("type")]          string Type,
    [property: JsonPropertyName("sourceVolume")]  double? SourceVolume,
    [property: JsonPropertyName("sourceErased")]  bool SourceErased,
    [property: JsonPropertyName("note")]          string Note);

public sealed record ToSolidResult(
    [property: JsonPropertyName("entity")]       EntityHandle Entity,
    [property: JsonPropertyName("volume")]       double Volume,
    [property: JsonPropertyName("wasType")]      string WasType,
    [property: JsonPropertyName("sourceArea")]   double? SourceArea,
    [property: JsonPropertyName("sourceErased")] bool SourceErased,
    [property: JsonPropertyName("note")]         string Note);

public sealed record SurfaceInfoResult(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("type")]     string Type,
    [property: JsonPropertyName("area")]     double Area,
    [property: JsonPropertyName("faces")]    int Faces,
    [property: JsonPropertyName("edges")]    int Edges,
    [property: JsonPropertyName("isPlanar")] bool IsPlanar,
    [property: JsonPropertyName("bbox")]     BoundingBoxDto Bbox,
    [property: JsonPropertyName("note")]     string Note);
