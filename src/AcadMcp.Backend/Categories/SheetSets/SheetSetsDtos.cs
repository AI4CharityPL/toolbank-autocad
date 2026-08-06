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

public sealed record SheetRenameArgs(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("sheet")]  string Sheet,
    [property: JsonPropertyName("number")] string? Number = null,
    [property: JsonPropertyName("title")]  string? Title = null);

/// <summary>The three identifying fields, reported together before and after a rename.</summary>
/// <remarks>
/// `name` is included but is NOT settable: it is composed from number + title. Measured by
/// changing one at a time - setting only the title moved the name, and SetName did nothing.
/// Reporting it anyway is what lets a caller see that its rename landed where it expected.
/// </remarks>
public sealed record SheetIdentity(
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("title")]  string Title,
    [property: JsonPropertyName("name")]   string Name);

public sealed record SheetRenameResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("before")] SheetIdentity Before,
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("title")]  string Title,
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("note")]   string Note);

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

// ─────────── subsets ───────────

public sealed record SubsetCreateArgs(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("parent")]      string? Parent = null);

public sealed record SubsetArgs(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("subset")] string Subset);

public sealed record MoveSheetArgs(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("sheet")]  string Sheet,
    [property: JsonPropertyName("subset")] string? Subset = null);

public sealed record SubsetCreateResult(
    [property: JsonPropertyName("path")]             string Path,
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("description")]      string Description,
    [property: JsonPropertyName("parent")]           string Parent,
    [property: JsonPropertyName("parentIsSheetSet")] bool ParentIsSheetSet,
    [property: JsonPropertyName("sheetCount")]       int SheetCount);

public sealed record SubsetDeleteResult(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("deleted")]     string Deleted,
    [property: JsonPropertyName("removedFrom")] string RemovedFrom,
    [property: JsonPropertyName("note")]        string Note);

public sealed record MoveSheetResult(
    [property: JsonPropertyName("path")]                 string Path,
    [property: JsonPropertyName("sheet")]                string Sheet,
    [property: JsonPropertyName("number")]               string Number,
    [property: JsonPropertyName("from")]                 string From,
    [property: JsonPropertyName("to")]                   string To,
    [property: JsonPropertyName("movedToSheetSetRoot")]  bool MovedToSheetSetRoot,
    [property: JsonPropertyName("note")]                 string Note);

// ─────────── custom properties ───────────

public sealed record SetSheetPropertyArgs(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("sheet")]    string Sheet,
    [property: JsonPropertyName("property")] string Property,
    [property: JsonPropertyName("value")]    string Value);

public sealed record DefinePropertyArgs(
    [property: JsonPropertyName("path")]         string Path,
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue = null,
    [property: JsonPropertyName("scope")]        string? Scope = null);

public sealed record SetSheetPropertyResult(
    [property: JsonPropertyName("path")]     string Path,
    [property: JsonPropertyName("sheet")]    string Sheet,
    [property: JsonPropertyName("property")] string Property,
    [property: JsonPropertyName("before")]   string Before,
    [property: JsonPropertyName("value")]    string Value,
    [property: JsonPropertyName("created")]  bool Created);

public sealed record DefinePropertyResult(
    [property: JsonPropertyName("path")]         string Path,
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("scope")]        string Scope,
    [property: JsonPropertyName("defaultValue")] string DefaultValue,
    [property: JsonPropertyName("before")]       string Before,
    [property: JsonPropertyName("created")]      bool Created,
    [property: JsonPropertyName("note")]         string Note);

// ─────────── order and removal ───────────

public sealed record ReorderArgs(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("sheet")]  string Sheet,
    [property: JsonPropertyName("before")] string? Before = null,
    [property: JsonPropertyName("after")]  string? After = null);

public sealed record SheetRefArgs(
    [property: JsonPropertyName("path")]  string Path,
    [property: JsonPropertyName("sheet")] string Sheet);

public sealed record ReorderResult(
    [property: JsonPropertyName("path")]           string Path,
    [property: JsonPropertyName("sheet")]          string Sheet,
    [property: JsonPropertyName("number")]         string Number,
    [property: JsonPropertyName("placed")]         string Placed,
    [property: JsonPropertyName("anchor")]         string Anchor,
    [property: JsonPropertyName("subset")]         string Subset,
    [property: JsonPropertyName("sheetsInSubset")] int SheetsInSubset);

public sealed record RemoveSheetResult(
    [property: JsonPropertyName("path")]            string Path,
    [property: JsonPropertyName("removed")]         string Removed,
    [property: JsonPropertyName("number")]          string Number,
    [property: JsonPropertyName("fromSubset")]      string FromSubset,
    [property: JsonPropertyName("sheetsRemaining")] int SheetsRemaining,
    [property: JsonPropertyName("note")]            string Note);

public sealed record AddSheetArgs(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("drawingPath")] string DrawingPath,
    [property: JsonPropertyName("layout")]      string Layout,
    [property: JsonPropertyName("number")]      string? Number = null,
    [property: JsonPropertyName("title")]       string? Title = null,
    [property: JsonPropertyName("subset")]      string? Subset = null);

public sealed record AddSheetResult(
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("number")]      string Number,
    [property: JsonPropertyName("title")]       string Title,
    [property: JsonPropertyName("layout")]      string Layout,
    [property: JsonPropertyName("drawingPath")] string DrawingPath,
    [property: JsonPropertyName("subset")]      string Subset,
    [property: JsonPropertyName("sheetCount")]  int SheetCount,
    [property: JsonPropertyName("note")]        string Note);
