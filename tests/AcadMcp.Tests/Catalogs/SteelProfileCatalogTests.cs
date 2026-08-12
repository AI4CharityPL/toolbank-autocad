// The catalogue-vs-consumer contract for acad-structural, mirroring CatalogContractTests.cs's
// discipline for FurnitureCatalog/PlumbingCatalog: every name list_steel_profiles publishes
// must be exactly what insert_steel_column/insert_beam accept. Unlike those two catalogues,
// SteelProfileCatalog has no sized-family form - a designation like "HEB200" names one fixed,
// standardised cross-section, not a caller-resizable family - so there is no suffix-parsing
// contract to test here, only the resolve-what-you-publish one plus the geometric invariants
// the drawn no-fillet outline (rule 72 §4) depends on.

using System;
using System.Linq;
using AcadMcp.Shared.Catalogs;
using Xunit;

namespace AcadMcp.Tests.Catalogs;

public class SteelProfileCatalogTests
{
    [Fact]
    public void Every_published_designation_can_be_resolved()
    {
        var published = SteelProfileCatalog.All;
        Assert.NotEmpty(published);

        var rejected = published
            .Where(e => !CanResolve(e.Designation))
            .Select(e => e.Designation)
            .ToList();

        Assert.True(rejected.Count == 0,
            $"list_steel_profiles publishes {published.Count} designations; Resolve rejects " +
            $"{rejected.Count} of them: {string.Join(", ", rejected)}");
    }

    [Fact]
    public void Resolving_a_designation_yields_the_published_entry_unchanged()
    {
        foreach (var e in SteelProfileCatalog.All)
        {
            var r = SteelProfileCatalog.Resolve(e.Designation);
            Assert.Equal(e.Designation, r.Designation);
            Assert.Same(e, r.Entry);
        }
    }

    [Fact]
    public void Designations_are_matched_case_insensitively()
    {
        Assert.Equal("HEB200", SteelProfileCatalog.Resolve("heb200").Designation);
        Assert.Equal("IPE300", SteelProfileCatalog.Resolve("ipe300").Designation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("HEB999")]
    [InlineData("completely wrong")]
    public void An_unknown_designation_is_an_error_not_a_default(string designation)
    {
        var ex = Assert.Throws<CatalogNameException>(() => SteelProfileCatalog.Resolve(designation));
        Assert.Contains("list_steel_profiles", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_designation_is_published_twice()
    {
        var duplicates = SteelProfileCatalog.All
            .GroupBy(e => e.Designation, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate designation(s): {string.Join(", ", duplicates)}");
    }

    // ─────────────── geometric invariants the drawn I/H outline depends on ───────────────
    //
    // insert_steel_column (rule 72 §4) draws a 12-vertex I/H outline computed from these exact
    // fields - if a bad entry ever had a flange thicker than half the section height, or a web
    // wider than the section itself, the drawn polygon would self-intersect.

    [Fact]
    public void Every_entry_has_room_for_a_web_between_its_flanges()
    {
        foreach (var e in SteelProfileCatalog.All)
            Assert.True(e.HeightMm > 2 * e.FlangeThicknessMm,
                $"{e.Designation}: height {e.HeightMm}mm does not exceed 2x flange thickness {e.FlangeThicknessMm}mm.");
    }

    [Fact]
    public void Every_entry_has_a_web_narrower_than_the_section()
    {
        foreach (var e in SteelProfileCatalog.All)
            Assert.True(e.WidthMm > e.WebThicknessMm,
                $"{e.Designation}: width {e.WidthMm}mm does not exceed web thickness {e.WebThicknessMm}mm.");
    }

    [Fact]
    public void Every_entry_has_positive_dimensions_weight_area_and_a_cited_standard()
    {
        foreach (var e in SteelProfileCatalog.All)
        {
            Assert.True(e.HeightMm > 0 && e.WidthMm > 0 && e.WebThicknessMm > 0 && e.FlangeThicknessMm > 0,
                $"{e.Designation} has a non-positive cross-section dimension.");
            Assert.True(e.WeightKgPerM > 0, $"{e.Designation} has a non-positive weight per metre.");
            Assert.True(e.AreaCm2 > 0, $"{e.Designation} has a non-positive area.");
            Assert.False(string.IsNullOrWhiteSpace(e.Standard), $"{e.Designation} cites no standard.");
            Assert.False(string.IsNullOrWhiteSpace(e.Description), $"{e.Designation} has no description.");
        }
    }

    [Fact]
    public void AreaCm2_matches_the_no_fillet_formula_the_drawn_outline_uses()
    {
        // Same formula rule 72 §4 documents insert_steel_column drawing - keeps the catalogue
        // and the geometry from silently diverging (see the rule's own warning about this).
        foreach (var e in SteelProfileCatalog.All)
        {
            double expected = (2.0 * e.WidthMm * e.FlangeThicknessMm +
                                (e.HeightMm - 2.0 * e.FlangeThicknessMm) * e.WebThicknessMm) / 100.0;
            Assert.True(Math.Abs(expected - e.AreaCm2) < 0.01,
                $"{e.Designation}: catalogued area {e.AreaCm2}cm^2 does not match the no-fillet formula ({expected:F2}cm^2).");
        }
    }

    [Fact]
    public void Filtering_by_series_narrows_the_listing_without_inventing_entries()
    {
        var all = SteelProfileCatalog.All;
        var heb = SteelProfileCatalog.Filtered(seriesFilter: "HEB");

        Assert.NotEmpty(heb);
        Assert.True(heb.Count < all.Count, "The HEB filter matched everything, so it is not filtering.");
        Assert.All(heb, e => Assert.Equal("HEB", e.Series, ignoreCase: true));
        Assert.All(heb, e => Assert.Contains(e, all));
    }

    private static bool CanResolve(string designation)
    {
        try
        {
            SteelProfileCatalog.Resolve(designation);
            return true;
        }
        catch (CatalogNameException)
        {
            return false;
        }
    }
}
