// YAML -> Rule loader. See rule 33-validators-rule-format.md.
//
// Hard rules enforced here:
//   - id non-empty, lowercase, dot/kebab segments
//   - severity in { error, warning, info }
//   - discipline in canonical enum
//   - description >= 25 chars
//   - if Fix is present, Scope OR a scoping check is required (rule 33 §7)

using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace AcadMcp.Backend.Validators;

public sealed class RuleLoadException : Exception
{
    public string SourceLocation { get; }
    public RuleLoadException(string source, string msg) : base($"{source}: {msg}")
    {
        SourceLocation = source;
    }
}

public static class RuleLoader
{
    public static Rule LoadFromYaml(string yamlText, string sourceLocation)
    {
        if (string.IsNullOrWhiteSpace(yamlText))
            throw new RuleLoadException(sourceLocation, "empty YAML document.");

        using var reader = new System.IO.StringReader(yamlText);
        var stream = new YamlStream();
        try { stream.Load(reader); }
        catch (Exception ex) { throw new RuleLoadException(sourceLocation, "YAML parse error: " + ex.Message); }

        if (stream.Documents.Count == 0)
            throw new RuleLoadException(sourceLocation, "no YAML document found.");

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
            throw new RuleLoadException(sourceLocation, "root must be a mapping.");

        var id = ReqString(root, "id", sourceLocation);
        ValidateId(id, sourceLocation);

        var name = ReqString(root, "name", sourceLocation);
        var severity = ParseSeverity(ReqString(root, "severity", sourceLocation), sourceLocation);
        var discipline = ParseDiscipline(ReqString(root, "discipline", sourceLocation), sourceLocation);
        var description = ReqString(root, "description", sourceLocation);
        if (description.Length < 25)
            throw new RuleLoadException(sourceLocation, "description must be at least 25 chars (got " + description.Length + ").");

        var refs = OptList(root, "references");
        var scope = root.Children.TryGetValue(new YamlScalarNode("scope"), out var sn) && sn is YamlMappingNode sm
            ? ParseScope(sm)
            : null;
        var checks = ParseChecks(ReqMappingOrSeq(root, "checks", sourceLocation), sourceLocation);
        if (checks.Count == 0)
            throw new RuleLoadException(sourceLocation, "checks must contain at least one entry.");

        FixSpec? fix = null;
        if (root.Children.TryGetValue(new YamlScalarNode("fix"), out var fn) && fn is YamlMappingNode fm)
        {
            fix = ParseFix(fm, sourceLocation);
            // Safety: a fix without ANY scoping at all could mass-mutate every entity. Reject.
            bool hasScope = scope is { } s && (
                (s.EntityTypes is { Count: > 0 }) ||
                (s.LayerIn is { Count: > 0 }) ||
                !string.IsNullOrWhiteSpace(s.LayerPattern));
            bool hasScopingCheck = checks.Any(c => IsScopingCheck(c));
            if (!hasScope && !hasScopingCheck)
                throw new RuleLoadException(sourceLocation,
                    "rule with `fix:` MUST also have a `scope:` (entity_types / layer_in / layer_pattern) or a scoping check (layer_equals / layer_in / layer_matches). Blanket fixes are forbidden by rule 33 §7.");
        }

        return new Rule
        {
            Id = id,
            Name = name,
            Severity = severity,
            Discipline = discipline,
            Description = description.TrimEnd(),
            References = refs,
            Scope = scope,
            Checks = checks,
            Fix = fix,
            SourceLocation = sourceLocation,
        };
    }

    private static bool IsScopingCheck(CheckSpec c) =>
        c.Type is "layer_equals" or "layer_in" or "layer_matches";

    // ─────────── helpers ───────────

    private static string ReqString(YamlMappingNode m, string key, string src)
    {
        if (!m.Children.TryGetValue(new YamlScalarNode(key), out var n) || n is not YamlScalarNode s || string.IsNullOrWhiteSpace(s.Value))
            throw new RuleLoadException(src, $"missing required field '{key}'.");
        return s.Value!;
    }

    private static IReadOnlyList<string> OptList(YamlMappingNode m, string key)
    {
        if (!m.Children.TryGetValue(new YamlScalarNode(key), out var n)) return Array.Empty<string>();
        return n switch
        {
            YamlSequenceNode seq => seq.Children.OfType<YamlScalarNode>().Select(x => x.Value ?? "").Where(x => x.Length > 0).ToList(),
            YamlScalarNode sc when !string.IsNullOrWhiteSpace(sc.Value) => new[] { sc.Value! },
            _ => Array.Empty<string>(),
        };
    }

    private static YamlNode ReqMappingOrSeq(YamlMappingNode m, string key, string src)
    {
        if (!m.Children.TryGetValue(new YamlScalarNode(key), out var n))
            throw new RuleLoadException(src, $"missing required field '{key}'.");
        return n;
    }

