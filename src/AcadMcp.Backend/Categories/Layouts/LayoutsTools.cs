// AutoCAD acad-layouts category. 9 tools covering paper-space layout (tab) lifecycle,
// layout viewport (entity) creation and scaling, and basic plot configuration
// (plotter / paper size / plot style / rotation).
//
// Rules: 19, 28-acad-blocks-layers-files-traps.md.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Layouts;

public static class LayoutsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    [McpTool("list_layouts", "List every paper-space layout (tab) in the active drawing with its tab order, current flag, and configured plotter / paper size.", "layouts",
        Intent = new[] { "wylistuj layouty", "list layouts", "show paperspace tabs", "wszystkie layouty", "what layouts exist" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<LayoutListResult> ListLayouts(IPluginGateway gw, LayoutsEmptyArgs args, CancellationToken ct)
        => LayoutsProxy.CallAsync<LayoutsEmptyArgs, LayoutListResult>(gw, "acad.layouts.list_layouts", args, T_FAST, ct);

    [McpTool("create_layout", "Create a new paper-space layout (tab). Optionally make it the current/active layout right after creation.", "layouts",
        Intent = new[] { "stworz layout", "dodaj nowy layout", "create paperspace layout", "add new tab", "new layout tab" },
        RequiresPlugin = true)]
    public static Task<LayoutResult> CreateLayout(IPluginGateway gw, CreateLayoutArgs args, CancellationToken ct)
        => LayoutsProxy.CallAsync<CreateLayoutArgs, LayoutResult>(gw, "acad.layouts.create_layout", args, T_NORMAL, ct);

    [McpTool("set_current_layout", "Switch the active layout to the named tab (use \"Model\" to return to model space). All subsequent draw / viewport tools target this layout.", "layouts",
        Intent = new[] { "ustaw biezacy layout", "set current layout", "switch active tab", "make layout current", "wybierz layout" },
        RequiresPlugin = true)]
    public static Task<LayoutResult> SetCurrentLayout(IPluginGateway gw, LayoutNameArg args, CancellationToken ct)
        => LayoutsProxy.CallAsync<LayoutNameArg, LayoutResult>(gw, "acad.layouts.set_current_layout", args, T_FAST, ct);

    [McpTool("delete_layout", "Delete a paper-space layout. Cannot delete the Model tab; cannot delete the last remaining paper-space tab.", "layouts",
        Intent = new[] { "usun layout", "delete layout", "skasuj zakladke", "remove layout tab", "drop paperspace tab" },
        RequiresPlugin = true)]
    public static Task<LayoutAffectedCount> DeleteLayout(IPluginGateway gw, LayoutNameArg args, CancellationToken ct)
        => LayoutsProxy.CallAsync<LayoutNameArg, LayoutAffectedCount>(gw, "acad.layouts.delete_layout", args, T_NORMAL, ct);

    [McpTool("rename_layout", "Rename a paper-space layout. The Model tab cannot be renamed; the new name must be unique and valid as an AutoCAD symbol name.", "layouts",
        Intent = new[] { "zmien nazwe layoutu", "rename layout", "przemianuj zakladke", "change layout name", "rename layout to" },
        RequiresPlugin = true)]
    public static Task<LayoutResult> RenameLayout(IPluginGateway gw, RenameLayoutArgs args, CancellationToken ct)
        => LayoutsProxy.CallAsync<RenameLayoutArgs, LayoutResult>(gw, "acad.layouts.rename_layout", args, T_NORMAL, ct);



    [McpTool("configure_plot", "Configure a layout's plot settings: plotter / device name, paper size (accepts canonical 'ISO_full_bleed_A0_(1189.00_x_841.00_MM)', locale 'ISO A0 (841.00 x 1189.00 MM)', or fuzzy alias 'A0' / 'ISO A0' / 'a0' — all three resolve to the plotter's canonical name), named plot style table, and 0/90/180/270 plot rotation. Pass null on any field to leave it untouched. Call layouts.list_paper_sizes first if you're unsure which media strings the installed plotter accepts.", "layouts",
        Intent = new[] { "konfiguruj plot", "ustaw drukarke layoutu", "configure plot settings", "set plotter and paper", "ustaw rozmiar arkusza" },
        RequiresPlugin = true)]
    public static Task<LayoutResult> ConfigurePlot(IPluginGateway gw, ConfigurePlotArgs args, CancellationToken ct)
        => LayoutsProxy.CallAsync<ConfigurePlotArgs, LayoutResult>(gw, "acad.layouts.configure_plot", args, T_NORMAL, ct);

    [McpTool("get_layout", "Return everything about one paper-space layout: its sheet size, plot device, plot style table, orientation, scale and tab order. Use it to check a sheet is set up before plotting from it. list_layouts is the one to use when the name is not known yet, and the viewports ON a layout are a separate question - see the viewports category.", "layouts",
        Intent = new[] { "pokaz layout", "get layout info", "describe layout", "info o layoucie", "show layout details" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<LayoutResult> GetLayout(IPluginGateway gw, LayoutNameArg args, CancellationToken ct)
        => LayoutsProxy.CallAsync<LayoutNameArg, LayoutResult>(gw, "acad.layouts.get_layout", args, T_FAST, ct);

    [McpTool("list_paper_sizes",
        "Enumerate every paper size supported by a plotter (plotter=null -> the current layout's plotter or the first registered device). Returns the canonical media names (what configure_plot needs) plus the locale-facing display name. Call this before configure_plot if you're unsure which media strings the installed plotter accepts — especially for non-standard devices (e.g. 'DWG To PDF.pc3', 'Microsoft Print to PDF', pen plotters, PublishToWeb PNG). configure_plot accepts canonical / locale / fuzzy names (e.g. 'A0', 'ISO A0'); use this tool when even fuzzy resolution fails.",
        "layouts",
        Intent = new[] { "list paper sizes", "wylistuj rozmiary papieru", "show available media", "jakie rozmiary arkuszy", "enumerate plotter paper sizes", "which papers does the plotter support", "available paper formats" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<ListPaperSizesResult> ListPaperSizes(IPluginGateway gw, ListPaperSizesArgs args, CancellationToken ct)
        => LayoutsProxy.CallAsync<ListPaperSizesArgs, ListPaperSizesResult>(gw, "acad.layouts.list_paper_sizes", args, T_FAST, ct);
}
