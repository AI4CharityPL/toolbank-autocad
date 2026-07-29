// AutoCAD acad-mechanical domain category. 10 high-level mechanical-drafting
// tools that compose primitives from acad-geometry-2d, acad-layers, and
// acad-annotations per rule 35 §2. Implements rule 37 (mechanical traps):
// edge-class linetypes (visible / hidden / centre), section cut + arrows +
// labels, the three plan-view hole variants, threaded-hole 3/4 minor arc
// (rule 37 §4a), bolt-head top view as flat-to-flat hexagon, and the filled
// equilateral-triangle revision marker.
//
// v1 limitations (called out in tool descriptions):
//   * Side-view hole variants (counterbore depth, blind hole drill point,
//     countersink flare) ship in Phase 7 along with the bundled DWG library.
//   * `draw_section_hatch` is intentionally NOT in v1 — it depends on having
//     an existing closed boundary. Use acad-geometry2d.draw_hatch with the
//     pattern returned by `mechanical_health.materials[*].pattern`.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Mechanical;

public static class MechanicalTools
{
    private const int T_NORMAL = 15_000;

    // ─────────── infrastructure ───────────

    [McpTool("ensure_mechanical_layers",
        "Idempotently create the ISO-mechanical 11-layer key (ME-VISIBLE, ME-HIDDEN, ME-CENTER, ME-DIMS, ME-TEXT, ME-SECTION, ME-HATCH, ME-THREAD, ME-CONSTRUCTION, ME-TITLE, ME-REV) per rule 37 §9, with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. ME-VISIBLE = 0.50 mm Continuous, ME-HIDDEN = 0.25 mm HIDDEN, ME-CENTER = 0.18 mm CENTER, ME-SECTION = 0.70 mm PHANTOM). Existing layers are left alone, never overwritten. ME-CONSTRUCTION is non-plottable. includeConstruction=false skips it; includeRevision=false skips ME-REV.",
        "mechanical",
        Intent = new[]
        {
            "stworz wszystkie warstwy mechaniczne",
            "ensure mechanical layers ISO",
            "setup ME-* layer key",
            "wlacz standardowe warstwy ME-* w projekcie",
            "create ISO mechanical layer standard"
        },
        RequiresPlugin = true)]
    public static async Task<EnsureMechanicalLayersResult> EnsureMechanicalLayers(
        IPluginGateway gw, EnsureMechanicalLayersArgs args, CancellationToken ct)
    {
        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var outcomes = new List<LayerEnsureOutcome>();
        int created = 0, already = 0;
        foreach (var spec in MechanicalPalette.All)
        {
            if (!args.IncludeConstruction && spec.Name == MechanicalPalette.LayerConstruction) continue;
            if (!args.IncludeRevision     && spec.Name == MechanicalPalette.LayerRev)         continue;
            try
            {
                var didCreate = await MechanicalProxy.EnsureLayerAsync(
                    gw, existing, spec.Name, spec.AciColor, spec.Linetype, spec.LineweightMm, spec.Plottable, ct
                ).ConfigureAwait(false);
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
        return new EnsureMechanicalLayersResult(outcomes, created, already);
    }

    // ─────────── edge-class lines ───────────

    [McpTool("draw_visible_edge",
        "Draw a visible feature edge as a Continuous line on layer ME-VISIBLE (default). Use this rather than acad-geometry2d.draw_line whenever the line has semantic meaning — the layer assignment is what makes the drawing readable per ISO 128.",
        "mechanical",
        Intent = new[]
        {
            "narysuj widoczna krawedz",
            "draw visible edge line",
            "linia widoczna",
            "ME-VISIBLE line",
            "edge on visible layer"
        },
        RequiresPlugin = true)]
    public static Task<EdgeLineResult> DrawVisibleEdge(IPluginGateway gw, DrawVisibleEdgeArgs args, CancellationToken ct)
        => DrawTypedEdge(gw, args.Start, args.End, args.Layer, MechanicalPalette.LayerVisible, ct);

    [McpTool("draw_hidden_edge",
        "Draw an occluded edge as a HIDDEN line on layer ME-HIDDEN (default). Per rule 37 §1 hidden geometry MUST live on its own layer — drafting it on ME-VISIBLE is the #1 'looks fine, fails inspection' bug.",
        "mechanical",
        Intent = new[]
        {
            "narysuj krawedz ukryta",
            "draw hidden edge line",
            "linia kreskowa",
            "ME-HIDDEN line",
            "occluded edge"
        },
        RequiresPlugin = true)]
    public static Task<EdgeLineResult> DrawHiddenEdge(IPluginGateway gw, DrawHiddenEdgeArgs args, CancellationToken ct)
        => DrawTypedEdge(gw, args.Start, args.End, args.Layer, MechanicalPalette.LayerHidden, ct);

    [McpTool("draw_centerline",
        "Draw an axis / centreline as a CENTER line on layer ME-CENTER (default). For round features prefer draw_centerline_cross which sizes the extension automatically per ISO 128.",
        "mechanical",
        Intent = new[]
        {
            "narysuj os",
            "draw centerline axis",
            "linia osiowa CENTER",
            "ME-CENTER line",
            "centerline ISO 128"
        },
        RequiresPlugin = true)]
    public static Task<EdgeLineResult> DrawCenterline(IPluginGateway gw, DrawCenterlineArgs args, CancellationToken ct)
        => DrawTypedEdge(gw, args.Start, args.End, args.Layer, MechanicalPalette.LayerCenter, ct);

    [McpTool("draw_centerline_cross",
        "Draw the canonical round-feature centreline crosshair: TWO perpendicular CENTER-linetype lines on layer ME-CENTER (default), each extending featureRadiusMm + extensionMm beyond the centre point in both directions, rotated by rotationDeg. Per rule 37 §2 this is what a circle's centreline SHOULD look like — agents who try to do it with two raw draw_centerline calls usually forget the extension and the drawing looks like a `+` glued to the circle.",
        "mechanical",
        Intent = new[]
        {
            "krzyz osiowy okregu",
            "draw centerline cross",
            "krzyz na okregu",
            "centerline crosshair for hole",
            "axes through center"
        },
        RequiresPlugin = true)]
    public static async Task<CenterlineCrossResult> DrawCenterlineCross(
        IPluginGateway gw, DrawCenterlineCrossArgs args, CancellationToken ct)
    {
        if (args.FeatureRadiusMm <= 0)  throw new ArgumentException("featureRadiusMm must be > 0");
        if (args.ExtensionMm     <  0)  throw new ArgumentException("extensionMm must be >= 0");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureCenterLayerAsync(gw, existing, args.Layer, created, ct).ConfigureAwait(false);

        double r = args.FeatureRadiusMm + args.ExtensionMm;
        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
        double pcosA = -sinA, psinA = cosA;

        var hStart = new Point2dDto(args.Center.X - cosA * r,  args.Center.Y - sinA * r);
        var hEnd   = new Point2dDto(args.Center.X + cosA * r,  args.Center.Y + sinA * r);
        var vStart = new Point2dDto(args.Center.X - pcosA * r, args.Center.Y - psinA * r);
        var vEnd   = new Point2dDto(args.Center.X + pcosA * r, args.Center.Y + psinA * r);

        var h = await MechanicalProxy.DrawLineAsync(gw, hStart, hEnd, args.Layer, ct).ConfigureAwait(false);
        var v = await MechanicalProxy.DrawLineAsync(gw, vStart, vEnd, args.Layer, ct).ConfigureAwait(false);

        return new CenterlineCrossResult(h, v, created);
    }

    // ─────────── section views ───────────

    [McpTool("draw_section_cut_line",
        "Draw a section cutting plane line per ISO 128 type H: thick PHANTOM polyline on layer ME-SECTION (lineweight 0.70 mm by default via the ensured layer), arrow heads on each end pointing in the viewing direction (perpendicular to the cut, pointing OUTWARD from the start→end direction by rotating +90°), and a label DBText on layer ME-TEXT placed at each end. Returns all 5 entity handles. Per rule 37 §3 the sectioned hatch is NOT drawn here — call acad-geometry2d.draw_hatch on the resulting sectioned-view boundary separately.",
        "mechanical",
        Intent = new[]
        {
            "narysuj linie cieciowa A-A",
            "draw section cut line A",
            "section cutting plane",
            "linia przekroju z grotami",
            "cutting plane line ISO 128"
        },
        RequiresPlugin = true)]
    public static async Task<DrawSectionCutLineResult> DrawSectionCutLine(
        IPluginGateway gw, DrawSectionCutLineArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Label)) throw new ArgumentException("label must not be empty");
        if (args.ArrowSizeMm <= 0)                  throw new ArgumentException("arrowSizeMm must be > 0");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureSectionLayerAsync(gw, existing, args.SectionLayer, created, ct).ConfigureAwait(false);
        await EnsureTextLayerAsync(gw, existing, args.TextLayer, created, ct).ConfigureAwait(false);

        // Cutting plane = single 2-vertex polyline (so it inherits PHANTOM nicely).
        var cuttingPlane = await MechanicalProxy.DrawPolylineAsync(
            gw, new[] { args.Start, args.End }, closed: false, args.SectionLayer, ct).ConfigureAwait(false);

        // Direction along the cut, and outward perpendicular (+90° from start→end).
        double dx = args.End.X - args.Start.X;
        double dy = args.End.Y - args.Start.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) throw new ArgumentException("section start and end coincide");
        double tx = dx / len, ty = dy / len;
        double nx = -ty,      ny = tx;          // outward perpendicular

