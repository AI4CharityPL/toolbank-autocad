// Plugin-side DTOs for acad-ucs. Wire names must match the backend's UcsDtos.cs.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record Ucs3PointArgsDto(
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("xAxisPoint")]  Point3dDto XAxisPoint,
    [property: JsonPropertyName("yAxisPoint")]  Point3dDto YAxisPoint,
    [property: JsonPropertyName("name")]        string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

internal sealed record UcsOriginArgsDto(
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("name")]        string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

internal sealed record UcsZAxisArgsDto(
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("zAxis")]       Point3dDto ZAxis,
    [property: JsonPropertyName("name")]        string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

internal sealed record UcsRotateArgsDto(
    [property: JsonPropertyName("axis")]        string Axis,
    [property: JsonPropertyName("angleDeg")]    double AngleDeg,
    [property: JsonPropertyName("name")]        string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

internal sealed record UcsFromEntityArgsDto(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("name")]        string? Name = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = true);

internal sealed record UcsNameArgsDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record UcsSaveArgsDto(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("overwrite")] bool Overwrite = true);

internal sealed record UcsRenameArgsDto(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

internal sealed record UcsTransformArgsDto(
    [property: JsonPropertyName("point")] Point3dDto Point,
    [property: JsonPropertyName("from")]  string From = "world",
    [property: JsonPropertyName("to")]    string To = "current");
