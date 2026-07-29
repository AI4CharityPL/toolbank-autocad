// Subset of MCP protocol (Model Context Protocol) we implement on the host side.
// Spec ref: https://modelcontextprotocol.io/
//
// Methods supported:
//   initialize, initialized, tools/list, tools/call, ping, shutdown, exit, notifications/cancelled
//
// We do NOT implement resources, prompts, or sampling in Phase 0 - they will be added per-need.

namespace AcadMcp.Backend.Mcp;

public static class McpMethods
{
    public const string Initialize = "initialize";
    public const string Initialized = "notifications/initialized";
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
    public const string Ping = "ping";
    public const string Shutdown = "shutdown";
    public const string Exit = "exit";
    public const string CancelNotification = "notifications/cancelled";
}

public static class McpProtocolVersion
{
    public const string Current = "2025-06-18";
    public const string Fallback = "2024-11-05";
}
