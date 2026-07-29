// acad-verticals category. 7 composite tools (straight stair, spiral stair,
// U-shaped stair, ramp, elevator, escalator, platform lift, handrail) that
// orchestrate acad.geometry2d.* + acad.annotations.* primitives via
// ArchitectureProxy. Rule 67 (grid axes + verticals) + rule 35 §2 (composites).
//
// Every tool returns a compliance-warnings list based on WT (Polish
// Warunki Techniczne) §54..§68 thresholds captured in VerticalsPalette.
// Validators under validators/architectural/ turn those warnings into
// hard failures when running a safety audit.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Verticals;

public static class VerticalsTools
{
    private const int T_NORMAL = 20_000;
    private const int T_SLOW = 45_000;

    [McpTool("draw_stair_straight", "Draw a straight-run stair in plan view: outline rectangle, tread lines, UP/DN direction arrow with label, all on layers A-STRS and A-STRS-DIR. Computes tread depth from runLengthMm / treadCount and emits compliance warnings against WT §54 (public stair: 150-175 mm riser, 250-350 mm tread, 1200 mm min clear width).", "verticals",
        Intent = new[] { "narysuj schody proste", "draw straight stair", "insert stair run", "schody 1-biegowe", "straight-run stair plan" },
        RequiresPlugin = true)]
    public static async Task<StraightStairResult> DrawStairStraight(IPluginGateway gw, StraightStairArgs args, CancellationToken ct)
    {
        if (args.TreadCount < 2) throw new ArgumentException("treadCount must be >= 2.");
        if (args.RunLengthMm <= 0 || args.WidthMm <= 0) throw new ArgumentException("runLength / width must be positive.");

        var warnings = new List<string>();
        double treadDepth = args.RunLengthMm / args.TreadCount;
        if (treadDepth < VerticalsPalette.PublicStairTreadMinMm || treadDepth > VerticalsPalette.PublicStairTreadMaxMm)
            warnings.Add($"WT §54: tread depth {treadDepth:F0} mm outside 250..350 mm for public stairs.");
        if (args.RiserHeightMm < VerticalsPalette.PublicStairRiserMinMm || args.RiserHeightMm > VerticalsPalette.PublicStairRiserMaxMm)
            warnings.Add($"WT §54: riser {args.RiserHeightMm:F0} mm outside 150..175 mm for public stairs.");
        if (args.WidthMm < VerticalsPalette.PublicStairClearWidthMinMm)
            warnings.Add($"WT §54: clear width {args.WidthMm:F0} mm below 1200 mm public stair minimum.");
        // Blondel's rule: 2*riser + tread should sit between 600 and 650 mm.
        double blondel = 2 * args.RiserHeightMm + treadDepth;
        if (blondel < 600.0 || blondel > 650.0)
            warnings.Add($"Blondel ratio {blondel:F0} mm outside 600..650 mm comfort band.");

        double cos = Math.Cos(args.RotationDeg * Math.PI / 180.0);
        double sin = Math.Sin(args.RotationDeg * Math.PI / 180.0);

        Point2dDto Local(double x, double y) => new(
            args.Origin.X + x * cos - y * sin,
            args.Origin.Y + x * sin + y * cos);

        var outlineVerts = new List<Point2dDto>
        {
            Local(0, 0),
            Local(args.RunLengthMm, 0),
            Local(args.RunLengthMm, args.WidthMm),
            Local(0, args.WidthMm),
        };
        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, outlineVerts, closed: true, args.Layer, ct).ConfigureAwait(false);

        var treads = new List<EntityHandle>(args.TreadCount - 1);
        for (int i = 1; i < args.TreadCount; i++)
        {
            double x = i * treadDepth;
            treads.Add(await ArchitectureProxy.DrawLineAsync(gw, Local(x, 0), Local(x, args.WidthMm), args.Layer, ct).ConfigureAwait(false));
        }

        // Direction arrow along the run, centred in width, 60% of run length.
        double arrowY = args.WidthMm * 0.5;
        double ax1 = args.RunLengthMm * 0.2;
        double ax2 = args.RunLengthMm * 0.8;
        var arrowStart = args.DirectionUp ? Local(ax1, arrowY) : Local(ax2, arrowY);
        var arrowEnd   = args.DirectionUp ? Local(ax2, arrowY) : Local(ax1, arrowY);
        var arrow = await ArchitectureProxy.DrawLineAsync(gw, arrowStart, arrowEnd, args.ArrowLayer, ct).ConfigureAwait(false);

