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
