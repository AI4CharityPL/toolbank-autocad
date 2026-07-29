// Plugin-side DTOs for the acad-view category.
// Mirror Backend/Categories/View/ViewDtos.cs.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record ViewEmptyArgsDto();

internal sealed record ZoomWindowArgsDto(
    [property: JsonPropertyName("corner1")] Point2dDto Corner1,
    [property: JsonPropertyName("corner2")] Point2dDto Corner2);

internal sealed record ZoomCenterArgsDto(
    [property: JsonPropertyName("center")] Point2dDto Center,
    [property: JsonPropertyName("height")] double Height);

internal sealed record ZoomScaleArgsDto(
    [property: JsonPropertyName("scale")] double Scale);

internal sealed record SetCurrentViewArgsDto(
    [property: JsonPropertyName("name")] string Name);
