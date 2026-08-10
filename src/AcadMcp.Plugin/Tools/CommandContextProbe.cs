// Diagnostic command, not a bank tool. Kept because the finding it produces is one nobody should
// have to rediscover - see rule 26 §15.
//
// THE QUESTION: Editor.Command, Application.Invoke and DynamicLinker.LoadModule all answer a bare
// eInvalidInput when called from a pipe-served tool handler. Three LISP formulations were tried,
// then a whole command-context runner around ExecuteInCommandContextAsync - and all of them failed
// identically. Every one of those attempts varied the same two things: the arguments, and the
// dispatch path. NONE of them separated the PLUGIN from the dispatch path.
//
// This does. It is a plain [CommandMethod], invoked by a user typing ACADMCP_CMDTEST at the
// AutoCAD command line - the most ordinary command context there is. If these calls work here,
// the plugin and its references are fine and the fault is in how tools are dispatched. If they
// fail here too, the fault is in the plugin or the AutoCAD build, and no amount of dispatch
// rework would ever have helped.
//
// Results go to a file because the command line scrolls and cannot be read from outside AutoCAD.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(AcadMcp.Plugin.Tools.CommandContextProbe))]

namespace AcadMcp.Plugin.Tools;

public static class CommandContextProbe
{
    private const string OutPath = @"C:\tmp\acadmcp-cmdtest.txt";

    [CommandMethod("ACADMCP_CMDTEST")]
    public static void Run()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var db = doc.Database;
        var log = new StringBuilder();

        void Say(string s)
        {
            log.AppendLine(s);
            ed.WriteMessage("\n" + s);
        }

        Say("ACADMCP_CMDTEST - is a command context enough?");
        Say("context: IsApplicationContext=" + AcadApp.DocumentManager.IsApplicationContext);

        // A. Editor.Command from a real command handler.
        try
        {
            int before = Count(db);
            ed.Command("_.CIRCLE", "0,0", "10");
            int after = Count(db);
            Say("A. Editor.Command(_.CIRCLE) -> OK, entities " + before + " -> " + after);
        }
        catch (System.Exception ex) { Say("A. Editor.Command(_.CIRCLE) -> FAIL " + Describe(ex)); }

        // B. A LISP expression typed as a command line, which is how eval_lisp wanted to work.
        try
        {
            ed.Command("(setq acadmcp_probe (+ 1 2))");
            var v = AcadApp.GetSystemVariable("USERI1");
            Say("B. Editor.Command(lisp expr) -> OK (USERI1=" + v + ")");
        }
        catch (System.Exception ex) { Say("B. Editor.Command(lisp expr) -> FAIL " + Describe(ex)); }

        // C. Application.Invoke, the clean route that never worked.
        try
        {
            var rb = AcadApp.Invoke(new ResultBuffer(
                new TypedValue((int)LispDataType.Text, "getvar"),
                new TypedValue((int)LispDataType.Text, "CLAYER")));
            var parts = new List<string>();
            if (rb is not null)
                foreach (var tv in rb.AsArray()) parts.Add(tv.TypeCode + ":" + tv.Value);
            Say("C. Application.Invoke((getvar CLAYER)) -> OK [" + string.Join(", ", parts) + "]");
        }
        catch (System.Exception ex) { Say("C. Application.Invoke -> FAIL " + Describe(ex)); }

        // D. The same Invoke wrapped in a transaction, which is the ONE difference between this
        //    command handler and RunWriteAsync that is easy to reproduce here.
        try
        {
            using var tr = db.TransactionManager.StartTransaction();
            var rb = AcadApp.Invoke(new ResultBuffer(
                new TypedValue((int)LispDataType.Text, "getvar"),
                new TypedValue((int)LispDataType.Text, "CLAYER")));
            tr.Commit();
            Say("D. Application.Invoke INSIDE a transaction -> OK");
        }
        catch (System.Exception ex) { Say("D. Application.Invoke INSIDE a transaction -> FAIL " + Describe(ex)); }

        // E. And Editor.Command inside a transaction, the other half of the same question.
        try
        {
            using var tr = db.TransactionManager.StartTransaction();
            ed.Command("_.CIRCLE", "50,50", "5");
            tr.Commit();
            Say("E. Editor.Command INSIDE a transaction -> OK");
        }
        catch (System.Exception ex) { Say("E. Editor.Command INSIDE a transaction -> FAIL " + Describe(ex)); }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);
            File.WriteAllText(OutPath, log.ToString());
            ed.WriteMessage("\nWritten to " + OutPath + "\n");
        }
        catch (System.Exception ex) { ed.WriteMessage("\ncould not write results: " + ex.Message + "\n"); }
    }

    private static string Describe(System.Exception ex) =>
        ex is Autodesk.AutoCAD.Runtime.Exception ae
            ? "[" + ae.ErrorStatus + "] " + ae.Message
            : "[" + ex.GetType().Name + "] " + ex.Message;

    private static int Count(Database db)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        int n = 0;
        foreach (ObjectId id in ms) if (!id.IsErased) n++;
        tr.Commit();
        return n;
    }
}
