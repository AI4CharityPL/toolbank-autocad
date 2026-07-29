// Single source of truth for the electrical-schematic layer key + IEC 81346
// device-tag prefix lookup shipped with acad-electrical. Mirrors rule 39 §11
// + §6 (electrical-domain-traps.md).

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Electrical;

/// <summary>
/// Canonical layer names + colours + linetypes + lineweights for an electrical
/// schematic per IEC + JIC hybrid. Used by <c>ensure_electrical_layers</c>
/// and as default values for every drawing tool in the category.
/// </summary>
internal static class ElectricalPalette
{
    public const string LayerWire     = "E-WIRE";
    public const string LayerWirePwr  = "E-WIRE-PWR";
    public const string LayerWireCtrl = "E-WIRE-CTRL";
    public const string LayerSymbol   = "E-SYMBOL";
    public const string LayerTerm     = "E-TERM";
    public const string LayerLblWire  = "E-LBL-WIRE";
    public const string LayerLblDev   = "E-LBL-DEV";
    public const string LayerLblRung  = "E-LBL-RUNG";
    public const string LayerXref     = "E-XREF";
    public const string LayerTitle    = "E-TITLE";
    public const string LayerPanel    = "E-PANEL";
    public const string LayerNote     = "E-NOTE";

    public sealed record Spec(
        string Name, int AciColor, string Linetype, double LineweightMm, string Purpose, bool Plottable);

    public static IReadOnlyList<Spec> All { get; } = new List<Spec>
    {
        new(LayerWire,     7, "Continuous", 0.30, "signal / control wires",          true),
        new(LayerWirePwr,  1, "Continuous", 0.50, "power rails L1/L2/L3/N/PE",       true),
        new(LayerWireCtrl, 4, "Continuous", 0.25, "low-voltage control wires",       true),
        new(LayerSymbol,   7, "Continuous", 0.30, "symbol bodies",                   true),
        new(LayerTerm,     6, "Continuous", 0.40, "terminal blocks",                 true),
        new(LayerLblWire,  2, "Continuous", 0.18, "wire numbers",                    true),
        new(LayerLblDev,   2, "Continuous", 0.18, "device tags (-K1 / -Q1 / -F1)",   true),
        new(LayerLblRung,  2, "Continuous", 0.25, "rung numbers (left rail)",        true),
        new(LayerXref,     8, "Continuous", 0.18, "coil↔contact cross-references",   true),
        new(LayerTitle,    7, "Continuous", 0.50, "title block geometry",            true),
        new(LayerPanel,    7, "Continuous", 0.50, "panel-layout outlines (Phase 7)", true),
        new(LayerNote,     2, "Continuous", 0.18, "schematic notes",                 true),
    };

    /// <summary>Phase-7 bundled-block roster (rule 39 §12).</summary>
    public static IReadOnlyList<string> PlannedBlocks { get; } = new[]
    {
        "RES_IEC.dwg", "RES_ANSI.dwg",
        "CAP_NONPOL.dwg", "CAP_POL.dwg",
        "CONTACT_NO_IEC.dwg", "CONTACT_NC_IEC.dwg",
        "CONTACT_NO_ANSI.dwg", "CONTACT_NC_ANSI.dwg",
        "COIL_IEC.dwg", "COIL_ANSI.dwg",
        "MOTOR_IEC.dwg", "MOTOR_ANSI.dwg",
        "FUSE_IEC.dwg", "FUSE_ANSI.dwg",
        "LAMP_IEC.dwg", "LAMP_ANSI.dwg",
        "TRANSFORMER_IEC.dwg",
        "JUNCTION_DOT.dwg",
        "TERMINAL_BLOCK_8WAY.dwg", "TERMINAL_BLOCK_12WAY.dwg",
    };
}

/// <summary>IEC 81346-2 device-tag prefix letters (rule 39 §6). The list is
/// consulted by <see cref="DeviceTag"/> at parse time so agents can't invent
/// prefixes.</summary>
internal static class IecDeviceTagPrefixes
{
    public static IReadOnlyDictionary<char, string> Allowed { get; } =
        new Dictionary<char, string>
        {
            ['K'] = "electromechanical relay / contactor",
            ['Q'] = "switch, circuit breaker, motor starter",
            ['F'] = "fuse, protective device",
            ['S'] = "manual control (switch, push-button)",
            ['B'] = "sensor (transducer)",
            ['M'] = "motor",
            ['T'] = "transformer",
            ['G'] = "generator, supply",
            ['X'] = "terminal block",
            ['W'] = "wire / cable",
            ['H'] = "indicator / lamp",
        };
}
