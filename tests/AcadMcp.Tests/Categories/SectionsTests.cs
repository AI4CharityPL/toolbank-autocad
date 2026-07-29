// Smoke tests for the acad-sections category (Phase D D9b).
// Asserts that all 4 composite tools are registered and that SectionsPalette
// resolves compass directions / layer names per rule 70.

using System.Linq;
using AcadMcp.Backend.Categories.Sections;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class SectionsTests
{
    [Fact]
    public void Catalog_contains_all_four_section_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("sections");
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Equal(4, tools.Count);
        Assert.Contains("insert_section_line", names);
        Assert.Contains("insert_section_title", names);
        Assert.Contains("insert_elevation_marker", names);
        Assert.Contains("list_section_lines", names);
    }

    [Fact]
    public void All_sections_declare_category_and_require_plugin()
    {
        var registry = new ToolRegistry();
        foreach (var tool in registry.ToolsFor("sections"))
        {
            Assert.Equal("sections", tool.Category);
            Assert.True(tool.RequiresPlugin,
                $"Section tool {tool.Name} MUST require the plugin (rule 70 §6 — composites dispatch only through primitives).");
        }
    }

    [Fact]
    public void ResolveDirectionDeg_recognises_compass_names()
    {
        Assert.Equal(0.0,   SectionsPalette.ResolveDirectionDeg("E"));
        Assert.Equal(90.0,  SectionsPalette.ResolveDirectionDeg("N"));
        Assert.Equal(180.0, SectionsPalette.ResolveDirectionDeg("W"));
        Assert.Equal(270.0, SectionsPalette.ResolveDirectionDeg("S"));
        Assert.Equal(45.0,  SectionsPalette.ResolveDirectionDeg("NE"));
        Assert.Equal(135.0, SectionsPalette.ResolveDirectionDeg("NW"));
        Assert.Equal(315.0, SectionsPalette.ResolveDirectionDeg("SE"));
        Assert.Equal(225.0, SectionsPalette.ResolveDirectionDeg("SW"));
    }

    [Fact]
    public void ResolveDirectionDeg_accepts_bare_degrees()
    {
        Assert.Equal(0.0,   SectionsPalette.ResolveDirectionDeg("0"));
        Assert.Equal(33.5,  SectionsPalette.ResolveDirectionDeg("33.5"));
        Assert.Equal(-45.0, SectionsPalette.ResolveDirectionDeg("-45"));
    }

    [Fact]
    public void ResolveDirectionDeg_falls_back_to_east_for_unknown()
    {
        Assert.Equal(0.0, SectionsPalette.ResolveDirectionDeg(""));
        Assert.Equal(0.0, SectionsPalette.ResolveDirectionDeg(null!));
        Assert.Equal(0.0, SectionsPalette.ResolveDirectionDeg("bogus"));
    }

    [Fact]
    public void Layer_names_follow_A_DETL_convention()
    {
        // Rule 70 §1 — every sections layer starts with A-DETL-
        foreach (var layer in new[] {
            SectionsPalette.LayerSectionLine,
            SectionsPalette.LayerSectionTitle,
            SectionsPalette.LayerElevationMarker,
        })
        {
            Assert.StartsWith("A-DETL-", layer);
        }
    }

    [Fact]
    public void Dashed_linetype_is_DASHED2()
    {
        // Rule 70 §2 — cut lines use DASHED2 scaled by plan scale
        Assert.Equal("DASHED2", SectionsPalette.SectionCutLinetype);
        Assert.True(SectionsPalette.SectionCutLtScale > 0);
    }

    [Fact]
    public void Plot_sizes_are_mm_on_paper_and_sane()
    {
        Assert.True(SectionsPalette.PlotOffsetTickMm        > 0 && SectionsPalette.PlotOffsetTickMm        < 50);
        Assert.True(SectionsPalette.PlotTitleUnderlineMm    > 0 && SectionsPalette.PlotTitleUnderlineMm    < 500);
        Assert.True(SectionsPalette.PlotElevationTriangleMm > 0 && SectionsPalette.PlotElevationTriangleMm < 50);
        Assert.True(SectionsPalette.PlotElevationBaselineMm > 0 && SectionsPalette.PlotElevationBaselineMm < 500);
    }

    [Fact]
    public void Directions_dictionary_contains_all_eight_compass_points()
    {
        foreach (var dir in new[] { "N", "E", "S", "W", "NE", "NW", "SE", "SW" })
        {
            Assert.True(SectionsPalette.Directions.ContainsKey(dir));
        }
        Assert.Equal(8, SectionsPalette.Directions.Count);
    }
}
