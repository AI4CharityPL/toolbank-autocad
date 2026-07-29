// AutoCAD acad-layers category. 13 tools covering layer CRUD, properties (color/linetype/lineweight),
// state (frozen/locked/off/plottable), current-layer pick, rename, purge and named layer states.
// Each method is a thin proxy through IPluginGateway to "acad.layers.<verb>".
//
// Rules: 19-tool-implementation-pattern.mdc, 28-acad-blocks-layers-files-traps.mdc.

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
}
