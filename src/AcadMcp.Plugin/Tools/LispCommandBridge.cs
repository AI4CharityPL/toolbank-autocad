// Bridge between MCP's APPLICATION-context dispatch and AutoCAD's COMMAND context.
//
// MEASURED (rule 26 §15): Editor.Command throws eInvalidInput from an ordinary tool handler,
// because that handler runs in the APPLICATION context, never a COMMAND one. The only proven way
// into a command context is a genuine [CommandMethod] AutoCAD's own command processor invokes -
// verified by CommandContextProbe.cs, which is diagnostic-only and needs a human to type it.
//
// This bridges the two WITHOUT a human. The application-context side writes a REQUEST file,
// queues one of the CommandMethods below through Document.SendStringToExecute - safe to call from
// application context, because it only QUEUES text as if typed, the same as a human would - and
// polls for a RESPONSE file the CommandMethod writes once its real, synchronous drawing work is
// done. The response file's CONTENTS are the evidence, never the fact that something was queued:
// that is the exact silent-queueing trap rule 26 §15 forbids ("a tool that merely reports 'sent'
// is still forbidden"). Each CommandMethod writes a response EVEN ON FAILURE, specifically so a
// clean AutoCAD refusal reads back fast and precise rather than as an indistinguishable timeout.
//
// The GUID that ties a request to its response travels as a QUEUED ANSWER to Editor.GetString,
// not as a command-line argument - CommandMethods take none. Queuing "ACADMCP_RUNSEQ <guid> "
// starts the command, and its first act is GetString, which consumes "<guid>" exactly the way it
// would consume a human's typed answer.
//
// A previous attempt built ExecuteInCommandContextAsync and HUNG AutoCAD; this avoids that whole
// mechanism and goes through the ordinary command queue instead - the one route rule 26 §15's own
// isolating experiment proved works.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadRt = Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(AcadMcp.Plugin.Tools.LispCommandBridge))]

namespace AcadMcp.Plugin.Tools;

internal static class LispCommandBridge
{
    private static readonly string Dir = Path.Combine(Path.GetTempPath(), "acadmcp-cmdbridge");

    internal sealed record BridgeResult(bool Completed, JsonObject? Response, string? TimeoutNote);

    // ─────────── application-context side: queue the command, wait for the response file ───────────

    internal static async Task<BridgeResult> RunAsync(
        string commandName, JsonObject request, int timeoutMs, CancellationToken ct)
    {
        Directory.CreateDirectory(Dir);
        var id = Guid.NewGuid().ToString("N");
        var reqPath = Path.Combine(Dir, id + ".request.json");
        var respPath = Path.Combine(Dir, id + ".response.json");
        try
        {
            await File.WriteAllTextAsync(reqPath, request.ToJsonString(), ct).ConfigureAwait(false);

            var doc = AcadApp.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active AutoCAD document.");
            // SendStringToExecute only QUEUES - unlike Editor.Command it is safe to call from
            // application context. The trailing space on each token is a synthetic Enter.
            await UiThreadDispatcher.Run(
                () => doc.SendStringToExecute(commandName + " " + id + " ", true, false, false), ct)
                .ConfigureAwait(false);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (ct.IsCancellationRequested)
                    return new BridgeResult(false, null, "cancelled while waiting for a response.");
                if (File.Exists(respPath))
                {
                    var text = await File.ReadAllTextAsync(respPath, ct).ConfigureAwait(false);
                    var node = JsonNode.Parse(text) as JsonObject;
                    return new BridgeResult(true, node, null);
                }
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
            return new BridgeResult(false, null,
                $"'{commandName}' was queued via SendStringToExecute but produced no response " +
                $"within {timeoutMs} ms. This most often means a modal dialog is open, or the " +
                "sequence left a prompt waiting for an answer it never received - look at the " +
                "AutoCAD window; this tool cannot see what is on screen.");
        }
        finally
        {
            TryDelete(reqPath);
            TryDelete(respPath);
        }
    }

    // ─────────── LISP evaluation: no [CommandMethod] needed ───────────
    //
    // Unlike run_command_sequence, this does not need a custom command name or the
    // GetString-relay trick, because the thing being queued is a raw LISP form - and AutoCAD's
    // command line accepts LISP directly when typed (that is how a human evaluates LISP
    // interactively), the same fact rule 26 §15 names as "the remaining candidate" for this.
    // Editor.Command TOKENISES command input and rejects LISP; SendStringToExecute queues text
    // as if typed and does not have that restriction.
    //
    // The user's expression never gets embedded in the queued LISP text itself - it goes into a
    // REQUEST FILE that the wrapper reads, which sidesteps every LISP string-escaping question
    // entirely: no matter what characters the expression contains, it is never concatenated into
    // a string literal.
    //
    // MEASURED live: AutoLISP's (read) takes a STRING, not a file object - unlike Common Lisp,
    // it has no form that reads directly from an open file. Calling (read <file>) compiles into
    // nothing (LISP has no compile step) and fails at runtime with "zly typ argumentu: stringp",
    // a bad-argument-type error naming the predicate it failed rather than the function that
    // needed it. The fix is (read (read-line <file>)) - read one line as a string, then parse
    // that string - which is also why the request file is written as ONE line regardless of
    // whether the caller's source had embedded newlines: read-line stops at the first one.

