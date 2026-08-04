using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>One furniture block the catalogue publishes. Dimensions are millimetres.</summary>
public sealed record FurnitureCatalogEntry(
    string Name,
    string Category,
    string Domain,
    double WidthMm,
    double DepthMm,
    string Description);

/// <summary>What <see cref="FurnitureCatalog.Resolve"/> decided a caller's name meant.</summary>
/// <param name="Match">Which of the three name forms was recognised.</param>
/// <param name="Family">
/// The family to draw. For <see cref="CatalogMatch.Fixed"/> this is the entry name itself.
/// </param>
/// <param name="Entry">The catalogue entry behind the match; never null.</param>
public sealed record FurnitureResolution(
    CatalogMatch Match,
    string Family,
    double WidthMm,
    double DepthMm,
    FurnitureCatalogEntry Entry);

/// <summary>
/// The furniture block catalogue, and the single place a caller-supplied name is turned into
/// a family plus dimensions.
/// </summary>
/// <remarks>
/// <see cref="All"/> is what <c>list_furniture_catalog</c> publishes and <see cref="Resolve"/>
/// is what <c>insert_furniture</c> accepts. They must agree, and
/// <c>CatalogContractTests</c> asserts that every name the first hands out is accepted by the
/// second. They did not agree once, and 11 of 26 published names were uninsertable.
/// </remarks>
public static class FurnitureCatalog
{
    /// <summary>Entries drawn from a bespoke recipe, always at their published size.</summary>
    public static IReadOnlyList<FurnitureCatalogEntry> Fixed { get; } = new List<FurnitureCatalogEntry>
    {
        // beds (hospital)
        new("FURN-BED-STD",    "bed",   "hospital",    900, 2000, "Standard hospital bed, two-wheel frame + pillow strip"),
        new("FURN-BED-ICU",    "bed",   "hospital",   1000, 2200, "ICU bed, with head-monitor box + side rails"),
        new("FURN-BED-BARIAT", "bed",   "hospital",   1200, 2200, "Bariatric bed (reinforced, wide frame)"),
        new("FURN-BED-PED",    "bed",   "hospital",    700, 1500, "Pediatric bed with side rail"),
        new("FURN-BED-OR",     "bed",   "hospital",    550, 2100, "Operating-room table, narrow body + trendelenburg end"),
        new("FURN-BED-LBR",    "bed",   "hospital",   1050, 2300, "Labour/delivery bed with foot stirrups"),
        // chairs
        new("FURN-CHAIR-OFF",  "chair", "office",       550,  550, "Office swivel chair (round base + 5 legs)"),
        new("FURN-CHAIR-ARM",  "chair", "residential",  800,  800, "Armchair, square"),
        new("FURN-CHAIR-STL",  "chair", "hospital",     450,  450, "Round stool"),
        new("FURN-CHAIR-EXAM", "chair", "hospital",     600,  600, "Medical rolling exam stool"),
        new("FURN-CHAIR-WHL",  "chair", "hospital",     700, 1100, "Wheelchair (seat + backrest + 2 wheels)"),
        // sofas
        new("FURN-SOFA-2",     "sofa",  "residential", 1800,  800, "2-seat lounge sofa, cushioned"),
        new("FURN-SOFA-3",     "sofa",  "residential", 2200,  800, "3-seat lounge sofa, cushioned"),
        new("FURN-SOFA-CLN-2", "sofa",  "hospital",    1800,  700, "2-seat clinic waiting sofa (vinyl)"),
        new("FURN-SOFA-CLN-3", "sofa",  "hospital",    2200,  700, "3-seat clinic waiting sofa (vinyl)"),
    };

