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
    public void Catalog_contains_all_twenty_four_dimension_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("dimensions");
        Assert.Equal(24, tools.Count);

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
        // Roadmap 3.2, first tranche: the category could PLACE eleven kinds of dimension and
        // change none of them afterwards. All three edit how a dimension looks, never what it
        // measures.
        Assert.Contains("dimension_jogged_radius", names);
        Assert.Contains("dimension_oblique", names);
        Assert.Contains("edit_dimension_text", names);
        // Second tranche. dimension_update is NOT a second name for set_entity_dimstyle: that
        // one assigns a style and leaves per-entity overrides standing, this one re-applies the
        // style's own values and clears them.
        Assert.Contains("dimension_tolerance", names);
        Assert.Contains("dimension_update", names);
        Assert.Contains("dimension_space", names);
        Assert.Contains("dimension_arc_symbol", names);
    }
}
