// DTOs for the acad-viewports category. Wire names are [JsonPropertyName]; see rule 22.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Viewports;

public sealed record EmptyViewportArgs();

public sealed record CreateViewportArgs(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("center")]     Point3dDto Center,
    [property: JsonPropertyName("width")]      double Width,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("scale")]      double? Scale = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record CreatePolygonalViewportArgs(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("vertices")]   IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("scale")]      double? Scale = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record ViewportHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record SetViewportScaleArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("scale")]  double Scale);

public sealed record SetViewportLockArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("locked")] bool Locked);

public sealed record SetViewportShadePlotArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("mode")]   string Mode);

public sealed record LayoutNameArgs(
    [property: JsonPropertyName("layoutName")] string? LayoutName = null);

public sealed record ViewportLayerVisibilityArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("layers")] IReadOnlyList<string> Layers);

public sealed record ViewportLayerOverrideArgs(
    [property: JsonPropertyName("handle")]           string Handle,
    [property: JsonPropertyName("layer")]            string Layer,
    [property: JsonPropertyName("color")]            ColorDto? Color = null,
    [property: JsonPropertyName("linetype")]         string? Linetype = null,
    [property: JsonPropertyName("lineweightMm")]     double? LineweightMm = null,
    [property: JsonPropertyName("transparencyPct")]  int? TransparencyPct = null);

public sealed record ClearViewportOverridesArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("layer")]  string? Layer = null);

// ─────────────── results ───────────────

public sealed record ViewportInfo(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("layoutName")]   string LayoutName,
    [property: JsonPropertyName("number")]       int Number,
    [property: JsonPropertyName("centerPaper")]  Point3dDto CenterPaper,
    [property: JsonPropertyName("widthPaper")]   double WidthPaper,
    [property: JsonPropertyName("heightPaper")]  double HeightPaper,
    [property: JsonPropertyName("customScale")]  double CustomScale,
    [property: JsonPropertyName("scaleLabel")]   string ScaleLabel,
    [property: JsonPropertyName("locked")]       bool Locked,
    [property: JsonPropertyName("on")]           bool On,
    [property: JsonPropertyName("layer")]        string Layer,
    [property: JsonPropertyName("shadePlot")]    string ShadePlot,
    [property: JsonPropertyName("isPolygonal")]  bool IsPolygonal,
    [property: JsonPropertyName("frozenLayers")] IReadOnlyList<string> FrozenLayers,
    [property: JsonPropertyName("overriddenLayers")] IReadOnlyList<string> OverriddenLayers);

public sealed record ViewportResult(
    [property: JsonPropertyName("viewport")] ViewportInfo Viewport);

public sealed record ViewportListResult(
    [property: JsonPropertyName("viewports")] IReadOnlyList<ViewportInfo> Viewports,
    [property: JsonPropertyName("count")]     int Count);

public sealed record ViewportExtentsResult(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("modelMin")]    Point2dDto ModelMin,
    [property: JsonPropertyName("modelMax")]    Point2dDto ModelMax,
    [property: JsonPropertyName("customScale")] double CustomScale);

public sealed record LayerOverrideEntry(
    [property: JsonPropertyName("layer")]           string Layer,
    [property: JsonPropertyName("frozen")]          bool Frozen,
    [property: JsonPropertyName("color")]           ColorDto? Color,
    [property: JsonPropertyName("linetype")]        string? Linetype,
    [property: JsonPropertyName("lineweightMm")]    double? LineweightMm,
    [property: JsonPropertyName("transparencyPct")] int? TransparencyPct);

public sealed record ViewportOverridesResult(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("overrides")] IReadOnlyList<LayerOverrideEntry> Overrides,
    [property: JsonPropertyName("count")]     int Count);

public sealed record ViewportAffected(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("handle")]   string? Handle = null);
