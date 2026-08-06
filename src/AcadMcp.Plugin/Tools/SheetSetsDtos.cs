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
