// Singleton registry: discover + index every Rule available to the running Backend.
//
// Discovery order (later wins on duplicate-id collision):
//   1. Embedded resources under AcadMcp.Backend.validators.*.yaml         (always present)
//   2. <repoRoot>/validators/**/*.yaml                                    (dev mode)
//   3. %LOCALAPPDATA%/AcadMcp/validators/**/*.yaml                        (user-added)
//
// Standards live alongside rules in `_standards/` folders; they are loaded by
// StandardLibrary, NOT by this registry.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Validators;

public sealed class RuleRegistry
{
    private readonly ILogger<RuleRegistry>? _logger;
    private readonly Dictionary<string, Rule> _byId = new(StringComparer.Ordinal);
    private readonly List<string> _loadErrors = new();

    public RuleRegistry(ILogger<RuleRegistry>? logger = null, string? repoRootOverride = null, string? userDirOverride = null)
    {
        _logger = logger;
        try
        {
            LoadEmbedded();
            var repoRoot = repoRootOverride ?? TryDetectRepoRoot();
            if (repoRoot is not null)
            {
                LoadFromDirectory(Path.Combine(repoRoot, "validators"), allowStandards: false);
            }
            var userDir = userDirOverride ?? DefaultUserDir();
            LoadFromDirectory(userDir, allowStandards: false);
            _logger?.LogInformation("Validator rules loaded: {Count} (errors: {Err})", _byId.Count, _loadErrors.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RuleRegistry init failed");
        }
    }

    public IReadOnlyCollection<Rule> All => _byId.Values;
    public IReadOnlyList<string> LoadErrors => _loadErrors;
    public string UserDir => DefaultUserDir();

    public bool TryGet(string id, out Rule? rule)
    {
        var ok = _byId.TryGetValue(id, out var r);
        rule = r;
        return ok;
    }

    public IEnumerable<Rule> Filter(Discipline? discipline = null, Severity? minSeverity = null) =>
        _byId.Values.Where(r =>
            (discipline is null || r.Discipline == discipline) &&
            (minSeverity is null || r.Severity >= minSeverity));

    // ─────────── loaders ───────────

    private void LoadEmbedded()
    {
        var asm = typeof(RuleRegistry).Assembly;
        // Resource names look like: AcadMcp.Backend.validators.general.units-must-be-mm.yaml
        // (Folder separators get replaced with '.' by the SDK.)
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("._standards.", StringComparison.OrdinalIgnoreCase)) continue; // standards loaded separately
            try
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s is null) continue;
                using var r = new StreamReader(s);
                var yaml = r.ReadToEnd();
                var rule = RuleLoader.LoadFromYaml(yaml, "embedded:" + name);
                Insert(rule);
            }
            catch (Exception ex) { Record("embedded:" + name, ex); }
        }
    }

    private void LoadFromDirectory(string dir, bool allowStandards)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var path in Directory.EnumerateFiles(dir, "*.yaml", SearchOption.AllDirectories))
        {
            // Skip standards folder when not explicitly asked.
            if (!allowStandards && path.IndexOf(Path.DirectorySeparatorChar + "_standards" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            try
            {
                var yaml = File.ReadAllText(path);
                var rule = RuleLoader.LoadFromYaml(yaml, "file:" + path);
                Insert(rule);
            }
            catch (Exception ex) { Record("file:" + path, ex); }
        }
    }

    private void Insert(Rule rule)
    {
        // Last-writer wins (filesystem overrides embedded; user-dir overrides repo).
        _byId[rule.Id] = rule;
    }

    private void Record(string source, Exception ex)
    {
        var msg = $"{source}: {ex.Message}";
        _loadErrors.Add(msg);
        _logger?.LogWarning("Rule load error: {Msg}", msg);
    }

    // ─────────── discovery helpers ───────────

    private static string? TryDetectRepoRoot()
    {
        try { return RepoRootDetector.Detect(); }
        catch { return null; }
    }

    private static string DefaultUserDir()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "AcadMcp", "validators");
    }
}