        // Arrow head = filled-style triangle (open polyline 3 points), tip ON the cut endpoint,
        // base offset outward by arrowSizeMm.
        var arrowStart = ArrowAt(args.Start, -tx, -ty, nx, ny, args.ArrowSizeMm);
        var arrowEnd   = ArrowAt(args.End,    tx,  ty, nx, ny, args.ArrowSizeMm);
        var arrowStartHandle = await MechanicalProxy.DrawPolylineAsync(
            gw, arrowStart, closed: true, args.SectionLayer, ct).ConfigureAwait(false);
        var arrowEndHandle   = await MechanicalProxy.DrawPolylineAsync(
            gw, arrowEnd,   closed: true, args.SectionLayer, ct).ConfigureAwait(false);

        // Labels — placed outward from the arrow tip by ~3·arrowSize so they don't collide.
        double labelOffset = args.ArrowSizeMm * 3.0;
        var labelStartPos = new Point2dDto(args.Start.X - tx * labelOffset + nx * args.ArrowSizeMm,
                                           args.Start.Y - ty * labelOffset + ny * args.ArrowSizeMm);
        var labelEndPos   = new Point2dDto(args.End.X   + tx * labelOffset + nx * args.ArrowSizeMm,
                                           args.End.Y   + ty * labelOffset + ny * args.ArrowSizeMm);
        var labelStart = await MechanicalProxy.AddDBTextAsync(
            gw, labelStartPos, args.Label, args.LabelTextHeightMm, args.TextLayer, 0.0, alignment: null, ct).ConfigureAwait(false);
        var labelEnd   = await MechanicalProxy.AddDBTextAsync(
            gw, labelEndPos,   args.Label, args.LabelTextHeightMm, args.TextLayer, 0.0, alignment: null, ct).ConfigureAwait(false);

