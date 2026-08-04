// The same contract as CatalogContractTests, applied to a properties dictionary.
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

public class DimStylePropertyTests
{
    [Fact]
    public void Every_advertised_property_resolves()
    {
        Assert.NotEmpty(DimStyleProperties.All);

        var rejected = DimStyleProperties.All
            .Where(p => !CanResolve(p))
            .Select(p => p.Name)
            .ToList();

        Assert.True(rejected.Count == 0,
            $"list_dimstyle_properties advertises {DimStyleProperties.All.Count} properties; " +
            $"create_dimstyle rejects {rejected.Count} of them: {string.Join(", ", rejected)}");
    }

    [Fact]
    public void Names_list_and_All_agree()
    {
        Assert.Equal(
            DimStyleProperties.All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            DimStyleProperties.Names);
    }

    [Fact]
    public void Every_property_has_a_dimvar_and_a_real_description()
    {
        foreach (var p in DimStyleProperties.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.DimVar), $"{p.Name} names no DIMVAR.");
            Assert.StartsWith("DIM", p.DimVar, StringComparison.Ordinal);
            Assert.True(p.Description.Length >= 25, $"{p.Name} has a description of {p.Description.Length} chars.");
        }
    }

    [Fact]
    public void No_two_properties_share_a_name_or_a_dimvar()
    {
        var dupName = DimStyleProperties.All.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupName.Count == 0, $"duplicate names: {string.Join(", ", dupName)}");

        var dupVar = DimStyleProperties.All.GroupBy(p => p.DimVar, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupVar.Count == 0, $"two properties write the same DIMVAR: {string.Join(", ", dupVar)}");
    }

    [Fact]
    public void Ranges_are_the_right_way_round_and_admit_a_value()
    {
        foreach (var p in DimStyleProperties.All)
        {
            if (p.Min is double min && p.Max is double max)
            {
                Assert.True(min <= max, $"{p.Name} has min {min} above max {max}.");
                // A range no value satisfies would make the property advertised and unusable.
                DimStyleProperties.Resolve(p.Name, Math.Floor((min + max) / 2) is var mid && mid >= min ? mid : min);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dimtxt")]          // the DIMVAR itself is NOT the wire name
    [InlineData("nosuchproperty")]
    public void An_unknown_property_is_an_error_naming_the_known_ones(string name)
    {
        var ex = Assert.Throws<CatalogNameException>(() => DimStyleProperties.Resolve(name, 1.0));
        Assert.Contains("textHeight", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Out_of_range_values_are_refused_with_the_bound()
    {
        var ex = Assert.Throws<CatalogNameException>(() => DimStyleProperties.Resolve("decimalPlaces", 99));
        Assert.Contains("at most", ex.Message, StringComparison.Ordinal);
        Assert.Contains("DIMDEC", ex.Message, StringComparison.Ordinal);

        var ex2 = Assert.Throws<CatalogNameException>(() => DimStyleProperties.Resolve("textHeight", -1));
        Assert.Contains("at least", ex2.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enumerated_and_colour_properties_refuse_a_fraction()
    {
        // DIMDEC, DIMZIN, DIMTAD and the colour indices are Int16 on the record. A fractional
        // value silently truncating would set something the caller did not ask for.
        Assert.Throws<CatalogNameException>(() => DimStyleProperties.Resolve("decimalPlaces", 2.5));
        Assert.Throws<CatalogNameException>(() => DimStyleProperties.Resolve("textColor", 7.5));

        // A genuine number property must still accept one.
        var ok = DimStyleProperties.Resolve("textHeight", 2.5);
        Assert.Equal("DIMTXT", ok.DimVar);
    }

    [Fact]
    public void Names_are_matched_case_insensitively()
    {
        Assert.Equal("DIMTXT", DimStyleProperties.Resolve("TEXTHEIGHT", 2.5).DimVar);
        Assert.Equal("DIMSCALE", DimStyleProperties.Resolve("Scale", 50).DimVar);
    }

    private static bool CanResolve(DimStyleProperty p)
    {
        try
        {
            var v = p.Min ?? 1.0;
            if (p.Kind != DimPropKind.Number) v = Math.Ceiling(v);
            DimStyleProperties.Resolve(p.Name, v);
            return true;
        }
        catch (CatalogNameException)
        {
            return false;
        }
    }
}
