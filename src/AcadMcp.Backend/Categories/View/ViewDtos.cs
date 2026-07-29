// DTOs for the acad-view category. Mirror the plugin wire shape under "acad.view.<verb>".
// Rules: 19, 22.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.View;

public sealed record ViewEmptyArgs();

public sealed record ZoomWindowArgs(
    [property: JsonPropertyName("corner1")] Point2dDto Corner1,
    [property: JsonPropertyName("corner2")] Point2dDto Corner2);

public sealed record ZoomExtentsArgs();

public sealed record ZoomAllArgs();

public sealed record ZoomScaleArgs(
    [property: JsonPropertyName("scale")] double Scale);

public sealed record ZoomCenterArgs(
    [property: JsonPropertyName("center")] Point2dDto Center,
    [property: JsonPropertyName("height")] double Height);

public sealed record SetCurrentViewArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record ViewDescriptor(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("centerX")]     double CenterX,
    [property: JsonPropertyName("centerY")]     double CenterY,
    [property: JsonPropertyName("width")]       double Width,
    [property: JsonPropertyName("height")]      double Height,
    [property: JsonPropertyName("isPaperSpace")] bool IsPaperSpace);

public sealed record ListViewsResult(
    [property: JsonPropertyName("views")] IReadOnlyList<ViewDescriptor> Views);

public sealed record CurrentViewResult(
    [property: JsonPropertyName("view")] ViewDescriptor View);

public sealed record ViewAffectedResult(
    [property: JsonPropertyName("affected")] int Affected);
