// AutoCAD plugin handlers for the acad-underlays category (roadmap 3.5, underlay half).
// Registered under "acad.underlays.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED API shape - see the header comment in the backend's UnderlaysTools.cs for the full
// probe trail. DgnReference/DwfReference share a common UnderlayReference base (Position,
// Rotation, ScaleFactors, Contrast, Fade, Monochrome, GetClipBoundary/SetClipBoundary as
// Point2d[] - NOT Point2dCollection, unlike RasterImage's clip API - IsClipped, Width, Height),
// and DgnDefinition/DwfDefinition share UnderlayDefinition (SourceFileName, ItemName,
// ActiveFileName, Load(password)). Layer-level visibility and Bind are confirmed ABSENT - seven
// candidate names tried, none compiled - so this category has no list_underlay_layers,
// set_underlay_layer_visibility or bind_underlay.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class UnderlaysPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private const string DgnDictKey = "ACAD_DGN_DEFINITIONS";
    private const string DwfDictKey = "ACAD_DWF_DEFINITIONS";

    public static void Register(ToolHost host)
    {
        host.Register("acad.underlays.attach_dgn_underlay", AttachDgnUnderlay);
        host.Register("acad.underlays.attach_dwf_underlay", AttachDwfUnderlay);
        host.Register("acad.underlays.list_underlays",       ListUnderlays);
        host.Register("acad.underlays.detach_underlay",      DetachUnderlay);
        host.Register("acad.underlays.clip_underlay",        ClipUnderlay);
        host.Register("acad.underlays.set_underlay_adjust",  SetUnderlayAdjust);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── shared helpers ───────────

    private static ObjectId DictId(Database db, Transaction tr, string key)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        return nod.Contains(key) ? nod.GetAt(key) : ObjectId.Null;
    }

    private static DBDictionary GetOrCreateDict(Database db, Transaction tr, string key)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (nod.Contains(key))
            return (DBDictionary)tr.GetObject(nod.GetAt(key), OpenMode.ForWrite);
        nod.UpgradeOpen();
        var dict = new DBDictionary();
        nod.SetAt(key, dict);
        tr.AddNewlyCreatedDBObject(dict, true);
        return dict;
    }

    private static string? FindDefName(DBDictionary dict, ObjectId defId)
    {
        foreach (DBDictionaryEntry e in dict)
            if (e.Value == defId) return e.Key;
        return null;
    }

    private static BlockTableRecord ModelSpace(Database db, Transaction tr, OpenMode mode)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], mode);
    }

    private static string DictKeyFor(UnderlayReference u) => u is DgnReference ? DgnDictKey : DwfDictKey;
    private static string KindOf(UnderlayReference u) => u is DgnReference ? "dgn" : "dwf";

    private static UnderlayReference RequireUnderlay(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required: which underlay.");
        var id = AcadEnv.ResolveHandle(db, handle!);
        if (tr.GetObject(id, mode) is not UnderlayReference u)
            throw new ArgumentException($"Handle '{handle}' is not an underlay reference (DGN or DWF).");
        return u;
    }

    private static object DescribeUnderlay(Transaction tr, UnderlayReference u, DBDictionary? dict)
    {
        string name = dict is null ? "<unknown>" : (FindDefName(dict, u.DefinitionId) ?? "<unknown>");
        string path = "";
        string? itemName = null;
        try
        {
            var def = (UnderlayDefinition)tr.GetObject(u.DefinitionId, OpenMode.ForRead);
            path = def.ActiveFileName ?? def.SourceFileName ?? "";
            itemName = def.ItemName;
        }
        catch (Exception) { /* definition missing or unreadable - report what we can */ }

        double rotDeg = u.Rotation * 180.0 / Math.PI;
        return new
        {
            handle = u.Handle.ToString(),
            name,
            kind = KindOf(u),
            path,
            itemName,
            insertionPoint = AcadEnv.FromPoint3d(u.Position),
            rotationDegrees = rotDeg,
            scale = u.ScaleFactors.X,
            extents = AcadEnv.BoundsOf(u.GeometricExtents),
            // MEASURED live: IsClipped reads back TRUE on a freshly attached, never-clipped
            // underlay - it is not a "has a custom clip been applied" flag the way RasterImage's
            // IsClipped is. GetClipBoundary().Length is the honest signal: empty until
            // SetClipBoundary is actually called with real points.
            clipped = u.GetClipBoundary().Length > 0,
            adjust = new { contrast = u.Contrast, fade = u.Fade, monochrome = u.Monochrome },
            layer = u.Layer,
        };
    }

    // ─────────── attaching ───────────

    private static Task<ToolDispatchResult> AttachDgnUnderlay(JsonObject args, CancellationToken ct) =>
        AttachUnderlay("acad.underlays.attach_dgn_underlay", args, ct, DgnDictKey,
            path => new DgnDefinition { SourceFileName = path },
            () => new DgnReference());

    private static Task<ToolDispatchResult> AttachDwfUnderlay(JsonObject args, CancellationToken ct) =>
        AttachUnderlay("acad.underlays.attach_dwf_underlay", args, ct, DwfDictKey,
            path => new DwfDefinition { SourceFileName = path },
            () => new DwfReference());

    private static Task<ToolDispatchResult> AttachUnderlay(
        string toolKey, JsonObject args, CancellationToken ct, string dictKey,
        Func<string, UnderlayDefinition> makeDef, Func<UnderlayReference> makeRef) =>
        Run(toolKey, args, ct, (doc, db, tr) =>
        {
            var a = Read<UnderlayAttachArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the underlay file to attach.");
            if (!File.Exists(a.Path))
                throw new ArgumentException($"No file at '{a.Path}'.");
            var insertion = AcadEnv.ToPoint3d(a.InsertionPoint);

            var dict = GetOrCreateDict(db, tr, dictKey);
            string name = string.IsNullOrWhiteSpace(a.Name)
                ? Path.GetFileNameWithoutExtension(a.Path)
                : a.Name!;
            string requestedResolved = Path.GetFullPath(a.Path!);
            string requestedItem = a.ItemName ?? "";

            bool reusedDef;
            ObjectId defId;
            if (dict.Contains(name))
            {
                var existingId = dict.GetAt(name);
                var existingDef = (UnderlayDefinition)tr.GetObject(existingId, OpenMode.ForRead);
                string existingResolved = Path.GetFullPath(
                    existingDef.ActiveFileName ?? existingDef.SourceFileName ?? "");
                string existingItem = existingDef.ItemName ?? "";
                if (!string.Equals(existingResolved, requestedResolved, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existingItem, requestedItem, StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"An underlay called '{name}' is already attached from a different file " +
                        $"or item ('{existingDef.ActiveFileName}', item '{existingItem}'). Pick a " +
                        "different name, or detach_underlay the existing one first.");
                defId = existingId;
                reusedDef = true;
            }
            else
            {
                var def = makeDef(a.Path!);
                // MEASURED live: ItemName forced to "" (empty string) throws eInvalidInput /
                // eLoadFailed on files that loaded fine once ItemName was left UNSET instead - an
                // explicit empty string is not the same as never touching the property, unlike
                // RasterImageDef which has no equivalent concept at all.
                if (!string.IsNullOrEmpty(a.ItemName)) def.ItemName = a.ItemName;
                try { def.Load(""); }
                catch (Exception ex)
                {
                    throw new ArgumentException(
                        $"AutoCAD could not load '{a.Path}'" +
                        (string.IsNullOrEmpty(a.ItemName) ? "" : $" (item '{a.ItemName}')") +
                        $" as an underlay: {ex.Message}. If the file has more than one " +
                        "model/sheet, itemName must name one of them exactly.");
                }
                defId = dict.SetAt(name, def);
                tr.AddNewlyCreatedDBObject(def, true);
                reusedDef = false;
            }

            var u = makeRef();
            u.DefinitionId = defId;
            var ms = ModelSpace(db, tr, OpenMode.ForWrite);
            if (!string.IsNullOrWhiteSpace(a.Layer)) u.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer);
            ms.AppendEntity(u);
            tr.AddNewlyCreatedDBObject(u, true);

            u.Position = insertion;
            u.Rotation = (a.RotationDegrees ?? 0) * Math.PI / 180.0;
            u.ScaleFactors = new Scale3d(a.Scale ?? 1.0);

            return Wrap(new
            {
                underlay = DescribeUnderlay(tr, u, dict),
                reusedDefinition = reusedDef,
                note = reusedDef
                    ? "Reused the existing definition '" + name + "' - same source file and item, " +
                      "so this is a second placement of it rather than a duplicate."
                    : "New definition created and loaded.",
            });
        });

    // ─────────── reading ───────────

    private static Task<ToolDispatchResult> ListUnderlays(JsonObject args, CancellationToken ct) =>
        Run("acad.underlays.list_underlays", args, ct, (doc, db, tr) =>
        {
            var dgnDictId = DictId(db, tr, DgnDictKey);
            var dwfDictId = DictId(db, tr, DwfDictKey);
            DBDictionary? dgnDict = dgnDictId.IsNull ? null : (DBDictionary)tr.GetObject(dgnDictId, OpenMode.ForRead);
            DBDictionary? dwfDict = dwfDictId.IsNull ? null : (DBDictionary)tr.GetObject(dwfDictId, OpenMode.ForRead);

            var found = new List<object>();
            var ms = ModelSpace(db, tr, OpenMode.ForRead);
            foreach (ObjectId eid in ms)
            {
                if (eid.IsErased) continue;                        // rule 26 section 8
                if (tr.GetObject(eid, OpenMode.ForRead) is UnderlayReference u)
                    found.Add(DescribeUnderlay(tr, u, u is DgnReference ? dgnDict : dwfDict));
            }
            return Wrap(new
            {
                count = found.Count,
                underlays = found,
                note = "No definition dictionary yet means no underlay of that kind has ever been " +
                       "attached, reported as zero rather than an error.",
            });
        });

    // ─────────── detaching ───────────

    private static Task<ToolDispatchResult> DetachUnderlay(JsonObject args, CancellationToken ct) =>
        Run("acad.underlays.detach_underlay", args, ct, (doc, db, tr) =>
        {
            var a = Read<UnderlayHandleArgsDto>(args);
            var u = RequireUnderlay(db, tr, a.Handle, OpenMode.ForWrite);
            var defId = u.DefinitionId;
            string dictKey = DictKeyFor(u);

            var dictId = DictId(db, tr, dictKey);
            DBDictionary? dict = dictId.IsNull ? null : (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);
            string name = dict is null ? "<unknown>" : (FindDefName(dict, defId) ?? "<unknown>");

            u.Erase();

            bool stillUsed = false;
            var ms = ModelSpace(db, tr, OpenMode.ForRead);
            foreach (ObjectId eid in ms)
            {
                if (eid.IsErased) continue;
                if (tr.GetObject(eid, OpenMode.ForRead) is UnderlayReference other && other.DefinitionId == defId)
                {
                    stillUsed = true;
                    break;
                }
            }

            bool defRemoved = false;
            if (!stillUsed && dict is not null && name != "<unknown>" && dict.Contains(name))
            {
                var def = (UnderlayDefinition)tr.GetObject(defId, OpenMode.ForWrite);
                dict.Remove(name);
                def.Erase();
                defRemoved = true;
            }

            if (!u.IsErased)
                throw new InvalidOperationException("The entity did not actually erase.");

            return Wrap(new
            {
                handle = a.Handle,
                name,
                defRemoved,
                note = defRemoved
                    ? "No other placement used the same source definition, so it was removed as " +
                      "well as the entity."
                    : "Another placement still uses the same source definition, so only this " +
                      "entity was removed.",
            });
        });

    // ─────────── clipping ───────────

    private static Task<ToolDispatchResult> ClipUnderlay(JsonObject args, CancellationToken ct) =>
        Run("acad.underlays.clip_underlay", args, ct, (doc, db, tr) =>
        {
            var a = Read<UnderlayClipArgsDto>(args);
            var u = RequireUnderlay(db, tr, a.Handle, OpenMode.ForWrite);

            var extentsBefore = AcadEnv.BoundsOf(u.GeometricExtents);
            double uW = u.Width, uH = u.Height;

            int count = a.Points?.Count ?? 0;
            if (count == 0)
            {
                u.SetClipBoundary(Array.Empty<Point2d>());
            }
            else
            {
                if (count < 2)
                    throw new ArgumentException(
                        "points needs at least 2 (a rectangle, two opposite corners) or 3+ (a polygon).");
                Point2d[] pts;
                if (count == 2)
                {
                    var p0 = a.Points![0];
                    var p1 = a.Points[1];
                    pts = new[]
                    {
                        new Point2d(p0.X, p0.Y), new Point2d(p1.X, p0.Y),
                        new Point2d(p1.X, p1.Y), new Point2d(p0.X, p1.Y), new Point2d(p0.X, p0.Y),
                    };
                }
                else
                {
                    var list = a.Points!.Select(p => new Point2d(p.X, p.Y)).ToList();
                    var first = list[0];
                    var last = list[^1];
                    if (System.Math.Abs(first.X - last.X) > 1e-9 || System.Math.Abs(first.Y - last.Y) > 1e-9)
                        list.Add(first);
                    pts = list.ToArray();
                }
                u.SetClipBoundary(pts);
            }

            var boundaryBack = u.GetClipBoundary();
            var extentsAfter = AcadEnv.BoundsOf(u.GeometricExtents);

            return Wrap(new
            {
                handle = a.Handle,
                // MEASURED live: IsClipped is always true, even with an empty boundary -
                // GetClipBoundary().Length is what actually reflects whether a clip is in effect.
                clipped = boundaryBack?.Length > 0,
                boundaryPointCount = boundaryBack?.Length ?? 0,
                underlayWidth = uW,
                underlayHeight = uH,
                extentsBefore,
                extentsAfter,
                note = "points are in the underlay's OWN local coordinates - (0,0) to " +
                       "(underlayWidth, underlayHeight) BEFORE scale - not drawing coordinates. " +
                       "Omitting points removes the clip.",
            });
        });

    // ─────────── adjust ───────────

    private static Task<ToolDispatchResult> SetUnderlayAdjust(JsonObject args, CancellationToken ct) =>
        Run("acad.underlays.set_underlay_adjust", args, ct, (doc, db, tr) =>
        {
            var a = Read<UnderlayAdjustArgsDto>(args);
            if (a.Contrast is null && a.Fade is null && a.Monochrome is null)
                throw new ArgumentException(
                    "Nothing to change. Give at least one of contrast, fade, monochrome.");
            foreach (var (label, v) in new (string, int?)[] { ("contrast", a.Contrast), ("fade", a.Fade) })
                if (v is not null && (v < 0 || v > 100))
                    throw new ArgumentException($"{label} runs 0-100, got {v}.");

            var u = RequireUnderlay(db, tr, a.Handle, OpenMode.ForWrite);

            var before = new { contrast = u.Contrast, fade = u.Fade, monochrome = u.Monochrome };
            var changed = new List<string>();
            if (a.Contrast is not null) { u.Contrast = a.Contrast.Value; changed.Add("contrast"); }
            if (a.Fade is not null) { u.Fade = a.Fade.Value; changed.Add("fade"); }
            if (a.Monochrome is not null) { u.Monochrome = a.Monochrome.Value; changed.Add("monochrome"); }
            var after = new { contrast = u.Contrast, fade = u.Fade, monochrome = u.Monochrome };

            return Wrap(new
            {
                handle = a.Handle,
                before,
                after,
                note = "Only " + string.Join(", ", changed) + " changed; the others are read and " +
                       "reported unchanged.",
            });
        });
}
