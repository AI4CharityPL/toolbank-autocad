// Canonical layers + building-code constants for acad-verticals.
// Mirrors rule 67 §2 (layers) and WT (Warunki Techniczne) §54..§68 code limits.

namespace AcadMcp.Backend.Categories.Verticals;

internal static class VerticalsPalette
{
    // Layers (A-* = architectural, S-* = structural).
    public const string LayerStairs       = "A-STRS";
    public const string LayerStairsDir    = "A-STRS-DIR";
    public const string LayerRamp         = "A-RAMP";
    public const string LayerRampDir      = "A-RAMP-DIR";
    public const string LayerElevator     = "A-VTRN-ELEV";
    public const string LayerEscalator    = "A-VTRN-ESCL";
    public const string LayerPlatformLift = "A-VTRN-LIFT";
    public const string LayerHandrail     = "A-RAIL";
    public const string LayerAnnoNote     = "A-ANNO-NOTE";

    // WT §54..§68 Polish building code numeric constraints.
    //   - public stair: riser 15..17.5 cm, tread 25..35 cm
    //   - residential stair: riser ≤ 19 cm, tread ≥ 25 cm
    //   - egress stair (public): min clear width 120 cm
    //   - ramp for disabled access: max slope 6% (1:16.67); max 8% if < 500 mm rise
    //   - bed-lift (hospital): min 1600×2600 mm cabin, 1600 kg capacity
    //   - passenger lift (public): min 1100×1400 mm cabin, 1000 kg capacity
    //   - handrail height: 900..1100 mm for public stairs
    //
    // Callers are expected to warn (not throw) if inputs fall outside these ranges so
    // architects can experiment with concept-phase layouts. Hard failure belongs in
    // validators/architectural/*.
    public const double PublicStairRiserMinMm = 150.0;
    public const double PublicStairRiserMaxMm = 175.0;
    public const double PublicStairTreadMinMm = 250.0;
    public const double PublicStairTreadMaxMm = 350.0;
    public const double PublicStairClearWidthMinMm = 1200.0;
    public const double AccessibleRampMaxSlope = 0.06;
    public const double BedLiftMinWidthMm      = 1600.0;
    public const double BedLiftMinDepthMm      = 2600.0;
    public const double PassengerLiftMinWidthMm = 1100.0;
    public const double PassengerLiftMinDepthMm = 1400.0;
    public const double HandrailHeightMinMm = 900.0;
    public const double HandrailHeightMaxMm = 1100.0;
}
