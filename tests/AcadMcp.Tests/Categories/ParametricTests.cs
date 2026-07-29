// Pluginless tests for acad-parametric: catalog size, palette, and
// parametric_health ReadOnly flag. Constraint-application tools
// (apply_geom_*, apply_dim_*, delete_entity_constraints) are intentionally
// not part of this category's exposed surface -- see the header comment on
// src/AcadMcp.Backend/Categories/Parametric/ParametricTools.cs.

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
        "list_constraint_entities",
        "get_dynamic_block_properties",
        "set_dynamic_block_property",
        "parametric_health",
    };

    [Fact]
    public void Catalog_contains_all_five_parametric_tools()
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
