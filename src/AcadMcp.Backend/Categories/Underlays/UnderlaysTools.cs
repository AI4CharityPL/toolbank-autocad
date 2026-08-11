// MCP tool surface for the acad-underlays category (roadmap 3.5, underlay half).
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results), 26 (traps).
//
// MEASURED against the compiler before anything here was written: DgnReference and DwfReference
// both derive from a common UnderlayReference base (DgnDefinition/DwfDefinition from
// UnderlayDefinition), which is why detach/list/clip/adjust are one implementation each rather
// than two. Confirmed present: Position, Rotation, ScaleFactors (a Scale3d), Contrast (int),
// Fade (int), Monochrome (bool), AdjustColorForBackground (bool), GetClipBoundary/SetClipBoundary
// (Point2d[], not Point2dCollection - a different shape from RasterImage's clip API), IsClipped,
// Width, Height. UnderlayDefinition.Load takes a REQUIRED password argument (empty string for
// none unprotected).
//
// Confirmed ABSENT after 7 tried names: any per-layer visibility control (GetLayers, SetLayer,
// IsLayerVisible, UnderlayLayers, SubItems, LayerNames all fail to compile) and Bind(). So
// list_underlay_layers, set_underlay_layer_visibility and bind_underlay are not in this tranche -
// not a guess, a measurement. set_underlay_contrast and set_underlay_monochrome from the original
// roadmap list collapse into one set_underlay_adjust, the same consolidation already applied to
// acad-images' set_image_adjust and acad-materials' modify_material.
//
// PDF underlays are not in this tranche either - no PDF sample file was available to verify
// against, and PdfReference/PdfDefinition are a third pair this tranche never touched.
//
// MEASURED live: UnderlayReference.IsClipped reads TRUE on a freshly attached, never-clipped
// entity, and stays true after SetClipBoundary is called with an empty array - it is not the
// "has a custom clip been applied" flag RasterImage's IsClipped is. `clipped` in every result
// here is GetClipBoundary().Length > 0 instead, which correctly reads empty until a real
// boundary is set. Now rule 26 section 23.
//
// attach_dgn_underlay is WITHHELD, not because it fails but because it cannot be PROVEN: every
// .dgn on this machine (10 files, all under UserDataCache\Template) is a "Seed" file - an empty
// starting template for EXPORTING a new DGN, not real content - and every one refuses to load
// with eInvalidInput regardless of itemName. attach_dwf_underlay uses the IDENTICAL generic code
// path and works cleanly against a real DWF (AutoCAD's own Sheet Sets sample), which is strong
// evidence the DGN implementation is correct and only the available files are unsuitable - the
// same "blocked on a file" shape as 4.5 point clouds, not an API defect. The plugin handler stays
// registered; unblock with one real (non-seed) .dgn.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Underlays;

public static class UnderlaysTools
{
    private const int T_NORMAL = 15_000;
    private const int T_READ = 5_000;

    [McpTool("attach_dwf_underlay", "Attach a DWF or DWFx file as an underlay reference. itemName selects which sheet inside the DWF to show - a DWF can hold several; omit it to use the file's default. Refuses a missing file. A name already in use is refused UNLESS it points at the exact same file and item, in which case this is a second placement sharing the existing definition (reusedDefinition: true) - same behaviour as acad-images.attach_image.", "underlays",
        Intent = new[] { "attach a dwf underlay", "insert a dwf file",
                         "dolacz podklad dwf", "attach a dwf reference",
                         "wstaw plik dwf jako podklad", "reference a dwf sheet",
                         "underlay a dwf file" },
        RequiresPlugin = true)]
    public static Task<UnderlayAttachResult> AttachDwfUnderlay(IPluginGateway gw, UnderlayAttachArgs args, CancellationToken ct)
        => UnderlaysProxy.CallAsync<UnderlayAttachArgs, UnderlayAttachResult>(gw, "acad.underlays.attach_dwf_underlay", args, T_NORMAL, ct);

    [McpTool("list_underlays", "List DGN and DWF underlay references attached to the drawing: kind (dgn/dwf), source path, item name, insertion point, rotation, scale, whether clipped, and the contrast/fade/monochrome adjustment. Read-only.", "underlays",
        Intent = new[] { "list the underlays", "what underlays are in this drawing",
                         "lista podkladow", "show attached dgn and dwf references",
                         "jakie podklady sa dolaczone", "find an underlay by name",
                         "which underlay references exist" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<UnderlayListResult> ListUnderlays(IPluginGateway gw, UnderlaysNoArgs args, CancellationToken ct)
        => UnderlaysProxy.CallAsync<UnderlaysNoArgs, UnderlayListResult>(gw, "acad.underlays.list_underlays", args, T_READ, ct);

    [McpTool("detach_underlay", "Remove one underlay reference (DGN or DWF) from the drawing. If no other reference still uses the same source definition, the definition is removed too and defRemoved is true; if another placement of the same file/item remains, only this entity goes.", "underlays",
        Intent = new[] { "detach this underlay", "remove an underlay from the drawing",
                         "usun podklad z rysunku", "delete a dgn or dwf reference",
                         "odlacz podklad od rysunku", "remove a placed underlay",
                         "get rid of an underlay" },
        RequiresPlugin = true)]
    public static Task<UnderlayDetachResult> DetachUnderlay(IPluginGateway gw, UnderlayHandleArgs args, CancellationToken ct)
        => UnderlaysProxy.CallAsync<UnderlayHandleArgs, UnderlayDetachResult>(gw, "acad.underlays.detach_underlay", args, T_NORMAL, ct);

    [McpTool("clip_underlay", "Clip an underlay to a boundary given in the underlay's OWN local coordinates - (0,0) to (underlayWidth, underlayHeight) as reported by list_underlays or this tool's own result BEFORE scale is applied, NOT drawing (WCS) coordinates. Exactly two points clip to the rectangle between them; three or more clip to that polygon. Omitting points removes the clip. Reports the entity's drawing-space extents before and after, so a clip that changed nothing shows up as unchanged extents.", "underlays",
        Intent = new[] { "clip this underlay", "crop the dgn reference",
                         "przytnij podklad", "clip the dwf to a rectangle",
                         "obetnij podklad do wielokata", "remove the underlay clip",
                         "unclip this underlay" },
        RequiresPlugin = true)]
    public static Task<UnderlayClipResult> ClipUnderlay(IPluginGateway gw, UnderlayClipArgs args, CancellationToken ct)
        => UnderlaysProxy.CallAsync<UnderlayClipArgs, UnderlayClipResult>(gw, "acad.underlays.clip_underlay", args, T_NORMAL, ct);

    [McpTool("set_underlay_adjust", "Set an underlay's contrast, fade and/or monochrome display. Only the ones given are changed; the others are read and reported unchanged. Collapses the roadmap's separate set_underlay_contrast/set_underlay_monochrome into one tool, matching acad-images.set_image_adjust and acad-materials.modify_material.", "underlays",
        Intent = new[] { "adjust underlay contrast", "make this underlay monochrome",
                         "zmien kontrast podkladu", "fade this underlay",
                         "ustaw podklad na monochromatyczny", "turn off underlay colour",
                         "increase underlay fade" },
        RequiresPlugin = true)]
    public static Task<UnderlayAdjustResult> SetUnderlayAdjust(IPluginGateway gw, UnderlayAdjustArgs args, CancellationToken ct)
        => UnderlaysProxy.CallAsync<UnderlayAdjustArgs, UnderlayAdjustResult>(gw, "acad.underlays.set_underlay_adjust", args, T_NORMAL, ct);
}
