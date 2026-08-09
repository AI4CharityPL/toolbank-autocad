// Smoke + regression test for acad-geometry-3d category (15 tools).
// Asserts: catalog completeness, snake_case names, RequiresPlugin/ReadOnly flags
// and Intent ≥ 5 examples per tool (rule 22).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class Geometry3dTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        "draw_box", "draw_sphere", "draw_cylinder", "draw_cone", "draw_torus",
        "draw_pyramid", "draw_wedge",
        "extrude_curve", "revolve_curve", "draw_planar_surface",
        "get_volume", "get_surface_area", "get_3d_centroid",
        "get_3d_bounding_box", "get_mass_properties",

        // Roadmap 4.1, first tranche: the rest of how a solid is made from a curve.
        // extrude pushes a profile straight and revolve spins it; these carry it along an
        // arbitrary path, run a skin between cross sections, and draw the helix that is the
        // usual path for a spring or a thread.
        "sweep_curve", "loft_curves", "draw_helix",

        // Roadmap 4.1, second tranche. Both are checkable against arithmetic: cutting conserves
        // volume, and the overlap of two known boxes is computable on paper. interfere_solids is
        // NOT boolean_ops.intersect_solids - that one replaces the target with the common
        // volume, this one leaves both parties standing and hands back a third solid.
        "slice_solid", "interfere_solids",

        // Roadmap 4.1, third tranche. An imprint adds EDGES, not material - which is why
        // the tool reports the volume before and after and refuses to call it an imprint
        // if that changed. A tool that cut instead would also report more faces.
        "imprint_edges",

        // Roadmap 4.1, fourth tranche: the face/edge family. The two list_ tools are the
        // addressing scheme the rest of the family was blocked on - every SOLIDEDIT call in
        // the managed API takes SubentityId[], which a caller cannot spell, so an index plus
        // the geometry behind it is how an edge gets named.
        "list_solid_edges", "list_solid_faces", "fillet_edge", "chamfer_edge",

        // Roadmap 4.1, fifth tranche: the rest of SOLIDEDIT, all reachable once a face
        // could be named. shell_solid is here after being wrongly struck - the probe that
        // condemned it asked for ShellSolid; the method is ShellBody.
        "extrude_face", "offset_face", "move_face", "rotate_face", "taper_face",
        "delete_face", "shell_solid",
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("geometry-3d").Select(t => t.Name).OrderBy(n => n).ToArray();
        var expected = ExpectedTools.OrderBy(n => n).ToArray();
        Assert.Equal(expected, tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("geometry-3d"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5, $"{t.Name} > 5 words");
        }
    }

    [Fact]
    public void All_tools_require_plugin()
    {
        foreach (var t in NewRegistry().ToolsFor("geometry-3d"))
        {
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
        }
    }

    [Fact]
    public void Read_only_tools_are_marked()
    {
        foreach (var t in NewRegistry().ToolsFor("geometry-3d"))
        {
            if (t.Name.StartsWith("get_") || t.Name.StartsWith("list_"))
                Assert.True(t.ReadOnly, $"{t.Name} should be ReadOnly = true");
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("geometry-3d"))
        {
            Assert.True(t.Intent.Count >= 5,
                $"{t.Name} has only {t.Intent.Count} intents (need >= 5 PL+EN combined)");
        }
    }
}
