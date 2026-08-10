// MCP tool surface for the acad-lisp category.
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results).
//
// The whole category is built on routes that return SYNCHRONOUSLY. SendStringToExecute queues and
// its result cannot be observed, so nothing here uses it - see LispPluginTools for the detail.
//
// define_command_alias is deliberately absent: AutoCAD exposes no API for command aliases, and the
// only route is editing the user's global acad.pgp and reloading it with RE_INIT. That is a
// permanent change to the AutoCAD installation rather than to a drawing, so it is out of scope for
// this bank rather than merely unbuilt.
//
// SIX MORE ARE WITHDRAWN, all on ONE measured root cause - eval_lisp, load_lisp_file,
// list_loaded_lisp, run_command_sequence, run_script_file and netload_assembly. Application.Invoke,
// Editor.Command and DynamicLinker.LoadModule all answer eInvalidInput when called from where this
// plugin dispatches. They require a COMMAND context; the plugin runs in APPLICATION context, on the
// UI thread inside a document lock and an open transaction. This is not a naming problem and not
// fixable by trying a different LISP form - it was tried three ways and failed identically each
// time. The documented fix is DocumentCollection.ExecuteInCommandContextAsync, which needs an async
// runner this plugin does not yet have, and that is the next tranche. The plugin handlers stay
// registered so the finding stays reproducible; they are simply not offered in the bank.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Lisp;

public static class LispTools
{
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 60_000;

    [McpTool("list_loaded_applications", "List the ARX, CRX and DBX modules AutoCAD has registered, together with its own managed core assemblies (accoremgd.dll, acmgd.dll). Read-only. MEASURED LIMIT, so that a short answer is not mistaken for a broken tool: this returns what DynamicLinker.GetLoadedModules reports, which on a normal session is about 25 entries and does NOT include .NET assemblies loaded with NETLOAD - a netloaded plugin will not appear here even though it is running. `pattern` filters the list by substring.", "lisp",
        Intent = new[] { "list loaded arx modules", "what modules has autocad loaded",
                         "jakie moduly sa zaladowane", "show the loaded arx and dbx modules",
                         "lista zaladowanych modulow arx", "which autocad modules are registered",
                         "jak sprawdzic zaladowane moduly na rysunku" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<LispModulesResult> ListLoadedApplications(IPluginGateway gw, LispSymbolsArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispSymbolsArgs, LispModulesResult>(gw, "acad.lisp.list_loaded_applications", args, T_NORMAL, ct);

    [McpTool("get_system_variable", "Read one AutoCAD system variable by name - CLAYER, OSMODE, INSUNITS and the rest. Read-only. Read straight from Application.GetSystemVariable rather than through LISP. The value comes back as text with its underlying CLR type named alongside, because a system variable can hold a string, an integer, a real or a point, and set_system_variable has to be given the matching one. An unknown name is refused rather than answered with an empty value; list_system_variables gives the ones this bank knows by name.", "lisp",
        Intent = new[] { "get a system variable", "read the value of osmode",
                         "odczytaj zmienna systemowa", "what is the current layer variable",
                         "jaka jest wartosc zmiennej systemowej", "getvar",
                         "check a sysvar setting" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<LispSysvarResult> GetSystemVariable(IPluginGateway gw, LispSysvarGetArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispSysvarGetArgs, LispSysvarResult>(gw, "acad.lisp.get_system_variable", args, T_NORMAL, ct);

    [McpTool("set_system_variable", "Set one AutoCAD system variable - setvar. The value is converted to the type the variable already holds, since passing a string to an integer variable is rejected outright. The new value is READ BACK afterwards and the tool refuses if it did not change: a read-only system variable accepts the call and quietly keeps its old value, which would otherwise be indistinguishable from success. Both the previous and the new value are reported, so a change can be undone by setting the old one back.", "lisp",
        Intent = new[] { "set a system variable", "setvar osmode",
                         "ustaw zmienna systemowa", "turn off object snap",
                         "zmien wartosc zmiennej systemowej", "change insunits",
                         "set the current layer variable" },
        RequiresPlugin = true)]
    public static Task<LispSysvarSetResult> SetSystemVariable(IPluginGateway gw, LispSysvarSetArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispSysvarSetArgs, LispSysvarSetResult>(gw, "acad.lisp.set_system_variable", args, T_NORMAL, ct);

    [McpTool("list_system_variables", "List the system variables worth knowing, grouped by what they affect - drafting aids, current properties, text and dimensions, units, display, 3D, file state and plotting - each with its live value and type. Read-only. `pattern` filters by name or by group. IMPORTANT: this is a CURATED list, not a complete one - AutoCAD exposes no way to enumerate system variables, there being no table to walk - so treat it as a starting point rather than a boundary: any variable not listed still works with get_system_variable and set_system_variable. Every value is read live rather than remembered, so a name this build of AutoCAD does not have is counted as not present instead of being reported with a stale value.", "lisp",
        Intent = new[] { "list system variables", "what system variables are set",
                         "lista zmiennych systemowych", "show the drafting settings",
                         "jakie sa ustawienia jednostek", "which sysvars control display",
                         "show current drawing settings" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<LispSysvarListResult> ListSystemVariables(IPluginGateway gw, LispSymbolsArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispSymbolsArgs, LispSysvarListResult>(gw, "acad.lisp.list_system_variables", args, T_NORMAL, ct);

    [McpTool("purge_regapps", "Remove unreferenced registered application names from the drawing. Registered app names are the keys extended data is filed under, and they accumulate in a drawing that has passed through several applications. Only the UNREFERENCED ones go: Database.Purge is asked first and strikes from the candidate list everything still in use, so a name that some xdata still points at is kept - erasing one that is referenced would corrupt that xdata. ACAD is AutoCAD's own and is never offered. Reports the names purged and the ones remaining, with the counts before and after.", "lisp",
        Intent = new[] { "purge unused regapps", "clean up registered application names",
                         "wyczysc nieuzywane regapp", "purge the drawing of stale app names",
                         "usun nieuzywane nazwy aplikacji", "remove leftover xdata app names",
                         "tidy registered applications" },
        RequiresPlugin = true)]
    public static Task<LispPurgeResult> PurgeRegapps(IPluginGateway gw, LispPurgeArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispPurgeArgs, LispPurgeResult>(gw, "acad.lisp.purge_regapps", args, T_NORMAL, ct);
}
