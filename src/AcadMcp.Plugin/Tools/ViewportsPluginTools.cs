// AutoCAD plugin handlers for the acad-viewports category.
//
// create_viewport and set_viewport_scale ARE here. They were briefly pointed at the
// acad.layouts.* handlers to avoid duplication; those answer {entity:...} while this
// category's contract is {viewport:...}, so the handle came back null and nine downstream
// tools failed on it. Sharing an implementation is only free when the contracts match.
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
        host.Register("acad.viewports.set_viewport_ucs", SetUcs);
        host.Register("acad.viewports.set_viewport_annotation_scale", SetAnnotationScale);
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
        // No Number-based guard here, deliberately. Viewport.Number is assigned when AutoCAD
        // activates and regenerates a layout, so a viewport that was just created reads 1 or
        // -1 until then - exactly like the layout's own paper-space pseudo-viewport. Using it
        // to tell the two apart rejected every freshly created viewport, which is to say every
        // viewport an agent makes. Number is reported in the info payload so a caller can see
        // it; it is not used to refuse work.
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
        ucs = UcsInfo(tr, vp),
        annotationScale = vp.AnnotationScale?.Name,
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

    // ─────────── per-viewport UCS and annotation scale (rule 43) ───────────
    //
    // Both were specced when acad-viewports was built and withheld: the first waited on
    // acad-ucs, the second on acad-annotative. Both categories now exist and are verified, so
    // these stop being guesses about how a UCS or a scale is identified and start being
    // ordinary lookups against a table the caller can list.

    private static Task<ToolDispatchResult> SetUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.set_viewport_ucs", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpUcsArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Ucs))
                throw new ArgumentException("ucs is required. Pass a saved UCS name, or 'world' to clear the viewport's UCS.");

            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);

            if (string.Equals(a.Ucs, "world", StringComparison.OrdinalIgnoreCase))
            {
                vp.SetUcsToWorld();
            }
            else
            {
                var table = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
                if (!table.Has(a.Ucs))
                {
                    var known = new List<string>();
                    foreach (ObjectId id in table)
                        known.Add(((UcsTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
                    known.Sort(StringComparer.OrdinalIgnoreCase);
                    throw new ArgumentException(
                        $"No UCS named '{a.Ucs}'. Saved: " + (known.Count == 0 ? "(none)" : string.Join(", ", known)) +
                        ". Use ucs.list_ucs, or pass 'world'.");
                }
                vp.SetUcs(table[a.Ucs]);
            }

            // Not optional and not exposed as an argument. Without UcsPerViewport AutoCAD does
            // not store the UCS against this viewport: the setting applies until the next
            // layout switch and then quietly reverts, having reported success. Rule 43.
            vp.UcsPerViewport = true;

            return Wrap(new { viewport = Info(db, tr, vp), ucs = UcsInfo(tr, vp) });
        });

    private static Task<ToolDispatchResult> SetAnnotationScale(JsonObject args, CancellationToken ct) =>
        Run("acad.viewports.set_viewport_annotation_scale", args, ct, (doc, db, tr) =>
        {
            var a = Read<VpAnnotationScaleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.ScaleName))
                throw new ArgumentException("scaleName is required, e.g. '1:50'.");

            var coll = db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");
            if (coll is null)
                throw new InvalidOperationException("This drawing has no annotation scale collection.");

            if (coll.GetContext(a.ScaleName) is not AnnotationScale scale)
            {
                var known = coll.Cast<ObjectContext>().OfType<AnnotationScale>()
                    .Select(s => s.Name)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                throw new ArgumentException(
                    $"No annotation scale named '{a.ScaleName}' in this drawing. Available: " +
                    (known.Count == 0 ? "(none)" : string.Join(", ", known)) +
                    ". Use annotative.list_annotation_scales, or annotative.add_scale_to_list to add one.");
            }

            var vp = OpenVp(db, tr, a.Handle, OpenMode.ForWrite);
            vp.AnnotationScale = scale;

            // AutoCAD's own UI keeps the annotation scale and the view scale linked, and a
            // viewport where they disagree means text sized for one scale on a window drawn at
            // another. Sync by default; syncViewScale:false is for the deliberate case.
            double? appliedViewScale = null;
            if (a.SyncViewScale && scale.DrawingUnits > 0)
            {
                vp.CustomScale = scale.PaperUnits / scale.DrawingUnits;
                appliedViewScale = vp.CustomScale;
            }

            return Wrap(new
            {
                viewport = Info(db, tr, vp),
                annotationScale = new
                {
                    name = scale.Name,
                    paperUnits = scale.PaperUnits,
                    drawingUnits = scale.DrawingUnits,
                    scaleFactor = scale.DrawingUnits > 0 ? scale.PaperUnits / scale.DrawingUnits : 0.0,
                },
                viewScaleSynced = a.SyncViewScale,
                appliedViewScale,
            });
        });

    /// <summary>
    /// The viewport's own UCS, reported so a caller can verify what a set actually produced
    /// rather than trusting the return code.
    /// </summary>
    private static object UcsInfo(Transaction tr, Viewport vp)
    {
        string name;
        if (vp.UcsName.IsNull)
        {
            name = "world";
        }
        else
        {
            try { name = ((UcsTableRecord)tr.GetObject(vp.UcsName, OpenMode.ForRead)).Name; }
            catch (Autodesk.AutoCAD.Runtime.Exception) { name = "(unnamed)"; }
        }

        // GetUcs takes ref, not out - the arguments have to exist before the call.
        var origin = Point3d.Origin;
        var xAxis = Vector3d.XAxis;
        var yAxis = Vector3d.YAxis;
        vp.GetUcs(ref origin, ref xAxis, ref yAxis);
        return new
        {
            name,
            perViewport = vp.UcsPerViewport,
            origin = new { x = origin.X, y = origin.Y, z = origin.Z },
            xAxis = new { x = xAxis.X, y = xAxis.Y, z = xAxis.Z },
            yAxis = new { x = yAxis.X, y = yAxis.Y, z = yAxis.Z },
        };
    }

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
                // Not filtered on Number: it is unassigned (1 or -1) until AutoCAD activates
                // the layout, so filtering on it hides every viewport an agent just created -
                // the same unreliable signal that had to come out of OpenVp. The layout's own
                // pseudo-viewport is instead identified structurally: it is the first Viewport
                // in the block table record and the only one with no width.
                if (tr.GetObject(id, OpenMode.ForRead) is Viewport vp && vp.Width > 0)
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
