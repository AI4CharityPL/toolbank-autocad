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
