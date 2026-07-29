// IEC 81346 device-tag parser. Pure C#, no AutoCAD types. Lives in the
// Backend so the source generator and tests can validate it independently
// of a running AutoCAD.
//
// Single source of truth referenced from rule 39 §6 + §6a.

using System;
using System.Text.RegularExpressions;

namespace AcadMcp.Backend.Categories.Electrical;

/// <summary>
/// Parsed IEC 81346 device tag — function aspect (=), location aspect (+),
/// product aspect (-) + prefix letter + sequence number.
/// Examples accepted by <see cref="Parse"/>:
///   <c>-K1</c>          (short product aspect)
///   <c>+CAB1-K1</c>     (location + product)
///   <c>=PWR+CAB1-K1</c> (function + location + product)
///   <c>K1</c>           (short, dash inferred — coerced to <c>-K1</c>)
/// </summary>
public sealed record DeviceTag(
    string? Function,
    string? Location,
    char Prefix,
    string Sequence)
{
    /// <summary>Render in canonical short form: <c>=FUNC+LOC-PREFIXSEQ</c>.</summary>
    public string Canonical
    {
        get
        {
            var fn  = string.IsNullOrEmpty(Function) ? "" : "=" + Function;
            var loc = string.IsNullOrEmpty(Location) ? "" : "+" + Location;
            return $"{fn}{loc}-{Prefix}{Sequence}";
        }
    }

    public static DeviceTag Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("device tag empty");

        // =FUNC+LOC-PREFIXSEQ — function and location are optional, the product
        // aspect (`-PREFIXSEQ`) is required. We allow the leading `-` to be
        // dropped (rule 39 §6a) and re-insert it on canonicalisation.
        var m = Regex.Match(text.Trim(),
            @"^(=([A-Za-z0-9_]+))?(\+([A-Za-z0-9_]+))?-?([A-Za-z])([A-Za-z0-9_]+)$",
            RegexOptions.CultureInvariant);
        if (!m.Success)
            throw new FormatException(
                $"device tag '{text}' does not match IEC 81346 form '[=FUNC][+LOC]-PrefixSeq'");

        var function = m.Groups[2].Success ? m.Groups[2].Value : null;
        var location = m.Groups[4].Success ? m.Groups[4].Value : null;
        char prefix  = char.ToUpperInvariant(m.Groups[5].Value[0]);
        var sequence = m.Groups[6].Value;

        if (!IecDeviceTagPrefixes.Allowed.ContainsKey(prefix))
        {
            var allowed = string.Join(", ", IecDeviceTagPrefixes.Allowed.Keys);
            throw new FormatException(
                $"device tag prefix '{prefix}' not in IEC 81346 set ({allowed}). " +
                "See rule 39 §6 for the full list with meanings.");
        }
        return new DeviceTag(function, location, prefix, sequence);
    }

    /// <summary>Validate without constructing — useful for fast pre-checks.</summary>
    public static bool TryParse(string text, out DeviceTag? tag)
    {
        try { tag = Parse(text); return true; }
        catch { tag = null; return false; }
    }
}
