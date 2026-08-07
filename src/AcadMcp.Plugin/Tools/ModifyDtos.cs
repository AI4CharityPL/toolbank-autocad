// Plugin-side DTOs for the acad-modify category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record HandlesArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

internal sealed record MoveArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("from")]    Point3dDto From,
    [property: JsonPropertyName("to")]      Point3dDto To);

internal sealed record RotateArgsDto(
    [property: JsonPropertyName("handles")]  IReadOnlyList<string> Handles,
    [property: JsonPropertyName("center")]   Point3dDto Center,
    [property: JsonPropertyName("angleDeg")] double AngleDeg,
    [property: JsonPropertyName("axis")]     Vector3dDto? Axis = null);

internal sealed record ScaleArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("center")]  Point3dDto Center,
    [property: JsonPropertyName("factor")]  double Factor);

internal sealed record MirrorArgsDto(
    [property: JsonPropertyName("handles")]      IReadOnlyList<string> Handles,
    [property: JsonPropertyName("planeOrigin")]  Point3dDto PlaneOrigin,
    [property: JsonPropertyName("planeNormal")]  Vector3dDto PlaneNormal,
    [property: JsonPropertyName("eraseSource")]  bool EraseSource = false);

internal sealed record CopyArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("from")]    Point3dDto From,
    [property: JsonPropertyName("to")]      Point3dDto To,
    [property: JsonPropertyName("count")]   int Count = 1);

internal sealed record ArrayRectArgsDto(
    [property: JsonPropertyName("handles")]   IReadOnlyList<string> Handles,
    [property: JsonPropertyName("rows")]      int Rows,
    [property: JsonPropertyName("cols")]      int Cols,
    [property: JsonPropertyName("rowSpacing")] double RowSpacing,
    [property: JsonPropertyName("colSpacing")] double ColSpacing,
    [property: JsonPropertyName("levels")]     int Levels = 1,
    [property: JsonPropertyName("levelSpacing")] double LevelSpacing = 0.0);

internal sealed record ArrayPolarArgsDto(
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("center")]     Point3dDto Center,
    [property: JsonPropertyName("itemCount")]  int ItemCount,
    [property: JsonPropertyName("totalAngleDeg")] double TotalAngleDeg = 360.0,
    [property: JsonPropertyName("rotateItems")] bool RotateItems = true);

internal sealed record AlignArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("sourceA")] Point3dDto SourceA,
    [property: JsonPropertyName("sourceB")] Point3dDto SourceB,
    [property: JsonPropertyName("targetA")] Point3dDto TargetA,
    [property: JsonPropertyName("targetB")] Point3dDto TargetB,
    [property: JsonPropertyName("scale")]   bool Scale = false);

internal sealed record SetLayerArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("layer")]   string Layer);

internal sealed record SetColorArgsDto(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("color")]   ColorDto Color);

internal sealed record SetLinetypeArgsDto(
    [property: JsonPropertyName("handles")]  IReadOnlyList<string> Handles,
    [property: JsonPropertyName("linetype")] string Linetype,
    [property: JsonPropertyName("scale")]    double? Scale = null);

internal sealed record SetLineweightArgsDto(
    [property: JsonPropertyName("handles")]      IReadOnlyList<string> Handles,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm);

internal sealed record MatchPropertiesArgsDto(
    [property: JsonPropertyName("sourceHandle")] string SourceHandle,
    [property: JsonPropertyName("targetHandles")] IReadOnlyList<string> TargetHandles);

internal sealed record GroupCreateArgsDto(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("selectable")] bool Selectable = true);

internal sealed record GroupNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

// ─────────── transform by reference (roadmap 3.1) ───────────

internal sealed record ReferenceScaleArgsDto(
    [property: JsonPropertyName("handles")]         List<string>? Handles,
    [property: JsonPropertyName("basePoint")]       Point3dDto? BasePoint,
    [property: JsonPropertyName("referenceLength")] double? ReferenceLength,
    [property: JsonPropertyName("referenceStart")]  Point3dDto? ReferenceStart,
    [property: JsonPropertyName("referenceEnd")]    Point3dDto? ReferenceEnd,
    [property: JsonPropertyName("newLength")]       double? NewLength);

internal sealed record ReferenceRotateArgsDto(
    [property: JsonPropertyName("handles")]           List<string>? Handles,
    [property: JsonPropertyName("basePoint")]         Point3dDto? BasePoint,
    [property: JsonPropertyName("referenceAngleDeg")] double? ReferenceAngleDeg,
    [property: JsonPropertyName("referenceStart")]    Point3dDto? ReferenceStart,
    [property: JsonPropertyName("referenceEnd")]      Point3dDto? ReferenceEnd,
    [property: JsonPropertyName("newAngleDeg")]       double? NewAngleDeg);
