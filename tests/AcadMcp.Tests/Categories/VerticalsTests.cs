// Smoke test for acad-verticals category (Phase D D7).
// Asserts that all 8 composite tools are registered and discoverable.

using System.Linq;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class VerticalsTests
{
    [Fact]
    public void Catalog_contains_all_eight_verticals_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("verticals");
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Equal(8, tools.Count);
        Assert.Contains("draw_stair_straight", names);
        Assert.Contains("draw_stair_spiral", names);
        Assert.Contains("draw_stair_u_shaped", names);
        Assert.Contains("draw_ramp", names);
        Assert.Contains("insert_elevator_v", names);
        Assert.Contains("insert_escalator", names);
        Assert.Contains("insert_platform_lift", names);
        Assert.Contains("draw_handrail", names);
    }
}
