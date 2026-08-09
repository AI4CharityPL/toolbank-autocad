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

// ─────────── roadmap 4.1, second tranche: slicing and interference ───────────

public sealed record SliceSolidArgs(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("planePoint")]  Point3dDto? PlanePoint = null,
    [property: JsonPropertyName("planeNormal")] Point3dDto? PlaneNormal = null,
    [property: JsonPropertyName("keepBoth")]    bool? KeepBoth = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record SliceSolidResult(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("otherHalf")]       EntityHandle? OtherHalf,
    [property: JsonPropertyName("volumeBefore")]    double VolumeBefore,
    [property: JsonPropertyName("keptVolume")]      double KeptVolume,
    [property: JsonPropertyName("otherVolume")]     double? OtherVolume,
    [property: JsonPropertyName("volumesSum")]      double? VolumesSum,
    [property: JsonPropertyName("keptBoth")]        bool KeptBoth,
    [property: JsonPropertyName("note")]            string Note);

public sealed record InterfereArgs(
    [property: JsonPropertyName("handle1")]     string Handle1,
    [property: JsonPropertyName("handle2")]     string Handle2,
    [property: JsonPropertyName("createSolid")] bool? CreateSolid = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record InterfereResult(
    [property: JsonPropertyName("interferes")]       bool Interferes,
    [property: JsonPropertyName("entity")]           EntityHandle? Entity,
    [property: JsonPropertyName("interferenceVolume")] double? InterferenceVolume,
    [property: JsonPropertyName("volume1")]          double Volume1,
    [property: JsonPropertyName("volume2")]          double Volume2,
    [property: JsonPropertyName("originalsIntact")]  bool OriginalsIntact,
    [property: JsonPropertyName("note")]             string Note);

public sealed record ImprintArgs(
    [property: JsonPropertyName("solidHandle")] string SolidHandle,
    [property: JsonPropertyName("curveHandle")] string CurveHandle,
    [property: JsonPropertyName("eraseSource")] bool? EraseSource = null);

public sealed record ImprintResult(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("facesBefore")]  int FacesBefore,
    [property: JsonPropertyName("faces")]        int Faces,
    [property: JsonPropertyName("edgesBefore")]  int EdgesBefore,
    [property: JsonPropertyName("edges")]        int Edges,
    [property: JsonPropertyName("volumeBefore")] double VolumeBefore,
    [property: JsonPropertyName("volume")]       double Volume,
    [property: JsonPropertyName("sourceErased")] bool SourceErased,
    [property: JsonPropertyName("note")]         string Note);

// ─────────── roadmap 4.1: the face/edge family ───────────

public sealed record SolidQueryArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record SolidEdgeInfo(
    [property: JsonPropertyName("index")]    int Index,
    [property: JsonPropertyName("start")]    Point3dDto Start,
    [property: JsonPropertyName("end")]      Point3dDto End,
    [property: JsonPropertyName("midpoint")] Point3dDto Midpoint,
    [property: JsonPropertyName("length")]   double Length);

public sealed record SolidEdgesResult(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("count")]  int Count,
    [property: JsonPropertyName("edges")]  IReadOnlyList<SolidEdgeInfo> Edges,
    [property: JsonPropertyName("note")]   string Note);

public sealed record SolidFaceInfo(
    [property: JsonPropertyName("index")]     int Index,
    [property: JsonPropertyName("centroid")]  Point3dDto Centroid,
    [property: JsonPropertyName("normal")]    Point3dDto? Normal,
    [property: JsonPropertyName("edgeCount")] int EdgeCount);

public sealed record SolidFacesResult(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("count")]  int Count,
    [property: JsonPropertyName("faces")]  IReadOnlyList<SolidFaceInfo> Faces,
    [property: JsonPropertyName("note")]   string Note);

public sealed record EdgeOpArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("edgeIndexes")]   IReadOnlyList<int>? EdgeIndexes = null,
    [property: JsonPropertyName("nearPoints")]    IReadOnlyList<Point3dDto>? NearPoints = null,
    [property: JsonPropertyName("radius")]        double? Radius = null,
    [property: JsonPropertyName("distance")]      double? Distance = null,
    [property: JsonPropertyName("distance2")]     double? Distance2 = null,
    [property: JsonPropertyName("baseFaceIndex")] int? BaseFaceIndex = null,
    [property: JsonPropertyName("allowFaceLoss")] bool? AllowFaceLoss = null);

