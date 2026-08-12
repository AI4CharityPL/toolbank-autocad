using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>
/// One hot-rolled steel I/H-section the catalogue publishes. Dimensions are millimetres.
/// </summary>
/// <param name="AreaCm2">
/// Nominal cross-sectional area computed from <see cref="HeightMm"/>/<see cref="WidthMm"/>/
/// <see cref="WebThicknessMm"/>/<see cref="FlangeThicknessMm"/> assuming square corners (no
/// root radius) - <c>2*WidthMm*FlangeThicknessMm + (HeightMm-2*FlangeThicknessMm)*WebThicknessMm</c>,
/// converted to cm^2. This deliberately matches the fillet-free outline
/// <c>insert_steel_column</c> actually draws (rule 72 §4), so a live area check on the drawn
/// polygon can be compared directly against this field. A real mill certificate's area is a
/// few percent higher because of the root radius this catalogue omits - do not treat
/// <see cref="AreaCm2"/> as the certified section property for a load calculation.
/// </param>
/// <param name="WeightKgPerM">
/// Mass per metre as published by the source table (includes the root radius the drawn
/// geometry omits, so this is systematically a little heavier than <see cref="AreaCm2"/> * steel
/// density would predict - expected, not a data error).
/// </param>
public sealed record SteelProfileEntry(
    string Designation,
    string Series,
    double HeightMm,
    double WidthMm,
    double WebThicknessMm,
    double FlangeThicknessMm,
    double WeightKgPerM,
    double AreaCm2,
    string Standard,
    string Description);

/// <summary>What <see cref="SteelProfileCatalog.Resolve"/> decided a caller's designation meant.</summary>
public sealed record SteelProfileResolution(string Designation, SteelProfileEntry Entry);

/// <summary>
/// A representative subset of hot-rolled European H/I steel sections (HEA/HEB/IPE), and the
/// single place a caller-supplied designation is turned into a profile.
/// </summary>
/// <remarks>
/// Unlike <see cref="FurnitureCatalog"/>/<see cref="PlumbingCatalog"/>, there is no sized-family
/// form here - a designation like <c>HEB200</c> names one fixed, standardised cross-section, not
/// a parametric family a caller can resize. <see cref="Resolve"/> is therefore a direct
/// case-insensitive lookup, not a <c>CatalogNaming.TrySplitSized</c> parse.
///
/// Sourcing (rule 72 §5 has the full confidence table): HEA/HEB dimensions and mass/m were
/// fetched from a published structural-steel dimension reference citing NEN-EN 10025-1/2; IPE
/// figures from a separate Eurocode-properties reference citing EN 10365. Both are real,
/// named technical sources, not invented - but neither was cross-checked against a mill
/// certificate or the raw standard text, and the two series come from two different source
/// pages, so treat every figure as Confirmed-from-a-named-secondary-source, not
/// Confirmed-from-primary-standard-text. Verify before using on a real (non-demonstration)
/// project - the same discipline `docs/knowledge-base/residential/STANDARDS.md` already applies
/// to its own Probable/Unconfirmed rows.
/// </remarks>
public static class SteelProfileCatalog
{
    private static double Area(double heightMm, double widthMm, double webMm, double flangeMm) =>
        (2.0 * widthMm * flangeMm + (heightMm - 2.0 * flangeMm) * webMm) / 100.0;

    public static IReadOnlyList<SteelProfileEntry> All { get; } = BuildAll();

