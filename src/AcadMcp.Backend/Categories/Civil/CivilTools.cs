// AutoCAD acad-civil domain category. 10 high-level civil-engineering /
// surveying tools that compose primitives from acad-geometry-2d, acad-layers,
// and acad-annotations per rule 35 §2. Implements rule 38 (civil traps):
// stationing notation (metric / US), surveyor bearings + parcel closure,
// road centreline vs edge linetype split, major / minor topographic
// contours, signed spot elevations, and the true-north arrow.
//
// v1 limitations (called out in tool descriptions):
//   * Vertical alignments / profile views ship in Phase 7.
//   * Spirals (clothoid transitions) are NOT in v1 — only tangents and
//     circular curves.
//   * draw_north_arrow synthesises a simple triangle inline; the COMPASS
//     variant ships with the Phase-7 block library.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Civil;

public static class CivilTools
{
    // ─────────── infrastructure ───────────

    [McpTool("ensure_civil_layers",
        "Idempotently create the 12-layer civil-engineering key (C-ROAD-CNTR, C-ROAD-EDGE, C-ROAD-LANE, C-PROP, C-ESMT, C-ROW, C-TOPO-MAJR, C-TOPO-MINR, C-TOPO-SPOT, C-STAT, C-ANNO, C-NORTH) per rule 38 §9, with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. C-ROAD-CNTR = 0.30 mm CENTER, C-ROAD-EDGE = 0.50 mm Continuous, C-PROP = 0.50 mm PHANTOM2, C-TOPO-MAJR = 0.35 mm, C-TOPO-MINR = 0.13 mm). Existing layers are left alone, never overwritten. includeRoad / includeProperty / includeTopo flags skip the corresponding sub-set so a survey-only drawing does not get road layers it never uses.",
        "civil",
        Intent = new[]
        {
            "stworz wszystkie warstwy civil",
            "ensure civil engineering layers",
            "setup C-* layer key",
            "wlacz standardowe warstwy C-* w projekcie",
            "create civil layer standard"
        },
        RequiresPlugin = true)]
    public static async Task<EnsureCivilLayersResult> EnsureCivilLayers(
        IPluginGateway gw, EnsureCivilLayersArgs args, CancellationToken ct)
    {
        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var outcomes = new List<LayerEnsureOutcome>();
        int created = 0, already = 0;

        foreach (var spec in CivilPalette.All)
        {
            if (!args.IncludeRoad     && IsRoadLayer(spec.Name))     continue;
            if (!args.IncludeProperty && IsPropertyLayer(spec.Name)) continue;
            if (!args.IncludeTopo     && IsTopoLayer(spec.Name))     continue;
            try
            {
                var didCreate = await CivilProxy.EnsureLayerAsync(
                    gw, existing, spec.Name, spec.AciColor, spec.Linetype,
                    spec.LineweightMm, spec.Plottable, ct).ConfigureAwait(false);
                if (didCreate) created++; else already++;
                outcomes.Add(new LayerEnsureOutcome(
                    spec.Name, didCreate ? "created" : "already_exists", spec.AciColor, spec.Linetype, spec.LineweightMm));
            }
            catch (Exception ex)
            {
                outcomes.Add(new LayerEnsureOutcome(
                    spec.Name, "failed", spec.AciColor, spec.Linetype, spec.LineweightMm, ex.Message));
            }
        }
        return new EnsureCivilLayersResult(outcomes, created, already);
    }

    // ─────────── road alignment ───────────

