// Typed DTOs for the acad-layouts category.
// Mirrors the wire shape consumed by the plugin under "acad.layouts.<verb>".
// See rule 19, rule 28-acad-blocks-layers-files-traps.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Layouts;

public sealed record LayoutsEmptyArgs();

public sealed record LayoutNameArg(
    [property: JsonPropertyName("name")] string Name);

public sealed record CreateLayoutArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("setCurrent")] bool SetCurrent = false);

public sealed record RenameLayoutArgs(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

public sealed record CreateViewportArgs(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("center")]     Point3dDto Center,
    [property: JsonPropertyName("width")]      double Width,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("scale")]      double Scale = 0.0,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record SetViewportScaleArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("scale")]  double Scale);

public sealed record ConfigurePlotArgs(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("plotter")]    string? Plotter = null,
    [property: JsonPropertyName("paperSize")]  string? PaperSize = null,
    [property: JsonPropertyName("plotStyle")]  string? PlotStyle = null,
    [property: JsonPropertyName("rotation")]   int Rotation = 0);

// ─────────── results ───────────

public sealed record LayoutInfo(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("tabOrder")]  int TabOrder,
    [property: JsonPropertyName("isCurrent")] bool IsCurrent,
    [property: JsonPropertyName("plotter")]   string? Plotter,
    [property: JsonPropertyName("paperSize")] string? PaperSize);

public sealed record LayoutListResult(
    [property: JsonPropertyName("layouts")] IReadOnlyList<LayoutInfo> Layouts,
    [property: JsonPropertyName("current")] string Current);

public sealed record LayoutResult(
    [property: JsonPropertyName("layout")] LayoutInfo Layout);

public sealed record LayoutEntityResult(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

public sealed record LayoutAffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

public sealed record ListPaperSizesArgs(
    [property: JsonPropertyName("plotter")] string? Plotter = null);

public sealed record PaperSizeInfo(
    [property: JsonPropertyName("canonical")] string Canonical,
    [property: JsonPropertyName("locale")]    string? Locale);

public sealed record ListPaperSizesResult(
    [property: JsonPropertyName("plotters")]           IReadOnlyList<string> Plotters,
    [property: JsonPropertyName("plotter")]            string Plotter,
    [property: JsonPropertyName("sizes")]              IReadOnlyList<PaperSizeInfo> Sizes,
    [property: JsonPropertyName("currentLayoutPaper")] string? CurrentLayoutPaper,
    [property: JsonPropertyName("currentLayoutPlotter")] string? CurrentLayoutPlotter);
