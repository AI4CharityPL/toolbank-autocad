// Single source of truth for the civil-engineering layer key shipped with
// acad-civil. Mirrors rule 38 §9 (civil-domain-traps.mdc).
//
// If you add a new layer here, also:
//   1. Update rule 38 §9 (the table).
//   2. Add a paired validator under validators/civil/ if the layer implies a
//      "must be on this layer" rule.

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Civil;

/// <summary>
/// Canonical layer names + colours + linetypes + lineweights for a civil plan
/// per Polish PN + US NCS hybrid. Used by <c>ensure_civil_layers</c> and as
/// default values for every drawing tool in the category.
/// </summary>
internal static class CivilPalette
{
    public const string LayerRoadCntr = "C-ROAD-CNTR";
    public const string LayerRoadEdge = "C-ROAD-EDGE";
    public const string LayerRoadLane = "C-ROAD-LANE";
    public const string LayerProperty = "C-PROP";
    public const string LayerEasement = "C-ESMT";
    public const string LayerRow      = "C-ROW";
    public const string LayerTopoMajr = "C-TOPO-MAJR";
    public const string LayerTopoMinr = "C-TOPO-MINR";
    public const string LayerTopoSpot = "C-TOPO-SPOT";
    public const string LayerStation  = "C-STAT";
    public const string LayerAnno     = "C-ANNO";
    public const string LayerNorth    = "C-NORTH";

    public sealed record Spec(
        string Name, int AciColor, string Linetype, double LineweightMm, string Purpose, bool Plottable);

    public static IReadOnlyList<Spec> All { get; } = new List<Spec>
    {
        new(LayerRoadCntr, 4, "CENTER",     0.30, "road centreline (alignment)", true),
        new(LayerRoadEdge, 7, "Continuous", 0.50, "edge of pavement",            true),
        new(LayerRoadLane, 3, "DASHED",     0.18, "lane lines",                  true),
        new(LayerProperty, 6, "PHANTOM2",   0.50, "property / parcel boundary",  true),
        new(LayerEasement, 6, "HIDDEN2",    0.25, "easement",                    true),
        new(LayerRow,      6, "PHANTOM",    0.50, "right of way",                true),
        new(LayerTopoMajr, 8, "Continuous", 0.35, "major contour line",          true),
        new(LayerTopoMinr, 9, "Continuous", 0.13, "minor contour line",          true),
        new(LayerTopoSpot, 2, "Continuous", 0.18, "spot elevation marks + labels", true),
        new(LayerStation,  2, "Continuous", 0.18, "stationing tick marks + labels", true),
        new(LayerAnno,     2, "Continuous", 0.18, "civil annotations",           true),
        new(LayerNorth,    7, "Continuous", 0.50, "north arrow",                 true),
    };

    /// <summary>Phase-7 bundled blocks (rule 38 §10).</summary>
    public static IReadOnlyList<string> PlannedBlocks { get; } = new[]
    {
        "NORTH_ARROW_BASIC.dwg",
        "NORTH_ARROW_COMPASS.dwg",
        "BENCHMARK_GEODETIC.dwg",
        "MANHOLE_CIRCULAR.dwg",
        "CATCH_BASIN_GRATE.dwg",
        "TREE_DECIDUOUS.dwg",
        "TREE_CONIFEROUS.dwg",
        "STATION_TICK_MAJOR.dwg",
    };
}

/// <summary>
/// Closure-tolerance presets per parcel category (rule 38 §3, Polish geodetic
/// office values). Tools accept either an explicit metres value or one of
/// these presets via <see cref="CivilParcelKind"/>.
/// </summary>
public enum CivilParcelKind
{
    Residential   = 0,   // < 0.02 m
    Commercial    = 1,   // < 0.05 m
    Agricultural  = 2,   // < 0.20 m
    Forest        = 3,   // < 0.50 m
}

internal static class CivilTolerances
{
    public static double ClosureMetresFor(CivilParcelKind kind) => kind switch
    {
        CivilParcelKind.Residential  => 0.02,
        CivilParcelKind.Commercial   => 0.05,
        CivilParcelKind.Agricultural => 0.20,
        CivilParcelKind.Forest       => 0.50,
        _                            => 0.05,
    };
}
