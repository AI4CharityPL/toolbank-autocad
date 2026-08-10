// Second tranche of the acad-data category: tagging, querying, and CSV round-tripping a table.
// Registered under "acad.data.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// The tagging tools sit on the xdata layer from the first tranche rather than inventing a second
// storage mechanism: a tag IS xdata under a reserved application name. That means a tag survives
// copying with the entity, can be read by get_xdata like anything else, and shows up honestly in
// list_registered_apps instead of hiding somewhere only these tools know about.
//
// The CSV tools are OWN WORK, not wrappers: Table.ExportToCsv and Table.ImportFromCsv do not
// exist in the managed API - measured, not assumed. Reading and writing the cells is
// straightforward and makes the result exactly predictable, which is what the verification needs.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

internal static class DataQueryPluginTools
{
    /// The application name tags are filed under. Reserved on purpose and documented, so a tag is
    /// ordinary xdata that get_xdata can read rather than a private format.
    private const string TagApp = "TOOLBANK_TAG";

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.data.tag_entities",         TagEntities);
        host.Register("acad.data.list_tagged_entities", ListTaggedEntities);
        host.Register("acad.data.query_by_property",    QueryByProperty);
        host.Register("acad.data.export_table_to_csv",  ExportTableToCsv);
        host.Register("acad.data.import_csv_to_table",  ImportCsvToTable);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── tagging ───────────

    private static (string? Tag, string? Value) ReadTag(Entity ent)
    {
        using var rb = ent.XData;
        if (rb is null) return (null, null);
        bool mine = false;
        string? tag = null, val = null;
        foreach (var tv in rb.AsArray())
        {
            if ((DxfCode)tv.TypeCode == DxfCode.ExtendedDataRegAppName)
            {
                mine = string.Equals(tv.Value?.ToString(), TagApp, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!mine) continue;
            if ((DxfCode)tv.TypeCode != DxfCode.ExtendedDataAsciiString) continue;
            if (tag is null) tag = tv.Value?.ToString();
            else if (val is null) val = tv.Value?.ToString();
        }
        return (tag, val);
    }

    private static Task<ToolDispatchResult> TagEntities(JsonObject args, CancellationToken ct) =>
        Run("acad.data.tag_entities", args, ct, (doc, db, tr) =>
        {
            var a = Read<TagArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which entities to tag.");
            if (string.IsNullOrWhiteSpace(a.Tag))
                throw new ArgumentException(
                    "tag is required - the name of the tag, for example 'REVIEWED' or 'PHASE-2'.");

            // The reserved name has to be registered like any other, for the same reason.
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!rat.Has(TagApp))
            {
                rat.UpgradeOpen();
                var rec = new RegAppTableRecord { Name = TagApp };
                rat.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }

            var tagged = new List<object>();
            int replaced = 0;
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                var (oldTag, _) = ReadTag(ent);
                if (oldTag is not null) replaced++;

                var values = new List<TypedValue>
                {
                    new((int)DxfCode.ExtendedDataRegAppName, TagApp),
                    new((int)DxfCode.ExtendedDataAsciiString, a.Tag!),
                };
                if (a.Value is not null)
                    values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, a.Value));

                using (var rb = new ResultBuffer(values.ToArray())) ent.XData = rb;

