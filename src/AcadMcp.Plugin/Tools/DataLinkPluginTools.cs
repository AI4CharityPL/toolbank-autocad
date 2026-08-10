// Third tranche of the acad-data category: data links between a table and a spreadsheet.
// Registered under "acad.data.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED API shape, because the obvious names are wrong:
//   Database.DataLinkManagerId does NOT exist - Database.DataLinkManager does.
//   DataLinkManager.UpdateDataLink does NOT exist; the update routes are DataLink.Update and
//   Table.UpdateDataLink(UpdateDirection, UpdateOption).
//   DataLinkManager.GetDataLink takes a NAME only - there is no overload taking an ObjectId or a
//   collection - so listing goes through the ACAD_DATALINK dictionary in the named objects
//   dictionary, which is where the links actually live.
//   Table.SetDataLink/GetDataLink are [Obsolete] and name their replacement: Cells[r,c].DataLink.

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
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class DataLinkPluginTools
{
    private const string LinkDict = "ACAD_DATALINK";

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.data.create_data_link", CreateDataLink);
        host.Register("acad.data.list_data_links",  ListDataLinks);
        host.Register("acad.data.link_table_to_source", LinkTableToSource);
        host.Register("acad.data.unlink_table",     UnlinkTable);
        host.Register("acad.data.update_data_link", UpdateDataLinkTool);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Table RequireTable(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required.");
        var obj = tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (obj is Table t) return t;
        throw new ArgumentException(
            "Entity " + handle + " is a " + obj.GetRXClass().Name + ", not a table.");
    }

    private static List<string> LinkNames(Database db, Transaction tr)
    {
        var names = new List<string>();
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(LinkDict)) return names;
        var dict = (DBDictionary)tr.GetObject(nod.GetAt(LinkDict), OpenMode.ForRead);
        foreach (DBDictionaryEntry e in dict) names.Add(e.Key);
        return names;
    }

    private static Task<ToolDispatchResult> CreateDataLink(JsonObject args, CancellationToken ct) =>
        Run("acad.data.create_data_link", args, ct, (doc, db, tr) =>
        {
            var a = Read<DataLinkArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: what to call this data link.");
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException(
                    "path is required: the spreadsheet the link reads from.");
            var path = Path.GetFullPath(a.Path!);
            if (!File.Exists(path))
                throw new ArgumentException(
                    "No file at " + path + ". A data link records a path and does not create the " +
                    "file, so a link to a file that is not there would fail only later, when " +
                    "something tried to read it.");

            var dlm = db.DataLinkManager;
            if (LinkNames(db, tr).Any(n => string.Equals(n, a.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    "A data link called '" + a.Name + "' already exists in this drawing. " +
                    "list_data_links shows them; give a different name.");

            // The connection string is the source file, optionally with a sheet and range after
            // it. AutoCAD's own format, not one invented here.
            var conn = string.IsNullOrWhiteSpace(a.Range)
                ? path
                : path + "!" + a.Range;

            var dl = new DataLink
            {
                Name = a.Name!,
                Description = a.Description ?? ("Link to " + Path.GetFileName(path)),
                ConnectionString = conn,
                DataAdapterId = string.IsNullOrWhiteSpace(a.Adapter) ? "AcExcel" : a.Adapter!,
                DataLinkOption = DataLinkOption.PersistCache,
                UpdateOption = (int)UpdateOption.None,
            };

            ObjectId id;
            try { id = dlm.AddDataLink(dl); }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the data link with " + ex.ErrorStatus + ". The adapter '" +
                    (a.Adapter ?? "AcExcel") + "' may not accept this file type - the Excel " +
                    "adapter is the only one AutoCAD ships.");
            }
            if (id.IsNull)
                throw new InvalidOperationException(
                    "AddDataLink returned a null id, so the link was not created.");
            tr.AddNewlyCreatedDBObject(dl, true);

            // Read back through the manager BY NAME - a different route from the one that made it.
            var back = dlm.GetDataLink(a.Name!);
            if (back.IsNull)
                throw new InvalidOperationException(
                    "The data link does not read back by name after being added.");

            return Wrap(new
            {
                name = a.Name,
                handle = dl.Handle.ToString(),
                connectionString = conn,
                adapter = dl.DataAdapterId,
                sourceExists = File.Exists(path),
                note = "A data link records WHERE data comes from. link_table_to_source points a " +
                       "table at it and update_data_link " +
                       "pulls the values through. The connection string is the file path, " +
                       "optionally followed by ! and a sheet or range - AutoCAD's own format. Read " +
                       "back through the manager by name, which is a different route from the one " +
                       "that created it.",
            });
        });

    private static Task<ToolDispatchResult> ListDataLinks(JsonObject args, CancellationToken ct) =>
        Run("acad.data.list_data_links", args, ct, (doc, db, tr) =>
        {
            var dlm = db.DataLinkManager;
            var found = new List<object>();
            foreach (var name in LinkNames(db, tr))
            {
                var id = dlm.GetDataLink(name);
                if (id.IsNull) continue;
                var dl = (DataLink)tr.GetObject(id, OpenMode.ForRead);
                found.Add(new
                {
                    name = dl.Name,
                    description = dl.Description,
                    connectionString = dl.ConnectionString,
                    adapter = dl.DataAdapterId,
                    handle = dl.Handle.ToString(),
                });
            }

            return Wrap(new
            {
                count = found.Count,
                links = found,
                note = "Listed by walking the ACAD_DATALINK dictionary in the named objects " +
                       "dictionary, because DataLinkManager.GetDataLink takes a NAME and has no " +
                       "overload that enumerates - the dictionary is where the links actually " +
                       "live. A link being here says nothing about whether its source file still " +
                       "exists; the connection string is reported so that can be checked.",
            });
        });

    private static Task<ToolDispatchResult> LinkTableToSource(JsonObject args, CancellationToken ct) =>
        Run("acad.data.link_table_to_source", args, ct, (doc, db, tr) =>
        {
            var a = Read<DataLinkArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: which data link to attach.");
            var table = RequireTable(db, tr, a.Handle, OpenMode.ForWrite);

            var dlm = db.DataLinkManager;
            var id = dlm.GetDataLink(a.Name!);
            if (id.IsNull)
                throw new ArgumentException(
                    "No data link called '" + a.Name + "'. create_data_link makes one; " +
                    "list_data_links shows what is there.");

            int row = a.Row ?? 0, col = a.Column ?? 0;
            if (row < 0 || row >= table.Rows.Count || col < 0 || col >= table.Columns.Count)
                throw new ArgumentException(
                    "Cell (" + row + ", " + col + ") is outside this table, which is " +
                    table.Rows.Count + " by " + table.Columns.Count + ".");

            try { table.Cells[row, col].DataLink = id; }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to attach the data link with " + ex.ErrorStatus + ".");
            }

            if (table.Cells[row, col].IsLinked != true)
                throw new InvalidOperationException(
                    "The cell does not read back as linked after the data link was attached.");

            return Wrap(new
            {
                handle = a.Handle,
                name = a.Name,
                row,
                column = col,
                note = "Attached to ONE CELL - Table.Cells[row,column].DataLink is the current API; " +
                       "the whole-table SetDataLink is marked obsolete and names this as its " +
                       "replacement. The cell is the anchor: AutoCAD fills outwards from it when " +
                       "the link is updated. Attaching does NOT fetch anything - call " +
                       "update_data_link to pull the values through.",
            });
        });

    private static Task<ToolDispatchResult> UnlinkTable(JsonObject args, CancellationToken ct) =>
        Run("acad.data.unlink_table", args, ct, (doc, db, tr) =>
        {
            var a = Read<DataLinkArgsDto>(args);
            var table = RequireTable(db, tr, a.Handle, OpenMode.ForWrite);
            int row = a.Row ?? 0, col = a.Column ?? 0;
            if (row < 0 || row >= table.Rows.Count || col < 0 || col >= table.Columns.Count)
                throw new ArgumentException(
                    "Cell (" + row + ", " + col + ") is outside this table.");

            if (table.Cells[row, col].IsLinked != true)
                throw new ArgumentException(
                    "Cell (" + row + ", " + col + ") carries no data link, so there is nothing to " +
                    "unlink.");

            // MEASURED: assigning ObjectId.Null to Cell.DataLink is eInvalidInput. The route that
            // works is RemoveDataLink() on the cell, and IsLinked is what reads the state back -
            // DataLink returning a null id is not the same question.
            table.Cells[row, col].RemoveDataLink();
            if (table.Cells[row, col].IsLinked == true)
                throw new InvalidOperationException("The cell still reads back as linked.");

            return Wrap(new
            {
                handle = a.Handle,
                row,
                column = col,
                note = "The cell keeps the VALUES it was last given - unlinking stops the table " +
                       "following the source, it does not empty the table. The data link object " +
                       "itself stays in the drawing and can be attached again or left for " +
                       "list_data_links to show.",
            });
        });

    private static Task<ToolDispatchResult> UpdateDataLinkTool(JsonObject args, CancellationToken ct) =>
        Run("acad.data.update_data_link", args, ct, (doc, db, tr) =>
        {
            var a = Read<DataLinkArgsDto>(args);
            var table = RequireTable(db, tr, a.Handle, OpenMode.ForWrite);

            var dir = (a.Direction ?? "fromsource").Trim().ToLowerInvariant() switch
            {
                "fromsource" or "sourcetodata" => UpdateDirection.SourceToData,
                "tosource" or "datatosource" => UpdateDirection.DataToSource,
                _ => throw new ArgumentException(
                    "direction must be fromSource (read the spreadsheet into the table) or " +
                    "toSource (write the table back out to the spreadsheet)."),
            };

            var before = new List<string>();
            for (int c = 0; c < table.Columns.Count; c++)
                before.Add(table.Cells[0, c].TextString ?? "");

            try
            {
                // DataLinkManager.UpdateDataLink does not exist; this is the route that does.
                table.UpdateDataLink(dir, UpdateOption.None);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the update with " + ex.ErrorStatus + ". The source file may " +
                    "be missing, open exclusively elsewhere, or of a type the Excel adapter does " +
                    "not read.");
            }

            table.GenerateLayout();
            var after = new List<string>();
            for (int c = 0; c < table.Columns.Count; c++)
                after.Add(table.Cells[0, c].TextString ?? "");

            return Wrap(new
            {
                handle = a.Handle,
                direction = dir == UpdateDirection.SourceToData ? "fromSource" : "toSource",
                rows = table.Rows.Count,
                columns = table.Columns.Count,
                firstRowBefore = before,
                firstRowAfter = after,
                changed = !before.SequenceEqual(after),
                note = "Ran through Table.UpdateDataLink, because DataLinkManager.UpdateDataLink " +
                       "does not exist. The first row is reported BEFORE and AFTER, and `changed` " +
                       "says whether anything actually moved - AutoCAD returns no status of its " +
                       "own, so without that an update that fetched nothing would look exactly " +
                       "like one that did. `changed` false is not necessarily an error: it also " +
                       "means the table already matched the source.",
            });
        });
}
