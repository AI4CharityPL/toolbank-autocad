using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Backend.Mcp;

/// <summary>An MCP server (router or category) running over stdio JSON-RPC.</summary>
public interface ICategoryServer
{
    Task RunAsync(CancellationToken ct);
}
