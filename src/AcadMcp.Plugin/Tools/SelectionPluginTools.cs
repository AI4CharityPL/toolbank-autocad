// AutoCAD plugin handlers for the acad-selection category.
// Registered under "acad.selection.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern).
//
// Selection model: We avoid Editor.SelectXxx interactive prompts (rule 14 - no AutoCAD
// command-line modals). Instead we enumerate ModelSpace and apply geometric/property
// predicates against entity bounding boxes and properties. This keeps everything
// scriptable from the agent and free of side effects.

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
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class SelectionPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Named selection sets are stored as XRecord under db.NamedObjectsDictionaryId/AcadMcp_SelectionSets.
    private const string SelectionDictName = "ACADMCP_SELECTION_SETS";

    public static void Register(ToolHost host)
    {
        host.Register("acad.selection.select_all",          SelectAll);
        host.Register("acad.selection.select_by_layer",     SelectByLayer);
        host.Register("acad.selection.select_by_color",     SelectByColor);
        host.Register("acad.selection.select_by_type",      SelectByType);
        host.Register("acad.selection.select_by_handle",    SelectByHandle);
        host.Register("acad.selection.select_window",       SelectWindow);
        host.Register("acad.selection.select_fence",        SelectFence);
        host.Register("acad.selection.select_polygon",      SelectPolygon);
        host.Register("acad.selection.filter_entities",     FilterEntitiesHandler);
        host.Register("acad.selection.save_selection_set",  SaveSelectionSet);
        host.Register("acad.selection.load_selection_set",  LoadSelectionSet);
        host.Register("acad.selection.count_entities",      CountEntities);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static IEnumerable<Entity> EnumerateModelSpace(Database db, Transaction tr)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (id.IsErased) continue;
            yield return (Entity)tr.GetObject(id, OpenMode.ForRead);
        }
    }

    private static JsonObject WrapSelection(IEnumerable<Entity> ents)
    {
        var handles = ents.Where(e => !e.IsErased).Select(e => e.Handle.ToString()).ToList();
        return Wrap(new { count = handles.Count, handles });
    }

    // ─────────────── selection by criteria ───────────────

    private static Task<ToolDispatchResult> SelectAll(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_all", args, ct, (doc, db, tr) =>
            WrapSelection(EnumerateModelSpace(db, tr)));

    private static Task<ToolDispatchResult> SelectByLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_by_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<ByLayerArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Layer)) throw new ArgumentException("layer required.");
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(a.Layer)) throw new ArgumentException($"layer '{a.Layer}' not found.");
            var ltr = (LayerTableRecord)tr.GetObject(lt[a.Layer], OpenMode.ForRead);
            if (a.Frozen.HasValue && a.Frozen.Value != ltr.IsFrozen)
                return Wrap(new { count = 0, handles = Array.Empty<string>() });
            return WrapSelection(EnumerateModelSpace(db, tr).Where(e => string.Equals(e.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)));
        });

    private static Task<ToolDispatchResult> SelectByColor(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_by_color", args, ct, (doc, db, tr) =>
        {
            var a = Read<ByColorArgsDto>(args);
            return WrapSelection(EnumerateModelSpace(db, tr).Where(e => MatchColor(e, a.Color, a.MatchAci)));
        });

    private static bool MatchColor(Entity e, ColorDto c, bool matchAci)
    {
        var col = e.Color;
        if (matchAci && c.AciIndex.HasValue && col.IsByAci)
            return col.ColorIndex == c.AciIndex.Value;
        return col.Red == (byte)c.R && col.Green == (byte)c.G && col.Blue == (byte)c.B;
    }

    private static Task<ToolDispatchResult> SelectByType(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_by_type", args, ct, (doc, db, tr) =>
        {
            var a = Read<ByTypeArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.DxfType)) throw new ArgumentException("dxfType required.");
            var target = a.DxfType.Trim().ToUpperInvariant();
            return WrapSelection(EnumerateModelSpace(db, tr).Where(e =>
                string.Equals(GetDxfName(e), target, StringComparison.OrdinalIgnoreCase)));
        });

    private static string GetDxfName(Entity e)
    {
        try { return e.GetRXClass().DxfName; } catch { return ""; }
    }

    private static Task<ToolDispatchResult> SelectByHandle(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_by_handle", args, ct, (doc, db, tr) =>
        {
            var a = Read<ByHandleArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                return Wrap(new { count = 0, handles = Array.Empty<string>() });
            var ok = new List<string>(a.Handles.Count);
            foreach (var h in a.Handles)
            {
                try
                {
                    var id = AcadEnv.ResolveHandle(db, h);
                    var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                    if (!ent.IsErased) ok.Add(ent.Handle.ToString());
                }
                catch { /* skip invalid handle */ }
            }
            return Wrap(new { count = ok.Count, handles = ok });
        });

    // ─────────────── geometric selection ───────────────

    private static Task<ToolDispatchResult> SelectWindow(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_window", args, ct, (doc, db, tr) =>
        {
            var a = Read<WindowArgsDto>(args);
            var min = AcadEnv.ToPoint3d(a.Min);
            var max = AcadEnv.ToPoint3d(a.Max);
            var box = new Extents3d(
                new Point3d(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z)),
                new Point3d(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z)));
            return WrapSelection(EnumerateModelSpace(db, tr).Where(e => InWindow(e, box, a.Crossing)));
        });

    private static bool InWindow(Entity e, Extents3d box, bool crossing)
    {
        if (!e.Bounds.HasValue) return false;
        var b = e.Bounds.Value;
        if (crossing)
        {
            return b.MinPoint.X <= box.MaxPoint.X && b.MaxPoint.X >= box.MinPoint.X
                && b.MinPoint.Y <= box.MaxPoint.Y && b.MaxPoint.Y >= box.MinPoint.Y
                && b.MinPoint.Z <= box.MaxPoint.Z && b.MaxPoint.Z >= box.MinPoint.Z;
        }
        return b.MinPoint.X >= box.MinPoint.X && b.MaxPoint.X <= box.MaxPoint.X
            && b.MinPoint.Y >= box.MinPoint.Y && b.MaxPoint.Y <= box.MaxPoint.Y
            && b.MinPoint.Z >= box.MinPoint.Z && b.MaxPoint.Z <= box.MaxPoint.Z;
    }

    private static Task<ToolDispatchResult> SelectFence(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_fence", args, ct, (doc, db, tr) =>
        {
            var a = Read<FenceArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 2)
                throw new ArgumentException("fence needs >= 2 vertices.");
            var verts = a.Vertices.Select(p => AcadEnv.ToPoint3d(p)).ToList();
            return WrapSelection(EnumerateModelSpace(db, tr).Where(e => CrossesFence(e, verts)));
        });

    private static bool CrossesFence(Entity e, List<Point3d> verts)
    {
        if (e is Curve c)
        {
            using var pts = new Point3dCollection();
            for (int i = 0; i < verts.Count - 1; i++)
            {
                using var seg = new Line(verts[i], verts[i + 1]);
                c.IntersectWith(seg, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                if (pts.Count > 0) return true;
            }
            return false;
        }
        // Non-curves: bbox vs each fence segment.
        if (!e.Bounds.HasValue) return false;
        var b = e.Bounds.Value;
        for (int i = 0; i < verts.Count - 1; i++)
            if (SegmentIntersectsBox(verts[i], verts[i + 1], b)) return true;
        return false;
    }

    private static bool SegmentIntersectsBox(Point3d p0, Point3d p1, Extents3d b)
    {
        // Cheap conservative test: any endpoint inside box, or 2D AABB overlap with bbox of segment.
        if (PointInBox(p0, b) || PointInBox(p1, b)) return true;
        var sb = new Extents3d(
            new Point3d(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), Math.Min(p0.Z, p1.Z)),
            new Point3d(Math.Max(p0.X, p1.X), Math.Max(p0.Y, p1.Y), Math.Max(p0.Z, p1.Z)));
        return sb.MinPoint.X <= b.MaxPoint.X && sb.MaxPoint.X >= b.MinPoint.X
            && sb.MinPoint.Y <= b.MaxPoint.Y && sb.MaxPoint.Y >= b.MinPoint.Y;
    }

    private static bool PointInBox(Point3d p, Extents3d b) =>
        p.X >= b.MinPoint.X && p.X <= b.MaxPoint.X &&
        p.Y >= b.MinPoint.Y && p.Y <= b.MaxPoint.Y;

    private static Task<ToolDispatchResult> SelectPolygon(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.select_polygon", args, ct, (doc, db, tr) =>
        {
            var a = Read<PolygonArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 3)
                throw new ArgumentException("polygon needs >= 3 vertices.");
            var poly = a.Vertices.Select(p => new Point2d(p.X, p.Y)).ToList();
            return WrapSelection(EnumerateModelSpace(db, tr).Where(e => InPolygon(e, poly, a.Crossing)));
        });

    private static bool InPolygon(Entity e, List<Point2d> poly, bool crossing)
    {
        if (!e.Bounds.HasValue) return false;
        var b = e.Bounds.Value;
        // 4 corners of the entity's XY bbox.
        var corners = new[]
        {
            new Point2d(b.MinPoint.X, b.MinPoint.Y),
            new Point2d(b.MaxPoint.X, b.MinPoint.Y),
            new Point2d(b.MaxPoint.X, b.MaxPoint.Y),
            new Point2d(b.MinPoint.X, b.MaxPoint.Y),
        };
        int inside = corners.Count(c => PointInPoly(c, poly));
        return crossing ? inside > 0 : inside == 4;
    }

    private static bool PointInPoly(Point2d p, List<Point2d> poly)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            bool cross = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                         (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y + 1e-30) + pi.X);
            if (cross) inside = !inside;
        }
        return inside;
    }

    private static Task<ToolDispatchResult> FilterEntitiesHandler(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.filter_entities", args, ct, (doc, db, tr) =>
        {
            var a = Read<FilterEntitiesDto>(args);
            IEnumerable<Entity> source;
            if (a.Handles is { Count: > 0 })
            {
                var list = new List<Entity>(a.Handles.Count);
                foreach (var h in a.Handles)
                {
                    try { list.Add((Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForRead)); }
                    catch { }
                }
                source = list;
            }
            else
            {
                source = EnumerateModelSpace(db, tr);
            }
            string? type = a.DxfType?.Trim().ToUpperInvariant();
            return WrapSelection(source.Where(e =>
                (a.Layer is null || string.Equals(e.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) &&
                (type is null || string.Equals(GetDxfName(e), type, StringComparison.OrdinalIgnoreCase)) &&
                (a.Color is null || MatchColor(e, a.Color, a.Color.AciIndex.HasValue))));
        });

    // ─────────────── named selection sets ───────────────

    private static DBDictionary GetSelectionDict(Database db, Transaction tr, bool createIfMissing)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (nod.Contains(SelectionDictName))
            return (DBDictionary)tr.GetObject(nod.GetAt(SelectionDictName), OpenMode.ForWrite);
        if (!createIfMissing)
            throw new InvalidOperationException("no saved selection sets exist.");
        nod.UpgradeOpen();
        var dict = new DBDictionary();
        nod.SetAt(SelectionDictName, dict);
        tr.AddNewlyCreatedDBObject(dict, true);
        return dict;
    }

    private static Task<ToolDispatchResult> SaveSelectionSet(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.save_selection_set", args, ct, (doc, db, tr) =>
        {
            var a = Read<SaveSetArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name required.");
            var dict = GetSelectionDict(db, tr, createIfMissing: true);
            // Validate handles before persisting.
            var validHandles = new List<string>(a.Handles?.Count ?? 0);
            if (a.Handles != null)
            {
                foreach (var h in a.Handles)
                {
                    try { _ = AcadEnv.ResolveHandle(db, h); validHandles.Add(h); }
                    catch { }
                }
            }
            // Encode as a single delimited string in an Xrecord (TypedValue 1).
            var xr = new Xrecord
            {
                Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, string.Join(",", validHandles))),
            };
            if (dict.Contains(a.Name))
            {
                // Replace via SetAt - existing entry is erased automatically.
            }
            dict.SetAt(a.Name, xr);
            tr.AddNewlyCreatedDBObject(xr, true);
            return Wrap(new { count = validHandles.Count });
        });

    private static Task<ToolDispatchResult> LoadSelectionSet(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.load_selection_set", args, ct, (doc, db, tr) =>
        {
            var a = Read<LoadSetArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name required.");
            var dict = GetSelectionDict(db, tr, createIfMissing: false);
            if (!dict.Contains(a.Name))
                throw new InvalidOperationException($"selection set '{a.Name}' does not exist.");
            var xr = (Xrecord)tr.GetObject(dict.GetAt(a.Name), OpenMode.ForRead);
            var stored = "";
            using (var rb = xr.Data) { foreach (var tv in rb) { stored = tv.Value?.ToString() ?? ""; break; } }
            var wanted = stored.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var live = new List<string>(wanted.Length);
            foreach (var h in wanted)
            {
                try { _ = AcadEnv.ResolveHandle(db, h); live.Add(h); }
                catch { }
            }
            return Wrap(new { name = a.Name, handles = live });
        });

    // ─────────────── counting ───────────────

    private static Task<ToolDispatchResult> CountEntities(JsonObject args, CancellationToken ct) =>
        Run("acad.selection.count_entities", args, ct, (doc, db, tr) =>
        {
            var a = Read<ByTypeArgsDto>(args);
            string? type = string.IsNullOrWhiteSpace(a.DxfType) ? null : a.DxfType.Trim().ToUpperInvariant();
            int count = 0;
            foreach (var e in EnumerateModelSpace(db, tr))
            {
                if (type is null || string.Equals(GetDxfName(e), type, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return Wrap(new { count });
        });
}
