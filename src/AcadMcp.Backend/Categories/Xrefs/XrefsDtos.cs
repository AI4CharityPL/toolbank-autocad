// DTOs for the acad-xrefs category. Wire names are [JsonPropertyName]; see rule 22.
//
// Vocabulary, because AutoCAD's own is ambiguous:
//   blockName  - the xref's name in the block table ("A-PLAN"), stable, what every other
//                tool refers to. Preferred key for anything that identifies an xref.
//   handle     - a single BlockReference (insert) of that xref. One xref definition can have
//                several inserts; clipping is per-insert, path/reload/bind are per-definition.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Xrefs;

// ─────────────── attach / detach ───────────────

public sealed record AttachXrefArgs(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("blockName")]   string? BlockName = null,
    [property: JsonPropertyName("insertion")]   Point3dDto? Insertion = null,
    [property: JsonPropertyName("scaleX")]      double? ScaleX = null,
    [property: JsonPropertyName("scaleY")]      double? ScaleY = null,
    [property: JsonPropertyName("scaleZ")]      double? ScaleZ = null,
    [property: JsonPropertyName("rotationDeg")] double? RotationDeg = null,
    [property: JsonPropertyName("layer")]       string? Layer = null,
    [property: JsonPropertyName("relativePath")] bool RelativePath = true);

public sealed record XrefRefArgs(
    [property: JsonPropertyName("blockName")] string BlockName);

public sealed record XrefBindArgs(
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("insertMode")] bool InsertMode = false);

public sealed record SetXrefPathArgs(
    [property: JsonPropertyName("blockName")]    string BlockName,
    [property: JsonPropertyName("path")]         string Path,
    [property: JsonPropertyName("relativePath")] bool RelativePath = true,
    [property: JsonPropertyName("reload")]       bool Reload = true);

public sealed record RepathAllArgs(
    [property: JsonPropertyName("oldPrefix")]    string OldPrefix,
    [property: JsonPropertyName("newPrefix")]    string NewPrefix,
    [property: JsonPropertyName("relativePath")] bool RelativePath = true,
    [property: JsonPropertyName("dryRun")]       bool DryRun = false);

// ─────────────── clipping ───────────────

public sealed record ClipXrefRectArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("corner1")] Point2dDto Corner1,
    [property: JsonPropertyName("corner2")] Point2dDto Corner2,
    [property: JsonPropertyName("inverted")] bool Inverted = false);

public sealed record ClipXrefPolyArgs(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("inverted")] bool Inverted = false);

public sealed record XrefHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record SetClipDisplayArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("visible")] bool Visible);

// ─────────────── layer overrides ───────────────

public sealed record XrefLayerOverrideArgs(
    [property: JsonPropertyName("blockName")]     string BlockName,
    [property: JsonPropertyName("layer")]         string Layer,
    [property: JsonPropertyName("color")]         ColorDto? Color = null,
    [property: JsonPropertyName("linetype")]      string? Linetype = null,
    [property: JsonPropertyName("lineweightMm")]  double? LineweightMm = null,
    [property: JsonPropertyName("off")]           bool? Off = null,
    [property: JsonPropertyName("frozen")]        bool? Frozen = null);

public sealed record XrefLayerResetArgs(
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("layer")]     string? Layer = null);

// ─────────────── results ───────────────

public sealed record XrefInfo(
    [property: JsonPropertyName("blockName")]   string BlockName,
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("foundPath")]   string? FoundPath,
    [property: JsonPropertyName("status")]      string Status,
    [property: JsonPropertyName("isOverlay")]   bool IsOverlay,
    [property: JsonPropertyName("isNested")]    bool IsNested,
    [property: JsonPropertyName("parentName")]  string? ParentName,
    [property: JsonPropertyName("insertCount")] int InsertCount,
    [property: JsonPropertyName("isResolved")]  bool IsResolved,
    [property: JsonPropertyName("isUnloaded")]  bool IsUnloaded);

public sealed record XrefListResult(
    [property: JsonPropertyName("xrefs")] IReadOnlyList<XrefInfo> Xrefs,
    [property: JsonPropertyName("count")] int Count);

public sealed record XrefInfoResult(
    [property: JsonPropertyName("xref")]    XrefInfo Xref,
    [property: JsonPropertyName("inserts")] IReadOnlyList<EntityHandle> Inserts);

public sealed record XrefAttachResult(
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("entity")]    EntityHandle Entity,
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("isOverlay")] bool IsOverlay);

public sealed record XrefRepathEntry(
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("oldPath")]   string? OldPath,
    [property: JsonPropertyName("newPath")]   string? NewPath,
    [property: JsonPropertyName("applied")]   bool Applied,
    [property: JsonPropertyName("resolves")]  bool Resolves);

public sealed record XrefRepathResult(
    [property: JsonPropertyName("entries")] IReadOnlyList<XrefRepathEntry> Entries,
    [property: JsonPropertyName("changed")] int Changed,
    [property: JsonPropertyName("dryRun")]  bool DryRun);

public sealed record XrefMissingResult(
    [property: JsonPropertyName("missing")] IReadOnlyList<XrefInfo> Missing,
    [property: JsonPropertyName("count")]   int Count);

public sealed record XrefSymbolsResult(
    [property: JsonPropertyName("blockName")]  string BlockName,
    [property: JsonPropertyName("layers")]     IReadOnlyList<string> Layers,
    [property: JsonPropertyName("linetypes")]  IReadOnlyList<string> Linetypes,
    [property: JsonPropertyName("textStyles")] IReadOnlyList<string> TextStyles,
    [property: JsonPropertyName("dimStyles")]  IReadOnlyList<string> DimStyles,
    [property: JsonPropertyName("blocks")]     IReadOnlyList<string> Blocks);

public sealed record XrefClipResult(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("clipped")]    bool Clipped,
    [property: JsonPropertyName("inverted")]   bool Inverted,
    [property: JsonPropertyName("vertexCount")] int VertexCount);

public sealed record XrefAffected(
    [property: JsonPropertyName("affected")]  int Affected,
    [property: JsonPropertyName("blockName")] string? BlockName = null);
