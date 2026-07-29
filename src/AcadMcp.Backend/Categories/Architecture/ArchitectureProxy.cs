// Composition helper for acad-architecture. Per rule 35 §2 every domain tool
// MUST orchestrate existing primitives (acad.geometry2d.*, acad.blocks.*,
// acad.layers.*, acad.annotations.*, acad.dimensions.*) instead of duplicating
// plugin handlers. This proxy keeps those calls in one place so the tool
// implementations stay readable.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Architecture;

// Visibility note: `ArchitectureProxy` exposes generic primitive gateways
// (draw_line / draw_polyline / draw_circle / draw_arc / add_dbtext /
// dimension_linear / dimension_aligned / create_layer) that are reused by
// every composite-tool category (Verticals, Grids, Callouts, Sections…).
// Keep it `public` so sibling categories can compose without duplicating
// JSON-object plumbing (rule 35 §2).
public static class ArchitectureProxy
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ---------- layer helpers ----------

    /// <summary>Return the set of layer names that already exist in the drawing.</summary>
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

    /// <summary>Idempotent: create the layer only if it isn't already present.</summary>
    public static async Task<bool> EnsureLayerAsync(
        IPluginGateway gw,
        HashSet<string> existing,
        string name,
        int aciColor,
        string linetype,
        CancellationToken ct)
    {
        if (existing.Contains(name)) return false;
        var args = new JsonObject
        {
            ["name"] = name,
            ["color"] = new JsonObject
            {
                ["r"] = 0, ["g"] = 0, ["b"] = 0, ["aciIndex"] = aciColor
            },
            ["linetype"] = linetype,
            ["plottable"] = true,
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
        return await CallEntityAsync(gw, "acad.annotations.add_dbtext", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    public static async Task<EntityHandle> DimensionLinearAsync(
        IPluginGateway gw, Point2dDto a, Point2dDto b, Point2dDto dimLinePoint, string layer, double rotationDeg, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["p1"]            = ToPoint3dNode(a),
            ["p2"]            = ToPoint3dNode(b),
            ["dimLinePoint"]  = ToPoint3dNode(dimLinePoint),
            ["rotationDeg"]   = rotationDeg,
            ["layer"]         = layer,
        };
        return await CallEntityAsync(gw, "acad.dimensions.linear", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    public static async Task<EntityHandle> DimensionAlignedAsync(
        IPluginGateway gw, Point2dDto a, Point2dDto b, Point2dDto dimLinePoint, string layer, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["p1"]            = ToPoint3dNode(a),
            ["p2"]            = ToPoint3dNode(b),
            ["dimLinePoint"]  = ToPoint3dNode(dimLinePoint),
            ["layer"]         = layer,
        };
        return await CallEntityAsync(gw, "acad.dimensions.aligned", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    /// <summary>Promote a Point2dDto to a Point3dDto JSON object (Z=0).</summary>
    private static JsonObject ToPoint3dNode(Point2dDto p) =>
        new() { ["x"] = p.X, ["y"] = p.Y, ["z"] = 0.0 };

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
}

/// <summary>Thrown when a primitive plugin tool returns an unexpected shape.</summary>
public sealed class InvalidPluginShapeException : System.Exception
{
    public InvalidPluginShapeException(string msg) : base(msg) { }
}
