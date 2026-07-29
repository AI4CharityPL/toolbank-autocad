// Smoke tests for the acad-schedules category (Phase D D8).
// Asserts that all 5 composite tools are registered and that the palette
// presets and finish-legend rows are wired correctly.

using System.Linq;
using AcadMcp.Backend.Categories.Schedules;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class SchedulesTests
{
    [Fact]
    public void Catalog_contains_all_schedule_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("schedules");
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Equal(9, tools.Count);
        Assert.Contains("generate_door_schedule", names);
        Assert.Contains("generate_window_schedule", names);
        Assert.Contains("generate_room_schedule", names);
        Assert.Contains("generate_finish_legend", names);
        Assert.Contains("update_schedules", names);
        Assert.Contains("get_room_data", names);
        Assert.Contains("correct_room_area", names);
        Assert.Contains("audit_all_rooms", names);
        Assert.Contains("correct_all_room_areas", names);
    }

    [Fact]
    public void Palette_exposes_hospital_and_office_presets()
    {
        Assert.True(SchedulesPalette.Presets.ContainsKey(SchedulesPalette.StyleHospital));
        Assert.True(SchedulesPalette.Presets.ContainsKey(SchedulesPalette.StyleOffice));

        var hosp = SchedulesPalette.Presets[SchedulesPalette.StyleHospital];
        Assert.Equal(5.0, hosp.TitleTextHeight);
        Assert.Equal(3.5, hosp.HeaderTextHeight);
        Assert.Equal(2.5, hosp.BodyTextHeight);
        Assert.Equal(1, hosp.TitleFillAci);

        var off = SchedulesPalette.Presets[SchedulesPalette.StyleOffice];
        Assert.Equal(5, off.TitleFillAci);
    }

    [Fact]
    public void Palette_column_counts_match_header_counts()
    {
        Assert.Equal(SchedulesPalette.DoorHeaders.Count,   SchedulesPalette.DoorCols.Count);
        Assert.Equal(SchedulesPalette.WindowHeaders.Count, SchedulesPalette.WindowCols.Count);
        Assert.Equal(SchedulesPalette.RoomHeaders.Count,   SchedulesPalette.RoomCols.Count);
        Assert.Equal(SchedulesPalette.FinishHeaders.Count, SchedulesPalette.FinishCols.Count);
    }

    [Fact]
    public void Default_finish_rows_match_header_width()
    {
        int expected = SchedulesPalette.FinishHeaders.Count;
        foreach (var row in SchedulesPalette.DefaultFinishRows)
        {
            Assert.Equal(expected, row.Count);
        }
        Assert.True(SchedulesPalette.DefaultFinishRows.Count >= 10,
            "Finish legend should ship with at least 10 hospital-grade entries.");
    }

    [Fact]
    public void Titles_are_polish_and_non_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(SchedulesPalette.TitleDoors));
        Assert.Contains("STOLARKI DRZWIOWEJ", SchedulesPalette.TitleDoors);
        Assert.Contains("STOLARKI OKIENNEJ", SchedulesPalette.TitleWindows);
        Assert.Contains("POMIESZCZEŃ",       SchedulesPalette.TitleRooms);
        Assert.Contains("WYKOŃCZEŃ",         SchedulesPalette.TitleFinish);
    }
}
