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
                       "returns the original mesh rather than an approximation of it.",
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
}
