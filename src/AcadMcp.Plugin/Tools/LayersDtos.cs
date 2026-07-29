// Plugin-side DTOs for the acad-layers category. Mirror Backend/Categories/Layers/LayersDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record LayersEmptyArgsDto();

internal sealed record LayerNameArgDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record CreateLayerArgsDto(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("color")]        ColorDto? Color = null,
    [property: JsonPropertyName("linetype")]     string? Linetype = null,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm = null,
    [property: JsonPropertyName("plottable")]    bool Plottable = true,
    [property: JsonPropertyName("description")]  string? Description = null);

internal sealed record SetLayerColorArgsDto(
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("color")] ColorDto Color);

internal sealed record SetLayerLinetypeArgsDto(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("linetype")] string Linetype);

internal sealed record SetLayerLineweightArgsDto(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm);

internal sealed record SetLayerStateArgsDto(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("frozen")]    bool? Frozen = null,
    [property: JsonPropertyName("locked")]    bool? Locked = null,
    [property: JsonPropertyName("off")]       bool? Off = null,
    [property: JsonPropertyName("plottable")] bool? Plottable = null);

internal sealed record RenameLayerArgsDto(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

internal sealed record SaveLayerStateArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string? Description = null);
