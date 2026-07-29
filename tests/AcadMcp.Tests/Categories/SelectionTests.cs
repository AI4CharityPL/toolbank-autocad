// Smoke + regression test for acad-selection category (12 tools).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class SelectionTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        "select_all", "select_by_layer", "select_by_color", "select_by_type", "select_by_handle",
        "select_window", "select_fence", "select_polygon",
        "filter_entities", "save_selection_set", "load_selection_set", "count_entities",
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("selection").Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedTools.OrderBy(n => n).ToArray(), tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("selection"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5);
        }
    }

    [Fact]
    public void All_tools_require_plugin()
    {
        foreach (var t in NewRegistry().ToolsFor("selection"))
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
    }

    [Fact]
    public void Pure_select_and_count_are_read_only()
    {
        // save_selection_set is the only writer in this category.
        foreach (var t in NewRegistry().ToolsFor("selection"))
        {
            if (t.Name == "save_selection_set") continue;
            Assert.True(t.ReadOnly, $"{t.Name} should be ReadOnly = true");
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("selection"))
            Assert.True(t.Intent.Count >= 5, $"{t.Name} needs ≥5 intents");
    }
}
