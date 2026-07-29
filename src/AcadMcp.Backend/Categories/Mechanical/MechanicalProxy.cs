// Composition helper for acad-mechanical (rule 35 §2). Calls primitives from
// acad.geometry2d.*, acad.layers.*, acad.annotations.* over IPluginGateway.
// Layer creation honours the rule 37 §9 lineweight column — mechanical
// drawings rely on visible vs hidden vs centre weighting and silently
// ignoring the field gives unprofessional output.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Mechanical;

internal static class MechanicalProxy
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ---------- layer helpers ----------

    public static async Task<HashSet<string>> ListLayerNamesAsync(IPluginGateway gw, CancellationToken ct)
    {
        var resp = await gw.InvokeAsync("acad.layers.list_layers", new JsonObject(), T_FAST, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException("acad.layers.list_layers returned null");
        var arr = resp["layers"] as JsonArray
                  ?? throw new InvalidPluginShapeException("acad.layers.list_layers missing 'layers' array");
        var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var node in arr)
        {
            var name = node?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(name)) set.Add(name);
        }
        return set;
    }

    /// <summary>Idempotent layer create with full mechanical metadata (color +
    /// linetype + lineweight + plottable). The lineweight column is what
    /// makes mechanical drawings printable — never drop it.</summary>
    public static async Task<bool> EnsureLayerAsync(
        IPluginGateway gw,
        HashSet<string> existing,
        string name,
        int aciColor,
        string linetype,
        double lineweightMm,
        bool plottable,
        CancellationToken ct)
    {
        if (existing.Contains(name)) return false;
        var args = new JsonObject
        {
            ["name"] = name,
            ["color"] = new JsonObject
            {
                ["r"] = 0, ["g"] = 0, ["b"] = 0, ["aciIndex"] = aciColor,
            },
            ["linetype"]     = linetype,
            ["lineweightMm"] = lineweightMm,
            ["plottable"]    = plottable,
        };
        await gw.InvokeAsync("acad.layers.create_layer", args, T_NORMAL, ct).ConfigureAwait(false);
        existing.Add(name);
        return true;
    }

    // ---------- geometry primitives ----------

    public static async Task<EntityHandle> DrawLineAsync(
        IPluginGateway gw, Point2dDto start, Point2dDto end, string layer, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["start"] = JsonSerializer.SerializeToNode(start, Opts),
            ["end"]   = JsonSerializer.SerializeToNode(end,   Opts),
            ["layer"] = layer,
        };
        return await CallEntityAsync(gw, "acad.geometry2d.draw_line", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    public static async Task<EntityHandle> DrawPolylineAsync(
        IPluginGateway gw, IReadOnlyList<Point2dDto> vertices, bool closed, string layer, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["vertices"] = JsonSerializer.SerializeToNode(vertices, Opts),
            ["closed"]   = closed,
            ["layer"]    = layer,
        };
        return await CallEntityAsync(gw, "acad.geometry2d.draw_polyline", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    public static async Task<EntityHandle> DrawCircleAsync(
        IPluginGateway gw, Point2dDto center, double radius, string layer, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["center"] = JsonSerializer.SerializeToNode(center, Opts),
            ["radius"] = radius,
            ["layer"]  = layer,
        };
        return await CallEntityAsync(gw, "acad.geometry2d.draw_circle", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    public static async Task<EntityHandle> DrawArcAsync(
        IPluginGateway gw,
        Point2dDto center,
        double radius,
        double startAngleDeg,
        double endAngleDeg,
        string layer,
        CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["center"]        = JsonSerializer.SerializeToNode(center, Opts),
            ["radius"]        = radius,
            ["startAngleDeg"] = startAngleDeg,
            ["endAngleDeg"]   = endAngleDeg,
            ["layer"]         = layer,
        };
        return await CallEntityAsync(gw, "acad.geometry2d.draw_arc", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    public static async Task<EntityHandle> AddDBTextAsync(
        IPluginGateway gw,
        Point2dDto position,
        string contents,
        double heightMm,
        string layer,
        double rotationDeg,
        string? alignment,
        CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["position"]    = ToPoint3dNode(position),
            ["contents"]    = contents,
            ["height"]      = heightMm,
            ["layer"]       = layer,
            ["rotationDeg"] = rotationDeg,
        };
        if (!string.IsNullOrEmpty(alignment))
            args["alignment"] = alignment;
        return await CallEntityAsync(gw, "acad.annotations.add_dbtext", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    /// <summary>Apply a hatch (e.g. SOLID for filled triangles) inside the named
    /// boundary entities. Returns the hatch handle.</summary>
    public static async Task<EntityHandle> DrawHatchAsync(
        IPluginGateway gw,
        IReadOnlyList<string> boundaryHandles,
        string pattern,
        double scale,
        double angleDeg,
        string layer,
        CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["boundaryHandles"] = JsonSerializer.SerializeToNode(boundaryHandles, Opts),
            ["pattern"]         = pattern,
            ["scale"]           = scale,
            ["angleDeg"]        = angleDeg,
            ["layer"]           = layer,
        };
        return await CallEntityAsync(gw, "acad.geometry2d.draw_hatch", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    // ---------- helpers ----------

    private static async Task<EntityHandle> CallEntityAsync(
        IPluginGateway gw, string toolKey, JsonObject args, int timeoutMs, CancellationToken ct)
    {
        var resp = await gw.InvokeAsync(toolKey, args, timeoutMs, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException($"{toolKey} returned null");
        var entityNode = resp["entity"]
                         ?? throw new InvalidPluginShapeException($"{toolKey} missing 'entity' in result");
        return entityNode.Deserialize<EntityHandle>(Opts)
               ?? throw new InvalidPluginShapeException($"{toolKey} returned non-EntityHandle 'entity'");
    }

    private static JsonObject ToPoint3dNode(Point2dDto p) =>
        new() { ["x"] = p.X, ["y"] = p.Y, ["z"] = 0.0 };
}

internal sealed class InvalidPluginShapeException : System.Exception
{
    public InvalidPluginShapeException(string msg) : base(msg) { }
}
