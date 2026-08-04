// AutoCAD plugin handlers for the acad-hatches category.
// Each handler is registered under "acad.hatches.<verb>" and ALWAYS runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern),
//        62-hatching-policy (material preset table).
//
// Pattern table and material presets follow PN-EN-ISO 128 + AIA-2017 conventions.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using AcadMcp.Shared.Catalogs;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class HatchesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.hatches.draw_hatch", DrawHatch);
        host.Register("acad.hatches.draw_hatch_by_boundary", DrawHatchByBoundary);
        host.Register("acad.hatches.list_patterns", ListPatterns);
        host.Register("acad.hatches.apply_material_preset", ApplyMaterialPreset);
        host.Register("acad.hatches.apply_material_preset_by_point", ApplyMaterialPresetByPoint);
        host.Register("acad.hatches.clip_hatch", ClipHatch);
        host.Register("acad.hatches.regenerate_hatches", RegenerateHatches);
        host.Register("acad.hatches.list_hatches", ListHatches);
    }

    // ─────────── helpers ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> RunW(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Task<ToolDispatchResult> RunR(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunReadAsync(toolKey, ct, work);

    // ─────────── pattern catalog + material presets (rule 62) ───────────

    // Pattern catalogue and material presets live in AcadMcp.Shared.Catalogs.HatchCatalog,
    // outside AutoCAD's reach so CI can test them. Two contracts are enforced there: every
    // published name resolves, and every material preset points at a pattern the catalogue
    // actually lists. See CatalogContractTests.

    // ─────────── handler: draw_hatch ───────────

    private static Task<ToolDispatchResult> DrawHatch(JsonObject args, CancellationToken ct) =>
        RunW("acad.hatches.draw_hatch", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesDrawHatchArgsDto>(args);
            var hatch = BuildHatchFromBoundaries(db, tr,
                a.BoundaryHandles, a.Pattern, a.Scale, a.AngleDeg, a.Layer,
                a.Color, a.BackgroundColor, a.Associative, a.Annotative);
            return Wrap(new
            {
                entity = AcadEnv.ToHandle(hatch),
                pattern = hatch.PatternName,
                scale = hatch.PatternScale,
                angleDeg = hatch.PatternAngle * 180.0 / Math.PI,
                material = (string?)null
            });
        });

    // ─────────── handler: draw_hatch_by_boundary ───────────

    private static Task<ToolDispatchResult> DrawHatchByBoundary(JsonObject args, CancellationToken ct) =>
        RunW("acad.hatches.draw_hatch_by_boundary", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesDrawByBoundaryArgsDto>(args);
            var bounds = TraceBoundaryAsHandles(doc, db, tr,
                new Point3d(a.SeedPoint.X, a.SeedPoint.Y, 0), a.DetectIslands);

            var hatch = BuildHatchFromBoundaries(db, tr,
                bounds, a.Pattern, a.Scale, a.AngleDeg, a.Layer,
                a.Color, backgroundColor: null, associative: true, annotative: false);
            return Wrap(new
            {
                entity = AcadEnv.ToHandle(hatch),
                pattern = hatch.PatternName,
                scale = hatch.PatternScale,
                angleDeg = hatch.PatternAngle * 180.0 / Math.PI,
                material = (string?)null
            });
        });

    // ─────────── handler: apply_material_preset ───────────

    private static Task<ToolDispatchResult> ApplyMaterialPreset(JsonObject args, CancellationToken ct) =>
        RunW("acad.hatches.apply_material_preset", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesApplyPresetArgsDto>(args);
            var preset = ResolvePreset(a.Material);
            var scale = preset.Scale * (a.ScaleMultiplier > 0 ? a.ScaleMultiplier : 1.0);
            var color = new ColorDto(0, 0, 0, preset.AciColor);
            var hatch = BuildHatchFromBoundaries(db, tr,
                a.BoundaryHandles, preset.Pattern, scale, preset.AngleDeg, a.Layer,
                color, backgroundColor: null, associative: true, annotative: false);
            return Wrap(new
            {
                entity = AcadEnv.ToHandle(hatch),
                pattern = hatch.PatternName,
                scale = hatch.PatternScale,
                angleDeg = hatch.PatternAngle * 180.0 / Math.PI,
                material = a.Material
            });
        });

    // ─────────── handler: apply_material_preset_by_point ───────────

    private static Task<ToolDispatchResult> ApplyMaterialPresetByPoint(JsonObject args, CancellationToken ct) =>
        RunW("acad.hatches.apply_material_preset_by_point", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesApplyPresetByPointArgsDto>(args);
            var preset = ResolvePreset(a.Material);
            var bounds = TraceBoundaryAsHandles(doc, db, tr,
                new Point3d(a.SeedPoint.X, a.SeedPoint.Y, 0), a.DetectIslands);
            var scale = preset.Scale * (a.ScaleMultiplier > 0 ? a.ScaleMultiplier : 1.0);
            var color = new ColorDto(0, 0, 0, preset.AciColor);
            var hatch = BuildHatchFromBoundaries(db, tr,
                bounds, preset.Pattern, scale, preset.AngleDeg, a.Layer,
                color, backgroundColor: null, associative: true, annotative: false);
            return Wrap(new
            {
                entity = AcadEnv.ToHandle(hatch),
                pattern = hatch.PatternName,
                scale = hatch.PatternScale,
                angleDeg = hatch.PatternAngle * 180.0 / Math.PI,
                material = a.Material
            });
        });

    // ─────────── handler: clip_hatch ───────────

    private static Task<ToolDispatchResult> ClipHatch(JsonObject args, CancellationToken ct) =>
        RunW("acad.hatches.clip_hatch", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesClipArgsDto>(args);
            if (a.BoundaryHandles is null || a.BoundaryHandles.Count == 0)
                throw new ArgumentException("clip_hatch needs >= 1 boundary handle.");

            var hatchId = AcadEnv.ResolveHandle(db, a.Handle);
            var hatch = tr.GetObject(hatchId, OpenMode.ForWrite) as Hatch
                ?? throw new InvalidOperationException($"Handle '{a.Handle}' is not a Hatch.");

            // Remove existing loops.
            while (hatch.NumberOfLoops > 0)
                hatch.RemoveLoopAt(0);

            var loopIds = new ObjectIdCollection();
            foreach (var h in a.BoundaryHandles)
                loopIds.Add(AcadEnv.ResolveHandle(db, h));
            hatch.AppendLoop(HatchLoopTypes.Default, loopIds);
            hatch.EvaluateHatch(true);

            return Wrap(new
            {
                entity = AcadEnv.ToHandle(hatch),
                pattern = hatch.PatternName,
                scale = hatch.PatternScale,
                angleDeg = hatch.PatternAngle * 180.0 / Math.PI,
                material = (string?)null
            });
        });

    // ─────────── handler: regenerate_hatches ───────────

    private static Task<ToolDispatchResult> RegenerateHatches(JsonObject args, CancellationToken ct) =>
        RunW("acad.hatches.regenerate_hatches", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesRegenerateArgsDto>(args);
            var targets = new List<ObjectId>();

            if (a.Handles is { Count: > 0 })
            {
                foreach (var h in a.Handles)
                    targets.Add(AcadEnv.ResolveHandle(db, h));
            }
            else if (a.AllInModelSpace || (a.Layers is { Count: > 0 }))
            {
                var layerSet = a.Layers is { Count: > 0 }
                    ? new HashSet<string>(a.Layers, StringComparer.OrdinalIgnoreCase)
                    : null;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Hatch;
                    if (ent is null) continue;
                    if (layerSet is null || layerSet.Contains(ent.Layer))
                        targets.Add(id);
                }
            }
            else
            {
                throw new ArgumentException(
                    "regenerate_hatches requires one of: handles[], layers[], or allInModelSpace=true.");
            }

            int ok = 0, fail = 0;
            var failed = new List<string>();
            foreach (var id in targets)
            {
                try
                {
                    var h = tr.GetObject(id, OpenMode.ForWrite) as Hatch;
                    if (h is null) continue;
                    h.EvaluateHatch(true);
                    ok++;
                }
                catch
                {
                    fail++;
                    try { failed.Add(id.Handle.ToString()); } catch { /* ignore */ }
                }
            }

            return Wrap(new { regenerated = ok, failed = fail, failedHandles = failed });
        });

    // ─────────── handler: list_hatches ───────────

    private static Task<ToolDispatchResult> ListHatches(JsonObject args, CancellationToken ct) =>
        RunR("acad.hatches.list_hatches", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesListHatchesArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var layerF = string.IsNullOrWhiteSpace(a.LayerFilter) ? null : a.LayerFilter;
            var patternF = string.IsNullOrWhiteSpace(a.PatternFilter) ? null : a.PatternFilter;

            var list = new List<object>();
            foreach (ObjectId id in ms)
            {
                var h = tr.GetObject(id, OpenMode.ForRead) as Hatch;
                if (h is null) continue;
                if (layerF is not null && !string.Equals(h.Layer, layerF, StringComparison.OrdinalIgnoreCase)) continue;
                if (patternF is not null && !string.Equals(h.PatternName, patternF, StringComparison.OrdinalIgnoreCase)) continue;

                double? area = null;
                try { area = h.Area; } catch { /* open loop etc. */ }

                list.Add(new
                {
                    handle = h.Handle.ToString(),
                    layer = h.Layer,
                    pattern = h.PatternName,
                    scale = h.PatternScale,
                    angleDeg = h.PatternAngle * 180.0 / Math.PI,
                    area,
                    loopCount = h.NumberOfLoops,
                    associative = h.Associative
                });
            }

            return Wrap(new { hatches = list, count = list.Count });
        });

    // ─────────── handler: list_patterns ───────────

    private static Task<ToolDispatchResult> ListPatterns(JsonObject args, CancellationToken ct) =>
        RunR("acad.hatches.list_patterns", args, ct, (doc, db, tr) =>
        {
            var a = Read<HatchesListPatternsArgsDto>(args);
            var list = HatchCatalog.AllPatterns(a.CategoryFilter)
                .Select(e => (object)new
                {
                    name = e.Name,
                    category = e.Category,
                    description = e.Description,
                    defaultScale = e.DefaultScale,
                    defaultAngleDeg = e.DefaultAngleDeg
                })
                .ToList();

            return Wrap(new { patterns = list, count = list.Count });
        });

    // ─────────── internal: build hatch from boundary handles ───────────

    private static Hatch BuildHatchFromBoundaries(
        Database db, Transaction tr,
        IReadOnlyList<string> boundaryHandles,
        string pattern,
        double scale,
        double angleDeg,
        string? layer,
        ColorDto? color,
        ColorDto? backgroundColor,
        bool associative,
        bool annotative)
    {
        if (boundaryHandles is null || boundaryHandles.Count == 0)
            throw new ArgumentException("hatch needs >= 1 boundary handle.");

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var hatch = new Hatch();
        if (!string.IsNullOrWhiteSpace(layer)) hatch.LayerId = AcadEnv.EnsureLayer(db, tr, layer);
        ms.AppendEntity(hatch);
        tr.AddNewlyCreatedDBObject(hatch, true);

        hatch.SetHatchPattern(HatchPatternType.PreDefined, string.IsNullOrWhiteSpace(pattern) ? "ANSI31" : pattern);
        hatch.PatternScale = scale > 0 ? scale : 1.0;
        hatch.PatternAngle = angleDeg * Math.PI / 180.0;
        hatch.Associative = associative;
        hatch.HatchStyle = HatchStyle.Normal;
        if (annotative) hatch.Annotative = AnnotativeStates.True;
        if (color is not null) hatch.Color = AcadEnv.FromColorDto(color);
        if (backgroundColor is not null) hatch.BackgroundColor = AcadEnv.FromColorDto(backgroundColor);

        var loopIds = new ObjectIdCollection();
        foreach (var h in boundaryHandles)
            loopIds.Add(AcadEnv.ResolveHandle(db, h));
        hatch.AppendLoop(HatchLoopTypes.Default, loopIds);
        hatch.EvaluateHatch(true);
        return hatch;
    }

    // ─────────── internal: seed-point boundary tracing ───────────
    //
    // Editor.TraceBoundary returns in-memory Polyline/Region entities that are NOT in the DB.
    // We persist them to a hidden layer "A-BNDRY-TEMP" so the hatch can reference them by ObjectId,
    // which is required for associativity. Callers using associative=true keep these boundaries;
    // the layer is frozen+non-plotting so they don't pollute plots.
    /// <summary>
    /// Frame the whole drawing so TraceBoundary can see it. Returns false when there is nothing
    /// drawable to frame. Same managed route as view.zoom_extents - deliberately not the
    /// command layer, which is what made zoom_extents itself fail with eInvalidInput.
    /// </summary>
    private static bool TryFrameDrawing(Document doc, Database db)
    {
        try { db.UpdateExt(true); } catch (Autodesk.AutoCAD.Runtime.Exception) { }

        var lo = db.Extmin;
        var hi = db.Extmax;
        if (lo.X > hi.X || lo.Y > hi.Y) return false;   // the empty-drawing sentinel

        double w = hi.X - lo.X, h = hi.Y - lo.Y;
        if (w <= 1e-9 && h <= 1e-9) { w = h = 100.0; }
        else if (w <= 1e-9) { w = h; }
        else if (h <= 1e-9) { h = w; }

        using var vtr = new ViewTableRecord
        {
            CenterPoint = new Point2d((lo.X + hi.X) / 2.0, (lo.Y + hi.Y) / 2.0),
            Width = w * 1.04,
            Height = h * 1.04,
        };
        doc.Editor.SetCurrentView(vtr);
        return true;
    }

    private static List<string> TraceBoundaryAsHandles(
        Document doc, Database db, Transaction tr,
        Point3d seed, bool detectIslands)
    {
        // Editor.TraceBoundary reads its seed point in the CURRENT UCS, not in WCS.
        //
        // Every argument in this codebase is WCS unless a `ucs` argument says otherwise
        // (rule 43), so handing the caller's point straight to TraceBoundary silently offsets
        // it by whatever the current UCS happens to be. With the UCS at world - the common
        // case - the two agree and the tool works, which is why this survived so long.
        //
        // Reproduced: a rectangle spanning (50000,50000)-(56000,54000), a seed at
        // (53000,52000) plainly inside it, and a current UCS with origin (1000,2000). The seed
        // was read as WCS (54000,54000), which lands exactly on the top edge, so TraceBoundary
        // correctly found no enclosing region - and the tool blamed the user's geometry.
        // The same seed succeeds immediately after ucs.set_ucs_world.
        //
        // CurrentUserCoordinateSystem is the UCS -> WCS matrix, so WCS -> UCS is its inverse.
        var ucsToWcs = doc.Editor.CurrentUserCoordinateSystem;
        var seedInUcs = seed.TransformBy(ucsToWcs.Inverse());

        // Second condition, independent of the first: TraceBoundary only sees geometry that is
        // in the current view. A region sitting off-screen produces an empty result, not an
        // error, which is indistinguishable from "your geometry is not closed".
        //
        // Measured, both conditions varied independently:
        //
        //     UCS world     + view away from region  -> fails
        //     UCS world     + view on region         -> works
        //     UCS offset    + view away from region  -> fails
        //     UCS offset    + view on region         -> works (only once the seed is
        //                                                      transformed, above)
        //
        // So frame the drawing before tracing, and put the user's view back afterwards. An
        // agent calling a hatch tool has not asked for its view to move, and leaving it moved
        // would be a side effect nobody requested.
        DBObjectCollection found;
        ViewTableRecord? savedView = null;
        try
        {
            try
            {
                savedView = doc.Editor.GetCurrentView();
                if (TryFrameDrawing(doc, db)) { /* framed */ }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Framing is a best-effort improvement to the odds, not a precondition. If the
                // view cannot be changed, still attempt the trace - it may well succeed, and a
                // real failure is reported below with everything the caller needs.
            }

            try
            {
                found = doc.Editor.TraceBoundary(seedInUcs, detectIslands)
                    ?? new DBObjectCollection();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException(
                    $"Editor.TraceBoundary failed at WCS ({seed.X:F1},{seed.Y:F1}) " +
                    $"= UCS ({seedInUcs.X:F1},{seedInUcs.Y:F1}): {ex.Message}.");
            }
        }
        finally
        {
            if (savedView is not null)
            {
                try { doc.Editor.SetCurrentView(savedView); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* view restore is not worth failing the tool over */ }
                savedView.Dispose();
            }
        }

        if (found.Count == 0)
        {
            // Report both points. When these differ, a caller who believed the seed was inside
            // the region is looking at the reason, rather than at advice about their geometry.
            var ucsNote = seed.IsEqualTo(seedInUcs, new Tolerance(1e-9, 1e-9))
                ? "The current UCS is world, so the seed was used as given."
                : $"NOTE: the current UCS is not world, so the seed was taken to UCS " +
                  $"({seedInUcs.X:F1},{seedInUcs.Y:F1}). Arguments here are WCS; if you meant " +
                  "UCS coordinates, convert them first or call ucs.set_ucs_world.";
            throw new InvalidOperationException(
                $"TraceBoundary found no closed region around WCS ({seed.X:F1},{seed.Y:F1}). " +
                $"Check that the surrounding geometry forms a fully closed area. {ucsNote}");
        }

        // Ensure hidden layer for trace boundaries (non-plotting, thawed-for-hatch-creation).
        const string BLayer = "A-BNDRY-TEMP";
        var layerId = AcadEnv.EnsureLayer(db, tr, BLayer);
        var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
        ltr.IsPlottable = false;  // do not plot temp boundaries
        ltr.IsOff = false;        // must remain on for hatch evaluation

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var handles = new List<string>();
        foreach (DBObject obj in found)
        {
            if (obj is Entity ent)
            {
                ent.LayerId = layerId;
                ms.AppendEntity(ent);
                tr.AddNewlyCreatedDBObject(ent, true);
                handles.Add(ent.Handle.ToString());
            }
            else
            {
                obj.Dispose();
            }
        }
        return handles;
    }

    // ─────────── internal: resolve material preset ───────────

    private static MaterialPreset ResolvePreset(string material)
        => HatchCatalog.ResolvePreset(material);
}
