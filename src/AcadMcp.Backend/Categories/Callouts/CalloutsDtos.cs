// DTOs for the 5 composite tools in the acad-callouts category.
// Drawing-unit is millimetre. Text/symbol sizes follow plotMm × scaleFactor
// per CalloutsPalette so callers can declare "1:100" once and let the palette
// resolve the final drawing-unit size (rule 69).

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Callouts;

// ─────────── shared ───────────

public sealed record CalloutResultSummary(
    [property: JsonPropertyName("layer")]   string Layer,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("notes")]   IReadOnlyList<string> Notes);

// ─────────── insert_north_arrow ───────────

public sealed record InsertNorthArrowArgs(
    [property: JsonPropertyName("position")]     Point2dDto Position,
    [property: JsonPropertyName("scale")]        string Scale = "1:100",
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]        string Layer = CalloutsPalette.LayerNorth,
    [property: JsonPropertyName("label")]        string Label = "N",
    [property: JsonPropertyName("textHeightPlotMm")] double TextHeightPlotMm = CalloutsPalette.PlotMidTextMm,
    [property: JsonPropertyName("filledArrow")]  bool FilledArrow = true);

public sealed record InsertNorthArrowResult(
    [property: JsonPropertyName("summary")]   CalloutResultSummary Summary,
    [property: JsonPropertyName("diameterMm")] double DiameterMm,
    [property: JsonPropertyName("scaleFactor")] int ScaleFactor);

// ─────────── insert_scale_bar ───────────

public sealed record InsertScaleBarArgs(
    [property: JsonPropertyName("position")]        Point2dDto Position,
    [property: JsonPropertyName("scale")]           string Scale = "1:100",
    [property: JsonPropertyName("layer")]           string Layer = CalloutsPalette.LayerSbar,
    [property: JsonPropertyName("segmentMeters")]   double? SegmentMeters = null,   // override preset
    [property: JsonPropertyName("segmentCount")]    int? SegmentCount = null,       // override preset
    [property: JsonPropertyName("showScaleText")]   bool ShowScaleText = true,
    [property: JsonPropertyName("textHeightPlotMm")] double TextHeightPlotMm = CalloutsPalette.PlotSmallTextMm,
    [property: JsonPropertyName("labelHeightPlotMm")] double LabelHeightPlotMm = CalloutsPalette.PlotMidTextMm);

public sealed record InsertScaleBarResult(
    [property: JsonPropertyName("summary")]    CalloutResultSummary Summary,
    [property: JsonPropertyName("totalLengthMm")] double TotalLengthMm,
    [property: JsonPropertyName("segmentMeters")] double SegmentMeters,
    [property: JsonPropertyName("segmentCount")]  int SegmentCount,
    [property: JsonPropertyName("scaleFactor")]   int ScaleFactor);

// ─────────── insert_section_callout ───────────

public sealed record InsertSectionCalloutArgs(
    [property: JsonPropertyName("startPoint")]     Point2dDto StartPoint,
    [property: JsonPropertyName("endPoint")]       Point2dDto EndPoint,
    [property: JsonPropertyName("label")]          string Label = "A",
    [property: JsonPropertyName("scale")]          string Scale = "1:100",
    [property: JsonPropertyName("layer")]          string Layer = CalloutsPalette.LayerSymb,
    [property: JsonPropertyName("drawCutLine")]    bool DrawCutLine = true,
    [property: JsonPropertyName("cutLinetype")]    string CutLinetype = "DASHED2",
    [property: JsonPropertyName("viewDirection")]  string ViewDirection = "right", // right|left
    [property: JsonPropertyName("sheetReference")] string? SheetReference = null,
    [property: JsonPropertyName("markerDiameterPlotMm")] double MarkerDiameterPlotMm = CalloutsPalette.PlotSectionMarkerDiameterMm,
    [property: JsonPropertyName("textHeightPlotMm")]      double TextHeightPlotMm = CalloutsPalette.PlotMidTextMm);

