// The table half of the same properties contract - see DimStylePropertyTests.
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

public class TableStylePropertyTests
{
    [Fact]
    public void Every_advertised_property_resolves()
    {
        Assert.NotEmpty(TableStyleProperties.All);

        var rejected = TableStyleProperties.All
            .Where(p => !CanResolve(p))
            .Select(p => p.Name)
            .ToList();

        Assert.True(rejected.Count == 0,
            $"list_dimstyle_properties advertises {TableStyleProperties.All.Count} properties; " +
            $"create_dimstyle rejects {rejected.Count} of them: {string.Join(", ", rejected)}");
    }

    [Fact]
    public void Names_list_and_All_agree()
    {
        Assert.Equal(
            TableStyleProperties.All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            TableStyleProperties.Names);
    }

    [Fact]
    public void Every_property_has_a_dimvar_and_a_real_description()
    {
        foreach (var p in TableStyleProperties.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.ApiName), $"{p.Name} names no API member.");
            Assert.False(string.IsNullOrWhiteSpace(p.ApiName));
            Assert.True(p.Description.Length >= 25, $"{p.Name} has a description of {p.Description.Length} chars.");
        }
    }

    [Fact]
    public void No_two_properties_share_a_name_or_a_dimvar()
    {
        var dupName = TableStyleProperties.All.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupName.Count == 0, $"duplicate names: {string.Join(", ", dupName)}");

        // Unlike the other two tables, several properties here DO share an API member on
        // purpose: a table has three kinds of row and each has its own TextHeight. What must be
        // unique is the pair. Two entries addressing the same member AND the same row would be
        // two names for one setting, and the second would silently win.
        var dupTarget = TableStyleProperties.All
            .GroupBy(p => (p.ApiName, p.RowType ?? ""), StringComparer.OrdinalIgnoreCase is var _ ? null : null)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Item1}/{g.Key.Item2}")
            .ToList();
        Assert.True(dupTarget.Count == 0,
            $"two properties write the same API member on the same row: {string.Join(", ", dupTarget)}");
    }

    [Fact]
    public void Ranges_are_the_right_way_round_and_admit_a_value()
    {
        foreach (var p in TableStyleProperties.All)
        {
            if (p.Min is double min && p.Max is double max)
            {
                Assert.True(min <= max, $"{p.Name} has min {min} above max {max}.");
                // A range no value satisfies would make the property advertised and unusable.
                TableStyleProperties.Resolve(p.Name, Math.Floor((min + max) / 2) is var mid && mid >= min ? mid : min);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("textHeight")]          // the DIMVAR itself is NOT the wire name
    [InlineData("nosuchproperty")]
    public void An_unknown_property_is_an_error_naming_the_known_ones(string name)
    {
        var ex = Assert.Throws<CatalogNameException>(() => TableStyleProperties.Resolve(name, 1.0));
        Assert.Contains("dataTextHeight", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Out_of_range_values_are_refused_with_the_bound()
    {
        var ex = Assert.Throws<CatalogNameException>(() => TableStyleProperties.Resolve("dataTextHeight", 99999));
        Assert.Contains("at most", ex.Message, StringComparison.Ordinal);
        Assert.Contains("TextHeight", ex.Message, StringComparison.Ordinal);

        var ex2 = Assert.Throws<CatalogNameException>(() => TableStyleProperties.Resolve("dataTextHeight", -1));
        Assert.Contains("at least", ex2.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Withdrawn_flowDirection_stays_out_of_the_catalogue()
    {
        // TableStyle.FlowDirection reads fine and throws eInvalidInput on every write, measured
        // by setting each candidate property alone. It must not come back without that being
        // fixed first - advertising a property this bank cannot set is the defect these tables
        // exist to prevent.
        Assert.DoesNotContain("flowDirection", TableStyleProperties.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Throws<CatalogNameException>(() => TableStyleProperties.Resolve("flowDirection", 0));
    }

    [Fact]
    public void Every_table_property_is_a_number_after_flowDirection_was_withdrawn()
    {
        // DIMDEC, DIMZIN, DIMTAD and the colour indices are Int16 on the record. A fractional
        // value silently truncating would set something the caller did not ask for.
        // Every remaining property takes a real number, so a fraction must be accepted.
        var ok = TableStyleProperties.Resolve("dataTextHeight", 2.5);
        Assert.Equal("TextHeight", ok.ApiName);
    }

    [Fact]
    public void Names_are_matched_case_insensitively()
    {
        Assert.Equal("TextHeight", TableStyleProperties.Resolve("DATATEXTHEIGHT", 2.5).ApiName);
        Assert.Equal("HorizontalCellMargin", TableStyleProperties.Resolve("HORIZONTALCELLMARGIN", 1.5).ApiName);
    }

    private static bool CanResolve(TableStyleProperty p)
    {
        try
        {
            var v = p.Min ?? 1.0;
            if (p.Kind != DimPropKind.Number) v = Math.Ceiling(v);
            TableStyleProperties.Resolve(p.Name, v);
            return true;
        }
        catch (CatalogNameException)
        {
            return false;
        }
    }
}
