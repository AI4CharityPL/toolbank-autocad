// AutoCAD plugin handlers for the acad-boolean-ops category.
// Registered under "acad.booleanops.<verb>"; everything runs on the UI thread.
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

namespace AcadMcp.Plugin.Tools;

internal static class BooleanOpsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.booleanops.union_solids",       UnionSolids);
        host.Register("acad.booleanops.subtract_solids",    SubtractSolids);
        host.Register("acad.booleanops.intersect_solids",   IntersectSolids);
        host.Register("acad.booleanops.union_regions",      UnionRegions);
        host.Register("acad.booleanops.subtract_regions",   SubtractRegions);
        host.Register("acad.booleanops.intersect_regions",  IntersectRegions);
        host.Register("acad.booleanops.create_region",      CreateRegion);
        host.Register("acad.booleanops.check_intersection", CheckIntersection);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Solid3d ResolveSolid(Database db, Transaction tr, string handle, OpenMode mode)
    {
        var id = AcadEnv.ResolveHandle(db, handle);
        var ent = (Entity)tr.GetObject(id, mode);
        if (ent is Solid3d s) return s;
        throw new ArgumentException($"handle '{handle}' is not a 3D solid (got {ent.GetRXClass().Name}).");
    }

    private static Region ResolveRegion(Database db, Transaction tr, string handle, OpenMode mode)
    {
        var id = AcadEnv.ResolveHandle(db, handle);
        var ent = (Entity)tr.GetObject(id, mode);
        if (ent is Region r) return r;
        throw new ArgumentException($"handle '{handle}' is not a Region (got {ent.GetRXClass().Name}).");
    }

    // ─────────── solid booleans ───────────

    private static Task<ToolDispatchResult> UnionSolids(JsonObject args, CancellationToken ct) =>
        SolidBoolean(args, ct, "acad.booleanops.union_solids", BooleanOperationType.BoolUnite);

    private static Task<ToolDispatchResult> SubtractSolids(JsonObject args, CancellationToken ct) =>
        SolidBoolean(args, ct, "acad.booleanops.subtract_solids", BooleanOperationType.BoolSubtract);

    private static Task<ToolDispatchResult> IntersectSolids(JsonObject args, CancellationToken ct) =>
        SolidBoolean(args, ct, "acad.booleanops.intersect_solids", BooleanOperationType.BoolIntersect);

    private static Task<ToolDispatchResult> SolidBoolean(JsonObject args, CancellationToken ct, string toolKey, BooleanOperationType op) =>
        Run(toolKey, args, ct, (doc, db, tr) =>
        {
            var a = Read<SolidBooleanArgsDto>(args);
            if (a.ToolHandles is null || a.ToolHandles.Count == 0)
                throw new ArgumentException("at least one tool solid is required.");
            var target = ResolveSolid(db, tr, a.TargetHandle, OpenMode.ForWrite);
            foreach (var th in a.ToolHandles)
            {
                var toolSolid = ResolveSolid(db, tr, th, OpenMode.ForWrite);
                target.BooleanOperation(op, toolSolid);
                if (a.EraseTools && !toolSolid.IsErased)
                    toolSolid.Erase(true);
            }
            return Wrap(new { entity = AcadEnv.ToHandle(target) });
        });

    // ─────────── region booleans ───────────

    private static Task<ToolDispatchResult> UnionRegions(JsonObject args, CancellationToken ct) =>
        RegionBoolean(args, ct, "acad.booleanops.union_regions", BooleanOperationType.BoolUnite);

    private static Task<ToolDispatchResult> SubtractRegions(JsonObject args, CancellationToken ct) =>
        RegionBoolean(args, ct, "acad.booleanops.subtract_regions", BooleanOperationType.BoolSubtract);

    private static Task<ToolDispatchResult> IntersectRegions(JsonObject args, CancellationToken ct) =>
        RegionBoolean(args, ct, "acad.booleanops.intersect_regions", BooleanOperationType.BoolIntersect);

    private static Task<ToolDispatchResult> RegionBoolean(JsonObject args, CancellationToken ct, string toolKey, BooleanOperationType op) =>
        Run(toolKey, args, ct, (doc, db, tr) =>
        {
            var a = Read<RegionBooleanArgsDto>(args);
            if (a.ToolHandles is null || a.ToolHandles.Count == 0)
                throw new ArgumentException("at least one tool region is required.");
            var target = ResolveRegion(db, tr, a.TargetHandle, OpenMode.ForWrite);
            foreach (var th in a.ToolHandles)
            {
                var toolReg = ResolveRegion(db, tr, th, OpenMode.ForWrite);
                target.BooleanOperation(op, toolReg);
                if (a.EraseTools && !toolReg.IsErased)
                    toolReg.Erase(true);
            }
            return Wrap(new { entity = AcadEnv.ToHandle(target) });
        });

    // ─────────── region creation ───────────

    private static Task<ToolDispatchResult> CreateRegion(JsonObject args, CancellationToken ct) =>
        Run("acad.booleanops.create_region", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateRegionArgsDto>(args);
            if (a.CurveHandles is null || a.CurveHandles.Count == 0)
                throw new ArgumentException("at least one curve handle is required.");
            using var col = new DBObjectCollection();
            var sources = new List<Entity>();
            foreach (var h in a.CurveHandles)
            {
                var id = AcadEnv.ResolveHandle(db, h);
                var ent = (Entity)tr.GetObject(id, a.EraseSource ? OpenMode.ForWrite : OpenMode.ForRead);
                col.Add(ent);
                sources.Add(ent);
            }
            using var regions = Region.CreateFromCurves(col);
            if (regions.Count == 0)
                throw new InvalidOperationException("no regions could be created from the given curves.");
            var handles = new List<EntityHandle>(regions.Count);
            for (int i = 0; i < regions.Count; i++)
            {
                var r = (Region)regions[i];
                handles.Add(AcadEnv.Persist(db, tr, r, a.Layer));
            }
            if (a.EraseSource)
            {
                foreach (var s in sources) if (!s.IsErased) s.Erase(true);
            }
            return Wrap(new { entities = handles });
        });

    // ─────────── intersection probe ───────────

    private static Task<ToolDispatchResult> CheckIntersection(JsonObject args, CancellationToken ct) =>
        Run("acad.booleanops.check_intersection", args, ct, (doc, db, tr) =>
        {
            var a = Read<CheckIntersectArgsDto>(args);
            var entA = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleA), OpenMode.ForRead);
            var entB = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.HandleB), OpenMode.ForRead);

            // Bounding-box prefilter.
            var bbA = entA.Bounds;
            var bbB = entB.Bounds;
            bool bbOverlap = bbA.HasValue && bbB.HasValue && BoundsIntersect(bbA.Value, bbB.Value);
            if (!bbOverlap)
                return Wrap(new { intersect = false, relation = "disjoint_bbox" });

            // Curve-curve case via IntersectWith.
            if (entA is Curve ca && entB is Curve cb)
            {
                using var pts = new Point3dCollection();
                ca.IntersectWith(cb, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                bool hit = pts.Count > 0;
                return Wrap(new { intersect = hit, relation = hit ? "curves_cross" : "curves_no_cross_in_bbox" });
            }

            // Generic Entity.IntersectWith (works for many DB ents incl. Solid3d/Region).
            try
            {
                using var pts = new Point3dCollection();
                entA.IntersectWith(entB, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                bool hit = pts.Count > 0;
                return Wrap(new
                {
                    intersect = hit || bbOverlap,
                    relation = hit ? "boundaries_cross" : "bbox_overlap_no_boundary_cross"
                });
            }
            catch
            {
                // Fall back to bbox overlap as best signal.
                return Wrap(new { intersect = true, relation = "bbox_overlap_unverified" });
            }
        });

    private static bool BoundsIntersect(Extents3d a, Extents3d b)
    {
        return a.MinPoint.X <= b.MaxPoint.X && a.MaxPoint.X >= b.MinPoint.X
            && a.MinPoint.Y <= b.MaxPoint.Y && a.MaxPoint.Y >= b.MinPoint.Y
            && a.MinPoint.Z <= b.MaxPoint.Z && a.MaxPoint.Z >= b.MinPoint.Z;
    }
}
