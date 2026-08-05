// Typed DTOs for the acad-files category.
// Mirrors the wire shape consumed by the plugin under "acad.files.<verb>".
// See rule 19, rule 28-acad-blocks-layers-files-traps.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Files;

public sealed record FilesEmptyArgs();

public sealed record OpenDocumentArgs(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("readOnly")] bool ReadOnly = false,
    [property: JsonPropertyName("password")] string? Password = null);

public sealed record SaveAsArgs(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("dwgVersion")] string? DwgVersion = null);

public sealed record CloseDocumentArgs(
    [property: JsonPropertyName("path")]    string? Path = null,
    [property: JsonPropertyName("save")]    bool SaveBeforeClose = false);

public sealed record ImportFileArgs(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("insertion")] Point3dDto? Insertion = null);

public sealed record ExportFileArgs(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("format")]   string Format,
    [property: JsonPropertyName("layout")]   string? Layout = null,
    [property: JsonPropertyName("scope")]    string? Scope = null,
    [property: JsonPropertyName("window")]   WindowArea? Window = null,
    [property: JsonPropertyName("widthPx")]  int? WidthPx = null,
    [property: JsonPropertyName("heightPx")] int? HeightPx = null);

/// <summary>
/// World-space rectangle (model-space drawing units) used when scope="Window".
/// When supplied, plots the exact rectangle (xMin/yMin bottom-left, xMax/yMax top-right).
/// Pairs well with <c>widthPx</c>/<c>heightPx</c> to request a specific output resolution.
/// </summary>
public sealed record WindowArea(
    [property: JsonPropertyName("xMin")] double XMin,
    [property: JsonPropertyName("yMin")] double YMin,
    [property: JsonPropertyName("xMax")] double XMax,
    [property: JsonPropertyName("yMax")] double YMax);

public sealed record DocumentInfo(
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("isReadOnly")]  bool IsReadOnly,
    [property: JsonPropertyName("isModified")]  bool IsModified,
    [property: JsonPropertyName("dwgVersion")]  string? DwgVersion,
    [property: JsonPropertyName("entityCount")] int EntityCount);

public sealed record DocumentsListResult(
    [property: JsonPropertyName("documents")] IReadOnlyList<DocumentInfo> Documents,
    [property: JsonPropertyName("active")]    string? Active);

public sealed record DocumentResult(
    [property: JsonPropertyName("document")] DocumentInfo Document);

public sealed record FilePathResult(
    [property: JsonPropertyName("path")] string Path);

public sealed record AuditArgs(
    [property: JsonPropertyName("fix")] bool Fix = false);

public sealed record AuditResult(
    [property: JsonPropertyName("errorsFound")] int ErrorsFound,
    [property: JsonPropertyName("errorsFixed")] int ErrorsFixed);

public sealed record FilesAffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

// ─────────────── drawing properties, DWGPROPS (roadmap 2.4) ───────────────

public sealed record SetDrawingPropertiesArgs(
    [property: JsonPropertyName("title")]          string? Title = null,
    [property: JsonPropertyName("subject")]        string? Subject = null,
    [property: JsonPropertyName("author")]         string? Author = null,
    [property: JsonPropertyName("keywords")]       string? Keywords = null,
    [property: JsonPropertyName("comments")]       string? Comments = null,
    [property: JsonPropertyName("revisionNumber")] string? RevisionNumber = null,
    [property: JsonPropertyName("hyperlinkBase")]  string? HyperlinkBase = null);

public sealed record SetCustomPropertyArgs(
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("value")] string? Value = null);

public sealed record DrawingProperties(
    [property: JsonPropertyName("title")]          string Title,
    [property: JsonPropertyName("subject")]        string Subject,
    [property: JsonPropertyName("author")]         string Author,
    [property: JsonPropertyName("keywords")]       string Keywords,
    [property: JsonPropertyName("comments")]       string Comments,
    [property: JsonPropertyName("lastSavedBy")]    string LastSavedBy,
    [property: JsonPropertyName("revisionNumber")] string RevisionNumber,
    [property: JsonPropertyName("hyperlinkBase")]  string HyperlinkBase,
    [property: JsonPropertyName("custom")]         IReadOnlyDictionary<string, string> Custom);

public sealed record DrawingPropertiesResult(
    [property: JsonPropertyName("properties")] DrawingProperties Properties,
    [property: JsonPropertyName("note")]       string Note);

public sealed record DrawingPropertiesApplyResult(
    [property: JsonPropertyName("applied")]    IReadOnlyList<string> Applied,
    [property: JsonPropertyName("properties")] DrawingProperties Properties);

public sealed record CustomPropertyResult(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("action")]     string Action,
    [property: JsonPropertyName("properties")] DrawingProperties Properties);
