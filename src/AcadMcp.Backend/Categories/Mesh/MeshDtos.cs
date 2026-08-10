// Typed DTOs for the acad-mesh category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Mesh;

public sealed record MeshBoxArgs(
    [property: JsonPropertyName("corner1")]     Point3dDto Corner1,
    [property: JsonPropertyName("corner2")]     Point3dDto Corner2,
    [property: JsonPropertyName("smoothLevel")] int? SmoothLevel = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record MeshHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record MeshSmoothArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("level")]  int? Level = null,
    [property: JsonPropertyName("by")]     int? By = null);

public sealed record MeshConvertArgs(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("smooth")]      bool? Smooth = null,
    [property: JsonPropertyName("optimize")]    bool? Optimize = null,
    [property: JsonPropertyName("eraseSource")] bool? EraseSource = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record MeshSize(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z);

public sealed record MeshBoxResult(
    [property: JsonPropertyName("entity")]      EntityHandle Entity,
    [property: JsonPropertyName("vertices")]    int Vertices,
    [property: JsonPropertyName("faces")]       int Faces,
    [property: JsonPropertyName("smoothLevel")] int SmoothLevel,
    [property: JsonPropertyName("size")]        MeshSize Size,
    [property: JsonPropertyName("note")]        string Note);

public sealed record MeshInfoResult(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("vertices")]    int Vertices,
    [property: JsonPropertyName("faces")]       int Faces,
    [property: JsonPropertyName("smoothLevel")] int SmoothLevel,
    [property: JsonPropertyName("bbox")]        BoundingBoxDto Bbox,
    [property: JsonPropertyName("note")]        string Note);

public sealed record MeshSmoothResult(
    [property: JsonPropertyName("handle")]            string Handle,
    [property: JsonPropertyName("smoothLevelBefore")] int SmoothLevelBefore,
    [property: JsonPropertyName("smoothLevel")]       int SmoothLevel,
    [property: JsonPropertyName("facesBefore")]       int FacesBefore,
    [property: JsonPropertyName("faces")]             int Faces,
    [property: JsonPropertyName("verticesNow")]       int VerticesNow,
    [property: JsonPropertyName("cageFaces")]         int CageFaces,
    [property: JsonPropertyName("cageDiagonalBefore")] double CageDiagonalBefore,
    [property: JsonPropertyName("cageDiagonal")]       double CageDiagonal,
    [property: JsonPropertyName("note")]              string Note);

public sealed record MeshToSolidResult(
    [property: JsonPropertyName("entity")]           EntityHandle Entity,
    [property: JsonPropertyName("volume")]           double Volume,
    [property: JsonPropertyName("meshFaces")]        int MeshFaces,
    [property: JsonPropertyName("meshSmoothLevel")]  int MeshSmoothLevel,
    [property: JsonPropertyName("sourceErased")]     bool SourceErased,
    [property: JsonPropertyName("note")]             string Note);

public sealed record MeshToSurfaceResult(
    [property: JsonPropertyName("entity")]       EntityHandle Entity,
    [property: JsonPropertyName("type")]         string Type,
    [property: JsonPropertyName("meshFaces")]    int MeshFaces,
    [property: JsonPropertyName("sourceErased")] bool SourceErased,
    [property: JsonPropertyName("note")]         string Note);

// ─────────── roadmap 4.3, second tranche ───────────

public sealed record MeshCreaseArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("level")]  double? Level = null);

public sealed record MeshCylinderArgs(
    [property: JsonPropertyName("basePoint")]   Point3dDto BasePoint,
    [property: JsonPropertyName("radius")]      double? Radius = null,
    [property: JsonPropertyName("height")]      double? Height = null,
    [property: JsonPropertyName("sides")]       int? Sides = null,
    [property: JsonPropertyName("smoothLevel")] int? SmoothLevel = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record MeshCreaseResult(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("creaseLevel")] double CreaseLevel,
    [property: JsonPropertyName("smoothLevel")] int SmoothLevel,
    [property: JsonPropertyName("allEdges")]    bool AllEdges,
    [property: JsonPropertyName("note")]        string Note);

