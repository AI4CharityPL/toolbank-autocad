// Smoke tests for the acad-plotstyles category (Phase D D9c).
// Asserts that all 3 composite tools are registered and that PlotstylesPalette
// exposes the canonical presets + ISO lineweight tier table per rule 61.

using System.IO;
using System.Linq;
using AcadMcp.Backend.Categories.Plotstyles;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class PlotstylesTests
{
    [Fact]
    public void Catalog_contains_all_three_plotstyles_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("plotstyles");
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Equal(3, tools.Count);
        Assert.Contains("ensure_ctb", names);
        Assert.Contains("apply_plotstyle_to_layout", names);
        Assert.Contains("list_plotstyles", names);
    }

    [Fact]
    public void All_plotstyles_tools_declare_category_and_require_plugin()
    {
        var registry = new ToolRegistry();
        foreach (var tool in registry.ToolsFor("plotstyles"))
        {
            Assert.Equal("plotstyles", tool.Category);
            Assert.True(tool.RequiresPlugin,
                $"Plotstyle tool {tool.Name} MUST require the plugin (rule 61 §6 — enumeration + refresh + configure_plot all run inside AutoCAD).");
        }
    }

    [Fact]
    public void DefaultPresets_contains_three_canonical_sheets()
    {
        Assert.Equal(3, PlotstylesPalette.DefaultPresets.Count);
        Assert.Contains("HOSPITAL-ISO.ctb",  PlotstylesPalette.DefaultPresets);
        Assert.Contains("ISO-Standard.ctb",  PlotstylesPalette.DefaultPresets);
        Assert.Contains("monochrome.ctb",    PlotstylesPalette.DefaultPresets);
    }

    [Fact]
    public void ArchLineweightMm_covers_nine_colour_tiers()
    {
        // Rule 61 §2 — all 9 ACI indices mapped to plotted mm
        Assert.Equal(9, PlotstylesPalette.ArchLineweightMm.Count);
        for (int i = 1; i <= 9; i++)
        {
            Assert.True(PlotstylesPalette.ArchLineweightMm.ContainsKey(i),
                $"ACI {i} missing from lineweight tier table.");
        }
    }

    [Fact]
    public void ArchLineweightMm_follows_rule_61_table()
    {
        // Rule 61 §2 — the canonical table.
        Assert.Equal(0.18, PlotstylesPalette.ArchLineweightMm[1]);   // RED
        Assert.Equal(0.25, PlotstylesPalette.ArchLineweightMm[2]);   // YELLOW
        Assert.Equal(0.35, PlotstylesPalette.ArchLineweightMm[3]);   // GREEN  — walls
        Assert.Equal(0.50, PlotstylesPalette.ArchLineweightMm[4]);   // CYAN   — section cuts
        Assert.Equal(0.13, PlotstylesPalette.ArchLineweightMm[5]);   // BLUE
        Assert.Equal(0.70, PlotstylesPalette.ArchLineweightMm[6]);   // MAGENTA — fire walls
        Assert.Equal(0.25, PlotstylesPalette.ArchLineweightMm[7]);   // WHITE
        Assert.Equal(0.13, PlotstylesPalette.ArchLineweightMm[8]);   // DARK GREY
        Assert.Equal(0.13, PlotstylesPalette.ArchLineweightMm[9]);   // LIGHT GREY
    }

    [Fact]
    public void AssetsDirectory_resolves_to_repo_assets_plotstyles()
    {
        // Walks up from test binary looking for src/AcadMcp.Backend/AcadMcp.Backend.csproj
        // and returns <repo>/assets/plotstyles
        var dir = PlotstylesPalette.AssetsDirectory();
        Assert.EndsWith(Path.Combine("assets", "plotstyles"), dir);
    }

    [Fact]
    public void Lineweight_tiers_are_monotonic_by_visual_priority()
    {
        // Section cuts (4 — CYAN) MUST be thicker than walls (3 — GREEN)
        Assert.True(PlotstylesPalette.ArchLineweightMm[4] > PlotstylesPalette.ArchLineweightMm[3]);
        // Walls (3) MUST be thicker than door frames (2 — YELLOW)
        Assert.True(PlotstylesPalette.ArchLineweightMm[3] > PlotstylesPalette.ArchLineweightMm[2]);
        // Fire walls (6 — MAGENTA) MUST be the thickest architectural line
        foreach (var (aci, mm) in PlotstylesPalette.ArchLineweightMm)
        {
            if (aci == 6) continue;
            Assert.True(mm <= PlotstylesPalette.ArchLineweightMm[6],
                $"ACI {aci} = {mm}mm is thicker than MAGENTA fire-wall tier = {PlotstylesPalette.ArchLineweightMm[6]}mm.");
        }
    }
}
