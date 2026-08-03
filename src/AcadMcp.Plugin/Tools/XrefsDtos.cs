// Plugin-side DTOs for acad-xrefs. Wire names must match the backend's XrefsDtos.cs exactly -
// that pairing is what SchemaContractTests protects on the backend side.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record AttachXrefArgsDto(
    [property: JsonPropertyName("path")]         string Path,
    [property: JsonPropertyName("blockName")]    string? BlockName = null,
    [property: JsonPropertyName("insertion")]    Point3dDto? Insertion = null,
    [property: JsonPropertyName("scaleX")]       double? ScaleX = null,
    [property: JsonPropertyName("scaleY")]       double? ScaleY = null,
    [property: JsonPropertyName("scaleZ")]       double? ScaleZ = null,
    [property: JsonPropertyName("rotationDeg")]  double? RotationDeg = null,
    [property: JsonPropertyName("layer")]        string? Layer = null,
    [property: JsonPropertyName("relativePath")] bool RelativePath = true);

internal sealed record XrefRefArgsDto(
    [property: JsonPropertyName("blockName")] string BlockName);

internal sealed record XrefBindArgsDto(
    [property: JsonPropertyName("blockName")]  string BlockName,
    [property: JsonPropertyName("insertMode")] bool InsertMode = false);

internal sealed record SetXrefPathArgsDto(
    [property: JsonPropertyName("blockName")]    string BlockName,
    [property: JsonPropertyName("path")]         string Path,
    [property: JsonPropertyName("relativePath")] bool RelativePath = true,
    [property: JsonPropertyName("reload")]       bool Reload = true);

internal sealed record RepathAllArgsDto(
    [property: JsonPropertyName("oldPrefix")]    string OldPrefix,
    [property: JsonPropertyName("newPrefix")]    string NewPrefix,
    [property: JsonPropertyName("relativePath")] bool RelativePath = true,
    [property: JsonPropertyName("dryRun")]       bool DryRun = false);

internal sealed record ClipRectArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("corner1")]  Point2dDto Corner1,
    [property: JsonPropertyName("corner2")]  Point2dDto Corner2,
    [property: JsonPropertyName("inverted")] bool Inverted = false);

internal sealed record ClipPolyArgsDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("inverted")] bool Inverted = false);

internal sealed record XrefHandleArgsDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record SetClipDisplayArgsDto(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("visible")] bool Visible);

internal sealed record XrefLayerOverrideArgsDto(
    [property: JsonPropertyName("blockName")]    string BlockName,
    [property: JsonPropertyName("layer")]        string Layer,
    [property: JsonPropertyName("color")]        ColorDto? Color = null,
    [property: JsonPropertyName("linetype")]     string? Linetype = null,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm = null,
    [property: JsonPropertyName("off")]          bool? Off = null,
    [property: JsonPropertyName("frozen")]       bool? Frozen = null);

internal sealed record XrefLayerResetArgsDto(
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("layer")]     string? Layer = null);
