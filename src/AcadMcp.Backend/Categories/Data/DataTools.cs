// MCP tool surface for the acad-data category.
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results).
//
// Two storage mechanisms, and choosing between them is the thing an agent gets wrong:
//
//   XDATA hangs off ONE ENTITY, is filed under a registered application name, holds a flat list
//   of typed values, and is capped at 16 KB per entity per application. It travels with the
//   entity through copy.
//
//   DICTIONARIES are drawing-wide, or hang off one entity as its extension dictionary, hold
//   NAMED entries (usually Xrecords), nest, and have no cap worth worrying about.
//
// A few values belonging to one object are xdata; a structure, or anything shared between
// objects, is a dictionary. Every description here repeats that, because picking the wrong one
// is not an error that shows up until the data has to be found again.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Data;

public static class DataTools
{
    private const int T_NORMAL = 15_000;

    [McpTool("attach_xdata", "Attach extended data to an entity under an application name. Xdata is how an application stores its own information ON an entity - a cost code, a revision, an ID from another system - so that it travels with the entity when it is copied. The application name is what keeps two applications from treading on each other, and it is REGISTERED automatically here because AutoCAD refuses xdata filed under an unregistered name. Each value carries an explicit `type`: string, real, int, point, layer or handle. The type is given rather than guessed because JSON cannot tell 1 from 1.0 and AutoCAD very much can - an int stored where a real was meant reads back as a different type. The values are read back after writing and reported, so a write that did not take cannot look like one that did. HARD LIMIT: 16 KB per entity per application, which a long list of strings reaches sooner than it looks; anything larger belongs in a dictionary. Xdata already on the entity under other application names is left alone, and any found is listed in the result.", "data",
        Intent = new[] { "attach xdata to this entity", "store custom data on an object",
                         "dopisz dane rozszerzone do obiektu", "tag an entity with an id",
                         "zapisz wlasne dane na encji rysunku", "put application data on an entity",
                         "store a cost code on this object" },
        RequiresPlugin = true)]
    public static Task<XdataAttachResult> AttachXdata(IPluginGateway gw, XdataAttachArgs args, CancellationToken ct)
        => DataProxy.CallAsync<XdataAttachArgs, XdataAttachResult>(gw, "acad.data.attach_xdata", args, T_NORMAL, ct);

