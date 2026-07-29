// IPC helper for acad-parametric → acad.parametric.* plugin handlers (rule 35 §2).

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;

namespace AcadMcp.Backend.Categories.Parametric;

internal static class ParametricProxy
{
    public const int T_NormalMs = 60_000;
    public const int T_FastMs   = 5_000;

    internal static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<JsonNode> InvokeAsync(
        IPluginGateway gw, string toolKey, JsonObject args, CancellationToken ct)
    {
        var node = await gw.InvokeAsync(toolKey, args, T_NormalMs, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginResponseException($"{toolKey} returned null result");
        return node;
    }

    public static async Task<bool> EnsureLayerAsync(
        IPluginGateway gw, HashSet<string> existing,
        string name, int aciColor, string linetype, double lineweightMm, bool plottable,
        CancellationToken ct)
    {
        if (existing.Contains(name)) return false;
        var args = new JsonObject
        {
            ["name"] = name,
            ["color"] = new JsonObject { ["r"] = 0, ["g"] = 0, ["b"] = 0, ["aciIndex"] = aciColor },
            ["linetype"]     = linetype,
            ["lineweightMm"] = lineweightMm,
            ["plottable"]    = plottable,
        };
        await gw.InvokeAsync("acad.layers.create_layer", args, T_NormalMs, ct).ConfigureAwait(false);
        existing.Add(name);
        return true;
    }

    public static async Task<HashSet<string>> ListLayerNamesAsync(IPluginGateway gw, CancellationToken ct)
    {
        var resp = await gw.InvokeAsync("acad.layers.list_layers", new JsonObject(), T_FastMs, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginResponseException("acad.layers.list_layers returned null");
        var arr = resp["layers"] as JsonArray
                  ?? throw new InvalidPluginResponseException("missing 'layers'");
        var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var node in arr)
        {
            var n = node?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(n)) set.Add(n);
        }
        return set;
    }
}

internal sealed class InvalidPluginResponseException : System.Exception
{
    public InvalidPluginResponseException(string msg) : base(msg) { }
}
