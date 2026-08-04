// MCP tools for the acad-ucs category: User Coordinate Systems.
//
// The contract every drawing tool follows is settled in
// docs/engineering-rules/43-coordinate-systems.md: coordinates are WCS unless a tool is given
// an explicit optional `ucs`, absence always means WCS, results always come back in WCS, and
// an unknown UCS name is an error rather than a silent fallback. This category creates and
// manages the coordinate systems themselves; drawing tools gain the `ucs` argument
// progressively as they are next touched, so nothing here changes an existing call.
//
// transform_point exists so a caller can do the conversion explicitly today, before every
// drawing tool carries the argument.
//
// Deliberately not here:
//   ucs_from_face  - needs subentity picking, which has no non-interactive form
//   ucs_icon       - display-only; changes nothing an API caller can observe

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Ucs;

public static class UcsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    // ─────────────── creation ───────────────

    [McpTool("create_ucs_3point", "Define a UCS from three points: origin, a point on the positive X axis, and a point on the positive-Y side. This is the general case - it fixes origin, rotation and plane in one call. Pass a name to save it; makeCurrent defaults to true.", "ucs",
        Intent = new[] { "utworz uklad wspolrzednych z trzech punktow", "zdefiniuj ucs przez punkty", "create ucs from 3 points",
                         "define coordinate system by origin and axes", "set ucs three point", "nowy uklad na podstawie punktow" },
        RequiresPlugin = true)]
    public static Task<UcsResult> CreateUcs3Point(IPluginGateway gw, UcsFrom3PointsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<UcsFrom3PointsArgs, UcsResult>(gw, "acad.ucs.create_ucs_3point", args, T_NORMAL, ct);

    [McpTool("create_ucs_origin", "Move the UCS origin without changing its axis directions. The cheapest useful UCS: work near a building corner in small local coordinates instead of large absolute ones.", "ucs",
        Intent = new[] { "przesun poczatek ukladu", "ustaw origin ucs", "move ucs origin",
                         "shift coordinate system origin", "set local origin", "nowy punkt zerowy" },
        RequiresPlugin = true)]
    public static Task<UcsResult> CreateUcsOrigin(IPluginGateway gw, UcsFromOriginArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<UcsFromOriginArgs, UcsResult>(gw, "acad.ucs.create_ucs_origin", args, T_NORMAL, ct);

    [McpTool("create_ucs_zaxis", "Define a UCS from an origin and a Z-axis direction; X and Y are derived. Use this for work on an inclined plane - a roof slope, a ramp, a sloped section.", "ucs",
        Intent = new[] { "utworz ucs z kierunku osi z", "uklad na plaszczyznie pochylej", "create ucs from z axis",
                         "ucs on inclined plane", "define coordinate system by normal", "uklad dla polaci dachu" },
        RequiresPlugin = true)]
    public static Task<UcsResult> CreateUcsZAxis(IPluginGateway gw, UcsFromZAxisArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<UcsFromZAxisArgs, UcsResult>(gw, "acad.ucs.create_ucs_zaxis", args, T_NORMAL, ct);

    [McpTool("rotate_ucs", "Rotate the current UCS about its own X, Y or Z axis by an angle in degrees. axis is 'x', 'y' or 'z'. Rotating about Z is the usual case: aligning to a wall that does not run north-south.", "ucs",
        Intent = new[] { "obroc uklad wspolrzednych", "obroc ucs wokol osi z", "rotate ucs",
                         "turn coordinate system", "align ucs to angled wall", "obrot ukladu o kat" },
        RequiresPlugin = true)]
    public static Task<UcsResult> RotateUcs(IPluginGateway gw, UcsRotateArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<UcsRotateArgs, UcsResult>(gw, "acad.ucs.rotate_ucs", args, T_NORMAL, ct);

    [McpTool("create_ucs_from_entity", "Align the UCS to an existing entity's own plane and orientation, given its handle. The fastest way to start drawing on something already in the model - a wall face, a sloped polyline, a rotated block.", "ucs",
        Intent = new[] { "ustaw ucs wedlug obiektu", "wyrownaj uklad do encji", "create ucs from object",
                         "align ucs to entity", "coordinate system from existing geometry", "uklad wedlug istniejacego elementu" },
        RequiresPlugin = true)]
    public static Task<UcsResult> CreateUcsFromEntity(IPluginGateway gw, EntityHandleUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<EntityHandleUcsArgs, UcsResult>(gw, "acad.ucs.create_ucs_from_entity", args, T_NORMAL, ct);

    // ─────────────── world / restore ───────────────

    [McpTool("set_ucs_world", "Reset the current UCS to WCS. Every tool in the bank interprets coordinates in WCS by default, so this returns the drawing to the state those tools assume.", "ucs",
        Intent = new[] { "przywroc uklad swiatowy", "ustaw ucs na world", "set ucs to world",
                         "reset coordinate system", "back to wcs", "wroc do globalnego ukladu" },
        RequiresPlugin = true)]
    public static Task<UcsResult> SetUcsWorld(IPluginGateway gw, EmptyUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<EmptyUcsArgs, UcsResult>(gw, "acad.ucs.set_ucs_world", args, T_FAST, ct);

    [McpTool("save_ucs", "Save the current UCS under a name so it can be restored later. Named UCSs are how a multi-storey or multi-wing project keeps its local coordinate systems addressable.", "ucs",
        Intent = new[] { "zapisz uklad wspolrzednych", "nazwij biezacy ucs", "save named ucs",
                         "store current coordinate system", "name this ucs", "zachowaj uklad pod nazwa" },
        RequiresPlugin = true)]
    public static Task<UcsResult> SaveUcs(IPluginGateway gw, SaveUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<SaveUcsArgs, UcsResult>(gw, "acad.ucs.save_ucs", args, T_NORMAL, ct);

    [McpTool("restore_ucs", "Make a previously saved named UCS current. Errors if the name does not exist rather than silently leaving the current UCS in place.", "ucs",
        Intent = new[] { "przywroc zapisany uklad", "wczytaj nazwany ucs", "restore named ucs",
                         "switch to saved coordinate system", "load ucs by name", "uzyj zapisanego ukladu" },
        RequiresPlugin = true)]
    public static Task<UcsResult> RestoreUcs(IPluginGateway gw, UcsNameArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<UcsNameArgs, UcsResult>(gw, "acad.ucs.restore_ucs", args, T_NORMAL, ct);

    [McpTool("delete_ucs", "Delete a named UCS. The current UCS is unaffected even if it happens to match the deleted definition.", "ucs",
        Intent = new[] { "usun nazwany uklad", "skasuj ucs", "delete named ucs",
                         "remove saved coordinate system", "drop ucs by name", "wykasuj uklad wspolrzednych" },
        RequiresPlugin = true)]
    public static Task<UcsAffected> DeleteUcs(IPluginGateway gw, UcsNameArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<UcsNameArgs, UcsAffected>(gw, "acad.ucs.delete_ucs", args, T_NORMAL, ct);

    [McpTool("rename_ucs", "Rename a saved UCS. The new name must be a valid AutoCAD symbol name and must not already exist.", "ucs",
        Intent = new[] { "zmien nazwe ukladu", "przemianuj ucs", "rename named ucs",
                         "change ucs name", "rename saved coordinate system", "nowa nazwa ukladu" },
        RequiresPlugin = true)]
    public static Task<UcsAffected> RenameUcs(IPluginGateway gw, RenameUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<RenameUcsArgs, UcsAffected>(gw, "acad.ucs.rename_ucs", args, T_NORMAL, ct);

    // ─────────────── inspection ───────────────

    [McpTool("get_current_ucs", "Return the current UCS: origin and the three axis vectors in WCS, plus whether it is the world system. Read-only.", "ucs",
        Intent = new[] { "pokaz biezacy uklad", "jaki jest aktualny ucs", "get current ucs",
                         "what coordinate system is active", "current ucs origin and axes", "aktualny uklad wspolrzednych" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<UcsResult> GetCurrentUcs(IPluginGateway gw, EmptyUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<EmptyUcsArgs, UcsResult>(gw, "acad.ucs.get_current_ucs", args, T_FAST, ct);

    [McpTool("list_ucs", "List every named UCS in the drawing with its origin and axes, plus the current one. Read-only. Call this before restore_ucs to see what names exist.", "ucs",
        Intent = new[] { "wylistuj uklady wspolrzednych", "jakie ucs sa zapisane", "list named ucs",
                         "show all coordinate systems", "what ucs exist in this drawing", "zapisane uklady" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<UcsListResult> ListUcs(IPluginGateway gw, EmptyUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<EmptyUcsArgs, UcsListResult>(gw, "acad.ucs.list_ucs", args, T_FAST, ct);

    [McpTool("transform_point", "Convert one point between coordinate systems. 'from' and 'to' each accept 'world', 'current', or a saved UCS name. Use this to work out WCS coordinates for the drawing tools while they are still WCS-only.", "ucs",
        Intent = new[] { "przelicz punkt miedzy ukladami", "zamien wspolrzedne ucs na world", "transform point between coordinate systems",
                         "convert ucs coordinates to wcs", "translate point to another ucs", "przeliczenie wspolrzednych" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<TransformPointResult> TransformPoint(IPluginGateway gw, TransformPointArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<TransformPointArgs, TransformPointResult>(gw, "acad.ucs.transform_point", args, T_FAST, ct);

    // ─────────── closing out roadmap 1.2 ───────────

    [McpTool("create_ucs_from_view", "Set the current UCS so its XY plane faces the screen - AutoCAD's UCS View. This is what makes text and dimensions placed on an isometric or a rotated view read straight instead of lying flat on the model's own plane. The origin stays where the current UCS has it, because changing what you are looking at should not move where coordinates are measured from.", "ucs",
        Intent = new[] { "ukd rownolegly do ekranu", "uklad wspolrzednych z widoku", "create ucs from view",
                         "ucs view", "align ucs to screen", "tekst prostopadle do ekranu na izometrii" },
        RequiresPlugin = true)]
    public static Task<UcsResult> CreateUcsFromView(IPluginGateway gw, EmptyUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<EmptyUcsArgs, UcsResult>(gw, "acad.ucs.create_ucs_from_view", args, T_FAST, ct);

    [McpTool("set_ucs_previous", "Step back to the UCS in use before the last change, like AutoCAD's UCS Previous. The history covers changes made through these tools in this session only - a UCS changed by hand in AutoCAD is not in it, and the tool says so rather than silently doing nothing. Reports how many steps remain.", "ucs",
        Intent = new[] { "wroc do poprzedniego ukd", "cofnij zmiane ukladu wspolrzednych", "set ucs previous",
                         "undo ucs change", "previous coordinate system", "poprzedni uklad wspolrzednych" },
        RequiresPlugin = true)]
    public static Task<UcsPreviousResult> SetUcsPrevious(IPluginGateway gw, EmptyUcsArgs args, CancellationToken ct)
        => UcsProxy.CallAsync<EmptyUcsArgs, UcsPreviousResult>(gw, "acad.ucs.set_ucs_previous", args, T_FAST, ct);
}
