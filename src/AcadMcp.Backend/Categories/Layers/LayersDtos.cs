// Typed DTOs for the acad-layers category.
// Mirrors the wire shape consumed by the plugin under "acad.layers.<verb>".
// See rule 19-tool-implementation-pattern.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Layers;

public sealed record EmptyArgs();

public sealed record LayerNameArg(
    [property: JsonPropertyName("name")] string Name);

public sealed record LayerNamesArg(
    [property: JsonPropertyName("names")] IReadOnlyList<string> Names);

public sealed record CreateLayerArgs(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("color")]        ColorDto? Color = null,
    [property: JsonPropertyName("linetype")]     string? Linetype = null,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm = null,
    [property: JsonPropertyName("plottable")]    bool Plottable = true,
    [property: JsonPropertyName("description")]  string? Description = null);

public sealed record SetLayerColorArgs(
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("color")] ColorDto Color);

public sealed record SetLayerLinetypeArgs(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("linetype")] string Linetype);

public sealed record SetLayerLineweightArgs(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm);

public sealed record SetLayerStateArgs(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("frozen")]    bool? Frozen = null,
    [property: JsonPropertyName("locked")]    bool? Locked = null,
    [property: JsonPropertyName("off")]       bool? Off = null,
    [property: JsonPropertyName("plottable")] bool? Plottable = null);

public sealed record RenameLayerArgs(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

public sealed record SaveLayerStateArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record LayerListResult(
    [property: JsonPropertyName("layers")]  IReadOnlyList<LayerInfo> Layers,
    [property: JsonPropertyName("current")] string Current);

public sealed record LayerResult(
    [property: JsonPropertyName("layer")] LayerInfo Layer);

public sealed record AffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

public sealed record StringListResult(
    [property: JsonPropertyName("items")] IReadOnlyList<string> Items);

// ─────────────── named layer states, beyond save/restore/list (roadmap 2.4) ───────────────

public sealed record LayerStateNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record LayerStateFileArgs(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("overwrite")] bool Overwrite = false);

public sealed record RenameLayerStateArgs(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("newName")] string NewName);

public sealed record LayerStateDescriptionArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record LayerStateExportResult(
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("path")]  string Path,
    [property: JsonPropertyName("bytes")] long Bytes);

public sealed record LayerStateImportResult(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("imported")] IReadOnlyList<string> Imported,
    [property: JsonPropertyName("count")]    int Count,
    [property: JsonPropertyName("note")]     string? Note = null);

public sealed record LayerStateDeleteResult(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("deleted")] bool Deleted,
    [property: JsonPropertyName("note")]    string Note);

public sealed record LayerStateRenameResult(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("renamed")] bool Renamed);

public sealed record LayerStateDescriptionResult(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description);

public sealed record LayerStateCompareResult(
    [property: JsonPropertyName("name")]                  string Name,
    [property: JsonPropertyName("description")]           string Description,
    [property: JsonPropertyName("matchesCurrentDrawing")] bool MatchesCurrentDrawing,
    [property: JsonPropertyName("layers")]                IReadOnlyList<string> Layers,
    [property: JsonPropertyName("layerCount")]            int LayerCount,
    [property: JsonPropertyName("note")]                  string Note);