    internal sealed record EvalResult(bool Completed, string? Status, string? Body, string? TimeoutNote);

    internal static async Task<EvalResult> EvalAsync(
        Document doc, string lispSource, int timeoutMs, CancellationToken ct)
    {
        Directory.CreateDirectory(Dir);
        var id = Guid.NewGuid().ToString("N");
        var reqPath = Path.Combine(Dir, id + ".lisp-req.txt");
        var respPath = Path.Combine(Dir, id + ".lisp-resp.txt");
        try
        {
            // Collapsed to one line - AutoLISP does not care about whitespace/newlines inside an
            // expression (parens define structure), and read-line only ever gets the first line.
            var oneLine = string.Join(" ", lispSource.Split(
                new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
            await File.WriteAllTextAsync(reqPath, oneLine, ct).ConfigureAwait(false);

            // LISP wants forward slashes even on Windows - a backslash starts an escape. Both
            // paths are generated by us (GUID-based), never user content, so no escaping risk.
            string reqLisp = reqPath.Replace("\\", "/");
            string respLisp = respPath.Replace("\\", "/");
            string wrapper =
                "(progn (vl-load-com) " +
                "(setq MCPEVF (open \"" + reqLisp + "\" \"r\")) " +
                "(setq MCPEVLN (vl-catch-all-apply 'read-line (list MCPEVF))) " +
                "(close MCPEVF) " +
                "(setq MCPEVO (open \"" + respLisp + "\" \"w\")) " +
                "(if (or (vl-catch-all-error-p MCPEVLN) (null MCPEVLN)) " +
                "(progn (write-line \"ERROR\" MCPEVO) (write-line \"request file was empty or unreadable\" MCPEVO)) " +
                "(progn (setq MCPEVRD (vl-catch-all-apply 'read (list MCPEVLN))) " +
                "(if (vl-catch-all-error-p MCPEVRD) " +
                "(progn (write-line \"ERROR\" MCPEVO) (write-line (vl-catch-all-error-message MCPEVRD) MCPEVO)) " +
                "(progn (setq MCPEVR (vl-catch-all-apply 'eval (list MCPEVRD))) " +
                "(if (vl-catch-all-error-p MCPEVR) " +
                "(progn (write-line \"ERROR\" MCPEVO) (write-line (vl-catch-all-error-message MCPEVR) MCPEVO)) " +
                "(progn (write-line \"OK\" MCPEVO) (write-line (vl-prin1-to-string MCPEVR) MCPEVO))))))) " +
                "(close MCPEVO) (princ))";

            await UiThreadDispatcher.Run(
                () => doc.SendStringToExecute(wrapper + " \n", true, false, false), ct)
                .ConfigureAwait(false);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (ct.IsCancellationRequested)
                    return new EvalResult(false, null, null, "cancelled while waiting for a response.");
                if (File.Exists(respPath))
                {
                    var lines = await File.ReadAllLinesAsync(respPath, ct).ConfigureAwait(false);
                    var status = lines.Length > 0 ? lines[0] : "ERROR";
                    var body = string.Join("\n", lines.Skip(1));
                    return new EvalResult(true, status, body, null);
                }
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
            return new EvalResult(false, null, null,
                $"LISP evaluation was queued via SendStringToExecute but produced no response " +
                $"within {timeoutMs} ms. This most often means a syntax error left something " +
                "waiting for more input, or a modal dialog is open - look at the AutoCAD window; " +
                "this tool cannot see what is on screen.");
        }
        finally
        {
            TryDelete(reqPath);
            TryDelete(respPath);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (System.Exception) { /* best effort */ }
    }

    private static JsonObject? ReadRequest(string id)
    {
        var path = Path.Combine(Dir, id + ".request.json");
        if (!File.Exists(path)) return null;
        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
    }

    private static void WriteResponse(string id, JsonObject body)
    {
        var path = Path.Combine(Dir, id + ".response.json");
        File.WriteAllText(path, body.ToJsonString());
    }

    private static string? ReadGuidAnswer(Editor ed, string prompt)
    {
        var opts = new PromptStringOptions("\n" + prompt) { AllowSpaces = false };
        var res = ed.GetString(opts);
        return res.Status == PromptStatus.OK ? res.StringResult : null;
    }

    private static int CountModelSpace(Database db)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        int n = 0;
        foreach (ObjectId id in ms) { if (!id.IsErased) n++; }
        tr.Commit();
        return n;
    }

    private static string DescribeError(System.Exception ex) =>
        ex is AcadRt.Exception ae ? $"[{ae.ErrorStatus}] {ae.Message}" : $"[{ex.GetType().Name}] {ex.Message}";

    // ─────────── command-context side: the real CommandMethods ───────────

    [CommandMethod("ACADMCP_RUNSEQ")]
    public static void RunSeq()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        var id = ReadGuidAnswer(doc.Editor, "Request id: ");
        if (id is null) return;
        var resp = new JsonObject();
        try
        {
            var req = ReadRequest(id) ?? throw new InvalidOperationException("request file missing");
            var tokens = req["tokens"]!.AsArray().Select(n => (object)n!.GetValue<string>()).ToArray();
            var db = doc.Database;
            int before = CountModelSpace(db);
            // SYNCHRONOUS and genuinely in COMMAND context now - this is the one call rule 26
            // §15 proved fails everywhere else and works only here.
            doc.Editor.Command(tokens);
            int after = CountModelSpace(db);
            resp["ok"] = true;
            resp["entitiesBefore"] = before;
            resp["entitiesAfter"] = after;
            resp["entitiesAdded"] = after - before;
        }
        catch (System.Exception ex)
        {
            resp["ok"] = false;
            resp["error"] = DescribeError(ex);
        }
        WriteResponse(id, resp);
    }

    [CommandMethod("ACADMCP_RUNSCRIPT")]
    public static void RunScript()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        var id = ReadGuidAnswer(doc.Editor, "Request id: ");
        if (id is null) return;
        var resp = new JsonObject();
        try
        {
            var req = ReadRequest(id) ?? throw new InvalidOperationException("request file missing");
            var path = req["path"]!.GetValue<string>();
            var db = doc.Database;
            int before = CountModelSpace(db);
            // MEASURED live: SCRIPT opens the "Select Script File" dialog even when given a path
            // in the Command() array, unless FILEDIA is 0 - same precedent as NETLOAD. Restored
            // afterward regardless of outcome.
            object? prevFiledia = null;
            try { prevFiledia = AcadApp.GetSystemVariable("FILEDIA"); } catch (System.Exception) { }
            try
            {
                AcadApp.SetSystemVariable("FILEDIA", (short)0);
                doc.Editor.Command("_.SCRIPT", path);
            }
            finally
            {
                if (prevFiledia is not null)
                {
                    try { AcadApp.SetSystemVariable("FILEDIA", prevFiledia); } catch (System.Exception) { }
                }
            }
            int after = CountModelSpace(db);
            resp["ok"] = true;
            resp["entitiesBefore"] = before;
            resp["entitiesAfter"] = after;
            resp["entitiesAdded"] = after - before;
        }
        catch (System.Exception ex)
        {
            resp["ok"] = false;
            resp["error"] = DescribeError(ex);
        }
        WriteResponse(id, resp);
    }

