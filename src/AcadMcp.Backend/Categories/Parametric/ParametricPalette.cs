// Single source of truth for the parametric / constraint-sketch layer key.
// Mirrors rule 42 §9 (42-parametric-domain-traps.md).

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Parametric;

internal static class ParametricPalette
{
    public const string LayerConstruction = "P-CONSTRUCTION";
    public const string LayerSketch       = "P-SKETCH";
    public const string LayerConstrained  = "P-CONSTRAINED";
    public const string LayerDynamic      = "P-DYNAMIC";
    public const string LayerParamLbl     = "P-PARAM-LBL";
    public const string LayerNote         = "P-NOTE";

    public sealed record Spec(
        string Name, int AciColor, string Linetype, double LineweightMm, string Purpose, bool Plottable);

    public static IReadOnlyList<Spec> All { get; } = new List<Spec>
    {
        new(LayerConstruction, 8, "Continuous", 0.18, "construction geometry (datum frame, guides)", true),
        new(LayerSketch,       7, "Continuous", 0.25, "unconstrained / WIP sketch curves",            true),
        new(LayerConstrained,  5, "Continuous", 0.35, "fully constrained profile curves",             true),
        new(LayerDynamic,      4, "Continuous", 0.30, "dynamic block references (optional target)", true),
        new(LayerParamLbl,     2, "Continuous", 0.18, "parameter / expression labels (Phase 7)",    true),
        new(LayerNote,         2, "Continuous", 0.18, "parametric workflow notes",                    true),
    };

    public static IReadOnlyList<string> PlannedBlocks { get; } = new[]
    {
        "PARAM_DOOR_SWING.dwg",
        "PARAM_TITLE_STRIP.dwg",
        "PARAM_VIEW_LABEL.dwg",
    };
}
