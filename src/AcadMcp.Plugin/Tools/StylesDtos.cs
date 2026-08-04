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