public sealed record InsertSectionCalloutResult(
    [property: JsonPropertyName("summary")]   CalloutResultSummary Summary,
    [property: JsonPropertyName("label")]     string Label,
    [property: JsonPropertyName("lengthMm")]  double LengthMm,
    [property: JsonPropertyName("scaleFactor")] int ScaleFactor);

// ─────────── insert_detail_callout ───────────

public sealed record InsertDetailCalloutArgs(
    [property: JsonPropertyName("center")]          Point2dDto Center,
    [property: JsonPropertyName("radiusMm")]        double RadiusMm,
    [property: JsonPropertyName("label")]           string Label = "1",
    [property: JsonPropertyName("scale")]           string Scale = "1:50",
    [property: JsonPropertyName("targetScale")]     string? TargetScale = null,   // detail-sheet scale, defaults to args.Scale
    [property: JsonPropertyName("layer")]           string Layer = CalloutsPalette.LayerSymb,
    [property: JsonPropertyName("leaderEndPoint")]  Point2dDto? LeaderEndPoint = null,
    [property: JsonPropertyName("sheetReference")]  string? SheetReference = null,
    [property: JsonPropertyName("markerDiameterPlotMm")] double MarkerDiameterPlotMm = CalloutsPalette.PlotDetailMarkerDiameterMm,
    [property: JsonPropertyName("textHeightPlotMm")]     double TextHeightPlotMm = CalloutsPalette.PlotMidTextMm);

public sealed record InsertDetailCalloutResult(
    [property: JsonPropertyName("summary")]     CalloutResultSummary Summary,
    [property: JsonPropertyName("label")]       string Label,
    [property: JsonPropertyName("radiusMm")]    double RadiusMm,
    [property: JsonPropertyName("scaleFactor")] int ScaleFactor);

// ─────────── insert_title_block ───────────

public sealed record TitleBlockField(
    [property: JsonPropertyName("key")]   string Key,
    [property: JsonPropertyName("value")] string Value);

public sealed record InsertTitleBlockArgs(
    [property: JsonPropertyName("bottomLeft")]   Point2dDto BottomLeft,
    [property: JsonPropertyName("sheetSize")]    string SheetSize = "A1",
    [property: JsonPropertyName("scale")]        string Scale = "1:100",
    [property: JsonPropertyName("layer")]        string Layer = CalloutsPalette.LayerTtlb,
    [property: JsonPropertyName("borderLayer")]  string BorderLayer = CalloutsPalette.LayerBorder,
    [property: JsonPropertyName("drawBorder")]   bool DrawBorder = true,
    [property: JsonPropertyName("fields")]       IReadOnlyList<TitleBlockField>? Fields = null,
    [property: JsonPropertyName("titleText")]    string? TitleText = null,
    [property: JsonPropertyName("projectName")]  string? ProjectName = null,
    [property: JsonPropertyName("sheetNumber")]  string? SheetNumber = null,
    [property: JsonPropertyName("author")]       string? Author = null,
    [property: JsonPropertyName("date")]         string? Date = null,
    [property: JsonPropertyName("titleHeightPlotMm")] double TitleHeightPlotMm = CalloutsPalette.PlotBigTextMm,
    [property: JsonPropertyName("fieldHeightPlotMm")] double FieldHeightPlotMm = CalloutsPalette.PlotSmallTextMm);

public sealed record InsertTitleBlockResult(
    [property: JsonPropertyName("summary")]       CalloutResultSummary Summary,
    [property: JsonPropertyName("sheetSize")]     string SheetSize,
    [property: JsonPropertyName("sheetWidthMm")]  double SheetWidthMm,
    [property: JsonPropertyName("sheetHeightMm")] double SheetHeightMm,
    [property: JsonPropertyName("scaleFactor")]   int ScaleFactor);
