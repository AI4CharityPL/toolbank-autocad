using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>One settable property of a table style.</summary>
/// <param name="ApiName">
/// The TableStyle member behind it. Several are per-row-type rather than per-style — a table
/// has a title row, a header row and data rows, each with its own text height — so the wire
/// name carries the row and this carries the member plus which row it addresses.
/// </param>
/// <param name="RowType">
/// Which row this property applies to, or null when it is a whole-style setting.
/// </param>
public sealed record TableStyleProperty(
    string Name,
    string ApiName,
    DimPropKind Kind,
    string Description,
    string? RowType = null,
    double? Min = null,
    double? Max = null);

/// <summary>
/// Table-style properties this bank can author. Third table in the same family as
/// <see cref="DimStyleProperties"/> and <see cref="MLeaderStyleProperties"/>, with the same
/// contract, for the same reason: one shape to learn, and CI can check the advertised list
/// against the accepted one without AutoCAD.
///
/// <c>FlowDirection</c> is deliberately absent. It reads fine and <b>throws eInvalidInput on
/// every write</b>, on a freshly created style and on a database-resident one alike — measured
/// by setting each property alone and watching only that one fail. Advertising a property this
/// bank cannot actually set is the exact defect these tables exist to prevent, so it is
/// withheld rather than listed. See docs/KNOWN-GAPS.md section B.
/// </summary>
public static class TableStyleProperties
{
    public static IReadOnlyList<TableStyleProperty> All { get; } = new List<TableStyleProperty>
    {
        new("horizontalCellMargin", "HorizontalCellMargin", DimPropKind.Number,
            "Padding between cell content and the cell's left and right edges.", null, 0.0, 1000),
        new("verticalCellMargin",   "VerticalCellMargin",   DimPropKind.Number,
            "Padding between cell content and the cell's top and bottom edges.", null, 0.0, 1000),
        new("titleTextHeight",  "TextHeight", DimPropKind.Number,
            "Text height in the title row - the table's own caption.", "TitleRow", 0.01, 1000),
        new("headerTextHeight", "TextHeight", DimPropKind.Number,
            "Text height in the header row - the column captions.", "HeaderRow", 0.01, 1000),
        new("dataTextHeight",   "TextHeight", DimPropKind.Number,
            "Text height in the data rows - the schedule content itself, and the one worth getting right first.",
            "DataRow", 0.01, 1000),
    };

    private static readonly Dictionary<string, TableStyleProperty> ByName =
        All.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Names { get; } =
        All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Resolve a caller-supplied property name and range-check its value.</summary>
    /// <exception cref="CatalogNameException">Unknown name, or a value outside the property's range.</exception>
    public static TableStyleProperty Resolve(string name, double value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameException(
                "A table-style property name is required. Known: " + string.Join(", ", Names) + ".");

        if (!ByName.TryGetValue(name.Trim(), out var prop))
            throw new CatalogNameException(
                $"Unknown table-style property '{name}'. Known: " + string.Join(", ", Names) +
                ". Use list_tablestyle_properties for what each one does.");

        if (prop.Min is double min && value < min)
            throw new CatalogNameException($"'{prop.Name}' ({prop.ApiName}) must be at least {min}; got {value}.");
        if (prop.Max is double max && value > max)
            throw new CatalogNameException($"'{prop.Name}' ({prop.ApiName}) must be at most {max}; got {value}.");
        if (prop.Kind != DimPropKind.Number && value != Math.Floor(value))
            throw new CatalogNameException($"'{prop.Name}' ({prop.ApiName}) takes a whole number; got {value}.");

        return prop;
    }
}
