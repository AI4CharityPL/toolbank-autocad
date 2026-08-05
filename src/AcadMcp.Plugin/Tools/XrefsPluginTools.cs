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
        host.Register("acad.xrefs.clip_xref_by_object", ClipByObject);
        host.Register("acad.xrefs.invert_xref_clip", InvertClip);
        host.Register("acad.xrefs.delete_xref_clip", DeleteClip);
        host.Register("acad.xrefs.set_clip_frame_display", SetClipFrameDisplay);

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

    /// <summary>
    /// For operations that MUST NOT run inside a caller-owned transaction: DetachXref and
    /// BindXrefs rewrite the symbol tables wholesale.
    ///
    /// The first version of detach/bind called tr.Commit() from inside the Run delegate and
    /// then invoked them - which crashed AutoCAD outright ("Pipe is broken", plugin gone).
    /// Committing a transaction the runner still owns and then mutating the symbol tables it
    /// was scoped to is not survivable. Any lookup here opens and disposes its own short
    /// transaction first, then the operation runs with none open.
    /// </summary>
    private static async Task<ToolDispatchResult> RunNoTransaction(string key, CancellationToken ct,
        Func<Document, Database, JsonObject> work)
    {
        try
        {
            var doc = Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document open in AutoCAD.");
            using var docLock = doc.LockDocument();
            var json = await UiThreadDispatcher.Run(() => work(doc, doc.Database), ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail(key, ex); }
    }

    /// <summary>Resolve an xref block name to its BTR id in a short, self-contained transaction.</summary>
    private static (ObjectId Id, bool Nested) LookupXrefId(Database db, string blockName)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var btr = FindXrefBtr(db, tr, blockName, OpenMode.ForRead);
        var id = btr.ObjectId;
        var nested = TryNode(db, btr)?.IsNested == true;
        tr.Commit();
        return (id, nested);
    }

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
        RunNoTransaction("acad.xrefs.detach_xref", ct, (doc, db) =>
        {
            var a = Read<XrefRefArgsDto>(args);
            var (id, nested) = LookupXrefId(db, a.BlockName);
            if (nested)
                throw new ArgumentException(
                    $"'{a.BlockName}' is nested under another reference and cannot be detached here. " +
                    "Detach it in the parent drawing instead.");
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
        RunNoTransaction("acad.xrefs.bind_xref", ct, (doc, db) =>
        {
            var a = Read<XrefBindArgsDto>(args);
            var (id, nested) = LookupXrefId(db, a.BlockName);
            if (nested)
                throw new ArgumentException($"'{a.BlockName}' is nested; bind its parent instead.");
            db.BindXrefs(new ObjectIdCollection { id }, a.InsertMode);
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

    private static Task<ToolDispatchResult> ClipByObject(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.clip_xref_by_object", args, ct, (doc, db, tr) =>
        {
            // Same clip as clip_xref_polygonal, but reading the outline from geometry that
            // already exists rather than from a vertex list. On a real project the boundary is
            // usually already drawn - a site outline, a fire compartment, a lease line - and
            // retyping its coordinates is both tedious and a chance to get them wrong.
            var a = Read<XrefClipByObjectArgsDto>(args);
            var br = OpenInsert(db, tr, a.Handle, OpenMode.ForWrite);
            var boundary = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.BoundaryHandle), OpenMode.ForRead);

            var pts = new Point2dCollection();
            switch (boundary)
            {
                case Polyline pl:
                    if (!pl.Closed)
                        throw new ArgumentException(
                            $"Boundary {a.BoundaryHandle} is an open polyline. A clip needs a closed outline.");
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var p2 = pl.GetPoint2dAt(i);
                        pts.Add(p2);
                    }
                    break;

                case Circle c:
                    // A clip boundary is a polygon, so approximate the circle. 64 segments keeps
                    // the error under about 0.1% of the radius, which is finer than any plotted
                    // line width at architectural scales.
                    const int segments = 64;
                    for (int i = 0; i < segments; i++)
                    {
                        double t = 2.0 * Math.PI * i / segments;
                        pts.Add(new Point2d(c.Center.X + c.Radius * Math.Cos(t),
                                            c.Center.Y + c.Radius * Math.Sin(t)));
                    }
                    break;

                default:
                    throw new ArgumentException(
                        $"Boundary {a.BoundaryHandle} is a {boundary.GetRXClass().Name}. " +
                        "Use a closed polyline or a circle.");
            }

            if (pts.Count < 3)
                throw new ArgumentException($"Boundary {a.BoundaryHandle} has fewer than 3 distinct points.");
            if (pts[0].GetDistanceTo(pts[pts.Count - 1]) > 1e-9) pts.Add(pts[0]);

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
            var sf = OpenFilter(tr, br, OpenMode.ForRead)
                     ?? throw new ArgumentException($"Insert {a.Handle} has no clip boundary to invert.");

            // SpatialFilter.Inverted is settable while the filter is being CREATED and inert
            // afterwards: assigning it on a filter already in the extension dictionary is
            // accepted silently and changes nothing. Verified live - invert returned
            // inverted:false twice running, while clip_xref_rect with inverted:true worked.
            // So invert rebuilds the filter from the existing boundary with the flag flipped,
            // rather than reporting a toggle that did not happen.
            var pts = new Point2dCollection();
            foreach (Point2d p in sf.Definition.GetPoints()) pts.Add(p);
            bool want = !sf.Inverted;

            return ApplyClip(db, tr, br, pts, want);
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

    // KNOWN-GAPS A3. This used to be set_xref_clip_display and took a HANDLE, which promised a
    // per-reference setting it could not deliver: XCLIPFRAME is drawing-wide. The handle was read
    // only to check that insert had a clip, so the signature implied a scope the behaviour never
    // had. Renamed, handle dropped, and the sysvar's third state exposed - "visible on screen but
    // not plotted" is precisely what a drafter wants while laying out, and hiding it behind a
    // boolean threw it away.
    private static Task<ToolDispatchResult> SetClipFrameDisplay(JsonObject args, CancellationToken ct) =>
        Run("acad.xrefs.set_clip_frame_display", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetClipFrameDisplayArgsDto>(args);

            short Before() => Convert.ToInt16(Application.GetSystemVariable("XCLIPFRAME"));
            var before = Before();

            short want = (a.Mode ?? "").Trim().ToLowerInvariant() switch
            {
                "hidden" or "off" => 0,
                "display" or "screenonly" => 1,
                "displayandplot" or "on" => 2,
                _ => throw new ArgumentException(
                    "mode must be 'hidden', 'display' (on screen but never plotted) or " +
                    "'displayAndPlot'; got '" + a.Mode + "'."),
            };

            Application.SetSystemVariable("XCLIPFRAME", want);

            static string Name(short v) => v switch
            {
                0 => "hidden", 1 => "display", 2 => "displayAndPlot", _ => "unknown(" + v + ")",
            };

            return Wrap(new
            {
                before = Name(before),
                mode = Name(Before()),
                scope = "drawing",
                note = "XCLIPFRAME is a drawing-wide system variable. This affects the clip " +
                       "frames of EVERY clipped xref and block in the drawing, not one reference.",
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
            {
                // An unresolved xref has no dependent layers in this drawing at all - they are
                // dropped when it fails to load and only come back on a successful reload.
                // Saying "no such layer" there sends the caller hunting for a name problem
                // that does not exist.
                var status = StatusOf(btr);
                if (!string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        $"XREF '{btr.Name}' is {status}, so none of its layers are present in this " +
                        "drawing. Fix the path (set_xref_path / repath_all_xrefs) and reload it first.");

                throw new ArgumentException(
                    $"XREF '{btr.Name}' has no layer '{a.Layer}'. " +
                    "Use list_xref_dependent_symbols to see the layers it brings.");
            }

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
