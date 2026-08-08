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
using AcadRt = Autodesk.AutoCAD.Runtime;
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

        // roadmap 4.1 - the rest of how a solid is made from a curve
        host.Register("acad.geometry3d.sweep_curve",         SweepCurve);
        host.Register("acad.geometry3d.loft_curves",         LoftCurves);
        host.Register("acad.geometry3d.draw_helix",          DrawHelix);
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

    // ─────────── roadmap 4.1: sweep, loft, helix ───────────
    //
    // extrude_curve pushes a profile in a straight line and revolve_curve spins it about an
    // axis. These three are the rest of how a solid gets made from a curve: along an arbitrary
    // PATH, between a series of CROSS SECTIONS, and the helix that is the usual path for a
    // spring or a thread.
    //
    // All three can be checked against ARITHMETIC rather than against another call of the same
    // code, which is rare and worth using: a solid swept along a straight path has the volume of
    // its profile times the path length, and a helix has the length of its own hypotenuse.

    /// <summary>A closed planar curve as a Region, which is what the solid builders take.</summary>
    private static Region RegionFrom(Entity ent, string what)
    {
        if (ent is Region r) return r;
        if (ent is Curve c)
        {
            using var col = new DBObjectCollection();
            col.Add(c);
            DBObjectCollection regions;
            try
            {
                regions = Region.CreateFromCurves(col);
            }
            catch (AcadRt.Exception ex)
            {
                // It THROWS on an open curve rather than returning an empty collection, so a
                // count check never runs and the caller gets a bare eInvalidInput that says
                // nothing about what was wrong with their profile.
                throw new ArgumentException(
                    "The " + what + " (" + ent.GetRXClass().Name + ") could not be made into a " +
                    "region - AutoCAD reported " + ex.ErrorStatus + ". A solid needs a CLOSED " +
                    "planar profile; an open curve would sweep into a surface, not a solid.");
            }
            using (regions)
            {
                if (regions.Count == 0)
                    throw new ArgumentException(
                        "The " + what + " (" + ent.GetRXClass().Name + ") made no region - a " +
                        "solid needs a CLOSED planar profile.");
                return (Region)regions[0];
            }
        }
        throw new ArgumentException(
            "The " + what + " is a " + ent.GetRXClass().Name + "; a closed planar curve or a " +
            "Region is needed.");
    }

    private static Task<ToolDispatchResult> SweepCurve(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.sweep_curve", args, ct, (doc, db, tr) =>
        {
            var a = Read<SweepArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.ProfileHandle) || string.IsNullOrWhiteSpace(a.PathHandle))
                throw new ArgumentException(
                    "profileHandle and pathHandle are both required: the shape being swept, and " +
                    "the curve it is swept along.");
            if (string.Equals(a.ProfileHandle, a.PathHandle, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "profileHandle and pathHandle are the same entity; a curve cannot be swept " +
                    "along itself.");

            var profileEnt = (Entity)tr.GetObject(
                AcadEnv.ResolveHandle(db, a.ProfileHandle!), OpenMode.ForWrite);
            var pathEnt = (Entity)tr.GetObject(
                AcadEnv.ResolveHandle(db, a.PathHandle!), OpenMode.ForWrite);
            if (pathEnt is not Curve pathCurve)
                throw new ArgumentException(
                    "The path is a " + pathEnt.GetRXClass().Name + ", not a curve. A sweep needs " +
                    "something to travel along - a line, arc, polyline, spline or helix.");

            var pathLength = pathCurve.GetDistanceAtParameter(pathCurve.EndParam) -
                             pathCurve.GetDistanceAtParameter(pathCurve.StartParam);
            using var region = RegionFrom(profileEnt, "profile");
            var profileArea = region.Area;

            var builder = new SweepOptionsBuilder
            {
                Align = (a.Align ?? "path").Trim().ToLowerInvariant() switch
                {
                    "path" => SweepOptionsAlignOption.AlignSweepEntityToPath,
                    "none" => SweepOptionsAlignOption.NoAlignment,
                    "translate" => SweepOptionsAlignOption.TranslateSweepEntityToPath,
                    // Only three members exist - asked of the compiler, which rejected a fourth
                    // named TranslateAndAlignSweepEntityToPath that the docs suggest.
                    _ => throw new ArgumentException(
                        "align must be path (the profile turns to stay square to the path, which " +
                        "is what you want for a pipe), none, or translate."),
                },
                Bank = a.Bank ?? true,
                TwistAngle = (a.TwistDeg ?? 0) * Math.PI / 180.0,
                ScaleFactor = a.Scale ?? 1.0,
            };
            if (a.Scale is <= 0)
                throw new ArgumentException("scale must be greater than zero.");

            var solid = new Solid3d();
            solid.CreateSweptSolid(region, pathCurve, builder.ToSweepOptions());

            var handle = AcadEnv.Persist(db, tr, solid, a.Layer);
            var volume = solid.MassProperties.Volume;
            if (volume <= 0)
                throw new InvalidOperationException(
                    "The swept solid has no volume, so nothing usable was made. The profile area " +
                    "was " + profileArea + " and the path " + pathLength + " long.");

            if (a.EraseSources == true)
            {
                profileEnt.Erase();
                pathEnt.Erase();
            }

            // Reported so the caller can check the result against arithmetic rather than trust
            // it: a profile swept SQUARE along a straight path encloses area x length, and a
            // number far off that means the profile turned, scaled or twisted on the way.
            var expected = profileArea * pathLength;
            return Wrap(new
            {
                entity = handle,
                volume,
                profileArea,
                pathLength,
                areaTimesLength = expected,
                ratioToAreaTimesLength = expected > 0 ? volume / expected : (double?)null,
                sourcesErased = a.EraseSources == true,
                note = "areaTimesLength is what the volume WOULD be for a profile carried square " +
                       "along a straight path - " + expected + " here against a measured " +
                       volume + ". They agree on a straight path and diverge as the path bends, " +
                       "which is the geometry doing its job rather than an error.",
            });
        });

    private static Task<ToolDispatchResult> LoftCurves(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.loft_curves", args, ct, (doc, db, tr) =>
        {
            var a = Read<LoftArgsDto>(args);
            if (a.ProfileHandles is null || a.ProfileHandles.Count < 2)
                throw new ArgumentException(
                    "profileHandles needs at least 2 cross sections: a loft runs a skin between " +
                    "them, and one section has nothing to run to.");

            var sections = new List<Entity>();
            var opened = new List<Entity>();
            var areas = new List<double>();
            foreach (var h in a.ProfileHandles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                opened.Add(ent);
                var reg = RegionFrom(ent, "cross section " + h);
                areas.Add(reg.Area);
                sections.Add(reg);
            }

            var guides = new List<Entity>();
            foreach (var h in a.GuideHandles ?? new List<string>())
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                opened.Add(ent);
                if (ent is not Curve gc)
                    throw new ArgumentException(
                        "Guide " + h + " is a " + ent.GetRXClass().Name + ", not a curve.");
                guides.Add(gc);
            }

            Entity? path = null;
            if (!string.IsNullOrWhiteSpace(a.PathHandle))
            {
                var ent = (Entity)tr.GetObject(
                    AcadEnv.ResolveHandle(db, a.PathHandle!), OpenMode.ForWrite);
                opened.Add(ent);
                if (ent is not Curve pc)
                    throw new ArgumentException(
                        "The path is a " + ent.GetRXClass().Name + ", not a curve.");
                path = pc;
            }
            if (guides.Count > 0 && path is not null)
                throw new ArgumentException(
                    "Give guides OR a path, not both - AutoCAD's LOFT offers them as alternatives " +
                    "and cannot follow the two at once.");

            var lob = new LoftOptionsBuilder
            {
                Closed = a.Closed ?? false,
                Ruled = a.Ruled ?? false,
            };

            var solid = new Solid3d();
            solid.CreateLoftedSolid(sections.ToArray(), guides.ToArray(), path,
                                    lob.ToLoftOptions());

            var handle = AcadEnv.Persist(db, tr, solid, a.Layer);
            var volume = solid.MassProperties.Volume;
            if (volume <= 0)
                throw new InvalidOperationException(
                    "The lofted solid has no volume. Cross-section areas were " +
                    string.Join(", ", areas) + ".");

            if (a.EraseSources == true) foreach (var e in opened) e.Erase();

            return Wrap(new
            {
                entity = handle,
                volume,
                crossSections = sections.Count,
                guides = guides.Count,
                hasPath = path is not null,
                sectionAreas = areas,
                closed = lob.Closed,
                ruled = lob.Ruled,
                sourcesErased = a.EraseSources == true,
                note = "sectionAreas are the areas the skin runs between, so a volume can be " +
                       "checked against them: two equal sections a distance D apart make a prism " +
                       "of area x D, and a taper makes less. ruled=true joins the sections with " +
                       "straight sides instead of a smooth skin.",
            });
        });

    private static Task<ToolDispatchResult> DrawHelix(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.draw_helix", args, ct, (doc, db, tr) =>
        {
            var a = Read<HelixArgsDto>(args);
            if (a.Center is null)
                throw new ArgumentException("center is required: the base centre of the helix.");
            if (a.BaseRadius is null || a.BaseRadius <= 0)
                throw new ArgumentException("baseRadius is required and must be greater than zero.");
            if (a.Turns is null || a.Turns <= 0)
                throw new ArgumentException(
                    "turns is required and must be greater than zero: how many times it goes " +
                    "round. Fractions are allowed.");
            if (a.Height is null)
                throw new ArgumentException(
                    "height is required: how far it rises over all its turns. Pass 0 for a flat " +
                    "spiral, which is what a helix of no height is.");
            var top = a.TopRadius ?? a.BaseRadius.Value;
            if (top <= 0) throw new ArgumentException("topRadius must be greater than zero.");

            var centre = AcadEnv.ToPoint3d(a.Center);
            var helix = new Helix();
            helix.SetDatabaseDefaults(db);
            helix.SetAxisPoint(centre, true);
            helix.StartPoint = new Point3d(centre.X + a.BaseRadius.Value, centre.Y, centre.Z);
            helix.BaseRadius = a.BaseRadius.Value;
            helix.TopRadius = top;
            // Height, Turns and TurnHeight are three views of one geometry, and TURN HEIGHT is
            // the one that drives the other two. Measured, after two wrong orders:
            //   Turns=5 then Height=300  ->  300 turns  (height / the default TurnHeight of 1)
            //   Height=300 then Turns=5  ->  height 5   (turns x that same TurnHeight of 1)
            // Setting the turn height first makes both of the others come out right, and the
            // read-back below is what caught each wrong order rather than shipping it.
            helix.TurnHeight = a.Turns.Value == 0 ? 0 : a.Height.Value / a.Turns.Value;
            helix.Turns = a.Turns.Value;
            helix.Twist = !(a.Clockwise ?? false);   // Twist true is counter-clockwise
            helix.CreateHelix();

            // Read back, because the interdependence above means an assignment can be quietly
            // overruled by the next one.
            if (Math.Abs(helix.Turns - a.Turns.Value) > 1e-6 ||
                Math.Abs(helix.Height - a.Height.Value) > 1e-6)
                throw new InvalidOperationException(
                    "The helix came out with " + helix.Turns + " turns over a height of " +
                    helix.Height + ", against the " + a.Turns + " and " + a.Height + " asked " +
                    "for. Height, turns and turn height are interdependent and one has " +
                    "overruled another, so this is not being reported as success.");

            var handle = AcadEnv.Persist(db, tr, helix, a.Layer);

            var length = helix.GetDistanceAtParameter(helix.EndParam) -
                         helix.GetDistanceAtParameter(helix.StartParam);

            // A constant-radius helix unrolls into a right triangle: the circumference walked
            // (2 pi r n) against the height climbed. Reported so the caller can check the curve
            // against that rather than against another call of this same code. It only holds
            // when the two radii agree - a tapered helix is a cone's spiral and longer sums are
            // needed - so the expectation is only offered when it applies.
            double? expected = Math.Abs(top - a.BaseRadius.Value) < 1e-9
                ? Math.Sqrt(Math.Pow(2 * Math.PI * a.BaseRadius.Value * a.Turns.Value, 2) +
                            Math.Pow(a.Height.Value, 2))
                : null;

            return Wrap(new
            {
                entity = handle,
                baseRadius = helix.BaseRadius,
                topRadius = helix.TopRadius,
                height = helix.Height,
                turns = helix.Turns,
                turnHeight = helix.TurnHeight,
                clockwise = !helix.Twist,
                length,
                expectedLength = expected,
                note = expected is not null
                    ? "A constant-radius helix unrolls into a right triangle - the " +
                      (2 * Math.PI * a.BaseRadius.Value * a.Turns.Value) + " walked round against " +
                      "the " + a.Height + " climbed - so its length should be " + expected +
                      " and measures " + length + ". Checkable arithmetic, not a second opinion " +
                      "from the same code."
                    : "expectedLength is only offered for a constant radius; this one tapers " +
                      "from " + a.BaseRadius + " to " + top + ", where the unrolled triangle no " +
                      "longer applies.",
            });
        });
}
