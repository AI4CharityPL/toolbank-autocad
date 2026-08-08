// Plugin-side DTOs for the acad-annotations category.
// Mirror Backend/Categories/Annotations/AnnotationsDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record AnnotationsEmptyArgsDto();

internal sealed record AddDBTextArgsDto(
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("contents")]    string Contents,
    [property: JsonPropertyName("height")]      double Height = 2.5,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("textStyle")]   string? TextStyle = null,
    [property: JsonPropertyName("alignment")]   string? Alignment = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

internal sealed record UpdateDBTextArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("contents")] string Contents);

internal sealed record AddMTextArgsDto(
    [property: JsonPropertyName("position")]        Point3dDto Position,
    [property: JsonPropertyName("contents")]        string Contents,
    [property: JsonPropertyName("textHeight")]      double TextHeight = 2.5,
    // The WRAP width, in drawing units. It was exposed only as "widthFactor", which in AutoCAD
    // means horizontal letter compression - so a caller asking for widthFactor 0.8 expecting
    // condensed text got an MText 0.8 units wide and a plausible-looking mess. "width" is the
    // right name and wins when both are given; the old one still works so nothing breaks.
    [property: JsonPropertyName("widthFactor")]     double Width = 0.0,
    [property: JsonPropertyName("width")]           double? WrapWidth = null,
    [property: JsonPropertyName("rotationDeg")]     double RotationDeg = 0.0,
    [property: JsonPropertyName("attachmentPoint")] string? AttachmentPoint = null,
    [property: JsonPropertyName("textStyle")]       string? TextStyle = null,
    [property: JsonPropertyName("layer")]           string? Layer = null);

internal sealed record UpdateMTextArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("contents")] string Contents);

internal sealed record AddMLeaderArgsDto(
    [property: JsonPropertyName("arrowTip")]      Point3dDto ArrowTip,
    [property: JsonPropertyName("textPosition")]  Point3dDto TextPosition,
    [property: JsonPropertyName("contents")]      string Contents,
    [property: JsonPropertyName("textHeight")]    double TextHeight = 2.5,
    [property: JsonPropertyName("dogleg")]        bool EnableDogleg = true,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record AddBlockMLeaderArgsDto(
    [property: JsonPropertyName("arrowTip")]      Point3dDto ArrowTip,
    [property: JsonPropertyName("blockPosition")] Point3dDto BlockPosition,
    [property: JsonPropertyName("blockName")]     string BlockName,
    [property: JsonPropertyName("scale")]         double Scale = 1.0,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record AddTableArgsDto(
    [property: JsonPropertyName("position")]   Point3dDto Position,
    [property: JsonPropertyName("rows")]       int Rows,
    [property: JsonPropertyName("cols")]       int Cols,
    [property: JsonPropertyName("rowHeight")]  double RowHeight = 8.0,
    [property: JsonPropertyName("colWidth")]   double ColWidth = 30.0,
    [property: JsonPropertyName("data")]       IReadOnlyList<IReadOnlyList<string>>? Data = null,
    [property: JsonPropertyName("textStyle")]  string? TextStyle = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

internal sealed record SetTableCellArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("row")]      int Row,
    [property: JsonPropertyName("col")]      int Col,
    [property: JsonPropertyName("contents")] string Contents);

internal sealed record CreateTextStyleArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("font")]        string Font,
    [property: JsonPropertyName("height")]      double Height = 0.0,
    [property: JsonPropertyName("widthFactor")] double WidthFactor = 1.0,
    [property: JsonPropertyName("obliqueDeg")]  double ObliqueDeg = 0.0);

internal sealed record TextStyleNameArgDto(
    [property: JsonPropertyName("name")] string Name);

// ─────────── roadmap 3.3, first tranche: finding text across a drawing ───────────

internal sealed record TextSearchArgsDto(
    [property: JsonPropertyName("pattern")]     string? Pattern,
    [property: JsonPropertyName("regex")]       bool? Regex,
    [property: JsonPropertyName("matchCase")]   bool? MatchCase,
    [property: JsonPropertyName("wholeWord")]   bool? WholeWord,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter,
    [property: JsonPropertyName("handles")]     List<string>? Handles,
    [property: JsonPropertyName("limit")]       int? Limit);

internal sealed record FindReplaceArgsDto(
    [property: JsonPropertyName("find")]        string? Find,
    [property: JsonPropertyName("replaceWith")] string? ReplaceWith,
    [property: JsonPropertyName("regex")]       bool? Regex,
    [property: JsonPropertyName("matchCase")]   bool? MatchCase,
    [property: JsonPropertyName("wholeWord")]   bool? WholeWord,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter,
    [property: JsonPropertyName("handles")]     List<string>? Handles,
    [property: JsonPropertyName("dryRun")]      bool? DryRun);

internal sealed record ExportTextArgsDto(
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter,
    [property: JsonPropertyName("format")]      string? Format);

// ─────────── roadmap 3.3, second tranche: where text sits and how big it is ───────────

internal sealed record JustifyTextArgsDto(
    [property: JsonPropertyName("handles")]       List<string>? Handles,
    [property: JsonPropertyName("justification")] string? Justification);

internal sealed record TextFitArgsDto(
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("point1")] Point3dDto? Point1,
    [property: JsonPropertyName("point2")] Point3dDto? Point2);

internal sealed record ScaleTextArgsDto(
    [property: JsonPropertyName("handles")]   List<string>? Handles,
    [property: JsonPropertyName("factor")]    double? Factor,
    [property: JsonPropertyName("newHeight")] double? NewHeight);

// ─────────── roadmap 3.3, third tranche: how an MText presents itself ───────────

internal sealed record BackgroundMaskArgsDto(
    [property: JsonPropertyName("handles")]            List<string>? Handles,
    [property: JsonPropertyName("enabled")]            bool? Enabled,
    [property: JsonPropertyName("useDrawingBackground")] bool? UseDrawingBackground,
    [property: JsonPropertyName("color")]              ColorDto? Color,
    [property: JsonPropertyName("scaleFactor")]        double? ScaleFactor);

internal sealed record MTextColumnArgsDto(
    [property: JsonPropertyName("handle")]      string? Handle,
    [property: JsonPropertyName("mode")]        string? Mode,
    [property: JsonPropertyName("count")]       int? Count,
    [property: JsonPropertyName("width")]       double? Width,
    [property: JsonPropertyName("gutter")]      double? Gutter,
    [property: JsonPropertyName("autoHeight")]  bool? AutoHeight);
