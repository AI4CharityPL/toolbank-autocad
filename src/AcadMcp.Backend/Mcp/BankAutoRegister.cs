// Generates / updates an MCPBank manifest for one category from [McpTool] metadata.
// Used by `AcadMcp.Backend.exe --category <name> --regenerate-manifest`.
// See rule 30-mcpbank-manifest.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Mcp;

public static class BankAutoRegister
{
    /// <summary>
    /// Read the existing manifest (if any) for <paramref name="category"/> and update its
    /// <c>tools_summary</c> and <c>intent_examples</c> from in-process <see cref="IToolCatalog"/> data.
    /// Preserves human-edited fields: <c>description</c>, <c>tags</c> (extends only), <c>metadata</c>.
    /// </summary>
    /// <returns>True if file was created or updated; false if no change.</returns>
    public static bool RegenerateManifest(
        string repoRoot,
        string category,
        IReadOnlyList<McpToolMetadata> tools,
        bool createIfMissing = true)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category required", nameof(category));

        var manifestDir = Path.Combine(repoRoot, "mcpbank-manifests");
        Directory.CreateDirectory(manifestDir);
        var manifestPath = Path.Combine(manifestDir, $"acad-{category}.json");
        var launcherPath = Path.Combine(repoRoot, "bin-launchers", $"acad-{category}.cmd");

        JsonObject manifest;
        bool isNew = !File.Exists(manifestPath);

        if (isNew)
        {
            if (!createIfMissing) return false;
            manifest = NewManifestSkeleton(category, launcherPath);
        }
        else
        {
            manifest = (JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject)
                       ?? throw new InvalidOperationException($"Could not parse {manifestPath}");
        }

        var toolsArr = new JsonArray();
        var tagBag = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var intentBag = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in tools.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var derivedTags = DeriveTagsFromName(t.Name);
            foreach (var tag in derivedTags) tagBag.Add(tag);
            foreach (var ex in t.Intent) intentBag.Add(ex);

            toolsArr.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["tags"] = new JsonArray(derivedTags.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()),
            });
        }

        manifest["tools_summary"] = toolsArr;

        if (manifest["intent_examples"] is not JsonArray existingIntents)
        {
            existingIntents = new JsonArray();
            manifest["intent_examples"] = existingIntents;
        }
        foreach (var i in existingIntents.OfType<JsonValue>())
        {
            var s = i.GetValue<string>();
            if (string.IsNullOrWhiteSpace(s)) continue;
            // Skip leftover scaffold placeholders so they don't accumulate forever.
            // See rule 31-mcpbank-discovery-hygiene.md.
            if (s.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (s.StartsWith("(seed)", StringComparison.OrdinalIgnoreCase)) continue;
            intentBag.Add(s);
        }
        manifest["intent_examples"] = new JsonArray(intentBag.OrderBy(s => s).Select(s => (JsonNode)JsonValue.Create(s)).ToArray());

        if (manifest["tags"] is JsonArray existingTags)
        {
            foreach (var t in existingTags.OfType<JsonValue>())
            {
                var s = t.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(s)) tagBag.Add(s);
            }
        }
        manifest["tags"] = new JsonArray(tagBag.OrderBy(s => s).Select(s => (JsonNode)JsonValue.Create(s)).ToArray());

        var meta = manifest["metadata"] as JsonObject ?? new JsonObject();
        meta["category"] = category;
        meta["tool_count_target"] = tools.Count;
        meta["last_regenerated_utc"] = DateTime.UtcNow.ToString("O");
        manifest["metadata"] = meta;

        var serialized = manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        if (!isNew)
        {
            var existing = File.ReadAllText(manifestPath).TrimEnd();
            if (existing == serialized.TrimEnd()) return false;
        }

        File.WriteAllText(manifestPath, serialized);
        return true;
    }

    private static JsonObject NewManifestSkeleton(string category, string launcherPath)
    {
        return new JsonObject
        {
            ["id"] = $"acad-{category}",
            ["name"] = $"acad-{category}",
            ["description"] = $"AutoCAD MCP - category '{category}'. (Auto-generated stub - replace with a real description per rule 31-mcpbank-discovery-hygiene.md.)",
            ["transport"] = new JsonObject
            {
                ["type"] = "stdio",
                ["command"] = launcherPath,
                ["args"] = new JsonArray(),
                ["env"] = new JsonObject(),
            },
            ["lazy_mode"] = true,
            ["tags"] = new JsonArray("autocad", "cad", "dwg", category),
            ["intent_examples"] = new JsonArray(),
            ["tools_summary"] = new JsonArray(),
            ["metadata"] = new JsonObject
            {
                ["category"] = category,
                ["requires_plugin"] = true,
                ["supported_acad_versions"] = new JsonArray("2020", "2021", "2022", "2023", "2024", "2025"),
                ["supported_lt"] = false,
                ["owner"] = "AutoCAD MCP Megasystem",
                ["version"] = "0.1.0",
            },
        };
    }

    private static IEnumerable<string> DeriveTagsFromName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) yield break;
        foreach (var part in toolName.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length >= 2) yield return part.ToLowerInvariant();
        }
    }
}
