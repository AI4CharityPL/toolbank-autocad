using System;
using System.Text.Json.Nodes;

namespace AcadMcp.Companion.Mcp;

/// <summary>A single tool advertised by the AutoCAD tool-bank server.</summary>
public sealed record McpToolInfo(string Name, string? Description, JsonObject? InputSchema);

/// <summary>Result of a tool invocation: flattened text content + error flag.</summary>
public sealed record McpCallResult(string Text, bool IsError);

/// <summary>Options controlling how the tool-bank server process is located and launched.</summary>
public sealed class McpClientOptions
{
    /// <summary>Absolute path to AcadMcp.Backend.exe (preferred) or AcadMcp.Backend.dll.</summary>
    public string? ServerExecutablePath { get; set; }

    /// <summary>Named pipe the backend uses to reach the AutoCAD plugin. Default "acadmcp".</summary>
    public string PipeName { get; set; } = "acadmcp";

    /// <summary>Hard ceiling for a single tool call, in milliseconds.</summary>
    public int CallTimeoutMs { get; set; } = 120_000;

    /// <summary>
    /// Optional diagnostic sink. Receives connection lifecycle, every tool call/result and
    /// drained backend stderr. Wire to the companion log to debug "agent can't see the drawing".
    /// </summary>
    public Action<string>? Log { get; set; }
}
