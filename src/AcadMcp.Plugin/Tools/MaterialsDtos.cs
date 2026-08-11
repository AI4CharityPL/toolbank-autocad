// Plugin-side DTOs for the acad-materials category.
// Mirrors src/AcadMcp.Backend/Categories/Materials/MaterialsDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record MaterialArgsDto(
    [property: JsonPropertyName("name")]          string? Name,
    [property: JsonPropertyName("description")]   string? Description,
    [property: JsonPropertyName("diffuse")]       ColorDto? Diffuse,
    [property: JsonPropertyName("diffuseFactor")] double? DiffuseFactor,
    [property: JsonPropertyName("specular")]      ColorDto? Specular,
    [property: JsonPropertyName("gloss")]         double? Gloss,
    [property: JsonPropertyName("opacity")]       double? Opacity,
    [property: JsonPropertyName("handles")]       List<string>? Handles,
    [property: JsonPropertyName("force")]         bool? Force);
