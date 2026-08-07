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
        // draw_mline is here rather than in phase 3.1 with the rest of the geometry extensions:
        // acad-styles gained MLINE style authoring and nothing in the bank could draw with one,
        // which makes such a style both unusable by an agent and impossible to check by sight.
        "draw_line", "draw_polyline", "draw_mline", "draw_circle", "draw_arc", "draw_ellipse",
        "draw_rectangle", "draw_polygon", "draw_spline", "draw_point", "draw_donut",
        "draw_xline", "draw_ray", "draw_text", "draw_mtext", "draw_hatch", "draw_revcloud",
        "get_entity", "list_entities_in_window", "get_curve_length", "get_area",
        "get_bounding_box", "get_intersections", "get_distance_points", "get_distance_to_entity",
        "offset_curve", "trim_curve", "extend_curve", "join_curves", "explode_entity",
        "fillet_corner", "chamfer_corner", "delete_entities",

        // Roadmap 3.1, first tranche. A drawn polyline is a first draft; without these the only
        // way to change one is to delete it and draw it again, losing its handle and its layer.
        "list_polyline_vertices", "polyline_add_vertex", "polyline_remove_vertex",
        "edit_polyline_vertex", "set_polyline_width", "reverse_curve",

        // Roadmap 3.1, second tranche: breaking and dividing. The break tools ERASE the original
        // and replace it with pieces; divide and measure only mark a curve, they never cut it.
        "break_at_point", "break_between_points", "divide_object", "measure_object",
        // Added after the visual check: DBPoints at the default PDMODE draw as a single
        // pixel, so divide/measure markers were invisible and looked like nothing happened.
        "set_point_style",

        // Roadmap 3.1, third tranche: what covers what. A wipeout behind the thing it
        // should hide is invisible, so create_wipeout brings it to the front by default.
        "set_draworder", "set_object_transparency", "create_wipeout", "set_wipeout_frame",

        // Roadmap 3.1, fourth tranche: splines. draw_spline interpolates THROUGH fit
        // points; draw_spline_cv is pulled by control vertices it does not touch.
        "draw_spline_cv", "edit_spline_fit_point", "spline_to_polyline",

        // Roadmap 3.1, fifth tranche. lengthen_curve is NOT extend_curve: it takes a
        // distance, where extend_curve runs until it meets a boundary entity.
        "lengthen_curve", "draw_ellipse_arc",

        // Roadmap 3.1, sixth tranche. Both share the hatches category's TraceBoundary
        // handling, where the UCS-seed and view-framing traps of A1 were already solved.
        "boundary_from_point", "region_from_boundary",

        // Roadmap 3.1, seventh tranche. There is no pick in an MCP call, so blend_curves
        // uses the nearest pair of free ends and reports which - a blend across the wrong
        // two still looks like a perfectly good spline.
        "blend_curves",

        // Roadmap 3.1, eighth tranche. draw_mline could draw a wall and acad-styles could
        // author its style, but nothing could change one afterwards.
        "edit_mline_vertex", "mline_join",

        // Roadmap 3.1, ninth tranche. fit_polyline's two modes are two different curves -
        // one runs through the vertices, the other is only pulled towards them - and
        // stretch_window is the one edit that moves part of an entity and leaves the rest.
        "fit_polyline", "stretch_window",
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