    [CommandMethod("ACADMCP_NETLOAD")]
    public static void NetloadCmd()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        var id = ReadGuidAnswer(doc.Editor, "Request id: ");
        if (id is null) return;
        var resp = new JsonObject();
        try
        {
            var req = ReadRequest(id) ?? throw new InvalidOperationException("request file missing");
            var path = req["path"]!.GetValue<string>();

            // MEASURED (same precedent as SCRIPT): NETLOAD opens a file-picker dialog unless
            // FILEDIA is 0, in which case the path on the command line is taken directly.
            object? prevFiledia = null;
            try { prevFiledia = AcadApp.GetSystemVariable("FILEDIA"); } catch (System.Exception) { }
            try
            {
                AcadApp.SetSystemVariable("FILEDIA", (short)0);
                doc.Editor.Command("_.NETLOAD", path);
            }
            finally
            {
                if (prevFiledia is not null)
                {
                    try { AcadApp.SetSystemVariable("FILEDIA", prevFiledia); } catch (System.Exception) { }
                }
            }

            // DynamicLinker.GetLoadedModules/IsModuleLoaded do NOT report netloaded .NET
            // assemblies (rule 26, list_loaded_applications' own measured limit) - read back
            // through a DIFFERENT route instead: NETLOAD loads into the CURRENT process, so
            // AppDomain.CurrentDomain.GetAssemblies() sees it directly.
            var wantName = System.IO.Path.GetFileNameWithoutExtension(path);
            bool loaded = System.AppDomain.CurrentDomain.GetAssemblies().Any(a =>
            {
                try
                {
                    return string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(a.Location), wantName,
                        System.StringComparison.OrdinalIgnoreCase);
                }
                catch (System.Exception) { return false; }
            });

            resp["ok"] = true;
            resp["loaded"] = loaded;
        }
        catch (System.Exception ex)
        {
            resp["ok"] = false;
            resp["error"] = DescribeError(ex);
        }
        WriteResponse(id, resp);
    }
}
