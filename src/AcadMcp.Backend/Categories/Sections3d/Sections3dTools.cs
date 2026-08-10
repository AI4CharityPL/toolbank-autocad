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

    [McpTool("create_section_orthographic", "Place a section plane through the middle of the model in one of the six standard views - front, back, left, right, top or bottom - without working out any coordinates yourself. Front and back cut across the width looking along Y, left and right across the depth looking along X, and top and bottom are the horizontal cut a floor plan is made from. `offset` shifts the plane off centre along the direction it looks, which is how you cut at a particular floor rather than half way up; `sourceHandles` restricts the extents to named solids instead of the whole of model space. AutoCAD has no API for this - neither Section.CreateOrthographic nor SetOrthographic exists - so the plane is placed by arithmetic over the model's extents, and the extents used are reported back so the position can be checked. Like every section plane it CUTS NOTHING: use generate_section to draw the result.", "sections-3d",
        Intent = new[] { "place a front section through the model", "cut a standard elevation",
                         "przekroj czolowy przez model", "section the model from the left",
                         "przekroj poziomy przez srodek modelu", "make a top section plane",
                         "orthographic section plane", "section plane through the middle" },
        RequiresPlugin = true)]
    public static Task<SectionOrthographicResult> CreateSectionOrthographic(IPluginGateway gw, SectionOrthographicArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionOrthographicArgs, SectionOrthographicResult>(gw, "acad.sections3d.create_section_orthographic", args, T_SLOW, ct);

    [McpTool("generate_section_block", "Draw the geometry a section plane would cut and put it into a BLOCK rather than leaving it loose in the drawing - what SECTIONPLANETOBLOCK does. Use this when the section is to be moved, copied onto a sheet or scaled as one thing; use generate_section when the curves are wanted individually for dimensioning or editing. sourceHandles is required and names the solids to cut. The block name must not already exist, because overwriting a definition would silently change every insert of it. The curves live inside the block, so they will not appear in a model-space selection. The source solids are untouched.", "sections-3d",
        Intent = new[] { "generate the section as a block", "sectionplanetoblock",
                         "wygeneruj przekroj jako blok", "make a section block",
                         "przekroj do bloku", "put the section geometry in a block",
                         "section block from this plane" },
        RequiresPlugin = true)]
    public static Task<SectionBlockResult> GenerateSectionBlock(IPluginGateway gw, SectionBlockArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionBlockArgs, SectionBlockResult>(gw, "acad.sections3d.generate_section_block", args, T_SLOW, ct);

    [McpTool("set_section_settings", "Control how a section is DRAWN: the colour, layer, visibility, division lines, hidden-line treatment and linetype scale of each part of it, plus which objects it takes as its source. `part` picks what is being styled - cut (the outline of the cut face), fill (the poche inside it), background (what lies beyond the plane) or foreground (what lies in front of it) - and this is how a section reads as a drawing rather than a wireframe. Settings are held per section TYPE as well, so `kind` 2d and 3d carry their own. NOT every property applies to every part, and the limits are measured rather than guessed: colour, layer and linetypeScale work on all four parts of a 2d or 3d section; `visible` works everywhere EXCEPT the cut of a 2d section (the cut outline IS the section, so it cannot be hidden) and the background of a 3d one; `divisionLines` exists only on the cut of a 2d section; `hiddenLine` only on the background and foreground of a 2d section. NOTHING can be styled on kind=live - AutoCAD refuses every property there, so use set_live_section to switch live sectioning and let it take its appearance from the model. Asking for an unsupported combination is refused with the reason rather than passed through as AutoCAD's bare eInvalidInput. faceTransparency and edgeTransparency are REPORTED but cannot be set: the setters exist and refuse every value from 0 to 255 on every part. Every value is READ BACK after being written and reported, because a setting the object quietly declines to keep would otherwise look identical to one it took. These affect what generate_section and generate_section_block produce NEXT time; geometry already drawn keeps the settings it was drawn with.", "sections-3d",
        Intent = new[] { "set the colour of the section cut lines", "style the section hatch",
                         "ustaw kolor przekroju", "change the section layer",
                         "ustawienia wygladu przekroju", "hide the background geometry in a section",
                         "section display settings", "przezroczystosc przekroju" },
        RequiresPlugin = true)]
    public static Task<SectionSettingsResult> SetSectionSettings(IPluginGateway gw, SectionSettingsArgs args, CancellationToken ct)
        => Sections3dProxy.CallAsync<SectionSettingsArgs, SectionSettingsResult>(gw, "acad.sections3d.set_section_settings", args, T_NORMAL, ct);
}
