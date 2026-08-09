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
using System.Linq;
using AcadRt = Autodesk.AutoCAD.Runtime;
using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;
using BrepEdge = Autodesk.AutoCAD.BoundaryRepresentation.Edge;
using BrepFace = Autodesk.AutoCAD.BoundaryRepresentation.Face;
using LoopType = Autodesk.AutoCAD.BoundaryRepresentation.LoopType;

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

        // roadmap 4.1 - cutting a solid, and finding where two overlap
        host.Register("acad.geometry3d.slice_solid",         SliceSolid);
        host.Register("acad.geometry3d.interfere_solids",    InterfereSolids);
        host.Register("acad.geometry3d.imprint_edges",       ImprintEdges);

        // roadmap 4.1 - the face/edge family. The two list_ tools come first because nothing
        // else here is usable without them: every SOLIDEDIT operation in the managed API takes
        // SubentityId[], and a SubentityId is not something a caller can spell.
        host.Register("acad.geometry3d.list_solid_edges",    ListSolidEdges);
        host.Register("acad.geometry3d.list_solid_faces",    ListSolidFaces);
        host.Register("acad.geometry3d.fillet_edge",         FilletEdge);
        host.Register("acad.geometry3d.chamfer_edge",        ChamferEdge);

        // roadmap 4.1 - the rest of SOLIDEDIT, all reachable now that a face can be named
        host.Register("acad.geometry3d.extrude_face",        ExtrudeFace);
        host.Register("acad.geometry3d.offset_face",         OffsetFace);
        host.Register("acad.geometry3d.move_face",           MoveFace);
        host.Register("acad.geometry3d.rotate_face",         RotateFace);
        host.Register("acad.geometry3d.taper_face",          TaperFace);
        host.Register("acad.geometry3d.delete_face",         DeleteFace);
        host.Register("acad.geometry3d.shell_solid",         ShellSolid);
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

    // ─────────── roadmap 4.1: cutting a solid, and finding where two overlap ───────────
    //
    // Both are checkable against arithmetic, which is why they are together. Cutting CONSERVES
    // volume: the two halves must add back up to what went in, and a slice that lost material or
    // double-counted it says so in that sum and nowhere else - both halves are perfectly good
    // solids either way. Interference between two boxes overlapping in a known region has a
    // volume you can work out on paper.

    /// <summary>A point as (x, y, z), for messages that have to name one.</summary>
    private static string Fmt3(Point3d p) =>
        "(" + p.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " +
        p.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " +
        p.Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";

    private static Task<ToolDispatchResult> SliceSolid(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.slice_solid", args, ct, (doc, db, tr) =>
        {
            var a = Read<SliceSolidArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle))
                throw new ArgumentException("handle is required: the solid to cut.");
            if (a.PlanePoint is null || a.PlaneNormal is null)
                throw new ArgumentException(
                    "planePoint and planeNormal are both required: a point the cutting plane " +
                    "passes through, and the direction it faces. The half the normal points " +
                    "TOWARDS is the one kept.");

            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle!), OpenMode.ForWrite);
            if (ent is not Solid3d solid)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetRXClass().Name + ", not a 3D solid.");

            var n = AcadEnv.ToPoint3d(a.PlaneNormal) - Point3d.Origin;
            if (n.Length < 1e-12)
                throw new ArgumentException(
                    "planeNormal is a zero vector, so it names no direction to cut along.");

            var volumeBefore = solid.MassProperties.Volume;
            var plane = new Plane(AcadEnv.ToPoint3d(a.PlanePoint), n.GetNormal());
            var keepBoth = a.KeepBoth ?? false;

            Solid3d? other = solid.Slice(plane, keepBoth);

            var kept = solid.MassProperties.Volume;
            if (kept <= 0)
                throw new InvalidOperationException(
                    "The kept half has no volume, so the plane lies beyond the solid on the far " +
                    "side of its normal. It measured " + volumeBefore + " before the cut.");

            // A plane that MISSES leaves the solid whole and returns no second half - and every
            // number in the result is then perfectly honest and completely useless: 125000
            // before, 125000 kept, and a note claiming the other half is gone when there never
            // was one. Caught here rather than reported as a cut, with the solid's own extents
            // so the caller can see where their plane should have been.
            if (other is null && Math.Abs(kept - volumeBefore) <= volumeBefore * 1e-9)
            {
                var ext = solid.GeometricExtents;
                throw new ArgumentException(
                    "The plane through " + Fmt3(AcadEnv.ToPoint3d(a.PlanePoint)) + " facing " +
                    Fmt3(new Point3d(n.X, n.Y, n.Z)) + " does not pass through the solid, so " +
                    "nothing was cut - it still measures " + volumeBefore + ". The solid spans " +
                    Fmt3(ext.MinPoint) + " to " + Fmt3(ext.MaxPoint) + ".");
            }

            EntityHandle? otherHandle = null;
            double? otherVolume = null;
            if (other is not null)
            {
                otherVolume = other.MassProperties.Volume;
                otherHandle = AcadEnv.Persist(db, tr, other, a.Layer ?? solid.Layer);
            }

            double? sum = otherVolume is null ? null : kept + otherVolume;
            if (sum is not null && Math.Abs(sum.Value - volumeBefore) > volumeBefore * 1e-6)
                throw new InvalidOperationException(
                    "The two halves add up to " + sum + " where the solid measured " +
                    volumeBefore + " before the cut. Slicing conserves volume, so this is not " +
                    "being reported as a successful cut.");

            return Wrap(new
            {
                handle = a.Handle,
                otherHalf = otherHandle,
                volumeBefore,
                keptVolume = kept,
                otherVolume,
                volumesSum = sum,
                keptBoth = keepBoth,
                note = keepBoth
                    ? "Both halves kept: the original handle is the side the normal points " +
                      "towards, otherHalf is the rest. They sum to " + sum + " against the " +
                      volumeBefore + " that went in, which is the check that the cut neither " +
                      "lost nor duplicated material."
                    : "Only the half the normal points towards was kept; the other is gone. Pass " +
                      "keepBoth to get it as a second solid, which also makes the volume sum " +
                      "checkable.",
            });
        });

    private static Task<ToolDispatchResult> InterfereSolids(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.interfere_solids", args, ct, (doc, db, tr) =>
        {
            var a = Read<InterfereArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle1) || string.IsNullOrWhiteSpace(a.Handle2))
                throw new ArgumentException("handle1 and handle2 are both required.");
            if (string.Equals(a.Handle1, a.Handle2, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "handle1 and handle2 are the same solid, which interferes with itself " +
                    "everywhere and tells you nothing.");

            var e1 = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle1!), OpenMode.ForRead);
            var e2 = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle2!), OpenMode.ForRead);
            if (e1 is not Solid3d s1)
                throw new ArgumentException(
                    "Entity " + a.Handle1 + " is a " + e1.GetRXClass().Name + ", not a 3D solid.");
            if (e2 is not Solid3d s2)
                throw new ArgumentException(
                    "Entity " + a.Handle2 + " is a " + e2.GetRXClass().Name + ", not a 3D solid.");

            var v1 = s1.MassProperties.Volume;
            var v2 = s2.MassProperties.Volume;
            var interferes = s1.CheckInterference(s2);

            EntityHandle? made = null;
            double? overlap = null;
            if (interferes && a.CreateSolid != false)
            {
                // Cloned first, and that is the whole difference from
                // boolean_ops.intersect_solids: THAT one replaces the target with the common
                // volume, while a clash check has to leave both parties standing and hand back
                // a third solid describing the overlap.
                var clone1 = (Solid3d)s1.Clone();
                using var clone2 = (Solid3d)s2.Clone();
                clone1.BooleanOperation(BooleanOperationType.BoolIntersect, clone2);
                overlap = clone1.MassProperties.Volume;
                if (overlap <= 0)
                {
                    clone1.Dispose();
                    overlap = null;
                }
                else
                {
                    made = AcadEnv.Persist(db, tr, clone1, a.Layer);
                }
            }

            // Read back. A clash check that consumed the geometry it was asked about would be
            // worse than useless, and a boolean on the wrong object would do exactly that.
            var after1 = s1.MassProperties.Volume;
            var after2 = s2.MassProperties.Volume;
            var intact = Math.Abs(after1 - v1) < 1e-9 && Math.Abs(after2 - v2) < 1e-9;
            if (!intact)
                throw new InvalidOperationException(
                    "The solids changed while being checked - " + v1 + " became " + after1 +
                    " and " + v2 + " became " + after2 + ". An interference check must leave " +
                    "both of them alone.");

            return Wrap(new
            {
                interferes,
                entity = made,
                interferenceVolume = overlap,
                volume1 = v1,
                volume2 = v2,
                originalsIntact = intact,
                note = interferes
                    ? "They clash, and BOTH originals are untouched - the difference from " +
                      "boolean_ops.intersect_solids, which replaces the target with the common " +
                      "volume. The clash comes back as a third solid of " + overlap + "."
                    : "They do not clash, so no interference solid was made. Both originals are " +
                      "untouched.",
            });
        });

    /// <summary>How many faces and edges a solid has, read off its boundary representation.</summary>
    private static (int Faces, int Edges) Topology(Solid3d solid)
    {
        using var brep = new Brep(solid);
        int f = 0, e = 0;
        foreach (var _ in brep.Faces) f++;
        foreach (var _ in brep.Edges) e++;
        return (f, e);
    }

    private static Task<ToolDispatchResult> ImprintEdges(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.imprint_edges", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImprintArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.SolidHandle) || string.IsNullOrWhiteSpace(a.CurveHandle))
                throw new ArgumentException(
                    "solidHandle and curveHandle are both required: the solid to imprint on, and " +
                    "the curve to press into its face.");

            var se = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.SolidHandle!), OpenMode.ForWrite);
            if (se is not Solid3d solid)
                throw new ArgumentException(
                    "Entity " + a.SolidHandle + " is a " + se.GetRXClass().Name + ", not a 3D solid.");
            var ce = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.CurveHandle!), OpenMode.ForWrite);

            var (f0, e0) = Topology(solid);
            var v0 = solid.MassProperties.Volume;

            try
            {
                solid.ImprintEntity(ce);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the imprint with " + ex.ErrorStatus + ". The curve has to " +
                    "LIE ON a face of the solid - imprinting is pressing a line into a surface, " +
                    "not cutting through the shape. A curve floating above the face, or crossing " +
                    "into the interior, has no face to be pressed into.");
            }

            var (f1, e1) = Topology(solid);
            var v1 = solid.MassProperties.Volume;

            // THE claim, and the one thing that separates an imprint from a cut: it adds edges,
            // not material. A tool that quietly subtracted the curve's swept area would report
            // more faces too, and only the volume would say so.
            if (Math.Abs(v1 - v0) > Math.Abs(v0) * 1e-9)
                throw new InvalidOperationException(
                    "The solid measured " + v0 + " before the imprint and " + v1 + " after. " +
                    "Imprinting divides a face; it never adds or removes material, so this is " +
                    "not being reported as an imprint.");

            if (f1 == f0 && e1 == e0)
                throw new ArgumentException(
                    "Nothing was imprinted: the solid still has " + f0 + " faces and " + e0 +
                    " edges. The curve did not meet any face - it has to lie ON one.");

            if (a.EraseSource == true) ce.Erase();

            return Wrap(new
            {
                handle = a.SolidHandle,
                facesBefore = f0,
                faces = f1,
                edgesBefore = e0,
                edges = e1,
                volumeBefore = v0,
                volume = v1,
                sourceErased = a.EraseSource == true,
                note = "An imprint divides a FACE and adds edges - " + f0 + " faces became " + f1 +
                       " and " + e0 + " edges became " + e1 + " - while the volume stays at " +
                       v1 + ". That last part is the check: a tool that cut instead of imprinting " +
                       "would also report more faces, and only the volume would give it away.",
            });
        });

    // ───────────────────────────────────────────────────────────────────────────────
    // The face/edge family: addressing
    //
    // Every SOLIDEDIT operation in the managed API - FilletEdges, ChamferEdges, ExtrudeFaces,
    // TaperFaces, OffsetFaces, RemoveFaces, TransformFaces, ShellBody - takes a SubentityId[].
    // A SubentityId is an opaque handle into the solid's boundary representation; a caller on
    // the other end of a JSON pipe cannot spell one and it would not survive the round trip. So
    // the scheme is: enumerate the Brep, hand back an INDEX plus the geometry of every slot, and
    // accept back either that index or a point in space to snap to. Reporting the geometry is
    // what makes the choice checkable - an index on its own is a number the caller must trust.
    //
    // Indexes are stable while the solid is unedited and NOT across an edit: filleting an edge
    // rebuilds the boundary and renumbers the rest. Both list tools say so in their own output.
    // ───────────────────────────────────────────────────────────────────────────────

    private readonly record struct EdgeSlot(
        int Index, SubentityId Id, Point3d Start, Point3d End, Point3d Mid, double Length,
        Point3d[] Samples);

    private readonly record struct FaceSlot(
        int Index, SubentityId Id, Point3d Centroid, Vector3d Normal, bool NormalKnown,
        int EdgeCount);

    /// <summary>Points along a curve, used both to report an edge and to snap a point to it.</summary>
    private static Point3d[] SampleCurve(Autodesk.AutoCAD.Geometry.Curve3d c, int n)
    {
        var iv = c.GetInterval();
        double lo = iv.LowerBound, hi = iv.UpperBound;
        var pts = new Point3d[n];
        for (int k = 0; k < n; k++)
            pts[k] = c.EvaluatePoint(lo + (hi - lo) * k / (double)(n - 1));
        return pts;
    }

    /// <summary>
    /// Runs one step of a Brep walk and, if it throws, says WHICH step and with what status.
    /// A bare "Exception of type BoundaryRepresentation.Exception was thrown" names neither, and
    /// six candidate causes cost six deploys to tell apart. Per-step attribution costs one.
    /// </summary>
    private static T Step<T>(string what, Func<T> f)
    {
        try { return f(); }
        catch (Autodesk.AutoCAD.BoundaryRepresentation.Exception ex)
        {
            throw new InvalidOperationException(
                "Reading the solid's boundary failed at [" + what + "] with " + ex.ErrorStatus + ".", ex);
        }
    }

    /// <summary>
    /// A Brep whose faces and edges know their own SubentityId.
    ///
    /// `new Brep(entity)` does NOT give you that: its faces and edges throw MissingSubentity the
    /// moment you ask for SubentityPath, and since every SOLIDEDIT operation needs exactly that,
    /// the entity constructor is useless for anything but counting. The path has to be rooted on
    /// the solid's ObjectId. Measured, not guessed - a step-attributed walk pinned the failure to
    /// `edge[0].SubentityPath.SubentId` with MissingSubentity while `new Brep(solid)` itself and
    /// the whole enumeration succeeded, which is why imprint_edges (which only counts) never hit it.
    /// </summary>
    private static Brep AddressableBrep(Solid3d solid)
    {
        var root = new SubentityId(SubentityType.Null, IntPtr.Zero);
        var path = new FullSubentityPath(new[] { solid.ObjectId }, root);
        return Step("new Brep(FullSubentityPath)", () => new Brep(path));
    }

    private static List<EdgeSlot> EdgeSlots(Solid3d solid)
    {
        using var brep = AddressableBrep(solid);
        var list = new List<EdgeSlot>();
        int i = 0;
        foreach (BrepEdge e in Step("brep.Edges", () => brep.Edges))
        {
            var idx = i;
            var c = Step($"edge[{idx}].Curve", () => e.Curve);
            var iv = Step($"edge[{idx}].Curve.GetInterval", () => c.GetInterval());
            var sp = Step($"edge[{idx}].Curve.StartPoint", () => c.StartPoint);
            var ep = Step($"edge[{idx}].Curve.EndPoint", () => c.EndPoint);
            double len;
            try { len = c.GetLength(iv.LowerBound, iv.UpperBound, 1e-9); }
            catch { len = sp.DistanceTo(ep); }
            var mid = Step($"edge[{idx}].Curve.EvaluatePoint",
                           () => c.EvaluatePoint((iv.LowerBound + iv.UpperBound) / 2.0));
            var samples = Step($"edge[{idx}] sampling", () => SampleCurve(c, 17));
            var sid = Step($"edge[{idx}].SubentityPath.SubentId", () => e.SubentityPath.SubentId);
            list.Add(new EdgeSlot(i++, sid, sp, ep, mid, len, samples));
        }
        return list;
    }

    private static List<FaceSlot> FaceSlots(Solid3d solid)
    {
        using var brep = AddressableBrep(solid);
        var list = new List<FaceSlot>();
        int i = 0;
        foreach (BrepFace f in Step("brep.Faces", () => brep.Faces))
        {
            var fi = i;
            // The normal comes from NEWELL'S METHOD over the ordered vertices of the face's
            // exterior loop, and the ordering is the whole point. Three arbitrary sampled points
            // give a plane but not a side: measured on a filleted box, that approach reported
            // (0,0,-1) twice and (0,0,1) never - the sign was noise. A Brep traverses a face's
            // exterior loop so that the material is on one consistent side, so the winding
            // carries the outward direction and Newell reads it off.
            //
            // The normal is then reported ONLY if the boundary is genuinely flat, checked by
            // sampling along every edge rather than at the corners: a fillet's quarter-cylinder
            // has corners that can be coplanar while the face between them is not. A curved face
            // has no single normal, and reporting a plausible one is worse than reporting none -
            // `facing` refuses when it cannot tell, instead of picking the wrong side.
            var pts = new List<Point3d>();
            var outer = new List<Point3d>();
            int edges = 0;
            foreach (var loop in Step($"face[{fi}].Loops", () => f.Loops))
            {
                var isOuter = Step($"face[{fi}] loop.LoopType",
                                   () => loop.LoopType) == LoopType.LoopExterior;
                foreach (var lv in Step($"face[{fi}] loop.Vertices", () => loop.Vertices))
                {
                    var vp = Step($"face[{fi}] loop vertex point", () => lv.Point);
                    if (isOuter) outer.Add(vp);
                }
                foreach (var le in Step($"face[{fi}] loop.Edges", () => loop.Edges))
                {
                    edges++;
                    pts.AddRange(Step($"face[{fi}] loop edge sampling", () => SampleCurve(le.Curve, 5)));
                }
            }

            var centroid = Point3d.Origin;
            if (pts.Count > 0)
            {
                double x = 0, y = 0, z = 0;
                foreach (var p in pts) { x += p.X; y += p.Y; z += p.Z; }
                centroid = new Point3d(x / pts.Count, y / pts.Count, z / pts.Count);
            }

            var normal = Vector3d.ZAxis;
            var known = false;
            if (outer.Count >= 3)
            {
                double nx = 0, ny = 0, nz = 0;
                for (int k = 0; k < outer.Count; k++)
                {
                    var p = outer[k];
                    var q = outer[(k + 1) % outer.Count];
                    nx += (p.Y - q.Y) * (p.Z + q.Z);
                    ny += (p.Z - q.Z) * (p.X + q.X);
                    nz += (p.X - q.X) * (p.Y + q.Y);
                }
                var n = new Vector3d(nx, ny, nz);
                if (n.Length > 1e-9)
                {
                    var cand = n.GetNormal();
                    // Flat only if EVERY sampled boundary point sits in the plane.
                    var scale = Math.Max(1.0, pts.Max(q => q.DistanceTo(centroid)));
                    if (pts.All(q => Math.Abs((q - centroid).DotProduct(cand)) <= scale * 1e-6))
                    {
                        normal = cand;
                        known = true;
                    }
                }
            }

            var fsid = Step($"face[{fi}].SubentityPath.SubentId", () => f.SubentityPath.SubentId);
            list.Add(new FaceSlot(i++, fsid, centroid, normal, known, edges));
        }
        return list;
    }

    private static double DistanceToEdge(in EdgeSlot e, Point3d p)
    {
        var best = double.MaxValue;
        foreach (var s in e.Samples) best = Math.Min(best, s.DistanceTo(p));
        return best;
    }

    private static List<EdgeSlot> PickEdges(List<EdgeSlot> all, IReadOnlyList<int>? indexes,
                                            IReadOnlyList<Point3dDto>? nearPoints)
    {
        var picked = new List<EdgeSlot>();
        if (indexes is not null)
            foreach (var ix in indexes)
            {
                if (ix < 0 || ix >= all.Count)
                    throw new ArgumentException(
                        "Edge index " + ix + " is out of range: this solid has " + all.Count +
                        " edges, numbered 0 to " + (all.Count - 1) +
                        ". Call list_solid_edges to see them with their positions.");
                picked.Add(all[ix]);
            }

        if (nearPoints is not null)
            foreach (var dto in nearPoints)
            {
                var p = AcadEnv.ToPoint3d(dto);
                var ordered = all.OrderBy(e => DistanceToEdge(e, p)).ToList();
                var d0 = DistanceToEdge(ordered[0], p);
                // A point equidistant from two edges names neither of them. Snapping silently to
                // whichever sorted first is how a caller ends up rounding the wrong corner.
                if (ordered.Count > 1 && Math.Abs(DistanceToEdge(ordered[1], p) - d0) < 1e-9)
                    throw new ArgumentException(
                        "The point " + Fmt3(p) + " is the same distance (" + d0 + ") from edge " +
                        ordered[0].Index + " and edge " + ordered[1].Index + ", so it names " +
                        "neither. Move it towards the middle of the edge you mean, or give " +
                        "edgeIndexes instead.");
                picked.Add(ordered[0]);
            }

        if (picked.Count == 0)
            throw new ArgumentException(
                "Name the edges to work on, either as edgeIndexes from list_solid_edges or as " +
                "nearPoints, each of which snaps to the edge closest to it.");

        var seen = new HashSet<int>();
        var unique = new List<EdgeSlot>();
        foreach (var e in picked) if (seen.Add(e.Index)) unique.Add(e);
        return unique;
    }

    /// <summary>Turn indexes, points and/or a direction into face slots, refusing the ambiguous.</summary>
    private static List<FaceSlot> PickFaces(List<FaceSlot> all, IReadOnlyList<int>? indexes,
                                            IReadOnlyList<Point3dDto>? nearPoints,
                                            Point3dDto? facing)
    {
        var picked = new List<FaceSlot>();
        if (indexes is not null)
            foreach (var ix in indexes)
            {
                if (ix < 0 || ix >= all.Count)
                    throw new ArgumentException(
                        "Face index " + ix + " is out of range: this solid has " + all.Count +
                        " faces, numbered 0 to " + (all.Count - 1) +
                        ". Call list_solid_faces to see them with their centroids and normals.");
                picked.Add(all[ix]);
            }

        if (nearPoints is not null)
            foreach (var dto in nearPoints)
            {
                var p = AcadEnv.ToPoint3d(dto);
                var ordered = all.OrderBy(f => f.Centroid.DistanceTo(p)).ToList();
                var d0 = ordered[0].Centroid.DistanceTo(p);
                if (ordered.Count > 1 && Math.Abs(ordered[1].Centroid.DistanceTo(p) - d0) < 1e-9)
                    throw new ArgumentException(
                        "The point " + Fmt3(p) + " is the same distance (" + d0 + ") from face " +
                        ordered[0].Index + " and face " + ordered[1].Index + ", so it names " +
                        "neither. Give faceIndexes, or a point clearly nearer the one you mean.");
                picked.Add(ordered[0]);
            }

        if (facing is not null)
        {
            // "The top face" without a list call first. A cube has exactly one face pointing at
            // +Z; a cylinder standing on end has one too. Where two tie - facing the corner of a
            // cube, say - the direction names neither and is refused rather than resolved.
            var v = AcadEnv.ToVector3d(facing);
            if (v.Length < 1e-12)
                throw new ArgumentException("facing cannot be the zero vector: it names no direction.");
            var n = v.GetNormal();
            var withNormals = all.Where(f => f.NormalKnown).ToList();
            if (withNormals.Count == 0)
                throw new ArgumentException(
                    "None of this solid's faces has a usable normal, so facing cannot pick one. " +
                    "Use faceIndexes from list_solid_faces.");
            var ordered = withNormals.OrderByDescending(f => f.Normal.DotProduct(n)).ToList();
            var best = ordered[0].Normal.DotProduct(n);
            if (ordered.Count > 1 && Math.Abs(ordered[1].Normal.DotProduct(n) - best) < 1e-9)
                throw new ArgumentException(
                    "The direction " + Fmt3(new Point3d(n.X, n.Y, n.Z)) + " points equally at " +
                    "face " + ordered[0].Index + " and face " + ordered[1].Index + ", so it names " +
                    "neither. Aim it squarely at the one you mean, or give faceIndexes.");
            picked.Add(ordered[0]);
        }

        if (picked.Count == 0)
            throw new ArgumentException(
                "Name the faces to work on: faceIndexes from list_solid_faces, nearPoints which " +
                "snap to the nearest face, or facing which picks the face pointing that way.");

        var seen = new HashSet<int>();
        var unique = new List<FaceSlot>();
        foreach (var f in picked) if (seen.Add(f.Index)) unique.Add(f);
        return unique;
    }

    private static object Describe(in FaceSlot f) => new
    {
        index = f.Index,
        centroid = AcadEnv.FromPoint3d(f.Centroid),
        normal = f.NormalKnown
            ? AcadEnv.FromPoint3d(new Point3d(f.Normal.X, f.Normal.Y, f.Normal.Z)) : null,
        edgeCount = f.EdgeCount,
    };

    /// <summary>
    /// Shared tail for every face operation: apply, measure, and refuse to call it a success when
    /// nothing moved. Each of these can be handed a value that AutoCAD accepts and quietly
    /// ignores, and every one of them then returns a healthy result over an unchanged solid.
    /// </summary>
    private static JsonObject FaceOp(
        Database db, Transaction tr, FaceOpArgsDto a, string verb, string what,
        Func<Solid3d, SubentityId[], JsonObject?> apply)
    {
        var solid = RequireSolid(db, tr, a.Handle, "handle");
        var picked = PickFaces(FaceSlots(solid), a.FaceIndexes, a.NearPoints, a.Facing);
        var v0 = solid.MassProperties.Volume;
        var (f0, e0) = Topology(solid);

        JsonObject? extra;
        try
        {
            extra = apply(solid, picked.Select(p => p.Id).ToArray());
        }
        catch (AcadRt.Exception ex)
        {
            throw new ArgumentException(
                "AutoCAD refused to " + verb + " with " + ex.ErrorStatus + ". " + what);
        }

        var v1 = solid.MassProperties.Volume;
        var (f1, e1) = Topology(solid);

        if (f1 == f0 && e1 == e0 && Math.Abs(v1 - v0) <= Math.Abs(v0) * 1e-12)
            throw new InvalidOperationException(
                "Nothing changed: the solid still has " + f0 + " faces, " + e0 + " edges and the " +
                "same volume, " + v0 + ". The faces named were accepted but the operation had no " +
                "effect, which AutoCAD reports as success.");

        var result = new JsonObject
        {
            ["handle"] = a.Handle,
            ["facesAffected"] = picked.Count,
            ["faces"] = JsonSerializer.SerializeToNode(picked.Select(f => Describe(f)).ToArray(), Opts),
            ["facesBefore"] = f0,
            ["faceCount"] = f1,
            ["volumeBefore"] = v0,
            ["volume"] = v1,
            ["volumeChange"] = v1 - v0,
        };
        if (extra is not null)
            foreach (var kv in extra) result[kv.Key] = kv.Value?.DeepClone();
        return result;
    }

    private static object Describe(in EdgeSlot e) => new
    {
        index = e.Index,
        start = AcadEnv.FromPoint3d(e.Start),
        end = AcadEnv.FromPoint3d(e.End),
        midpoint = AcadEnv.FromPoint3d(e.Mid),
        length = e.Length,
    };

    /// <summary>
    /// The largest radius (or chamfer distance) these edges take without destroying a face,
    /// found by bisection on THROWAWAY CLONES.
    ///
    /// This exists because AutoCAD does not refuse an oversized fillet. Asked for radius 300 on a
    /// 100 face it rounds happily, swallows a whole face, and returns success - measured, on a
    /// pristine cube: six faces went to five and the volume dropped by a third. A tool that only
    /// said "too large, try smaller" would leave the caller guessing at a number the geometry
    /// already determines, so this searches for it.
    ///
    /// The clones are appended inside the caller's transaction and the caller then THROWS, which
    /// skips tr.Commit() and aborts everything - the clones never existed and the real solid is
    /// untouched. That rollback is asserted in the verification, not assumed.
    /// </summary>
    private static double? LargestSafeSize(
        Database db, Transaction tr, Solid3d original, IReadOnlyList<int> edgeIndexes,
        double requested, Func<Solid3d, SubentityId[], double, bool> apply)
    {
        var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        var reference = EdgeSlots(original);

        bool Fits(double size)
        {
            Solid3d? clone = null;
            try
            {
                clone = (Solid3d)original.Clone();
                ms.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);

                var slots = EdgeSlots(clone);
                // The index -> edge mapping on the clone must be the SAME mapping, or the search
                // is measuring a different edge and its answer is worse than no answer.
                if (slots.Count != reference.Count) return false;
                foreach (var ix in edgeIndexes)
                    if (slots[ix].Mid.DistanceTo(reference[ix].Mid) > 1e-9) return false;

                var (f0, _) = Topology(clone);
                if (!apply(clone, edgeIndexes.Select(ix => slots[ix].Id).ToArray(), size)) return false;
                var (f1, _) = Topology(clone);
                return f1 >= f0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (clone is not null && !clone.IsErased) clone.Erase();
            }
        }

        if (!Fits(requested * 1e-4)) return null;      // nothing works; no number worth reporting

        double lo = requested * 1e-4, hi = requested;
        for (int k = 0; k < 14 && hi - lo > requested * 1e-4; k++)
        {
            var mid = (lo + hi) / 2.0;
            if (Fits(mid)) lo = mid; else hi = mid;
        }
        return lo;
    }

    private static string TooLarge(string what, double requested, double? largest, int facesBefore,
                                   int facesAfter, string allowFlag)
    {
        var limit = largest is null
            ? "No size was found that leaves the faces intact, so this edge may not take " + what +
              " at all."
            : "The largest that leaves every face standing is about " + Math.Round(largest.Value, 4) +
              ", found by bisection on throwaway copies of this solid.";
        return "Refused: " + what + " " + requested + " is large enough to DESTROY faces - the " +
               "solid would go from " + facesBefore + " faces down to " + facesAfter + ". " + limit +
               " AutoCAD itself accepts this without complaint and returns a success, which is " +
               "why it is caught here. The solid has been left exactly as it was. Pass " +
               allowFlag + " if reshaping the part this way is genuinely what you want.";
    }

    private static Solid3d RequireSolid(Database db, Transaction tr, string? handle, string argName)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException(argName + " is required.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), OpenMode.ForWrite);
        if (ent is not Solid3d s)
            throw new ArgumentException(
                "Entity " + handle + " is a " + ent.GetRXClass().Name + ", not a 3D solid.");
        return s;
    }

    private static Task<ToolDispatchResult> ListSolidEdges(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.list_solid_edges", args, ct, (doc, db, tr) =>
        {
            var a = Read<SolidQueryArgsDto>(args);
            var slots = EdgeSlots(RequireSolid(db, tr, a.Handle, "handle"));
            return Wrap(new
            {
                handle = a.Handle,
                count = slots.Count,
                edges = slots.Select(e => Describe(e)).ToArray(),
                note = "Indexes address edges for fillet_edge and chamfer_edge. They are stable " +
                       "only while the solid is unedited - filleting one edge rebuilds the " +
                       "boundary and renumbers the rest, so list again after every edit. Each " +
                       "edge comes with its endpoints and midpoint so the index can be checked " +
                       "against the geometry rather than trusted.",
            });
        });

    private static Task<ToolDispatchResult> ListSolidFaces(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.list_solid_faces", args, ct, (doc, db, tr) =>
        {
            var a = Read<SolidQueryArgsDto>(args);
            var slots = FaceSlots(RequireSolid(db, tr, a.Handle, "handle"));
            return Wrap(new
            {
                handle = a.Handle,
                count = slots.Count,
                faces = slots.Select(f => new
                {
                    index = f.Index,
                    centroid = AcadEnv.FromPoint3d(f.Centroid),
                    normal = f.NormalKnown ? AcadEnv.FromPoint3d(new Point3d(f.Normal.X, f.Normal.Y, f.Normal.Z)) : null,
                    edgeCount = f.EdgeCount,
                }).ToArray(),
                note = "The centroid is the average of points sampled round the face boundary, " +
                       "so on a face with a hole in it the centroid can fall inside the hole - " +
                       "it LOCATES the face, it is not a point guaranteed to lie on it. The " +
                       "normal comes from three sampled boundary points and is omitted when the " +
                       "boundary is degenerate. Indexes are stable only while the solid is " +
                       "unedited.",
            });
        });

    // ─────────── the face operations ───────────

    private static Task<ToolDispatchResult> ExtrudeFace(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.extrude_face", args, ct, (doc, db, tr) =>
        {
            var a = Read<FaceOpArgsDto>(args);
            if (a.Distance is null && a.PathHandle is null)
                throw new ArgumentException(
                    "Give either distance - how far to push the face along its own normal - or " +
                    "pathHandle, a curve to push it along.");
            if (a.Distance is not null && a.PathHandle is not null)
                throw new ArgumentException(
                    "distance and pathHandle are alternatives: one pushes the face straight along " +
                    "its normal, the other follows a curve. Give one.");

            var r = FaceOp(db, tr, a, "extrude those faces",
                "A face cannot be pushed so far that the solid turns inside out, and a taper " +
                "steep enough to close the extrusion off is refused too.",
                (solid, ids) =>
                {
                    if (a.PathHandle is not null)
                    {
                        var pe = (Entity)tr.GetObject(
                            AcadEnv.ResolveHandle(db, a.PathHandle), OpenMode.ForRead);
                        if (pe is not Curve path)
                            throw new ArgumentException(
                                "The path is a " + pe.GetRXClass().Name + ", not a curve.");
                        solid.ExtrudeFacesAlongPath(ids, path);
                        return new JsonObject { ["alongPath"] = a.PathHandle };
                    }
                    solid.ExtrudeFaces(ids, a.Distance!.Value, (a.TaperAngleDeg ?? 0) * Math.PI / 180.0);
                    return new JsonObject
                    {
                        ["distance"] = a.Distance.Value,
                        ["taperAngleDeg"] = a.TaperAngleDeg ?? 0,
                    };
                });
            r["note"] = "Pushing a flat face of area A straight out by d adds exactly A*d of " +
                        "material, so the volume change is checkable on paper whenever the face " +
                        "is planar and the taper is zero. A negative distance pushes INWARD and " +
                        "hollows the solid out instead. The face and edge indexes have now " +
                        "changed; list them again before the next edit.";
            return r;
        });

    private static Task<ToolDispatchResult> OffsetFace(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.offset_face", args, ct, (doc, db, tr) =>
        {
            if (Read<FaceOpArgsDto>(args).Distance is null)
                throw new ArgumentException(
                    "distance is required: how far to move each face along its own normal. " +
                    "Positive grows the solid, negative shrinks it.");
            var a = Read<FaceOpArgsDto>(args);
            var r = FaceOp(db, tr, a, "offset those faces",
                "Offsetting inward by more than the solid is thick would turn it inside out.",
                (solid, ids) =>
                {
                    solid.OffsetFaces(ids, a.Distance!.Value);
                    return new JsonObject { ["distance"] = a.Distance.Value };
                });
            r["note"] = "Offset moves each face along its OWN normal, so offsetting all six faces " +
                        "of a 100 cube by 10 gives a 120 cube, not a 110 one - the growth happens " +
                        "on both sides of every axis. This is the difference from move_face, " +
                        "which moves faces in one direction you choose. The face and edge indexes " +
                        "have now changed; list them again before the next edit.";
            return r;
        });

    private static Task<ToolDispatchResult> MoveFace(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.move_face", args, ct, (doc, db, tr) =>
        {
            var a = Read<FaceOpArgsDto>(args);
            if (a.From is null || a.To is null)
                throw new ArgumentException(
                    "from and to are required: the displacement to move the faces by, given as " +
                    "two points exactly like modify.move.");
            var v = AcadEnv.ToPoint3d(a.To) - AcadEnv.ToPoint3d(a.From);
            if (v.Length < 1e-12)
                throw new ArgumentException("from and to are the same point, so nothing would move.");

            var r = FaceOp(db, tr, a, "move those faces",
                "A face cannot be moved through the far side of the solid.",
                (solid, ids) =>
                {
                    solid.TransformFaces(ids, Matrix3d.Displacement(v));
                    return new JsonObject { ["distance"] = v.Length };
                });
            r["note"] = "Moving one flat face of a box straight out by d adds exactly (its area)*d, " +
                        "the same arithmetic as extrude_face. The difference is direction: move " +
                        "takes a displacement you choose, offset_face always follows each face's " +
                        "own normal. The face and edge indexes have now changed.";
            return r;
        });

    private static Task<ToolDispatchResult> RotateFace(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.rotate_face", args, ct, (doc, db, tr) =>
        {
            var a = Read<FaceOpArgsDto>(args);
            if (a.AngleDeg is null)
                throw new ArgumentException("angleDeg is required: how far to turn the faces.");
            if (a.AxisStart is null || a.AxisEnd is null)
                throw new ArgumentException(
                    "axisStart and axisEnd are required: the line the faces turn about. Unlike a " +
                    "2D rotation there is no default axis in 3D - a point does not name one.");
            var p0 = AcadEnv.ToPoint3d(a.AxisStart);
            var axis = AcadEnv.ToPoint3d(a.AxisEnd) - p0;
            if (axis.Length < 1e-12)
                throw new ArgumentException(
                    "axisStart and axisEnd are the same point, so they name no axis.");

            var r = FaceOp(db, tr, a, "rotate those faces",
                "A face cannot be turned so far that it passes through the rest of the solid.",
                (solid, ids) =>
                {
                    solid.TransformFaces(ids,
                        Matrix3d.Rotation(a.AngleDeg!.Value * Math.PI / 180.0, axis.GetNormal(), p0));
                    return new JsonObject { ["angleDeg"] = a.AngleDeg.Value };
                });
            r["note"] = "Angles are degrees, counter-clockwise looking down the axis from " +
                        "axisEnd towards axisStart. Tilting a face is how a wedge or a sloping " +
                        "roof is made from a box. The face and edge indexes have now changed.";
            return r;
        });

    private static Task<ToolDispatchResult> TaperFace(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.taper_face", args, ct, (doc, db, tr) =>
        {
            var a = Read<FaceOpArgsDto>(args);
            if (a.AngleDeg is null)
                throw new ArgumentException(
                    "angleDeg is required: the draft angle. This is the taper a moulded or cast " +
                    "part needs so it can be pulled from its mould.");
            if (a.BasePoint is null || a.Direction is null)
                throw new ArgumentException(
                    "basePoint and direction are required. The taper pivots about the base point " +
                    "and leans along the direction - the face stays put where it crosses that " +
                    "point and swings further the further from it you go, so those two together " +
                    "decide which end grows and which shrinks.");
            var dir = AcadEnv.ToVector3d(a.Direction);
            if (dir.Length < 1e-12)
                throw new ArgumentException("direction cannot be the zero vector.");

            var r = FaceOp(db, tr, a, "taper those faces",
                "A draft angle steep enough to fold the face through itself is refused.",
                (solid, ids) =>
                {
                    solid.TaperFaces(ids, AcadEnv.ToPoint3d(a.BasePoint), dir.GetNormal(),
                                     a.AngleDeg!.Value * Math.PI / 180.0);
                    return new JsonObject { ["angleDeg"] = a.AngleDeg.Value };
                });
            r["note"] = "Draft angle in degrees. Tapering the four sides of a box about its base " +
                        "turns it into a frustum, which is the usual reason to reach for this. " +
                        "The face and edge indexes have now changed.";
            return r;
        });

    private static Task<ToolDispatchResult> DeleteFace(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.delete_face", args, ct, (doc, db, tr) =>
        {
            var a = Read<FaceOpArgsDto>(args);
            var r = FaceOp(db, tr, a, "delete those faces",
                "A face can only go if the faces around it can be grown back together to close " +
                "the gap. That works for a feature added to a shape - a fillet, a chamfer, a " +
                "boss - and not for a face of the shape itself: deleting one side of a box would " +
                "leave it open, and a solid cannot be open.",
                (solid, ids) => { solid.RemoveFaces(ids); return null; });
            r["note"] = "This is how a FEATURE is removed rather than a shape edited: delete the " +
                        "curved face a fillet added and the sharp edge comes back, and the volume " +
                        "returns to exactly what it was before the fillet - which is the check " +
                        "worth making, since a partial removal still leaves a valid solid.";
            return r;
        });

    private static Task<ToolDispatchResult> ShellSolid(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.shell_solid", args, ct, (doc, db, tr) =>
        {
            var a = Read<FaceOpArgsDto>(args);
            if (a.Thickness is null || a.Thickness == 0)
                throw new ArgumentException(
                    "thickness is required and cannot be 0. NEGATIVE keeps the wall inside the " +
                    "original surface, so the part stays the size it was; POSITIVE grows the wall " +
                    "outward and the part gets bigger in every direction. Measured on a 100 cube " +
                    "open at the top: -10 gives 424000, +10 gives 584000.");

            var solid = RequireSolid(db, tr, a.Handle, "handle");
            var all = FaceSlots(solid);
            // MEASURED: ShellBody throws IndexOutOfRangeException on an empty SubentityId[]. The
            // first version of this tool advertised "name no faces and the void is sealed inside",
            // which read well and does not work - AutoCAD will not shell without an opening.
            var opened = PickFaces(all, a.FaceIndexes, a.NearPoints, a.Facing);

            var v0 = solid.MassProperties.Volume;
            var (f0, _) = Topology(solid);
            try
            {
                solid.ShellBody(opened.Select(f => f.Id).ToArray(), a.Thickness.Value);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the shell with " + ex.ErrorStatus + ". A wall thicker than " +
                    "half the narrowest part of the solid has nowhere to go - the inner surface " +
                    "would pass through itself. The thickness asked for was " + a.Thickness.Value + ".");
            }
            var v1 = solid.MassProperties.Volume;
            var (f1, _) = Topology(solid);

            if (Math.Abs(v1 - v0) <= Math.Abs(v0) * 1e-12)
                throw new InvalidOperationException(
                    "Nothing was hollowed: the solid still measures " + v0 + ". A shell always " +
                    "removes material, so an unchanged volume means it did not happen.");

            return Wrap(new
            {
                handle = a.Handle,
                thickness = a.Thickness.Value,
                openFaces = opened.Count,
                faces = opened.Select(f => Describe(f)).ToArray(),
                facesBefore = f0,
                faceCount = f1,
                volumeBefore = v0,
                volume = v1,
                volumeRemoved = v0 - v1,
                note = "Shelling hollows the solid out to a wall of the given thickness. The faces " +
                       "named are the ones LEFT OPEN - name the top of a box and you get a box " +
                       "with no lid. At least one face is required: AutoCAD will not shell a " +
                       "solid with no opening at all, so a sealed void has to be made by " +
                       "subtracting an inner solid with boolean_ops.subtract_solids. The SIGN of the thickness is the thing to get " +
                       "right: negative hollows INWARD and leaves the part the size it was, " +
                       "positive grows the wall OUTWARD and makes it bigger. On a 100 cube open " +
                       "at the top, -10 leaves a cavity of 80 x 80 x 90 and comes to 424000, " +
                       "while +10 grows the outside to 120 x 120 x 110 and comes to 584000. Both " +
                       "are valid shells and only the sign tells them apart, so check the volume " +
                       "against the one that was wanted.",
            });
        });

    private static Task<ToolDispatchResult> FilletEdge(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.fillet_edge", args, ct, (doc, db, tr) =>
        {
            var a = Read<EdgeOpArgsDto>(args);
            if (a.Radius is null || a.Radius <= 0)
                throw new ArgumentException("radius is required and must be greater than 0.");
            var solid = RequireSolid(db, tr, a.Handle, "handle");

            var picked = PickEdges(EdgeSlots(solid), a.EdgeIndexes, a.NearPoints);
            var v0 = solid.MassProperties.Volume;
            var (f0, _) = Topology(solid);

            var radii = new DoubleCollection();
            var startSetback = new DoubleCollection();
            var endSetback = new DoubleCollection();
            foreach (var _ in picked)
            {
                radii.Add(a.Radius.Value);
                startSetback.Add(0.0);
                endSetback.Add(0.0);
            }

            try
            {
                solid.FilletEdges(picked.Select(p => p.Id).ToArray(), radii, startSetback, endSetback);
            }
            catch (AcadRt.Exception ex)
            {
                var shortest = picked.OrderBy(p => p.Length).First();
                throw new ArgumentException(
                    "AutoCAD refused the fillet with " + ex.ErrorStatus + ". The usual cause is a " +
                    "radius too large for the geometry: the shortest edge selected is " +
                    shortest.Length + " long and the radius asked for is " + a.Radius.Value +
                    ". A fillet needs room on BOTH faces meeting at the edge, so the limit is " +
                    "usually well under the edge length.");
            }

            var v1 = solid.MassProperties.Volume;
            var (f1, _) = Topology(solid);

            // A fillet that did nothing leaves a perfectly good solid and a success code behind
            // it. Rounding an edge REPLACES that edge with a curved face, so the face count is
            // what says otherwise.
            if (f1 == f0 && Math.Abs(v1 - v0) <= Math.Abs(v0) * 1e-12)
                throw new InvalidOperationException(
                    "Nothing was rounded: the solid still has " + f0 + " faces and the same " +
                    "volume, " + v0 + ". The edges named were accepted but produced no fillet.");

            // A fillet big enough to swallow a face is almost never what was meant, and AutoCAD
            // will not say so. Throwing here aborts the transaction, which puts the solid back.
            if (f1 < f0 && a.AllowFaceLoss != true)
            {
                var largest = LargestSafeSize(db, tr, solid, picked.Select(p => p.Index).ToList(),
                    a.Radius.Value, (c, ids, size) =>
                    {
                        var rr = new DoubleCollection();
                        var s1 = new DoubleCollection();
                        var s2 = new DoubleCollection();
                        foreach (var _ in ids) { rr.Add(size); s1.Add(0.0); s2.Add(0.0); }
                        c.FilletEdges(ids, rr, s1, s2);
                        return true;
                    });
                throw new ArgumentException(
                    TooLarge("a fillet radius of", a.Radius.Value, largest, f0, f1, "allowFaceLoss: true"));
            }

            return Wrap(new
            {
                handle = a.Handle,
                edgesFilleted = picked.Count,
                edges = picked.Select(e => Describe(e)).ToArray(),
                radius = a.Radius.Value,
                facesBefore = f0,
                faces = f1,
                volumeBefore = v0,
                volume = v1,
                volumeRemoved = v0 - v1,
                facesConsumed = f1 < f0,
                note = "Rounding a convex edge REMOVES material - " + (v0 - v1) + " of it here - " +
                       "and replaces the edge with a curved face, which is why the face count " +
                       "went from " + f0 + " to " + f1 + ". On a straight edge of length L the " +
                       "amount removed is exactly L*r*r*(1 - pi/4), but only while the fillet " +
                       "FITS INSIDE both faces meeting at the edge; past that the arc runs off " +
                       "them and the shape is no longer that prism. " +
                       (f1 < f0
                           ? "WARNING: allowFaceLoss was set and this fillet CONSUMED faces - the " +
                             "solid went from " + f0 + " faces down to " + f1 + ". Without that " +
                             "flag it would have been refused, because AutoCAD accepts an " +
                             "oversized radius without complaint and a mistyped one reshapes the " +
                             "part while still returning success. "
                           : "") +
                       "The edge indexes have now CHANGED; call list_solid_edges again before " +
                       "the next edit.",
            });
        });

    private static Task<ToolDispatchResult> ChamferEdge(JsonObject args, CancellationToken ct) =>
        Run("acad.geometry3d.chamfer_edge", args, ct, (doc, db, tr) =>
        {
            var a = Read<EdgeOpArgsDto>(args);
            var baseDist = a.Distance ?? a.Radius;
            if (baseDist is null || baseDist <= 0)
                throw new ArgumentException("distance is required and must be greater than 0.");
            var otherDist = a.Distance2 ?? baseDist.Value;
            if (otherDist <= 0) throw new ArgumentException("distance2 must be greater than 0.");

            var solid = RequireSolid(db, tr, a.Handle, "handle");
            var picked = PickEdges(EdgeSlots(solid), a.EdgeIndexes, a.NearPoints);

            // ChamferEdges wants a BASE FACE as well as the edges: the two distances are measured
            // from the edge across each of the two faces meeting there, and the base face says
            // which distance goes where. With equal distances that cannot matter, which is why it
            // is optional; with unequal ones it decides which way the bevel leans, and guessing
            // it would silently produce a mirror image of what was asked for.
            var faces = FaceSlots(solid);
            FaceSlot chosen;
            if (a.BaseFaceIndex is not null)
            {
                var ix = a.BaseFaceIndex.Value;
                if (ix < 0 || ix >= faces.Count)
                    throw new ArgumentException(
                        "baseFaceIndex " + ix + " is out of range: this solid has " + faces.Count +
                        " faces, numbered 0 to " + (faces.Count - 1) +
                        ". Call list_solid_faces to see them.");
                chosen = faces[ix];
            }
            else
            {
                if (Math.Abs(baseDist.Value - otherDist) > 1e-12)
                    throw new ArgumentException(
                        "distance and distance2 differ (" + baseDist.Value + " vs " + otherDist +
                        "), so which face the first distance is measured across decides which way " +
                        "the bevel leans. Give baseFaceIndex explicitly - list_solid_faces reports " +
                        "every face with its centroid and normal.");
                var target = picked[0].Mid;
                chosen = faces.OrderBy(f => f.Centroid.DistanceTo(target)).First();
            }

            var v0 = solid.MassProperties.Volume;
            var (f0, _) = Topology(solid);

            try
            {
                solid.ChamferEdges(picked.Select(p => p.Id).ToArray(), chosen.Id,
                                   baseDist.Value, otherDist);
            }
            catch (AcadRt.Exception ex)
            {
                var shortest = picked.OrderBy(p => p.Length).First();
                throw new ArgumentException(
                    "AutoCAD refused the chamfer with " + ex.ErrorStatus + ". The usual cause is a " +
                    "distance too large for the geometry: the shortest edge selected is " +
                    shortest.Length + " long and the distances asked for are " + baseDist.Value +
                    " and " + otherDist + ". The bevel has to fit on both faces meeting at the " +
                    "edge without running off the far side of either.");
            }

            var v1 = solid.MassProperties.Volume;
            var (f1, _) = Topology(solid);

            if (f1 == f0 && Math.Abs(v1 - v0) <= Math.Abs(v0) * 1e-12)
                throw new InvalidOperationException(
                    "Nothing was bevelled: the solid still has " + f0 + " faces and the same " +
                    "volume, " + v0 + ".");

            if (f1 < f0 && a.AllowFaceLoss != true)
            {
                var ratio = otherDist / baseDist.Value;
                var largest = LargestSafeSize(db, tr, solid, picked.Select(p => p.Index).ToList(),
                    baseDist.Value, (c, ids, size) =>
                    {
                        // The search scales BOTH distances together, so an asymmetric bevel keeps
                        // its shape while shrinking - a number found by moving only one of them
                        // would describe a different chamfer from the one asked for.
                        var cf = FaceSlots(c);
                        if (chosen.Index >= cf.Count) return false;
                        c.ChamferEdges(ids, cf[chosen.Index].Id, size, size * ratio);
                        return true;
                    });
                throw new ArgumentException(
                    TooLarge("a chamfer distance of", baseDist.Value, largest, f0, f1,
                             "allowFaceLoss: true"));
            }

            return Wrap(new
            {
                handle = a.Handle,
                edgesChamfered = picked.Count,
                edges = picked.Select(e => Describe(e)).ToArray(),
                distance = baseDist.Value,
                distance2 = otherDist,
                baseFaceIndex = chosen.Index,
                baseFaceCentroid = AcadEnv.FromPoint3d(chosen.Centroid),
                facesBefore = f0,
                faces = f1,
                volumeBefore = v0,
                volume = v1,
                volumeRemoved = v0 - v1,
                facesConsumed = f1 < f0,
                note = "A chamfer cuts a flat bevel where a fillet rounds. On a straight edge of " +
                       "length L with equal distances d the amount removed is exactly L*d*d/2 - " +
                       (v0 - v1) + " here - which is MORE than a fillet of radius d takes, " +
                       "because the fillet keeps the material inside the arc. The edge indexes " +
                       "have now CHANGED; call list_solid_edges again before the next edit.",
            });
        });
}
