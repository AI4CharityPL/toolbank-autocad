// Plugin-side DTOs for the acad-images category.
// Mirrors src/AcadMcp.Backend/Categories/Images/ImagesDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record ImageAttachArgsDto(
    [property: JsonPropertyName("path")]            string? Path,
    [property: JsonPropertyName("insertionPoint")]  Point3dDto InsertionPoint,
    [property: JsonPropertyName("width")]           double? Width,
    [property: JsonPropertyName("height")]          double? Height,
    [property: JsonPropertyName("rotationDegrees")] double? RotationDegrees,
    [property: JsonPropertyName("layer")]           string? Layer,
    [property: JsonPropertyName("name")]            string? Name);

internal sealed record ImageHandleArgsDto(
    [property: JsonPropertyName("handle")] string? Handle);

internal sealed record ImageClipArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("points")] List<Point2dDto>? Points);

internal sealed record ImageAdjustArgsDto(
    [property: JsonPropertyName("handle")]     string? Handle,
    [property: JsonPropertyName("brightness")] int? Brightness,
    [property: JsonPropertyName("contrast")]   int? Contrast,
    [property: JsonPropertyName("fade")]       int? Fade);

internal sealed record ImageFrameArgsDto(
    [property: JsonPropertyName("frame")] int? Frame);

internal sealed record ImagePathArgsDto(
    [property: JsonPropertyName("handle")]  string? Handle,
    [property: JsonPropertyName("newPath")] string? NewPath);
