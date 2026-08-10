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
