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

// ─────────── roadmap 4.2, second tranche ───────────

public sealed record SurfaceBlendArgs(
    [property: JsonPropertyName("handle1")] string Handle1,
    [property: JsonPropertyName("handle2")] string Handle2,
    [property: JsonPropertyName("layer")]   string? Layer = null);

public sealed record SurfaceProjectArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("surfaceHandle")] string SurfaceHandle,
    [property: JsonPropertyName("direction")]     Point3dDto? Direction = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record NurbsEditArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("u")]      int? U = null,
    [property: JsonPropertyName("v")]      int? V = null,
    [property: JsonPropertyName("to")]     Point3dDto? To = null,
    [property: JsonPropertyName("by")]     Point3dDto? By = null);

public sealed record BlendResult(
    [property: JsonPropertyName("entity")]  EntityHandle Entity,
    [property: JsonPropertyName("area")]    double Area,
    [property: JsonPropertyName("type")]    string Type,
    [property: JsonPropertyName("length1")] double Length1,
    [property: JsonPropertyName("length2")] double Length2,
    [property: JsonPropertyName("note")]    string Note);

public sealed record ProjectResult(
    [property: JsonPropertyName("entities")]         IReadOnlyList<EntityHandle> Entities,
    [property: JsonPropertyName("count")]            int Count,
    [property: JsonPropertyName("projectedLength")]  double ProjectedLength,
    [property: JsonPropertyName("sourceLength")]     double? SourceLength,
    [property: JsonPropertyName("note")]             string Note);

public sealed record ToNurbsResult(
    [property: JsonPropertyName("entities")]     IReadOnlyList<EntityHandle> Entities,
    [property: JsonPropertyName("count")]        int Count,
    [property: JsonPropertyName("wasType")]      string WasType,
    [property: JsonPropertyName("area")]         double Area,
    [property: JsonPropertyName("areaBefore")]   double AreaBefore,
    [property: JsonPropertyName("sourceErased")] bool SourceErased,
    [property: JsonPropertyName("note")]         string Note);

public sealed record NurbsControlPoint(
    [property: JsonPropertyName("u")]     int U,
    [property: JsonPropertyName("v")]     int V,
    [property: JsonPropertyName("point")] Point3dDto Point);

public sealed record NurbsInfoResult(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("degreeU")]         int DegreeU,
    [property: JsonPropertyName("degreeV")]         int DegreeV,
    [property: JsonPropertyName("controlPointsU")]  int ControlPointsU,
    [property: JsonPropertyName("controlPointsV")]  int ControlPointsV,
    [property: JsonPropertyName("area")]            double Area,
    [property: JsonPropertyName("controlPoints")]   IReadOnlyList<NurbsControlPoint> ControlPoints,
    [property: JsonPropertyName("note")]            string Note);

public sealed record NurbsEditResult(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("u")]          int U,
    [property: JsonPropertyName("v")]          int V,
    [property: JsonPropertyName("from")]       Point3dDto From,
    [property: JsonPropertyName("to")]         Point3dDto To,
    [property: JsonPropertyName("moved")]      double Moved,
    [property: JsonPropertyName("areaBefore")] double AreaBefore,
    [property: JsonPropertyName("area")]       double Area,
    [property: JsonPropertyName("areaChange")] double AreaChange,
    [property: JsonPropertyName("note")]       string Note);
