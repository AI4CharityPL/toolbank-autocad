// MCP tool surface for the acad-views category.
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results).
//
// Named as acad-views rather than acad-views-cameras for a measured reason: there is NO Camera
// type in the managed API. In AutoCAD a camera IS a named view carrying a target and a lens
// length, so set_camera_target and set_camera_lens act on views and there is nothing left for a
// create_camera or list_cameras to do. set_view_category is struck too - ViewTableRecord has no
// Category, the view category being a Sheet Set concept.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Views;

public static class ViewsTools
{
    private const int T_NORMAL = 15_000;

    [McpTool("create_named_view", "Save a named view - where the camera sits, what it looks at, and how much it frames. Give the center point plus width and height in drawing units; to build one from a rectangle instead use create_view_from_window. Optionally set target and viewDirection for a 3D view, lensLength to make it a camera, and twist to rotate it. A named view does NOT change what is on screen: it records a viewpoint for restore_view_in_viewport to use later. Refuses a name already in use rather than replacing a view silently. Read back through a fresh table lookup after creation, and the width and height are checked against what was asked for.", "views",
        Intent = new[] { "save a named view", "create a view of this area",
                         "zapisz widok nazwany", "store the current viewpoint as a view",
                         "utworz nazwany widok na rysunku", "make a named view",
                         "define a view for a viewport" },
        RequiresPlugin = true)]
    public static Task<ViewCreateResult> CreateNamedView(IPluginGateway gw, ViewCreateArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewCreateArgs, ViewCreateResult>(gw, "acad.views.create_named_view", args, T_NORMAL, ct);

    [McpTool("create_view_from_window", "Save a named view framing the rectangle between two corners - the same result as create_named_view but specified the way a user points at a drawing rather than by center and size. The corners may be given in any order, since a window dragged right-to-left is still a window. Optional target, viewDirection, lensLength and twist behave exactly as in create_named_view. Refuses a name already in use.", "views",
        Intent = new[] { "create a view from this window", "save a view of this rectangle",
                         "zapisz widok z okna", "make a named view around these two corners",
                         "utworz widok z prostokata", "frame this area as a view",
                         "view from two corners" },
        RequiresPlugin = true)]
    public static Task<ViewCreateResult> CreateViewFromWindow(IPluginGateway gw, ViewWindowArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewWindowArgs, ViewCreateResult>(gw, "acad.views.create_view_from_window", args, T_NORMAL, ct);

    [McpTool("list_named_views", "List every named view in the drawing with its center, size, target, direction, lens length, twist, clipping and whether it carries its own UCS. Read-only. Note that a fresh drawing has NONE - unlike the named objects dictionary, the view table starts empty, so a count of zero is normal rather than a sign that something is wrong. lensLength is what makes a view a camera, since AutoCAD has no separate camera object.", "views",
        Intent = new[] { "list the named views", "what views are saved in this drawing",
                         "lista nazwanych widokow", "show saved viewpoints",
                         "jakie widoki sa zapisane w rysunku", "find a view by name",
                         "list cameras in the drawing" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<ViewListResult> ListNamedViews(IPluginGateway gw, ViewsNoArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewsNoArgs, ViewListResult>(gw, "acad.views.list_named_views", args, T_NORMAL, ct);

    [McpTool("delete_named_view", "Delete a named view from the drawing. Confirmed gone from the view table afterwards rather than assumed. Worth knowing: a viewport that was showing this view KEEPS what it is displaying, because restore_view_in_viewport copies the settings across rather than leaving a reference behind - so deleting a view never disturbs a layout.", "views",
        Intent = new[] { "delete a named view", "remove a saved view",
                         "usun nazwany widok", "get rid of this view",
                         "skasuj zapisany widok z rysunku", "clean up unused views",
                         "delete a camera" },
        RequiresPlugin = true)]
    public static Task<ViewDeleteResult> DeleteNamedView(IPluginGateway gw, ViewNameArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewNameArgs, ViewDeleteResult>(gw, "acad.views.delete_named_view", args, T_NORMAL, ct);

    [McpTool("restore_view_in_viewport", "Point a layout viewport at a saved view - its target, direction, height and twist. Takes the viewport's handle, which viewports.list_viewports finds; this wants a LAYOUT viewport, not the model-space window. IMPORTANT, and it is a consequence of how the API works rather than a choice: Viewport.SetView does not exist, so the settings are COPIED across and the viewport keeps no reference to the view. Changing or deleting the view afterwards leaves the viewport exactly as it is - restore again to pick up a change.", "views",
        Intent = new[] { "restore this view in a viewport", "set a viewport to show a saved view",
                         "przywroc widok w rzutni", "apply a named view to this viewport",
                         "ustaw rzutnie na zapisany widok", "point a viewport at a view",
                         "show this view in the layout" },
        RequiresPlugin = true)]
    public static Task<ViewRestoreResult> RestoreViewInViewport(IPluginGateway gw, ViewRestoreArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewRestoreArgs, ViewRestoreResult>(gw, "acad.views.restore_view_in_viewport", args, T_NORMAL, ct);

    [McpTool("set_camera_target", "Set the point a named view looks AT. Together with the view direction and the lens length this is everything AutoCAD means by a camera - there is no Camera object in the managed API, so a camera is a named view and this is how you aim it. The previous target is reported so a change can be undone, and the new one is read back after writing.", "views",
        Intent = new[] { "set the camera target", "aim this view at a point",
                         "ustaw cel kamery", "point the camera at this location",
                         "zmien punkt na ktory patrzy widok", "set what the view looks at",
                         "retarget a camera" },
        RequiresPlugin = true)]
    public static Task<ViewTargetResult> SetCameraTarget(IPluginGateway gw, ViewTargetArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewTargetArgs, ViewTargetResult>(gw, "acad.views.set_camera_target", args, T_NORMAL, ct);

    [McpTool("set_camera_lens", "Set the lens length of a named view, in millimetres on the 35 mm convention: 50 is normal, below about 35 is wide angle, above 85 is telephoto. IMPORTANT: the lens only shows in a PERSPECTIVE view, and perspective belongs to a viewport rather than to the stored view - so setting a lens on a view being displayed in parallel projection is stored faithfully and changes nothing visible. set_perspective_mode is the other half. The previous value is reported and the new one read back.", "views",
        Intent = new[] { "set the camera lens length", "make this view wide angle",
                         "ustaw ogniskowa kamery", "change the lens to 35mm",
                         "zmien ogniskowa widoku", "set focal length for a view",
                         "telephoto view" },
        RequiresPlugin = true)]
    public static Task<ViewLensResult> SetCameraLens(IPluginGateway gw, ViewLensArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewLensArgs, ViewLensResult>(gw, "acad.views.set_camera_lens", args, T_NORMAL, ct);

    [McpTool("set_perspective_mode", "Turn perspective projection on or off for a layout VIEWPORT. Measured and worth stating plainly, because the name suggests otherwise: perspective belongs to the viewport and NOT to a stored view - ViewTableRecord has no PerspectiveOn at all - which is why this takes a viewport handle rather than a view name. The lens length in force is reported alongside; it means nothing while perspective is off. Refuses when perspective is already in the state asked for, rather than reporting a change that did not happen.", "views",
        Intent = new[] { "turn on perspective in this viewport", "switch to perspective projection",
                         "wlacz perspektywe w rzutni", "make this viewport perspective",
                         "przelacz rzutnie na perspektywe", "turn perspective off",
                         "parallel or perspective projection" },
        RequiresPlugin = true)]
    public static Task<ViewPerspectiveResult> SetPerspectiveMode(IPluginGateway gw, ViewPerspectiveArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewPerspectiveArgs, ViewPerspectiveResult>(gw, "acad.views.set_perspective_mode", args, T_NORMAL, ct);

    [McpTool("set_view_ucs_association", "Associate a UCS with a named view, so the drawing plane follows when the view is restored - which is what makes a working view usable for drawing rather than only for looking. Pass a UCS name, or 'world' (the default) for the world coordinate system. Note for anyone extending this: ViewTableRecord.UcsName is READ-ONLY, so the association is made by object id through SetUcs, and IsUcsAssociatedToView is what reads it back. An unknown UCS name is refused and points at ucs.list_ucs.", "views",
        Intent = new[] { "associate a ucs with this view", "make the view restore its ucs",
                         "powiaz uklad wspolrzednych z widokiem", "set the ucs for a named view",
                         "przypisz ucs do widoku", "make a working view",
                         "view should bring back its coordinate system" },
        RequiresPlugin = true)]
    public static Task<ViewUcsResult> SetViewUcsAssociation(IPluginGateway gw, ViewUcsArgs args, CancellationToken ct)
        => ViewsProxy.CallAsync<ViewUcsArgs, ViewUcsResult>(gw, "acad.views.set_view_ucs_association", args, T_NORMAL, ct);
}
