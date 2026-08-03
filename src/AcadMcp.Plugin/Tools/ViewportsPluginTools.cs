// AutoCAD plugin handlers for the acad-viewports category.
//
// create_viewport and set_viewport_scale are NOT here - they stay under acad.layouts.* and the
// backend points at those. One implementation cannot drift from itself.
//
// Per-viewport layer state lives in two different places, which is the thing to know:
//   frozen    -> on the Viewport, via FreezeLayersInViewport / ThawLayersInViewport /
//                GetFrozenLayers. Implemented here and working.
//   overrides -> colour/linetype/lineweight/transparency, on the LayerTableRecord. NOT
//                implemented: the 2025 SDK exposes HasOverrides as a plain bool with no
//                viewport argument and none of the Set*InViewport accessors this needs.
//                Withheld rather than guessed at - see ViewportsTools.cs header.
// Looking for freezes on the layer, or overrides on the viewport, finds nothing and reads
// like "nothing is set" rather than like a mistake.

using System;
using System.Collections.Generic;
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

internal static class ViewportsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.viewports.create_viewport", CreateRect);
        host.Register("acad.viewports.set_viewport_scale", SetScale);
        host.Register("acad.viewports.create_polygonal_viewport", CreatePolygonal);
        host.Register("acad.viewports.delete_viewport", DeleteViewport);
        host.Register("acad.viewports.list_viewports", ListViewports);
        host.Register("acad.viewports.get_viewport_info", GetViewportInfo);
        host.Register("acad.viewports.get_viewport_extents_in_model", GetExtentsInModel);
        host.Register("acad.viewports.set_viewport_lock", SetLock);
        host.Register("acad.viewports.set_viewport_on_off", SetOnOff);
        host.Register("acad.viewports.set_viewport_shade_plot", SetShadePlot);
        host.Register("acad.viewports.set_viewport_layer_freeze", (a, c) => FreezeThaw(a, c, freeze: true));
        host.Register("acad.viewports.set_viewport_layer_thaw", (a, c) => FreezeThaw(a, c, freeze: false));
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();
    private static Task<ToolDispatchResult> Run(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);

    // ─────────── helpers ───────────

    private static Viewport OpenVp(Database db, Transaction tr, string handle, OpenMode mode)
    {
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle), mode);
        if (ent is not Viewport vp)
            throw new ArgumentException(
                $"Handle {handle} is a {ent.GetRXClass().Name}, not a Viewport. " +
                "Use list_viewports to get a viewport handle.");
        // Viewport number 1 is the paper-space "sheet" pseudo-viewport, not a window.
        if (vp.Number == 1 || vp.Number < 0)
            throw new ArgumentException(
                $"Handle {handle} is the layout's own paper-space viewport (number 1), not a " +
                "drawing window. It cannot be scaled, locked or layer-overridden.");
        return vp;
    }

    private static string LayoutNameOf(Transaction tr, Viewport vp)
    {
        try
        {
            var btr = (BlockTableRecord)tr.GetObject(vp.OwnerId, OpenMode.ForRead);
            var lay = (Layout)tr.GetObject(btr.LayoutId, OpenMode.ForRead);
            return lay.LayoutName;
        }
        catch { return "?"; }
    }

    private static string ScaleLabel(double s)
    {
        if (s <= 0) return "unset";
        var inv = 1.0 / s;
        return Math.Abs(inv - Math.Round(inv)) < 1e-6 ? $"1:{Math.Round(inv)}" : $"{s:0.######}";
    }

    private static List<string> FrozenLayerNames(Transaction tr, Viewport vp)
    {
        var names = new List<string>();
        try
        {
            var ids = vp.GetFrozenLayers();
            if (ids is null) return names;
            foreach (ObjectId id in ids)
            {
                try { names.Add(((LayerTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name); } catch { }
            }
        }
        catch { }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>
    /// Layers carrying ANY per-viewport property override. LayerTableRecord.HasOverrides is a
    /// plain bool with no viewport argument in the 2025 SDK, so this reports "this layer is
    /// overridden in some viewport", not "in THIS one". Reported as-is rather than implied to
    /// be per-viewport - see the set/list/clear tools withheld from this tranche.
    /// </summary>
    private static List<string> OverriddenLayerNames(Database db, Transaction tr, Viewport vp)
    {
        var names = new List<string>();
        try
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in lt)
            {
                var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                try { if (ltr.HasOverrides) names.Add(ltr.Name); } catch { }
            }
        }
        catch { }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static object Info(Database db, Transaction tr, Viewport vp) => new
    {
        handle = AcadEnv.ToHandle(vp).Handle,
        layoutName = LayoutNameOf(tr, vp),
        number = vp.Number,
        centerPaper = new { x = vp.CenterPoint.X, y = vp.CenterPoint.Y, z = vp.CenterPoint.Z },
        widthPaper = vp.Width,
        heightPaper = vp.Height,
        customScale = vp.CustomScale,
        scaleLabel = ScaleLabel(vp.CustomScale),
        locked = vp.Locked,
        on = vp.On,
        layer = vp.Layer,
        shadePlot = vp.ShadePlot.ToString(),
        isPolygonal = !vp.NonRectClipEntityId.IsNull,
        frozenLayers = FrozenLayerNames(tr, vp),
        overriddenLayers = OverriddenLayerNames(db, tr, vp),
    };

    private static ObjectIdCollection ResolveLayers(Database db, Transaction tr, IReadOnlyList<string> names)
    {
        if (names is null || names.Count == 0) throw new ArgumentException("layers: at least one name required.");
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        var ids = new ObjectIdCollection();
        var missing = new List<string>();
        foreach (var n in names)
        {
            if (lt.Has(n)) ids.Add(lt[n]); else missing.Add(n);
        }
        if (missing.Count > 0)
            throw new ArgumentException($"No such layer(s): {string.Join(", ", missing)}.");
        return ids;
    }

    // ─────────── creation / deletion ───────────

    /// <summary>
    /// Rectangular viewport. This exists rather than reusing acad.layouts.create_viewport
    /// because that handler answers with {entity:...} while this category's contract is
    /// {viewport:...}. Pointing the backend at it produced a null handle and took nine
    /// downstream tools with it - a declared-vs-actual shape mismatch, the same class of
    /// defect the sweep spent its time removing. The layout switch/restore is kept.
    /// </summary>
    private static Task<ToolDispatchResult> CreateRect(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.create_viewport", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateRectViewportArgsDto>(args);
            if (a.Width <= 0 || a.Height <= 0) throw new ArgumentException("width and height must be > 0.");
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.LayoutName)) throw new ArgumentException($"Layout '{a.LayoutName}' does not exist.");

            var prev = lm.CurrentLayout;
            bool switched = !string.Equals(prev, a.LayoutName, StringComparison.OrdinalIgnoreCase);
            if (switched) lm.CurrentLayout = a.LayoutName;
            try
            {
                var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.LayoutName), OpenMode.ForRead);
                var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                var vp = new Viewport
                {
                    CenterPoint = AcadEnv.ToPoint3d(a.Center),
                    Width = a.Width,
                    Height = a.Height,
                };
                if (!string.IsNullOrWhiteSpace(a.Layer)) vp.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer!);
                paper.AppendEntity(vp);
                tr.AddNewlyCreatedDBObject(vp, true);
                if (a.Scale is > 0) vp.CustomScale = a.Scale.Value;
                try { vp.On = true; } catch { }
                return Wrap(new { viewport = Info(db, tr, vp) });
            }
            finally { if (switched) { try { lm.CurrentLayout = prev; } catch { } } }
        });

    private static Task<ToolDispatchResult> SetScale(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.set_viewport_scale", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpScaleArgsDto>(args);
            if (a.Scale <= 0) throw new ArgumentException("scale must be > 0.");
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            vp.CustomScale = a.Scale;
            return Wrap(new { viewport = Info(db, tr, vp) });
        });

    private static Task<ToolDispatchResult> CreatePolygonal(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.create_polygonal_viewport", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreatePolyViewportArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 3)
                throw new ArgumentException("polygonal viewport needs at least 3 vertices.");

            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.LayoutName)) throw new ArgumentException($"Layout '{a.LayoutName}' does not exist.");

            var prev = lm.CurrentLayout;
            bool switched = !string.Equals(prev, a.LayoutName, StringComparison.OrdinalIgnoreCase);
            if (switched) lm.CurrentLayout = a.LayoutName;
            try
            {
                var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.LayoutName), OpenMode.ForRead);
                var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

                // The clip outline is a real paper-space polyline; the viewport points at it.
                var pl = new Polyline { Closed = true };
                for (int i = 0; i < a.Vertices.Count; i++)
                    pl.AddVertexAt(i, new Point2d(a.Vertices[i].X, a.Vertices[i].Y), 0, 0, 0);
                paper.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);

                var ext = pl.GeometricExtents;
                var vp = new Viewport
                {
                    CenterPoint = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2,
                                              (ext.MinPoint.Y + ext.MaxPoint.Y) / 2, 0),
                    Width = ext.MaxPoint.X - ext.MinPoint.X,
                    Height = ext.MaxPoint.Y - ext.MinPoint.Y,
                };
                if (!string.IsNullOrWhiteSpace(a.Layer)) vp.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer!);
                paper.AppendEntity(vp);
                tr.AddNewlyCreatedDBObject(vp, true);

                vp.NonRectClipEntityId = pl.ObjectId;
                vp.NonRectClipOn = true;
                if (a.Scale is > 0) vp.CustomScale = a.Scale.Value;
                try { vp.On = true; } catch { }

                return Wrap(new { viewport = Info(db, tr, vp) });
            }
            finally { if (switched) { try { lm.CurrentLayout = prev; } catch { } } }
        });

    private static Task<ToolDispatchResult> DeleteViewport(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.delete_viewport", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpHandleArgsDto>(args);
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            if (!vp.NonRectClipEntityId.IsNull)
            {
                try
                {
                    var clip = (Entity)tr.GetObject(vp.NonRectClipEntityId, OpenMode.ForWrite);
                    clip.Erase();
                }
                catch { }
            }
            vp.Erase();
            return Wrap(new { affected = 1, handle = a.Handle });
        });

    // ─────────── inspection ───────────

    private static IEnumerable<Viewport> AllViewports(Database db, Transaction tr, string? layoutName)
    {
        var lm = LayoutManager.Current;
        var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        foreach (DBDictionaryEntry e in dict)
        {
            var layout = (Layout)tr.GetObject(e.Value, OpenMode.ForRead);
            if (string.Equals(layout.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(layoutName) &&
                !string.Equals(layout.LayoutName, layoutName, StringComparison.OrdinalIgnoreCase)) continue;

            var btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Viewport vp && vp.Number != 1)
                    yield return vp;
            }
        }
    }

    private static Task<ToolDispatchResult> ListViewports(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.list_viewports", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayoutNameArgsDto>(args);
            var list = AllViewports(db, tr, a.LayoutName).Select(vp => Info(db, tr, vp)).ToList();
            return Wrap(new { viewports = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> GetViewportInfo(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.get_viewport_info", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpHandleArgsDto>(args);
            return Wrap(new { viewport = Info(db, tr, OpenVp(db, tr, a.Handle, OpenMode.ForRead)) });
        });

    private static Task<ToolDispatchResult> GetExtentsInModel(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.get_viewport_extents_in_model", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpHandleArgsDto>(args);
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForRead);
            if (vp.CustomScale <= 0)
                throw new InvalidOperationException(
                    "Viewport has no usable scale, so the model area it covers cannot be derived. " +
                    "Set one with set_viewport_scale first.");

            // ViewCenter is in model coordinates; paper size / scale gives the model span.
            double wModel = vp.Width / vp.CustomScale;
            double hModel = vp.Height / vp.CustomScale;
            var c = vp.ViewCenter;
            return Wrap(new
            {
                handle = a.Handle,
                modelMin = new { x = c.X - wModel / 2, y = c.Y - hModel / 2 },
                modelMax = new { x = c.X + wModel / 2, y = c.Y + hModel / 2 },
                customScale = vp.CustomScale,
            });
        });

    // ─────────── properties ───────────

    private static Task<ToolDispatchResult> SetLock(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.set_viewport_lock", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpFlagArgsDto>(args);
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            vp.Locked = a.Locked;
            return Wrap(new { viewport = Info(db, tr, vp) });
        });

    private static Task<ToolDispatchResult> SetOnOff(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.set_viewport_on_off", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpFlagArgsDto>(args);
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            vp.On = a.Locked;   // wire field is "locked"; reused as the on/off flag
            return Wrap(new { viewport = Info(db, tr, vp) });
        });

    private static Task<ToolDispatchResult> SetShadePlot(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.set_viewport_shade_plot", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpShadeArgsDto>(args);
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            if (!Enum.TryParse<ShadePlotType>(a.Mode, ignoreCase: true, out var mode))
                throw new ArgumentException(
                    $"Unknown shade plot mode '{a.Mode}'. Use one of: " +
                    string.Join(", ", Enum.GetNames(typeof(ShadePlotType))) + ".");
            vp.ShadePlot = mode;
            return Wrap(new { viewport = Info(db, tr, vp) });
        });

    // ─────────── per-viewport layer state ───────────

    private static Task<ToolDispatchResult> FreezeThaw(JsonObject args, CancellationToken ct, bool freeze) =>
        Run(freeze ? "acad.viewports.set_viewport_layer_freeze" : "acad.viewports.set_viewport_layer_thaw",
            args, ct, (doc, db, tr) =>
        {
            var a = Read<VpLayersArgsDto>(args);
            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            var ids = ResolveLayers(db, tr, a.Layers);
            if (freeze) vp.FreezeLayersInViewport(ids.Cast<ObjectId>().GetEnumerator());
            else vp.ThawLayersInViewport(ids.Cast<ObjectId>().GetEnumerator());
            return Wrap(new { viewport = Info(db, tr, vp) });
        });

}
