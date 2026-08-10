// Plugin-side DTOs for the acad-sections-3d category.
// Mirrors src/AcadMcp.Backend/Categories/Sections3d/Sections3dDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record SectionCreateArgsDto(
    // No `normal`: Section.Normal is READ-ONLY and the cut plane is the one containing the
    // section line and verticalDirection, so the normal is a result, never an input.
    [property: JsonPropertyName("vertices")]          List<Point3dDto>? Vertices,
    [property: JsonPropertyName("verticalDirection")] Point3dDto? VerticalDirection,
    [property: JsonPropertyName("state")]             string? State,
    [property: JsonPropertyName("elevation")]         double? Elevation,
    [property: JsonPropertyName("height")]            double? Height,
    [property: JsonPropertyName("depth")]             double? Depth,
    [property: JsonPropertyName("liveSection")]       bool? LiveSection,
    [property: JsonPropertyName("layer")]             string? Layer);

internal sealed record SectionStateArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("state")]  string? State);

internal sealed record SectionLiveArgsDto(
    [property: JsonPropertyName("handle")]  string? Handle,
    [property: JsonPropertyName("enabled")] bool? Enabled);

internal sealed record SectionHeightArgsDto(
    [property: JsonPropertyName("handle")]    string? Handle,
    [property: JsonPropertyName("above")]     double? Above,
    [property: JsonPropertyName("below")]     double? Below,
    [property: JsonPropertyName("elevation")] double? Elevation);

internal sealed record SectionGenerateArgsDto(
    [property: JsonPropertyName("handle")]            string? Handle,
    [property: JsonPropertyName("sourceHandles")]     List<string>? SourceHandles,
    [property: JsonPropertyName("kind")]              string? Kind,
    [property: JsonPropertyName("includeBackground")] bool? IncludeBackground,
    [property: JsonPropertyName("includeForeground")] bool? IncludeForeground,
    [property: JsonPropertyName("includeTangency")]   bool? IncludeTangency,
    [property: JsonPropertyName("layer")]             string? Layer);
