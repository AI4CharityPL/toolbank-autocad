// AutoCAD acad-boolean-ops category. 8 tools covering Solid3d & Region boolean
// operations, region creation, intersection probing and disjoint-solid separation.
// Each method is a thin proxy through IPluginGateway to "acad.booleanops.<verb>".
//
// Rules: 19-tool-implementation-pattern.mdc, 20..25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.BooleanOps;

public static class BooleanOpsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("union_solids", "Boolean union: merge one or more tool 3D solids into the target solid. Tool solids are erased by default.", "boolean-ops",
        Intent = new[] { "scal bryly", "suma boolowska bryl", "boolean union solids", "merge solids", "combine 3d solids" },
        RequiresPlugin = true)]
    public static Task<BooleanResult> UnionSolids(IPluginGateway gw, SolidBooleanArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<SolidBooleanArgs, BooleanResult>(gw, "acad.booleanops.union_solids", args, T_SLOW, ct);

    [McpTool("subtract_solids", "Boolean subtract: remove every tool 3D solid from the target solid. Tool solids are erased by default.", "boolean-ops",
        Intent = new[] { "odejmij bryle", "roznica bool bryl", "boolean subtract solids", "subtract solids", "carve solid" },
        RequiresPlugin = true)]
    public static Task<BooleanResult> SubtractSolids(IPluginGateway gw, SolidBooleanArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<SolidBooleanArgs, BooleanResult>(gw, "acad.booleanops.subtract_solids", args, T_SLOW, ct);

    [McpTool("intersect_solids", "Boolean intersect: replace target with the common volume of target and every tool 3D solid.", "boolean-ops",
        Intent = new[] { "czesc wspolna bryl", "iloczyn bool bryl", "boolean intersect solids", "intersect solids", "common volume of solids" },
        RequiresPlugin = true)]
    public static Task<BooleanResult> IntersectSolids(IPluginGateway gw, SolidBooleanArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<SolidBooleanArgs, BooleanResult>(gw, "acad.booleanops.intersect_solids", args, T_SLOW, ct);

    [McpTool("union_regions", "Boolean union of 2D regions (target + tools).", "boolean-ops",
        Intent = new[] { "scal regiony", "suma regionow", "boolean union regions", "merge 2d regions", "combine regions" },
        RequiresPlugin = true)]
    public static Task<BooleanResult> UnionRegions(IPluginGateway gw, RegionBooleanArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<RegionBooleanArgs, BooleanResult>(gw, "acad.booleanops.union_regions", args, T_NORMAL, ct);

    [McpTool("subtract_regions", "Boolean subtract for 2D regions (target − tools).", "boolean-ops",
        Intent = new[] { "odejmij regiony", "roznica regionow", "boolean subtract regions", "subtract 2d regions", "punch hole in region" },
        RequiresPlugin = true)]
    public static Task<BooleanResult> SubtractRegions(IPluginGateway gw, RegionBooleanArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<RegionBooleanArgs, BooleanResult>(gw, "acad.booleanops.subtract_regions", args, T_NORMAL, ct);

    [McpTool("intersect_regions", "Boolean intersect for 2D regions (common area of target and tools).", "boolean-ops",
        Intent = new[] { "czesc wspolna regionow", "iloczyn regionow", "boolean intersect regions", "intersect 2d regions", "common area of regions" },
        RequiresPlugin = true)]
    public static Task<BooleanResult> IntersectRegions(IPluginGateway gw, RegionBooleanArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<RegionBooleanArgs, BooleanResult>(gw, "acad.booleanops.intersect_regions", args, T_NORMAL, ct);

    [McpTool("create_region", "Build one or more 2D Region entities from closed planar boundary curves. Returns the list of created regions.", "boolean-ops",
        Intent = new[] { "stworz region", "konwertuj polilinie na region", "create region from curves", "make region", "convert curves to region" },
        RequiresPlugin = true)]
    public static Task<EntitiesResultBool> CreateRegion(IPluginGateway gw, CreateRegionArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<CreateRegionArgs, EntitiesResultBool>(gw, "acad.booleanops.create_region", args, T_NORMAL, ct);

    [McpTool("check_intersection", "Check whether two entities (solids, regions or curves) intersect, and report a coarse spatial relation tag.", "boolean-ops",
        Intent = new[] { "czy bryly sie przecinaja", "sprawdz czy obiekty sie przecinaja", "check entity intersection", "do entities overlap", "test 3d intersection" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<IntersectCheckResult> CheckIntersection(IPluginGateway gw, CheckIntersectArgs args, CancellationToken ct)
        => BooleanOpsProxy.CallAsync<CheckIntersectArgs, IntersectCheckResult>(gw, "acad.booleanops.check_intersection", args, T_NORMAL, ct);
}
