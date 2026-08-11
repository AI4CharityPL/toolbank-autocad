// Plugin-side DTOs for the acad-underlays category.
// Mirrors src/AcadMcp.Backend/Categories/Underlays/UnderlaysDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record UnderlayAttachArgsDto(
    [property: JsonPropertyName("path")]            string? Path,
    [property: JsonPropertyName("insertionPoint")]  Point3dDto InsertionPoint,
    [property: JsonPropertyName("itemName")]        string? ItemName,
    [property: JsonPropertyName("scale")]           double? Scale,
    [property: JsonPropertyName("rotationDegrees")] double? RotationDegrees,
    [property: JsonPropertyName("layer")]           string? Layer,
    [property: JsonPropertyName("name")]            string? Name);

internal sealed record UnderlayHandleArgsDto(
    [property: JsonPropertyName("handle")] string? Handle);

internal sealed record UnderlayClipArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("points")] List<Point2dDto>? Points);

internal sealed record UnderlayAdjustArgsDto(
    [property: JsonPropertyName("handle")]     string? Handle,
    [property: JsonPropertyName("contrast")]   int? Contrast,
    [property: JsonPropertyName("fade")]       int? Fade,
    [property: JsonPropertyName("monochrome")] bool? Monochrome);
