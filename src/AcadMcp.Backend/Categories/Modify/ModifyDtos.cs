// Typed DTOs for the acad-modify category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Modify;

public sealed record HandlesArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record MoveArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("from")]    Point3dDto From,
    [property: JsonPropertyName("to")]      Point3dDto To);

public sealed record RotateArgs(
    [property: JsonPropertyName("handles")]  IReadOnlyList<string> Handles,
    [property: JsonPropertyName("center")]   Point3dDto Center,
    [property: JsonPropertyName("angleDeg")] double AngleDeg,
    [property: JsonPropertyName("axis")]     Vector3dDto? Axis = null);

public sealed record ScaleArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("center")]  Point3dDto Center,
    [property: JsonPropertyName("factor")]  double Factor);

public sealed record MirrorArgs(
    [property: JsonPropertyName("handles")]      IReadOnlyList<string> Handles,
    [property: JsonPropertyName("planeOrigin")]  Point3dDto PlaneOrigin,
    [property: JsonPropertyName("planeNormal")]  Vector3dDto PlaneNormal,
    [property: JsonPropertyName("eraseSource")]  bool EraseSource = false);

public sealed record CopyArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("from")]    Point3dDto From,
    [property: JsonPropertyName("to")]      Point3dDto To,
    [property: JsonPropertyName("count")]   int Count = 1);

public sealed record ArrayRectArgs(
    [property: JsonPropertyName("handles")]   IReadOnlyList<string> Handles,
    [property: JsonPropertyName("rows")]      int Rows,
    [property: JsonPropertyName("cols")]      int Cols,
    [property: JsonPropertyName("rowSpacing")] double RowSpacing,
    [property: JsonPropertyName("colSpacing")] double ColSpacing,
    [property: JsonPropertyName("levels")]     int Levels = 1,
    [property: JsonPropertyName("levelSpacing")] double LevelSpacing = 0.0);

public sealed record ArrayPolarArgs(
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("center")]     Point3dDto Center,
    [property: JsonPropertyName("itemCount")]  int ItemCount,
    [property: JsonPropertyName("totalAngleDeg")] double TotalAngleDeg = 360.0,
    [property: JsonPropertyName("rotateItems")] bool RotateItems = true);

public sealed record AlignArgs(
    [property: JsonPropertyName("handles")]      IReadOnlyList<string> Handles,
    [property: JsonPropertyName("sourceA")] Point3dDto SourceA,
    [property: JsonPropertyName("sourceB")] Point3dDto SourceB,
    [property: JsonPropertyName("targetA")] Point3dDto TargetA,
    [property: JsonPropertyName("targetB")] Point3dDto TargetB,
    [property: JsonPropertyName("scale")]   bool Scale = false);

public sealed record SetLayerArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("layer")]   string Layer);

public sealed record SetColorArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("color")]   ColorDto Color);

public sealed record SetLinetypeArgs(
    [property: JsonPropertyName("handles")]  IReadOnlyList<string> Handles,
    [property: JsonPropertyName("linetype")] string Linetype,
    [property: JsonPropertyName("scale")]    double? Scale = null);

public sealed record SetLineweightArgs(
    [property: JsonPropertyName("handles")]      IReadOnlyList<string> Handles,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm);

public sealed record MatchPropertiesArgs(
    [property: JsonPropertyName("sourceHandle")] string SourceHandle,
    [property: JsonPropertyName("targetHandles")] IReadOnlyList<string> TargetHandles);

public sealed record GroupCreateArgs(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("selectable")] bool Selectable = true);

public sealed record GroupNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record EntitiesAffected(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record CopiedEntities(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record AffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

/// <summary>
/// Result of a tool that hands work to AutoCAD's command queue instead of doing it inline.
/// There is deliberately no count: the command has not run when this is returned, so any
/// number here would be invented. undo/redo previously returned AffectedCount and reported
/// "affected": 1 unconditionally, including when nothing was undone at all.
/// </summary>
public sealed record QueuedCommandResult(
    [property: JsonPropertyName("queued")] bool Queued,
    [property: JsonPropertyName("note")]   string Note);

public sealed record GroupNameResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("memberCount")] int MemberCount);
