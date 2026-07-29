// Smoke test for acad-grids category (Phase D D7).
// Asserts that all 6 composite tools are registered.

using System.Linq;
using AcadMcp.Backend.Categories.Grids;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class GridsTests
{
    [Fact]
    public void Catalog_contains_all_six_grid_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("grids");
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Equal(6, tools.Count);
        Assert.Contains("draw_grid", names);
        Assert.Contains("add_grid_axis", names);
        Assert.Contains("add_grid_bubble", names);
        Assert.Contains("list_grid_axes", names);
        Assert.Contains("snap_to_grid", names);
        Assert.Contains("delete_grid", names);
    }

    [Fact]
    public void LetterLabel_produces_spreadsheet_style_column_names()
    {
        Assert.Equal("A", GridsPalette.LetterLabel(0));
        Assert.Equal("Z", GridsPalette.LetterLabel(25));
        Assert.Equal("AA", GridsPalette.LetterLabel(26));
        Assert.Equal("AB", GridsPalette.LetterLabel(27));
        Assert.Equal("AZ", GridsPalette.LetterLabel(51));
        Assert.Equal("BA", GridsPalette.LetterLabel(52));
    }

    [Fact]
    public void CumulativeOffsets_starts_at_zero_and_sums_spacings()
    {
        var offsets = GridsPalette.CumulativeOffsets(new[] { 7200.0, 7200.0, 3600.0 });
        Assert.Equal(4, offsets.Count);
        Assert.Equal(0.0,    offsets[0]);
        Assert.Equal(7200.0, offsets[1]);
        Assert.Equal(14400.0, offsets[2]);
        Assert.Equal(18000.0, offsets[3]);
    }

    [Fact]
    public void SnapToGrid_picks_nearest_intersection_and_labels_it()
    {
        var result = GridsTools.SnapToGrid(new SnapToGridArgs(
            Point: new Point2dDto(14200.0, 7100.0),
            Origin: new Point2dDto(0, 0),
            XSpacingsMm: new[] { 7200.0, 7200.0 },
            YSpacingsMm: new[] { 7200.0, 7200.0 }));

        Assert.Equal("C", result.XLabel);
        Assert.Equal("2", result.YLabel);
        Assert.Equal("C/2", result.CellLabel);
        Assert.Equal(14400.0, result.Snapped.X);
        Assert.Equal(7200.0, result.Snapped.Y);
    }
}
