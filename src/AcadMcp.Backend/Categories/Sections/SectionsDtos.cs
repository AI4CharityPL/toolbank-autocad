// DTOs for the 4 composite tools in the acad-sections category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Backend.Categories.Callouts;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Sections;

public sealed record SectionsResultSummary(
    [property: JsonPropertyName("layer")]   string Layer,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("notes")]   IReadOnlyList<string> Notes);

// ─────────── insert_section_line ───────────

public sealed record InsertSectionLineArgs(
    [property: JsonPropertyName("startPoint")]      Point2dDto StartPoint,
    [property: JsonPropertyName("endPoint")]        Point2dDto EndPoint,
    [property: JsonPropertyName("label")]           string Label = "A",
    [property: JsonPropertyName("scale")]           string Scale = "1:100",
    [property: JsonPropertyName("layer")]           string Layer = SectionsPalette.LayerSectionLine,
    [property: JsonPropertyName("drawEndMarkers")]  bool DrawEndMarkers = true,
    [property: JsonPropertyName("markerLayer")]     string MarkerLayer = CalloutsPalette.LayerSymb,
    [property: JsonPropertyName("drawOffsetTicks")] bool DrawOffsetTicks = true,
    [property: JsonPropertyName("applyDashedLinetype")] bool ApplyDashedLinetype = true,
    [property: JsonPropertyName("viewDirection")]   string ViewDirection = "right",
    [property: JsonPropertyName("sheetReference")]  string? SheetReference = null);

public sealed record InsertSectionLineResult(
    [property: JsonPropertyName("summary")]       SectionsResultSummary Summary,
    [property: JsonPropertyName("cutLineHandle")] string CutLineHandle,
    [property: JsonPropertyName("lengthMm")]      double LengthMm,
    [property: JsonPropertyName("label")]         string Label,
    [property: JsonPropertyName("scaleFactor")]   int ScaleFactor,
    [property: JsonPropertyName("markerHandles")] IReadOnlyList<string> MarkerHandles);

// ─────────── insert_section_title ───────────

public sealed record InsertSectionTitleArgs(
    [property: JsonPropertyName("position")]        Point2dDto Position,
    [property: JsonPropertyName("label")]           string Label = "A-A",
    [property: JsonPropertyName("scale")]           string Scale = "1:100",
    [property: JsonPropertyName("viewScale")]       string? ViewScale = null,  // defaults to args.Scale
    [property: JsonPropertyName("caption")]         string Caption = SectionsPalette.CaptionSection,
    [property: JsonPropertyName("layer")]           string Layer = SectionsPalette.LayerSectionTitle,
    [property: JsonPropertyName("drawUnderline")]   bool DrawUnderline = true,
    [property: JsonPropertyName("titleHeightPlotMm")] double TitleHeightPlotMm = CalloutsPalette.PlotBigTextMm,
    [property: JsonPropertyName("scaleTextHeightPlotMm")] double ScaleTextHeightPlotMm = CalloutsPalette.PlotMidTextMm);

public sealed record InsertSectionTitleResult(
    [property: JsonPropertyName("summary")]     SectionsResultSummary Summary,
    [property: JsonPropertyName("label")]       string Label,
    [property: JsonPropertyName("viewScale")]   string ViewScale,
    [property: JsonPropertyName("scaleFactor")] int ScaleFactor);

// ─────────── insert_elevation_marker ───────────

public sealed record InsertElevationMarkerArgs(
    [property: JsonPropertyName("position")]        Point2dDto Position,
    [property: JsonPropertyName("direction")]       string Direction = "N",
    [property: JsonPropertyName("label")]           string? Label = null,   // defaults to "ELEWACJA <direction>"
    [property: JsonPropertyName("scale")]           string Scale = "1:100",
    [property: JsonPropertyName("layer")]           string Layer = SectionsPalette.LayerElevationMarker,
    [property: JsonPropertyName("sheetReference")]  string? SheetReference = null,
    [property: JsonPropertyName("labelHeightPlotMm")] double LabelHeightPlotMm = CalloutsPalette.PlotMidTextMm);

public sealed record InsertElevationMarkerResult(
    [property: JsonPropertyName("summary")]     SectionsResultSummary Summary,
    [property: JsonPropertyName("direction")]   string Direction,
    [property: JsonPropertyName("directionDeg")] double DirectionDeg,
    [property: JsonPropertyName("scaleFactor")] int ScaleFactor);

// ─────────── list_section_lines ───────────

public sealed record ListSectionLinesArgs(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null);  // defaults to LayerSectionLine

public sealed record SectionLineEntry(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("layer")]   string Layer,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("lengthMm")] double? LengthMm);

public sealed record ListSectionLinesResult(
    [property: JsonPropertyName("entries")]      IReadOnlyList<SectionLineEntry> Entries,
    [property: JsonPropertyName("layerFilter")]  string LayerFilter,
    [property: JsonPropertyName("count")]        int Count);
