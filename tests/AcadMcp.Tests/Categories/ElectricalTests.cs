// Pluginless tests for acad-electrical (Phase 6.4). Asserts the source
// generator wired the 12 expected tools, the palette ships the 12-layer
// E-* key with the right metadata, electrical_health is pluginless+readonly,
// and the IEC 81346 device-tag parser accepts/rejects the right inputs.

using System;
using System.Linq;
using AcadMcp.Backend.Categories.Electrical;
using AcadMcp.Backend.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class ElectricalTests
{
    private static readonly string[] ExpectedTools =
    {
        "ensure_electrical_layers",
        "draw_ladder_rails",
        "draw_ladder_rung",
        "draw_wire",
        "draw_wire_junction",
        "place_resistor",
        "place_contact_no",
        "place_contact_nc",
        "place_coil",
        "place_terminal_block",
        "place_device_tag",
        "electrical_health",
    };

    [Fact]
    public void Catalog_contains_all_twelve_electrical_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("electrical").ToList();

        Assert.Equal(ExpectedTools.Length, tools.Count);
        foreach (var name in ExpectedTools)
            Assert.Contains(tools, t => t.Name == name);
    }

    [Fact]
    public void Electrical_palette_ships_iec_jic_layer_key()
    {
        var names = ElectricalPalette.All.Select(s => s.Name).ToHashSet();
        Assert.Contains("E-WIRE",      names);
        Assert.Contains("E-WIRE-PWR",  names);
        Assert.Contains("E-WIRE-CTRL", names);
        Assert.Contains("E-SYMBOL",    names);
        Assert.Contains("E-TERM",      names);
        Assert.Contains("E-LBL-WIRE",  names);
        Assert.Contains("E-LBL-DEV",   names);
        Assert.Contains("E-LBL-RUNG",  names);
        Assert.Contains("E-XREF",      names);
        Assert.Contains("E-PANEL",     names);
        Assert.Equal(12, ElectricalPalette.All.Count);

        // Power rails MUST be the bold red layer per rule 39 §9.
        var pwr = ElectricalPalette.All.Single(s => s.Name == "E-WIRE-PWR");
        Assert.Equal(1, pwr.AciColor);
        Assert.Equal(0.50, pwr.LineweightMm);
    }

    [Fact]
    public void Iec_prefix_table_covers_all_eleven_documented_letters()
    {
        var keys = IecDeviceTagPrefixes.Allowed.Keys.ToHashSet();
        foreach (var c in new[] { 'K', 'Q', 'F', 'S', 'B', 'M', 'T', 'G', 'X', 'W', 'H' })
            Assert.Contains(c, keys);
        Assert.Equal(11, IecDeviceTagPrefixes.Allowed.Count);
    }

    [Fact]
    public void Electrical_health_tool_is_pluginless_and_readonly()
    {
        var registry = new ToolRegistry();
        var health = registry.ToolsFor("electrical").Single(t => t.Name == "electrical_health");
        Assert.True(health.ReadOnly,        "electrical_health must be ReadOnly (rule 19).");
        Assert.False(health.RequiresPlugin, "electrical_health must not require AutoCAD (rule 22).");
    }

    [Theory]
    [InlineData("-K1",            "K", "1")]
    [InlineData("K1",             "K", "1")] // dash inferred per rule 39 §6a
    [InlineData("+CAB1-K1",       "K", "1")]
    [InlineData("=PWR+CAB1-K1",   "K", "1")]
    [InlineData("-q12",           "Q", "12")] // lowercase prefix coerced
    [InlineData("-F1A",           "F", "1A")]
    public void DeviceTag_parses_iec_81346_forms(string text, string prefix, string sequence)
    {
        var tag = DeviceTag.Parse(text);
        Assert.Equal(prefix[0], tag.Prefix);
        Assert.Equal(sequence,  tag.Sequence);
    }

    [Theory]
    [InlineData("-A1")]   // 'A' not in IEC 81346 set
    [InlineData("-Z99")]  // 'Z' not in set
    [InlineData("")]      // empty
    [InlineData("--K1")]  // double dash
    public void DeviceTag_rejects_invalid_input(string text)
    {
        Assert.ThrowsAny<Exception>(() => DeviceTag.Parse(text));
    }

    [Fact]
    public void DeviceTag_round_trips_through_canonical_form()
    {
        var tag = DeviceTag.Parse("=PWR+CAB1-K12");
        Assert.Equal("=PWR+CAB1-K12", tag.Canonical);

        var minimal = DeviceTag.Parse("K1");
        Assert.Equal("-K1", minimal.Canonical);
    }
}
