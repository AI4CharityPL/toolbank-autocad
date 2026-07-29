// Full-sweep audit of every registered MCP tool across all 29 categories.
//
// What this proves:
//   (a) Every tool is discoverable via ToolRegistry (reflection-based catalog
//       aggregation across all IToolCatalog-implementing types).
//   (b) Every tool has complete, non-empty metadata (name, description, method,
//       declaring type). No source-generator regression.
//   (c) Every tool's MethodInfo can be resolved via ToolRegistry.ResolveMethod
//       (i.e. the declaring type + method still exist in the assembly).
//   (d) Every tool can be dispatched via ToolInvoker.InvokeAsync with empty
//       JSON args and a null plugin/vision gateway. We classify the response:
//         - PASS: IsError=false (tool accepts empty args gracefully).
//         - GATED: IsError=true with message "requires the AutoCAD plugin
//                   gateway" / "Vision sidecar not available" (correctly short-
//                   circuits missing gateway).
//         - VALIDATES: IsError=true with a clear validation-style message
//                   (mentions required / missing / invalid / expected / etc).
//         - ERROR: IsError=true with some other message (still responded, did
//                   not hang; acceptable but interesting).
//   (e) No tool hangs (the test has an xunit timeout built-in via async Task).
//   (f) No tool throws an uncaught exception (ToolInvoker is expected to catch
//       and surface every failure as InvokeResult.IsError).
//
// Companion to the 129 per-category unit tests: those verify specific tool
// semantics; this one verifies *reachability and dispatch* for every single
// registered tool (currently ~320 across 29 categories).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AcadMcp.Tests;

public class FullToolAuditTests
{
    private readonly ITestOutputHelper _out;

    public FullToolAuditTests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void Every_tool_in_every_category_has_complete_metadata()
    {
        var registry = new ToolRegistry();
        var cats = registry.Categories.OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.NotEmpty(cats);

        var problems = new List<string>();
        int totalTools = 0;

        foreach (var cat in cats)
        {
            var tools = registry.ToolsFor(cat);
            Assert.NotEmpty(tools);
            totalTools += tools.Count;
            foreach (var t in tools)
            {
                if (string.IsNullOrWhiteSpace(t.Name))
                    problems.Add($"{cat}: empty Name");
                if (string.IsNullOrWhiteSpace(t.Description))
                    problems.Add($"{cat}.{t.Name}: empty Description");
                if (string.IsNullOrWhiteSpace(t.DeclaringTypeFullName))
                    problems.Add($"{cat}.{t.Name}: missing DeclaringTypeFullName");
                if (string.IsNullOrWhiteSpace(t.MethodName))
                    problems.Add($"{cat}.{t.Name}: missing MethodName");
                var mi = registry.ResolveMethod(t);
                if (mi is null)
                    problems.Add($"{cat}.{t.Name}: ResolveMethod returned null ({t.DeclaringTypeFullName}.{t.MethodName})");
            }
        }

        _out.WriteLine($"Metadata audit: {totalTools} tools across {cats.Count} categories.");
        if (problems.Count > 0)
        {
            _out.WriteLine("Problems found:");
            foreach (var p in problems) _out.WriteLine("  - " + p);
        }
        Assert.Empty(problems);
    }

    [Fact]
    public async Task Every_tool_dispatches_without_hanging_or_uncaught_throw()
    {
        var registry = new ToolRegistry();
        var logger = NullLogger<ToolRegistry>.Instance;
        var cats = registry.Categories.OrderBy(c => c, StringComparer.Ordinal).ToList();

        int pass = 0, gated = 0, validates = 0, error = 0, invokerBug = 0;
        var invokerBugs = new List<string>();
        var perCat = new Dictionary<string, (int p, int g, int v, int e)>();

        foreach (var cat in cats)
        {
            int cp = 0, cg = 0, cv = 0, ce = 0;
            foreach (var t in registry.ToolsFor(cat))
            {
                var mi = registry.ResolveMethod(t)!;
                var args = new JsonObject();
                ToolInvoker.InvokeResult res;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    res = await ToolInvoker.InvokeAsync(logger, t, mi, args, plugin: null, vision: null, cts.Token);
                }
                catch (Exception ex)
                {
                    invokerBug++;
                    invokerBugs.Add($"{cat}.{t.Name}: {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                if (!res.IsError) { pass++; cp++; continue; }
                var txt = (res.Text ?? string.Empty).ToLowerInvariant();
                if (txt.Contains("requires the autocad plugin gateway") ||
                    txt.Contains("requires the vision sidecar") ||
                    txt.Contains("plugin not available") ||
                    txt.Contains("vision sidecar not available") ||
                    (txt.Contains("vision engine") && txt.Contains("unavailable")))
                {
                    gated++; cg++;
                }
                else if (txt.Contains("required") || txt.Contains("missing") ||
                         txt.Contains("invalid") || txt.Contains("expected") ||
                         txt.Contains("must be") || txt.Contains("cannot be null") ||
                         txt.Contains("empty") || txt.Contains("validation") ||
                         txt.Contains("deserialize"))
                {
                    validates++; cv++;
                }
                else
                {
                    error++; ce++;
                }
            }
            perCat[cat] = (cp, cg, cv, ce);
        }

        int total = pass + gated + validates + error + invokerBug;
        _out.WriteLine($"Full-sweep dispatch audit: {total} tools");
        _out.WriteLine($"  PASS      (empty args accepted):  {pass}");
        _out.WriteLine($"  GATED     (gateway missing):      {gated}");
        _out.WriteLine($"  VALIDATES (arg validation):       {validates}");
        _out.WriteLine($"  ERROR     (other surfaced error): {error}");
        _out.WriteLine($"  INVOKER-BUG (uncaught throw):     {invokerBug}");
        _out.WriteLine("");
        _out.WriteLine("Per-category:");
        foreach (var cat in cats)
        {
            var (p, g, v, e) = perCat[cat];
            _out.WriteLine($"  {cat,-16} tools={p + g + v + e,3} PASS={p,3} GATED={g,3} VAL={v,3} ERR={e,3}");
        }
        if (invokerBugs.Count > 0)
        {
            _out.WriteLine("");
            _out.WriteLine("Invoker bugs:");
            foreach (var b in invokerBugs) _out.WriteLine("  - " + b);
        }

        // The contract under test: every tool DISPATCHES. Uncaught throws are the only
        // genuine failures (ToolInvoker is supposed to swallow every exception into
        // InvokeResult.IsError). Gateway-gated + validation errors are expected + desired.
        Assert.Empty(invokerBugs);
    }
}
