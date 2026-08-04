using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>One hatch pattern the catalogue publishes.</summary>
public sealed record HatchPatternEntry(
    string Name,
    string Category,
    string Description,
    double DefaultScale,
    double DefaultAngleDeg);

/// <summary>
/// A named material and the hatch settings that draw it. Scale assumes millimetre drawing
/// units.
/// </summary>
public sealed record MaterialPreset(
    string Material,
    string Pattern,
    double Scale,
    double AngleDeg,
    int AciColor);

/// <summary>
/// Hatch patterns and material presets, per <c>docs/engineering-rules/62-hatching-policy.md</c>.
/// </summary>
/// <remarks>
/// This catalogue carries a second contract the furniture and plumbing ones do not: every
/// material preset names a pattern, and that pattern has to exist in the pattern catalogue.
/// A preset pointing at an unlisted pattern is invisible — <c>list_hatch_patterns</c> would
/// not show it, while <c>apply_material_preset</c> would happily ask AutoCAD to load it.
/// <c>CatalogContractTests</c> checks both directions.
/// </remarks>
public static class HatchCatalog
{
    /// <summary>Every pattern <c>list_hatch_patterns</c> publishes, keyed by name.</summary>
    public static IReadOnlyDictionary<string, HatchPatternEntry> Patterns { get; } =
        new Dictionary<string, HatchPatternEntry>(StringComparer.OrdinalIgnoreCase)
        {
            // ANSI patterns (ISO 128 mechanical)
            { "ANSI31",  new("ANSI31",  "ANSI", "Iron / brick elevation (45° diagonal)", 1.0, 0.0) },
            { "ANSI32",  new("ANSI32",  "ANSI", "Steel",                                 1.0, 0.0) },
            { "ANSI33",  new("ANSI33",  "ANSI", "Bronze / brass",                        1.0, 0.0) },
            { "ANSI34",  new("ANSI34",  "ANSI", "Plastic / rubber",                      1.0, 0.0) },
            { "ANSI35",  new("ANSI35",  "ANSI", "Fire brick / refractory",               1.0, 0.0) },
            { "ANSI36",  new("ANSI36",  "ANSI", "Marble / slate",                        1.0, 0.0) },
            { "ANSI37",  new("ANSI37",  "ANSI", "Lead / zinc (crosshatch)",              1.0, 0.0) },
            { "ANSI38",  new("ANSI38",  "ANSI", "Aluminum",                              1.0, 0.0) },

            // ISO patterns (PN-EN-ISO 128)
            { "ISO02W100", new("ISO02W100", "ISO", "Dashed line (ISO)",              1.0, 0.0) },
            { "ISO03W100", new("ISO03W100", "ISO", "Dashed space (ISO)",             1.0, 0.0) },
            { "ISO09W100", new("ISO09W100", "ISO", "Long dash short dash (ISO)",     1.0, 0.0) },

            // Architectural (ISO 128 + PN-EN)
            { "AR-CONC",  new("AR-CONC",  "ARCH", "Concrete (stone aggregate)",            1.0, 0.0) },
            { "AR-BRSTD", new("AR-BRSTD", "ARCH", "Brick — standard (common bond)",        1.0, 0.0) },
            { "AR-BRELM", new("AR-BRELM", "ARCH", "Brick — English bond",                  1.0, 0.0) },
            { "AR-B816",  new("AR-B816",  "ARCH", "Block 8x16 (cinder / concrete block)",  1.0, 0.0) },
            { "AR-B88",   new("AR-B88",   "ARCH", "Block 8x8",                             1.0, 0.0) },
            { "AR-RROOF", new("AR-RROOF", "ARCH", "Rough stone / irregular roof tile",     1.0, 0.0) },
            { "AR-HBONE", new("AR-HBONE", "ARCH", "Herringbone parquet",                   1.0, 0.0) },
            { "AR-PARQ1", new("AR-PARQ1", "ARCH", "Parquet (standard)",                    1.0, 0.0) },
            { "AR-SAND",  new("AR-SAND",  "ARCH", "Sand",                                  1.0, 0.0) },
            { "AR-RSHKE", new("AR-RSHKE", "ARCH", "Roof shingles",                         1.0, 0.0) },

            // Material-specific
            { "BATTING", new("BATTING", "MATERIAL", "Insulation (zigzag)",          1.0, 0.0) },
            { "EARTH",   new("EARTH",   "MATERIAL", "Earth / soil",                 1.0, 0.0) },
            { "CORK",    new("CORK",    "MATERIAL", "Cork",                         1.0, 0.0) },
            { "NET",     new("NET",     "MATERIAL", "Mesh / grid (Faraday)",        1.0, 0.0) },
            { "NET3",    new("NET3",    "MATERIAL", "3-direction mesh",             1.0, 0.0) },
            { "GRAVEL",  new("GRAVEL",  "MATERIAL", "Gravel",                       1.0, 0.0) },
            { "SWAMP",   new("SWAMP",   "MATERIAL", "Swamp / wetland",              1.0, 0.0) },
            { "GRASS",   new("GRASS",   "MATERIAL", "Grass",                        1.0, 0.0) },
            { "HONEY",   new("HONEY",   "MATERIAL", "Honeycomb",                    1.0, 0.0) },
            { "TRIANG",  new("TRIANG",  "MATERIAL", "Triangles",                    1.0, 0.0) },
            { "DOTS",    new("DOTS",    "MATERIAL", "Dots",                         1.0, 0.0) },
            { "CROSS",   new("CROSS",   "MATERIAL", "Crosses",                      1.0, 0.0) },
            { "ESCHER",  new("ESCHER",  "MATERIAL", "Escher pattern",               1.0, 0.0) },
            { "FLEX",    new("FLEX",    "MATERIAL", "Flexible material",            1.0, 0.0) },
            { "ZIGZAG",  new("ZIGZAG",  "MATERIAL", "Zigzag",                       1.0, 0.0) },
            { "CLAY",    new("CLAY",    "MATERIAL", "Clay",                         1.0, 0.0) },
            { "SACNCR",  new("SACNCR",  "MATERIAL", "Sand + concrete composite",    1.0, 0.0) },

            // Solid / line
            { "SOLID", new("SOLID", "SOLID", "Solid fill",      1.0, 0.0) },
            { "LINE",  new("LINE",  "LINE",  "Parallel lines",  1.0, 0.0) },
        };