        double labelHeight = Math.Max(args.WidthMm * 0.15, 150.0);
        var label = await ArchitectureProxy.AddDBTextAsync(gw,
            Local(args.RunLengthMm * 0.5, args.WidthMm * 0.5 + labelHeight * 0.75),
            args.Label,
            labelHeight,
            VerticalsPalette.LayerAnnoNote,
            args.RotationDeg,
            ct).ConfigureAwait(false);

        return new StraightStairResult(outline, treads, arrow, label, treadDepth, warnings);
    }

    [McpTool("draw_stair_spiral", "Draw a spiral stair in plan view: outer arc, inner arc (column), radial tread lines, centre dot + label. Rule 67 §3 (spiral).", "verticals",
        Intent = new[] { "narysuj schody spiralne", "draw spiral stair", "krecone schody", "circular stair plan", "helical stair" },
        RequiresPlugin = true)]
    public static async Task<SpiralStairResult> DrawStairSpiral(IPluginGateway gw, SpiralStairArgs args, CancellationToken ct)
    {
        if (args.TreadCount < 3) throw new ArgumentException("treadCount must be >= 3 for spiral stair.");
        if (args.RadiusMm <= args.InnerRadiusMm) throw new ArgumentException("radiusMm must exceed innerRadiusMm.");
        if (args.SweepDeg <= 0 || args.SweepDeg > 360) throw new ArgumentException("sweepDeg must be in (0, 360].");

        double startRad = args.StartAngleDeg * Math.PI / 180.0;
        double endRad   = (args.StartAngleDeg + args.SweepDeg) * Math.PI / 180.0;

        var outer = await ArchitectureProxy.DrawArcAsync(gw, args.Center, args.RadiusMm, args.StartAngleDeg, args.StartAngleDeg + args.SweepDeg, args.Layer, ct).ConfigureAwait(false);
        var inner = await ArchitectureProxy.DrawArcAsync(gw, args.Center, args.InnerRadiusMm, args.StartAngleDeg, args.StartAngleDeg + args.SweepDeg, args.Layer, ct).ConfigureAwait(false);

        var treads = new List<EntityHandle>();
        for (int i = 0; i <= args.TreadCount; i++)
        {
            double t = startRad + (endRad - startRad) * (i / (double)args.TreadCount);
            var p1 = new Point2dDto(args.Center.X + args.InnerRadiusMm * Math.Cos(t), args.Center.Y + args.InnerRadiusMm * Math.Sin(t));
            var p2 = new Point2dDto(args.Center.X + args.RadiusMm * Math.Cos(t),      args.Center.Y + args.RadiusMm * Math.Sin(t));
            treads.Add(await ArchitectureProxy.DrawLineAsync(gw, p1, p2, args.Layer, ct).ConfigureAwait(false));
        }

        double labelH = Math.Max(args.RadiusMm * 0.15, 150.0);
        var label = await ArchitectureProxy.AddDBTextAsync(gw, args.Center, args.DirectionUp ? "UP" : "DN", labelH, VerticalsPalette.LayerAnnoNote, 0.0, ct).ConfigureAwait(false);
        return new SpiralStairResult(outer, inner, treads, label);
    }

    [McpTool("draw_stair_u_shaped", "Draw a U-shaped (two-run, 180° return) stair: two straight runs separated by gapMm + one landing rectangle. Each run returns its own compliance warnings.", "verticals",
        Intent = new[] { "narysuj schody U", "draw u-shaped stair", "return stair", "schody dwubiegowe", "stair with landing" },
        RequiresPlugin = true)]
    public static async Task<UShapedStairResult> DrawStairUShaped(IPluginGateway gw, UShapedStairArgs args, CancellationToken ct)
    {
        if (args.TreadsPerRun < 2) throw new ArgumentException("treadsPerRun must be >= 2.");
        if (args.LandingDepthMm < args.WidthMm)
            throw new ArgumentException("landingDepthMm must be at least run width.");

        double cos = Math.Cos(args.RotationDeg * Math.PI / 180.0);
        double sin = Math.Sin(args.RotationDeg * Math.PI / 180.0);
        Point2dDto Local(double x, double y) => new(
            args.Origin.X + x * cos - y * sin,
            args.Origin.Y + x * sin + y * cos);

        var firstRun = await DrawStairStraight(gw, new StraightStairArgs(
            Origin: args.Origin,
            RunLengthMm: args.RunLengthMm,
            WidthMm: args.WidthMm,
            TreadCount: args.TreadsPerRun,
            RiserHeightMm: args.RiserHeightMm,
            RotationDeg: args.RotationDeg,
            DirectionUp: args.DirectionUp,
            Label: args.DirectionUp ? "UP" : "DN",
            Layer: args.Layer,
            ArrowLayer: args.ArrowLayer), ct).ConfigureAwait(false);

        // Landing sits after the first run, total depth = 2*width + gap.
        var landingVerts = new List<Point2dDto>
        {
            Local(args.RunLengthMm, 0),
            Local(args.RunLengthMm + args.LandingDepthMm, 0),
            Local(args.RunLengthMm + args.LandingDepthMm, 2 * args.WidthMm + args.GapMm),
            Local(args.RunLengthMm, 2 * args.WidthMm + args.GapMm),
        };
        var landing = await ArchitectureProxy.DrawPolylineAsync(gw, landingVerts, closed: true, args.Layer, ct).ConfigureAwait(false);

        // Second run runs back in the -X direction.
        var secondOrigin = Local(args.RunLengthMm, args.WidthMm + args.GapMm);
        var secondRun = await DrawStairStraight(gw, new StraightStairArgs(
            Origin: secondOrigin,
            RunLengthMm: args.RunLengthMm,
            WidthMm: args.WidthMm,
            TreadCount: args.TreadsPerRun,
            RiserHeightMm: args.RiserHeightMm,
            RotationDeg: args.RotationDeg + 180.0,
            DirectionUp: args.DirectionUp,
            Label: args.DirectionUp ? "UP" : "DN",
            Layer: args.Layer,
            ArrowLayer: args.ArrowLayer), ct).ConfigureAwait(false);

        return new UShapedStairResult(firstRun, secondRun, landing);
    }

    [McpTool("draw_ramp", "Draw an accessibility ramp: rectangle outline, slope arrow (up or down) along length, percentage label. Emits compliance warnings against WT §66 (accessible ramp max 6% for rise > 500 mm).", "verticals",
        Intent = new[] { "narysuj pochylnie", "draw ramp", "accessibility ramp", "pochylnia dla niepelnosprawnych", "wheelchair ramp plan" },
        RequiresPlugin = true)]
    public static async Task<RampResult> DrawRamp(IPluginGateway gw, RampArgs args, CancellationToken ct)
    {
        if (args.LengthMm <= 0 || args.WidthMm <= 0 || args.RiseMm <= 0)
            throw new ArgumentException("length / width / rise must be positive.");
        double slope = args.RiseMm / args.LengthMm;
        var warnings = new List<string>();
        if (args.Accessible && args.RiseMm > 500.0 && slope > VerticalsPalette.AccessibleRampMaxSlope)
            warnings.Add($"WT §66: slope {slope * 100:F1}% exceeds 6% max for accessible ramp with rise > 500 mm.");
        if (args.Accessible && args.WidthMm < 1200.0)
            warnings.Add($"WT §66: accessible ramp width {args.WidthMm:F0} mm below 1200 mm.");

        double cos = Math.Cos(args.RotationDeg * Math.PI / 180.0);
        double sin = Math.Sin(args.RotationDeg * Math.PI / 180.0);
        Point2dDto Local(double x, double y) => new(
            args.Origin.X + x * cos - y * sin,
            args.Origin.Y + x * sin + y * cos);

        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, new List<Point2dDto>
        {
            Local(0, 0),
            Local(args.LengthMm, 0),
            Local(args.LengthMm, args.WidthMm),
            Local(0, args.WidthMm),
        }, closed: true, args.Layer, ct).ConfigureAwait(false);

        double arrowY = args.WidthMm * 0.5;
        var arrowStart = args.DirectionUp ? Local(args.LengthMm * 0.1, arrowY) : Local(args.LengthMm * 0.9, arrowY);
        var arrowEnd   = args.DirectionUp ? Local(args.LengthMm * 0.9, arrowY) : Local(args.LengthMm * 0.1, arrowY);
        var arrow = await ArchitectureProxy.DrawLineAsync(gw, arrowStart, arrowEnd, args.ArrowLayer, ct).ConfigureAwait(false);

        double labelH = Math.Max(args.WidthMm * 0.15, 150.0);
        var label = await ArchitectureProxy.AddDBTextAsync(gw,
            Local(args.LengthMm * 0.5, args.WidthMm * 0.5 + labelH * 0.75),
            $"{slope * 100:F1}%",
            labelH,
            VerticalsPalette.LayerAnnoNote,
            args.RotationDeg,
            ct).ConfigureAwait(false);

        return new RampResult(outline, arrow, label, slope * 100.0, warnings);
    }

    [McpTool("insert_elevator_v", "Draw an elevator shaft in plan: rectangle outline, two diagonal lines (indicates lift symbol per ISO 7001), centred kind label. Kind must be 'passenger', 'bed' or 'goods'; the tool warns when cabin size falls below the kind-specific minimum (bed-lift 1600×2600 in hospitals per WT §193; passenger lift 1100×1400).", "verticals",
        Intent = new[] { "wstaw winde", "insert elevator", "draw lift shaft", "bed lift hospital", "passenger elevator", "winda szpitalna" },
        RequiresPlugin = true)]
    public static async Task<ElevatorResult> InsertElevatorV(IPluginGateway gw, ElevatorArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0 || args.DepthMm <= 0) throw new ArgumentException("width / depth must be positive.");
        var warnings = new List<string>();
        var kind = (args.Kind ?? "passenger").ToLowerInvariant();
        if (kind == "bed")
        {
            if (args.WidthMm < VerticalsPalette.BedLiftMinWidthMm || args.DepthMm < VerticalsPalette.BedLiftMinDepthMm)
                warnings.Add($"WT §193: bed-lift cabin {args.WidthMm:F0}×{args.DepthMm:F0} mm below 1600×2600 mm hospital minimum.");
        }
        else if (kind == "passenger")
        {
            if (args.WidthMm < VerticalsPalette.PassengerLiftMinWidthMm || args.DepthMm < VerticalsPalette.PassengerLiftMinDepthMm)
                warnings.Add($"passenger lift cabin {args.WidthMm:F0}×{args.DepthMm:F0} mm below 1100×1400 mm accessibility minimum.");
        }

        double cos = Math.Cos(args.RotationDeg * Math.PI / 180.0);
        double sin = Math.Sin(args.RotationDeg * Math.PI / 180.0);
        double hw = args.WidthMm * 0.5;
        double hd = args.DepthMm * 0.5;
        Point2dDto Local(double x, double y) => new(
            args.Center.X + x * cos - y * sin,
            args.Center.Y + x * sin + y * cos);

        var corners = new List<Point2dDto>
        {
            Local(-hw, -hd),
            Local( hw, -hd),
            Local( hw,  hd),
            Local(-hw,  hd),
        };
        var shaft = await ArchitectureProxy.DrawPolylineAsync(gw, corners, closed: true, args.Layer, ct).ConfigureAwait(false);
        var diag1 = await ArchitectureProxy.DrawLineAsync(gw, corners[0], corners[2], args.Layer, ct).ConfigureAwait(false);
        var diag2 = await ArchitectureProxy.DrawLineAsync(gw, corners[1], corners[3], args.Layer, ct).ConfigureAwait(false);

        var labelText = args.Label ?? kind switch { "bed" => "BED-LIFT", "goods" => "GOODS", _ => "LIFT" };
        double labelH = Math.Max(Math.Min(args.WidthMm, args.DepthMm) * 0.15, 150.0);
        var label = await ArchitectureProxy.AddDBTextAsync(gw, args.Center, labelText, labelH, VerticalsPalette.LayerAnnoNote, args.RotationDeg, ct).ConfigureAwait(false);

        return new ElevatorResult(shaft, new[] { diag1, diag2 }, label, warnings);
    }

    [McpTool("insert_escalator", "Draw an escalator run: rectangle outline, evenly-spaced step lines, direction arrow, UP/DN label. Rotation locates the lower landing at origin.", "verticals",
        Intent = new[] { "wstaw schody ruchome", "insert escalator", "moving stair", "draw escalator run", "ruchome schody" },
        RequiresPlugin = true)]
    public static async Task<EscalatorResult> InsertEscalator(IPluginGateway gw, EscalatorArgs args, CancellationToken ct)
    {
        if (args.StepCount < 4) throw new ArgumentException("stepCount must be >= 4.");

        double cos = Math.Cos(args.RotationDeg * Math.PI / 180.0);
        double sin = Math.Sin(args.RotationDeg * Math.PI / 180.0);
        Point2dDto Local(double x, double y) => new(
            args.Origin.X + x * cos - y * sin,
            args.Origin.Y + x * sin + y * cos);

        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, new List<Point2dDto>
        {
            Local(0, 0),
            Local(args.LengthMm, 0),
            Local(args.LengthMm, args.WidthMm),
            Local(0, args.WidthMm),
        }, closed: true, args.Layer, ct).ConfigureAwait(false);

        var steps = new List<EntityHandle>(args.StepCount - 1);
        double stepLen = args.LengthMm / args.StepCount;
        for (int i = 1; i < args.StepCount; i++)
        {
            double x = i * stepLen;
            steps.Add(await ArchitectureProxy.DrawLineAsync(gw, Local(x, 0), Local(x, args.WidthMm), args.Layer, ct).ConfigureAwait(false));
        }

        double arrowY = args.WidthMm * 0.5;
        var arrowStart = args.DirectionUp ? Local(args.LengthMm * 0.2, arrowY) : Local(args.LengthMm * 0.8, arrowY);
        var arrowEnd   = args.DirectionUp ? Local(args.LengthMm * 0.8, arrowY) : Local(args.LengthMm * 0.2, arrowY);
        var arrow = await ArchitectureProxy.DrawLineAsync(gw, arrowStart, arrowEnd, args.ArrowLayer, ct).ConfigureAwait(false);

        double labelH = Math.Max(args.WidthMm * 0.15, 150.0);
        var label = await ArchitectureProxy.AddDBTextAsync(gw,
            Local(args.LengthMm * 0.5, args.WidthMm * 0.5 + labelH * 0.75),
            args.DirectionUp ? "UP" : "DN",
            labelH,
            VerticalsPalette.LayerAnnoNote,
            args.RotationDeg,
            ct).ConfigureAwait(false);

        return new EscalatorResult(outline, steps, arrow, label);
    }

    [McpTool("insert_platform_lift", "Draw a platform lift (wheelchair lift / stair-chair): rectangle outline + PL label. Default 1100×1400 mm matches PN-EN 81-41.", "verticals",
        Intent = new[] { "wstaw platforme dla niepelnosprawnych", "insert platform lift", "wheelchair lift", "stair chair", "podnosnik dla wozkow" },
        RequiresPlugin = true)]
    public static async Task<PlatformLiftResult> InsertPlatformLift(IPluginGateway gw, PlatformLiftArgs args, CancellationToken ct)
    {
        double cos = Math.Cos(args.RotationDeg * Math.PI / 180.0);
        double sin = Math.Sin(args.RotationDeg * Math.PI / 180.0);
        double hw = args.WidthMm * 0.5;
        double hd = args.DepthMm * 0.5;
        Point2dDto Local(double x, double y) => new(
            args.Center.X + x * cos - y * sin,
            args.Center.Y + x * sin + y * cos);

        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, new List<Point2dDto>
        {
            Local(-hw, -hd), Local(hw, -hd), Local(hw, hd), Local(-hw, hd),
        }, closed: true, args.Layer, ct).ConfigureAwait(false);

        double labelH = Math.Max(Math.Min(args.WidthMm, args.DepthMm) * 0.2, 100.0);
        var label = await ArchitectureProxy.AddDBTextAsync(gw, args.Center, args.Label, labelH, VerticalsPalette.LayerAnnoNote, args.RotationDeg, ct).ConfigureAwait(false);
        return new PlatformLiftResult(outline, label);
    }

    [McpTool("draw_handrail", "Draw a handrail run as a polyline on A-RAIL with an optional mm-height annotation. Warns when heightMm falls outside WT §298 public stair range 900..1100 mm.", "verticals",
        Intent = new[] { "narysuj porecz", "draw handrail", "stair handrail", "balustrada", "railing on stair" },
        RequiresPlugin = true)]
    public static async Task<HandrailResult> DrawHandrail(IPluginGateway gw, HandrailArgs args, CancellationToken ct)
    {
        if (args.Path is null || args.Path.Count < 2) throw new ArgumentException("path must have >= 2 points.");
        var warnings = new List<string>();
        if (args.HeightMm < VerticalsPalette.HandrailHeightMinMm || args.HeightMm > VerticalsPalette.HandrailHeightMaxMm)
            warnings.Add($"WT §298: handrail height {args.HeightMm:F0} mm outside 900..1100 mm for public stairs.");

        var poly = await ArchitectureProxy.DrawPolylineAsync(gw, args.Path, closed: false, args.Layer, ct).ConfigureAwait(false);
        EntityHandle? label = null;
        if (args.Annotate)
        {
            var mid = args.Path[args.Path.Count / 2];
            label = await ArchitectureProxy.AddDBTextAsync(gw, mid, $"RAIL h={args.HeightMm:F0}", 120.0, VerticalsPalette.LayerAnnoNote, 0.0, ct).ConfigureAwait(false);
        }
        return new HandrailResult(poly, label, warnings);
    }
}
