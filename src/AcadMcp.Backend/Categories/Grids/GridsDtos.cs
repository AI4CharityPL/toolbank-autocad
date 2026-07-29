// Typed DTOs for acad-grids.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Grids;

public sealed record DrawGridArgs(
    [property: JsonPropertyName("origin")]         Point2dDto Origin,
    [property: JsonPropertyName("xSpacingsMm")]    IReadOnlyList<double> XSpacingsMm,
    [property: JsonPropertyName("ySpacingsMm")]    IReadOnlyList<double> YSpacingsMm,
    [property: JsonPropertyName("extendMm")]       double ExtendMm = GridsPalette.DefaultExtendMm,
    [property: JsonPropertyName("bubbleRadiusMm")] double BubbleRadiusMm = GridsPalette.DefaultBubbleRadiusMm,
    [property: JsonPropertyName("xAxisLabels")]    IReadOnlyList<string>? XAxisLabels = null,
    [property: JsonPropertyName("yAxisLabels")]    IReadOnlyList<string>? YAxisLabels = null,
    [property: JsonPropertyName("bubblesNorth")]   bool BubblesNorth = true,
    [property: JsonPropertyName("bubblesSouth")]   bool BubblesSouth = false,
    [property: JsonPropertyName("bubblesEast")]    bool BubblesEast = false,
    [property: JsonPropertyName("bubblesWest")]    bool BubblesWest = true,
    [property: JsonPropertyName("axisLayer")]      string AxisLayer = GridsPalette.LayerAxisMajor,
    [property: JsonPropertyName("bubbleLayer")]    string BubbleLayer = GridsPalette.LayerBubble);

public sealed record AxisEntity(
    [property: JsonPropertyName("label")]    string Label,
    [property: JsonPropertyName("axisLine")] EntityHandle AxisLine,
    [property: JsonPropertyName("bubbles")]  IReadOnlyList<EntityHandle> Bubbles);

public sealed record DrawGridResult(
    [property: JsonPropertyName("xAxes")]     IReadOnlyList<AxisEntity> XAxes,
    [property: JsonPropertyName("yAxes")]     IReadOnlyList<AxisEntity> YAxes,
    [property: JsonPropertyName("originUsed")] Point2dDto OriginUsed,
    [property: JsonPropertyName("gridBoxMin")] Point2dDto GridBoxMin,
    [property: JsonPropertyName("gridBoxMax")] Point2dDto GridBoxMax);

public sealed record AddGridAxisArgs(
    [property: JsonPropertyName("start")]          Point2dDto Start,
    [property: JsonPropertyName("end")]            Point2dDto End,
    [property: JsonPropertyName("label")]          string Label,
    [property: JsonPropertyName("bubbleAtStart")]  bool BubbleAtStart = false,
    [property: JsonPropertyName("bubbleAtEnd")]    bool BubbleAtEnd = true,
    [property: JsonPropertyName("bubbleRadiusMm")] double BubbleRadiusMm = GridsPalette.DefaultBubbleRadiusMm,
    [property: JsonPropertyName("extendMm")]       double ExtendMm = GridsPalette.DefaultExtendMm,
    [property: JsonPropertyName("axisLayer")]      string AxisLayer = GridsPalette.LayerAxisMajor,
    [property: JsonPropertyName("bubbleLayer")]    string BubbleLayer = GridsPalette.LayerBubble);

public sealed record AddGridAxisResult(
    [property: JsonPropertyName("axisLine")] EntityHandle AxisLine,
    [property: JsonPropertyName("bubbles")]  IReadOnlyList<EntityHandle> Bubbles);

public sealed record AddGridBubbleArgs(
    [property: JsonPropertyName("center")]   Point2dDto Center,
    [property: JsonPropertyName("label")]    string Label,
    [property: JsonPropertyName("radiusMm")] double RadiusMm = GridsPalette.DefaultBubbleRadiusMm,
    [property: JsonPropertyName("layer")]    string Layer = GridsPalette.LayerBubble);

public sealed record AddGridBubbleResult(
    [property: JsonPropertyName("circle")] EntityHandle Circle,
    [property: JsonPropertyName("label")]  EntityHandle LabelHandle);

public sealed record ListGridAxesArgs(
    [property: JsonPropertyName("axisLayer")]   string AxisLayer = GridsPalette.LayerAxisMajor,
    [property: JsonPropertyName("bubbleLayer")] string BubbleLayer = GridsPalette.LayerBubble);

public sealed record ListGridAxesResult(
    [property: JsonPropertyName("axisHandles")]   IReadOnlyList<string> AxisHandles,
    [property: JsonPropertyName("bubbleHandles")] IReadOnlyList<string> BubbleHandles,
    [property: JsonPropertyName("totalAxes")]     int TotalAxes,
    [property: JsonPropertyName("totalBubbles")]  int TotalBubbles);

public sealed record SnapToGridArgs(
    [property: JsonPropertyName("point")]        Point2dDto Point,
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("xSpacingsMm")]  IReadOnlyList<double> XSpacingsMm,
    [property: JsonPropertyName("ySpacingsMm")]  IReadOnlyList<double> YSpacingsMm,
    [property: JsonPropertyName("xAxisLabels")]  IReadOnlyList<string>? XAxisLabels = null,
    [property: JsonPropertyName("yAxisLabels")]  IReadOnlyList<string>? YAxisLabels = null);

public sealed record SnapToGridResult(
    [property: JsonPropertyName("snapped")]     Point2dDto Snapped,
    [property: JsonPropertyName("xLabel")]      string XLabel,
    [property: JsonPropertyName("yLabel")]      string YLabel,
    [property: JsonPropertyName("xIndex")]      int XIndex,
    [property: JsonPropertyName("yIndex")]      int YIndex,
    [property: JsonPropertyName("distanceMm")]  double DistanceMm,
    [property: JsonPropertyName("cellLabel")]   string CellLabel);

public sealed record DeleteGridArgs(
    [property: JsonPropertyName("axisLayer")]   string AxisLayer = GridsPalette.LayerAxisMajor,
    [property: JsonPropertyName("bubbleLayer")] string BubbleLayer = GridsPalette.LayerBubble,
    [property: JsonPropertyName("handles")]     IReadOnlyList<string>? Handles = null);

public sealed record DeleteGridResult(
    [property: JsonPropertyName("erased")]      int Erased,
    [property: JsonPropertyName("eraseReason")] string EraseReason);
