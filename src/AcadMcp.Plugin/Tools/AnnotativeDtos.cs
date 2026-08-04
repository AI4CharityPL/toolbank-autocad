// Plugin-side DTOs for acad-annotative. Wire names must match the backend's AnnotativeDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record AnnoSetArgsDto(
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("annotative")] bool Annotative = true);

internal sealed record AnnoObjScalesArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("scales")]  IReadOnlyList<string> Scales);

internal sealed record AnnoHandlesArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

internal sealed record AnnoScaleNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record AnnoAddScaleArgsDto(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("paperUnits")]   double PaperUnits = 1.0,
    [property: JsonPropertyName("drawingUnits")] double DrawingUnits = 1.0,
    [property: JsonPropertyName("makeCurrent")]  bool MakeCurrent = false);

internal sealed record AnnoFlagArgsDto(
    [property: JsonPropertyName("enabled")] bool Enabled);

internal sealed record AnnoScaleFilterArgsDto(
    [property: JsonPropertyName("scale")] string? Scale = null);
