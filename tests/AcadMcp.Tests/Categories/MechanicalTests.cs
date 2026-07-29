// Pluginless tests for acad-mechanical (Phase 6.2, extended Phase 7.3 with
// side-view holes + section hatch). Asserts the source generator wired the
// 14 expected tools and that the ISO-mechanical layer key + ISO 128
// material→hatch lookup table stay in sync with rule 37.

using System.Linq;
using AcadMcp.Backend.Categories.Mechanical;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class MechanicalTests
{
    private static readonly string[] ExpectedTools =
    {
        "ensure_mechanical_layers",
        "draw_visible_edge",
        "draw_hidden_edge",
        "draw_centerline",
        "draw_centerline_cross",
        "draw_section_cut_line",
        "draw_through_hole",
        "draw_counterbore_hole",
        "draw_threaded_hole",
        "draw_bolt_head_top_view",
        "draw_revision_triangle",
        "draw_hole_side_view",
        "draw_section_hatch",
        "mechanical_health",
    };

    [Fact]
    public void Catalog_contains_all_fourteen_mechanical_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("mechanical").ToList();

        Assert.Equal(ExpectedTools.Length, tools.Count);
        foreach (var name in ExpectedTools)
            Assert.Contains(tools, t => t.Name == name);
    }

    [Fact]
    public void Mechanical_palette_contains_iso_layer_key()
    {
        var names = MechanicalPalette.All.Select(s => s.Name).ToHashSet();
        Assert.Contains("ME-VISIBLE", names);
        Assert.Contains("ME-HIDDEN",  names);
        Assert.Contains("ME-CENTER",  names);
        Assert.Contains("ME-SECTION", names);
        Assert.Contains("ME-THREAD",  names);
        Assert.Contains("ME-REV",     names);
        // Rule 37 §9 specifies an 11-layer key — fail loudly if a layer is dropped.
        Assert.Equal(11, MechanicalPalette.All.Count);

        // Construction layer must be the only non-plottable entry.
        var nonPlot = MechanicalPalette.All.Where(s => !s.Plottable).Select(s => s.Name).ToList();
        Assert.Equal(new[] { "ME-CONSTRUCTION" }, nonPlot);
    }

    [Fact]
    public void Mechanical_patterns_cover_iso_128_50_materials()
    {
        var keys = MechanicalPatterns.ByMaterial.Keys.ToHashSet();
        // The four ISO 128-50 anchor materials called out in rule 37 §8.
        foreach (var material in new[] { "steel", "cast_iron", "aluminium", "concrete" })
            Assert.Contains(material, keys);
    }

    [Fact]
    public void Mechanical_health_tool_is_pluginless_and_readonly()
    {
        var registry = new ToolRegistry();
        var health = registry.ToolsFor("mechanical").Single(t => t.Name == "mechanical_health");
        Assert.True(health.ReadOnly,            "mechanical_health must be ReadOnly (rule 19).");
        Assert.False(health.RequiresPlugin,     "mechanical_health must not require AutoCAD (rule 22).");
    }
}
