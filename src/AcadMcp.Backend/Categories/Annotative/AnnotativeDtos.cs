// DTOs for the acad-annotative category.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Annotative;

public sealed record EmptyAnnotativeArgs();

public sealed record SetAnnotativeArgs(
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("annotative")] bool Annotative = true);

public sealed record ObjectScalesArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("scales")]  IReadOnlyList<string> Scales);

public sealed record HandlesArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record HandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record ScaleNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record AddScaleArgs(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("paperUnits")]    double PaperUnits = 1.0,
    [property: JsonPropertyName("drawingUnits")]  double DrawingUnits = 1.0,
    [property: JsonPropertyName("makeCurrent")]   bool MakeCurrent = false);

public sealed record BoolFlagArgs(
    [property: JsonPropertyName("enabled")] bool Enabled);

public sealed record ScaleFilterArgs(
    [property: JsonPropertyName("scale")] string? Scale = null);

// ─────────────── results ───────────────

public sealed record ScaleInfo(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("paperUnits")]   double PaperUnits,
    [property: JsonPropertyName("drawingUnits")] double DrawingUnits,
    [property: JsonPropertyName("scaleFactor")]  double ScaleFactor,
    [property: JsonPropertyName("isCurrent")]    bool IsCurrent);

public sealed record ScaleListResult(
    [property: JsonPropertyName("scales")]  IReadOnlyList<ScaleInfo> Scales,
    [property: JsonPropertyName("current")] string Current,
    [property: JsonPropertyName("count")]   int Count);

public sealed record ScaleResult(
    [property: JsonPropertyName("scale")] ScaleInfo Scale);

public sealed record AnnotativeObjectInfo(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("annotative")]  bool Annotative,
    [property: JsonPropertyName("scales")]      IReadOnlyList<string> Scales);

public sealed record AnnotativeObjectResult(
    [property: JsonPropertyName("objects")] IReadOnlyList<AnnotativeObjectInfo> Objects,
    [property: JsonPropertyName("count")]   int Count);

public sealed record AnnotativeAffected(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("skipped")]  IReadOnlyList<string>? Skipped = null);

public sealed record AnnotationVisibilityResult(
    [property: JsonPropertyName("showAllScales")] bool ShowAllScales,
    [property: JsonPropertyName("autoAddScale")]  bool AutoAddScale,
    [property: JsonPropertyName("annoAllVisible")] int AnnoAllVisible,
    [property: JsonPropertyName("annoAutoScale")]  int AnnoAutoScale);
