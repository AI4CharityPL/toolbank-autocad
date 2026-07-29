// Predicate evaluator for entity-level + doc-level checks.
// One static class with a switch on CheckSpec.Type.
//
// Contract (rule 34 Â§5):  predicates that depend on a field MUST emit Pass when
// the field is null / not applicable (don't fail on missing data).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AcadMcp.Backend.Categories.Validators;

namespace AcadMcp.Backend.Validators;

public sealed record EntityCheckOutcome(bool Pass, string Expected, string Observed, bool Skipped = false);

public sealed record DocCheckOutcome(bool Pass, string Expected, string Observed);

public static class CheckEvaluator
{
    private static readonly Regex CompiledTrue = new("(?:)", RegexOptions.Compiled);

    /// <summary>Evaluate one entity-level check against one entity snapshot.</summary>
    public static EntityCheckOutcome Evaluate(CheckSpec check, EntitySnapshotDto e, EvalContext ctx)
    {
        switch (check.Type)
        {
            case "layer_equals":
            {
                var v = AsString(check.Params, "value");
                bool pass = string.Equals(e.Layer, v, StringComparison.OrdinalIgnoreCase);
                return new(pass, $"layer == '{v}'", $"layer == '{e.Layer}'");
            }
            case "entity_class_equals":
            {
                // Compares the AutoCAD RX class name ("AcDbLine", "AcDbCircle", "AcDbArc", ...).
                // Useful for disciplines that need exact runtime type, not just DXF name
                // (e.g. detect a Circle where an Arc thread relief is expected).
                var v = AsString(check.Params, "value");
                if (string.IsNullOrEmpty(e.ClassName))
                    return new(true, $"class == '{v}'", "no class metadata (skipped)", Skipped: true);
                bool pass = string.Equals(e.ClassName, v, StringComparison.OrdinalIgnoreCase);
                return new(pass, $"class == '{v}'", $"class == '{e.ClassName}'");
            }
            case "text_matches_regex":
            {
                // Broader variant of text_matches: can target an attribute tag or the text value.
                // Params: pattern (required), attribute (optional - block attribute tag).
                var pat = AsString(check.Params, "pattern");
                string? haystack = null;
                string haystackLabel = "text";
                if (check.Params.TryGetValue("attribute", out var attrObj) && attrObj is string tag && !string.IsNullOrWhiteSpace(tag))
                {
                    if (e.Attributes is null) return new(true, $"attribute '{tag}' matches /{pat}/", "not a block reference (skipped)", Skipped: true);
                    if (!e.Attributes.TryGetValue(tag, out var val))
                        return new(false, $"attribute '{tag}' matches /{pat}/", $"attribute '{tag}' missing");
                    haystack = val; haystackLabel = $"attribute '{tag}'";
                }
                else
                {
                    haystack = e.TextValue;
                    if (haystack is null) return new(true, $"text matches /{pat}/", "no text on entity (skipped)", Skipped: true);
                }
                bool pass = ctx.GetRegex(pat).IsMatch(haystack ?? "");
                return new(pass, $"{haystackLabel} matches /{pat}/", $"{haystackLabel} == '{Truncate(haystack ?? "", 60)}'");
            }
            case "polyline_closure_within":
            {
                // Checks first vertex and last vertex are within tolerance (effectively closed).
                // Skips entities without vertex data or with fewer than 2 vertices.
                if (e.Vertices is null || e.Vertices.Length < 2)
                    return new(true, "polyline closure", "no vertex data (skipped)", Skipped: true);
                var tol = AsDouble(check.Params, "tolerance");
                var a = e.Vertices[0]; var b = e.Vertices[^1];
                double dx = a[0] - b[0], dy = a[1] - b[1], dz = (a.Length > 2 && b.Length > 2) ? a[2] - b[2] : 0;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                bool pass = dist <= tol;
                return new(pass, $"closure gap <= {tol}", $"closure gap == {dist:F4}");
            }
            case "polyline_endpoints_share":
            {
                // Cross-entity check: every endpoint (first + last) of this polyline must coincide
                // (within tolerance) with an entity matching the filter.
                // Params: tolerance (double), block_name (string?, optional), layer (string?, optional),
                //         require (bool, default true). When require=false the check becomes "forbidden".
                if (e.Vertices is null || e.Vertices.Length < 2)
                    return new(true, "endpoint sharing", "no vertex data (skipped)", Skipped: true);
                var tol = AsDouble(check.Params, "tolerance");
                string? blockName = check.Params.TryGetValue("block_name", out var bn) ? bn?.ToString() : null;
                string? layer = check.Params.TryGetValue("layer", out var ln) ? ln?.ToString() : null;
                bool require = !check.Params.TryGetValue("require", out var rq) || rq is not bool b || b;

                var endpoints = new[] { e.Vertices[0], e.Vertices[^1] };
                int satisfied = 0;
                foreach (var ep in endpoints)
                {
                    bool match = false;
                    foreach (var other in ctx.AllEntities ?? Array.Empty<EntitySnapshotDto>())
                    {
                        if (ReferenceEquals(other, e)) continue;
                        if (blockName is not null && !string.Equals(other.BlockName, blockName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (layer is not null && !string.Equals(other.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

                        double cx = (other.BboxMin[0] + other.BboxMax[0]) * 0.5;
                        double cy = (other.BboxMin[1] + other.BboxMax[1]) * 0.5;
                        double dx = ep[0] - cx, dy = ep[1] - cy;
                        if (Math.Sqrt(dx * dx + dy * dy) <= tol) { match = true; break; }
                    }
                    if (match) satisfied++;
                }
                string filter = blockName is not null ? $"block '{blockName}'"
                    : layer is not null ? $"layer '{layer}'" : "any entity";
                bool pass = require ? satisfied == endpoints.Length : satisfied == 0;
                return new(pass,
                    require ? $"both endpoints near {filter} (tol {tol})" : $"no endpoint near {filter} (tol {tol})",
                    $"{satisfied}/{endpoints.Length} endpoints matched");
            }
            case "layer_in":
            {
                var values = AsStringList(check.Params, "values");
                bool pass = values.Any(v => string.Equals(e.Layer, v, StringComparison.OrdinalIgnoreCase));
                return new(pass, $"layer in [{string.Join(", ", values)}]", $"layer == '{e.Layer}'");
            }
            case "layer_matches":
            {
                var pat = AsString(check.Params, "pattern");
                var rx = ctx.GetRegex(pat);
                bool pass = rx.IsMatch(e.Layer);
                return new(pass, $"layer matches /{pat}/", $"layer == '{e.Layer}'");
            }
            case "color_equals":
            {
                if (check.Params.TryGetValue("aci", out var aciObj) && aciObj is long aci)
                {
                    if (e.ColorAci is null) return new(true, $"aci == {aci}", "color = ByLayer/ByBlock (skipped)", Skipped: true);
                    bool pass = e.ColorAci == (int)aci;
                    return new(pass, $"aci == {aci}", $"aci == {e.ColorAci}");
                }
                if (check.Params.TryGetValue("rgb", out var rgbObj) && rgbObj is List<object?> rgbList && rgbList.Count == 3)
                {
                    if (e.ColorRgb is null) return new(true, "rgb match", "color = ByLayer/ByBlock (skipped)", Skipped: true);
                    var want = rgbList.Select(x => Convert.ToInt32(x, CultureInfo.InvariantCulture)).ToArray();
                    bool pass = e.ColorRgb[0] == want[0] && e.ColorRgb[1] == want[1] && e.ColorRgb[2] == want[2];
                    return new(pass, $"rgb == [{want[0]},{want[1]},{want[2]}]", $"rgb == [{e.ColorRgb[0]},{e.ColorRgb[1]},{e.ColorRgb[2]}]");
                }
                return new(true, "color_equals (no params)", "skipped", Skipped: true);
            }
            case "color_in":
            {
                var acis = AsIntList(check.Params, "aci");
                if (e.ColorAci is null) return new(true, "aci in list", "color = ByLayer/ByBlock (skipped)", Skipped: true);
                bool pass = acis.Contains(e.ColorAci.Value);
                return new(pass, $"aci in [{string.Join(", ", acis)}]", $"aci == {e.ColorAci}");
            }
            case "linetype_equals":
            {
                var v = AsString(check.Params, "value");
                if (string.IsNullOrEmpty(e.Linetype)) return new(true, $"linetype == '{v}'", "linetype = ByLayer/ByBlock (skipped)", Skipped: true);
                bool pass = string.Equals(e.Linetype, v, StringComparison.OrdinalIgnoreCase);
                return new(pass, $"linetype == '{v}'", $"linetype == '{e.Linetype}'");
            }
            case "lineweight_at_least":
            {
                var v = AsDouble(check.Params, "value_mm");
                if (e.LineweightMm is null) return new(true, $"lineweight >= {v} mm", "lineweight = ByLayer/ByBlock (skipped)", Skipped: true);
                bool pass = e.LineweightMm >= v;
                return new(pass, $"lineweight >= {v} mm", $"lineweight == {e.LineweightMm} mm");
            }
            case "length_at_least": return CompareNullable(e.Length, AsDouble(check.Params, "value"), ">=", "length");
            case "length_at_most":  return CompareNullable(e.Length, AsDouble(check.Params, "value"), "<=", "length");
            case "area_at_least":   return CompareNullable(e.Area,   AsDouble(check.Params, "value"), ">=", "area");
            case "area_at_most":    return CompareNullable(e.Area,   AsDouble(check.Params, "value"), "<=", "area");
            case "radius_at_least": return CompareNullable(e.Radius, AsDouble(check.Params, "value"), ">=", "radius");
            case "radius_at_most":  return CompareNullable(e.Radius, AsDouble(check.Params, "value"), "<=", "radius");
            case "text_matches":
            {
                if (e.TextValue is null) return new(true, "text match", "no text on entity (skipped)", Skipped: true);
                var pat = AsString(check.Params, "pattern");
                bool pass = ctx.GetRegex(pat).IsMatch(e.TextValue);
                return new(pass, $"text matches /{pat}/", $"text == '{Truncate(e.TextValue, 60)}'");
            }
            case "text_height_at_least":
            {
                if (e.TextHeight is null) return new(true, "text height", "no text height (skipped)", Skipped: true);
                var v = AsDouble(check.Params, "value");
                bool pass = e.TextHeight >= v;
                return new(pass, $"text height >= {v}", $"text height == {e.TextHeight}");
            }
            case "attribute_present":
            {
                if (e.Attributes is null) return new(true, "attribute present", "not a block reference (skipped)", Skipped: true);
                var tag = AsString(check.Params, "tag");
                bool pass = e.Attributes.ContainsKey(tag);
                return new(pass, $"attribute '{tag}' present", $"attributes == [{string.Join(",", e.Attributes.Keys)}]");
            }
            case "attribute_value_matches":
            {
                if (e.Attributes is null) return new(true, "attribute match", "not a block reference (skipped)", Skipped: true);
                var tag = AsString(check.Params, "tag");
                if (!e.Attributes.TryGetValue(tag, out var val)) return new(false, $"attribute '{tag}' present and matches", $"attribute '{tag}' missing");
                var pat = AsString(check.Params, "pattern");
                bool pass = ctx.GetRegex(pat).IsMatch(val);
                return new(pass, $"attribute '{tag}' matches /{pat}/", $"'{tag}' = '{Truncate(val, 60)}'");
            }
            case "bbox_inside":
            {
                var min = AsDoubleList(check.Params, "min");
                var max = AsDoubleList(check.Params, "max");
                bool pass = e.BboxMin[0] >= min[0] && e.BboxMin[1] >= min[1] && e.BboxMax[0] <= max[0] && e.BboxMax[1] <= max[1];
                return new(pass,
                    $"bbox inside ({min[0]},{min[1]})â€“({max[0]},{max[1]})",
                    $"bbox = ({e.BboxMin[0]:F1},{e.BboxMin[1]:F1})â€“({e.BboxMax[0]:F1},{e.BboxMax[1]:F1})");
            }
            case "bbox_outside":
            {
                var min = AsDoubleList(check.Params, "min");
                var max = AsDoubleList(check.Params, "max");
                bool overlap = !(e.BboxMax[0] < min[0] || e.BboxMin[0] > max[0] || e.BboxMax[1] < min[1] || e.BboxMin[1] > max[1]);
                bool pass = !overlap;
                return new(pass,
                    $"bbox outside ({min[0]},{min[1]})â€“({max[0]},{max[1]})",
                    $"bbox = ({e.BboxMin[0]:F1},{e.BboxMin[1]:F1})â€“({e.BboxMax[0]:F1},{e.BboxMax[1]:F1})");
            }
            case "not":
            {
                if (check.Children.Count != 1) return new(true, "not", "malformed (skipped)", Skipped: true);
                var inner = Evaluate(check.Children[0], e, ctx);
                return inner with { Pass = !inner.Pass, Expected = "NOT (" + inner.Expected + ")" };
            }
            case "any_of":
            {
                if (check.Children.Count == 0) return new(true, "any_of", "empty (skipped)", Skipped: true);
                var outcomes = check.Children.Select(c => Evaluate(c, e, ctx)).ToList();
                var active = outcomes.Where(o => !o.Skipped).ToList();
                if (active.Count == 0) return new(true, "any_of", "all children skipped", Skipped: true);
                bool pass = active.Any(o => o.Pass);
                return new(pass, "any_of (" + string.Join(" OR ", outcomes.Select(o => o.Expected)) + ")",
                    string.Join(" / ", outcomes.Select(o => o.Observed)));
            }
            case "all_of":
            {
                if (check.Children.Count == 0) return new(true, "all_of", "empty (skipped)", Skipped: true);
                var outcomes = check.Children.Select(c => Evaluate(c, e, ctx)).ToList();
                var active = outcomes.Where(o => !o.Skipped).ToList();
                if (active.Count == 0) return new(true, "all_of", "all children skipped", Skipped: true);
                bool pass = active.All(o => o.Pass);
                var failed = active.FirstOrDefault(o => !o.Pass);
                return new(pass, "all_of (" + string.Join(" AND ", outcomes.Select(o => o.Expected)) + ")",
                    failed is null ? "all passed" : failed.Observed);
            }
            // Doc-level checks accidentally hitting per-entity loop -> treat as no-op pass.
            case "entity_count_at_least":
            case "entity_count_at_most":
            case "layer_must_exist":
            case "block_must_be_defined":
            case "text_style_must_exist":
            case "units_must_be":
                return new(true, check.Type + " (doc-level)", "skipped at entity level", Skipped: true);
            default:
                return new(true, "unknown check type '" + check.Type + "'", "skipped", Skipped: true);
        }
    }

    /// <summary>Evaluate one doc-level check. Returns null if check.Type is entity-level.</summary>
    public static DocCheckOutcome? EvaluateDoc(CheckSpec check, DocSummaryDto doc, EvalContext ctx)
    {
        switch (check.Type)
        {
            case "entity_count_at_least":
            {
                var types = AsStringList(check.Params, "entity_types");
                var v = (int)AsDouble(check.Params, "value");
                int count = SumCounts(doc, types);
                bool pass = count >= v;
                return new(pass, $"count(any of [{string.Join(", ", types)}]) >= {v}", $"count == {count}");
            }
            case "entity_count_at_most":
            {
                var types = AsStringList(check.Params, "entity_types");
                var v = (int)AsDouble(check.Params, "value");
                int count = SumCounts(doc, types);
                bool pass = count <= v;
                return new(pass, $"count(any of [{string.Join(", ", types)}]) <= {v}", $"count == {count}");
            }
            case "layer_must_exist":
            {
                var n = AsString(check.Params, "name");
                bool pass = doc.LayerNames.Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
                return new(pass, $"layer '{n}' must exist", pass ? "present" : "missing");
            }
            case "block_must_be_defined":
            {
                var n = AsString(check.Params, "name");
                bool pass = doc.BlockNames.Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
                return new(pass, $"block '{n}' must be defined", pass ? "present" : "missing");
            }
            case "text_style_must_exist":
            {
                var n = AsString(check.Params, "name");
                bool pass = doc.TextStyleNames.Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
                return new(pass, $"text style '{n}' must exist", pass ? "present" : "missing");
            }
            case "units_must_be":
            {
                var v = AsString(check.Params, "value");
                bool pass = string.Equals(doc.Units, v, StringComparison.OrdinalIgnoreCase);
                return new(pass, $"units == '{v}'", $"units == '{doc.Units}'");
            }
            // Entity-level checks shouldn't end up here.
            default: return null;
        }
    }

    public static bool IsDocLevel(string type) => type is
        "entity_count_at_least" or "entity_count_at_most" or
        "layer_must_exist" or "block_must_be_defined" or
        "text_style_must_exist" or "units_must_be";

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static EntityCheckOutcome CompareNullable(double? value, double threshold, string op, string field)
    {
        if (value is null) return new(true, $"{field} {op} {threshold}", $"no {field} on this entity (skipped)", Skipped: true);
        bool pass = op switch { ">=" => value >= threshold, "<=" => value <= threshold, _ => false };
        return new(pass, $"{field} {op} {threshold}", $"{field} == {value}");
    }

    private static int SumCounts(DocSummaryDto doc, IEnumerable<string> types)
    {
        int sum = 0;
        foreach (var t in types)
        {
            if (doc.EntityCountsByType.TryGetValue(t, out var c)) sum += c;
        }
        return sum;
    }

    private static string AsString(IReadOnlyDictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null) throw new ArgumentException($"check missing required param '{key}'");
        return v.ToString() ?? "";
    }

    private static double AsDouble(IReadOnlyDictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null) throw new ArgumentException($"check missing required param '{key}'");
        return Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string> AsStringList(IReadOnlyDictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null) return Array.Empty<string>();
        if (v is List<object?> list) return list.Select(x => x?.ToString() ?? "").ToList();
        return new[] { v.ToString() ?? "" };
    }

    private static IReadOnlyList<int> AsIntList(IReadOnlyDictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null) return Array.Empty<int>();
        if (v is List<object?> list) return list.Select(x => Convert.ToInt32(x, CultureInfo.InvariantCulture)).ToList();
        return new[] { Convert.ToInt32(v, CultureInfo.InvariantCulture) };
    }

    private static IReadOnlyList<double> AsDoubleList(IReadOnlyDictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null) return Array.Empty<double>();
        if (v is List<object?> list) return list.Select(x => Convert.ToDouble(x, CultureInfo.InvariantCulture)).ToList();
        return new[] { Convert.ToDouble(v, CultureInfo.InvariantCulture) };
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "â€¦";
}

/// <summary>Per-validation-run state. Caches compiled regexes (rule 34 Â§6).</summary>
public sealed class EvalContext
{
    private readonly Dictionary<string, Regex> _regexCache = new(StringComparer.Ordinal);

    /// <summary>
    /// All entities collected for this validation run. Cross-entity predicates
    /// (e.g. polyline_endpoints_share) read from this list; predicates that only look
    /// at the current entity leave it alone. Populated once by ValidationEngine before
    /// the per-entity loop.
    /// </summary>
    public IReadOnlyList<EntitySnapshotDto>? AllEntities { get; set; }

    public Regex GetRegex(string pattern)
    {
        if (!_regexCache.TryGetValue(pattern, out var rx))
        {
            rx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            _regexCache[pattern] = rx;
        }
        return rx;
    }
}

