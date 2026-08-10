// AutoCAD acad-mesh category. A mesh is a CAGE of flat faces that AutoCAD can smooth - neither
// a solid nor a surface. SubDMesh carries no volume, no surface area and no watertight flag, so
// the only way to measure a mesh is to convert it, which makes that conversion its own check.
// What a mesh does carry exactly is its vertex and face counts, and those are the arithmetic
// everything here is verified against.
//
// Rules: 19-tool-implementation-pattern.md, 20..25, 26 (traps).

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Mesh;

public static class MeshTools
{
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("create_mesh_box", "Create a mesh BOX from two opposite corners - the mesh counterpart of geometry_3d.draw_box. A mesh is a cage of flat faces that AutoCAD can round off by subdividing, which is what you want for organic or sculpted shapes rather than engineered ones. smoothLevel 0 leaves it sharp and each level after that rounds it further, towards a ball. The cage stays 8 vertices and 6 faces at every level - AutoCAD reports the cage, not the subdivision - and those counts are checked against what came back, because there is no CreateBox for meshes in the AutoCAD API: the cage is written out by hand here, so the numbers are known before the call.", "mesh",
        Intent = new[] { "create a mesh box", "make a subdivision box",
                         "narysuj siatke prostopadloscienna", "mesh cube",
                         "box as a mesh not a solid", "siatka szescienna",
                         "smooth mesh box for sculpting" },
        RequiresPlugin = true)]
    public static Task<MeshBoxResult> CreateMeshBox(IPluginGateway gw, MeshBoxArgs args, CancellationToken ct)
        => MeshProxy.CallAsync<MeshBoxArgs, MeshBoxResult>(gw, "acad.mesh.create_mesh_box", args, T_SLOW, ct);

    [McpTool("get_mesh_info", "Report a mesh's vertex count, face count, current smoothness and bounding box. Read-only. Note what is NOT here and cannot be: a mesh has no volume and no surface area, because SubDMesh exposes neither - to measure one you convert it with convert_mesh_to_solid, which is itself the check on whether the cage is watertight. The counts are exact but describe the CAGE rather than the subdivided surface, so they stay the same whatever the smoothness - a box mesh answers 6 faces at every level. The bounding box reports the cage too, so it will not show smoothing either - to see that, convert the mesh to a solid and compare volumes.", "mesh",
        Intent = new[] { "get mesh info", "how many faces does this mesh have",
                         "informacje o siatce", "what smoothness is this mesh",
                         "ile scian ma ta siatka", "describe a mesh entity",
                         "mesh vertex and face count" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<MeshInfoResult> GetMeshInfo(IPluginGateway gw, MeshHandleArgs args, CancellationToken ct)
        => MeshProxy.CallAsync<MeshHandleArgs, MeshInfoResult>(gw, "acad.mesh.get_mesh_info", args, T_NORMAL, ct);

    [McpTool("set_mesh_smoothness", "Raise or lower how much AutoCAD rounds a mesh off - MESHSMOOTHMORE and MESHSMOOTHLESS. Give either `level`, the smoothness to set from 0 to 4, or `by`, a step from where it is now: by 1 is smooth-more and by -1 is smooth-less. Be aware that NEITHER the face count NOR the bounding box will show you this happened: both report the CAGE rather than the subdivided surface, so a box answers 6 faces and the same extents at every level. That is measured rather than assumed - guards built on each of them in turn rejected correct results. What the tool checks is the level reading back; to see the SHAPE change, convert to a solid and compare volumes, where a box smoothed to level 2 comes out at roughly a third of the sharp one. Smoothing is REVERSIBLE because the cage is kept, so coming back down returns the original mesh rather than an approximation of it.", "mesh",
        Intent = new[] { "smooth a mesh more", "meshsmoothmore", "meshsmoothless",
                         "wygladz siatke", "set mesh smoothness to 2",
                         "zmniejsz wygladzenie siatki", "make this mesh rounder" },
        RequiresPlugin = true)]
    public static Task<MeshSmoothResult> SetMeshSmoothness(IPluginGateway gw, MeshSmoothArgs args, CancellationToken ct)
        => MeshProxy.CallAsync<MeshSmoothArgs, MeshSmoothResult>(gw, "acad.mesh.set_mesh_smoothness", args, T_SLOW, ct);

    [McpTool("convert_mesh_to_solid", "Turn a mesh into a 3D SOLID - AutoCAD's CONVTOSOLID. This only works on a WATERTIGHT cage, where every edge is shared by exactly two faces; a mesh with a hole in it, or with faces wound inconsistently, encloses nothing and is refused. Because SubDMesh has no volume of its own, this is the only way to measure a mesh, and an unsmoothed box mesh converting to exactly its side cubed is the proof the cage was built and wound correctly. Pass smooth true to have AutoCAD fit curved faces to the facets rather than keeping them flat - the volume then differs, because the shape does.", "mesh",
        Intent = new[] { "convert a mesh to a solid", "convtosolid from mesh",
                         "zamien siatke na bryle", "make a solid out of this mesh",
                         "measure the volume of a mesh", "objetosc siatki",
                         "turn a watertight mesh into a solid" },
        RequiresPlugin = true)]
    public static Task<MeshToSolidResult> ConvertMeshToSolid(IPluginGateway gw, MeshConvertArgs args, CancellationToken ct)
        => MeshProxy.CallAsync<MeshConvertArgs, MeshToSolidResult>(gw, "acad.mesh.convert_mesh_to_solid", args, T_SLOW, ct);

    [McpTool("convert_mesh_to_surface", "Turn a mesh into a SURFACE - AutoCAD's CONVTOSURFACE. Unlike convert_mesh_to_solid this works on ANY mesh, watertight or not, because a surface is a shell that never had to enclose anything: a torn cage the solid conversion refuses converts to a surface perfectly well. Pass smooth true to fit curved faces to the facets rather than keeping them flat. Use surfaces.get_surface_info to measure what comes out.", "mesh",
        Intent = new[] { "convert a mesh to a surface", "convtosurface from mesh",
                         "zamien siatke na powierzchnie", "make a surface out of this mesh",
                         "mesh to shell", "siatka na powierzchnie",
                         "convert an open mesh that will not become a solid" },
        RequiresPlugin = true)]
    public static Task<MeshToSurfaceResult> ConvertMeshToSurface(IPluginGateway gw, MeshConvertArgs args, CancellationToken ct)
        => MeshProxy.CallAsync<MeshConvertArgs, MeshToSurfaceResult>(gw, "acad.mesh.convert_mesh_to_surface", args, T_SLOW, ct);
}
