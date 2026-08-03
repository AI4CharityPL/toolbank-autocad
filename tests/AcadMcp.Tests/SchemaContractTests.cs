// Contract tests for the input schema every tool advertises over MCP.
//
// These exist because of a bug that survived 139 unit tests and a full-sweep audit:
// McpToolGenerator emitted `Parameters: Array.Empty<McpParameter>()` for every tool, so
// all ~340 tools advertised {"type":"object","properties":{}} - in tools/list AND in
// acad_load_category. Every tool still worked when called with the right argument names,
// but there was no way for a client to discover those names short of reading the C#.
//
// FullToolAuditTests dispatches every tool with EMPTY arguments, so an empty schema looked
// entirely healthy to it. The gap was never "does the tool run" but "can a caller find out
// how to call it". That is what these tests cover.
//
// The load-bearing one is Schema_json_names_match_the_args_record: it compares the
// advertised property names against the [JsonPropertyName] attributes that
// System.Text.Json actually binds. If those two ever disagree, a model follows the schema,
// the backend silently binds nothing, and the tool fails with a NullReferenceException or -
// worse - quietly ignores the argument.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Xunit;
using Xunit.Abstractions;

namespace AcadMcp.Tests;

public class SchemaContractTests
{
    private readonly ITestOutputHelper _out;

    public SchemaContractTests(ITestOutputHelper output) { _out = output; }

    /// <summary>
    /// Tools whose payload is genuinely free-form, so a bare {"type":"object"} is the
    /// honest schema rather than a missing one.
    ///   acad_call          - universal dispatcher, forwards args to another tool
    ///   acad_design_iterate- each plan step carries the target tool's own args
    /// </summary>
    private static readonly HashSet<string> FreeFormArgs = new(StringComparer.Ordinal)
    {
        "acad_call",
        "acad_design_iterate",
    };

    private static IEnumerable<(string Category, McpToolMetadata Tool)> AllTools(ToolRegistry registry)
    {
        foreach (var cat in registry.Categories.OrderBy(c => c, StringComparer.Ordinal))
            foreach (var t in registry.ToolsFor(cat))
                yield return (cat, t);
    }

    /// <summary>The caller-supplied args type: not the gateway, not the cancellation token.</summary>
    private static Type? ArgsTypeOf(MethodInfo method) =>
        method.GetParameters()
              .Select(p => p.ParameterType)
              .FirstOrDefault(t => t != typeof(CancellationToken) && !t.IsInterface);

    private static ConstructorInfo? PrimaryCtor(Type argsType) =>
        argsType.GetConstructors()
                .Where(c => c.GetParameters().Length > 0)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

    [Fact]
    public void Every_tool_with_arguments_advertises_them()
    {
        var registry = new ToolRegistry();
        var problems = new List<string>();
        int withArgs = 0, parameterless = 0;

        foreach (var (cat, t) in AllTools(registry))
        {
            var method = registry.ResolveMethod(t);
            if (method is null) continue;

            var argsType = ArgsTypeOf(method);
            var ctor = argsType is null ? null : PrimaryCtor(argsType);

            if (ctor is null) { parameterless++; continue; }
            withArgs++;

            if (t.Parameters.Count == 0)
            {
                problems.Add($"{cat}/{t.Name}: args record {argsType!.Name} has " +
                             $"{ctor.GetParameters().Length} parameter(s) but the tool advertises none");
            }
        }

        _out.WriteLine($"tools with arguments: {withArgs}, genuinely parameterless: {parameterless}");
        Assert.True(problems.Count == 0,
            $"{problems.Count} tool(s) hide their arguments from clients:\n  " + string.Join("\n  ", problems));
        Assert.True(withArgs > 250, $"expected the bulk of the tool bank to take arguments, saw {withArgs}");
    }

