// AutoCAD acad-layers category. 13 tools covering layer CRUD, properties (color/linetype/lineweight),
// state (frozen/locked/off/plottable), current-layer pick, rename, purge and named layer states.
// Each method is a thin proxy through IPluginGateway to "acad.layers.<verb>".
//
// Rules: 19-tool-implementation-pattern.md, 28-acad-blocks-layers-files-traps.md.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Layers;

public static class LayersTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    [McpTool("list_layers", "List every layer in the active drawing with color, linetype, lineweight, plottable/frozen/locked/off flags, plus the current layer name.", "layers",
        Intent = new[] { "wylistuj warstwy", "list all layers", "show layers", "wszystkie warstwy", "what layers exist" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<LayerListResult> ListLayers(IPluginGateway gw, EmptyArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<EmptyArgs, LayerListResult>(gw, "acad.layers.list_layers", args, T_FAST, ct);

    [McpTool("get_layer", "Get full descriptor of one layer by name.", "layers",
        Intent = new[] { "pokaz warstwe", "get layer info", "describe layer", "info o warstwie", "show layer details" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<LayerResult> GetLayer(IPluginGateway gw, LayerNameArg args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerNameArg, LayerResult>(gw, "acad.layers.get_layer", args, T_FAST, ct);

    [McpTool("create_layer", "Create a new layer. Accepts color (RGB or ACI), linetype name, lineweight in mm, plottable flag and description.", "layers",
        Intent = new[] { "stworz warstwe", "dodaj warstwe", "create layer", "add new layer", "new layer with color" },
        RequiresPlugin = true)]
    public static Task<LayerResult> CreateLayer(IPluginGateway gw, CreateLayerArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<CreateLayerArgs, LayerResult>(gw, "acad.layers.create_layer", args, T_NORMAL, ct);

    [McpTool("set_current_layer", "Set the active (\"current\") layer; subsequent draw operations default to this layer.", "layers",
        Intent = new[] { "ustaw biezaca warstwe", "set current layer", "make layer current", "switch active layer", "wybierz warstwe biezaca" },
        RequiresPlugin = true)]
    public static Task<LayerResult> SetCurrentLayer(IPluginGateway gw, LayerNameArg args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerNameArg, LayerResult>(gw, "acad.layers.set_current_layer", args, T_FAST, ct);

    [McpTool("set_layer_color", "Set a layer's color (true RGB or ACI 1..255).", "layers",
        Intent = new[] { "zmien kolor warstwy", "set layer color", "color of layer", "ustaw kolor warstwy", "kolor warstwy rgb" },
        RequiresPlugin = true)]
    public static Task<LayerResult> SetLayerColor(IPluginGateway gw, SetLayerColorArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<SetLayerColorArgs, LayerResult>(gw, "acad.layers.set_layer_color", args, T_FAST, ct);

    [McpTool("set_layer_linetype", "Set a layer's linetype (must already be loaded; returns LayerNotFound if linetype is missing).", "layers",
        Intent = new[] { "zmien typ linii warstwy", "set layer linetype", "ustaw linetype warstwy", "linia przerywana na warstwie", "layer linetype" },
        RequiresPlugin = true)]
    public static Task<LayerResult> SetLayerLinetype(IPluginGateway gw, SetLayerLinetypeArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<SetLayerLinetypeArgs, LayerResult>(gw, "acad.layers.set_layer_linetype", args, T_FAST, ct);

    [McpTool("set_layer_lineweight", "Set a layer's lineweight in millimeters; snaps to nearest standard AutoCAD value (e.g. 0.13, 0.18, 0.25, 0.5, 0.7, 1.0 mm).", "layers",
        Intent = new[] { "zmien grubosc linii warstwy", "set layer lineweight", "grubosc warstwy", "layer lineweight in mm", "warstwa grubosc" },
        RequiresPlugin = true)]
    public static Task<LayerResult> SetLayerLineweight(IPluginGateway gw, SetLayerLineweightArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<SetLayerLineweightArgs, LayerResult>(gw, "acad.layers.set_layer_lineweight", args, T_FAST, ct);

    [McpTool("set_layer_state", "Toggle one or more layer state flags: frozen, locked, off, plottable. null = leave unchanged. Cannot freeze the current layer.", "layers",
        Intent = new[] { "zamroz warstwe", "freeze layer", "lock layer", "thaw layer", "wylacz warstwe" },
        RequiresPlugin = true)]
    public static Task<LayerResult> SetLayerState(IPluginGateway gw, SetLayerStateArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<SetLayerStateArgs, LayerResult>(gw, "acad.layers.set_layer_state", args, T_FAST, ct);

    [McpTool("rename_layer", "Rename a layer. Layer 0 cannot be renamed; new name must be a valid AutoCAD symbol name.", "layers",
        Intent = new[] { "zmien nazwe warstwy", "rename layer", "przemianuj warstwe", "change layer name", "rename layer to" },
        RequiresPlugin = true)]
    public static Task<LayerResult> RenameLayer(IPluginGateway gw, RenameLayerArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<RenameLayerArgs, LayerResult>(gw, "acad.layers.rename_layer", args, T_FAST, ct);

    [McpTool("delete_layer", "Delete a layer (only if no entities reference it). Layer 0 and Defpoints cannot be deleted.", "layers",
        Intent = new[] { "usun warstwe", "delete layer", "skasuj warstwe", "remove layer", "drop layer" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> DeleteLayer(IPluginGateway gw, LayerNameArg args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerNameArg, AffectedCount>(gw, "acad.layers.delete_layer", args, T_NORMAL, ct);

    [McpTool("purge_unused_layers", "Purge every layer that has no entity references and is not protected (0 / Defpoints / current). Returns number of layers removed.", "layers",
        Intent = new[] { "wyczysc warstwy", "purge unused layers", "remove empty layers", "wyczysc nieuzywane warstwy", "purge layers" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> PurgeUnusedLayers(IPluginGateway gw, EmptyArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<EmptyArgs, AffectedCount>(gw, "acad.layers.purge_unused_layers", args, T_NORMAL, ct);

    [McpTool("save_layer_state", "Save the current visibility/lock/color/linetype state of every layer under a named layer state (LAS).", "layers",
        Intent = new[] { "zapisz stan warstw", "save layer state", "save las", "zapisz konfiguracje warstw", "checkpoint layer visibility" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> SaveLayerState(IPluginGateway gw, SaveLayerStateArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<SaveLayerStateArgs, AffectedCount>(gw, "acad.layers.save_layer_state", args, T_NORMAL, ct);

    [McpTool("restore_layer_state", "Restore a previously saved named layer state.", "layers",
        Intent = new[] { "przywroc stan warstw", "restore layer state", "load las", "wczytaj zapisany stan warstw", "apply layer state" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> RestoreLayerState(IPluginGateway gw, LayerNameArg args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerNameArg, AffectedCount>(gw, "acad.layers.restore_layer_state", args, T_NORMAL, ct);

    [McpTool("list_layer_states", "List every saved named layer state in the active drawing.", "layers",
        Intent = new[] { "wylistuj stany warstw", "list layer states", "list las", "show saved layer states", "wszystkie zapisane stany warstw" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<StringListResult> ListLayerStates(IPluginGateway gw, EmptyArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<EmptyArgs, StringListResult>(gw, "acad.layers.list_layer_states", args, T_FAST, ct);

    // ─────────── named layer states, beyond save/restore/list (roadmap 2.4) ───────────
    //
    // Roadmap 2.4 planned an acad-standards category. Ten of its fourteen tools have no managed
    // API and eight of those already exist in acad-validators, so it was struck; what survived is
    // this group and the drawing-property tools in acad-files. See COVERAGE-ROADMAP 2.4.

    [McpTool("export_layer_state", "Write one named layer state out to a .las file so it can be reused in other drawings or kept under version control alongside the project. It writes a file, not the DWG, so the drawing is unchanged. The result reports the byte count, because an export that produced an empty file is otherwise indistinguishable from one that worked.", "layers",
        Intent = new[] { "wyeksportuj stan warstw do pliku", "zapisz stan warstw jako las",
                         "export layer state to a file", "save layer state as las",
                         "przenies stan warstw do innego rysunku", "layer state to file" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<LayerStateExportResult> ExportLayerState(IPluginGateway gw, LayerStateFileArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerStateFileArgs, LayerStateExportResult>(gw, "acad.layers.export_layer_state", args, T_NORMAL, ct);

    [McpTool("import_layer_state", "Read a .las file into this drawing as a named layer state. The name comes from inside the file rather than from you, so the result reports which states actually appeared - established by comparing the drawing before and after rather than by assuming. AutoCAD refuses to import over an existing name; delete or rename the local one first.", "layers",
        Intent = new[] { "zaimportuj stan warstw z pliku", "wczytaj plik las",
                         "import layer state from a file", "load a las layer state",
                         "pobierz stan warstw z innego projektu", "layer state from file" },
        RequiresPlugin = true)]
    public static Task<LayerStateImportResult> ImportLayerState(IPluginGateway gw, LayerStateFileArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerStateFileArgs, LayerStateImportResult>(gw, "acad.layers.import_layer_state", args, T_NORMAL, ct);

    [McpTool("delete_layer_state", "Delete a named layer state. THE LAYERS ARE NOT TOUCHED - a layer state is a recording of visibility and properties, so deleting it removes the recording and nothing else. The result says so explicitly, because this is a tool name an agent could reasonably fear means something more destructive.", "layers",
        Intent = new[] { "usun stan warstw", "skasuj zapisany stan warstw", "delete layer state",
                         "remove a saved layer state", "wyczysc stany warstw", "drop layer state" },
        RequiresPlugin = true)]
    public static Task<LayerStateDeleteResult> DeleteLayerState(IPluginGateway gw, LayerStateNameArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerStateNameArgs, LayerStateDeleteResult>(gw, "acad.layers.delete_layer_state", args, T_NORMAL, ct);

    [McpTool("rename_layer_state", "Rename a named layer state. Refuses a name that is already taken rather than merging into it, and confirms both halves of the rename in the result - the old name gone and the new one present - since a rename that half happened is worse than one that failed outright.", "layers",
        Intent = new[] { "zmien nazwe stanu warstw", "przemianuj stan warstw", "rename layer state",
                         "give a layer state a better name", "poprawa nazwy stanu warstw",
                         "change layer state name" },
        RequiresPlugin = true)]
    public static Task<LayerStateRenameResult> RenameLayerState(IPluginGateway gw, RenameLayerStateArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<RenameLayerStateArgs, LayerStateRenameResult>(gw, "acad.layers.rename_layer_state", args, T_NORMAL, ct);

    [McpTool("set_layer_state_description", "Attach or replace the description on a named layer state - what it is for, which sheet it belongs to, which discipline it serves. Pass an empty description to clear it. Worth doing: a drawing holding six states called PLAN-1 through PLAN-6 with no descriptions is one an agent cannot choose between.", "layers",
        Intent = new[] { "opis stanu warstw", "dodaj opis do stanu warstw", "set layer state description",
                         "describe what a layer state is for", "podpisz stan warstw",
                         "annotate a layer state" },
        RequiresPlugin = true)]
    public static Task<LayerStateDescriptionResult> SetLayerStateDescription(IPluginGateway gw, LayerStateDescriptionArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerStateDescriptionArgs, LayerStateDescriptionResult>(gw, "acad.layers.set_layer_state_description", args, T_NORMAL, ct);

    [McpTool("compare_layer_state", "Answer whether restoring this state would change anything, without restoring it, and list the layers it covers. Read-only. This is the call to make before restore_layer_state in a drawing somebody is working in: the difference between a no-op and a restore that silently reorganises their view.", "layers",
        Intent = new[] { "czy stan warstw rozni sie od rysunku", "co zmieni przywrocenie stanu warstw",
                         "compare layer state to the drawing", "would restoring this change anything",
                         "sprawdz stan warstw przed przywroceniem", "which layers does this state cover" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<LayerStateCompareResult> CompareLayerState(IPluginGateway gw, LayerStateNameArgs args, CancellationToken ct)
        => LayersProxy.CallAsync<LayerStateNameArgs, LayerStateCompareResult>(gw, "acad.layers.compare_layer_state", args, T_FAST, ct);
}
