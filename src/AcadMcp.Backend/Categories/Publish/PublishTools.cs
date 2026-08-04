// MCP tools for the acad-publish category — turning a finished model into deliverables.
//
// This first tranche is named page setups, contracted in
// docs/engineering-rules/44-page-setups.md before a line of it was written.
//
// A page setup is NOT the same thing as a layout's own plot configuration.
// layouts.configure_plot configures one layout directly and stays exactly as it is; that is the
// right tool when there is nothing to reuse. These tools define reusable, named configurations
// that can be applied to many layouts and carried between drawings — which is how a firm keeps
// twenty sheets plotting identically.
//
// Deliberately deferred to later tranches of roadmap 2.2:
//   publish_layouts / publish_to_pdf_multisheet / publish_to_dwf — the Publisher API, a
//     different and heavier mechanism than PlotFactory. files.export_file already covers
//     single-sheet output and covers it well.
//   set_plot_stamp — PLOTSTAMP is a per-plot decoration, not part of PlotSettings.
//   batch_plot_from_list, plot_preview_extents, get_plot_status, cancel_plot — all need the
//     async plot queue, which is the mechanism export_file had to force OFF (BACKGROUNDPLOT)
//     to get a file that existed when the call returned. Wiring an agent to a queue that
//     reports completion before it completes needs its own contract first.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Publish;

public static class PublishTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;

    [McpTool("create_page_setup", "Define a NAMED, reusable page setup in this drawing - device, paper size, plot style table, rotation - that can then be applied to many layouts at once. Either snapshot a layout you already configured by hand (fromLayout), or state the settings explicitly; passing both is an error rather than a precedence rule nobody remembers. Refuses to overwrite an existing name unless overwrite is true, because a firm's standard page setups should not be redefined by accident.", "publish",
        Intent = new[] { "utworz nazwane ustawienia strony", "zdefiniuj page setup", "create page setup",
                         "named plot configuration", "reusable sheet setup", "zapisz konfiguracje wydruku pod nazwa" },
        RequiresPlugin = true)]
    public static Task<PageSetupResult> CreatePageSetup(IPluginGateway gw, CreatePageSetupArgs args, CancellationToken ct)
        => PublishProxy.CallAsync<CreatePageSetupArgs, PageSetupResult>(gw, "acad.publish.create_page_setup", args, T_NORMAL, ct);

    [McpTool("list_page_setups", "List the named page setups defined in this drawing, with the device, paper size, plot style table and rotation each one carries. Read-only. Call this before apply_page_setup - the names are per-drawing, so an agent cannot know them in advance.", "publish",
        Intent = new[] { "wylistuj ustawienia strony", "jakie sa page setupy", "list page setups",
                         "show named plot configurations", "what sheet setups exist", "pokaz konfiguracje wydruku" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<PageSetupListResult> ListPageSetups(IPluginGateway gw, EmptyPublishArgs args, CancellationToken ct)
        => PublishProxy.CallAsync<EmptyPublishArgs, PageSetupListResult>(gw, "acad.publish.list_page_setups", args, T_FAST, ct);

    [McpTool("apply_page_setup", "Apply a named page setup to layouts, so a whole set plots identically. Name the layouts explicitly, or pass allLayouts true - there is no 'all layouts' default, because applying a page setup to every tab in a drawing because an argument was omitted is precisely the accident worth designing out. Reports the outcome per layout rather than a count, so a partial success reads as one.", "publish",
        Intent = new[] { "zastosuj ustawienia strony do arkuszy", "ustaw ten sam wydruk na wszystkich ukladach",
                         "apply page setup to layouts", "make all sheets plot the same", "assign page setup",
                         "przypisz konfiguracje wydruku do ukladu" },
        RequiresPlugin = true)]
    public static Task<ApplyPageSetupResult> ApplyPageSetup(IPluginGateway gw, ApplyPageSetupArgs args, CancellationToken ct)
        => PublishProxy.CallAsync<ApplyPageSetupArgs, ApplyPageSetupResult>(gw, "acad.publish.apply_page_setup", args, T_NORMAL, ct);

    [McpTool("delete_page_setup", "Remove a named page setup from this drawing. Layouts previously configured from it keep their settings - applying a page setup copies it rather than linking to it - and the result says so, so nobody expects issued sheets to revert.", "publish",
        Intent = new[] { "usun nazwane ustawienia strony", "skasuj page setup", "delete page setup",
                         "remove named plot configuration", "get rid of sheet setup", "wykasuj konfiguracje wydruku" },
        RequiresPlugin = true)]
    public static Task<DeletePageSetupResult> DeletePageSetup(IPluginGateway gw, PageSetupNameArgs args, CancellationToken ct)
        => PublishProxy.CallAsync<PageSetupNameArgs, DeletePageSetupResult>(gw, "acad.publish.delete_page_setup", args, T_NORMAL, ct);

}