                var (newTag, newVal) = ReadTag(ent);
                if (!string.Equals(newTag, a.Tag, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Entity " + h + " does not read back with the tag that was written.");
                tagged.Add(new { handle = h, tag = newTag, value = newVal, previousTag = oldTag });
            }

            return Wrap(new
            {
                count = tagged.Count,
                tag = a.Tag,
                value = a.Value,
                replacedExistingTag = replaced,
                entities = tagged,
                note = "A tag IS xdata, filed under the reserved application name " + TagApp +
                       ", so it travels with the entity when it is copied, can be read with " +
                       "get_xdata like anything else, and shows up in list_registered_apps rather " +
                       "than hiding in a private format. One tag per entity: tagging again " +
                       "REPLACES, and the count of entities that already had one is reported " +
                       "above because nothing else would tell you.",
            });
        });

    private static Task<ToolDispatchResult> ListTaggedEntities(JsonObject args, CancellationToken ct) =>
        Run("acad.data.list_tagged_entities", args, ct, (doc, db, tr) =>
        {
            var a = Read<TagArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var found = new List<object>();
            var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;                       // rule 26 §8
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                var (tag, val) = ReadTag(ent);
                if (tag is null) continue;
                tagCounts[tag] = tagCounts.TryGetValue(tag, out var c) ? c + 1 : 1;
                if (!string.IsNullOrWhiteSpace(a.Tag)
                    && !string.Equals(tag, a.Tag, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(new
                {
                    handle = ent.Handle.ToString(),
                    objectClass = ent.GetRXClass().Name,
                    layer = ent.Layer,
                    tag,
                    value = val,
                });
            }

            return Wrap(new
            {
                tag = a.Tag,
                count = found.Count,
                entities = found,
                tagsInDrawing = tagCounts.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                         .Select(kv => new { tag = kv.Key, count = kv.Value })
                                         .ToList(),
                note = "Model space only, and erased entities are skipped. Naming a tag filters to " +
                       "it; omitting one lists everything tagged. The tagsInDrawing summary counts " +
                       "every tag present regardless of the filter, which is the quick way to see " +
                       "what tags exist before querying one.",
            });
        });

    // ─────────── querying ───────────

    private static Task<ToolDispatchResult> QueryByProperty(JsonObject args, CancellationToken ct) =>
        Run("acad.data.query_by_property", args, ct, (doc, db, tr) =>
        {
            var a = Read<QueryArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Layer) && string.IsNullOrWhiteSpace(a.ObjectClass)
                && a.ColorIndex is null && string.IsNullOrWhiteSpace(a.HasXdataApp)
                && string.IsNullOrWhiteSpace(a.Linetype))
                throw new ArgumentException(
                    "Give at least one filter: layer, objectClass, colorIndex, linetype or " +
                    "hasXdataApp. A query with no filter would return the whole drawing, which " +
                    "is what list_entities_in_window is for.");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            bool HasApp(Entity e, string app)
            {
                using var rb = e.XData;
                if (rb is null) return false;
                foreach (var tv in rb.AsArray())
                    if ((DxfCode)tv.TypeCode == DxfCode.ExtendedDataRegAppName
                        && string.Equals(tv.Value?.ToString(), app, StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }

            var matched = new List<object>();
            int scanned = 0;
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                scanned++;

                if (!string.IsNullOrWhiteSpace(a.Layer)
                    && !string.Equals(ent.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(a.ObjectClass)
                    && ent.GetRXClass().Name.IndexOf(a.ObjectClass!, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (a.ColorIndex is not null && ent.ColorIndex != a.ColorIndex.Value) continue;
                if (!string.IsNullOrWhiteSpace(a.Linetype)
                    && !string.Equals(ent.Linetype, a.Linetype, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(a.HasXdataApp) && !HasApp(ent, a.HasXdataApp!)) continue;

                matched.Add(new
                {
                    handle = ent.Handle.ToString(),
                    objectClass = ent.GetRXClass().Name,
                    layer = ent.Layer,
                    colorIndex = ent.ColorIndex,
                    linetype = ent.Linetype,
                });
            }

            return Wrap(new
            {
                scanned,
                count = matched.Count,
                entities = matched,
                filters = new
                {
                    layer = a.Layer,
                    objectClass = a.ObjectClass,
                    colorIndex = a.ColorIndex,
                    linetype = a.Linetype,
                    hasXdataApp = a.HasXdataApp,
                },
                note = "Model space only, erased entities skipped, and every filter given must " +
                       "match - they are ANDed, not ORed. objectClass matches on substring, so " +
                       "'Line' finds AcDbLine and AcDbPolyline both; give the full class name to " +
                       "be exact. hasXdataApp finds entities carrying data from a named " +
                       "application, which is how you find everything one tool has touched. " +
                       "`scanned` is reported so a small count can be told from an empty drawing.",
            });
        });

    // ─────────── tables and CSV ───────────

    private static Table RequireTable(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required.");
        var obj = tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (obj is Table t) return t;
        throw new ArgumentException(
            "Entity " + handle + " is a " + obj.GetRXClass().Name + ", not a table.");
    }

    /// One CSV field. Quotes anything holding a comma, a quote or a newline, and doubles interior
    /// quotes - the ordinary CSV rules. Without this a cell containing a comma silently becomes
    /// two columns on the way out, which is the classic way a "working" export corrupts data.
    private static string CsvEscape(string s)
    {
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static Task<ToolDispatchResult> ExportTableToCsv(JsonObject args, CancellationToken ct) =>
        Run("acad.data.export_table_to_csv", args, ct, (doc, db, tr) =>
        {
            var a = Read<TableCsvArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: where to write the .csv.");
            var table = RequireTable(db, tr, a.Handle, OpenMode.ForRead);
            var path = Path.GetFullPath(a.Path!);
            if (File.Exists(path) && a.Overwrite != true)
                throw new ArgumentException(
                    "A file already exists at " + path + ". Pass overwrite true to replace it - " +
                    "silently overwriting someone's data is not something this will do by default.");

            int rows = table.Rows.Count, cols = table.Columns.Count;
            var sb = new StringBuilder();
            for (int r = 0; r < rows; r++)
            {
                var cells = new List<string>();
                for (int c = 0; c < cols; c++)
                {
                    string text;
                    try { text = table.Cells[r, c].TextString ?? ""; }
                    catch { text = ""; }          // a merged or empty cell is not an error
                    cells.Add(CsvEscape(text));
                }
                sb.Append(string.Join(",", cells));
                if (r < rows - 1) sb.Append("\r\n");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            var written = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (written == 0 && rows > 0)
                throw new InvalidOperationException(
                    "The file was written but is empty, though the table has " + rows + " rows.");

            return Wrap(new
            {
                path,
                rows,
                columns = cols,
                bytes = written,
                note = "Written here rather than by AutoCAD: Table.ExportToCsv does not exist in " +
                       "the managed API. Cell text is taken as displayed, so a formula exports as " +
                       "its RESULT and not as the formula. Fields containing a comma, a quote or " +
                       "a newline are quoted and interior quotes doubled - without that a cell " +
                       "with a comma in it silently becomes two columns, which is the classic way " +
                       "an export that looks fine corrupts the data. UTF-8, no byte-order mark.",
            });
        });

    private static Task<ToolDispatchResult> ImportCsvToTable(JsonObject args, CancellationToken ct) =>
        Run("acad.data.import_csv_to_table", args, ct, (doc, db, tr) =>
        {
            var a = Read<TableCsvArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the .csv to read.");
            var path = Path.GetFullPath(a.Path!);
            if (!File.Exists(path))
                throw new ArgumentException("No file at " + path + ".");

            var lines = File.ReadAllLines(path).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0)
                throw new ArgumentException("That CSV holds no rows.");
            var grid = lines.Select(ParseCsvLine).ToList();
            int cols = grid.Max(g => g.Count);
            int rows = grid.Count;

            var table = RequireTable(db, tr, a.Handle, OpenMode.ForWrite);
            table.SetSize(rows, cols);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    table.Cells[r, c].TextString = c < grid[r].Count ? grid[r][c] : "";

            table.GenerateLayout();

            // Read back through the table rather than trusting the write, and check a value that
            // would be wrong if the row and column order were transposed - which is the mistake
            // a symmetrical test grid would never catch.
            var check = table.Cells[0, 0].TextString ?? "";
            if (rows > 1 && cols > 1)
            {
                var expect01 = grid[0].Count > 1 ? grid[0][1] : "";
                var got01 = table.Cells[0, 1].TextString ?? "";
                if (!string.Equals(got01, expect01, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Cell (0,1) reads back as '" + got01 + "' but the CSV has '" + expect01 +
                        "' there - rows and columns are the wrong way round.");
            }

            return Wrap(new
            {
                handle = a.Handle,
                path,
                rows = table.Rows.Count,
                columns = table.Columns.Count,
                firstCell = check,
                note = "Read here rather than by AutoCAD: Table.ImportFromCsv does not exist in the " +
                       "managed API. The table is RESIZED to the CSV and its previous contents are " +
                       "replaced - this fills a table, it does not merge into one. Quoted fields " +
                       "are parsed properly, so a cell containing a comma stays one cell. Ragged " +
                       "rows are padded to the widest row rather than refused. Everything lands as " +
                       "TEXT: a column of numbers arrives as text that looks like numbers, which " +
                       "is what a CSV actually contains.",
            });
        });
}
