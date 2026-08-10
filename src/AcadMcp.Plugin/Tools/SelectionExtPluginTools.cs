// Phase 3.4 extensions to the acad-selection category: similarity, ranges, duplicates,
// visibility isolation and saved filters.
// Registered under "acad.selection.<verb>"; everything runs on the UI thread.
//
// Rules: 9 (no interactive Editor.Get*), 10 (UI thread), 11 (transactions), 19, 26 (traps).
//
// Everything here enumerates model space and applies a predicate, which is the pattern the rest
// of this category already uses and the reason rule 26 §9 exists: the Editor's interactive
// selection helpers open a modal prompt that blocks the UI thread and is invisible to an agent.
//
// select_last and select_previous are the two exceptions, since they are the only way to reach
// AutoCAD's own selection history - and whether they work from where this plugin dispatches is
// measured rather than assumed, the same question that cost six tools in acad-lisp (rule 26 §15).

using System;
using System.Collections.Generic;
using System.Globalization;
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
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class SelectionExtPluginTools
{
    private const string FilterDict = "ACADMCP_SELECTION_FILTERS";

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.selection.select_similar",         SelectSimilar);
        host.Register("acad.selection.select_by_area_range",   SelectByAreaRange);
        host.Register("acad.selection.select_by_length_range", SelectByLengthRange);
        host.Register("acad.selection.select_duplicates",      SelectDuplicates);
        host.Register("acad.selection.select_last",            SelectLast);
        host.Register("acad.selection.hide_objects",           HideObjects);
        host.Register("acad.selection.isolate_objects",        IsolateObjects);
        host.Register("acad.selection.unisolate_objects",      UnisolateObjects);
        host.Register("acad.selection.create_selection_filter", CreateSelectionFilter);
        host.Register("acad.selection.apply_saved_filter",     ApplySavedFilter);
        host.Register("acad.selection.list_selection_filters", ListSelectionFilters);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static IEnumerable<Entity> ModelSpace(Database db, Transaction tr, OpenMode mode)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (id.IsErased) continue;                    // rule 26 §8
            if (tr.GetObject(id, mode) is Entity e) yield return e;
        }
    }

    private static object Describe(Entity e) => new
    {
        handle = e.Handle.ToString(),
        objectClass = e.GetRXClass().Name,
        layer = e.Layer,
        colorIndex = e.ColorIndex,
        visible = e.Visible,
    };

    // ─────────── similarity ───────────

    private static Task<ToolDispatchResult> SelectSimilar(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_similar", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle))
                throw new ArgumentException(
                    "handle is required: the entity whose likeness to look for.");
            var refEnt = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle!), OpenMode.ForRead);

            // Which properties count as "similar" is a CHOICE, so it is stated and adjustable
            // rather than hidden. AutoCAD's own SELECTSIMILAR is driven by SELECTSIMILARMODE and
            // defaults to name and layer; the same default is used here.
            bool byLayer = a.MatchLayer ?? true;
            bool byColor = a.MatchColor ?? false;
            bool byLinetype = a.MatchLinetype ?? false;

            var refClass = refEnt.GetRXClass().Name;
            var found = new List<object>();
            foreach (var e in ModelSpace(db, tr, OpenMode.ForRead))
            {
                if (e.GetRXClass().Name != refClass) continue;
                if (byLayer && !string.Equals(e.Layer, refEnt.Layer, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (byColor && e.ColorIndex != refEnt.ColorIndex) continue;
                if (byLinetype && !string.Equals(e.Linetype, refEnt.Linetype, StringComparison.OrdinalIgnoreCase))
                    continue;
                found.Add(Describe(e));
            }

            return Wrap(new
            {
                referenceHandle = a.Handle,
                referenceClass = refClass,
                matchedOn = new
                {
                    objectClass = true,
                    layer = byLayer,
                    color = byColor,
                    linetype = byLinetype,
                },
                count = found.Count,
                entities = found,
                note = "The reference entity is INCLUDED in the result, because it is similar to " +
                       "itself and excluding it would make the count disagree with what you see. " +
                       "Object class always has to match; layer matches by default and colour and " +
                       "linetype do not, which mirrors AutoCAD's own SELECTSIMILAR default. What " +
                       "counts as similar is a choice, so it is reported above rather than left " +
                       "implicit.",
            });
        });

    // ─────────── measured ranges ───────────

    private static (double? Area, double? Length) Measure(Entity e)
    {
        if (e is not Curve c) return (null, null);
        double? area = null, len = null;
        try { if (c.Closed) area = Math.Abs(c.Area); } catch { }
        try { len = Math.Abs(c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam)); }
        catch { }
        return (area, len);
    }

    private static Task<ToolDispatchResult> SelectByAreaRange(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_by_area_range", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (a.Min is null && a.Max is null)
                throw new ArgumentException("Give min, max, or both - a range with neither is not a range.");
            if (a.Min is not null && a.Max is not null && a.Min > a.Max)
                throw new ArgumentException("min is greater than max, so nothing could ever match.");

            var found = new List<object>();
            int scanned = 0, measurable = 0;
            foreach (var e in ModelSpace(db, tr, OpenMode.ForRead))
            {
                scanned++;
                var (area, _) = Measure(e);
                if (area is null) continue;
                measurable++;
                if (a.Min is not null && area < a.Min) continue;
                if (a.Max is not null && area > a.Max) continue;
                found.Add(new { handle = e.Handle.ToString(), objectClass = e.GetRXClass().Name,
                                layer = e.Layer, area });
            }

            return Wrap(new
            {
                min = a.Min, max = a.Max,
                scanned, measurable, count = found.Count, entities = found,
                note = "Only CLOSED curves have an area, so `measurable` says how many of the " +
                       "scanned entities could be considered at all - without it, a count of zero " +
                       "would not distinguish 'nothing in range' from 'nothing has an area'. " +
                       "Bounds are inclusive. A self-intersecting closed polyline reports the " +
                       "absolute value AutoCAD computes, which is not the area a person would " +
                       "measure by hand.",
            });
        });

    private static Task<ToolDispatchResult> SelectByLengthRange(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_by_length_range", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (a.Min is null && a.Max is null)
                throw new ArgumentException("Give min, max, or both - a range with neither is not a range.");
            if (a.Min is not null && a.Max is not null && a.Min > a.Max)
                throw new ArgumentException("min is greater than max, so nothing could ever match.");

            var found = new List<object>();
            int scanned = 0, measurable = 0;
            foreach (var e in ModelSpace(db, tr, OpenMode.ForRead))
            {
                scanned++;
                var (_, len) = Measure(e);
                if (len is null) continue;
                measurable++;
                if (a.Min is not null && len < a.Min) continue;
                if (a.Max is not null && len > a.Max) continue;
                found.Add(new { handle = e.Handle.ToString(), objectClass = e.GetRXClass().Name,
                                layer = e.Layer, length = len });
            }

            return Wrap(new
            {
                min = a.Min, max = a.Max,
                scanned, measurable, count = found.Count, entities = found,
                note = "Only CURVES have a length - a block insert or a piece of text does not - so " +
                       "`measurable` says how many were eligible, which is what tells a count of " +
                       "zero apart from a drawing full of things that cannot be measured. For a " +
                       "CLOSED curve the length is the perimeter. Bounds are inclusive.",
            });
        });

    private static Task<ToolDispatchResult> SelectDuplicates(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_duplicates", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            double tol = a.Tolerance ?? 1e-6;
            if (tol <= 0) throw new ArgumentException("tolerance must be greater than zero.");

            // Two entities count as duplicates when they are the same class on the same layer with
            // the same extents to within tolerance. That is a HEURISTIC and is described as one:
            // it will call two different splines sharing a bounding box duplicates, and it is
            // exactly what finds the doubled-up lines that OVERKILL exists for.
            var buckets = new Dictionary<string, List<Entity>>();
            int scanned = 0;
            foreach (var e in ModelSpace(db, tr, OpenMode.ForRead))
            {
                scanned++;
                Extents3d ext;
                try { ext = e.GeometricExtents; } catch { continue; }
                string key = string.Join("|", new[]
                {
                    e.GetRXClass().Name,
                    e.Layer.ToUpperInvariant(),
                    Q(ext.MinPoint.X, tol), Q(ext.MinPoint.Y, tol), Q(ext.MinPoint.Z, tol),
                    Q(ext.MaxPoint.X, tol), Q(ext.MaxPoint.Y, tol), Q(ext.MaxPoint.Z, tol),
                });
                if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Entity>();
                list.Add(e);
            }

            var groups = new List<object>();
            int extras = 0;
            foreach (var kv in buckets.Where(b => b.Value.Count > 1))
            {
                extras += kv.Value.Count - 1;
                groups.Add(new
                {
                    objectClass = kv.Value[0].GetRXClass().Name,
                    layer = kv.Value[0].Layer,
                    count = kv.Value.Count,
                    keep = kv.Value[0].Handle.ToString(),
                    duplicates = kv.Value.Skip(1).Select(x => x.Handle.ToString()).ToList(),
                });
            }

            return Wrap(new
            {
                scanned,
                groupCount = groups.Count,
                duplicateCount = extras,
                groups,
                tolerance = tol,
                note = "Finds nothing and deletes nothing - it REPORTS. Each group names one entity " +
                       "to `keep` and lists the rest as `duplicates`, so the handles can be passed " +
                       "to modify.delete_entities when you have looked at them. Duplicates are " +
                       "judged by class, layer and bounding box within the tolerance, which is a " +
                       "HEURISTIC: two different splines that happen to share a bounding box will " +
                       "be reported together. It is the same test that finds the doubled-up lines " +
                       "OVERKILL exists for, and the reason the tool reports rather than acts.",
            });
        });

    private static string Q(double v, double tol) =>
        Math.Round(v / tol).ToString(CultureInfo.InvariantCulture);

    // ─────────── AutoCAD's own selection history ───────────

    private static Task<ToolDispatchResult> SelectLast(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_last", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            int want = a.Count is > 0 ? a.Count!.Value : 1;

            // Editor.SelectLast reaches AutoCAD's own idea of the last selection, which in an
            // agent flow is usually empty - nothing here ever makes a UI selection. So the
            // reliable meaning of "last" is the last entity ADDED to model space, and model space
            // enumerates in creation order, which is what this uses. Both are reported: the
            // Editor's answer when it has one, and the creation-order answer always.
            string? editorNote = null;
            var editorHandles = new List<string>();
            try
            {
                var res = doc.Editor.SelectLast();
                if (res.Status == PromptStatus.OK)
                    foreach (var id in res.Value.GetObjectIds())
                        editorHandles.Add(id.Handle.ToString());
                else
                    editorNote = "Editor.SelectLast returned " + res.Status;
            }
            catch (System.Exception ex) { editorNote = "Editor.SelectLast failed: " + ex.Message; }

            var all = ModelSpace(db, tr, OpenMode.ForRead).ToList();
            var last = all.Skip(Math.Max(0, all.Count - want))
                          .Select(Describe).ToList();

            return Wrap(new
            {
                count = last.Count,
                entities = last,
                editorSelectLastCount = editorHandles.Count,
                editorSelectLastNote = editorNote,
                note = "The entities returned are the LAST ADDED to model space, which enumerates " +
                       "in creation order - that is the dependable meaning of 'last' for an agent, " +
                       "since nothing this bank does creates a UI selection. AutoCAD's own " +
                       "Editor.SelectLast is consulted as well and reported separately, so you can " +
                       "see when it has something and when it does not; in a scripted session it " +
                       "is usually empty, which is a fact about the editor rather than an error.",
            });
        });

    // ─────────── visibility ───────────

    private static Task<ToolDispatchResult> HideObjects(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.hide_objects", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which entities to hide.");

            int hidden = 0, already = 0;
            foreach (var h in a.Handles)
            {
                var e = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                if (!e.Visible) { already++; continue; }
                e.Visible = false;
                if (e.Visible)
                    throw new InvalidOperationException("Entity " + h + " still reads back visible.");
                hidden++;
            }

            return Wrap(new
            {
                hidden, alreadyHidden = already, requested = a.Handles.Count,
                note = "Hidden by setting Entity.Visible false, which is what AutoCAD's HIDEOBJECTS " +
                       "does. The entities are STILL THERE - they are not erased, they still count " +
                       "in a selection, and they come back with unisolate_objects. Each one is " +
                       "read back after being hidden, and entities already hidden are counted " +
                       "separately rather than reported as newly hidden.",
            });
        });

    private static Task<ToolDispatchResult> IsolateObjects(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.isolate_objects", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which entities to keep visible.");
            var keep = new HashSet<string>(a.Handles.Select(h => h.ToUpperInvariant()));

            int hidden = 0, kept = 0;
            foreach (var e in ModelSpace(db, tr, OpenMode.ForWrite))
            {
                if (keep.Contains(e.Handle.ToString().ToUpperInvariant()))
                {
                    // Isolating must also REVEAL the named entities - isolating something that
                    // happened to be hidden and leaving it hidden would be the wrong answer.
                    if (!e.Visible) e.Visible = true;
                    kept++;
                    continue;
                }
                if (!e.Visible) continue;
                e.Visible = false;
                hidden++;
            }

            if (kept != keep.Count)
                throw new InvalidOperationException(
                    "Asked to isolate " + keep.Count + " entities but found " + kept +
                    " of them in model space.");

            return Wrap(new
            {
                kept, hidden,
                note = "Everything else in model space is hidden; the named entities are made " +
                       "visible, since isolating something that was already hidden and leaving it " +
                       "hidden would be the wrong answer. Nothing is erased - unisolate_objects " +
                       "brings it all back. Model space only: entities in a layout are untouched.",
            });
        });

    private static Task<ToolDispatchResult> UnisolateObjects(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.unisolate_objects", args, ct, (doc, db, tr) =>
        {
            int shown = 0, alreadyVisible = 0;
            foreach (var e in ModelSpace(db, tr, OpenMode.ForWrite))
            {
                if (e.Visible) { alreadyVisible++; continue; }
                e.Visible = true;
                shown++;
            }

            return Wrap(new
            {
                shown, alreadyVisible,
                note = "Shows EVERYTHING in model space, which is what AutoCAD's UNISOLATEOBJECTS " +
                       "does - it does not restore some earlier state, and there is no undo of an " +
                       "isolate that puts back exactly what was hidden before. Anything hidden for " +
                       "its own reasons before you isolated will therefore also reappear; that is " +
                       "the behaviour, not a defect.",
            });
        });

    // ─────────── saved filters ───────────

    private static DBDictionary FilterDictionary(Database db, Transaction tr, bool create)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,
            create ? OpenMode.ForWrite : OpenMode.ForRead);
        if (nod.Contains(FilterDict))
            return (DBDictionary)tr.GetObject(nod.GetAt(FilterDict),
                create ? OpenMode.ForWrite : OpenMode.ForRead);
        if (!create)
            throw new ArgumentException(
                "No selection filters have been saved in this drawing yet. " +
                "create_selection_filter makes one.");
        var d = new DBDictionary();
        nod.SetAt(FilterDict, d);
        tr.AddNewlyCreatedDBObject(d, true);
        return d;
    }

    private static Task<ToolDispatchResult> CreateSelectionFilter(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.create_selection_filter", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: what to call the filter.");
            if (string.IsNullOrWhiteSpace(a.Layer) && string.IsNullOrWhiteSpace(a.ObjectClass)
                && a.ColorIndex is null && a.Min is null && a.Max is null)
                throw new ArgumentException(
                    "A filter needs at least one criterion: layer, objectClass, colorIndex, or a " +
                    "min/max range. One with none would match everything.");

            var dict = FilterDictionary(db, tr, create: true);
            if (dict.Contains(a.Name!))
                throw new ArgumentException(
                    "A filter called '" + a.Name + "' already exists. Delete it or use another " +
                    "name; replacing one silently would change what every later call selects.");

            var values = new List<TypedValue>
            {
                new((int)DxfCode.Text, a.Layer ?? ""),
                new((int)DxfCode.Text, a.ObjectClass ?? ""),
                new((int)DxfCode.Int32, a.ColorIndex ?? -1),
                new((int)DxfCode.Real, a.Min ?? double.NaN),
                new((int)DxfCode.Real, a.Max ?? double.NaN),
                new((int)DxfCode.Text, (a.RangeKind ?? "none").ToLowerInvariant()),
            };
            var xr = new Xrecord { Data = new ResultBuffer(values.ToArray()) };
            dict.SetAt(a.Name!, xr);
            tr.AddNewlyCreatedDBObject(xr, true);

            var back = FilterDictionary(db, tr, create: false);
            if (!back.Contains(a.Name!))
                throw new InvalidOperationException("The filter does not read back from the dictionary.");

            return Wrap(new
            {
                name = a.Name,
                layer = a.Layer, objectClass = a.ObjectClass, colorIndex = a.ColorIndex,
                min = a.Min, max = a.Max, rangeKind = a.RangeKind ?? "none",
                note = "Saved in the drawing, in a dictionary of this bank's own, so it travels " +
                       "with the .dwg and is there in the next session - a filter that only " +
                       "existed in memory would be useless for the job filters are for. Every " +
                       "criterion given must match when it is applied; they are ANDed. rangeKind " +
                       "says whether min and max mean an area or a length.",
            });
        });

    private static Task<ToolDispatchResult> ListSelectionFilters(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.list_selection_filters", args, ct, (doc, db, tr) =>
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            var found = new List<object>();
            if (nod.Contains(FilterDict))
            {
                var dict = (DBDictionary)tr.GetObject(nod.GetAt(FilterDict), OpenMode.ForRead);
                foreach (DBDictionaryEntry e in dict)
                {
                    var xr = (Xrecord)tr.GetObject(e.Value, OpenMode.ForRead);
                    var v = xr.Data?.AsArray() ?? Array.Empty<TypedValue>();
                    found.Add(new
                    {
                        name = e.Key,
                        layer = Str(v, 0), objectClass = Str(v, 1),
                        colorIndex = Int(v, 2) is int ci && ci >= 0 ? ci : (int?)null,
                        min = Dbl(v, 3), max = Dbl(v, 4), rangeKind = Str(v, 5),
                    });
                }
            }
            return Wrap(new
            {
                count = found.Count,
                filters = found,
                note = "Filters saved in this drawing. They are stored in a dictionary, so they " +
                       "travel with the .dwg rather than living only for the session.",
            });
        });

    private static string? Str(TypedValue[] v, int i) =>
        i < v.Length && v[i].Value is string s && s.Length > 0 ? s : null;
    private static int? Int(TypedValue[] v, int i) =>
        i < v.Length ? Convert.ToInt32(v[i].Value) : null;
    private static double? Dbl(TypedValue[] v, int i)
    {
        if (i >= v.Length) return null;
        var d = Convert.ToDouble(v[i].Value);
        return double.IsNaN(d) ? null : d;
    }

    private static Task<ToolDispatchResult> ApplySavedFilter(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.apply_saved_filter", args, ct, (doc, db, tr) =>
        {
            var a = Read<SelExtArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: which saved filter to apply.");
            var dict = FilterDictionary(db, tr, create: false);
            if (!dict.Contains(a.Name!))
                throw new ArgumentException(
                    "No filter called '" + a.Name + "'. list_selection_filters shows what is saved.");

            var xr = (Xrecord)tr.GetObject(dict.GetAt(a.Name!), OpenMode.ForRead);
            var v = xr.Data?.AsArray() ?? Array.Empty<TypedValue>();
            string? layer = Str(v, 0), cls = Str(v, 1), kind = Str(v, 5);
            int? color = Int(v, 2) is int ci && ci >= 0 ? ci : null;
            double? min = Dbl(v, 3), max = Dbl(v, 4);

            var found = new List<object>();
            int scanned = 0;
            foreach (var e in ModelSpace(db, tr, OpenMode.ForRead))
            {
                scanned++;
                if (layer is not null && !string.Equals(e.Layer, layer, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cls is not null && e.GetRXClass().Name.IndexOf(cls, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (color is not null && e.ColorIndex != color.Value) continue;
                if (min is not null || max is not null)
                {
                    var (area, len) = Measure(e);
                    double? m = string.Equals(kind, "area", StringComparison.OrdinalIgnoreCase) ? area : len;
                    if (m is null) continue;
                    if (min is not null && m < min) continue;
                    if (max is not null && m > max) continue;
                }
                found.Add(Describe(e));
            }

            return Wrap(new
            {
                name = a.Name,
                criteria = new { layer, objectClass = cls, colorIndex = color, min, max, rangeKind = kind },
                scanned, count = found.Count, entities = found,
                note = "The criteria actually used are reported above, read back out of the saved " +
                       "filter rather than restated from the request - so a filter that was saved " +
                       "differently from how it was meant shows up here rather than silently " +
                       "selecting the wrong things. All criteria are ANDed, and `scanned` tells a " +
                       "small result apart from an empty drawing.",
            });
        });
}
