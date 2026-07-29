// AutoCAD plugin handlers for the acad-files category.
// Registered under "acad.files.<verb>"; everything runs on the UI thread.
//
// Rules: 10, 11, 12, 19, 28-acad-blocks-layers-files-traps.mdc
//   - trap #10: Database.SaveAs is sync-on-UI-thread, format enum lags marketing year.
//   - trap #11: PDF / DWF export must use PlotEngine; Begin* / End* must be paired.
//   - trap #12: db.Audit(report, fix:true) mutates; default to fix:false for inspection.
//   - rule 16: do NOT close the document while transactions are still open.
//
// Heavy file-ops (open / save / close / import / export) run via UiThreadDispatcher
// but generally OUTSIDE a transaction, because the AutoCAD APIs that drive them
// either own their own transaction (db.SaveAs, db.DxfOut) or operate at the
// DocumentManager level (Open, CloseAndDiscard).

using System;
using System.Collections.Generic;
using System.IO;
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
using Autodesk.AutoCAD.PlottingServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadMcp.Plugin.Tools;

internal static class FilesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.files.list_documents",       ListDocuments);
        host.Register("acad.files.get_active_document",  GetActiveDocument);
        host.Register("acad.files.open_document",        OpenDocument);
        host.Register("acad.files.save_document",        SaveDocument);
        host.Register("acad.files.save_document_as",     SaveDocumentAs);
        host.Register("acad.files.close_document",       CloseDocument);
        host.Register("acad.files.import_file",          ImportFile);
        host.Register("acad.files.export_file",          ExportFile);
        host.Register("acad.files.purge_database",       PurgeDatabase);
        host.Register("acad.files.audit_database",       AuditDatabase);
        host.Register("acad.files.new_document",         NewDocument);
    }

    // ─────────── infra ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    /// Run on UI thread WITHOUT opening a transaction (file-ops do their own).
    private static async Task<ToolDispatchResult> RunUi(string toolKey, JsonObject args, CancellationToken ct, Func<JsonObject> work)
    {
        try
        {
            var json = await UiThreadDispatcher.Run(work, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail(toolKey, ex); }
    }

    /// Run on UI thread, acquiring DocumentLock on the background thread first (avoids EDU-modal deadlock).
    private static async Task<ToolDispatchResult> RunUiWithLock(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, JsonObject> work)
    {
        try
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document.");
            using var docLock = doc.LockDocument();
            var json = await UiThreadDispatcher.Run(() => work(doc, doc.Database), ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail(toolKey, ex); }
    }

    // ─────────── helpers ───────────

    /// internal, not private: reused by CheckpointPluginTools so restore
    /// results carry the same shape of document info as the files tools do.
    internal static DocumentInfoDto BuildDocumentInfo(Document doc)
    {
        var db = doc.Database;
        string? path = null;
        try { path = string.IsNullOrEmpty(doc.Name) ? null : doc.Name; } catch { }

        bool readOnly = false;
        try { readOnly = doc.IsReadOnly; } catch { }
        // No public Document.IsModified; the closest signal is Database.UndoOpenCount (>0 means
        // there are uncommitted changes since last save). Fall back to false on older verticals.
        bool modified = false;
        try
        {
            var prop = typeof(Document).GetProperty("IsModified");
            if (prop?.GetValue(doc) is bool b1) modified = b1;
            else
            {
                var dbProp = typeof(Database).GetProperty("DbModified");
                if (dbProp?.GetValue(db) is bool b2) modified = b2;
            }
        }
        catch { }

        string? dwgVer = null;
        try { dwgVer = db.OriginalFileVersion.ToString(); } catch { }

        int entityCount = 0;
        try
        {
            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId _ in ms) entityCount++;
            tr.Commit();
        }
        catch { }

        return new DocumentInfoDto(
            Path: path,
            Name: TryGetDocumentName(doc),
            IsReadOnly: readOnly,
            IsModified: modified,
            DwgVersion: dwgVer,
            EntityCount: entityCount);
    }

    private static string TryGetDocumentName(Document doc)
    {
        try
        {
            var path = doc.Name;
            if (string.IsNullOrEmpty(path)) return "Drawing";
            return Path.GetFileName(path);
        }
        catch { return "Drawing"; }
    }

    // The DwgVersion enum surface differs across AutoCAD verticals (some legacy values are dropped
    // in newer SDKs). Use reflection so this code compiles + degrades gracefully.
    private static DwgVersion ResolveDwgVersion(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return DwgVersion.Current;
        var raw = requested.Trim().ToUpperInvariant();
        var token = raw switch
        {
            "R14" or "14"       => "AC1014",
            "2000"              => "AC1015",
            "2004"              => "AC1018",
            "2007"              => "AC1021",
            "2010"              => "AC1024",
            "2013"              => "AC1027",
            "2018"              => "AC1032",
            "CURRENT" or "NEWEST" => "Current",
            _                   => raw
        };
        if (Enum.TryParse<DwgVersion>(token, ignoreCase: true, out var v)) return v;
        throw new ArgumentException(
            $"Unknown / unsupported dwgVersion '{requested}'. " +
            "Use a token like \"AC1027\" or year like \"2018\" / \"2013\" / \"2010\".");
    }

    private static Document? FindDocumentByPath(string path)
    {
        var dm = AcadApp.DocumentManager;
        if (dm is null) return null;
        var norm = Path.GetFullPath(path);
        foreach (Document d in dm)
        {
            try
            {
                if (string.Equals(Path.GetFullPath(d.Name), norm, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            catch { }
        }
        return null;
    }

    // ─────────── tools ───────────

    private static Task<ToolDispatchResult> ListDocuments(JsonObject args, CancellationToken ct) =>
        RunUi("acad.files.list_documents", args, ct, () =>
        {
            var dm = AcadApp.DocumentManager
                     ?? throw new InvalidOperationException("DocumentManager unavailable.");
            var list = new List<DocumentInfoDto>();
            string? active = null;
            try { active = dm.MdiActiveDocument is { } a ? TryGetDocumentName(a) : null; } catch { }
            foreach (Document d in dm) list.Add(BuildDocumentInfo(d));
            return Wrap(new { documents = list, active });
        });

    private static Task<ToolDispatchResult> GetActiveDocument(JsonObject args, CancellationToken ct) =>
        RunUi("acad.files.get_active_document", args, ct, () =>
        {
            var doc = AcadEnv.RequireActiveDocument();
            return Wrap(new { document = BuildDocumentInfo(doc) });
        });

    private static Task<ToolDispatchResult> OpenDocument(JsonObject args, CancellationToken ct) =>
        RunUi("acad.files.open_document", args, ct, () =>
        {
            var a = Read<OpenDocumentArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path)) throw new ArgumentException("path is required.");
            if (!File.Exists(a.Path)) throw new FileNotFoundException($"File not found: {a.Path}", a.Path);

            var dm = AcadApp.DocumentManager
                     ?? throw new InvalidOperationException("DocumentManager unavailable.");

            // Reuse open document if it is already loaded.
            var existing = FindDocumentByPath(a.Path);
            if (existing is not null)
            {
                dm.MdiActiveDocument = existing;
                return Wrap(new { document = BuildDocumentInfo(existing) });
            }

            // password (if provided): pre-set DBX password; here we rely on DocumentCollection.Open.
            var newDoc = dm.Open(a.Path, a.ReadOnly);
            return Wrap(new { document = BuildDocumentInfo(newDoc) });
        });

    private static Task<ToolDispatchResult> NewDocument(JsonObject args, CancellationToken ct) =>
        RunUi("acad.files.new_document", args, ct, () =>
        {
            var dm = AcadApp.DocumentManager
                     ?? throw new InvalidOperationException("DocumentManager unavailable.");
            // Empty template name → AutoCAD default (acad.dwt / acadiso.dwt depending on locale).
            var doc = dm.Add(string.Empty);
            return Wrap(new { document = BuildDocumentInfo(doc) });
        });

    private static Task<ToolDispatchResult> SaveDocument(JsonObject args, CancellationToken ct) =>
        RunUiWithLock("acad.files.save_document", args, ct, (doc, db) =>
        {
            var path = doc.Name;
            if (string.IsNullOrWhiteSpace(path) ||
                path.StartsWith("Drawing", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Active document has no path. Use save_document_as instead.");
            }
            db.SaveAs(path, DwgVersion.Current);
            return Wrap(new { document = BuildDocumentInfo(doc) });
        });

    private static Task<ToolDispatchResult> SaveDocumentAs(JsonObject args, CancellationToken ct) =>
        RunUiWithLock("acad.files.save_document_as", args, ct, (doc, db) =>
        {
            var a = Read<SaveAsArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path)) throw new ArgumentException("path is required.");
            var ver = ResolveDwgVersion(a.DwgVersion);
            var dir = Path.GetDirectoryName(Path.GetFullPath(a.Path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            db.SaveAs(a.Path, ver);
            return Wrap(new { document = BuildDocumentInfo(doc) });
        });

    private static async Task<ToolDispatchResult> CloseDocument(JsonObject args, CancellationToken ct)
    {
        try
        {
            var a = Read<CloseDocumentArgsDto>(args);
            var dm = AcadApp.DocumentManager
                     ?? throw new InvalidOperationException("DocumentManager unavailable.");
            Document target = string.IsNullOrWhiteSpace(a.Path)
                ? Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document.")
                : FindDocumentByPath(a.Path!) ?? throw new FileNotFoundException(
                    $"No open document matches '{a.Path}'.", a.Path);

            if (a.SaveBeforeClose)
            {
                var path = target.Name;
                if (string.IsNullOrWhiteSpace(path) ||
                    path.StartsWith("Drawing", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Cannot save+close: document has no path. Save it first with save_document_as.");
                using var docLock = target.LockDocument();
                var json = await UiThreadDispatcher.Run(() =>
                {
                    target.CloseAndSave(path);
                    return Wrap(new { affected = 1 });
                }, ct).ConfigureAwait(false);
                return new ToolDispatchResult(true, json, null);
            }
            else
            {
                var json = await UiThreadDispatcher.Run(() =>
                {
                    target.CloseAndDiscard();
                    return Wrap(new { affected = 1 });
                }, ct).ConfigureAwait(false);
                return new ToolDispatchResult(true, json, null);
            }
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.files.close_document", ex); }
    }

    private static Task<ToolDispatchResult> ImportFile(JsonObject args, CancellationToken ct) =>
        RunUiWithLock("acad.files.import_file", args, ct, (doc, db) =>
        {
            var a = Read<ImportFileArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path)) throw new ArgumentException("path is required.");
            if (!File.Exists(a.Path)) throw new FileNotFoundException($"File not found: {a.Path}", a.Path);

            var ext = Path.GetExtension(a.Path).ToUpperInvariant();
            var insertion = a.Insertion is null ? Point3d.Origin : AcadEnv.ToPoint3d(a.Insertion);

            int affected = 0;
            switch (ext)
            {
                case ".DWG":
                {
                    using var src = new Database(false, true);
                    src.ReadDwgFile(a.Path, FileShare.Read, true, "");
                    src.CloseInput(true);

                    var ids = new ObjectIdCollection();
                    using (var tr = src.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(src.BlockTableId, OpenMode.ForRead);
                        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        foreach (ObjectId id in ms) { ids.Add(id); affected++; }
                        tr.Commit();
                    }

                    if (affected > 0)
                    {
                        using var tr2 = db.TransactionManager.StartTransaction();
                        var bt = (BlockTable)tr2.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var ms = (BlockTableRecord)tr2.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        var idMap = new IdMapping();
                        db.WblockCloneObjects(ids, ms.ObjectId, idMap, DuplicateRecordCloning.Replace, false);

                        if (insertion != Point3d.Origin)
                        {
                            var xform = Matrix3d.Displacement(insertion - Point3d.Origin);
                            foreach (IdPair p in idMap)
                            {
                                if (!p.IsCloned || !p.IsPrimary) continue;
                                var ent = tr2.GetObject(p.Value, OpenMode.ForWrite) as Entity;
                                ent?.TransformBy(xform);
                            }
                        }
                        tr2.Commit();
                    }
                    break;
                }
                case ".DXF":
                {
                    using (db.TransactionManager.StartTransaction()) { }
                    db.DxfIn(a.Path, null);
                    affected = -1;
                    break;
                }
                default:
                    throw new ArgumentException(
                        $"Unsupported import extension '{ext}'. Supported: .DWG, .DXF.");
            }

            return Wrap(new { affected = affected < 0 ? 0 : affected });
        });

    private static Task<ToolDispatchResult> ExportFile(JsonObject args, CancellationToken ct) =>
        RunUiWithLock("acad.files.export_file", args, ct, (doc, db) =>
        {
            var a = Read<ExportFileArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))   throw new ArgumentException("path is required.");
            if (string.IsNullOrWhiteSpace(a.Format)) throw new ArgumentException("format is required.");
            var fmt = a.Format.Trim().ToUpperInvariant();

            var dir = Path.GetDirectoryName(Path.GetFullPath(a.Path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            switch (fmt)
            {
                case "DWG":
                    db.SaveAs(a.Path, DwgVersion.Current);
                    break;
                case "DXF":
                    db.DxfOut(a.Path, 16, DwgVersion.Current);
                    break;
                case "PDF":
                case "DWF":
                case "DWFX":
                case "IMAGE":
                case "PNG":
                    PlotToDevice(doc, db, a, fmt);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported format '{a.Format}'. Use DWG, DXF, PDF, DWF, DWFX, IMAGE/PNG.");
            }
            return Wrap(new { path = a.Path });
        });

    private static Task<ToolDispatchResult> PurgeDatabase(JsonObject args, CancellationToken ct) =>
        RunUiWithLock("acad.files.purge_database", args, ct, (doc, db) =>
        {
            int totalRemoved = 0;
            int passes = 0;
            while (passes++ < 16)
            {
                var ids = CollectAllSymbolIds(db);
                if (ids.Count == 0) break;
                db.Purge(ids);
                if (ids.Count == 0) break;

                using var tr = db.TransactionManager.StartTransaction();
                int actuallyErased = 0;
                foreach (ObjectId id in ids)
                {
                    try
                    {
                        var obj = tr.GetObject(id, OpenMode.ForWrite, false, true);
                        if (!obj.IsErased) { obj.Erase(true); actuallyErased++; }
                    }
                    catch { }
                }
                tr.Commit();
                if (actuallyErased == 0) break;
                totalRemoved += actuallyErased;
            }
            return Wrap(new { affected = totalRemoved });
        });

    private static Task<ToolDispatchResult> AuditDatabase(JsonObject args, CancellationToken ct) =>
        RunUiWithLock("acad.files.audit_database", args, ct, (doc, db) =>
        {
            // AutoCAD 2025 SDK exposes Audit as an extension method
            // Database.Audit(bool fixErrors, bool cmdLnEcho). The classic
            // AuditInfo path is sealed/internal on this build (rule 26 §12a),
            // so we prefer the extension. We still try the legacy reflective
            // path first for backward compat with 2020-2024 SDKs that expose
            // Database.Audit(AuditInfo).
            var a = Read<AuditArgsDto>(args);
            int? errorsFound = null;
            int? errorsFixed = null;
            string mode;

            var legacyAudit = typeof(Database).GetMethod(
                "Audit",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Database).Assembly.GetType("Autodesk.AutoCAD.DatabaseServices.AuditInfo")! },
                modifiers: null);

            if (legacyAudit is not null)
            {
                var aiType = legacyAudit.GetParameters()[0].ParameterType;
                object? ai = null;
                foreach (var ctor in aiType.GetConstructors(
                             System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                             System.Reflection.BindingFlags.Instance))
                {
                    var ps = ctor.GetParameters();
                    if (ps.Length == 0) { ai = ctor.Invoke(null); break; }
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(bool))
                    {
                        ai = ctor.Invoke(new object[] { a.Fix });
                        break;
                    }
                }
                if (ai is not null)
                {
                    aiType.GetProperty("FixErrors")?.SetValue(ai, a.Fix);
                    aiType.GetProperty("PrintDest")?.SetValue(ai, 0);
                    legacyAudit.Invoke(db, new[] { ai });
                    errorsFound = (int?)aiType.GetProperty("NumErrors")?.GetValue(ai);
                    errorsFixed = (int?)aiType.GetProperty("NumFixes")?.GetValue(ai);
                    mode = "legacy";
                }
                else
                {
                    // Fall through to the modern extension method if we
                    // couldn't materialise an AuditInfo.
                    db.Audit(a.Fix, bCmdLnEcho: false);
                    mode = "extension-no-counters";
                }
            }
            else
            {
                // AutoCAD 2025+ — Database.Audit(bool, bool) extension only.
                // Counters are not surfaced through the managed API; return
                // {ran:true, fix:a.Fix} and let callers inspect the Editor.
                db.Audit(a.Fix, bCmdLnEcho: false);
                mode = "extension-no-counters";
            }

            return Wrap(new
            {
                ran = true,
                fix = a.Fix,
                mode,
                errorsFound,
                errorsFixed,
            });
        });

    // ─────────── purge helper ───────────

    private static ObjectIdCollection CollectAllSymbolIds(Database db)
    {
        var ids = new ObjectIdCollection();
        using var tr = db.TransactionManager.StartTransaction();
        AddTable<BlockTable>(tr, db.BlockTableId, ids, skipBuiltin: true);
        AddTable<LayerTable>(tr, db.LayerTableId, ids, skipBuiltin: true);
        AddTable<LinetypeTable>(tr, db.LinetypeTableId, ids, skipBuiltin: true);
        AddTable<TextStyleTable>(tr, db.TextStyleTableId, ids, skipBuiltin: true);
        AddTable<DimStyleTable>(tr, db.DimStyleTableId, ids, skipBuiltin: true);
        AddTable<RegAppTable>(tr, db.RegAppTableId, ids, skipBuiltin: true);
        AddTable<UcsTable>(tr, db.UcsTableId, ids, skipBuiltin: false);
        AddTable<ViewTable>(tr, db.ViewTableId, ids, skipBuiltin: false);
        tr.Commit();
        return ids;
    }

    private static void AddTable<T>(Transaction tr, ObjectId tableId, ObjectIdCollection ids, bool skipBuiltin)
        where T : SymbolTable
    {
        var table = (T)tr.GetObject(tableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            try
            {
                var rec = (SymbolTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (skipBuiltin && IsBuiltinSymbol(rec.Name)) continue;
                ids.Add(id);
            }
            catch { }
        }
    }

    private static bool IsBuiltinSymbol(string name) =>
        name.Equals("0", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Continuous", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Standard", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("*", StringComparison.Ordinal);

    // ─────────── PNG media picker ───────────

    /// Parse `PublishToWeb PNG.pc3` canonical media list (names look like
    /// "Sun_hi_(1600.00_x_1280.00_Pixels)") and pick the entry whose pixel
    /// dimensions are closest (in L2 sense) to the caller's request, preferring
    /// entries that cover the request fully. Falls back to the largest available.
    private static string PickPngMedia(PlotSettingsValidator psv, PlotSettings ps, int wantW, int wantH)
    {
        var names = psv.GetCanonicalMediaNameList(ps);
        if (names is null || names.Count == 0)
            return "Sun_hi_(1600.00_x_1280.00_Pixels)";

        (string name, int w, int h, double distance)? bestCover = null;
        (string name, int w, int h, double distance)? bestAny   = null;

        foreach (var item in names)
        {
            var raw = item as string;
            if (string.IsNullOrEmpty(raw)) continue;
            if (!TryParsePixelMedia(raw, out int w, out int h)) continue;
            double dx = w - wantW, dy = h - wantH;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (w >= wantW && h >= wantH)
            {
                if (bestCover is null || distance < bestCover.Value.distance)
                    bestCover = (raw, w, h, distance);
            }
            if (bestAny is null || distance < bestAny.Value.distance)
                bestAny = (raw, w, h, distance);
        }

        return bestCover?.name ?? bestAny?.name ?? "Sun_hi_(1600.00_x_1280.00_Pixels)";
    }

    private static bool TryParsePixelMedia(string raw, out int w, out int h)
    {
        w = h = 0;
        if (string.IsNullOrEmpty(raw)) return false;
        int open = raw.IndexOf('(');
        int close = raw.IndexOf(')');
        if (open < 0 || close <= open) return false;
        var inside = raw.Substring(open + 1, close - open - 1);
        if (inside.IndexOf("Pixel", StringComparison.OrdinalIgnoreCase) < 0) return false;
        var parts = inside.Split(new[] { "_x_", "x" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double dw)) return false;
        // parts[1] ends with "_Pixels"; strip non-numeric tail.
        var tail = new string(parts[1].TakeWhile(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        if (!double.TryParse(tail, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double dh)) return false;
        w = (int)Math.Round(dw);
        h = (int)Math.Round(dh);
        return w > 0 && h > 0;
    }

    // ─────────── plot helper (trap #11) ───────────

    private static void PlotToDevice(Document doc, Database db, ExportFileArgsDto a, string fmt)
    {
        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
            throw new InvalidOperationException("AutoCAD plot engine is busy; retry shortly.");

        // If caller supplied a window rectangle but forgot scope="Window", infer it so
        // the workflow "give me a PNG of this area" needs only one argument.
        bool wantsWindow = a.Window is not null ||
                           string.Equals(a.Scope, "Window", StringComparison.OrdinalIgnoreCase);

        using var tr = db.TransactionManager.StartTransaction();
        var lm = LayoutManager.Current;
        string layoutName = !string.IsNullOrWhiteSpace(a.Layout) ? a.Layout! : lm.CurrentLayout;
        if (!lm.LayoutExists(layoutName))
            throw new ArgumentException($"Layout '{layoutName}' does not exist.");
        var layout = (Layout)tr.GetObject(lm.GetLayoutId(layoutName), OpenMode.ForRead);

        var ps  = new PlotSettings(layout.ModelType);
        ps.CopyFrom(layout);
        var psv = PlotSettingsValidator.Current;

        // Pick device + paper for the requested format.
        string device = fmt switch
        {
            "PDF"            => "DWG To PDF.pc3",
            "DWF" or "DWFX"  => fmt == "DWFX" ? "DWFx ePlot (XPS Compatible).pc3" : "DWF6 ePlot.pc3",
            "IMAGE" or "PNG" => "PublishToWeb PNG.pc3",
            _ => throw new ArgumentException($"Unsupported plot format '{fmt}'.")
        };

        // Pixel-size request: pick a media name that best matches the requested aspect.
        // PublishToWeb PNG.pc3 ships with a discrete list of canonical medias; we pick the
        // closest pre-defined one, then AutoCAD rasterises at that exact pixel resolution.
        // If no widthPx supplied, fall back to the layout's current media name.
        string paper;
        if ((fmt == "IMAGE" || fmt == "PNG") && (a.WidthPx is int wp && a.HeightPx is int hp))
        {
            paper = PickPngMedia(psv, ps, wp, hp);
        }
        else
        {
            paper = !string.IsNullOrWhiteSpace(layout.CanonicalMediaName)
                ? layout.CanonicalMediaName
                : "ANSI_A_(8.50_x_11.00_Inches)";
        }

        try { psv.SetPlotConfigurationName(ps, device, paper); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"AutoCAD does not have plot device '{device}' (or paper '{paper}'): {ex.Message}", ex);
        }

        var scope = wantsWindow ? "WINDOW" : (a.Scope ?? "Extents").Trim().ToUpperInvariant();
        psv.SetPlotType(ps, scope switch
        {
            "DISPLAY"  => Autodesk.AutoCAD.DatabaseServices.PlotType.Display,
            "EXTENTS"  => Autodesk.AutoCAD.DatabaseServices.PlotType.Extents,
            "LIMITS"   => Autodesk.AutoCAD.DatabaseServices.PlotType.Limits,
            "VIEW"     => Autodesk.AutoCAD.DatabaseServices.PlotType.View,
            "WINDOW"   => Autodesk.AutoCAD.DatabaseServices.PlotType.Window,
            "LAYOUT"   => Autodesk.AutoCAD.DatabaseServices.PlotType.Layout,
            _          => Autodesk.AutoCAD.DatabaseServices.PlotType.Extents,
        });

        if (scope == "WINDOW")
        {
            if (a.Window is null)
                throw new ArgumentException(
                    "scope=\"Window\" requires the window rectangle: { xMin, yMin, xMax, yMax } in drawing units.");
            if (a.Window.XMax <= a.Window.XMin || a.Window.YMax <= a.Window.YMin)
                throw new ArgumentException(
                    "window rectangle must satisfy xMax > xMin and yMax > yMin.");
            psv.SetPlotWindowArea(ps,
                new Extents2d(a.Window.XMin, a.Window.YMin, a.Window.XMax, a.Window.YMax));
        }

        psv.SetUseStandardScale(ps, true);
        psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
        psv.SetPlotCentered(ps, true);

        using var pe = PlotFactory.CreatePublishEngine();
        var pi = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = ps };
        var piv = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
        piv.Validate(pi);

        var pp = new PlotProgressDialog(false, 1, true);
        try
        {
            pe.BeginPlot(pp, null);                                  // trap #11: paired Begin/End
            pe.BeginDocument(pi, doc.Name, null, 1, true, a.Path);
            var ppi = new PlotPageInfo();
            pe.BeginPage(ppi, pi, true, null);
            pe.BeginGenerateGraphics(null);
            pe.EndGenerateGraphics(null);
            pe.EndPage(null);
            pe.EndDocument(null);
            pe.EndPlot(null);
        }
        finally
        {
            pp.Destroy();
        }
        tr.Commit();
    }
}