    /// <summary>Material name to hatch settings. Scale assumes millimetre drawing units.</summary>
    public static IReadOnlyDictionary<string, MaterialPreset> MaterialPresets { get; } =
        new Dictionary<string, MaterialPreset>(StringComparer.OrdinalIgnoreCase)
        {
            { "concrete",            new("concrete",            "AR-CONC",  50.0,  0.0,  8) },  // gray
            { "reinforced-concrete", new("reinforced-concrete", "ANSI37",    5.0,  0.0,  8) },
            { "concrete-block",      new("concrete-block",      "AR-B816",  50.0,  0.0,  8) },
            { "brick",               new("brick",               "AR-BRSTD", 50.0,  0.0,  1) },  // red
            { "brick-elm",           new("brick-elm",           "AR-BRELM", 50.0,  0.0,  1) },
            { "insulation",          new("insulation",          "BATTING",  50.0,  0.0,  4) },  // cyan
            { "plaster",             new("plaster",             "ANSI31",    5.0, 45.0,  8) },
            { "stone",               new("stone",               "AR-RROOF", 50.0,  0.0, 42) },  // brown
            { "earth",               new("earth",               "EARTH",    50.0,  0.0, 42) },
            { "soil",                new("soil",                "EARTH",    50.0,  0.0, 42) },
            { "steel",               new("steel",               "ANSI32",    5.0, 45.0,  7) },
            { "glass",               new("glass",               "LINE",      1.0, 45.0,  4) },
            { "wood-cross",          new("wood-cross",          "ANSI32",    5.0,  0.0, 42) },
            { "wood-grain",          new("wood-grain",          "AR-HBONE",  1.0,  0.0, 42) },
            { "parquet",             new("parquet",             "AR-PARQ1",  1.0,  0.0, 42) },
            { "herringbone",         new("herringbone",         "AR-HBONE",  1.0,  0.0, 42) },
            { "tile",                new("tile",                "AR-B816",   1.0,  0.0,  8) },
            { "lead-shield",         new("lead-shield",         "SOLID",     1.0,  0.0,  6) },  // magenta - RTG/lead shielding
            { "faraday",             new("faraday",             "NET",      50.0,  0.0,  3) },  // green - Faraday cage mesh
            { "sand",                new("sand",                "AR-SAND",  50.0,  0.0, 40) },
            { "cork",                new("cork",                "CORK",     50.0,  0.0, 42) },
            { "gravel",              new("gravel",              "GRAVEL",   50.0,  0.0,  8) },
            { "grass",               new("grass",               "GRASS",    50.0,  0.0,  3) },
        };

    /// <summary>Exactly what <c>list_hatch_patterns</c> publishes, ordered as it orders them.</summary>
    public static IReadOnlyList<HatchPatternEntry> AllPatterns(string? categoryFilter = null)
    {
        IEnumerable<HatchPatternEntry> all = Patterns.Values;

        if (!string.IsNullOrWhiteSpace(categoryFilter))
            all = all.Where(e => string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));

        return all
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Resolve a material preset name.</summary>
    /// <exception cref="CatalogNameException">The material is unknown.</exception>
    public static MaterialPreset ResolvePreset(string material)
    {
        if (string.IsNullOrWhiteSpace(material))
            throw new CatalogNameException(
                "material preset name is required. Known presets are those returned by list_material_presets.");

        if (!MaterialPresets.TryGetValue(material.Trim(), out var preset))
        {
            var known = string.Join(", ", MaterialPresets.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            throw new CatalogNameException($"Unknown material preset '{material}'. Known: {known}.");
        }

        return preset;
    }

    /// <summary>Resolve a hatch pattern name to its catalogue entry.</summary>
    /// <exception cref="CatalogNameException">The pattern is not in the catalogue.</exception>
    public static HatchPatternEntry ResolvePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new CatalogNameException(
                "pattern name is required. Known patterns are those returned by list_hatch_patterns.");

        if (!Patterns.TryGetValue(pattern.Trim(), out var entry))
            throw new CatalogNameException(
                $"Pattern '{pattern}' is not in the catalogue. Known patterns are those returned by list_hatch_patterns.");

        return entry;
    }
}
