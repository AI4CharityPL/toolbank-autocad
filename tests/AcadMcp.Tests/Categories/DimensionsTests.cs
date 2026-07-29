// Smoke test for acad-dimensions category.
// Pins tool count and names (rule 24 + rule 66).

using System.Linq;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class DimensionsTests
{
    [Fact]
    public void Catalog_contains_all_seventeen_dimension_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("dimensions");
        Assert.Equal(17, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        // original 12:
        Assert.Contains("dimension_linear", names);
        Assert.Contains("dimension_aligned", names);
        Assert.Contains("dimension_angular_3p", names);
        Assert.Contains("dimension_angular_2l", names);
        Assert.Contains("dimension_radial", names);
        Assert.Contains("dimension_diametric", names);
        Assert.Contains("dimension_arc_length", names);
        Assert.Contains("dimension_ordinate", names);
        Assert.Contains("dimension_baseline_chain", names);
        Assert.Contains("dimension_continued_chain", names);
        Assert.Contains("list_dimstyles", names);
        Assert.Contains("set_entity_dimstyle", names);
        // D6 additions (rule 66):
        Assert.Contains("ensure_architectural_dimstyle", names);
        Assert.Contains("dimension_cumulative_chain", names);
        Assert.Contains("apply_arch_tick_style", names);
        Assert.Contains("auto_dim_walls", names);
        Assert.Contains("dimension_overall", names);
    }
}
