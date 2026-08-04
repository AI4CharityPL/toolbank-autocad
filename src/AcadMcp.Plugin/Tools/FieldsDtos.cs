// Plugin-side DTOs for acad-fields. Wire names must match the backend's FieldsDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record FieldPlacementArgsDto(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

internal sealed record FieldDateArgsDto(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("format")]    string Format = "yyyy-MM-dd",
    [property: JsonPropertyName("kind")]      string Kind = "create",
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

internal sealed record FieldFilenameArgsDto(
    [property: JsonPropertyName("position")]         Point3dDto Position,
    [property: JsonPropertyName("includePath")]      bool IncludePath = false,
    [property: JsonPropertyName("includeExtension")] bool IncludeExtension = false,
    [property: JsonPropertyName("height")]           double? Height = null,
    [property: JsonPropertyName("layer")]            string? Layer = null,
    [property: JsonPropertyName("textStyle")]        string? TextStyle = null,
    [property: JsonPropertyName("prefix")]           string? Prefix = null,
    [property: JsonPropertyName("suffix")]           string? Suffix = null);

internal sealed record FieldObjectPropertyArgsDto(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("property")]  string Property,
    [property: JsonPropertyName("format")]    string? Format = null,
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

internal sealed record FieldSysVarArgsDto(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("variable")]  string Variable,
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

internal sealed record FieldRawArgsDto(
    [property: JsonPropertyName("position")]   Point3dDto Position,
    [property: JsonPropertyName("expression")] string Expression,
    [property: JsonPropertyName("height")]     double? Height = null,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    [property: JsonPropertyName("textStyle")]  string? TextStyle = null);

internal sealed record FieldHandleArgsDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record UpdateFieldsArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string>? Handles = null);

internal sealed record FieldEvalModeArgsDto(
    [property: JsonPropertyName("onOpen")]  bool OnOpen = true,
    [property: JsonPropertyName("onSave")]  bool OnSave = true,
    [property: JsonPropertyName("onPlot")]  bool OnPlot = true,
    [property: JsonPropertyName("onRegen")] bool OnRegen = true);
