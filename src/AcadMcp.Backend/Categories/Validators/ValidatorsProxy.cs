// One-call proxy to the AutoCAD plugin for the acad-validators category.
// See rule 19 + rule 34 §1 / §3.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;

namespace AcadMcp.Backend.Categories.Validators;

internal static class ValidatorsProxy
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
        var result = await gateway.InvokeAsync(pluginToolKey, node, timeoutMs, ct).ConfigureAwait(false);
        if (result is null)
            throw new System.InvalidOperationException($"Plugin tool '{pluginToolKey}' returned null result for {typeof(TResult).Name}.");
        return result.Deserialize<TResult>(Opts)
               ?? throw new System.InvalidOperationException($"Plugin tool '{pluginToolKey}' returned shape that does not match {typeof(TResult).Name}.");
    }
}
