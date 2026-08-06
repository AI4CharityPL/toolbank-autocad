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

    // ─────────── writes: the second tranche ───────────
    //
    // A .DST is a SHARED file. Rule 45 §1: each of these locks it, mutates, commits and unlocks
    // inside the one call, and reports who holds the lock rather than an HRESULT when it cannot.
    // The commit is UnlockDb(db, bCommit: true) and it happens on the success path, so a failed
    // save reaches the caller instead of being swallowed by a finally.
    //
    // Every one of them answers with `before`, so the change is reversible from its own result.

    [McpTool("set_sheet_number", "Renumber one sheet in a sheet set - the 'A-101' that appears in the title block and orders the drawing list. Writes to the shared .DST: it is locked for the call, saved, and unlocked. Addresses the sheet by its current name OR its current number, and answers with both the old and the new number so the edit can be undone from the result alone.", "sheetsets",
        Intent = new[] { "zmien numer arkusza", "przenumeruj arkusz", "ustaw numer arkusza na",
                         "set sheet number", "renumber a sheet", "change drawing number",
                         "nadaj arkuszowi numer A-101" },
        RequiresPlugin = true)]
    public static Task<SheetNumberResult> SetSheetNumber(IPluginGateway gw, SheetWriteArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetWriteArgs, SheetNumberResult>(gw, "acad.sheetsets.set_sheet_number", args, T_NORMAL, ct);

    [McpTool("rename_sheet", "Rename one sheet in a sheet set. The name is what the Sheet Set Manager tree shows; it is separate from the sheet's number and from its title, and changing it does not touch either. Writes to the shared .DST under a lock. Answers with the old name, the new name and the sheet's number, which is the identifier that did not move.", "sheetsets",
        Intent = new[] { "zmien nazwe arkusza", "przemianuj arkusz", "rename a sheet",
                         "change sheet name", "inna nazwa arkusza w zestawie",
                         "popraw nazwe arkusza" },
        RequiresPlugin = true)]
    public static Task<SheetRenameResult> RenameSheet(IPluginGateway gw, SheetWriteArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetWriteArgs, SheetRenameResult>(gw, "acad.sheetsets.rename_sheet", args, T_NORMAL, ct);

    [McpTool("set_sheet_title", "Set a sheet's title - the descriptive line a title block prints under the number, such as 'Ground Floor Plan'. Distinct from the sheet's name, which is the Sheet Set Manager label. Writes to the shared .DST under a lock. Pass an empty string to clear the title, which is a different request from omitting the argument and is treated as one.", "sheetsets",
        Intent = new[] { "ustaw tytul arkusza", "zmien tytul arkusza", "opis arkusza w tabliczce",
                         "set sheet title", "change the sheet title", "nazwa rysunku na arkuszu" },
        RequiresPlugin = true)]
    public static Task<SheetTitleResult> SetSheetTitle(IPluginGateway gw, SheetWriteArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetWriteArgs, SheetTitleResult>(gw, "acad.sheetsets.set_sheet_title", args, T_NORMAL, ct);

    [McpTool("set_sheet_do_not_plot", "Mark one sheet do-not-plot, or clear that mark. The Publisher skips a do-not-plot sheet rather than failing the job, so this is how a sheet is held back from an issue without being removed from the set. Writes to the shared .DST under a lock. Pass doNotPlot=false to put the sheet back into the next publish.", "sheetsets",
        Intent = new[] { "wylacz arkusz z drukowania", "nie drukuj tego arkusza",
                         "oznacz arkusz jako niedrukowalny", "set sheet do not plot",
                         "exclude a sheet from publishing", "przywroc arkusz do drukowania" },
        RequiresPlugin = true)]
    public static Task<SheetDoNotPlotResult> SetSheetDoNotPlot(IPluginGateway gw, SheetFlagArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetFlagArgs, SheetDoNotPlotResult>(gw, "acad.sheetsets.set_sheet_do_not_plot", args, T_NORMAL, ct);
}
