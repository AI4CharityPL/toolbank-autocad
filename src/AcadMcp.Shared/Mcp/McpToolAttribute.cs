// Marker attribute for MCP tools. Source generator scans for this and validates Intent is non-empty.
// See rule 20-mcp-tool-attribute.md.

using System;

namespace AcadMcp.Shared.Mcp;

/// <summary>
/// Marks a static method as an MCP tool. The source generator <c>AcadMcp.SourceGen</c>
/// produces the tool registry entry and emits a build error if <see cref="Intent"/> is empty
/// or fewer than 5 PL+EN examples are provided combined.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    public McpToolAttribute(string name, string description, string category)
    {
        Name = name;
        Description = description;
        Category = category;
        Intent = Array.Empty<string>();
    }

    /// <summary>Tool name in snake_case, max 5 words. Example: <c>draw_circle_by_3_points</c>.</summary>
    public string Name { get; }

    /// <summary>English description for the LLM. Concise, action-focused.</summary>
    public string Description { get; }

    /// <summary>Category id without the <c>acad-</c> prefix. Must match the file's folder name.</summary>
    public string Category { get; }

    /// <summary>
    /// Intent example phrases used by ToolBank's <c>find_tools</c> for semantic discovery.
    /// REQUIRED: minimum 5 entries combined PL+EN. Half PL, half EN recommended. Examples:
    /// <c>{ "narysuj okrag", "stworz kolo", "draw a circle", "create circle entity", "make round shape" }</c>.
    /// </summary>
    public string[] Intent { get; init; }

    /// <summary>If true, tool falls back to COM Automation when plugin unavailable (e.g. on AutoCAD LT).</summary>
    public bool ComFallback { get; init; } = false;

    /// <summary>If true, tool requires the .NET plugin and will fail with NotSupportedOnLT on LT installs.</summary>
    public bool RequiresPlugin { get; init; } = false;

    /// <summary>If true, tool only reads from the database (no transactions, faster path).</summary>
    public bool ReadOnly { get; init; } = false;

    /// <summary>Optional execution strategy hint. Default <see cref="ExecutionStrategy.Plugin"/>.</summary>
    public ExecutionStrategy Strategy { get; init; } = ExecutionStrategy.Plugin;
}

/// <summary>How a tool is executed in AutoCAD.</summary>
public enum ExecutionStrategy
{
    /// <summary>Via .NET plugin over named pipe (default, structured response).</summary>
    Plugin = 0,

    /// <summary>Via COM Automation (fallback for LT).</summary>
    Com = 1,

    /// <summary>Via LISP script send (legacy/exotic operations).</summary>
    Lisp = 2,

    /// <summary>Pure local computation, no AutoCAD interaction needed.</summary>
    Local = 3,
}
