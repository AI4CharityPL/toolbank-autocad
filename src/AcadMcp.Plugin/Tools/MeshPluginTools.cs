// AutoCAD plugin handlers for the acad-mesh category.
// Registered under "acad.mesh.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// A mesh is a CAGE of flat faces that AutoCAD can smooth. It is neither a solid nor a surface:
// SubDMesh has no Volume, no SurfaceArea and no IsWatertight, so the only way to measure one is
// to convert it - which makes that conversion its own check. What a mesh does carry exactly is
// its VERTEX AND FACE COUNTS, and those are the arithmetic this category is verified against.
//
// There are no factory methods for mesh primitives: SubDMesh has no CreateBox to match
// Solid3d.CreateBox. Everything here is built from SetSubDMesh(vertices, faces, smoothLevel),
// which means the tessellation is written out by hand and the counts are known before the call.

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

internal static class MeshPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.mesh.create_mesh_box",         CreateMeshBox);
        host.Register("acad.mesh.get_mesh_info",           GetMeshInfo);
        host.Register("acad.mesh.set_mesh_smoothness",     SetMeshSmoothness);
        host.Register("acad.mesh.convert_mesh_to_solid",   ConvertMeshToSolid);
        host.Register("acad.mesh.convert_mesh_to_surface", ConvertMeshToSurface);

        // roadmap 4.3, second tranche - creasing, and two more primitives
        host.Register("acad.mesh.set_mesh_crease",         SetMeshCrease);
        host.Register("acad.mesh.create_mesh_cylinder",    CreateMeshCylinder);
        host.Register("acad.mesh.create_mesh_wedge",       CreateMeshWedge);
    }

    /// <summary>
    /// Build a mesh from a cage and persist it, checking the counts that were known in advance.
    ///
    /// Every primitive here is tessellated by hand, because SubDMesh has no factory methods at
    /// all - so the vertex and face counts are decided before the call and can be asserted
    /// against what comes back rather than merely reported.
    /// </summary>
    private static JsonObject BuildMesh(Database db, Transaction tr, Point3dCollection verts,
                                        Int32Collection faces, int smooth, string? layer,
                                        int expectVerts, int expectFaces, string what, object extra)
    {
        var mesh = new SubDMesh();
        try
        {
            mesh.SetSubDMesh(verts, faces, smooth);
        }
        catch (AcadRt.Exception ex)
        {
            mesh.Dispose();
            throw new ArgumentException(
                "AutoCAD refused to build the " + what + " with " + ex.ErrorStatus + ".");
        }

        var handle = AcadEnv.Persist(db, tr, mesh, layer);
        var nv = mesh.NumberOfVertices;
        var nf = mesh.NumberOfFaces;
        if (nv != expectVerts || nf != expectFaces)
            throw new InvalidOperationException(
                "The " + what + " cage was handed over as " + expectVerts + " vertices and " +
                expectFaces + " faces, and came back as " + nv + " and " + nf + ". AutoCAD " +
                "reports the cage rather than the subdivision, so these should match exactly " +
                "whatever the smoothness - they do not, so the mesh is not the one that was built.");

        var result = new JsonObject
        {
            ["entity"] = JsonSerializer.SerializeToNode(handle, Opts),
            ["vertices"] = nv,
            ["faces"] = nf,
            ["smoothLevel"] = smooth,
        };
        var node = JsonSerializer.SerializeToNode(extra, Opts) as JsonObject;
        if (node is not null)
            foreach (var kv in node) result[kv.Key] = kv.Value?.DeepClone();
        return result;
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    /// <summary>
    /// How many faces a SubDMesh face array describes.
    ///
    /// The array is a flat run of [vertexCount, i0, i1, ...] per face, so the face count is not
    /// the array length over anything fixed - a cage of quads and triangles mixes stride. Walked
    /// rather than divided.
    /// </summary>
    private static int CountFaces(Int32Collection faceArray)
    {
        int n = 0, i = 0;
        while (i < faceArray.Count)
        {
            var verts = faceArray[i];
            if (verts <= 0) break;          // malformed; stop rather than loop forever
            i += verts + 1;
            n++;
        }
        return n;
    }

    private static SubDMesh RequireMesh(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (ent is SubDMesh m) return m;
        throw new ArgumentException(
            "Entity " + handle + " is a " + ent.GetRXClass().Name + ", not a mesh. A mesh is a " +
            "cage of flat faces - use surfaces.convert_to_surface or geometry_3d tools for the " +
            "other kinds.");
    }

    // ─────────── making one ───────────

    private static Task<ToolDispatchResult> CreateMeshBox(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.create_mesh_box", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshBoxArgsDto>(args);
            if (a.Corner1 is null || a.Corner2 is null)
                throw new ArgumentException("corner1 and corner2 are required.");
            var p1 = AcadEnv.ToPoint3d(a.Corner1);
            var p2 = AcadEnv.ToPoint3d(a.Corner2);
            double x0 = Math.Min(p1.X, p2.X), x1 = Math.Max(p1.X, p2.X);
            double y0 = Math.Min(p1.Y, p2.Y), y1 = Math.Max(p1.Y, p2.Y);
            double z0 = Math.Min(p1.Z, p2.Z), z1 = Math.Max(p1.Z, p2.Z);
            if (x1 - x0 <= 1e-9 || y1 - y0 <= 1e-9 || z1 - z0 <= 1e-9)
                throw new ArgumentException(
                    "The two corners give a box with a zero side: " + (x1 - x0) + " by " +
                    (y1 - y0) + " by " + (z1 - z0) + ".");

            var smooth = a.SmoothLevel ?? 0;
            if (smooth < 0 || smooth > 4)
                throw new ArgumentException(
                    "smoothLevel must be 0 to 4. 0 leaves the box sharp; each level after that " +
                    "subdivides every face into four and rounds the shape further, so the face " +
                    "count goes 6, 24, 96, 384, 1536 - and level 4 on a large model is slow " +
                    "enough that AutoCAD itself warns about it.");

            // Eight corners, then six quads wound counter-clockwise seen from OUTSIDE. Winding is
            // not decoration: a face wound the other way points its normal into the solid, and a
            // mesh with mixed winding converts into a solid of the wrong volume while still
            // looking right on screen.
            var verts = new Point3dCollection
            {
                new Point3d(x0, y0, z0),  // 0
                new Point3d(x1, y0, z0),  // 1
                new Point3d(x1, y1, z0),  // 2
                new Point3d(x0, y1, z0),  // 3
                new Point3d(x0, y0, z1),  // 4
                new Point3d(x1, y0, z1),  // 5
                new Point3d(x1, y1, z1),  // 6
                new Point3d(x0, y1, z1),  // 7
            };

            // Each face is a run: the vertex count, then that many indices.
            var faces = new Int32Collection
            {
                4, 0, 3, 2, 1,   // bottom, facing -Z
                4, 4, 5, 6, 7,   // top,    facing +Z
                4, 0, 1, 5, 4,   // front,  facing -Y
                4, 1, 2, 6, 5,   // right,  facing +X
                4, 2, 3, 7, 6,   // back,   facing +Y
                4, 3, 0, 4, 7,   // left,   facing -X
            };

            var mesh = new SubDMesh();
            try
            {
                mesh.SetSubDMesh(verts, faces, smooth);
            }
            catch (AcadRt.Exception ex)
            {
                mesh.Dispose();
                throw new ArgumentException(
                    "AutoCAD refused to build the mesh with " + ex.ErrorStatus + ".");
            }

            var handle = AcadEnv.Persist(db, tr, mesh, a.Layer);
            var nv = mesh.NumberOfVertices;
            var nf = mesh.NumberOfFaces;

            // The cage is known before the call - eight corners and six quads, written out above -
            // so a mesh that came back with anything else was not built from what it was handed.
            //
            // MEASURED: this holds at EVERY smooth level, because NumberOfFaces reports the CAGE
            // rather than the subdivided surface. A guard asserting 6 * 4^level here fired on a
            // perfectly good mesh before that was understood.
            if (nv != 8 || nf != 6)
                throw new InvalidOperationException(
                    "A box cage is 8 vertices and 6 faces at every smooth level - the count " +
                    "reported is the cage, not the subdivision - but this came back with " +
                    nv + " and " + nf + ".");

            return Wrap(new
            {
                entity = handle,
                vertices = nv,
                faces = nf,
                smoothLevel = smooth,
                size = new { x = x1 - x0, y = y1 - y0, z = z1 - z0 },
                note = "A mesh BOX: a cage of " + nv + " vertices and " + nf + " faces at " +
                       "smooth level " + smooth + ". Those counts describe the CAGE and stay the " +
                       "same at every level - AutoCAD reports the cage, not the subdivision - so " +
                       "what smoothing changes is the SHAPE: the corners pull in and the box " +
                       "rounds towards a ball, shrinking inside its own cage. There is no " +
                       "CreateBox for meshes in the API; this cage is written out by hand, which " +
                       "is why the counts are known in advance and checked against what returned.",
            });
        });

    // ─────────── reading and smoothing ───────────

    private static Task<ToolDispatchResult> GetMeshInfo(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.get_mesh_info", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshHandleArgsDto>(args);
            var m = RequireMesh(db, tr, a.Handle, OpenMode.ForRead);
            var ext = m.GeometricExtents;
            return Wrap(new
            {
                handle = a.Handle,
                vertices = m.NumberOfVertices,
                faces = m.NumberOfFaces,
                smoothLevel = m.SmoothLevel,
                bbox = new
                {
                    min = AcadEnv.FromPoint3d(ext.MinPoint),
                    max = AcadEnv.FromPoint3d(ext.MaxPoint),
                },
                note = "A mesh carries no volume and no surface area - SubDMesh exposes neither, " +
                       "so measuring one means converting it to a solid first, which is exactly " +
                       "what convert_mesh_to_solid is for. What a mesh does carry exactly is its " +
                       "counts: " + m.NumberOfVertices + " vertices over " + m.NumberOfFaces +
                       " faces at smooth level " + m.SmoothLevel + ".",
            });
        });

    private static Task<ToolDispatchResult> SetMeshSmoothness(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.set_mesh_smoothness", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshSmoothArgsDto>(args);
            var m = RequireMesh(db, tr, a.Handle, OpenMode.ForWrite);
            var before = m.SmoothLevel;
            var facesBefore = m.NumberOfFaces;

            int target;
            if (a.Level is not null && a.By is not null)
                throw new ArgumentException("level and by are alternatives; give one.");
            if (a.Level is not null) target = a.Level.Value;
            else if (a.By is not null) target = before + a.By.Value;
            else throw new ArgumentException(
                "Give either level - the smoothness to set - or by, a step up or down from where " +
                "it is now. by: 1 is AutoCAD's MESHSMOOTHMORE and by: -1 is MESHSMOOTHLESS.");

            if (target < 0 || target > 4)
                throw new ArgumentException(
                    "Smoothness runs 0 to 4 and this would set " + target + ". The mesh is at " +
                    before + " now. Each level divides every face into four, so 4 is already " +
                    "256 times the face count of 0.");
            if (target == before)
                throw new ArgumentException(
                    "The mesh is already at smooth level " + before + ", so nothing would change.");

            // SubDMesh.SmoothLevel is READ-ONLY - it can be asked but not assigned, which the
            // first version of this tool got wrong. The level is fixed when the cage is handed
            // over, so changing it means handing the cage over again at the new level. The cage
            // itself is what Vertices and FaceArray return, and it does NOT grow with smoothing:
            // that is exactly why smoothing is reversible in AutoCAD.
            var cageVerts = m.Vertices;
            var cageFaces = m.FaceArray;
            var cageFaceCount = CountFaces(cageFaces);
            var extBefore = m.GeometricExtents;
            var sizeBefore = extBefore.MaxPoint - extBefore.MinPoint;
            try
            {
                m.SetSubDMesh(cageVerts, cageFaces, target);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to rebuild the mesh at smooth level " + target + " with " +
                    ex.ErrorStatus + ".");
            }

            var landed = m.SmoothLevel;
            var facesAfter = m.NumberOfFaces;
            if (landed != target)
                throw new InvalidOperationException(
                    "The smoothness was set to " + target + " but reads back as " + landed + ".");

            // NO further guard here, and that is a decision rather than an oversight.
            //
            // Two candidate signals were tried and BOTH turned out to report the cage rather than
            // the subdivided surface: NumberOfFaces stays at 6 for a box at every level, and
            // GeometricExtents stayed at exactly the box diagonal, 100*sqrt(3), across a 0 to 1
            // smoothing. Guards built on either fired on a perfectly good mesh. A guard founded on
            // an assumption that has not been checked is worse than no guard - it rejects correct
            // results while looking rigorous.
            //
            // What IS checked is the level reading back, above. To see that the SHAPE changed,
            // convert the mesh to a solid and compare volumes; the note says so, and the live
            // verification does exactly that - a level 2 box comes out at about a third of the
            // sharp one.
            var extAfter = m.GeometricExtents;
            var sizeAfter = extAfter.MaxPoint - extAfter.MinPoint;

            return Wrap(new
            {
                handle = a.Handle,
                smoothLevelBefore = before,
                smoothLevel = landed,
                facesBefore,
                faces = facesAfter,
                verticesNow = m.NumberOfVertices,
                cageFaces = cageFaceCount,
                cageDiagonalBefore = sizeBefore.Length,
                cageDiagonal = sizeAfter.Length,
                note = "Smoothness " + before + " to " + landed + ". Neither the face count nor " +
                       "the bounding box will show you this happened: BOTH report the CAGE rather " +
                       "than the subdivided surface, so a box answers " + cageFaceCount +
                       " faces and the same extents at every level. That is measured, not " +
                       "assumed - guards built on each of them in turn rejected correct results. " +
                       "To see the SHAPE change, convert to a solid and compare volumes: a box " +
                       "smoothed to level 2 comes out at roughly a third of the sharp one. " +
                       "Smoothing is REVERSIBLE because the cage is kept, so coming back down " +
                       "returns the original mesh rather than an approximation of it. WARNING, " +
                       "measured: changing the smoothness rebuilds the mesh through SetSubDMesh, " +
                       "which carries no crease data, so ANY CREASES ARE LOST. Crease AFTER " +
                       "smoothing, not before. This tool cannot warn you when it happens, because " +
                       "reading a crease back needs a FullSubentityPath and SubDMesh exposes no " +
                       "way to obtain one.",
            });
        });

    // ─────────── converting out ───────────

    private static Task<ToolDispatchResult> ConvertMeshToSolid(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.convert_mesh_to_solid", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshConvertArgsDto>(args);
            var m = RequireMesh(db, tr, a.Handle, OpenMode.ForWrite);
            var faces = m.NumberOfFaces;
            var smooth = m.SmoothLevel;

            Solid3d solid;
            try
            {
                solid = m.ConvertToSolid(a.Smooth ?? false, a.Optimize ?? false)
                        ?? throw new InvalidOperationException("AutoCAD returned no solid.");
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to convert the mesh with " + ex.ErrorStatus + ". A mesh can " +
                    "only become a solid if it is WATERTIGHT - every edge shared by exactly two " +
                    "faces. A cage with a hole in it, or with faces wound inconsistently, " +
                    "encloses nothing.");
            }

            var volume = solid.MassProperties.Volume;
            if (volume <= 1e-9)
            {
                solid.Dispose();
                throw new InvalidOperationException(
                    "The conversion produced a solid of volume " + volume + ". AutoCAD returned " +
                    "an object rather than an error, but a solid enclosing nothing means the mesh " +
                    "was not watertight - which is usually a winding problem rather than a gap, " +
                    "since a face wound the wrong way looks perfectly normal on screen.");
            }

            var handle = AcadEnv.Persist(db, tr, solid, a.Layer);
            if (a.EraseSource == true && !m.IsErased) m.Erase();

            return Wrap(new
            {
                entity = handle,
                volume,
                meshFaces = faces,
                meshSmoothLevel = smooth,
                sourceErased = a.EraseSource == true,
                note = "The mesh became a solid of volume " + volume + ". This is the only way to " +
                       "MEASURE a mesh, since SubDMesh carries no volume of its own - so an " +
                       "unsmoothed box mesh converting to exactly its side cubed is the check " +
                       "that the cage was built and wound correctly. With smooth false the facets " +
                       "are kept as flat faces; with smooth true AutoCAD fits curved faces to " +
                       "them, and the volume then differs because the shape does.",
            });
        });

    private static Task<ToolDispatchResult> ConvertMeshToSurface(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.convert_mesh_to_surface", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshConvertArgsDto>(args);
            var m = RequireMesh(db, tr, a.Handle, OpenMode.ForWrite);
            var faces = m.NumberOfFaces;

            Entity made;
            try
            {
                made = m.ConvertToSurface(a.Smooth ?? false, a.Optimize ?? false)
                       ?? throw new InvalidOperationException("AutoCAD returned no surface.");
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to convert the mesh with " + ex.ErrorStatus + ".");
            }

            var handle = AcadEnv.Persist(db, tr, made, a.Layer);
            if (a.EraseSource == true && !m.IsErased) m.Erase();

            return Wrap(new
            {
                entity = handle,
                type = made.GetRXClass().Name,
                meshFaces = faces,
                sourceErased = a.EraseSource == true,
                note = "A surface is a shell with no inside, so this works on ANY mesh - a torn " +
                       "cage that convert_mesh_to_solid would refuse converts to a surface " +
                       "perfectly well, because a surface never had to enclose anything. Use " +
                       "surfaces.get_surface_info to measure the result.",
            });
        });

    // ─────────── roadmap 4.3, second tranche ───────────

    private static Task<ToolDispatchResult> SetMeshCrease(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.set_mesh_crease", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshCreaseArgsDto>(args);
            if (a.Level is null)
                throw new ArgumentException(
                    "level is required: how sharply to hold the edges against smoothing. 0 lets " +
                    "them round off completely, which is what removes a crease; a positive number " +
                    "is how many levels of smoothing the edge resists before it starts to soften, " +
                    "and -1 holds it sharp for ever.");
            var level = a.Level.Value;
            if (level < -1)
                throw new ArgumentException(
                    "level cannot be below -1. -1 means always sharp, 0 means not creased, and " +
                    "anything above 0 is the number of smoothing levels the edge holds out for.");

            var m = RequireMesh(db, tr, a.Handle, OpenMode.ForWrite);
            var smooth = m.SmoothLevel;

            try
            {
                // The single-argument form creases EVERY edge. Naming individual edges needs a
                // FullSubentityPath[], and SubDMesh exposes no way to get those - the
                // GetSubentityPathsAt* family that Solid3d has is absent here - so all-edges is
                // the whole of what this API offers, and the description says so rather than
                // implying a selection that cannot be made.
                m.SetCrease(level);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused to set the crease with " + ex.ErrorStatus + ".");
            }

            return Wrap(new
            {
                handle = a.Handle,
                creaseLevel = level,
                smoothLevel = smooth,
                allEdges = true,
                note = "Every edge of the mesh is now creased to " + level + ". A crease HOLDS an " +
                       "edge against smoothing: crease a box fully and it stays a box however " +
                       "much you smooth it, which is the check worth making - convert it to a " +
                       "solid and the volume comes back to the sharp figure rather than the " +
                       "rounded one. 0 removes the crease, -1 holds for ever, and a positive " +
                       "number is how many levels the edge resists before softening. ORDER " +
                       "MATTERS, and it is the opposite of what most people try: crease AFTER " +
                       "setting the smoothness. Changing smoothness rebuilds the mesh and " +
                       "silently discards every crease - measured, a box creased then smoothed " +
                       "comes out at the rounded volume while one smoothed then creased comes out " +
                       "sharp. This also applies to ALL edges: SubDMesh offers no way to name " +
                       "individual ones, because the GetSubentityPathsAt* family that Solid3d has " +
                       "is not there.",
            });
        });

    private static Task<ToolDispatchResult> CreateMeshCylinder(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.create_mesh_cylinder", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshCylinderArgsDto>(args);
            if (a.BasePoint is null) throw new ArgumentException("basePoint is required.");
            if (a.Radius is null || a.Radius <= 0) throw new ArgumentException("radius must be > 0.");
            if (a.Height is null || a.Height == 0)
                throw new ArgumentException("height is required and cannot be 0.");
            var sides = a.Sides ?? 8;
            if (sides < 3 || sides > 64)
                throw new ArgumentException(
                    "sides must be 3 to 64. A mesh cylinder is a PRISM - the cage has flat sides " +
                    "and smoothing rounds it - so the count decides how round it starts: 8 is " +
                    "AutoCAD's own default and 3 gives a triangular prism, which is a legitimate " +
                    "shape rather than a broken cylinder.");

            var b = AcadEnv.ToPoint3d(a.BasePoint);
            var r = a.Radius.Value;
            var h = a.Height.Value;

            // Two rings of `sides` vertices; two cap faces and `sides` quads round the wall.
            var verts = new Point3dCollection();
            for (int i = 0; i < sides; i++)
            {
                var t = 2.0 * Math.PI * i / sides;
                verts.Add(new Point3d(b.X + r * Math.Cos(t), b.Y + r * Math.Sin(t), b.Z));
            }
            for (int i = 0; i < sides; i++)
            {
                var t = 2.0 * Math.PI * i / sides;
                verts.Add(new Point3d(b.X + r * Math.Cos(t), b.Y + r * Math.Sin(t), b.Z + h));
            }

            var faces = new Int32Collection();
            // Bottom cap, wound clockwise seen from above so its normal points down and out.
            faces.Add(sides);
            for (int i = sides - 1; i >= 0; i--) faces.Add(i);
            // Top cap, wound the other way.
            faces.Add(sides);
            for (int i = 0; i < sides; i++) faces.Add(sides + i);
            // The wall.
            for (int i = 0; i < sides; i++)
            {
                var j = (i + 1) % sides;
                faces.Add(4);
                faces.Add(i);
                faces.Add(j);
                faces.Add(sides + j);
                faces.Add(sides + i);
            }

            var smooth = a.SmoothLevel ?? 0;
            if (smooth < 0 || smooth > 4) throw new ArgumentException("smoothLevel must be 0 to 4.");

            // A prism of n sides inscribed in radius r has base area (n/2)*r^2*sin(2*pi/n), which
            // is what its volume should come to once converted - LESS than pi*r^2*h, because the
            // flat sides cut the corners off the circle.
            var baseArea = 0.5 * sides * r * r * Math.Sin(2.0 * Math.PI / sides);
            return BuildMesh(db, tr, verts, faces, smooth, a.Layer,
                             2 * sides, sides + 2, "mesh cylinder", new
                             {
                                 sides,
                                 radius = r,
                                 height = h,
                                 prismVolume = baseArea * Math.Abs(h),
                                 circleVolume = Math.PI * r * r * Math.Abs(h),
                                 note = "A mesh cylinder is a PRISM of " + sides + " sides, not a " +
                                        "circle: the cage has flat walls and smoothing rounds them. " +
                                        "So its volume once converted is (n/2)*r*r*sin(2*pi/n)*h = " +
                                        (baseArea * Math.Abs(h)) + ", which is LESS than the " +
                                        "pi*r*r*h of " + (Math.PI * r * r * Math.Abs(h)) + " a true " +
                                        "cylinder would hold - the flat sides cut the corners off " +
                                        "the circle. Raise sides to close that gap, or smooth it.",
                             });
        });

    private static Task<ToolDispatchResult> CreateMeshWedge(JsonObject args, CancellationToken ct) =>
        Run("acad.mesh.create_mesh_wedge", args, ct, (doc, db, tr) =>
        {
            var a = Read<MeshBoxArgsDto>(args);
            if (a.Corner1 is null || a.Corner2 is null)
                throw new ArgumentException("corner1 and corner2 are required.");
            var p1 = AcadEnv.ToPoint3d(a.Corner1);
            var p2 = AcadEnv.ToPoint3d(a.Corner2);
            double x0 = Math.Min(p1.X, p2.X), x1 = Math.Max(p1.X, p2.X);
            double y0 = Math.Min(p1.Y, p2.Y), y1 = Math.Max(p1.Y, p2.Y);
            double z0 = Math.Min(p1.Z, p2.Z), z1 = Math.Max(p1.Z, p2.Z);
            if (x1 - x0 <= 1e-9 || y1 - y0 <= 1e-9 || z1 - z0 <= 1e-9)
                throw new ArgumentException(
                    "The two corners give a wedge with a zero side: " + (x1 - x0) + " by " +
                    (y1 - y0) + " by " + (z1 - z0) + ".");

            var smooth = a.SmoothLevel ?? 0;
            if (smooth < 0 || smooth > 4) throw new ArgumentException("smoothLevel must be 0 to 4.");

            // A wedge is the box with the top edge collapsed onto the x0 side: six vertices, and
            // five faces - two triangles and three quads.
            var verts = new Point3dCollection
            {
                new Point3d(x0, y0, z0),  // 0
                new Point3d(x1, y0, z0),  // 1
                new Point3d(x1, y1, z0),  // 2
                new Point3d(x0, y1, z0),  // 3
                new Point3d(x0, y0, z1),  // 4
                new Point3d(x0, y1, z1),  // 5
            };
            var faces = new Int32Collection
            {
                4, 0, 3, 2, 1,   // base,   facing -Z
                4, 0, 1, 2, 3,   // placeholder replaced below
            };
            faces.Clear();
            faces.Add(4); faces.Add(0); faces.Add(3); faces.Add(2); faces.Add(1);  // base
            faces.Add(3); faces.Add(0); faces.Add(1); faces.Add(4);                // front triangle
            faces.Add(3); faces.Add(3); faces.Add(5); faces.Add(2);                // back triangle
            faces.Add(4); faces.Add(1); faces.Add(2); faces.Add(5); faces.Add(4);  // slope
            faces.Add(4); faces.Add(0); faces.Add(4); faces.Add(5); faces.Add(3);  // vertical back

            var volume = 0.5 * (x1 - x0) * (y1 - y0) * (z1 - z0);
            return BuildMesh(db, tr, verts, faces, smooth, a.Layer, 6, 5, "mesh wedge", new
            {
                size = new { x = x1 - x0, y = y1 - y0, z = z1 - z0 },
                halfBoxVolume = volume,
                note = "A wedge is exactly HALF the box on the same two corners - " + volume +
                       " here - which is what its volume must come to once converted, and is the " +
                       "arithmetic worth checking. Six vertices and five faces: two triangles and " +
                       "three quads, so the cage mixes face sizes, which a box does not.",
            });
        });
}
