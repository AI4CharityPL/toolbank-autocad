// Typed DTOs for the acad-annotations category.
// Mirrors the wire shape consumed by the plugin under "acad.annotations.<verb>".
// See rule 19-tool-implementation-pattern.md, rule 27-acad-text-and-table-traps.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Annotations;

public sealed record AnnotationsEmptyArgs();

// ─────────── DBText / DTEXT ───────────

public sealed record AddDBTextArgs(
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("contents")]    string Contents,
    [property: JsonPropertyName("height")]      double Height = 2.5,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("textStyle")]   string? TextStyle = null,
    [property: JsonPropertyName("alignment")]   string? Alignment = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record UpdateDBTextArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("contents")] string Contents);

// ─────────── MText ───────────

public sealed record AddMTextArgs(
    [property: JsonPropertyName("position")]       Point3dDto Position,
    [property: JsonPropertyName("contents")]       string Contents,
    [property: JsonPropertyName("textHeight")]     double TextHeight = 2.5,
    [property: JsonPropertyName("widthFactor")]    double Width = 0.0,
    [property: JsonPropertyName("rotationDeg")]    double RotationDeg = 0.0,
    [property: JsonPropertyName("attachmentPoint")] string? AttachmentPoint = null,
    [property: JsonPropertyName("textStyle")]      string? TextStyle = null,
    [property: JsonPropertyName("layer")]          string? Layer = null);

public sealed record UpdateMTextArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("contents")] string Contents);

// ─────────── MLeader / Leader ───────────

public sealed record AddMLeaderArgs(
    [property: JsonPropertyName("arrowTip")]      Point3dDto ArrowTip,
    [property: JsonPropertyName("textPosition")]  Point3dDto TextPosition,
    [property: JsonPropertyName("contents")]      string Contents,
    [property: JsonPropertyName("textHeight")]    double TextHeight = 2.5,
    [property: JsonPropertyName("dogleg")]        bool EnableDogleg = true,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record AddBlockMLeaderArgs(
    [property: JsonPropertyName("arrowTip")]      Point3dDto ArrowTip,
    [property: JsonPropertyName("blockPosition")] Point3dDto BlockPosition,
    [property: JsonPropertyName("blockName")]     string BlockName,
    [property: JsonPropertyName("scale")]         double Scale = 1.0,
    [property: JsonPropertyName("layer")]         string? Layer = null);

// ─────────── Tables ───────────

public sealed record AddTableArgs(
    [property: JsonPropertyName("position")]   Point3dDto Position,
    [property: JsonPropertyName("rows")]       int Rows,
    [property: JsonPropertyName("cols")]       int Cols,
    [property: JsonPropertyName("rowHeight")]  double RowHeight = 8.0,
    [property: JsonPropertyName("colWidth")]   double ColWidth = 30.0,
    [property: JsonPropertyName("data")]       IReadOnlyList<IReadOnlyList<string>>? Data = null,
    [property: JsonPropertyName("textStyle")]  string? TextStyle = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record SetTableCellArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("row")]      int Row,
    [property: JsonPropertyName("col")]      int Col,
    [property: JsonPropertyName("contents")] string Contents);

// ─────────── Text styles ───────────

public sealed record CreateTextStyleArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("font")]       string Font,
    [property: JsonPropertyName("height")]     double Height = 0.0,
    [property: JsonPropertyName("widthFactor")] double WidthFactor = 1.0,
    [property: JsonPropertyName("obliqueDeg")] double ObliqueDeg = 0.0);

public sealed record TextStyleNameArg(
    [property: JsonPropertyName("name")] string Name);

// ─────────── results ───────────

