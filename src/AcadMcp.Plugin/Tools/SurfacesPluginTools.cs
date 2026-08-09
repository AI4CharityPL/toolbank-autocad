// AutoCAD plugin handlers for the acad-surfaces category.
// Registered under "acad.surfaces.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// A surface has AREA where a solid has volume, and that is what every tool here is checked
// against: extruding a curve of length L by a height h makes exactly L*h of surface, and
// revolving it a full turn about an axis at distance r makes 2*pi*r*L. A surface tool that
// silently made a solid, or made nothing, still returns a handle.

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
using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;
using DbSurface = Autodesk.AutoCAD.DatabaseServices.Surface;

namespace AcadMcp.Plugin.Tools;

internal static class SurfacesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.surfaces.extrude_surface",    ExtrudeSurface);
        host.Register("acad.surfaces.revolve_surface",    RevolveSurface);
        host.Register("acad.surfaces.sweep_surface",      SweepSurface);
        host.Register("acad.surfaces.offset_surface",     OffsetSurface);
        host.Register("acad.surfaces.convert_to_surface", ConvertToSurface);
        host.Register("acad.surfaces.convert_to_solid",   ConvertToSolid);
        host.Register("acad.surfaces.get_surface_info",   GetSurfaceInfo);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Entity Resolve(Database db, Transaction tr, string? handle, string argName,
                                  OpenMode mode = OpenMode.ForRead)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException(argName + " is required.");
        return (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
    }

    /// <summary>
    /// Total area of a surface, summed off its boundary representation.
    ///
    /// There is no Surface.Area property; the Brep faces carry it. This is the number every tool
    /// here is checked against, so it is read the same way in all of them rather than each
    /// reporting whatever its own creation call happened to hand back.
    /// </summary>
    private static double AreaOf(Entity ent)
    {
        double area = 0;
        try
        {
            using var brep = new Brep(ent);
            foreach (var face in brep.Faces)
            {
                try { area += face.GetArea(); } catch { /* one bad face must not hide the rest */ }
            }
        }
        catch (System.Exception ex)
        {
            throw new InvalidOperationException(
                "The result was created but its area could not be measured (" + ex.Message +
                "), which means its boundary is not readable and it is not a usable surface.");
        }
        return area;
    }

    private static double CurveLength(Curve c)
    {
        var a = c.GetDistanceAtParameter(c.StartParam);
        var b = c.GetDistanceAtParameter(c.EndParam);
        return Math.Abs(b - a);
    }

    /// <summary>Persist a new surface and refuse to report a success over an empty one.</summary>
    private static JsonObject Finish(Database db, Transaction tr, Entity made, string? layer,
                                     string what, object? extra = null)
    {
        var handle = AcadEnv.Persist(db, tr, made, layer);
        var area = AreaOf(made);
        if (area <= 1e-9)
            throw new InvalidOperationException(
                "The " + what + " came back with an area of " + area + ". AutoCAD returned an " +
                "object rather than an error, but a surface with no area is not a surface - " +
                "check that the profile is not degenerate and that the two inputs are not " +
                "coincident.");

        var result = new JsonObject
        {
            ["entity"] = JsonSerializer.SerializeToNode(handle, Opts),
            ["area"] = area,
            ["type"] = made.GetRXClass().Name,
        };
        if (extra is not null)
        {
            var node = JsonSerializer.SerializeToNode(extra, Opts) as JsonObject;
            if (node is not null)
                foreach (var kv in node) result[kv.Key] = kv.Value?.DeepClone();
        }
        return result;
    }

    // ─────────── making surfaces ───────────

    private static Task<ToolDispatchResult> ExtrudeSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.extrude_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<SurfaceExtrudeArgsDto>(args);
            if (a.Height is null || a.Height == 0)
                throw new ArgumentException(
                    "height is required and cannot be 0: how far to sweep the curve. Negative " +
                    "goes the other way along the direction.");
            var ent = Resolve(db, tr, a.Handle, "handle");
            if (ent is not Curve curve)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetRXClass().Name + ", not a curve. " +
                    "Extruding a surface sweeps a CURVE - an open one gives an open sheet, a " +
                    "closed one gives a tube. To extrude an AREA into a solid, that is " +
                    "geometry_3d.extrude_curve.");

            var len = CurveLength(curve);
            var dir = a.Direction is null
                ? Vector3d.ZAxis
                : AcadEnv.ToVector3d(a.Direction);
            if (dir.Length < 1e-12)
                throw new ArgumentException("direction cannot be the zero vector.");

            var sweep = new SweepOptionsBuilder
            {
                DraftAngle = (a.TaperAngleDeg ?? 0) * Math.PI / 180.0,
            };
            DbSurface srf;
            try
            {
                // Static factory returning a new Surface - NOT an instance method on one you
                // made yourself, which is how this was written first.
                srf = DbSurface.CreateExtrudedSurface(new Profile3d(curve),
                                                      dir.GetNormal() * a.Height.Value,
                                                      sweep.ToSweepOptions());
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the extrusion with " + ex.ErrorStatus + ". A taper steep " +
                    "enough to close the sheet on itself is refused, and so is a direction lying " +
                    "in the plane of a flat curve - that one sweeps the curve along itself and " +
                    "produces nothing.");
            }

            return Finish(db, tr, srf, a.Layer, "extruded surface", new
            {
                curveLength = len,
                height = Math.Abs(a.Height.Value),
                lengthTimesHeight = len * Math.Abs(a.Height.Value),
                taperAngleDeg = a.TaperAngleDeg ?? 0,
                note = "Sweeping a curve of length " + len + " through " +
                       Math.Abs(a.Height.Value) + " makes exactly " +
                       (len * Math.Abs(a.Height.Value)) + " of surface when the taper is zero, " +
                       "so the answer is checkable on paper. This makes a SHEET with no " +
                       "thickness and no volume; geometry_3d.extrude_curve is the one that makes " +
                       "a solid out of a closed profile.",
            });
        });

    private static Task<ToolDispatchResult> RevolveSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.revolve_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<SurfaceRevolveArgsDto>(args);
            if (a.AxisStart is null || a.AxisEnd is null)
                throw new ArgumentException(
                    "axisStart and axisEnd are required: the line the curve turns about. There " +
                    "is no default axis in 3D - a point does not name one.");
            var ent = Resolve(db, tr, a.Handle, "handle");
            if (ent is not Curve curve)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetRXClass().Name + ", not a curve.");

            var p0 = AcadEnv.ToPoint3d(a.AxisStart);
            var axis = AcadEnv.ToPoint3d(a.AxisEnd) - p0;
            if (axis.Length < 1e-12)
                throw new ArgumentException("axisStart and axisEnd are the same point.");

            var angleDeg = a.AngleDeg ?? 360.0;
            if (Math.Abs(angleDeg) < 1e-9)
                throw new ArgumentException("angleDeg cannot be 0 - nothing would be swept.");

            var len = CurveLength(curve);
            var opts = new RevolveOptionsBuilder();
            DbSurface srf;
            try
            {
                srf = DbSurface.CreateRevolvedSurface(new Profile3d(curve), p0, axis.GetNormal(),
                                                      angleDeg * Math.PI / 180.0, 0.0,
                                                      opts.ToRevolveOptions());
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the revolve with " + ex.ErrorStatus + ". A curve that CROSSES " +
                    "its own axis of revolution sweeps through itself and is refused; move the " +
                    "axis clear of the curve.");
            }

            // Pappus again, as for the swept solid: a curve of length L revolved a full turn
            // sweeps 2*pi*R*L, where R is the distance from the axis to the curve's CENTROID -
            // not to its nearest or farthest point. For a straight line parallel to the axis
            // those coincide and the check is exact.
            var mid = curve.GetPointAtParameter((curve.StartParam + curve.EndParam) / 2.0);
            var toMid = mid - p0;
            var radius = (toMid - axis.GetNormal() * toMid.DotProduct(axis.GetNormal())).Length;
            var pappus = 2.0 * Math.PI * radius * len * (Math.Abs(angleDeg) / 360.0);

            return Finish(db, tr, srf, a.Layer, "revolved surface", new
            {
                curveLength = len,
                angleDeg,
                radiusToMidpoint = radius,
                pappusArea = pappus,
                note = "Pappus: a curve of length " + len + " swept " + Math.Abs(angleDeg) +
                       " degrees about an axis " + radius + " away covers " + pappus + ". That " +
                       "holds exactly when the curve keeps a constant distance from the axis - a " +
                       "line parallel to it - and is an approximation otherwise, because the " +
                       "radius that matters is the one to the curve's centroid.",
            });
        });

    private static Task<ToolDispatchResult> SweepSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.sweep_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<SurfaceSweepArgsDto>(args);
            var profile = Resolve(db, tr, a.ProfileHandle, "profileHandle");
            var pathEnt = Resolve(db, tr, a.PathHandle, "pathHandle");
            if (profile is not Curve pc)
                throw new ArgumentException(
                    "The profile is a " + profile.GetRXClass().Name + ", not a curve.");
            if (pathEnt is not Curve path)
                throw new ArgumentException(
                    "The path is a " + pathEnt.GetRXClass().Name + ", not a curve.");
            if (string.Equals(a.ProfileHandle, a.PathHandle, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "The profile and the path are the same entity, so it would be swept along " +
                    "itself.");

            var profLen = CurveLength(pc);
            var pathLen = CurveLength(path);

            var sweep = new SweepOptionsBuilder
            {
                Align = SweepOptionsAlignOption.AlignSweepEntityToPath,
                Bank = a.Bank ?? true,
                TwistAngle = (a.TwistDeg ?? 0) * Math.PI / 180.0,
            };
            DbSurface srf;
            try
            {
                srf = DbSurface.CreateSweptSurface(new Profile3d(pc), new Profile3d(path),
                                                   sweep.ToSweepOptions());
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the sweep with " + ex.ErrorStatus + ". A path that bends " +
                    "tighter than the profile is wide makes the surface pass through itself.");
            }

            return Finish(db, tr, srf, a.Layer, "swept surface", new
            {
                profileLength = profLen,
                pathLength = pathLen,
                profileTimesPath = profLen * pathLen,
                note = "On a STRAIGHT path the area is exactly the profile length times the path " +
                       "length, here " + (profLen * pathLen) + ". On a bend it differs, and by " +
                       "the amount the inside of the turn loses against the outside - so a " +
                       "difference is not an error, but a wild one is.",
            });
        });

    private static Task<ToolDispatchResult> OffsetSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.offset_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<SurfaceOffsetArgsDto>(args);
            if (a.Distance is null || a.Distance == 0)
                throw new ArgumentException(
                    "distance is required and cannot be 0. The sign chooses which side of the " +
                    "surface the copy sits on.");
            var ent = Resolve(db, tr, a.Handle, "handle");
            if (ent is not DbSurface src)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetRXClass().Name + ", not a surface. " +
                    "To make a parallel copy of a flat CURVE, that is geometry_2d.offset_curve; " +
                    "to move the faces of a SOLID, geometry_3d.offset_face.");

            var areaBefore = AreaOf(src);
            DbSurface made;
            try
            {
                made = DbSurface.CreateOffsetSurface(src, a.Distance.Value) as DbSurface
                       ?? throw new InvalidOperationException(
                           "AutoCAD returned something that is not a surface.");
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the offset with " + ex.ErrorStatus + ". Offsetting a curved " +
                    "surface by more than its tightest radius of curvature folds it through " +
                    "itself, which is the usual cause.");
            }

            return Finish(db, tr, made, a.Layer, "offset surface", new
            {
                distance = a.Distance.Value,
                sourceArea = areaBefore,
                note = "A NEW surface parallel to the original, which is left alone. On a FLAT " +
                       "surface the offset is a translation and the area is unchanged - " +
                       areaBefore + " here. On a curved one the area changes, growing on the " +
                       "convex side and shrinking on the concave, which is the arithmetic worth " +
                       "checking if the result looks wrong.",
            });
        });

    // ─────────── converting between the two kinds ───────────

    private static Task<ToolDispatchResult> ConvertToSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.convert_to_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<ConvertArgsDto>(args);
            var ent = Resolve(db, tr, a.Handle, "handle", OpenMode.ForWrite);
            var wasVolume = ent is Solid3d s0 ? s0.MassProperties.Volume : (double?)null;
            var wasType = ent.GetRXClass().Name;

            DbSurface made;
            try
            {
                made = DbSurface.CreateFrom(ent)
                       ?? throw new InvalidOperationException("AutoCAD returned no surface.");
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to convert " + wasType + " with " + ex.ErrorStatus + ". A " +
                    "surface can be made from a solid, a region, or a closed planar curve; an " +
                    "open curve bounds nothing to become a surface.");
            }

            var handle = AcadEnv.Persist(db, tr, made, a.Layer);
            var area = AreaOf(made);
            if (a.EraseSource == true && !ent.IsErased) ent.Erase();

            return Wrap(new
            {
                entity = handle,
                area,
                wasType,
                type = made.GetRXClass().Name,
                sourceVolume = wasVolume,
                sourceErased = a.EraseSource == true,
                note = "A surface is a SHELL with no inside: converting a solid keeps its skin " +
                       "and throws the volume away" +
                       (wasVolume is null ? "" : " - " + wasVolume + " of it here") +
                       ". The area of the result should match the solid's surface area, which is " +
                       "the check. convert_to_solid goes back the other way, and only succeeds " +
                       "when the surfaces enclose a watertight space.",
            });
        });

    private static Task<ToolDispatchResult> ConvertToSolid(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.convert_to_solid", args, ct, (doc, db, tr) =>
        {
            var a = Read<ConvertArgsDto>(args);
            var ent = Resolve(db, tr, a.Handle, "handle", OpenMode.ForWrite);
            var wasType = ent.GetRXClass().Name;
            var areaBefore = ent is DbSurface ? AreaOf(ent) : (double?)null;

            // CreateFrom is an INSTANCE method here, unlike Surface.CreateFrom which is static:
            // you make an empty Solid3d and fill it from the entity.
            var made = new Solid3d();
            try
            {
                made.CreateFrom(ent);
            }
            catch (AcadRt.Exception ex)
            {
                made.Dispose();
                throw new ArgumentException(
                    "AutoCAD refused to convert " + wasType + " with " + ex.ErrorStatus + ". A " +
                    "solid can only be made from something that ENCLOSES a space: a closed mesh, " +
                    "a watertight set of surfaces, or a thickened region. An open sheet has no " +
                    "inside and cannot become a solid - that is not a limitation of this tool.");
            }

            var volume = made.MassProperties.Volume;
            if (volume <= 1e-9)
            {
                made.Dispose();
                throw new InvalidOperationException(
                    "The conversion returned a solid of volume " + volume + ". AutoCAD produced " +
                    "an object rather than an error, but a solid with no volume encloses nothing " +
                    "and the source was not watertight.");
            }

            var handle = AcadEnv.Persist(db, tr, made, a.Layer);
            if (a.EraseSource == true && !ent.IsErased) ent.Erase();

            return Wrap(new
            {
                entity = handle,
                volume,
                wasType,
                sourceArea = areaBefore,
                sourceErased = a.EraseSource == true,
                note = "The volume is " + volume + ", and a volume greater than zero is the whole " +
                       "proof that the source really did enclose a space - a conversion that " +
                       "quietly produced an empty solid would hand back just as valid a handle. " +
                       "convert_to_surface goes the other way and always works, because throwing " +
                       "the inside away needs no watertightness.",
            });
        });

    private static Task<ToolDispatchResult> GetSurfaceInfo(JsonObject args, CancellationToken ct) =>
        Run("acad.surfaces.get_surface_info", args, ct, (doc, db, tr) =>
        {
            var a = Read<ConvertArgsDto>(args);
            var ent = Resolve(db, tr, a.Handle, "handle");
            if (ent is not DbSurface srf)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetRXClass().Name + ", not a surface.");

            int faces = 0, edges = 0;
            using (var brep = new Brep(srf))
            {
                foreach (var _ in brep.Faces) faces++;
                foreach (var _ in brep.Edges) edges++;
            }

            bool planar = false;
            try { srf.GetPlane(); planar = true; } catch { planar = false; }

            var ext = srf.GeometricExtents;
            return Wrap(new
            {
                handle = a.Handle,
                type = srf.GetRXClass().Name,
                area = AreaOf(srf),
                faces,
                edges,
                isPlanar = planar,
                bbox = new
                {
                    min = AcadEnv.FromPoint3d(ext.MinPoint),
                    max = AcadEnv.FromPoint3d(ext.MaxPoint),
                },
                note = "The concrete TYPE is the useful part: a PlaneSurface, ExtrudedSurface, " +
                       "RevolvedSurface, SweptSurface, LoftedSurface or NurbSurface each accept " +
                       "different edits, and asking for one the surface does not support is the " +
                       "commonest failure in this category. isPlanar says whether the whole " +
                       "surface lies in one plane, which a flat trimmed NURBS also can.",
            });
        });
}
