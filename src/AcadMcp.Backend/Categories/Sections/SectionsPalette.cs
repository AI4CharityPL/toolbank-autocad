// Canonical layers, linetypes and plot sizes for the acad-sections composite
// category. Paired with acad-callouts (rule 69) - section lines live on
// A-DETL-SECT, end markers still come from CalloutsTools.InsertSectionCallout.
//
// Drawing-unit is millimetre. All plotted sizes are multiplied by the user-
// declared plan-scale factor (CalloutsPalette.ResolveScaleFactor).

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Sections;

internal static class SectionsPalette
{
    // ─────────── layers (rule 70) ───────────
    public const string LayerSectionLine       = "A-DETL-SECT";
    public const string LayerSectionTitle      = "A-DETL-TITL";
    public const string LayerElevationMarker   = "A-DETL-ELEV";

    // ─────────── linetypes ───────────
    public const string SectionCutLinetype = "DASHED2";
    public const double SectionCutLtScale  = 1.0;   // multiplied by scaleFactor at runtime

    // ─────────── plot sizes (mm on paper, multiplied by plan scale) ───────────
    public const double PlotOffsetTickMm        = 6.0;   // length of a 90° step tick at each endpoint
    public const double PlotTitleUnderlineMm    = 80.0;  // section-title underline length on paper
    public const double PlotElevationTriangleMm = 8.0;   // elevation-direction triangle side length
    public const double PlotElevationBaselineMm = 30.0;  // horizontal baseline under elevation marker

    // ─────────── elevation directions ───────────
    public static readonly IReadOnlyDictionary<string, double> Directions =
        new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["N"]  = 90.0,    // pointing up (model-space +Y)
            ["E"]  = 0.0,
            ["S"]  = 270.0,
            ["W"]  = 180.0,
            ["NE"] = 45.0,
            ["NW"] = 135.0,
            ["SE"] = 315.0,
            ["SW"] = 225.0,
        };

    public static double ResolveDirectionDeg(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0.0;
        if (Directions.TryGetValue(name.Trim(), out var deg)) return deg;
        // Accept bare numeric degrees too ("45", "180").
        if (double.TryParse(name.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var d)) return d;
        return 0.0;
    }

    // ─────────── polish captions ───────────
    public const string CaptionSection   = "PRZEKRÓJ";
    public const string CaptionElevation = "ELEWACJA";
    public const string CaptionScale     = "SKALA";
}
