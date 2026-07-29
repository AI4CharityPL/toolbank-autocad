using System.Collections.Generic;
using System.Text.Json.Nodes;
using AcadMcp.Companion.Mcp;

namespace AcadMcp.Companion.Agent;

/// <summary>
/// Adapts the tool catalog advertised by the AutoCAD tool-bank server into the
/// vendor-neutral <see cref="ToolDefinition"/> shape the model providers consume.
/// </summary>
public static class AcadToolCatalog
{
    public static IReadOnlyList<ToolDefinition> ToDefinitions(IReadOnlyList<McpToolInfo> tools)
    {
        var defs = new List<ToolDefinition>(tools.Count);
        foreach (var t in tools)
        {
            var schema = t.InputSchema?.DeepClone().AsObject() ?? DefaultSchema();
            if (schema["type"] is null) schema["type"] = "object";
            if (schema["properties"] is null) schema["properties"] = new JsonObject();
            defs.Add(new ToolDefinition(t.Name, t.Description ?? t.Name, schema));
        }
        return defs;
    }

    private static JsonObject DefaultSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
    };
}
