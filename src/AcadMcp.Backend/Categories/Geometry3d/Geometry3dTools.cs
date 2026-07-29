// AutoCAD acad-geometry-3d category. 15 tools covering 3D primitive solids,
// extrude/revolve, planar surfaces and mass-property queries. Each method is a
// thin proxy through IPluginGateway to the matching plugin handler under the key
// "acad.geometry3d.<verb>".
//
// Rules: 19-tool-implementation-pattern.md, 20..25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Geometry3d;

public static class Geometry3dTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    // ───────────────────────── primitives ─────────────────────────

    [McpTool("draw_box", "Create a 3D solid box defined by two opposite corner points (axis-aligned in WCS).", "geometry-3d",
        Intent = new[] { "narysuj prostopadloscian", "stworz bryle prostopadloscienna", "draw 3d box", "create box solid", "box from two corners" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawBox(IPluginGateway gw, DrawBoxArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawBoxArgs, EntityResult3>(gw, "acad.geometry3d.draw_box", args, T_NORMAL, ct);

    [McpTool("draw_sphere", "Create a 3D solid sphere by center point and radius.", "geometry-3d",
        Intent = new[] { "narysuj kule", "stworz sfere", "draw sphere", "create sphere solid", "sphere by center and radius" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawSphere(IPluginGateway gw, DrawSphereArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawSphereArgs, EntityResult3>(gw, "acad.geometry3d.draw_sphere", args, T_NORMAL, ct);

    [McpTool("draw_cylinder", "Create a 3D solid cylinder by base center, radius, and height (Z+).", "geometry-3d",
        Intent = new[] { "narysuj walec", "stworz cylinder", "draw cylinder", "create cylinder solid", "cylinder by base radius and height" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawCylinder(IPluginGateway gw, DrawCylinderArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawCylinderArgs, EntityResult3>(gw, "acad.geometry3d.draw_cylinder", args, T_NORMAL, ct);

    [McpTool("draw_cone", "Create a 3D solid cone or frustum (set topRadius>0 for frustum).", "geometry-3d",
        Intent = new[] { "narysuj stozek", "stworz stozek scinany", "draw cone", "create cone solid", "frustum cone" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawCone(IPluginGateway gw, DrawConeArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawConeArgs, EntityResult3>(gw, "acad.geometry3d.draw_cone", args, T_NORMAL, ct);

    [McpTool("draw_torus", "Create a 3D solid torus by center, major (tube path) radius and minor (tube) radius.", "geometry-3d",
        Intent = new[] { "narysuj torus", "stworz pierscien", "draw torus", "create torus solid", "donut shape solid" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawTorus(IPluginGateway gw, DrawTorusArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawTorusArgs, EntityResult3>(gw, "acad.geometry3d.draw_torus", args, T_NORMAL, ct);

    [McpTool("draw_pyramid", "Create a 3D solid pyramid or frustum with N sides (3..32). Use topRadius>0 for frustum.", "geometry-3d",
        Intent = new[] { "narysuj ostroslup", "stworz piramide", "draw pyramid", "create pyramid solid", "frustum pyramid n sides" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawPyramid(IPluginGateway gw, DrawPyramidArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawPyramidArgs, EntityResult3>(gw, "acad.geometry3d.draw_pyramid", args, T_NORMAL, ct);

    [McpTool("draw_wedge", "Create a 3D solid wedge (right-angle prism) defined by two opposite corners.", "geometry-3d",
        Intent = new[] { "narysuj klin", "stworz pryzme prostokatna", "draw wedge", "create wedge solid", "right angle prism" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawWedge(IPluginGateway gw, DrawWedgeArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<DrawWedgeArgs, EntityResult3>(gw, "acad.geometry3d.draw_wedge", args, T_NORMAL, ct);

    // ───────────────────────── extrude / revolve / surface ─────────────────────────

    [McpTool("extrude_curve", "Extrude a closed planar curve (Polyline / Region / Circle) into a 3D solid by given height with optional taper angle in degrees.", "geometry-3d",
        Intent = new[] { "wyciagnij polilinie", "extruduj krzywa", "extrude polyline", "create solid by extrusion", "extrude with taper" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> ExtrudeCurve(IPluginGateway gw, ExtrudeCurveArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<ExtrudeCurveArgs, EntityResult3>(gw, "acad.geometry3d.extrude_curve", args, T_SLOW, ct);

    [McpTool("revolve_curve", "Revolve a closed planar curve around an arbitrary axis (axisStart, axisEnd) by angle in degrees (default 360).", "geometry-3d",
        Intent = new[] { "obrot krzywej", "revolve polilinie", "revolve curve around axis", "create solid of revolution", "spin curve to make solid" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> RevolveCurve(IPluginGateway gw, RevolveCurveArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<RevolveCurveArgs, EntityResult3>(gw, "acad.geometry3d.revolve_curve", args, T_SLOW, ct);

    [McpTool("draw_planar_surface", "Create a planar surface entity from one or more closed planar boundary curves (handles).", "geometry-3d",
        Intent = new[] { "narysuj powierzchnie plaska", "stworz planar surface", "draw planar surface from boundary", "create planar surface", "make surface from closed curve" },
        RequiresPlugin = true)]
    public static Task<EntityResult3> DrawPlanarSurface(IPluginGateway gw, PlanarSurfaceArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<PlanarSurfaceArgs, EntityResult3>(gw, "acad.geometry3d.draw_planar_surface", args, T_SLOW, ct);

    // ───────────────────────── queries ─────────────────────────

    [McpTool("get_volume", "Return the volume of a 3D solid (single value, current units).", "geometry-3d",
        Intent = new[] { "policz objetosc", "ile wynosi objetosc", "compute solid volume", "get volume of solid", "measure 3d volume" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<VolumeResult> GetVolume(IPluginGateway gw, HandleArg3 args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<HandleArg3, VolumeResult>(gw, "acad.geometry3d.get_volume", args, T_FAST, ct);

    [McpTool("get_surface_area", "Return total surface area of a 3D solid or surface.", "geometry-3d",
        Intent = new[] { "policz pole powierzchni 3d", "powierzchnia bryly", "compute surface area", "get total surface area", "measure 3d area" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<AreaResult3> GetSurfaceArea(IPluginGateway gw, HandleArg3 args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<HandleArg3, AreaResult3>(gw, "acad.geometry3d.get_surface_area", args, T_FAST, ct);

    [McpTool("get_3d_centroid", "Return the centroid (center of mass) of a 3D solid in WCS coordinates.", "geometry-3d",
        Intent = new[] { "srodek ciezkosci", "centroid bryly", "compute solid centroid", "get center of mass 3d", "where is centroid" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<CentroidResult> Get3dCentroid(IPluginGateway gw, HandleArg3 args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<HandleArg3, CentroidResult>(gw, "acad.geometry3d.get_3d_centroid", args, T_FAST, ct);

    [McpTool("get_3d_bounding_box", "Return the axis-aligned 3D bounding box of an entity (min and max points).", "geometry-3d",
        Intent = new[] { "bounding box 3d", "wymiary bryly", "get 3d bounding box", "compute extents 3d", "axis aligned bbox" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<BoundingBox3Result> Get3dBoundingBox(IPluginGateway gw, HandleArg3 args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<HandleArg3, BoundingBox3Result>(gw, "acad.geometry3d.get_3d_bounding_box", args, T_FAST, ct);

    [McpTool("get_mass_properties", "Return full mass properties of a 3D solid: volume, surface area, centroid, principal moments and radii of gyration.", "geometry-3d",
        Intent = new[] { "wlasciwosci masowe", "moment bezwladnosci", "compute mass properties", "get inertia and centroid", "full mass props of solid" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<MassPropertiesResult> GetMassProperties(IPluginGateway gw, HandleArg3 args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<HandleArg3, MassPropertiesResult>(gw, "acad.geometry3d.get_mass_properties", args, T_NORMAL, ct);
}
