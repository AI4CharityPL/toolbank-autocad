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

    internal sealed record HatchPatternEntry(string Category, string Description, double DefaultScale, double DefaultAngle);

    private static readonly IReadOnlyDictionary<string, HatchPatternEntry> s_patternCatalog =
        new Dictionary<string, HatchPatternEntry>(StringComparer.OrdinalIgnoreCase)
        {
            // ANSI patterns (ISO 128 mechanical)
            { "ANSI31",  new("ANSI",      "Iron / brick elevation (45° diagonal)",  1.0,  0.0) },
            { "ANSI32",  new("ANSI",      "Steel",                                    1.0,  0.0) },
            { "ANSI33",  new("ANSI",      "Bronze / brass",                           1.0,  0.0) },
            { "ANSI34",  new("ANSI",      "Plastic / rubber",                         1.0,  0.0) },
            { "ANSI35",  new("ANSI",      "Fire brick / refractory",                  1.0,  0.0) },
            { "ANSI36",  new("ANSI",      "Marble / slate",                           1.0,  0.0) },
            { "ANSI37",  new("ANSI",      "Lead / zinc (crosshatch)",                 1.0,  0.0) },
            { "ANSI38",  new("ANSI",      "Aluminum",                                 1.0,  0.0) },

            // ISO patterns (PN-EN-ISO 128)
            { "ISO02W100", new("ISO",     "Dashed line (ISO)",                        1.0,  0.0) },
            { "ISO03W100", new("ISO",     "Dashed space (ISO)",                       1.0,  0.0) },
            { "ISO09W100", new("ISO",     "Long dash short dash (ISO)",               1.0,  0.0) },

            // Architectural (ISO 128 + PN-EN)
            { "AR-CONC",   new("ARCH",    "Concrete (stone aggregate)",               1.0,  0.0) },
            { "AR-BRSTD",  new("ARCH",    "Brick — standard (common bond)",           1.0,  0.0) },
            { "AR-BRELM",  new("ARCH",    "Brick — English bond",                     1.0,  0.0) },
            { "AR-B816",   new("ARCH",    "Block 8x16 (cinder / concrete block)",     1.0,  0.0) },
            { "AR-B88",    new("ARCH",    "Block 8x8",                                1.0,  0.0) },
            { "AR-RROOF",  new("ARCH",    "Rough stone / irregular roof tile",        1.0,  0.0) },
            { "AR-HBONE",  new("ARCH",    "Herringbone parquet",                      1.0,  0.0) },
            { "AR-PARQ1",  new("ARCH",    "Parquet (standard)",                       1.0,  0.0) },
            { "AR-SAND",   new("ARCH",    "Sand",                                     1.0,  0.0) },
            { "AR-RSHKE",  new("ARCH",    "Roof shingles",                            1.0,  0.0) },

            // Material-specific
            { "BATTING",   new("MATERIAL","Insulation (zigzag)",                      1.0,  0.0) },
            { "EARTH",     new("MATERIAL","Earth / soil",                             1.0,  0.0) },
            { "CORK",      new("MATERIAL","Cork",                                     1.0,  0.0) },
            { "NET",       new("MATERIAL","Mesh / grid (Faraday)",                    1.0,  0.0) },
            { "NET3",      new("MATERIAL","3-direction mesh",                         1.0,  0.0) },
            { "GRAVEL",    new("MATERIAL","Gravel",                                   1.0,  0.0) },
            { "SWAMP",     new("MATERIAL","Swamp / wetland",                          1.0,  0.0) },
            { "GRASS",     new("MATERIAL","Grass",                                    1.0,  0.0) },
            { "HONEY",     new("MATERIAL","Honeycomb",                                1.0,  0.0) },
            { "TRIANG",    new("MATERIAL","Triangles",                                1.0,  0.0) },
            { "DOTS",      new("MATERIAL","Dots",                                     1.0,  0.0) },
            { "CROSS",     new("MATERIAL","Crosses",                                  1.0,  0.0) },
            { "ESCHER",    new("MATERIAL","Escher pattern",                           1.0,  0.0) },
            { "FLEX",      new("MATERIAL","Flexible material",                        1.0,  0.0) },
            { "ZIGZAG",    new("MATERIAL","Zigzag",                                   1.0,  0.0) },
            { "CLAY",      new("MATERIAL","Clay",                                     1.0,  0.0) },
            { "SACNCR",    new("MATERIAL","Sand + concrete composite",                1.0,  0.0) },

            // Solid / line
            { "SOLID",     new("SOLID",   "Solid fill",                               1.0,  0.0) },
            { "LINE",      new("LINE",    "Parallel lines",                           1.0,  0.0) },
        };

    internal sealed record MaterialPreset(string Pattern, double Scale, double Angle, int AciColor);

    // Material -> (pattern, scale, angle, ACI color). Scale assumes mm drawing units.
    private static readonly IReadOnlyDictionary<string, MaterialPreset> s_materialPresets =
        new Dictionary<string, MaterialPreset>(StringComparer.OrdinalIgnoreCase)
        {
            { "concrete",             new("AR-CONC",  50.0,  0.0,   8)  },  // gray
            { "reinforced-concrete",  new("ANSI37",    5.0,  0.0,   8)  },
            { "concrete-block",       new("AR-B816",  50.0,  0.0,   8)  },
            { "brick",                new("AR-BRSTD", 50.0,  0.0,   1)  },  // red
            { "brick-elm",            new("AR-BRELM", 50.0,  0.0,   1)  },
            { "insulation",           new("BATTING",  50.0,  0.0,   4)  },  // cyan
            { "plaster",              new("ANSI31",    5.0, 45.0,   8)  },
            { "stone",                new("AR-RROOF", 50.0,  0.0,  42)  },  // brown
            { "earth",                new("EARTH",    50.0,  0.0,  42)  },
            { "soil",                 new("EARTH",    50.0,  0.0,  42)  },
            { "steel",                new("ANSI32",    5.0, 45.0,   7)  },
            { "glass",                new("LINE",      1.0, 45.0,   4)  },
            { "wood-cross",           new("ANSI32",    5.0,  0.0,  42)  },
            { "wood-grain",           new("AR-HBONE",  1.0,  0.0,  42)  },
            { "parquet",              new("AR-PARQ1",  1.0,  0.0,  42)  },
            { "herringbone",          new("AR-HBONE",  1.0,  0.0,  42)  },
            { "tile",                 new("AR-B816",   1.0,  0.0,   8)  },
            { "lead-shield",          new("SOLID",     1.0,  0.0,   6)  },  // magenta — RTG/lead shielding
            { "faraday",              new("NET",      50.0,  0.0,   3)  },  // green — Faraday cage mesh
            { "sand",                 new("AR-SAND",  50.0,  0.0,  40)  },
            { "cork",                 new("CORK",     50.0,  0.0,  42)  },
            { "gravel",               new("GRAVEL",   50.0,  0.0,   8)  },
            { "grass",                new("GRASS",    50.0,  0.0,   3)  },
        };

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

            if (bounds.Count == 0)
                throw new InvalidOperationException(
                    "TraceBoundary found no closed region around seed point — check that surrounding geometry is closed.");

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
                a.BoundaryHandles, preset.Pattern, scale, preset.Angle, a.Layer,
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
            if (bounds.Count == 0)
                throw new InvalidOperationException(
                    "TraceBoundary found no closed region around seed point.");
            var scale = preset.Scale * (a.ScaleMultiplier > 0 ? a.ScaleMultiplier : 1.0);
            var color = new ColorDto(0, 0, 0, preset.AciColor);
            var hatch = BuildHatchFromBoundaries(db, tr,
                bounds, preset.Pattern, scale, preset.Angle, a.Layer,
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
            var filter = string.IsNullOrWhiteSpace(a.CategoryFilter) ? null : a.CategoryFilter;
            var list = new List<object>();
            foreach (var kv in s_patternCatalog.OrderBy(k => k.Value.Category).ThenBy(k => k.Key))
            {
                if (filter is not null && !string.Equals(kv.Value.Category, filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(new
                {
                    name = kv.Key,
                    category = kv.Value.Category,
                    description = kv.Value.Description,
                    defaultScale = kv.Value.DefaultScale,
                    defaultAngleDeg = kv.Value.DefaultAngle
                });
            }
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
    private static List<string> TraceBoundaryAsHandles(
        Document doc, Database db, Transaction tr,
        Point3d seed, bool detectIslands)
    {
        DBObjectCollection found;
        try
        {
            found = doc.Editor.TraceBoundary(seed, detectIslands)
                ?? new DBObjectCollection();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            throw new InvalidOperationException(
                $"Editor.TraceBoundary failed at ({seed.X:F1},{seed.Y:F1}): {ex.Message}. " +
                "Ensure the seed point is inside a fully closed area and geometry is visible on screen.");
        }

        if (found.Count == 0) return new List<string>();

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
    {
        if (string.IsNullOrWhiteSpace(material))
            throw new ArgumentException("material preset name is required.");
        if (!s_materialPresets.TryGetValue(material.Trim(), out var preset))
        {
            var all = string.Join(", ", s_materialPresets.Keys);
            throw new ArgumentException(
                $"Unknown material preset '{material}'. Known: {all}.");
        }
        return preset;
    }
}