    private static List<SteelProfileEntry> BuildAll()
    {
        var entries = new List<SteelProfileEntry>();

        // HEA - light series. h/b/tw/tf/mass: NEN-EN 10025-1/2 dimension reference.
        void Hea(string designation, double h, double b, double tw, double tf, double mass) =>
            entries.Add(new(designation, "HEA", h, b, tw, tf, mass, Area(h, b, tw, tf),
                "PN-EN 10025-1/2", $"{designation} light-series European wide-flange H-section"));
        Hea("HEA100", 96,  100, 5.0, 8.0,  17.0);
        Hea("HEA140", 133, 140, 5.5, 8.5,  25.1);
        Hea("HEA160", 152, 160, 6.0, 9.0,  31.0);
        Hea("HEA200", 190, 200, 6.5, 10.0, 43.1);
        Hea("HEA240", 230, 240, 7.5, 12.0, 61.5);
        Hea("HEA300", 290, 300, 8.5, 14.0, 90.0);

        // HEB - medium series. Same source family as HEA.
        void Heb(string designation, double h, double b, double tw, double tf, double mass) =>
            entries.Add(new(designation, "HEB", h, b, tw, tf, mass, Area(h, b, tw, tf),
                "PN-EN 10025-1/2", $"{designation} medium-series European wide-flange H-section"));
        Heb("HEB100", 100, 100, 6.0,  10.0, 20.8);
        Heb("HEB140", 140, 140, 7.0,  12.0, 34.4);
        Heb("HEB160", 160, 160, 8.0,  13.0, 43.4);
        Heb("HEB200", 200, 200, 9.0,  15.0, 62.5);
        Heb("HEB240", 240, 240, 10.0, 17.0, 84.8);
        Heb("HEB300", 300, 300, 11.0, 19.0, 119.0);

        // IPE - European I-beam. h/b/tw/tf/mass: EN 10365 design-properties reference.
        void Ipe(string designation, double h, double b, double tw, double tf, double mass) =>
            entries.Add(new(designation, "IPE", h, b, tw, tf, mass, Area(h, b, tw, tf),
                "EN 10365", $"{designation} European I-beam"));
        Ipe("IPE100", 100, 55,  4.1, 5.7,  8.10);
        Ipe("IPE140", 140, 73,  4.7, 6.9,  12.9);
        Ipe("IPE160", 160, 82,  5.0, 7.4,  15.8);
        Ipe("IPE200", 200, 100, 5.6, 8.5,  22.4);
        Ipe("IPE240", 240, 120, 6.2, 9.8,  30.7);
        Ipe("IPE300", 300, 150, 7.1, 10.7, 42.2);
        Ipe("IPE360", 360, 170, 8.0, 12.7, 57.1);

        return entries;
    }

    /// <summary>Exactly what <c>list_steel_profiles</c> publishes, filter applied.</summary>
    public static IReadOnlyList<SteelProfileEntry> Filtered(string? seriesFilter = null)
    {
        IEnumerable<SteelProfileEntry> merged = All;
        if (!string.IsNullOrWhiteSpace(seriesFilter))
            merged = merged.Where(e => string.Equals(e.Series, seriesFilter, StringComparison.OrdinalIgnoreCase));
        return merged
            .OrderBy(e => e.Series, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.HeightMm)
            .ToList();
    }

    /// <summary>
    /// Turn a caller-supplied designation (e.g. <c>"HEB200"</c>, case-insensitive) into a
    /// profile. Unlike <see cref="FurnitureCatalog.Resolve"/> there is no sized-suffix form -
    /// a standardised section designation is not a caller-resizable family.
    /// </summary>
    /// <exception cref="CatalogNameException">The designation matches nothing in the catalogue.</exception>
    public static SteelProfileResolution Resolve(string designation)
    {
        if (string.IsNullOrWhiteSpace(designation))
            throw new CatalogNameException("Profile designation is required. Names accepted here are exactly those returned by list_steel_profiles.");

        var hit = All.FirstOrDefault(e => string.Equals(e.Designation, designation, StringComparison.OrdinalIgnoreCase));
        if (hit is not null)
            return new SteelProfileResolution(hit.Designation, hit);

        throw new CatalogNameException(
            $"Profile '{designation}' is not in the catalogue. Names accepted here are exactly those returned by " +
            "list_steel_profiles (e.g. 'HEB200', 'IPE300') - this is a fixed representative subset, not the full EN 10365 range.");
    }
}
