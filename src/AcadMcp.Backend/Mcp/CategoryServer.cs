// Generic MCP server bound to ONE category. Lists/calls only that category's tools.

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Backend.Sidecar;
using AcadMcp.Shared.Mcp;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Mcp;

public sealed class CategoryServer : ICategoryServer, IJsonRpcDispatcher
{
    private readonly ILogger<CategoryServer> _logger;
    private readonly ToolRegistry _registry;
    private readonly StartupOptions _options;
    private readonly IPluginGateway? _plugin;
    private readonly IVisionSidecarClient? _vision;

    public CategoryServer(
        ILogger<CategoryServer> logger,
        ToolRegistry registry,
        StartupOptions options,
        IPluginGateway? plugin = null,
        IVisionSidecarClient? vision = null)
    {
        _logger = logger;
        _registry = registry;
        _options = options;
        _plugin = plugin;
        _vision = vision;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var host = new StdioJsonRpcHost(_logger, this);
        await host.RunAsync(ct).ConfigureAwait(false);
    }

    public Task<JsonObject?> DispatchAsync(JsonObject? request, CancellationToken ct)
    {
        if (request is null) return Task.FromResult<JsonObject?>(null);

        var id = request["id"];
        var method = request["method"]?.GetValue<string>();
        var prms = request["params"] as JsonObject;

        try
        {
            return method switch
            {
                McpMethods.Initialize => Task.FromResult<JsonObject?>(HandleInitialize(id, prms)),
                McpMethods.Initialized => Task.FromResult<JsonObject?>(null), // notification, no response
                McpMethods.ToolsList => Task.FromResult<JsonObject?>(HandleToolsList(id)),
                McpMethods.ToolsCall => HandleToolsCallAsync(id, prms, ct),
                McpMethods.Ping => Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildResult(id, new JsonObject())),
                McpMethods.Shutdown => Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildResult(id, new JsonObject())),
                McpMethods.Exit => Task.FromResult<JsonObject?>(null),
                _ => Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildError(id, -32601, $"Method not found: {method}"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch failed for method {Method}", method);
            return Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildError(id, -32603, "Internal error: " + ex.Message));
        }
    }

    private JsonObject HandleInitialize(JsonNode? id, JsonObject? prms)
    {
        var result = new JsonObject
        {
            ["protocolVersion"] = McpProtocolVersion.Current,
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject { ["listChanged"] = false },
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "acad-" + _options.Category,
                ["version"] = "0.1.0",
            },
            ["instructions"] = $"AutoCAD MCP category '{_options.Category}'. " +
                                $"Tools available: {_registry.ToolsFor(_options.Category).Count}. " +
                                "Use mcpd_get_schema for full per-tool schemas (lazy mode is encouraged).",
        };
        return StdioJsonRpcHost.BuildResult(id, result);
    }

    private JsonObject HandleToolsList(JsonNode? id)
    {
        var tools = _registry.ToolsFor(_options.Category);
        var arr = new JsonArray();
        foreach (var t in tools)
        {
            arr.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = BuildLlmDescription(t),
                ["inputSchema"] = BuildInputSchema(t),
            });
        }
        return StdioJsonRpcHost.BuildResult(id, new JsonObject { ["tools"] = arr });
    }

    private async Task<JsonObject?> HandleToolsCallAsync(JsonNode? id, JsonObject? prms, CancellationToken ct)
    {
        var name = prms?["name"]?.GetValue<string>();
        var args = prms?["arguments"] as JsonObject ?? new JsonObject();

        if (string.IsNullOrEmpty(name))
        {
            return StdioJsonRpcHost.BuildError(id, -32602, "tools/call requires 'name' parameter");
        }

        if (!_registry.TryGetTool(_options.Category, name!, out var meta) || meta is null)
        {
            return StdioJsonRpcHost.BuildError(id, -32601, $"Tool '{name}' not found in category '{_options.Category}'");
        }

        var method = _registry.ResolveMethod(meta);
        if (method is null)
        {
            return StdioJsonRpcHost.BuildError(id, -32603, $"Tool '{name}' has no resolvable method");
        }

        var invoke = await ToolInvoker.InvokeAsync(_logger, meta, method, args, _plugin, _vision, ct).ConfigureAwait(false);
        return StdioJsonRpcHost.BuildResult(id, new JsonObject
        {
            ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = invoke.Text } },
            ["isError"] = invoke.IsError,
        });
    }

    private static string BuildLlmDescription(McpToolMetadata t)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(t.Description);
        if (t.Intent.Count > 0)
        {
            sb.Append("\n\nIntent examples (PL/EN): ");
            sb.Append(string.Join(" | ", t.Intent));
        }
        if (t.RequiresPlugin) sb.Append("\n[Requires AutoCAD .NET plugin - not available on AutoCAD LT]");
        if (t.ReadOnly) sb.Append("\n[Read-only]");
        return sb.ToString();
    }

    private static JsonObject BuildInputSchema(McpToolMetadata t)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var p in t.Parameters)
        {
            props[p.JsonName] = new JsonObject
            {
                ["type"] = MapClrTypeToJson(p.ClrType),
                ["description"] = p.Description ?? "",
            };
            if (p.Required) required.Add(p.JsonName);
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static string MapClrTypeToJson(Type t) => t switch
    {
        _ when t == typeof(string) => "string",
        _ when t == typeof(int) || t == typeof(long) => "integer",
        _ when t == typeof(double) || t == typeof(float) || t == typeof(decimal) => "number",
        _ when t == typeof(bool) => "boolean",
        _ when t.IsArray => "array",
        _ => "object",
    };
}
