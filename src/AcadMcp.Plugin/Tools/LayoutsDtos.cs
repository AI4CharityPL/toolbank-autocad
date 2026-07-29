// Plugin-side DTOs for the acad-layouts category.
// Mirror Backend/Categories/Layouts/LayoutsDtos.cs.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record LayoutsEmptyArgsDto();

internal sealed record LayoutNameArgDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record CreateLayoutArgsDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("setCurrent")] bool SetCurrent = false);

internal sealed record RenameLayoutArgsDto(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

internal sealed record CreateViewportArgsDto(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("center")]     Point3dDto Center,
    [property: JsonPropertyName("width")]      double Width,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("scale")]      double Scale = 0.0,
    [property: JsonPropertyName("layer")]      string? Layer = null);

internal sealed record SetViewportScaleArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("scale")]  double Scale);

internal sealed record ConfigurePlotArgsDto(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("plotter")]    string? Plotter = null,
    [property: JsonPropertyName("paperSize")]  string? PaperSize = null,
    [property: JsonPropertyName("plotStyle")]  string? PlotStyle = null,
    [property: JsonPropertyName("rotation")]   int Rotation = 0);

internal sealed record LayoutInfoDto(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("tabOrder")]  int TabOrder,
    [property: JsonPropertyName("isCurrent")] bool IsCurrent,
    [property: JsonPropertyName("plotter")]   string? Plotter,
    [property: JsonPropertyName("paperSize")] string? PaperSize);

internal sealed record ListPaperSizesArgsDto(
    [property: JsonPropertyName("plotter")] string? Plotter = null);

internal sealed record PaperSizeInfoDto(
    [property: JsonPropertyName("canonical")] string Canonical,
    [property: JsonPropertyName("locale")]    string? Locale);
