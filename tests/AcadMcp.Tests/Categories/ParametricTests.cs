// Pluginless tests for acad-parametric (Phase 6.5, extended Phase 7.3 with
// 5 more geometric constraint types + linear/aligned dimensional
// constraints): catalog size, palette, and parametric_health ReadOnly flag.

using System.Linq;
using AcadMcp.Backend.Categories.Parametric;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class ParametricTests
{
    private static readonly string[] ExpectedTools =
    {
        "ensure_parametric_layers",
        "apply_geom_horizontal",
        "apply_geom_vertical",
        "apply_geom_parallel",
        "apply_geom_perpendicular",
        "apply_geom_coincident",
        "apply_geom_fix",
        "apply_geom_tangent",
        "apply_geom_concentric",
        "apply_geom_collinear",
        "apply_geom_equal",
        "apply_geom_symmetric",
        "apply_dim_linear",
        "apply_dim_aligned",
        "delete_entity_constraints",
        "list_constraint_entities",
        "get_dynamic_block_properties",
        "set_dynamic_block_property",
        "parametric_health",
    };

    [Fact]
    public void Catalog_contains_all_nineteen_parametric_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("parametric").ToList();
        Assert.Equal(ExpectedTools.Length, tools.Count);
        foreach (var name in ExpectedTools)
            Assert.Contains(tools, t => t.Name == name);
    }

    [Fact]
    public void Parametric_palette_has_six_layers()
    {
        Assert.Equal(6, ParametricPalette.All.Count);
        var names = ParametricPalette.All.Select(s => s.Name).ToHashSet();
        Assert.Contains("P-SKETCH", names);
        Assert.Contains("P-CONSTRAINED", names);
    }

    [Fact]
    public void Parametric_health_is_pluginless_and_readonly()
    {
        var registry = new ToolRegistry();
        var h = registry.ToolsFor("parametric").Single(t => t.Name == "parametric_health");
        Assert.True(h.ReadOnly);
        Assert.False(h.RequiresPlugin);
    }

    [Fact]
    public void Dynamic_angle_policy_string_is_non_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ParametricTools.DynamicAnglePolicy));
    }
}
