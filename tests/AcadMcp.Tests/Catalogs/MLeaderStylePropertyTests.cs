// The multileader half of the same properties contract - see DimStylePropertyTests.
// Kept as a separate file rather than a shared generic: when one of the two tables changes
// shape, the diff should say which.
//
// A "pass me a map of property names to values" argument is the shape that produced four
// catalogue-advertises-what-the-tool-refuses defects in an earlier review. The property table
// lives in AcadMcp.Shared precisely so this can run on every push rather than only when
// somebody has AutoCAD open.

using System;
using System.Linq;
using AcadMcp.Shared.Catalogs;
using Xunit;

namespace AcadMcp.Tests.Catalogs;

public class MLeaderStylePropertyTests
{
    [Fact]
    public void Every_advertised_property_resolves()
    {
        Assert.NotEmpty(MLeaderStyleProperties.All);

        var rejected = MLeaderStyleProperties.All
            .Where(p => !CanResolve(p))
            .Select(p => p.Name)
            .ToList();

        Assert.True(rejected.Count == 0,
            $"list_dimstyle_properties advertises {MLeaderStyleProperties.All.Count} properties; " +
            $"create_dimstyle rejects {rejected.Count} of them: {string.Join(", ", rejected)}");
    }

    [Fact]
    public void Names_list_and_All_agree()
    {
        Assert.Equal(
            MLeaderStyleProperties.All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            MLeaderStyleProperties.Names);
    }

    [Fact]
    public void Every_property_has_a_dimvar_and_a_real_description()
    {
        foreach (var p in MLeaderStyleProperties.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.ApiName), $"{p.Name} names no API member.");
            Assert.False(string.IsNullOrWhiteSpace(p.ApiName));
            Assert.True(p.Description.Length >= 25, $"{p.Name} has a description of {p.Description.Length} chars.");
        }
    }

    [Fact]
    public void No_two_properties_share_a_name_or_a_dimvar()
    {
        var dupName = MLeaderStyleProperties.All.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupName.Count == 0, $"duplicate names: {string.Join(", ", dupName)}");

        var dupVar = MLeaderStyleProperties.All.GroupBy(p => p.ApiName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupVar.Count == 0, $"two properties write the same API member: {string.Join(", ", dupVar)}");
    }

    [Fact]
    public void Ranges_are_the_right_way_round_and_admit_a_value()
    {
        foreach (var p in MLeaderStyleProperties.All)
        {
            if (p.Min is double min && p.Max is double max)
            {
                Assert.True(min <= max, $"{p.Name} has min {min} above max {max}.");
                // A range no value satisfies would make the property advertised and unusable.
                MLeaderStyleProperties.Resolve(p.Name, Math.Floor((min + max) / 2) is var mid && mid >= min ? mid : min);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TextHeight_wrong_case_is_fine_but_this_is_not_a_name")]          // the DIMVAR itself is NOT the wire name
    [InlineData("nosuchproperty")]
    public void An_unknown_property_is_an_error_naming_the_known_ones(string name)
    {
        var ex = Assert.Throws<CatalogNameException>(() => MLeaderStyleProperties.Resolve(name, 1.0));
        Assert.Contains("textHeight", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Out_of_range_values_are_refused_with_the_bound()
    {
        var ex = Assert.Throws<CatalogNameException>(() => MLeaderStyleProperties.Resolve("maxLeaderPoints", 99));
        Assert.Contains("at most", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MaxLeaderSegmentsPoints", ex.Message, StringComparison.Ordinal);

        var ex2 = Assert.Throws<CatalogNameException>(() => MLeaderStyleProperties.Resolve("textHeight", -1));
        Assert.Contains("at least", ex2.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enumerated_and_colour_properties_refuse_a_fraction()
    {
        // DIMDEC, DIMZIN, DIMTAD and the colour indices are Int16 on the record. A fractional
        // value silently truncating would set something the caller did not ask for.
        Assert.Throws<CatalogNameException>(() => MLeaderStyleProperties.Resolve("maxLeaderPoints", 2.5));
        Assert.Throws<CatalogNameException>(() => MLeaderStyleProperties.Resolve("enableDogleg", 0.5));

        // A genuine number property must still accept one.
        var ok = MLeaderStyleProperties.Resolve("textHeight", 2.5);
        Assert.Equal("TextHeight", ok.ApiName);
    }

    [Fact]
    public void Names_are_matched_case_insensitively()
    {
        Assert.Equal("TextHeight", MLeaderStyleProperties.Resolve("TEXTHEIGHT", 2.5).ApiName);
        Assert.Equal("Scale", MLeaderStyleProperties.Resolve("Scale", 50).ApiName);
    }

    private static bool CanResolve(MLeaderStyleProperty p)
    {
        try
        {
            var v = p.Min ?? 1.0;
            if (p.Kind != DimPropKind.Number) v = Math.Ceiling(v);
            MLeaderStyleProperties.Resolve(p.Name, v);
            return true;
        }
        catch (CatalogNameException)
        {
            return false;
        }
    }
}
