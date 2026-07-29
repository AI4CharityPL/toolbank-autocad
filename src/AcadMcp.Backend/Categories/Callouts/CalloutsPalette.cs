// Canonical layers, symbol geometry and sheet presets for the acad-callouts
// composite category. Callouts draw plan symbols (north arrow, scale bar,
// section markers, detail markers, title blocks) from pure geometric
// primitives - no block library is required (rule 35 §2, rule 69).
//
// All dimensions are drawing-unit millimetres. Plotted heights follow the
// ISO 5455 architectural standard: 25-35 mm plot height for key symbols,
// 2.5-5 mm plot height for text. At 1:100 that means the drawing-unit
// callout must be 2500-3500 mm tall (scaleFactor = 100). The palette
// therefore parameterises sizes by plotMm x scaleFactor so callers just
// declare the plan scale once.

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Callouts;

internal static class CalloutsPalette
{
    // ─────────── layers (rule 68) ───────────
    public const string LayerNorth   = "A-ANNO-NORT";
    public const string LayerSbar    = "A-ANNO-SBAR";
    public const string LayerSymb    = "A-ANNO-SYMB";   // section + detail markers
    public const string LayerTtlb    = "A-ANNO-TTLB";
    public const string LayerText    = "A-ANNO-TEXT";
    public const string LayerBorder  = "A-ANNO-BORD";

    // ─────────── default plot sizes (mm on paper) ───────────
    public const double PlotNorthDiameterMm = 30.0;   // 30 mm Ø plotted circle
    public const double PlotScaleBarLengthMm = 50.0;  // 50 mm plotted bar
    public const double PlotSectionMarkerDiameterMm = 10.0;
    public const double PlotDetailMarkerDiameterMm  = 12.0;
    public const double PlotBigTextMm   = 5.0;        // title-block title
    public const double PlotMidTextMm   = 3.5;        // marker letters, labels
    public const double PlotSmallTextMm = 2.5;        // scale bar units, side notes

    // ─────────── canonical plan scales (drawing-mm per plot-mm) ───────────
    public static readonly IReadOnlyDictionary<string, int> Scales =
        new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["1:1"]   = 1,     // paper-space placement (drawing unit = plot mm)
            ["1:5"]   = 5,
            ["1:10"]  = 10,
            ["1:20"]  = 20,
            ["1:25"]  = 25,
            ["1:50"]  = 50,
            ["1:100"] = 100,
            ["1:200"] = 200,
            ["1:500"] = 500,
            ["1:1000"] = 1000,
        };

    public static int ResolveScaleFactor(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 100;
        if (Scales.TryGetValue(name.Trim(), out var f)) return f;
        // Accept bare numeric "100" as 1:100 shorthand.
        if (int.TryParse(name.Trim().TrimStart('1', ':'), out var n) && n > 0) return n;
        return 100;
    }

    // ─────────── ISO sheet formats (mm) ───────────
    public sealed record SheetFormat(string Name, double WidthMm, double HeightMm, double MarginLeftMm, double MarginEdgeMm);

    public static readonly IReadOnlyDictionary<string, SheetFormat> Sheets =
        new Dictionary<string, SheetFormat>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["A0"] = new("A0", 1189, 841, 25, 10),
            ["A1"] = new("A1", 841,  594, 25, 10),
            ["A2"] = new("A2", 594,  420, 25, 10),
            ["A3"] = new("A3", 420,  297, 25,  7),
            ["A4"] = new("A4", 297,  210, 25,  5),
        };

    public static SheetFormat ResolveSheet(string name)
    {
        if (Sheets.TryGetValue((name ?? "A1").Trim(), out var s)) return s;
        return Sheets["A1"];
    }

    // ─────────── scale-bar presets ───────────
    // Returns (segmentLengthMm, segmentCount, segmentValueM). Total plotted length is always ~50 mm.
    public sealed record ScaleBarPreset(double SegmentM, int SegmentCount, string Unit);

    public static ScaleBarPreset ResolveScaleBarPreset(int scaleFactor) => scaleFactor switch
    {
        <= 25  => new ScaleBarPreset(0.5, 5, "m"),
        <= 50  => new ScaleBarPreset(1.0, 5, "m"),
        <= 100 => new ScaleBarPreset(1.0, 5, "m"),
        <= 200 => new ScaleBarPreset(2.0, 5, "m"),
        _      => new ScaleBarPreset(5.0, 5, "m"),
    };

    // ─────────── title block rows (rule 69 §3) ───────────
    public static readonly IReadOnlyList<string> DefaultTitleBlockRows = new[]
    {
        "PROJEKT",
        "INWESTOR",
        "ADRES",
        "BRANŻA",
        "FAZA",
        "STADIUM",
        "RYSUNEK",
        "SKALA",
        "NR RYS.",
        "DATA",
        "PROJEKTANT",
        "SPRAWDZAJĄCY",
    };
}
