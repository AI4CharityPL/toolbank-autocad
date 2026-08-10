// AutoCAD plugin handlers for the acad-views category.
// Registered under "acad.views.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED API shape, and it is why this category is not called acad-views-cameras:
//
//   There is NO Camera type in the managed API. In AutoCAD a camera IS a named view carrying a
//   target and a lens length, so set_camera_target and set_camera_lens act on a ViewTableRecord
//   and there is nothing else for create_camera or list_cameras to create or list.
//
//   ViewTableRecord has NO PerspectiveOn - the Viewport ENTITY does. So set_perspective_mode
//   works on a viewport, never on a stored view, and says so.
//
//   ViewTableRecord.Category does not exist; the view category is a Sheet Set concept.
//
//   Viewport.SetView and SetViewFromViewportTableRecord do not exist, so restoring a view into a
//   viewport is done by copying target, direction, height and twist across by hand.
//
//   UcsName is READ-ONLY: a view's UCS is set by id through SetUcs / SetUcsToWorld.

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
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class ViewsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.views.create_named_view",        CreateNamedView);
        host.Register("acad.views.create_view_from_window",  CreateViewFromWindow);
        host.Register("acad.views.list_named_views",         ListNamedViews);
        host.Register("acad.views.delete_named_view",        DeleteNamedView);
        host.Register("acad.views.restore_view_in_viewport", RestoreViewInViewport);
        host.Register("acad.views.set_view_ucs_association", SetViewUcsAssociation);
        host.Register("acad.views.set_camera_target",        SetCameraTarget);
        host.Register("acad.views.set_camera_lens",          SetCameraLens);
        host.Register("acad.views.set_perspective_mode",     SetPerspectiveMode);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static ViewTableRecord RequireView(Database db, Transaction tr, string? name, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name is required: which named view.");
        var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
        if (!vt.Has(name!))
            throw new ArgumentException(
                "No named view called '" + name + "'. list_named_views shows what is there.");
        return (ViewTableRecord)tr.GetObject(vt[name!], mode);
    }

    private static Viewport RequireViewport(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("viewportHandle is required.");
        var obj = tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (obj is Viewport vp) return vp;
        throw new ArgumentException(
            "Entity " + handle + " is a " + obj.GetRXClass().Name + ", not a viewport. " +
            "viewports.list_viewports finds them; a layout viewport is what this wants, not the " +
            "model-space window.");
    }

    private static object Describe(ViewTableRecord v) => new
    {
        name = v.Name,
        center = new { x = v.CenterPoint.X, y = v.CenterPoint.Y },
        width = v.Width,
        height = v.Height,
        target = AcadEnv.FromPoint3d(v.Target),
        viewDirection = AcadEnv.FromPoint3d(new Point3d(
            v.ViewDirection.X, v.ViewDirection.Y, v.ViewDirection.Z)),
        lensLength = v.LensLength,
        twist = v.ViewTwist,
        elevation = v.Elevation,
        frontClip = v.FrontClipEnabled ? v.FrontClipDistance : (double?)null,
        backClip = v.BackClipEnabled ? v.BackClipDistance : (double?)null,
        ucsAssociated = v.IsUcsAssociatedToView,
        handle = v.Handle.ToString(),
    };

    // ─────────── making and listing ───────────

    private static JsonObject MakeView(Database db, Transaction tr, ViewArgsDto a,
                                       Point2d centre, double width, double height, string how)
    {
        if (string.IsNullOrWhiteSpace(a.Name))
            throw new ArgumentException("name is required: what to call the view.");
        if (width <= 1e-9 || height <= 1e-9)
            throw new ArgumentException(
                "The view has zero width or height, so there would be nothing to look at.");

        var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForWrite);
        if (vt.Has(a.Name!))
            throw new ArgumentException(
                "A named view called '" + a.Name + "' already exists. Delete it first, or use a " +
                "different name - replacing one silently would lose whatever it pointed at.");

        var v = new ViewTableRecord
        {
            Name = a.Name!,
            CenterPoint = centre,
            Width = width,
            Height = height,
        };
        if (a.Target is not null) v.Target = AcadEnv.ToPoint3d(a.Target);
        if (a.ViewDirection is not null)
        {
            var d = AcadEnv.ToVector3d(a.ViewDirection);
            if (d.Length < 1e-12)
                throw new ArgumentException("viewDirection cannot be the zero vector.");
            v.ViewDirection = d;
        }
        if (a.LensLength is not null)
        {
            if (a.LensLength.Value <= 0)
                throw new ArgumentException("lensLength must be greater than zero.");
            v.LensLength = a.LensLength.Value;
        }
        if (a.Twist is not null) v.ViewTwist = a.Twist.Value;

        vt.Add(v);
        tr.AddNewlyCreatedDBObject(v, true);

        // Read back through a FRESH table lookup rather than trusting the object just added.
        var vt2 = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
        if (!vt2.Has(a.Name!))
            throw new InvalidOperationException(
                "The view was added but does not read back from the view table.");
        var back = (ViewTableRecord)tr.GetObject(vt2[a.Name!], OpenMode.ForRead);
        if (Math.Abs(back.Width - width) > 1e-6 || Math.Abs(back.Height - height) > 1e-6)
            throw new InvalidOperationException(
                "The view reads back " + back.Width + " by " + back.Height + " rather than the " +
                width + " by " + height + " it was given.");

        return Wrap(new
        {
            view = Describe(back),
            createdFrom = how,
            note = "A named view stores WHERE the camera is and what it looks at; it does not " +
                   "change the current display. restore_view_in_viewport puts it into a layout " +
                   "viewport. A view has a lens length and a target, which is all a CAMERA is in " +
                   "AutoCAD - there is no separate camera object in the API, so set_camera_lens " +
                   "and set_camera_target work on views like this one.",
        });
    }

    private static Task<ToolDispatchResult> CreateNamedView(JsonObject args, CancellationToken ct) =>
        Run("acad.views.create_named_view", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            if (a.Center is null || a.Width is null || a.Height is null)
                throw new ArgumentException(
                    "center, width and height are required. To make a view from two corners " +
                    "instead, use create_view_from_window.");
            return MakeView(db, tr, a,
                new Point2d(a.Center.X, a.Center.Y), a.Width.Value, a.Height.Value,
                "center and size");
        });

    private static Task<ToolDispatchResult> CreateViewFromWindow(JsonObject args, CancellationToken ct) =>
        Run("acad.views.create_view_from_window", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            if (a.Corner1 is null || a.Corner2 is null)
                throw new ArgumentException(
                    "corner1 and corner2 are required: the rectangle the view frames.");
            // Corners in any order - a window dragged right-to-left is still a window.
            double x1 = Math.Min(a.Corner1.X, a.Corner2.X), x2 = Math.Max(a.Corner1.X, a.Corner2.X);
            double y1 = Math.Min(a.Corner1.Y, a.Corner2.Y), y2 = Math.Max(a.Corner1.Y, a.Corner2.Y);
            return MakeView(db, tr, a,
                new Point2d((x1 + x2) / 2, (y1 + y2) / 2), x2 - x1, y2 - y1, "window corners");
        });

    private static Task<ToolDispatchResult> ListNamedViews(JsonObject args, CancellationToken ct) =>
        Run("acad.views.list_named_views", args, ct, (doc, db, tr) =>
        {
            var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
            var found = new List<object>();
            foreach (ObjectId id in vt)
            {
                if (id.IsErased) continue;
                found.Add(Describe((ViewTableRecord)tr.GetObject(id, OpenMode.ForRead)));
            }
            return Wrap(new
            {
                count = found.Count,
                views = found,
                note = "Every named view in the drawing. A fresh drawing has none - unlike the " +
                       "named objects dictionary, the view table starts empty, so a count of zero " +
                       "here is normal rather than a sign of anything wrong. `ucsAssociated` says " +
                       "whether the view carries its own UCS, and lensLength is what makes a view " +
                       "a camera.",
            });
        });

    private static Task<ToolDispatchResult> DeleteNamedView(JsonObject args, CancellationToken ct) =>
        Run("acad.views.delete_named_view", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            var v = RequireView(db, tr, a.Name, OpenMode.ForWrite);
            var name = v.Name;
            v.Erase();

            var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
            if (vt.Has(name))
                throw new InvalidOperationException(
                    "The view still reads back from the table after being erased.");

            return Wrap(new
            {
                name,
                deleted = true,
                note = "Erased and confirmed gone from the view table. A viewport that was showing " +
                       "this view keeps what it is displaying - restoring a view COPIES its " +
                       "settings across rather than leaving a reference behind, so deleting the " +
                       "view does not disturb any viewport.",
            });
        });

    // ─────────── cameras, which are views ───────────

    private static Task<ToolDispatchResult> SetCameraTarget(JsonObject args, CancellationToken ct) =>
        Run("acad.views.set_camera_target", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            if (a.Target is null)
                throw new ArgumentException("target is required: the point the view looks at.");
            var v = RequireView(db, tr, a.Name, OpenMode.ForWrite);

            var before = v.Target;
            var want = AcadEnv.ToPoint3d(a.Target);
            v.Target = want;

            var back = RequireView(db, tr, a.Name, OpenMode.ForRead).Target;
            if (back.DistanceTo(want) > 1e-6)
                throw new InvalidOperationException(
                    "The target reads back as " + back + " rather than the " + want + " it was set to.");

            return Wrap(new
            {
                name = a.Name,
                targetBefore = AcadEnv.FromPoint3d(before),
                target = AcadEnv.FromPoint3d(back),
                note = "The target is what the view LOOKS AT; the view direction is the way it " +
                       "looks from there. Together with lensLength that is everything AutoCAD " +
                       "means by a camera - there is no Camera object in the API. Read back after " +
                       "writing.",
            });
        });

    private static Task<ToolDispatchResult> SetCameraLens(JsonObject args, CancellationToken ct) =>
        Run("acad.views.set_camera_lens", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            if (a.LensLength is null)
                throw new ArgumentException("lensLength is required, in millimetres.");
            if (a.LensLength.Value <= 0)
                throw new ArgumentException("lensLength must be greater than zero.");
            var v = RequireView(db, tr, a.Name, OpenMode.ForWrite);

            var before = v.LensLength;
            v.LensLength = a.LensLength.Value;
            var back = RequireView(db, tr, a.Name, OpenMode.ForRead).LensLength;
            if (Math.Abs(back - a.LensLength.Value) > 1e-9)
                throw new InvalidOperationException(
                    "The lens reads back as " + back + " rather than " + a.LensLength.Value + ".");

            return Wrap(new
            {
                name = a.Name,
                lensLengthBefore = before,
                lensLength = back,
                note = "In millimetres, on the 35 mm convention: 50 is normal, below about 35 is " +
                       "wide angle and above 85 is telephoto. The lens only shows in a PERSPECTIVE " +
                       "view - set_perspective_mode turns that on for a viewport - so a changed " +
                       "lens on a parallel view is stored faithfully and changes nothing you can " +
                       "see, which is why this reports the previous value rather than claiming a " +
                       "visible effect.",
            });
        });

    // ─────────── viewports ───────────

    private static Task<ToolDispatchResult> RestoreViewInViewport(JsonObject args, CancellationToken ct) =>
        Run("acad.views.restore_view_in_viewport", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            var v = RequireView(db, tr, a.Name, OpenMode.ForRead);
            var vp = RequireViewport(db, tr, a.ViewportHandle, OpenMode.ForWrite);

            // Viewport.SetView does not exist, so the settings are copied across by hand. That
            // also means the viewport keeps NO reference to the view: deleting the view later
            // leaves the viewport exactly as it is.
            var beforeHeight = vp.ViewHeight;
            vp.ViewTarget = v.Target;
            vp.ViewDirection = v.ViewDirection;
            vp.ViewHeight = v.Height;
            vp.TwistAngle = v.ViewTwist;
            if (v.LensLength > 0) vp.LensLength = v.LensLength;

            if (Math.Abs(vp.ViewHeight - v.Height) > 1e-6)
                throw new InvalidOperationException(
                    "The viewport height reads back as " + vp.ViewHeight + " rather than the " +
                    v.Height + " the view specifies.");

            return Wrap(new
            {
                name = a.Name,
                viewportHandle = a.ViewportHandle,
                viewHeightBefore = beforeHeight,
                viewHeight = vp.ViewHeight,
                target = AcadEnv.FromPoint3d(vp.ViewTarget),
                twist = vp.TwistAngle,
                note = "Viewport.SetView does not exist in the managed API, so the view's target, " +
                       "direction, height and twist are COPIED across. The consequence worth " +
                       "knowing: the viewport keeps no reference to the view, so changing or " +
                       "deleting the view afterwards does not alter the viewport - restore it " +
                       "again to pick up a change.",
            });
        });

    private static Task<ToolDispatchResult> SetPerspectiveMode(JsonObject args, CancellationToken ct) =>
        Run("acad.views.set_perspective_mode", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            if (a.Enabled is null)
                throw new ArgumentException("enabled is required: true or false.");
            var vp = RequireViewport(db, tr, a.ViewportHandle, OpenMode.ForWrite);

            var before = vp.PerspectiveOn;
            if (before == a.Enabled.Value)
                throw new ArgumentException(
                    "Perspective is already " + (before ? "on" : "off") + " for this viewport, so " +
                    "nothing would change.");

            vp.PerspectiveOn = a.Enabled.Value;
            if (vp.PerspectiveOn != a.Enabled.Value)
                throw new InvalidOperationException(
                    "Perspective reads back as " + vp.PerspectiveOn + " after being set to " +
                    a.Enabled.Value + ".");

            return Wrap(new
            {
                viewportHandle = a.ViewportHandle,
                perspectiveBefore = before,
                perspective = vp.PerspectiveOn,
                lensLength = vp.LensLength,
                note = "Perspective belongs to the VIEWPORT, not to a stored view - " +
                       "ViewTableRecord has no PerspectiveOn at all, which is measured rather than " +
                       "assumed. That is why this takes a viewport handle and not a view name. " +
                       "The lens length above is what perspective will use; it means nothing while " +
                       "perspective is off.",
            });
        });

    private static Task<ToolDispatchResult> SetViewUcsAssociation(JsonObject args, CancellationToken ct) =>
        Run("acad.views.set_view_ucs_association", args, ct, (doc, db, tr) =>
        {
            var a = Read<ViewArgsDto>(args);
            var v = RequireView(db, tr, a.Name, OpenMode.ForWrite);
            var before = v.IsUcsAssociatedToView;

            string how;
            if (string.IsNullOrWhiteSpace(a.UcsName) || a.UcsName!.Equals("world", StringComparison.OrdinalIgnoreCase))
            {
                v.SetUcsToWorld();
                how = "world";
            }
            else
            {
                var ut = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
                if (!ut.Has(a.UcsName!))
                    throw new ArgumentException(
                        "No UCS called '" + a.UcsName + "'. ucs.list_ucs shows what is defined. " +
                        "Pass 'world' to associate the world coordinate system instead.");
                // UcsName is READ-ONLY: the association is made by id, never by name.
                v.SetUcs(ut[a.UcsName!]);
                how = a.UcsName!;
            }

            var back = RequireView(db, tr, a.Name, OpenMode.ForRead);
            if (!back.IsUcsAssociatedToView)
                throw new InvalidOperationException(
                    "The view does not read back as having a UCS associated.");

            return Wrap(new
            {
                name = a.Name,
                ucsAssociatedBefore = before,
                ucsAssociated = back.IsUcsAssociatedToView,
                ucs = how,
                note = "Associating a UCS means the drawing plane follows when the view is " +
                       "restored, which is what makes a working view usable for drawing rather " +
                       "than only for looking. ViewTableRecord.UcsName is READ-ONLY, so the " +
                       "association is made by object id through SetUcs, or SetUcsToWorld for the " +
                       "world system; IsUcsAssociatedToView is what reads it back.",
            });
        });
}
