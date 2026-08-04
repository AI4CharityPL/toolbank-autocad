// Plugin-side DTOs for acad-publish. Wire names must match the backend's PublishDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record CreatePageSetupArgsDto(
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("fromLayout")]     string? FromLayout = null,
    [property: JsonPropertyName("device")]         string? Device = null,
    [property: JsonPropertyName("media")]          string? Media = null,
    [property: JsonPropertyName("plotStyleTable")] string? PlotStyleTable = null,
    [property: JsonPropertyName("rotation")]       int? Rotation = null,
    [property: JsonPropertyName("overwrite")]      bool Overwrite = false);

internal sealed record PageSetupNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record EmptyPublishArgsDto();

// No allLayouts default, deliberately - see rule 44. Exactly one of layouts/allLayouts.
internal sealed record ApplyPageSetupArgsDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("layouts")]    IReadOnlyList<string>? Layouts = null,
    [property: JsonPropertyName("allLayouts")] bool AllLayouts = false);

internal sealed record ImportPageSetupArgsDto(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("overwrite")] bool Overwrite = false);
