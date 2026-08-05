// MCP tools for acad-sheetsets — roadmap 2.1, first tranche: read-only.
//
// Read-only on purpose, and first on purpose. `fields.insert_field_sheet_set_property` shipped
// long ago and has been dead ever since, because nothing could read a sheet-set property for it
// to bind to. Six read tools need none of the Save() discipline the write half will and they turn
// that field live.
//
// The contract this obeys is docs/engineering-rules/45-sheet-sets-com.md, written before any of
// this code. Two of its decisions are visible in the signatures below:
//
//   Every tool takes the .DST PATH. There is no open_sheet_set / close_sheet_set pair, because
//   IAcSmSheetSetMgr.FindOpenDatabase lets each call resolve the file itself. Nothing is held
//   between calls, so the question "what happens when a second client opens a different set"
//   never arises - and every stateful thing this bank has tried has had to be withdrawn.
//
//   A sheet is addressed by NAME OR NUMBER. On a real project people say "A-101", which is a
//   number, at least as often as they say the sheet's name.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.SheetSets;

public static class SheetSetsTools
{
    private const int T_FAST = 8_000;

    // Opening a .DST goes through COM and may touch a network share, which is slower than any
    // in-process database read in this bank.
    private const int T_NORMAL = 30_000;

    [McpTool("get_sheet_set_info", "Summarise a sheet set file: its name, description, how many sheets it holds and how many subsets. Read-only. Takes the .DST path - every tool in this category does, because none of them hold a sheet set open between calls. Start here to confirm a path is a readable sheet set before asking it anything else.", "sheetsets",
        Intent = new[] { "informacje o zestawie arkuszy", "ile arkuszy ma ten zestaw",
                         "sheet set info", "summarise a sheet set", "otworz zestaw arkuszy dst",
                         "what is in this sheet set" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SheetSetInfoResult> GetSheetSetInfo(IPluginGateway gw, SheetSetPathArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetSetPathArgs, SheetSetInfoResult>(gw, "acad.sheetsets.get_sheet_set_info", args, T_NORMAL, ct);

    [McpTool("get_sheet_set_path", "Confirm a .DST path resolves to a readable sheet set and report its name and description. Read-only. Cheaper than get_sheet_set_info because it does not walk the sheet tree, so it is the call to make when all you need is to validate a path before passing it to the other tools.", "sheetsets",
        Intent = new[] { "sprawdz sciezke zestawu arkuszy", "czy ten plik dst jest poprawny",
                         "validate a sheet set path", "resolve sheet set file",
                         "nazwa zestawu arkuszy z pliku", "check dst file" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SheetSetPathResult> GetSheetSetPath(IPluginGateway gw, SheetSetPathArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetSetPathArgs, SheetSetPathResult>(gw, "acad.sheetsets.get_sheet_set_path", args, T_NORMAL, ct);

    [McpTool("list_sheets", "List every sheet in a sheet set - number, name, title, description, the subset it sits under, and whether it is marked do-not-plot. Read-only. Subsets are walked recursively and each sheet reports its full subset path, so a nested set reads as a flat list an agent can act on rather than a tree it has to traverse.", "sheetsets",
        Intent = new[] { "lista arkuszy w zestawie", "jakie arkusze sa w tym zestawie",
                         "list sheets in a sheet set", "show all sheets", "numery arkuszy",
                         "sheet numbers and titles" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SheetListResult> ListSheets(IPluginGateway gw, SheetSetPathArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetSetPathArgs, SheetListResult>(gw, "acad.sheetsets.list_sheets", args, T_NORMAL, ct);

    [McpTool("list_subsets", "List the subsets of a sheet set with their full paths and how many sheets each holds directly. Read-only. Subsets are how a real set is organised by discipline or by phase, and a subset path is what move_sheet_to_subset will take once the write half of this category exists.", "sheetsets",
        Intent = new[] { "lista podzestawow", "jak podzielony jest zestaw arkuszy",
                         "list subsets of a sheet set", "sheet set organisation",
                         "podzestawy branzowe", "sheet set categories" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SubsetListResult> ListSubsets(IPluginGateway gw, SheetSetPathArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetSetPathArgs, SubsetListResult>(gw, "acad.sheetsets.list_subsets", args, T_NORMAL, ct);

    [McpTool("get_sheet_property", "Read the properties of ONE sheet - name, number, title, description, plus every custom property on it. Read-only. Identify the sheet by its NAME or by its NUMBER, since on a real project people say 'A-101' at least as often as they say a sheet's name. Name a single property to get just that one, with whether it was built in or custom; omit it to get all of them. This is the tool fields.insert_field_sheet_set_property has been waiting for.", "sheetsets",
        Intent = new[] { "wlasciwosci arkusza", "jaki numer ma ten arkusz",
                         "get a sheet property", "read sheet number and title",
                         "dane arkusza do tabliczki", "sheet custom property value" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SheetPropertyResult> GetSheetProperty(IPluginGateway gw, SheetPropertyArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetPropertyArgs, SheetPropertyResult>(gw, "acad.sheetsets.get_sheet_property", args, T_NORMAL, ct);

    [McpTool("list_custom_properties", "List the custom properties defined at SHEET SET level - the project-wide values a title block binds to, such as client or project number. Read-only. Per-sheet custom properties are reported by get_sheet_property instead, because a sheet can override the set and reporting both here would hide which value actually applies.", "sheetsets",
        Intent = new[] { "wlasne wlasciwosci zestawu arkuszy", "numer projektu w zestawie",
                         "list sheet set custom properties", "project-wide properties",
                         "co tabliczka moze pobrac z zestawu", "sheet set project data" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<CustomPropertiesResult> ListCustomProperties(IPluginGateway gw, SheetSetPathArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetSetPathArgs, CustomPropertiesResult>(gw, "acad.sheetsets.list_custom_properties", args, T_FAST, ct);
}