// ─────────── roadmap 4.1, last tranche: shape and health ───────────

public sealed record PolysolidArgs(
    [property: JsonPropertyName("pathHandle")] string? PathHandle = null,
    [property: JsonPropertyName("vertices")]   IReadOnlyList<Point3dDto>? Vertices = null,
    [property: JsonPropertyName("closed")]     bool? Closed = null,
    [property: JsonPropertyName("width")]      double? Width = null,
    [property: JsonPropertyName("height")]     double? Height = null,
    [property: JsonPropertyName("justify")]    string? Justify = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record PolysolidResult(
    [property: JsonPropertyName("entity")]     EntityHandle Entity,
    [property: JsonPropertyName("volume")]     double Volume,
    [property: JsonPropertyName("width")]      double Width,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("pathLength")] double PathLength,
    [property: JsonPropertyName("widthTimesHeightTimesLength")] double WidthTimesHeightTimesLength,
    [property: JsonPropertyName("note")]       string Note);

public sealed record PressPullArgs(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("distance")]     double? Distance = null,
    [property: JsonPropertyName("targetHandle")] string? TargetHandle = null,
    [property: JsonPropertyName("eraseSource")]  bool? EraseSource = null,
    [property: JsonPropertyName("layer")]        string? Layer = null);

public sealed record PressPullResult(
    [property: JsonPropertyName("entity")]            EntityHandle? Entity,
    [property: JsonPropertyName("handle")]            string? Handle,
    [property: JsonPropertyName("mode")]              string? Mode,
    [property: JsonPropertyName("area")]              double Area,
    [property: JsonPropertyName("distance")]          double Distance,
    [property: JsonPropertyName("volume")]            double Volume,
    [property: JsonPropertyName("pushedVolume")]      double? PushedVolume,
    [property: JsonPropertyName("volumeBefore")]      double? VolumeBefore,
    [property: JsonPropertyName("volumeChange")]      double? VolumeChange,
    [property: JsonPropertyName("areaTimesDistance")] double? AreaTimesDistance,
    [property: JsonPropertyName("note")]              string Note);

public sealed record CleanSolidResult(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("facesBefore")]  int FacesBefore,
    [property: JsonPropertyName("faces")]        int Faces,
    [property: JsonPropertyName("edgesBefore")]  int EdgesBefore,
    [property: JsonPropertyName("edges")]        int Edges,
    [property: JsonPropertyName("facesRemoved")] int FacesRemoved,
    [property: JsonPropertyName("edgesRemoved")] int EdgesRemoved,
    [property: JsonPropertyName("volume")]       double Volume,
    [property: JsonPropertyName("note")]         string Note);

public sealed record CheckSolidResult(
    [property: JsonPropertyName("handle")]              string Handle,
    [property: JsonPropertyName("valid")]               bool Valid,
    [property: JsonPropertyName("faces")]               int Faces,
    [property: JsonPropertyName("edges")]               int Edges,
    [property: JsonPropertyName("vertices")]            int Vertices,
    [property: JsonPropertyName("shells")]              int Shells,
    [property: JsonPropertyName("complexes")]           int Complexes,
    [property: JsonPropertyName("rings")]               int Rings,
    [property: JsonPropertyName("eulerCharacteristic")] int EulerCharacteristic,
    [property: JsonPropertyName("genus")]               int? Genus,
    [property: JsonPropertyName("volume")]              double Volume,
    [property: JsonPropertyName("surfaceArea")]         double SurfaceArea,
    [property: JsonPropertyName("problems")]            IReadOnlyList<string> Problems,
    [property: JsonPropertyName("note")]                string Note);

