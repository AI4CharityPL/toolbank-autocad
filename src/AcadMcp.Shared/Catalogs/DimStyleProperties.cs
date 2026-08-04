using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>What kind of value a dimension-style property takes.</summary>
public enum DimPropKind
{
    /// <summary>A length or factor in drawing units, or a plain multiplier.</summary>
    Number,
    /// <summary>A small integer selecting one of a fixed set of behaviours.</summary>
    Enumerated,
    /// <summary>An AutoCAD Color Index, 0-256.</summary>
    ColorIndex,
}

/// <summary>One settable property of a dimension style.</summary>
/// <param name="Name">
/// The wire name, which is the AutoCAD DIMVAR without its prefix and in lower camel case
/// (<c>textHeight</c> for DIMTXT). Callers should not have to know DIMVAR spellings to set a
/// text height, and an agent will not guess "dimtxt" from a plain-language request.
/// </param>
/// <param name="DimVar">The underlying DIMVAR, reported so the mapping is never a secret.</param>
public sealed record DimStyleProperty(
    string Name,
    string DimVar,
    DimPropKind Kind,
    string Description,
    double? Min = null,
    double? Max = null);

/// <summary>
/// The dimension-style properties this bank can author, and the single source of truth for
/// what <c>styles.create_dimstyle</c> and <c>styles.modify_dimstyle</c> accept.
/// </summary>
/// <remarks>
/// This lives in AcadMcp.Shared for the same reason the block catalogues do: it is pure data,
/// nothing about a property name and its range needs AutoCAD to evaluate, and putting it here
/// means CI can hold the advertised list and the accepted list to each other on every push.
/// Four defects in one earlier review were a discovery tool advertising what the action tool
/// then refused; a properties dictionary is exactly the shape that invites the fifth.
/// </remarks>
public static class DimStyleProperties
{
    public static IReadOnlyList<DimStyleProperty> All { get; } = new List<DimStyleProperty>
    {
        // ── overall sizing ──
        new("scale",            "DIMSCALE", DimPropKind.Number,
            "Overall scale applied to every size in the style. On a 1:50 sheet this is 50 - it is what makes a 2.5 mm text height plot at 2.5 mm.",
            Min: 0.0001, Max: 100000),
        new("textHeight",       "DIMTXT",   DimPropKind.Number,
            "Text height in paper millimetres before DIMSCALE is applied. 2.5 is the usual architectural value.",
            Min: 0.01, Max: 1000),
        new("arrowSize",        "DIMASZ",   DimPropKind.Number,
            "Arrowhead or tick size, in the same paper units as textHeight.",
            Min: 0.0, Max: 1000),
        new("textGap",          "DIMGAP",   DimPropKind.Number,
            "Gap between the dimension line and the text. Also the box offset when text is boxed.",
            Min: 0.0, Max: 1000),

        // ── extension lines ──
        new("extLineExtend",    "DIMEXE",   DimPropKind.Number,
            "How far extension lines continue past the dimension line.", Min: 0.0, Max: 1000),
        new("extLineOffset",    "DIMEXO",   DimPropKind.Number,
            "Gap between the measured geometry and the start of the extension line, so dimensions do not touch what they measure.",
            Min: 0.0, Max: 1000),

        // ── spacing ──
        new("baselineSpacing",  "DIMDLI",   DimPropKind.Number,
            "Distance between successive dimension lines in a baseline chain.", Min: 0.0, Max: 100000),

        // ── units and rounding ──
        new("decimalPlaces",    "DIMDEC",   DimPropKind.Enumerated,
            "Decimal places shown in the primary measurement. 0 for whole millimetres, which is the norm on architectural plans.",
            Min: 0, Max: 8),
        new("measurementScale", "DIMLFAC",  DimPropKind.Number,
            "Multiplier applied to the measured length before it is displayed. 0.001 turns millimetres into metres on the label without moving anything.",
            Min: -100000, Max: 100000),
        new("roundTo",          "DIMRND",   DimPropKind.Number,
            "Round every measurement to this increment. 1.0 rounds to whole millimetres; 0 disables rounding.",
            Min: 0.0, Max: 100000),
        new("zeroSuppression",  "DIMZIN",   DimPropKind.Enumerated,
            "Which leading and trailing zeros to hide. 8 suppresses trailing zeros, which is what stops 3600.00 reading as clutter.",
            Min: 0, Max: 15),

        // ── text placement ──
        new("textPosition",     "DIMTAD",   DimPropKind.Enumerated,
            "Vertical placement of text: 0 centred in the dimension line, 1 above it (the architectural convention), 2 on the far side, 3 to JIS, 4 below.",
            Min: 0, Max: 4),

        // ── colours ──
        new("dimLineColor",     "DIMCLRD",  DimPropKind.ColorIndex,
            "ACI colour of dimension lines and arrowheads. 0 is ByBlock, 256 is ByLayer.",
            Min: 0, Max: 256),
        new("extLineColor",     "DIMCLRE",  DimPropKind.ColorIndex,
            "ACI colour of extension lines.", Min: 0, Max: 256),
        new("textColor",        "DIMCLRT",  DimPropKind.ColorIndex,
            "ACI colour of dimension text.", Min: 0, Max: 256),
    };

    private static readonly Dictionary<string, DimStyleProperty> ByName =
        All.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every property name this bank accepts, sorted.</summary>
    public static IReadOnlyList<string> Names { get; } =
        All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Resolve a caller-supplied property name and check its value is in range.
    /// </summary>
    /// <exception cref="CatalogNameException">
    /// The name is not one this bank authors, or the value is out of the property's range.
    /// Both are errors rather than a clamp or a skip: silently ignoring an unknown property
    /// would report success over a style that was not changed.
    /// </exception>
    public static DimStyleProperty Resolve(string name, double value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameException(
                "A dimension-style property name is required. Known: " + string.Join(", ", Names) + ".");

        if (!ByName.TryGetValue(name.Trim(), out var prop))
            throw new CatalogNameException(
                $"Unknown dimension-style property '{name}'. Known: " + string.Join(", ", Names) +
                ". Use list_dimstyle_properties for what each one does.");

        if (prop.Min is double min && value < min)
            throw new CatalogNameException(
                $"'{prop.Name}' ({prop.DimVar}) must be at least {min}; got {value}.");
        if (prop.Max is double max && value > max)
            throw new CatalogNameException(
                $"'{prop.Name}' ({prop.DimVar}) must be at most {max}; got {value}.");

        if (prop.Kind != DimPropKind.Number && value != Math.Floor(value))
            throw new CatalogNameException(
                $"'{prop.Name}' ({prop.DimVar}) takes a whole number; got {value}.");

        return prop;
    }
}
