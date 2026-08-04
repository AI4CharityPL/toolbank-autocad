// AutoCAD plugin handlers for the acad-annotative category.
//
// The object model, because three different things are all called "scale":
//
//   ObjectContextCollection "ACDB_ANNOTATIONSCALES"  - the drawing's scale LIST. Each entry is
//                                                      an AnnotationScale (an ObjectContext)
//                                                      with Name / PaperUnits / DrawingUnits.
//   Database.Cannoscale                              - the CURRENT scale. Setting it changes
//                                                      what new annotative objects get and what
//                                                      model space displays. It does not touch
//                                                      any existing object.
//   ObjectContexts.AddContext(entity, context)       - gives ONE entity a representation at one
//                                                      scale. This is the only thing that makes
//                                                      an object visible at that scale.
//
// An annotative object with no representation at a viewport's scale simply does not draw there.
// That is the single most common "my text vanished" and it is not a bug.

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
// ObjectContexts (the per-entity scale-representation API) lives under Internal, not
// DatabaseServices, despite operating on Entity. Documented so nobody removes it as unused.
using Autodesk.AutoCAD.Internal;

namespace AcadMcp.Plugin.Tools;

internal static class AnnotativePluginTools
{
    private const string ScaleCollection = "ACDB_ANNOTATIONSCALES";

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.annotative.set_annotative", SetAnnotative);
        host.Register("acad.annotative.add_annotation_scale", (a, c) => AddRemoveScale(a, c, add: true));
        host.Register("acad.annotative.remove_annotation_scale", (a, c) => AddRemoveScale(a, c, add: false));
        host.Register("acad.annotative.list_object_annotation_scales", ListObjectScales);
        host.Register("acad.annotative.list_annotative_objects", ListAnnotativeObjects);
        host.Register("acad.annotative.sync_scale_positions", SyncScalePositions);

        host.Register("acad.annotative.list_scale_list", ListScaleList);
        host.Register("acad.annotative.add_scale_to_list", AddScaleToList);
        host.Register("acad.annotative.delete_scale_from_list", DeleteScaleFromList);
        host.Register("acad.annotative.reset_scale_list", ResetScaleList);

        host.Register("acad.annotative.set_current_annotation_scale", SetCurrentScale);
        host.Register("acad.annotative.get_current_annotation_scale", GetCurrentScale);
        host.Register("acad.annotative.set_annotation_visibility", SetAnnoVisibility);
        host.Register("acad.annotative.set_auto_add_scale", SetAutoAddScale);
        host.Register("acad.annotative.get_annotation_settings", GetAnnoSettings);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();
    private static Task<ToolDispatchResult> Run(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);

    // ─────────── helpers ───────────

    private static ObjectContextCollection Scales(Database db)
        => db.ObjectContextManager.GetContextCollection(ScaleCollection)
           ?? throw new InvalidOperationException(
                  $"This drawing has no {ScaleCollection} context collection - it may predate annotative scaling.");

    private static AnnotationScale FindScale(Database db, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("scale name is required.");
        var coll = Scales(db);
        if (coll.GetContext(name) is AnnotationScale s) return s;

        var known = coll.Cast<ObjectContext>().Select(c => c.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        throw new ArgumentException(
            $"No annotation scale named '{name}' in this drawing. Available: {string.Join(", ", known)}. " +
            "Add it with add_scale_to_list first.");
    }

    private static object ScaleDto(AnnotationScale s, string currentName) => new
    {
        name = s.Name,
        paperUnits = s.PaperUnits,
        drawingUnits = s.DrawingUnits,
        scaleFactor = s.DrawingUnits == 0 ? 0 : s.PaperUnits / s.DrawingUnits,
        isCurrent = string.Equals(s.Name, currentName, StringComparison.OrdinalIgnoreCase),
    };

    private static string CurrentName(Database db)
    {
        try { return db.Cannoscale?.Name ?? ""; } catch { return ""; }
    }

    private static List<string> ScalesOf(Entity ent)
    {
        var names = new List<string>();
        try
        {
            var ctxs = ent.Database.ObjectContextManager.GetContextCollection(ScaleCollection);
            if (ctxs is null) return names;
            foreach (ObjectContext c in ctxs)
            {
                try { if (ObjectContexts.HasContext(ent, c)) names.Add(c.Name); } catch { }
            }
        }
        catch { }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static bool IsAnnotative(Entity ent)
    {
        try { return ent.Annotative == AnnotativeStates.True; } catch { return false; }
    }

    private static object ObjDto(Entity ent) => new
    {
        handle = ent.Handle.ToString(),
        objectClass = ent.GetRXClass().Name,
        layer = ent.Layer,
        annotative = IsAnnotative(ent),
        scales = ScalesOf(ent),
    };

    // ─────────── per-object ───────────

    private static Task<ToolDispatchResult> SetAnnotative(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.set_annotative", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoSetArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0) throw new ArgumentException("handles: at least one required.");

            int n = 0; var skipped = new List<string>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                try
                {
                    // Not every entity type supports annotative scaling. Report which ones
                    // rather than throwing away the whole batch for one line.
                    if (ent.Annotative == AnnotativeStates.NotApplicable)
                    {
                        skipped.Add($"{h} ({ent.GetRXClass().Name}: not annotative-capable)");
                        continue;
                    }
                    ent.Annotative = a.Annotative ? AnnotativeStates.True : AnnotativeStates.False;
                    n++;
                }
                catch (Exception ex) { skipped.Add($"{h}: {ex.Message}"); }
            }
            return Wrap(new { affected = n, skipped = skipped.Count > 0 ? skipped : null });
        });

