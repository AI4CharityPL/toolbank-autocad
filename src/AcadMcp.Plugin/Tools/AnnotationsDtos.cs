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
    [property: JsonPropertyName("widthFactor")]     double Width = 0.0,
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