        return new DrawSectionCutLineResult(
            cuttingPlane, arrowStartHandle, arrowEndHandle, labelStart, labelEnd, created);
    }

    // ─────────── holes ───────────

    [McpTool("draw_through_hole",
        "Draw a plan-view through hole: profile circle on layer ME-VISIBLE (default) at the requested diameter PLUS a centreline crosshair on ME-CENTER (default) extending centerlineExtensionMm past the circle on each axis (rule 37 §4). Returns the profile circle and both centreline handles in one call.",
        "mechanical",
        Intent = new[]
        {
            "narysuj otwor przelotowy",
            "draw through hole top view",
            "otwor przelotowy z osiami",
            "through hole plan view",
            "circle with centerline crosshair"
        },
        RequiresPlugin = true)]
    public static async Task<DrawHoleResult> DrawThroughHole(
        IPluginGateway gw, DrawThroughHoleArgs args, CancellationToken ct)
    {
        if (args.DiameterMm <= 0) throw new ArgumentException("diameterMm must be > 0");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureVisibleLayerAsync(gw, existing, args.ProfileLayer, created, ct).ConfigureAwait(false);
        await EnsureCenterLayerAsync(gw, existing, args.CenterLayer, created, ct).ConfigureAwait(false);

        double r = args.DiameterMm / 2.0;
        var profile = await MechanicalProxy.DrawCircleAsync(gw, args.Center, r, args.ProfileLayer, ct).ConfigureAwait(false);
        var (h, v) = await DrawCrosshairAsync(gw, args.Center, r, args.CenterlineExtensionMm, args.CenterLayer, ct).ConfigureAwait(false);
        return new DrawHoleResult(profile, h, v, created);
    }

    [McpTool("draw_counterbore_hole",
        "Draw a plan-view counterbore hole: outer counterbore circle on layer ME-VISIBLE plus an inner through-hole circle on the same layer plus a centreline crosshair on ME-CENTER sized to the counterbore radius. counterboreDiameterMm MUST be greater than throughDiameterMm — the tool fails fast otherwise.",
        "mechanical",
        Intent = new[]
        {
            "narysuj otwor pogłebiany",
            "draw counterbore hole top view",
            "otwor z pogłebieniem walcowym",
            "counterbore plan view",
            "pocketed bolt hole"
        },
        RequiresPlugin = true)]
    public static async Task<DrawCounterboreHoleResult> DrawCounterboreHole(
        IPluginGateway gw, DrawCounterboreHoleArgs args, CancellationToken ct)
    {
        if (args.ThroughDiameterMm <= 0)     throw new ArgumentException("throughDiameterMm must be > 0");
        if (args.CounterboreDiameterMm <= 0) throw new ArgumentException("counterboreDiameterMm must be > 0");
        if (args.CounterboreDiameterMm <= args.ThroughDiameterMm)
            throw new ArgumentException("counterboreDiameterMm must be greater than throughDiameterMm");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureVisibleLayerAsync(gw, existing, args.ProfileLayer, created, ct).ConfigureAwait(false);
        await EnsureCenterLayerAsync(gw, existing, args.CenterLayer, created, ct).ConfigureAwait(false);

        double rThrough = args.ThroughDiameterMm    / 2.0;
        double rCbore   = args.CounterboreDiameterMm / 2.0;
        var through = await MechanicalProxy.DrawCircleAsync(gw, args.Center, rThrough, args.ProfileLayer, ct).ConfigureAwait(false);
        var cbore   = await MechanicalProxy.DrawCircleAsync(gw, args.Center, rCbore,   args.ProfileLayer, ct).ConfigureAwait(false);
        var (h, v)  = await DrawCrosshairAsync(gw, args.Center, rCbore, args.CenterlineExtensionMm, args.CenterLayer, ct).ConfigureAwait(false);
        return new DrawCounterboreHoleResult(through, cbore, h, v, created);
    }

    [McpTool("draw_threaded_hole",
        "Draw a plan-view threaded (tapped) hole per rule 37 §4 + §4a: a FULL outer circle at majorDiameterMm on layer ME-VISIBLE, an INNER 3/4 ARC at minorDiameterMm on layer ME-THREAD (HIDDEN linetype) — the gap demonstrates that the inner circle is the thread minor diameter, not a true geometric circle — plus a centreline crosshair on ME-CENTER. The arc gap is threadGapDeg wide (default 90°, so the arc spans 270°) starting at threadGapStartDeg (default 0° = +X axis). minorDiameterMm MUST be smaller than majorDiameterMm.",
        "mechanical",
        Intent = new[]
        {
            "narysuj otwor gwintowany",
            "draw threaded hole top view",
            "otwor z gwintem M10",
            "tapped hole plan view",
            "otwor z lukiem 3/4"
        },
        RequiresPlugin = true)]
    public static async Task<DrawThreadedHoleResult> DrawThreadedHole(
        IPluginGateway gw, DrawThreadedHoleArgs args, CancellationToken ct)
    {
        if (args.MajorDiameterMm <= 0) throw new ArgumentException("majorDiameterMm must be > 0");
        if (args.MinorDiameterMm <= 0) throw new ArgumentException("minorDiameterMm must be > 0");
        if (args.MinorDiameterMm >= args.MajorDiameterMm)
            throw new ArgumentException("minorDiameterMm must be smaller than majorDiameterMm");
        if (args.ThreadGapDeg <= 0 || args.ThreadGapDeg >= 360.0)
            throw new ArgumentException("threadGapDeg must be in (0, 360)");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureVisibleLayerAsync(gw, existing, args.ProfileLayer, created, ct).ConfigureAwait(false);
        await EnsureLayerExactAsync(gw, existing, args.ThreadLayer, MechanicalPalette.LayerThread, created, ct).ConfigureAwait(false);
        await EnsureCenterLayerAsync(gw, existing, args.CenterLayer, created, ct).ConfigureAwait(false);

        double rMajor = args.MajorDiameterMm / 2.0;
        double rMinor = args.MinorDiameterMm / 2.0;

        var major = await MechanicalProxy.DrawCircleAsync(gw, args.Center, rMajor, args.ProfileLayer, ct).ConfigureAwait(false);

        // Arc spans (360° - threadGapDeg), starting AFTER the gap.
        double startDeg = args.ThreadGapStartDeg + args.ThreadGapDeg;
        double endDeg   = args.ThreadGapStartDeg + 360.0;
        double spanDeg  = 360.0 - args.ThreadGapDeg;
        var minorArc = await MechanicalProxy.DrawArcAsync(
            gw, args.Center, rMinor, startDeg, endDeg, args.ThreadLayer, ct).ConfigureAwait(false);

        var (h, v) = await DrawCrosshairAsync(
            gw, args.Center, rMajor, args.CenterlineExtensionMm, args.CenterLayer, ct).ConfigureAwait(false);

        return new DrawThreadedHoleResult(major, minorArc, h, v, spanDeg, created);
    }

    [McpTool("draw_hole_side_view",
        "Draw a hole's SIDE view (vertical cross-section through the hole axis) -- the plan-view hole tools (draw_through_hole etc.) only draw the top-down circle; this is the companion detail/section view. kind='through': two parallel wall lines, open at both ends (a through hole has no bottom). kind='blind': walls stepping down to a drill-point V at drillPointAngleDeg (118° standard). kind='counterbore': wider walls for counterboreDepthMm then narrower walls to depthMm (requires counterboreDiameterMm + counterboreDepthMm). kind='countersink': an angled flare from headDiameterMm down to diameterMm over countersinkAngleDeg (requires headDiameterMm), then straight walls to depthMm. Y runs downward from topCenter (the top surface) into the material. A centreline is always drawn on ME-CENTER (default) extending centerlineExtensionMm past both ends.",
        "mechanical",
        Intent = new[]
        {
            "widok boczny otworu",
            "draw hole side view section",
            "przekroj otworu z gwintem",
            "blind hole side view drill point",
            "countersink side view flare"
        },
        RequiresPlugin = true)]
    public static async Task<DrawHoleSideViewResult> DrawHoleSideView(
        IPluginGateway gw, DrawHoleSideViewArgs args, CancellationToken ct)
    {
        if (args.DiameterMm <= 0) throw new ArgumentException("diameterMm must be > 0");
        if (args.DepthMm <= 0)    throw new ArgumentException("depthMm must be > 0");
        string kind = args.Kind?.ToLowerInvariant() ?? throw new ArgumentException("kind must not be empty");
        if (kind is not ("through" or "blind" or "counterbore" or "countersink"))
            throw new ArgumentException("kind must be one of: through, blind, counterbore, countersink");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureVisibleLayerAsync(gw, existing, args.ProfileLayer, created, ct).ConfigureAwait(false);
        await EnsureCenterLayerAsync(gw, existing, args.CenterLayer, created, ct).ConfigureAwait(false);

        double r = args.DiameterMm / 2.0;
        double x0 = args.TopCenter.X, yTop = args.TopCenter.Y;
        double yBottom = yTop - args.DepthMm;
        var profile = new List<EntityHandle>();

        switch (kind)
        {
            case "through":
            {
                profile.Add(await MechanicalProxy.DrawLineAsync(gw,
                    new Point2dDto(x0 - r, yTop), new Point2dDto(x0 - r, yBottom), args.ProfileLayer, ct).ConfigureAwait(false));
                profile.Add(await MechanicalProxy.DrawLineAsync(gw,
                    new Point2dDto(x0 + r, yTop), new Point2dDto(x0 + r, yBottom), args.ProfileLayer, ct).ConfigureAwait(false));
                break;
            }
            case "blind":
            {
                if (args.DrillPointAngleDeg <= 0 || args.DrillPointAngleDeg >= 180)
                    throw new ArgumentException("drillPointAngleDeg must be in (0, 180)");
                double halfAngleRad = args.DrillPointAngleDeg * Math.PI / 360.0;
                double pointDepth = r / Math.Tan(halfAngleRad);
                double yWallBottom = yBottom + pointDepth;
                var tip = new Point2dDto(x0, yBottom);
                profile.Add(await MechanicalProxy.DrawPolylineAsync(gw,
                    new[] { new Point2dDto(x0 - r, yTop), new Point2dDto(x0 - r, yWallBottom), tip },
                    closed: false, args.ProfileLayer, ct).ConfigureAwait(false));
                profile.Add(await MechanicalProxy.DrawPolylineAsync(gw,
                    new[] { new Point2dDto(x0 + r, yTop), new Point2dDto(x0 + r, yWallBottom), tip },
                    closed: false, args.ProfileLayer, ct).ConfigureAwait(false));
                break;
            }
            case "counterbore":
            {
                double cboreD = args.CounterboreDiameterMm ?? throw new ArgumentException("counterboreDiameterMm is required for kind=counterbore");
                double cboreDepth = args.CounterboreDepthMm ?? throw new ArgumentException("counterboreDepthMm is required for kind=counterbore");
                if (cboreD <= args.DiameterMm) throw new ArgumentException("counterboreDiameterMm must be greater than diameterMm");
                if (cboreDepth <= 0 || cboreDepth >= args.DepthMm) throw new ArgumentException("counterboreDepthMm must be > 0 and less than depthMm");
                double rCbore = cboreD / 2.0;
                double yStep = yTop - cboreDepth;
                profile.Add(await MechanicalProxy.DrawPolylineAsync(gw,
                    new[] { new Point2dDto(x0 - rCbore, yTop), new Point2dDto(x0 - rCbore, yStep), new Point2dDto(x0 - r, yStep), new Point2dDto(x0 - r, yBottom) },
                    closed: false, args.ProfileLayer, ct).ConfigureAwait(false));
                profile.Add(await MechanicalProxy.DrawPolylineAsync(gw,
                    new[] { new Point2dDto(x0 + rCbore, yTop), new Point2dDto(x0 + rCbore, yStep), new Point2dDto(x0 + r, yStep), new Point2dDto(x0 + r, yBottom) },
                    closed: false, args.ProfileLayer, ct).ConfigureAwait(false));
                break;
            }
            case "countersink":
            {
                double headD = args.HeadDiameterMm ?? throw new ArgumentException("headDiameterMm is required for kind=countersink");
                if (headD <= args.DiameterMm) throw new ArgumentException("headDiameterMm must be greater than diameterMm");
                if (args.CountersinkAngleDeg <= 0 || args.CountersinkAngleDeg >= 180)
                    throw new ArgumentException("countersinkAngleDeg must be in (0, 180)");
                double rHead = headD / 2.0;
                double halfAngleRad = args.CountersinkAngleDeg * Math.PI / 360.0;
                double csinkDepth = (rHead - r) / Math.Tan(halfAngleRad);
                if (csinkDepth >= args.DepthMm) throw new ArgumentException("countersink depth (derived from headDiameterMm/countersinkAngleDeg) must be less than depthMm");
                double yFlareEnd = yTop - csinkDepth;
                profile.Add(await MechanicalProxy.DrawPolylineAsync(gw,
                    new[] { new Point2dDto(x0 - rHead, yTop), new Point2dDto(x0 - r, yFlareEnd), new Point2dDto(x0 - r, yBottom) },
                    closed: false, args.ProfileLayer, ct).ConfigureAwait(false));
                profile.Add(await MechanicalProxy.DrawPolylineAsync(gw,
                    new[] { new Point2dDto(x0 + rHead, yTop), new Point2dDto(x0 + r, yFlareEnd), new Point2dDto(x0 + r, yBottom) },
                    closed: false, args.ProfileLayer, ct).ConfigureAwait(false));
                break;
            }
        }

        double ext = args.CenterlineExtensionMm;
        var centerline = await MechanicalProxy.DrawLineAsync(gw,
            new Point2dDto(x0, yTop + ext), new Point2dDto(x0, yBottom - ext), args.CenterLayer, ct).ConfigureAwait(false);

        return new DrawHoleSideViewResult(kind, profile, centerline, created);
    }

    [McpTool("draw_section_hatch",
        "Apply a material-appropriate section hatch (ISO 128 §6 / rule 37 §8 convention -- steel ANSI31, cast iron ANSI32, aluminium ANSI37, etc., see mechanical_health.materials) over an existing closed boundary. This is the tool the header comment on this file used to say didn't exist in v1 -- it now looks up pattern/scale/angle from the same material table mechanical_health reports, so an agent doesn't have to hardcode hatch parameters per material. scaleOverride/angleOverrideDeg let you deviate from the table default for an unusual drawing scale.",
        "mechanical",
        Intent = new[]
        {
            "zakreskuj przekroj stali",
            "draw section hatch steel",
            "przekroj z materialem",
            "hatch section by material",
            "cross-section fill pattern"
        },
        RequiresPlugin = true)]
    public static async Task<DrawSectionHatchResult> DrawSectionHatch(
        IPluginGateway gw, DrawSectionHatchArgs args, CancellationToken ct)
    {
        if (args.BoundaryHandles is null || args.BoundaryHandles.Count == 0)
            throw new ArgumentException("boundaryHandles must contain at least one entity handle");
        if (!MechanicalPatterns.ByMaterial.TryGetValue(args.Material, out var spec))
            throw new ArgumentException(
                $"unknown material '{args.Material}'. Known materials: {string.Join(", ", MechanicalPatterns.ByMaterial.Keys)}");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, MechanicalPalette.LayerHatch, created, ct).ConfigureAwait(false);

        double scale = args.ScaleOverride ?? spec.Scale;
        double angle = args.AngleOverrideDeg ?? spec.AngleDeg;
        var hatch = await MechanicalProxy.DrawHatchAsync(gw, args.BoundaryHandles, spec.Pattern, scale, angle, args.Layer, ct).ConfigureAwait(false);

        return new DrawSectionHatchResult(hatch, args.Material, spec.Pattern, scale, angle, created);
    }

    // ─────────── fasteners ───────────

    [McpTool("draw_bolt_head_top_view",
        "Draw the top view of a hex-head bolt per rule 37 §5: a regular hexagon with two flats parallel to the X axis (rotated by rotationDeg) sized by flatToFlatMm, optionally a Continuous shank circle inside (rotation matters for the hexagon, not the circle), and a centreline crosshair on ME-CENTER sized to the across-corners radius. Pass nominalDiameterMm for documentation only — it's echoed back in the result for traceability but does not affect geometry. The across-corners diameter is reported.",
        "mechanical",
        Intent = new[]
        {
            "narysuj lecb sruby od gory",
            "draw bolt hex head top view",
            "sruba M10 widok z gory",
            "hex head bolt plan view",
            "szesciokat plaski dla sruby"
        },
        RequiresPlugin = true)]
    public static async Task<DrawBoltHeadTopViewResult> DrawBoltHeadTopView(
        IPluginGateway gw, DrawBoltHeadTopViewArgs args, CancellationToken ct)
    {
        if (args.FlatToFlatMm <= 0) throw new ArgumentException("flatToFlatMm must be > 0");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureVisibleLayerAsync(gw, existing, args.ProfileLayer, created, ct).ConfigureAwait(false);
        await EnsureCenterLayerAsync(gw, existing, args.CenterLayer, created, ct).ConfigureAwait(false);

        // Across corners = flat-to-flat / cos(30°), so half-corner radius = flatToFlat / sqrt(3).
        double halfFlat = args.FlatToFlatMm / 2.0;
        double rCorners = halfFlat / Math.Cos(Math.PI / 6.0);
        double acrossCorners = rCorners * 2.0;

        // Hexagon vertices: 6 points at 30°, 90°, 150°, 210°, 270°, 330° (so two flats are horizontal).
        double rad0 = (args.RotationDeg + 30.0) * Math.PI / 180.0;
        var verts = new Point2dDto[6];
        for (int i = 0; i < 6; i++)
        {
            double a = rad0 + i * Math.PI / 3.0;
            verts[i] = new Point2dDto(args.Center.X + rCorners * Math.Cos(a),
                                      args.Center.Y + rCorners * Math.Sin(a));
        }
        var hexagon = await MechanicalProxy.DrawPolylineAsync(gw, verts, closed: true, args.ProfileLayer, ct).ConfigureAwait(false);

        EntityHandle? shank = null;
        if (args.IncludeShankCircle)
        {
            // Shank circle is inscribed into the hexagon = flat-to-flat / 2 radius (the inscribed circle).
            shank = await MechanicalProxy.DrawCircleAsync(gw, args.Center, halfFlat, args.ProfileLayer, ct).ConfigureAwait(false);
        }

        var (h, v) = await DrawCrosshairAsync(
            gw, args.Center, rCorners, args.CenterlineExtensionMm, args.CenterLayer, ct).ConfigureAwait(false);

        return new DrawBoltHeadTopViewResult(hexagon, shank, h, v, acrossCorners, created);
    }

    // ─────────── revisions ───────────

    [McpTool("draw_revision_triangle",
        "Draw the canonical revision marker per rule 37 §6: a filled equilateral triangle (closed polyline + SOLID hatch) on layer ME-REV with the revision letter or number drawn as DBText centred on the triangle. Returns BOTH the triangle handle and the text handle so the agent can later move them together. The triangle pointer sits at `position`; rotationDeg orients its tip (default 0° = pointing UP).",
        "mechanical",
        Intent = new[]
        {
            "wstaw trojkat rewizji",
            "draw revision triangle",
            "rewizja nr 1",
            "revision marker triangle",
            "tag revision A"
        },
        RequiresPlugin = true)]
    public static async Task<DrawRevisionTriangleResult> DrawRevisionTriangle(
        IPluginGateway gw, DrawRevisionTriangleArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Revision)) throw new ArgumentException("revision must not be empty");
        if (args.SideMm <= 0) throw new ArgumentException("sideMm must be > 0");

        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.TriangleLayer, MechanicalPalette.LayerRev, created, ct).ConfigureAwait(false);
        if (!string.Equals(args.TextLayer, args.TriangleLayer, StringComparison.OrdinalIgnoreCase))
            await EnsureLayerExactAsync(gw, existing, args.TextLayer, MechanicalPalette.LayerRev, created, ct).ConfigureAwait(false);

        // Equilateral triangle around `position` — circumradius = side / sqrt(3).
        double circ = args.SideMm / Math.Sqrt(3.0);
        double rad0 = (args.RotationDeg + 90.0) * Math.PI / 180.0;
        var verts = new Point2dDto[3];
        for (int i = 0; i < 3; i++)
        {
            double a = rad0 + i * 2.0 * Math.PI / 3.0;
            verts[i] = new Point2dDto(args.Position.X + circ * Math.Cos(a),
                                      args.Position.Y + circ * Math.Sin(a));
        }
        var triangle = await MechanicalProxy.DrawPolylineAsync(
            gw, verts, closed: true, args.TriangleLayer, ct).ConfigureAwait(false);

        // Fill it. Skip silently on hatch failure — the marker is still readable as outline.
        try
        {
            await MechanicalProxy.DrawHatchAsync(
                gw, new[] { triangle.Handle }, "SOLID", 1.0, 0.0, args.TriangleLayer, ct).ConfigureAwait(false);
        }
        catch
        {
            // SOLID hatch can fail on non-planar boundaries on some AutoCAD configs — we still
            // return the triangle outline + text. Caller can hatch manually.
        }

        // Centre-aligned text. AutoCAD's "Middle" alignment text uses position as its centre.
        var text = await MechanicalProxy.AddDBTextAsync(
            gw, args.Position, args.Revision, args.TextHeightMm, args.TextLayer,
            args.RotationDeg, alignment: "Middle", ct).ConfigureAwait(false);

        return new DrawRevisionTriangleResult(triangle, text, created);
    }

    // ─────────── introspection ───────────

    [McpTool("mechanical_health",
        "Report the ISO-mechanical layer key, the material → hatch pattern lookup table (rule 37 §8), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which pattern to pass to acad-geometry2d.draw_hatch when sectioning steel — without making a real call to AutoCAD.",
        "mechanical",
        Intent = new[]
        {
            "co potrafi mechanical",
            "list mechanical layer key",
            "mechanical material hatch lookup",
            "diagnostyka kategorii mechanical",
            "mechanical category metadata"
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static MechanicalHealthResult MechanicalHealth(MechanicalHealthArgs _)
    {
        var layers = MechanicalPalette.All
            .Select(s => new MechanicalLayerSpec(s.Name, s.AciColor, s.Linetype, s.LineweightMm, s.Plottable, s.Purpose))
            .ToList();
        var materials = MechanicalPatterns.ByMaterial
            .Select(kv => new MechanicalMaterialSpec(kv.Key, kv.Value.Pattern, kv.Value.Scale, kv.Value.AngleDeg))
            .ToList();
        return new MechanicalHealthResult(layers, materials, MechanicalPalette.PlannedBlocks, "mechanical", "0.1.0");
    }

    // ─────────── private helpers ───────────

    private static async Task<EdgeLineResult> DrawTypedEdge(
        IPluginGateway gw,
        Point2dDto start, Point2dDto end,
        string requestedLayer, string defaultLayer,
        CancellationToken ct)
    {
        var existing = await MechanicalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        await EnsureLayerExactAsync(gw, existing, requestedLayer, defaultLayer, created, ct).ConfigureAwait(false);
        var entity = await MechanicalProxy.DrawLineAsync(gw, start, end, requestedLayer, ct).ConfigureAwait(false);
        return new EdgeLineResult(entity, created);
    }

    private static async Task<(EntityHandle h, EntityHandle v)> DrawCrosshairAsync(
        IPluginGateway gw,
        Point2dDto center,
        double featureRadiusMm,
        double extensionMm,
        string layer,
        CancellationToken ct)
    {
        double r = featureRadiusMm + extensionMm;
        var hStart = new Point2dDto(center.X - r, center.Y);
        var hEnd   = new Point2dDto(center.X + r, center.Y);
        var vStart = new Point2dDto(center.X, center.Y - r);
        var vEnd   = new Point2dDto(center.X, center.Y + r);
        var h = await MechanicalProxy.DrawLineAsync(gw, hStart, hEnd, layer, ct).ConfigureAwait(false);
        var v = await MechanicalProxy.DrawLineAsync(gw, vStart, vEnd, layer, ct).ConfigureAwait(false);
        return (h, v);
    }

    /// <summary>Compute a 3-vertex closed polyline approximating an arrow-head triangle.</summary>
    private static Point2dDto[] ArrowAt(
        Point2dDto tip, double tx, double ty, double nx, double ny, double sizeMm)
    {
        // Tip + two base vertices offset BACKWARDS along the tangent and ±half-width along the normal.
        double half = sizeMm * 0.4;
        var b1 = new Point2dDto(tip.X + tx * sizeMm + nx * half, tip.Y + ty * sizeMm + ny * half);
        var b2 = new Point2dDto(tip.X + tx * sizeMm - nx * half, tip.Y + ty * sizeMm - ny * half);
        return new[] { tip, b1, b2 };
    }

    private static Task EnsureVisibleLayerAsync(
        IPluginGateway gw, HashSet<string> existing, string requested, List<string> createdSink, CancellationToken ct)
        => EnsureLayerExactAsync(gw, existing, requested, MechanicalPalette.LayerVisible, createdSink, ct);

    private static Task EnsureCenterLayerAsync(
        IPluginGateway gw, HashSet<string> existing, string requested, List<string> createdSink, CancellationToken ct)
        => EnsureLayerExactAsync(gw, existing, requested, MechanicalPalette.LayerCenter, createdSink, ct);

    private static Task EnsureSectionLayerAsync(
        IPluginGateway gw, HashSet<string> existing, string requested, List<string> createdSink, CancellationToken ct)
        => EnsureLayerExactAsync(gw, existing, requested, MechanicalPalette.LayerSection, createdSink, ct);

    private static Task EnsureTextLayerAsync(
        IPluginGateway gw, HashSet<string> existing, string requested, List<string> createdSink, CancellationToken ct)
        => EnsureLayerExactAsync(gw, existing, requested, MechanicalPalette.LayerText, createdSink, ct);

    /// <summary>Ensure the layer exists with the metadata of the canonical default
    /// (so an agent passing a custom layer name still gets the right colour/linetype/lineweight).</summary>
    private static async Task EnsureLayerExactAsync(
        IPluginGateway gw,
        HashSet<string> existing,
        string requested,
        string defaultName,
        List<string> createdSink,
        CancellationToken ct)
    {
        var spec = MechanicalPalette.All.FirstOrDefault(
            s => string.Equals(s.Name, defaultName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown default layer '{defaultName}'");

        var created = await MechanicalProxy.EnsureLayerAsync(
            gw, existing, requested, spec.AciColor, spec.Linetype, spec.LineweightMm, spec.Plottable, ct).ConfigureAwait(false);
        if (created) createdSink.Add(requested);
    }
}
