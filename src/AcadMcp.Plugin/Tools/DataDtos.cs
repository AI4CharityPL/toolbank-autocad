// Plugin-side DTOs for the acad-data category.
// Mirrors src/AcadMcp.Backend/Categories/Data/DataDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

/// One typed value. `type` is explicit rather than inferred from the JSON, because JSON cannot
/// tell 1 from 1.0 and AutoCAD very much can.
internal sealed record DataValueDto(
    [property: JsonPropertyName("type")]  string? Type,
    [property: JsonPropertyName("value")] object? Value,
    [property: JsonPropertyName("point")] Point3dDto? Point);

internal sealed record XdataArgsDto(
    [property: JsonPropertyName("handle")]  string? Handle,
    [property: JsonPropertyName("appName")] string? AppName,
    [property: JsonPropertyName("data")]    List<DataValueDto>? Data);

internal sealed record DictArgsDto(
    [property: JsonPropertyName("handle")]           string? Handle,
    [property: JsonPropertyName("path")]             string? Path,
    [property: JsonPropertyName("key")]              string? Key,
    [property: JsonPropertyName("data")]             List<DataValueDto>? Data,
    [property: JsonPropertyName("nested")]           bool? Nested,
    [property: JsonPropertyName("force")]            bool? Force,
    [property: JsonPropertyName("xlateReferences")]  bool? XlateReferences);

internal sealed record TagArgsDto(
    [property: JsonPropertyName("handles")] List<string>? Handles,
    [property: JsonPropertyName("tag")]     string? Tag,
    [property: JsonPropertyName("value")]   string? Value);

internal sealed record QueryArgsDto(
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("objectClass")] string? ObjectClass,
    [property: JsonPropertyName("colorIndex")]  int? ColorIndex,
    [property: JsonPropertyName("linetype")]    string? Linetype,
    [property: JsonPropertyName("hasXdataApp")] string? HasXdataApp);

internal sealed record TableCsvArgsDto(
    [property: JsonPropertyName("handle")]    string? Handle,
    [property: JsonPropertyName("path")]      string? Path,
    [property: JsonPropertyName("overwrite")] bool? Overwrite);

internal sealed record DataLinkArgsDto(
    [property: JsonPropertyName("name")]        string? Name,
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("range")]       string? Range,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("adapter")]     string? Adapter,
    [property: JsonPropertyName("handle")]      string? Handle,
    [property: JsonPropertyName("row")]         int? Row,
    [property: JsonPropertyName("column")]      int? Column,
    [property: JsonPropertyName("direction")]   string? Direction);
