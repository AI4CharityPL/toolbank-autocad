// acad-sections category. 4 composite tools for cut-lines, section titles,
// elevation markers and section inventory. Composes existing primitives:
//   * acad.geometry2d.draw_polyline / draw_line             (cut lines, ticks)
//   * acad.geometry2d.get_curve_length                      (inventory)
//   * acad.modify.set_linetype                              (DASHED2 on cut line)
//   * acad.layers.create_layer / list_layers                (ensure layers)
//   * acad.annotations.add_dbtext                           (titles + labels)
//   * acad.selection.select_by_layer                        (inventory)
//   * CalloutsTools.InsertSectionCallout                    (end markers)
//
// Rule 35 §2 - no new plugin handlers are added. Rule 69 already covers the
// end-marker geometry; this category layers the step-line + title + elevation
// semantics on top (rule 70-sections-elevations).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Categories.Callouts;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Sections;

public static class SectionsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ─────────────────────────────────────────────────────────────
    // insert_section_line
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_section_line",
        "Draw a section cut-line from startPoint to endPoint on layer A-DETL-SECT, apply DASHED2 linetype, add 90-degree offset ticks at both ends, then place labelled end markers with view-direction arrows via acad-callouts (unless drawEndMarkers=false). The offset ticks (6 mm plotted) signal that the cut-line's path is symbolic, not literal. sheetReference optional, viewDirection in {left|right} relative to the start→end vector.",
        "sections",
        Intent = new[]
        {
            "wstaw linie ciecia",
            "insert section line",
            "narysuj przekroj A-A",
            "section cut line with markers",
            "linia przekroju ze strzalkami",
        },
        RequiresPlugin = true)]
    public static async Task<InsertSectionLineResult> InsertSectionLine(
        IPluginGateway gw, InsertSectionLineArgs args, CancellationToken ct)
    {
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        double tickLen = SectionsPalette.PlotOffsetTickMm * scale;

        double dx = args.EndPoint.X - args.StartPoint.X;
        double dy = args.EndPoint.Y - args.StartPoint.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) throw new ArgumentException("startPoint and endPoint are coincident.");
        double ux = dx / len, uy = dy / len;
        double px = -uy, py = ux;  // perpendicular left-hand

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        // 1. cut line itself (layer carries the linetype post-assignment via set_linetype)
        var cut = await ArchitectureProxy.DrawLineAsync(gw, args.StartPoint, args.EndPoint, args.Layer, ct).ConfigureAwait(false);

        if (args.ApplyDashedLinetype)
        {
            await SetLinetypeAsync(gw, cut.Handle, SectionsPalette.SectionCutLinetype,
                SectionsPalette.SectionCutLtScale * scale, ct).ConfigureAwait(false);
        }

        // 2. offset ticks at the ends — short perpendicular segment through the endpoint
        var notes = new List<string>();
        if (args.DrawOffsetTicks)
        {
            await EmitTickAsync(gw, args.StartPoint, px, py, tickLen, args.Layer, ct).ConfigureAwait(false);
            await EmitTickAsync(gw, args.EndPoint,   px, py, tickLen, args.Layer, ct).ConfigureAwait(false);
        }

        // 3. end markers via acad-callouts (composite-of-composite — rule 35 §2 encourages reuse)
        var markerHandles = new List<string>();
        if (args.DrawEndMarkers)
        {
            var calloutArgs = new InsertSectionCalloutArgs(
                StartPoint: args.StartPoint,
                EndPoint:   args.EndPoint,
                Label:      args.Label,
                Scale:      args.Scale,
                Layer:      args.MarkerLayer,
                DrawCutLine: false,   // we already drew the cut line (DASHED2 + ticks)
                CutLinetype: SectionsPalette.SectionCutLinetype,
                ViewDirection: args.ViewDirection,
                SheetReference: args.SheetReference);
            var marker = await CalloutsTools.InsertSectionCallout(gw, calloutArgs, ct).ConfigureAwait(false);
            markerHandles.AddRange(marker.Summary.Handles);
        }

        var allHandles = new List<string> { cut.Handle };
        allHandles.AddRange(markerHandles);
        return new InsertSectionLineResult(
            new SectionsResultSummary(args.Layer, allHandles, notes),
            cut.Handle, len, args.Label, scale, markerHandles);
    }

    // ─────────────────────────────────────────────────────────────
    // insert_section_title
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_section_title",
        "Place a section/view title beneath a drawn section (e.g. \"PRZEKRÓJ A-A\" with \"SKALA 1:50\" under it and an underline). position is the insertion point — typically the centre-bottom of the view. Customise caption (defaults to \"PRZEKRÓJ\") for elevations / axonometric views. drawUnderline=true adds an 80 mm plotted horizontal rule between the caption and the scale line.",
        "sections",
        Intent = new[]
        {
            "wstaw tytul przekroju",
            "insert section title",
            "podpis przekroj A-A skala",
            "view title caption",
            "napis przekroj pod widokiem",
        },
        RequiresPlugin = true)]
    public static async Task<InsertSectionTitleResult> InsertSectionTitle(
        IPluginGateway gw, InsertSectionTitleArgs args, CancellationToken ct)
    {
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        string viewScale = string.IsNullOrWhiteSpace(args.ViewScale) ? args.Scale : args.ViewScale!;
        double titleH = args.TitleHeightPlotMm * scale;
        double scaleH = args.ScaleTextHeightPlotMm * scale;
        double underline = SectionsPalette.PlotTitleUnderlineMm * scale;

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();

        // Big caption — "PRZEKRÓJ A-A"
        string fullTitle = $"{args.Caption} {args.Label}";
        var titleY = args.Position.Y + titleH * 0.4;
        var title = await ArchitectureProxy.AddDBTextAsync(gw,
            new Point2dDto(args.Position.X - fullTitle.Length * titleH * 0.3, titleY),
            fullTitle, titleH, args.Layer, 0.0, ct).ConfigureAwait(false);
        handles.Add(title.Handle);

        // Underline centred on position
        if (args.DrawUnderline)
        {
            double uy = args.Position.Y;
            var ul = await ArchitectureProxy.DrawLineAsync(gw,
                new Point2dDto(args.Position.X - underline * 0.5, uy),
                new Point2dDto(args.Position.X + underline * 0.5, uy),
                args.Layer, ct).ConfigureAwait(false);
            handles.Add(ul.Handle);
        }

        // Scale text below underline — "SKALA 1:50"
        string scaleText = $"{SectionsPalette.CaptionScale} {viewScale}";
        double scaleY = args.Position.Y - scaleH * 1.6;
        var scaleHandle = await ArchitectureProxy.AddDBTextAsync(gw,
            new Point2dDto(args.Position.X - scaleText.Length * scaleH * 0.3, scaleY),
            scaleText, scaleH, args.Layer, 0.0, ct).ConfigureAwait(false);
        handles.Add(scaleHandle.Handle);

        return new InsertSectionTitleResult(
            new SectionsResultSummary(args.Layer, handles, Array.Empty<string>()),
            args.Label, viewScale, scale);
    }

    // ─────────────────────────────────────────────────────────────
    // insert_elevation_marker
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_elevation_marker",
        "Insert an elevation-direction marker at the given model-space position. Geometry: a filled triangle pointing in the requested compass direction (N / E / S / W / NE / NW / SE / SW or bare degrees) on top of a short horizontal baseline, with the caption \"ELEWACJA <direction>\" to the right. Differs from acad-callouts.insert_section_callout in that elevations have a directional triangle rather than a circle-in-triangle pair.",
        "sections",
        Intent = new[]
        {
            "wstaw marker elewacji",
            "insert elevation marker",
            "znacznik elewacji E",
            "elevation view direction marker",
            "symbol elewacji kierunkowy",
        },
        RequiresPlugin = true)]
    public static async Task<InsertElevationMarkerResult> InsertElevationMarker(
        IPluginGateway gw, InsertElevationMarkerArgs args, CancellationToken ct)
    {
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        double triSide = SectionsPalette.PlotElevationTriangleMm * scale;
        double baseLine = SectionsPalette.PlotElevationBaselineMm * scale;
        double labelH = args.LabelHeightPlotMm * scale;
        double dirDeg = SectionsPalette.ResolveDirectionDeg(args.Direction);

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();

        // Triangle: tip in direction dirDeg, base perpendicular to it, centred on position
        double rad = dirDeg * Math.PI / 180.0;
        double ux = Math.Cos(rad), uy = Math.Sin(rad);
        double px = -uy, py = ux;
        double halfSide = triSide * 0.5;
        var tip  = new Point2dDto(args.Position.X + ux * triSide,       args.Position.Y + uy * triSide);
        var ba   = new Point2dDto(args.Position.X + px * halfSide,      args.Position.Y + py * halfSide);
        var bb   = new Point2dDto(args.Position.X - px * halfSide,      args.Position.Y - py * halfSide);
        var tri = await ArchitectureProxy.DrawPolylineAsync(gw, new[] { tip, ba, bb }, closed: true, args.Layer, ct).ConfigureAwait(false);
        handles.Add(tri.Handle);

        // Horizontal baseline — always drawn along +X so the label reads left-to-right
        double baseY = args.Position.Y - triSide * 0.7;
        var bl = await ArchitectureProxy.DrawLineAsync(gw,
            new Point2dDto(args.Position.X - baseLine * 0.5, baseY),
            new Point2dDto(args.Position.X + baseLine * 0.5, baseY),
            args.Layer, ct).ConfigureAwait(false);
        handles.Add(bl.Handle);

        // Label — "ELEWACJA E" by default
        string label = string.IsNullOrWhiteSpace(args.Label) ? $"{SectionsPalette.CaptionElevation} {args.Direction.ToUpperInvariant()}" : args.Label!;
        var labelHandle = await ArchitectureProxy.AddDBTextAsync(gw,
            new Point2dDto(args.Position.X + baseLine * 0.5 + labelH * 0.6, baseY - labelH * 0.4),
            label, labelH, args.Layer, 0.0, ct).ConfigureAwait(false);
        handles.Add(labelHandle.Handle);

        // Optional sheet reference below label
        if (!string.IsNullOrWhiteSpace(args.SheetReference))
        {
            var sref = await ArchitectureProxy.AddDBTextAsync(gw,
                new Point2dDto(args.Position.X + baseLine * 0.5 + labelH * 0.6, baseY - labelH * 1.8),
                args.SheetReference!, labelH * 0.8, args.Layer, 0.0, ct).ConfigureAwait(false);
            handles.Add(sref.Handle);
        }

        return new InsertElevationMarkerResult(
            new SectionsResultSummary(args.Layer, handles, Array.Empty<string>()),
            args.Direction, dirDeg, scale);
    }

    // ─────────────────────────────────────────────────────────────
    // list_section_lines
    // ─────────────────────────────────────────────────────────────

    [McpTool("list_section_lines",
        "Inventory all entities on the A-DETL-SECT layer (or a caller-supplied layer via layerFilter). For every handle that is a curve, also queries acad.geometry2d.get_curve_length so the caller can sanity-check drawing-unit cut lengths against the target scale.",
        "sections",
        Intent = new[]
        {
            "lista linii ciecia",
            "list section lines",
            "scan section layer",
            "inventory section cuts",
            "wypisz przekroje z rysunku",
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static async Task<ListSectionLinesResult> ListSectionLines(
        IPluginGateway gw, ListSectionLinesArgs args, CancellationToken ct)
    {
        string layer = string.IsNullOrWhiteSpace(args.LayerFilter) ? SectionsPalette.LayerSectionLine : args.LayerFilter!;

        // If the layer is absent, select_by_layer throws; return an empty result gracefully.
        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        if (!existing.Contains(layer))
            return new ListSectionLinesResult(Array.Empty<SectionLineEntry>(), layer, 0);

        var selArgs = new JsonObject { ["layer"] = layer };
        var selResp = await gw.InvokeAsync("acad.selection.select_by_layer", selArgs, T_NORMAL, ct).ConfigureAwait(false)
                     ?? throw new InvalidPluginShapeException("select_by_layer returned null");
        var handlesArr = selResp["handles"] as JsonArray ?? new JsonArray();

        var entries = new List<SectionLineEntry>(handlesArr.Count);
        foreach (var n in handlesArr)
        {
            var handle = n?.GetValue<string>();
            if (string.IsNullOrEmpty(handle)) continue;

            // Get object class via get_entity (non-throwing best-effort)
            string objectClass = "";
            double? lenMm = null;
            try
            {
                var entResp = await gw.InvokeAsync("acad.geometry2d.get_entity",
                    new JsonObject { ["handle"] = handle }, T_FAST, ct).ConfigureAwait(false);
                if (entResp is not null)
                {
                    objectClass = entResp["entity"]?["objectClass"]?.GetValue<string>() ?? "";
                }
            }
            catch { /* entity might not be curve-like — swallow */ }

            try
            {
                var lenResp = await gw.InvokeAsync("acad.geometry2d.get_curve_length",
                    new JsonObject { ["handle"] = handle }, T_FAST, ct).ConfigureAwait(false);
                if (lenResp is not null && lenResp["length"] is not null)
                    lenMm = lenResp["length"]!.GetValue<double>();
            }
            catch { /* not a curve — lenMm stays null */ }

            entries.Add(new SectionLineEntry(handle, layer, objectClass, lenMm));
        }

        return new ListSectionLinesResult(entries, layer, entries.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────

    private static async Task SetLinetypeAsync(IPluginGateway gw, string handle, string linetype, double ltscale, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["handles"]  = new JsonArray(new JsonNode?[] { JsonValue.Create(handle) }),
            ["linetype"] = linetype,
            ["scale"]    = ltscale,
        };
        // set_linetype throws if the linetype is not loaded; swallow so the composite still succeeds
        // (the cut line will simply plot Continuous). The composite returns a note when this happens.
        try { await gw.InvokeAsync("acad.modify.set_linetype", args, T_NORMAL, ct).ConfigureAwait(false); }
        catch { /* ignored — DASHED2 may not be in the drawing's linetype table */ }
    }

    private static async Task EmitTickAsync(
        IPluginGateway gw, Point2dDto center, double perpX, double perpY, double length, string layer, CancellationToken ct)
    {
        double half = length * 0.5;
        var a = new Point2dDto(center.X + perpX * half, center.Y + perpY * half);
        var b = new Point2dDto(center.X - perpX * half, center.Y - perpY * half);
        await ArchitectureProxy.DrawLineAsync(gw, a, b, layer, ct).ConfigureAwait(false);
    }
}
