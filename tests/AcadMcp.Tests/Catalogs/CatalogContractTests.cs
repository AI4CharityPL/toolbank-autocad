// The catalogue-vs-consumer contract.
//
// The rule these tests enforce is one sentence: every name a discovery tool publishes must
// be accepted by the action tool it points at.
//
// This was the single highest-value missing test in docs/KNOWN-GAPS.md, because four
// separate defects in one review were violations of it and each was found by hand. Two of
// them are the ones covered here: list_furniture_catalog and list_plumbing_catalog publish
// family names together with default dimensions, while insert_furniture and insert_plumbing
// went straight to parsing a "-W-D" suffix and threw before any family lookup. 11 of 26
// furniture names and 6 of 14 plumbing names were uninsertable through the tool the listing
// told the agent to use.
//
// Note what these tests do NOT do: they never draw anything. Turning a resolution into
// geometry needs AutoCAD, and no CI runner has it. Deciding what a name means does not, so
// that half lives in AcadMcp.Shared and is tested on every push. Splitting the two is what
// makes this contract enforceable at all.

using System;
using System.Linq;
using AcadMcp.Shared.Catalogs;
using Xunit;

namespace AcadMcp.Tests.Catalogs;

public class CatalogContractTests
{
    // ───────────────────────── the load-bearing pair ─────────────────────────

    [Fact]
    public void Every_published_furniture_name_can_be_resolved()
    {
        var published = FurnitureCatalog.All();
        Assert.NotEmpty(published);

        var rejected = published
            .Where(e => !CanResolve(() => FurnitureCatalog.Resolve(e.Name)))
            .Select(e => e.Name)
            .ToList();

        Assert.True(rejected.Count == 0,
            $"list_furniture_catalog publishes {published.Count} names; insert_furniture rejects " +
            $"{rejected.Count} of them: {string.Join(", ", rejected)}");
    }

    [Fact]
    public void Every_published_plumbing_name_can_be_resolved()
    {
        var published = PlumbingCatalog.All();
        Assert.NotEmpty(published);

        var rejected = published
            .Where(e => !CanResolve(() => PlumbingCatalog.Resolve(e.Name)))
            .Select(e => e.Name)
            .ToList();

        Assert.True(rejected.Count == 0,
            $"list_plumbing_catalog publishes {published.Count} names; insert_plumbing rejects " +
            $"{rejected.Count} of them: {string.Join(", ", rejected)}");
    }

    // ─────────────── the published dimensions must be the real ones ───────────────
    //
    // Resolving without throwing is not enough. The listing publishes a width and depth next
    // to each name, and an agent will believe them. If resolution produced different numbers
    // the drawing would silently disagree with the schedule that quoted the catalogue.

    [Fact]
    public void Resolving_a_bare_furniture_name_yields_the_published_dimensions()
    {
        foreach (var e in FurnitureCatalog.All())
        {
            var r = FurnitureCatalog.Resolve(e.Name);
            Assert.Equal(e.WidthMm, r.WidthMm);
            Assert.Equal(e.DepthMm, r.DepthMm);
        }
    }

    [Fact]
    public void Resolving_a_bare_plumbing_name_yields_the_published_dimensions_and_accessibility()
    {
        foreach (var e in PlumbingCatalog.All())
        {
            var r = PlumbingCatalog.Resolve(e.Name);
            Assert.Equal(e.WidthMm, r.WidthMm);
            Assert.Equal(e.DepthMm, r.DepthMm);
            Assert.Equal(e.Accessible, r.Accessible);
        }
    }

    // ─────────────────────── the three name forms ───────────────────────

    [Fact]
    public void A_fixed_entry_resolves_as_fixed()
    {
        var r = FurnitureCatalog.Resolve("FURN-BED-ICU");
        Assert.Equal(CatalogMatch.Fixed, r.Match);
        Assert.Equal(1000, r.WidthMm);
        Assert.Equal(2200, r.DepthMm);
    }

    [Fact]
    public void A_bare_family_name_resolves_to_the_family_defaults()
    {
        // The exact case that used to throw: published by the listing, rejected by the tool.
        var r = FurnitureCatalog.Resolve("FURN-DESK-OFF");
        Assert.Equal(CatalogMatch.FamilyDefaults, r.Match);
        Assert.Equal("FURN-DESK-OFF", r.Family);
        Assert.Equal(1600, r.WidthMm);
        Assert.Equal(800, r.DepthMm);
    }

