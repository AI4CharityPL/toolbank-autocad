// DTOs for the acad-ucs category. See docs/engineering-rules/43-coordinate-systems.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Ucs;

public sealed record EmptyUcsArgs();

public sealed record UcsFrom3PointsArgs(
    [property: JsonPropertyName("origin")]     Point3dDto Origin,
    [property: JsonPropertyName("xAxisPoint")] Point3dDto XAxisPoint,
    [property: JsonPropertyName("yAxisPoint")] Point3dDto YAxisPoint,
    [property: JsonPropertyName("name")]       string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

public sealed record UcsFromOriginArgs(
    [property: JsonPropertyName("origin")] Point3dDto Origin,
    [property: JsonPropertyName("name")]   string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

public sealed record UcsFromZAxisArgs(
    [property: JsonPropertyName("origin")] Point3dDto Origin,
    [property: JsonPropertyName("zAxis")]  Point3dDto ZAxis,
    [property: JsonPropertyName("name")]   string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

public sealed record UcsRotateArgs(
    [property: JsonPropertyName("axis")]     string Axis,
    [property: JsonPropertyName("angleDeg")] double AngleDeg,
    [property: JsonPropertyName("name")]     string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

public sealed record UcsNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record SaveUcsArgs(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("overwrite")] bool Overwrite = true);

public sealed record TransformPointArgs(
    [property: JsonPropertyName("point")] Point3dDto Point,
    [property: JsonPropertyName("from")]  string From = "world",
    [property: JsonPropertyName("to")]    string To = "current");

// ─────────────── results ───────────────

public sealed record UcsInfo(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("origin")]    Point3dDto Origin,
    [property: JsonPropertyName("xAxis")]     Point3dDto XAxis,
    [property: JsonPropertyName("yAxis")]     Point3dDto YAxis,
    [property: JsonPropertyName("zAxis")]     Point3dDto ZAxis,
    [property: JsonPropertyName("isCurrent")] bool IsCurrent,
    [property: JsonPropertyName("isWorld")]   bool IsWorld);

// savedAs and isCurrent are separate from UcsInfo.Name on purpose: a UCS can be saved under a
// name WITHOUT becoming current (makeCurrent:false), and before this record carried them a caller
// had no way to tell those two outcomes apart. They are nullable because the read-only tools
// share this shape and neither applies to them.
//
// THIRD TIME THIS SHAPE HAS BITTEN. The plugin emitted both fields, this record did not declare
// them, and deserialisation dropped them silently - the same failure as `alsoDeleted` in
// delete_layer_filter and `replaced` in import_dimstyle_from_dwg. A field produced on one side of
// the pipe and undeclared on the other vanishes without any error anywhere. See KNOWN-GAPS C.
public sealed record UcsResult(
    [property: JsonPropertyName("ucs")]       UcsInfo Ucs,
    [property: JsonPropertyName("savedAs")]   string? SavedAs = null,
    [property: JsonPropertyName("isCurrent")] bool? IsCurrent = null);

public sealed record UcsListResult(
    [property: JsonPropertyName("named")]   IReadOnlyList<UcsInfo> Named,
    [property: JsonPropertyName("current")] UcsInfo Current,
    [property: JsonPropertyName("count")]   int Count);

public sealed record TransformPointResult(
    [property: JsonPropertyName("input")]  Point3dDto Input,
    [property: JsonPropertyName("output")] Point3dDto Output,
    [property: JsonPropertyName("from")]   string From,
    [property: JsonPropertyName("to")]     string To);

public sealed record UcsAffected(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("name")]     string? Name = null);

public sealed record EntityHandleUcsArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("name")]   string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

public sealed record RenameUcsArgs(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

public sealed record UcsPreviousResult(
    [property: JsonPropertyName("ucs")]               UcsInfo Ucs,
    [property: JsonPropertyName("remainingHistory")]  int RemainingHistory);
