// MCP tools for acad-sheetsets — roadmap 2.1. Six reads, then four writes.
//
// The reads came first on purpose. `fields.insert_field_sheet_set_property` shipped long ago and
// was dead ever since, because nothing could read a sheet-set property for it to bind to. Six
// read tools needed none of the save discipline the writes do, and they turned that field live.
//
// The writes then had to establish what "saved" even means here, which took measuring rather
// than reading: the commit is UnlockDb(db, bCommit: true), a sheet's name is composed from its
// number and title rather than stored, and a title cannot be set to "" although it can be
// cleared. None of the three is what the API's shape suggests.
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

    // Renaming a sheet means setting its number, its title, or both - AutoCAD's own command is
    // "Rename & Renumber Sheet". A sheet has no separately stored name: what the Sheet Set
    // Manager shows is number and title composed together. Measured one variable at a time -
    // changing only the title moved the displayed name, and SetName moved nothing.
    //
    // Both fields go under ONE lock, so a caller cannot end up renumbered but not retitled
    // because a second call failed.
    [McpTool("rename_sheet", "Rename and renumber a sheet in one locked write - AutoCAD's own \"Rename & Renumber Sheet\". Pass number, title, or both; at least one is required. A sheet has NO separately stored name: what the Sheet Set Manager displays is its number and title composed together, so those two are what renaming a sheet actually sets. Answers with all three fields as they were and as they now are. Pass \"\" as the title to clear it.", "sheetsets",
        Intent = new[] { "zmien nazwe arkusza", "przemianuj arkusz", "rename a sheet",
                         "change sheet name", "inna nazwa arkusza w zestawie",
                         "popraw nazwe arkusza", "zmien numer i tytul arkusza naraz",
                         "rename and renumber a sheet" },
        RequiresPlugin = true)]
    public static Task<SheetRenameResult> RenameSheet(IPluginGateway gw, SheetRenameArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetRenameArgs, SheetRenameResult>(gw, "acad.sheetsets.rename_sheet", args, T_NORMAL, ct);

    [McpTool("set_sheet_title", "Set a sheet's title - the descriptive line a title block prints under the number, such as 'Ground Floor Plan'. Writes to the shared .DST under a lock. The sheet's displayed name is composed from its number and title, so setting the title moves that name too. Pass \"\" to clear the title. AutoCAD itself rejects an empty title, so the tool sends a space and the file stores it as empty - the result reports the \"\" that will be on disk, not the space that is briefly in memory.", "sheetsets",
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

    // ─────────── subsets: how a real set is organised ───────────
    //
    // A subset is addressed by its bare name or by its full "Parent / Child" path, because
    // list_subsets reports both and a caller should be able to paste either back.

    [McpTool("create_subset", "Create a subset in a sheet set - the discipline or phase folder a real project organises its sheets into, such as Architectural or Phase 2. Nests inside another subset when parent names one, otherwise sits at the top level of the set. Writes to the shared .DST under a lock. Refuses a name already in use, because subset names are how move_sheet_to_subset addresses them and a duplicate would make that ambiguous.", "sheetsets",
        Intent = new[] { "utworz podzestaw arkuszy", "dodaj folder branzowy do zestawu",
                         "create a subset in a sheet set", "add a discipline folder",
                         "nowy podzestaw", "organise sheets into groups" },
        RequiresPlugin = true)]
    public static Task<SubsetCreateResult> CreateSubset(IPluginGateway gw, SubsetCreateArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SubsetCreateArgs, SubsetCreateResult>(gw, "acad.sheetsets.create_subset", args, T_NORMAL, ct);

    // Empty-only, deliberately. What RemoveSubset does with the sheets inside is undocumented and
    // unmeasured, and the possibilities include deleting them. This bank has twice shipped a tool
    // that reported the opposite of the truth by guessing at undocumented behaviour; the cost of
    // guessing wrong HERE would be somebody's sheets.
    [McpTool("delete_subset", "Delete an EMPTY subset from a sheet set. Refuses while it still holds sheets and reports how many, because what AutoCAD does with the sheets inside a removed subset is not documented and could include deleting them - move them out with move_sheet_to_subset first. Sheets are never removed by this tool. Writes to the shared .DST under a lock.", "sheetsets",
        Intent = new[] { "usun podzestaw", "skasuj folder w zestawie arkuszy",
                         "delete a subset", "remove a sheet set subset",
                         "pozbadz sie pustego podzestawu", "usun grupe arkuszy" },
        RequiresPlugin = true)]
    public static Task<SubsetDeleteResult> DeleteSubset(IPluginGateway gw, SubsetArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SubsetArgs, SubsetDeleteResult>(gw, "acad.sheetsets.delete_subset", args, T_NORMAL, ct);

    [McpTool("move_sheet_to_subset", "Move one sheet into a subset, or back to the top level of the sheet set when subset is omitted. The sheet is re-parented rather than copied, so the set's total sheet count does not change. Identify the sheet by its name or its number, and the subset by its bare name or its full 'Parent / Child' path. Writes to the shared .DST under a lock.", "sheetsets",
        Intent = new[] { "przenies arkusz do podzestawu", "przypisz arkusz do branzy",
                         "move a sheet into a subset", "reorganise sheets between subsets",
                         "wyjmij arkusz z podzestawu", "assign sheet to a discipline" },
        RequiresPlugin = true)]
    public static Task<MoveSheetResult> MoveSheetToSubset(IPluginGateway gw, MoveSheetArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<MoveSheetArgs, MoveSheetResult>(gw, "acad.sheetsets.move_sheet_to_subset", args, T_NORMAL, ct);

    // ─────────── custom properties: the data a title block binds to ───────────
    //
    // This is the half of the category that makes `fields.insert_field_sheet_set_property`
    // worth having. Reading a property was enough to make that field render; writing one is
    // what lets an agent fill a title block rather than only quote it.

    [McpTool("set_sheet_property", "Set a custom property on ONE sheet - the per-sheet value a title block prints, such as its revision or who checked it. Writes to the shared .DST under a lock and creates the property on that sheet if it does not exist yet. Refuses the built-in fields name, number, title and description, naming the tool that sets each, because writing one here would create a second property sharing the name and only one of them would mean anything. Answers with the previous value and whether it was created.", "sheetsets",
        Intent = new[] { "ustaw wlasciwosc arkusza", "wpisz rewizje do arkusza",
                         "set a custom property on a sheet", "fill a title block field",
                         "kto sprawdzil ten arkusz", "per-sheet custom value" },
        RequiresPlugin = true)]
    public static Task<SetSheetPropertyResult> SetSheetProperty(IPluginGateway gw, SetSheetPropertyArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SetSheetPropertyArgs, SetSheetPropertyResult>(gw, "acad.sheetsets.set_sheet_property", args, T_NORMAL, ct);

    [McpTool("define_custom_property", "Define a custom property on the sheet set - the project-wide data a title block binds to, such as client, project number or issue date. scope='sheetSet' (the default) means one value shared by the whole project; scope='sheet' means every sheet carries its own, which set_sheet_property then fills in per sheet. Writes to the shared .DST under a lock. Setting an existing property updates its value and keeps the scope it already had, so an update never silently re-scopes it.", "sheetsets",
        Intent = new[] { "zdefiniuj wlasciwosc zestawu arkuszy", "dodaj numer projektu do zestawu",
                         "define a sheet set custom property", "project-wide title block data",
                         "nowa wlasciwosc dla wszystkich arkuszy", "add a custom property" },
        RequiresPlugin = true)]
    public static Task<DefinePropertyResult> DefineCustomProperty(IPluginGateway gw, DefinePropertyArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<DefinePropertyArgs, DefinePropertyResult>(gw, "acad.sheetsets.define_custom_property", args, T_NORMAL, ct);

    // ─────────── order and removal ───────────

    [McpTool("reorder_sheet", "Move a sheet up or down the drawing list, placing it before or after another sheet. Exactly one of before or after is required; both name a sheet by its name or number. Ordering happens WITHIN one subset - if the two sheets sit in different subsets the tool refuses and points at move_sheet_to_subset, so that 'put A-102 after A-101' can never quietly relocate a sheet. Writes to the shared .DST under a lock.", "sheetsets",
        Intent = new[] { "zmien kolejnosc arkuszy", "przesun arkusz w gore listy",
                         "reorder sheets in a sheet set", "put this sheet after that one",
                         "uporzadkuj liste rysunkow", "change drawing list order" },
        RequiresPlugin = true)]
    public static Task<ReorderResult> ReorderSheet(IPluginGateway gw, ReorderArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<ReorderArgs, ReorderResult>(gw, "acad.sheetsets.reorder_sheet", args, T_NORMAL, ct);

    [McpTool("remove_sheet", "Remove a sheet from the sheet set. This removes the set's REFERENCE to a layout - the layout itself, and the drawing file holding it, are left exactly as they were, so nothing is destroyed and the sheet can be added back. Identify it by name or number. Answers with how many sheets remain. Writes to the shared .DST under a lock.", "sheetsets",
        Intent = new[] { "usun arkusz z zestawu", "wyrzuc arkusz z listy rysunkow",
                         "remove a sheet from the sheet set", "take a sheet out of the set",
                         "arkusz nie nalezy juz do zestawu", "drop a sheet from the drawing list" },
        RequiresPlugin = true)]
    public static Task<RemoveSheetResult> RemoveSheet(IPluginGateway gw, SheetRefArgs args, CancellationToken ct)
        => SheetSetsProxy.CallAsync<SheetRefArgs, RemoveSheetResult>(gw, "acad.sheetsets.remove_sheet", args, T_NORMAL, ct);
}
