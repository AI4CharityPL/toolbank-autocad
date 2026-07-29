// Bundled "standards" - presets that resolve to a list of rule ids.
// Loaded from validators/_standards/<id>.yaml (embedded + filesystem).
//
// Schema:
//   id: iso-cad-baseline
//   name: ISO baseline CAD hygiene
//   rules:
//     - general.layers.no-zero-named-entities
//     - ...

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AcadMcp.Backend.Categories.Validators;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging;
using YamlDotNet.RepresentationModel;

namespace AcadMcp.Backend.Validators;

public sealed class StandardLibrary
{
    private readonly Dictionary<string, StandardDescriptor> _byId = new(StringComparer.Ordinal);

    public StandardLibrary(ILogger<StandardLibrary>? logger = null, string? repoRootOverride = null)
    {
        try { LoadEmbedded(); }
        catch (Exception ex) { logger?.LogWarning(ex, "Embedded standards scan failed"); }
        try
        {
            var repo = repoRootOverride ?? RepoRootDetector.Detect();
            if (repo is not null)
                LoadFromDir(Path.Combine(repo, "validators", "_standards"));
        }
        catch (Exception ex) { logger?.LogWarning(ex, "Filesystem standards scan failed"); }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        LoadFromDir(Path.Combine(local, "AcadMcp", "validators", "_standards"));
    }

    public IEnumerable<StandardDescriptor> All => _byId.Values;
    public bool TryGet(string id, out StandardDescriptor? d) { var ok = _byId.TryGetValue(id, out var v); d = v; return ok; }

    private void LoadEmbedded()
    {
        var asm = typeof(StandardLibrary).Assembly;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.Contains("._standards.", StringComparison.OrdinalIgnoreCase)) continue;
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) continue;
            using var r = new StreamReader(s);
            try { var d = ParseYaml(r.ReadToEnd()); if (d is not null) _byId[d.Id] = d; }
            catch { /* swallow; standards are advisory */ }
        }
    }

    private void LoadFromDir(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var path in Directory.EnumerateFiles(dir, "*.yaml", SearchOption.AllDirectories))
        {
            try { var d = ParseYaml(File.ReadAllText(path)); if (d is not null) _byId[d.Id] = d; }
            catch { /* swallow; standards are advisory */ }
        }
    }

    private static StandardDescriptor? ParseYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root) return null;
        var id = (root.Children[new YamlScalarNode("id")] as YamlScalarNode)?.Value;
        var name = (root.Children[new YamlScalarNode("name")] as YamlScalarNode)?.Value ?? id;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;
        var rules = new List<string>();
        if (root.Children.TryGetValue(new YamlScalarNode("rules"), out var rn) && rn is YamlSequenceNode seq)
        {
            rules.AddRange(seq.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? "").Where(s => s.Length > 0));
        }
        return new StandardDescriptor(id!, name!, rules);
    }
}
