// AutoCAD acad-dimensions category. 12 tools covering linear / aligned / angular (3-point and 2-line) /
// radial / diametric / arc-length / ordinate dimensions, baseline and continued chains derived from
// repeated linear dims, plus dimstyle list and per-entity style assignment.
//
// Rules: 19, 27 (text & dimension traps).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Dimensions;

public static class DimensionsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("dimension_linear", "Place a linear (rotated) dimension between p1 and p2 with the dim line passing through dimLinePoint at the given rotation in degrees (0 = horizontal).", "dimensions",
        Intent = new[] { "wymiar liniowy", "linear dimension", "horizontal dimension", "wymiar poziomy", "rotated linear dim" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Linear(IPluginGateway gw, LinearDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<LinearDimArgs, DimEntityResult>(gw, "acad.dimensions.linear", args, T_NORMAL, ct);

    [McpTool("dimension_aligned", "Place an aligned dimension parallel to the segment p1->p2 at dimLinePoint.", "dimensions",
        Intent = new[] { "wymiar rownolegly", "aligned dimension", "dimension parallel to segment", "wymiar wzdluz krawedzi", "dimaligned" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Aligned(IPluginGateway gw, AlignedDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<AlignedDimArgs, DimEntityResult>(gw, "acad.dimensions.aligned", args, T_NORMAL, ct);

    [McpTool("dimension_angular_3p", "Place an angular dimension defined by a vertex (center) and two rays through 'first' and 'second'. The arc passes through arcPoint.", "dimensions",
        Intent = new[] { "wymiar katowy 3 punkty", "angular dimension three points", "kat z trzech punktow", "angle 3 points", "angular dim from vertex" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Angular3p(IPluginGateway gw, AngularDim3pArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<AngularDim3pArgs, DimEntityResult>(gw, "acad.dimensions.angular_3p", args, T_NORMAL, ct);

    [McpTool("dimension_angular_2l", "Place an angular dimension between two existing line entities. The arc passes through arcPoint.", "dimensions",
        Intent = new[] { "wymiar katowy 2 linie", "angular dimension two lines", "kat miedzy liniami", "angle between two lines", "angular dim from lines" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Angular2l(IPluginGateway gw, AngularDim2lArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<AngularDim2lArgs, DimEntityResult>(gw, "acad.dimensions.angular_2l", args, T_NORMAL, ct);

    [McpTool("dimension_radial", "Place a radial dimension on a Circle or Arc; chordPoint specifies which side of the curve the leader exits.", "dimensions",
        Intent = new[] { "wymiar promienia", "radial dimension", "promien okregu wymiar", "dim radius of circle", "dimradius" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Radial(IPluginGateway gw, RadialDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<RadialDimArgs, DimEntityResult>(gw, "acad.dimensions.radial", args, T_NORMAL, ct);

    [McpTool("dimension_diametric", "Place a diametric dimension on a Circle through farPoint (the leader anchor on the far side of the curve).", "dimensions",
        Intent = new[] { "wymiar srednicy", "diametric dimension", "srednica okregu wymiar", "dim diameter of circle", "dimdiameter" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Diametric(IPluginGateway gw, DiametricDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<DiametricDimArgs, DimEntityResult>(gw, "acad.dimensions.diametric", args, T_NORMAL, ct);

    [McpTool("dimension_arc_length", "Place an arc-length dimension on an Arc; arcPoint locates the dimension arc.", "dimensions",
        Intent = new[] { "wymiar dlugosci luku", "arc length dimension", "dlugosc luku wymiar", "dim arc length", "dimarc" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> ArcLength(IPluginGateway gw, ArcLengthDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<ArcLengthDimArgs, DimEntityResult>(gw, "acad.dimensions.arc_length", args, T_NORMAL, ct);

    [McpTool("dimension_ordinate", "Place an ordinate (X or Y datum) dimension at definingPoint with leader endpoint at leaderEnd. useXAxis=true measures the X distance from UCS origin.", "dimensions",
        Intent = new[] { "wymiar wspolrzednych", "ordinate dimension", "dim x ordinate", "dim y ordinate", "wspolrzedna od ukladu" },
        RequiresPlugin = true)]
    public static Task<DimEntityResult> Ordinate(IPluginGateway gw, OrdinateDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<OrdinateDimArgs, DimEntityResult>(gw, "acad.dimensions.ordinate", args, T_NORMAL, ct);

    [McpTool("dimension_baseline_chain", "Build a baseline chain of linear dimensions from a common baseline point to N subsequent points; spacing is taken from the dimstyle's DIMDLI.", "dimensions",
        Intent = new[] { "lancuch wymiarow bazowych", "baseline dimension chain", "wymiary od bazy", "baseline chain dimensions", "dimbaseline" },
        RequiresPlugin = true)]
    public static Task<DimEntitiesResult> BaselineChain(IPluginGateway gw, BaselineDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<BaselineDimArgs, DimEntitiesResult>(gw, "acad.dimensions.baseline_chain", args, T_SLOW, ct);

    [McpTool("dimension_continued_chain", "Build a continued chain of linear dimensions: each new dimension's first extension line is the previous dimension's second extension line.", "dimensions",
        Intent = new[] { "lancuch wymiarow ciaglych", "continued dimension chain", "wymiary ciagle", "continued chain dimensions", "dimcontinue" },
        RequiresPlugin = true)]
    public static Task<DimEntitiesResult> ContinuedChain(IPluginGateway gw, ContinuedDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<ContinuedDimArgs, DimEntitiesResult>(gw, "acad.dimensions.continued_chain", args, T_SLOW, ct);

    [McpTool("list_dimstyles", "List all dimension styles defined in the active drawing plus the current Dimstyle.", "dimensions",
        Intent = new[] { "wylistuj style wymiarowania", "list dimension styles", "show dimstyles", "wszystkie dimstyles", "what dimstyles exist" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<DimStyleListResult> ListDimStyles(IPluginGateway gw, DimEmptyArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<DimEmptyArgs, DimStyleListResult>(gw, "acad.dimensions.list_dimstyles", args, T_FAST, ct);

    [McpTool("set_entity_dimstyle", "Assign a dimension style to one or more existing dimension entities (by handle).", "dimensions",
        Intent = new[] { "zmien styl wymiarow", "set dimstyle on dimension", "apply dimstyle to entities", "ustaw styl wymiarowania", "swap dimension style" },
        RequiresPlugin = true)]
    public static Task<DimAffectedCount> SetEntityDimStyle(IPluginGateway gw, SetDimStyleArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<SetDimStyleArgs, DimAffectedCount>(gw, "acad.dimensions.set_entity_dimstyle", args, T_NORMAL, ct);

    // ─────────── D6 additions ───────────

    [McpTool("ensure_architectural_dimstyle",
        "Idempotently create (or update) the ARCH-ISO dimension style used by the 3-level architectural dimension hierarchy (rule 66). Sets architectural tick arrowheads ('ArchTick'), text height scaled to plot scale, mm precision, no trailing zeros. Safe to call multiple times; returns whether the style was created, updated or left untouched.",
        "dimensions",
        Intent = new[] { "stworz styl wymiarowania architektoniczny", "ensure ARCH-ISO dimstyle", "setup architectural dim style", "dimstyle z kreskami architektonicznymi", "tick marks dimension style" },
        RequiresPlugin = true)]
    public static Task<EnsureArchitecturalDimStyleResult> EnsureArchitecturalDimStyle(
        IPluginGateway gw, EnsureArchitecturalDimStyleArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<EnsureArchitecturalDimStyleArgs, EnsureArchitecturalDimStyleResult>(
            gw, "acad.dimensions.ensure_architectural_dimstyle", args, T_NORMAL, ct);

    [McpTool("dimension_cumulative_chain",
        "Cumulative dimension chain: N dimensions sharing a SINGLE dim line, each reporting the distance from baselinePoint to point_i (running total). Use for exterior overall/axis/opening dimension rows per rule 66 §1. Differs from baseline_chain (staggered by DIMDLI) and continued_chain (end-to-end).",
        "dimensions",
        Intent = new[] { "lancuch wymiarow narastajacych", "cumulative dimension chain", "running totals dimension", "wymiary kumulatywne od bazy", "biegnace sumy wymiarow" },
        RequiresPlugin = true)]
    public static Task<DimEntitiesResult> CumulativeChain(
        IPluginGateway gw, CumulativeDimArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<CumulativeDimArgs, DimEntitiesResult>(
            gw, "acad.dimensions.cumulative_chain", args, T_SLOW, ct);

    [McpTool("apply_arch_tick_style",
        "Scan every Dimension entity on a given layer and re-assign its dimension style to the target (default ARCH-ISO). If ensureStyle=true the target style is auto-created via ensure_architectural_dimstyle first. Useful for retrofitting legacy drawings to rule 66's architectural tick convention in one call.",
        "dimensions",
        Intent = new[] { "zamien styl wymiarow na warstwie", "apply arch tick style", "retrofit dim layer to arch", "zmien wszystkie wymiary na warstwie", "normalise dim layer style" },
        RequiresPlugin = true)]
    public static Task<ApplyArchTickStyleResult> ApplyArchTickStyle(
        IPluginGateway gw, ApplyArchTickStyleArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<ApplyArchTickStyleArgs, ApplyArchTickStyleResult>(
            gw, "acad.dimensions.apply_arch_tick_style", args, T_NORMAL, ct);

    [McpTool("auto_dim_walls",
        "Automatically build a continued dimension chain across a list of wall handles. Each wall's start/end endpoints are projected onto the baseline direction through 'origin' + 'baselineDeg'; duplicates within mergeToleranceMm are collapsed. Remaining projected points are sorted and fed to continued_chain. Walls whose endpoints cannot be resolved (non-Curve entities) are returned in skippedHandles instead of aborting. Combined with ensure_architectural_dimstyle this replaces 90%+ of manual DIMLINEAR / DIMCONT chains when dimensioning a facade.",
        "dimensions",
        Intent = new[] { "automatycznie wymiaruj sciany", "auto dim walls", "chain dimensions across walls", "zrob lancuch wymiarow po scianach", "batch dimension building facade" },
        RequiresPlugin = true)]
    public static async Task<AutoDimWallsResult> AutoDimWalls(
        IPluginGateway gw, AutoDimWallsArgs args, CancellationToken ct)
    {
        if (args.WallHandles is null || args.WallHandles.Count == 0)
            throw new ArgumentException("wallHandles must contain at least one handle");
        if (args.MergeToleranceMm < 0) throw new ArgumentException("mergeToleranceMm must be >= 0");

        double rad = args.BaselineDeg * Math.PI / 180.0;
        double dx = Math.Cos(rad), dy = Math.Sin(rad);
        double nx = -dy, ny = dx;

        var points = new List<(double t, double x, double y)>();
        var skipped = new List<string>();

        foreach (var handle in args.WallHandles)
        {
            var payload = new JsonObject { ["handle"] = handle };
            JsonNode? resp;
            try
            {
                resp = await gw.InvokeAsync("acad.geometry2d.get_entity", payload, T_FAST, ct).ConfigureAwait(false);
            }
            catch
            {
                skipped.Add(handle);
                continue;
            }
            if (resp is null) { skipped.Add(handle); continue; }

            var sp = resp["startPoint"];
            var ep = resp["endPoint"];
            if (sp is null || ep is null) { skipped.Add(handle); continue; }

            double sx = sp["x"]?.GetValue<double>() ?? 0.0;
            double sy = sp["y"]?.GetValue<double>() ?? 0.0;
            double ex = ep["x"]?.GetValue<double>() ?? 0.0;
            double ey = ep["y"]?.GetValue<double>() ?? 0.0;

            double ts = (sx - args.Origin.X) * dx + (sy - args.Origin.Y) * dy;
            double te = (ex - args.Origin.X) * dx + (ey - args.Origin.Y) * dy;
            points.Add((ts, args.Origin.X + dx * ts, args.Origin.Y + dy * ts));
            points.Add((te, args.Origin.X + dx * te, args.Origin.Y + dy * te));
        }

        if (points.Count < 2)
            return new AutoDimWallsResult(new List<EntityHandle>(), points.Count, skipped);

        points.Sort((a, b) => a.t.CompareTo(b.t));
        var merged = new List<(double t, double x, double y)> { points[0] };
        foreach (var p in points.Skip(1))
            if (p.t - merged[^1].t > args.MergeToleranceMm)
                merged.Add(p);

        if (merged.Count < 2)
            return new AutoDimWallsResult(new List<EntityHandle>(), merged.Count, skipped);

        double dimOffX = args.Origin.X + nx * args.DimLineOffsetMm;
        double dimOffY = args.Origin.Y + ny * args.DimLineOffsetMm;

        var chainArgs = new ContinuedDimArgs(
            StartPoint: new Point3dDto(merged[0].x, merged[0].y, 0),
            SubsequentPoints: merged.Skip(1)
                .Select(p => new Point3dDto(p.x, p.y, 0))
                .ToList(),
            DimLinePoint: new Point3dDto(dimOffX, dimOffY, 0),
            RotationDeg: args.BaselineDeg,
            DimStyle: args.DimStyle,
            Layer: args.Layer);

        var result = await DimensionsProxy.CallAsync<ContinuedDimArgs, DimEntitiesResult>(
            gw, "acad.dimensions.continued_chain", chainArgs, T_SLOW, ct).ConfigureAwait(false);

        return new AutoDimWallsResult(result.Entities, merged.Count, skipped);
    }

    [McpTool("dimension_overall",
        "Place a single linear dimension spanning the overall bbox of one or more entities projected onto rotationDeg. Useful for 'outer-most' exterior dimensions (rule 66 level 1). rotationDeg 0° measures along X (horizontal), 90° along Y. Uses the bounding boxes fetched via acad.geometry2d.get_entity.",
        "dimensions",
        Intent = new[] { "wymiar ogolny budynku", "overall dimension", "outer dimension of entities", "dim total width", "dim overall length" },
        RequiresPlugin = true)]
    public static async Task<DimEntityResult> DimensionOverall(
        IPluginGateway gw, DimOverallArgs args, CancellationToken ct)
    {
        if (args.Handles is null || args.Handles.Count == 0)
            throw new ArgumentException("handles must contain at least one handle");

        double rad = args.RotationDeg * Math.PI / 180.0;
        double dx = Math.Cos(rad), dy = Math.Sin(rad);
        double nx = -dy, ny = dx;
        double tMin = double.PositiveInfinity, tMax = double.NegativeInfinity;
        double anchorX = 0, anchorY = 0; bool haveAnchor = false;

        foreach (var handle in args.Handles)
        {
            var payload = new JsonObject { ["handle"] = handle };
            var resp = await gw.InvokeAsync("acad.geometry2d.get_entity", payload, T_FAST, ct).ConfigureAwait(false);
            var bb = resp?["bbox"];
            if (bb is null) continue;
            double x0 = bb["min"]?["x"]?.GetValue<double>() ?? 0;
            double y0 = bb["min"]?["y"]?.GetValue<double>() ?? 0;
            double x1 = bb["max"]?["x"]?.GetValue<double>() ?? 0;
            double y1 = bb["max"]?["y"]?.GetValue<double>() ?? 0;
            foreach (var (cx, cy) in new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) })
            {
                if (!haveAnchor) { anchorX = cx; anchorY = cy; haveAnchor = true; }
                double t = cx * dx + cy * dy;
                if (t < tMin) tMin = t;
                if (t > tMax) tMax = t;
            }
        }

        if (!haveAnchor || double.IsInfinity(tMin))
            throw new InvalidOperationException("no usable bboxes for handles");

        double p1x = dx * tMin + nx * ((anchorX * nx + anchorY * ny));
        double p1y = dy * tMin + ny * ((anchorX * nx + anchorY * ny));
        double p2x = dx * tMax + nx * ((anchorX * nx + anchorY * ny));
        double p2y = dy * tMax + ny * ((anchorX * nx + anchorY * ny));
        double midX = (p1x + p2x) / 2.0 + nx * args.DimLineOffsetMm;
        double midY = (p1y + p2y) / 2.0 + ny * args.DimLineOffsetMm;

        var linArgs = new LinearDimArgs(
            P1: new Point3dDto(p1x, p1y, 0),
            P2: new Point3dDto(p2x, p2y, 0),
            DimLinePoint: new Point3dDto(midX, midY, 0),
            RotationDeg: args.RotationDeg,
            DimStyle: args.DimStyle,
            Layer: args.Layer);

        return await DimensionsProxy.CallAsync<LinearDimArgs, DimEntityResult>(
            gw, "acad.dimensions.linear", linArgs, T_NORMAL, ct).ConfigureAwait(false);
    }

    // ─────────── roadmap 3.2: editing a dimension after it is placed ───────────

    [McpTool("dimension_jogged_radius", "Place a JOGGED radius dimension on a circle or arc - AutoCAD's DIMJOGGED. This is the dimension for an arc whose true centre is off the sheet: it is drawn from overrideCenter, a false centre near the arc, and the zig-zag in the dimension line is what tells the reader the centre is not really there. The MEASUREMENT is still the real radius, and the tool refuses to return a dimension whose measurement does not match the curve. Use dimensions.radial when the centre is on the sheet.", "dimensions",
        Intent = new[] { "wymiar promienia z odsadzeniem", "promien luku o srodku poza arkuszem",
                         "jogged radius dimension", "dimjogged", "radius dimension with a jog",
                         "wymiar duzego promienia" },
        RequiresPlugin = true)]
    public static Task<JoggedRadiusResult> DimensionJoggedRadius(IPluginGateway gw, JoggedRadiusArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<JoggedRadiusArgs, JoggedRadiusResult>(gw, "acad.dimensions.jogged_radius", args, T_NORMAL, ct);

    [McpTool("dimension_oblique", "Lean the EXTENSION lines of linear and aligned dimensions to a given angle - AutoCAD's DIMEDIT Oblique. This is what separates a crowded chain of dimensions into something readable, and it changes appearance only: the dimension line and the measured distance are untouched, and the tool refuses to report success if a measurement moved. Radial, diametric and angular dimensions have no extension lines and are refused by name. Pass obliqueDeg 0 to straighten them again.", "dimensions",
        Intent = new[] { "pochyl linie pomocnicze wymiaru", "rozsun stloczone wymiary",
                         "oblique dimension extension lines", "dimedit oblique",
                         "skos linii wymiarowych", "make a crowded dimension chain readable" },
        RequiresPlugin = true)]
    public static Task<ObliqueResult> DimensionOblique(IPluginGateway gw, ObliqueArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<ObliqueArgs, ObliqueResult>(gw, "acad.dimensions.oblique", args, T_NORMAL, ct);

    [McpTool("edit_dimension_text", "Override what a dimension DISPLAYS, and where that text sits, without touching what it measured. AutoCAD's text conventions are carried through and reported: \"\" means show the measurement (the default state, not blank), \"<>\" embeds the measurement inside your own text, and a single space suppresses the text altogether. textPosition moves the text, resetPosition puts it back where the style wants it, and textRotationDeg turns it. The measurement is read before and after and a change is reported as a failure, because an override must never alter the number the drawing is a record of.", "dimensions",
        Intent = new[] { "zmien tekst wymiaru", "dopisz TYP do wymiaru", "przesun tekst wymiaru",
                         "edit dimension text", "override dimension text",
                         "move dimension text", "add a suffix to a dimension" },
        RequiresPlugin = true)]
    public static Task<EditDimTextResult> EditDimensionText(IPluginGateway gw, EditDimTextArgs args, CancellationToken ct)
        => DimensionsProxy.CallAsync<EditDimTextArgs, EditDimTextResult>(gw, "acad.dimensions.edit_dimension_text", args, T_NORMAL, ct);
}
