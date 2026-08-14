// DTOs for the acad-viewports category. Wire names are [JsonPropertyName]; see rule 22.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Viewports;

public sealed record EmptyViewportArgs();

public sealed record CreateViewportArgs(
    [property: JsonPropertyName("layoutName")]   string LayoutName,
    [property: JsonPropertyName("center")]       Point3dDto Center,
    [property: JsonPropertyName("width")]        double Width,
    [property: JsonPropertyName("height")]       double Height,
    [property: JsonPropertyName("scale")]        double? Scale = null,
    [property: JsonPropertyName("layer")]        string? Layer = null,
    // Model-space point to pan to (sets Viewport.ViewCenter). Without it, a freshly created
    // viewport's pan target defaults near the origin regardless of where the drawing actually
    // sits - see the plugin-side ViewportsDtos.cs note for the live-confirmed symptom. This is
    // the Backend-side half of the same two-hop DTO (rule 35 §11): SelectionProxy-style proxies
    // re-serialize THIS record before forwarding to the plugin, so a field added only on one
    // side is silently dropped - both were updated together here.
    [property: JsonPropertyName("modelCenter")]  Point2dDto? ModelCenter = null);

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

// ucs is deliberately non-nullable and has no default. Everywhere else in this codebase an
// absent `ucs` means WCS (rule 43); on the tool whose only job is to set the coordinate
// system, that default would silently undo a deliberate setting. Pass "world" to clear.
public sealed record SetViewportUcsArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("ucs")]    string Ucs);

public sealed record SetViewportAnnotationScaleArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("scaleName")]     string ScaleName,
    [property: JsonPropertyName("syncViewScale")] bool SyncViewScale = true);

public sealed record ClipViewportByObjectArgs(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("clipHandle")] string ClipHandle);

public sealed record SetViewportViewDirectionArgs(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("preset")]    string? Preset = null,
    [property: JsonPropertyName("direction")] Point3dDto? Direction = null);

public sealed record SetViewportTwistArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("angleDeg")] double AngleDeg);

public sealed record SetViewportVisualStyleArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("style")]  string Style);

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
    [property: JsonPropertyName("overriddenLayers")] IReadOnlyList<string> OverriddenLayers,
    // These two must be declared here or System.Text.Json drops them on the way out of the
    // backend and the client sees a viewport with no UCS and no annotation scale, reported as
    // a success. That has happened three times in this codebase; the DTO is the first place to
    // look when a field the plugin definitely sent fails to arrive.
    [property: JsonPropertyName("ucs")]              ViewportUcsInfo? Ucs = null,
    [property: JsonPropertyName("annotationScale")]  string? AnnotationScale = null);

public sealed record ViewportUcsInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("perViewport")] bool PerViewport,
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("xAxis")]       Point3dDto XAxis,
    [property: JsonPropertyName("yAxis")]       Point3dDto YAxis);

public sealed record ViewportAnnotationScaleInfo(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("paperUnits")]   double PaperUnits,
    [property: JsonPropertyName("drawingUnits")] double DrawingUnits,
    [property: JsonPropertyName("scaleFactor")]  double ScaleFactor);

public sealed record ViewportResult(
    [property: JsonPropertyName("viewport")] ViewportInfo Viewport);

public sealed record ViewportUcsResult(
    [property: JsonPropertyName("viewport")] ViewportInfo Viewport,
    [property: JsonPropertyName("ucs")]      ViewportUcsInfo Ucs);

public sealed record ViewportAnnotationScaleResult(
    [property: JsonPropertyName("viewport")]         ViewportInfo Viewport,
    [property: JsonPropertyName("annotationScale")]  ViewportAnnotationScaleInfo AnnotationScale,
    [property: JsonPropertyName("viewScaleSynced")]  bool ViewScaleSynced,
    [property: JsonPropertyName("appliedViewScale")] double? AppliedViewScale);

public sealed record ViewportClipResult(
    [property: JsonPropertyName("viewport")]   ViewportInfo Viewport,
    [property: JsonPropertyName("clipHandle")] string ClipHandle);

public sealed record ViewportViewDirectionResult(
    [property: JsonPropertyName("viewport")]      ViewportInfo Viewport,
    [property: JsonPropertyName("viewDirection")] Point3dDto ViewDirection);

public sealed record ViewportTwistResult(
    [property: JsonPropertyName("viewport")] ViewportInfo Viewport,
    [property: JsonPropertyName("twistDeg")] double TwistDeg);

public sealed record ViewportSyncScaleResult(
    [property: JsonPropertyName("viewport")]            ViewportInfo Viewport,
    [property: JsonPropertyName("annotationScaleName")] string AnnotationScaleName,
    [property: JsonPropertyName("customScaleBefore")]   double CustomScaleBefore,
    [property: JsonPropertyName("customScaleAfter")]    double CustomScaleAfter,
    [property: JsonPropertyName("changed")]             bool Changed);

public sealed record ViewportVisualStyleResult(
    [property: JsonPropertyName("viewport")]    ViewportInfo Viewport,
    [property: JsonPropertyName("visualStyle")] string VisualStyle);

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
