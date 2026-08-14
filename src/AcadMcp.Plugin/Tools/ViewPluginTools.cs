// AutoCAD plugin handlers for the acad-view category.
// Registered under "acad.view.<verb>"; all runs on the UI thread (rule 10).
//
// Viewport manipulation uses the managed API where possible (ViewTableRecord +
// Editor.SetCurrentView) and falls back to Editor.Command("_.ZOOM", ...) for
// ZOOM_EXTENTS / ZOOM_ALL where no clean managed equivalent exists. All ZOOM
// command tokens are _-prefixed (locale-independent) per rule 15.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 15 (SendCommand discipline),
// 19 (impl pattern).

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
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class ViewPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.view.zoom_window",      ZoomWindow);
        host.Register("acad.view.zoom_extents",     ZoomExtents);
        host.Register("acad.view.zoom_all",         ZoomAll);
        host.Register("acad.view.zoom_center",      ZoomCenter);
        host.Register("acad.view.zoom_scale",       ZoomScale);
        host.Register("acad.view.list_views",       ListViews);
        host.Register("acad.view.set_current_view", SetCurrentView);
        host.Register("acad.view.get_current_view", GetCurrentView);
    }

    // ─────────── infra ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    /// Run a view op on the UI thread. Does NOT acquire DocumentLock (view ops are
    /// non-destructive to the db) and does NOT open a transaction unless the caller
    /// opens its own. Matches the "RunEditorCommand" pattern in ParametricPluginTools.
    private static async Task<ToolDispatchResult> RunUi(string toolKey, CancellationToken ct,
        Func<Document, Database, JsonObject> work)
    {
        try
        {
            var doc = Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document open in AutoCAD.");
            var json = await UiThreadDispatcher.Run(() => work(doc, doc.Database), ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (OperationCanceledException)
        {
            return new ToolDispatchResult(false, null,
                new ErrorInfo(AcadErrorCode.Timeout, $"Tool '{toolKey}' was cancelled."));
        }
        catch (Exception ex) { return AcadErrorMapper.Fail(toolKey, ex); }
    }

    // ─────────── zoom tools ───────────

    private static Task<ToolDispatchResult> ZoomWindow(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.zoom_window", ct, (doc, db) =>
        {
            var a = Read<ZoomWindowArgsDto>(args);
            if (a.Corner1 is null || a.Corner2 is null)
                throw new ArgumentException("zoom_window requires both corner1 and corner2.");

            // Normalise corners into a well-formed rectangle.
            double xMin = Math.Min(a.Corner1.X, a.Corner2.X);
            double xMax = Math.Max(a.Corner1.X, a.Corner2.X);
            double yMin = Math.Min(a.Corner1.Y, a.Corner2.Y);
            double yMax = Math.Max(a.Corner1.Y, a.Corner2.Y);

            if (xMax - xMin <= double.Epsilon || yMax - yMin <= double.Epsilon)
                throw new ArgumentException("zoom_window corners produce a degenerate rectangle (zero width or height).");

            // Managed-API approach: construct a ViewTableRecord that tightly frames the rectangle.
            // Center + Height + Width lets Editor.SetCurrentView restore the view synchronously.
            using var vtr = new ViewTableRecord
            {
                CenterPoint = new Point2d((xMin + xMax) / 2.0, (yMin + yMax) / 2.0),
                Width       = xMax - xMin,
                Height      = yMax - yMin,
            };
            ConfigureViewForCurrentSpace(doc, db, vtr);
            doc.Editor.SetCurrentView(vtr);

            return Wrap(new { affected = 1 });
        });

    /// <summary>
    /// Editor.SetCurrentView throws a native eNullObjectPointer whenever a paperspace layout
    /// is the active tab and the caller is not "inside" a floating viewport - confirmed live
    /// (2026-08-14): every zoom_* tool fails this way in paperspace, the identical calls
    /// succeed fine in model space.
    ///
    /// First hypothesis (CVPORT left stale by LayoutManager.CurrentLayout's own programmatic
    /// switch) was tested live and did NOT fix it alone. Cross-checked against 2 independent
    /// Autodesk Community threads: a freshly constructed ViewTableRecord defaults
    /// IsPaperspaceView to false, and SetCurrentView needs it explicitly true whenever the
    /// current viewport IS the paperspace background - without it, it tries to resolve
    /// model-space-only internal state that does not exist there and null-pointers. CVPORT is
    /// still synced first (harmless, addresses the same underlying "which viewport is this"
    /// ambiguity from a different angle), then IsPaperspaceView is computed properly from
    /// Editor.CurrentViewportObjectId matching Database.PaperSpaceVportId - true whenever on
    /// the paperspace background, false if a caller had somehow entered a floating viewport
    /// first (this bank's own tools never do, but the check holds correctly either way).
    /// </summary>
    private static void ConfigureViewForCurrentSpace(Document doc, Database db, ViewTableRecord vtr)
    {
        if (db.TileMode)
        {
            vtr.IsPaperspaceView = false;
            return;
        }
        try { Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("CVPORT", 1); }
        catch (Autodesk.AutoCAD.Runtime.Exception) { /* best-effort */ }
        try
        {
            vtr.IsPaperspaceView = db.PaperSpaceVportId == doc.Editor.CurrentViewportObjectId;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            vtr.IsPaperspaceView = true; // on the paperspace background is this bank's only real usage pattern
        }
    }

    /// <summary>
    /// Frame a model-space rectangle, with a small margin so geometry is not flush against
    /// the viewport edge (what ZOOM _E does implicitly).
    /// </summary>
    private static JsonObject SetViewToRect(Document doc, double xMin, double yMin, double xMax, double yMax)
    {
        double w = xMax - xMin, h = yMax - yMin;

        // A single point, or a drawing containing one zero-length entity, has no extent to
        // frame. Give it an arbitrary but sane window rather than throwing.
        if (w <= 1e-9 && h <= 1e-9) { w = h = 100.0; }
        else if (w <= 1e-9) { w = h; }
        else if (h <= 1e-9) { h = w; }

        double cx = (xMin + xMax) / 2.0, cy = (yMin + yMax) / 2.0;
        const double margin = 1.04;

        using var vtr = new ViewTableRecord
        {
            CenterPoint = new Point2d(cx, cy),
            Width = w * margin,
            Height = h * margin,
        };
        ConfigureViewForCurrentSpace(doc, doc.Database, vtr);
        doc.Editor.SetCurrentView(vtr);
        return Wrap(new
        {
            affected = 1,
            window = new { xMin, yMin, xMax, yMax },
        });
    }

    /// <summary>
    /// True model-space extents. Database.Extmin/Extmax go stale after edits and are
    /// inverted on an empty drawing, so refresh first and sanity-check the result.
    /// </summary>
    private static bool TryGetDrawingExtents(Database db, out double xMin, out double yMin, out double xMax, out double yMax)
    {
        xMin = yMin = xMax = yMax = 0;
        try { db.UpdateExt(true); } catch { /* nothing drawable yet */ }

        var lo = db.Extmin;
        var hi = db.Extmax;
        if (lo.X > hi.X || lo.Y > hi.Y) return false;   // the empty-drawing sentinel
        xMin = lo.X; yMin = lo.Y; xMax = hi.X; yMax = hi.Y;
        return true;
    }

    private static Task<ToolDispatchResult> ZoomExtents(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.zoom_extents", ct, (doc, db) =>
        {
            // Was RunAcadCommand(doc, "_.ZOOM", "_E"). That failed with eInvalidInput on every
            // call from a pipe dispatch - the same failure mode rule 15 and rule 26 §9 warn
            // about for the command layer, and the one that took out the parametric tools.
            // zoom_center / zoom_window already prove the managed route works, so use it here
            // too: refresh the extents, then frame them with a ViewTableRecord.
            if (!TryGetDrawingExtents(db, out var xMin, out var yMin, out var xMax, out var yMax))
                throw new InvalidOperationException(
                    "Drawing has no extents to zoom to (it contains no drawable geometry).");

            return SetViewToRect(doc, xMin, yMin, xMax, yMax);
        });

    private static Task<ToolDispatchResult> ZoomAll(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.zoom_all", ct, (doc, db) =>
        {
            // ZOOM _A frames the drawing limits, or the extents when geometry spills past them.
            bool hasExt = TryGetDrawingExtents(db, out var xMin, out var yMin, out var xMax, out var yMax);

            var lmin = db.Limmin;
            var lmax = db.Limmax;
            bool hasLimits = lmax.X > lmin.X && lmax.Y > lmin.Y;

            if (hasLimits && hasExt)
            {
                xMin = Math.Min(xMin, lmin.X); yMin = Math.Min(yMin, lmin.Y);
                xMax = Math.Max(xMax, lmax.X); yMax = Math.Max(yMax, lmax.Y);
            }
            else if (hasLimits)
            {
                xMin = lmin.X; yMin = lmin.Y; xMax = lmax.X; yMax = lmax.Y;
            }
            else if (!hasExt)
            {
                throw new InvalidOperationException(
                    "Drawing has neither limits nor extents to zoom to.");
            }

            return SetViewToRect(doc, xMin, yMin, xMax, yMax);
        });

    private static Task<ToolDispatchResult> ZoomCenter(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.zoom_center", ct, (doc, db) =>
        {
            var a = Read<ZoomCenterArgsDto>(args);
            if (a.Center is null) throw new ArgumentException("zoom_center requires center point.");
            if (a.Height <= 0) throw new ArgumentException("zoom_center height must be > 0.");

            using var vtr = new ViewTableRecord
            {
                CenterPoint = new Point2d(a.Center.X, a.Center.Y),
                Height      = a.Height,
                Width       = a.Height, // will be rescaled by viewport aspect on set
            };
            ConfigureViewForCurrentSpace(doc, db, vtr);
            doc.Editor.SetCurrentView(vtr);
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> ZoomScale(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.zoom_scale", ct, (doc, db) =>
        {
            var a = Read<ZoomScaleArgsDto>(args);
            if (a.Scale <= 0) throw new ArgumentException("zoom_scale must be > 0.");

            using var current = doc.Editor.GetCurrentView();
            using var vtr = new ViewTableRecord
            {
                CenterPoint = current.CenterPoint,
                Width       = current.Width  / a.Scale,
                Height      = current.Height / a.Scale,
            };
            ConfigureViewForCurrentSpace(doc, db, vtr);
            doc.Editor.SetCurrentView(vtr);
            return Wrap(new { affected = 1 });
        });

    // ─────────── named-view tools ───────────

    private static Task<ToolDispatchResult> ListViews(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.list_views", ct, (doc, db) =>
        {
            var views = new List<object>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
                foreach (ObjectId id in vt)
                {
                    if (id.IsErased) continue;
                    var v = (ViewTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    views.Add(new
                    {
                        name         = v.Name,
                        centerX      = v.CenterPoint.X,
                        centerY      = v.CenterPoint.Y,
                        width        = v.Width,
                        height       = v.Height,
                        isPaperSpace = v.IsPaperspaceView,
                    });
                }
                tr.Commit();
            }
            return Wrap(new { views });
        });

    private static Task<ToolDispatchResult> SetCurrentView(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.set_current_view", ct, (doc, db) =>
        {
            var a = Read<SetCurrentViewArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");

            ViewTableRecord? snapshot = null;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
                if (!vt.Has(a.Name))
                    throw new ArgumentException($"Named view '{a.Name}' not found in VIEW table.");
                var src = (ViewTableRecord)tr.GetObject(vt[a.Name], OpenMode.ForRead);
                snapshot = new ViewTableRecord
                {
                    CenterPoint = src.CenterPoint,
                    Width       = src.Width,
                    Height      = src.Height,
                    ViewDirection = src.ViewDirection,
                    Target      = src.Target,
                };
                tr.Commit();
            }
            using (snapshot)
            {
                doc.Editor.SetCurrentView(snapshot!);
            }
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> GetCurrentView(JsonObject args, CancellationToken ct) =>
        RunUi("acad.view.get_current_view", ct, (doc, db) =>
        {
            using var v = doc.Editor.GetCurrentView();
            return Wrap(new
            {
                view = new
                {
                    name         = string.IsNullOrEmpty(v.Name) ? "*CURRENT" : v.Name,
                    centerX      = v.CenterPoint.X,
                    centerY      = v.CenterPoint.Y,
                    width        = v.Width,
                    height       = v.Height,
                    isPaperSpace = v.IsPaperspaceView,
                }
            });
        });

    // ─────────── helpers ───────────

    /// <summary>Run an AutoCAD command synchronously with CMDECHO off.
    /// Always locale-prefix command names (<c>_.ZOOM</c>) per rule 15 §1.</summary>
    private static void RunAcadCommand(Document doc, params object[] tokens)
    {
        // CMDECHO off → no command-line spam during automated visual review loops.
        short prevCmdEcho = (short)Application.GetSystemVariable("CMDECHO");
        try
        {
            if (prevCmdEcho != 0) Application.SetSystemVariable("CMDECHO", (short)0);
            doc.Editor.Command(tokens);
        }
        finally
        {
            if (prevCmdEcho != 0) Application.SetSystemVariable("CMDECHO", prevCmdEcho);
        }
    }
}
