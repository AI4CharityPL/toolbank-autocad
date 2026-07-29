// Smoke test for acad-blocks category.
// Pins tool count and names (rule 24 + rule 28).

using System.Linq;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class BlocksTests
{
    [Fact]
    public void Catalog_contains_all_sixteen_block_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("blocks");
        Assert.Equal(16, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        // original 12:
        Assert.Contains("define_block", names);
        Assert.Contains("define_block_from_file", names);
        Assert.Contains("redefine_block", names);
        Assert.Contains("insert_block", names);
        Assert.Contains("explode_block_reference", names);
        Assert.Contains("get_block_reference_attributes", names);
        Assert.Contains("set_block_reference_attributes", names);
        Assert.Contains("list_blocks", names);
        Assert.Contains("extract_block_references", names);
        Assert.Contains("delete_block_definition", names);
        Assert.Contains("purge_unused_blocks", names);
        Assert.Contains("rename_block", names);
        // D6 additions:
        Assert.Contains("library_register", names);
        Assert.Contains("library_list", names);
        Assert.Contains("bulk_insert", names);
        Assert.Contains("swap_block", names);
    }
}
