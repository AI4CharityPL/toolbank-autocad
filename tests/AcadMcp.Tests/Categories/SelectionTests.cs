// Smoke + regression test for acad-selection category (12 tools).

using System.Collections.Generic;
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
        "select_window",

        // Phase 3.4 extensions. quick_select_by_property is deliberately absent: filter_entities
        // and data.query_by_property already cover it. select_previous is absent too - AutoCAD's
        // previous selection is the USER's, and nothing an agent does here creates one, so the
        // tool would almost always answer nothing; save_selection_set is the honest equivalent.
        "select_similar", "select_by_area_range", "select_by_length_range",
        "select_duplicates", "select_last",
        "hide_objects", "isolate_objects", "unisolate_objects",
        "create_selection_filter", "list_selection_filters", "apply_saved_filter", "select_fence", "select_polygon",
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
        // Selecting is a question, not a change, so nearly everything here is read-only. The
        // writers are named EXPLICITLY rather than excluded by a blanket rule, so that a new tool
        // arriving without ReadOnly has to be justified here rather than slipping in: the phase
        // 3.4 additions changed the visibility of entities and saved filters into the drawing,
        // which is why this list is no longer just save_selection_set.
        var writers = new HashSet<string>
        {
            "save_selection_set",
            "hide_objects", "isolate_objects", "unisolate_objects",
            "create_selection_filter",
        };
        foreach (var t in NewRegistry().ToolsFor("selection"))
        {
            if (writers.Contains(t.Name))
            {
                Assert.False(t.ReadOnly, $"{t.Name} changes the drawing and must not be ReadOnly");
                continue;
            }
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