    [Fact]
    public void Schema_json_names_match_the_args_record()
    {
        var registry = new ToolRegistry();
        var problems = new List<string>();
        int checkedParams = 0;

        foreach (var (cat, t) in AllTools(registry))
        {
            var method = registry.ResolveMethod(t);
            if (method is null) continue;
            var argsType = ArgsTypeOf(method);
            var ctor = argsType is null ? null : PrimaryCtor(argsType);
            if (ctor is null) continue;

            // What System.Text.Json will actually bind on the wire.
            var bindable = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in ctor.GetParameters())
            {
                var prop = argsType!.GetProperty(p.Name!, BindingFlags.Public | BindingFlags.Instance |
                                                          BindingFlags.IgnoreCase);
                var name = prop?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                           ?? p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                           ?? char.ToLowerInvariant(p.Name![0]) + p.Name!.Substring(1);
                bindable.Add(name);
            }

            foreach (var advertised in t.Parameters)
            {
                checkedParams++;
                if (!bindable.Contains(advertised.JsonName))
                {
                    problems.Add($"{cat}/{t.Name}: advertises '{advertised.JsonName}', which the " +
                                 $"{argsType!.Name} record does not bind (binds: {string.Join(", ", bindable.OrderBy(x => x))})");
                }
            }
        }

        _out.WriteLine($"parameter names cross-checked against their args records: {checkedParams}");
        Assert.True(problems.Count == 0,
            $"{problems.Count} advertised parameter(s) would silently fail to bind:\n  " + string.Join("\n  ", problems));
        Assert.True(checkedParams > 900, $"expected the full parameter surface, saw {checkedParams}");
    }

    [Fact]
    public void Emitted_schemas_are_structurally_valid()
    {
        var registry = new ToolRegistry();
        var problems = new List<string>();

        foreach (var (cat, t) in AllTools(registry))
        {
            var schema = JsonSchemaBuilder.BuildToolSchema(t);

            Assert.Equal("object", schema["type"]?.GetValue<string>());
            var props = schema["properties"] as JsonObject;
            Assert.NotNull(props);

            // A required name that is not among the properties is unsatisfiable.
            if (schema["required"] is JsonArray req)
            {
                foreach (var r in req)
                {
                    var name = r?.GetValue<string>();
                    if (name is not null && props![name] is null)
                        problems.Add($"{cat}/{t.Name}: '{name}' is required but is not a property");
                }
            }

            if (FreeFormArgs.Contains(t.Name)) continue;

            // {"type":"object"} with neither properties nor additionalProperties tells a model
            // nothing. Either the DTO was not expanded, or it is free-form and belongs in
            // FreeFormArgs above.
            //
            // additionalProperties counts as described: that is how a string-keyed dictionary
            // (IReadOnlyDictionary<string,T>, e.g. block attributes) is represented in JSON
            // Schema. It has no fixed property names by definition, and the value schema is
            // what a caller actually needs.
            foreach (var kv in props!)
            {
                if (kv.Value is JsonObject o &&
                    o["type"]?.GetValue<string>() == "object" &&
                    o["properties"] is null &&
                    o["additionalProperties"] is null)
                {
                    problems.Add($"{cat}/{t.Name}: property '{kv.Key}' is an unexpanded object");
                }
            }
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} schema problem(s):\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void Required_parameters_have_no_default_and_optional_ones_are_reachable()
    {
        var registry = new ToolRegistry();
        var problems = new List<string>();

        foreach (var (cat, t) in AllTools(registry))
        {
            foreach (var p in t.Parameters)
            {
                if (p.Required && p.DefaultValue is not null)
                    problems.Add($"{cat}/{t.Name}: '{p.JsonName}' is required yet carries default '{p.DefaultValue}'");

                if (string.IsNullOrWhiteSpace(p.JsonName))
                    problems.Add($"{cat}/{t.Name}: a parameter has an empty wire name");

                if (p.ClrType is null)
                    problems.Add($"{cat}/{t.Name}: '{p.JsonName}' has no CLR type");
            }

            var dupes = t.Parameters.GroupBy(p => p.JsonName, StringComparer.Ordinal)
                                    .Where(g => g.Count() > 1)
                                    .Select(g => g.Key);
            foreach (var d in dupes)
                problems.Add($"{cat}/{t.Name}: parameter name '{d}' is declared more than once");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} parameter problem(s):\n  " + string.Join("\n  ", problems));
    }
}
