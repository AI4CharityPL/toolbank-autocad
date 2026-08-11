// Typed DTOs for the acad-images category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Images;

public sealed record ImageAttachArgs(
    [property: JsonPropertyName("path")]            string Path,
    [property: JsonPropertyName("insertionPoint")]  Point3dDto InsertionPoint,
    [property: JsonPropertyName("width")]           double Width,
    [property: JsonPropertyName("height")]          double? Height = null,
    [property: JsonPropertyName("rotationDegrees")] double? RotationDegrees = null,
    [property: JsonPropertyName("layer")]           string? Layer = null,
    [property: JsonPropertyName("name")]            string? Name = null);

public sealed record ImagesNoArgs();

public sealed record ImageHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record ImageClipArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("points")] IReadOnlyList<Point2dDto>? Points = null);

public sealed record ImageAdjustArgs(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("brightness")] int? Brightness = null,
    [property: JsonPropertyName("contrast")]   int? Contrast = null,
    [property: JsonPropertyName("fade")]       int? Fade = null);

public sealed record ImageFrameArgs(
    [property: JsonPropertyName("frame")] int Frame);

public sealed record ImagePathArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("newPath")] string NewPath);

public sealed record ImageAdjustValues(
    [property: JsonPropertyName("brightness")] int Brightness,
    [property: JsonPropertyName("contrast")]   int Contrast,
    [property: JsonPropertyName("fade")]       int Fade);

public sealed record ImageInfo(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("name")]            string Name,
    [property: JsonPropertyName("path")]            string Path,
    [property: JsonPropertyName("insertionPoint")]  Point3dDto InsertionPoint,
    [property: JsonPropertyName("width")]           double Width,
    [property: JsonPropertyName("height")]          double Height,
    [property: JsonPropertyName("rotationDegrees")] double RotationDegrees,
    [property: JsonPropertyName("extents")]         BoundingBoxDto Extents,
    [property: JsonPropertyName("clipped")]         bool Clipped,
    [property: JsonPropertyName("adjust")]          ImageAdjustValues Adjust,
    [property: JsonPropertyName("layer")]           string Layer);

public sealed record ImageAttachResult(
    [property: JsonPropertyName("image")]            ImageInfo Image,
    [property: JsonPropertyName("reusedDefinition")] bool ReusedDefinition,
    [property: JsonPropertyName("note")]             string Note);

public sealed record ImageListResult(
    [property: JsonPropertyName("count")]  int Count,
    [property: JsonPropertyName("images")] IReadOnlyList<ImageInfo> Images,
    [property: JsonPropertyName("note")]   string Note);

public sealed record ImageDetachResult(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("defRemoved")] bool DefRemoved,
    [property: JsonPropertyName("note")]       string Note);

public sealed record ImageClipResult(
    [property: JsonPropertyName("handle")]            string Handle,
    [property: JsonPropertyName("clipped")]           bool Clipped,
    [property: JsonPropertyName("boundaryPointCount")] int BoundaryPointCount,
    [property: JsonPropertyName("imageWidthPx")]      double ImageWidthPx,
    [property: JsonPropertyName("imageHeightPx")]     double ImageHeightPx,
    [property: JsonPropertyName("extentsBefore")]     BoundingBoxDto ExtentsBefore,
    [property: JsonPropertyName("extentsAfter")]      BoundingBoxDto ExtentsAfter,
    [property: JsonPropertyName("note")]              string Note);

public sealed record ImageAdjustResult(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("before")] ImageAdjustValues Before,
    [property: JsonPropertyName("after")]  ImageAdjustValues After,
    [property: JsonPropertyName("note")]   string Note);

public sealed record ImageFrameResult(
    [property: JsonPropertyName("before")] int Before,
    [property: JsonPropertyName("after")]  int After,
    [property: JsonPropertyName("note")]   string Note);

public sealed record ImagePathResult(
    [property: JsonPropertyName("handle")]         string Handle,
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("previousPath")]   string PreviousPath,
    [property: JsonPropertyName("newPath")]        string NewPath,
    [property: JsonPropertyName("loaded")]         bool Loaded,
    [property: JsonPropertyName("affectedHandles")] IReadOnlyList<string> AffectedHandles,
    [property: JsonPropertyName("note")]           string Note);
