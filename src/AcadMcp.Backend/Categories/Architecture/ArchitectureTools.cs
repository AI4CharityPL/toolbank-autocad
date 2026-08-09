// AutoCAD acad-architecture domain category. 10 high-level plan-view tools that
// compose primitives from acad-geometry2d, acad-layers, acad-annotations and
// acad-dimensions per rule 35 §2. Implements rule 36 (architecture domain
// traps) one trap at a time.
//
// Notes for v1:
//   * Walls = centreline (A-WALL-CTRL) + two parallel faces (A-WALL).
//     `connect_walls` (mitre/butt cleanup) is intentionally out of scope; agents
//     can chain walls via `draw_walls_chain` or call `acad-geometry2d.fillet_corner`
//     manually until Phase 7.
//   * `insert_door` and `insert_window` synthesise their geometry inline rather
//     than depending on bundled DWG block libraries (which ship in Phase 7).
//     They DO mark the host wall conceptually but DO NOT cut the wall opening
//     yet — the result.notes field calls this out explicitly so agents don't
//     ship plans assuming the cut was performed.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Architecture;

public static class ArchitectureTools
{
    // Reserved for future heavy operations (cf. draw_walls_chain on > 1k vertices).
    // No tool currently needs the slow tier; trim once Phase-7 cleanup tools land.
    private const int T_NORMAL = 15_000;

    // ─────────── infrastructure ───────────

    [McpTool("ensure_architectural_layers",
        "Idempotently create the AIA-style architectural + structural layer key (A-WALL, A-WALL-CTRL, A-DOOR, A-DOOR-SWING, A-GLAZ, A-ROOM-BNDY, A-ROOM-IDEN, A-CLNG, A-ROOF, A-STRS, A-ANNO-DIMS, A-ANNO-NOTE, plus structural S-COLS, S-COLS-CTRL, S-SLAB, S-SLAB-HATCH when includeStructural=true). Existing layers are left alone, never overwritten. Returns one outcome per layer (created | already_exists | failed).",
        "architecture",
        Intent = new[]
        {
            "stworz wszystkie warstwy architektoniczne",
            "ensure architectural layers",
            "setup AIA layer standard",
            "wlacz standardowe warstwy A-* w projekcie",
            "create plan-view layer key"
        },
        RequiresPlugin = true)]
    public static async Task<EnsureArchitecturalLayersResult> EnsureArchitecturalLayers(
        IPluginGateway gw, EnsureArchitecturalLayersArgs args, CancellationToken ct)
    {
        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var outcomes = new List<LayerEnsureOutcome>();
        int created = 0, already = 0;
        foreach (var spec in ArchitecturePalette.All)
        {
            if (!args.IncludeStructural && spec.Structural) continue;
            try
            {
                var didCreate = await ArchitectureProxy.EnsureLayerAsync(
                    gw, existing, spec.Name, spec.AciColor, spec.Linetype, ct).ConfigureAwait(false);
                if (didCreate) created++; else already++;
                outcomes.Add(new LayerEnsureOutcome(
                    spec.Name, didCreate ? "created" : "already_exists", spec.AciColor, spec.Linetype));
            }
            catch (Exception ex)
            {
                outcomes.Add(new LayerEnsureOutcome(spec.Name, "failed", spec.AciColor, spec.Linetype, ex.Message));
            }
        }
        return new EnsureArchitecturalLayersResult(outcomes, created, already);
    }

    // ─────────── walls ───────────

