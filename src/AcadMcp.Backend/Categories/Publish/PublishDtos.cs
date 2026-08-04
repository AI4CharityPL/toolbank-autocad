// DTOs for the acad-publish category. Wire names are [JsonPropertyName]; see rule 22.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Publish;

public sealed record EmptyPublishArgs();

public sealed record CreatePageSetupArgs(
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("fromLayout")]     string? FromLayout = null,
    [property: JsonPropertyName("device")]         string? Device = null,
    [property: JsonPropertyName("media")]          string? Media = null,
    [property: JsonPropertyName("plotStyleTable")] string? PlotStyleTable = null,
    [property: JsonPropertyName("rotation")]       int? Rotation = null,
    [property: JsonPropertyName("overwrite")]      bool Overwrite = false);

public sealed record PageSetupNameArgs(
    [property: JsonPropertyName("name")] string Name);

// Exactly one of layouts / allLayouts. There is no "all layouts" default on purpose - rule 44.
public sealed record ApplyPageSetupArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("layouts")]    IReadOnlyList<string>? Layouts = null,
    [property: JsonPropertyName("allLayouts")] bool AllLayouts = false);

// ─────────────── results ───────────────

public sealed record PageSetupInfo(
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("device")]           string? Device,
    [property: JsonPropertyName("media")]            string? Media,
    [property: JsonPropertyName("plotStyleTable")]   string? PlotStyleTable,
    [property: JsonPropertyName("plotType")]         string? PlotType,
    [property: JsonPropertyName("rotation")]         string? Rotation,
    [property: JsonPropertyName("centered")]         bool Centered,
    [property: JsonPropertyName("useStandardScale")] bool UseStandardScale,
    [property: JsonPropertyName("stdScaleType")]     string? StdScaleType,
    [property: JsonPropertyName("plotPaperUnits")]   string? PlotPaperUnits,
    [property: JsonPropertyName("modelType")]        bool ModelType);

public sealed record PageSetupResult(
    [property: JsonPropertyName("pageSetup")] PageSetupInfo PageSetup);

public sealed record PageSetupListResult(
    [property: JsonPropertyName("pageSetups")] IReadOnlyList<PageSetupInfo> PageSetups,
    [property: JsonPropertyName("count")]      int Count);

// Per-layout outcomes rather than a count: a page setup naming a device this machine lacks
// fails on some layouts and not others, and a partial success must read as one.
public sealed record PageSetupApplyOutcome(
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("error")]  string? Error);

public sealed record ApplyPageSetupResult(
    [property: JsonPropertyName("pageSetupName")] string PageSetupName,
    [property: JsonPropertyName("applied")]       int Applied,
    [property: JsonPropertyName("results")]       IReadOnlyList<PageSetupApplyOutcome> Results);

public sealed record DeletePageSetupResult(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("note")]     string? Note);


public sealed record PublishSheetsArgs(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("layouts")]   IReadOnlyList<string> Layouts,
    [property: JsonPropertyName("format")]    string Format = "PDF",
    [property: JsonPropertyName("pageSetup")] string? PageSetup = null,
    [property: JsonPropertyName("title")]     string? Title = null,
    [property: JsonPropertyName("allowUnsaved")] bool AllowUnsaved = false);

public sealed record PlotAreaArgs(
    [property: JsonPropertyName("layoutName")] string? LayoutName = null);

public sealed record PublishSheetsResult(
    [property: JsonPropertyName("path")]    string Path,
    [property: JsonPropertyName("format")]  string Format,
    [property: JsonPropertyName("sheets")]  int Sheets,
    [property: JsonPropertyName("layouts")] IReadOnlyList<string> Layouts,
    [property: JsonPropertyName("bytes")]   long Bytes);

public sealed record PlotAreaSize(
    [property: JsonPropertyName("width")]  double Width,
    [property: JsonPropertyName("height")] double Height);

public sealed record PlotAreaRect(
    [property: JsonPropertyName("xMin")] double XMin,
    [property: JsonPropertyName("yMin")] double YMin,
    [property: JsonPropertyName("xMax")] double XMax,
    [property: JsonPropertyName("yMax")] double YMax);

public sealed record PlotAreaScale(
    [property: JsonPropertyName("numerator")]   double Numerator,
    [property: JsonPropertyName("denominator")] double Denominator);

public sealed record PlotAreaResult(
    [property: JsonPropertyName("layout")]           string Layout,
    [property: JsonPropertyName("configured")]       bool Configured,
    [property: JsonPropertyName("note")]             string? Note,
    [property: JsonPropertyName("plotType")]         string PlotType,
    [property: JsonPropertyName("media")]            string? Media,
    [property: JsonPropertyName("device")]           string? Device,
    [property: JsonPropertyName("paperSize")]        PlotAreaSize PaperSize,
    [property: JsonPropertyName("margins")]          PlotAreaRect Margins,
    [property: JsonPropertyName("window")]           PlotAreaRect Window,
    [property: JsonPropertyName("rotation")]         string Rotation,
    [property: JsonPropertyName("centered")]         bool Centered,
    [property: JsonPropertyName("useStandardScale")] bool UseStandardScale,
    [property: JsonPropertyName("stdScaleType")]     string StdScaleType,
    [property: JsonPropertyName("customScale")]      PlotAreaScale CustomScale);

