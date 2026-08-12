// Smoke + shape tests for acad-structural domain category.
// These tests run without AutoCAD — they only verify the source-generated
// catalog matches what we shipped in StructuralTools.cs (rule 24 + rule 35 + rule 72).

using System.Linq;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class StructuralTests
{
    [Fact]
    public void Catalog_contains_all_five_structural_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("structural");

        Assert.Equal(5, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("list_steel_profiles", names);
        Assert.Contains("insert_steel_column", names);
        Assert.Contains("insert_beam", names);
        Assert.Contains("insert_lintel", names);
        Assert.Contains("ensure_structural_layers", names);
    }

    [Fact]
    public void Every_tool_name_is_snake_case()
    {
        var registry = new ToolRegistry();
        foreach (var tool in registry.ToolsFor("structural"))
            Assert.Matches("^[a-z][a-z0-9_]*$", tool.Name);
    }

    [Fact]
    public void Every_tool_has_at_least_five_intents_including_one_english_one()
    {
        var registry = new ToolRegistry();
        foreach (var tool in registry.ToolsFor("structural"))
        {
            Assert.True(tool.Intent.Count >= 5, $"{tool.Name} has fewer than 5 intent examples.");
            Assert.Contains(tool.Intent, i => System.Text.RegularExpressions.Regex.IsMatch(i, "^[a-zA-Z0-9 '\\-]+$"));
        }
    }

    [Fact]
    public void List_steel_profiles_is_pluginless_and_readonly()
    {
        // The one tool in this category that makes no AutoCAD call at all - a pure in-memory
        // catalog read (rule 72's own framing: works before AutoCAD is even open).
        var registry = new ToolRegistry();
        var tool = registry.ToolsFor("structural").Single(t => t.Name == "list_steel_profiles");

        Assert.True(tool.ReadOnly, "list_steel_profiles must be ReadOnly.");
        Assert.False(tool.RequiresPlugin, "list_steel_profiles must not require the plugin (rule 72 §2).");
    }

    [Theory]
    [InlineData("insert_steel_column")]
    [InlineData("insert_beam")]
    [InlineData("insert_lintel")]
    [InlineData("ensure_structural_layers")]
    public void Drawing_tools_require_the_plugin(string toolName)
    {
        var registry = new ToolRegistry();
        var tool = registry.ToolsFor("structural").Single(t => t.Name == toolName);
        Assert.True(tool.RequiresPlugin, $"{toolName} draws into the document and must require the plugin.");
    }

    [Fact]
    public void Architecture_palette_now_carries_the_shared_beam_and_lintel_layers()
    {
        // Rule 72 §1: S-BEAM/S-BEAM-CTRL/S-LINTEL live in ArchitecturePalette, not a forked
        // StructuralPalette - pin that they exist and are flagged Structural.
        var beam = ArchitecturePalette.All.Single(s => s.Name == "S-BEAM");
        var beamCtrl = ArchitecturePalette.All.Single(s => s.Name == "S-BEAM-CTRL");
        var lintel = ArchitecturePalette.All.Single(s => s.Name == "S-LINTEL");

        Assert.True(beam.Structural);
        Assert.True(beamCtrl.Structural);
        Assert.True(lintel.Structural);
        Assert.Equal(ArchitecturePalette.LayerBeam, beam.Name);
        Assert.Equal(ArchitecturePalette.LayerBeamCtrl, beamCtrl.Name);
        Assert.Equal(ArchitecturePalette.LayerLintel, lintel.Name);
    }
}