    [McpTool("draw_wall",
        "Draw one straight wall segment as a centreline on A-WALL-CTRL plus two parallel face polylines (offset ±thickness/2) on A-WALL. Returns all three entity handles plus the segment length and the list of layers auto-created on demand. Wall ends are square (perpendicular cap) by default — connect mitres with acad-geometry2d.fillet_corner or use draw_walls_chain for connected runs.",
        "architecture",
        Intent = new[]
        {
            "narysuj sciane",
            "postaw sciane od A do B",
            "draw wall from point to point",
            "wall with thickness",
            "200 mm wall"
        },
        RequiresPlugin = true)]
    public static async Task<DrawWallResult> DrawWall(IPluginGateway gw, DrawWallArgs args, CancellationToken ct)
    {
        if (args.ThicknessMm <= 0)
            throw new ArgumentException("thicknessMm must be > 0");
        var dx = args.End.X - args.Start.X;
        var dy = args.End.Y - args.Start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6)
            throw new ArgumentException("wall start and end coincide");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.CenterlineLayer, 8, "CENTER", ct).ConfigureAwait(false))
            created.Add(args.CenterlineLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.FaceLayer, 7, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.FaceLayer);

        // unit normal perpendicular to the wall direction
        double nx = -dy / len;
        double ny =  dx / len;
        double half = args.ThicknessMm / 2.0;
        var leftStart  = new Point2dDto(args.Start.X + nx * half, args.Start.Y + ny * half);
        var leftEnd    = new Point2dDto(args.End.X   + nx * half, args.End.Y   + ny * half);
        var rightStart = new Point2dDto(args.Start.X - nx * half, args.Start.Y - ny * half);
        var rightEnd   = new Point2dDto(args.End.X   - nx * half, args.End.Y   - ny * half);

        var centreline = await ArchitectureProxy.DrawLineAsync(gw, args.Start, args.End, args.CenterlineLayer, ct).ConfigureAwait(false);
        var leftFace   = await ArchitectureProxy.DrawLineAsync(gw, leftStart,  leftEnd,  args.FaceLayer, ct).ConfigureAwait(false);
        var rightFace  = await ArchitectureProxy.DrawLineAsync(gw, rightStart, rightEnd, args.FaceLayer, ct).ConfigureAwait(false);

        return new DrawWallResult(centreline, leftFace, rightFace, len, args.ThicknessMm, created);
    }

    [McpTool("draw_walls_chain",
        "Draw a continuous run of walls from a list of vertices in one call. Generates a single centreline polyline on A-WALL-CTRL and two offset face polylines on A-WALL (built by stitching together the perpendicular offsets at each vertex — joints are mitred at the angle bisector). Set closed=true to close the run back to the first vertex (e.g. for a room outline). MUCH cheaper than draw_wall × N because it issues 3 polyline calls instead of 3·N line calls.",
        "architecture",
        Intent = new[]
        {
            "narysuj kilka scian po kolei",
            "lancuch scian z polilinii",
            "draw connected walls polyline",
            "wall chain with mitred corners",
            "closed wall loop room outline"
        },
        RequiresPlugin = true)]
    public static async Task<DrawWallsChainResult> DrawWallsChain(
        IPluginGateway gw, DrawWallsChainArgs args, CancellationToken ct)
    {
        if (args.Vertices is null || args.Vertices.Count < 2)
            throw new ArgumentException("draw_walls_chain needs >= 2 vertices");
        if (args.ThicknessMm <= 0)
            throw new ArgumentException("thicknessMm must be > 0");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.CenterlineLayer, 8, "CENTER", ct).ConfigureAwait(false))
            created.Add(args.CenterlineLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.FaceLayer, 7, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.FaceLayer);

        var (leftFaceVerts, rightFaceVerts, totalLen) = ComputeOffsetFaces(args.Vertices, args.ThicknessMm, args.Closed);

        var centerline = await ArchitectureProxy.DrawPolylineAsync(gw, args.Vertices, args.Closed, args.CenterlineLayer, ct).ConfigureAwait(false);
        var leftFace   = await ArchitectureProxy.DrawPolylineAsync(gw, leftFaceVerts,  args.Closed, args.FaceLayer, ct).ConfigureAwait(false);
        var rightFace  = await ArchitectureProxy.DrawPolylineAsync(gw, rightFaceVerts, args.Closed, args.FaceLayer, ct).ConfigureAwait(false);

        var segCount = args.Closed ? args.Vertices.Count : args.Vertices.Count - 1;
        return new DrawWallsChainResult(centerline, leftFace, rightFace, segCount, totalLen, args.ThicknessMm, created);
    }

    // ─────────── doors and windows ───────────

    [McpTool("insert_door",
        "THE ARCHITECTURE ONE: draws the door as primitives on this layer standard, and optionally cuts the wall. The openings category has its own insert_door which places a numbered BLOCK with schedule attributes instead - use that one when the door has to appear in a door schedule. Insert a door at a hinge point. Draws the door panel (rectangle width × frameThicknessMm on A-DOOR) at the requested opening angle plus a swing arc (default quarter-circle, on A-DOOR-SWING). swingDirection='left' (default) hinges on the LEFT side of the wall axis; 'right' hinges on the RIGHT. Pass wallHandle to also cut the host wall at the door's jambs (hinge -> hinge + widthMm along hingeAngleDeg) before drawing the panel -- omit it to only draw the door primitives without touching any wall (e.g. when the wall was already cut separately via split_wall_at_opening).",
        "architecture",
        Intent = new[]
        {
            "wstaw drzwi w sciane",
            "insert single door 900 mm",
            "draw door with swing arc",
            "drzwi otwierane w lewo",
            "door at hinge point"
        },
        RequiresPlugin = true)]
    public static async Task<InsertDoorResult> InsertDoor(IPluginGateway gw, InsertDoorArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0) throw new ArgumentException("widthMm must be > 0");
        if (args.OpeningDeg <= 0 || args.OpeningDeg > 180) throw new ArgumentException("openingDeg must be in (0, 180]");

        // Hinge axis = wall direction at the hinge. Door swings to the perpendicular
        // side determined by swingDirection.
        double hingeRad   = args.HingeAngleDeg * Math.PI / 180.0;
        double openingRad = args.OpeningDeg    * Math.PI / 180.0;
        int sign = string.Equals(args.SwingDirection, "right", StringComparison.OrdinalIgnoreCase) ? -1 : 1;

        var v0 = args.Hinge;

        SplitWallAtOpeningResult? wallOpening = null;
        if (!string.IsNullOrWhiteSpace(args.WallHandle))
        {
            // Jambs run along the WALL axis (hingeAngleDeg), not the door's open
            // swing angle -- the opening spans from the hinge to hinge + widthMm
            // in the closed-door direction.
            var jamb1 = v0;
            var jamb2 = new Point2dDto(v0.X + Math.Cos(hingeRad) * args.WidthMm, v0.Y + Math.Sin(hingeRad) * args.WidthMm);
            wallOpening = await SplitWallAtOpening(gw, new SplitWallAtOpeningArgs(args.WallHandle!, jamb1, jamb2), ct).ConfigureAwait(false);
        }

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.DoorLayer, 30, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.DoorLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.SwingLayer, 30, "DASHED", ct).ConfigureAwait(false))
            created.Add(args.SwingLayer);

        // Door panel = rectangle from hinge along the (hinge axis rotated by openingRad)
        // direction with width = WidthMm, perpendicular thickness = frameThicknessMm.
        double panelAngle = hingeRad + sign * openingRad;
        double cosA = Math.Cos(panelAngle), sinA = Math.Sin(panelAngle);
        double cosP = -sinA * sign, sinP = cosA * sign;  // perpendicular offset for thickness
        var v1 = new Point2dDto(v0.X + cosA * args.WidthMm,                             v0.Y + sinA * args.WidthMm);
        var v2 = new Point2dDto(v1.X + cosP * args.FrameThicknessMm,                    v1.Y + sinP * args.FrameThicknessMm);
        var v3 = new Point2dDto(v0.X + cosP * args.FrameThicknessMm,                    v0.Y + sinP * args.FrameThicknessMm);
        var panelVerts = new[] { v0, v1, v2, v3 };
        var panel = await ArchitectureProxy.DrawPolylineAsync(gw, panelVerts, true, args.DoorLayer, ct).ConfigureAwait(false);

        // Swing arc = arc centred on hinge, radius = widthMm, from the closed
        // position (along hinge axis) to the opened position (panelAngle).
        double startDeg = sign > 0 ? args.HingeAngleDeg                   : args.HingeAngleDeg - args.OpeningDeg;
        double endDeg   = sign > 0 ? args.HingeAngleDeg + args.OpeningDeg : args.HingeAngleDeg;
        var swing = await ArchitectureProxy.DrawArcAsync(
            gw, args.Hinge, args.WidthMm, startDeg, endDeg, args.SwingLayer, ct).ConfigureAwait(false);

        return new InsertDoorResult(panel, swing, args.WidthMm, args.OpeningDeg, created, wallOpening,
            wallOpening is not null
                ? "Door panel + swing arc drawn. Wall opening was cut at the door's jambs."
                : "Door panel + swing arc drawn. No wallHandle was supplied, so no wall was cut.");
    }

    [McpTool("insert_window",
        "THE ARCHITECTURE ONE: draws the window as primitives on this layer standard. The openings category has its own insert_window which places a numbered BLOCK with schedule attributes instead. Insert a window centred at a point along a wall axis. Draws 5 entities on A-GLAZ: the sill line (wall side closer to exterior), the glass line (in the middle of the wall), the header line (wall side closer to interior), and two perpendicular jamb lines closing the opening. Pass wallHandle to also cut the host wall at the window's own axis span before drawing -- omit it to only draw the window primitives without touching any wall. rotationDeg is the wall's heading in degrees (0 = horizontal, +90 = vertical going up).",
        "architecture",
        Intent = new[]
        {
            "wstaw okno w sciane",
            "insert window 1200 mm",
            "draw window with sill and header",
            "okno o szerokosci 1.2 m",
            "window centered on wall"
        },
        RequiresPlugin = true)]
    public static async Task<InsertWindowResult> InsertWindow(IPluginGateway gw, InsertWindowArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0)         throw new ArgumentException("widthMm must be > 0");
        if (args.WallThicknessMm <= 0) throw new ArgumentException("wallThicknessMm must be > 0");

        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad),  sinA = Math.Sin(rad);
        double half = args.WidthMm / 2.0;
        double t    = args.WallThicknessMm / 2.0;

        // Wall axis points (a -> b) along rotationDeg, centred on args.Center.
        var aOnAxis = new Point2dDto(args.Center.X - cosA * half, args.Center.Y - sinA * half);
        var bOnAxis = new Point2dDto(args.Center.X + cosA * half, args.Center.Y + sinA * half);

        SplitWallAtOpeningResult? wallOpening = null;
        if (!string.IsNullOrWhiteSpace(args.WallHandle))
        {
            wallOpening = await SplitWallAtOpening(gw, new SplitWallAtOpeningArgs(args.WallHandle!, aOnAxis, bOnAxis), ct).ConfigureAwait(false);
        }

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 4, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);

        // Perpendicular offsets give sill (-t) and header (+t).
        double nx = -sinA, ny = cosA;
        Point2dDto Off(Point2dDto p, double s) => new(p.X + nx * s, p.Y + ny * s);
        var sillA   = Off(aOnAxis, -t);  var sillB   = Off(bOnAxis, -t);
        var headerA = Off(aOnAxis,  t);  var headerB = Off(bOnAxis,  t);

        var sill   = await ArchitectureProxy.DrawLineAsync(gw, sillA,    sillB,    args.Layer, ct).ConfigureAwait(false);
        var glass  = await ArchitectureProxy.DrawLineAsync(gw, aOnAxis,  bOnAxis,  args.Layer, ct).ConfigureAwait(false);
        var header = await ArchitectureProxy.DrawLineAsync(gw, headerA,  headerB,  args.Layer, ct).ConfigureAwait(false);
        var leftJamb  = await ArchitectureProxy.DrawLineAsync(gw, sillA, headerA, args.Layer, ct).ConfigureAwait(false);
        var rightJamb = await ArchitectureProxy.DrawLineAsync(gw, sillB, headerB, args.Layer, ct).ConfigureAwait(false);

        return new InsertWindowResult(sill, glass, header, leftJamb, rightJamb, args.WidthMm, created, wallOpening,
            wallOpening is not null
                ? "Window primitives drawn. Wall opening was cut at the window's axis span."
                : "Window primitives drawn. No wallHandle was supplied, so no wall was cut.");
    }

    // ─────────── columns ───────────

    [McpTool("insert_rect_column",
        "Insert a rectangular structural column profile on layer S-COLS plus a small crosshair centre-mark on S-COLS-CTRL. width = X-axis, depth = Y-axis (before rotation). Column is auto-centered on the supplied point.",
        "architecture",
        Intent = new[]
        {
            "wstaw slup prostokatny",
            "insert rectangular column",
            "structural column 400x400",
            "kolumna prostokatna z osiami",
            "draw rect column with center mark"
        },
        RequiresPlugin = true)]
    public static async Task<InsertColumnResult> InsertRectColumn(
        IPluginGateway gw, InsertRectColumnArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0 || args.DepthMm <= 0) throw new ArgumentException("widthMm/depthMm must be > 0");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.ColumnLayer, 1, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.ColumnLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.CenterMarkLayer, 8, "CENTER", ct).ConfigureAwait(false))
            created.Add(args.CenterMarkLayer);

        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
        double hw = args.WidthMm / 2.0, hd = args.DepthMm / 2.0;

        Point2dDto Corner(double x, double y) =>
            new(args.Center.X + x * cosA - y * sinA, args.Center.Y + x * sinA + y * cosA);

        var corners = new[] { Corner(-hw, -hd), Corner(hw, -hd), Corner(hw, hd), Corner(-hw, hd) };
        var profile = await ArchitectureProxy.DrawPolylineAsync(gw, corners, true, args.ColumnLayer, ct).ConfigureAwait(false);

        double m = args.CenterMarkSizeMm / 2.0;
        var ch = await ArchitectureProxy.DrawLineAsync(
            gw, new(args.Center.X - m, args.Center.Y), new(args.Center.X + m, args.Center.Y), args.CenterMarkLayer, ct).ConfigureAwait(false);
        var cv = await ArchitectureProxy.DrawLineAsync(
            gw, new(args.Center.X, args.Center.Y - m), new(args.Center.X, args.Center.Y + m), args.CenterMarkLayer, ct).ConfigureAwait(false);

        return new InsertColumnResult(profile, ch, cv, created);
    }

    [McpTool("insert_round_column",
        "Insert a circular structural column on layer S-COLS plus a small crosshair centre-mark on S-COLS-CTRL.",
        "architecture",
        Intent = new[]
        {
            "wstaw slup okragly",
            "insert round column",
            "circular structural column",
            "kolumna okragla 400 mm",
            "draw round column with center mark"
        },
        RequiresPlugin = true)]
    public static async Task<InsertColumnResult> InsertRoundColumn(
        IPluginGateway gw, InsertRoundColumnArgs args, CancellationToken ct)
    {
        if (args.DiameterMm <= 0) throw new ArgumentException("diameterMm must be > 0");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.ColumnLayer, 1, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.ColumnLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.CenterMarkLayer, 8, "CENTER", ct).ConfigureAwait(false))
            created.Add(args.CenterMarkLayer);

        var profile = await ArchitectureProxy.DrawCircleAsync(
            gw, args.Center, args.DiameterMm / 2.0, args.ColumnLayer, ct).ConfigureAwait(false);

        double m = args.CenterMarkSizeMm / 2.0;
        var ch = await ArchitectureProxy.DrawLineAsync(
            gw, new(args.Center.X - m, args.Center.Y), new(args.Center.X + m, args.Center.Y), args.CenterMarkLayer, ct).ConfigureAwait(false);
        var cv = await ArchitectureProxy.DrawLineAsync(
            gw, new(args.Center.X, args.Center.Y - m), new(args.Center.X, args.Center.Y + m), args.CenterMarkLayer, ct).ConfigureAwait(false);

        return new InsertColumnResult(profile, ch, cv, created);
    }

    // ─────────── rooms ───────────

    [McpTool("define_room",
        "Define a room: closed boundary polyline on A-ROOM-BNDY plus three text labels on A-ROOM-IDEN (room number, room name, computed area in m²). Area is computed from the polyline using the shoelace formula and reported in m² (assuming the drawing is in millimetres). tagPosition defaults to the polygon's centroid. The whole result is one transactional 'room' the agent can later reference by handle.",
        "architecture",
        Intent = new[]
        {
            "zdefiniuj pomieszczenie",
            "define room with number and name",
            "stworz pokoj 101 z polem",
            "create room boundary with tag",
            "label room with area m2"
        },
        RequiresPlugin = true)]
    public static async Task<DefineRoomResult> DefineRoom(IPluginGateway gw, DefineRoomArgs args, CancellationToken ct)
    {
        if (args.Vertices is null || args.Vertices.Count < 3)
            throw new ArgumentException("define_room needs >= 3 vertices");
        if (string.IsNullOrWhiteSpace(args.Number)) throw new ArgumentException("number must not be empty");
        if (string.IsNullOrWhiteSpace(args.Name))   throw new ArgumentException("name must not be empty");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.BoundaryLayer, 8, "DASHED", ct).ConfigureAwait(false))
            created.Add(args.BoundaryLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.TagLayer, 7, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.TagLayer);

        var areaMm2  = ShoelaceArea(args.Vertices);
        var areaM2   = areaMm2 / 1_000_000.0;
        var centroid = args.TagPosition ?? Centroid(args.Vertices);

        var boundary = await ArchitectureProxy.DrawPolylineAsync(
            gw, args.Vertices, closed: true, args.BoundaryLayer, ct).ConfigureAwait(false);

        // Stack three text lines around the centroid — number on top, name middle, area bottom.
        var lineSpacing = args.TagTextHeightMm * 1.4;
        var numberPos = new Point2dDto(centroid.X, centroid.Y + lineSpacing);
        var namePos   = new Point2dDto(centroid.X, centroid.Y);
        var areaPos   = new Point2dDto(centroid.X, centroid.Y - lineSpacing);

        // The area label carries "m²", and AutoCAD's default text style is backed by an SHX
        // font that has no glyph for it - so every room tag on a default install used to read
        // "20,46 m?". The string was never wrong; the font could not draw it.
        //
        // Setting a TrueType style as current does not help either: DBText takes the style it
        // is given, and this tool was giving none. So name one explicitly. The style is created
        // on demand and the call is idempotent, so a drawing that already has it is untouched.
        //
        // tagTextStyle lets an office with its own standards name their style instead; pass
        // their name and nothing is created. Passing "" opts out entirely and takes whatever
        // the drawing's current style is, "m?" and all - deliberate, and their call to make.
        var tagStyle = args.TagTextStyle;
        if (tagStyle is null)
        {
            tagStyle = DefaultRoomTagTextStyle;
            // "Arial" is the typeface name, not "arial.ttf" - the plugin builds a FontDescriptor
            // from this string, and a file name there yields a style bound to a typeface that
            // does not exist, which renders as the fallback and looks like the style was ignored.
            await ArchitectureProxy.EnsureTextStyleAsync(gw, tagStyle, "Arial", ct).ConfigureAwait(false);
        }
        else if (tagStyle.Length == 0)
        {
            tagStyle = null;   // explicit opt-out: use whatever the drawing has current
        }

        var numberText = await ArchitectureProxy.AddDBTextAsync(
            gw, numberPos, args.Number, args.TagTextHeightMm, args.TagLayer, 0.0, ct, tagStyle).ConfigureAwait(false);
        var nameText   = await ArchitectureProxy.AddDBTextAsync(
            gw, namePos, args.Name,   args.TagTextHeightMm, args.TagLayer, 0.0, ct, tagStyle).ConfigureAwait(false);
        var areaText   = await ArchitectureProxy.AddDBTextAsync(
            gw, areaPos, $"{areaM2:F2} m²", args.TagTextHeightMm, args.TagLayer, 0.0, ct, tagStyle).ConfigureAwait(false);

        return new DefineRoomResult(boundary, numberText, nameText, areaText, areaM2, centroid, created);
    }

    /// <summary>
    /// Text style created on demand for room tags. TrueType, because the area label contains
    /// "m²" and the SHX fonts AutoCAD ships with have no glyph for it.
    /// </summary>
    private const string DefaultRoomTagTextStyle = "ACADMCP-ROOM";

    // ─────────── dimensioning ───────────

    [McpTool("dimension_wall",
        "Place ONE dimension along a wall segment between two endpoints. Auto-picks linear vs aligned per rule 36 §9: walls within 1° of horizontal/vertical use linear (rotation locked); anything else uses aligned. forceLinear / forceAligned override the heuristic. offsetMm is the perpendicular distance from the wall axis to the dimension line.",
        "architecture",
        Intent = new[]
        {
            "zwymiaruj sciane",
            "dimension wall",
            "wymiar wzdluz sciany",
            "linear dim along wall",
            "auto dimension between two points"
        },
        RequiresPlugin = true)]
    public static async Task<DimensionWallResult> DimensionWall(
        IPluginGateway gw, DimensionWallArgs args, CancellationToken ct)
    {
        if (args.ForceLinear && args.ForceAligned)
            throw new ArgumentException("cannot set both forceLinear and forceAligned");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 2, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);

        double dx = args.End.X - args.Start.X;
        double dy = args.End.Y - args.Start.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) throw new ArgumentException("dimension start and end coincide");

        double angDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        bool isOrthogonal =
            Math.Abs(NormalizeDeg(angDeg)) < 1.0 ||
            Math.Abs(NormalizeDeg(angDeg) - 90.0) < 1.0 ||
            Math.Abs(NormalizeDeg(angDeg) + 90.0) < 1.0 ||
            Math.Abs(Math.Abs(NormalizeDeg(angDeg)) - 180.0) < 1.0;

        bool useLinear = args.ForceLinear || (!args.ForceAligned && isOrthogonal);

        // Dimension line offset perpendicular to the wall axis.
        double nx = -dy / len, ny = dx / len;
        var dimLinePoint = new Point2dDto(
            (args.Start.X + args.End.X) / 2.0 + nx * args.OffsetMm,
            (args.Start.Y + args.End.Y) / 2.0 + ny * args.OffsetMm);

        EntityHandle dim;
        string primitive;
        if (useLinear)
        {
            // For a horizontal wall use rotation 0; for a vertical wall use 90.
            double rotation = Math.Abs(NormalizeDeg(angDeg) - 90.0) < 1.0 ||
                              Math.Abs(NormalizeDeg(angDeg) + 90.0) < 1.0 ? 90.0 : 0.0;
            dim = await ArchitectureProxy.DimensionLinearAsync(
                gw, args.Start, args.End, dimLinePoint, args.Layer, rotation, ct).ConfigureAwait(false);
            primitive = "linear";
        }
        else
        {
            dim = await ArchitectureProxy.DimensionAlignedAsync(
                gw, args.Start, args.End, dimLinePoint, args.Layer, ct).ConfigureAwait(false);
            primitive = "aligned";
        }

        return new DimensionWallResult(dim, primitive, len, created);
    }

    // ─────────── ceilings / stairs / ramps / elevators / tags (D6) ───────────

    [McpTool("draw_ceiling_grid",
        "Draw a T-bar suspended ceiling grid inside a rectangular bounding box. Creates a closed border polyline plus N vertical and M horizontal interior lines spaced by tileWidthMm × tileDepthMm. All entities land on A-CLNG (configurable). rotationDeg rotates the whole grid around the bbox centre (0 = axis-aligned). Returns the border handle plus separate lists of vertical/horizontal tile gridlines.",
        "architecture",
        Intent = new[]
        {
            "narysuj siatke sufitu podwieszanego",
            "draw suspended ceiling grid",
            "ceiling tile grid 600x600",
            "kratka sufitu akustycznego",
            "T-bar grid ceiling"
        },
        RequiresPlugin = true)]
    public static async Task<DrawCeilingGridResult> DrawCeilingGrid(IPluginGateway gw, DrawCeilingGridArgs args, CancellationToken ct)
    {
        if (args.TileWidthMm <= 0 || args.TileDepthMm <= 0) throw new ArgumentException("tileWidth/Depth must be > 0");
        double w = args.BboxMax.X - args.BboxMin.X;
        double d = args.BboxMax.Y - args.BboxMin.Y;
        if (w <= 0 || d <= 0) throw new ArgumentException("bboxMax must be strictly greater than bboxMin on both axes");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 5, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);

        double cx = (args.BboxMin.X + args.BboxMax.X) / 2.0;
        double cy = (args.BboxMin.Y + args.BboxMax.Y) / 2.0;
        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosR = Math.Cos(rad), sinR = Math.Sin(rad);
        Point2dDto Rot(double lx, double ly) => new(cx + lx * cosR - ly * sinR, cy + lx * sinR + ly * cosR);

        // Border (local bbox around centre).
        double hx = w / 2.0, hy = d / 2.0;
        var border = new[]
        {
            Rot(-hx, -hy), Rot(hx, -hy), Rot(hx, hy), Rot(-hx, hy)
        };
        var borderHandle = await ArchitectureProxy.DrawPolylineAsync(gw, border, closed: true, args.Layer, ct).ConfigureAwait(false);

        int tilesX = Math.Max(1, (int)Math.Floor(w / args.TileWidthMm));
        int tilesY = Math.Max(1, (int)Math.Floor(d / args.TileDepthMm));
        double stepX = w / tilesX;
        double stepY = d / tilesY;

        var verticals = new List<EntityHandle>();
        for (int i = 1; i < tilesX; i++)
        {
            double lx = -hx + i * stepX;
            verticals.Add(await ArchitectureProxy.DrawLineAsync(gw, Rot(lx, -hy), Rot(lx, hy), args.Layer, ct).ConfigureAwait(false));
        }
        var horizontals = new List<EntityHandle>();
        for (int j = 1; j < tilesY; j++)
        {
            double ly = -hy + j * stepY;
            horizontals.Add(await ArchitectureProxy.DrawLineAsync(gw, Rot(-hx, ly), Rot(hx, ly), args.Layer, ct).ConfigureAwait(false));
        }

        return new DrawCeilingGridResult(new[] { borderHandle }, verticals, horizontals, tilesX, tilesY, created);
    }

    [McpTool("insert_stair",
        "Draw a simple straight-run stair on A-STRS: outline rectangle (widthMm × runLengthMm), treadCount-1 perpendicular tread lines at equal spacing, and a travel-direction arrow (shaft + head). The arrow ends with an 'UP' label (configurable) on A-ANNO-NOTE. directionDeg points along the run (0 = +X). For multi-flight or spiral stairs use acad-verticals in Phase D7.",
        "architecture",
        Intent = new[]
        {
            "wstaw schody proste",
            "insert straight stair",
            "rysuj bieg schodow z kierunkiem",
            "draw stair run with treads",
            "stair UP arrow 10 stopni"
        },
        RequiresPlugin = true)]
    public static async Task<InsertStairResult> InsertStair(IPluginGateway gw, InsertStairArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0 || args.RunLengthMm <= 0) throw new ArgumentException("widthMm/runLengthMm must be > 0");
        if (args.TreadCount < 2) throw new ArgumentException("treadCount must be >= 2");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 6, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.AnnoLayer, 2, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.AnnoLayer);

        double rad = args.DirectionDeg * Math.PI / 180.0;
        double cosR = Math.Cos(rad), sinR = Math.Sin(rad);
        Point2dDto Tx(double lx, double ly) => new(args.Origin.X + lx * cosR - ly * sinR, args.Origin.Y + lx * sinR + ly * cosR);

        double hw = args.WidthMm / 2.0;
        var outlineVerts = new[] { Tx(0, -hw), Tx(args.RunLengthMm, -hw), Tx(args.RunLengthMm, hw), Tx(0, hw) };
        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, outlineVerts, closed: true, args.Layer, ct).ConfigureAwait(false);

        double treadDepth = args.RunLengthMm / args.TreadCount;
        var treads = new List<EntityHandle>();
        for (int i = 1; i < args.TreadCount; i++)
        {
            double lx = i * treadDepth;
            treads.Add(await ArchitectureProxy.DrawLineAsync(gw, Tx(lx, -hw), Tx(lx, hw), args.Layer, ct).ConfigureAwait(false));
        }

        // Arrow down the centreline: shaft from 10% → 90% of run, arrowhead at 90%.
        double ax0 = args.RunLengthMm * 0.10;
        double ax1 = args.RunLengthMm * 0.90;
        double headLen = Math.Min(args.WidthMm * 0.25, treadDepth * 0.8);
        var arrow = new List<EntityHandle>
        {
            await ArchitectureProxy.DrawLineAsync(gw, Tx(ax0, 0), Tx(ax1, 0), args.AnnoLayer, ct).ConfigureAwait(false),
            await ArchitectureProxy.DrawLineAsync(gw, Tx(ax1, 0), Tx(ax1 - headLen,  headLen * 0.5), args.AnnoLayer, ct).ConfigureAwait(false),
            await ArchitectureProxy.DrawLineAsync(gw, Tx(ax1, 0), Tx(ax1 - headLen, -headLen * 0.5), args.AnnoLayer, ct).ConfigureAwait(false),
        };

        var labelPos = Tx(ax0 - args.TextHeightMm * 0.5, 0);
        var label = await ArchitectureProxy.AddDBTextAsync(
            gw, labelPos, $"{args.UpLabel} {args.TreadCount} x {Math.Round(treadDepth)}",
            args.TextHeightMm, args.AnnoLayer, args.DirectionDeg, ct).ConfigureAwait(false);

        return new InsertStairResult(outline, treads, arrow, label, treadDepth, created);
    }

    [McpTool("insert_ramp",
        "Draw a simple rectangular ramp outline on A-STRS plus a slope arrow (shaft + head) along the travel direction and a text label reporting the gradient as 'N% RAMP' on A-ANNO-NOTE. widthMm runs perpendicular to directionDeg, lengthMm runs along it.",
        "architecture",
        Intent = new[]
        {
            "wstaw rampe dla niepelnosprawnych",
            "insert accessible ramp",
            "ramp with slope percent label",
            "draw ramp 6 procent",
            "wstaw pochylnie z oznaczeniem spadku"
        },
        RequiresPlugin = true)]
    public static async Task<InsertRampResult> InsertRamp(IPluginGateway gw, InsertRampArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0 || args.LengthMm <= 0) throw new ArgumentException("widthMm/lengthMm must be > 0");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 6, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.AnnoLayer, 2, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.AnnoLayer);

        double rad = args.DirectionDeg * Math.PI / 180.0;
        double cosR = Math.Cos(rad), sinR = Math.Sin(rad);
        Point2dDto Tx(double lx, double ly) => new(args.Origin.X + lx * cosR - ly * sinR, args.Origin.Y + lx * sinR + ly * cosR);

        double hw = args.WidthMm / 2.0;
        var outlineVerts = new[] { Tx(0, -hw), Tx(args.LengthMm, -hw), Tx(args.LengthMm, hw), Tx(0, hw) };
        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, outlineVerts, closed: true, args.Layer, ct).ConfigureAwait(false);

        double headLen = Math.Min(args.WidthMm * 0.3, args.LengthMm * 0.1);
        var arrow = new List<EntityHandle>
        {
            await ArchitectureProxy.DrawLineAsync(gw, Tx(args.LengthMm * 0.10, 0), Tx(args.LengthMm * 0.90, 0), args.AnnoLayer, ct).ConfigureAwait(false),
            await ArchitectureProxy.DrawLineAsync(gw, Tx(args.LengthMm * 0.90, 0), Tx(args.LengthMm * 0.90 - headLen,  headLen * 0.5), args.AnnoLayer, ct).ConfigureAwait(false),
            await ArchitectureProxy.DrawLineAsync(gw, Tx(args.LengthMm * 0.90, 0), Tx(args.LengthMm * 0.90 - headLen, -headLen * 0.5), args.AnnoLayer, ct).ConfigureAwait(false),
        };

        var labelPos = Tx(args.LengthMm * 0.5, -hw - args.TextHeightMm * 1.2);
        var label = await ArchitectureProxy.AddDBTextAsync(
            gw, labelPos, $"{args.SlopePercent:0.##}% RAMP",
            args.TextHeightMm, args.AnnoLayer, args.DirectionDeg, ct).ConfigureAwait(false);

        return new InsertRampResult(outline, arrow, label, created);
    }

    [McpTool("insert_elevator",
        "Draw an elevator shaft on A-STRS as a rectangle with two diagonal lines (X) plus a centred label on A-ANNO-NOTE. No cab / mechanical details — use this as a plan-view placeholder for lifts/verticals. For more detail use acad-verticals in Phase D7.",
        "architecture",
        Intent = new[]
        {
            "wstaw winde schemat",
            "insert elevator shaft",
            "draw elevator placeholder",
            "szyb windy z krzyzem",
            "lift shaft X mark plan"
        },
        RequiresPlugin = true)]
    public static async Task<InsertElevatorResult> InsertElevator(IPluginGateway gw, InsertElevatorArgs args, CancellationToken ct)
    {
        if (args.WidthMm <= 0 || args.DepthMm <= 0) throw new ArgumentException("widthMm/depthMm must be > 0");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 6, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.AnnoLayer, 2, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.AnnoLayer);

        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosR = Math.Cos(rad), sinR = Math.Sin(rad);
        Point2dDto Tx(double lx, double ly) => new(args.Center.X + lx * cosR - ly * sinR, args.Center.Y + lx * sinR + ly * cosR);

        double hw = args.WidthMm / 2.0, hd = args.DepthMm / 2.0;
        var outlineVerts = new[] { Tx(-hw, -hd), Tx(hw, -hd), Tx(hw, hd), Tx(-hw, hd) };
        var shaft = await ArchitectureProxy.DrawPolylineAsync(gw, outlineVerts, closed: true, args.Layer, ct).ConfigureAwait(false);

        var d1 = await ArchitectureProxy.DrawLineAsync(gw, Tx(-hw, -hd), Tx( hw,  hd), args.Layer, ct).ConfigureAwait(false);
        var d2 = await ArchitectureProxy.DrawLineAsync(gw, Tx(-hw,  hd), Tx( hw, -hd), args.Layer, ct).ConfigureAwait(false);
        var label = await ArchitectureProxy.AddDBTextAsync(
            gw, args.Center, args.Label, args.TextHeightMm, args.AnnoLayer, args.RotationDeg, ct).ConfigureAwait(false);

        return new InsertElevatorResult(shaft, d1, d2, label, created);
    }

    [McpTool("attach_room_tag",
        "Attach a compact room tag built as a 3-line MTEXT-style stack (number / name / area) at a centroid. When areaM2 is null the third line is omitted. Implementation uses 3 stacked DBText rows on A-ROOM-IDEN because MText creation runs through acad-annotations in a later phase.",
        "architecture",
        Intent = new[]
        {
            "dodaj etykiete pokoju",
            "tag room with number name area",
            "attach room label",
            "oznacz pomieszczenie z nazwa",
            "room ID tag"
        },
        RequiresPlugin = true)]
    public static async Task<AttachRoomTagResult> AttachRoomTag(IPluginGateway gw, AttachRoomTagArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Number)) throw new ArgumentException("number must not be empty");
        if (string.IsNullOrWhiteSpace(args.Name))   throw new ArgumentException("name must not be empty");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 7, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.Layer);

        double lineSpacing = args.TextHeightMm * 1.4;
        var numberPos = new Point2dDto(args.Center.X, args.Center.Y + lineSpacing);
        var namePos   = new Point2dDto(args.Center.X, args.Center.Y);
        var areaPos   = new Point2dDto(args.Center.X, args.Center.Y - lineSpacing);

        var numberText = await ArchitectureProxy.AddDBTextAsync(gw, numberPos, args.Number, args.TextHeightMm, args.Layer, args.RotationDeg, ct).ConfigureAwait(false);
        var nameText   = await ArchitectureProxy.AddDBTextAsync(gw, namePos,   args.Name,   args.TextHeightMm * 0.8, args.Layer, args.RotationDeg, ct).ConfigureAwait(false);
        if (args.AreaM2.HasValue)
            await ArchitectureProxy.AddDBTextAsync(gw, areaPos, $"{args.AreaM2.Value:F2} m\u00b2", args.TextHeightMm * 0.7, args.Layer, args.RotationDeg, ct).ConfigureAwait(false);

        _ = nameText;
        return new AttachRoomTagResult(numberText, created);
    }

    [McpTool("split_wall_at_opening",
        "Cut a hole for a door/window in a wall entity — wrapper around acad.openings.cut_wall_for_opening. Workflow: (1) call split_wall_at_opening(wallHandle, jamb1, jamb2) BEFORE insert_door / insert_window so the wall faces are trimmed at the jambs; (2) then call the opening tool. v1 inherits the wrapped primitive's limitation (Line + 2-vertex Polyline walls); multi-vertex polyline walls will be supported once acad-verticals lands in Phase D7.",
        "architecture",
        Intent = new[]
        {
            "wytnij otwor w scianie pod drzwi",
            "cut hole in wall for opening",
            "split wall at door jambs",
            "przytnij sciane pod okno",
            "trim wall for opening jamb1 jamb2"
        },
        RequiresPlugin = true)]
    public static async Task<SplitWallAtOpeningResult> SplitWallAtOpening(IPluginGateway gw, SplitWallAtOpeningArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.WallHandle)) throw new ArgumentException("wallHandle must not be empty");

        var payload = new JsonObject
        {
            ["wallHandle"] = args.WallHandle,
            ["jamb1"] = JsonSerializer.SerializeToNode(args.Jamb1)!,
            ["jamb2"] = JsonSerializer.SerializeToNode(args.Jamb2)!,
        };
        var resp = await gw.InvokeAsync("acad.openings.cut_wall_for_opening", payload, T_NORMAL, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException("acad.openings.cut_wall_for_opening returned null");

        string original = resp["originalHandle"]?.GetValue<string>() ?? args.WallHandle;
        string? left    = resp["leftHandle"]?.GetValue<string>();
        string? right   = resp["rightHandle"]?.GetValue<string>();
        double gap      = resp["gapLengthMm"]?.GetValue<double>() ?? 0.0;

        return new SplitWallAtOpeningResult(
            original, left, right, gap,
            "Wall trimmed at jambs. v1 handles Line + 2-vertex Polyline walls only; multi-vertex polyline support ships with acad-verticals in Phase D7.");
    }

    // ─────────── introspection ───────────

    [McpTool("architecture_health",
        "Report the architectural layer key + planned bundled block library used by this category. ReadOnly: does NOT touch the active drawing. Use this from the agent to discover defaults without making a real call to AutoCAD.",
        "architecture",
        Intent = new[]
        {
            "co potrafi architecture",
            "list architecture layer key",
            "what blocks does architecture ship",
            "diagnostyka kategorii architecture",
            "architecture category metadata"
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static ArchitectureHealthResult ArchitectureHealth(ArchitectureHealthArgs _)
    {
        var layers = ArchitecturePalette.All
            .Select(s => new ArchitecturalLayerSpec(s.Name, s.AciColor, s.Linetype, s.Purpose))
            .ToList();
        return new ArchitectureHealthResult(layers, ArchitecturePalette.PlannedBlocks, "architecture", "0.1.0");
    }

    // ─────────── private helpers ───────────

    /// <summary>Compute parallel offset polylines for a wall chain (mitred at vertices).</summary>
    private static (List<Point2dDto> Left, List<Point2dDto> Right, double TotalLength) ComputeOffsetFaces(
        IReadOnlyList<Point2dDto> verts, double thicknessMm, bool closed)
    {
        var n = verts.Count;
        double half = thicknessMm / 2.0;

        // Per-segment unit normals (left = +90°, right = -90°).
        var normals = new (double nx, double ny, double len)[closed ? n : n - 1];
        double total = 0;
        for (int i = 0; i < normals.Length; i++)
        {
            int j = (i + 1) % n;
            double dx = verts[j].X - verts[i].X;
            double dy = verts[j].Y - verts[i].Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9)
                throw new ArgumentException($"chain vertex {i} and {j} coincide");
            total += len;
            normals[i] = (-dy / len, dx / len, len);
        }

        var left  = new List<Point2dDto>(n);
        var right = new List<Point2dDto>(n);
        for (int i = 0; i < n; i++)
        {
            (double nxIn, double nyIn) prev;
            (double nxOut, double nyOut) next;

            if (closed)
            {
                int prevIdx = (i - 1 + n) % n;
                prev = (normals[prevIdx].nx, normals[prevIdx].ny);
                next = (normals[i].nx, normals[i].ny);
            }
            else
            {
                if (i == 0)         { var s = normals[0];     prev = (s.nx, s.ny); next = (s.nx, s.ny); }
                else if (i == n - 1){ var s = normals[n - 2]; prev = (s.nx, s.ny); next = (s.nx, s.ny); }
                else                { prev = (normals[i - 1].nx, normals[i - 1].ny);
                                      next = (normals[i].nx,     normals[i].ny); }
            }

            // Mitre = average of the two unit normals scaled to half thickness.
            // Falls back to either normal if the segments are exactly anti-parallel.
            double bx = prev.nxIn + next.nxOut;
            double by = prev.nyIn + next.nyOut;
            double bl = Math.Sqrt(bx * bx + by * by);
            double mx, my;
            if (bl < 1e-9)
            {
                mx = next.nxOut; my = next.nyOut;
            }
            else
            {
                // Project onto the bisector with proper mitre length.
                double dot = prev.nxIn * next.nxOut + prev.nyIn * next.nyOut;
                double mitreScale = half / Math.Max(0.1, Math.Sqrt((1.0 + dot) / 2.0));
                mx = bx / bl * mitreScale;
                my = by / bl * mitreScale;
            }
            left.Add( new(verts[i].X + mx, verts[i].Y + my));
            right.Add(new(verts[i].X - mx, verts[i].Y - my));
        }
        return (left, right, total);
    }

    /// <summary>Polygon area via the shoelace formula. Sign is unimportant; we take |area|.</summary>
    private static double ShoelaceArea(IReadOnlyList<Point2dDto> verts)
    {
        double sum = 0;
        int n = verts.Count;
        for (int i = 0; i < n; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % n];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2.0;
    }

    /// <summary>Polygon centroid (assumes simple, non-self-intersecting polygon).</summary>
    private static Point2dDto Centroid(IReadOnlyList<Point2dDto> verts)
    {
        double cx = 0, cy = 0, sa = 0;
        int n = verts.Count;
        for (int i = 0; i < n; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % n];
            double cross = a.X * b.Y - b.X * a.Y;
            cx += (a.X + b.X) * cross;
            cy += (a.Y + b.Y) * cross;
            sa += cross;
        }
        if (Math.Abs(sa) < 1e-9)
        {
            // Degenerate or zero-area — fallback to mean of vertices.
            double mx = 0, my = 0;
            foreach (var p in verts) { mx += p.X; my += p.Y; }
            return new Point2dDto(mx / n, my / n);
        }
        sa *= 3.0;
        return new Point2dDto(cx / sa, cy / sa);
    }

    /// <summary>Normalise an angle in degrees to the range (-180, 180].</summary>
    private static double NormalizeDeg(double deg)
    {
        deg %= 360.0;
        if (deg <= -180.0) deg += 360.0;
        if (deg > 180.0)   deg -= 360.0;
        return deg;
    }
}
