// Tool metadata records produced by the source generator and consumed by ToolRegistry.

using System;
using System.Collections.Generic;

namespace AcadMcp.Shared.Mcp;

/// <summary>Compile-time-discovered metadata for one MCP tool.</summary>
public sealed record McpToolMetadata(
    string Name,
    string Description,
    string Category,
    IReadOnlyList<string> Intent,
    bool ReadOnly,
    bool ComFallback,
    bool RequiresPlugin,
    ExecutionStrategy Strategy,
    IReadOnlyList<McpParameter> Parameters,
    Type ResultType,
    string DeclaringTypeFullName,
    string MethodName);

/// <summary>One input parameter to a tool method.</summary>
public sealed record McpParameter(
    string Name,
    Type ClrType,
    string JsonName,
    bool Required,
    string? Description,
    object? DefaultValue);

/// <summary>Marker interface implemented by source-generated category catalogs.</summary>
public interface IToolCatalog
{
    /// <summary>Category id without the <c>acad-</c> prefix.</summary>
    string Category { get; }

    /// <summary>All tools defined in this category.</summary>
    IReadOnlyList<McpToolMetadata> Tools { get; }
}
