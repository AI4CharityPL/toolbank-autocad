// Generic HTTP-based proxy that serializes args, POSTs to the Python sidecar,
// and deserializes the result. Mirrors the pipe-based proxies for other categories.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Sidecar;

namespace AcadMcp.Backend.Categories.Vision;

internal static class VisionProxy
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<TResult> PostAsync<TArgs, TResult>(
        IVisionSidecarClient sidecar,
        string relativePath,
        TArgs args,
        int timeoutMs,
        CancellationToken ct)
        where TArgs : class
        where TResult : class
    {
        var node = JsonSerializer.SerializeToNode(args, Opts) ?? new JsonObject();
        var resultNode = await sidecar.PostJsonAsync(relativePath, node, timeoutMs, ct).ConfigureAwait(false);
        if (resultNode is null)
            throw new System.InvalidOperationException(
                $"Vision sidecar '{relativePath}' returned null result for {typeof(TResult).Name}.");
        return resultNode.Deserialize<TResult>(Opts)
            ?? throw new System.InvalidOperationException(
                $"Vision sidecar '{relativePath}' returned shape that does not match {typeof(TResult).Name}.");
    }

    public static async Task<TResult> GetAsync<TResult>(
        IVisionSidecarClient sidecar,
        string relativePath,
        int timeoutMs,
        CancellationToken ct)
        where TResult : class
    {
        var resultNode = await sidecar.GetJsonAsync(relativePath, timeoutMs, ct).ConfigureAwait(false);
        if (resultNode is null)
            throw new System.InvalidOperationException(
                $"Vision sidecar '{relativePath}' returned null result for {typeof(TResult).Name}.");
        return resultNode.Deserialize<TResult>(Opts)
            ?? throw new System.InvalidOperationException(
                $"Vision sidecar '{relativePath}' returned shape that does not match {typeof(TResult).Name}.");
    }
}
