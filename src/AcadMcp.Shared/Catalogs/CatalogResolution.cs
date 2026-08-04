// Shared vocabulary for "the caller named a catalogue item; what did they mean?"
//
// Why this lives in AcadMcp.Shared rather than in the plugin, where it is used:
//
// Four defects in one review were a discovery tool advertising something the action tool
// then refused. Two of them were exactly this: list_furniture_catalog and
// list_plumbing_catalog publish family names alongside their default width and depth, but
// the insert tools went straight to parsing a "-W-D" suffix and threw before any family
// lookup happened. 11 of 26 furniture names and 6 of 14 plumbing names could not be
// inserted through the tool that the listing told the agent to use.
//
// The bug was found by hand. It should have been found by a test. It could not be, because
// the catalogues were private static fields inside a plugin assembly that references
// AutoCAD's managed libraries -- which no CI runner can load, so nothing in tests/ could
// see them.
//
// The catalogues are pure data: names, categories, millimetre dimensions, prose. Nothing
// about them needs AutoCAD. Moving them here, together with the name resolution, puts the
// catalogue-vs-consumer contract inside the assembly that CI already builds and tests. The
// plugin keeps what genuinely needs AutoCAD -- turning a resolution into geometry.

using System;

namespace AcadMcp.Shared.Catalogs;

/// <summary>How a caller-supplied block name was matched against a catalogue.</summary>
public enum CatalogMatch
{
    /// <summary>A fixed catalogue entry, drawn from a bespoke recipe at its published size.</summary>
    Fixed,

    /// <summary>
    /// A bare family name, exactly as the listing publishes it, taking the family's own
    /// default dimensions. This is the case that used to throw.
    /// </summary>
    FamilyDefaults,

    /// <summary>A family name carrying an explicit <c>-W-D</c> millimetre suffix.</summary>
    SizedSuffix,
}

/// <summary>
/// Thrown when a name matches no catalogue entry. Deliberately an error rather than a
/// fallback to something plausible: the caller cannot see the drawing, so a silently
/// substituted default becomes a wrong drawing nobody notices.
/// </summary>
public sealed class CatalogNameException : ArgumentException
{
    public CatalogNameException(string message) : base(message) { }
}

/// <summary>Shared helpers for the <c>PREFIX-FAMILY-SUBTYPE-W-D</c> naming convention.</summary>
public static class CatalogNaming
{
    /// <summary>
    /// Split a possibly size-suffixed name into its family and millimetre dimensions.
    /// Returns false when the name carries no valid <c>-W-D</c> suffix.
    /// </summary>
    public static bool TrySplitSized(string name, out string family, out double widthMm, out double depthMm)
    {
        family = name;
        widthMm = 0;
        depthMm = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var parts = name.Split('-');
        // PREFIX-FAMILY-SUBTYPE-W-D is five tokens; fewer cannot carry a suffix.
        if (parts.Length < 5) return false;

        // Explicit arithmetic rather than parts[^2]: AcadMcp.Shared also targets net48,
        // which has no System.Index.
        if (!int.TryParse(parts[parts.Length - 2], out var w)) return false;
        if (!int.TryParse(parts[parts.Length - 1], out var d)) return false;

        // string.Join(char, ...) is not available on net48 either.
        family = string.Join("-", parts, 0, parts.Length - 2);
        widthMm = w;
        depthMm = d;
        return true;
    }
}