    private static void ValidateId(string id, string src)
    {
        if (id != id.ToLowerInvariant())
            throw new RuleLoadException(src, $"id '{id}' must be all-lowercase.");
        var parts = id.Split('.');
        if (parts.Length < 2)
            throw new RuleLoadException(src, $"id '{id}' must contain at least one '.' (format: discipline.area.slug).");
        foreach (var p in parts)
        {
            if (string.IsNullOrEmpty(p))
                throw new RuleLoadException(src, $"id '{id}' has empty segment.");
            for (int i = 0; i < p.Length; i++)
            {
                var ch = p[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '-'))
                    throw new RuleLoadException(src, $"id '{id}' has invalid character '{ch}'. Allowed: a-z, 0-9, '-' (segments) and '.' (separators).");
            }
        }
    }

    private static Severity ParseSeverity(string s, string src) => s.ToLowerInvariant() switch
    {
        "error"   => Severity.Error,
        "warning" => Severity.Warning,
        "info"    => Severity.Info,
        _ => throw new RuleLoadException(src, $"severity must be one of error|warning|info, got '{s}'."),
    };

    private static Discipline ParseDiscipline(string s, string src) => s.ToLowerInvariant() switch
    {
        "general"       => Discipline.General,
        "architectural" => Discipline.Architectural,
        "mechanical"    => Discipline.Mechanical,
        "electrical"    => Discipline.Electrical,
        "civil"         => Discipline.Civil,
        "mep"           => Discipline.Mep,
        "parametric"    => Discipline.Parametric,
        _ => throw new RuleLoadException(src, $"discipline must be one of general|architectural|mechanical|electrical|civil|mep|parametric, got '{s}'."),
    };

    private static RuleScope ParseScope(YamlMappingNode m) => new()
    {
        EntityTypes = OptList(m, "entity_types"),
        LayerPattern = m.Children.TryGetValue(new YamlScalarNode("layer_pattern"), out var lp) && lp is YamlScalarNode lps ? lps.Value : null,
        LayerIn = OptList(m, "layer_in"),
        InPaperspace = m.Children.TryGetValue(new YamlScalarNode("in_paperspace"), out var pn) && pn is YamlScalarNode ps && string.Equals(ps.Value, "true", StringComparison.OrdinalIgnoreCase),
    };

    private static IReadOnlyList<CheckSpec> ParseChecks(YamlNode node, string src)
    {
        if (node is not YamlSequenceNode seq)
            throw new RuleLoadException(src, "`checks` must be a sequence (list).");
        var list = new List<CheckSpec>(seq.Children.Count);
        foreach (var item in seq.Children)
        {
            if (item is not YamlMappingNode mn)
                throw new RuleLoadException(src, "every entry in `checks` must be a mapping (e.g. `{ type: layer_equals, value: WALLS }`).");
            list.Add(ParseCheck(mn, src));
        }
        return list;
    }

    private static CheckSpec ParseCheck(YamlMappingNode m, string src)
    {
        var type = ReqString(m, "type", src);
        var children = Array.Empty<CheckSpec>();
        var paramDict = new Dictionary<string, object?>(StringComparer.Ordinal);

        switch (type)
        {
            case "not":
                if (!m.Children.TryGetValue(new YamlScalarNode("check"), out var child) || child is not YamlMappingNode cm)
                    throw new RuleLoadException(src, "`not` check requires nested `check:` mapping.");
                children = new[] { ParseCheck(cm, src) };
                break;
            case "any_of":
            case "all_of":
                if (!m.Children.TryGetValue(new YamlScalarNode("checks"), out var nested))
                    throw new RuleLoadException(src, $"`{type}` check requires nested `checks:` sequence.");
                children = ParseChecks(nested, src).ToArray();
                break;
            default:
                foreach (var kv in m.Children)
                {
                    if (kv.Key is not YamlScalarNode ks || ks.Value == "type") continue;
                    paramDict[ks.Value!] = ScalarOrList(kv.Value);
                }
                break;
        }

        return new CheckSpec { Type = type, Params = paramDict, Children = children };
    }

    private static FixSpec ParseFix(YamlMappingNode m, string src)
    {
        var type = ReqString(m, "type", src);
        var paramDict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kv in m.Children)
        {
            if (kv.Key is not YamlScalarNode ks || ks.Value == "type") continue;
            paramDict[ks.Value!] = ScalarOrList(kv.Value);
        }
        return new FixSpec { Type = type, Params = paramDict };
    }

    /// <summary>YAML scalar to typed primitive: bool / double / int / string. Sequences -> list of those.</summary>
    private static object? ScalarOrList(YamlNode n) => n switch
    {
        YamlScalarNode s => CoerceScalar(s.Value),
        YamlSequenceNode seq => seq.Children.Select(ScalarOrList).ToList(),
        _ => null,
    };

    private static object? CoerceScalar(string? v)
    {
        if (v is null) return null;
        if (string.Equals(v, "true",  StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(v, "null",  StringComparison.OrdinalIgnoreCase)) return null;
        if (long.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i))
            return i;
        if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return v;
    }
}
