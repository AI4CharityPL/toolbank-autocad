// Canonical layers + label-generation helpers for acad-grids.
// See rule 67 §2 (grid axes).

using System;
using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Grids;

internal static class GridsPalette
{
    public const string LayerAxisMajor = "A-GRID";
    public const string LayerAxisMinor = "A-GRID-MINOR";
    public const string LayerBubble    = "A-GRID-BUB";
    public const string LayerAxisNote  = "A-GRID-ID";

    public const double DefaultBubbleRadiusMm = 400.0;
    public const double DefaultExtendMm       = 2000.0;

    // Axis orientations — X-axis = vertical line drawn along Y, X-labels slide along X.
    //   xAxisLabels default to letters (A, B, …, AA, AB, …)  [Polish convention: A, B, C…]
    //   yAxisLabels default to numbers (1, 2, 3, …)           [Polish convention: 1, 2, 3…]
    public static string LetterLabel(int zeroBasedIndex)
    {
        if (zeroBasedIndex < 0) throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
        var buf = new System.Text.StringBuilder();
        int n = zeroBasedIndex;
        do
        {
            int rem = n % 26;
            buf.Insert(0, (char)('A' + rem));
            n = n / 26 - 1;
        } while (n >= 0);
        return buf.ToString();
    }

    public static string NumericLabel(int zeroBasedIndex) => (zeroBasedIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static IReadOnlyList<double> CumulativeOffsets(IReadOnlyList<double> spacings)
    {
        var result = new List<double>(spacings.Count + 1) { 0.0 };
        double sum = 0.0;
        for (int i = 0; i < spacings.Count; i++)
        {
            sum += spacings[i];
            result.Add(sum);
        }
        return result;
    }
}
