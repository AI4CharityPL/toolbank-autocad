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
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("center")]     Point3dDto Center,
    [property: JsonPropertyName("width")]      double Width,
    [property: JsonPropertyName("height")]     double Height,
    [property: JsonPropertyName("scale")]      double? Scale = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

internal sealed record VpScaleArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("scale")]  double Scale);

// ucs is required, unlike the optional `ucs` on drawing tools. On a tool whose whole purpose
// is to set the coordinate system, an absent argument means the caller has not said what they
// want; defaulting to WCS would silently undo a deliberate setting. See rule 43.
internal sealed record VpUcsArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("ucs")]    string Ucs);

internal sealed record VpAnnotationScaleArgsDto(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("scaleName")]     string ScaleName,
    [property: JsonPropertyName("syncViewScale")] bool SyncViewScale = true);
