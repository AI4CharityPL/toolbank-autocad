// DTOs for acad-sheetsets. Every argument record carries the .DST path, per rule 45 §3: no tool
// holds a sheet set open across calls, so there is no session to refer to.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.SheetSets;

public sealed record SheetSetPathArgs(
    [property: JsonPropertyName("path")] string Path);

public sealed record SheetPropertyArgs(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("sheet")]    string Sheet,
    [property: JsonPropertyName("property")] string? Property = null);

public sealed record SheetInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("number")]      string Number,
    [property: JsonPropertyName("title")]       string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("subset")]      string Subset,
    [property: JsonPropertyName("doNotPlot")]   bool DoNotPlot);

public sealed record SubsetInfo(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("path")]       string Path,
    [property: JsonPropertyName("sheetCount")] int SheetCount);

public sealed record SheetSetInfoResult(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("sheetCount")]  int SheetCount,
    [property: JsonPropertyName("subsetCount")] int SubsetCount);

public sealed record SheetSetPathResult(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("note")]        string Note);

public sealed record SheetListResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("sheets")] IReadOnlyList<SheetInfo> Sheets,
    [property: JsonPropertyName("count")]  int Count);

public sealed record SubsetListResult(
    [property: JsonPropertyName("path")]    string Path,
    [property: JsonPropertyName("subsets")] IReadOnlyList<SubsetInfo> Subsets,
    [property: JsonPropertyName("count")]   int Count);

// One record covers both shapes get_sheet_property answers with: a single named property, or the
// whole set when no name was asked for. Declaring every field the plugin can emit matters here -
// KNOWN-GAPS C0 records three separate occasions where a field the plugin produced and a DTO did
// not declare vanished silently on deserialisation.
public sealed record SheetPropertyResult(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("sheet")]    string Sheet,
    [property: JsonPropertyName("property")] string? Property = null,
    [property: JsonPropertyName("value")]    string? Value = null,
    [property: JsonPropertyName("kind")]     string? Kind = null,
    [property: JsonPropertyName("builtIn")]  IReadOnlyDictionary<string, string>? BuiltIn = null,
    [property: JsonPropertyName("custom")]   IReadOnlyDictionary<string, string>? Custom = null);

public sealed record CustomPropertiesResult(
    [property: JsonPropertyName("path")]               string Path,
    [property: JsonPropertyName("sheetSetProperties")] IReadOnlyDictionary<string, string> SheetSetProperties,
    [property: JsonPropertyName("count")]              int Count,
    [property: JsonPropertyName("note")]               string Note);

// ─────────── writes ───────────
//
// Every write result carries `before`. A caller that changed the wrong sheet can put it back
// without a prior read, and a caller reading the result can tell "set it to A-102" from "it was
// already A-102" - which the new value alone cannot distinguish.

public sealed record SheetWriteArgs(
    [property: JsonPropertyName("path")]  string Path,
    [property: JsonPropertyName("sheet")] string Sheet,
    [property: JsonPropertyName("value")] string Value);

public sealed record SheetFlagArgs(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("sheet")]     string Sheet,
    [property: JsonPropertyName("doNotPlot")] bool DoNotPlot);

public sealed record SheetNumberResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("sheet")]  string Sheet,
    [property: JsonPropertyName("before")] string Before,
    [property: JsonPropertyName("number")] string Number);

// No `sheet` field, unlike its siblings: this tool changes the name, so "the sheet's name" is
// ambiguous in its own result. `before` and `name` are the old and new names, and `number` is the
// identifier that did not move.
public sealed record SheetRenameResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("before")] string Before,
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("number")] string Number);

public sealed record SheetTitleResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("sheet")]  string Sheet,
    [property: JsonPropertyName("before")] string Before,
    [property: JsonPropertyName("title")]  string Title);

public sealed record SheetDoNotPlotResult(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("sheet")]     string Sheet,
    [property: JsonPropertyName("before")]    bool Before,
    [property: JsonPropertyName("doNotPlot")] bool DoNotPlot,
    [property: JsonPropertyName("note")]      string Note);
