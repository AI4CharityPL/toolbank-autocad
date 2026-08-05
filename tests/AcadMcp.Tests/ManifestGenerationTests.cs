// Contract tests for what a regenerated manifest says about each tool.
//
// The bank's Intent phrasings - 2,387 of them, bilingual, and the source generator refuses to
// build a tool without them - existed for a year while being flattened away on export. Each
// tool's phrasings went into the manifest's category-level intent_examples bag and nowhere
// else, so a discovery layer could tell a request was about styles but had nothing to rank
// create_dimstyle above its twenty siblings with.
//
// Measured over this bank with MCP Nexus, 16 plain-language requests, half of them Polish, both
// registries built by the same script from the same 448 tools: top-3 37% -> 75%, and end to end
// with a frontier model choosing, 56% -> 81%. Nothing about the search changed. That is the
// whole reason these tests exist - the regression they guard against is invisible to every
// other test in this suite, because the tools all still work.
//
// The second test covers a defect introduced while fixing the first: a blanket "regenerate
// every category" loop emptied acad-router.json, whose ten meta-tools live in RouterServer.cs
// rather than in a Categories/ folder. The only symptom was a tool count dropping from 448 to
// 438.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Xunit;

namespace AcadMcp.Tests;

public sealed class ManifestGenerationTests : IDisposable
{
    private readonly string _repoRoot =
        Path.Combine(Path.GetTempPath(), "acadmcp-manifest-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_repoRoot)) Directory.Delete(_repoRoot, recursive: true); }
        catch (IOException) { /* a temp dir we could not remove is not a test failure */ }
    }

    private static McpToolMetadata Tool(string name, params string[] intent) => new(
        Name: name,
        Description: $"Does {name}.",
        Category: "testcat",
        Intent: intent,
        ReadOnly: true,
        ComFallback: false,
        RequiresPlugin: false,
        Strategy: ExecutionStrategy.Plugin,
        Parameters: Array.Empty<McpParameter>(),
        ResultType: typeof(object),
        DeclaringTypeFullName: "AcadMcp.Tests.Fake",
        MethodName: name);

    private JsonObject ReadManifest(string category)
    {
        var path = Path.Combine(_repoRoot, "mcpbank-manifests", $"acad-{category}.json");
        return (JsonNode.Parse(File.ReadAllText(path)) as JsonObject)!;
    }

    [Fact]
    public void Each_tool_carries_its_own_intent_phrasings()
    {
        var tools = new List<McpToolMetadata>
        {
            Tool("draw_wall", "narysuj sciane", "draw a wall"),
            Tool("insert_door", "wstaw drzwi", "put a door in this wall"),
        };

        BankAutoRegister.RegenerateManifest(_repoRoot, "testcat", tools);

        var summary = ReadManifest("testcat")["tools_summary"]!.AsArray();
        var wall = summary.First(t => (string)t!["name"]! == "draw_wall")!;
        var phrasings = wall["intent"]!.AsArray().Select(n => (string)n!).ToArray();

        Assert.Equal(new[] { "narysuj sciane", "draw a wall" }, phrasings);
    }

    [Fact]
    public void Intent_stays_attached_to_the_tool_not_pooled_across_the_category()
    {
        // The bug this guards: every phrasing landing in one category-level bag, so a query
        // matching "wstaw drzwi" ranked draw_wall exactly as highly as insert_door.
        var tools = new List<McpToolMetadata>
        {
            Tool("draw_wall", "narysuj sciane"),
            Tool("insert_door", "wstaw drzwi"),
        };

        BankAutoRegister.RegenerateManifest(_repoRoot, "testcat", tools);

        var summary = ReadManifest("testcat")["tools_summary"]!.AsArray();
        foreach (var entry in summary)
        {
            var name = (string)entry!["name"]!;
            var phrasings = entry["intent"]!.AsArray().Select(n => (string)n!).ToList();
            Assert.Single(phrasings);
            Assert.Equal(name == "draw_wall" ? "narysuj sciane" : "wstaw drzwi", phrasings[0]);
        }
    }

    [Fact]
    public void Blank_phrasings_are_dropped_rather_than_written_as_empty_strings()
    {
        var tools = new List<McpToolMetadata> { Tool("draw_wall", "narysuj sciane", "  ", "") };

        BankAutoRegister.RegenerateManifest(_repoRoot, "testcat", tools);

        var wall = ReadManifest("testcat")["tools_summary"]!.AsArray()
            .First(t => (string)t!["name"]! == "draw_wall")!;
        Assert.Equal(new[] { "narysuj sciane" }, wall["intent"]!.AsArray().Select(n => (string)n!));
    }

    [Fact]
    public void Refuses_to_empty_a_manifest_whose_tools_are_maintained_by_hand()
    {
        // acad-router's ten meta-tools are declared in RouterServer.cs, not with [McpTool] in a
        // Categories/ folder, so the in-process catalogue reports nothing for it. Regenerating
        // must fail loudly instead of deleting them.
        BankAutoRegister.RegenerateManifest(_repoRoot, "router", new[] { Tool("acad_status", "status") });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BankAutoRegister.RegenerateManifest(_repoRoot, "router", Array.Empty<McpToolMetadata>()));

        Assert.Contains("Refusing to regenerate", ex.Message, StringComparison.Ordinal);

        var stillThere = ReadManifest("router")["tools_summary"]!.AsArray();
        Assert.Single(stillThere);
    }

    [Fact]
    public void An_empty_catalogue_is_fine_when_the_manifest_is_also_empty()
    {
        // The guard must not block a genuinely new category that has no tools yet.
        var changed = BankAutoRegister.RegenerateManifest(
            _repoRoot, "brandnew", Array.Empty<McpToolMetadata>());

        Assert.True(changed);
        Assert.Empty(ReadManifest("brandnew")["tools_summary"]!.AsArray());
    }
}
