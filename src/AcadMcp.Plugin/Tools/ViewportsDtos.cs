// Plugin-side DTOs for acad-viewports. Wire names must match the backend's ViewportsDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record CreatePolyViewportArgsDto(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("vertices")]   IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("scale")]      double? Scale = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

internal sealed record VpHandleArgsDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record LayoutNameArgsDto(
    [property: JsonPropertyName("layoutName")] string? LayoutName = null);

internal sealed record VpFlagArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("locked")] bool Locked);

internal sealed record VpShadeArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("mode")]   string Mode);

internal sealed record VpLayersArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("layers")] IReadOnlyList<string> Layers);

internal sealed record VpLayerOverrideArgsDto(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("layer")]           string Layer,
    [property: JsonPropertyName("color")]           ColorDto? Color = null,
    [property: JsonPropertyName("linetype")]        string? Linetype = null,
    [property: JsonPropertyName("lineweightMm")]    double? LineweightMm = null,
    [property: JsonPropertyName("transparencyPct")] int? TransparencyPct = null);

internal sealed record VpClearArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("layer")]  string? Layer = null);

internal sealed record CreateRectViewportArgsDto(
    [property: JsonPropertyName("layoutName")]   string LayoutName,
    [property: JsonPropertyName("center")]       Point3dDto Center,
    [property: JsonPropertyName("width")]        double Width,
    [property: JsonPropertyName("height")]       double Height,
    [property: JsonPropertyName("scale")]        double? Scale = null,
    [property: JsonPropertyName("layer")]        string? Layer = null,
    // Model-space point to pan to (Viewport.ViewCenter). Without this, a freshly created
    // Viewport defaults ViewCenter near the origin regardless of where the drawing actually
    // is - confirmed live: a viewport created over a building at x0-21500/y0-21500 showed a
    // 55000x45000mm model window centred near (148,105), leaving most of the paper blank and
    // the building crowded into one corner. Optional so existing callers keep today's
    // behaviour; pass the drawing's own extents centre to actually frame it.
    [property: JsonPropertyName("modelCenter")]  Point2dDto? ModelCenter = null);

internal sealed record VpScaleArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("scale")]  double Scale);

// ucs is required, unlike the optional `ucs` on drawing tools. On a tool whose whole purpose
// is to set the coordinate system, an absent argument means the caller has not said what they
// want; defaulting to WCS would silently undo a deliberate setting. See rule 43.
internal sealed record VpUcsArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("ucs")]    string Ucs);

internal sealed record VpClipByObjectArgsDto(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("clipHandle")]  string ClipHandle);

internal sealed record VpViewDirectionArgsDto(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("preset")]    string? Preset = null,
    [property: JsonPropertyName("direction")] Point3dDto? Direction = null);

internal sealed record VpTwistArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("angleDeg")] double AngleDeg);

internal sealed record VpVisualStyleArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("style")]  string Style);

internal sealed record VpAnnotationScaleArgsDto(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("scaleName")]     string ScaleName,
    [property: JsonPropertyName("syncViewScale")] bool SyncViewScale = true);
