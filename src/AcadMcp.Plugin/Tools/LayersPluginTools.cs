// AutoCAD plugin handlers for the acad-layers category.
// Registered under "acad.layers.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 28 (layer traps).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.LayerManager;

namespace AcadMcp.Plugin.Tools;

internal static class LayersPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // LayerStateMasks does NOT define an "All" sentinel on every AutoCAD vertical, so OR the
    // documented per-property bits explicitly. Save / restore everything we expose via this MCP.
    private const LayerStateMasks AllLayerStateMasks =
        LayerStateMasks.On | LayerStateMasks.Frozen | LayerStateMasks.Locked |
        LayerStateMasks.Plot | LayerStateMasks.NewViewport | LayerStateMasks.Color |
        LayerStateMasks.LineType | LayerStateMasks.LineWeight | LayerStateMasks.PlotStyle |
        LayerStateMasks.CurrentViewport;

    public static void Register(ToolHost host)
    {
        host.Register("acad.layers.list_layers",          ListLayers);
        host.Register("acad.layers.get_layer",            GetLayer);
        host.Register("acad.layers.create_layer",         CreateLayer);
        host.Register("acad.layers.set_current_layer",    SetCurrentLayer);
        host.Register("acad.layers.set_layer_color",      SetLayerColor);
        host.Register("acad.layers.set_layer_linetype",   SetLayerLinetype);
        host.Register("acad.layers.set_layer_lineweight", SetLayerLineweight);
        host.Register("acad.layers.set_layer_state",      SetLayerState);
        host.Register("acad.layers.rename_layer",         RenameLayer);
        host.Register("acad.layers.delete_layer",         DeleteLayer);
        host.Register("acad.layers.purge_unused_layers",  PurgeUnusedLayers);
        host.Register("acad.layers.save_layer_state",     SaveLayerState);
        host.Register("acad.layers.restore_layer_state",  RestoreLayerState);
        host.Register("acad.layers.list_layer_states",    ListLayerStates);

        // The rest of the LayerStateManager surface: export/import to .las, delete, rename,
        // describe and compare. Separate file because none of the symbol-table patterns above
        // apply - LayerStateManager writes on its own, outside any transaction opened here.
        LayersStatePluginTools.Register(host);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── helpers ───────────

    private static LayerInfo BuildLayerInfo(Database db, Transaction tr, LayerTableRecord ltr)
    {
        string? linetypeName = null;
        try
        {
            var lt = (LinetypeTableRecord)tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead);
            linetypeName = lt.Name;
        }
        catch { }

        return new LayerInfo(
            Name: ltr.Name,
            Color: AcadEnv.ColorToDto(ltr.Color),
            LineweightMm: AcadEnv.LineWeightToMm(ltr.LineWeight),
            Linetype: linetypeName,
            Frozen: ltr.IsFrozen,
            Locked: ltr.IsLocked,
            Off: ltr.IsOff,
            Plottable: ltr.IsPlottable,
            Description: string.IsNullOrEmpty(ltr.Description) ? null : ltr.Description);
    }

    // ─────────── list / get ───────────

    private static Task<ToolDispatchResult> ListLayers(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.list_layers", args, ct, (doc, db, tr) =>
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var layers = new List<LayerInfo>();
            foreach (ObjectId id in lt)
            {
                var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                layers.Add(BuildLayerInfo(db, tr, ltr));
            }
            string current;
            try
            {
                var cur = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
                current = cur.Name;
            }
            catch { current = "0"; }
            return Wrap(new { layers, current });
        });

    private static Task<ToolDispatchResult> GetLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.get_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayerNameArgDto>(args);
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name);
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    // ─────────── create / modify ───────────

    private static Task<ToolDispatchResult> CreateLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.create_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateLayerArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.Name, "Layer");
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
            if (lt.Has(a.Name))
            {
                var existing = (LayerTableRecord)tr.GetObject(lt[a.Name], OpenMode.ForRead);
                return Wrap(new { layer = BuildLayerInfo(db, tr, existing) });
            }
            var ltr = new LayerTableRecord { Name = a.Name };
            if (a.Color is not null) ltr.Color = AcadEnv.FromColorDto(a.Color);
            if (!string.IsNullOrWhiteSpace(a.Linetype)) ltr.LinetypeObjectId = AcadEnv.ResolveLinetype(db, tr, a.Linetype!);
            if (a.LineweightMm.HasValue) ltr.LineWeight = AcadEnv.NearestLineWeight(a.LineweightMm.Value);
            ltr.IsPlottable = a.Plottable;
            if (!string.IsNullOrWhiteSpace(a.Description)) ltr.Description = a.Description;
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> SetCurrentLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.set_current_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayerNameArgDto>(args);
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name);
            if (ltr.IsFrozen)
                throw new InvalidOperationException("Cannot set a frozen layer as current. Thaw it first.");
            db.Clayer = ltr.ObjectId;
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> SetLayerColor(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.set_layer_color", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLayerColorArgsDto>(args);
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name, OpenMode.ForWrite);
            ltr.Color = AcadEnv.FromColorDto(a.Color);
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> SetLayerLinetype(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.set_layer_linetype", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLayerLinetypeArgsDto>(args);
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name, OpenMode.ForWrite);
            ltr.LinetypeObjectId = AcadEnv.ResolveLinetype(db, tr, a.Linetype);
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> SetLayerLineweight(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.set_layer_lineweight", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLayerLineweightArgsDto>(args);
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name, OpenMode.ForWrite);
            ltr.LineWeight = AcadEnv.NearestLineWeight(a.LineweightMm);
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> SetLayerState(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.set_layer_state", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLayerStateArgsDto>(args);
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name, OpenMode.ForWrite);
            // Trap #6/8 from rule 28: cannot freeze the current layer.
            if (a.Frozen.HasValue && a.Frozen.Value && ltr.ObjectId == db.Clayer)
                throw new InvalidOperationException("Cannot freeze the current layer. Switch the current layer first.");
            if (a.Frozen.HasValue)    ltr.IsFrozen    = a.Frozen.Value;
            if (a.Locked.HasValue)    ltr.IsLocked    = a.Locked.Value;
            if (a.Off.HasValue)       ltr.IsOff       = a.Off.Value;
            if (a.Plottable.HasValue) ltr.IsPlottable = a.Plottable.Value;
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> RenameLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.rename_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<RenameLayerArgsDto>(args);
            if (string.Equals(a.OldName, "0", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Layer '0' cannot be renamed.");
            AcadEnv.ValidateSymbolName(a.NewName, "Layer");
            var ltr = AcadEnv.ResolveLayer(db, tr, a.OldName, OpenMode.ForWrite);
            ltr.Name = a.NewName;
            return Wrap(new { layer = BuildLayerInfo(db, tr, ltr) });
        });

    private static Task<ToolDispatchResult> DeleteLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.delete_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayerNameArgDto>(args);
            if (a.Name == "0" || string.Equals(a.Name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Layer '{a.Name}' is protected and cannot be deleted.");
            var ltr = AcadEnv.ResolveLayer(db, tr, a.Name, OpenMode.ForWrite);
            if (ltr.ObjectId == db.Clayer)
                throw new InvalidOperationException("Cannot delete the current layer.");
            // Erase only succeeds if no entity references the layer.
            ltr.Erase(true);
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> PurgeUnusedLayers(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.purge_unused_layers", args, ct, (doc, db, tr) =>
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var ids = new ObjectIdCollection();
            foreach (ObjectId id in lt) ids.Add(id);
            db.Purge(ids);
            int removed = 0;
            foreach (ObjectId id in ids)
            {
                if (id == db.Clayer) continue;
                try
                {
                    var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                    if (rec.Name == "0" || string.Equals(rec.Name, "Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
                    rec.Erase(true);
                    removed++;
                }
                catch { /* skip layers that can't be erased */ }
            }
            return Wrap(new { affected = removed });
        });

    // ─────────── named layer states ───────────

    private static Task<ToolDispatchResult> SaveLayerState(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.save_layer_state", args, ct, (doc, db, tr) =>
        {
            var a = Read<SaveLayerStateArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.Name, "LayerState");
            var lsm = new LayerStateManager(db);
            if (lsm.HasLayerState(a.Name)) lsm.DeleteLayerState(a.Name);
            // Third argument is the VIEWPORT the state belongs to, not a layer.
            // This passed db.Clayer - the current layer's ObjectId - so AutoCAD rejected
            // every save with eNotThatKindOfClass, which in turn made restore_layer_state
            // fail with "does not exist" because nothing was ever written.
            // ObjectId.Null means "not viewport-specific", matching RestoreLayerState below.
            lsm.SaveLayerState(a.Name, AllLayerStateMasks, ObjectId.Null);
            if (!string.IsNullOrWhiteSpace(a.Description))
                lsm.SetLayerStateDescription(a.Name, a.Description);
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> RestoreLayerState(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.restore_layer_state", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayerNameArgDto>(args);
            var lsm = new LayerStateManager(db);
            if (!lsm.HasLayerState(a.Name))
                throw new ArgumentException($"Layer state '{a.Name}' does not exist.");
            lsm.RestoreLayerState(a.Name, ObjectId.Null, 0, AllLayerStateMasks);
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> ListLayerStates(JsonObject args, CancellationToken ct) =>
        Run("acad.layers.list_layer_states", args, ct, (doc, db, tr) =>
        {
            var lsm = new LayerStateManager(db);
            var names = new List<string>();
            // GetLayerStateNames returns StringCollection (non-generic IEnumerable), and on a
            // drawing that has never held a layer state it can come back null - which made
            // this throw NullReferenceException instead of answering with an empty list.
            // "None saved yet" is a normal state, not an error.
            var coll = lsm.GetLayerStateNames(false, false);
            if (coll is not null)
            {
                foreach (string? n in coll)
                    if (!string.IsNullOrEmpty(n)) names.Add(n!);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return Wrap(new { items = names, count = names.Count });
        });
}
