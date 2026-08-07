// Catalogue test for acad-annotations.

using System.Linq;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class AnnotationsTests
{
    // Was Assert.NotEmpty, which passes at 15 tools, at 12, and at one - it could not tell a
    // category that grew from one that had been gutted. Named and counted, like every other
    // category's catalogue test.
    [Fact]
    public void Catalog_contains_all_fifteen_annotation_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("annotations");
        Assert.Equal(15, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("add_dbtext", names);
        Assert.Contains("update_dbtext", names);
        Assert.Contains("add_mtext", names);
        Assert.Contains("update_mtext", names);
        Assert.Contains("add_mleader_text", names);
        Assert.Contains("add_mleader_block", names);
        Assert.Contains("add_table", names);
        Assert.Contains("set_table_cell", names);
        Assert.Contains("create_text_style", names);
        Assert.Contains("set_current_text_style", names);
        Assert.Contains("list_text_styles", names);
        Assert.Contains("delete_text_style", names);
        // Roadmap 3.3, first tranche. All three read the SIX places text lives, not the two
        // obvious ones: text, mtext, mleaders, block attributes, table cells and dimension
        // text overrides.
        Assert.Contains("list_text_by_pattern", names);
        Assert.Contains("find_replace_text", names);
        Assert.Contains("export_text_content", names);
    }
}
