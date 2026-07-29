// AutoCAD plugin handlers for the acad-validators category.
// Three tools: collect_entities, doc_summary, apply_fixes. All run on the UI thread.
// See rule 33-validators-rule-format.mdc + rule 34-validators-engine-traps.mdc.
//
// IMPORTANT (rule 34 §2): collect_entities + doc_summary are STRICTLY READ-ONLY -
// open everything ForRead, never UpgradeOpen. Mutation lives only in apply_fixes,
// which runs in its own document lock + transaction (rule 34 §3).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class ValidatorsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.validators.collect_entities", CollectEntities);
        host.Register("acad.validators.doc_summary",      DocSummary);
        host.Register("acad.validators.apply_fixes",      ApplyFixes);
        host.Register("acad.validators.check_overlaps",   CheckOverlaps);
    }

    private static T Read<T>(JsonObject args) =>
        JsonSerializer.Deserialize<T>(args, Opts) ?? throw new ArgumentException($"cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    // ─────────── collect_entities (read-only) ───────────

    private static async Task<ToolDispatchResult> CollectEntities(JsonObject args, CancellationToken ct)
    {
        try
        {
            var json = await UiThreadDispatcher.Run(() =>
            {
                var doc = AcadEnv.RequireActiveDocument();
                var db = doc.Database;
                using var tr = doc.TransactionManager.StartTransaction();
                var a = Read<CollectEntitiesArgsDto>(args);
                var typeSet = a.EntityTypes is { Length: > 0 }
                    ? new HashSet<string>(a.EntityTypes, StringComparer.OrdinalIgnoreCase)
                    : null;
                var layerSet = a.LayerIn is { Length: > 0 }
                    ? new HashSet<string>(a.LayerIn, StringComparer.OrdinalIgnoreCase)
                    : null;
                Regex? layerRx = !string.IsNullOrWhiteSpace(a.LayerPattern)
                    ? new Regex(a.LayerPattern!, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    : null;

                int scanned = 0;
                var list = new List<EntitySnapshotPluginDto>();
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                foreach (ObjectId btrId in bt)
                {
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    if (!btr.IsLayout) continue;
                    bool isModel = string.Equals(btr.Name, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase);
                    bool isPaper = !isModel;
                    if (a.InPaperspace == true && !isPaper) continue;
                    if (a.InPaperspace == false && !isModel) continue;
                    // a.InPaperspace == null -> include all layouts.
                    foreach (ObjectId id in btr)
                    {
                        if (id.IsErased) continue;
                        scanned++;
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent is null || ent.IsErased) continue;
                        var canonical = CanonicalTypeName(ent);
                        if (typeSet is not null && !typeSet.Contains(canonical)) continue;
                        if (layerSet is not null && !layerSet.Contains(ent.Layer)) continue;
                        if (layerRx is not null && !layerRx.IsMatch(ent.Layer)) continue;

                        list.Add(BuildSnapshot(ent, canonical, tr, isPaper));
                    }
                }

                tr.Commit();
                return Wrap(new CollectEntitiesResultDto(list, scanned));
            }, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.validators.collect_entities", ex); }
    }

    private static EntitySnapshotPluginDto BuildSnapshot(Entity ent, string canonical, Transaction tr, bool inPaperspace)
    {
        int? aci = null; int[]? rgb = null;
        try
        {
            var col = ent.Color;
            if (col.IsByAci && col.ColorIndex >= 1 && col.ColorIndex <= 255) aci = col.ColorIndex;
            else if (col.ColorMethod == ColorMethod.ByColor) rgb = new int[] { col.Red, col.Green, col.Blue };
        }
        catch { }

        string lt = "";
        try { lt = ent.Linetype; } catch { }

        double? lw = null;
        try
        {
            var w = ent.LineWeight;
            if (w != LineWeight.ByLayer && w != LineWeight.ByBlock && w != LineWeight.ByLineWeightDefault)
                lw = ((int)w) / 100.0;
        }
        catch { }

        double? len = null, area = null, radius = null;
        string? text = null; double? textHeight = null;
        string? blockName = null; Dictionary<string, string>? attrs = null;
        double[][]? vertices = null;

        try
        {
            switch (ent)
            {
                case Line l:
                    len = l.Length;
                    vertices = new[] {
                        new[] { l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z },
                        new[] { l.EndPoint.X,   l.EndPoint.Y,   l.EndPoint.Z   },
                    };
                    break;
                case Polyline pl:
                    len = pl.Length;
                    if (pl.Closed) try { area = pl.Area; } catch { }
                    vertices = ExtractPolylineVertices(pl);
                    break;
                case Polyline2d pl2:
                    try { len = pl2.Length; } catch { }
                    if (pl2.Closed) try { area = pl2.Area; } catch { }
                    vertices = ExtractPolyline2dVertices(pl2, tr);
                    break;
                case Polyline3d pl3:
                    try { len = pl3.Length; } catch { }
                    vertices = ExtractPolyline3dVertices(pl3, tr);
                    break;
                case Arc arc:
                    radius = arc.Radius;
                    try { len = arc.Length; } catch { }
                    break;
                case Circle c:
                    radius = c.Radius;
                    try { area = c.Area; } catch { }
                    break;
                case Ellipse el:
                    try { area = el.Area; } catch { }
                    break;
                case Spline sp:
                    try { len = sp.GetDistanceAtParameter(sp.EndParam); } catch { }
                    break;
                case Region rg:
                    try { area = rg.Area; } catch { }
                    break;
                case Hatch h:
                    try { area = h.Area; } catch { }
                    break;
                case DBText dbt:
                    text = dbt.TextString; textHeight = dbt.Height; break;
                case MText mt:
                    text = mt.Text; textHeight = mt.TextHeight; break;
                case BlockReference br:
                    try { blockName = br.Name; } catch { }
                    attrs = ReadBlockAttributes(br, tr);
                    break;
            }
        }
        catch { }

        string? className = null;
        try { className = ent.GetRXClass()?.Name; } catch { }

        double[] bMin = new double[] { 0, 0, 0 }, bMax = new double[] { 0, 0, 0 };
        try
        {
            if (ent.Bounds.HasValue)
            {
                var b = ent.Bounds.Value;
                bMin = new double[] { b.MinPoint.X, b.MinPoint.Y, b.MinPoint.Z };
                bMax = new double[] { b.MaxPoint.X, b.MaxPoint.Y, b.MaxPoint.Z };
            }
        }
        catch { }

        return new EntitySnapshotPluginDto(
            Handle: ent.Handle.ToString(),
            DxfType: canonical,
            ClassName: className,
            Layer: SafeLayer(ent),
            ColorAci: aci,
            ColorRgb: rgb,
            Linetype: lt,
            LineweightMm: lw,
            Length: len,
            Area: area,
            Radius: radius,
            TextValue: text,
            TextHeight: textHeight,
            BlockName: blockName,
            Attributes: attrs,
            Vertices: vertices,
            BboxMin: bMin,
            BboxMax: bMax,
            InPaperspace: inPaperspace);
    }

    private static double[][]? ExtractPolylineVertices(Polyline pl)
    {
        try
        {
            int n = pl.NumberOfVertices;
            var arr = new double[n][];
            for (int i = 0; i < n; i++)
            {
                var p = pl.GetPoint3dAt(i);
                arr[i] = new[] { p.X, p.Y, p.Z };
            }
            return arr;
        }
        catch { return null; }
    }

    private static double[][]? ExtractPolyline2dVertices(Polyline2d pl, Transaction tr)
    {
        try
        {
            var list = new List<double[]>();
            foreach (ObjectId vid in pl)
            {
                if (vid.IsErased) continue;
                if (tr.GetObject(vid, OpenMode.ForRead) is Vertex2d v && !v.IsErased)
                {
                    var p = v.Position;
                    list.Add(new[] { p.X, p.Y, p.Z });
                }
            }
            return list.Count == 0 ? null : list.ToArray();
        }
        catch { return null; }
    }

    private static double[][]? ExtractPolyline3dVertices(Polyline3d pl, Transaction tr)
    {
        try
        {
            var list = new List<double[]>();
            foreach (ObjectId vid in pl)
            {
                if (vid.IsErased) continue;
                if (tr.GetObject(vid, OpenMode.ForRead) is PolylineVertex3d v && !v.IsErased)
                {
                    var p = v.Position;
                    list.Add(new[] { p.X, p.Y, p.Z });
                }
            }
            return list.Count == 0 ? null : list.ToArray();
        }
        catch { return null; }
    }

    private static Dictionary<string, string>? ReadBlockAttributes(BlockReference br, Transaction tr)
    {
        try
        {
            if (br.AttributeCollection is null || br.AttributeCollection.Count == 0) return null;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in br.AttributeCollection)
            {
                if (id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is AttributeReference ar && !ar.IsErased)
                {
                    dict[ar.Tag] = ar.TextString ?? "";
                }
            }
            return dict.Count == 0 ? null : dict;
        }
        catch { return null; }
    }

    private static string CanonicalTypeName(Entity ent)
    {
        // Prefer the .NET runtime class name (Line, Polyline, Circle, ...) - that's what the rule schema uses.
        try { return ent.GetType().Name; } catch { return "Unknown"; }
    }

    private static string SafeLayer(Entity e) { try { return e.Layer; } catch { return "<unknown>"; } }

    // ─────────── doc_summary (read-only) ───────────

    private static async Task<ToolDispatchResult> DocSummary(JsonObject args, CancellationToken ct)
    {
        try
        {
            var json = await UiThreadDispatcher.Run(() =>
            {
                var doc = AcadEnv.RequireActiveDocument();
                var db = doc.Database;
                using var tr = doc.TransactionManager.StartTransaction();

                string name = doc.Name ?? "<unsaved>";
                string? path = string.IsNullOrEmpty(doc.Name) ? null : doc.Name;

                var layers = new List<string>();
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    layers.Add(ltr.Name);
                }
                var blocks = new List<string>();
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in bt)
                {
                    var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (btr.IsLayout) continue;
                    if (btr.IsAnonymous) continue;
                    blocks.Add(btr.Name);
                }
                var textStyles = new List<string>();
                var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                foreach (ObjectId id in tst)
                {
                    var tsr = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    textStyles.Add(tsr.Name);
                }
                var dimStyles = new List<string>();
                var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                foreach (ObjectId id in dst)
                {
                    var dsr = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    dimStyles.Add(dsr.Name);
                }

                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (id.IsErased) continue;
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is null || ent.IsErased) continue;
                    var k = ent.GetType().Name;
                    counts.TryGetValue(k, out var c);
                    counts[k] = c + 1;
                }

                tr.Commit();

                return Wrap(new DocSummaryResultDto(
                    DocumentName: name,
                    DocumentPath: path,
                    Units: ResolveUnits(db),
                    LayerNames: layers,
                    BlockNames: blocks,
                    TextStyleNames: textStyles,
                    DimStyleNames: dimStyles,
                    EntityCountsByType: counts));
            }, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.validators.doc_summary", ex); }
    }

    private static string ResolveUnits(Database db)
    {
        try
        {
            return db.Insunits switch
            {
                UnitsValue.Millimeters => "mm",
                UnitsValue.Centimeters => "cm",
                UnitsValue.Meters      => "m",
                UnitsValue.Inches      => "in",
                UnitsValue.Feet        => "ft",
                UnitsValue.Undefined   => "undefined",
                _ => db.Insunits.ToString().ToLowerInvariant(),
            };
        }
        catch { return "undefined"; }
    }

    // ─────────── apply_fixes (mutates - single transaction per call) ───────────

    private static async Task<ToolDispatchResult> ApplyFixes(JsonObject args, CancellationToken ct)
    {
        try
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document.");
            // Acquire DocumentLock on background thread to avoid EDU-modal deadlock (see PluginToolRunner).
            using var docLock = doc.LockDocument();

            var json = await UiThreadDispatcher.Run(() =>
            {
                var db = doc.Database;
                var a = Read<ApplyFixesArgsDto>(args);
                var fixes = a.Fixes ?? new List<EntityFixPluginDto>();

                var outcomes = new List<FixOutcomePluginDto>(fixes.Count);
                int applied = 0;

                using var tr = doc.TransactionManager.StartTransaction();
                bool aborted = false;
                int abortedAt = -1;

                for (int i = 0; i < fixes.Count; i++)
                {
                    var f = fixes[i];
                    if (aborted)
                    {
                        outcomes.Add(new FixOutcomePluginDto(f.Handle, f.FixType, "rolled_back",
                            "earlier fix in this batch failed; transaction aborted (rule 34 §3)."));
                        continue;
                    }
                    try
                    {
                        var oc = ApplyOne(db, tr, f);
                        outcomes.Add(oc);
                        if (oc.Outcome == "applied") applied++;
                    }
                    catch (Exception ex)
                    {
                        outcomes.Add(new FixOutcomePluginDto(f.Handle, f.FixType, "error", ex.Message));
                        aborted = true; abortedAt = i;
                    }
                }

                if (aborted)
                {
                    try { tr.Abort(); } catch { }
                    // Rewrite all preceding "applied" outcomes as rolled_back too -
                    // they live in a now-aborted tx (rule 34 §3).
                    for (int i = 0; i < abortedAt; i++)
                    {
                        if (outcomes[i].Outcome == "applied")
                            outcomes[i] = outcomes[i] with { Outcome = "rolled_back",
                                Message = outcomes[i].Message + " (transaction was aborted by a later failure; change rolled back)." };
                    }
                    applied = 0;
                }
                else
                {
                    tr.Commit();
                }

                return Wrap(new ApplyFixesResultDto(fixes.Count, applied, outcomes));
            }, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.validators.apply_fixes", ex); }
    }

    private static FixOutcomePluginDto ApplyOne(Database db, Transaction tr, EntityFixPluginDto f)
    {
        if (string.IsNullOrWhiteSpace(f.Handle))
            return new FixOutcomePluginDto("", f.FixType, "error", "handle missing.");
        ObjectId id;
        try { id = AcadEnv.ResolveHandle(db, f.Handle); }
        catch (Exception ex) { return new FixOutcomePluginDto(f.Handle, f.FixType, "error", "resolve handle: " + ex.Message); }

        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
        if (ent is null || ent.IsErased)
            return new FixOutcomePluginDto(f.Handle, f.FixType, "error", "entity not found / erased.");

        switch (f.FixType)
        {
            case "move_to_layer":
            {
                var layer = ParamString(f, "layer") ?? throw new ArgumentException("move_to_layer needs 'layer' param.");
                AcadEnv.ValidateSymbolName(layer, "layer");
                if (string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase))
                    return new FixOutcomePluginDto(f.Handle, f.FixType, "already_satisfied", $"already on layer '{layer}'.");
                bool create = ParamBool(f, "create_if_missing", false);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layer))
                {
                    if (!create) throw new InvalidOperationException($"layer '{layer}' does not exist (set create_if_missing=true).");
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord { Name = layer };
                    lt.Add(ltr); tr.AddNewlyCreatedDBObject(ltr, true);
                }
                ent.UpgradeOpen();
                ent.Layer = layer;
                return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", $"moved to layer '{layer}'.");
            }
            case "set_color":
            {
                ent.UpgradeOpen();
                if (TryParamInt(f, "aci", out var aci))
                {
                    if (aci < 0 || aci > 256) throw new ArgumentException("aci out of range [0..256].");
                    ent.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)aci);
                    return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", $"color set to ACI {aci}.");
                }
                if (TryParamRgb(f, out var r, out var g, out var b))
                {
                    ent.Color = Color.FromRgb((byte)r, (byte)g, (byte)b);
                    return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", $"color set to rgb({r},{g},{b}).");
                }
                throw new ArgumentException("set_color needs 'aci' (int) or 'rgb' ([r,g,b]).");
            }
            case "set_linetype":
            {
                var name = ParamString(f, "value") ?? throw new ArgumentException("set_linetype needs 'value'.");
                var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (!lt.Has(name))
                    throw new InvalidOperationException($"linetype '{name}' is not loaded; load it via LINETYPE first.");
                ent.UpgradeOpen();
                ent.Linetype = name;
                return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", $"linetype set to '{name}'.");
            }
            case "set_lineweight":
            {
                if (!TryParamDouble(f, "value_mm", out var mm))
                    throw new ArgumentException("set_lineweight needs 'value_mm'.");
                ent.UpgradeOpen();
                ent.LineWeight = AcadEnv.NearestLineWeight(mm);
                return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", $"lineweight set near {mm} mm.");
            }
            case "delete_entity":
            {
                ent.UpgradeOpen();
                ent.Erase();
                return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", "entity erased.");
            }
            case "set_attribute":
            {
                if (ent is not BlockReference br)
                    throw new InvalidOperationException("set_attribute only applies to BlockReference entities.");
                var tag = ParamString(f, "tag") ?? throw new ArgumentException("set_attribute needs 'tag'.");
                var value = ParamString(f, "value") ?? "";
                bool found = false;
                foreach (ObjectId aid in br.AttributeCollection)
                {
                    if (aid.IsErased) continue;
                    var ar = tr.GetObject(aid, OpenMode.ForRead) as AttributeReference;
                    if (ar is null) continue;
                    if (string.Equals(ar.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        ar.UpgradeOpen();
                        ar.TextString = value;
                        found = true;
                    }
                }
                if (!found) throw new InvalidOperationException($"attribute '{tag}' not found on block reference.");
                return new FixOutcomePluginDto(f.Handle, f.FixType, "applied", $"attribute '{tag}' set to '{value}'.");
            }
            default:
                throw new InvalidOperationException($"unknown fix type '{f.FixType}'.");
        }
    }

    private static string? ParamString(EntityFixPluginDto f, string key)
    {
        if (f.Params is null || !f.Params.ContainsKey(key)) return null;
        var n = f.Params[key];
        return n switch
        {
            JsonValue v when v.TryGetValue(out string? s) => s,
            null => null,
            _ => n?.ToString(),
        };
    }

    private static bool ParamBool(EntityFixPluginDto f, string key, bool fallback)
    {
        if (f.Params is null || !f.Params.ContainsKey(key)) return fallback;
        try { return f.Params[key]?.GetValue<bool>() ?? fallback; }
        catch
        {
            var s = f.Params[key]?.ToString();
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryParamInt(EntityFixPluginDto f, string key, out int value)
    {
        value = 0;
        if (f.Params is null || !f.Params.ContainsKey(key)) return false;
        try { value = f.Params[key]!.GetValue<int>(); return true; }
        catch
        {
            try { value = (int)f.Params[key]!.GetValue<long>(); return true; }
            catch { return int.TryParse(f.Params[key]?.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value); }
        }
    }

    private static bool TryParamDouble(EntityFixPluginDto f, string key, out double value)
    {
        value = 0;
        if (f.Params is null || !f.Params.ContainsKey(key)) return false;
        try { value = f.Params[key]!.GetValue<double>(); return true; }
        catch
        {
            return double.TryParse(f.Params[key]?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }

    private static bool TryParamRgb(EntityFixPluginDto f, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (f.Params is null || !f.Params.ContainsKey("rgb")) return false;
        if (f.Params["rgb"] is JsonArray arr && arr.Count == 3)
        {
            try
            {
                r = arr[0]!.GetValue<int>();
                g = arr[1]!.GetValue<int>();
                b = arr[2]!.GetValue<int>();
                return true;
            }
            catch { return false; }
        }
        return false;
    }

    // ─────────── check_overlaps (read-only) ───────────
    //
    // Finds pairs (a, b) where `a` is on any of `layersA` and `b` on any of `layersB`
    // whose bounding boxes (or curves, for mode=polyline_crosses_polyline) overlap.
    //
    // Use cases:
    //   - "doors that cut through walls": layersA=["A-DOOR*"], layersB=["A-WALL-*"],
    //     mode="polyline_crosses_polyline" → only reports real geometric crossings,
    //     not just bbox overlap (doors' arcs sit NEXT to walls by design).
    //   - "labels overlapping labels": layersA=layersB=["A-ANNO-TEXT"],
    //     mode="bbox_intersect" → flags stacked mtext.
    //   - "notes touching geometry": layersA=["A-ANNO-NOTE"], layersB=["A-DOOR","A-WALL-*"],
    //     mode="bbox_intersect" → flags notes printed over lines.
    //
    // Implementation: single read-only transaction, collect candidates by layer
    // (optionally filtered by drawing-unit window), O(nA * nB) bbox prefilter,
    // then per-mode refinement. Handles are always sorted so (a, b) vs (b, a)
    // collapse to a single result when layersA == layersB.

    private static readonly string[] _overlapModes =
    {
        "bbox_intersect", "centroid_in_bbox", "polyline_crosses_polyline",
    };

    private static async Task<ToolDispatchResult> CheckOverlaps(JsonObject args, CancellationToken ct)
    {
        try
        {
            var json = await UiThreadDispatcher.Run(() =>
            {
                var doc = AcadEnv.RequireActiveDocument();
                var db = doc.Database;
                using var tr = doc.TransactionManager.StartTransaction();

                var a = Read<CheckOverlapsArgsDto>(args);
                var layersA = (a.LayersA is { Length: > 0 })
                    ? new HashSet<string>(a.LayersA, StringComparer.OrdinalIgnoreCase)
                    : throw new ArgumentException("check_overlaps: layersA is required (non-empty).");
                var layersB = (a.LayersB is { Length: > 0 })
                    ? new HashSet<string>(a.LayersB, StringComparer.OrdinalIgnoreCase)
                    : layersA;
                var mode = string.IsNullOrWhiteSpace(a.Mode) ? "bbox_intersect" : a.Mode!.Trim().ToLowerInvariant();
                if (Array.IndexOf(_overlapModes, mode) < 0)
                    throw new ArgumentException(
                        $"check_overlaps mode must be one of {string.Join(", ", _overlapModes)}; got '{a.Mode}'.");

                double tol = Math.Max(0, a.Tolerance);
                int maxResults = a.MaxResults <= 0 ? 500 : a.MaxResults;

                bool SameLayerSet = ReferenceEquals(layersA, layersB) ||
                    (layersA.Count == layersB.Count && layersA.SetEquals(layersB));

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                var listA = new List<OverlapEntityInfo>();
                var listB = new List<OverlapEntityInfo>();

                foreach (ObjectId id in ms)
                {
                    if (id.IsErased) continue;
                    if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent || ent.IsErased) continue;
                    if (!ent.Bounds.HasValue) continue;
                    var b = ent.Bounds.Value;
                    double minX = b.MinPoint.X - tol, minY = b.MinPoint.Y - tol;
                    double maxX = b.MaxPoint.X + tol, maxY = b.MaxPoint.Y + tol;

                    if (a.Window is not null)
                    {
                        if (maxX < a.Window.XMin || minX > a.Window.XMax ||
                            maxY < a.Window.YMin || minY > a.Window.YMax) continue;
                    }

                    var info = new OverlapEntityInfo
                    {
                        Handle  = ent.Handle.ToString(),
                        Layer   = SafeLayer(ent),
                        DxfType = CanonicalTypeName(ent),
                        MinX    = b.MinPoint.X, MinY = b.MinPoint.Y,
                        MaxX    = b.MaxPoint.X, MaxY = b.MaxPoint.Y,
                        ObjectId = id,
                    };
                    if (layersA.Contains(info.Layer)) listA.Add(info);
                    if (!SameLayerSet && layersB.Contains(info.Layer)) listB.Add(info);
                }
                // When the two layer sets coincide we pair within listA itself.
                if (SameLayerSet) listB = listA;

                var pairs = new List<OverlapPairPluginDto>();
                bool truncated = false;

                for (int i = 0; i < listA.Count && !truncated; i++)
                {
                    var ea = listA[i];
                    int jStart = SameLayerSet ? i + 1 : 0;
                    for (int j = jStart; j < listB.Count; j++)
                    {
                        var eb = listB[j];
                        if (ReferenceEquals(ea, eb)) continue;

                        // BBox prefilter (tolerant).
                        double iMinX = Math.Max(ea.MinX, eb.MinX) - tol;
                        double iMinY = Math.Max(ea.MinY, eb.MinY) - tol;
                        double iMaxX = Math.Min(ea.MaxX, eb.MaxX) + tol;
                        double iMaxY = Math.Min(ea.MaxY, eb.MaxY) + tol;
                        if (iMaxX < iMinX || iMaxY < iMinY) continue;

                        double overlapArea = Math.Max(0, iMaxX - iMinX) * Math.Max(0, iMaxY - iMinY);

                        var match = ClassifyOverlap(tr, ea, eb, mode, tol);
                        if (match is null) continue;

                        // Order handles so (A,B) and (B,A) collapse for SameLayerSet.
                        var (h1, h2, l1, l2, t1, t2, bb1, bb2) =
                            (ea.Handle, eb.Handle, ea.Layer, eb.Layer, ea.DxfType, eb.DxfType,
                             new[] { ea.MinX, ea.MinY, ea.MaxX, ea.MaxY },
                             new[] { eb.MinX, eb.MinY, eb.MaxX, eb.MaxY });
                        if (SameLayerSet && string.CompareOrdinal(h1, h2) > 0)
                        {
                            (h1, h2) = (h2, h1);
                            (l1, l2) = (l2, l1);
                            (t1, t2) = (t2, t1);
                            (bb1, bb2) = (bb2, bb1);
                        }

                        var severity = GradeSeverity(match.Value.intersections, overlapArea);
                        pairs.Add(new OverlapPairPluginDto(
                            HandleA: h1, HandleB: h2,
                            LayerA: l1, LayerB: l2,
                            DxfTypeA: t1, DxfTypeB: t2,
                            BboxA: bb1, BboxB: bb2,
                            OverlapArea: Math.Round(overlapArea, 4),
                            IntersectionCount: match.Value.intersections,
                            Severity: severity,
                            Mode: mode));

                        if (pairs.Count >= maxResults) { truncated = true; break; }
                    }
                }

                // Sort by severity (critical → minor), then by overlap area desc.
                int SeverityRank(string s) => s switch
                {
                    "critical" => 3, "major" => 2, "minor" => 1, _ => 0,
                };
                pairs.Sort((x, y) =>
                {
                    int r = SeverityRank(y.Severity).CompareTo(SeverityRank(x.Severity));
                    return r != 0 ? r : y.OverlapArea.CompareTo(x.OverlapArea);
                });

                tr.Commit();
                return Wrap(new CheckOverlapsResultDto(
                    Overlaps: pairs,
                    ScannedA: listA.Count,
                    ScannedB: SameLayerSet ? listA.Count : listB.Count,
                    Mode: mode,
                    Truncated: truncated));
            }, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.validators.check_overlaps", ex); }
    }

    private sealed class OverlapEntityInfo
    {
        public string Handle = ""; public string Layer = ""; public string DxfType = "";
        public double MinX, MinY, MaxX, MaxY;
        public ObjectId ObjectId;
    }

    /// Returns null if the pair does not match the given mode, or (intersections, hitKind)
    /// if it does. "intersections" is the curve intersection count (0 for bbox-only modes).
    private static (int intersections, string hitKind)? ClassifyOverlap(
        Transaction tr, OverlapEntityInfo a, OverlapEntityInfo b, string mode, double tol)
    {
        switch (mode)
        {
            case "bbox_intersect":
                return (0, "bbox");

            case "centroid_in_bbox":
            {
                double cx = (a.MinX + a.MaxX) / 2.0, cy = (a.MinY + a.MaxY) / 2.0;
                bool aInB = cx >= b.MinX - tol && cx <= b.MaxX + tol &&
                            cy >= b.MinY - tol && cy <= b.MaxY + tol;
                double cx2 = (b.MinX + b.MaxX) / 2.0, cy2 = (b.MinY + b.MaxY) / 2.0;
                bool bInA = cx2 >= a.MinX - tol && cx2 <= a.MaxX + tol &&
                            cy2 >= a.MinY - tol && cy2 <= a.MaxY + tol;
                return (aInB || bInA) ? (0, "centroid") : ((int, string)?)null;
            }

            case "polyline_crosses_polyline":
            {
                var ea = tr.GetObject(a.ObjectId, OpenMode.ForRead) as Entity;
                var eb = tr.GetObject(b.ObjectId, OpenMode.ForRead) as Entity;
                if (ea is not Curve ca || eb is not Curve cb) return null;
                try
                {
                    var pts = new Point3dCollection();
                    ca.IntersectWith(cb, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                    if (pts.Count == 0) return null;
                    return (pts.Count, "curve-crosses");
                }
                catch { return null; }
            }
        }
        return null;
    }

    private static string GradeSeverity(int intersections, double overlapArea)
    {
        // Polyline/curve intersections are always concerning; bbox-only overlaps
        // get severity purely from area (larger = more likely to be a real issue).
        if (intersections >= 2) return "critical";
        if (intersections == 1) return "major";
        if (overlapArea > 10_000) return "major";   // e.g. 100x100 mm overlap
        if (overlapArea > 1_000)  return "minor";
        return "minor";
    }
}
