// AutoCAD plugin handlers for the rest of named layer states — roadmap 2.4.
//
// acad-layers already saves, restores and lists them. What was missing is everything that makes
// a saved state maintainable: an agent could create one and was then stuck with it, unable to
// delete, rename, describe, compare or move it between drawings.
//
// These all hang off LayerStateManager, which is NOT a symbol table and NOT a dictionary — it is
// a wrapper constructed per Database with its own method surface, so none of the transaction
// patterns elsewhere in this file's neighbours apply. In particular it does its own writing:
// there is no record to open ForWrite and no AddNewlyCreatedDBObject.
//
// One trap carried over from save_layer_state next door: the viewport argument is an ObjectId of
// a VIEWPORT, and passing anything else — db.Clayer was the original bug — is rejected with
// eNotThatKindOfClass. ObjectId.Null means "not viewport-specific" and is what every call here
// uses.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcadMcp.Plugin.Tools;

internal static class LayersStatePluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.layers.export_layer_state", ExportLayerState);
        host.Register("acad.layers.import_layer_state", ImportLayerState);
        host.Register("acad.layers.delete_layer_state", DeleteLayerState);
        host.Register("acad.layers.rename_layer_state", RenameLayerState);
        host.Register("acad.layers.set_layer_state_description", SetLayerStateDescription);
        host.Register("acad.layers.compare_layer_state", CompareLayerState);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static LayerStateManager Manager(Database db) => new(db);

    private static void RequireState(LayerStateManager lsm, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required.");
        if (!lsm.HasLayerState(name))
            throw new ArgumentException(
                "No layer state named '" + name + "'. Use list_layer_states.");
    }

    private static Task<ToolDispatchResult> ExportLayerState(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.layers.export_layer_state", ct, (doc, db, tr) =>
        {
            var a = Read<LayerStateFileArgsDto>(args);
            var lsm = Manager(db);
            RequireState(lsm, a.Name);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: where to write the .las file.");

            var path = a.Path!;
            if (!Path.HasExtension(path)) path += ".las";
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                throw new ArgumentException("No such directory: " + dir);
            if (File.Exists(path) && !a.Overwrite)
                throw new ArgumentException(
                    "'" + path + "' already exists. Pass overwrite:true to replace it.");

            lsm.ExportLayerState(a.Name, path);

            // Confirm from the filesystem rather than from the absence of an exception. An
            // export that wrote nothing is the failure a caller cannot see.
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "AutoCAD reported no error but '" + path + "' was not created.");

            return Wrap(new { name = a.Name, path, bytes = new FileInfo(path).Length });
        });

    private static Task<ToolDispatchResult> ImportLayerState(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.layers.import_layer_state", ct, (doc, db, tr) =>
        {
            var a = Read<LayerStateFileArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the .las file to read.");
            if (!File.Exists(a.Path))
                throw new ArgumentException("No such file: " + a.Path);

            var lsm = Manager(db);

            // The state's name comes from inside the file, not from the caller, so which states
            // arrived can only be established by diffing the manager before and after.
            var before = lsm.GetLayerStateNames(false, false).Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            try
            {
                lsm.ImportLayerState(a.Path);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.DuplicateRecordName)
            {
                throw new ArgumentException(
                    "A layer state from '" + Path.GetFileName(a.Path) + "' already exists in this " +
                    "drawing. AutoCAD will not import over it - delete or rename the local one " +
                    "first with delete_layer_state or rename_layer_state.", ex);
            }

            var after = lsm.GetLayerStateNames(false, false).Cast<string>().ToList();
            var added = after.Where(n => !before.Contains(n)).ToList();

            return Wrap(new
            {
                path = a.Path,
                imported = added,
                count = added.Count,
                note = added.Count == 0
                    ? "The file was read but no new layer state appeared, which means its state " +
                      "was already present under the same name."
                    : null,
            });
        });

    private static Task<ToolDispatchResult> DeleteLayerState(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.layers.delete_layer_state", ct, (doc, db, tr) =>
        {
            var a = Read<LayerStateNameArgsDto>(args);
            var lsm = Manager(db);
            RequireState(lsm, a.Name);

            lsm.DeleteLayerState(a.Name);

            return Wrap(new
            {
                name = a.Name,
                deleted = !lsm.HasLayerState(a.Name),
                note = "Layers are untouched. A layer state records visibility and properties; " +
                       "deleting it removes the recording, not the layers.",
            });
        });

    private static Task<ToolDispatchResult> RenameLayerState(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.layers.rename_layer_state", ct, (doc, db, tr) =>
        {
            var a = Read<RenameLayerStateArgsDto>(args);
            var lsm = Manager(db);
            RequireState(lsm, a.Name);
            if (string.IsNullOrWhiteSpace(a.NewName))
                throw new ArgumentException("newName is required.");
            AcadEnv.ValidateSymbolName(a.NewName, "LayerState");
            if (lsm.HasLayerState(a.NewName))
                throw new ArgumentException(
                    "A layer state named '" + a.NewName + "' already exists.");

            lsm.RenameLayerState(a.Name, a.NewName);

            return Wrap(new
            {
                oldName = a.Name,
                name = a.NewName,
                renamed = lsm.HasLayerState(a.NewName) && !lsm.HasLayerState(a.Name),
            });
        });

    private static Task<ToolDispatchResult> SetLayerStateDescription(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.layers.set_layer_state_description", ct, (doc, db, tr) =>
        {
            var a = Read<LayerStateDescriptionArgsDto>(args);
            var lsm = Manager(db);
            RequireState(lsm, a.Name);

            lsm.SetLayerStateDescription(a.Name, a.Description ?? "");

            return Wrap(new
            {
                name = a.Name,
                description = lsm.GetLayerStateDescription(a.Name) ?? "",
            });
        });

    private static Task<ToolDispatchResult> CompareLayerState(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.layers.compare_layer_state", ct, (doc, db, tr) =>
        {
            var a = Read<LayerStateNameArgsDto>(args);
            var lsm = Manager(db);
            RequireState(lsm, a.Name);

            // CompareLayerStateToDb returns TRUE WHEN THEY MATCH. The name suggests a comparison
            // result and says nothing about polarity, so the first version negated it and this
            // tool answered the exact opposite of the truth - in a tool whose only job is saying
            // whether restoring would change anything, which is the worst place in the bank for
            // an inverted boolean.
            //
            // Established by measurement, not by reading the name: right after saving a state the
            // method returned true; after turning one layer off it returned false; after
            // restoring, true again. Same mistake as guessing IdPair.IsCloned in
            // import_dimstyle_from_dwg, and found the same way.
            var matches = lsm.CompareLayerStateToDb(a.Name, ObjectId.Null);
            var layers = lsm.GetLayerStateLayers(a.Name, false).Cast<string>()
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            return Wrap(new
            {
                name = a.Name,
                description = lsm.GetLayerStateDescription(a.Name) ?? "",
                matchesCurrentDrawing = matches,
                layers,
                layerCount = layers.Count,
                note = matches
                    ? "The drawing already matches this state; restoring it would change nothing."
                    : "Restoring this state WOULD change the drawing.",
            });
        });
}
