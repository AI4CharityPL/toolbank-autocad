// AutoCAD acad-livestream category (Phase 7.1). Poll-based access to entity-change
// and command-lifecycle events captured by the plugin's Database/Document event
// hooks (LivestreamPluginTools.cs) into a bounded ring buffer.
//
// Honest scope note (see LivestreamPluginTools.cs header for the full rationale):
// this is poll-based, not a raw second named pipe or true server push -- an MCP
// tool-calling agent has no channel to receive an unsolicited frame regardless of
// which pipe events travel over, so poll_events(sinceSeq) is what "livestream"
// actually means for this kind of client. Events are captured for real via
// AutoCAD API hooks, not synthesized or polled from AutoCAD itself.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Livestream;

public static class LivestreamTools
{
    private const int T_FAST = 5_000;

    [McpTool("poll_events",
        "Return entity-change and command-lifecycle events captured since sinceSeq (default 0 = from the start of the current buffer, which holds the most recent 2000 events). Each event has a monotonic seq -- pass the previous response's nextSeq back in as sinceSeq to get only what happened since your last poll. maxCount caps how many are returned in one call (default 200). Events are captured live via AutoCAD Database.ObjectAppended/Modified/Erased and Document.CommandWillStart/CommandEnded hooks, not by re-scanning the drawing.",
        "livestream",
        Intent = new[]
        {
            "co sie zmienilo od ostatniego razu",
            "poll recent events",
            "what changed since last check",
            "pobierz zdarzenia rysunku",
            "get entity change events"
        },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<PollEventsResult> PollEvents(IPluginGateway gw, PollEventsArgs args, CancellationToken ct)
        => LivestreamProxy.CallAsync<PollEventsArgs, PollEventsResult>(gw, "acad.livestream.poll", args, T_FAST, ct);

    [McpTool("livestream_status",
        "Report the event ring buffer's current size, capacity (2000), the highest sequence number issued so far (headSeq), how many older events have been dropped to stay within capacity, and how many documents currently have event hooks attached.",
        "livestream",
        Intent = new[]
        {
            "status bufora zdarzen",
            "livestream buffer status",
            "how many events buffered",
            "diagnostyka livestream",
            "event buffer diagnostics"
        },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<LivestreamStatusResult> LivestreamStatus(IPluginGateway gw, LivestreamEmptyArgs args, CancellationToken ct)
        => LivestreamProxy.CallAsync<LivestreamEmptyArgs, LivestreamStatusResult>(gw, "acad.livestream.status", args, T_FAST, ct);

    [McpTool("clear_events",
        "Discard all currently buffered events (does not affect future capture, only the backlog). Use this to reset state, e.g. at the start of a design_iterate loop so old noise doesn't show up in the first poll.",
        "livestream",
        Intent = new[]
        {
            "wyczysc bufor zdarzen",
            "clear event buffer",
            "reset livestream events",
            "wyczysc historie zdarzen",
            "discard buffered events"
        },
        RequiresPlugin = true)]
    public static Task<ClearEventsResult> ClearEvents(IPluginGateway gw, LivestreamEmptyArgs args, CancellationToken ct)
        => LivestreamProxy.CallAsync<LivestreamEmptyArgs, ClearEventsResult>(gw, "acad.livestream.clear", args, T_FAST, ct);
}