public sealed record AnnEntityResult(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

public sealed record AnnAffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

public sealed record TextStyleListResult(
    [property: JsonPropertyName("styles")]  IReadOnlyList<string> Styles,
    [property: JsonPropertyName("current")] string Current);

// ─────────── roadmap 3.3, first tranche: finding text across a drawing ───────────

public sealed record TextSearchArgs(
    [property: JsonPropertyName("pattern")]     string Pattern,
    [property: JsonPropertyName("regex")]       bool? Regex = null,
    [property: JsonPropertyName("matchCase")]   bool? MatchCase = null,
    [property: JsonPropertyName("wholeWord")]   bool? WholeWord = null,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null,
    [property: JsonPropertyName("handles")]     IReadOnlyList<string>? Handles = null,
    [property: JsonPropertyName("limit")]       int? Limit = null);

public sealed record TextHitDto(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("text")]        string Text,
    [property: JsonPropertyName("occurrences")] int Occurrences,
    [property: JsonPropertyName("position")]    IReadOnlyList<double> Position);

public sealed record TextSearchResult(
    [property: JsonPropertyName("pattern")]       string Pattern,
    [property: JsonPropertyName("regex")]         bool Regex,
    [property: JsonPropertyName("matchCase")]     bool MatchCase,
    [property: JsonPropertyName("wholeWord")]     bool WholeWord,
    [property: JsonPropertyName("scanned")]       int Scanned,
    [property: JsonPropertyName("scannedByType")] IReadOnlyDictionary<string, int> ScannedByType,
    [property: JsonPropertyName("matched")]       int Matched,
    [property: JsonPropertyName("occurrences")]   int Occurrences,
    [property: JsonPropertyName("truncated")]     bool Truncated,
    [property: JsonPropertyName("results")]       IReadOnlyList<TextHitDto> Results,
    [property: JsonPropertyName("note")]          string Note);

public sealed record FindReplaceArgs(
    [property: JsonPropertyName("find")]        string Find,
    [property: JsonPropertyName("replaceWith")] string? ReplaceWith = null,
    [property: JsonPropertyName("regex")]       bool? Regex = null,
    [property: JsonPropertyName("matchCase")]   bool? MatchCase = null,
    [property: JsonPropertyName("wholeWord")]   bool? WholeWord = null,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null,
    [property: JsonPropertyName("handles")]     IReadOnlyList<string>? Handles = null,
    [property: JsonPropertyName("dryRun")]      bool? DryRun = null);

public sealed record ReplacedTextDto(
    [property: JsonPropertyName("handle")]             string Handle,
    [property: JsonPropertyName("type")]               string Type,
    [property: JsonPropertyName("layer")]              string Layer,
    [property: JsonPropertyName("before")]             string Before,
    [property: JsonPropertyName("after")]              string After,
    [property: JsonPropertyName("occurrences")]        int Occurrences,
    [property: JsonPropertyName("hadFormattingCodes")] bool HadFormattingCodes);

public sealed record SkippedTextDto(
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("type")]         string Type,
    [property: JsonPropertyName("reason")]       string Reason,
    [property: JsonPropertyName("renderedText")] string? RenderedText = null);

public sealed record FindReplaceResult(
    [property: JsonPropertyName("find")]            string Find,
    [property: JsonPropertyName("replaceWith")]     string ReplaceWith,
    [property: JsonPropertyName("dryRun")]          bool DryRun,
    [property: JsonPropertyName("scanned")]         int Scanned,
    [property: JsonPropertyName("entitiesChanged")] int EntitiesChanged,
    [property: JsonPropertyName("occurrences")]     int Occurrences,
    [property: JsonPropertyName("changed")]         IReadOnlyList<ReplacedTextDto> Changed,
    [property: JsonPropertyName("skipped")]         IReadOnlyList<SkippedTextDto> Skipped,
    [property: JsonPropertyName("note")]            string Note);

public sealed record ExportTextArgs(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null,
    [property: JsonPropertyName("format")]      string? Format = null);

public sealed record ExportTextResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("items")]  int Items,
    [property: JsonPropertyName("bytes")]  long Bytes,
    [property: JsonPropertyName("note")]   string Note);
