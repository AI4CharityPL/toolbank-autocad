// One-line gateway proxy for the acad-images category.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;

namespace AcadMcp.Backend.Categories.Images;

internal static class ImagesProxy
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<TResult> CallAsync<TArgs, TResult>(
        IPluginGateway gateway,
        string pluginToolKey,
        TArgs args,
        int timeoutMs,
        CancellationToken ct)
        where TArgs : class
        where TResult : class
    {
        var node = JsonSerializer.SerializeToNode(args, Opts) as JsonObject ?? new JsonObject();
        var resultNode = await gateway.InvokeAsync(pluginToolKey, node, timeoutMs, ct).ConfigureAwait(false);
        if (resultNode is null)
            throw new System.InvalidOperationException(
                $"Plugin tool '{pluginToolKey}' returned null result for {typeof(TResult).Name}.");
        return resultNode.Deserialize<TResult>(Opts)
            ?? throw new System.InvalidOperationException(
                $"Plugin tool '{pluginToolKey}' returned shape that does not match {typeof(TResult).Name}.");
    }
}
