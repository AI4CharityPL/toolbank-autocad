// AutoCAD plugin handlers for the acad-geometry-3d category.
// Each handler is registered under "acad.geometry3d.<verb>" and ALWAYS runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;

namespace AcadMcp.Plugin.Tools;

internal static class Geometry3dPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.geometry3d.draw_box",            DrawBox);
        host.Register("acad.geometry3d.draw_sphere",         DrawSphere);
        host.Register("acad.geometry3d.draw_cylinder",       DrawCylinder);
        host.Register("acad.geometry3d.draw_cone",           DrawCone);
        host.Register("acad.geometry3d.draw_torus",          DrawTorus);
        host.Register("acad.geometry3d.draw_pyramid",        DrawPyramid);
        host.Register("acad.geometry3d.draw_wedge",          DrawWedge);
        host.Register("acad.geometry3d.extrude_curve",       ExtrudeCurve);
        host.Register("acad.geometry3d.revolve_curve",       RevolveCurve);
        host.Register("acad.geometry3d.draw_planar_surface", DrawPlanarSurface);
        host.Register("acad.geometry3d.get_volume",          GetVolume);
        host.Register("acad.geometry3d.get_surface_area",    GetSurfaceArea);
        host.Register("acad.geometry3d.get_3d_centroid",     Get3dCentroid);
        host.Register("acad.geometry3d.get_3d_bounding_box", Get3dBoundingBox);
        host.Register("acad.geometry3d.get_mass_properties", GetMassProperties);
    }

    // ─────────── helpers ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Solid3d ResolveSolid(Database db, Transaction tr, string handle)
    {
        var id = AcadEnv.ResolveHandle(db, handle);
        var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
        if (ent is Solid3d s) return s;
        throw new ArgumentException($"handle '{handle}' is not a 3D solid (got {ent.GetRXClass().Name}).");
    }

    private static Entity ResolveEntity(Database db, Transaction tr, string handle, OpenMode mode = OpenMode.ForRead)
    {
        var id = AcadEnv.ResolveHandle(db, handle);
        return (Entity)tr.GetObject(id, mode);
    }

    // ─────────── primitive solids ───────────

    private static Task<ToolDispatchResult> DrawBox(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_box", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawBoxArgsDto>(args);
            var p1 = AcadEnv.ToPoint3d(a.Corner1);
            var p2 = AcadEnv.ToPoint3d(a.Corner2);
            double xLen = Math.Abs(p2.X - p1.X);
            double yLen = Math.Abs(p2.Y - p1.Y);
            double zLen = Math.Abs(p2.Z - p1.Z);
            if (xLen <= 0 || yLen <= 0 || zLen <= 0)
                throw new ArgumentException("box requires three non-zero edge lengths.");
            var center = new Point3d((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, (p1.Z + p2.Z) / 2);
            var box = new Solid3d();
            box.CreateBox(xLen, yLen, zLen);
            box.TransformBy(Matrix3d.Displacement(center - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, box, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawSphere(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_sphere", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawSphereArgsDto>(args);
            if (a.Radius <= 0) throw new ArgumentException("sphere radius must be > 0.");
            var s = new Solid3d();
            s.CreateSphere(a.Radius);
            s.TransformBy(Matrix3d.Displacement(AcadEnv.ToPoint3d(a.Center) - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, s, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawCylinder(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_cylinder", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawCylinderArgsDto>(args);
            if (a.Radius <= 0 || a.Height <= 0)
                throw new ArgumentException("cylinder requires radius > 0 and height > 0.");
            var s = new Solid3d();
            // CreateFrustum(height, xRadius, yRadius, topRadius) - cylinder = topRadius == xRadius
            s.CreateFrustum(a.Height, a.Radius, a.Radius, a.Radius);
            var basePt = AcadEnv.ToPoint3d(a.BasePoint);
            var center = new Point3d(basePt.X, basePt.Y, basePt.Z + a.Height / 2);
            s.TransformBy(Matrix3d.Displacement(center - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, s, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawCone(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_cone", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawConeArgsDto>(args);
            if (a.Radius <= 0 || a.Height <= 0)
                throw new ArgumentException("cone requires radius > 0 and height > 0.");
            if (a.TopRadius < 0) throw new ArgumentException("topRadius must be >= 0.");
            var s = new Solid3d();
            s.CreateFrustum(a.Height, a.Radius, a.Radius, a.TopRadius);
            var basePt = AcadEnv.ToPoint3d(a.BasePoint);
            var center = new Point3d(basePt.X, basePt.Y, basePt.Z + a.Height / 2);
            s.TransformBy(Matrix3d.Displacement(center - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, s, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawTorus(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_torus", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawTorusArgsDto>(args);
            if (a.MajorRadius <= 0 || a.MinorRadius <= 0)
                throw new ArgumentException("torus requires majorRadius > 0 and minorRadius > 0.");
            var s = new Solid3d();
            s.CreateTorus(a.MajorRadius, a.MinorRadius);
            s.TransformBy(Matrix3d.Displacement(AcadEnv.ToPoint3d(a.Center) - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, s, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawPyramid(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_pyramid", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawPyramidArgsDto>(args);
            if (a.Sides < 3 || a.Sides > 32)
                throw new ArgumentException("pyramid sides must be in 3..32.");
            if (a.BaseRadius <= 0 || a.Height <= 0)
                throw new ArgumentException("pyramid requires baseRadius > 0 and height > 0.");
            if (a.TopRadius < 0)
                throw new ArgumentException("topRadius must be >= 0.");
            var s = new Solid3d();
            s.CreatePyramid(a.Height, a.Sides, a.BaseRadius, a.TopRadius);
            var basePt = AcadEnv.ToPoint3d(a.BasePoint);
            var center = new Point3d(basePt.X, basePt.Y, basePt.Z + a.Height / 2);
            s.TransformBy(Matrix3d.Displacement(center - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, s, a.Layer) });
        });

    private static Task<ToolDispatchResult> DrawWedge(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_wedge", args, ct, (doc, db, tr) =>
        {
            var a = Read<DrawWedgeArgsDto>(args);
            var p1 = AcadEnv.ToPoint3d(a.Corner1);
            var p2 = AcadEnv.ToPoint3d(a.Corner2);
            double xLen = Math.Abs(p2.X - p1.X);
            double yLen = Math.Abs(p2.Y - p1.Y);
            double zLen = Math.Abs(p2.Z - p1.Z);
            if (xLen <= 0 || yLen <= 0 || zLen <= 0)
                throw new ArgumentException("wedge requires three non-zero edge lengths.");
            var center = new Point3d((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, (p1.Z + p2.Z) / 2);
            var w = new Solid3d();
            w.CreateWedge(xLen, yLen, zLen);
            w.TransformBy(Matrix3d.Displacement(center - Point3d.Origin));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, w, a.Layer) });
        });

    // ─────────── extrude / revolve / surface ───────────

    private static Task<ToolDispatchResult> ExtrudeCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.extrude_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<ExtrudeCurveArgsDto>(args);
            if (Math.Abs(a.Height) < 1e-12) throw new ArgumentException("extrude height must be non-zero.");
            var ent = ResolveEntity(db, tr, a.Handle);
            double taperRad = a.TaperAngleDeg * Math.PI / 180.0;
            var solid = new Solid3d();
            // Region preferred; if Polyline / Circle, AutoCAD accepts directly via overload taking curve.
            if (ent is Region region)
            {
                solid.Extrude(region, a.Height, taperRad);
            }
            else if (ent is Curve curve)
            {
                // Convert closed planar curve to Region first.
                using var col = new DBObjectCollection();
                col.Add(curve);
                using var regionsCol = Region.CreateFromCurves(col);
                if (regionsCol.Count == 0)
                    throw new ArgumentException("could not build a region from the given curve - is it closed and planar?");
                using var firstRegion = (Region)regionsCol[0];
                solid.Extrude(firstRegion, a.Height, taperRad);
            }
            else
            {
                throw new ArgumentException($"entity {ent.GetRXClass().Name} cannot be extruded - need Region or closed planar Curve.");
            }
            return Wrap(new { entity = AcadEnv.Persist(db, tr, solid, a.Layer) });
        });

    private static Task<ToolDispatchResult> RevolveCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.revolve_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<RevolveCurveArgsDto>(args);
            var p0 = AcadEnv.ToPoint3d(a.AxisStart);
            var p1 = AcadEnv.ToPoint3d(a.AxisEnd);
            var axis = (p1 - p0);
            if (axis.Length < 1e-12)
                throw new ArgumentException("axisStart and axisEnd must be different points.");
            var dir = axis.GetNormal();
            double angRad = a.AngleDeg * Math.PI / 180.0;
            if (Math.Abs(angRad) < 1e-12) throw new ArgumentException("angleDeg must be non-zero.");

            var ent = ResolveEntity(db, tr, a.Handle);
            Region region;
            bool ownRegion = false;
            if (ent is Region r) region = r;
            else if (ent is Curve c)
            {
                using var col = new DBObjectCollection();
                col.Add(c);
                using var regionsCol = Region.CreateFromCurves(col);
                if (regionsCol.Count == 0)
                    throw new ArgumentException("could not build a region from the given curve.");
                region = (Region)regionsCol[0];
                ownRegion = true;
            }
            else
            {
                throw new ArgumentException($"entity {ent.GetRXClass().Name} cannot be revolved.");
            }
            try
            {
                var solid = new Solid3d();
                solid.Revolve(region, p0, dir, angRad);
                return Wrap(new { entity = AcadEnv.Persist(db, tr, solid, a.Layer) });
            }
            finally
            {
                if (ownRegion) region.Dispose();
            }
        });

    private static Task<ToolDispatchResult> DrawPlanarSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_planar_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlanarSurfaceArgsDto>(args);
            if (a.BoundaryHandles is null || a.BoundaryHandles.Count == 0)
                throw new ArgumentException("at least one boundary handle is required.");
            using var col = new DBObjectCollection();
            foreach (var h in a.BoundaryHandles)
            {
                var ent = ResolveEntity(db, tr, h);
                col.Add(ent);
            }
            // Region.CreateFromCurves is the universal sanctioned route to a closed planar 2D entity.
            // It returns one Region per closed planar loop. We persist the first and dispose the rest.
            using var regions = Region.CreateFromCurves(col);
            if (regions.Count == 0)
                throw new InvalidOperationException("no planar region could be created from the given boundaries.");
            var first = (Region)regions[0];
            var handle = AcadEnv.Persist(db, tr, first, a.Layer);
            for (int i = 1; i < regions.Count; i++)
            {
                ((Region)regions[i]).Dispose();
            }
            return Wrap(new { entity = handle });
        });

    // ─────────── queries ───────────

    private static Task<ToolDispatchResult> GetVolume(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.get_volume", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArg3Dto>(args);
            var s = ResolveSolid(db, tr, a.Handle);
            return Wrap(new { volume = s.MassProperties.Volume });
        });

    private static Task<ToolDispatchResult> GetSurfaceArea(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.get_surface_area", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArg3Dto>(args);
            var ent = ResolveEntity(db, tr, a.Handle);
            double area = 0;
            if (ent is Region r)
            {
                area = r.Area;
            }
            else if (ent is Solid3d sld)
            {
                using var brep = new Brep(sld);
                foreach (var face in brep.Faces)
                {
                    try { area += face.GetArea(); } catch { /* face may fail; skip */ }
                    face.Dispose();
                }
            }
            else if (ent is PlaneSurface ps)
            {
                using var brep = new Brep(ps);
                foreach (var face in brep.Faces)
                {
                    try { area += face.GetArea(); } catch { }
                    face.Dispose();
                }
            }
            else
            {
                throw new ArgumentException($"surface area not supported for {ent.GetRXClass().Name}.");
            }
            return Wrap(new { area });
        });

    private static Task<ToolDispatchResult> Get3dCentroid(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.get_3d_centroid", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArg3Dto>(args);
            var s = ResolveSolid(db, tr, a.Handle);
            var c = s.MassProperties.Centroid;
            return Wrap(new { centroid = AcadEnv.FromPoint3d(c) });
        });

    private static Task<ToolDispatchResult> Get3dBoundingBox(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.get_3d_bounding_box", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArg3Dto>(args);
            var ent = ResolveEntity(db, tr, a.Handle);
            if (!ent.Bounds.HasValue)
                throw new InvalidOperationException("entity has no computed extents.");
            return Wrap(new { bbox = AcadEnv.BoundsOf(ent.Bounds.Value) });
        });

    private static Task<ToolDispatchResult> GetMassProperties(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.get_mass_properties", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandleArg3Dto>(args);
            var s = ResolveSolid(db, tr, a.Handle);
            var mp = s.MassProperties;
            // Compute surface area via Brep; tolerate failures per face.
            double area = 0;
            try
            {
                using var brep = new Brep(s);
                foreach (var face in brep.Faces)
                {
                    try { area += face.GetArea(); } catch { }
                    face.Dispose();
                }
            }
            catch { area = 0; }

            // NOTE: AutoCAD .NET API has a typo: 'MomentsOfIntertia' (sic). Don't 'fix' it - it is the actual public name.
            var moi = new[] { mp.MomentsOfIntertia.X, mp.MomentsOfIntertia.Y, mp.MomentsOfIntertia.Z };
            var rog = new[] { mp.RadiiOfGyration.X,   mp.RadiiOfGyration.Y,   mp.RadiiOfGyration.Z };
            return Wrap(new
            {
                volume = mp.Volume,
                surfaceArea = area,
                centroid = AcadEnv.FromPoint3d(mp.Centroid),
                momentsOfInertia = moi,
                radiiOfGyration = rog,
            });
        });
}