public sealed record FaceOpArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("faceIndexes")]   IReadOnlyList<int>? FaceIndexes = null,
    [property: JsonPropertyName("nearPoints")]    IReadOnlyList<Point3dDto>? NearPoints = null,
    [property: JsonPropertyName("facing")]        Point3dDto? Facing = null,
    [property: JsonPropertyName("distance")]      double? Distance = null,
    [property: JsonPropertyName("taperAngleDeg")] double? TaperAngleDeg = null,
    [property: JsonPropertyName("pathHandle")]    string? PathHandle = null,
    [property: JsonPropertyName("from")]          Point3dDto? From = null,
    [property: JsonPropertyName("to")]            Point3dDto? To = null,
    [property: JsonPropertyName("axisStart")]     Point3dDto? AxisStart = null,
    [property: JsonPropertyName("axisEnd")]       Point3dDto? AxisEnd = null,
    [property: JsonPropertyName("angleDeg")]      double? AngleDeg = null,
    [property: JsonPropertyName("basePoint")]     Point3dDto? BasePoint = null,
    [property: JsonPropertyName("direction")]     Point3dDto? Direction = null,
    [property: JsonPropertyName("thickness")]     double? Thickness = null);

public sealed record FaceOpResult(
    [property: JsonPropertyName("handle")]         string Handle,
    [property: JsonPropertyName("facesAffected")]  int FacesAffected,
    [property: JsonPropertyName("faces")]          IReadOnlyList<SolidFaceInfo> Faces,
    [property: JsonPropertyName("facesBefore")]    int FacesBefore,
    [property: JsonPropertyName("faceCount")]      int FaceCount,
    [property: JsonPropertyName("volumeBefore")]   double VolumeBefore,
    [property: JsonPropertyName("volume")]         double Volume,
    [property: JsonPropertyName("volumeChange")]   double VolumeChange,
    [property: JsonPropertyName("distance")]       double? Distance,
    [property: JsonPropertyName("angleDeg")]       double? AngleDeg,
    [property: JsonPropertyName("taperAngleDeg")]  double? TaperAngleDeg,
    [property: JsonPropertyName("alongPath")]      string? AlongPath,
    [property: JsonPropertyName("note")]           string Note);

public sealed record ShellResult(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("thickness")]     double Thickness,
    [property: JsonPropertyName("openFaces")]     int OpenFaces,
    [property: JsonPropertyName("faces")]         IReadOnlyList<SolidFaceInfo> Faces,
    [property: JsonPropertyName("facesBefore")]   int FacesBefore,
    [property: JsonPropertyName("faceCount")]     int FaceCount,
    [property: JsonPropertyName("volumeBefore")]  double VolumeBefore,
    [property: JsonPropertyName("volume")]        double Volume,
    [property: JsonPropertyName("volumeRemoved")] double VolumeRemoved,
    [property: JsonPropertyName("note")]          string Note);

public sealed record FilletEdgeResult(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("edgesFilleted")] int EdgesFilleted,
    [property: JsonPropertyName("edges")]         IReadOnlyList<SolidEdgeInfo> Edges,
    [property: JsonPropertyName("radius")]        double Radius,
    [property: JsonPropertyName("facesBefore")]   int FacesBefore,
    [property: JsonPropertyName("faces")]         int Faces,
    [property: JsonPropertyName("volumeBefore")]  double VolumeBefore,
    [property: JsonPropertyName("volume")]        double Volume,
    [property: JsonPropertyName("volumeRemoved")] double VolumeRemoved,
    [property: JsonPropertyName("facesConsumed")] bool FacesConsumed,
    [property: JsonPropertyName("note")]          string Note);

public sealed record ChamferEdgeResult(
    [property: JsonPropertyName("handle")]            string Handle,
    [property: JsonPropertyName("edgesChamfered")]    int EdgesChamfered,
    [property: JsonPropertyName("edges")]             IReadOnlyList<SolidEdgeInfo> Edges,
    [property: JsonPropertyName("distance")]          double Distance,
    [property: JsonPropertyName("distance2")]         double Distance2,
    [property: JsonPropertyName("baseFaceIndex")]     int BaseFaceIndex,
    [property: JsonPropertyName("baseFaceCentroid")]  Point3dDto BaseFaceCentroid,
    [property: JsonPropertyName("facesBefore")]       int FacesBefore,
    [property: JsonPropertyName("faces")]             int Faces,
    [property: JsonPropertyName("volumeBefore")]      double VolumeBefore,
    [property: JsonPropertyName("volume")]            double Volume,
    [property: JsonPropertyName("volumeRemoved")]     double VolumeRemoved,
    [property: JsonPropertyName("facesConsumed")]     bool FacesConsumed,
    [property: JsonPropertyName("note")]              string Note);
