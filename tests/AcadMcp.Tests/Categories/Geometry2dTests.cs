// Smoke test for acad-geometry-2d category.
// NOTE: tests project (.csproj) is not yet wired into the solution — file is here so
// when xunit project is added (Phase 1.4+), these assertions auto-compile.
//
// What it checks:
//   1. catalog has the expected 32 tools
//   2. every tool name is snake_case, lowercase, non-empty
//   3. every write tool sets RequiresPlugin = true
//   4. every read-only tool sets ReadOnly = true and RequiresPlugin = true
//   5. each tool has >= 5 Intent examples (rule 22)

using System.Linq;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class Geometry2dTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        "draw_line", "draw_polyline", "draw_circle", "draw_arc", "draw_ellipse",
        "draw_rectangle", "draw_polygon", "draw_spline", "draw_point", "draw_donut",
        "draw_xline", "draw_ray", "draw_text", "draw_mtext", "draw_hatch", "draw_revcloud",
        "get_entity", "list_entities_in_window", "get_curve_length", "get_area",
        "get_bounding_box", "get_intersections", "get_distance_points", "get_distance_to_entity",
        "offset_curve", "trim_curve", "extend_curve", "join_curves", "explode_entity",
        "fillet_corner", "chamfer_corner", "delete_entities",
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var registry = NewRegistry();
        var tools = registry.ToolsFor("geometry-2d").Select(t => t.Name).OrderBy(n => n).ToArray();
        var expected = ExpectedTools.OrderBy(n => n).ToArray();
        Assert.Equal(expected, tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        var registry = NewRegistry();
        foreach (var t in registry.ToolsFor("geometry-2d"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5, $"{t.Name} > 5 words");
        }
    }

    [Fact]
    public void Plugin_required_tools_have_flag()
    {
        var registry = NewRegistry();
        var pure = new[] { "get_distance_points" };
        foreach (var t in registry.ToolsFor("geometry-2d"))
        {
            if (pure.Contains(t.Name)) continue;
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
        }
    }

    [Fact]
    public void Read_only_tools_are_idempotent_and_safe()
    {
        var registry = NewRegistry();
        foreach (var t in registry.ToolsFor("geometry-2d"))
        {
            if (t.Name.StartsWith("get_") || t.Name.StartsWith("list_"))
            {
                Assert.True(t.ReadOnly, $"{t.Name} should be ReadOnly = true");
            }
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        var registry = NewRegistry();
        foreach (var t in registry.ToolsFor("geometry-2d"))
        {
            Assert.True(t.Intent.Count >= 5,
                $"{t.Name} has only {t.Intent.Count} intents (need >= 5 PL+EN combined)");
        }
    }
}
