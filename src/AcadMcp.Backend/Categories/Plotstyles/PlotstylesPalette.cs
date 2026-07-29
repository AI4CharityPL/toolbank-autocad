// Canonical plot-style presets for the acad-plotstyles category (rule 61).
//
// Lineweight tiers map AutoCAD colour indices to plotted millimetres following
// the tradition of ISO 128 + PN-EN ISO 128 for architectural drawings.
//
// Actual CTB generation is outside scope — the AutoCAD SDK does not expose a
// public API to author CTB binaries programmatically. `ensure_ctb` therefore
// *copies* a pre-authored CTB from the repository asset folder (or a caller-
// supplied path) into AutoCAD's Plot Styles directory.

using System.Collections.Generic;
using System.IO;

namespace AcadMcp.Backend.Categories.Plotstyles;

internal static class PlotstylesPalette
{
    // ─────────── canonical CTB presets shipped with the repo ───────────
    // Files live under <repo>/assets/plotstyles/<name> — each preset may or
    // may not be checked into git (binary CTBs are opt-in). `ensure_ctb`
    // reports the expected source path when the asset is absent so the user
    // knows where to drop a custom CTB.
    public const string HospitalIsoCtb = "HOSPITAL-ISO.ctb";
    public const string IsoStandardCtb = "ISO-Standard.ctb";
    public const string MonochromeCtb  = "monochrome.ctb";

    public static readonly IReadOnlyList<string> DefaultPresets = new[]
    {
        HospitalIsoCtb,
        IsoStandardCtb,
        MonochromeCtb,
    };

    // ─────────── ISO/PN architectural lineweight tiers ───────────
    // AutoCAD colour index → plotted millimetres. Rule 61 §2.
    public static readonly IReadOnlyDictionary<int, double> ArchLineweightMm =
        new Dictionary<int, double>
        {
            [1] = 0.18,  // RED     — construction / hidden / axes (thin)
            [2] = 0.25,  // YELLOW  — door + window frames, fixtures (medium)
            [3] = 0.35,  // GREEN   — walls (interior thick)
            [4] = 0.50,  // CYAN    — load-bearing / section cuts (thick)
            [5] = 0.13,  // BLUE    — hidden lines, phantom (hair)
            [6] = 0.70,  // MAGENTA — fire walls REI, heavy seals (extra-thick)
            [7] = 0.25,  // WHITE/BK — text, general (medium)
            [8] = 0.13,  // DARK GREY — hatches (hair)
            [9] = 0.13,  // LIGHT GREY — secondary annotations (hair)
        };

    // ─────────── plot-styles directory resolution (backend side) ───────────
    // Repo asset directory: <repo>/assets/plotstyles/
    // Walks up from AppContext.BaseDirectory looking for the repo root marker
    // "src/AcadMcp.Backend/AcadMcp.Backend.csproj".
    public static string AssetsDirectory()
    {
        var dir = System.AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var marker = Path.Combine(dir, "src", "AcadMcp.Backend", "AcadMcp.Backend.csproj");
            if (File.Exists(marker))
                return Path.Combine(dir, "assets", "plotstyles");
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        // Fallback — tests running from somewhere unusual.
        return Path.Combine(System.AppContext.BaseDirectory, "assets", "plotstyles");
    }
}
