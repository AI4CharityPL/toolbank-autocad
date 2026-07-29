// Smoke + regression test for acad-boolean-ops category (8 tools).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class BooleanOpsTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        "union_solids", "subtract_solids", "intersect_solids",
        "union_regions", "subtract_regions", "intersect_regions",
        "create_region", "check_intersection",
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("boolean-ops").Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedTools.OrderBy(n => n).ToArray(), tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("boolean-ops"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5);
        }
    }

    [Fact]
    public void All_tools_require_plugin()
    {
        foreach (var t in NewRegistry().ToolsFor("boolean-ops"))
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
    }

    [Fact]
    public void Check_intersection_is_read_only()
    {
        var t = NewRegistry().ToolsFor("boolean-ops").Single(x => x.Name == "check_intersection");
        Assert.True(t.ReadOnly);
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("boolean-ops"))
            Assert.True(t.Intent.Count >= 5, $"{t.Name} needs ≥5 intents");
    }
}
