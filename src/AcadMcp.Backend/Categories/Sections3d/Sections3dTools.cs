// AutoCAD acad-sections-3d category. A section plane is a CUTTING PLANE that lives in the
// drawing: it does not modify the solids it crosses, it reports what the cut would look like.
// That is the whole difference from geometry_3d.slice_solid, which really does cut and hands
// back two solids.
//
// Rules: 19-tool-implementation-pattern.md, 20..25, 26 (traps).

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Sections3d;

public static class Sections3dTools
{
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("create_section_plane", "Place a section plane through a model - AutoCAD's SECTIONPLANE. Give the vertices of the line the section is cut along, seen in plan; two points give a straight section and more give a JOGGED one, which is how a plan cuts through different parts of a building at different places. The cut plane is the one CONTAINING that line and verticalDirection, which defaults to (0,0,1) and gives the upright section a plan line means; pass a horizontal vector instead and you get a horizontal cut through the model. The plane's normal - which way the section looks - follows from those two and CANNOT be given, because Section.Normal is read-only; reverse the order of the vertices to look the other way. Note that a section plane CUTS NOTHING: it is an object in the drawing that reports what a cut would look like, and the solids it crosses are untouched. geometry_3d.slice_solid is the one that really cuts.", "sections-3d",
        Intent = new[] { "place a section plane through the model", "sectionplane",
                         "wstaw plaszczyzne przekroju", "cut a section through this building",
                         "przekroj przez model", "make a jogged section line",
                         "add a section plane at this line",
                         "poziomy przekroj przez model" },
        RequiresPlugin = true)]
    public static Task<SectionCreateResult> CreateSectionPlane(IPluginGateway gw, SectionCreateArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionCreateArgs, SectionCreateResult>(gw, "acad.sections3d.create_section_plane", args, T_SLOW, ct);

    [McpTool("list_section_planes", "List every section plane in the drawing with its state, whether it is the live section, how many vertices its cut line has, its elevation and its normal. Read-only. Use it before the other tools here, which all address a plane by handle. Worth knowing: a section plane is an OBJECT and stays in the drawing until erased, and at most one can be the live section at a time.", "sections-3d",
        Intent = new[] { "list the section planes in this drawing", "what sections are defined",
                         "wypisz plaszczyzny przekroju", "show all section planes",
                         "ile przekrojow ma ten rysunek", "find the live section",
                         "which section plane is live" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SectionListResult> ListSectionPlanes(IPluginGateway gw, SectionListArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionListArgs, SectionListResult>(gw, "acad.sections3d.list_section_planes", args, T_NORMAL, ct);

    [McpTool("set_section_state", "Switch a section plane between its three states. PLANE is an unbounded cut, which is what a building section wants. BOUNDARY clips it to the outline the plane was given. VOLUME clips it to a box as well, which is what isolates one room or one bay out of a whole model. The state decides what generate_section produces and what a live section shows; it does not change the cut line itself. A height set on a plane-state section appears to do nothing, because only boundary and volume respect it.", "sections-3d",
        Intent = new[] { "change the section plane state", "make this section a volume",
                         "zmien stan plaszczyzny przekroju", "clip the section to a box",
                         "isolate one room with a section volume", "przekroj objetosciowy",
                         "switch section to boundary" },
        RequiresPlugin = true)]
    public static Task<SectionStateResult> SetSectionState(IPluginGateway gw, SectionStateArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionStateArgs, SectionStateResult>(gw, "acad.sections3d.set_section_state", args, T_NORMAL, ct);

    [McpTool("set_live_section", "Turn the LIVE section display on or off for a plane - AutoCAD's LIVESECTION. A live section shows the cut on screen without drawing anything: the model in front of the plane is hidden and the cut face is shaded, and nothing is added to the drawing. generate_section is the other half, drawing real geometry you can dimension and plot. Only one section can be live at a time, so turning this on turns the others off - that is AutoCAD behaviour, not a choice made here.", "sections-3d",
        Intent = new[] { "turn on the live section", "livesection",
                         "wlacz przekroj na zywo", "show the cut on screen without drawing it",
                         "wylacz podglad przekroju", "toggle live sectioning",
                         "hide the model in front of the section" },
        RequiresPlugin = true)]
    public static Task<SectionLiveResult> SetLiveSection(IPluginGateway gw, SectionLiveArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionLiveArgs, SectionLiveResult>(gw, "acad.sections3d.set_live_section", args, T_NORMAL, ct);

    [McpTool("set_section_height", "Set how far a section plane reaches up and down from its cut line, and where that line sits in Z. `above` and `below` are the reach; `elevation` is the height of the line itself. Every value is read back after being set, because these are plain properties that can accept a number the object then declines to keep. IMPORTANT: heights only bite in the BOUNDARY and VOLUME states - a plane-state section is unbounded and ignores them, which is why a height that appears to do nothing usually means the state is still plane.", "sections-3d",
        Intent = new[] { "set the height of a section plane", "how far the section reaches up",
                         "ustaw wysokosc plaszczyzny przekroju", "set section elevation",
                         "rzedna plaszczyzny przekroju", "limit the section depth",
                         "section plane above and below" },
        RequiresPlugin = true)]
    public static Task<SectionHeightResult> SetSectionHeight(IPluginGateway gw, SectionHeightArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionHeightArgs, SectionHeightResult>(gw, "acad.sections3d.set_section_height", args, T_NORMAL, ct);

    [McpTool("generate_section", "Draw the geometry a section plane would cut - AutoCAD's SECTIONPLANETOBLOCK. kind 2d gives flat curves you can dimension and plot, 3d gives the cut model as solids, and live gives what the live-section display shows. sourceHandles is required and names the solids to cut: a section plane is a plane, not a query, so it does not know what it crosses. By default only the CUT curves are drawn - what the plane passes through - because that is what a section drawing wants; background, foreground and tangency curves are what lies beyond, in front of and along the silhouette, and each is opt-in. A plane placed clear of the model produces an empty result rather than a complaint, so the tool refuses that case instead of reporting a success over nothing.", "sections-3d",
        Intent = new[] { "generate the section geometry", "sectionplanetoblock",
                         "wygeneruj przekroj z plaszczyzny", "draw the 2d section",
                         "narysuj przekroj przez bryly", "make a section block from this plane",
                         "turn a section plane into drawn geometry" },
        RequiresPlugin = true)]
    public static Task<SectionGenerateResult> GenerateSection(IPluginGateway gw, SectionGenerateArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionGenerateArgs, SectionGenerateResult>(gw, "acad.sections3d.generate_section", args, T_SLOW, ct);
}
