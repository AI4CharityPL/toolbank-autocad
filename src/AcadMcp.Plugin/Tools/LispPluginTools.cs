// AutoCAD plugin handlers for the acad-lisp category.
// Registered under "acad.lisp.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// THE DESIGN CONSTRAINT FOR THIS WHOLE CATEGORY: a result you cannot observe is not a result.
// Document.SendStringToExecute QUEUES - it returns before the command has run, so a tool built on
// it can only ever report "sent", which is the silent-queueing trap already hit by modify.undo.
// Nothing here uses it. The routes used instead all return synchronously:
//
//   Application.Invoke(ResultBuffer)  evaluates a LISP form and hands back a ResultBuffer
//   Editor.Command(...)               runs a command sequence and returns when it is finished
//   Application.Get/SetSystemVariable reaches system variables without going through LISP at all
//
// Editor.Command returns VOID, so it cannot be asked whether it worked. Every tool built on it
// therefore measures a SIDE EFFECT instead - the entity count, or a system variable - and says so.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class LispPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.lisp.eval_lisp",                EvalLisp);
        host.Register("acad.lisp.load_lisp_file",           LoadLispFile);
        host.Register("acad.lisp.list_loaded_lisp",         ListLoadedLisp);
        host.Register("acad.lisp.run_script_file",          RunScriptFile);
        host.Register("acad.lisp.run_command_sequence",     RunCommandSequence);
        host.Register("acad.lisp.netload_assembly",         NetloadAssembly);
        host.Register("acad.lisp.list_loaded_applications", ListLoadedApplications);
        host.Register("acad.lisp.get_system_variable",      GetSystemVariable);
        host.Register("acad.lisp.set_system_variable",      SetSystemVariable);
        host.Register("acad.lisp.list_system_variables",    ListSystemVariables);
        host.Register("acad.lisp.purge_regapps",            PurgeRegapps);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── turning a ResultBuffer into something JSON can carry ───────────

    /// LISP hands values back as a flat run of TypedValues with ListBegin/ListEnd markers around
    /// nested lists. This rebuilds the nesting, because handing an agent a flattened list would
    /// silently lose the structure that tells `(1 (2 3))` from `(1 2 3)`.
    private static JsonNode? FromResultBuffer(ResultBuffer? rb, out string typeSummary)
    {
        typeSummary = "nil";
        if (rb is null) return null;

        var values = rb.AsArray();
        if (values.Length == 0) return null;

        int i = 0;
        var kinds = new List<string>();
        JsonNode? node = ReadOne(values, ref i, kinds);
        typeSummary = kinds.Count == 0 ? "nil" : string.Join(",", kinds.Distinct());
        return node;
    }

    private static JsonNode? ReadOne(TypedValue[] v, ref int i, List<string> kinds)
    {
        if (i >= v.Length) return null;
        var tv = v[i];
        var t = (LispDataType)tv.TypeCode;

        if (t == LispDataType.ListBegin)
        {
            i++;
            var arr = new JsonArray();
            while (i < v.Length && (LispDataType)v[i].TypeCode != LispDataType.ListEnd)
                arr.Add(ReadOne(v, ref i, kinds));
            if (i < v.Length) i++;                 // step over the ListEnd
            kinds.Add("list");
            return arr;
        }

        i++;
        switch (t)
        {
            case LispDataType.Text: kinds.Add("text"); return JsonValue.Create((string?)tv.Value);
            case LispDataType.Double: kinds.Add("double"); return JsonValue.Create(Convert.ToDouble(tv.Value));
            case LispDataType.Int16: kinds.Add("int"); return JsonValue.Create(Convert.ToInt32(tv.Value));
            case LispDataType.Int32: kinds.Add("int"); return JsonValue.Create(Convert.ToInt32(tv.Value));
            case LispDataType.Point3d:
            case LispDataType.Point2d:
                kinds.Add("point");
                var p = tv.Value;
                return JsonSerializer.SerializeToNode(p?.ToString());
            case LispDataType.ObjectId:
                kinds.Add("objectId");
                return JsonValue.Create(tv.Value is ObjectId oid && !oid.IsNull
                    ? oid.Handle.ToString() : null);
            case LispDataType.SelectionSet: kinds.Add("selectionSet"); return JsonValue.Create("<selection set>");
            case LispDataType.T_atom: kinds.Add("T"); return JsonValue.Create(true);
            case LispDataType.Nil: kinds.Add("nil"); return null;
            case LispDataType.None: kinds.Add("nil"); return null;
            default: kinds.Add(t.ToString()); return JsonValue.Create(tv.Value?.ToString());
        }
    }

    // ─────────── evaluating LISP, and how ───────────

    /// MEASURED: Application.Invoke answers eInvalidInput for EVERY expression - (+ 1 2),
    /// (read "..."), (load "...") and (atoms-family 1) alike - when called from inside the
    /// document lock and open transaction every tool in this plugin runs in. It is kept as the
    /// first route because it is the clean one and costs nothing to try, but it is not relied on.
    ///
    /// The route that works is the command line, which evaluates a parenthesised expression the
    /// same way a user typing it does, through Editor.Command - synchronous, so it has finished
    /// when the call returns. Its problem is that the VALUE goes to the screen rather than to the
    /// caller, so the expression is wrapped to write the value to a file, which is then read back
    /// here. A file rather than a USERS1 system variable because the latter caps at 255 characters
    /// and (atoms-family 1) is far longer than that.
    ///
    /// The wrapper is also the guard: the file is deleted first, so a file that does not appear
    /// means the expression did not evaluate. Without that, an expression that silently failed
    /// would be indistinguishable from one that returned nil.
    private static (string Text, string Route, string Diagnostics) EvalToText(Document doc, string src)
    {
        var notes = new List<string>();

        try
        {
            var rb = AcadApp.Invoke(new ResultBuffer(
                new TypedValue((int)LispDataType.Text, "read"),
                new TypedValue((int)LispDataType.Text, src)));
            if (rb is not null)
            {
                var call = new List<TypedValue> { new((int)LispDataType.Text, "eval") };
                call.AddRange(rb.AsArray());
                var res = AcadApp.Invoke(new ResultBuffer(call.ToArray()));
                if (res is not null)
                {
                    var flat = string.Concat(res.AsArray().Select(t => t.Value?.ToString()));
                    return (flat, "invoke", "");
                }
            }
            notes.Add("invoke: returned nothing");
        }
        catch (System.Exception ex) { notes.Add("invoke: " + ex.Message); }

        var outPath = Path.Combine(Path.GetTempPath(),
            "acadmcp-lisp-" + Guid.NewGuid().ToString("N") + ".txt");
        try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        var lispPath = outPath.Replace("\\", "/");

        // vl-load-com is harmless if already loaded and is what makes vl-princ-to-string available.
        var wrapped =
            "(progn (vl-load-com)" +
            "(setq %tbf (open \"" + lispPath + "\" \"w\"))" +
            "(princ (vl-princ-to-string (progn " + src + ")) %tbf)" +
            "(close %tbf)(setq %tbf nil)(princ))";

        try
        {
            doc.Editor.Command(wrapped);
        }
        catch (System.Exception ex)
        {
            notes.Add("command: " + ex.Message);
            throw new ArgumentException(
                "LISP evaluation failed. " + string.Join(" | ", notes) +
                " -- check the expression is balanced and that every function it names exists; " +
                "a .lsp defining one has to be loaded first with load_lisp_file.");
        }

        if (!File.Exists(outPath))
            throw new ArgumentException(
                "The expression did not evaluate: AutoCAD accepted the line but nothing was " +
                "written, which is what a LISP error looks like from here - the error goes to the " +
                "command line rather than being raised. Check the expression is balanced and that " +
                "every function it names exists. " + string.Join(" | ", notes));

        string text;
        try { text = File.ReadAllText(outPath); }
        finally { try { File.Delete(outPath); } catch { } }

        return (text, "command", string.Join(" | ", notes));
    }

    /// Parses what LISP printed back into real JSON, so a list keeps its nesting. Printing and
    /// re-reading is a round trip through text, and a flattened list would otherwise be
    /// indistinguishable from a flat one.
    private static JsonNode? ParseLispText(string s, out string kinds)
    {
        int i = 0;
        var k = new List<string>();
        var node = ParseNode(s, ref i, k);
        kinds = k.Count == 0 ? "nil" : string.Join(",", k.Distinct());
        return node;
    }

    private static JsonNode? ParseNode(string s, ref int i, List<string> kinds)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length) return null;

        if (s[i] == '(')
        {
            i++;
            var arr = new JsonArray();
            while (true)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i >= s.Length || s[i] == ')') { if (i < s.Length) i++; break; }
                arr.Add(ParseNode(s, ref i, kinds));
            }
            kinds.Add("list");
            return arr;
        }

        if (s[i] == '"')
        {
            i++;
            var sb = new System.Text.StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) i++;
                sb.Append(s[i++]);
            }
            if (i < s.Length) i++;
            kinds.Add("text");
            return JsonValue.Create(sb.ToString());
        }

        var start = i;
        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '(' && s[i] != ')') i++;
        var atom = s.Substring(start, i - start);

        if (string.Equals(atom, "nil", StringComparison.OrdinalIgnoreCase)) { kinds.Add("nil"); return null; }
        if (string.Equals(atom, "T", StringComparison.OrdinalIgnoreCase)) { kinds.Add("T"); return JsonValue.Create(true); }
        if (long.TryParse(atom, System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture, out var l))
        { kinds.Add("int"); return JsonValue.Create(l); }
        if (double.TryParse(atom, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
        { kinds.Add("double"); return JsonValue.Create(d); }
        kinds.Add("symbol");
        return JsonValue.Create(atom);
    }

    // ─────────── eval_lisp ───────────

    private static Task<ToolDispatchResult> EvalLisp(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.eval_lisp", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispEvalArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Source))
                throw new ArgumentException(
                    "source is required: the LISP expression to evaluate, for example " +
                    "(+ 1 2) or (getvar \"CLAYER\").");
            var src = a.Source!.Trim();

            var (text, route, diag) = EvalToText(doc, src);
            var value = ParseLispText(text, out var kinds);

            return Wrap(new
            {
                source = src,
                value,
                valueTypes = kinds,
                printed = text.Trim(),
                route,
                routeNotes = string.IsNullOrEmpty(diag) ? null : diag,
                note = "Evaluated SYNCHRONOUSLY - the value above is the expression's real result " +
                       "and not an acknowledgement that something was queued. nil comes back as " +
                       "null and T as true, and a nested list keeps its nesting. `printed` is what " +
                       "LISP itself printed, kept alongside the parsed value so nothing is lost " +
                       "when a result has no JSON equivalent - an ename or a selection set prints " +
                       "as a symbol. This is the escape hatch: anything AutoCAD exposes to " +
                       "AutoLISP is reachable here, including functions a .lsp file has defined.",
            });
        });

    private static Task<ToolDispatchResult> LoadLispFile(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.load_lisp_file", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispLoadArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the .lsp file to load.");
            var path = Path.GetFullPath(a.Path!);
            if (!File.Exists(path))
                throw new ArgumentException(
                    "No file at " + path + ". load_lisp_file does not search the support path - " +
                    "give the full path.");

            // LISP wants forward slashes even on Windows: a backslash starts an escape.
            var lispPath = path.Replace("\\", "/");
            var (text, route, _) = EvalToText(doc, "(load \"" + lispPath + "\")");
            var value = ParseLispText(text, out var kinds);
            return Wrap(new
            {
                path,
                value,
                valueTypes = kinds,
                route,
                note = "(load) returns the value of the LAST expression in the file, which for a " +
                       "file of defuns is the name of the last function defined - that is the " +
                       "closest thing to a confirmation the LISP loader offers. To be sure a " +
                       "particular function arrived, call it with eval_lisp; an undefined one is " +
                       "an error rather than a silent nil.",
            });
        });

    private static Task<ToolDispatchResult> ListLoadedLisp(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.list_loaded_lisp", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispSymbolsArgsDto>(args);

            // There is no API that lists loaded .lsp FILES - AutoCAD does not keep that list. What
            // it does keep is the symbol table, and (atoms-family 1) returns every defined symbol
            // by name. That is the honest answer to "what LISP is loaded": the functions, not the
            // files they came from, and the tool says so rather than implying a file list.
            var (text, _, _) = EvalToText(doc, "(atoms-family 1)");
            var parsed = ParseLispText(text, out _);

            var names = new List<string>();
            if (parsed is JsonArray arr)
                foreach (var n in arr)
                    if (n is not null) names.Add(n.ToString());

            var pattern = a.Pattern?.Trim();
            var shown = string.IsNullOrEmpty(pattern)
                ? names
                : names.Where(n => n.IndexOf(pattern!, StringComparison.OrdinalIgnoreCase) >= 0)
                       .ToList();
            shown.Sort(StringComparer.OrdinalIgnoreCase);

            // C: prefixed symbols are the ones typed at the command line as commands.
            var commands = shown.Where(n => n.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                                .ToList();

            return Wrap(new
            {
                total = names.Count,
                count = shown.Count,
                symbols = shown.Take(a.Limit is > 0 ? a.Limit!.Value : 500).ToList(),
                commandSymbols = commands,
                truncated = shown.Count > (a.Limit is > 0 ? a.Limit!.Value : 500),
                note = "These are SYMBOLS, not files. AutoCAD keeps no list of which .lsp files " +
                       "were loaded, so the honest answer to 'what LISP is loaded' is what is " +
                       "defined - from (atoms-family 1). Built-in functions are in here as well " +
                       "as anything a .lsp defined, which is why `pattern` matters: a fresh " +
                       "drawing already answers with hundreds. Names starting C: are the ones " +
                       "that can be typed at the command line, listed separately above.",
            });
        });

    // ─────────── commands and scripts ───────────

    private static Task<ToolDispatchResult> RunCommandSequence(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.run_command_sequence", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispCommandArgsDto>(args);
            if (a.Tokens is null || a.Tokens.Count == 0)
                throw new ArgumentException(
                    "tokens is required: the command and its answers, one element each - for " +
                    "example [\"_.CIRCLE\", \"0,0\", \"10\"]. Underscore-dot prefixes keep it " +
                    "working on a non-English AutoCAD and immune to a redefined command.");

            int before = CountModelSpace(db, tr);
            var ed = doc.Editor;
            var boxed = a.Tokens.Cast<object>().ToArray();
            try
            {
                // SYNCHRONOUS: this returns when the command has finished, unlike
                // SendStringToExecute which returns immediately and leaves nothing to observe.
                ed.Command(boxed);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "The command sequence failed at AutoCAD with " + ex.ErrorStatus +
                    ". A sequence that runs out of answers leaves the command waiting, so check " +
                    "the token list is complete; a modal dialog cannot be answered this way at all.");
            }
            int after = CountModelSpace(db, tr);

            return Wrap(new
            {
                tokens = a.Tokens,
                entitiesBefore = before,
                entitiesAfter = after,
                entitiesAdded = after - before,
                note = "Editor.Command returns VOID - AutoCAD does not say whether a command " +
                       "sequence achieved anything - so the count of model-space entities before " +
                       "and after is measured here instead, and that is the only evidence this " +
                       "tool can honestly offer. A sequence that changes existing objects rather " +
                       "than adding any will correctly report 0 added; check such edits by " +
                       "reading the objects back. This runs synchronously, so by the time you " +
                       "read this the command has finished.",
            });
        });

    private static Task<ToolDispatchResult> RunScriptFile(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.run_script_file", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispLoadArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the .scr script file to run.");
            var path = Path.GetFullPath(a.Path!);
            if (!File.Exists(path))
                throw new ArgumentException("No file at " + path + ".");

            int before = CountModelSpace(db, tr);
            try
            {
                doc.Editor.Command("_.SCRIPT", path);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to run the script with " + ex.ErrorStatus +
                    ". A .scr is a list of command-line input, so a line that leaves a command " +
                    "waiting for an answer stalls the rest of the file.");
            }
            int after = CountModelSpace(db, tr);

            return Wrap(new
            {
                path,
                entitiesBefore = before,
                entitiesAfter = after,
                entitiesAdded = after - before,
                note = "Run through _.SCRIPT with Editor.Command, which returns when the script " +
                       "is finished - NOT through SendStringToExecute, which would queue it and " +
                       "leave nothing to report. As with run_command_sequence the entity count is " +
                       "the evidence, because AutoCAD returns no status of its own.",
            });
        });

    private static int CountModelSpace(Database db, Transaction tr)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        int n = 0;
        foreach (ObjectId id in ms) { if (!id.IsErased) n++; }
        return n;
    }

    // ─────────── .NET assemblies ───────────

    private static Task<ToolDispatchResult> NetloadAssembly(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.netload_assembly", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispLoadArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the .dll to load.");
            var path = Path.GetFullPath(a.Path!);
            if (!File.Exists(path))
                throw new ArgumentException("No file at " + path + ".");

            var already = SystemObjects.DynamicLinker.IsModuleLoaded(path);
            if (already)
                throw new ArgumentException(
                    "That assembly is already loaded. .NET assemblies CANNOT be unloaded from " +
                    "AutoCAD once in - the only way to load a rebuilt one is to restart AutoCAD, " +
                    "which is why this refuses rather than pretending to reload.");

            try
            {
                SystemObjects.DynamicLinker.LoadModule(path, false, false);
            }
            catch (System.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to load " + path + ": " + ex.Message +
                    ". The commonest causes are a .NET version the running AutoCAD cannot host, " +
                    "and a missing dependency next to the DLL.");
            }

            var loaded = SystemObjects.DynamicLinker.IsModuleLoaded(path);
            if (!loaded)
                throw new InvalidOperationException(
                    "LoadModule raised no error but the assembly does not read back as loaded.");

            return Wrap(new
            {
                path,
                loaded,
                note = "Confirmed by asking DynamicLinker.IsModuleLoaded afterwards rather than " +
                       "by the call not throwing. Note that a .NET assembly cannot be UNLOADED: " +
                       "to pick up a rebuilt DLL, AutoCAD has to be restarted.",
            });
        });

    private static Task<ToolDispatchResult> ListLoadedApplications(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.list_loaded_applications", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispSymbolsArgsDto>(args);
            var mods = SystemObjects.DynamicLinker.GetLoadedModules();
            var all = new List<string>();
            foreach (var m in mods) if (m is not null) all.Add(m);

            var pattern = a.Pattern?.Trim();
            var shown = string.IsNullOrEmpty(pattern)
                ? all
                : all.Where(n => n.IndexOf(pattern!, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            shown.Sort(StringComparer.OrdinalIgnoreCase);

            return Wrap(new
            {
                total = all.Count,
                count = shown.Count,
                modules = shown,
                note = "Every module AutoCAD has loaded, its own ARX and .NET assemblies included, " +
                       "so the list is long and `pattern` is the useful way in. This is the list " +
                       "netload_assembly checks against, and it is how to tell whether a plugin " +
                       "is actually in rather than merely installed.",
            });
        });

    // ─────────── system variables ───────────

    private static Task<ToolDispatchResult> GetSystemVariable(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.get_system_variable", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispSysvarArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required, for example CLAYER or OSMODE.");
            var name = a.Name!.Trim().ToUpperInvariant();

            object? v;
            try { v = AcadApp.GetSystemVariable(name); }
            catch (System.Exception)
            {
                throw new ArgumentException(
                    "There is no system variable called " + name + ". Names are not guessable - " +
                    "list_system_variables gives the ones this tool knows by name.");
            }

            return Wrap(new
            {
                name,
                value = JsonSerializer.SerializeToNode(v?.ToString()),
                clrType = v?.GetType().Name,
                note = "Read straight from Application.GetSystemVariable, not through LISP. The " +
                       "value comes back as text with its underlying type named alongside, " +
                       "because a system variable can be a string, an integer, a real or a point " +
                       "and set_system_variable has to be given the right one.",
            });
        });

    private static Task<ToolDispatchResult> SetSystemVariable(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.set_system_variable", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispSysvarArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required.");
            if (a.Value is null)
                throw new ArgumentException("value is required.");
            var name = a.Name!.Trim().ToUpperInvariant();

            object? before;
            try { before = AcadApp.GetSystemVariable(name); }
            catch (System.Exception)
            {
                throw new ArgumentException(
                    "There is no system variable called " + name + ".");
            }

            // The new value has to be handed over in the SAME CLR type the variable already
            // holds - passing a string to an integer variable is eInvalidInput, so the existing
            // value is what says which type to convert to.
            object typed;
            try
            {
                typed = before switch
                {
                    short => (short)Convert.ToInt32(a.Value.ToString()),
                    int => Convert.ToInt32(a.Value.ToString()),
                    double => Convert.ToDouble(a.Value.ToString(),
                                               System.Globalization.CultureInfo.InvariantCulture),
                    string => a.Value.ToString() ?? "",
                    _ => a.Value.ToString() ?? "",
                };
            }
            catch (System.Exception)
            {
                throw new ArgumentException(
                    name + " holds a " + (before?.GetType().Name ?? "null") + ", and '" + a.Value +
                    "' cannot be read as one.");
            }

            try { AcadApp.SetSystemVariable(name, typed); }
            catch (System.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to set " + name + " to '" + a.Value + "': " + ex.Message +
                    ". Some system variables are READ-ONLY and some only accept particular values.");
            }

            var after = AcadApp.GetSystemVariable(name);
            // Read back, always. A read-only variable can accept the call and keep its old value.
            if (string.Equals(before?.ToString(), after?.ToString(), StringComparison.Ordinal)
                && !string.Equals(before?.ToString(), typed.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    name + " still reads back as '" + after + "' after being set to '" + a.Value +
                    "', so the assignment did not take - it is most likely read-only.");

            return Wrap(new
            {
                name,
                valueBefore = before?.ToString(),
                value = after?.ToString(),
                clrType = after?.GetType().Name,
                note = "Read back after being written, and refused if it did not change - a " +
                       "read-only system variable accepts the call and quietly keeps its old " +
                       "value, which would otherwise look exactly like success. The value is " +
                       "converted to the type the variable already holds, since passing a string " +
                       "to an integer variable is rejected.",
            });
        });

    /// The system variables worth naming, grouped. AutoCAD exposes NO way to enumerate them - there
    /// is no table to walk - so this is a curated list, and the tool says so rather than implying
    /// it is complete. Every one is read live, so a name that has gone away shows up as missing.
    private static readonly (string Group, string[] Names)[] KnownSysvars =
    {
        ("drafting", new[] { "OSMODE", "ORTHOMODE", "POLARANG", "SNAPMODE", "GRIDMODE", "AUTOSNAP" }),
        ("current", new[] { "CLAYER", "CECOLOR", "CELTYPE", "CELWEIGHT", "CELTSCALE", "CANNOSCALE" }),
        ("text and dims", new[] { "TEXTSTYLE", "TEXTSIZE", "DIMSTYLE", "DIMSCALE", "MIRRTEXT" }),
        ("units", new[] { "INSUNITS", "LUNITS", "LUPREC", "AUNITS", "AUPREC", "MEASUREMENT" }),
        ("display", new[] { "FILLMODE", "LTSCALE", "PSLTSCALE", "HIGHLIGHT", "BLIPMODE", "VISRETAIN" }),
        ("3d", new[] { "ISOLINES", "FACETRES", "DISPSILH", "SURFTAB1", "SURFTAB2", "ELEVATION", "THICKNESS" }),
        ("file and state", new[] { "DWGNAME", "DWGPREFIX", "DWGTITLED", "SAVETIME", "FILEDIA", "CMDDIA" }),
        ("plot", new[] { "PLOTROTMODE", "BACKGROUNDPLOT", "PLQUIET" }),
    };

    private static Task<ToolDispatchResult> ListSystemVariables(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.list_system_variables", args, ct, (doc, db, tr) =>
        {
            var a = Read<LispSymbolsArgsDto>(args);
            var pattern = a.Pattern?.Trim();

            var found = new List<object>();
            int missing = 0;
            foreach (var (group, names) in KnownSysvars)
            {
                foreach (var n in names)
                {
                    if (!string.IsNullOrEmpty(pattern)
                        && n.IndexOf(pattern!, StringComparison.OrdinalIgnoreCase) < 0
                        && group.IndexOf(pattern!, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    object? v;
                    try { v = AcadApp.GetSystemVariable(n); }
                    catch (System.Exception) { missing++; continue; }
                    found.Add(new { name = n, group, value = v?.ToString(), clrType = v?.GetType().Name });
                }
            }

            return Wrap(new
            {
                count = found.Count,
                variables = found,
                notPresent = missing,
                note = "A CURATED list, not a complete one: AutoCAD exposes no way to enumerate " +
                       "system variables - there is no table to walk - so these are the ones worth " +
                       "naming, grouped by what they affect. Every value here was read live rather " +
                       "than remembered, so a name this build of AutoCAD does not have is counted " +
                       "in notPresent instead of being reported with a stale value. Any variable " +
                       "not listed still works with get_system_variable and set_system_variable; " +
                       "this is a starting point, not the boundary.",
            });
        });

    // ─────────── regapps ───────────

    private static Task<ToolDispatchResult> PurgeRegapps(JsonObject args, CancellationToken ct) =>
        Run("acad.lisp.purge_regapps", args, ct, (doc, db, tr) =>
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);

            var before = new List<string>();
            foreach (ObjectId id in rat)
            {
                if (id.IsErased) continue;
                before.Add(((RegAppTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
            }

            var candidates = new ObjectIdCollection();
            foreach (ObjectId id in rat)
            {
                if (id.IsErased) continue;
                var rec = (RegAppTableRecord)tr.GetObject(id, OpenMode.ForRead);
                // ACAD is AutoCAD's own and is never purgeable; leaving it out keeps the
                // "purgeable" count honest rather than counting a guaranteed failure.
                if (string.Equals(rec.Name, "ACAD", StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add(id);
            }

            // Purge REMOVES from the collection everything that cannot go, so what is left is the
            // set that really is unreferenced. This is the whole reason not to just erase them:
            // erasing a regapp that xdata still points at corrupts that xdata.
            db.Purge(candidates);

            var purged = new List<string>();
            foreach (ObjectId id in candidates)
            {
                var rec = (RegAppTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                purged.Add(rec.Name);
                rec.Erase();
            }

            var after = new List<string>();
            var rat2 = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            foreach (ObjectId id in rat2)
            {
                if (id.IsErased) continue;
                after.Add(((RegAppTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
            }

            return Wrap(new
            {
                registeredBefore = before.Count,
                registeredAfter = after.Count,
                purgedCount = purged.Count,
                purged,
                remaining = after,
                note = "Registered application names are the keys xdata is filed under, and they " +
                       "accumulate in a drawing that has been through several applications. Only " +
                       "the UNREFERENCED ones go: Database.Purge is asked first and removes from " +
                       "the candidate list everything that is still in use, so a name some xdata " +
                       "still points at is kept. Erasing one that is referenced would corrupt " +
                       "that xdata. ACAD is AutoCAD's own and is never offered.",
            });
        });
}
