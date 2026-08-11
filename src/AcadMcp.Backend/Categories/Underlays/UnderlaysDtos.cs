// Typed DTOs for the acad-underlays category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Underlays;

public sealed record UnderlayAttachArgs(
    [property: JsonPropertyName("path")]            string Path,
    [property: JsonPropertyName("insertionPoint")]  Point3dDto InsertionPoint,
    [property: JsonPropertyName("itemName")]        string? ItemName = null,
    [property: JsonPropertyName("scale")]           double? Scale = null,
    [property: JsonPropertyName("rotationDegrees")] double? RotationDegrees = null,
    [property: JsonPropertyName("layer")]           string? Layer = null,
    [property: JsonPropertyName("name")]            string? Name = null);

public sealed record UnderlaysNoArgs();

public sealed record UnderlayHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record UnderlayClipArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("points")] IReadOnlyList<Point2dDto>? Points = null);

public sealed record UnderlayAdjustArgs(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("contrast")]   int? Contrast = null,
    [property: JsonPropertyName("fade")]       int? Fade = null,
    [property: JsonPropertyName("monochrome")] bool? Monochrome = null);

public sealed record UnderlayAdjustValues(
    [property: JsonPropertyName("contrast")]   int Contrast,
    [property: JsonPropertyName("fade")]       int Fade,
    [property: JsonPropertyName("monochrome")] bool Monochrome);

public sealed record UnderlayInfo(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("name")]            string Name,
    [property: JsonPropertyName("kind")]            string Kind,
    [property: JsonPropertyName("path")]            string Path,
    [property: JsonPropertyName("itemName")]        string? ItemName,
    [property: JsonPropertyName("insertionPoint")]  Point3dDto InsertionPoint,
    [property: JsonPropertyName("rotationDegrees")] double RotationDegrees,
    [property: JsonPropertyName("scale")]           double Scale,
    [property: JsonPropertyName("extents")]         BoundingBoxDto Extents,
    [property: JsonPropertyName("clipped")]         bool Clipped,
    [property: JsonPropertyName("adjust")]          UnderlayAdjustValues Adjust,
    [property: JsonPropertyName("layer")]           string Layer);

public sealed record UnderlayAttachResult(
    [property: JsonPropertyName("underlay")]         UnderlayInfo Underlay,
    [property: JsonPropertyName("reusedDefinition")] bool ReusedDefinition,
    [property: JsonPropertyName("note")]             string Note);

public sealed record UnderlayListResult(
    [property: JsonPropertyName("count")]     int Count,
    [property: JsonPropertyName("underlays")] IReadOnlyList<UnderlayInfo> Underlays,
    [property: JsonPropertyName("note")]      string Note);

public sealed record UnderlayDetachResult(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("defRemoved")] bool DefRemoved,
    [property: JsonPropertyName("note")]       string Note);

public sealed record UnderlayClipResult(
    [property: JsonPropertyName("handle")]             string Handle,
    [property: JsonPropertyName("clipped")]            bool Clipped,
    [property: JsonPropertyName("boundaryPointCount")] int BoundaryPointCount,
    [property: JsonPropertyName("underlayWidth")]      double UnderlayWidth,
    [property: JsonPropertyName("underlayHeight")]     double UnderlayHeight,
    [property: JsonPropertyName("extentsBefore")]      BoundingBoxDto ExtentsBefore,
    [property: JsonPropertyName("extentsAfter")]       BoundingBoxDto ExtentsAfter,
    [property: JsonPropertyName("note")]               string Note);

public sealed record UnderlayAdjustResult(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("before")] UnderlayAdjustValues Before,
    [property: JsonPropertyName("after")]  UnderlayAdjustValues After,
    [property: JsonPropertyName("note")]   string Note);
