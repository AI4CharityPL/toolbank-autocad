// Smoke + regression test for acad-modify category (18 tools).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class ModifyTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        "move", "rotate", "scale", "mirror", "copy",
        "array_rectangular", "array_polar", "align",
        "set_layer", "set_color", "set_linetype", "set_lineweight", "match_properties",
        "erase",
        "create_group", "ungroup",
        // Roadmap 3.1: transform by a MEASUREMENT rather than a factor. `scale` and `rotate`
        // stay, because knowing the factor is the other half of the job.
        "scale_by_reference", "rotate_by_reference",
        // "undo" and "redo" were withdrawn 2026-08-04 and must NOT come back here without the
        // underlying problem being fixed first. Measured: after calling undo, the entity was
        // still present at 0.0 s, 0.5 s, 1.5 s and 3.0 s - the queued UNDO never runs in this
        // dispatch context. The handlers and proxy methods still exist; only the [McpTool]
        // attributes are gone. acad_undo_checkpoint / acad_restore_checkpoint are the working
        // rollback. See docs/KNOWN-GAPS.md A2.
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("modify").Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedTools.OrderBy(n => n).ToArray(), tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("modify"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5);
        }
    }

    [Fact]
    public void All_tools_require_plugin_and_are_writes()
    {
        foreach (var t in NewRegistry().ToolsFor("modify"))
        {
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
            Assert.False(t.ReadOnly, $"{t.Name} mutates state and must NOT be ReadOnly");
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("modify"))
            Assert.True(t.Intent.Count >= 5, $"{t.Name} needs ≥5 intents");
    }
}