    [McpTool("get_xdata", "Read the extended data on an entity, grouped BY APPLICATION - which is how it is stored and how it is deleted, since one entity can carry data from several applications at once without them interfering. Read-only. Give an appName to see only that application's values, or omit it to see every application present. Values come back with the type they were stored as, so one written as a real reads back as a real rather than as an int that happens to be whole. An entity with no xdata reports an empty list rather than an error - most entities never have any.", "data",
        Intent = new[] { "read the xdata on this entity", "what custom data is on this object",
                         "odczytaj dane rozszerzone obiektu", "show extended data",
                         "jakie dane sa zapisane na tej encji", "get application data from an entity",
                         "check for a tag on this entity" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<XdataGetResult> GetXdata(IPluginGateway gw, XdataGetArgs args, CancellationToken ct)
        => DataProxy.CallAsync<XdataGetArgs, XdataGetResult>(gw, "acad.data.get_xdata", args, T_NORMAL, ct);

    [McpTool("delete_xdata", "Remove ONE application's extended data from an entity, leaving any other application's data on it untouched. The mechanism is AutoCAD's documented one - writing a buffer that holds only the application name - which looks like a mistake and is not, so it is done here rather than left to the caller. Verified by reading back afterwards, and the list of other applications present before and after is reported so that 'left untouched' can be checked rather than trusted. Refuses when the entity carries nothing under that name, rather than reporting a deletion that did not happen.", "data",
        Intent = new[] { "delete the xdata from this entity", "remove custom data from an object",
                         "usun dane rozszerzone z obiektu", "clear an application tag",
                         "skasuj dane aplikacji z encji", "strip extended data off an entity",
                         "remove my application data" },
        RequiresPlugin = true)]
    public static Task<XdataDeleteResult> DeleteXdata(IPluginGateway gw, XdataDeleteArgs args, CancellationToken ct)
        => DataProxy.CallAsync<XdataDeleteArgs, XdataDeleteResult>(gw, "acad.data.delete_xdata", args, T_NORMAL, ct);

    [McpTool("register_app_name", "Register an application name in the drawing so xdata can be filed under it. You rarely need this: attach_xdata registers the name it is given, because AutoCAD refuses xdata under an unregistered one. It is here for when the name should exist before any entity carries data under it. Refuses a name that is already registered rather than reporting a change that did not happen. An unreferenced name can be removed again with lisp.purge_regapps.", "data",
        Intent = new[] { "register an application name", "add a regapp to the drawing",
                         "zarejestruj nazwe aplikacji", "create an xdata application name",
                         "dodaj nazwe aplikacji do rysunku", "regapp",
                         "prepare an app name for xdata" },
        RequiresPlugin = true)]
    public static Task<AppRegisterResult> RegisterAppName(IPluginGateway gw, AppNameArgs args, CancellationToken ct)
        => DataProxy.CallAsync<AppNameArgs, AppRegisterResult>(gw, "acad.data.register_app_name", args, T_NORMAL, ct);

    [McpTool("list_registered_apps", "List every application name registered in this drawing. Read-only. ACAD is AutoCAD's own and is always present. Worth knowing: a name being registered does NOT mean any entity carries data under it - registration and use are separate, which is precisely why lisp.purge_regapps has something to do. To find out whether a name is actually in use, read the entities with get_xdata.", "data",
        Intent = new[] { "list registered application names", "what regapps are in this drawing",
                         "lista zarejestrowanych aplikacji", "show xdata application names",
                         "jakie nazwy aplikacji sa w rysunku", "which applications have touched this drawing",
                         "list regapps" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<AppListResult> ListRegisteredApps(IPluginGateway gw, DataNoArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DataNoArgs, AppListResult>(gw, "acad.data.list_registered_apps", args, T_NORMAL, ct);

    [McpTool("create_extension_dictionary", "Give an entity its own dictionary. An extension dictionary belongs to ONE entity and travels with it - copy the entity and the data comes too - which is the difference from the drawing-wide named objects dictionary. Against xdata the trade is: no 16 KB cap, NAMED entries rather than a flat list, and nesting, at the cost of not being filed under an application name. An entity does not have one until something puts it there, and it can only have one, so this refuses if it already has it rather than silently reusing it. Afterwards, address it by passing the ENTITY's handle to the dictionary and xrecord tools.", "data",
        Intent = new[] { "create an extension dictionary on this entity", "give an object its own dictionary",
                         "utworz slownik rozszerzen dla obiektu", "attach a dictionary to an entity",
                         "dodaj slownik do encji rysunku", "store structured data on one object",
                         "extension dictionary" },
        RequiresPlugin = true)]
    public static Task<ExtDictResult> CreateExtensionDictionary(IPluginGateway gw, EntityHandleArgs args, CancellationToken ct)
        => DataProxy.CallAsync<EntityHandleArgs, ExtDictResult>(gw, "acad.data.create_extension_dictionary", args, T_NORMAL, ct);

    [McpTool("list_dictionaries", "List the entries of a dictionary. Read-only. Without a handle this lists the drawing-wide NAMED OBJECTS dictionary, where AutoCAD keeps layouts, groups, plot settings, materials and much else - so a fresh drawing is far from empty and most of what is in there is not yours. With an entity handle it lists that entity's extension dictionary instead. `path` walks nested dictionaries, with / between names. Each entry reports its class and whether it is itself a dictionary, so you can tell what can be read with read_xrecord and what has to be walked into.", "data",
        Intent = new[] { "list the dictionaries in this drawing", "what is in the named objects dictionary",
                         "lista slownikow w rysunku", "show dictionary entries",
                         "jakie wpisy sa w slowniku", "browse the drawing dictionaries",
                         "list entries of an extension dictionary" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<DictListResult> ListDictionaries(IPluginGateway gw, DictListArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DictListArgs, DictListResult>(gw, "acad.data.list_dictionaries", args, T_NORMAL, ct);

    [McpTool("get_dictionary_entry", "Read one entry from a dictionary by key. Read-only. An entry can be any database object: when it is an XRECORD its values are decoded and returned, when it is a nested dictionary the entry count is given instead and `path` reaches inside it, and anything else reports its class so you know what you are looking at rather than getting an empty result. Without a handle the key is looked up in the drawing-wide named objects dictionary; with one, in that entity's extension dictionary.", "data",
        Intent = new[] { "read a dictionary entry", "get the value stored under this key",
                         "odczytaj wpis ze slownika", "what is stored under this dictionary key",
                         "pobierz wartosc z slownika rysunku", "look up a named record",
                         "read a stored setting" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<DictGetResult> GetDictionaryEntry(IPluginGateway gw, DictEntryArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DictEntryArgs, DictGetResult>(gw, "acad.data.get_dictionary_entry", args, T_NORMAL, ct);

    [McpTool("set_dictionary_entry", "Store a named entry in a dictionary - an xrecord of typed values by default, or a nested sub-dictionary with `nested` true. IMPORTANT: this REPLACES an entry of the same name, so the result reports whether something was overwritten; nothing else would tell you. The entry is read back through a fresh dictionary lookup rather than by trusting the object just written. Without a handle the entry goes in the drawing-wide named objects dictionary; with one, in that entity's extension dictionary, which must exist already - create_extension_dictionary makes it. Use create_xrecord instead when the key must NOT already exist.", "data",
        Intent = new[] { "store a value in a dictionary", "save a setting under a key",
                         "zapisz wpis w slowniku", "put a named record in the drawing",
                         "dodaj wartosc do slownika rysunku", "create a nested dictionary",
                         "persist some data by name" },
        RequiresPlugin = true)]
    public static Task<DictSetResult> SetDictionaryEntry(IPluginGateway gw, DictSetArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DictSetArgs, DictSetResult>(gw, "acad.data.set_dictionary_entry", args, T_NORMAL, ct);

    [McpTool("delete_dictionary_entry", "Remove a named entry from a dictionary and erase the object it pointed at - removing the name alone would leave an orphan in the drawing with nothing referring to it. A nested dictionary that still holds entries is REFUSED unless `force` is set, because deleting it takes everything inside with it; the number of entries that would go is reported in the refusal. Verified by reading back afterwards.", "data",
        Intent = new[] { "delete a dictionary entry", "remove a stored key",
                         "usun wpis ze slownika", "delete a named record",
                         "skasuj klucz ze slownika rysunku", "clean up stored data",
                         "remove a nested dictionary" },
        RequiresPlugin = true)]
    public static Task<DictDeleteResult> DeleteDictionaryEntry(IPluginGateway gw, DictDeleteArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DictDeleteArgs, DictDeleteResult>(gw, "acad.data.delete_dictionary_entry", args, T_NORMAL, ct);

    [McpTool("create_xrecord", "Create a new xrecord - a named list of typed values living in a dictionary. This is the way to store a structure that is too large or too widely shared for xdata: there is no 16 KB cap, and entries are named rather than positional. REFUSES a key that already exists, so a repeated call cannot quietly overwrite something; update_xrecord is for replacing contents. Each value carries an explicit type - string, real, int, point, layer or handle - because JSON cannot tell 1 from 1.0. `xlateReferences` controls whether handles inside are translated when the drawing is bound or inserted elsewhere; leave it false unless the values really are handles that must follow. Read back after writing.", "data",
        Intent = new[] { "create an xrecord", "store a structured record in the drawing",
                         "utworz xrecord w slowniku", "save a list of values by name",
                         "zapisz strukture danych w rysunku", "store data too big for xdata",
                         "make a named data record" },
        RequiresPlugin = true)]
    public static Task<XrecordCreateResult> CreateXrecord(IPluginGateway gw, XrecordCreateArgs args, CancellationToken ct)
        => DataProxy.CallAsync<XrecordCreateArgs, XrecordCreateResult>(gw, "acad.data.create_xrecord", args, T_NORMAL, ct);

    [McpTool("read_xrecord", "Read the values from an xrecord by key. Read-only. Values come back with the type they were stored as, so one written as a real reads back as a real and not as an int that happens to be whole - which is why the type is given explicitly on the way in. Refuses when the key holds something that is not an xrecord, naming what it actually is, rather than returning an empty list.", "data",
        Intent = new[] { "read an xrecord", "get the values stored under this name",
                         "odczytaj xrecord ze slownika", "read back my stored structure",
                         "pobierz zapisane dane z rysunku", "load a named data record",
                         "what values are in this xrecord" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<XrecordReadResult> ReadXrecord(IPluginGateway gw, XrecordReadArgs args, CancellationToken ct)
        => DataProxy.CallAsync<XrecordReadArgs, XrecordReadResult>(gw, "acad.data.read_xrecord", args, T_NORMAL, ct);

    [McpTool("update_xrecord", "Replace the contents of an existing xrecord. IMPORTANT: the contents are REPLACED, not merged - an xrecord holds one list and writing a new one discards the old, so the previous value count is reported to make that visible. Refuses a key that does not exist, so a typo cannot quietly create a second record alongside the one you meant; create_xrecord is for making a new one. Read back after writing.", "data",
        Intent = new[] { "update an xrecord", "change the values stored under this name",
                         "zaktualizuj xrecord", "overwrite a stored record",
                         "zmien zapisane dane w rysunku", "replace the contents of a data record",
                         "modify stored values" },
        RequiresPlugin = true)]
    public static Task<XrecordUpdateResult> UpdateXrecord(IPluginGateway gw, XrecordUpdateArgs args, CancellationToken ct)
        => DataProxy.CallAsync<XrecordUpdateArgs, XrecordUpdateResult>(gw, "acad.data.update_xrecord", args, T_NORMAL, ct);

    [McpTool("tag_entities", "Tag one or more entities with a name, and optionally a value - the quick way to mark a set of objects so they can be found again later. A tag IS xdata under the reserved application name TOOLBANK_TAG, which matters: the tag travels with the entity when it is copied, can be read by get_xdata like any other extended data, and shows up in list_registered_apps rather than hiding in a private format. ONE tag per entity - tagging again REPLACES, and the number of entities that already carried a tag is reported, because nothing else would tell you. Find them again with list_tagged_entities, or with query_by_property using hasXdataApp.", "data",
        Intent = new[] { "tag these entities", "mark objects so i can find them later",
                         "oznacz obiekty tagiem", "label a set of entities",
                         "otaguj encje na rysunku", "mark these as reviewed",
                         "add a tag to selected objects" },
        RequiresPlugin = true)]
    public static Task<TagResult> TagEntities(IPluginGateway gw, TagArgs args, CancellationToken ct)
        => DataProxy.CallAsync<TagArgs, TagResult>(gw, "acad.data.tag_entities", args, T_NORMAL, ct);

    [McpTool("list_tagged_entities", "Find the entities carrying a tag. Read-only. Name a tag to filter to it, or omit one to list everything tagged. Model space only, and erased entities are skipped. The result also carries a tagsInDrawing summary counting every tag present regardless of the filter, which is the quick way to see what tags exist before querying one of them - useful because a tag that was never applied and a tag spelled slightly differently look the same from the outside.", "data",
        Intent = new[] { "list tagged entities", "which objects have this tag",
                         "znajdz otagowane obiekty", "show everything i marked",
                         "jakie tagi sa na rysunku", "find entities by tag",
                         "what did i tag as reviewed" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<TagListResult> ListTaggedEntities(IPluginGateway gw, TagListArgs args, CancellationToken ct)
        => DataProxy.CallAsync<TagListArgs, TagListResult>(gw, "acad.data.list_tagged_entities", args, T_NORMAL, ct);

    [McpTool("query_by_property", "Find entities in model space by their properties: layer, object class, colour index, linetype, or whether they carry xdata from a named application. Read-only. Every filter given must match - they are ANDed, not ORed - and at least one is required, since a query with no filter would return the whole drawing. objectClass matches on SUBSTRING, so 'Line' finds AcDbLine and AcDbPolyline both; give the full class name to be exact. hasXdataApp is how you find everything one application or tool has touched. The number of entities scanned is reported alongside the number matched, so a small result can be told from an empty drawing.", "data",
        Intent = new[] { "find entities by layer", "query objects by property",
                         "znajdz obiekty po warstwie", "which entities are on this layer",
                         "wyszukaj encje po wlasciwosciach", "find all circles in the drawing",
                         "select objects by colour" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<QueryResult> QueryByProperty(IPluginGateway gw, QueryArgs args, CancellationToken ct)
        => DataProxy.CallAsync<QueryArgs, QueryResult>(gw, "acad.data.query_by_property", args, T_NORMAL, ct);

    [McpTool("export_table_to_csv", "Write a table entity out as a .csv file. Refuses to overwrite an existing file unless `overwrite` is set. IMPORTANT, and it is not an AutoCAD wrapper: Table.ExportToCsv does not exist in the managed API, so this reads the cells and writes the file itself. Two consequences worth knowing - cell text is taken AS DISPLAYED, so a formula exports as its result rather than as the formula; and fields containing a comma, a quote or a newline are properly quoted with interior quotes doubled, without which a cell containing a comma silently becomes two columns. UTF-8 with no byte-order mark.", "data",
        Intent = new[] { "export this table to csv", "save a table as a csv file",
                         "eksportuj tabele do csv", "get the table data out as a file",
                         "zapisz tabele do pliku csv", "dump a table to a spreadsheet file",
                         "export table data" },
        RequiresPlugin = true)]
    public static Task<CsvExportResult> ExportTableToCsv(IPluginGateway gw, TableCsvArgs args, CancellationToken ct)
        => DataProxy.CallAsync<TableCsvArgs, CsvExportResult>(gw, "acad.data.export_table_to_csv", args, T_NORMAL, ct);

    [McpTool("import_csv_to_table", "Fill an existing table entity from a .csv file. IMPORTANT: the table is RESIZED to the CSV and its previous contents are REPLACED - this fills a table, it does not merge into one. Table.ImportFromCsv does not exist in the managed API, so the parsing is done here: quoted fields are handled properly, so a cell containing a comma stays one cell, and ragged rows are padded to the widest row rather than refused. Everything lands as TEXT, so a column of numbers arrives as text that looks like numbers - which is what a CSV actually contains. Create the table first with annotations.add_table.", "data",
        Intent = new[] { "import a csv into this table", "fill a table from a csv file",
                         "wczytaj csv do tabeli", "load spreadsheet data into a table",
                         "zaimportuj dane z pliku csv do tabeli", "populate a table from a file",
                         "put csv data in the drawing" },
        RequiresPlugin = true)]
    public static Task<CsvImportResult> ImportCsvToTable(IPluginGateway gw, TableCsvArgs args, CancellationToken ct)
        => DataProxy.CallAsync<TableCsvArgs, CsvImportResult>(gw, "acad.data.import_csv_to_table", args, T_NORMAL, ct);

    [McpTool("create_data_link", "Create a data link - a named record of WHERE table data comes from. It fetches nothing by itself: link_table_to_source points a table at it and update_data_link pulls the values through. `path` is the spreadsheet, and `range` optionally narrows it to a sheet or cell range, joined to the path with ! as AutoCAD does. The file must exist, because a link to a missing file would fail only later when something tried to read it. `adapter` defaults to AcExcel, which is the only adapter AutoCAD ships. MEASURED, and it decides whether this tool is any use to you: the Excel adapter ACCEPTS a plain .csv, but does NOT split it on commas - each line arrives whole in a single cell. A CSV data link therefore gives you raw lines, not a table. For a real grid use a .xlsx, or use import_csv_to_table instead, which parses the CSV properly but does not stay linked. Refuses a name already in use rather than replacing a link other tables may be pointing at. Read back through the manager by name after creation, which is a different route from the one that made it.", "data",
        Intent = new[] { "create a data link to a spreadsheet", "link a drawing to an excel file",
                         "utworz lacze do arkusza", "set up a data link",
                         "polacz rysunek z plikiem excel", "make a link to external table data",
                         "connect a table to a spreadsheet file" },
        RequiresPlugin = true)]
    public static Task<DataLinkCreateResult> CreateDataLink(IPluginGateway gw, DataLinkCreateArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DataLinkCreateArgs, DataLinkCreateResult>(gw, "acad.data.create_data_link", args, T_NORMAL, ct);

    [McpTool("list_data_links", "List the data links defined in this drawing, with the connection string each one reads from. Read-only. Listed by walking the ACAD_DATALINK dictionary, because DataLinkManager.GetDataLink takes a NAME and offers no way to enumerate - the dictionary is where the links actually live. A link appearing here says nothing about whether its source file still exists; the connection string is reported so that can be checked.", "data",
        Intent = new[] { "list the data links in this drawing", "what spreadsheets is this drawing linked to",
                         "lista laczy do danych", "show data links",
                         "jakie lacza sa w rysunku", "which external files feed this drawing",
                         "find broken data links" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<DataLinkListResult> ListDataLinks(IPluginGateway gw, DataNoArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DataNoArgs, DataLinkListResult>(gw, "acad.data.list_data_links", args, T_NORMAL, ct);

    [McpTool("link_table_to_source", "Point a table cell at a data link, so the table can follow a spreadsheet. The link attaches to ONE CELL, which is the anchor AutoCAD fills outwards from - Table.Cells[row,column].DataLink is the current API and the whole-table SetDataLink is obsolete and names this as its replacement. Row and column default to 0,0. MEASURED: attaching ALREADY pulls the data through - so the first update_data_link afterwards will correctly report changed=false, having nothing left to do. That is not a failure. update_data_link earns its place when the source has changed since. The cell is verified to read back with the link attached.", "data",
        Intent = new[] { "link this table to a data link", "attach a spreadsheet link to a table",
                         "podlacz tabele do lacza danych", "make this table follow the excel file",
                         "polacz tabele ze zrodlem danych", "point a table at external data",
                         "bind a table to a spreadsheet" },
        RequiresPlugin = true)]
    public static Task<DataLinkAttachResult> LinkTableToSource(IPluginGateway gw, DataLinkAttachArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DataLinkAttachArgs, DataLinkAttachResult>(gw, "acad.data.link_table_to_source", args, T_NORMAL, ct);

    [McpTool("unlink_table", "Detach the data link from a table cell, so the table stops following its source. IMPORTANT: the cell KEEPS the values it was last given - unlinking stops the table updating, it does not empty it. Note for anyone extending this: the working route is Cell.RemoveDataLink(), and assigning ObjectId.Null to Cell.DataLink is eInvalidInput; the state reads back through Cell.IsLinked, which is a bool? rather than a bool. The data link object itself stays in the drawing and can be attached again; list_data_links still shows it. Refuses a cell that carries no link rather than reporting a change that did not happen.", "data",
        Intent = new[] { "unlink this table", "stop a table following its spreadsheet",
                         "odlacz tabele od zrodla", "detach the data link from a table",
                         "usun powiazanie tabeli z plikiem", "break the link but keep the values",
                         "disconnect a table from excel" },
        RequiresPlugin = true)]
    public static Task<DataLinkUnlinkResult> UnlinkTable(IPluginGateway gw, DataLinkCellArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DataLinkCellArgs, DataLinkUnlinkResult>(gw, "acad.data.unlink_table", args, T_NORMAL, ct);

    [McpTool("update_data_link", "Pull data through a table's link - or push it back. `direction` fromSource (the default) reads the spreadsheet into the table; toSource writes the table back out to the spreadsheet, which changes a file outside the drawing and should be used deliberately. Runs through Table.UpdateDataLink, because DataLinkManager.UpdateDataLink does not exist. AutoCAD returns no status of its own, so the first row is reported BEFORE and AFTER with a `changed` flag - without that, an update that fetched nothing would look exactly like one that worked. Note that changed=false is not necessarily a failure: it also means the table already matched its source.", "data",
        Intent = new[] { "update the data link", "refresh this table from its spreadsheet",
                         "odswiez lacze danych w tabeli", "pull the latest data into the table",
                         "zaktualizuj tabele ze zrodla", "write the table back to excel",
                         "refresh linked table data" },
        RequiresPlugin = true)]
    public static Task<DataLinkUpdateResult> UpdateDataLink(IPluginGateway gw, DataLinkUpdateArgs args, CancellationToken ct)
        => DataProxy.CallAsync<DataLinkUpdateArgs, DataLinkUpdateResult>(gw, "acad.data.update_data_link", args, T_NORMAL, ct);
}
