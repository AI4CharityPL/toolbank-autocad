// MCP tools for the acad-viewports category.
//
// create_viewport and set_viewport_scale used to live in acad-layouts. They moved here
// because splitting viewport work across two categories makes it undiscoverable - a caller
// searching "viewport" should find all of it in one place. acad-layouts keeps what is
// genuinely layout-level: tabs, page setups, paper sizes.
//
// The point of this category is set_viewport_layer_* : per-viewport layer state is how ONE
// model serves an architectural plan, a fire plan and a furniture plan. Without it every
// "view" needs its own duplicated geometry.
//
// create_viewport and set_viewport_scale reuse the acad.layouts.* plugin handlers rather
// than duplicating them - those already carry the model-space fix (the layout is switched
// and restored around the call), and one implementation cannot drift from itself.
//
// Deliberately deferred (see docs/COVERAGE-ROADMAP.md):
//   set_viewport_ucs               - waits for acad-ucs (Phase 1.2)
//   set_viewport_annotation_scale  - waits for acad-annotative (Phase 1.5)
//   maximize_viewport              - MAXACT/MSPACE is the command layer; not without a
//                                    supervised contract for it
//   set_viewport_layer_override    - per-viewport PROPERTY overrides (colour/linetype/
//   list_viewport_layer_overrides    lineweight/transparency). LayerTableRecord in the 2025
//   clear_viewport_layer_overrides   SDK exposes HasOverrides as a plain bool with no
//                                    viewport argument, and none of the Set*InViewport /
//                                    Get*InViewport methods this needs. The capability
//                                    exists in AutoCAD, so this is a matter of finding the
//                                    right API rather than a limitation - withheld until
//                                    that is confirmed rather than guessed at, per the
//                                    precedent of the parametric constraint tools.
//                                    Per-viewport FREEZE (the larger half of the feature)
//                                    ships here and works.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Viewports;

