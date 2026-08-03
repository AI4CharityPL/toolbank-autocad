// AutoCAD plugin handlers for the acad-xrefs category.
// Registered under "acad.xrefs.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern).
//
// Two API notes that cost time if you do not know them:
//
//   * Attach and detach act on the DEFINITION (BlockTableRecord). Reload/unload/bind take an
//     ObjectIdCollection of those BTR ids. Clipping acts on an INSERT (BlockReference) and
//     lives in a SpatialFilter stored under ACAD_FILTER -> SPATIAL in the insert's extension
//     dictionary. Mixing the two levels is the most common way to get eNotApplicable here.
//
//   * db.ResolveXrefs must run before the graph reflects a path change, and it is what turns
//     Unresolved into Resolved. Every path-mutating handler calls it and then re-reads the
//     status, so the result reports what actually happened rather than what was requested.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class XrefsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.xrefs.attach_xref", (a, c) => Attach(a, c, overlay: false));
        host.Register("acad.xrefs.attach_xref_overlay", (a, c) => Attach(a, c, overlay: true));
        host.Register("acad.xrefs.detach_xref", DetachXref);
        host.Register("acad.xrefs.reload_xref", ReloadXref);
        host.Register("acad.xrefs.reload_all_xrefs", ReloadAllXrefs);
        host.Register("acad.xrefs.unload_xref", UnloadXref);
        host.Register("acad.xrefs.bind_xref", BindXref);

        host.Register("acad.xrefs.list_xrefs", ListXrefs);
        host.Register("acad.xrefs.get_xref_info", GetXrefInfo);
        host.Register("acad.xrefs.list_nested_xrefs", ListNestedXrefs);
        host.Register("acad.xrefs.find_missing_xrefs", FindMissingXrefs);
        host.Register("acad.xrefs.list_xref_dependent_symbols", ListDependentSymbols);

        host.Register("acad.xrefs.set_xref_path", SetXrefPath);
        host.Register("acad.xrefs.repath_all_xrefs", RepathAllXrefs);

        host.Register("acad.xrefs.clip_xref_rect", ClipRect);
        host.Register("acad.xrefs.clip_xref_polygonal", ClipPolygonal);
        host.Register("acad.xrefs.invert_xref_clip", InvertClip);
        host.Register("acad.xrefs.delete_xref_clip", DeleteClip);
        host.Register("acad.xrefs.set_xref_clip_display", SetClipDisplay);

        host.Register("acad.xrefs.set_xref_layer_override", SetLayerOverride);
        host.Register("acad.xrefs.reset_xref_layer_overrides", ResetLayerOverrides);
    }

    // ─────────── infra ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string key, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);

    // ─────────── helpers ───────────

    /// <summary>Locate an xref definition by block name, or throw with the names that do exist.</summary>
    private static BlockTableRecord FindXrefBtr(Database db, Transaction tr, string blockName, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(blockName))
            throw new ArgumentException("blockName is required.");

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        if (bt.Has(blockName))
        {
            var btr = (BlockTableRecord)tr.GetObject(bt[blockName], mode);
            if (btr.IsFromExternalReference) return btr;
            throw new ArgumentException(
                $"Block '{blockName}' exists but is a local block, not an XREF. " +
                "Use the acad-blocks category for local blocks.");
        }

        var known = new List<string>();
        foreach (ObjectId id in bt)
        {
            var b = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
            if (b.IsFromExternalReference) known.Add(b.Name);
        }
        throw new ArgumentException(
            $"No XREF named '{blockName}' in this drawing. Present: " +
            (known.Count == 0 ? "(none)" : string.Join(", ", known.OrderBy(x => x))) + ".");
    }

    private static string StatusOf(BlockTableRecord btr)
    {
        try { return btr.XrefStatus.ToString(); } catch { return "Unknown"; }
    }

    /// <summary>Every BlockReference insert of one xref definition.</summary>
    private static List<ObjectId> InsertsOf(Transaction tr, BlockTableRecord btr)
    {
        var list = new List<ObjectId>();
        try
        {
            foreach (ObjectId id in btr.GetBlockReferenceIds(directOnly: true, forceValidity: true))
                list.Add(id);
        }
        catch { }
        return list;
    }

    private static object BuildInfo(Database db, Transaction tr, BlockTableRecord btr,
                                    XrefGraphNode? node)
    {
        string? path = null;
        try { path = btr.PathName; } catch { }

        string? found = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var resolved = HostApplicationServices.Current.FindFile(
                    path, db, FindFileHint.XRefDrawing);
                if (!string.IsNullOrWhiteSpace(resolved)) found = resolved;
            }
        }
        catch { /* FindFile throws when it cannot resolve - that IS the answer */ }

        bool nested = node?.IsNested ?? false;
        string? parent = null;
        try
        {
            if (nested && node is not null && node.NumIn > 0 && node.In(0) is XrefGraphNode p)
                parent = p.Name;
        }
        catch { }

        var status = StatusOf(btr);
        return new
        {
            blockName = btr.Name,
            path,
            foundPath = found,
            status,
            isOverlay = node?.XrefStatus == XrefStatus.Resolved && IsOverlay(btr),
            isNested = nested,
            parentName = parent,
            insertCount = InsertsOf(tr, btr).Count,
            isResolved = string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase),
            isUnloaded = string.Equals(status, "Unloaded", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static bool IsOverlay(BlockTableRecord btr)
    {
        try { return btr.IsFromOverlayReference; } catch { return false; }
    }

    private static IEnumerable<(BlockTableRecord Btr, XrefGraphNode? Node)> EnumerateXrefs(
        Database db, Transaction tr, OpenMode mode = OpenMode.ForRead)
    {
        XrefGraph? graph = null;
        try { graph = db.GetHostDwgXrefGraph(includeGhosts: true); } catch { }

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId id in bt)
        {
            var btr = (BlockTableRecord)tr.GetObject(id, mode);
            if (!btr.IsFromExternalReference) continue;

            XrefGraphNode? node = null;
            if (graph is not null)
            {
                try { node = graph.GetXrefNode(btr.ObjectId); } catch { }
            }
            yield return (btr, node);
        }
    }

    // ─────────── attach / detach ───────────

    private static Task<ToolDispatchResult> Attach(JsonObject args, CancellationToken ct, bool overlay) =>
        Run(overlay ? "acad.xrefs.attach_xref_overlay" : "acad.xrefs.attach_xref", args, ct, (doc, db, tr) =>
        {
            var a = Read<AttachXrefArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path)) throw new ArgumentException("path is required.");

            var full = Path.GetFullPath(a.Path);
            if (!File.Exists(full))
                throw new ArgumentException($"XREF source drawing not found: {full}");

            var name = string.IsNullOrWhiteSpace(a.BlockName)
                ? Path.GetFileNameWithoutExtension(full)
                : a.BlockName!;
            AcadEnv.ValidateSymbolName(name, "XrefBlock");

            // Store relative when asked and the two files share a root, because that is the
            // form that survives the project folder being moved or handed over.
            string stored = full;
            if (a.RelativePath)
            {
                try
                {
                    var host = doc.Name;
                    if (!string.IsNullOrWhiteSpace(host) && Path.IsPathRooted(host))
                    {
                        var rel = Path.GetRelativePath(Path.GetDirectoryName(host)!, full);
                        if (!Path.IsPathRooted(rel)) stored = rel.Replace('\\', '/');
                    }
                }
                catch { /* keep the absolute path */ }
            }

            var btrId = overlay ? db.OverlayXref(full, name) : db.AttachXref(full, name);
            if (btrId.IsNull) throw new InvalidOperationException($"AutoCAD did not create an XREF for '{full}'.");

            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
            if (!ReferenceEquals(stored, full))
            {
                try { btr.PathName = stored; } catch (Exception ex) { Log.Warn($"attach_xref: relative path rejected ({ex.Message}); kept absolute."); }
            }

            var br = new BlockReference(
                a.Insertion is null ? Point3d.Origin : AcadEnv.ToPoint3d(a.Insertion), btrId)
            {
                ScaleFactors = new Scale3d(a.ScaleX ?? 1.0, a.ScaleY ?? 1.0, a.ScaleZ ?? 1.0),
                Rotation = (a.RotationDeg ?? 0.0) * Math.PI / 180.0,
            };

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            if (!string.IsNullOrWhiteSpace(a.Layer)) br.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer!);
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);

            return Wrap(new
            {
                blockName = btr.Name,
                entity = AcadEnv.ToHandle(br),
                path = btr.PathName ?? full,
                isOverlay = overlay,
            });
        });

    private static Task<ToolDispatchResult> DetachXref(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.detach_xref", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefRefArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            var node = TryNode(db, btr);
            if (node?.IsNested == true)
                throw new ArgumentException(
                    $"'{a.BlockName}' is nested under another reference and cannot be detached here. " +
                    "Detach it in the parent drawing instead.");

            var id = btr.ObjectId;
            tr.Commit();                 // DetachXref works outside the open transaction
            db.DetachXref(id);
            return Wrap(new { affected = 1, blockName = a.BlockName });
        });

    private static XrefGraphNode? TryNode(Database db, BlockTableRecord btr)
    {
        try { return db.GetHostDwgXrefGraph(true).GetXrefNode(btr.ObjectId); } catch { return null; }
    }

    private static Task<ToolDispatchResult> ReloadXref(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.reload_xref", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefRefArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            var ids = new ObjectIdCollection { btr.ObjectId };
            db.ReloadXrefs(ids);

            var fresh = (BlockTableRecord)tr.GetObject(btr.ObjectId, OpenMode.ForRead);
            return Wrap(new
            {
                xref = BuildInfo(db, tr, fresh, TryNode(db, fresh)),
                inserts = InsertsOf(tr, fresh).Select(id =>
                    AcadEnv.ToHandle((Entity)tr.GetObject(id, OpenMode.ForRead))).ToList(),
            });
        });

    private static Task<ToolDispatchResult> ReloadAllXrefs(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.reload_all_xrefs", args, ct, (doc, db, tr) =>
        {
            var ids = new ObjectIdCollection();
            foreach (var (btr, _) in EnumerateXrefs(db, tr)) ids.Add(btr.ObjectId);
            if (ids.Count > 0) db.ReloadXrefs(ids);

            var list = EnumerateXrefs(db, tr).Select(x => BuildInfo(db, tr, x.Btr, x.Node)).ToList();
            return Wrap(new { xrefs = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> UnloadXref(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.unload_xref", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefRefArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            db.UnloadXrefs(new ObjectIdCollection { btr.ObjectId });
            return Wrap(new { affected = 1, blockName = a.BlockName });
        });

    private static Task<ToolDispatchResult> BindXref(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.bind_xref", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefBindArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            var node = TryNode(db, btr);
            if (node?.IsNested == true)
                throw new ArgumentException(
                    $"'{a.BlockName}' is nested; bind its parent instead.");

            var ids = new ObjectIdCollection { btr.ObjectId };
            tr.Commit();                 // BindXrefs rewrites the symbol tables
            db.BindXrefs(ids, a.InsertMode);
            return Wrap(new { affected = 1, blockName = a.BlockName });
        });

    // ─────────── inspection ───────────

    private static Task<ToolDispatchResult> ListXrefs(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.list_xrefs", args, ct, (doc, db, tr) =>
        {
            var list = EnumerateXrefs(db, tr).Select(x => BuildInfo(db, tr, x.Btr, x.Node)).ToList();
            return Wrap(new { xrefs = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> ListNestedXrefs(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.list_nested_xrefs", args, ct, (doc, db, tr) =>
        {
            var list = EnumerateXrefs(db, tr)
                .Where(x => x.Node?.IsNested == true)
                .Select(x => BuildInfo(db, tr, x.Btr, x.Node)).ToList();
            return Wrap(new { xrefs = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> FindMissingXrefs(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.find_missing_xrefs", args, ct, (doc, db, tr) =>
        {
            var list = new List<object>();
            foreach (var (btr, node) in EnumerateXrefs(db, tr))
            {
                var info = BuildInfo(db, tr, btr, node);
                var found = info.GetType().GetProperty("foundPath")?.GetValue(info) as string;
                var status = info.GetType().GetProperty("status")?.GetValue(info) as string ?? "";
                if (string.IsNullOrWhiteSpace(found) ||
                    status.Equals("Unresolved", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("FileNotFound", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(info);
                }
            }
            return Wrap(new { missing = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> GetXrefInfo(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.get_xref_info", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefRefArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            return Wrap(new
            {
                xref = BuildInfo(db, tr, btr, TryNode(db, btr)),
                inserts = InsertsOf(tr, btr).Select(id =>
                    AcadEnv.ToHandle((Entity)tr.GetObject(id, OpenMode.ForRead))).ToList(),
            });
        });

    private static Task<ToolDispatchResult> ListDependentSymbols(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.list_xref_dependent_symbols", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefRefArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            // Xref-dependent symbols carry the "xrefname|SYMBOL" naming form.
            var prefix = btr.Name + "|";

            List<string> Collect(ObjectId tableId)
            {
                var outp = new List<string>();
                try
                {
                    var table = (SymbolTable)tr.GetObject(tableId, OpenMode.ForRead);
                    foreach (ObjectId id in table)
                    {
                        var rec = (SymbolTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (rec.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            outp.Add(rec.Name);
                    }
                }
                catch { }
                outp.Sort(StringComparer.OrdinalIgnoreCase);
                return outp;
            }

            return Wrap(new
            {
                blockName = btr.Name,
                layers = Collect(db.LayerTableId),
                linetypes = Collect(db.LinetypeTableId),
                textStyles = Collect(db.TextStyleTableId),
                dimStyles = Collect(db.DimStyleTableId),
                blocks = Collect(db.BlockTableId),
            });
        });

    // ─────────── paths ───────────

    private static Task<ToolDispatchResult> SetXrefPath(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.set_xref_path", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetXrefPathArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path)) throw new ArgumentException("path is required.");
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForWrite);

            var stored = a.Path;
            if (a.RelativePath && Path.IsPathRooted(a.Path))
            {
                try
                {
                    var host = doc.Name;
                    if (!string.IsNullOrWhiteSpace(host) && Path.IsPathRooted(host))
                    {
                        var rel = Path.GetRelativePath(Path.GetDirectoryName(host)!, Path.GetFullPath(a.Path));
                        if (!Path.IsPathRooted(rel)) stored = rel.Replace('\\', '/');
                    }
                }
                catch { }
            }

            btr.PathName = stored;
            if (a.Reload)
            {
                db.ResolveXrefs(useThreadEngine: false, doNewOnly: false);
                db.ReloadXrefs(new ObjectIdCollection { btr.ObjectId });
            }

            var fresh = (BlockTableRecord)tr.GetObject(btr.ObjectId, OpenMode.ForRead);
            return Wrap(new
            {
                xref = BuildInfo(db, tr, fresh, TryNode(db, fresh)),
                inserts = InsertsOf(tr, fresh).Select(id =>
                    AcadEnv.ToHandle((Entity)tr.GetObject(id, OpenMode.ForRead))).ToList(),
            });
        });

    private static Task<ToolDispatchResult> RepathAllXrefs(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.repath_all_xrefs", args, ct, (doc, db, tr) =>
        {
            var a = Read<RepathAllArgsDto>(args);
            if (string.IsNullOrEmpty(a.OldPrefix)) throw new ArgumentException("oldPrefix is required.");

            var entries = new List<object>();
            int changed = 0;

            foreach (var (btr, _) in EnumerateXrefs(db, tr, OpenMode.ForRead))
            {
                string? oldPath = null;
                try { oldPath = btr.PathName; } catch { }
                if (string.IsNullOrWhiteSpace(oldPath) ||
                    oldPath!.IndexOf(a.OldPrefix, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var newPath = oldPath.Replace(a.OldPrefix, a.NewPrefix, StringComparison.OrdinalIgnoreCase);

                // Report whether the NEW path actually resolves. A bulk repath that reports
                // success while pointing every reference at nothing is the failure mode this
                // dry run exists to prevent.
                bool resolves;
                try
                {
                    var probe = Path.IsPathRooted(newPath)
                        ? newPath
                        : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(doc.Name) ?? ".", newPath));
                    resolves = File.Exists(probe);
                }
                catch { resolves = false; }

                if (!a.DryRun)
                {
                    var w = (BlockTableRecord)tr.GetObject(btr.ObjectId, OpenMode.ForWrite);
                    w.PathName = newPath;
                    changed++;
                }

                entries.Add(new
                {
                    blockName = btr.Name,
                    oldPath,
                    newPath,
                    applied = !a.DryRun,
                    resolves,
                });
            }

            if (!a.DryRun && changed > 0)
                db.ResolveXrefs(useThreadEngine: false, doNewOnly: false);

            return Wrap(new { entries, changed, dryRun = a.DryRun });
        });

    // ─────────── clipping ───────────

    private static BlockReference OpenInsert(Database db, Transaction tr, string handle, OpenMode mode)
    {
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle), mode);
        if (ent is not BlockReference br)
            throw new ArgumentException(
                $"Handle {handle} is a {ent.GetRXClass().Name}, not a BlockReference. " +
                "Clipping acts on an xref INSERT - take a handle from get_xref_info.inserts.");
        return br;
    }

    private static JsonObject ApplyClip(Database db, Transaction tr, BlockReference br,
                                        Point2dCollection pts, bool inverted)
    {
        if (br.ExtensionDictionary.IsNull) br.CreateExtensionDictionary();
        var extDict = (DBDictionary)tr.GetObject(br.ExtensionDictionary, OpenMode.ForWrite);

        DBDictionary filterDict;
        if (extDict.Contains("ACAD_FILTER"))
        {
            filterDict = (DBDictionary)tr.GetObject(extDict.GetAt("ACAD_FILTER"), OpenMode.ForWrite);
        }
        else
        {
            filterDict = new DBDictionary();
            extDict.SetAt("ACAD_FILTER", filterDict);
            tr.AddNewlyCreatedDBObject(filterDict, true);
        }

        var sf = new SpatialFilter
        {
            Definition = new SpatialFilterDefinition(
                pts, Vector3d.ZAxis, 0.0, double.MaxValue, double.MinValue, enabled: true),
        };
        try { sf.Inverted = inverted; }
        catch (Exception ex) when (inverted)
        {
            throw new InvalidOperationException(
                "Inverted clipping is not supported by this AutoCAD build: " + ex.Message, ex);
        }

        if (filterDict.Contains("SPATIAL"))
        {
            var old = (DBObject)tr.GetObject(filterDict.GetAt("SPATIAL"), OpenMode.ForWrite);
            old.Erase();
        }
        filterDict.SetAt("SPATIAL", sf);
        tr.AddNewlyCreatedDBObject(sf, true);

        return Wrap(new
        {
            handle = AcadEnv.ToHandle(br).Handle,
            clipped = true,
            inverted,
            vertexCount = pts.Count,
        });
    }

    private static Task<ToolDispatchResult> ClipRect(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.clip_xref_rect", args, ct, (doc, db, tr) =>
        {
            var a = Read<ClipRectArgsDto>(args);
            var br = OpenInsert(db, tr, a.Handle, OpenMode.ForWrite);
            double x1 = Math.Min(a.Corner1.X, a.Corner2.X), x2 = Math.Max(a.Corner1.X, a.Corner2.X);
            double y1 = Math.Min(a.Corner1.Y, a.Corner2.Y), y2 = Math.Max(a.Corner1.Y, a.Corner2.Y);
            if (x2 - x1 < 1e-9 || y2 - y1 < 1e-9)
                throw new ArgumentException("clip rectangle is degenerate (zero width or height).");

            // A two-point definition is AutoCAD's own rectangular clip form.
            var pts = new Point2dCollection { new Point2d(x1, y1), new Point2d(x2, y2) };
            return ApplyClip(db, tr, br, pts, a.Inverted);
        });

    private static Task<ToolDispatchResult> ClipPolygonal(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.clip_xref_polygonal", args, ct, (doc, db, tr) =>
        {
            var a = Read<ClipPolyArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 3)
                throw new ArgumentException("polygonal clip needs at least 3 vertices.");
            var br = OpenInsert(db, tr, a.Handle, OpenMode.ForWrite);

            var pts = new Point2dCollection();
            foreach (var v in a.Vertices) pts.Add(new Point2d(v.X, v.Y));
            // Close it if the caller did not.
            if (pts[0].GetDistanceTo(pts[pts.Count - 1]) > 1e-9) pts.Add(pts[0]);

            return ApplyClip(db, tr, br, pts, a.Inverted);
        });

    private static SpatialFilter? OpenFilter(Transaction tr, BlockReference br, OpenMode mode)
    {
        if (br.ExtensionDictionary.IsNull) return null;
        var extDict = (DBDictionary)tr.GetObject(br.ExtensionDictionary, OpenMode.ForRead);
        if (!extDict.Contains("ACAD_FILTER")) return null;
        var fd = (DBDictionary)tr.GetObject(extDict.GetAt("ACAD_FILTER"), OpenMode.ForRead);
        if (!fd.Contains("SPATIAL")) return null;
        return tr.GetObject(fd.GetAt("SPATIAL"), mode) as SpatialFilter;
    }

    private static Task<ToolDispatchResult> InvertClip(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.invert_xref_clip", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefHandleArgsDto>(args);
            var br = OpenInsert(db, tr, a.Handle, OpenMode.ForWrite);
            var sf = OpenFilter(tr, br, OpenMode.ForWrite)
                     ?? throw new ArgumentException($"Insert {a.Handle} has no clip boundary to invert.");
            sf.Inverted = !sf.Inverted;
            return Wrap(new
            {
                handle = a.Handle,
                clipped = true,
                inverted = sf.Inverted,
                vertexCount = sf.Definition.GetPoints().Count,
            });
        });

    private static Task<ToolDispatchResult> DeleteClip(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.delete_xref_clip", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefHandleArgsDto>(args);
            var br = OpenInsert(db, tr, a.Handle, OpenMode.ForWrite);
            var sf = OpenFilter(tr, br, OpenMode.ForWrite);
            if (sf is not null) sf.Erase();
            // No clip is not an error - the requested end state is "unclipped" either way.
            return Wrap(new { handle = a.Handle, clipped = false, inverted = false, vertexCount = 0 });
        });

    private static Task<ToolDispatchResult> SetClipDisplay(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.set_xref_clip_display", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetClipDisplayArgsDto>(args);
            var br = OpenInsert(db, tr, a.Handle, OpenMode.ForRead);
            var sf = OpenFilter(tr, br, OpenMode.ForRead)
                     ?? throw new ArgumentException($"Insert {a.Handle} has no clip boundary.");

            // XCLIPFRAME is a database-wide setting, not per-insert. Say so rather than
            // pretending the call was scoped to this one reference.
            Application.SetSystemVariable("XCLIPFRAME", a.Visible ? (short)2 : (short)0);
            Log.Info($"set_xref_clip_display: XCLIPFRAME is drawing-wide; all clip frames are now {(a.Visible ? "visible" : "hidden")}.");

            return Wrap(new
            {
                handle = a.Handle,
                clipped = true,
                inverted = sf.Inverted,
                vertexCount = sf.Definition.GetPoints().Count,
            });
        });

    /// <summary>Snap millimetres to the nearest standard AutoCAD lineweight. Mirrors
    /// ModifyPluginTools.NearestLineweight - AutoCAD only accepts the enum values.</summary>
    private static LineWeight NearestLineweight(double mm)
    {
        var hundredths = (int)Math.Round(mm * 100.0);
        LineWeight best = LineWeight.LineWeight025;
        int bestDelta = int.MaxValue;
        foreach (LineWeight lw in Enum.GetValues(typeof(LineWeight)))
        {
            int v = (int)lw;
            if (v < 0) continue;                       // ByLayer / ByBlock / Default
            int d = Math.Abs(v - hundredths);
            if (d < bestDelta) { bestDelta = d; best = lw; }
        }
        return best;
    }

    // ─────────── layer overrides ───────────

    private static Task<ToolDispatchResult> SetLayerOverride(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.set_xref_layer_override", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefLayerOverrideArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);
            var full = a.Layer.Contains('|') ? a.Layer : btr.Name + "|" + a.Layer;

            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(full))
                throw new ArgumentException(
                    $"XREF '{btr.Name}' has no layer '{a.Layer}'. " +
                    "Use list_xref_dependent_symbols to see the layers it brings.");

            var ltr = (LayerTableRecord)tr.GetObject(lt[full], OpenMode.ForWrite);
            if (a.Color is not null) ltr.Color = AcadEnv.FromColorDto(a.Color);
            if (!string.IsNullOrWhiteSpace(a.Linetype)) ltr.LinetypeObjectId = AcadEnv.ResolveLinetype(db, tr, a.Linetype!);
            if (a.LineweightMm.HasValue) ltr.LineWeight = NearestLineweight(a.LineweightMm.Value);
            if (a.Off.HasValue) ltr.IsOff = a.Off.Value;
            if (a.Frozen.HasValue) ltr.IsFrozen = a.Frozen.Value;

            return Wrap(new { affected = 1, blockName = btr.Name });
        });

    private static Task<ToolDispatchResult> ResetLayerOverrides(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.reset_xref_layer_overrides", args, ct, (doc, db, tr) =>
        {
            var a = Read<XrefLayerResetArgsDto>(args);
            var btr = FindXrefBtr(db, tr, a.BlockName, OpenMode.ForRead);

            // Reloading the xref is what restores source-drawing layer properties; there is no
            // per-property "revert" in the API. VISRETAIN=0 makes the reload discard overrides.
            var prev = db.Visretain;
            try
            {
                db.Visretain = false;
                db.ReloadXrefs(new ObjectIdCollection { btr.ObjectId });
            }
            finally { db.Visretain = prev; }

            return Wrap(new { affected = 1, blockName = btr.Name });
        });
}
