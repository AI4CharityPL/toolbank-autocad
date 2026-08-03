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

            var pts = a.Vertices.Select(AcadEnv.ToPoint2d).ToList();

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
}