    [Fact]
    public void A_sized_suffix_overrides_the_family_defaults()
    {
        var r = FurnitureCatalog.Resolve("FURN-DESK-OFF-1800-900");
        Assert.Equal(CatalogMatch.SizedSuffix, r.Match);
        Assert.Equal("FURN-DESK-OFF", r.Family);
        Assert.Equal(1800, r.WidthMm);
        Assert.Equal(900, r.DepthMm);
    }

    [Fact]
    public void Explicit_arguments_beat_both_the_suffix_and_the_defaults()
    {
        var fromDefaults = FurnitureCatalog.Resolve("FURN-TBL-RECT", overrideWidthMm: 2000);
        Assert.Equal(2000, fromDefaults.WidthMm);
        Assert.Equal(800, fromDefaults.DepthMm);   // untouched default

        var fromSuffix = FurnitureCatalog.Resolve("FURN-TBL-RECT-1200-800", overrideDepthMm: 950);
        Assert.Equal(1200, fromSuffix.WidthMm);    // from the suffix
        Assert.Equal(950, fromSuffix.DepthMm);     // from the caller
    }

    [Fact]
    public void Names_are_matched_case_insensitively()
    {
        // Agents do not reliably preserve case, and rejecting on it would be a bad error.
        Assert.Equal("FURN-DESK-OFF", FurnitureCatalog.Resolve("furn-desk-off").Family);
        Assert.Equal("PLMB-BSN-ACC", PlumbingCatalog.Resolve("plmb-bsn-acc").Family);
    }

