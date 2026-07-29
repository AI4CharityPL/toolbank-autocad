// acad-grids category. 6 composite tools for column-grid and axis management:
//   * draw_grid          — whole orthogonal grid from spacings
//   * add_grid_axis      — single labeled axis line
//   * add_grid_bubble    — single bubble (circle + label) at a point
//   * list_grid_axes     — enumerate existing axes (layer-filtered, read-only)
//   * snap_to_grid       — pure-backend nearest-intersection snap, read-only
//   * delete_grid        — erase axes + bubbles by layer or by handle list
//
// All drawing calls go through ArchitectureProxy primitives (rule 35 §2).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Grids;

public static class GridsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;
    private const int T_SLOW = 45_000;

    [McpTool("draw_grid",
        "Draw an orthogonal column grid from two lists of spacings (mm). X-axes get letter labels (A, B, C, …), Y-axes get numeric labels (1, 2, 3, …). Bubbles (circle + label) are drawn on the configured sides (default: north + west). Lines and bubbles live on A-GRID / A-GRID-BUB. Rule 67 §1 (grid policy).",
        "grids",
        Intent = new[]
        {
            "narysuj siatke modulowa",
            "draw column grid",
            "generate axis grid",
            "siatka konstrukcyjna",
            "structural grid with bubbles",
        },
        RequiresPlugin = true)]
    public static async Task<DrawGridResult> DrawGrid(IPluginGateway gw, DrawGridArgs args, CancellationToken ct)
    {
        if (args.XSpacingsMm is null) throw new ArgumentException("xSpacingsMm required.");
        if (args.YSpacingsMm is null) throw new ArgumentException("ySpacingsMm required.");

        var xOffsets = GridsPalette.CumulativeOffsets(args.XSpacingsMm);
        var yOffsets = GridsPalette.CumulativeOffsets(args.YSpacingsMm);

        var xLabels = args.XAxisLabels ?? Enumerable.Range(0, xOffsets.Count).Select(GridsPalette.LetterLabel).ToList();
        var yLabels = args.YAxisLabels ?? Enumerable.Range(0, yOffsets.Count).Select(GridsPalette.NumericLabel).ToList();

        if (xLabels.Count < xOffsets.Count) throw new ArgumentException($"xAxisLabels count {xLabels.Count} < required {xOffsets.Count}.");
        if (yLabels.Count < yOffsets.Count) throw new ArgumentException($"yAxisLabels count {yLabels.Count} < required {yOffsets.Count}.");

        double yMin = args.Origin.Y - args.ExtendMm;
        double yMax = args.Origin.Y + yOffsets[yOffsets.Count - 1] + args.ExtendMm;
        double xMin = args.Origin.X - args.ExtendMm;
        double xMax = args.Origin.X + xOffsets[xOffsets.Count - 1] + args.ExtendMm;

        var xAxisResults = new List<AxisEntity>(xOffsets.Count);
        for (int i = 0; i < xOffsets.Count; i++)
        {
            double x = args.Origin.X + xOffsets[i];
            var axisLine = await ArchitectureProxy.DrawLineAsync(gw,
                new Point2dDto(x, yMin), new Point2dDto(x, yMax), args.AxisLayer, ct).ConfigureAwait(false);
            var bubbles = new List<EntityHandle>(2);
            if (args.BubblesNorth) bubbles.Add(await DrawBubbleAsync(gw, new Point2dDto(x, yMax + args.BubbleRadiusMm), xLabels[i], args.BubbleRadiusMm, args.BubbleLayer, ct).ConfigureAwait(false));
            if (args.BubblesSouth) bubbles.Add(await DrawBubbleAsync(gw, new Point2dDto(x, yMin - args.BubbleRadiusMm), xLabels[i], args.BubbleRadiusMm, args.BubbleLayer, ct).ConfigureAwait(false));
            xAxisResults.Add(new AxisEntity(xLabels[i], axisLine, bubbles));
        }

        var yAxisResults = new List<AxisEntity>(yOffsets.Count);
        for (int i = 0; i < yOffsets.Count; i++)
        {
            double y = args.Origin.Y + yOffsets[i];
            var axisLine = await ArchitectureProxy.DrawLineAsync(gw,
                new Point2dDto(xMin, y), new Point2dDto(xMax, y), args.AxisLayer, ct).ConfigureAwait(false);
            var bubbles = new List<EntityHandle>(2);
            if (args.BubblesEast) bubbles.Add(await DrawBubbleAsync(gw, new Point2dDto(xMax + args.BubbleRadiusMm, y), yLabels[i], args.BubbleRadiusMm, args.BubbleLayer, ct).ConfigureAwait(false));
            if (args.BubblesWest) bubbles.Add(await DrawBubbleAsync(gw, new Point2dDto(xMin - args.BubbleRadiusMm, y), yLabels[i], args.BubbleRadiusMm, args.BubbleLayer, ct).ConfigureAwait(false));
            yAxisResults.Add(new AxisEntity(yLabels[i], axisLine, bubbles));
        }

        return new DrawGridResult(
            xAxisResults,
            yAxisResults,
            args.Origin,
            new Point2dDto(xMin, yMin),
            new Point2dDto(xMax, yMax));
    }

    [McpTool("add_grid_axis",
        "Add one labeled grid-axis line. Optionally attaches bubbles (circle + text) at the start and/or end. Axis extends by extendMm past the provided endpoints before the bubble so the bubble sits outside the grid box. Rule 67 §3.",
        "grids",
        Intent = new[]
        {
            "dodaj os modulowa",
            "add grid axis",
            "insert single axis with bubble",
            "os konstrukcyjna z balonikiem",
            "labeled axis line",
        },
        RequiresPlugin = true)]
    public static async Task<AddGridAxisResult> AddGridAxis(IPluginGateway gw, AddGridAxisArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Label)) throw new ArgumentException("label required.");

        double dx = args.End.X - args.Start.X;
        double dy = args.End.Y - args.Start.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) throw new ArgumentException("start and end must be distinct.");
        double ux = dx / len;
        double uy = dy / len;

        var lineStart = new Point2dDto(args.Start.X - ux * args.ExtendMm, args.Start.Y - uy * args.ExtendMm);
        var lineEnd   = new Point2dDto(args.End.X   + ux * args.ExtendMm, args.End.Y   + uy * args.ExtendMm);
        var axisLine = await ArchitectureProxy.DrawLineAsync(gw, lineStart, lineEnd, args.AxisLayer, ct).ConfigureAwait(false);

        var bubbles = new List<EntityHandle>(2);
        if (args.BubbleAtStart)
        {
            var c = new Point2dDto(lineStart.X - ux * args.BubbleRadiusMm, lineStart.Y - uy * args.BubbleRadiusMm);
            bubbles.Add(await DrawBubbleAsync(gw, c, args.Label, args.BubbleRadiusMm, args.BubbleLayer, ct).ConfigureAwait(false));
        }
        if (args.BubbleAtEnd)
        {
            var c = new Point2dDto(lineEnd.X + ux * args.BubbleRadiusMm, lineEnd.Y + uy * args.BubbleRadiusMm);
            bubbles.Add(await DrawBubbleAsync(gw, c, args.Label, args.BubbleRadiusMm, args.BubbleLayer, ct).ConfigureAwait(false));
        }
        return new AddGridAxisResult(axisLine, bubbles);
    }

    [McpTool("add_grid_bubble",
        "Add a single grid bubble: a circle of the given radius plus a centred label. Used to retro-fit bubbles onto existing axis lines or for section / detail callouts.",
        "grids",
        Intent = new[]
        {
            "dodaj balonik osi",
            "add grid bubble",
            "axis bubble circle",
            "insert axis marker",
            "kółko z oznaczeniem osi",
        },
        RequiresPlugin = true)]
    public static async Task<AddGridBubbleResult> AddGridBubble(IPluginGateway gw, AddGridBubbleArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Label)) throw new ArgumentException("label required.");
        if (args.RadiusMm <= 0) throw new ArgumentException("radiusMm must be > 0.");

        var circle = await ArchitectureProxy.DrawCircleAsync(gw, args.Center, args.RadiusMm, args.Layer, ct).ConfigureAwait(false);
        double textH = args.RadiusMm * 0.9;
        // Position the text slightly below-left of centre so DBText baseline sits on centre.
        var textPos = new Point2dDto(args.Center.X - textH * 0.3 * args.Label.Length, args.Center.Y - textH * 0.5);
        var label = await ArchitectureProxy.AddDBTextAsync(gw, textPos, args.Label, textH, args.Layer, 0.0, ct).ConfigureAwait(false);
        return new AddGridBubbleResult(circle, label);
    }

    [McpTool("list_grid_axes",
        "Enumerate handles of all entities living on the grid axis and bubble layers. Read-only; used by validators + callouts to find grid bubbles for intersection queries.",
        "grids",
        Intent = new[]
        {
            "wyswietl siatke osi",
            "list grid axes",
            "enumerate axis bubbles",
            "ile osi siatki w rysunku",
            "grid inventory read-only",
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static async Task<ListGridAxesResult> ListGridAxes(IPluginGateway gw, ListGridAxesArgs args, CancellationToken ct)
    {
        var axisHandles = await SelectByLayerAsync(gw, args.AxisLayer, ct).ConfigureAwait(false);
        var bubbleHandles = await SelectByLayerAsync(gw, args.BubbleLayer, ct).ConfigureAwait(false);
        return new ListGridAxesResult(axisHandles, bubbleHandles, axisHandles.Count, bubbleHandles.Count);
    }

    [McpTool("snap_to_grid",
        "Snap a point to the nearest grid intersection given an origin + two spacing lists. Returns snapped XY, axis labels (A, B, 1, 2…) and distance from the input point. PURE maths, no plugin call — use before drawing to align entities to structural axes. Rule 67 §5.",
        "grids",
        Intent = new[]
        {
            "przyciagnij do siatki",
            "snap point to grid",
            "align to nearest axis",
            "nearest grid intersection",
            "nearest axis for coords",
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static SnapToGridResult SnapToGrid(SnapToGridArgs args)
    {
        if (args is null) throw new ArgumentException("args required.");
        if (args.Point is null) throw new ArgumentException("point required (expected { x, y }).");
        if (args.Origin is null) throw new ArgumentException("origin required (expected { x, y }).");
        if (args.XSpacingsMm is null) throw new ArgumentException("xSpacingsMm required.");
        if (args.YSpacingsMm is null) throw new ArgumentException("ySpacingsMm required.");
        if (args.XSpacingsMm.Count == 0) throw new ArgumentException("xSpacingsMm must contain at least one spacing value.");
        if (args.YSpacingsMm.Count == 0) throw new ArgumentException("ySpacingsMm must contain at least one spacing value.");

        var xOffsets = GridsPalette.CumulativeOffsets(args.XSpacingsMm);
        var yOffsets = GridsPalette.CumulativeOffsets(args.YSpacingsMm);
        int xIdx = NearestIndex(xOffsets, args.Point.X - args.Origin.X);
        int yIdx = NearestIndex(yOffsets, args.Point.Y - args.Origin.Y);
        double sx = args.Origin.X + xOffsets[xIdx];
        double sy = args.Origin.Y + yOffsets[yIdx];

        var xLabels = args.XAxisLabels ?? Enumerable.Range(0, xOffsets.Count).Select(GridsPalette.LetterLabel).ToList();
        var yLabels = args.YAxisLabels ?? Enumerable.Range(0, yOffsets.Count).Select(GridsPalette.NumericLabel).ToList();

        double dx = args.Point.X - sx;
        double dy = args.Point.Y - sy;
        return new SnapToGridResult(
            new Point2dDto(sx, sy),
            xLabels[xIdx],
            yLabels[yIdx],
            xIdx,
            yIdx,
            Math.Sqrt(dx * dx + dy * dy),
            $"{xLabels[xIdx]}/{yLabels[yIdx]}");
    }

    [McpTool("delete_grid",
        "Erase grid axes + bubbles. If handles is provided, only those handles are erased; otherwise every entity on axisLayer + bubbleLayer is erased. Use list_grid_axes first to preview.",
        "grids",
        Intent = new[]
        {
            "usun siatke osi",
            "delete grid",
            "clear grid axes",
            "wyczysc baloniki",
            "remove column grid",
        },
        RequiresPlugin = true)]
    public static async Task<DeleteGridResult> DeleteGrid(IPluginGateway gw, DeleteGridArgs args, CancellationToken ct)
    {
        List<string> toErase;
        string reason;
        if (args.Handles is not null && args.Handles.Count > 0)
        {
            toErase = args.Handles.ToList();
            reason = $"handle list (n={toErase.Count})";
        }
        else
        {
            toErase = new List<string>();
            toErase.AddRange(await SelectByLayerAsync(gw, args.AxisLayer, ct).ConfigureAwait(false));
            toErase.AddRange(await SelectByLayerAsync(gw, args.BubbleLayer, ct).ConfigureAwait(false));
            reason = $"layers [{args.AxisLayer}, {args.BubbleLayer}]";
        }

        if (toErase.Count == 0) return new DeleteGridResult(0, $"no entities matched {reason}");

        var eraseArgs = new JsonObject { ["handles"] = new JsonArray(toErase.Select(h => (JsonNode?)JsonValue.Create(h)).ToArray()) };
        var resp = await gw.InvokeAsync("acad.modify.erase", eraseArgs, T_NORMAL, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException("acad.modify.erase returned null");
        int affected = resp["affected"]?.GetValue<int>() ?? 0;
        return new DeleteGridResult(affected, reason);
    }

    // ─────────── helpers ───────────

    private static async Task<EntityHandle> DrawBubbleAsync(IPluginGateway gw, Point2dDto center, string label, double radiusMm, string layer, CancellationToken ct)
    {
        var circle = await ArchitectureProxy.DrawCircleAsync(gw, center, radiusMm, layer, ct).ConfigureAwait(false);
        double textH = radiusMm * 0.9;
        var textPos = new Point2dDto(center.X - textH * 0.3 * label.Length, center.Y - textH * 0.5);
        await ArchitectureProxy.AddDBTextAsync(gw, textPos, label, textH, layer, 0.0, ct).ConfigureAwait(false);
        return circle;
    }

    private static async Task<IReadOnlyList<string>> SelectByLayerAsync(IPluginGateway gw, string layer, CancellationToken ct)
    {
        var args = new JsonObject { ["layer"] = layer };
        JsonNode? resp;
        try
        {
            resp = await gw.InvokeAsync("acad.selection.select_by_layer", args, T_FAST, ct).ConfigureAwait(false);
        }
        catch (PluginToolException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Plugin throws eArgException when the layer does not yet exist; treat as empty selection.
            return Array.Empty<string>();
        }
        if (resp is null) return Array.Empty<string>();
        var handles = resp["handles"] as JsonArray;
        if (handles is null) return Array.Empty<string>();
        var list = new List<string>(handles.Count);
        foreach (var node in handles)
        {
            var s = node?.GetValue<string>();
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }
        return list;
    }

    private static int NearestIndex(IReadOnlyList<double> sorted, double target)
    {
        int best = 0;
        double bestDist = Math.Abs(sorted[0] - target);
        for (int i = 1; i < sorted.Count; i++)
        {
            double d = Math.Abs(sorted[i] - target);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }
}
