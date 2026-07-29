// Smoke tests for the acad-callouts category (Phase D D9a).
// Asserts that all 5 composite tools are registered and that CalloutsPalette
// resolves scales / sheet formats / bar presets per rule 69.

using System.Linq;
using AcadMcp.Backend.Categories.Callouts;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class CalloutsTests
{
    [Fact]
    public void Catalog_contains_all_five_callout_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("callouts");
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Equal(5, tools.Count);
        Assert.Contains("insert_north_arrow", names);
        Assert.Contains("insert_scale_bar", names);
        Assert.Contains("insert_section_callout", names);
        Assert.Contains("insert_detail_callout", names);
        Assert.Contains("insert_title_block", names);
    }

    [Fact]
    public void All_callouts_declare_category_and_require_plugin()
    {
        var registry = new ToolRegistry();
        foreach (var tool in registry.ToolsFor("callouts"))
        {
            Assert.Equal("callouts", tool.Category);
            Assert.True(tool.RequiresPlugin,
                $"Callout tool {tool.Name} MUST require the plugin (rule 69 §1 — all writes go through primitives).");
        }
    }

    [Fact]
    public void ResolveScaleFactor_recognises_common_scales()
    {
        Assert.Equal(100, CalloutsPalette.ResolveScaleFactor("1:100"));
        Assert.Equal(50,  CalloutsPalette.ResolveScaleFactor("1:50"));
        Assert.Equal(20,  CalloutsPalette.ResolveScaleFactor("1:20"));
        Assert.Equal(500, CalloutsPalette.ResolveScaleFactor("1:500"));
    }

    [Fact]
    public void ResolveScaleFactor_falls_back_to_100_for_unknown()
    {
        Assert.Equal(100, CalloutsPalette.ResolveScaleFactor(""));
        Assert.Equal(100, CalloutsPalette.ResolveScaleFactor(null!));
        Assert.Equal(100, CalloutsPalette.ResolveScaleFactor("bogus"));
    }

    [Fact]
    public void ResolveScaleBarPreset_segments_follow_scale()
    {
        // Rule 69 §5 — segment metres scale with plan scale
        Assert.Equal(0.5, CalloutsPalette.ResolveScaleBarPreset(20).SegmentM);
        Assert.Equal(1.0, CalloutsPalette.ResolveScaleBarPreset(50).SegmentM);
        Assert.Equal(1.0, CalloutsPalette.ResolveScaleBarPreset(100).SegmentM);
        Assert.Equal(2.0, CalloutsPalette.ResolveScaleBarPreset(200).SegmentM);
        Assert.Equal(5.0, CalloutsPalette.ResolveScaleBarPreset(500).SegmentM);

        // Every preset emits exactly 5 segments totalling a 50 mm plotted bar.
        foreach (var sf in new[] { 25, 50, 100, 200, 500 })
        {
            Assert.Equal(5, CalloutsPalette.ResolveScaleBarPreset(sf).SegmentCount);
        }
    }

    [Fact]
    public void Sheet_formats_cover_iso_series()
    {
        foreach (var name in new[] { "A0", "A1", "A2", "A3", "A4" })
        {
            var sheet = CalloutsPalette.ResolveSheet(name);
            Assert.Equal(name, sheet.Name);
            Assert.True(sheet.WidthMm > 0);
            Assert.True(sheet.HeightMm > 0);
            Assert.True(sheet.WidthMm >= sheet.HeightMm,
                $"ISO {name} landscape orientation expected (width >= height).");
        }
    }

    [Fact]
    public void ResolveSheet_falls_back_to_A1()
    {
        Assert.Equal("A1", CalloutsPalette.ResolveSheet("bogus").Name);
        Assert.Equal("A1", CalloutsPalette.ResolveSheet("").Name);
    }

    [Fact]
    public void Default_title_block_rows_include_required_fields()
    {
        // Rule 69 §3 — PL title block mandatory rows
        foreach (var row in new[] { "PROJEKT", "RYSUNEK", "SKALA", "NR RYS.", "DATA", "PROJEKTANT" })
        {
            Assert.Contains(row, CalloutsPalette.DefaultTitleBlockRows);
        }
        Assert.Equal(12, CalloutsPalette.DefaultTitleBlockRows.Count);
    }

    [Fact]
    public void Layer_names_follow_A_ANNO_convention()
    {
        // Rule 69 §1 — every callout layer starts with A-ANNO-
        foreach (var layer in new[] {
            CalloutsPalette.LayerNorth, CalloutsPalette.LayerSbar,
            CalloutsPalette.LayerSymb,  CalloutsPalette.LayerTtlb,
            CalloutsPalette.LayerText,  CalloutsPalette.LayerBorder,
        })
        {
            Assert.StartsWith("A-ANNO-", layer);
        }
    }
}
