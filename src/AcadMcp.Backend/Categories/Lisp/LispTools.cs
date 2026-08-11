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
// ONE IS FIXED, 2026-08-11: run_command_sequence now goes through LispCommandBridge - a real
// [CommandMethod] queued via Document.SendStringToExecute (safe from application context, because
// it only queues) that writes its result to a file once it has run SYNCHRONOUSLY in a genuine
// COMMAND context. ExecuteInCommandContextAsync, the fix rule 26 §15 originally pointed at, was
// tried separately and HUNG AutoCAD; this bridge avoids it entirely. Two further limits were
// MEASURED live rather than assumed and are now refusals, not silent wrong answers: a second
// command chained after the first in one call is dropped without error, and a command that
// prompts for object SELECTION (ERASE, MOVE, ...) completes and reports success while changing
// nothing. See the tool's own description and rule 26 §22.
//
// run_script_file was ALSO built on the same bridge and WITHDRAWN, 2026-08-11: even a single
// CIRCLE inside a .scr ran through _.SCRIPT reports entitiesAdded=0 - nothing is drawn. Two
// measured attempts, both wrong: the obvious cause (SCRIPT opens a file-picker dialog unless
// FILEDIA=0) was tried and confirmed NOT to be it - FILEDIA is forced to 0 and correctly restored
// afterward, and the script still does nothing. The likely cause is the same "only one command's
// worth of input survives nesting" limit that dropped a second chained command in
// run_command_sequence, this time inside SCRIPT's own internal line-by-line replay rather than
// at the outer Editor.Command call - but that is a hypothesis, not a measurement, and shipping a
// script runner that silently runs nothing is exactly the failure this bank exists to refuse.
//
// FOUR REMAIN WITHDRAWN. netload_assembly needs the same command-context fix as
// run_command_sequence but is deliberately not attempted in this tranche - dynamic assembly
// loading is a materially different risk from queuing drawing commands. eval_lisp,
// load_lisp_file and list_loaded_lisp need actual LISP EVALUATION, which neither Editor.Command
// (tokenises command input; a parenthesised expression is invalid to it) nor Application.Invoke
// (unusable from this plugin in any context) provides - the remaining candidate is
// SendStringToExecute with the expression wrapped to write its value to a file, unbuilt. The
// plugin handlers for all five stay registered so each finding stays reproducible; they are
// simply not offered in the bank.

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

    [McpTool("run_command_sequence", "Run ONE AutoCAD command with its answers, one token each - for example [\"_.CIRCLE\", \"0,0\", \"10\"]. Underscore-dot prefixes keep it working on a non-English AutoCAD and immune to a redefined command name. Runs in a genuine COMMAND context (rule 26 §15) via a queued [CommandMethod], NOT the application context every other tool in this bank dispatches from - Editor.Command throws from there. TWO MEASURED LIMITS, both refused rather than silently wrong: a second command chained after the first in one call is dropped without error, so only one command per call is accepted; and commands that prompt for object SELECTION (ERASE, MOVE, COPY and similar - not an exhaustive list) complete and report success while changing nothing, so the common ones are refused by name. Commands that only draw new geometry (CIRCLE, LINE, RECTANG, PLINE, ...) work reliably. Editor.Command returns VOID, so the count of model-space entities before and after is the evidence this tool can honestly report. A sequence that runs out of answers leaves AutoCAD waiting for one it will never get - this tool then TIMES OUT rather than hanging forever, and says to check the AutoCAD window, because it cannot see what is on screen.", "lisp",
        Intent = new[] { "run a command sequence", "drive a command from the command line",
                         "uruchom sekwencje polecen", "run this autocad command with answers",
                         "wykonaj sekwencje polecen", "send a command sequence",
                         "type this command for me" },
        RequiresPlugin = true)]
    public static Task<LispCommandResult> RunCommandSequence(IPluginGateway gw, LispCommandArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispCommandArgs, LispCommandResult>(gw, "acad.lisp.run_command_sequence", args, T_SLOW, ct);

    // run_script_file is WITHDRAWN - see the header comment. Two measured fix attempts, both
    // wrong; not exposed here, matching eval_lisp/load_lisp_file/list_loaded_lisp/netload_assembly.
}
