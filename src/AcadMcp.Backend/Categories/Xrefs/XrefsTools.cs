// MCP tools for the acad-xrefs category: external reference management.
//
// Deliberately NOT in this category (see docs/COVERAGE-ROADMAP.md §1.1):
//   refedit_begin / refedit_save / refedit_discard - REFEDIT is a modal, stateful command
//   sequence and belongs to the command layer that produced eInvalidInput in zoom_extents and
//   silent queueing in undo/redo. Shipping it before that channel has a supervised contract
//   would repeat a mistake this repository has already paid for twice. The parametric
//   constraint tools set the precedent: withheld rather than shipped broken.
//
// Vocabulary: blockName identifies an xref DEFINITION (path, reload, bind, overrides);
// handle identifies one BlockReference INSERT of it (clipping is per-insert).

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Xrefs;

/// <summary>Parameterless args for the xref tools that take no input.</summary>
public sealed record EmptyXrefArgs();

public static class XrefsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;
    private const int T_SLOW = 60_000;   // attach/reload/bind read whole drawings off disk

    // ─────────────────────── attach / detach ───────────────────────

    [McpTool("attach_xref", "Attach an external drawing as an XREF (attachment). Attachments are carried into any drawing that in turn references this one - use attach_xref_overlay when you do not want that. Block name defaults to the file name; insertion, scale and rotation default to origin/1/0.", "xrefs",
        Intent = new[] { "podlacz rysunek jako xref", "dolacz plik dwg jako referencje", "attach external reference",
                         "attach xref", "reference another drawing", "wstaw podklad z innego pliku" },
        RequiresPlugin = true)]
    public static Task<XrefAttachResult> AttachXref(IPluginGateway gw, AttachXrefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<AttachXrefArgs, XrefAttachResult>(gw, "acad.xrefs.attach_xref", args, T_SLOW, ct);

    [McpTool("attach_xref_overlay", "Attach an external drawing as an OVERLAY. Overlays are NOT carried through when this drawing is itself referenced elsewhere, which is what stops circular and duplicated references in a multi-discipline set. Prefer this for cross-discipline backgrounds.", "xrefs",
        Intent = new[] { "podlacz jako nakladka", "dolacz xref jako overlay", "attach as overlay",
                         "overlay external reference", "reference without propagating", "xref nakladkowy" },
        RequiresPlugin = true)]
    public static Task<XrefAttachResult> AttachXrefOverlay(IPluginGateway gw, AttachXrefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<AttachXrefArgs, XrefAttachResult>(gw, "acad.xrefs.attach_xref_overlay", args, T_SLOW, ct);

    [McpTool("detach_xref", "Detach an XREF completely: removes the definition and every insert of it. Fails if the xref is nested under another reference - detach the parent instead.", "xrefs",
        Intent = new[] { "odlacz xref", "usun referencje zewnetrzna", "detach xref",
                         "remove external reference", "get rid of xref", "skasuj podklad" },
        RequiresPlugin = true)]
    public static Task<XrefAffected> DetachXref(IPluginGateway gw, XrefRefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefRefArgs, XrefAffected>(gw, "acad.xrefs.detach_xref", args, T_NORMAL, ct);

    [McpTool("reload_xref", "Reload one XREF from disk, picking up changes another author has saved. Returns the resolved status so a failure to find the file is visible rather than silent.", "xrefs",
        Intent = new[] { "przeladuj xref", "odswiez referencje", "reload xref",
                         "refresh external reference", "pull latest xref changes", "zaktualizuj podklad" },
        RequiresPlugin = true)]
    public static Task<XrefInfoResult> ReloadXref(IPluginGateway gw, XrefRefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefRefArgs, XrefInfoResult>(gw, "acad.xrefs.reload_xref", args, T_SLOW, ct);

    [McpTool("reload_all_xrefs", "Reload every resolved XREF in the drawing. Reports per-xref status so partial failures are visible.", "xrefs",
        Intent = new[] { "przeladuj wszystkie xrefy", "odswiez wszystkie referencje", "reload all xrefs",
                         "refresh every external reference", "update all references", "zaktualizuj wszystkie podklady" },
        RequiresPlugin = true)]
    public static Task<XrefListResult> ReloadAllXrefs(IPluginGateway gw, EmptyXrefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<EmptyXrefArgs, XrefListResult>(gw, "acad.xrefs.reload_all_xrefs", args, T_SLOW, ct);

    [McpTool("unload_xref", "Unload an XREF: its geometry stops displaying and stops loading, but the definition and inserts stay so it can be reloaded later. Use this rather than detach for temporarily hiding a heavy reference.", "xrefs",
        Intent = new[] { "wyladuj xref", "ukryj referencje bez usuwania", "unload xref",
                         "temporarily hide external reference", "stop loading xref", "wylacz podklad" },
        RequiresPlugin = true)]
    public static Task<XrefAffected> UnloadXref(IPluginGateway gw, XrefRefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefRefArgs, XrefAffected>(gw, "acad.xrefs.unload_xref", args, T_NORMAL, ct);

    [McpTool("bind_xref", "Bind an XREF into this drawing, making it a permanent local block. Default (bind mode) renames dependent symbols to blockName$0$LAYER; insertMode=true merges them into existing local symbols instead, which is usually what you want for issue-ready files but can collide with local names.", "xrefs",
        Intent = new[] { "zwiaz xref na stale", "wbuduj referencje w rysunek", "bind xref",
                         "make xref permanent", "convert xref to block", "scal podklad z rysunkiem" },
        RequiresPlugin = true)]
    public static Task<XrefAffected> BindXref(IPluginGateway gw, XrefBindArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefBindArgs, XrefAffected>(gw, "acad.xrefs.bind_xref", args, T_SLOW, ct);

    // ─────────────────────── inspection ───────────────────────

    [McpTool("list_xrefs", "List every XREF in the drawing with its path, resolution status, overlay/attachment kind, nesting and insert count. Read-only. This is the tool to call first when a drawing does not look right.", "xrefs",
        Intent = new[] { "wylistuj xrefy", "pokaz referencje zewnetrzne", "list external references",
                         "what xrefs are in this drawing", "show all references", "jakie podklady sa dolaczone" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<XrefListResult> ListXrefs(IPluginGateway gw, EmptyXrefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<EmptyXrefArgs, XrefListResult>(gw, "acad.xrefs.list_xrefs", args, T_FAST, ct);

    [McpTool("get_xref_info", "Full descriptor of one XREF by block name, plus the handles of every BlockReference insert of it. Use the returned handles for clipping, which is per-insert.", "xrefs",
        Intent = new[] { "pokaz szczegoly xrefa", "informacje o referencji", "get xref details",
                         "xref info", "describe external reference", "gdzie jest wstawiony ten xref" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<XrefInfoResult> GetXrefInfo(IPluginGateway gw, XrefRefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefRefArgs, XrefInfoResult>(gw, "acad.xrefs.get_xref_info", args, T_FAST, ct);

    [McpTool("list_nested_xrefs", "List XREFs that are referenced by another XREF rather than directly by this drawing, with their parent. Nested references cannot be detached or repathed here - do that in the parent drawing.", "xrefs",
        Intent = new[] { "wylistuj zagniezdzone xrefy", "pokaz referencje w referencjach", "list nested xrefs",
                         "show nested external references", "which xrefs come from other xrefs", "xrefy posrednie" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<XrefListResult> ListNestedXrefs(IPluginGateway gw, EmptyXrefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<EmptyXrefArgs, XrefListResult>(gw, "acad.xrefs.list_nested_xrefs", args, T_FAST, ct);

    [McpTool("find_missing_xrefs", "List every XREF whose file cannot be resolved at its saved path. Read-only; pair with set_xref_path or repath_all_xrefs to fix them.", "xrefs",
        Intent = new[] { "znajdz brakujace xrefy", "ktore referencje sa nierozwiazane", "find missing xrefs",
                         "which xrefs are not found", "broken external references", "zepsute sciezki podkladow" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<XrefMissingResult> FindMissingXrefs(IPluginGateway gw, EmptyXrefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<EmptyXrefArgs, XrefMissingResult>(gw, "acad.xrefs.find_missing_xrefs", args, T_NORMAL, ct);

    [McpTool("list_xref_dependent_symbols", "List the layers, linetypes, text styles, dimension styles and blocks that arrive in this drawing through one XREF. These are the names that get renamed on bind - check here before binding to predict collisions.", "xrefs",
        Intent = new[] { "wylistuj warstwy z xrefa", "jakie style przychodza z referencji", "list xref dependent symbols",
                         "what layers does this xref bring", "xref symbol table entries", "symbole zalezne od xrefa" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<XrefSymbolsResult> ListXrefDependentSymbols(IPluginGateway gw, XrefRefArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefRefArgs, XrefSymbolsResult>(gw, "acad.xrefs.list_xref_dependent_symbols", args, T_NORMAL, ct);

    // ─────────────────────── paths ───────────────────────

    [McpTool("set_xref_path", "Point one XREF at a different file. relativePath=true stores it relative to this drawing, which is what survives moving the project folder. Reloads by default so the result is immediately verifiable.", "xrefs",
        Intent = new[] { "zmien sciezke xrefa", "napraw sciezke referencji", "set xref path",
                         "repath external reference", "point xref at another file", "przekieruj podklad" },
        RequiresPlugin = true)]
    public static Task<XrefInfoResult> SetXrefPath(IPluginGateway gw, SetXrefPathArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<SetXrefPathArgs, XrefInfoResult>(gw, "acad.xrefs.set_xref_path", args, T_SLOW, ct);

    [McpTool("repath_all_xrefs", "Bulk path repair: replace oldPrefix with newPrefix in every XREF path. dryRun=true reports what would change, including whether each new path actually resolves, without writing anything. Run the dry run first.", "xrefs",
        Intent = new[] { "napraw wszystkie sciezki xrefow", "zmien katalog referencji hurtowo", "repath all xrefs",
                         "bulk fix xref paths", "move xref folder", "podmien prefiks sciezek podkladow" },
        RequiresPlugin = true)]
    public static Task<XrefRepathResult> RepathAllXrefs(IPluginGateway gw, RepathAllArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<RepathAllArgs, XrefRepathResult>(gw, "acad.xrefs.repath_all_xrefs", args, T_SLOW, ct);

    // ─────────────────────── clipping ───────────────────────

    [McpTool("clip_xref_rect", "Clip one XREF insert to a rectangle given by two opposite corners in WCS. inverted=true hides what is inside the rectangle instead of outside. Clipping is per-insert, so pass a handle from get_xref_info, not a block name.", "xrefs",
        Intent = new[] { "przytnij xref prostokatem", "obetnij referencje do obszaru", "clip xref rectangle",
                         "crop external reference", "limit xref to window", "ogranicz widoczny fragment podkladu" },
        RequiresPlugin = true)]
    public static Task<XrefClipResult> ClipXrefRect(IPluginGateway gw, ClipXrefRectArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<ClipXrefRectArgs, XrefClipResult>(gw, "acad.xrefs.clip_xref_rect", args, T_NORMAL, ct);

    [McpTool("clip_xref_polygonal", "Clip one XREF insert to an arbitrary closed polygon given as an ordered vertex list in WCS. Needs at least 3 vertices; the polygon is closed automatically.", "xrefs",
        Intent = new[] { "przytnij xref wielokatem", "obetnij referencje po obrysie", "clip xref polygon",
                         "polygonal xref clip", "crop reference to shape", "przytnij podklad do konturu" },
        RequiresPlugin = true)]
    public static Task<XrefClipResult> ClipXrefPolygonal(IPluginGateway gw, ClipXrefPolyArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<ClipXrefPolyArgs, XrefClipResult>(gw, "acad.xrefs.clip_xref_polygonal", args, T_NORMAL, ct);

    [McpTool("invert_xref_clip", "Flip an existing clip boundary inside-out: what was hidden becomes visible and the reverse. Errors if the insert has no clip.", "xrefs",
        Intent = new[] { "odwroc przyciecie xrefa", "zamien wnetrze z zewnetrzem obciecia", "invert xref clip",
                         "flip clip boundary", "show outside instead of inside", "odwroc obszar podkladu" },
        RequiresPlugin = true)]
    public static Task<XrefClipResult> InvertXrefClip(IPluginGateway gw, XrefHandleArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefHandleArgs, XrefClipResult>(gw, "acad.xrefs.invert_xref_clip", args, T_NORMAL, ct);

    [McpTool("delete_xref_clip", "Remove the clip boundary from an XREF insert so the whole reference displays again. Succeeds quietly when there was no clip.", "xrefs",
        Intent = new[] { "usun przyciecie xrefa", "pokaz caly xref", "delete xref clip",
                         "remove clip boundary", "show whole reference", "odblokuj caly podklad" },
        RequiresPlugin = true)]
    public static Task<XrefClipResult> DeleteXrefClip(IPluginGateway gw, XrefHandleArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefHandleArgs, XrefClipResult>(gw, "acad.xrefs.delete_xref_clip", args, T_NORMAL, ct);

    [McpTool("set_xref_clip_display", "Show or hide the clip boundary frame itself, without changing what the clip hides. Frames are useful while laying out and wrong on an issued sheet.", "xrefs",
        Intent = new[] { "pokaz ramke przyciecia", "ukryj obrys obciecia xrefa", "set xref clip frame visibility",
                         "show clip boundary", "hide clipping frame", "widocznosc ramki podkladu" },
        RequiresPlugin = true)]
    public static Task<XrefClipResult> SetXrefClipDisplay(IPluginGateway gw, SetClipDisplayArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<SetClipDisplayArgs, XrefClipResult>(gw, "acad.xrefs.set_xref_clip_display", args, T_NORMAL, ct);

    // ─────────────────────── layer overrides ───────────────────────

    [McpTool("set_xref_layer_override", "Override colour, linetype, lineweight, on/off or frozen state for one layer coming from an XREF, without touching the source drawing. This is how a background reference is greyed back on a plan.", "xrefs",
        Intent = new[] { "nadpisz warstwe z xrefa", "wyszarz podklad", "override xref layer",
                         "grey out reference layer", "change xref layer colour", "zmien kolor warstwy referencji" },
        RequiresPlugin = true)]
    public static Task<XrefAffected> SetXrefLayerOverride(IPluginGateway gw, XrefLayerOverrideArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefLayerOverrideArgs, XrefAffected>(gw, "acad.xrefs.set_xref_layer_override", args, T_NORMAL, ct);

    [McpTool("reset_xref_layer_overrides", "Drop layer overrides for one XREF, returning its layers to the properties defined in the source drawing. Pass a layer name to reset just that one, or omit it to reset all of them.", "xrefs",
        Intent = new[] { "usun nadpisania warstw xrefa", "przywroc oryginalne wlasciwosci podkladu", "reset xref layer overrides",
                         "clear reference layer overrides", "restore xref layer properties", "cofnij wyszarzenie" },
        RequiresPlugin = true)]
    public static Task<XrefAffected> ResetXrefLayerOverrides(IPluginGateway gw, XrefLayerResetArgs args, CancellationToken ct)
        => XrefsProxy.CallAsync<XrefLayerResetArgs, XrefAffected>(gw, "acad.xrefs.reset_xref_layer_overrides", args, T_NORMAL, ct);
}
