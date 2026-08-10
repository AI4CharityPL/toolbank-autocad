// Smoke + regression test for the acad-mesh category.
// Asserts catalog completeness, snake_case names, RequiresPlugin/ReadOnly flags and
// Intent >= 5 examples per tool (rule 22).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class MeshTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        // Roadmap 4.3, first tranche. A mesh is a cage of flat faces AutoCAD can smooth; it is
        // neither a solid nor a surface, and SubDMesh carries no volume, no surface area and no
        // watertight flag - so measuring a mesh means converting it, which makes the conversion
        // its own check. The exact numbers a mesh does carry are its vertex and face counts.
        //
        // create_mesh_box is written out by hand: SubDMesh has NO factory methods for primitives,
        // unlike Solid3d which has the lot. That is why the counts are known before the call.
        "create_mesh_box", "get_mesh_info", "set_mesh_smoothness",
        "convert_mesh_to_solid", "convert_mesh_to_surface",

        // Second tranche: creasing and two more hand-tessellated primitives. set_mesh_crease
        // works on ALL edges because SubDMesh exposes no way to name individual ones - the
        // GetSubentityPathsAt family that Solid3d has is absent, and a tool cannot select
        // what the API will not address.
        "set_mesh_crease", "create_mesh_cylinder", "create_mesh_wedge",
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("mesh").Select(t => t.Name).OrderBy(n => n).ToArray();
        var expected = ExpectedTools.OrderBy(n => n).ToArray();
        Assert.Equal(expected, tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("mesh"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5, $"{t.Name} > 5 words");
        }
    }

    [Fact]
    public void All_tools_require_plugin()
    {
        foreach (var t in NewRegistry().ToolsFor("mesh"))
        {
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
        }
    }

    [Fact]
    public void Read_only_tools_are_marked()
    {
        foreach (var t in NewRegistry().ToolsFor("mesh"))
        {
            if (t.Name.StartsWith("get_") || t.Name.StartsWith("list_"))
                Assert.True(t.ReadOnly, $"{t.Name} should be ReadOnly = true");
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("mesh"))
        {
            Assert.True(t.Intent.Count >= 5,
                $"{t.Name} has only {t.Intent.Count} intents (need >= 5 PL+EN combined)");
        }
    }
}
