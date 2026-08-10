// Plugin-side DTOs for the acad-views category.
// Mirrors src/AcadMcp.Backend/Categories/Views/ViewsDtos.cs wire shape.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record ViewArgsDto(
    [property: JsonPropertyName("name")]            string? Name,
    [property: JsonPropertyName("center")]          Point3dDto? Center,
    [property: JsonPropertyName("width")]           double? Width,
    [property: JsonPropertyName("height")]          double? Height,
    [property: JsonPropertyName("corner1")]         Point3dDto? Corner1,
    [property: JsonPropertyName("corner2")]         Point3dDto? Corner2,
    [property: JsonPropertyName("target")]          Point3dDto? Target,
    [property: JsonPropertyName("viewDirection")]   Point3dDto? ViewDirection,
    [property: JsonPropertyName("lensLength")]      double? LensLength,
    [property: JsonPropertyName("twist")]           double? Twist,
    [property: JsonPropertyName("viewportHandle")]  string? ViewportHandle,
    [property: JsonPropertyName("enabled")]         bool? Enabled,
    [property: JsonPropertyName("ucsName")]         string? UcsName);
