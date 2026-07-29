// AutoCAD acad-view category. Model-space view control: zoom window/extents/all/scale/center,
// set active named view, list named views, get current view. Pre-step for acad.files.export_file
// scope="Display" and for AI-driven visual inspection loops where the agent needs to frame a
// specific region before capturing a PNG via export_file + describe_image.
//
// Rules: 10, 11, 12, 15, 19, 22-25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.View;

public static class ViewTools
{
    private const int T_FAST   = 5_000;
    private const int T_NORMAL = 15_000;

    [McpTool("zoom_window",
        "Zoom the active model-space view to the axis-aligned rectangle defined by two corner points (drawing units). Use before acad.files.export_file scope=\"Display\" to capture a specific region as PNG, or to frame an area for visual inspection. Corners can be in any order; the tool normalises them. This changes what the user sees in AutoCAD and what PlotType.Display would capture.",
        "view",
        Intent = new[]
        {
            "zoom do prostokata", "zoom window", "zblizenie na obszar rysunku",
            "frame a region of the drawing", "zoom to rectangle",
            "przyblizyc do obszaru", "pokaz fragment rysunku"
        },
        RequiresPlugin = true)]
    public static Task<ViewAffectedResult> ZoomWindow(IPluginGateway gw, ZoomWindowArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ZoomWindowArgs, ViewAffectedResult>(gw, "acad.view.zoom_window", args, T_NORMAL, ct);

    [McpTool("zoom_extents",
        "Zoom the active model-space view to fit the bounding box of all entities (ZOOM _E). Use as a reset between regional captures.",
        "view",
        Intent = new[]
        {
            "zoom extents", "zoom na calosc rysunku", "pokaz caly rysunek",
            "fit all to view", "ZOOM E"
        },
        RequiresPlugin = true)]
    public static Task<ViewAffectedResult> ZoomExtents(IPluginGateway gw, ViewEmptyArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ViewEmptyArgs, ViewAffectedResult>(gw, "acad.view.zoom_extents", args, T_NORMAL, ct);

    [McpTool("zoom_all",
        "Zoom the active view to the drawing limits + extents (ZOOM _A). Shows every drawing limit rectangle as well as the entity extent.",
        "view",
        Intent = new[]
        {
            "zoom all", "pokaz cale granice rysunku", "zoom na limity",
            "show drawing limits and extents", "ZOOM A"
        },
        RequiresPlugin = true)]
    public static Task<ViewAffectedResult> ZoomAll(IPluginGateway gw, ViewEmptyArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ViewEmptyArgs, ViewAffectedResult>(gw, "acad.view.zoom_all", args, T_NORMAL, ct);

    [McpTool("zoom_center",
        "Zoom to a specific center point with a requested view height in drawing units (ZOOM _C <center> <height>). Useful to frame a named fixture at a known scale.",
        "view",
        Intent = new[]
        {
            "zoom to center", "zoom na punkt", "wysrodkuj widok",
            "center view on a point with height",
            "center zoom"
        },
        RequiresPlugin = true)]
    public static Task<ViewAffectedResult> ZoomCenter(IPluginGateway gw, ZoomCenterArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ZoomCenterArgs, ViewAffectedResult>(gw, "acad.view.zoom_center", args, T_NORMAL, ct);

    [McpTool("zoom_scale",
        "Zoom the active view by a relative scale factor (ZOOM _S <factor>x). factor>1 zooms in, 0<factor<1 zooms out.",
        "view",
        Intent = new[]
        {
            "zoom scale", "zoom factor", "scale view by factor",
            "zoom in by 2x", "powieksz widok o czynnik"
        },
        RequiresPlugin = true)]
    public static Task<ViewAffectedResult> ZoomScale(IPluginGateway gw, ZoomScaleArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ZoomScaleArgs, ViewAffectedResult>(gw, "acad.view.zoom_scale", args, T_NORMAL, ct);

    [McpTool("list_views",
        "List all named views stored in the drawing's VIEW table with their center point and size. Use to pick a saved architectural view (e.g. \"FLOOR-1\", \"SITE\", \"DETAIL-A\") before capture.",
        "view",
        Intent = new[]
        {
            "wylistuj widoki", "list named views", "pokaz zapisane widoki",
            "show named views in drawing", "which views are saved"
        },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<ListViewsResult> ListViews(IPluginGateway gw, ViewEmptyArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ViewEmptyArgs, ListViewsResult>(gw, "acad.view.list_views", args, T_FAST, ct);

    [McpTool("set_current_view",
        "Restore a named view by name (equivalent to the VIEW _R <name> command). Fails with a clear error if the name doesn't exist in the VIEW table.",
        "view",
        Intent = new[]
        {
            "ustaw nazwany widok", "restore named view", "switch to view",
            "activate named view", "przelacz na widok"
        },
        RequiresPlugin = true)]
    public static Task<ViewAffectedResult> SetCurrentView(IPluginGateway gw, SetCurrentViewArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<SetCurrentViewArgs, ViewAffectedResult>(gw, "acad.view.set_current_view", args, T_NORMAL, ct);

    [McpTool("get_current_view",
        "Return the currently active view's center point, width, height and paper-space flag. Use to confirm a zoom actually took effect before capturing.",
        "view",
        Intent = new[]
        {
            "biezacy widok", "current view info", "describe active view",
            "pokaz parametry aktywnego widoku", "view info"
        },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<CurrentViewResult> GetCurrentView(IPluginGateway gw, ViewEmptyArgs args, CancellationToken ct)
        => ViewProxy.CallAsync<ViewEmptyArgs, CurrentViewResult>(gw, "acad.view.get_current_view", args, T_FAST, ct);
}