    [McpTool("draw_alignment_tangent",
        "Draw a single straight (tangent) segment of a road horizontal alignment as a line on layer C-ROAD-CNTR (default) — picks up CENTER linetype because the layer carries it. Per rule 38 §6 the road centreline MUST be a CENTER linetype on C-ROAD-CNTR; agents who reach for acad-geometry2d.draw_line directly bypass the linetype assignment.",
        "civil",
        Intent = new[]
        {
            "narysuj odcinek prostej drogi",
            "draw alignment tangent segment",
            "tangenta osi drogi",
            "road centerline tangent",
            "alignment straight segment"
        },
        RequiresPlugin = true)]
    public static async Task<AlignmentSegmentResult> DrawAlignmentTangent(
        IPluginGateway gw, DrawAlignmentTangentArgs args, CancellationToken ct)
    {
        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, CivilPalette.LayerRoadCntr, created, ct).ConfigureAwait(false);
        var entity = await CivilProxy.DrawLineAsync(gw, args.Start, args.End, args.Layer, ct).ConfigureAwait(false);
        return new AlignmentSegmentResult(entity, created);
    }

    [McpTool("draw_alignment_curve",
        "Draw a single circular curve segment of a road horizontal alignment as an Arc on layer C-ROAD-CNTR (default). Spirals / clothoid transitions are NOT in v1 — only tangents and circular curves. The arc spans from startAngleDeg to endAngleDeg around the centre with the given radius (in metres, in the drawing's current units).",
        "civil",
        Intent = new[]
        {
            "narysuj luk osi drogi",
            "draw alignment circular curve",
            "luk poziomy drogi",
            "road centerline curve",
            "alignment arc segment"
        },
        RequiresPlugin = true)]
    public static async Task<AlignmentSegmentResult> DrawAlignmentCurve(
        IPluginGateway gw, DrawAlignmentCurveArgs args, CancellationToken ct)
    {
        if (args.RadiusM <= 0) throw new ArgumentException("radiusM must be > 0");
        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, CivilPalette.LayerRoadCntr, created, ct).ConfigureAwait(false);
        var entity = await CivilProxy.DrawArcAsync(
            gw, args.Center, args.RadiusM, args.StartAngleDeg, args.EndAngleDeg, args.Layer, ct).ConfigureAwait(false);
        return new AlignmentSegmentResult(entity, created);
    }

    [McpTool("draw_alignment_spiral",
        "Draw a clothoid (Euler spiral) transition segment of a road horizontal alignment on layer C-ROAD-CNTR (default) -- the piece the v1 alignment tools were missing between a tangent and a circular curve. Approximated with the standard 2-term power-series clothoid expansion (drafting-grade accuracy, not survey-grade) sampled into `segments` points and drawn as a polyline. startBearingDeg is the tangent direction at Start (0 = +X, counter-clockwise); turnDirection picks which way it curves; endRadiusM is the circular-curve radius the spiral transitions INTO at its far end (the clothoid parameter A is derived as sqrt(endRadiusM * lengthM)). Returns the end point and end bearing so the next draw_alignment_curve call can continue tangent-to-curve without the agent doing the clothoid math itself.",
        "civil",
        Intent = new[]
        {
            "narysuj klotoide",
            "draw spiral transition curve",
            "krzywa przejsciowa drogi",
            "clothoid alignment segment",
            "euler spiral road curve"
        },
        RequiresPlugin = true)]
    public static async Task<DrawAlignmentSpiralResult> DrawAlignmentSpiral(
        IPluginGateway gw, DrawAlignmentSpiralArgs args, CancellationToken ct)
    {
        if (args.LengthM <= 0)    throw new ArgumentException("lengthM must be > 0");
        if (args.EndRadiusM <= 0) throw new ArgumentException("endRadiusM must be > 0");
        if (args.Segments < 2)    throw new ArgumentException("segments must be >= 2");
        int sign = string.Equals(args.TurnDirection, "right", StringComparison.OrdinalIgnoreCase) ? -1 : 1;

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, CivilPalette.LayerRoadCntr, created, ct).ConfigureAwait(false);

        double a2 = args.EndRadiusM * args.LengthM;           // clothoid parameter A^2 = R * L
        double a = Math.Sqrt(a2);
        double bearingRad = args.StartBearingDeg * Math.PI / 180.0;
        double cosB = Math.Cos(bearingRad), sinB = Math.Sin(bearingRad);

        var verts = new Point2dDto[args.Segments + 1];
        for (int i = 0; i <= args.Segments; i++)
        {
            double l = args.LengthM * i / args.Segments;
            // 2-term truncated clothoid power series (standard drafting approximation).
            double xLocal = l - (Math.Pow(l, 5) / (40.0 * a2 * a2));
            double yLocal = sign * (Math.Pow(l, 3) / (6.0 * a2));
            // Rotate local (tangent-frame) coordinates into world space and translate.
            double wx = args.Start.X + xLocal * cosB - yLocal * sinB;
            double wy = args.Start.Y + xLocal * sinB + yLocal * cosB;
            verts[i] = new Point2dDto(wx, wy);
        }

        var entity = await CivilProxy.DrawPolylineAsync(gw, verts, closed: false, args.Layer, ct).ConfigureAwait(false);

        double deflectionRad = (args.LengthM * args.LengthM) / (2.0 * a2); // tangent angle at l = L
        double endBearingDeg = args.StartBearingDeg + sign * (deflectionRad * 180.0 / Math.PI);

        return new DrawAlignmentSpiralResult(entity, verts[^1], endBearingDeg, a, created);
    }

    [McpTool("draw_vertical_profile",
        "Draw a road vertical alignment (profile view) grade line from a list of PVI points (station, elevation, and an optional parabolic vertical-curve length centred on that PVI). Interior PVIs with a curveLengthStation get a sampled symmetric parabola instead of a sharp grade break; PVIs without one (or the first/last point) stay a straight grade line to their neighbour. Drawn as ONE polyline on C-ROAD-CNTR (default) in a local station/elevation coordinate frame: drawing X = origin.X + (station - firstStation) * horizontalScale, drawing Y = origin.Y + (elevation - datumElevation) * verticalScale -- pass datumElevation close to (but below) the lowest PVI so the profile doesn't end up thousands of drawing units above origin, exactly like a real profile sheet's datum line. verticalScale defaults to 10x (a common profile exaggeration) since 1:1 road grades read as nearly flat lines otherwise.",
        "civil",
        Intent = new[]
        {
            "narysuj profil podluzny drogi",
            "draw vertical alignment profile",
            "niweleta drogi",
            "road profile grade line",
            "PVI vertical curve profile"
        },
        RequiresPlugin = true)]
    public static async Task<DrawVerticalProfileResult> DrawVerticalProfile(
        IPluginGateway gw, DrawVerticalProfileArgs args, CancellationToken ct)
    {
        if (args.Points is null || args.Points.Count < 2)
            throw new ArgumentException("points must contain at least 2 PVIs");
        if (args.SamplesPerCurve < 2) throw new ArgumentException("samplesPerCurve must be >= 2");

        var pvis = args.Points.OrderBy(p => p.Station).ToList();
        double firstStation = pvis[0].Station;

        Point2dDto ToDrawing(double station, double elevation) => new(
            args.Origin.X + (station - firstStation) * args.HorizontalScale,
            args.Origin.Y + (elevation - args.DatumElevation) * args.VerticalScale);

        var verts = new List<Point2dDto> { ToDrawing(pvis[0].Station, pvis[0].Elevation) };

        for (int i = 1; i < pvis.Count - 1; i++)
        {
            var prev = pvis[i - 1];
            var cur  = pvis[i];
            var next = pvis[i + 1];
            double curveLen = cur.CurveLengthStation ?? 0;

            if (curveLen <= 0)
            {
                verts.Add(ToDrawing(cur.Station, cur.Elevation));
                continue;
            }

            double halfLen = curveLen / 2.0;
            if (cur.Station - halfLen < prev.Station || cur.Station + halfLen > next.Station)
                throw new ArgumentException(
                    $"curveLengthStation at PVI station {cur.Station} extends past its neighbouring PVI -- shorten it or move the PVIs further apart");

            double g1 = (cur.Elevation - prev.Elevation) / (cur.Station - prev.Station);
            double g2 = (next.Elevation - cur.Elevation) / (next.Station - cur.Station);
            double bvcStation = cur.Station - halfLen;
            double bvcElevation = cur.Elevation - g1 * halfLen;
            // Symmetric parabola: elev(x) = bvcElevation + g1*x + ((g2-g1)/(2*curveLen)) * x^2, x = distance from BVC.
            double rate = (g2 - g1) / (2.0 * curveLen);
            for (int s = 1; s <= args.SamplesPerCurve; s++)
            {
                double x = curveLen * s / args.SamplesPerCurve;
                double elev = bvcElevation + g1 * x + rate * x * x;
                verts.Add(ToDrawing(bvcStation + x, elev));
            }
        }

        verts.Add(ToDrawing(pvis[^1].Station, pvis[^1].Elevation));

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, CivilPalette.LayerRoadCntr, created, ct).ConfigureAwait(false);

        var gradeLine = await CivilProxy.DrawPolylineAsync(gw, verts.ToArray(), closed: false, args.Layer, ct).ConfigureAwait(false);
        return new DrawVerticalProfileResult(gradeLine, verts.Count, created);
    }

    [McpTool("draw_road_corridor",
        "Given a road centreline polyline + a total widthM, draws the centreline on C-ROAD-CNTR (CENTER linetype) PLUS two parallel edge polylines on C-ROAD-EDGE (Continuous), each offset by widthM/2 to either side at every vertex (mitred at internal vertices using the average of the incoming and outgoing tangent normals). Per rule 38 §6 the edges are Continuous, NOT CENTER — the layer assignment is what makes the plan readable. Returns all 3 entity handles + the widthM used.",
        "civil",
        Intent = new[]
        {
            "narysuj korytarz drogowy",
            "draw road corridor edges",
            "krawedzie jezdni offset",
            "road pavement edges",
            "draw road with width"
        },
        RequiresPlugin = true)]
    public static async Task<DrawRoadCorridorResult> DrawRoadCorridor(
        IPluginGateway gw, DrawRoadCorridorArgs args, CancellationToken ct)
    {
        if (args.WidthM <= 0) throw new ArgumentException("widthM must be > 0");
        if (args.Centerline == null || args.Centerline.Count < 2)
            throw new ArgumentException("centerline must have at least 2 vertices");

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.CenterlineLayer, CivilPalette.LayerRoadCntr, created, ct).ConfigureAwait(false);
        await EnsureLayerExactAsync(gw, existing, args.EdgeLayer,       CivilPalette.LayerRoadEdge, created, ct).ConfigureAwait(false);

        var center = await CivilProxy.DrawPolylineAsync(gw, args.Centerline, closed: false, args.CenterlineLayer, ct).ConfigureAwait(false);

        double half = args.WidthM / 2.0;
        var leftVerts  = OffsetPolyline(args.Centerline, +half);
        var rightVerts = OffsetPolyline(args.Centerline, -half);
        var left  = await CivilProxy.DrawPolylineAsync(gw, leftVerts,  closed: false, args.EdgeLayer, ct).ConfigureAwait(false);
        var right = await CivilProxy.DrawPolylineAsync(gw, rightVerts, closed: false, args.EdgeLayer, ct).ConfigureAwait(false);

        return new DrawRoadCorridorResult(center, left, right, args.WidthM, created);
    }

    // ─────────── stationing ───────────

    [McpTool("place_station_labels",
        "Walk the centreline polyline and at every interval (default 20 m) drop: (1) a small perpendicular tick mark on layer C-STAT and (2) a labelled DBText with the station notation parallel to the alignment, offset to one side. Notation respects the system flag: 'metric_km' → '0+020' (Polish / EU, default), 'us_feet' → '0+20' (US, where 1 station = 100 ft). Per rule 38 §7 ticks are perpendicular to the LOCAL tangent, recomputed at every vertex, NOT to the global +X axis.",
        "civil",
        Intent = new[]
        {
            "wstaw stacjonowanie 0+020",
            "place stationing labels along road",
            "etykiety stacjonowania",
            "station labels every 20m",
            "chainage labels along centerline"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceStationLabelsResult> PlaceStationLabels(
        IPluginGateway gw, PlaceStationLabelsArgs args, CancellationToken ct)
    {
        if (args.Centerline == null || args.Centerline.Count < 2)
            throw new ArgumentException("centerline must have at least 2 vertices");
        if (args.IntervalM <= 0) throw new ArgumentException("intervalM must be > 0");

        var system = ParseStationingSystem(args.System);

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.TickLayer,  CivilPalette.LayerStation, created, ct).ConfigureAwait(false);
        if (!string.Equals(args.LabelLayer, args.TickLayer, StringComparison.OrdinalIgnoreCase))
            await EnsureLayerExactAsync(gw, existing, args.LabelLayer, CivilPalette.LayerStation, created, ct).ConfigureAwait(false);

        // Compute total length and segment-cumulative lengths.
        var verts  = args.Centerline;
        var segLen = new double[verts.Count - 1];
        double total = 0.0;
        for (int i = 0; i < segLen.Length; i++)
        {
            double dx = verts[i + 1].X - verts[i].X;
            double dy = verts[i + 1].Y - verts[i].Y;
            segLen[i] = Math.Sqrt(dx * dx + dy * dy);
            total += segLen[i];
        }
        if (total <= 0) throw new ArgumentException("centerline has zero total length");

        var stations = new List<PlacedStation>();
        for (double s = args.StartStationM; s <= args.StartStationM + total + 1e-9; s += args.IntervalM)
        {
            double sFromStart = s - args.StartStationM;
            // Find which segment this station falls in and the local tangent.
            double cum = 0.0;
            int segIndex = segLen.Length - 1;
            for (int i = 0; i < segLen.Length; i++)
            {
                if (cum + segLen[i] >= sFromStart - 1e-9) { segIndex = i; break; }
                cum += segLen[i];
            }
            double along = sFromStart - cum;
            double t = segLen[segIndex] > 0 ? along / segLen[segIndex] : 0.0;
            var p0 = verts[segIndex];
            var p1 = verts[segIndex + 1];

            double tx = (p1.X - p0.X) / segLen[segIndex];
            double ty = (p1.Y - p0.Y) / segLen[segIndex];
            double nx = -ty, ny = tx;   // perpendicular, +90° from tangent
            double tangentDeg = Math.Atan2(ty, tx) * 180.0 / Math.PI;

            var stationPos = new Point2dDto(p0.X + tx * along, p0.Y + ty * along);
            var tickStart  = new Point2dDto(stationPos.X - nx * (args.TickLengthM / 2.0),
                                            stationPos.Y - ny * (args.TickLengthM / 2.0));
            var tickEnd    = new Point2dDto(stationPos.X + nx * (args.TickLengthM / 2.0),
                                            stationPos.Y + ny * (args.TickLengthM / 2.0));
            var tick = await CivilProxy.DrawLineAsync(gw, tickStart, tickEnd, args.TickLayer, ct).ConfigureAwait(false);

            var labelPos = new Point2dDto(stationPos.X + nx * args.LabelOffsetM,
                                          stationPos.Y + ny * args.LabelOffsetM);
            var label = CivilStationing.Format(s, system);
            var textHandle = await CivilProxy.AddDBTextAsync(
                gw, labelPos, label, args.TextHeightM, args.LabelLayer, tangentDeg, alignment: null, ct
            ).ConfigureAwait(false);

            stations.Add(new PlacedStation(s, label, stationPos, tick, textHandle));
        }
        return new PlaceStationLabelsResult(stations, created);
    }

    // ─────────── parcel ───────────

    [McpTool("draw_parcel",
        "Build a parcel polyline by walking from `start` along a list of (bearing, distance) legs and draw it on layer C-PROP (PHANTOM2 linetype, default). Bearings MUST be surveyor textual form: 'N 45 30 15 E' / 'N 45° 30\\' 15\" E' / 'S 30 W'. Computes the closure error (distance from the last vertex back to the start) and reports it in metres along with `closureStatus = 'in_tolerance' | 'out_of_tolerance'`. Tolerance is set by `kind` ('residential' < 0.02 m, 'commercial' < 0.05 m, 'agricultural' < 0.20 m, 'forest' < 0.50 m per rule 38 §3) or via `toleranceMOverride`. Setting autoClose=true closes the polyline geometrically (last vertex snapped to first) but the original closure error is still reported.",
        "civil",
        Intent = new[]
        {
            "narysuj dzialke z bearings",
            "draw parcel from bearing distance legs",
            "polyline dzialki geodezyjnej",
            "parcel closure check",
            "lot polygon from surveyor traverse"
        },
        RequiresPlugin = true)]
    public static async Task<DrawParcelResult> DrawParcel(
        IPluginGateway gw, DrawParcelArgs args, CancellationToken ct)
    {
        if (args.Legs == null || args.Legs.Count < 3)
            throw new ArgumentException("a parcel needs at least 3 legs");

        var bearingLegs = new List<(Bearing, double)>();
        for (int i = 0; i < args.Legs.Count; i++)
        {
            var leg = args.Legs[i];
            Bearing b;
            try { b = Bearing.Parse(leg.BearingText); }
            catch (Exception ex)
            {
                throw new ArgumentException($"leg #{i + 1} bearing '{leg.BearingText}' invalid: {ex.Message}", ex);
            }
            bearingLegs.Add((b, leg.DistanceM));
        }

        var kind = ParseParcelKind(args.Kind);
        double tol = args.ToleranceMOverride ?? CivilTolerances.ClosureMetresFor(kind);
        var traverse = CivilParcel.Traverse(args.Start, bearingLegs, tol);

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, CivilPalette.LayerProperty, created, ct).ConfigureAwait(false);

        // Use the traverse vertices (not snapped). If autoClose, close the polyline geometrically;
        // otherwise leave it open so the closure error is visually obvious.
        var vertices = traverse.Vertices;
        bool closed = args.AutoClose;
        var parcel = await CivilProxy.DrawPolylineAsync(gw, vertices, closed: closed, args.Layer, ct).ConfigureAwait(false);

        return new DrawParcelResult(
            Parcel: parcel,
            Vertices: vertices,
            ClosureErrorM: traverse.ClosureErrorM,
            ToleranceM: tol,
            ClosureStatus: traverse.WithinTolerance ? "in_tolerance" : "out_of_tolerance",
            AutoClosed: args.AutoClose,
            CreatedLayers: created);
    }

    // ─────────── topography ───────────

    [McpTool("draw_contour_line",
        "Draw a topographic contour line as a polyline on layer C-TOPO-MAJR (when isMajor=true, default) or C-TOPO-MINR (when isMajor=false). When isMajor=true, also drops a labelled DBText with the elevation (formatted to 2 decimals) at the labelEvery-th vertex. Per rule 38 §4 minor contours are unlabelled; major contours MUST be labelled — agents who set isMajor=true on a 1 m contour break the visual hierarchy.",
        "civil",
        Intent = new[]
        {
            "narysuj warstwice glowna",
            "draw major contour line with label",
            "contour line topograficzna",
            "warstwica 250m",
            "topographic contour line"
        },
        RequiresPlugin = true)]
    public static async Task<DrawContourLineResult> DrawContourLine(
        IPluginGateway gw, DrawContourLineArgs args, CancellationToken ct)
    {
        if (args.Vertices == null || args.Vertices.Count < 2)
            throw new ArgumentException("contour needs at least 2 vertices");

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        string layer = args.IsMajor ? args.MajorLayer : args.MinorLayer;
        string defaultLayer = args.IsMajor ? CivilPalette.LayerTopoMajr : CivilPalette.LayerTopoMinr;
        await EnsureLayerExactAsync(gw, existing, layer, defaultLayer, created, ct).ConfigureAwait(false);

        var contour = await CivilProxy.DrawPolylineAsync(gw, args.Vertices, closed: false, layer, ct).ConfigureAwait(false);

        EntityHandle? label = null;
        if (args.IsMajor && args.Vertices.Count > 0)
        {
            await EnsureLayerExactAsync(gw, existing, args.LabelLayer, CivilPalette.LayerTopoMajr, created, ct).ConfigureAwait(false);
            int idx = Math.Min(Math.Max(args.LabelEvery, 0), args.Vertices.Count - 1);
            var labelPos = args.Vertices[idx];
            var labelText = args.ElevationM.ToString("F2", CultureInfo.InvariantCulture);
            label = await CivilProxy.AddDBTextAsync(
                gw, labelPos, labelText, args.TextHeightM, args.LabelLayer, 0.0, alignment: null, ct).ConfigureAwait(false);
        }
        return new DrawContourLineResult(contour, label, layer, created);
    }

    [McpTool("place_spot_elevation",
        "Place a survey spot elevation at `position`: a small + cross (two perpendicular short lines on C-TOPO-SPOT) AND a signed elevation text formatted '+102.45' / '-1.23' (Polish PN-EN ISO 6709 conventional 2-decimal precision) offset by textOffsetM to the upper-right. Returns BOTH the cross handles and the text handle. Per rule 38 §5 drawing only the text breaks downstream takeoffs because the actual point is missing.",
        "civil",
        Intent = new[]
        {
            "punkt wysokosciowy +102.45",
            "place spot elevation cross + text",
            "rzedna terenu",
            "spot elevation marker",
            "geodetic point with elevation"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceSpotElevationResult> PlaceSpotElevation(
        IPluginGateway gw, PlaceSpotElevationArgs args, CancellationToken ct)
    {
        if (args.CrossSizeM <= 0) throw new ArgumentException("crossSizeM must be > 0");

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.CrossLayer, CivilPalette.LayerTopoSpot, created, ct).ConfigureAwait(false);
        if (!string.Equals(args.TextLayer, args.CrossLayer, StringComparison.OrdinalIgnoreCase))
            await EnsureLayerExactAsync(gw, existing, args.TextLayer, CivilPalette.LayerTopoSpot, created, ct).ConfigureAwait(false);

        double half = args.CrossSizeM / 2.0;
        var hStart = new Point2dDto(args.Position.X - half, args.Position.Y);
        var hEnd   = new Point2dDto(args.Position.X + half, args.Position.Y);
        var vStart = new Point2dDto(args.Position.X, args.Position.Y - half);
        var vEnd   = new Point2dDto(args.Position.X, args.Position.Y + half);
        var crossH = await CivilProxy.DrawLineAsync(gw, hStart, hEnd, args.CrossLayer, ct).ConfigureAwait(false);
        var crossV = await CivilProxy.DrawLineAsync(gw, vStart, vEnd, args.CrossLayer, ct).ConfigureAwait(false);

        // Sign always shown explicitly (rule 38 §5).
        string formatted = args.ElevationM >= 0
            ? "+" + args.ElevationM.ToString("F2", CultureInfo.InvariantCulture)
            : args.ElevationM.ToString("F2", CultureInfo.InvariantCulture);
        var textPos = new Point2dDto(args.Position.X + args.TextOffsetM, args.Position.Y + args.TextOffsetM * 0.5);
        var text = await CivilProxy.AddDBTextAsync(
            gw, textPos, formatted, args.TextHeightM, args.TextLayer, 0.0, alignment: null, ct).ConfigureAwait(false);

        return new PlaceSpotElevationResult(crossH, crossV, text, formatted, created);
    }

    // ─────────── north arrow ───────────

    [McpTool("draw_north_arrow",
        "Draw a basic north arrow at `position`: an isoceles triangle pointing toward TRUE north (rotated by trueNorthDegFromPageNorth from the page +Y axis per rule 38 §8) with optional 'N' letter above the tip. The triangle apex is sizeM tall, the base is 0.4 × sizeM wide, drawn on layer C-NORTH (Continuous, default). Per rule 38 §8 a north arrow with the default 0° rotation when the drawing is rotated ruins all bearings on the plan — agents MUST pass the drawing rotation explicitly. The COMPASS variant ships with the Phase-7 block library.",
        "civil",
        Intent = new[]
        {
            "wstaw strzalke polnocy",
            "draw north arrow",
            "true north arrow with rotation",
            "kompas N",
            "north indicator"
        },
        RequiresPlugin = true)]
    public static async Task<DrawNorthArrowResult> DrawNorthArrow(
        IPluginGateway gw, DrawNorthArrowArgs args, CancellationToken ct)
    {
        if (args.SizeM <= 0) throw new ArgumentException("sizeM must be > 0");

        var existing = await CivilProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, CivilPalette.LayerNorth, created, ct).ConfigureAwait(false);

        // Triangle pointing UP (+Y) before rotation, then rotated by trueNorthDeg
        // (CCW positive in math sense, but page-north convention rotates the
        // tip CW for positive declination). Stick to math convention here and
        // document in the tool description that positive == declination east.
        double rad = -args.TrueNorthDegFromPageNorth * Math.PI / 180.0; // CW positive for "true north east of page north"
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);

        // Local triangle (apex at +Y, base at -Y).
        var localApex      = (X: 0.0,                  Y: args.SizeM);
        var localBaseLeft  = (X: -args.SizeM * 0.20,   Y: -args.SizeM * 0.40);
        var localBaseRight = (X:  args.SizeM * 0.20,   Y: -args.SizeM * 0.40);
        var localCenter    = (X: 0.0,                  Y: -args.SizeM * 0.40);

        Point2dDto Tx((double X, double Y) p) =>
            new(args.Position.X + p.X * cosA - p.Y * sinA,
                args.Position.Y + p.X * sinA + p.Y * cosA);

        var tri = new[]
        {
            Tx(localApex),
            Tx(localBaseRight),
            Tx(localCenter),
            Tx(localBaseLeft),
        };
        var arrow = await CivilProxy.DrawPolylineAsync(gw, tri, closed: true, args.Layer, ct).ConfigureAwait(false);

        EntityHandle? letter = null;
        if (args.IncludeLetter)
        {
            var letterLocal = (X: 0.0, Y: args.SizeM * 1.25);
            var letterPos   = Tx(letterLocal);
            // Rotate the letter so it stays upright with the arrow.
            double letterRotDeg = -args.TrueNorthDegFromPageNorth;
            letter = await CivilProxy.AddDBTextAsync(
                gw, letterPos, "N", args.SizeM * 0.5, args.Layer, letterRotDeg, alignment: "Middle", ct
            ).ConfigureAwait(false);
        }
        return new DrawNorthArrowResult(arrow, letter, created);
    }

    // ─────────── introspection ───────────

    [McpTool("civil_health",
        "Report the 12-layer civil engineering key, the parcel-closure tolerance presets (residential / commercial / agricultural / forest), the supported stationing systems ('metric_km' / 'us_feet'), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which closure tolerance applies to a residential lot — without making a real call to AutoCAD.",
        "civil",
        Intent = new[]
        {
            "co potrafi civil",
            "list civil layer key",
            "civil parcel tolerances",
            "diagnostyka kategorii civil",
            "civil category metadata"
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static CivilHealthResult CivilHealth(CivilHealthArgs _)
    {
        var layers = CivilPalette.All
            .Select(s => new CivilLayerSpec(s.Name, s.AciColor, s.Linetype, s.LineweightMm, s.Plottable, s.Purpose))
            .ToList();
        var tolerances = Enum.GetValues(typeof(CivilParcelKind))
            .Cast<CivilParcelKind>()
            .Select(k => new CivilParcelToleranceSpec(k.ToString().ToLowerInvariant(), CivilTolerances.ClosureMetresFor(k)))
            .ToList();
        return new CivilHealthResult(
            layers, tolerances,
            new[] { "metric_km", "us_feet" },
            CivilPalette.PlannedBlocks, "civil", "0.1.0");
    }

    // ─────────── private helpers ───────────

    private static bool IsRoadLayer(string n) =>
        n == CivilPalette.LayerRoadCntr || n == CivilPalette.LayerRoadEdge ||
        n == CivilPalette.LayerRoadLane || n == CivilPalette.LayerStation;

    private static bool IsPropertyLayer(string n) =>
        n == CivilPalette.LayerProperty || n == CivilPalette.LayerEasement || n == CivilPalette.LayerRow;

    private static bool IsTopoLayer(string n) =>
        n == CivilPalette.LayerTopoMajr || n == CivilPalette.LayerTopoMinr || n == CivilPalette.LayerTopoSpot;

    private static StationingSystem ParseStationingSystem(string s) =>
        s?.ToLowerInvariant() switch
        {
            "metric_km" or "metric" or "eu" or "pn" => StationingSystem.MetricKm,
            "us_feet" or "us" or "imperial"         => StationingSystem.UsFeet,
            _ => throw new ArgumentException($"unknown stationing system '{s}' (expected 'metric_km' or 'us_feet')"),
        };

    private static CivilParcelKind ParseParcelKind(string s) =>
        s?.ToLowerInvariant() switch
        {
            "residential" => CivilParcelKind.Residential,
            "commercial"  => CivilParcelKind.Commercial,
            "agricultural" or "ag" => CivilParcelKind.Agricultural,
            "forest" or "large_tract" => CivilParcelKind.Forest,
            _ => throw new ArgumentException($"unknown parcel kind '{s}'"),
        };

    /// <summary>Offset a 2D polyline by `offset` (positive = left of the
    /// direction of travel). Mitres at internal vertices using the average
    /// of incoming and outgoing normals — produces clean corners for road
    /// edges within the v1 small-angle assumption (no acute-corner self-
    /// intersection check; that lands when the edge corridor takes spirals).</summary>
    private static Point2dDto[] OffsetPolyline(IReadOnlyList<Point2dDto> verts, double offset)
    {
        var result = new Point2dDto[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            (double tx, double ty) tIn  = i > 0                ? Tangent(verts[i - 1], verts[i]) : Tangent(verts[i], verts[i + 1]);
            (double tx, double ty) tOut = i < verts.Count - 1  ? Tangent(verts[i],     verts[i + 1]) : tIn;

            // Average tangent then perpendicular (+90° = LEFT of travel).
            double ax = tIn.tx + tOut.tx;
            double ay = tIn.ty + tOut.ty;
            double mag = Math.Sqrt(ax * ax + ay * ay);
            if (mag < 1e-9) { ax = tIn.tx; ay = tIn.ty; mag = 1.0; }
            ax /= mag; ay /= mag;
            double nx = -ay, ny = ax;     // left perpendicular

            result[i] = new Point2dDto(verts[i].X + nx * offset, verts[i].Y + ny * offset);
        }
        return result;
    }

    private static (double tx, double ty) Tangent(Point2dDto a, Point2dDto b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double m  = Math.Sqrt(dx * dx + dy * dy);
        return m > 1e-9 ? (dx / m, dy / m) : (1.0, 0.0);
    }

    /// <summary>Ensure the layer exists with the metadata of the canonical default.</summary>
    private static async Task EnsureLayerExactAsync(
        IPluginGateway gw,
        HashSet<string> existing,
        string requested,
        string defaultName,
        List<string> createdSink,
        CancellationToken ct)
    {
        var spec = CivilPalette.All.FirstOrDefault(
            s => string.Equals(s.Name, defaultName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown default layer '{defaultName}'");

        var created = await CivilProxy.EnsureLayerAsync(
            gw, existing, requested, spec.AciColor, spec.Linetype, spec.LineweightMm, spec.Plottable, ct).ConfigureAwait(false);
        if (created) createdSink.Add(requested);
    }
}
