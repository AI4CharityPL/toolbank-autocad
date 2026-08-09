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

    // ─────────── roadmap 4.1: the rest of how a solid is made from a curve ───────────

    [McpTool("sweep_curve", "Sweep a closed profile along a path curve to make a 3D solid - AutoCAD's SWEEP. extrude_curve pushes a profile in a straight line; this carries it along a line, arc, polyline, spline or helix, which is how a pipe, a handrail or a moulding is modelled. align='path' keeps the profile square to the path as it turns, which is almost always what you want. The result reports the profile area, the path length and their product, so the volume can be checked against arithmetic: a profile carried square along a STRAIGHT path encloses exactly area times length, and the two diverge as the path bends.", "geometry-3d",
        Intent = new[] { "przeciagnij profil po sciezce", "zrob rure wzdluz linii",
                         "sweep a profile along a path", "make a pipe from a profile",
                         "porecz wzdluz krzywej", "sweep solid" },
        RequiresPlugin = true)]
    public static Task<SweepResult> SweepCurve(IPluginGateway gw, SweepArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<SweepArgs, SweepResult>(gw, "acad.geometry3d.sweep_curve", args, T_SLOW, ct);

    [McpTool("loft_curves", "Run a solid skin between two or more cross sections - AutoCAD's LOFT. Optionally follow guide curves OR a path, which are alternatives and cannot both be given. ruled=true joins the sections with straight sides instead of a smooth skin; closed=true runs the skin back from the last section to the first. The result lists each section's area, so the volume can be checked against them: two equal sections a distance apart make a prism of area times distance, and a taper makes less.", "geometry-3d",
        Intent = new[] { "przejscie miedzy przekrojami", "polacz przekroje bryla",
                         "loft between cross sections", "make a transition piece",
                         "wyciagniecie przez przekroje", "loft solid" },
        RequiresPlugin = true)]
    public static Task<LoftResult> LoftCurves(IPluginGateway gw, LoftArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<LoftArgs, LoftResult>(gw, "acad.geometry3d.loft_curves", args, T_SLOW, ct);

    [McpTool("draw_helix", "Draw a helix - the usual path for a spring, a thread or a spiral stair, and the natural companion to sweep_curve. Give the base centre, a base radius, the height and the number of turns; topRadius makes it taper, and height 0 makes a flat spiral. The result carries expectedLength for a constant-radius helix, worked from the unrolled right triangle - the circumference walked against the height climbed - so the curve can be checked against arithmetic rather than against a second opinion from the same code.", "geometry-3d",
        Intent = new[] { "narysuj helise", "sprezyna spirala", "draw a helix",
                         "spiral path for a spring", "gwint sciezka", "spiral stair path" },
        RequiresPlugin = true)]
    public static Task<HelixResult> DrawHelix(IPluginGateway gw, HelixArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<HelixArgs, HelixResult>(gw, "acad.geometry3d.draw_helix", args, T_NORMAL, ct);

    // ─────────── roadmap 4.1: cutting a solid, and finding where two overlap ───────────

    [McpTool("slice_solid", "Cut a 3D solid with a plane - AutoCAD's SLICE. Give a point the plane passes through and the direction it faces; the half the normal points TOWARDS is the one kept, and keepBoth returns the other as a second solid instead of discarding it. Cutting conserves volume, so with keepBoth the two halves are checked to add back up to what went in and a cut that lost or duplicated material is reported as a failure - it would otherwise leave two perfectly good-looking solids.", "geometry-3d",
        Intent = new[] { "przetnij bryle plaszczyzna", "podziel solid na pol",
                         "slice a solid with a plane", "cut a 3d solid in half",
                         "przekroj bryly", "split solid by plane" },
        RequiresPlugin = true)]
    public static Task<SliceSolidResult> SliceSolid(IPluginGateway gw, SliceSolidArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<SliceSolidArgs, SliceSolidResult>(gw, "acad.geometry3d.slice_solid", args, T_SLOW, ct);

    [McpTool("interfere_solids", "Find where two 3D solids clash, and hand the overlap back as a THIRD solid - AutoCAD's INTERFERE. Both originals are left untouched, which is the difference from boolean_ops.intersect_solids: that one replaces the target with the common volume, so it answers the question by destroying the thing you asked about. This is the services-coordination check. Pass createSolid false to get only the yes/no and the volumes. Both solids are measured before and after and any change to either is reported as a failure.", "geometry-3d",
        Intent = new[] { "kolizja miedzy bryłami", "sprawdz przenikanie solidow",
                         "check clash between solids", "interfere solids",
                         "koordynacja miedzybranzowa kolizje", "find overlap volume" },
        RequiresPlugin = true)]
    public static Task<InterfereResult> InterfereSolids(IPluginGateway gw, InterfereArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<InterfereArgs, InterfereResult>(gw, "acad.geometry3d.interfere_solids", args, T_SLOW, ct);

    [McpTool("imprint_edges", "Press a curve that LIES ON a face of a 3D solid into that face, splitting it into separate faces - AutoCAD's IMPRINT. This adds edges, not material: it is how you mark out a bolt pattern, a recess outline or a weld line before pushing or pulling it, and how you get a face you can select on its own. The curve has to sit on a face; one floating above it or crossing into the interior has nothing to be pressed into and is refused. The face and edge counts are reported before and after, and so is the volume, which must not change - a tool that cut instead of imprinting would also report more faces, and only the volume would give it away. Use boolean_ops.subtract_solids to actually remove material.", "geometry-3d",
        Intent = new[] { "odcisnij krzywa na bryle", "podziel sciane bryly linia",
                         "imprint curve onto solid", "imprint edges on a face",
                         "zaznacz obrys na scianie bryly", "split a face of a solid",
                         "mark bolt pattern on a face" },
        RequiresPlugin = true)]
    public static Task<ImprintResult> ImprintEdges(IPluginGateway gw, ImprintArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<ImprintArgs, ImprintResult>(gw, "acad.geometry3d.imprint_edges", args, T_SLOW, ct);

    [McpTool("list_solid_edges", "List every edge of a 3D solid with an INDEX, its endpoints, its midpoint and its length. Call this before fillet_edge or chamfer_edge: those need to be told which edges to work on, and an edge cannot be named any other way - AutoCAD identifies it by an internal handle that does not survive a round trip. The geometry comes back with each index so the choice can be checked against the drawing rather than trusted. Indexes are stable only while the solid is unedited: rounding one edge rebuilds the boundary and renumbers the rest, so list again after every edit.", "geometry-3d",
        Intent = new[] { "wypisz krawedzie bryly", "jakie krawedzie ma ta bryla",
                         "list edges of a solid", "show solid edges with indexes",
                         "ktora krawedz zaokraglic", "which edge do I fillet",
                         "enumerate solid edges" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SolidEdgesResult> ListSolidEdges(IPluginGateway gw, SolidQueryArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<SolidQueryArgs, SolidEdgesResult>(gw, "acad.geometry3d.list_solid_edges", args, T_NORMAL, ct);

    [McpTool("list_solid_faces", "List every face of a 3D solid with an INDEX, the centroid of its boundary, its outward normal and how many edges it has. This is how a face is named for chamfer_edge's baseFaceIndex, and it is the readable answer to 'what does this solid consist of'. The centroid LOCATES a face - on a face with a hole in it the centroid can fall inside the hole - so use the normal to tell top from bottom rather than the position alone. Indexes are stable only while the solid is unedited.", "geometry-3d",
        Intent = new[] { "wypisz sciany bryly", "ile scian ma bryla",
                         "list faces of a solid", "show solid faces with normals",
                         "ktora sciana jest gorna", "which face is the top one",
                         "enumerate solid faces" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SolidFacesResult> ListSolidFaces(IPluginGateway gw, SolidQueryArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<SolidQueryArgs, SolidFacesResult>(gw, "acad.geometry3d.list_solid_faces", args, T_NORMAL, ct);

    [McpTool("fillet_edge", "Round one or more edges of a 3D solid to a given radius - AutoCAD's FILLETEDGE. Name the edges either as edgeIndexes from list_solid_edges or as nearPoints, each snapping to the edge closest to it; a point equidistant from two edges is refused rather than snapped to whichever sorted first. Rounding a convex edge removes material and replaces the edge with a curved face, and the amount removed on a straight edge of length L is exactly L*r*r*(1-pi/4), so the result is checkable on paper. A radius large enough to DESTROY a face is refused and the solid is left exactly as it was: AutoCAD itself accepts an oversized radius without complaint - asked for 300 on a 100 face it swallows a whole face and returns success - so the refusal reports the largest radius that does fit, found by bisection. Pass allowFaceLoss to reshape the part deliberately. The tool also refuses to report success when the face count and the volume both came back unchanged. This is the 3D operation; geometry_2d.fillet_corner is the flat one.", "geometry-3d",
        Intent = new[] { "zaokraglij krawedz bryly", "filet na krawedzi 3d",
                         "fillet an edge of a solid", "round the edges of a 3d box",
                         "zaokraglenie naroza bryly", "filletedge", "round solid edge" },
        RequiresPlugin = true)]
    public static Task<FilletEdgeResult> FilletEdge(IPluginGateway gw, EdgeOpArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<EdgeOpArgs, FilletEdgeResult>(gw, "acad.geometry3d.fillet_edge", args, T_SLOW, ct);

    [McpTool("chamfer_edge", "Cut a flat bevel along one or more edges of a 3D solid - AutoCAD's CHAMFEREDGE. Name the edges as edgeIndexes from list_solid_edges or as nearPoints. The two distances are measured from the edge across each of the two faces meeting there; with equal distances the bevel is symmetric and no more is needed, but if they differ you must give baseFaceIndex, because which face the first distance belongs to decides which way the bevel leans - guessing it would silently produce the mirror image. On a straight edge of length L with equal distances d the amount removed is exactly L*d*d/2, which is more than a fillet of radius d takes. A distance large enough to destroy a face is refused the same way fillet_edge refuses one, with the largest distance that fits reported back and allowFaceLoss to override. Use fillet_edge to round instead of bevel.", "geometry-3d",
        Intent = new[] { "sfazuj krawedz bryly", "faza na krawedzi 3d",
                         "chamfer an edge of a solid", "bevel the edge of a 3d box",
                         "sciecie naroza bryly", "chamferedge", "bevel solid edge" },
        RequiresPlugin = true)]
    public static Task<ChamferEdgeResult> ChamferEdge(IPluginGateway gw, EdgeOpArgs args, CancellationToken ct)
        => Geometry3dProxy.CallAsync<EdgeOpArgs, ChamferEdgeResult>(gw, "acad.geometry3d.chamfer_edge", args, T_SLOW, ct);
}
