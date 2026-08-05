// Plugin-side DTOs for acad-styles. Wire names must match the backend's StylesDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record EmptyStylesArgsDto();

internal sealed record DimStyleNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record CreateDimStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

internal sealed record ModifyDimStyleArgsDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

internal sealed record CopyDimStyleArgsDto(
    [property: JsonPropertyName("sourceName")]  string SourceName,
    [property: JsonPropertyName("newName")]     string NewName,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false);

internal sealed record MLeaderStyleNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record CreateMLeaderStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

internal sealed record ModifyMLeaderStyleArgsDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

internal sealed record TableStyleNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record CreateTableStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

internal sealed record ModifyTableStyleArgsDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

internal sealed record MlineElementSpecDto(
    [property: JsonPropertyName("offset")]     double Offset,
    [property: JsonPropertyName("colorIndex")] int? ColorIndex = null,
    [property: JsonPropertyName("linetype")]   string? Linetype = null);

internal sealed record CreateMlineStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("elements")]    IReadOnlyList<MlineElementSpecDto>? Elements = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("showMiters")]  bool ShowMiters = false,
    [property: JsonPropertyName("startAngle")]  double? StartAngle = null,
    [property: JsonPropertyName("endAngle")]    double? EndAngle = null,
    [property: JsonPropertyName("startCap")]    string? StartCap = null,
    [property: JsonPropertyName("endCap")]      string? EndCap = null,
    [property: JsonPropertyName("fillColorIndex")] int? FillColorIndex = null,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

internal sealed record ModifyMlineStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("elements")]    IReadOnlyList<MlineElementSpecDto>? Elements = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("showMiters")]  bool? ShowMiters = null,
    [property: JsonPropertyName("startAngle")]  double? StartAngle = null,
    [property: JsonPropertyName("endAngle")]    double? EndAngle = null,
    [property: JsonPropertyName("startCap")]    string? StartCap = null,
    [property: JsonPropertyName("endCap")]      string? EndCap = null,
    [property: JsonPropertyName("fillColorIndex")] int? FillColorIndex = null);

internal sealed record MlineStyleNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record CreateLayerFilterArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("expression")]  string? Expression,
    [property: JsonPropertyName("parent")]      string? Parent,
    [property: JsonPropertyName("overwrite")]   bool Overwrite);

internal sealed record CreateLayerGroupFilterArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("layers")]      IReadOnlyList<string>? Layers,
    [property: JsonPropertyName("parent")]      string? Parent,
    [property: JsonPropertyName("overwrite")]   bool Overwrite);

internal sealed record LayerFilterNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record SetTableCellStyleArgsDto(
    [property: JsonPropertyName("name")]                 string Name,
    [property: JsonPropertyName("row")]                  string? Row,
    [property: JsonPropertyName("alignment")]            string? Alignment,
    [property: JsonPropertyName("colorIndex")]           int? ColorIndex,
    [property: JsonPropertyName("backgroundColorIndex")] int? BackgroundColorIndex);

internal sealed record CreateVisualStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("basedOn")]     string? BasedOn,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("overwrite")]   bool Overwrite);

internal sealed record SetPointDisplayArgsDto(
    [property: JsonPropertyName("glyph")]    string? Glyph,
    [property: JsonPropertyName("surround")] string? Surround,
    [property: JsonPropertyName("mode")]     int? Mode,
    [property: JsonPropertyName("size")]     double? Size);

internal sealed record DimOverrideQueryArgsDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record ApplyDimOverrideArgsDto(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double>? Properties);

internal sealed record ImportDimStyleArgsDto(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("names")]     IReadOnlyList<string>? Names,
    [property: JsonPropertyName("overwrite")] bool Overwrite);
