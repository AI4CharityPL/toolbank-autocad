// AutoCAD plugin handlers for the acad-geometry-2d category.
// Each handler is registered under "acad.geometry2d.<verb>" and ALWAYS runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern).

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
using AcadDb = Autodesk.AutoCAD.DatabaseServices;
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class Geometry2dPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.geometry2d.draw_line", DrawLine);
        host.Register("acad.geometry2d.draw_polyline", DrawPolyline);
        host.Register("acad.geometry2d.draw_mline", DrawMline);
        host.Register("acad.geometry2d.draw_circle", DrawCircle);
        host.Register("acad.geometry2d.draw_arc", DrawArc);
        host.Register("acad.geometry2d.draw_ellipse", DrawEllipse);
        host.Register("acad.geometry2d.draw_rectangle", DrawRectangle);
        host.Register("acad.geometry2d.draw_polygon", DrawPolygon);
        host.Register("acad.geometry2d.draw_spline", DrawSpline);
        host.Register("acad.geometry2d.draw_point", DrawPoint);
        host.Register("acad.geometry2d.draw_donut", DrawDonut);
        host.Register("acad.geometry2d.draw_xline", DrawXLine);
        host.Register("acad.geometry2d.draw_ray", DrawRay);
        host.Register("acad.geometry2d.draw_text", DrawText);
        host.Register("acad.geometry2d.draw_mtext", DrawMText);
        host.Register("acad.geometry2d.draw_hatch", DrawHatch);
        host.Register("acad.geometry2d.draw_revcloud", DrawRevcloud);

        host.Register("acad.geometry2d.get_entity", GetEntity);
        host.Register("acad.geometry2d.list_entities_in_window", ListEntitiesInWindow);
        host.Register("acad.geometry2d.get_curve_length", GetCurveLength);
        host.Register("acad.geometry2d.get_area", GetArea);
        host.Register("acad.geometry2d.get_bounding_box", GetBoundingBox);
        host.Register("acad.geometry2d.get_intersections", GetIntersections);
        host.Register("acad.geometry2d.get_distance_points", GetDistancePoints);
        host.Register("acad.geometry2d.get_distance_to_entity", GetDistanceToEntity);

        host.Register("acad.geometry2d.offset_curve", OffsetCurve);
        host.Register("acad.geometry2d.trim_curve", TrimCurve);
        host.Register("acad.geometry2d.extend_curve", ExtendCurve);
        host.Register("acad.geometry2d.join_curves", JoinCurves);
        host.Register("acad.geometry2d.explode_entity", ExplodeEntity);
        host.Register("acad.geometry2d.fillet_corner", FilletCorner);
        host.Register("acad.geometry2d.chamfer_corner", ChamferCorner);
        host.Register("acad.geometry2d.delete_entities", DeleteEntities);

        // roadmap 3.1 - polyline vertex editing
        host.Register("acad.geometry2d.list_polyline_vertices", ListPolylineVertices);
        host.Register("acad.geometry2d.polyline_add_vertex", PolylineAddVertex);
        host.Register("acad.geometry2d.polyline_remove_vertex", PolylineRemoveVertex);
        host.Register("acad.geometry2d.edit_polyline_vertex", EditPolylineVertex);
        host.Register("acad.geometry2d.set_polyline_width", SetPolylineWidth);
        host.Register("acad.geometry2d.reverse_curve", ReverseCurve);

        // roadmap 3.1 - breaking and dividing
        host.Register("acad.geometry2d.break_at_point", BreakAtPoint);
        host.Register("acad.geometry2d.break_between_points", BreakBetweenPoints);
        host.Register("acad.geometry2d.divide_object", DivideObject);
        host.Register("acad.geometry2d.measure_object", MeasureObject);
        host.Register("acad.geometry2d.set_point_style", SetPointStyle);

        // roadmap 3.1 - display order, transparency, wipeouts
        host.Register("acad.geometry2d.set_draworder", SetDrawOrder);
        host.Register("acad.geometry2d.set_object_transparency", SetObjectTransparency);
        host.Register("acad.geometry2d.create_wipeout", CreateWipeout);
        host.Register("acad.geometry2d.set_wipeout_frame", SetWipeoutFrame);

        // roadmap 3.1 - splines
        host.Register("acad.geometry2d.draw_spline_cv", DrawSplineCv);
        host.Register("acad.geometry2d.edit_spline_fit_point", EditSplineFitPoint);
        host.Register("acad.geometry2d.spline_to_polyline", SplineToPolyline);
    }

    // ─────────── helpers ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── creation handlers ───────────

    private static Task<ToolDispatchResult> DrawLine(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_line", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawLineArgsDto>(args);
            var ent = new Line(AcadEnv.ToPoint3d(a.Start), AcadEnv.ToPoint3d(a.End));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, ent, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawPolyline(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_polyline", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawPolylineArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 2)
                throw new ArgumentException("polyline needs >= 2 vertices");
            var pl = new Polyline { Closed = a.Closed };
            for (int i = 0; i < a.Vertices.Count; i++)
                pl.AddVertexAt(i, AcadEnv.ToPoint2d(a.Vertices[i]), 0, a.GlobalWidth ?? 0, a.GlobalWidth ?? 0);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, pl, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawMline(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_mline", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawMlineArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 2)
                throw new ArgumentException("mline needs >= 2 vertices");

            var ml = new Mline();

            // Style FIRST. Mline reads the style's element offsets when segments are appended, so
            // appending before the style is set produces an entity drawn with STANDARD's single
            // element regardless of what is assigned afterwards - a wall that returns success and
            // draws one line.
            var dict = (DBDictionary)tr.GetObject(db.MLStyleDictionaryId, OpenMode.ForRead);
            if (!string.IsNullOrWhiteSpace(a.Style))
            {
                if (!dict.Contains(a.Style))
                    throw new ArgumentException(
                        "No multiline style named '" + a.Style + "'. Use list_mlinestyles, or create " +
                        "one with create_mlinestyle.");
                ml.Style = dict.GetAt(a.Style);
            }
            else
            {
                ml.Style = db.CmlstyleID;
            }

            ml.Normal = Vector3d.ZAxis;
            ml.Scale = a.Scale ?? 1.0;
            ml.Justification = (a.Justification ?? "zero").Trim().ToLowerInvariant() switch
            {
                "top" => MlineJustification.Top,
                "zero" or "center" or "centre" => MlineJustification.Zero,
                "bottom" => MlineJustification.Bottom,
                _ => throw new ArgumentException(
                    "justification must be 'top', 'zero' or 'bottom'; got '" + a.Justification + "'."),
            };

            foreach (var v in a.Vertices) ml.AppendSegment(AcadEnv.ToPoint3d(v));
            if (a.Closed) ml.IsClosed = true;

            return Wrap(new { entity = AcadEnv.Persist(db, tr, ml, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawCircle(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_circle", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawCircleArgsDto>(args);
            if (a.Radius <= 0) throw new ArgumentException("radius must be > 0");
            var c = new Circle(AcadEnv.ToPoint3d(a.Center), Vector3d.ZAxis, a.Radius);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, c, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawArc(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_arc", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawArcArgsDto>(args);
            if (a.Radius <= 0) throw new ArgumentException("radius must be > 0");
            var arc = new Arc(AcadEnv.ToPoint3d(a.Center), a.Radius,
                a.StartAngleDeg * Math.PI / 180.0, a.EndAngleDeg * Math.PI / 180.0);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, arc, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawEllipse(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_ellipse", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawEllipseArgsDto>(args);
            if (a.Ratio <= 0 || a.Ratio > 1) throw new ArgumentException("ratio must be in (0, 1]");
            var center = AcadEnv.ToPoint3d(a.Center);
            var major = AcadEnv.ToPoint3d(a.MajorAxis) - center;
            var ell = new Ellipse(center, Vector3d.ZAxis, major, a.Ratio, 0, 2 * Math.PI);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, ell, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawRectangle(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_rectangle", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawRectangleArgsDto>(args);
            var p1 = a.Corner1; var p2 = a.Corner2;
            double xMin = Math.Min(p1.X, p2.X), xMax = Math.Max(p1.X, p2.X);
            double yMin = Math.Min(p1.Y, p2.Y), yMax = Math.Max(p1.Y, p2.Y);
            var pl = new Polyline { Closed = true };
            pl.AddVertexAt(0, new Point2d(xMin, yMin), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(xMax, yMin), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(xMax, yMax), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(xMin, yMax), 0, 0, 0);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, pl, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawPolygon(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_polygon", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawPolygonArgsDto>(args);
            if (a.Sides < 3 || a.Sides > 1024) throw new ArgumentException("sides must be 3..1024");
            if (a.Radius <= 0) throw new ArgumentException("radius must be > 0");
            var pl = new Polyline { Closed = true };
            double r = a.Inscribed ? a.Radius : a.Radius / Math.Cos(Math.PI / a.Sides);
            for (int i = 0; i < a.Sides; i++)
            {
                double t = 2.0 * Math.PI * i / a.Sides + Math.PI / 2.0;
                pl.AddVertexAt(i, new Point2d(a.Center.X + r * Math.Cos(t), a.Center.Y + r * Math.Sin(t)), 0, 0, 0);
            }
            return Wrap(new { entity = AcadEnv.Persist(db, tr, pl, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawSpline(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_spline", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawSplineArgsDto>(args);
            if (a.FitPoints is null || a.FitPoints.Count < 2)
                throw new ArgumentException("spline needs >= 2 fit points");
            var pts = new Point3dCollection();
            foreach (var p in a.FitPoints) pts.Add(AcadEnv.ToPoint3d(p));
            if (a.Closed && a.FitPoints.Count > 0)
            {
                var first = AcadEnv.ToPoint3d(a.FitPoints[0]);
                var last = AcadEnv.ToPoint3d(a.FitPoints[a.FitPoints.Count - 1]);
                if (first.DistanceTo(last) > 1e-9) pts.Add(first);
            }
            var sp = new Spline(pts, 0, 0.0);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, sp, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawPoint(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_point", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawPointArgsDto>(args);
            var p = new DBPoint(AcadEnv.ToPoint3d(a.Position));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, p, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawDonut(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_donut", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawDonutArgsDto>(args);
            if (a.OuterDiameter <= 0 || a.InnerDiameter < 0 || a.InnerDiameter >= a.OuterDiameter)
                throw new ArgumentException("require 0 <= innerDiameter < outerDiameter");
            double rOut = a.OuterDiameter / 2.0;
            double rIn = a.InnerDiameter / 2.0;
            double width = rOut - rIn;
            double centerR = (rOut + rIn) / 2.0;
            var pl = new Polyline { Closed = true };
            var p1 = new Point2d(a.Center.X - centerR, a.Center.Y);
            var p2 = new Point2d(a.Center.X + centerR, a.Center.Y);
            pl.AddVertexAt(0, p1, 1.0, width, width);
            pl.AddVertexAt(1, p2, 1.0, width, width);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, pl, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawXLine(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_xline", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawXLineArgsDto>(args);
            var dir = new Vector3d(a.Direction.X, a.Direction.Y, 0);
            if (dir.Length < 1e-9) throw new ArgumentException("direction must be non-zero");
            var x = new Xline { BasePoint = AcadEnv.ToPoint3d(a.BasePoint), UnitDir = dir.GetNormal() };
            return Wrap(new { entity = AcadEnv.Persist(db, tr, x, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawRay(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_ray", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawRayArgsDto>(args);
            var dir = new Vector3d(a.Direction.X, a.Direction.Y, 0);
            if (dir.Length < 1e-9) throw new ArgumentException("direction must be non-zero");
            var r = new Ray { BasePoint = AcadEnv.ToPoint3d(a.BasePoint), UnitDir = dir.GetNormal() };
            return Wrap(new { entity = AcadEnv.Persist(db, tr, r, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawText(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_text", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawTextArgsDto>(args);
            if (a.Height <= 0) throw new ArgumentException("height must be > 0");
            var t = new DBText
            {
                Position = AcadEnv.ToPoint3d(a.Position),
                TextString = a.Text ?? "",
                Height = a.Height,
                Rotation = a.RotationDeg * Math.PI / 180.0,
            };
            if (!string.IsNullOrWhiteSpace(a.Style))
            {
                var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (ts.Has(a.Style)) t.TextStyleId = ts[a.Style];
            }
            return Wrap(new { entity = AcadEnv.Persist(db, tr, t, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawMText(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_mtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawMTextArgsDto>(args);
            if (a.Height <= 0 || a.Width <= 0) throw new ArgumentException("height and width must be > 0");
            var m = new MText
            {
                Location = AcadEnv.ToPoint3d(a.InsertionPoint),
                Width = a.Width,
                TextHeight = a.Height,
                Contents = a.Text ?? "",
            };
            if (!string.IsNullOrWhiteSpace(a.Style))
            {
                var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (ts.Has(a.Style)) m.TextStyleId = ts[a.Style];
            }
            return Wrap(new { entity = AcadEnv.Persist(db, tr, m, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawHatch(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_hatch", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawHatchArgsDto>(args);
            if (a.BoundaryHandles is null || a.BoundaryHandles.Count == 0)
                throw new ArgumentException("hatch needs >= 1 boundary handle");
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var hatch = new Hatch();
            if (!string.IsNullOrWhiteSpace(a.Layer)) hatch.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer);
            ms.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            hatch.SetHatchPattern(HatchPatternType.PreDefined, a.Pattern ?? "ANSI31");
            hatch.PatternScale = a.Scale > 0 ? a.Scale : 1.0;
            hatch.PatternAngle = a.AngleDeg * Math.PI / 180.0;
            hatch.Associative = true;
            hatch.HatchStyle = HatchStyle.Normal;

            var loopIds = new ObjectIdCollection();
            foreach (var h in a.BoundaryHandles)
            {
                loopIds.Add(AcadEnv.ResolveHandle(db, h));
            }
            hatch.AppendLoop(HatchLoopTypes.Default, loopIds);
            hatch.EvaluateHatch(true);
            return Wrap(new { entity = AcadEnv.ToHandle(hatch) });
        });

    private static Task<ToolDispatchResult> DrawRevcloud(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_revcloud", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawRevcloudArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 3)
                throw new ArgumentException("revcloud needs >= 3 vertices");

            // This used to add every vertex with bulge 0 and ignore arcMin/arcMax entirely,
            // so it produced a plain closed polygon - not a revision cloud. Each edge is now
            // subdivided into scallops whose chord length lands inside [arcMin, arcMax], and
            // every segment gets a bulge so it renders as an arc.
            double min = a.ArcMin > 0 ? a.ArcMin : 0;
            double max = a.ArcMax > 0 ? a.ArcMax : 0;
            if (min <= 0 && max <= 0) { min = 300; max = 500; }      // AutoCAD's own defaults
            else if (min <= 0) min = max / 2;
            else if (max <= 0) max = min * 2;
            if (max < min) (min, max) = (max, min);
            double target = (min + max) / 2.0;

            var pts = a.Vertices.Select(v => AcadEnv.ToPoint2d(v)).ToList();

            // Arcs must bulge away from the enclosed area. Positive bulge turns to the left of
            // travel, which is inward on a CCW ring, so the sign follows the signed area.
            double twiceArea = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % pts.Count];
                twiceArea += (p.X * q.Y) - (q.X * p.Y);
            }
            // tan(theta/4) for theta ~= 106 deg: the shallow scallop AutoCAD draws.
            double bulge = (twiceArea >= 0 ? -1.0 : 1.0) * 0.5;

            var pl = new Polyline { Closed = true };
            int idx = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % pts.Count];
                double len = p.GetDistanceTo(q);
                int steps = Math.Max(1, (int)Math.Round(len / target));

                for (int s = 0; s < steps; s++)
                {
                    double f = (double)s / steps;
                    var v = new Point2d(p.X + (q.X - p.X) * f, p.Y + (q.Y - p.Y) * f);
                    pl.AddVertexAt(idx++, v, bulge, 0, 0);
                }
            }
            return Wrap(new { entity = AcadEnv.Persist(db, tr, pl, a.Layer), scallops = idx });
        });

    // ─────────── query handlers ───────────

    private static Task<ToolDispatchResult> GetEntity(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_entity", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArgDto>(args);
            var id = AcadEnv.ResolveHandle(db, a.Handle);
            var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
            BoundingBoxDto? bbox = null;
            try { bbox = AcadEnv.BoundsOf(ent.GeometricExtents); } catch { }
            double? length = null, area = null;
            bool? closed = null;
            Point2dDto? sp = null, ep = null;
            if (ent is Curve curve)
            {
                try { length = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam); } catch { }
                try { area = curve.Area; } catch { }
                try { closed = curve.Closed; } catch { }
                try { sp = AcadEnv.FromPoint(curve.StartPoint); } catch { }
                try { ep = AcadEnv.FromPoint(curve.EndPoint); } catch { }
            }
            else if (ent is Hatch hatch)
            {
                try { area = hatch.Area; } catch { }
            }
            var color = AcadEnv.ColorOf(ent);
            string? lt = null;
            try { lt = ent.Linetype; } catch { }
            return Wrap(new
            {
                handle = a.Handle,
                @class = ent.GetRXClass().Name,
                layer = ent.Layer,
                color,
                linetype = lt,
                bbox,
                length,
                area,
                isClosed = closed,
                startPoint = sp,
                endPoint = ep,
            });
        });

    private static Task<ToolDispatchResult> ListEntitiesInWindow(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.list_entities_in_window", args, ct, (doc, db, tr) =>
        {
            var a = Read<WindowArgDto>(args);
            if (a.Corner1 is null) throw new ArgumentException("corner1 required (expected { x, y }).");
            if (a.Corner2 is null) throw new ArgumentException("corner2 required (expected { x, y }).");
            double xMin = Math.Min(a.Corner1.X, a.Corner2.X), xMax = Math.Max(a.Corner1.X, a.Corner2.X);
            double yMin = Math.Min(a.Corner1.Y, a.Corner2.Y), yMax = Math.Max(a.Corner1.Y, a.Corner2.Y);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var found = new List<EntityHandle>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent is null) continue;
                if (!string.IsNullOrEmpty(a.LayerFilter) && !string.Equals(ent.Layer, a.LayerFilter, StringComparison.OrdinalIgnoreCase)) continue;
                Extents3d e;
                try { e = ent.GeometricExtents; } catch { continue; }
                bool intersects =
                    !(e.MaxPoint.X < xMin || e.MinPoint.X > xMax ||
                      e.MaxPoint.Y < yMin || e.MinPoint.Y > yMax);
                bool fullyInside = e.MinPoint.X >= xMin && e.MaxPoint.X <= xMax &&
                                   e.MinPoint.Y >= yMin && e.MaxPoint.Y <= yMax;
                if (a.Crossing ? intersects : fullyInside) found.Add(AcadEnv.ToHandle(ent));
            }
            return Wrap(new { entities = found });
        });

    private static Task<ToolDispatchResult> GetCurveLength(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_curve_length", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArgDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            if (ent is not Curve c) throw new ArgumentException("entity is not a Curve");
            double len = c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam);
            return Wrap(new { value = len });
        });

    private static Task<ToolDispatchResult> GetArea(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_area", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArgDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            double area = ent switch
            {
                Curve c => c.Area,
                Hatch h => h.Area,
                Region r => r.Area,
                _ => throw new ArgumentException("entity has no area (not a closed curve, hatch or region)"),
            };
            return Wrap(new { value = area });
        });

    private static Task<ToolDispatchResult> GetBoundingBox(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_bounding_box", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArgDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            return Wrap(new { bbox = AcadEnv.BoundsOf(ent.GeometricExtents) });
        });

    private static Task<ToolDispatchResult> GetIntersections(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_intersections", args, ct, (doc, db, tr) =>
        {
            var a = Read<TwoHandlesArgDto>(args);
            var ea = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.A), OpenMode.ForRead);
            var eb = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.B), OpenMode.ForRead);
            var pts = new Point3dCollection();
            ea.IntersectWith(eb, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
            var list = new List<Point2dDto>(pts.Count);
            foreach (Point3d p in pts) list.Add(AcadEnv.FromPoint(p));
            return Wrap(new { points = list });
        });

    private static Task<ToolDispatchResult> GetDistancePoints(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_distance_points", args, ct, (doc, db, tr) =>
        {
            var a = Read<TwoPointsArgDto>(args);
            double d = AcadEnv.ToPoint2d(a.A).GetDistanceTo(AcadEnv.ToPoint2d(a.B));
            return Wrap(new { value = d });
        });

    private static Task<ToolDispatchResult> GetDistanceToEntity(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.get_distance_to_entity", args, ct, (doc, db, tr) =>
        {
            var a = Read<PointAndHandleArgDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            if (ent is not Curve c) throw new ArgumentException("entity is not a Curve");
            var closest = c.GetClosestPointTo(AcadEnv.ToPoint3d(a.Point), false);
            double d = closest.DistanceTo(AcadEnv.ToPoint3d(a.Point));
            return Wrap(new { value = d });
        });

    // ─────────── modification handlers ───────────

    private static Task<ToolDispatchResult> OffsetCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.offset_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<OffsetArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            if (ent is not Curve c) throw new ArgumentException("entity is not a Curve");
            double signed = string.Equals(a.Side, "left", StringComparison.OrdinalIgnoreCase) ? -Math.Abs(a.Distance) : Math.Abs(a.Distance);
            var offsets = c.GetOffsetCurves(signed);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var handles = new List<EntityHandle>();
            foreach (DBObject o in offsets)
            {
                if (o is Entity ne)
                {
                    ms.AppendEntity(ne);
                    tr.AddNewlyCreatedDBObject(ne, true);
                    handles.Add(AcadEnv.ToHandle(ne));
                }
            }
            return Wrap(new { entities = handles });
        });

    private static Task<ToolDispatchResult> TrimCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.trim_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<TrimExtendArgsDto>(args);
            var target = (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleToModify), OpenMode.ForWrite);
            if (a.BoundaryHandles is null || a.BoundaryHandles.Count == 0)
                throw new ArgumentException("trim needs >= 1 boundary handle");
            var pts = new Point3dCollection();
            foreach (var bh in a.BoundaryHandles)
            {
                var bnd = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, bh), OpenMode.ForRead);
                target.IntersectWith(bnd, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
            }
            if (pts.Count == 0) throw new InvalidOperationException("no intersections - nothing to trim");

            var ordered = new List<double>();
            foreach (Point3d p in pts) ordered.Add(target.GetParameterAtPoint(target.GetClosestPointTo(p, false)));
            ordered.Sort();

            double pickParam = a.PickPoint is null
                ? (target.StartParam + target.EndParam) / 2.0
                : target.GetParameterAtPoint(target.GetClosestPointTo(AcadEnv.ToPoint3d(a.PickPoint), false));

            double lower = target.StartParam, upper = target.EndParam;
            foreach (var p in ordered)
            {
                if (p < pickParam && p > lower) lower = p;
                else if (p > pickParam && p < upper) upper = p;
            }

            var splits = target.GetSplitCurves(new DoubleCollection { lower, upper });
            if (splits is null || splits.Count == 0) throw new InvalidOperationException("trim split produced no curves");
            target.Erase();

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var handles = new List<EntityHandle>();
            for (int i = 0; i < splits.Count; i++)
            {
                if (i == 1) continue; // drop the middle segment between the two boundaries
                if (splits[i] is Entity ne)
                {
                    ms.AppendEntity(ne);
                    tr.AddNewlyCreatedDBObject(ne, true);
                    handles.Add(AcadEnv.ToHandle(ne));
                }
            }
            return Wrap(new { entities = handles });
        });

    private static Task<ToolDispatchResult> ExtendCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.extend_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<TrimExtendArgsDto>(args);
            var target = (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleToModify), OpenMode.ForWrite);
            if (a.BoundaryHandles is null || a.BoundaryHandles.Count == 0)
                throw new ArgumentException("extend needs >= 1 boundary handle");

            Point3d? extendTo = null;
            foreach (var bh in a.BoundaryHandles)
            {
                var bnd = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, bh), OpenMode.ForRead);
                var pts = new Point3dCollection();
                if (bnd is Curve bc)
                {
                    var line = new Line(target.StartPoint, target.EndPoint);
                    line.IntersectWith(bc, Intersect.ExtendThis, pts, IntPtr.Zero, IntPtr.Zero);
                }
                foreach (Point3d p in pts)
                {
                    if (extendTo is null || p.DistanceTo(target.EndPoint) < extendTo.Value.DistanceTo(target.EndPoint))
                        extendTo = p;
                }
            }
            if (extendTo is null) throw new InvalidOperationException("no boundary intersection found - nothing to extend to");
            try { target.Extend(false, extendTo.Value); }
            catch { target.Extend(true, extendTo.Value); }
            return Wrap(new { entities = new[] { AcadEnv.ToHandle(target) } });
        });

    private static Task<ToolDispatchResult> JoinCurves(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.join_curves", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandlesArgDto>(args);
            if (a.Handles is null || a.Handles.Count < 2)
                throw new ArgumentException("join needs >= 2 handles");
            var curves = a.Handles
                .Select(h => (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite))
                .ToList();

            // Curve.JoinEntity only works when the geometry stays representable as the first
            // curve's own type - two collinear Lines, arcs on one circle, a polyline being
            // extended. Two Lines meeting at a corner throw eNotApplicable, which is the most
            // common join there is and what this tool's own description promises ("into a
            // single polyline"). So: try the native join, and on eNotApplicable fall back to
            // stitching the endpoints into a real Polyline, which is what AutoCAD's own JOIN
            // command does in that situation.
            // Strategy is chosen BEFORE anything is mutated. Discovering mid-loop that
            // JoinEntity cannot continue would leave some curves already joined and erased,
            // with no clean way back inside the caller's transaction.
            var chain = TryOrderIntoChain(curves);
            if (chain is not null && curves.All(c => c is Line or Arc or Polyline))
            {
                var pl = new Polyline();
                for (int i = 0; i < chain.Count; i++)
                    pl.AddVertexAt(i, new Point2d(chain[i].X, chain[i].Y), 0, 0, 0);

                // A chain whose ends coincide is a closed loop, not a doubled-up vertex.
                if (chain.Count > 2 && chain[0].DistanceTo(chain[^1]) < 1e-6)
                {
                    pl.RemoveVertexAt(pl.NumberOfVertices - 1);
                    pl.Closed = true;
                }

                var layer = curves[0].Layer;
                foreach (var c in curves) c.Erase();
                return Wrap(new
                {
                    entity = AcadEnv.Persist(db, tr, pl, layer),
                    strategy = "polyline",
                    vertices = pl.NumberOfVertices,
                });
            }

            var first = curves[0];
            for (int i = 1; i < curves.Count; i++)
            {
                try { first.JoinEntity(curves[i]); curves[i].Erase(); }
                catch (AcadRt.Exception ex)
                {
                    throw new InvalidOperationException(
                        $"join failed for handle {a.Handles[i]}: {ex.Message}. " +
                        "The curves must either be joinable in place (collinear lines, arcs on " +
                        "one circle) or form an end-to-end chain that can become a polyline.", ex);
                }
            }
            return Wrap(new { entity = AcadEnv.ToHandle(first), strategy = "join_entity" });
        });

    /// <summary>
    /// Order curves into a single open or closed chain of points, or null when they do not
    /// connect end-to-end. Greedy walk: start at the first curve and repeatedly attach
    /// whichever unused curve touches the current free end.
    /// </summary>
    private static List<Point3d>? TryOrderIntoChain(List<Curve> curves)
    {
        const double Tol = 1e-6;
        if (curves.Count < 2) return null;

        var ends = new List<(Point3d a, Point3d b)>();
        foreach (var c in curves)
        {
            try { ends.Add((c.StartPoint, c.EndPoint)); }
            catch { return null; }   // closed curves (circle, ellipse) have no usable ends
        }

        var used = new bool[curves.Count];
        var pts = new List<Point3d> { ends[0].a, ends[0].b };
        used[0] = true;

        for (int placed = 1; placed < curves.Count; placed++)
        {
            var tail = pts[^1];
            int found = -1;
            for (int i = 0; i < curves.Count; i++)
            {
                if (used[i]) continue;
                if (tail.DistanceTo(ends[i].a) < Tol) { pts.Add(ends[i].b); found = i; break; }
                if (tail.DistanceTo(ends[i].b) < Tol) { pts.Add(ends[i].a); found = i; break; }
            }
            if (found < 0) return null;   // not a single connected chain
            used[found] = true;
        }
        return pts;
    }

    private static Task<ToolDispatchResult> ExplodeEntity(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.explode_entity", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArgDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            var pieces = new DBObjectCollection();
            ent.Explode(pieces);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var handles = new List<EntityHandle>();
            foreach (DBObject o in pieces)
            {
                if (o is Entity ne)
                {
                    ms.AppendEntity(ne);
                    tr.AddNewlyCreatedDBObject(ne, true);
                    handles.Add(AcadEnv.ToHandle(ne));
                }
            }
            ent.Erase();
            return Wrap(new { entities = handles });
        });

    private static Task<ToolDispatchResult> FilletCorner(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.fillet_corner", args, ct, (doc, db, tr) =>
        {
            var a = Read<FilletArgsDto>(args);
            if (a.Radius <= 0) throw new ArgumentException("radius must be > 0");
            var ca = (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleA), OpenMode.ForRead);
            var cb = (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleB), OpenMode.ForRead);
            var pts = new Point3dCollection();
            ca.IntersectWith(cb, Intersect.ExtendBoth, pts, IntPtr.Zero, IntPtr.Zero);
            if (pts.Count == 0) throw new InvalidOperationException("curves do not intersect - cannot fillet");
            var ip = pts[0];
            var ta = ca.GetFirstDerivative(ca.GetClosestPointTo(ip, false)).GetNormal();
            var tb = cb.GetFirstDerivative(cb.GetClosestPointTo(ip, false)).GetNormal();
            double half = ta.GetAngleTo(tb) / 2.0;
            if (half < 1e-9 || half > Math.PI - 1e-9) throw new InvalidOperationException("curves are tangent or collinear - cannot fillet");
            double dist = a.Radius / Math.Tan(half);
            var pa = ip + ta.Negate() * dist;
            var pb = ip + tb.Negate() * dist;
            var bisector = ((Vector3d)(ta.Negate() + tb.Negate())).GetNormal();
            var center = ip + bisector * (a.Radius / Math.Sin(half));
            double startAngle = Math.Atan2(pa.Y - center.Y, pa.X - center.X);
            double endAngle = Math.Atan2(pb.Y - center.Y, pb.X - center.X);
            var arc = new Arc(center, a.Radius, startAngle, endAngle);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, arc, layer: null) });
        });

    private static Task<ToolDispatchResult> ChamferCorner(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.chamfer_corner", args, ct, (doc, db, tr) =>
        {
            var a = Read<ChamferArgsDto>(args);
            if (a.DistA <= 0 || a.DistB <= 0) throw new ArgumentException("distances must be > 0");
            var ca = (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleA), OpenMode.ForRead);
            var cb = (Curve)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleB), OpenMode.ForRead);
            var pts = new Point3dCollection();
            ca.IntersectWith(cb, Intersect.ExtendBoth, pts, IntPtr.Zero, IntPtr.Zero);
            if (pts.Count == 0) throw new InvalidOperationException("curves do not intersect - cannot chamfer");
            var ip = pts[0];
            var ta = ca.GetFirstDerivative(ca.GetClosestPointTo(ip, false)).GetNormal();
            var tb = cb.GetFirstDerivative(cb.GetClosestPointTo(ip, false)).GetNormal();
            var pa = ip + ta.Negate() * a.DistA;
            var pb = ip + tb.Negate() * a.DistB;
            var line = new Line(pa, pb);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, line, layer: null) });
        });

    private static Task<ToolDispatchResult> DeleteEntities(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.delete_entities", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandlesArgDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("delete needs >= 1 handle");
            foreach (var h in a.Handles)
            {
                var id = AcadEnv.ResolveHandle(db, h);
                var ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                ent.Erase();
            }
            return Wrap(new { ok = true });
        });

    // ─────────── splines (roadmap 3.1) ───────────
    //
    // `draw_spline` already exists and interpolates THROUGH fit points. A control-vertex spline
    // is the other half of how AutoCAD models curves: the vertices pull the curve without lying
    // on it, which is what you want for a road centreline or a smooth façade, and what you get
    // when you edit a spline's shape rather than the points it must hit.

    /// <summary>A clamped uniform knot vector for the given control point count and degree.</summary>
    /// <remarks>
    /// n control points at degree d need n + d + 1 knots. "Clamped" means the first and last
    /// d+1 are repeated, which is what makes the curve start at the first vertex and end at the
    /// last — without it a CV spline floats away from both ends and looks like a tool that put
    /// the geometry somewhere else entirely.
    /// </remarks>
    private static DoubleCollection ClampedKnots(int count, int degree)
    {
        var knots = new DoubleCollection();
        var spans = count - degree;
        for (int i = 0; i <= degree; i++) knots.Add(0.0);
        for (int i = 1; i < spans; i++) knots.Add(i);
        for (int i = 0; i <= degree; i++) knots.Add(spans);
        return knots;
    }

    private static Task<ToolDispatchResult> DrawSplineCv(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.draw_spline_cv", args, ct, (doc, db, tr) =>
        {
            var a = Read<SplineCvArgsDto>(args);
            if (a.ControlPoints is null || a.ControlPoints.Count < 2)
                throw new ArgumentException(
                    "controlPoints is required and needs at least 2. These are the vertices that " +
                    "PULL the curve - unlike draw_spline's fit points, the curve does not pass " +
                    "through them except at the two ends.");

            var degree = a.Degree ?? 3;
            if (degree < 1 || degree > 11)
                throw new ArgumentException("degree must be between 1 and 11; AutoCAD's own limit.");
            if (a.ControlPoints.Count <= degree)
                throw new ArgumentException(
                    "A degree-" + degree + " spline needs more than " + degree + " control points; " +
                    a.ControlPoints.Count + " were given. Either add points or lower the degree - " +
                    "degree 1 is a polyline, 2 is a quadratic and 3 is AutoCAD's default.");

            var pts = new Point3dCollection();
            foreach (var p in a.ControlPoints) pts.Add(AcadEnv.ToPoint3d(p));

            var weights = new DoubleCollection();
            for (int i = 0; i < pts.Count; i++) weights.Add(1.0);

            var sp = new Spline(degree, false, a.Closed == true, false,
                                pts, ClampedKnots(pts.Count, degree), weights, 0.0, 0.0);

            var handle = AcadEnv.Persist(db, tr, sp, a.Layer);
            return Wrap(new
            {
                entity = handle,
                controlPoints = sp.NumControlPoints,
                degree = sp.Degree,
                closed = sp.Closed,
                length = sp.GetDistanceAtParameter(sp.EndParam),
                note = "Control vertices shape the curve without lying on it, except the first " +
                       "and last, which it does touch. Use draw_spline when the curve must pass " +
                       "through given points instead.",
            });
        });

    private static Task<ToolDispatchResult> EditSplineFitPoint(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.edit_spline_fit_point", args, ct, (doc, db, tr) =>
        {
            var a = Read<SplineFitPointArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle))
                throw new ArgumentException("handle is required.");
            if (a.Point is null) throw new ArgumentException("point is required: where it moves to.");

            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle!), OpenMode.ForWrite);
            if (ent is not Spline sp)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetType().Name + ", not a Spline.");

            // A CV spline has no fit points at all, and asking for one throws an HRESULT that
            // says nothing about which kind of spline this is. Say it plainly instead.
            if (!sp.HasFitData || sp.NumFitPoints == 0)
                throw new ArgumentException(
                    "This spline carries no fit points - it was defined by control vertices, so " +
                    "there is nothing here to move. Fit points exist only on a spline made to pass " +
                    "through given points, such as one from draw_spline.");

            if (a.Index is null || a.Index < 0 || a.Index >= sp.NumFitPoints)
                throw new ArgumentException(
                    "index is required and must be 0.." + (sp.NumFitPoints - 1) + "; this spline " +
                    "has " + sp.NumFitPoints + " fit point(s).");

            var i = a.Index.Value;
            var before = sp.GetFitPointAt(i);
            var lengthBefore = sp.GetDistanceAtParameter(sp.EndParam);
            sp.SetFitPointAt(i, AcadEnv.ToPoint3d(a.Point));
            var now = sp.GetFitPointAt(i);

            if (now.DistanceTo(AcadEnv.ToPoint3d(a.Point)) > 1e-6)
                throw new InvalidOperationException(
                    "The fit point reads back at " + Fmt(now) + " rather than where it was moved " +
                    "to, so the edit did not take and nothing is being reported as success.");

            return Wrap(new
            {
                handle = a.Handle,
                index = i,
                before = new[] { before.X, before.Y, before.Z },
                point = new[] { now.X, now.Y, now.Z },
                fitPoints = sp.NumFitPoints,
                lengthBefore,
                length = sp.GetDistanceAtParameter(sp.EndParam),
                note = "A fit point is a point the curve must pass through, so moving one changes " +
                       "the shape either side of it, not just at it.",
            });
        });

    private static Task<ToolDispatchResult> SplineToPolyline(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.spline_to_polyline", args, ct, (doc, db, tr) =>
        {
            var a = Read<SplineConvertArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle))
                throw new ArgumentException("handle is required.");

            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle!), OpenMode.ForWrite);
            if (ent is not Spline sp)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetType().Name + ", not a Spline.");

            var lengthBefore = sp.GetDistanceAtParameter(sp.EndParam);
            var converted = sp.ToPolyline();
            if (converted is not Entity pl)
                throw new InvalidOperationException("AutoCAD returned no polyline for this spline.");

            pl.SetPropertiesFrom(sp);
            var handle = AcadEnv.Persist(db, tr, pl, a.Layer);

            var keep = a.KeepOriginal == true;
            if (!keep) sp.Erase();

            double? lengthAfter = pl is Curve c
                ? c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam)
                : null;

            return Wrap(new
            {
                entity = handle,
                type = pl.GetType().Name,
                vertices = pl is Polyline p2 ? p2.NumberOfVertices : (int?)null,
                lengthBefore,
                length = lengthAfter,
                originalKept = keep,
                originalHandle = keep ? a.Handle : null,
                note = "The conversion APPROXIMATES the curve with arc and line segments, so the " +
                       "length changes slightly - both are reported. The original spline is erased " +
                       "unless keepOriginal is true, in which case two entities now overlap.",
            });
        });

    // ─────────── display order, transparency, wipeouts (roadmap 3.1) ───────────
    //
    // These three go together on a real sheet: a wipeout is only useful if it sits in FRONT of
    // what it hides, and transparency is the alternative when you want to see through rather
    // than blank out.

    private static Task<ToolDispatchResult> SetDrawOrder(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.set_draworder", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawOrderArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: the entities to reorder.");

            var position = (a.Position ?? "").Trim().ToLowerInvariant();
            var needsRef = position is "above" or "below";
            if (position is not ("front" or "back" or "above" or "below"))
                throw new ArgumentException(
                    "position must be 'front', 'back', 'above' or 'below'; got '" + a.Position + "'.");
            if (needsRef && string.IsNullOrWhiteSpace(a.RelativeTo))
                throw new ArgumentException(
                    "position '" + position + "' needs relativeTo: the handle this should sit " +
                    position + ". Use 'front' or 'back' to move to the extreme instead.");

            var ids = new ObjectIdCollection();
            foreach (var h in a.Handles) ids.Add(AcadEnv.ResolveHandle(db, h));

            // Draw order is a property of the SPACE, not of the entity, and every entity in the
            // call has to live in the same one - the table belongs to a single block record.
            var first = (Entity)tr.GetObject(ids[0], OpenMode.ForRead);
            var owner = (BlockTableRecord)tr.GetObject(first.OwnerId, OpenMode.ForRead);
            for (int i = 1; i < ids.Count; i++)
            {
                var e = (Entity)tr.GetObject(ids[i], OpenMode.ForRead);
                if (e.OwnerId != first.OwnerId)
                    throw new ArgumentException(
                        "All entities must be in the same space: draw order is a property of a " +
                        "block record, not of the drawing, so model space and a layout cannot be " +
                        "reordered together.");
            }

            var table = (DrawOrderTable)tr.GetObject(owner.DrawOrderTableId, OpenMode.ForWrite);
            switch (position)
            {
                case "front": table.MoveToTop(ids); break;
                case "back": table.MoveToBottom(ids); break;
                case "above":
                    table.MoveAbove(ids, AcadEnv.ResolveHandle(db, a.RelativeTo!)); break;
                case "below":
                    table.MoveBelow(ids, AcadEnv.ResolveHandle(db, a.RelativeTo!)); break;
            }

            return Wrap(new
            {
                affected = ids.Count,
                position,
                relativeTo = a.RelativeTo,
                note = "Draw order decides what covers what where entities overlap. It is per " +
                       "space, so this affects the model space or the layout the entities are in, " +
                       "not the whole drawing.",
            });
        });

    private static Task<ToolDispatchResult> SetObjectTransparency(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.set_object_transparency", args, ct, (doc, db, tr) =>
        {
            var a = Read<TransparencyArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required.");

            var mode = (a.Mode ?? "value").Trim().ToLowerInvariant();
            Autodesk.AutoCAD.Colors.Transparency t;
            double? pct = null;

            switch (mode)
            {
                case "bylayer":
                case "byblock":
                    // WITHDRAWN, and measured rather than assumed. `new Transparency(
                    // TransparencyMethod.ByLayer)` compiles - the enum member exists and so does
                    // that constructor - but assigning the result to Entity.Transparency throws
                    // eInvalidKey on every entity type tried: Line, Circle, Polyline and Hatch.
                    // The percentage form succeeds on all four in the same run, so this is the
                    // constructor, not the entity or the transaction.
                    //
                    // There is no other way in: probing found no constructor taking a raw DXF
                    // value (0x01000000 is the ByLayer sentinel) and no static Transparency.ByLayer.
                    //
                    // Refused with the measurement rather than silently mapped to opaque, which
                    // would look like it worked and quietly break inheritance.
                    throw new ArgumentException(
                        "mode '" + a.Mode + "' is not available. AutoCAD's managed API accepts " +
                        "new Transparency(TransparencyMethod." + (mode == "bylayer" ? "ByLayer" : "ByBlock") +
                        ") at compile time but throws eInvalidKey when the result is assigned - " +
                        "measured on Line, Circle, Polyline and Hatch, while the percentage form " +
                        "succeeded on all four. Give percent instead; 0 is opaque. See KNOWN-GAPS B.");
                case "value":
                    if (a.Percent is null)
                        throw new ArgumentException(
                            "percent is required when mode is 'value' (the default): 0 is opaque " +
                            "and 90 is as see-through as AutoCAD allows.");
                    if (a.Percent < 0 || a.Percent > 90)
                        throw new ArgumentException(
                            "percent must be between 0 and 90. AutoCAD's own limit is 90; above " +
                            "that an object would be invisible and indistinguishable from deleted.");
                    pct = a.Percent.Value;
                    // AutoCAD stores ALPHA, where 255 is fully opaque - the inverse of the
                    // percentage its UI shows. Setting alpha to the percentage directly would
                    // make "10% transparent" nearly invisible.
                    var alpha = (byte)Math.Round(255.0 * (1.0 - pct.Value / 100.0));
                    t = new Autodesk.AutoCAD.Colors.Transparency(alpha);
                    break;
                default:
                    throw new ArgumentException(
                        "mode must be 'value'; got '" + a.Mode + "'. byLayer and byBlock are recognised but unavailable - see the message they give.");
            }

            var done = new List<object>();
            foreach (var h in a.Handles)
            {
                var e = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                e.Transparency = t;
                done.Add(new { handle = h, alpha = (int)e.Transparency.Alpha });
            }

            return Wrap(new
            {
                affected = done.Count, mode, percent = pct, entities = done,
                note = "AutoCAD stores transparency as ALPHA (255 = opaque), the inverse of the " +
                       "percentage its UI shows. On SCREEN it renders whenever TRANSPARENCYDISPLAY " +
                       "is on, which is the default. In PLOTTED or EXPORTED output it is ignored " +
                       "unless PLOTTRANSPARENCYOVERRIDE is 1 - measured: a 40% object exported to " +
                       "PNG through the plot engine came out fully opaque. A PNG is therefore not " +
                       "evidence that this tool did nothing.",
            });
        });

    private static Task<ToolDispatchResult> CreateWipeout(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.create_wipeout", args, ct, (doc, db, tr) =>
        {
            var a = Read<WipeoutArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 3)
                throw new ArgumentException(
                    "vertices is required and needs at least 3 points: a wipeout is an area, and " +
                    "two points enclose nothing.");

            var pts = new Point2dCollection();
            foreach (var v in a.Vertices) pts.Add(AcadEnv.ToPoint2d(v));
            // A wipeout's boundary must close. AutoCAD closes it itself if the last point does
            // not repeat the first, and duplicating it here would leave a zero-length edge.
            if (pts[0].GetDistanceTo(pts[pts.Count - 1]) > 1e-9) pts.Add(pts[0]);

            var wipe = new Wipeout();
            wipe.SetDatabaseDefaults();
            wipe.SetFrom(pts, Vector3d.ZAxis);

            var handle = AcadEnv.Persist(db, tr, wipe, a.Layer);

            // In FRONT by default. A wipeout behind what it is meant to hide is invisible and
            // looks exactly like a tool that did nothing - which is the whole failure shape this
            // bank keeps finding, so the default is the useful one rather than AutoCAD's.
            if (a.BringToFront != false)
            {
                var owner = (BlockTableRecord)tr.GetObject(wipe.OwnerId, OpenMode.ForRead);
                var table = (DrawOrderTable)tr.GetObject(owner.DrawOrderTableId, OpenMode.ForWrite);
                var ids = new ObjectIdCollection { wipe.ObjectId };
                table.MoveToTop(ids);
            }

            return Wrap(new
            {
                entity = handle,
                vertices = pts.Count,
                broughtToFront = a.BringToFront != false,
                note = "A wipeout hides what is BEHIND it, so it was moved to the front - pass " +
                       "bringToFront=false to leave it where it was drawn. Its frame is controlled " +
                       "drawing-wide by set_wipeout_frame.",
            });
        });

    private static Task<ToolDispatchResult> SetWipeoutFrame(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.set_wipeout_frame", args, ct, (doc, db, tr) =>
        {
            var a = Read<WipeoutFrameArgsDto>(args);
            var mode = (a.Mode ?? "").Trim().ToLowerInvariant();
            short value = mode switch
            {
                "hidden" or "off" => 0,
                "shown" or "on" => 1,
                "displayednotplotted" or "displaynotplot" => 2,
                _ => throw new ArgumentException(
                    "mode must be 'hidden', 'shown' or 'displayedNotPlotted'; got '" + a.Mode + "'."),
            };

            var before = (short)Autodesk.AutoCAD.ApplicationServices.Application
                .GetSystemVariable("WIPEOUTFRAME");
            Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("WIPEOUTFRAME", value);
            var now = (short)Autodesk.AutoCAD.ApplicationServices.Application
                .GetSystemVariable("WIPEOUTFRAME");
            if (now != value)
                throw new InvalidOperationException(
                    "WIPEOUTFRAME still reads " + now + " after being set to " + value + ".");

            return Wrap(new
            {
                mode, wipeoutframe = (int)now, before = (int)before,
                note = "Drawing-wide, and every wipeout changes with it. 'displayedNotPlotted' is " +
                       "the setting a real sheet usually wants: the frame is visible while you work " +
                       "and absent from the plot.",
            });
        });

    // ─────────── breaking and dividing curves (roadmap 3.1) ───────────

    private static Curve RequireCurve(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required: the curve to work on.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (ent is Curve c) return c;
        throw new ArgumentException(
            "Entity " + handle + " is a " + ent.GetType().Name + ", not a Curve.");
    }

    /// <summary>The point ON the curve nearest the one given, and how far off it was.</summary>
    /// <remarks>
    /// AutoCAD's own BREAK takes a picked point and snaps it to the curve. `GetSplitCurves`
    /// does NOT: hand it a point that is not exactly on the curve and it throws, or splits
    /// somewhere unintended. Projecting first is what makes "break it near here" work the way a
    /// person means it, and the distance is reported so a caller can tell a 0.001 rounding error
    /// from having named the wrong curve entirely.
    /// </remarks>
    private static (Point3d OnCurve, double Offset) SnapToCurve(Curve c, Point3d wanted)
    {
        var p = c.GetClosestPointTo(wanted, extend: false);
        return (p, p.DistanceTo(wanted));
    }

    private static double LengthOf(Curve c) => c.GetDistanceAtParameter(c.EndParam)
                                             - c.GetDistanceAtParameter(c.StartParam);


    /// <summary>How DBPoint entities are drawn, drawing-wide. AutoCAD's DDPTYPE.</summary>
    /// <remarks>
    /// divide_object and measure_object place DBPoints, and a DBPoint at the default PDMODE of 0
    /// draws as a SINGLE PIXEL. Measured: after dividing a line into five, the exported PNG showed
    /// the line and no markers at all. The markers were there and the numbers were right; nothing
    /// was visible. A tool whose output cannot be seen looks like a tool that did nothing, so the
    /// bank needs a way to make them visible.
    ///
    /// PDMODE is drawing-wide and PDSIZE with it - this is not a per-entity property, which is why
    /// it is one tool rather than an argument on the other two.
    /// </remarks>
    private static Task<ToolDispatchResult> SetPointStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.set_point_style", args, ct, (doc, db, tr) =>
        {
            var a = Read<PointStyleArgsDto>(args);
            var named = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
            {
                ["dot"] = 0, ["none"] = 1, ["cross"] = 2, ["x"] = 3, ["tick"] = 4,
                ["circle"] = 32, ["circleDot"] = 33, ["circleCross"] = 34, ["circleX"] = 35,
                ["square"] = 64, ["squareDot"] = 65, ["squareCross"] = 66, ["squareX"] = 67,
            };

            short mode;
            if (a.Mode is not null)
            {
                if (!named.TryGetValue(a.Mode, out mode))
                    throw new ArgumentException(
                        "Unknown point style '" + a.Mode + "'. Use one of: " +
                        string.Join(", ", named.Keys) + ", or give pdmode as a number.");
            }
            else if (a.Pdmode is not null)
            {
                mode = (short)a.Pdmode.Value;
            }
            else
            {
                throw new ArgumentException(
                    "Give either mode (a name such as 'x' or 'circleCross') or pdmode (the raw " +
                    "number). 'dot' is AutoCAD's default and draws as a single pixel, which is why " +
                    "points placed by divide_object often look as though they were never created.");
            }

            var beforeMode = (short)Autodesk.AutoCAD.ApplicationServices.Application
                .GetSystemVariable("PDMODE");
            var beforeSize = System.Convert.ToDouble(Autodesk.AutoCAD.ApplicationServices.Application
                .GetSystemVariable("PDSIZE"));

            // Sysvars are Int16 here; passing an int throws eInvalidInput (rule 26).
            Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("PDMODE", mode);
            if (a.Size is not null)
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("PDSIZE", a.Size.Value);

            var nowMode = (short)Autodesk.AutoCAD.ApplicationServices.Application
                .GetSystemVariable("PDMODE");
            if (nowMode != mode)
                throw new InvalidOperationException(
                    "PDMODE still reads " + nowMode + " after being set to " + mode + ".");

            return Wrap(new
            {
                mode = a.Mode, pdmode = nowMode, beforePdmode = beforeMode,
                pdsize = System.Convert.ToDouble(Autodesk.AutoCAD.ApplicationServices.Application
                    .GetSystemVariable("PDSIZE")),
                beforePdsize = beforeSize,
                note = "PDMODE and PDSIZE are DRAWING-WIDE: every point in the drawing changes, " +
                       "existing ones included. A negative size is a percentage of the viewport; " +
                       "a positive one is absolute drawing units.",
            });
        });

    private static Task<ToolDispatchResult> BreakAtPoint(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.break_at_point", args, ct, (doc, db, tr) =>
        {
            var a = Read<BreakAtPointArgsDto>(args);
            if (a.Point is null) throw new ArgumentException("point is required: where to break.");
            var c = RequireCurve(db, tr, a.Handle, OpenMode.ForWrite);
            if (c.Closed)
                throw new ArgumentException(
                    "This curve is closed, so breaking it at ONE point would leave it closed with " +
                    "nothing removed. Use break_between_points, which opens it by taking a piece out.");

            var (at, offset) = SnapToCurve(c, AcadEnv.ToPoint3d(a.Point));
            var lengthBefore = LengthOf(c);

            var pts = new Point3dCollection { at };
            DBObjectCollection pieces;
            try { pieces = c.GetSplitCurves(pts); }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD would not split this curve at that point (" + ex.Message + "). The " +
                    "point resolved to " + Fmt(at) + ", which is " + offset.ToString("0.###") +
                    " from the one given. Breaking exactly at an endpoint is not a break.");
            }

            if (pieces.Count < 2)
                throw new ArgumentException(
                    "Splitting produced " + pieces.Count + " piece(s), not two. That happens when " +
                    "the point falls on an endpoint - it resolved to " + Fmt(at) + ".");

            var made = new List<object>();
            foreach (DBObject o in pieces)
            {
                if (o is not Entity ne) continue;
                ne.SetPropertiesFrom(c);
                made.Add(new { handle = AcadEnv.Persist(db, tr, ne, null).Handle,
                               length = ne is Curve nc ? LengthOf(nc) : 0.0 });
            }
            c.Erase();

            return Wrap(new
            {
                brokenAt = new[] { at.X, at.Y, at.Z },
                offsetFromRequested = offset,
                lengthBefore,
                pieces = made,
                count = made.Count,
                note = "The original entity is gone and its handle with it; the pieces are new " +
                       "entities that inherit its layer, colour and linetype.",
            });
        });

    private static Task<ToolDispatchResult> BreakBetweenPoints(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.break_between_points", args, ct, (doc, db, tr) =>
        {
            var a = Read<BreakBetweenArgsDto>(args);
            if (a.Point1 is null || a.Point2 is null)
                throw new ArgumentException("point1 and point2 are both required.");
            var c = RequireCurve(db, tr, a.Handle, OpenMode.ForWrite);

            var (p1, off1) = SnapToCurve(c, AcadEnv.ToPoint3d(a.Point1));
            var (p2, off2) = SnapToCurve(c, AcadEnv.ToPoint3d(a.Point2));
            if (p1.DistanceTo(p2) < 1e-9)
                throw new ArgumentException(
                    "Both points resolved to " + Fmt(p1) + ", so there is no piece between them. " +
                    "Use break_at_point to split without removing anything.");

            var lengthBefore = LengthOf(c);
            var pts = new Point3dCollection { p1, p2 };
            DBObjectCollection pieces;
            try { pieces = c.GetSplitCurves(pts); }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD would not split this curve between those points (" + ex.Message +
                    "). They resolved to " + Fmt(p1) + " and " + Fmt(p2) + ".");
            }

            // Split points come back ordered ALONG the curve regardless of which order they were
            // given in, so on an OPEN curve the piece to discard is the middle one - not "the
            // second argument's".
            //
            // Any other count is not handled, and that is deliberate rather than an oversight. A
            // closed curve splits into two arcs and which one lies "between" the points depends
            // on the direction the curve runs, which is not visible to the caller and which this
            // has not measured. Guessing it would remove the wrong half of a circle and report
            // success - the failure shape this whole bank exists to remove.
            if (pieces.Count != 3)
            {
                foreach (DBObject o in pieces) o.Dispose();
                throw new ArgumentException(
                    "Splitting produced " + pieces.Count + " piece(s) rather than three, which " +
                    "means this is not the open-curve case this tool handles. On a closed curve " +
                    "two points make two arcs and which one lies 'between' them depends on the " +
                    "direction the curve runs - removing the wrong half would look like success. " +
                    "Break a closed curve at one point first with break_at_point, then break the " +
                    "result.");
            }
            var keptIndices = new List<int> { 0, 2 };

            var kept = new List<object>();
            double removedLength = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] is not Entity ne) continue;
                if (keptIndices.Contains(i))
                {
                    ne.SetPropertiesFrom(c);
                    kept.Add(new { handle = AcadEnv.Persist(db, tr, ne, null).Handle,
                                   length = ne is Curve nc ? LengthOf(nc) : 0.0 });
                }
                else
                {
                    if (ne is Curve rc) removedLength += LengthOf(rc);
                    ne.Dispose();
                }
            }
            c.Erase();

            return Wrap(new
            {
                from = new[] { p1.X, p1.Y, p1.Z },
                to = new[] { p2.X, p2.Y, p2.Z },
                offsetFromRequested = new[] { off1, off2 },
                lengthBefore,
                removedLength,
                pieces = kept,
                count = kept.Count,
                note = "The original entity is gone and its handle with it. The piece between the " +
                       "two points was discarded; what remains inherits the original's properties.",
            });
        });

    private static string Fmt(Point3d p) =>
        "(" + p.X.ToString("0.###") + ", " + p.Y.ToString("0.###") + ", " + p.Z.ToString("0.###") + ")";

    /// <summary>Place a marker at each of the given points: a DBPoint, or a block if named.</summary>
    private static List<object> PlaceMarkers(
        Database db, Transaction tr, Curve c, List<double> distances, string? block,
        bool alignToCurve, string? layer)
    {
        ObjectId btrId = ObjectId.Null;
        if (!string.IsNullOrWhiteSpace(block))
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(block))
                throw new ArgumentException(
                    "No block named '" + block + "' in this drawing. Omit `block` to place plain " +
                    "points instead, or define it first.");
            btrId = bt[block];
        }

        var made = new List<object>();
        foreach (var d in distances)
        {
            var p = c.GetPointAtDist(d);
            Entity ent;
            if (btrId.IsNull)
            {
                ent = new DBPoint(p);
            }
            else
            {
                var br = new BlockReference(p, btrId);
                if (alignToCurve)
                {
                    // The tangent, taken as the first derivative at that point. AutoCAD's DIVIDE
                    // aligns blocks this way, and without it a door or a tree marker sits square
                    // to the world while the curve runs at an angle.
                    var v = c.GetFirstDerivative(p);
                    if (!v.IsZeroLength()) br.Rotation = Math.Atan2(v.Y, v.X);
                }
                ent = br;
            }
            made.Add(new
            {
                handle = AcadEnv.Persist(db, tr, ent, layer).Handle,
                point = new[] { p.X, p.Y, p.Z },
                distance = d,
            });
        }
        return made;
    }

    private static Task<ToolDispatchResult> DivideObject(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.divide_object", args, ct, (doc, db, tr) =>
        {
            var a = Read<DivideArgsDto>(args);
            if (a.Segments is null || a.Segments < 2)
                throw new ArgumentException(
                    "segments is required and must be at least 2: how many equal parts to divide " +
                    "into. Dividing into 1 would place no markers at all.");
            if (a.Segments > 32767)
                throw new ArgumentException("segments must be 32767 or fewer, as in AutoCAD's DIVIDE.");

            var c = RequireCurve(db, tr, a.Handle, OpenMode.ForRead);
            var len = LengthOf(c);
            var step = len / a.Segments.Value;

            // Interior points only: n segments have n-1 divisions. Marking the ends too would
            // put a marker on top of whatever already sits at each end of the curve.
            var ds = new List<double>();
            for (int i = 1; i < a.Segments.Value; i++) ds.Add(step * i);

            var made = PlaceMarkers(db, tr, c, ds, a.Block, a.AlignToCurve ?? true, a.Layer);
            return Wrap(new
            {
                handle = a.Handle, segments = a.Segments, segmentLength = step,
                curveLength = len, markers = made, count = made.Count,
                placed = string.IsNullOrWhiteSpace(a.Block) ? "points" : "blocks",
                note = "The curve itself is untouched - dividing marks it, it does not cut it. " +
                       (a.Segments - 1) + " marker(s) for " + a.Segments + " segments, at the " +
                       "divisions rather than the ends.",
            });
        });

    private static Task<ToolDispatchResult> MeasureObject(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.measure_object", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeasureArgsDto>(args);
            if (a.Distance is null || a.Distance <= 0)
                throw new ArgumentException("distance is required and must be greater than zero.");

            var c = RequireCurve(db, tr, a.Handle, OpenMode.ForRead);
            var len = LengthOf(c);
            if (a.Distance > len)
                throw new ArgumentException(
                    "distance " + a.Distance + " is longer than the curve (" +
                    len.ToString("0.###") + "), so no marker would be placed.");

            // Measured FROM THE START, and the remainder is left at the far end - which is what
            // AutoCAD's MEASURE does and why it differs from DIVIDE. The leftover is reported
            // because a caller spacing bolts along a beam needs to know about it.
            var ds = new List<double>();
            for (double d = a.Distance.Value; d < len - 1e-9; d += a.Distance.Value) ds.Add(d);

            var made = PlaceMarkers(db, tr, c, ds, a.Block, a.AlignToCurve ?? true, a.Layer);
            var remainder = len - (ds.Count == 0 ? 0 : ds[ds.Count - 1]);
            return Wrap(new
            {
                handle = a.Handle, distance = a.Distance, curveLength = len,
                markers = made, count = made.Count, remainder,
                placed = string.IsNullOrWhiteSpace(a.Block) ? "points" : "blocks",
                note = "Measured from the curve's start; the leftover " + remainder.ToString("0.###") +
                       " sits at the far end. That is what makes this different from divide_object, " +
                       "which spaces markers evenly and leaves no remainder.",
            });
        });

    // ─────────── polyline vertex editing (roadmap 3.1) ───────────
    //
    // A drawn polyline is a first draft. Everything below exists because the alternative to
    // editing one is deleting it and drawing it again, which loses its handle, its layer
    // overrides and anything referencing it.
    //
    // All of it is AcDbPolyline (the lightweight one). A Polyline2d/3d is a different class with
    // a different vertex model, so those are refused by name rather than silently mishandled.

    /// <summary>ConstantWidth, or null when the polyline has varying widths.</summary>
    /// <remarks>
    /// The GETTER throws eInvalidInput when the segments differ - it is not merely unset, it is
    /// unanswerable, because there is no single constant width to report. Reading it into a
    /// result AFTER setting one segment to its own width is what made set_polyline_width fail
    /// with a bare eInvalidInput that appeared to come from the edit. The edit was fine; building
    /// the answer was not.
    /// </remarks>
    private static double? SafeConstantWidth(Polyline pl)
    {
        try { return pl.ConstantWidth; }
        catch (AcadRt.Exception) { return null; }
    }

    private static Polyline RequirePolyline(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required: the polyline to edit.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (ent is Polyline pl) return pl;
        throw new ArgumentException(
            "Entity " + handle + " is a " + ent.GetType().Name + ", not a lightweight Polyline. " +
            "Polyline2d and Polyline3d store vertices as separate objects and are not handled by " +
            "these tools; convert with the PEDIT command first, or use the 3D tools.");
    }

    /// <summary>Validate a vertex index against the polyline, and say what the range actually is.</summary>
    private static int RequireVertexIndex(Polyline pl, int? index, bool allowAppend = false)
    {
        var limit = allowAppend ? pl.NumberOfVertices : pl.NumberOfVertices - 1;
        if (index is null)
            throw new ArgumentException(
                "index is required: which vertex, 0-based. This polyline has " +
                pl.NumberOfVertices + ", so valid values are 0.." + limit + ".");
        if (index < 0 || index > limit)
            throw new ArgumentException(
                "index " + index + " is out of range: this polyline has " + pl.NumberOfVertices +
                " vertices, so valid values are 0.." + limit + ".");
        return index.Value;
    }

    private static object VertexInfo(Polyline pl, int i)
    {
        var p = pl.GetPoint2dAt(i);
        return new
        {
            index = i,
            point = new[] { p.X, p.Y },
            bulge = pl.GetBulgeAt(i),
            startWidth = pl.GetStartWidthAt(i),
            endWidth = pl.GetEndWidthAt(i),
        };
    }

    private static List<object> AllVertices(Polyline pl)
    {
        var v = new List<object>();
        for (int i = 0; i < pl.NumberOfVertices; i++) v.Add(VertexInfo(pl, i));
        return v;
    }

    private static Task<ToolDispatchResult> ListPolylineVertices(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.geometry2d.list_polyline_vertices", ct, (doc, db, tr) =>
        {
            var a = Read<PolylineRefArgsDto>(args);
            var pl = RequirePolyline(db, tr, a.Handle, OpenMode.ForRead);
            return Wrap(new
            {
                handle = a.Handle,
                vertices = AllVertices(pl),
                count = pl.NumberOfVertices,
                closed = pl.Closed,
                length = pl.Length,
                constantWidth = SafeConstantWidth(pl),
                note = "Indices are 0-based and shift when a vertex is added or removed. A bulge " +
                       "is tan(quarter of the arc's included angle): 0 is a straight segment, 1 is " +
                       "a half circle, and the sign gives the direction.",
            });
        });

    private static Task<ToolDispatchResult> PolylineAddVertex(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.polyline_add_vertex", args, ct, (doc, db, tr) =>
        {
            var a = Read<PolylineVertexArgsDto>(args);
            var pl = RequirePolyline(db, tr, a.Handle, OpenMode.ForWrite);
            // allowAppend: index == NumberOfVertices means "put it on the end", which is the
            // common case and would otherwise need the caller to know the count first.
            var i = RequireVertexIndex(pl, a.Index, allowAppend: true);
            if (a.Point is null)
                throw new ArgumentException("point is required: where the new vertex goes.");

            var before = pl.NumberOfVertices;
            pl.AddVertexAt(i, AcadEnv.ToPoint2d(a.Point), a.Bulge ?? 0,
                           a.StartWidth ?? 0, a.EndWidth ?? 0);
            if (pl.NumberOfVertices != before + 1)
                throw new InvalidOperationException(
                    "The polyline still has " + pl.NumberOfVertices + " vertices after adding one.");

            return Wrap(new
            {
                handle = a.Handle, added = VertexInfo(pl, i),
                count = pl.NumberOfVertices, before, length = pl.Length,
            });
        });

    private static Task<ToolDispatchResult> PolylineRemoveVertex(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.polyline_remove_vertex", args, ct, (doc, db, tr) =>
        {
            var a = Read<PolylineRefArgsDto>(args);
            var pl = RequirePolyline(db, tr, a.Handle, OpenMode.ForWrite);
            var i = RequireVertexIndex(pl, a.Index);

            // A polyline needs two vertices to be a polyline. Removing the second-to-last leaves
            // a one-point entity AutoCAD draws as nothing - a tool reporting success over an
            // invisible result.
            if (pl.NumberOfVertices <= 2)
                throw new ArgumentException(
                    "This polyline has only " + pl.NumberOfVertices + " vertices; removing one " +
                    "would leave something that cannot be drawn. Delete the polyline instead.");

            var removed = VertexInfo(pl, i);
            var before = pl.NumberOfVertices;
            pl.RemoveVertexAt(i);
            if (pl.NumberOfVertices != before - 1)
                throw new InvalidOperationException(
                    "The polyline still has " + pl.NumberOfVertices + " vertices after removing one.");

            return Wrap(new
            {
                handle = a.Handle, removed, count = pl.NumberOfVertices, before, length = pl.Length,
            });
        });

    private static Task<ToolDispatchResult> EditPolylineVertex(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.edit_polyline_vertex", args, ct, (doc, db, tr) =>
        {
            var a = Read<PolylineVertexArgsDto>(args);
            var pl = RequirePolyline(db, tr, a.Handle, OpenMode.ForWrite);
            var i = RequireVertexIndex(pl, a.Index);

            if (a.Point is null && a.Bulge is null && a.StartWidth is null && a.EndWidth is null)
                throw new ArgumentException(
                    "Nothing to change. Give at least one of point, bulge, startWidth or endWidth. " +
                    "Omitted fields are left alone rather than reset, so a caller can move a vertex " +
                    "without flattening the arc it carries.");

            var before = VertexInfo(pl, i);
            if (a.Point is not null) pl.SetPointAt(i, AcadEnv.ToPoint2d(a.Point));
            if (a.Bulge is not null) pl.SetBulgeAt(i, a.Bulge.Value);
            if (a.StartWidth is not null) pl.SetStartWidthAt(i, a.StartWidth.Value);
            if (a.EndWidth is not null) pl.SetEndWidthAt(i, a.EndWidth.Value);

            return Wrap(new
            {
                handle = a.Handle, before, vertex = VertexInfo(pl, i),
                count = pl.NumberOfVertices, length = pl.Length,
            });
        });

    private static Task<ToolDispatchResult> SetPolylineWidth(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.set_polyline_width", args, ct, (doc, db, tr) =>
        {
            var a = Read<PolylineWidthArgsDto>(args);
            var pl = RequirePolyline(db, tr, a.Handle, OpenMode.ForWrite);
            if (a.Width is null)
                throw new ArgumentException("width is required.");
            if (a.Width < 0)
                throw new ArgumentException("width cannot be negative.");

            var beforeConstant = SafeConstantWidth(pl);
            var beforeWidths = AllVertices(pl);

            if (a.Segment is null)
            {
                // ConstantWidth is the whole-polyline setting AutoCAD's Properties palette shows.
                // Setting it does NOT clear per-segment widths that were set earlier, so those are
                // cleared explicitly - otherwise "set the width to 5" leaves segments at their old
                // values and the tool looks like it half-worked.
                pl.ConstantWidth = a.Width.Value;
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    pl.SetStartWidthAt(i, a.Width.Value);
                    pl.SetEndWidthAt(i, a.Width.Value);
                }
            }
            else
            {
                var seg = a.Segment.Value;
                var last = pl.NumberOfVertices - (pl.Closed ? 1 : 2);
                if (seg < 0 || seg > last)
                    throw new ArgumentException(
                        "segment " + seg + " is out of range: this polyline has " +
                        (last + 1) + " segment(s), so valid values are 0.." + last + ".");

                // ConstantWidth and per-segment widths are MUTUALLY EXCLUSIVE. While
                // ConstantWidth is non-zero, SetStartWidthAt throws a bare eInvalidInput that
                // names neither the property nor the conflict. Clearing it would make the whole
                // polyline hairline, so the width it implied is written back to every vertex
                // first - the appearance is unchanged, and only the named segment then moves.
                var current = SafeConstantWidth(pl);
                if (current is not null && current != 0.0)
                {
                    var implied = current.Value;
                    pl.ConstantWidth = 0.0;
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        pl.SetStartWidthAt(i, implied);
                        pl.SetEndWidthAt(i, implied);
                    }
                }

                pl.SetStartWidthAt(seg, a.Width.Value);
                pl.SetEndWidthAt(seg, a.Width.Value);
            }

            return Wrap(new
            {
                handle = a.Handle,
                width = a.Width,
                segment = a.Segment,
                scope = a.Segment is null ? "wholePolyline" : "oneSegment",
                beforeConstantWidth = beforeConstant,
                before = beforeWidths,
                vertices = AllVertices(pl),
                constantWidth = SafeConstantWidth(pl),
                note = SafeConstantWidth(pl) is null
                    ? "This polyline no longer has a single constant width, so constantWidth is " +
                      "null. AutoCAD's ConstantWidth property cannot answer for varying segments."
                    : null,
            });
        });

    private static Task<ToolDispatchResult> ReverseCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry2d.reverse_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<EntityRefArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle))
                throw new ArgumentException("handle is required.");
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle!), OpenMode.ForWrite);
            if (ent is not Curve c)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetType().Name + ", not a Curve. " +
                    "Direction only means something for curves.");

            var startBefore = c.StartPoint;
            var endBefore = c.EndPoint;

            // ReverseCurve returns void, so the only evidence it did anything is geometric - and
            // WHICH evidence depends on whether the curve is closed. A circle's start and end are
            // the same point, so the swap test proves nothing about one and fails every time; a
            // quarter-way sample does prove it, because reversing sends that point to the
            // three-quarter position.
            var isClosed = c.Closed;
            Point3d quarterBefore = default;
            var haveQuarter = false;
            if (isClosed)
            {
                try
                {
                    var len = c.GetDistanceAtParameter(c.EndParam);
                    quarterBefore = c.GetPointAtDist(len * 0.25);
                    haveQuarter = true;
                }
                catch (AcadRt.Exception) { }
            }

            c.ReverseCurve();

            if (!isClosed)
            {
                if (c.StartPoint.DistanceTo(endBefore) > 1e-9
                    || c.EndPoint.DistanceTo(startBefore) > 1e-9)
                    throw new InvalidOperationException(
                        "The curve's ends did not swap, so its direction was not reversed.");
            }
            else if (haveQuarter)
            {
                var len = c.GetDistanceAtParameter(c.EndParam);
                if (c.GetPointAtDist(len * 0.25).DistanceTo(quarterBefore) < 1e-9)
                    throw new ArgumentException(
                        "Reversing a " + ent.GetType().Name + " has no observable effect: the " +
                        "point a quarter of the way along it does not move. For a closed curve " +
                        "of this type AutoCAD does not change the direction geometry is " +
                        "generated in, so nothing was changed and success is not being " +
                        "reported. Reverse an open curve, or a Polyline, instead.");
            }

            return Wrap(new
            {
                handle = a.Handle,
                type = ent.GetType().Name,
                startBefore = new[] { startBefore.X, startBefore.Y, startBefore.Z },
                start = new[] { c.StartPoint.X, c.StartPoint.Y, c.StartPoint.Z },
                end = new[] { c.EndPoint.X, c.EndPoint.Y, c.EndPoint.Z },
                note = "Direction decides where an offset goes, which way a hatch boundary runs " +
                       "and where text along the curve reads from.",
            });
        });
}
