// Canonical layers, Polish titles and TableStyle presets for the acad-schedules
// composite category. Paired with the plugin-side ensure_table_style primitive
// which persists the preset as a real AutoCAD TableStyle.

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Schedules;

internal static class SchedulesPalette
{
    // ─────────── layers ───────────
    public const string LayerTables    = "A-ANNO-TBLS";
    public const string LayerLegend    = "A-ANNO-LEGN";

    // ─────────── Polish schedule titles ───────────
    public const string TitleDoors   = "ZESTAWIENIE STOLARKI DRZWIOWEJ";
    public const string TitleWindows = "ZESTAWIENIE STOLARKI OKIENNEJ";
    public const string TitleRooms   = "ZESTAWIENIE POMIESZCZEŃ";
    public const string TitleFinish  = "LEGENDA WYKOŃCZEŃ";

    // ─────────── TableStyle preset names ───────────
    public const string StyleHospital = "HOSPITAL-DEF";
    public const string StyleOffice   = "OFFICE-DEF";

    public sealed record TableStylePreset(
        string Name,
        double TitleTextHeight,
        double HeaderTextHeight,
        double BodyTextHeight,
        int TitleFillAci,
        int HeaderFillAci);

    public static readonly IReadOnlyDictionary<string, TableStylePreset> Presets =
        new Dictionary<string, TableStylePreset>(System.StringComparer.OrdinalIgnoreCase)
        {
            // HOSPITAL-DEF — dense, high-contrast. Title on ACI 1 (red), header on 40 (light orange).
            [StyleHospital] = new(StyleHospital, 5.0, 3.5, 2.5, 1, 40),
            // OFFICE-DEF — lighter, title on ACI 5 (blue), header on 9 (light grey).
            [StyleOffice]   = new(StyleOffice,   4.0, 3.0, 2.5, 5, 9),
        };

    // ─────────── default table geometry (drawing-units = mm) ───────────
    public const double DoorScheduleRowHeight  = 10.0;
    public const double WindowScheduleRowHeight = 10.0;
    public const double RoomScheduleRowHeight  = 8.0;
    public const double FinishLegendRowHeight  = 10.0;
    public const double TitleRowHeight         = 14.0;
    public const double HeaderRowHeight        = 10.0;

    // Column widths (mm) — order MUST match the header rows emitted by SchedulesTools.
    public static readonly IReadOnlyList<double> DoorCols =
        new[] { 18.0, 22.0, 18.0, 18.0, 18.0, 20.0, 16.0, 20.0, 30.0, 30.0 };
    public static readonly IReadOnlyList<double> WindowCols =
        new[] { 18.0, 22.0, 18.0, 18.0, 18.0, 20.0, 16.0, 20.0, 30.0 };
    public static readonly IReadOnlyList<double> RoomCols =
        new[] { 22.0, 80.0, 22.0, 30.0 };
    public static readonly IReadOnlyList<double> FinishCols =
        new[] { 20.0, 60.0, 30.0, 40.0, 30.0 };

    // ─────────── headers ───────────
    public static readonly IReadOnlyList<string> DoorHeaders =
        new[] { "NR", "TYP", "SZER. [mm]", "WYS. [mm]", "REI", "OGNIOOCH.", "RC", "DB", "POM. OD", "POM. DO" };
    public static readonly IReadOnlyList<string> WindowHeaders =
        new[] { "NR", "TYP", "SZER. [mm]", "WYS. [mm]", "PARAPET [mm]", "SZYBA", "RC", "DB", "POM." };
    public static readonly IReadOnlyList<string> RoomHeaders =
        new[] { "NR", "NAZWA", "POW. [m²]", "UWAGI" };
    public static readonly IReadOnlyList<string> FinishHeaders =
        new[] { "KOD", "WYKOŃCZENIE", "KOLOR (RAL)", "LOKALIZACJA", "UWAGI" };

    // ─────────── default finish-legend rows (Polish, hospital focus) ───────────
    public static readonly IReadOnlyList<IReadOnlyList<string>> DefaultFinishRows =
        new IReadOnlyList<string>[]
        {
            new[] { "F-01", "Wykładzina PVC homogeniczna, klasa ścieralności T",        "RAL 9002", "Korytarze, dyżurki",         "antypoślizgowa R10" },
            new[] { "F-02", "Wykładzina PVC antystatyczna (sale operacyjne)",           "RAL 6021", "Sale operacyjne, MR",         "antyelektrostatyczna <10^9 Ω" },
            new[] { "F-03", "Gres techniczny 60×60, antypoślizgowy",                     "RAL 7035", "Sanitariaty, pralnia",        "R11, A+B+C" },
            new[] { "F-04", "Epoksyd wylewany 2K",                                       "RAL 7040", "Sterylizacja, laboratorium",  "gładki, zmywalny" },
            new[] { "W-01", "Tynk gipsowy + farba lateksowa zmywalna",                   "RAL 9010", "Sale chorych, gabinety",      "zmywalna klasa 2" },
            new[] { "W-02", "Okładzina HPL antybakteryjna H=2100 mm",                    "RAL 9016", "SOR, korytarze OR",           "z zaokrąglonym narożnikiem" },
            new[] { "W-03", "Glazura 30×60 (biała)",                                     "RAL 9010", "Sanitariaty",                 "zaczyn epoksydowy" },
            new[] { "W-04", "Płyta gipsowo-kartonowa CD+EI60 + farba lateksowa",         "RAL 9010", "Strefa administracyjna",      "klasa ognioodporności EI60" },
            new[] { "C-01", "Sufit modułowy higieniczny 600×600 (klasa C1)",             "RAL 9010", "Sale chorych, korytarze",     "szczelność pyłowa" },
            new[] { "C-02", "Sufit podwieszany gładki (kl. B, zmywalny)",                "RAL 9010", "Sale operacyjne",             "laminarny nawiew" },
            new[] { "C-03", "Strop żelbetowy pod zabudowę mechaniczną",                   "—",        "Pomieszczenia techniczne",    "otwarty lub z malowaniem" },
        };
}