    private static Task<ToolDispatchResult> AddRemoveScale(JsonObject args, CancellationToken ct, bool add) =>
        Run(add ? "acad.annotative.add_annotation_scale" : "acad.annotative.remove_annotation_scale",
            args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoObjScalesArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0) throw new ArgumentException("handles: at least one required.");
            if (a.Scales is null || a.Scales.Count == 0) throw new ArgumentException("scales: at least one required.");

            var ctxs = a.Scales.Select(s => FindScale(db, s)).ToList();
            int n = 0; var skipped = new List<string>();

            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                if (!IsAnnotative(ent))
                {
                    skipped.Add($"{h}: not annotative - call set_annotative first");
                    continue;
                }
                foreach (var c in ctxs)
                {
                    try
                    {
                        if (add)
                        {
                            if (!ObjectContexts.HasContext(ent, c)) { ObjectContexts.AddContext(ent, c); n++; }
                        }
                        else
                        {
                            // Removing the last representation would leave an annotative object
                            // that can never draw anywhere. Refuse and say what to do instead.
                            if (ScalesOf(ent).Count <= 1)
                            {
                                skipped.Add($"{h}: '{c.Name}' is its only representation - use set_annotative false instead");
                                continue;
                            }
                            if (ObjectContexts.HasContext(ent, c)) { ObjectContexts.RemoveContext(ent, c); n++; }
                        }
                    }
                    catch (Exception ex) { skipped.Add($"{h}/{c.Name}: {ex.Message}"); }
                }
            }
            return Wrap(new { affected = n, skipped = skipped.Count > 0 ? skipped : null });
        });

    private static Task<ToolDispatchResult> ListObjectScales(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.list_object_annotation_scales", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoHandlesArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0) throw new ArgumentException("handles: at least one required.");
            var list = a.Handles
                .Select(h => (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForRead))
                .Select(ObjDto).ToList();
            return Wrap(new { objects = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> ListAnnotativeObjects(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.list_annotative_objects", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoScaleFilterArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var list = new List<object>();
            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                if (!IsAnnotative(ent)) continue;
                if (!string.IsNullOrWhiteSpace(a.Scale) &&
                    !ScalesOf(ent).Any(s => string.Equals(s, a.Scale, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                list.Add(ObjDto(ent));
            }
            return Wrap(new { objects = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> SyncScalePositions(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.sync_scale_positions", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoHandlesArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0) throw new ArgumentException("handles: at least one required.");

            var current = db.Cannoscale
                ?? throw new InvalidOperationException("No current annotation scale is set.");
            int n = 0; var skipped = new List<string>();

            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                if (!IsAnnotative(ent)) { skipped.Add($"{h}: not annotative"); continue; }

                var others = ScalesOf(ent)
                    .Where(s => !string.Equals(s, current.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (others.Count == 0) { skipped.Add($"{h}: only one representation, nothing to sync"); continue; }

                // Drop and re-add each other representation. Re-adding derives its placement
                // from the current scale's representation, which is exactly the "reset
                // positions" semantics. Order matters: the current scale is never removed, so
                // the object always keeps at least one representation to derive from.
                try
                {
                    foreach (var name in others)
                    {
                        var c = FindScale(db, name);
                        if (ObjectContexts.HasContext(ent, c)) ObjectContexts.RemoveContext(ent, c);
                        ObjectContexts.AddContext(ent, c);
                    }
                    n++;
                }
                catch (Exception ex) { skipped.Add($"{h}: {ex.Message}"); }
            }
            return Wrap(new { affected = n, skipped = skipped.Count > 0 ? skipped : null });
        });

    // ─────────── drawing scale list ───────────

    private static JsonObject ScaleListPayload(Database db)
    {
        var cur = CurrentName(db);
        var list = Scales(db).Cast<ObjectContext>().OfType<AnnotationScale>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => ScaleDto(s, cur)).ToList();
        return Wrap(new { scales = list, current = cur, count = list.Count });
    }

    private static Task<ToolDispatchResult> ListScaleList(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.list_scale_list", args, ct, (doc, db, tr) => ScaleListPayload(db));

    private static Task<ToolDispatchResult> AddScaleToList(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.add_scale_to_list", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoAddScaleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            if (a.PaperUnits <= 0 || a.DrawingUnits <= 0)
                throw new ArgumentException("paperUnits and drawingUnits must both be > 0.");

            var coll = Scales(db);
            if (coll.GetContext(a.Name) is AnnotationScale existing)
            {
                existing.PaperUnits = a.PaperUnits;
                existing.DrawingUnits = a.DrawingUnits;
            }
            else
            {
                var s = new AnnotationScale
                {
                    Name = a.Name,
                    PaperUnits = a.PaperUnits,
                    DrawingUnits = a.DrawingUnits,
                };
                coll.AddContext(s);
            }
            if (a.MakeCurrent) db.Cannoscale = (AnnotationScale)coll.GetContext(a.Name);

            return Wrap(new { scale = ScaleDto((AnnotationScale)coll.GetContext(a.Name), CurrentName(db)) });
        });

    private static Task<ToolDispatchResult> DeleteScaleFromList(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.delete_scale_from_list", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoScaleNameArgsDto>(args);
            var s = FindScale(db, a.Name);
            if (string.Equals(s.Name, CurrentName(db), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"'{a.Name}' is the current annotation scale. Switch with " +
                    "set_current_annotation_scale before deleting it.");

            // Orphaning representations is worse than refusing, so check first.
            var users = CountUsers(db, tr, s);
            if (users > 0)
                throw new ArgumentException(
                    $"'{a.Name}' is used by {users} annotative object(s). Remove it from them with " +
                    "remove_annotation_scale first, or use list_annotative_objects to find them.");

            Scales(db).RemoveContext(a.Name);
            return Wrap(new { affected = 1 });
        });

    private static int CountUsers(Database db, Transaction tr, ObjectContext ctx)
    {
        int n = 0;
        try
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                try { if (ObjectContexts.HasContext(ent, ctx)) n++; } catch { }
            }
        }
        catch { }
        return n;
    }

    private static Task<ToolDispatchResult> ResetScaleList(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.reset_scale_list", args, ct, (doc, db, tr) =>
        {
            var coll = Scales(db);
            var cur = CurrentName(db);
            var removable = coll.Cast<ObjectContext>().OfType<AnnotationScale>()
                .Where(s => !string.Equals(s.Name, cur, StringComparison.OrdinalIgnoreCase))
                .Where(s => CountUsers(db, tr, s) == 0)
                .Select(s => s.Name).ToList();

            foreach (var name in removable)
            {
                try { coll.RemoveContext(name); } catch { }
            }
            return ScaleListPayload(db);
        });

    // ─────────── current scale / sysvars ───────────

    private static Task<ToolDispatchResult> SetCurrentScale(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.set_current_annotation_scale", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoScaleNameArgsDto>(args);
            var s = FindScale(db, a.Name);
            db.Cannoscale = s;
            return Wrap(new { scale = ScaleDto(s, CurrentName(db)) });
        });

    private static Task<ToolDispatchResult> GetCurrentScale(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.get_current_annotation_scale", args, ct, (doc, db, tr) =>
        {
            var s = db.Cannoscale
                ?? throw new InvalidOperationException("No current annotation scale is set in this drawing.");
            return Wrap(new { scale = ScaleDto(s, s.Name) });
        });

    private static JsonObject AnnoSettings()
    {
        int vis = Convert.ToInt32(Application.GetSystemVariable("ANNOALLVISIBLE"));
        int auto = Convert.ToInt32(Application.GetSystemVariable("ANNOAUTOSCALE"));
        return Wrap(new
        {
            showAllScales = vis != 0,
            // ANNOAUTOSCALE is negative when the feature is disabled, positive when on.
            autoAddScale = auto > 0,
            annoAllVisible = vis,
            annoAutoScale = auto,
        });
    }

    private static Task<ToolDispatchResult> SetAnnoVisibility(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.set_annotation_visibility", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoFlagArgsDto>(args);
            // Int16, like every AutoCAD sysvar. Passing an int throws eInvalidInput - that
            // lesson has been paid for twice already (BACKGROUNDPLOT, FIELDEVAL).
            Application.SetSystemVariable("ANNOALLVISIBLE", a.Enabled ? (short)1 : (short)0);
            return AnnoSettings();
        });

    private static Task<ToolDispatchResult> SetAutoAddScale(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.set_auto_add_scale", args, ct, (doc, db, tr) =>
        {
            var a = Read<AnnoFlagArgsDto>(args);
            // ANNOAUTOSCALE keeps its magnitude and flips sign to disable, so toggling it off
            // and on again preserves which mode the user had chosen.
            int cur = Convert.ToInt32(Application.GetSystemVariable("ANNOAUTOSCALE"));
            int mag = Math.Abs(cur) == 0 ? 4 : Math.Abs(cur);
            Application.SetSystemVariable("ANNOAUTOSCALE", (short)(a.Enabled ? mag : -mag));
            return AnnoSettings();
        });

    private static Task<ToolDispatchResult> GetAnnoSettings(JsonObject args, CancellationToken ct) =>
        Run("acad.annotative.get_annotation_settings", args, ct, (doc, db, tr) => AnnoSettings());
}
