// AutoCAD acad-hatches category. 8 tools covering professional hatching:
// boundary-handle fills, seed-point auto-boundary fills, ISO/PN-EN pattern listing,
// named material presets (concrete/brick/insulation/stone/plaster/lead-shield/faraday/...),
// hatch clipping, post-edit regeneration and enumeration.
//
// Thin proxies over plugin handlers "acad.hatches.<verb>".
//
// Rules: 19-tool-implementation-pattern, 20..25 (tool authoring), 62-hatching-policy.
//
// Timeouts:
//   read-only queries       :  5 000 ms (list_patterns, list_hatches)
//   single hatch creation   : 15 000 ms (draw_hatch, apply_material_preset, clip_hatch)
//   seed-point boundary     : 30 000 ms (draw_hatch_by_boundary, apply_material_preset_by_point)
//   regenerate_all          : 30 000 ms

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Hatches;

public static class HatchesTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("draw_hatch",
        "Fill one or more closed boundaries (identified by handle) with a named hatch pattern. Supports pattern/scale/angle, layer override, foreground+background colors, associative/annotative modes. Preferred over geometry-2d draw_hatch when you need color or background.",
        "hatches",
        Intent = new[]
        {
            "wypelnij obszar kreskowaniem", "dodaj hatch", "narysuj kreskowanie na granicy",
            "apply hatch to boundary", "fill polyline with pattern", "fill area with ANSI31",
            "create hatch on handles", "add cross hatch", "nanies wzor wypelnienia"
        },
        RequiresPlugin = true)]
    public static Task<HatchEntityResult> DrawHatch(IPluginGateway gw, DrawHatchArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<DrawHatchArgs, HatchEntityResult>(gw, "acad.hatches.draw_hatch", args, T_NORMAL, ct);

    [McpTool("draw_hatch_by_boundary",
        "Fill the closed region enclosing the given seed point, auto-detecting the boundary using AutoCAD's TraceBoundary (with optional island detection). Ideal when you only know a point inside a room rather than its edges.",
        "hatches",
        Intent = new[]
        {
            "wypelnij pomieszczenie przez punkt", "autodetect boundary and hatch",
            "hatch by pick point", "fill region containing point", "wypelnij obszar wokol punktu",
            "auto boundary hatch", "flood fill with pattern"
        },
        RequiresPlugin = true)]
    public static Task<HatchEntityResult> DrawHatchByBoundary(IPluginGateway gw, DrawHatchByBoundaryArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<DrawHatchByBoundaryArgs, HatchEntityResult>(gw, "acad.hatches.draw_hatch_by_boundary", args, T_SLOW, ct);

    [McpTool("list_patterns",
        "Enumerate available hatch patterns with their category (ANSI, ISO, AR-architectural, PN-EN) and recommended default scale/angle. Read-only. Use to discover what patterns are installed before drawing.",
        "hatches",
        Intent = new[]
        {
            "lista wzorow kreskowania", "jakie patterny sa dostepne", "list hatch patterns",
            "show available hatches", "enumerate hatch patterns", "katalog wzorow"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListPatternsResult> ListPatterns(IPluginGateway gw, ListPatternsArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<ListPatternsArgs, ListPatternsResult>(gw, "acad.hatches.list_patterns", args, T_FAST, ct);

    [McpTool("apply_material_preset",
        "Apply a named material preset (e.g. 'concrete', 'brick', 'insulation', 'plaster', 'stone', 'steel', 'glass', 'wood-cross', 'wood-grain', 'lead-shield', 'faraday', 'earth', 'tile', 'reinforced-concrete') to the supplied boundary handles. Each preset maps material -> (pattern, scale, angle, color) per rule 62-hatching-policy.",
        "hatches",
        Intent = new[]
        {
            "kreskowanie betonem", "hatch z presetem cegly", "zastosuj wzor izolacji",
            "apply concrete hatch", "brick material preset", "insulation pattern on boundary",
            "material kamien na scianie", "fill with plaster preset"
        },
        RequiresPlugin = true)]
    public static Task<HatchEntityResult> ApplyMaterialPreset(IPluginGateway gw, ApplyMaterialPresetArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<ApplyMaterialPresetArgs, HatchEntityResult>(gw, "acad.hatches.apply_material_preset", args, T_NORMAL, ct);

    [McpTool("apply_material_preset_by_point",
        "Same as apply_material_preset but takes a seed point instead of boundary handles. Auto-detects enclosing boundary, then applies the material preset. Preferred for batch populating walls/floors once the geometry is in place.",
        "hatches",
        Intent = new[]
        {
            "wypelnij pokoj materialem przez punkt", "apply material by seed point",
            "auto material fill", "flood fill with material preset", "hatch material przez pick"
        },
        RequiresPlugin = true)]
    public static Task<HatchEntityResult> ApplyMaterialPresetByPoint(IPluginGateway gw, ApplyMaterialPresetByPointArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<ApplyMaterialPresetByPointArgs, HatchEntityResult>(gw, "acad.hatches.apply_material_preset_by_point", args, T_SLOW, ct);

    [McpTool("clip_hatch",
        "Replace the boundary of an existing hatch with a new set of closed polyline/region handles. Use after editing geometry so the hatch follows the new edges. Returns the re-evaluated hatch.",
        "hatches",
        Intent = new[]
        {
            "zmien granice hatchu", "replace hatch boundary", "update hatch loops",
            "clip hatch to new polylines", "rebind hatch to new edges"
        },
        RequiresPlugin = true)]
    public static Task<HatchEntityResult> ClipHatch(IPluginGateway gw, ClipHatchArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<ClipHatchArgs, HatchEntityResult>(gw, "acad.hatches.clip_hatch", args, T_NORMAL, ct);

    [McpTool("regenerate_hatches",
        "Re-evaluate one or more associative hatches after their boundaries have been edited. Scope: explicit handles, layer filter, or entire model-space. Returns the count of successfully regenerated hatches plus a list of handles that failed (e.g. open boundaries).",
        "hatches",
        Intent = new[]
        {
            "odswiez kreskowanie", "regenerate hatches", "re-evaluate hatch boundaries",
            "fix hatch after wall move", "update hatches in layer", "recompute associative fills"
        },
        RequiresPlugin = true)]
    public static Task<RegenerateHatchesResult> RegenerateHatches(IPluginGateway gw, RegenerateHatchesArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<RegenerateHatchesArgs, RegenerateHatchesResult>(gw, "acad.hatches.regenerate_hatches", args, T_SLOW, ct);

    [McpTool("list_hatches",
        "Enumerate all hatch entities currently in model-space (optionally filtered by layer and/or pattern). Returns handle, layer, pattern, scale, angle, area, loop-count, associativity for each. Read-only.",
        "hatches",
        Intent = new[]
        {
            "lista wszystkich hatchy", "enumerate hatches", "list existing fills",
            "find hatches on layer", "show hatches with pattern ANSI31"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListHatchesResult> ListHatches(IPluginGateway gw, ListHatchesArgs args, CancellationToken ct)
        => HatchesProxy.CallAsync<ListHatchesArgs, ListHatchesResult>(gw, "acad.hatches.list_hatches", args, T_FAST, ct);
}
