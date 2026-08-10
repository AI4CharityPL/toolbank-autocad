// Plugin-side DTOs for the acad-geo category.
// Mirrors src/AcadMcp.Backend/Categories/Geo/GeoDtos.cs wire shape.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record GeoArgsDto(
    [property: JsonPropertyName("latitude")]         double? Latitude,
    [property: JsonPropertyName("longitude")]        double? Longitude,
    [property: JsonPropertyName("altitude")]         double? Altitude,
    [property: JsonPropertyName("point")]            Point3dDto? Point,
    [property: JsonPropertyName("designPoint")]      Point3dDto? DesignPoint,
    [property: JsonPropertyName("coordinateSystem")] string? CoordinateSystem,
    [property: JsonPropertyName("horizontalUnits")]  string? HorizontalUnits,
    [property: JsonPropertyName("verticalUnits")]    string? VerticalUnits,
    [property: JsonPropertyName("notes")]            string? Notes,
    [property: JsonPropertyName("landingGap")]       double? LandingGap,
    [property: JsonPropertyName("layer")]            string? Layer);