public sealed record MeshCylinderResult(
    [property: JsonPropertyName("entity")]        EntityHandle Entity,
    [property: JsonPropertyName("vertices")]      int Vertices,
    [property: JsonPropertyName("faces")]         int Faces,
    [property: JsonPropertyName("smoothLevel")]   int SmoothLevel,
    [property: JsonPropertyName("sides")]         int Sides,
    [property: JsonPropertyName("radius")]        double Radius,
    [property: JsonPropertyName("height")]        double Height,
    [property: JsonPropertyName("prismVolume")]   double PrismVolume,
    [property: JsonPropertyName("circleVolume")]  double CircleVolume,
    [property: JsonPropertyName("note")]          string Note);

public sealed record MeshWedgeResult(
    [property: JsonPropertyName("entity")]         EntityHandle Entity,
    [property: JsonPropertyName("vertices")]       int Vertices,
    [property: JsonPropertyName("faces")]          int Faces,
    [property: JsonPropertyName("smoothLevel")]    int SmoothLevel,
    [property: JsonPropertyName("size")]           MeshSize Size,
    [property: JsonPropertyName("halfBoxVolume")]  double HalfBoxVolume,
    [property: JsonPropertyName("note")]           string Note);

// ─────────── roadmap 4.3, third tranche ───────────

public sealed record MeshSphereArgs(
    [property: JsonPropertyName("center")]      Point3dDto Center,
    [property: JsonPropertyName("radius")]      double? Radius = null,
    [property: JsonPropertyName("segments")]    int? Segments = null,
    [property: JsonPropertyName("rings")]       int? Rings = null,
    [property: JsonPropertyName("smoothLevel")] int? SmoothLevel = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record MeshFaceOpArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("faceIndex")]     int? FaceIndex = null,
    [property: JsonPropertyName("distance")]      double? Distance = null,
    [property: JsonPropertyName("direction")]     Point3dDto? Direction = null,
    [property: JsonPropertyName("taperAngleDeg")] double? TaperAngleDeg = null);

public sealed record MeshSphereResult(
    [property: JsonPropertyName("entity")]            EntityHandle Entity,
    [property: JsonPropertyName("vertices")]          int Vertices,
    [property: JsonPropertyName("faces")]             int Faces,
    [property: JsonPropertyName("smoothLevel")]       int SmoothLevel,
    [property: JsonPropertyName("segments")]          int Segments,
    [property: JsonPropertyName("rings")]             int Rings,
    [property: JsonPropertyName("radius")]            double Radius,
    [property: JsonPropertyName("trueSphereVolume")]  double TrueSphereVolume,
    [property: JsonPropertyName("note")]              string Note);

public sealed record MeshConeResult(
    [property: JsonPropertyName("entity")]         EntityHandle Entity,
    [property: JsonPropertyName("vertices")]       int Vertices,
    [property: JsonPropertyName("faces")]          int Faces,
    [property: JsonPropertyName("smoothLevel")]    int SmoothLevel,
    [property: JsonPropertyName("sides")]          int Sides,
    [property: JsonPropertyName("radius")]         double Radius,
    [property: JsonPropertyName("height")]         double Height,
    [property: JsonPropertyName("pyramidVolume")]  double PyramidVolume,
    [property: JsonPropertyName("coneVolume")]     double ConeVolume,
    [property: JsonPropertyName("note")]           string Note);

public sealed record MeshFaceExtrudeResult(
    [property: JsonPropertyName("handle")]         string Handle,
    [property: JsonPropertyName("faceIndex")]      int FaceIndex,
    [property: JsonPropertyName("distance")]       double Distance,
    [property: JsonPropertyName("verticesBefore")] int VerticesBefore,
    [property: JsonPropertyName("vertices")]       int Vertices,
    [property: JsonPropertyName("facesBefore")]    int FacesBefore,
    [property: JsonPropertyName("faces")]          int Faces,
    [property: JsonPropertyName("note")]           string Note);