    // ─────────────────────── failure is loud ───────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FURN-NOPE-XYZ")]
    [InlineData("FURN-DESK-NOSUCH-1600-800")]   // parses as sized, family unknown
    [InlineData("completely wrong")]
    public void An_unknown_furniture_name_is_an_error_not_a_default(string name)
    {
        var ex = Assert.Throws<CatalogNameException>(() => FurnitureCatalog.Resolve(name));

        // The message has to point at the listing. An agent that gets this back should be
        // able to recover without a human explaining the naming convention to it.
        Assert.Contains("list_furniture_catalog", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PLMB-NOPE-XYZ")]
    [InlineData("PLMB-SHW-NOSUCH-900-900")]
    public void An_unknown_plumbing_name_is_an_error_not_a_default(string name)
    {
        var ex = Assert.Throws<CatalogNameException>(() => PlumbingCatalog.Resolve(name));
        Assert.Contains("list_plumbing_catalog", ex.Message, StringComparison.Ordinal);
    }

    // ─────────────────────── catalogue hygiene ───────────────────────

    [Fact]
    public void No_name_appears_in_both_the_fixed_and_the_sized_list()
    {
        // A name in both lists resolves as Fixed and silently ignores size overrides, which
        // is not what a caller reading the listing would expect.
        AssertNoOverlap(
            FurnitureCatalog.Fixed.Select(e => e.Name),
            FurnitureCatalog.SizedFamilies.Select(e => e.Name),
            "furniture");

        AssertNoOverlap(
            PlumbingCatalog.Fixed.Select(e => e.Name),
            PlumbingCatalog.SizedFamilies.Select(e => e.Name),
            "plumbing");
    }

    [Fact]
    public void No_fixed_entry_name_parses_as_a_sized_name()
    {
        // If a fixed entry's own name looked size-suffixed, callers could not tell which
        // rules applied to it. FURN-SOFA-CLN-2 gets close: it ends in a number.
        foreach (var e in FurnitureCatalog.Fixed.Concat(PlumbingCatalog.Fixed.Select(
                     p => new FurnitureCatalogEntry(p.Name, p.Category, p.Domain, p.WidthMm, p.DepthMm, p.Description))))
        {
            Assert.False(CatalogNaming.TrySplitSized(e.Name, out _, out _, out _),
                $"Fixed entry '{e.Name}' also parses as a sized-family name, which makes its size rules ambiguous.");
        }
    }

    [Fact]
    public void Every_entry_has_positive_dimensions_and_a_real_description()
    {
        foreach (var e in FurnitureCatalog.All())
        {
            Assert.True(e.WidthMm > 0 && e.DepthMm > 0, $"{e.Name} has a non-positive dimension.");
            Assert.False(string.IsNullOrWhiteSpace(e.Description), $"{e.Name} has no description.");
        }

        foreach (var e in PlumbingCatalog.All())
        {
            Assert.True(e.WidthMm > 0 && e.DepthMm > 0, $"{e.Name} has a non-positive dimension.");
            Assert.False(string.IsNullOrWhiteSpace(e.Description), $"{e.Name} has no description.");
            Assert.False(string.IsNullOrWhiteSpace(e.Standard), $"{e.Name} cites no standard.");
        }
    }

    [Fact]
    public void Filters_narrow_the_listing_without_inventing_entries()
    {
        var all = FurnitureCatalog.All();
        var hospital = FurnitureCatalog.All(domainFilter: "hospital");

        Assert.NotEmpty(hospital);
        Assert.True(hospital.Count < all.Count, "The hospital filter matched everything, so it is not filtering.");
        Assert.All(hospital, e => Assert.Equal("hospital", e.Domain, ignoreCase: true));
        Assert.All(hospital, e => Assert.Contains(e, all));

        var accessible = PlumbingCatalog.All(accessibleOnly: true);
        Assert.NotEmpty(accessible);
        Assert.All(accessible, e => Assert.True(e.Accessible));
    }

    // ─────────────────────── hatches: the cross-reference ───────────────────────
    //
    // This catalogue carries a contract the other two do not. A material preset names a
    // pattern, and unlike a furniture dimension that mistake is invisible: list_hatch_patterns
    // would not show the pattern, while apply_material_preset would ask AutoCAD to load it
    // anyway and get whatever AutoCAD does with an unknown pattern name.

    [Fact]
    public void Every_material_preset_points_at_a_pattern_the_catalogue_lists()
    {
        var dangling = HatchCatalog.MaterialPresets.Values
            .Where(p => !HatchCatalog.Patterns.ContainsKey(p.Pattern))
            .Select(p => $"{p.Material} -> {p.Pattern}")
            .ToList();

        Assert.True(dangling.Count == 0,
            $"{dangling.Count} material preset(s) name a pattern that list_hatch_patterns does not " +
            $"publish: {string.Join(", ", dangling)}");
    }

    [Fact]
    public void Every_published_pattern_resolves()
    {
        var published = HatchCatalog.AllPatterns();
        Assert.NotEmpty(published);

        foreach (var e in published)
        {
            var r = HatchCatalog.ResolvePattern(e.Name);
            Assert.Equal(e.Name, r.Name);
        }
    }

    [Fact]
    public void Every_preset_resolves_and_keys_match_their_own_material_name()
    {
        foreach (var kv in HatchCatalog.MaterialPresets)
        {
            var r = HatchCatalog.ResolvePreset(kv.Key);
            Assert.Equal(kv.Value.Pattern, r.Pattern);

            // The dictionary key and the record's own Material field must agree, or an error
            // message quoting one while the caller used the other becomes nonsense.
            Assert.Equal(kv.Key, kv.Value.Material, ignoreCase: true);
        }
    }

    [Fact]
    public void An_unknown_material_preset_lists_the_known_ones()
    {
        var ex = Assert.Throws<CatalogNameException>(() => HatchCatalog.ResolvePreset("unobtainium"));

        // The agent cannot guess; the error has to carry the answer.
        Assert.Contains("concrete", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown material preset", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pattern_and_preset_names_are_matched_case_insensitively()
    {
        Assert.Equal("AR-CONC", HatchCatalog.ResolvePreset("CONCRETE").Pattern);
        Assert.Equal("ANSI31", HatchCatalog.ResolvePattern("ansi31").Name);
    }

    [Fact]
    public void Every_pattern_entry_key_matches_its_own_name()
    {
        foreach (var kv in HatchCatalog.Patterns)
        {
            Assert.Equal(kv.Key, kv.Value.Name, ignoreCase: true);
            Assert.False(string.IsNullOrWhiteSpace(kv.Value.Category), $"{kv.Key} has no category.");
            Assert.False(string.IsNullOrWhiteSpace(kv.Value.Description), $"{kv.Key} has no description.");
            Assert.True(kv.Value.DefaultScale > 0, $"{kv.Key} has a non-positive default scale.");
        }
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static bool CanResolve(Action resolve)
    {
        try
        {
            resolve();
            return true;
        }
        catch (CatalogNameException)
        {
            return false;
        }
    }

    private static void AssertNoOverlap(
        System.Collections.Generic.IEnumerable<string> a,
        System.Collections.Generic.IEnumerable<string> b,
        string label)
    {
        var overlap = a.Intersect(b, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(overlap.Count == 0,
            $"{label}: {overlap.Count} name(s) appear in both the fixed and sized lists: {string.Join(", ", overlap)}");
    }
}
