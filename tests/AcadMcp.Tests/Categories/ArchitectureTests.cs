// Smoke + shape tests for acad-architecture domain category.
// These tests run without AutoCAD — they only verify the source-generated
// catalog matches what we shipped in ArchitectureTools.cs (rule 24 + rule 35).

using System.Linq;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class ArchitectureTests
{
    [Fact]
    public void Catalog_contains_all_sixteen_architecture_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("architecture");

        Assert.Equal(16, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("ensure_architectural_layers", names);
        Assert.Contains("draw_wall", names);
        Assert.Contains("draw_walls_chain", names);
        Assert.Contains("insert_door", names);
        Assert.Contains("insert_window", names);
        Assert.Contains("insert_rect_column", names);
        Assert.Contains("insert_round_column", names);
        Assert.Contains("define_room", names);
        Assert.Contains("dimension_wall", names);
        Assert.Contains("architecture_health", names);
        // D6 additions (rule 66):
        Assert.Contains("draw_ceiling_grid", names);
        Assert.Contains("insert_stair", names);
        Assert.Contains("insert_ramp", names);
        Assert.Contains("insert_elevator", names);
        Assert.Contains("attach_room_tag", names);
        Assert.Contains("split_wall_at_opening", names);
    }

    [Fact]
    public void Architecture_palette_contains_canonical_AIA_layer_key()
    {
        // Rule 36 §11 — these names are LOAD-BEARING for downstream consumers
        // (validators, manifest descriptions, agent prompts). Pin them.
        var names = ArchitecturePalette.All.Select(s => s.Name).ToHashSet();

        Assert.Contains("A-WALL",         names);
        Assert.Contains("A-WALL-CTRL",    names);
        Assert.Contains("A-DOOR",         names);
        Assert.Contains("A-DOOR-SWING",   names);
        Assert.Contains("A-GLAZ",         names);
        Assert.Contains("A-ROOM-BNDY",    names);
        Assert.Contains("A-ROOM-IDEN",    names);
        Assert.Contains("A-ANNO-DIMS",    names);
        Assert.Contains("S-COLS",         names);
        Assert.Contains("S-COLS-CTRL",    names);
        Assert.Contains("S-SLAB",         names);
    }

    [Fact]
    public void Architecture_palette_carries_the_load_bearing_wall_layer_pair()
    {
        // Rule 74 C.1: draw_wall/draw_walls_chain's bearing=true default resolves to these -
        // colour 4 (CYAN) is the rule 61 §2 "load-bearing / section cuts (thick)" tier, first
        // actually used here rather than just reserved in the table.
        var bearing = ArchitecturePalette.All.Single(s => s.Name == "A-WALL-BEAR");
        var bearingCtrl = ArchitecturePalette.All.Single(s => s.Name == "A-WALL-BEAR-CTRL");

        Assert.Equal(4, bearing.AciColor);
        Assert.Equal("Continuous", bearing.Linetype);
        Assert.Equal(8, bearingCtrl.AciColor);
        Assert.Equal("CENTER", bearingCtrl.Linetype);
        Assert.Equal(ArchitecturePalette.LayerWallBearing, bearing.Name);
        Assert.Equal(ArchitecturePalette.LayerWallBearingCtrl, bearingCtrl.Name);
    }

    [Fact]
    public void Architecture_health_tool_is_pluginless_and_readonly()
    {
        var registry = new ToolRegistry();
        var tool = registry.ToolsFor("architecture").Single(t => t.Name == "architecture_health");

        Assert.True(tool.ReadOnly,        "architecture_health must be ReadOnly (rule 22).");
        Assert.False(tool.RequiresPlugin, "architecture_health must not require the plugin (rule 19).");
    }
}
