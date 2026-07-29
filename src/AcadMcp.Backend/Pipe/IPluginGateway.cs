// IPluginGateway is THE only sanctioned way for tool implementations to reach the AutoCAD plugin.
// See rule 18-backend-host-and-gateway.mdc.

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Pipe;

public interface IPluginGateway
{
    /// <summary>Plugin handshake metadata (acad version, vertical, isLT). Null until first successful connect.</summary>
    HandshakeResponse? Handshake { get; }

    /// <summary>True iff the underlying named pipe is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Lazy-connect on first call, then forward a tool invocation to the plugin.
    /// Throws <see cref="PluginUnavailableException"/> if the pipe is unreachable,
    /// or <see cref="PluginToolException"/> if the plugin returned a structured error.
    /// </summary>
    Task<JsonNode?> InvokeAsync(string tool, JsonObject args, int timeoutMs, CancellationToken ct);
}

/// <summary>Thrown when the AutoCAD plugin is not reachable on the named pipe.</summary>
public sealed class PluginUnavailableException : System.Exception
{
    public string PipeName { get; }
    public PluginUnavailableException(string pipeName, string message, System.Exception? inner = null)
        : base(message, inner)
    {
        PipeName = pipeName;
    }
}

/// <summary>Thrown when the plugin returned a structured ToolResponse error.</summary>
public sealed class PluginToolException : System.Exception
{
    public string Code { get; }
    public string ToolName { get; }
    public PluginToolException(string toolName, string code, string message)
        : base($"Plugin tool '{toolName}' failed [{code}]: {message}")
    {
        ToolName = toolName;
        Code = code;
    }
}
