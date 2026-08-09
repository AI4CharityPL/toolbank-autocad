// AutoCAD acad-surfaces category. Surfaces are SHELLS with area and no volume, which is the
// whole distinction from acad-geometry-3d: a surface is skin, a solid is material. Every tool
// here reports the area of what it made, because a surface tool that quietly produced nothing
// still hands back a perfectly good handle.
//
// Rules: 19-tool-implementation-pattern.md, 20..25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Surfaces;

public static class SurfacesTools
{
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("extrude_surface", "Sweep a curve in a straight line to make a SURFACE - AutoCAD's SURFEXTRUDE. An open curve gives an open sheet and a closed one gives a tube; either way the result has area and no volume, which is the whole difference from geometry_3d.extrude_curve, the one that turns a closed profile into a solid. Sweeping a curve of length L through a height h makes exactly L*h of surface when the taper is zero, so the answer is checkable on paper, and the tool reports both numbers. Use this for a wall face, a road surface or a terrain strip - anything you need the shape of but not the substance.", "surfaces",
        Intent = new[] { "extrude a curve into a surface", "make a surface by extruding a line",
                         "wyciagnij krzywa w powierzchnie", "surfextrude",
                         "create a sheet from a polyline", "powierzchnia z linii", "extrude to a sheet" },
        RequiresPlugin = true)]
    public static Task<SurfaceResult> ExtrudeSurface(IPluginGateway gw, SurfaceExtrudeArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceExtrudeArgs, SurfaceResult>(gw, "acad.surfaces.extrude_surface", args, T_SLOW, ct);

    [McpTool("revolve_surface", "Spin a curve about an axis to make a SURFACE of revolution - AutoCAD's SURFREVOLVE. Both ends of the axis are required: a point does not name an axis in 3D. The result is a shell, not a solid, so a revolved arc gives a dish rather than a lump; use geometry_3d.revolve_curve when what you want is filled. The area follows Pappus - a curve of length L swept a full turn at distance R covers 2*pi*R*L - and the tool reports that figure beside the measured one, exact for a curve that keeps a constant distance from the axis and an approximation otherwise. A curve crossing its own axis sweeps through itself and is refused.", "surfaces",
        Intent = new[] { "revolve a curve into a surface", "make a surface of revolution",
                         "obroc krzywa w powierzchnie", "surfrevolve",
                         "spin a profile around an axis", "powierzchnia obrotowa", "lathe a shell" },
        RequiresPlugin = true)]
    public static Task<SurfaceResult> RevolveSurface(IPluginGateway gw, SurfaceRevolveArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceRevolveArgs, SurfaceResult>(gw, "acad.surfaces.revolve_surface", args, T_SLOW, ct);

    [McpTool("sweep_surface", "Sweep a profile curve along a path curve to make a SURFACE - AutoCAD's SURFSWEEP. The profile turns to stay square to the path as it goes, which is what a handrail, a gutter or a road edge needs. On a straight path the area is exactly the profile length times the path length; round a bend it differs by what the inside of the turn loses against the outside, so the tool reports both. geometry_3d.sweep_curve is the same idea for a solid, and draw_polysolid the special case of a rectangular wall section.", "surfaces",
        Intent = new[] { "sweep a profile along a path as a surface", "surfsweep",
                         "przeciagnij profil wzdluz sciezki jako powierzchnie",
                         "make a handrail surface", "swept shell along a curve",
                         "powierzchnia przeciagniecia", "sweep a shape down a road" },
        RequiresPlugin = true)]
    public static Task<SurfaceResult> SweepSurface(IPluginGateway gw, SurfaceSweepArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceSweepArgs, SurfaceResult>(gw, "acad.surfaces.sweep_surface", args, T_SLOW, ct);

    [McpTool("offset_surface", "Make a NEW surface parallel to an existing one at a given distance - AutoCAD's SURFOFFSET. The original is left alone; the sign of the distance chooses which side the copy sits on. On a flat surface this is a translation and the area is unchanged, which is the check; on a curved one the area grows on the convex side and shrinks on the concave. Offsetting further than the tightest radius of curvature folds the surface through itself and is refused. To offset a flat CURVE use geometry_2d.offset_curve, and to move the faces of a SOLID use geometry_3d.offset_face.", "surfaces",
        Intent = new[] { "offset a surface", "make a parallel surface at a distance",
                         "odsun powierzchnie", "surfoffset",
                         "copy a surface 50 away", "powierzchnia rownolegla", "parallel shell" },
        RequiresPlugin = true)]
    public static Task<SurfaceResult> OffsetSurface(IPluginGateway gw, SurfaceOffsetArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceOffsetArgs, SurfaceResult>(gw, "acad.surfaces.offset_surface", args, T_SLOW, ct);

    [McpTool("convert_to_surface", "Turn a solid, a region or a closed planar curve into a SURFACE - AutoCAD's CONVTOSURFACE. A surface is a shell with no inside, so converting a solid keeps its skin and throws the volume away; the area of the result should match the solid's surface area, which is the check the tool reports. This always works, because discarding the inside needs nothing of the shape. convert_to_solid goes the other way and does not always work.", "surfaces",
        Intent = new[] { "convert a solid to a surface", "convtosurface",
                         "zamien bryle na powierzchnie", "turn a region into a surface",
                         "make a shell out of a solid", "konwersja na powierzchnie",
                         "strip the volume from a solid" },
        RequiresPlugin = true)]
    public static Task<ToSurfaceResult> ConvertToSurface(IPluginGateway gw, SurfaceConvertArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceConvertArgs, ToSurfaceResult>(gw, "acad.surfaces.convert_to_surface", args, T_SLOW, ct);

    [McpTool("convert_to_solid", "Turn a watertight set of surfaces, a closed mesh or a thickened region into a SOLID - AutoCAD's CONVTOSOLID. This only works on something that ENCLOSES a space: an open sheet has no inside and cannot become a solid, which is a fact about the geometry rather than a limitation of the tool. The volume of the result is reported and must be greater than zero - that is the whole proof the source really was closed, since a conversion that quietly produced an empty solid would hand back just as valid a handle. convert_to_surface goes the other way and always works.", "surfaces",
        Intent = new[] { "convert a surface to a solid", "convtosolid",
                         "zamien powierzchnie na bryle", "make a solid from closed surfaces",
                         "turn a watertight shell into a solid", "konwersja na bryle",
                         "close a shell into a solid" },
        RequiresPlugin = true)]
    public static Task<ToSolidResult> ConvertToSolid(IPluginGateway gw, SurfaceConvertArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceConvertArgs, ToSolidResult>(gw, "acad.surfaces.convert_to_solid", args, T_SLOW, ct);

    [McpTool("get_surface_info", "Report what a surface actually IS: its concrete type, its area, how many faces and edges it has, whether the whole thing lies in one plane, and its bounding box. The type is the useful part - a PlaneSurface, ExtrudedSurface, RevolvedSurface, SweptSurface, LoftedSurface and NurbSurface each accept different edits, and asking for one the surface does not support is the commonest failure in this category. Read-only. For a solid, the equivalent questions are geometry_3d.check_solid and get_surface_area.", "surfaces",
        Intent = new[] { "what kind of surface is this", "get surface info",
                         "jaka to jest powierzchnia", "area of a surface",
                         "is this surface flat", "informacje o powierzchni",
                         "describe a surface entity" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<SurfaceInfoResult> GetSurfaceInfo(IPluginGateway gw, SurfaceConvertArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceConvertArgs, SurfaceInfoResult>(gw, "acad.surfaces.get_surface_info", args, T_NORMAL, ct);

    // ───────────────────────── joining, projecting, NURBS ─────────────────────────

    [McpTool("blend_surfaces", "Bridge two edges with a new surface that runs smoothly between them - AutoCAD's SURFBLEND. Give the two curves to span; a blend needs edges that FACE each other across a gap, so curves that cross, or that lie end to end rather than side by side, leave nothing to bridge and are refused. Between two parallel straight curves the blend is a flat ruled sheet whose area is the average of the two lengths times the gap, which makes it checkable on paper; curved or non-parallel edges give a larger area, never a smaller one. Use loft_curves in geometry_3d when what you want is a solid through several cross-sections rather than a skin between two.", "surfaces",
        Intent = new[] { "blend between two edges", "surfblend",
                         "bridge two curves with a surface", "polacz dwie krawedzie powierzchnia",
                         "make a smooth transition between two edges", "przejscie miedzy krzywymi",
                         "skin between two curves" },
        RequiresPlugin = true)]
    public static Task<BlendResult> BlendSurfaces(IPluginGateway gw, SurfaceBlendArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceBlendArgs, BlendResult>(gw, "acad.surfaces.blend_surfaces", args, T_SLOW, ct);

    [McpTool("project_to_surface", "Cast geometry onto a surface along a direction and draw where it lands - AutoCAD's PROJECTGEOMETRY. The direction defaults to straight down, which is what draping a site boundary or a road centreline onto a terrain needs. Onto a surface square to the direction the projected length equals the original; onto a tilted one it comes out longer by one over the cosine of the tilt, and the tool reports both lengths so that is checkable. A projection that misses the surface is refused, with the likely cause named: measured, AutoCAD answers that case with GeneralModelingFailure rather than with an empty result.", "surfaces",
        Intent = new[] { "project a curve onto a surface", "projectgeometry",
                         "rzutuj krzywa na powierzchnie", "drape a boundary over a terrain",
                         "cast geometry onto a surface", "rzut geometrii na teren",
                         "project a road centreline onto ground" },
        RequiresPlugin = true)]
    public static Task<ProjectResult> ProjectToSurface(IPluginGateway gw, SurfaceProjectArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceProjectArgs, ProjectResult>(gw, "acad.surfaces.project_to_surface", args, T_SLOW, ct);

    [McpTool("convert_to_nurbs", "Re-describe a surface as NURBS - AutoCAD's CONVTONURBS. NURBS is the general form: it carries a grid of control points that can be pushed about with edit_nurbs_point, which an ExtrudedSurface or a RevolvedSurface cannot. Re-describing a shape must not RESHAPE it, so the tool measures the area before and after and refuses to call it a conversion if they differ - a badly approximated conversion would still hand back a perfectly valid NURBS surface. One surface can convert into several.", "surfaces",
        Intent = new[] { "convert a surface to nurbs", "convtonurbs",
                         "zamien powierzchnie na nurbs", "make this surface editable by control points",
                         "turn a surface into a nurbs surface", "konwersja na nurbs",
                         "get control points on this surface" },
        RequiresPlugin = true)]
    public static Task<ToNurbsResult> ConvertToNurbs(IPluginGateway gw, SurfaceConvertArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceConvertArgs, ToNurbsResult>(gw, "acad.surfaces.convert_to_nurbs", args, T_SLOW, ct);

    [McpTool("get_nurbs_info", "List the control-point CAGE of a NURBS surface: its degree in u and v, how many points there are each way, and where every one of them sits. This is what you read before calling edit_nurbs_point, which addresses a point by its (u, v) index. The points steer the surface without lying on it - moving one pulls the shape towards it rather than placing the surface there. Read-only. Only a NurbSurface has a cage; run convert_to_nurbs first on anything else.", "surfaces",
        Intent = new[] { "list the control points of a nurbs surface", "get nurbs info",
                         "wypisz punkty kontrolne powierzchni", "show cv",
                         "what degree is this nurbs surface", "siatka punktow kontrolnych",
                         "how many control points does this surface have" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<NurbsInfoResult> GetNurbsInfo(IPluginGateway gw, SurfaceConvertArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<SurfaceConvertArgs, NurbsInfoResult>(gw, "acad.surfaces.get_nurbs_info", args, T_NORMAL, ct);

    [McpTool("edit_nurbs_point", "Move one control point of a NURBS surface, addressed by its (u, v) index from get_nurbs_info - AutoCAD's control-vertex editing. Give either `to`, an absolute position, or `by`, a displacement. The surface is PULLED towards a control point rather than passing through it, so the shape changes by less than the point did; the tool reports the area before and after and refuses when the area did not change at all, because a cage point that steers nothing means the wrong index was addressed and AutoCAD reports that move as a success. This is how a flat panel becomes a curved one without rebuilding it.", "surfaces",
        Intent = new[] { "move a control point of a nurbs surface", "edit cv",
                         "przesun punkt kontrolny powierzchni", "bend a nurbs surface by a control point",
                         "pull a surface into a curve", "edycja punktow kontrolnych",
                         "warp a panel with control vertices" },
        RequiresPlugin = true)]
    public static Task<NurbsEditResult> EditNurbsPoint(IPluginGateway gw, NurbsEditArgs args, CancellationToken ct)
        => SurfacesProxy.CallAsync<NurbsEditArgs, NurbsEditResult>(gw, "acad.surfaces.edit_nurbs_point", args, T_SLOW, ct);
}
