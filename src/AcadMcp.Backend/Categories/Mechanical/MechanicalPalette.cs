// Single source of truth for the ISO-mechanical layer key shipped with
// acad-mechanical. Mirrors rule 37 §9 (mechanical-domain-traps.md).
//
// If you add a new layer here, also:
//   1. Update rule 37 §9 (the table).
//   2. Add a paired validator under validators/mechanical/ when the layer
//      implies a "must be on this layer" rule.

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Mechanical;

/// <summary>
/// Canonical layer names + colours + linetypes + lineweights for a mechanical
/// drawing per ISO 128 / ISO 5457. Used by <c>ensure_mechanical_layers</c>
/// and as default values for every drawing tool in the category.
/// </summary>
internal static class MechanicalPalette
{
    public const string LayerVisible      = "ME-VISIBLE";
    public const string LayerHidden       = "ME-HIDDEN";
    public const string LayerCenter       = "ME-CENTER";
    public const string LayerDims         = "ME-DIMS";
    public const string LayerText         = "ME-TEXT";
    public const string LayerSection      = "ME-SECTION";
    public const string LayerHatch        = "ME-HATCH";
    public const string LayerThread       = "ME-THREAD";
    public const string LayerConstruction = "ME-CONSTRUCTION";
    public const string LayerTitle        = "ME-TITLE";
    public const string LayerRev          = "ME-REV";

    /// <summary>One descriptor per layer in the ISO mechanical key.</summary>
    public sealed record Spec(
        string Name, int AciColor, string Linetype, double LineweightMm, string Purpose, bool Plottable);

    public static IReadOnlyList<Spec> All { get; } = new List<Spec>
    {
        new(LayerVisible,      7, "Continuous", 0.50, "visible feature edges",         true),
        new(LayerHidden,       8, "HIDDEN",     0.25, "hidden (occluded) edges",       true),
        new(LayerCenter,       4, "CENTER",     0.18, "centrelines, axes",             true),
        new(LayerDims,         2, "Continuous", 0.18, "dimensions",                    true),
        new(LayerText,         2, "Continuous", 0.18, "notes, labels",                 true),
        new(LayerSection,      1, "PHANTOM",    0.70, "section cutting plane",         true),
        new(LayerHatch,        8, "Continuous", 0.18, "section hatching",              true),
        new(LayerThread,       8, "HIDDEN",     0.25, "thread minor-Ø arcs / lines",   true),
        new(LayerConstruction, 9, "Continuous", 0.13, "construction (non-plottable)",  false),
        new(LayerTitle,        7, "Continuous", 0.50, "title block geometry",          true),
        new(LayerRev,          1, "Continuous", 0.50, "revision triangles + tags",     true),
    };

    /// <summary>Bundled blocks planned for Phase 7 (rule 37 §10). Listed here so
    /// the introspection tool can announce them before the binaries ship.</summary>
    public static IReadOnlyList<string> PlannedBlocks { get; } = new[]
    {
        "BOLT_HEX_M6.dwg",  "BOLT_HEX_M8.dwg",  "BOLT_HEX_M10.dwg",
        "BOLT_HEX_M12.dwg", "BOLT_HEX_M16.dwg", "BOLT_HEX_M20.dwg",
        "BOLT_HEX_M24.dwg",
        "WASHER_FLAT_M6.dwg", "WASHER_FLAT_M8.dwg", "WASHER_FLAT_M10.dwg",
        "BEARING_RADIAL_608.dwg",  "BEARING_RADIAL_6200.dwg", "BEARING_RADIAL_6300.dwg",
        "SURFACE_FINISH_BASIC.dwg",
        "WELD_SYMBOL_BASIC.dwg",
    };
}

/// <summary>
/// ISO 128-50 material → hatch pattern map (rule 37 §8). Single source of
/// truth so the agent always says "steel" / "aluminium" / "concrete" rather
/// than picking AutoCAD pattern names directly.
/// </summary>
internal static class MechanicalPatterns
{
    public sealed record HatchSpec(string Pattern, double Scale, double AngleDeg, string Material);

    /// <summary>Lookup by lower-cased material keyword. Keys are stable.</summary>
    public static IReadOnlyDictionary<string, HatchSpec> ByMaterial { get; } = new Dictionary<string, HatchSpec>
    {
        ["cast_iron"] = new("ANSI31",  1.0,  0.0, "cast iron / generic"),
        ["generic"]   = new("ANSI31",  1.0,  0.0, "generic"),
        ["steel"]     = new("ANSI31",  0.5,  0.0, "steel (tighter pitch)"),
        ["bronze"]    = new("ANSI37",  1.0,  0.0, "bronze / brass (crossed 45/135)"),
        ["brass"]     = new("ANSI37",  1.0,  0.0, "bronze / brass"),
        ["aluminium"] = new("ANSI33",  1.0,  0.0, "aluminium (30/60 dashes)"),
        ["aluminum"]  = new("ANSI33",  1.0,  0.0, "aluminium"),
        ["glass"]     = new("LINE",    1.0,  0.0, "glass (horizontal lines)"),
        ["soil"]      = new("EARTH",   1.0,  0.0, "soil"),
        ["concrete"]  = new("AR-CONC", 0.5,  0.0, "concrete"),
    };
}
