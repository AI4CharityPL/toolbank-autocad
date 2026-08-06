// DTOs for acad-sheetsets. Every one carries the .DST path: rule 45 §3 - no tool holds a
// sheet set open across calls.

using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record SheetSetPathArgsDto(
    [property: JsonPropertyName("path")] string? Path);

internal sealed record SheetPropertyArgsDto(
    [property: JsonPropertyName("path")]     string? Path,
    [property: JsonPropertyName("sheet")]    string? Sheet,
    [property: JsonPropertyName("property")] string? Property);

internal sealed record SheetWriteArgsDto(
    [property: JsonPropertyName("path")]  string? Path,
    [property: JsonPropertyName("sheet")] string? Sheet,
    [property: JsonPropertyName("value")] string? Value);

internal sealed record SheetFlagArgsDto(
    [property: JsonPropertyName("path")]      string? Path,
    [property: JsonPropertyName("sheet")]     string? Sheet,
    [property: JsonPropertyName("doNotPlot")] bool DoNotPlot);

// rename_sheet takes number and title rather than a single `value`, because a sheet has no
// separately stored name to set. Measured: changing only the title moved the reported name from
// "T-01 TITLE SHEET" to "T-01 PROBE TITLE", and IAcSmSheet.SetName left it untouched. The name is
// composed from number + title. AutoCAD's own UI agrees - its command is "Rename & Renumber
// Sheet" and it edits both fields together.
internal sealed record SheetRenameArgsDto(
    [property: JsonPropertyName("path")]   string? Path,
    [property: JsonPropertyName("sheet")]  string? Sheet,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("title")]  string? Title);

internal sealed record SubsetCreateArgsDto(
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("name")]        string? Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parent")]      string? Parent);

internal sealed record SubsetArgsDto(
    [property: JsonPropertyName("path")]   string? Path,
    [property: JsonPropertyName("subset")] string? Subset);

internal sealed record MoveSheetArgsDto(
    [property: JsonPropertyName("path")]   string? Path,
    [property: JsonPropertyName("sheet")]  string? Sheet,
    [property: JsonPropertyName("subset")] string? Subset);

internal sealed record SetSheetPropertyArgsDto(
    [property: JsonPropertyName("path")]     string? Path,
    [property: JsonPropertyName("sheet")]    string? Sheet,
    [property: JsonPropertyName("property")] string? Property,
    [property: JsonPropertyName("value")]    string? Value);

internal sealed record DefinePropertyArgsDto(
    [property: JsonPropertyName("path")]         string? Path,
    [property: JsonPropertyName("name")]         string? Name,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue,
    [property: JsonPropertyName("scope")]        string? Scope);

internal sealed record ReorderArgsDto(
    [property: JsonPropertyName("path")]   string? Path,
    [property: JsonPropertyName("sheet")]  string? Sheet,
    [property: JsonPropertyName("before")] string? Before,
    [property: JsonPropertyName("after")]  string? After);

internal sealed record SheetRefArgsDto(
    [property: JsonPropertyName("path")]  string? Path,
    [property: JsonPropertyName("sheet")] string? Sheet);

internal sealed record AddSheetArgsDto(
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("drawingPath")] string? DrawingPath,
    [property: JsonPropertyName("layout")]      string? Layout,
    [property: JsonPropertyName("number")]      string? Number,
    [property: JsonPropertyName("title")]       string? Title,
    [property: JsonPropertyName("subset")]      string? Subset);

internal sealed record CreateSheetSetArgsDto(
    [property: JsonPropertyName("path")]         string? Path,
    [property: JsonPropertyName("name")]         string? Name,
    [property: JsonPropertyName("description")]  string? Description,
    [property: JsonPropertyName("templatePath")] string? TemplatePath,
    [property: JsonPropertyName("overwrite")]    bool? Overwrite);

internal sealed record ViewCategoryArgsDto(
    [property: JsonPropertyName("path")]        string? Path,
    [property: JsonPropertyName("category")]    string? Category,
    [property: JsonPropertyName("description")] string? Description);

internal sealed record SetViewCategoryArgsDto(
    [property: JsonPropertyName("path")]     string? Path,
    [property: JsonPropertyName("view")]     string? View,
    [property: JsonPropertyName("category")] string? Category);

internal sealed record ResaveArgsDto(
    [property: JsonPropertyName("path")]                string? Path,
    [property: JsonPropertyName("apply")]               bool? Apply,
    [property: JsonPropertyName("includeOpenDrawings")] bool? IncludeOpenDrawings);
