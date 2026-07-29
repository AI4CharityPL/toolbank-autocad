// Smoke + discovery tests for acad-openings.
// Ensures the 10 D5 tools are catalogued and discoverable by key names used
// across Openings plugin handlers (acad.openings.*). Does NOT exercise the
// AutoCAD plugin runtime — that's covered by integration tests that run only
// when AutoCAD is attached.

using System.Linq;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class OpeningsTests
{
    [Fact]
    public void Catalog_contains_ten_tools_for_openings()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("openings");
        Assert.Equal(10, tools.Count);
    }

    [Theory]
    [InlineData("list_opening_catalog")]
    [InlineData("insert_door")]
    [InlineData("insert_window")]
    [InlineData("insert_opening_generic")]
    [InlineData("draw_door_by_points")]
    [InlineData("draw_window_by_points")]
    [InlineData("cut_wall_for_opening")]
    [InlineData("renumber_openings")]
    [InlineData("list_openings_in_model")]
    [InlineData("export_schedule")]
    public void Each_expected_tool_is_registered(string toolName)
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("openings");
        Assert.Contains(tools, t => t.Name == toolName);
    }

    [Fact]
    public void Read_only_flags_match_design()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("openings").ToDictionary(t => t.Name);

        Assert.True(tools["list_opening_catalog"].ReadOnly,    "list_opening_catalog must be read-only");
        Assert.True(tools["list_openings_in_model"].ReadOnly,  "list_openings_in_model must be read-only");
        Assert.True(tools["export_schedule"].ReadOnly,         "export_schedule must be read-only");

        Assert.False(tools["insert_door"].ReadOnly,            "insert_door must be write");
        Assert.False(tools["insert_window"].ReadOnly,          "insert_window must be write");
        Assert.False(tools["cut_wall_for_opening"].ReadOnly,   "cut_wall_for_opening must be write");
        Assert.False(tools["renumber_openings"].ReadOnly,      "renumber_openings must be write");
    }
}
