// AutoCAD acad-selection category. 12 tools covering selection by criteria,
// window/crossing/fence/polygon picks, named selection sets and filtering.
// All tools are read-only and idempotent.
//
// Rules: 19-tool-implementation-pattern.mdc, 20..25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Selection;

public static class SelectionTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    [McpTool("select_all", "Select every entity currently in model space (no filtering).", "selection",
        Intent = new[] { "zaznacz wszystko", "select all entities", "select everything", "wszystkie obiekty", "all model space entities" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectAll(IPluginGateway gw, EmptyArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<EmptyArgs, SelectionResult>(gw, "acad.selection.select_all", args, T_NORMAL, ct);

    [McpTool("select_by_layer", "Select all entities on the given layer. Optionally restrict by frozen/thawed state.", "selection",
        Intent = new[] { "zaznacz po warstwie", "select by layer", "all entities on layer", "obiekty na warstwie", "filter by layer" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByLayer(IPluginGateway gw, ByLayerArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByLayerArgs, SelectionResult>(gw, "acad.selection.select_by_layer", args, T_NORMAL, ct);

    [McpTool("select_by_color", "Select all entities by color (true RGB or ACI index).", "selection",
        Intent = new[] { "zaznacz po kolorze", "select by color", "all entities of color", "filter entities by color", "select red entities" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByColor(IPluginGateway gw, ByColorArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByColorArgs, SelectionResult>(gw, "acad.selection.select_by_color", args, T_NORMAL, ct);

    [McpTool("select_by_type", "Select entities by AutoCAD DXF entity name (e.g. \"LINE\", \"LWPOLYLINE\", \"CIRCLE\", \"3DSOLID\", \"INSERT\").", "selection",
        Intent = new[] { "zaznacz po typie", "select by entity type", "all lines", "all polylines", "select dxf type" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByType(IPluginGateway gw, ByTypeArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByTypeArgs, SelectionResult>(gw, "acad.selection.select_by_type", args, T_NORMAL, ct);

    [McpTool("select_by_handle", "Resolve a list of entity handles into a single selection result. Validates each handle exists.", "selection",
        Intent = new[] { "zaznacz po uchwytach", "select by handle", "lookup by handles", "resolve handles", "find entities by handle" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByHandle(IPluginGateway gw, ByHandleArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByHandleArgs, SelectionResult>(gw, "acad.selection.select_by_handle", args, T_FAST, ct);

    [McpTool("select_window", "Select entities fully inside (or, with crossing=true, intersecting) the WCS axis-aligned window from min to max.", "selection",
        Intent = new[] { "zaznacz oknem", "select by window", "window selection", "crossing selection", "select inside box" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectWindow(IPluginGateway gw, WindowArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<WindowArgs, SelectionResult>(gw, "acad.selection.select_window", args, T_NORMAL, ct);

    [McpTool("select_fence", "Select entities crossing a polyline fence defined by an ordered vertex list.", "selection",
        Intent = new[] { "zaznacz plotem", "select by fence", "fence selection", "polyline fence pick", "select crossing fence" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectFence(IPluginGateway gw, FenceArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<FenceArgs, SelectionResult>(gw, "acad.selection.select_fence", args, T_NORMAL, ct);

    [McpTool("select_polygon", "Select entities inside (crossing=false) or intersecting (crossing=true) a closed polygonal region.", "selection",
        Intent = new[] { "zaznacz wielokatem", "select by polygon", "polygon window", "polygon crossing", "select inside polygon" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectPolygon(IPluginGateway gw, PolygonArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<PolygonArgs, SelectionResult>(gw, "acad.selection.select_polygon", args, T_NORMAL, ct);

    [McpTool("filter_entities", "Apply an additional layer/type/color filter to a candidate set (or to all of model space if no handles supplied).", "selection",
        Intent = new[] { "filtruj zaznaczenie", "filter selection", "narrow selection by criteria", "filter entities", "apply filter to selection" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> FilterEntitiesTool(IPluginGateway gw, FilterEntities args, CancellationToken ct)
        => SelectionProxy.CallAsync<FilterEntities, SelectionResult>(gw, "acad.selection.filter_entities", args, T_NORMAL, ct);

    [McpTool("save_selection_set", "Save a list of entity handles under a named selection set (stored in the AcadMcp xrecord dictionary on the drawing).", "selection",
        Intent = new[] { "zapisz zestaw selekcji", "save selection set", "ssget save", "store named selection", "save selection by name" },
        RequiresPlugin = true)]
    public static Task<CountResult> SaveSelectionSet(IPluginGateway gw, SaveSetArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<SaveSetArgs, CountResult>(gw, "acad.selection.save_selection_set", args, T_FAST, ct);

    [McpTool("load_selection_set", "Load a previously saved named selection set and return its handles. Validates each handle still exists.", "selection",
        Intent = new[] { "wczytaj zestaw selekcji", "load selection set", "ssget load", "restore named selection", "get saved selection by name" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SetMembersResult> LoadSelectionSet(IPluginGateway gw, LoadSetArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<LoadSetArgs, SetMembersResult>(gw, "acad.selection.load_selection_set", args, T_FAST, ct);

    [McpTool("count_entities", "Count entities in model space (optionally filtered by DXF type).", "selection",
        Intent = new[] { "policz obiekty", "count entities", "how many entities", "count by type", "ile obiektow" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<CountResult> CountEntities(IPluginGateway gw, ByTypeArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByTypeArgs, CountResult>(gw, "acad.selection.count_entities", args, T_FAST, ct);
}
