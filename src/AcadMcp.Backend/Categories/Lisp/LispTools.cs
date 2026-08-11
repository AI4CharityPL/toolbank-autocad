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
// THREE MORE FIXED, 2026-08-11 (second pass): eval_lisp, load_lisp_file and list_loaded_lisp now
// go through LispCommandBridge.EvalAsync - the SAME queued-SendStringToExecute mechanism as
// run_command_sequence, but WITHOUT a custom [CommandMethod]: the command line accepts a raw
// LISP form directly when typed (that is how a human evaluates LISP interactively), so the
// wrapper is queued as-is. The user's expression never gets embedded in the queued text - it
// goes into a request FILE the wrapper reads with (read (open ...)), which sidesteps every LISP
// string-escaping question. This is rule 26 §24. The old attempt used Application.Invoke (dead,
// eInvalidInput everywhere) falling back to a bare Editor.Command(wrapped) call, which throws
// from application context for the exact reason run_command_sequence originally did.
//
// netload_assembly REMAINS WITHDRAWN - it needs the identical command-context fix, but writing
// it was deliberately not attempted: dynamic assembly loading is a materially different risk from
// evaluating LISP or queuing drawing commands, and this tranche did not have the user's specific
// go-ahead for that capability. The plugin handler stays registered so the finding stays
// reproducible.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Lisp;

public static class LispTools
{
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 60_000;

    [McpTool("eval_lisp", "Evaluate an AutoLISP expression and return its REAL VALUE - not an acknowledgement that something ran. This is the escape hatch: anything AutoCAD exposes to AutoLISP is reachable here, including functions a .lsp file has already defined with load_lisp_file. Runs via a queued command-line evaluation that writes its result to a file and this waits for it (rule 26 §15/§24) - synchronous from the caller's point of view. nil comes back as null, T as true, and a list keeps its nesting; `printed` carries what LISP itself printed alongside the parsed value, for results with no JSON equivalent (an ename, a selection set). A syntax error or an undefined function name is refused with LISP's own error message rather than returned as a plausible-looking nil.", "lisp",
        Intent = new[] { "eval this lisp and give me the result", "evaluate a lisp expression",
                         "wykonaj kod lisp", "call a lisp function",
                         "oblicz wyrazenie lispa i podaj wynik", "run autolisp code",
                         "escape hatch for something with no tool" },
        RequiresPlugin = true)]
    public static Task<LispEvalResult> EvalLisp(IPluginGateway gw, LispEvalArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispEvalArgs, LispEvalResult>(gw, "acad.lisp.eval_lisp", args, T_NORMAL, ct);

    [McpTool("load_lisp_file", "Load an AutoLISP (.lsp) file, making its functions available for eval_lisp to call. Does not search the support path - give the full path. Returns the value of the LAST expression in the file, which for a file of defuns is usually the name of the last one defined; that is the closest thing to a confirmation the loader offers, so to be sure a particular function arrived, call it with eval_lisp - an undefined name is an error there, not a silent nil.", "lisp",
        Intent = new[] { "load a lisp file", "appload a lisp file",
                         "wczytaj plik lisp", "load an autolisp routine from disk",
                         "zaladuj plik lisp", "load this lsp",
                         "make these lisp functions available" },
        RequiresPlugin = true)]
    public static Task<LispLoadResult> LoadLispFile(IPluginGateway gw, LispLoadArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispLoadArgs, LispLoadResult>(gw, "acad.lisp.load_lisp_file", args, T_NORMAL, ct);

    [McpTool("list_loaded_lisp", "List the AutoLISP symbols currently defined - from (atoms-family 1), the only enumeration AutoCAD actually offers. Read-only. IMPORTANT: these are SYMBOLS, not files - AutoCAD keeps no record of which .lsp files were loaded, so this is the honest answer to 'what LISP is loaded', not a file list. Built-in functions are included alongside anything a .lsp defined, which is why a fresh drawing already answers with hundreds and `pattern` matters. Names starting C: are the ones that can be typed at the command line, listed separately as commandSymbols.", "lisp",
        Intent = new[] { "list loaded lisp functions", "check a lisp routine loaded",
                         "jakie funkcje lisp sa zaladowane", "is this lisp function available",
                         "lista symboli lisp", "show lisp commands defined by a loaded file",
                         "what lisp is defined" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<LispSymbolsResult> ListLoadedLisp(IPluginGateway gw, LispSymbolsArgs args, CancellationToken ct)
        => LispProxy.CallAsync<LispSymbolsArgs, LispSymbolsResult>(gw, "acad.lisp.list_loaded_lisp", args, T_NORMAL, ct);

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
