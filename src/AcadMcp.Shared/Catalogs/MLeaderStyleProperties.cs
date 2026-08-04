using System;
using System.Collections.Generic;
using System.Linq;

namespace AcadMcp.Shared.Catalogs;

/// <summary>One settable property of a multileader style.</summary>
/// <param name="Name">The wire name, in plain terms rather than API spelling.</param>
/// <param name="ApiName">The MLeaderStyle member behind it, reported so the mapping is not a secret.</param>
public sealed record MLeaderStyleProperty(
    string Name,
    string ApiName,
    DimPropKind Kind,
    string Description,
    double? Min = null,
    double? Max = null);

/// <summary>
/// Multileader-style properties this bank can author. Same contract as
/// <see cref="DimStyleProperties"/> and, deliberately, the same shape — a caller who has learned
/// one properties map has learned both.
/// </summary>
/// <remarks>
/// Booleans travel as 0 or 1 rather than as a separate JSON type, so the whole argument stays
/// one map of names to numbers. Two value types in one dictionary would mean two ways to be
/// wrong about it.
///
/// <c>AlignSpace</c> is deliberately absent: the 2025 <c>MLeaderStyle</c> has no such member,
/// checked by compiling rather than assumed from the documentation.
/// </remarks>
public static class MLeaderStyleProperties
{
    public static IReadOnlyList<MLeaderStyleProperty> All { get; } = new List<MLeaderStyleProperty>
    {
        new("scale",           "Scale",                   DimPropKind.Number,
            "Overall scale applied to every size in the style, the multileader equivalent of DIMSCALE. On a 1:50 sheet this is 50.",
            Min: 0.0001, Max: 100000),
        new("textHeight",      "TextHeight",              DimPropKind.Number,
            "Height of the leader's text content in paper units, before scale is applied.",
            Min: 0.01, Max: 1000),
        new("arrowSize",       "ArrowSize",               DimPropKind.Number,
            "Arrowhead size at the pointing end of the leader.", Min: 0.0, Max: 1000),
        new("landingGap",      "LandingGap",              DimPropKind.Number,
            "Gap between the end of the landing line and the text it points to.", Min: 0.0, Max: 1000),
        new("doglegLength",    "DoglegLength",            DimPropKind.Number,
            "Length of the horizontal landing segment between the leader line and the text.",
            Min: 0.0, Max: 100000),
        new("breakSize",       "BreakSize",               DimPropKind.Number,
            "Size of the gap left where a leader line crosses another object, when leader breaks are used.",
            Min: 0.0, Max: 1000),
        new("maxLeaderPoints", "MaxLeaderSegmentsPoints", DimPropKind.Enumerated,
            "Maximum number of points a leader line may have. 2 gives a single straight segment, which is the usual architectural convention.",
            Min: 2, Max: 10),
        new("enableLanding",   "EnableLanding",           DimPropKind.Enumerated,
            "Whether the leader has a horizontal landing at the text end. 1 for yes, 0 for no.",
            Min: 0, Max: 1),
        new("enableDogleg",    "EnableDogleg",            DimPropKind.Enumerated,
            "Whether the landing includes the dogleg segment. 1 for yes, 0 for no.", Min: 0, Max: 1),
        new("enableFrameText", "EnableFrameText",         DimPropKind.Enumerated,
            "Whether the text content is boxed. 1 for yes, 0 for no.", Min: 0, Max: 1),
    };

    private static readonly Dictionary<string, MLeaderStyleProperty> ByName =
        All.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Names { get; } =
        All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Resolve a caller-supplied property name and range-check its value.</summary>
    /// <exception cref="CatalogNameException">Unknown name, or a value outside the property's range.</exception>
    public static MLeaderStyleProperty Resolve(string name, double value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameException(
                "A multileader-style property name is required. Known: " + string.Join(", ", Names) + ".");

        if (!ByName.TryGetValue(name.Trim(), out var prop))
            throw new CatalogNameException(
                $"Unknown multileader-style property '{name}'. Known: " + string.Join(", ", Names) +
                ". Use list_mleaderstyle_properties for what each one does.");

        if (prop.Min is double min && value < min)
            throw new CatalogNameException($"'{prop.Name}' ({prop.ApiName}) must be at least {min}; got {value}.");
        if (prop.Max is double max && value > max)
            throw new CatalogNameException($"'{prop.Name}' ({prop.ApiName}) must be at most {max}; got {value}.");
        if (prop.Kind != DimPropKind.Number && value != Math.Floor(value))
            throw new CatalogNameException($"'{prop.Name}' ({prop.ApiName}) takes a whole number; got {value}.");

        return prop;
    }
}
