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
}
