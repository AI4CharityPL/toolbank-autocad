// Plugin-side DTOs for the acad-files category.
// Mirror Backend/Categories/Files/FilesDtos.cs.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record FilesEmptyArgsDto();

internal sealed record OpenDocumentArgsDto(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("readOnly")] bool ReadOnly = false,
    [property: JsonPropertyName("password")] string? Password = null);

internal sealed record SaveAsArgsDto(
    [property: JsonPropertyName("path")]       string Path,
    [property: JsonPropertyName("dwgVersion")] string? DwgVersion = null);

internal sealed record CloseDocumentArgsDto(
    [property: JsonPropertyName("path")] string? Path = null,
    [property: JsonPropertyName("save")] bool SaveBeforeClose = false);

internal sealed record ImportFileArgsDto(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("insertion")] Point3dDto? Insertion = null);

internal sealed record ExportFileArgsDto(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("format")]   string Format,
    [property: JsonPropertyName("layout")]   string? Layout = null,
    [property: JsonPropertyName("scope")]    string? Scope = null,
    [property: JsonPropertyName("window")]   WindowAreaDto? Window = null,
    [property: JsonPropertyName("widthPx")]  int? WidthPx = null,
    [property: JsonPropertyName("heightPx")] int? HeightPx = null);

internal sealed record WindowAreaDto(
    [property: JsonPropertyName("xMin")] double XMin,
    [property: JsonPropertyName("yMin")] double YMin,
    [property: JsonPropertyName("xMax")] double XMax,
    [property: JsonPropertyName("yMax")] double YMax);

internal sealed record AuditArgsDto(
    [property: JsonPropertyName("fix")] bool Fix = false);

internal sealed record DocumentInfoDto(
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("isReadOnly")]  bool IsReadOnly,
    [property: JsonPropertyName("isModified")]  bool IsModified,
    [property: JsonPropertyName("dwgVersion")]  string? DwgVersion,
    [property: JsonPropertyName("entityCount")] int EntityCount);
