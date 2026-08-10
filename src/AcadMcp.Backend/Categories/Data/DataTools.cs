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
}