public static class ViewportsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;

    // ─────────────── creation ───────────────

    [McpTool("create_viewport", "Create a rectangular paperspace viewport on the named layout: a window width x height centred at 'center' in paper-space coordinates. Switches to that layout and back on its own, so it works from model space. Optional scale is the model-to-paper factor (0.02 = 1:50).", "viewports",
        Intent = new[] { "utworz rzutnie", "dodaj okno widokowe na arkuszu", "create viewport",
                         "add paperspace viewport", "make a viewport on the layout", "wstaw rzutnie na uklad" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> CreateViewport(IPluginGateway gw, CreateViewportArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<CreateViewportArgs, ViewportResult>(gw, "acad.layouts.create_viewport", args, T_NORMAL, ct);

    [McpTool("create_polygonal_viewport", "Create a non-rectangular paperspace viewport from an ordered vertex list in paper-space coordinates. Needs at least 3 vertices; the outline is closed automatically. Use this for L-shaped or angled sheet windows.", "viewports",
        Intent = new[] { "utworz rzutnie wielokatna", "nieprostokatne okno widokowe", "create polygonal viewport",
                         "irregular viewport shape", "L-shaped viewport", "rzutnia o dowolnym ksztalcie" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> CreatePolygonalViewport(IPluginGateway gw, CreatePolygonalViewportArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<CreatePolygonalViewportArgs, ViewportResult>(gw, "acad.viewports.create_polygonal_viewport", args, T_NORMAL, ct);

    [McpTool("delete_viewport", "Delete a paperspace viewport by handle. The model geometry it showed is untouched - only the window is removed.", "viewports",
        Intent = new[] { "usun rzutnie", "skasuj okno widokowe", "delete viewport",
                         "remove paperspace viewport", "get rid of viewport", "wykasuj rzutnie z arkusza" },
        RequiresPlugin = true)]
    public static Task<ViewportAffected> DeleteViewport(IPluginGateway gw, ViewportHandleArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<ViewportHandleArgs, ViewportAffected>(gw, "acad.viewports.delete_viewport", args, T_NORMAL, ct);

    // ─────────────── inspection ───────────────

    [McpTool("list_viewports", "List paperspace viewports with handle, layout, paper geometry, scale, lock state and how many layer overrides each carries. Pass layoutName to restrict to one tab, omit it for the whole drawing. Read-only.", "viewports",
        Intent = new[] { "wylistuj rzutnie", "pokaz okna widokowe", "list viewports",
                         "what viewports are on this layout", "show all paperspace views", "jakie rzutnie sa na arkuszu" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ViewportListResult> ListViewports(IPluginGateway gw, LayoutNameArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<LayoutNameArgs, ViewportListResult>(gw, "acad.viewports.list_viewports", args, T_FAST, ct);

    [McpTool("get_viewport_info", "Full descriptor of one viewport by handle, including its frozen layers and which layers carry property overrides.", "viewports",
        Intent = new[] { "pokaz szczegoly rzutni", "informacje o oknie widokowym", "get viewport details",
                         "describe viewport", "viewport properties", "wlasciwosci rzutni" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ViewportResult> GetViewportInfo(IPluginGateway gw, ViewportHandleArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<ViewportHandleArgs, ViewportResult>(gw, "acad.viewports.get_viewport_info", args, T_FAST, ct);

    [McpTool("get_viewport_extents_in_model", "Return the model-space rectangle a viewport is currently showing, derived from its centre, paper size and scale. Use this to work out what geometry a sheet window actually covers before annotating it.", "viewports",
        Intent = new[] { "jaki obszar modelu pokazuje rzutnia", "zakres rzutni w modelu", "viewport extents in model space",
                         "what does this viewport show", "model area covered by viewport", "wspolrzedne widoku rzutni" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ViewportExtentsResult> GetViewportExtentsInModel(IPluginGateway gw, ViewportHandleArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<ViewportHandleArgs, ViewportExtentsResult>(gw, "acad.viewports.get_viewport_extents_in_model", args, T_FAST, ct);

    // ─────────────── properties ───────────────

    [McpTool("set_viewport_scale", "Set the model-to-paper scale of a viewport (0.02 = 1:50, 0.01 = 1:100, 0.001 = 1:1000). Locking the viewport afterwards is what stops an accidental zoom changing the drawn scale of an issued sheet.", "viewports",
        Intent = new[] { "ustaw skale rzutni", "zmien skale okna widokowego", "set viewport scale",
                         "scale viewport to 1:50", "change drawing scale of viewport", "skala rzutni 1 do 100" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> SetViewportScale(IPluginGateway gw, SetViewportScaleArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<SetViewportScaleArgs, ViewportResult>(gw, "acad.layouts.set_viewport_scale", args, T_NORMAL, ct);

    [McpTool("set_viewport_lock", "Lock or unlock a viewport. A locked viewport cannot have its zoom or scale changed by panning inside it, which is the single most common way an issued sheet silently ends up at the wrong scale.", "viewports",
        Intent = new[] { "zablokuj rzutnie", "odblokuj okno widokowe", "lock viewport",
                         "unlock viewport", "prevent viewport zoom", "zabezpiecz skale rzutni" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> SetViewportLock(IPluginGateway gw, SetViewportLockArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<SetViewportLockArgs, ViewportResult>(gw, "acad.viewports.set_viewport_lock", args, T_NORMAL, ct);

    [McpTool("set_viewport_on_off", "Turn a viewport's display on or off. An off viewport keeps its position, size and scale but renders nothing - useful for sheets under construction without deleting the window.", "viewports",
        Intent = new[] { "wlacz rzutnie", "wylacz okno widokowe", "turn viewport on",
                         "turn viewport off", "hide viewport contents", "rzutnia niewidoczna" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> SetViewportOnOff(IPluginGateway gw, SetViewportLockArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<SetViewportLockArgs, ViewportResult>(gw, "acad.viewports.set_viewport_on_off", args, T_NORMAL, ct);

    [McpTool("set_viewport_shade_plot", "Set how a viewport plots: 'AsDisplayed', 'Wireframe', 'Hidden' or 'Rendered'. Hidden is what removes obscured 3D edges on a plotted sheet without changing the model.", "viewports",
        Intent = new[] { "ustaw tryb wydruku rzutni", "rzutnia z ukrytymi krawedziami", "set viewport shade plot",
                         "plot viewport as hidden", "wireframe plot mode", "sposob plotowania rzutni" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> SetViewportShadePlot(IPluginGateway gw, SetViewportShadePlotArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<SetViewportShadePlotArgs, ViewportResult>(gw, "acad.viewports.set_viewport_shade_plot", args, T_NORMAL, ct);

    // ─────────────── per-viewport layer state ───────────────

    [McpTool("set_viewport_layer_freeze", "Freeze layers in ONE viewport only. This is the mechanism that lets a single model produce an architectural plan and a fire plan: freeze the layers each sheet must not show, in that sheet's viewport, without touching the model or any other viewport.", "viewports",
        Intent = new[] { "zamroz warstwy w rzutni", "ukryj warstwy tylko na tym arkuszu", "freeze layers in viewport",
                         "hide layers in this viewport only", "per viewport layer freeze", "wylacz warstwe w jednej rzutni" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> SetViewportLayerFreeze(IPluginGateway gw, ViewportLayerVisibilityArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<ViewportLayerVisibilityArgs, ViewportResult>(gw, "acad.viewports.set_viewport_layer_freeze", args, T_NORMAL, ct);

    [McpTool("set_viewport_layer_thaw", "Thaw layers that were frozen in one viewport, so they display there again. Layers not frozen in that viewport are left alone.", "viewports",
        Intent = new[] { "odmroz warstwy w rzutni", "pokaz ponownie warstwy na arkuszu", "thaw layers in viewport",
                         "unfreeze layers in this viewport", "show layer again in viewport", "wlacz warstwe w rzutni" },
        RequiresPlugin = true)]
    public static Task<ViewportResult> SetViewportLayerThaw(IPluginGateway gw, ViewportLayerVisibilityArgs args, CancellationToken ct)
        => ViewportsProxy.CallAsync<ViewportLayerVisibilityArgs, ViewportResult>(gw, "acad.viewports.set_viewport_layer_thaw", args, T_NORMAL, ct);



}
