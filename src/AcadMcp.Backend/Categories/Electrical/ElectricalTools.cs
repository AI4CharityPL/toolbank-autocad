// AutoCAD acad-electrical domain category. 11 high-level
// electrical-schematic / ladder-logic tools that compose primitives from
// acad-geometry-2d, acad-layers, and acad-annotations per rule 35 §2.
// Implements rule 39 (electrical traps): IEC vs ANSI symbol style, the
// NO-vs-NC slash, junction dots, ladder rail+rung numbering, coil-to-contact
// cross-references, IEC 81346 device tags, terminal-block sequential
// numbering, and the schematic ≠ panel-layout split (panel layout deferred
// to Phase 7).
//
// v1 limitations (called out in tool descriptions):
//   * Panel-layout tools (place_din_rail, place_panel_device_outline,
//     route_wireway) ship in Phase 7.
//   * Cross-reference auto-tracking (a coil's contactRungs list propagated
//     from elsewhere in the drawing) is manual in v1; Phase 7 ships an
//     extractor.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Electrical;

public static class ElectricalTools
{
    public const string DefaultStyle = "iec";
    public const double DefaultUnitSizeMm = 5.0;

    // ─────────── infrastructure ───────────

    [McpTool("ensure_electrical_layers",
        "Idempotently create the 12-layer electrical-schematic key (E-WIRE, E-WIRE-PWR, E-WIRE-CTRL, E-SYMBOL, E-TERM, E-LBL-WIRE, E-LBL-DEV, E-LBL-RUNG, E-XREF, E-TITLE, E-PANEL, E-NOTE) per rule 39 §11 with the prescribed AutoCAD Color Index, linetype AND lineweight (e.g. E-WIRE-PWR = 0.50 mm Continuous ACI 1, E-WIRE-CTRL = 0.25 mm ACI 4, E-LBL-RUNG = 0.25 mm ACI 2). Existing layers are left alone, never overwritten. includePanel=true also creates E-PANEL for cross-sheet drawings; default false because v1 ships only the schematic side.",
        "electrical",
        Intent = new[]
        {
            "stworz wszystkie warstwy electrical",
            "ensure schematic E-* layers",
            "setup IEC electrical layer key",
            "wlacz standardowe warstwy E-* w projekcie",
            "create electrical schematic layer standard"
        },
        RequiresPlugin = true)]
    public static async Task<EnsureElectricalLayersResult> EnsureElectricalLayers(
        IPluginGateway gw, EnsureElectricalLayersArgs args, CancellationToken ct)
    {
        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var outcomes = new List<LayerEnsureOutcome>();
        int created = 0, already = 0;

        foreach (var spec in ElectricalPalette.All)
        {
            if (!args.IncludePanel && spec.Name == ElectricalPalette.LayerPanel) continue;
            try
            {
                var didCreate = await ElectricalProxy.EnsureLayerAsync(
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
        return new EnsureElectricalLayersResult(outcomes, created, already);
    }

    // ─────────── ladder ───────────

    [McpTool("draw_ladder_rails",
        "Draw the two vertical power rails of a ladder diagram on layer E-WIRE-PWR (default, ACI 1, 0.50 mm Continuous), spaced widthMm apart and heightMm tall starting from topLeft. Place rail labels (default 'L1' and 'N' per the Polish/IEC convention, rule 39 §9) above each rail on layer E-LBL-WIRE. Returns both rail handles and both label handles. To draw the rungs, follow with `draw_ladder_rung` calls; the rails do NOT auto-create rungs.",
        "electrical",
        Intent = new[]
        {
            "narysuj szyny ladder L1 N",
            "draw ladder rails L1 N",
            "szyny zasilajace w schemacie",
            "ladder diagram power rails",
            "vertical schematic rails"
        },
        RequiresPlugin = true)]
    public static async Task<DrawLadderRailsResult> DrawLadderRails(
        IPluginGateway gw, DrawLadderRailsArgs args, CancellationToken ct)
    {
        if (args.WidthMm  <= 0) throw new ArgumentException("widthMm must be > 0");
        if (args.HeightMm <= 0) throw new ArgumentException("heightMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.RailLayer,  ElectricalPalette.LayerWirePwr,  created, ct).ConfigureAwait(false);
        await EnsureLayerExactAsync(gw, existing, args.LabelLayer, ElectricalPalette.LayerLblWire, created, ct).ConfigureAwait(false);

        double leftX  = args.TopLeft.X;
        double rightX = args.TopLeft.X + args.WidthMm;
        double topY   = args.TopLeft.Y;
        double botY   = args.TopLeft.Y - args.HeightMm;

        var leftRail  = await ElectricalProxy.DrawLineAsync(gw, new(leftX,  topY), new(leftX,  botY), args.RailLayer, ct).ConfigureAwait(false);
        var rightRail = await ElectricalProxy.DrawLineAsync(gw, new(rightX, topY), new(rightX, botY), args.RailLayer, ct).ConfigureAwait(false);

        double labelY = topY + args.TextHeightMm * 0.7;
        var leftLbl  = await ElectricalProxy.AddDBTextAsync(
            gw, new(leftX,  labelY), args.LeftRailLabel,  args.TextHeightMm, args.LabelLayer, 0.0, "Middle", ct).ConfigureAwait(false);
        var rightLbl = await ElectricalProxy.AddDBTextAsync(
            gw, new(rightX, labelY), args.RightRailLabel, args.TextHeightMm, args.LabelLayer, 0.0, "Middle", ct).ConfigureAwait(false);

        return new DrawLadderRailsResult(leftRail, rightRail, leftLbl, rightLbl, created);
    }

    [McpTool("draw_ladder_rung",
        "Draw one horizontal rung between the left and right ladder rails at vertical position y on layer E-WIRE (default), and place its rung-number label (rungNumber) on the LEFT side at offset labelOffsetMm to the left of the left rail on layer E-LBL-RUNG. Per rule 39 §4 rung numbers go on the LEFT rail and are sequential. Place the contacts to the LEFT of the coil on the rung; the coil itself sits at the RIGHT end (use place_coil for that). The rung is just the conductor — devices are added separately.",
        "electrical",
        Intent = new[]
        {
            "narysuj rung 5 schematu",
            "draw ladder rung with number",
            "szczebel drabiny z numerem",
            "ladder rung",
            "horizontal control wire between rails"
        },
        RequiresPlugin = true)]
    public static async Task<DrawLadderRungResult> DrawLadderRung(
        IPluginGateway gw, DrawLadderRungArgs args, CancellationToken ct)
    {
        if (args.RightRailX <= args.LeftRailX) throw new ArgumentException("rightRailX must be > leftRailX");
        if (args.RungNumber <= 0)              throw new ArgumentException("rungNumber must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.RungLayer,  ElectricalPalette.LayerWire,    created, ct).ConfigureAwait(false);
        await EnsureLayerExactAsync(gw, existing, args.LabelLayer, ElectricalPalette.LayerLblRung, created, ct).ConfigureAwait(false);

        var rung = await ElectricalProxy.DrawLineAsync(
            gw, new(args.LeftRailX, args.Y), new(args.RightRailX, args.Y), args.RungLayer, ct).ConfigureAwait(false);

        var labelPos = new Point2dDto(args.LeftRailX - args.LabelOffsetMm, args.Y);
        var rungLabel = await ElectricalProxy.AddDBTextAsync(
            gw, labelPos, args.RungNumber.ToString(CultureInfo.InvariantCulture),
            args.TextHeightMm, args.LabelLayer, 0.0, "Middle", ct).ConfigureAwait(false);

        return new DrawLadderRungResult(rung, rungLabel, created);
    }

    // ─────────── wires ───────────

    [McpTool("draw_wire",
        "Draw a wire (poly-line) between symbol terminals or rung devices. Routes to the right layer per `kind`: 'signal' → E-WIRE (default, ACI 7, 0.30 mm), 'power' → E-WIRE-PWR (ACI 1, 0.50 mm), 'control' → E-WIRE-CTRL (ACI 4, 0.25 mm). Pass `layer` directly to override the routing. Per rule 39 §7 wires MUST connect at SYMBOL TERMINALS (use the `terminals` returned by place_resistor / place_contact_* / place_coil / place_terminal_block); a wire drawn to 'wherever the symbol body happens to start' breaks netlist extraction.",
        "electrical",
        Intent = new[]
        {
            "narysuj przewod",
            "draw wire signal control power",
            "polacz styk z cewka",
            "schematic wire between terminals",
            "control wiring"
        },
        RequiresPlugin = true)]
    public static async Task<DrawWireResult> DrawWire(
        IPluginGateway gw, DrawWireArgs args, CancellationToken ct)
    {
        if (args.Vertices == null || args.Vertices.Count < 2)
            throw new ArgumentException("wire needs at least 2 vertices");

        string layer = args.Layer ?? args.Kind?.ToLowerInvariant() switch
        {
            "power"   => ElectricalPalette.LayerWirePwr,
            "control" => ElectricalPalette.LayerWireCtrl,
            "signal" or null or "" => ElectricalPalette.LayerWire,
            _ => throw new ArgumentException($"unknown wire kind '{args.Kind}'"),
        };

        // Default-layer used to seed the EnsureLayerAsync metadata: same as `layer`
        // when `layer` is one of the canonical names; otherwise default to E-WIRE.
        string defaultLayer = ElectricalPalette.All.Any(s => string.Equals(s.Name, layer, StringComparison.OrdinalIgnoreCase))
            ? layer : ElectricalPalette.LayerWire;

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, layer, defaultLayer, created, ct).ConfigureAwait(false);

        var wire = await ElectricalProxy.DrawPolylineAsync(gw, args.Vertices, closed: false, layer, ct).ConfigureAwait(false);
        return new DrawWireResult(wire, layer, created);
    }

    [McpTool("draw_wire_junction",
        "Draw a filled junction dot at a wire intersection — the visual marker that the two wires ARE electrically connected (rule 39 §3). Without a dot, two crossing wires are conventionally NOT connected. Implementation: a small filled Circle on layer E-WIRE (default) — agents who skip this on T or + intersections produce ambiguous schematics that the inspector flags.",
        "electrical",
        Intent = new[]
        {
            "kropka polaczenia przewodow",
            "draw wire junction dot",
            "punkt polaczenia",
            "schematic wire junction marker",
            "T joint dot"
        },
        RequiresPlugin = true)]
    public static async Task<DrawWireJunctionResult> DrawWireJunction(
        IPluginGateway gw, DrawWireJunctionArgs args, CancellationToken ct)
    {
        if (args.DotRadiusMm <= 0) throw new ArgumentException("dotRadiusMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerWire, created, ct).ConfigureAwait(false);

        var dot = await ElectricalProxy.DrawCircleAsync(gw, args.Position, args.DotRadiusMm, args.Layer, ct).ConfigureAwait(false);
        // SOLID hatch on the circle — ignore failure (the open dot still reads as a junction).
        try
        {
            await ElectricalProxy.DrawHatchAsync(
                gw, new[] { dot.Handle }, "SOLID", 1.0, 0.0, args.Layer, ct).ConfigureAwait(false);
        }
        catch { /* unfilled junction dot is still legible */ }
        return new DrawWireJunctionResult(dot, created);
    }

    // ─────────── symbols ───────────

    [McpTool("place_resistor",
        "Place a resistor symbol at `position`, rotated by rotationDeg (0° = horizontal, terminals at left/right). style='iec' (default, Polish/EU) draws a rectangle of width 4×unitSize × height 1.5×unitSize; style='ansi' draws a zig-zag of 6 zags spanning the same width. Both styles expose two terminals named '1' (left / start) and '2' (right / end) with their EXACT coordinates so subsequent draw_wire calls snap to them (rule 39 §7). Default unitSize = 5 mm (rule 39 §10).",
        "electrical",
        Intent = new[]
        {
            "wstaw rezystor IEC prostokat",
            "place resistor schematic symbol",
            "rezystor zigzag ANSI",
            "resistor symbol with terminals",
            "schematic resistor R1"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceResistorResult> PlaceResistor(
        IPluginGateway gw, PlaceResistorArgs args, CancellationToken ct)
    {
        if (args.UnitSizeMm <= 0) throw new ArgumentException("unitSizeMm must be > 0");
        var style = NormaliseStyle(args.Style);

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerSymbol, created, ct).ConfigureAwait(false);

        double widthMm  = 4.0 * args.UnitSizeMm;
        double heightMm = 1.5 * args.UnitSizeMm;

        var rotator = MakeRotator(args.Position, args.RotationDeg);
        var body = new List<EntityHandle>();

        // Terminal lead-out lines (length = unitSize) on both sides.
        double leadLen = args.UnitSizeMm;
        double bodyHalf = widthMm / 2.0;
        var t1Local  = (-bodyHalf - leadLen, 0.0);
        var t2Local  = ( bodyHalf + leadLen, 0.0);
        var leadL = await ElectricalProxy.DrawLineAsync(gw, rotator(t1Local),                rotator((-bodyHalf, 0.0)), args.Layer, ct).ConfigureAwait(false);
        var leadR = await ElectricalProxy.DrawLineAsync(gw, rotator(( bodyHalf, 0.0)),       rotator(t2Local),          args.Layer, ct).ConfigureAwait(false);
        body.Add(leadL); body.Add(leadR);

        if (style == "iec")
        {
            var rect = new[]
            {
                rotator((-bodyHalf, -heightMm / 2.0)),
                rotator(( bodyHalf, -heightMm / 2.0)),
                rotator(( bodyHalf,  heightMm / 2.0)),
                rotator((-bodyHalf,  heightMm / 2.0)),
            };
            body.Add(await ElectricalProxy.DrawPolylineAsync(gw, rect, closed: true, args.Layer, ct).ConfigureAwait(false));
        }
        else // "ansi"
        {
            // 6-zag zig-zag fitting in the body envelope.
            int zags = 6;
            double dx = widthMm / zags;
            double amp = heightMm / 2.0;
            var pts = new List<Point2dDto>(capacity: zags + 1);
            pts.Add(rotator((-bodyHalf, 0.0)));
            for (int i = 1; i <= zags; i++)
            {
                double x = -bodyHalf + i * dx;
                double y = (i % 2 == 1) ? amp : -amp;
                if (i == zags) y = 0.0;
                pts.Add(rotator((x, y)));
            }
            body.Add(await ElectricalProxy.DrawPolylineAsync(gw, pts, closed: false, args.Layer, ct).ConfigureAwait(false));
        }

        var terminals = new[]
        {
            new Terminal("1", rotator(t1Local)),
            new Terminal("2", rotator(t2Local)),
        };
        return new PlaceResistorResult(body, terminals, style, created);
    }

    [McpTool("place_contact_no",
        "Place a Normally-Open contact symbol at `position`, rotated by rotationDeg. NO contact bridges only when its controlling coil is energised. Geometry: a horizontal bottom terminal line plus a short angled lever pointing up-and-away from the right terminal — NO horizontal slash (rule 39 §2: the slash is what distinguishes NC from NO). Exposes terminals 'in' (left) and 'out' (right). For NC use the SEPARATE place_contact_nc tool — never call this with a `kind` flag.",
        "electrical",
        Intent = new[]
        {
            "wstaw styk NO normalnie otwarty",
            "place normally open contact",
            "styk zwierajacy NO",
            "NO contact schematic symbol",
            "open contact"
        },
        RequiresPlugin = true)]
    public static Task<PlaceContactResult> PlaceContactNo(
        IPluginGateway gw, PlaceContactArgs args, CancellationToken ct)
        => PlaceContactInternal(gw, args, kind: "no", ct);

    [McpTool("place_contact_nc",
        "Place a Normally-Closed contact symbol at `position`, rotated by rotationDeg. NC contact opens only when its controlling coil is energised. Geometry: identical to NO (rule 39 §2) PLUS a horizontal slash through the angled lever — the slash IS the NC marker. Exposes terminals 'in' (left) and 'out' (right). For NO use the SEPARATE place_contact_no tool.",
        "electrical",
        Intent = new[]
        {
            "wstaw styk NC normalnie zamkniety",
            "place normally closed contact",
            "styk rozwierajacy NC",
            "NC contact schematic symbol",
            "closed contact"
        },
        RequiresPlugin = true)]
    public static Task<PlaceContactResult> PlaceContactNc(
        IPluginGateway gw, PlaceContactArgs args, CancellationToken ct)
        => PlaceContactInternal(gw, args, kind: "nc", ct);

    [McpTool("place_coil",
        "Place a relay / contactor coil symbol at `position`, rotated by rotationDeg. style='iec' (default) draws an empty rectangle of width 3×unitSize × height 2×unitSize with the device tag inside; style='ansi' draws a circle of radius unitSize with the tag inside. Optional `tag` (e.g. '-K1') is placed inside the symbol on layer E-LBL-DEV. Optional `contactRungs` (a JSON array of rung numbers like 12, 14, 18) emits the cross-reference text below the coil on layer E-XREF (rule 39 §5) — agents who omit this leave maintenance hunting through the drawing for K1's contacts. Exposes terminals 'A1' (top, IEC) and 'A2' (bottom, IEC).",
        "electrical",
        Intent = new[]
        {
            "wstaw cewke K1",
            "place relay coil schematic",
            "cewka stycznika z xref",
            "coil with contact rung references",
            "contactor coil symbol"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceCoilResult> PlaceCoil(
        IPluginGateway gw, PlaceCoilArgs args, CancellationToken ct)
    {
        if (args.UnitSizeMm <= 0) throw new ArgumentException("unitSizeMm must be > 0");
        var style = NormaliseStyle(args.Style);

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer,    ElectricalPalette.LayerSymbol,  created, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(args.Tag))
            await EnsureLayerExactAsync(gw, existing, args.TagLayer, ElectricalPalette.LayerLblDev, created, ct).ConfigureAwait(false);
        if (args.ContactRungs is { Count: > 0 })
            await EnsureLayerExactAsync(gw, existing, args.XrefLayer, ElectricalPalette.LayerXref, created, ct).ConfigureAwait(false);

        var rotator = MakeRotator(args.Position, args.RotationDeg);
        var body = new List<EntityHandle>();
        Point2dDto a1Pos, a2Pos;

        if (style == "iec")
        {
            double w = 3.0 * args.UnitSizeMm;
            double h = 2.0 * args.UnitSizeMm;
            var rect = new[]
            {
                rotator((-w / 2.0, -h / 2.0)),
                rotator(( w / 2.0, -h / 2.0)),
                rotator(( w / 2.0,  h / 2.0)),
                rotator((-w / 2.0,  h / 2.0)),
            };
            body.Add(await ElectricalProxy.DrawPolylineAsync(gw, rect, closed: true, args.Layer, ct).ConfigureAwait(false));
            a1Pos = rotator((0.0,  h / 2.0));
            a2Pos = rotator((0.0, -h / 2.0));
        }
        else // ansi
        {
            double r = args.UnitSizeMm;
            body.Add(await ElectricalProxy.DrawCircleAsync(gw, args.Position, r, args.Layer, ct).ConfigureAwait(false));
            a1Pos = rotator((0.0,  r));
            a2Pos = rotator((0.0, -r));
        }

        // Lead-out stubs for the terminals.
        body.Add(await ElectricalProxy.DrawLineAsync(gw, a1Pos, rotator((0.0,  args.UnitSizeMm * (style == "iec" ? 1.6 : 1.6))), args.Layer, ct).ConfigureAwait(false));
        body.Add(await ElectricalProxy.DrawLineAsync(gw, a2Pos, rotator((0.0, -args.UnitSizeMm * (style == "iec" ? 1.6 : 1.6))), args.Layer, ct).ConfigureAwait(false));

        EntityHandle? tagText  = null;
        EntityHandle? xrefText = null;
        if (!string.IsNullOrEmpty(args.Tag))
        {
            tagText = await ElectricalProxy.AddDBTextAsync(
                gw, args.Position, args.Tag, args.TextHeightMm, args.TagLayer, 0.0, "Middle", ct).ConfigureAwait(false);
        }
        if (args.ContactRungs is { Count: > 0 })
        {
            string xrefStr = string.Join(", ", args.ContactRungs);
            var xrefPos = rotator((0.0, -args.UnitSizeMm * 2.4));
            xrefText = await ElectricalProxy.AddDBTextAsync(
                gw, xrefPos, xrefStr, args.TextHeightMm, args.XrefLayer, 0.0, "Middle", ct).ConfigureAwait(false);
        }

        var terminals = new[] { new Terminal("A1", a1Pos), new Terminal("A2", a2Pos) };
        return new PlaceCoilResult(body, terminals, tagText, xrefText, style, created);
    }

    // ─────────── terminal block ───────────

    [McpTool("place_terminal_block",
        "Place a terminal block as `count` numbered rectangles in a horizontal row starting at `origin` (top-left corner), each rectangle of width pitchMm × height heightMm, with sequential numbers (startNumber, startNumber+1, …) labelled below. Per rule 39 §11 terminals live on layer E-TERM (ACI 6, 0.40 mm) and labels on E-LBL-WIRE. Returns each slot's body handle, label handle, AND its top + bottom centre points so wires can snap to either side of the block.",
        "electrical",
        Intent = new[]
        {
            "wstaw listwe zaciskowa 8 pin",
            "place terminal block 8 way",
            "listwa zaciskowa numerowana",
            "terminal strip with numbers",
            "X1-X8 terminals"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceTerminalBlockResult> PlaceTerminalBlock(
        IPluginGateway gw, PlaceTerminalBlockArgs args, CancellationToken ct)
    {
        if (args.Count < 1)        throw new ArgumentException("count must be >= 1");
        if (args.PitchMm <= 0)     throw new ArgumentException("pitchMm must be > 0");
        if (args.HeightMm <= 0)    throw new ArgumentException("heightMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer,      ElectricalPalette.LayerTerm,    created, ct).ConfigureAwait(false);
        await EnsureLayerExactAsync(gw, existing, args.LabelLayer, ElectricalPalette.LayerLblWire, created, ct).ConfigureAwait(false);

        var slots = new List<TerminalBlockSlot>(args.Count);
        for (int i = 0; i < args.Count; i++)
        {
            double x0 = args.Origin.X + i * args.PitchMm;
            double y0 = args.Origin.Y - args.HeightMm;
            var rect = new[]
            {
                new Point2dDto(x0,                 y0),
                new Point2dDto(x0 + args.PitchMm,  y0),
                new Point2dDto(x0 + args.PitchMm,  args.Origin.Y),
                new Point2dDto(x0,                 args.Origin.Y),
            };
            var bodyEntity = await ElectricalProxy.DrawPolylineAsync(gw, rect, closed: true, args.Layer, ct).ConfigureAwait(false);

            int number = args.StartNumber + i;
            var labelPos = new Point2dDto(x0 + args.PitchMm / 2.0, y0 - args.TextHeightMm * 1.2);
            var label = await ElectricalProxy.AddDBTextAsync(
                gw, labelPos, number.ToString(CultureInfo.InvariantCulture),
                args.TextHeightMm, args.LabelLayer, 0.0, "Middle", ct).ConfigureAwait(false);

            slots.Add(new TerminalBlockSlot(
                Number: number,
                Body: bodyEntity,
                Label: label,
                TopPosition:    new Point2dDto(x0 + args.PitchMm / 2.0, args.Origin.Y),
                BottomPosition: new Point2dDto(x0 + args.PitchMm / 2.0, y0)));
        }
        return new PlaceTerminalBlockResult(slots, created);
    }

    // ─────────── panel layout ───────────
    // The three tools the header comment used to list as "ship in Phase 7":
    // DIN rail, panel device footprints, and wireway routing -- the physical
    // panel-layout side of a project, as opposed to the schematic side above.

    [McpTool("place_din_rail",
        "Draw a DIN rail (EN 50022 top-hat rail, 35 mm wide by default) as a rectangle on layer E-PANEL (default) from start, lengthMm long, at rotationDeg. Pass slotPitchMm to also draw perpendicular tick marks every slotPitchMm along the rail as a visual device-spacing reference (omit for a plain rail outline). Returns the end point so a device outline or the next rail segment can be placed flush against it.",
        "electrical",
        Intent = new[]
        {
            "wstaw szyne din",
            "place din rail panel",
            "szyna montazowa 35mm",
            "din rail layout",
            "top-hat rail outline"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceDinRailResult> PlaceDinRail(
        IPluginGateway gw, PlaceDinRailArgs args, CancellationToken ct)
    {
        if (args.LengthMm <= 0)   throw new ArgumentException("lengthMm must be > 0");
        if (args.RailWidthMm <= 0) throw new ArgumentException("railWidthMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerPanel, created, ct).ConfigureAwait(false);

        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
        double nx = -sinA, ny = cosA; // perpendicular (rail width direction)
        double halfW = args.RailWidthMm / 2.0;

        Point2dDto Along(double d) => new(args.Start.X + cosA * d, args.Start.Y + sinA * d);
        Point2dDto Offset(Point2dDto p, double s) => new(p.X + nx * s, p.Y + ny * s);

        var a = Along(0);
        var b = Along(args.LengthMm);
        var rect = new[]
        {
            Offset(a, -halfW), Offset(b, -halfW), Offset(b, halfW), Offset(a, halfW),
        };
        var outline = await ElectricalProxy.DrawPolylineAsync(gw, rect, closed: true, args.Layer, ct).ConfigureAwait(false);

        var ticks = new List<EntityHandle>();
        if (args.SlotPitchMm is > 0)
        {
            for (double d = args.SlotPitchMm.Value; d < args.LengthMm; d += args.SlotPitchMm.Value)
            {
                var p = Along(d);
                ticks.Add(await ElectricalProxy.DrawLineAsync(
                    gw, Offset(p, -halfW), Offset(p, halfW), args.Layer, ct).ConfigureAwait(false));
            }
        }

        return new PlaceDinRailResult(outline, ticks, b, created);
    }

    [McpTool("place_panel_device_outline",
        "Draw a rectangular physical device footprint (breaker, contactor, relay body, etc.) on layer E-PANEL (default) for panel-layout drawings -- the physical counterpart to the schematic symbols above (place_coil etc. draw the SCHEMATIC symbol; this draws the PHYSICAL footprint you'd mount on a DIN rail). origin is the top-left corner. Pass tag to also place a device tag label centred below the outline on E-LBL-DEV.",
        "electrical",
        Intent = new[]
        {
            "wstaw obrys urzadzenia w rozdzielni",
            "place panel device outline",
            "fizyczny obrys stycznika",
            "device footprint panel layout",
            "breaker outline on rail"
        },
        RequiresPlugin = true)]
    public static async Task<PlacePanelDeviceOutlineResult> PlacePanelDeviceOutline(
        IPluginGateway gw, PlacePanelDeviceOutlineArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0)  throw new ArgumentException("widthMm must be > 0");
        if (args.HeightMm <= 0) throw new ArgumentException("heightMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerPanel, created, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(args.Tag))
            await EnsureLayerExactAsync(gw, existing, args.TagLayer, ElectricalPalette.LayerLblDev, created, ct).ConfigureAwait(false);

        var rect = new[]
        {
            new Point2dDto(args.Origin.X,               args.Origin.Y),
            new Point2dDto(args.Origin.X + args.WidthMm, args.Origin.Y),
            new Point2dDto(args.Origin.X + args.WidthMm, args.Origin.Y - args.HeightMm),
            new Point2dDto(args.Origin.X,                args.Origin.Y - args.HeightMm),
        };
        var outline = await ElectricalProxy.DrawPolylineAsync(gw, rect, closed: true, args.Layer, ct).ConfigureAwait(false);

        EntityHandle? tagText = null;
        if (!string.IsNullOrEmpty(args.Tag))
        {
            var tagPos = new Point2dDto(args.Origin.X + args.WidthMm / 2.0, args.Origin.Y - args.HeightMm / 2.0);
            tagText = await ElectricalProxy.AddDBTextAsync(
                gw, tagPos, args.Tag, args.TextHeightMm, args.TagLayer, 0.0, "Middle", ct).ConfigureAwait(false);
        }

        return new PlacePanelDeviceOutlineResult(outline, tagText, created);
    }

    [McpTool("route_wireway",
        "Draw a wireway / trunking channel along `path` on layer E-PANEL (default) as a centreline plus two parallel edge lines offset ±widthMm/2 (mitred at interior vertices, same offset approach as acad-civil.draw_road_corridor / acad-architecture.draw_walls_chain). Use this for the physical cable-management channel between panel devices, distinct from the schematic wire routing of draw_wire.",
        "electrical",
        Intent = new[]
        {
            "narysuj koryto kablowe",
            "route wireway trunking",
            "kanal kablowy w rozdzielni",
            "cable duct panel layout",
            "wireway channel"
        },
        RequiresPlugin = true)]
    public static async Task<RouteWirewayResult> RouteWireway(
        IPluginGateway gw, RouteWirewayArgs args, CancellationToken ct)
    {
        if (args.Path is null || args.Path.Count < 2) throw new ArgumentException("path must contain at least 2 points");
        if (args.WidthMm <= 0) throw new ArgumentException("widthMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerPanel, created, ct).ConfigureAwait(false);

        double half = args.WidthMm / 2.0;
        var left = new Point2dDto[args.Path.Count];
        var right = new Point2dDto[args.Path.Count];
        for (int i = 0; i < args.Path.Count; i++)
        {
            var prev = args.Path[Math.Max(i - 1, 0)];
            var next = args.Path[Math.Min(i + 1, args.Path.Count - 1)];
            double dx = next.X - prev.X, dy = next.Y - prev.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double nx = len < 1e-9 ? 0 : -dy / len, ny = len < 1e-9 ? 0 : dx / len;
            var p = args.Path[i];
            left[i]  = new Point2dDto(p.X + nx * half, p.Y + ny * half);
            right[i] = new Point2dDto(p.X - nx * half, p.Y - ny * half);
        }

        var centerline = await ElectricalProxy.DrawPolylineAsync(gw, args.Path.ToArray(), closed: false, args.Layer, ct).ConfigureAwait(false);
        var leftEdge   = await ElectricalProxy.DrawPolylineAsync(gw, left,  closed: false, args.Layer, ct).ConfigureAwait(false);
        var rightEdge  = await ElectricalProxy.DrawPolylineAsync(gw, right, closed: false, args.Layer, ct).ConfigureAwait(false);

        return new RouteWirewayResult(centerline, leftEdge, rightEdge, args.WidthMm, created);
    }

    // ─────────── device tag ───────────

    [McpTool("place_device_tag",
        "Place an IEC 81346 device tag as DBText on layer E-LBL-DEV. Accepts the short form ('K1' / '-K1'), the location-qualified form ('+CAB1-K1') or the fully-qualified form ('=PWR+CAB1-K1') per rule 39 §6a. The PREFIX letter is validated against the IEC 81346-2 set (-K / -Q / -F / -S / -B / -M / -T / -G / -X / -W / -H per rule 39 §6) — agents who invent prefixes ('-A1' for a contactor) get a fail-fast error with the allowed list. Returns the canonical string + the prefix character + a one-line description of what that prefix means.",
        "electrical",
        Intent = new[]
        {
            "wstaw tag urzadzenia -K1",
            "place IEC 81346 device tag",
            "tag stycznika -K1",
            "device tag with prefix validation",
            "tag -F1 fuse"
        },
        RequiresPlugin = true)]
    public static async Task<PlaceDeviceTagResult> PlaceDeviceTag(
        IPluginGateway gw, PlaceDeviceTagArgs args, CancellationToken ct)
    {
        var tag = DeviceTag.Parse(args.Tag);   // throws on invalid prefix per rule 39 §6

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerLblDev, created, ct).ConfigureAwait(false);

        var text = await ElectricalProxy.AddDBTextAsync(
            gw, args.Position, tag.Canonical, args.TextHeightMm, args.Layer, args.RotationDeg, alignment: null, ct
        ).ConfigureAwait(false);

        return new PlaceDeviceTagResult(
            text, tag.Canonical, tag.Prefix,
            IecDeviceTagPrefixes.Allowed[tag.Prefix],
            created);
    }

    // ─────────── introspection ───────────

    [McpTool("electrical_health",
        "Report the 12-layer electrical-schematic key, the IEC 81346 device-tag prefix lookup table (rule 39 §6), the supported symbol styles ('iec' default, 'ansi') with the office default (5 mm unit size, rule 39 §10), and the planned bundled-block list. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults — e.g. which prefix letter to use for a contactor — without making a real call to AutoCAD.",
        "electrical",
        Intent = new[]
        {
            "co potrafi electrical",
            "list electrical layer key",
            "list IEC tag prefixes",
            "diagnostyka kategorii electrical",
            "electrical category metadata"
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static ElectricalHealthResult ElectricalHealth(ElectricalHealthArgs _)
    {
        var layers = ElectricalPalette.All
            .Select(s => new ElectricalLayerSpec(s.Name, s.AciColor, s.Linetype, s.LineweightMm, s.Plottable, s.Purpose))
            .ToList();
        var prefixes = IecDeviceTagPrefixes.Allowed
            .Select(kv => new IecPrefixSpec(kv.Key, kv.Value))
            .ToList();
        return new ElectricalHealthResult(
            layers, prefixes, new[] { "iec", "ansi" }, DefaultStyle, DefaultUnitSizeMm,
            ElectricalPalette.PlannedBlocks, "electrical", "0.1.0");
    }

    // ─────────── private helpers ───────────

    private static async Task<PlaceContactResult> PlaceContactInternal(
        IPluginGateway gw, PlaceContactArgs args, string kind, CancellationToken ct)
    {
        if (args.UnitSizeMm <= 0) throw new ArgumentException("unitSizeMm must be > 0");

        var existing = await ElectricalProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created  = new List<string>();
        await EnsureLayerExactAsync(gw, existing, args.Layer, ElectricalPalette.LayerSymbol, created, ct).ConfigureAwait(false);

        var rotator = MakeRotator(args.Position, args.RotationDeg);
        var body = new List<EntityHandle>();

        // Local geometry (horizontal contact, unrotated):
        // - left terminal stub at (-2u, 0) → (-1u, 0)
        // - lever from (-1u, 0) up to (1u, 1u)         (NO and NC use the same lever)
        // - right terminal stub at (1u, 0) → (2u, 0)
        // - NC adds a horizontal slash from (-0.4u, 0.5u) → (0.4u, 0.5u)
        double u = args.UnitSizeMm;
        var leftStubA  = (-2.0 * u, 0.0);
        var leftStubB  = (-1.0 * u, 0.0);
        var leverA     = (-1.0 * u, 0.0);
        var leverB     = ( 1.0 * u, 1.0 * u);
        var rightStubA = ( 1.0 * u, 0.0);
        var rightStubB = ( 2.0 * u, 0.0);

        body.Add(await ElectricalProxy.DrawLineAsync(gw, rotator(leftStubA),  rotator(leftStubB),  args.Layer, ct).ConfigureAwait(false));
        body.Add(await ElectricalProxy.DrawLineAsync(gw, rotator(leverA),     rotator(leverB),     args.Layer, ct).ConfigureAwait(false));
        body.Add(await ElectricalProxy.DrawLineAsync(gw, rotator(rightStubA), rotator(rightStubB), args.Layer, ct).ConfigureAwait(false));

        if (kind == "nc")
        {
            var slashA = (-0.4 * u, 0.5 * u);
            var slashB = ( 0.4 * u, 0.5 * u);
            body.Add(await ElectricalProxy.DrawLineAsync(gw, rotator(slashA), rotator(slashB), args.Layer, ct).ConfigureAwait(false));
        }

        var terminals = new[]
        {
            new Terminal("in",  rotator(leftStubA)),
            new Terminal("out", rotator(rightStubB)),
        };
        return new PlaceContactResult(body, terminals, kind, created);
    }

    /// <summary>Build a transform that rotates a local (X, Y) point by
    /// <paramref name="rotationDeg"/> CCW around the origin and translates by
    /// <paramref name="anchor"/>.</summary>
    private static System.Func<(double, double), Point2dDto> MakeRotator(Point2dDto anchor, double rotationDeg)
    {
        double rad = rotationDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);
        return p => new Point2dDto(
            anchor.X + p.Item1 * cosA - p.Item2 * sinA,
            anchor.Y + p.Item1 * sinA + p.Item2 * cosA);
    }

    private static string NormaliseStyle(string s) =>
        (s ?? DefaultStyle).Trim().ToLowerInvariant() switch
        {
            "iec" => "iec",
            "ansi" or "ieee" or "us" => "ansi",
            _ => throw new ArgumentException($"unknown symbol style '{s}' (expected 'iec' or 'ansi')"),
        };

    private static async Task EnsureLayerExactAsync(
        IPluginGateway gw,
        HashSet<string> existing,
        string requested,
        string defaultName,
        List<string> createdSink,
        CancellationToken ct)
    {
        var spec = ElectricalPalette.All.FirstOrDefault(
            s => string.Equals(s.Name, defaultName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown default layer '{defaultName}'");
        var created = await ElectricalProxy.EnsureLayerAsync(
            gw, existing, requested, spec.AciColor, spec.Linetype, spec.LineweightMm, spec.Plottable, ct).ConfigureAwait(false);
        if (created) createdSink.Add(requested);
    }
}
