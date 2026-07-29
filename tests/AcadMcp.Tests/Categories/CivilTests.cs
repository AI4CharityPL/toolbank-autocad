// Pluginless tests for acad-civil (Phase 6.3, extended Phase 7.3 with spiral
// alignments + vertical profiles). Asserts the source generator wired the 12
// expected tools and exercises the pure CivilGeometry numerics (bearings,
// stationing, parcel closure) without touching AutoCAD.

using System.Linq;
using AcadMcp.Backend.Categories.Civil;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class CivilTests
{
    private static readonly string[] ExpectedTools =
    {
        "ensure_civil_layers",
        "draw_alignment_tangent",
        "draw_alignment_curve",
        "draw_alignment_spiral",
        "draw_vertical_profile",
        "draw_road_corridor",
        "place_station_labels",
        "draw_parcel",
        "draw_contour_line",
        "place_spot_elevation",
        "draw_north_arrow",
        "civil_health",
    };

    [Fact]
    public void Catalog_contains_all_twelve_civil_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("civil").ToList();

        Assert.Equal(ExpectedTools.Length, tools.Count);
        foreach (var name in ExpectedTools)
            Assert.Contains(tools, t => t.Name == name);
    }

    [Fact]
    public void Civil_palette_contains_canonical_layer_key()
    {
        var names = CivilPalette.All.Select(s => s.Name).ToHashSet();
        Assert.Contains("C-ROAD-CNTR", names);
        Assert.Contains("C-ROAD-EDGE", names);
        Assert.Contains("C-PROP",      names);
        Assert.Contains("C-TOPO-MAJR", names);
        Assert.Contains("C-TOPO-MINR", names);
        Assert.Contains("C-NORTH",     names);
        // Rule 38 §9 specifies a 12-layer key — fail loudly if a layer is dropped.
        Assert.Equal(12, CivilPalette.All.Count);
    }

    [Fact]
    public void Civil_health_tool_is_pluginless_and_readonly()
    {
        var registry = new ToolRegistry();
        var health = registry.ToolsFor("civil").Single(t => t.Name == "civil_health");
        Assert.True(health.ReadOnly,        "civil_health must be ReadOnly (rule 19).");
        Assert.False(health.RequiresPlugin, "civil_health must not require AutoCAD (rule 22).");
    }

    [Theory]
    [InlineData("N 0 0 0 E",       0.0,  1.0)]   // due north
    [InlineData("N 90 0 0 E",      1.0,  0.0)]   // due east
    [InlineData("S 0 0 0 E",       0.0, -1.0)]   // due south
    [InlineData("S 90 0 0 W",     -1.0,  0.0)]   // due west
    [InlineData("N 45 0 0 E",   0.7071, 0.7071)] // NE 45°
    [InlineData("S 30 0 0 W",  -0.5,  -0.866025)]
    public void Bearing_to_vector_matches_quadrant_rules(string text, double expectedX, double expectedY)
    {
        var b = Bearing.Parse(text);
        var (x, y) = b.ToVector();
        Assert.InRange(x - expectedX, -1e-3, 1e-3);
        Assert.InRange(y - expectedY, -1e-3, 1e-3);
    }

    [Fact]
    public void Bearing_round_trips_through_surveyor_string()
    {
        var b = Bearing.Parse("N 45 30 15 E");
        Assert.Equal(BearingQuadrant.NE, b.Quadrant);
        Assert.Equal(45,  b.Degrees);
        Assert.Equal(30,  b.Minutes);
        Assert.InRange(b.Seconds - 15.0, -1e-9, 1e-9);

        var s = b.ToSurveyorString();          // "N 45° 30' 15.00\" E"
        Assert.StartsWith("N 45° 30' 15", s);
        Assert.EndsWith("E", s);
    }

    [Fact]
    public void Stationing_metric_emits_km_plus_metres_form()
    {
        Assert.Equal("0+020.0", CivilStationing.Format(20.0,  StationingSystem.MetricKm));
        Assert.Equal("0+040.0", CivilStationing.Format(40.0,  StationingSystem.MetricKm));
        Assert.Equal("1+020.0", CivilStationing.Format(1020.0, StationingSystem.MetricKm));
        Assert.Equal("2+345.5", CivilStationing.Format(2345.5, StationingSystem.MetricKm));
    }

    [Fact]
    public void Parcel_traverse_reports_closure_error_and_tolerance()
    {
        // Walk a 10×10 m square: E10, S10, W10, N10. With ideal bearings, residual is ~0.
        var legs = new[]
        {
            (Bearing.Parse("N 90 0 0 E"), 10.0),
            (Bearing.Parse("S 0  0 0 E"), 10.0),
            (Bearing.Parse("S 90 0 0 W"), 10.0),
            (Bearing.Parse("N 0  0 0 E"), 10.0),
        };
        var result = CivilParcel.Traverse(new Point2dDto(0, 0), legs, toleranceM: 0.02);
        Assert.Equal(5, result.Vertices.Count);                  // 4 legs + start
        Assert.InRange(result.ClosureErrorM, 0.0, 1e-9);
        Assert.True(result.WithinTolerance);
        Assert.Equal(0.02, result.ToleranceM);
    }

    [Fact]
    public void Parcel_traverse_flags_out_of_tolerance_when_legs_drift()
    {
        // Same square but the last leg is slightly short → ~0.1 m residual on the closing edge.
        var legs = new[]
        {
            (Bearing.Parse("N 90 0 0 E"), 10.0),
            (Bearing.Parse("S 0  0 0 E"), 10.0),
            (Bearing.Parse("S 90 0 0 W"), 10.0),
            (Bearing.Parse("N 0  0 0 E"),  9.9),
        };
        var result = CivilParcel.Traverse(new Point2dDto(0, 0), legs, toleranceM: 0.02);
        Assert.False(result.WithinTolerance);
        Assert.InRange(result.ClosureErrorM, 0.05, 0.2);
    }
}
