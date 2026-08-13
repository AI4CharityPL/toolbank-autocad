// acad-callouts category. 5 composite tools that compose plan-symbol
// geometry out of existing primitives (rule 35 §2):
//   * insert_north_arrow     — ISO circle + arrow head + "N"
//   * insert_scale_bar       — chequered 5-segment scale bar + numbers + label
//   * insert_section_callout — cut-line + end markers + view-direction arrows
//   * insert_detail_callout  — detail circle + leader + callout bubble
//   * insert_title_block     — sheet border + 12-row project title block
//
// All drawing calls go through the primitive gateways exposed by
// ArchitectureProxy (draw_line / draw_polyline / draw_circle / draw_arc /
// add_dbtext / ensure_layer). Layer names follow rule 68 (A-ANNO-NORT /
// A-ANNO-SBAR / A-ANNO-SYMB / A-ANNO-TTLB / A-ANNO-BORD).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Callouts;

public static class CalloutsTools
{
    // ─────────────────────────────────────────────────────────────
    // insert_north_arrow
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_north_arrow",
        "Insert a north arrow symbol (ISO 5455) at the given model-space position. The symbol is a circle plus an inscribed north-pointing arrow plus a \"N\" label above. Plotted diameter is PlotNorthDiameterMm (30 mm) scaled by the requested drawing scale (1:100 → 3000 mm drawing-unit diameter). Layer A-ANNO-NORT, colour inherited from the layer.",
        "callouts",
        Intent = new[]
        {
            "wstaw strzalke polnocy",
            "insert north arrow",
            "rozka kompasu",
            "north indicator",
            "symbol N na planie",
        },
        RequiresPlugin = true)]
    public static async Task<InsertNorthArrowResult> InsertNorthArrow(
        IPluginGateway gw, InsertNorthArrowArgs args, CancellationToken ct)
    {
        if (args is null) throw new ArgumentException("args required.");
        if (args.Position is null) throw new ArgumentException("position required (expected { x, y }).");
        if (string.IsNullOrWhiteSpace(args.Scale)) throw new ArgumentException("scale required (e.g. \"1:100\").");
        if (string.IsNullOrWhiteSpace(args.Layer)) throw new ArgumentException("layer required (default suggestion: A-ANNO-NORT).");
        if (string.IsNullOrWhiteSpace(args.Label)) throw new ArgumentException("label required (default suggestion: \"N\").");
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        double diameter = CalloutsPalette.PlotNorthDiameterMm * scale;
        double radius = diameter * 0.5;
        double textHeight = args.TextHeightPlotMm * scale;

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();
        var notes = new List<string>();

        // 1. outer circle
        var circle = await ArchitectureProxy.DrawCircleAsync(gw, args.Position, radius, args.Layer, ct).ConfigureAwait(false);
        handles.Add(circle.Handle);

        // 2. arrow: diamond with narrow E/W tips, pointing north.
        //    We rotate the raw-north vertices by rotationDeg around the centre so callers can align
        //    the arrow to the sheet's north even if model space uses a different orientation.
        double arrowHalfWidth = radius * 0.22;  // narrow diamond ~22% radius
        double arrowTipOut    = radius * 0.85;
        double arrowTailIn    = radius * 0.55;

        var rawVertices = new[]
        {
            new Point2dDto(0,                 arrowTipOut),   // north tip
            new Point2dDto(arrowHalfWidth,    0),             // east waist
            new Point2dDto(0,                -arrowTailIn),   // south tail
            new Point2dDto(-arrowHalfWidth,   0),             // west waist
        };
        var rotated = rawVertices
            .Select(v => RotateTranslate(v, args.Position, args.RotationDeg))
            .ToArray();
        var arrow = await ArchitectureProxy.DrawPolylineAsync(gw, rotated, closed: true, args.Layer, ct).ConfigureAwait(false);
        handles.Add(arrow.Handle);

        // 3. diagonal fill: draw a short line from centre → north tip so the direction reads even on
        //    B&W plots where the closed polyline does not get a SOLID hatch.
        var centerLine = await ArchitectureProxy.DrawLineAsync(gw,
            args.Position,
            RotateTranslate(new Point2dDto(0, arrowTipOut), args.Position, args.RotationDeg),
            args.Layer, ct).ConfigureAwait(false);
        handles.Add(centerLine.Handle);

        // 4. "N" letter above the circle
        var labelOffset = RotateTranslate(new Point2dDto(0, radius + textHeight * 0.9), args.Position, args.RotationDeg);
        var label = await ArchitectureProxy.AddDBTextAsync(gw,
            labelOffset, args.Label, textHeight, args.Layer, args.RotationDeg, ct).ConfigureAwait(false);
        handles.Add(label.Handle);

        if (args.FilledArrow)
            notes.Add("SOLID hatch on the arrow head is not emitted by this composite; rely on the closed polyline plus centre-line for contrast. For a filled arrow, follow up with acad.geometry2d.draw_hatch using pattern=SOLID on handle " + arrow.Handle + ".");

        return new InsertNorthArrowResult(
            new CalloutResultSummary(args.Layer, handles, notes),
            diameter, scale);
    }

    // ─────────────────────────────────────────────────────────────
    // insert_scale_bar
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_scale_bar",
        "Insert a horizontal graphic scale bar at the given position. The bar is a chequered 5-segment rectangle (alternating black/white segments 1-by-4 mm plotted high) with numeric labels under each segment and a \"1:100\" scale caption centred above. Segment meters auto-scale to the plan scale (1:50 → 1 m segments, 1:200 → 2 m segments).",
        "callouts",
        Intent = new[]
        {
            "wstaw podzialke",
            "insert scale bar",
            "skala graficzna",
            "graphic scale",
            "podzialka liniowa",
        },
        RequiresPlugin = true)]
    public static async Task<InsertScaleBarResult> InsertScaleBar(
        IPluginGateway gw, InsertScaleBarArgs args, CancellationToken ct)
    {
        if (args is null) throw new ArgumentException("args required.");
        if (args.Position is null) throw new ArgumentException("position required (expected { x, y }).");
        if (string.IsNullOrWhiteSpace(args.Scale)) throw new ArgumentException("scale required (e.g. \"1:100\").");
        if (string.IsNullOrWhiteSpace(args.Layer)) throw new ArgumentException("layer required (default suggestion: A-ANNO-SBAR).");
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        var preset = CalloutsPalette.ResolveScaleBarPreset(scale);
        double segMeters = args.SegmentMeters ?? preset.SegmentM;
        int segCount = args.SegmentCount ?? preset.SegmentCount;

        // segment length in drawing mm = metres × 1000
        double segLen = segMeters * 1000.0;
        double totalLen = segLen * segCount;
        double barHeight = 2.0 * scale;          // 2 mm plotted (top stripe)
        double numberHeight = args.TextHeightPlotMm * scale;
        double labelHeight  = args.LabelHeightPlotMm * scale;

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();
        double x0 = args.Position.X;
        double y0 = args.Position.Y;

        // outer rectangle (two stripes high)
        var rect = await ArchitectureProxy.DrawPolylineAsync(gw, new[]
        {
            new Point2dDto(x0,                 y0),
            new Point2dDto(x0 + totalLen,       y0),
            new Point2dDto(x0 + totalLen,       y0 + 2 * barHeight),
            new Point2dDto(x0,                 y0 + 2 * barHeight),
        }, closed: true, args.Layer, ct).ConfigureAwait(false);
        handles.Add(rect.Handle);

        // mid-horizontal split line
        var mid = await ArchitectureProxy.DrawLineAsync(gw,
            new Point2dDto(x0,            y0 + barHeight),
            new Point2dDto(x0 + totalLen,  y0 + barHeight),
            args.Layer, ct).ConfigureAwait(false);
        handles.Add(mid.Handle);

        // vertical segment dividers + alternating chequer fills (drawn as short diagonal lines
        // inside every second cell so the bar reads as chequered even without SOLID hatch)
        for (int i = 0; i <= segCount; i++)
        {
            double x = x0 + i * segLen;
            var v = await ArchitectureProxy.DrawLineAsync(gw,
                new Point2dDto(x, y0),
                new Point2dDto(x, y0 + 2 * barHeight),
                args.Layer, ct).ConfigureAwait(false);
            handles.Add(v.Handle);

            if (i < segCount)
            {
                // chequer: fill bottom stripe on even segments, top stripe on odd segments using
                // a closed polyline — callers can hatch these later if desired
                bool bottomFill = (i % 2 == 0);
                double yLo = bottomFill ? y0 : y0 + barHeight;
                double yHi = bottomFill ? y0 + barHeight : y0 + 2 * barHeight;
                var cell = await ArchitectureProxy.DrawPolylineAsync(gw, new[]
                {
                    new Point2dDto(x,            yLo),
                    new Point2dDto(x + segLen,    yLo),
                    new Point2dDto(x + segLen,    yHi),
                    new Point2dDto(x,            yHi),
                }, closed: true, args.Layer, ct).ConfigureAwait(false);
                handles.Add(cell.Handle);
            }
        }

        // numbers under every divider
        double numY = y0 - numberHeight * 1.8;
        for (int i = 0; i <= segCount; i++)
        {
            double mValue = i * segMeters;
            string numText = i == segCount
                ? $"{mValue:0.##} {preset.Unit}"
                : mValue.ToString("0.##", CultureInfo.InvariantCulture);
            double x = x0 + i * segLen;
            var lbl = await ArchitectureProxy.AddDBTextAsync(gw,
                new Point2dDto(x - numberHeight * 0.3, numY),
                numText, numberHeight, args.Layer, 0.0, ct).ConfigureAwait(false);
            handles.Add(lbl.Handle);
        }

        // "1:<n>" caption above bar
        if (args.ShowScaleText)
        {
            double captionY = y0 + 2 * barHeight + labelHeight * 0.6;
            string captionText = $"SKALA {args.Scale}";
            var cap = await ArchitectureProxy.AddDBTextAsync(gw,
                new Point2dDto(x0, captionY),
                captionText, labelHeight, args.Layer, 0.0, ct).ConfigureAwait(false);
            handles.Add(cap.Handle);
        }

        return new InsertScaleBarResult(
            new CalloutResultSummary(args.Layer, handles, Array.Empty<string>()),
            totalLen, segMeters, segCount, scale);
    }

    // ─────────────────────────────────────────────────────────────
    // insert_section_callout
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_section_callout",
        "Insert a section cut-line plus two end markers (circle + label letter) plus two view-direction arrows. Optional drawCutLine=true draws the dashed cut polyline between startPoint and endPoint; set it to false if the plan already has a cut line. label defaults to \"A\" and creates markers reading \"A\" on both ends (A-A section). viewDirection controls which side the triangle arrows point (right|left relative to the start→end vector).",
        "callouts",
        Intent = new[]
        {
            "wstaw przekroj",
            "insert section callout",
            "znacznik przekroju A-A",
            "section marker",
            "linia ciecia z kolkiem",
        },
        RequiresPlugin = true)]
    public static async Task<InsertSectionCalloutResult> InsertSectionCallout(
        IPluginGateway gw, InsertSectionCalloutArgs args, CancellationToken ct)
    {
        if (args is null) throw new ArgumentException("args required.");
        if (args.StartPoint is null) throw new ArgumentException("startPoint required (expected { x, y }).");
        if (args.EndPoint is null) throw new ArgumentException("endPoint required (expected { x, y }).");
        if (string.IsNullOrWhiteSpace(args.Scale)) throw new ArgumentException("scale required (e.g. \"1:100\").");
        if (string.IsNullOrWhiteSpace(args.Label)) throw new ArgumentException("label required (e.g. \"A\").");
        if (string.IsNullOrWhiteSpace(args.ViewDirection)) throw new ArgumentException("viewDirection required (\"left\" or \"right\").");
        if (string.IsNullOrWhiteSpace(args.Layer)) throw new ArgumentException("layer required (default suggestion: A-ANNO-SYMB).");
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        double markerD = args.MarkerDiameterPlotMm * scale;
        double markerR = markerD * 0.5;
        double textH = args.TextHeightPlotMm * scale;
        double dx = args.EndPoint.X - args.StartPoint.X;
        double dy = args.EndPoint.Y - args.StartPoint.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) throw new ArgumentException("startPoint and endPoint are coincident");
        double ux = dx / len, uy = dy / len;
        // perpendicular unit (right-hand side when looking start→end)
        double px = -uy, py = ux;
        if (!args.ViewDirection.Equals("right", StringComparison.OrdinalIgnoreCase)) { px = -px; py = -py; }

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();

        if (args.DrawCutLine)
        {
            // Full cut line — even though the API does not parameterise linetype per-entity here,
            // the drawCutLine=false path lets the caller supply a pre-drawn dashed line.
            var cut = await ArchitectureProxy.DrawLineAsync(gw, args.StartPoint, args.EndPoint, args.Layer, ct).ConfigureAwait(false);
            handles.Add(cut.Handle);
        }

        // Helper: markers offset outward from each endpoint so the circle does not overlap the cut line.
        await EmitSectionMarkerAsync(gw, args.StartPoint, -ux, -uy, px, py, markerR, textH, args.Label, args.SheetReference, args.Layer, handles, ct).ConfigureAwait(false);
        await EmitSectionMarkerAsync(gw, args.EndPoint,    ux,  uy, px, py, markerR, textH, args.Label, args.SheetReference, args.Layer, handles, ct).ConfigureAwait(false);

        return new InsertSectionCalloutResult(
            new CalloutResultSummary(args.Layer, handles, Array.Empty<string>()),
            args.Label, len, scale);
    }

    private static async Task EmitSectionMarkerAsync(
        IPluginGateway gw,
        Point2dDto endpoint, double outUx, double outUy, double perpX, double perpY,
        double markerR, double textH, string label, string? sheetRef, string layer,
        List<string> handles, CancellationToken ct)
    {
        // Centre the marker just past the endpoint so the cut line tangentially meets the circle.
        double cx = endpoint.X + outUx * markerR * 1.4;
        double cy = endpoint.Y + outUy * markerR * 1.4;
        var center = new Point2dDto(cx, cy);

        var circle = await ArchitectureProxy.DrawCircleAsync(gw, center, markerR, layer, ct).ConfigureAwait(false);
        handles.Add(circle.Handle);

        // View-direction arrow — short triangle tangent to the marker, on the perp side
        double tipX = cx + perpX * markerR * 1.8;
        double tipY = cy + perpY * markerR * 1.8;
        double baseAX = cx + outUx * markerR * 0.5 + perpX * markerR * 0.4;
        double baseAY = cy + outUy * markerR * 0.5 + perpY * markerR * 0.4;
        double baseBX = cx - outUx * markerR * 0.5 + perpX * markerR * 0.4;
        double baseBY = cy - outUy * markerR * 0.5 + perpY * markerR * 0.4;
        var tri = await ArchitectureProxy.DrawPolylineAsync(gw, new[]
        {
            new Point2dDto(tipX,   tipY),
            new Point2dDto(baseAX, baseAY),
            new Point2dDto(baseBX, baseBY),
        }, closed: true, layer, ct).ConfigureAwait(false);
        handles.Add(tri.Handle);

        // Label letter inside the circle (top half if sheetRef present, centred otherwise)
        double labelY = sheetRef is null ? cy - textH * 0.5 : cy + textH * 0.1;
        var lbl = await ArchitectureProxy.AddDBTextAsync(gw,
            new Point2dDto(cx - textH * 0.35, labelY), label, textH, layer, 0.0, ct).ConfigureAwait(false);
        handles.Add(lbl.Handle);

        if (!string.IsNullOrWhiteSpace(sheetRef))
        {
            // horizontal divider + sheet number in the bottom half
            var div = await ArchitectureProxy.DrawLineAsync(gw,
                new Point2dDto(cx - markerR * 0.9, cy),
                new Point2dDto(cx + markerR * 0.9, cy),
                layer, ct).ConfigureAwait(false);
            handles.Add(div.Handle);

            var sref = await ArchitectureProxy.AddDBTextAsync(gw,
                new Point2dDto(cx - textH * 0.7, cy - textH * 1.2), sheetRef, textH * 0.7, layer, 0.0, ct).ConfigureAwait(false);
            handles.Add(sref.Handle);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // insert_detail_callout
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_detail_callout",
        "Mark a rectangular/circular area for a detail on a separate sheet. Draws a detail circle of radius radiusMm around center, a dashed-style leader line to leaderEndPoint, and a callout bubble containing the label and target scale. If leaderEndPoint is null, the bubble is positioned at radiusMm*2 to the upper-right of the centre.",
        "callouts",
        Intent = new[]
        {
            "wstaw odnosnik detalu",
            "insert detail callout",
            "okno detalu 1:20",
            "detail reference circle",
            "zaznaczenie detalu na planie",
        },
        RequiresPlugin = true)]
    public static async Task<InsertDetailCalloutResult> InsertDetailCallout(
        IPluginGateway gw, InsertDetailCalloutArgs args, CancellationToken ct)
    {
        if (args is null) throw new ArgumentException("args required.");
        if (args.Center is null) throw new ArgumentException("center required (expected { x, y }).");
        if (args.RadiusMm <= 0.0) throw new ArgumentException("radiusMm must be > 0.");
        if (string.IsNullOrWhiteSpace(args.Scale)) throw new ArgumentException("scale required (e.g. \"1:100\").");
        if (string.IsNullOrWhiteSpace(args.Label)) throw new ArgumentException("label required (e.g. \"1\" or \"D-01\").");
        if (string.IsNullOrWhiteSpace(args.Layer)) throw new ArgumentException("layer required (default suggestion: A-ANNO-SYMB).");
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        double bubbleD = args.MarkerDiameterPlotMm * scale;
        double bubbleR = bubbleD * 0.5;
        double textH = args.TextHeightPlotMm * scale;
        string targetScale = string.IsNullOrWhiteSpace(args.TargetScale) ? args.Scale : args.TargetScale!;

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();

        // 1. area circle at center
        var areaCircle = await ArchitectureProxy.DrawCircleAsync(gw, args.Center, args.RadiusMm, args.Layer, ct).ConfigureAwait(false);
        handles.Add(areaCircle.Handle);

        // 2. bubble position
        Point2dDto bubbleCenter;
        if (args.LeaderEndPoint is not null)
        {
            bubbleCenter = args.LeaderEndPoint;
        }
        else
        {
            double offX = args.Center.X + args.RadiusMm * 2.0;
            double offY = args.Center.Y + args.RadiusMm * 2.0;
            bubbleCenter = new Point2dDto(offX, offY);
        }

        // 3. leader line from the area-circle edge to the bubble edge
        double ldx = bubbleCenter.X - args.Center.X;
        double ldy = bubbleCenter.Y - args.Center.Y;
        double lLen = Math.Sqrt(ldx * ldx + ldy * ldy);
        if (lLen > 1e-6)
        {
            double lux = ldx / lLen, luy = ldy / lLen;
            var from = new Point2dDto(args.Center.X + lux * args.RadiusMm, args.Center.Y + luy * args.RadiusMm);
            var to   = new Point2dDto(bubbleCenter.X - lux * bubbleR,      bubbleCenter.Y - luy * bubbleR);
            var leader = await ArchitectureProxy.DrawLineAsync(gw, from, to, args.Layer, ct).ConfigureAwait(false);
            handles.Add(leader.Handle);
        }

        // 4. bubble circle
        var bubble = await ArchitectureProxy.DrawCircleAsync(gw, bubbleCenter, bubbleR, args.Layer, ct).ConfigureAwait(false);
        handles.Add(bubble.Handle);

        // 5. divider + label (top) + target scale (bottom) + optional sheet reference on the right
        var divider = await ArchitectureProxy.DrawLineAsync(gw,
            new Point2dDto(bubbleCenter.X - bubbleR * 0.9, bubbleCenter.Y),
            new Point2dDto(bubbleCenter.X + bubbleR * 0.9, bubbleCenter.Y),
            args.Layer, ct).ConfigureAwait(false);
        handles.Add(divider.Handle);

        var labelText = await ArchitectureProxy.AddDBTextAsync(gw,
            new Point2dDto(bubbleCenter.X - textH * 0.35, bubbleCenter.Y + textH * 0.15),
            args.Label, textH, args.Layer, 0.0, ct).ConfigureAwait(false);
        handles.Add(labelText.Handle);

        var scaleText = await ArchitectureProxy.AddDBTextAsync(gw,
            new Point2dDto(bubbleCenter.X - bubbleR * 0.8, bubbleCenter.Y - textH * 1.1),
            targetScale, textH * 0.7, args.Layer, 0.0, ct).ConfigureAwait(false);
        handles.Add(scaleText.Handle);

        if (!string.IsNullOrWhiteSpace(args.SheetReference))
        {
            var srefText = await ArchitectureProxy.AddDBTextAsync(gw,
                new Point2dDto(bubbleCenter.X + bubbleR * 1.2, bubbleCenter.Y - textH * 0.5),
                args.SheetReference!, textH * 0.8, args.Layer, 0.0, ct).ConfigureAwait(false);
            handles.Add(srefText.Handle);
        }

        return new InsertDetailCalloutResult(
            new CalloutResultSummary(args.Layer, handles, Array.Empty<string>()),
            args.Label, args.RadiusMm, scale);
    }

    // ─────────────────────────────────────────────────────────────
    // insert_title_block
    // ─────────────────────────────────────────────────────────────

    [McpTool("insert_title_block",
        "Draw an ISO 7200 sheet border plus a 12-row project title block in the lower-right corner. sheetSize accepts A0/A1/A2/A3/A4; the block is scaled so that the plotted paper size matches. Pass fields=[{key, value}, ...] to fill the standard rows (PROJEKT, INWESTOR, ADRES, BRANŻA, FAZA, STADIUM, RYSUNEK, SKALA, NR RYS., DATA, PROJEKTANT, SPRAWDZAJĄCY); missing rows are left empty. Shorthand projectName/sheetNumber/author/date/titleText populate the most common rows if fields is not supplied.",
        "callouts",
        Intent = new[]
        {
            "wstaw tabelke rysunkowa",
            "insert title block",
            "ramka arkusza A1",
            "sheet frame and stamp",
            "metryczka rysunku",
        },
        RequiresPlugin = true)]
    public static async Task<InsertTitleBlockResult> InsertTitleBlock(
        IPluginGateway gw, InsertTitleBlockArgs args, CancellationToken ct)
    {
        if (args is null) throw new ArgumentException("args required.");
        if (args.BottomLeft is null) throw new ArgumentException("bottomLeft required (expected { x, y }).");
        if (string.IsNullOrWhiteSpace(args.SheetSize)) throw new ArgumentException("sheetSize required (A0/A1/A2/A3/A4).");
        if (string.IsNullOrWhiteSpace(args.Scale)) throw new ArgumentException("scale required (e.g. \"1:100\").");
        if (string.IsNullOrWhiteSpace(args.Layer)) throw new ArgumentException("layer required (default suggestion: A-ANNO-TTLB).");
        if (string.IsNullOrWhiteSpace(args.BorderLayer)) throw new ArgumentException("borderLayer required (default suggestion: A-ANNO-BORD).");
        int scale = CalloutsPalette.ResolveScaleFactor(args.Scale);
        var sheet = CalloutsPalette.ResolveSheet(args.SheetSize);

        // Drawing-unit sheet size = paper size × scale factor.
        double w = sheet.WidthMm * scale;
        double h = sheet.HeightMm * scale;
        double marginL = sheet.MarginLeftMm * scale;
        double marginE = sheet.MarginEdgeMm * scale;
        double titleH = args.TitleHeightPlotMm * scale;
        double fieldH = args.FieldHeightPlotMm * scale;

        double x0 = args.BottomLeft.X;
        double y0 = args.BottomLeft.Y;

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false);
        if (args.DrawBorder)
            await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.BorderLayer, 7, "Continuous", ct).ConfigureAwait(false);

        var handles = new List<string>();

        // 1. outer paper boundary (for reference)
        if (args.DrawBorder)
        {
            var outer = await ArchitectureProxy.DrawPolylineAsync(gw, new[]
            {
                new Point2dDto(x0,         y0),
                new Point2dDto(x0 + w,     y0),
                new Point2dDto(x0 + w,     y0 + h),
                new Point2dDto(x0,         y0 + h),
            }, closed: true, args.BorderLayer, ct, args.LayoutName).ConfigureAwait(false);
            handles.Add(outer.Handle);

            // 2. drawing area border (ISO 7200: left margin 25, edges 5-10)
            var inner = await ArchitectureProxy.DrawPolylineAsync(gw, new[]
            {
                new Point2dDto(x0 + marginL,    y0 + marginE),
                new Point2dDto(x0 + w - marginE, y0 + marginE),
                new Point2dDto(x0 + w - marginE, y0 + h - marginE),
                new Point2dDto(x0 + marginL,    y0 + h - marginE),
            }, closed: true, args.BorderLayer, ct, args.LayoutName).ConfigureAwait(false);
            handles.Add(inner.Handle);
        }

        // 3. title block — stack of 12 rows in the bottom-right corner, 180 mm wide (plotted)
        double tbWidth  = 180.0 * scale;
        double tbRowH   = fieldH * 2.4;
        double tbHeight = tbRowH * CalloutsPalette.DefaultTitleBlockRows.Count;
        double tbX0 = x0 + w - marginE - tbWidth;
        double tbY0 = y0 + marginE;
        double tbX1 = tbX0 + tbWidth;
        double tbY1 = tbY0 + tbHeight;

        // title-block outer frame
        var tbOuter = await ArchitectureProxy.DrawPolylineAsync(gw, new[]
        {
            new Point2dDto(tbX0, tbY0),
            new Point2dDto(tbX1, tbY0),
            new Point2dDto(tbX1, tbY1),
            new Point2dDto(tbX0, tbY1),
        }, closed: true, args.Layer, ct, args.LayoutName).ConfigureAwait(false);
        handles.Add(tbOuter.Handle);

        // row separators + key / value columns
        double keyColFrac = 0.35;  // left 35% = row key, right 65% = row value
        double keyX = tbX0 + tbWidth * keyColFrac;

        // vertical separator between key + value columns
        var sep = await ArchitectureProxy.DrawLineAsync(gw,
            new Point2dDto(keyX, tbY0),
            new Point2dDto(keyX, tbY1),
            args.Layer, ct, args.LayoutName).ConfigureAwait(false);
        handles.Add(sep.Handle);

        // Build field values map. Supplied args.Fields wins; fall back on shorthand fields.
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (args.Fields is { Count: > 0 })
            foreach (var f in args.Fields) values[f.Key] = f.Value;
        if (!string.IsNullOrWhiteSpace(args.ProjectName)) values.TryAdd("PROJEKT", args.ProjectName!);
        if (!string.IsNullOrWhiteSpace(args.SheetNumber)) values.TryAdd("NR RYS.", args.SheetNumber!);
        if (!string.IsNullOrWhiteSpace(args.Author))      values.TryAdd("PROJEKTANT", args.Author!);
        if (!string.IsNullOrWhiteSpace(args.Date))        values.TryAdd("DATA", args.Date!);
        if (!string.IsNullOrWhiteSpace(args.TitleText))   values.TryAdd("RYSUNEK", args.TitleText!);
        values.TryAdd("SKALA", args.Scale);

        // Iterate rows top→bottom (reverse list because tbY0 is at bottom)
        var rows = CalloutsPalette.DefaultTitleBlockRows;
        for (int i = 0; i < rows.Count; i++)
        {
            double rowY = tbY1 - (i + 1) * tbRowH;  // bottom edge of row i (0 = top row)
            // separator below this row (except bottom)
            if (i < rows.Count - 1)
            {
                var hor = await ArchitectureProxy.DrawLineAsync(gw,
                    new Point2dDto(tbX0, rowY),
                    new Point2dDto(tbX1, rowY),
                    args.Layer, ct, args.LayoutName).ConfigureAwait(false);
                handles.Add(hor.Handle);
            }

            // key text
            double textY = rowY + tbRowH * 0.25;
            var keyText = await ArchitectureProxy.AddDBTextAsync(gw,
                new Point2dDto(tbX0 + fieldH * 0.4, textY),
                rows[i], fieldH, args.Layer, 0.0, ct, layoutName: args.LayoutName).ConfigureAwait(false);
            handles.Add(keyText.Handle);

            // value text
            values.TryGetValue(rows[i], out var val);
            if (!string.IsNullOrWhiteSpace(val))
            {
                double valueTextH = rows[i].Equals("RYSUNEK", StringComparison.OrdinalIgnoreCase) ? titleH : fieldH;
                var valText = await ArchitectureProxy.AddDBTextAsync(gw,
                    new Point2dDto(keyX + fieldH * 0.4, textY),
                    val!, valueTextH, args.Layer, 0.0, ct, layoutName: args.LayoutName).ConfigureAwait(false);
                handles.Add(valText.Handle);
            }
        }

        return new InsertTitleBlockResult(
            new CalloutResultSummary(args.Layer, handles, Array.Empty<string>()),
            sheet.Name, w, h, scale);
    }

    // ─────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────

    private static Point2dDto RotateTranslate(Point2dDto p, Point2dDto origin, double rotationDeg)
    {
        double rad = rotationDeg * Math.PI / 180.0;
        double c = Math.Cos(rad), s = Math.Sin(rad);
        double rx = p.X * c - p.Y * s;
        double ry = p.X * s + p.Y * c;
        return new Point2dDto(origin.X + rx, origin.Y + ry);
    }
}