    /// <summary>
    /// Families drawn parametrically. The dimensions here are defaults: a caller may name the
    /// family bare and take them, or append a <c>-W-D</c> suffix, or pass explicit overrides.
    /// </summary>
    public static IReadOnlyList<FurnitureCatalogEntry> SizedFamilies { get; } = new List<FurnitureCatalogEntry>
    {
        new("FURN-DESK-OFF",   "desk",    "office",      1600,  800, "Office desk with two drawer lines"),
        new("FURN-DESK-RCP",   "desk",    "office",      2400,  800, "Reception L-counter (desk + overhang)"),
        new("FURN-DESK-NST",   "desk",    "hospital",    3000,  900, "Nurse station with raised edge"),
        new("FURN-CBT-STR",    "cabinet", "office",       800,  400, "Storage cabinet with door-swing arc"),
        new("FURN-CBT-MED",    "cabinet", "hospital",     900,  450, "Medical cabinet with glass-door cross indicator"),
        new("FURN-CBT-FIL",    "cabinet", "office",      1000,  450, "File cabinet with drawer lines"),
        new("FURN-CBT-WDR",    "cabinet", "residential", 1200,  600, "Wardrobe with hanger rail indicator"),
        new("FURN-TBL-RECT",   "table",   "office",      1200,  800, "Rectangular table"),
        new("FURN-TBL-ROUND",  "table",   "office",      1200, 1200, "Round table (W=D=diameter)"),
        new("FURN-TBL-SQ",     "table",   "office",      1000, 1000, "Square table"),
        new("FURN-TBL-EXAM",   "table",   "hospital",    1900,  700, "Medical exam table with pillow + paper-roll slot"),
    };

    /// <summary>Exactly what <c>list_furniture_catalog</c> publishes, filters applied.</summary>
    public static IReadOnlyList<FurnitureCatalogEntry> All(string? categoryFilter = null, string? domainFilter = null)
    {
        IEnumerable<FurnitureCatalogEntry> merged = Fixed.Concat(SizedFamilies);

        if (!string.IsNullOrWhiteSpace(categoryFilter))
            merged = merged.Where(e => string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(domainFilter))
            merged = merged.Where(e => string.Equals(e.Domain, domainFilter, StringComparison.OrdinalIgnoreCase));

        return merged
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Turn a caller-supplied block name into a family and dimensions. Accepts every name
    /// <see cref="All"/> publishes, in all three forms.
    /// </summary>
    /// <param name="name">A fixed entry, a bare family name, or <c>FAMILY-W-D</c>.</param>
    /// <param name="overrideWidthMm">Caller's explicit width; wins over any other source.</param>
    /// <param name="overrideDepthMm">Caller's explicit depth; wins over any other source.</param>
    /// <exception cref="CatalogNameException">The name matches nothing in the catalogue.</exception>
    public static FurnitureResolution Resolve(string name, double? overrideWidthMm = null, double? overrideDepthMm = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameException("Block name is required. Names accepted here are exactly those returned by list_furniture_catalog.");

        var fixedHit = Fixed.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (fixedHit is not null)
        {
            // Fixed entries are drawn from a bespoke recipe built around their published
            // proportions, so a size override is not honoured here.
            return new FurnitureResolution(CatalogMatch.Fixed, fixedHit.Name, fixedHit.WidthMm, fixedHit.DepthMm, fixedHit);
        }

        var bare = SizedFamilies.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (bare is not null)
        {
            return new FurnitureResolution(
                CatalogMatch.FamilyDefaults,
                bare.Name,
                overrideWidthMm ?? bare.WidthMm,
                overrideDepthMm ?? bare.DepthMm,
                bare);
        }

        if (CatalogNaming.TrySplitSized(name, out var family, out var wMm, out var dMm))
        {
            var famHit = SizedFamilies.FirstOrDefault(e => string.Equals(e.Name, family, StringComparison.OrdinalIgnoreCase));
            if (famHit is not null)
            {
                return new FurnitureResolution(
                    CatalogMatch.SizedSuffix,
                    famHit.Name,
                    overrideWidthMm ?? wMm,
                    overrideDepthMm ?? dMm,
                    famHit);
            }

            throw new CatalogNameException(
                $"Block '{name}' parses as family '{family}' with size {wMm}x{dMm} mm, but '{family}' is not a known " +
                "sized family. Names accepted here are exactly those returned by list_furniture_catalog.");
        }

        throw new CatalogNameException(
            $"Block '{name}' is neither in the fixed catalog, a known sized family, nor a sized-family name " +
            "(expected format FURN-FAMILY-SUBTYPE-W-D). Names accepted here are exactly those returned by " +
            "list_furniture_catalog.");
    }
}
