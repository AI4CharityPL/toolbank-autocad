using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>One sanitary fixture the catalogue publishes. Dimensions are millimetres.</summary>
/// <param name="Accessible">
/// Whether the fixture satisfies the barrier-free clearances of its cited standard. Carried
/// on the entry rather than inferred from the name, because it drives both the drawn
/// clearance markers and the accessibility audit.
/// </param>
public sealed record PlumbingCatalogEntry(
    string Name,
    string Category,
    string Domain,
    double WidthMm,
    double DepthMm,
    bool Accessible,
    string Standard,
    string Description);

/// <summary>What <see cref="PlumbingCatalog.Resolve"/> decided a caller's name meant.</summary>
public sealed record PlumbingResolution(
    CatalogMatch Match,
    string Family,
    double WidthMm,
    double DepthMm,
    bool Accessible,
    PlumbingCatalogEntry Entry);

/// <summary>
/// The sanitary fixture catalogue, and the single place a caller-supplied name is turned into
/// a family plus dimensions.
/// </summary>
/// <remarks>
/// Same contract as <see cref="FurnitureCatalog"/>, and the same history: the listing
/// published <c>PLMB-BSN-ACC</c> with its width and depth, while the insert tool demanded a
/// <c>-W-D</c> suffix and rejected it. 6 of 14 published names — the accessible basin, both
/// showers and all three bathtubs — could not be inserted. <c>CatalogContractTests</c> now
/// asserts the agreement.
/// </remarks>
public static class PlumbingCatalog
{
    /// <summary>Entries drawn from a bespoke recipe, always at their published size.</summary>
    public static IReadOnlyList<PlumbingCatalogEntry> Fixed { get; } = new List<PlumbingCatalogEntry>
    {
        new("PLMB-WC-FS",   "wc",     "residential",  370,  650, false, "PN-EN 997",        "Floor-standing WC"),
        new("PLMB-WC-WH",   "wc",     "residential",  370,  540, false, "PN-EN 997",        "Wall-hung WC"),
        new("PLMB-WC-BID",  "wc",     "residential",  370,  550, false, "PN-EN 14528",      "Bidet-combo WC"),
        new("PLMB-WC-ACC",  "wc",     "universal",    800,  800, true,  "PN-EN 17210 §T.4", "Accessible WC with grab-bar markers"),
        new("PLMB-BSN-STD", "basin",  "residential",  600,  450, false, "PN-EN 14688",      "Standard wash basin"),
        new("PLMB-BSN-DBL", "basin",  "residential", 1200,  450, false, "PN-EN 14688",      "Double wash basin"),
        new("PLMB-UR-STD",  "urinal", "office",       380,  340, false, "PN-EN 13407",      "Standard wall-hung urinal"),
        new("PLMB-UR-ACC",  "urinal", "universal",    380,  450, true,  "PN-EN 17210 §U.4", "Accessible lower-rim urinal"),
    };

    /// <summary>Families drawn parametrically; the dimensions here are defaults.</summary>
    public static IReadOnlyList<PlumbingCatalogEntry> SizedFamilies { get; } = new List<PlumbingCatalogEntry>
    {
        new("PLMB-BSN-ACC",     "basin",   "universal",    700,  550, true,  "PN-EN 17210 §U.2", "Accessible basin (knee clearance)"),
        new("PLMB-SHW-SQ",      "shower",  "residential",  900,  900, false, "PN-EN 14527",      "Square shower tray"),
        new("PLMB-SHW-WI",      "shower",  "universal",   1200,  900, true,  "PN-EN 17210 §S.3", "Walk-in barrier-free shower"),
        new("PLMB-BT-STANDARD", "bathtub", "residential", 1700,  700, false, "PN-EN 232",        "Standard rectangular bathtub"),
        new("PLMB-BT-MINI",     "bathtub", "residential", 1500,  700, false, "PN-EN 232",        "Mini rectangular bathtub"),
        new("PLMB-BT-CORNER",   "bathtub", "residential", 1400, 1400, false, "PN-EN 232",        "Corner quarter-round bathtub"),
    };

    /// <summary>Exactly what <c>list_plumbing_catalog</c> publishes, filters applied.</summary>
    public static IReadOnlyList<PlumbingCatalogEntry> All(
        string? categoryFilter = null, string? domainFilter = null, bool accessibleOnly = false)
    {
        IEnumerable<PlumbingCatalogEntry> merged = Fixed.Concat(SizedFamilies);

        if (!string.IsNullOrWhiteSpace(categoryFilter))
            merged = merged.Where(e => string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(domainFilter))
            merged = merged.Where(e => string.Equals(e.Domain, domainFilter, StringComparison.OrdinalIgnoreCase));
        if (accessibleOnly)
            merged = merged.Where(e => e.Accessible);

        return merged
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Turn a caller-supplied block name into a family, dimensions and accessibility flag.
    /// Accepts every name <see cref="All"/> publishes.
    /// </summary>
    /// <exception cref="CatalogNameException">The name matches nothing in the catalogue.</exception>
    public static PlumbingResolution Resolve(string name, double? overrideWidthMm = null, double? overrideDepthMm = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameException("Block name is required. Names accepted here are exactly those returned by list_plumbing_catalog.");

        var fixedHit = Fixed.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (fixedHit is not null)
        {
            return new PlumbingResolution(
                CatalogMatch.Fixed, fixedHit.Name, fixedHit.WidthMm, fixedHit.DepthMm, fixedHit.Accessible, fixedHit);
        }

        var bare = SizedFamilies.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (bare is not null)
        {
            return new PlumbingResolution(
                CatalogMatch.FamilyDefaults,
                bare.Name,
                overrideWidthMm ?? bare.WidthMm,
                overrideDepthMm ?? bare.DepthMm,
                bare.Accessible,
                bare);
        }

        if (CatalogNaming.TrySplitSized(name, out var family, out var wMm, out var dMm))
        {
            var famHit = SizedFamilies.FirstOrDefault(e => string.Equals(e.Name, family, StringComparison.OrdinalIgnoreCase));
            if (famHit is not null)
            {
                return new PlumbingResolution(
                    CatalogMatch.SizedSuffix,
                    famHit.Name,
                    overrideWidthMm ?? wMm,
                    overrideDepthMm ?? dMm,
                    famHit.Accessible,
                    famHit);
            }

            throw new CatalogNameException(
                $"Block '{name}' parses as family '{family}' with size {wMm}x{dMm} mm, but '{family}' is not a known " +
                "sized family. Names accepted here are exactly those returned by list_plumbing_catalog.");
        }

        throw new CatalogNameException(
            $"Block '{name}' is neither in the fixed catalog, a known sized family, nor a sized-family name " +
            "(expected format PLMB-FAMILY-SUBTYPE-W-D). Names accepted here are exactly those returned by " +
            "list_plumbing_catalog.");
    }
}
