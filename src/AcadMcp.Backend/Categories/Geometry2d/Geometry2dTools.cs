// AutoCAD acad-geometry-2d category. ~30 tools covering 2D primitive creation,
// queries and modifications. Each method is a thin proxy through IPluginGateway
// to the matching plugin handler under the key "acad.geometry2d.<verb>".
//
// Rules: 19-tool-implementation-pattern.md, 20..25 (tool authoring).
//
// Timeouts:
//   read-only queries     :  5 000 ms
//   single-entity creation: 15 000 ms
//   batch / hatch / spline: 30 000 ms
//   trim / extend / fillet: 30 000 ms (may pick boundaries)

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Geometry2d;

public static class Geometry2dTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    // ───────────────────────── creation ─────────────────────────

    [McpTool("draw_line", "Draw a 2D straight line segment between two points on the active drawing.", "geometry-2d",
        Intent = new[] { "narysuj linie", "stworz odcinek", "draw a line", "create line segment", "linia od punktu do punktu" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawLine(IPluginGateway gw, DrawLineArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawLineArgs, EntityResult>(gw, "acad.geometry2d.draw_line", args, T_NORMAL, ct);

    [McpTool("draw_polyline", "Draw a 2D lightweight polyline through the given vertex list, optionally closed.", "geometry-2d",
        Intent = new[] { "narysuj polilinie", "stworz polilinie", "draw polyline", "create polyline", "polyline through points" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawPolyline(IPluginGateway gw, DrawPolylineArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawPolylineArgs, EntityResult>(gw, "acad.geometry2d.draw_polyline", args, T_SLOW, ct);

    [McpTool("draw_mline", "Draw a multiline (MLINE) through the given vertices using a named multiline style - the way a wall of a defined type is drawn in one call rather than as two offset polylines that must be kept parallel by hand. style defaults to the drawing's current one; create one first with create_mlinestyle. justification is 'top', 'zero' or 'bottom' and decides which of the style's parallel lines the vertices you pass actually lie on, so it changes where the wall sits relative to your points. scale multiplies every element offset, so a 200mm style drawn at scale 1.5 is 300mm wide.", "geometry-2d",
        Intent = new[] { "narysuj multilinie", "narysuj sciane stylem mline", "draw multiline",
                         "draw an mline with a wall style", "wielolinia po punktach",
                         "draw parallel lines as one entity" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawMline(IPluginGateway gw, DrawMlineArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawMlineArgs, EntityResult>(gw, "acad.geometry2d.draw_mline", args, T_SLOW, ct);

    [McpTool("draw_circle", "Draw a circle by center point and radius.", "geometry-2d",
        Intent = new[] { "narysuj okrag", "stworz kolo", "draw a circle", "create circle", "circle by center and radius" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawCircle(IPluginGateway gw, DrawCircleArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawCircleArgs, EntityResult>(gw, "acad.geometry2d.draw_circle", args, T_NORMAL, ct);

    [McpTool("draw_arc", "Draw an arc by center, radius, and start/end angle in degrees (CCW).", "geometry-2d",
        Intent = new[] { "narysuj luk", "stworz luk okregu", "draw arc", "create arc", "arc by center radius and angle" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawArc(IPluginGateway gw, DrawArcArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawArcArgs, EntityResult>(gw, "acad.geometry2d.draw_arc", args, T_NORMAL, ct);

    [McpTool("draw_ellipse", "Draw an ellipse by center, major-axis end point and minor-to-major ratio (0 < ratio <= 1).", "geometry-2d",
        Intent = new[] { "narysuj elipse", "stworz elipse", "draw ellipse", "create ellipse", "ellipse by axes" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawEllipse(IPluginGateway gw, DrawEllipseArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawEllipseArgs, EntityResult>(gw, "acad.geometry2d.draw_ellipse", args, T_NORMAL, ct);

    [McpTool("draw_rectangle", "Draw an axis-aligned rectangle as a closed polyline by two opposite corners.", "geometry-2d",
        Intent = new[] { "narysuj prostokat", "stworz prostokat", "draw rectangle", "create rectangle", "rectangle by two corners" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawRectangle(IPluginGateway gw, DrawRectangleArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawRectangleArgs, EntityResult>(gw, "acad.geometry2d.draw_rectangle", args, T_NORMAL, ct);

    [McpTool("draw_polygon", "Draw a regular polygon (3..1024 sides), inscribed or circumscribed.", "geometry-2d",
        Intent = new[] { "narysuj wielokat", "stworz wielokat foremny", "draw polygon", "create regular polygon", "polygon with N sides" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawPolygon(IPluginGateway gw, DrawPolygonArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawPolygonArgs, EntityResult>(gw, "acad.geometry2d.draw_polygon", args, T_NORMAL, ct);

    [McpTool("draw_spline", "Draw a 2D spline interpolated through the given fit points, optionally closed.", "geometry-2d",
        Intent = new[] { "narysuj krzywa sklejana", "stworz spline", "draw spline curve", "create spline", "spline through points" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawSpline(IPluginGateway gw, DrawSplineArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawSplineArgs, EntityResult>(gw, "acad.geometry2d.draw_spline", args, T_SLOW, ct);

    [McpTool("draw_point", "Draw a single point entity at a 2D position.", "geometry-2d",
        Intent = new[] { "wstaw punkt", "narysuj punkt", "draw a point", "create point entity", "point at coordinate" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawPoint(IPluginGateway gw, DrawPointArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawPointArgs, EntityResult>(gw, "acad.geometry2d.draw_point", args, T_FAST, ct);

    [McpTool("draw_donut", "Draw a donut (filled annulus) at a center, with inner and outer diameters.", "geometry-2d",
        Intent = new[] { "narysuj donut", "stworz pierscien", "draw donut shape", "create annulus", "filled ring" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawDonut(IPluginGateway gw, DrawDonutArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawDonutArgs, EntityResult>(gw, "acad.geometry2d.draw_donut", args, T_NORMAL, ct);

    [McpTool("draw_xline", "Draw an infinite construction line through a base point in a given direction.", "geometry-2d",
        Intent = new[] { "narysuj linie konstrukcyjna", "stworz xline", "draw infinite construction line", "create xline", "infinite line through point" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawXLine(IPluginGateway gw, DrawXLineArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawXLineArgs, EntityResult>(gw, "acad.geometry2d.draw_xline", args, T_FAST, ct);

    [McpTool("draw_ray", "Draw a half-infinite ray from a base point in a given direction.", "geometry-2d",
        Intent = new[] { "narysuj promien", "stworz ray", "draw ray from point", "create ray entity", "half infinite line" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawRay(IPluginGateway gw, DrawRayArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawRayArgs, EntityResult>(gw, "acad.geometry2d.draw_ray", args, T_FAST, ct);

    [McpTool("draw_text", "Draw single-line text (DTEXT) at a 2D position with given height/rotation/style.", "geometry-2d",
        Intent = new[] { "wstaw napis", "dodaj tekst jednolinijkowy", "draw single line text", "add dtext", "label at point" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawText(IPluginGateway gw, DrawTextArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawTextArgs, EntityResult>(gw, "acad.geometry2d.draw_text", args, T_NORMAL, ct);

    [McpTool("draw_mtext", "Draw multiline text (MTEXT) with wrap-width and height.", "geometry-2d",
        Intent = new[] { "wstaw tekst wieloliniowy", "dodaj mtext", "draw multiline text block", "add wrapped text", "mtext at point" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawMText(IPluginGateway gw, DrawMTextArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawMTextArgs, EntityResult>(gw, "acad.geometry2d.draw_mtext", args, T_NORMAL, ct);

    [McpTool("draw_hatch", "Apply an associative hatch over closed boundaries identified by handle.", "geometry-2d",
        Intent = new[] { "narysuj kreskowanie", "wypelnij obszar wzorem", "draw hatch pattern", "fill area with hatch", "associative hatch over boundary" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawHatch(IPluginGateway gw, DrawHatchArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawHatchArgs, EntityResult>(gw, "acad.geometry2d.draw_hatch", args, T_SLOW, ct);

    [McpTool("draw_revcloud", "Draw a revision cloud polyline through the given vertices with arc-length min/max.", "geometry-2d",
        Intent = new[] { "narysuj chmure rewizji", "rysuj revcloud", "draw revision cloud", "create revcloud markup", "cloud over polyline" },
        RequiresPlugin = true)]
    public static Task<EntityResult> DrawRevcloud(IPluginGateway gw, DrawRevcloudArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawRevcloudArgs, EntityResult>(gw, "acad.geometry2d.draw_revcloud", args, T_SLOW, ct);

    // ───────────────────────── queries (read-only) ─────────────────────────

    [McpTool("get_entity", "Return full descriptor (class, layer, color, bbox, length, area, endpoints) of an entity by handle.", "geometry-2d",
        Intent = new[] { "pobierz info o obiekcie", "opisz encje", "get entity details", "describe entity by handle", "fetch entity properties" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<EntityInfoResult> GetEntity(IPluginGateway gw, HandleArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandleArg, EntityInfoResult>(gw, "acad.geometry2d.get_entity", args, T_FAST, ct);

    [McpTool("list_entities_in_window", "List handles of all entities whose bounding box intersects the rectangular window.", "geometry-2d",
        Intent = new[] { "znajdz obiekty w oknie", "lista obiektow w prostokacie", "list entities in window", "find entities in rectangle", "select inside box" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<EntitiesResult> ListEntitiesInWindow(IPluginGateway gw, WindowArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<WindowArg, EntitiesResult>(gw, "acad.geometry2d.list_entities_in_window", args, T_FAST, ct);

    [McpTool("get_curve_length", "Return curve length (perimeter) of a line/polyline/arc/spline by handle.", "geometry-2d",
        Intent = new[] { "podaj dlugosc krzywej", "obwod polilinii", "get curve length", "compute perimeter", "length of polyline" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ScalarResult> GetCurveLength(IPluginGateway gw, HandleArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandleArg, ScalarResult>(gw, "acad.geometry2d.get_curve_length", args, T_FAST, ct);

    [McpTool("get_area", "Return enclosed area for a closed curve (circle, ellipse, closed polyline, hatch).", "geometry-2d",
        Intent = new[] { "podaj pole powierzchni", "oblicz pole zamknietego ksztaltu", "get enclosed area", "compute area of shape", "area of closed polyline" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ScalarResult> GetArea(IPluginGateway gw, HandleArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandleArg, ScalarResult>(gw, "acad.geometry2d.get_area", args, T_FAST, ct);

    [McpTool("get_bounding_box", "Return axis-aligned bounding box of an entity by handle (XY plane).", "geometry-2d",
        Intent = new[] { "podaj prostokat ograniczajacy", "bbox encji", "get bounding box", "compute aabb of entity", "extents of entity" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<BoundingBoxResult> GetBoundingBox(IPluginGateway gw, HandleArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandleArg, BoundingBoxResult>(gw, "acad.geometry2d.get_bounding_box", args, T_FAST, ct);

    [McpTool("get_intersections", "Return XY intersection points between two curves identified by handle.", "geometry-2d",
        Intent = new[] { "punkty przeciecia krzywych", "znajdz przeciecia", "get curve intersections", "compute intersections of two curves", "where do curves cross" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<PointsResult> GetIntersections(IPluginGateway gw, TwoHandlesArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<TwoHandlesArg, PointsResult>(gw, "acad.geometry2d.get_intersections", args, T_FAST, ct);

    [McpTool("get_distance_points", "Return Cartesian distance between two 2D points.", "geometry-2d",
        Intent = new[] { "odleglosc miedzy punktami", "podaj dystans dwoch punktow", "get distance between points", "compute distance points", "length between coordinates" },
        ReadOnly = true)]
    public static Task<ScalarResult> GetDistancePoints(IPluginGateway gw, TwoPointsArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<TwoPointsArg, ScalarResult>(gw, "acad.geometry2d.get_distance_points", args, T_FAST, ct);

    [McpTool("get_distance_to_entity", "Return shortest distance from a 2D point to a curve entity by handle.", "geometry-2d",
        Intent = new[] { "odleglosc punktu od krzywej", "najkrotsza odleglosc do encji", "get distance to entity", "closest distance from point to curve", "perpendicular distance" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ScalarResult> GetDistanceToEntity(IPluginGateway gw, PointAndHandleArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PointAndHandleArg, ScalarResult>(gw, "acad.geometry2d.get_distance_to_entity", args, T_FAST, ct);

    // ───────────────────────── modifications ─────────────────────────

    [McpTool("offset_curve", "Offset a curve by a signed distance, returning the new curve handle.", "geometry-2d",
        Intent = new[] { "przesun krzywa rownolegle", "offset polilinii", "offset a curve by distance", "parallel copy curve", "create offset entity" },
        RequiresPlugin = true)]
    public static Task<EntitiesResult> OffsetCurve(IPluginGateway gw, OffsetArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<OffsetArgs, EntitiesResult>(gw, "acad.geometry2d.offset_curve", args, T_NORMAL, ct);

    [McpTool("trim_curve", "Trim a curve at intersections with the boundary list, keeping the side opposite the pick point.", "geometry-2d",
        Intent = new[] { "przytnij krzywa", "trim do granic", "trim a curve to boundaries", "shorten curve at boundary", "remove segment between intersections" },
        RequiresPlugin = true)]
    public static Task<EntitiesResult> TrimCurve(IPluginGateway gw, TrimExtendArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<TrimExtendArgs, EntitiesResult>(gw, "acad.geometry2d.trim_curve", args, T_SLOW, ct);

    [McpTool("extend_curve", "Extend a curve until it reaches one of the boundary entities.", "geometry-2d",
        Intent = new[] { "wydluz krzywa", "extend do granic", "extend a curve to boundary", "lengthen curve to entity", "extend line to circle" },
        RequiresPlugin = true)]
    public static Task<EntitiesResult> ExtendCurve(IPluginGateway gw, TrimExtendArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<TrimExtendArgs, EntitiesResult>(gw, "acad.geometry2d.extend_curve", args, T_SLOW, ct);

    [McpTool("join_curves", "Join multiple coincident curves into a single polyline if topology allows.", "geometry-2d",
        Intent = new[] { "polacz krzywe", "scal segmenty", "join multiple curves", "merge curves into polyline", "connect collinear segments" },
        RequiresPlugin = true)]
    public static Task<EntityResult> JoinCurves(IPluginGateway gw, HandlesArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandlesArg, EntityResult>(gw, "acad.geometry2d.join_curves", args, T_NORMAL, ct);

    [McpTool("explode_entity", "Explode a polyline/block/hatch into its component primitives.", "geometry-2d",
        Intent = new[] { "rozbij obiekt", "explode polilinie", "explode entity into primitives", "decompose into segments", "break apart entity" },
        RequiresPlugin = true)]
    public static Task<EntitiesResult> ExplodeEntity(IPluginGateway gw, HandleArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandleArg, EntitiesResult>(gw, "acad.geometry2d.explode_entity", args, T_NORMAL, ct);

    [McpTool("fillet_corner", "Fillet two curves at their intersection with the given radius; returns the new fillet arc.", "geometry-2d",
        Intent = new[] { "zaokraglij naroznik", "fillet polilinii", "fillet corner with radius", "round corner between curves", "create fillet arc" },
        RequiresPlugin = true)]
    public static Task<EntityResult> FilletCorner(IPluginGateway gw, FilletArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<FilletArgs, EntityResult>(gw, "acad.geometry2d.fillet_corner", args, T_NORMAL, ct);

    [McpTool("chamfer_corner", "Chamfer two curves at their intersection with two distances; returns the new chamfer line.", "geometry-2d",
        Intent = new[] { "scinaj naroznik", "chamfer polilinii", "chamfer corner with two distances", "bevel corner between curves", "create chamfer segment" },
        RequiresPlugin = true)]
    public static Task<EntityResult> ChamferCorner(IPluginGateway gw, ChamferArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<ChamferArgs, EntityResult>(gw, "acad.geometry2d.chamfer_corner", args, T_NORMAL, ct);

    [McpTool("delete_entities", "Erase entities by handle. Pass multiple in one batch for atomicity.", "geometry-2d",
        Intent = new[] { "usun obiekty", "skasuj encje", "delete entities by handle", "erase multiple entities", "remove from drawing" },
        RequiresPlugin = true)]
    public static Task<OkResult> DeleteEntities(IPluginGateway gw, HandlesArg args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<HandlesArg, OkResult>(gw, "acad.geometry2d.delete_entities", args, T_NORMAL, ct);

    // ─────────── polyline vertex editing (roadmap 3.1) ───────────
    //
    // A drawn polyline is a first draft. Without these the only way to change one is to delete
    // it and draw it again, which throws away its handle, its layer and anything referencing it.
    //
    // All of them work on the lightweight Polyline. A Polyline2d or Polyline3d stores its
    // vertices as separate objects and is refused by name rather than silently mishandled.

    [McpTool("list_polyline_vertices", "List a polyline's vertices with their positions, bulges and per-segment widths, plus its length and whether it is closed. Read-only, and the tool to call before any of the editing ones, since they address vertices by 0-based index and those indices shift as vertices are added or removed. A bulge is tan of a quarter of the arc's included angle: 0 is a straight segment, 1 is a half circle, and the sign gives the direction.", "geometry-2d",
        Intent = new[] { "wierzcholki polilinii", "jakie punkty ma ta polilinia",
                         "list polyline vertices", "read polyline points and bulges",
                         "ile wierzcholkow ma polilinia", "polyline vertex coordinates" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<PolylineVertexListResult> ListPolylineVertices(IPluginGateway gw, PolylineRefArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PolylineRefArgs, PolylineVertexListResult>(gw, "acad.geometry2d.list_polyline_vertices", args, T_FAST, ct);

    [McpTool("polyline_add_vertex", "Insert a vertex into an existing polyline at a 0-based index, shifting the later ones along. Pass index equal to the current vertex count to append to the end. Optionally give the new vertex a bulge (to make the segment an arc) and start/end widths. Answers with the vertex as stored and the new count, so an insert can be checked without a second call.", "geometry-2d",
        Intent = new[] { "dodaj wierzcholek do polilinii", "wstaw punkt w polilinii",
                         "add a vertex to a polyline", "insert a point into a polyline",
                         "przedluz polilinie o punkt", "add polyline point" },
        RequiresPlugin = true)]
    public static Task<PolylineAddVertexResult> PolylineAddVertex(IPluginGateway gw, PolylineVertexArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PolylineVertexArgs, PolylineAddVertexResult>(gw, "acad.geometry2d.polyline_add_vertex", args, T_NORMAL, ct);

    [McpTool("polyline_remove_vertex", "Remove one vertex from a polyline by 0-based index, closing the gap. Refuses when only two vertices are left, because removing one would leave an entity AutoCAD draws as nothing - delete the polyline instead. Answers with the vertex that was removed, so the change can be undone from its own result.", "geometry-2d",
        Intent = new[] { "usun wierzcholek polilinii", "skasuj punkt z polilinii",
                         "remove a polyline vertex", "delete a point from a polyline",
                         "skroc polilinie o wierzcholek", "drop polyline point" },
        RequiresPlugin = true)]
    public static Task<PolylineRemoveVertexResult> PolylineRemoveVertex(IPluginGateway gw, PolylineRefArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PolylineRefArgs, PolylineRemoveVertexResult>(gw, "acad.geometry2d.polyline_remove_vertex", args, T_NORMAL, ct);

    [McpTool("edit_polyline_vertex", "Change one vertex of a polyline: move it, bend the segment leaving it by setting a bulge, or set its start and end widths. At least one of those is required. OMITTED FIELDS ARE LEFT ALONE rather than reset, so a vertex can be moved without flattening the arc it carries. Answers with the vertex as it was and as it now is.", "geometry-2d",
        Intent = new[] { "przesun wierzcholek polilinii", "zmien punkt polilinii",
                         "edit a polyline vertex", "move a polyline point",
                         "zrob luk z odcinka polilinii", "set a bulge on a polyline segment" },
        RequiresPlugin = true)]
    public static Task<PolylineEditVertexResult> EditPolylineVertex(IPluginGateway gw, PolylineVertexArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PolylineVertexArgs, PolylineEditVertexResult>(gw, "acad.geometry2d.edit_polyline_vertex", args, T_NORMAL, ct);

    [McpTool("set_polyline_width", "Set a polyline's width - the whole polyline when segment is omitted, or one 0-based segment when it is given. Setting the whole polyline also clears any per-segment widths set earlier, so the result is the width asked for rather than a mix of old and new. Answers with every vertex's widths before and after.", "geometry-2d",
        Intent = new[] { "ustaw szerokosc polilinii", "pogrub polilinie",
                         "set polyline width", "make a polyline thicker",
                         "szerokosc jednego segmentu polilinii", "polyline lineweight by width" },
        RequiresPlugin = true)]
    public static Task<PolylineWidthResult> SetPolylineWidth(IPluginGateway gw, PolylineWidthArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PolylineWidthArgs, PolylineWidthResult>(gw, "acad.geometry2d.set_polyline_width", args, T_NORMAL, ct);

    [McpTool("reverse_curve", "Reverse a curve's direction, swapping its start and end. Works on any curve - line, arc, polyline, spline, ellipse. Direction is not cosmetic: it decides which side an offset goes, which way a hatch boundary runs, and where text along the curve reads from. Answers with the endpoints before and after, since the only evidence the reversal happened is that they swapped.", "geometry-2d",
        Intent = new[] { "odwroc kierunek krzywej", "zmien kierunek polilinii",
                         "reverse a curve", "flip curve direction",
                         "zamien poczatek z koncem", "reverse polyline direction" },
        RequiresPlugin = true)]
    public static Task<ReverseCurveResult> ReverseCurve(IPluginGateway gw, EntityRefArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<EntityRefArgs, ReverseCurveResult>(gw, "acad.geometry2d.reverse_curve", args, T_NORMAL, ct);

    // ─────────── breaking and dividing curves (roadmap 3.1) ───────────
    //
    // Both break tools take a point NEAR the curve and snap it on, the way AutoCAD's own BREAK
    // does with a picked point. GetSplitCurves does not: an inexact point makes it throw or
    // split somewhere unintended. The distance the point moved is reported, so a rounding error
    // and "you named the wrong curve" look different.

    [McpTool("break_at_point", "Split an open curve into two at a point, the way AutoCAD's BREAK does with a single pick. The point does not have to be exactly on the curve - it is projected onto the nearest position and the result says how far it moved. The ORIGINAL ENTITY IS ERASED and two new ones take its place, inheriting its layer, colour and linetype, so its handle is no longer valid afterwards. Refuses closed curves, where breaking at one point would leave the curve closed and remove nothing.", "geometry-2d",
        Intent = new[] { "przerwij linie w punkcie", "podziel krzywa na dwie",
                         "break a curve at a point", "split a line in two",
                         "rozetnij polilinie w punkcie", "cut a curve at a point" },
        RequiresPlugin = true)]
    public static Task<BreakAtPointResult> BreakAtPoint(IPluginGateway gw, BreakAtPointArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<BreakAtPointArgs, BreakAtPointResult>(gw, "acad.geometry2d.break_at_point", args, T_NORMAL, ct);

    [McpTool("break_between_points", "Remove the piece of a curve between two points, leaving the two outer parts - AutoCAD's BREAK with two picks, used to gap a line where something crosses it. Both points are projected onto the curve. The ORIGINAL ENTITY IS ERASED and replaced by the two remaining pieces; the result reports how much length was removed. Handles open curves: on a closed one, which of the two arcs lies 'between' the points depends on the direction the curve runs, so it refuses rather than risk removing the wrong half.", "geometry-2d",
        Intent = new[] { "wytnij fragment linii", "zrob przerwe w linii",
                         "break a curve between two points", "remove a piece of a line",
                         "przerwa w linii pod przecięciem", "gap a line" },
        RequiresPlugin = true)]
    public static Task<BreakBetweenResult> BreakBetweenPoints(IPluginGateway gw, BreakBetweenArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<BreakBetweenArgs, BreakBetweenResult>(gw, "acad.geometry2d.break_between_points", args, T_NORMAL, ct);

    [McpTool("divide_object", "Mark a curve into a number of EQUAL parts, placing a point at each division - AutoCAD's DIVIDE. The curve itself is not cut; it is marked. n segments produce n-1 markers, at the divisions rather than at the ends, so nothing lands on top of whatever already sits at each end. Name a block to place that instead of points, and it is rotated to follow the curve unless alignToCurve is false.", "geometry-2d",
        Intent = new[] { "podziel obiekt na rowne czesci", "rozstaw punkty co rowno wzdluz linii",
                         "divide a curve into equal parts", "place markers evenly along a curve",
                         "podziel linie na 5 czesci", "equally spaced points on a curve" },
        RequiresPlugin = true)]
    public static Task<DivideResult> DivideObject(IPluginGateway gw, DivideArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DivideArgs, DivideResult>(gw, "acad.geometry2d.divide_object", args, T_NORMAL, ct);

    [McpTool("measure_object", "Mark a curve at a FIXED interval, placing a marker every given distance from the start - AutoCAD's MEASURE. Unlike divide_object the spacing is what you asked for and the leftover sits at the far end, which the result reports; that is the whole difference between the two. The curve is not cut. Name a block to place that instead of points, rotated to follow the curve unless alignToCurve is false.", "geometry-2d",
        Intent = new[] { "rozstaw punkty co 500 wzdluz linii", "zmierz linie co odcinek",
                         "place markers every N units along a curve", "measure a curve at intervals",
                         "rozmiesc bloki co metr", "fixed spacing along a curve" },
        RequiresPlugin = true)]
    public static Task<MeasureResult> MeasureObject(IPluginGateway gw, MeasureArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<MeasureArgs, MeasureResult>(gw, "acad.geometry2d.measure_object", args, T_NORMAL, ct);

    // Added after the visual check on divide_object showed nothing on screen. The markers were
    // there and the numbers were right; DBPoints at AutoCAD's default PDMODE of 0 draw as a
    // single pixel. A tool whose output cannot be seen looks like one that did nothing.
    [McpTool("set_point_style", "Set how POINT entities are drawn, drawing-wide - AutoCAD's DDPTYPE. Give a name such as 'x', 'circleCross' or 'square', or the raw pdmode number. This matters because AutoCAD's default draws a point as a single pixel, so markers placed by divide_object and measure_object are effectively invisible until this is changed. size is PDSIZE: negative is a percentage of the viewport, positive is absolute drawing units. Affects every point in the drawing, existing ones included.", "geometry-2d",
        Intent = new[] { "ustaw styl punktu", "pokaz punkty na rysunku",
                         "set the point style", "make points visible",
                         "punkty sa niewidoczne", "change how points are drawn" },
        RequiresPlugin = true)]
    public static Task<PointStyleResult> SetPointStyle(IPluginGateway gw, PointStyleArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<PointStyleArgs, PointStyleResult>(gw, "acad.geometry2d.set_point_style", args, T_FAST, ct);

    // ─────────── display order, transparency, wipeouts (roadmap 3.1) ───────────
    //
    // These belong together on a real sheet: a wipeout is only useful in FRONT of what it
    // hides, and transparency is the alternative when you want to see through rather than blank.

    [McpTool("set_draworder", "Change which entities are drawn on top where they overlap - AutoCAD's DRAWORDER. position is 'front', 'back', 'above' or 'below'; the last two need relativeTo, the handle to sit above or below. Draw order belongs to a SPACE rather than to the drawing, so every entity in one call must be in the same model space or layout, and the tool refuses a mix rather than silently reordering only some.", "geometry-2d",
        Intent = new[] { "zmien kolejnosc rysowania", "przenies obiekt na wierzch",
                         "set draw order", "bring to front", "send to back",
                         "co ma byc na wierzchu", "put this behind that" },
        RequiresPlugin = true)]
    public static Task<DrawOrderResult> SetDrawOrder(IPluginGateway gw, DrawOrderArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<DrawOrderArgs, DrawOrderResult>(gw, "acad.geometry2d.set_draworder", args, T_NORMAL, ct);

    [McpTool("set_object_transparency", "Set per-object transparency as a PERCENTAGE, the way AutoCAD's Properties palette shows it: 0 is opaque, 90 is as see-through as AutoCAD allows. AutoCAD stores alpha internally, which is the INVERSE of that percentage, and the result reports both so a caller comparing against raw DXF data is not surprised. Transparency shows on screen but is IGNORED in plotted or exported output unless PLOTTRANSPARENCYOVERRIDE is 1, so a PNG that looks opaque does not mean this failed. byLayer and byBlock are NOT available: the managed API compiles them but throws eInvalidKey on assignment, measured across four entity types - the tool says so rather than quietly making the object opaque.", "geometry-2d",
        Intent = new[] { "ustaw przezroczystosc obiektu", "zrob obiekt polprzezroczysty",
                         "set object transparency", "make this 50% transparent",
                         "przezroczystosc wedlug warstwy", "see-through entity" },
        RequiresPlugin = true)]
    public static Task<TransparencyResult> SetObjectTransparency(IPluginGateway gw, TransparencyArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<TransparencyArgs, TransparencyResult>(gw, "acad.geometry2d.set_object_transparency", args, T_NORMAL, ct);

    [McpTool("create_wipeout", "Create a wipeout: a filled area that HIDES whatever is behind it, used to clear space for a label or a detail bubble over busy geometry. Takes at least 3 boundary points and closes the loop itself. Moved to the FRONT by default, because a wipeout behind what it should hide is invisible and looks exactly like a tool that did nothing - pass bringToFront=false to leave it where drawn. Its frame is controlled drawing-wide by set_wipeout_frame.", "geometry-2d",
        Intent = new[] { "zaslon fragment rysunku", "utworz wipeout",
                         "create a wipeout", "mask geometry behind a label",
                         "wyczysc miejsce pod opis", "hide what is underneath" },
        RequiresPlugin = true)]
    public static Task<WipeoutResult> CreateWipeout(IPluginGateway gw, WipeoutArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<WipeoutArgs, WipeoutResult>(gw, "acad.geometry2d.create_wipeout", args, T_NORMAL, ct);

    [McpTool("set_wipeout_frame", "Show or hide the outline around every wipeout in the drawing - the WIPEOUTFRAME system variable. 'hidden' removes it, 'shown' displays and plots it, and 'displayedNotPlotted' is what a real sheet usually wants: visible while you work, absent from the plot. Drawing-wide, so every wipeout changes together; this is not a per-entity property.", "geometry-2d",
        Intent = new[] { "ukryj ramke wipeout", "pokaz obramowanie zaslony",
                         "set wipeout frame", "hide wipeout borders",
                         "ramka wipeout nie ma sie drukowac", "wipeout outline visibility" },
        RequiresPlugin = true)]
    public static Task<WipeoutFrameResult> SetWipeoutFrame(IPluginGateway gw, WipeoutFrameArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<WipeoutFrameArgs, WipeoutFrameResult>(gw, "acad.geometry2d.set_wipeout_frame", args, T_FAST, ct);

    // ─────────── splines (roadmap 3.1) ───────────

    [McpTool("draw_spline_cv", "Draw a spline from CONTROL VERTICES - the vertices pull the curve without lying on it, except the first and last, which it touches. This is the other half of how AutoCAD models curves: draw_spline interpolates THROUGH given fit points, which is what you want when the curve must hit surveyed positions, while control vertices are what you want when the shape matters more than the points, as on a road centreline or a facade. degree defaults to 3 and must be less than the number of control points.", "geometry-2d",
        Intent = new[] { "narysuj splajn na wierzcholkach sterujacych", "krzywa sterowana punktami",
                         "draw a control vertex spline", "NURBS by control points",
                         "gladka krzywa nie przechodzaca przez punkty", "CV spline" },
        RequiresPlugin = true)]
    public static Task<SplineCvResult> DrawSplineCv(IPluginGateway gw, SplineCvArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<SplineCvArgs, SplineCvResult>(gw, "acad.geometry2d.draw_spline_cv", args, T_NORMAL, ct);

    [McpTool("edit_spline_fit_point", "Move one fit point of a spline, by 0-based index. A fit point is a position the curve MUST pass through, so moving one reshapes the curve either side of it rather than only at it. Only works on a spline that has fit data - one made from control vertices carries none, and the tool says so plainly instead of passing through an HRESULT that does not mention which kind of spline it met. Answers with the point as it was and as it now is, plus the curve's length before and after.", "geometry-2d",
        Intent = new[] { "przesun punkt splajnu", "zmien ksztalt krzywej przez punkt",
                         "edit a spline fit point", "move a point the spline passes through",
                         "popraw przebieg splajnu", "adjust spline shape" },
        RequiresPlugin = true)]
    public static Task<SplineFitPointResult> EditSplineFitPoint(IPluginGateway gw, SplineFitPointArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<SplineFitPointArgs, SplineFitPointResult>(gw, "acad.geometry2d.edit_spline_fit_point", args, T_NORMAL, ct);

    [McpTool("spline_to_polyline", "Convert a spline into a polyline, for export or for tools that cannot take a true curve. The conversion APPROXIMATES the spline with arc and line segments, so the length changes slightly and both values are reported - do not treat them as equal. The original spline is erased unless keepOriginal is true, in which case two entities end up overlapping and the caller has to decide which to keep.", "geometry-2d",
        Intent = new[] { "zamien splajn na polilinie", "konwersja krzywej na polilinie",
                         "convert a spline to a polyline", "spline to polyline for export",
                         "polilinia zamiast splajnu", "flatten a spline" },
        RequiresPlugin = true)]
    public static Task<SplineToPolylineResult> SplineToPolyline(IPluginGateway gw, SplineConvertArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<SplineConvertArgs, SplineToPolylineResult>(gw, "acad.geometry2d.spline_to_polyline", args, T_NORMAL, ct);

    // ─────────── lengthening and elliptical arcs (roadmap 3.1) ───────────

    [McpTool("lengthen_curve", "Make an open curve longer or shorter WITHOUT a boundary to stop at - AutoCAD's LENGTHEN, as distinct from extend_curve, which runs until it meets another entity. mode is 'delta' (add this much, or a negative amount to take off), 'total' (end up this long) or 'percent' (150 makes it half as long again). By default the END moves and the start stays put; pass atStart=true to move the other one. The length is re-measured afterwards and a mismatch is reported as a failure rather than echoing back the number that was asked for.", "geometry-2d",
        Intent = new[] { "wydluz linie o", "skroc krzywa", "lengthen a curve by",
                         "make this line 200 long", "przedluz odcinek bez granicy",
                         "shorten a polyline" },
        RequiresPlugin = true)]
    public static Task<LengthenResult> LengthenCurve(IPluginGateway gw, LengthenArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<LengthenArgs, LengthenResult>(gw, "acad.geometry2d.lengthen_curve", args, T_NORMAL, ct);

    [McpTool("draw_ellipse_arc", "Draw a PART of an ellipse - an elliptical arc, which draw_ellipse cannot do since it only makes the whole thing. Takes the centre, the END POINT of the major axis (not its length), the minor-to-major ratio, and a start and end angle. Those angles are ELLIPSE PARAMETERS measured from the major axis and equal true bearings only when ratio is 1; on a squashed ellipse the drawn end sits at a different bearing than the number given, which is AutoCAD's convention rather than an error.", "geometry-2d",
        Intent = new[] { "narysuj luk eliptyczny", "wycinek elipsy",
                         "draw an elliptical arc", "part of an ellipse",
                         "elipsa od kata do kata", "ellipse arc between angles" },
        RequiresPlugin = true)]
    public static Task<EllipseArcResult> DrawEllipseArc(IPluginGateway gw, EllipseArcArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<EllipseArcArgs, EllipseArcResult>(gw, "acad.geometry2d.draw_ellipse_arc", args, T_NORMAL, ct);

    // ─────────── boundaries from a point (roadmap 3.1) ───────────
    //
    // The tracing lives in the hatches category, where KNOWN-GAPS A1 was solved: the seed is
    // taken to the current UCS and the drawing is framed so the region is on screen, because
    // TraceBoundary silently finds nothing otherwise. These two share that code rather than
    // rediscovering both traps.

    [McpTool("boundary_from_point", "Point at an enclosed area and get its OUTLINE as a real polyline - AutoCAD's BOUNDARY. The point is a WCS position inside the area; the geometry that encloses it is left untouched, so the new outline sits on top of it. With detectIslands on (the default) an enclosed hole produces its own boundary too. Fails with the seed reported in both WCS and the current UCS when no closed region is found, since a UCS that is not world is the usual reason a point that looks inside is not.", "geometry-2d",
        Intent = new[] { "utworz obrys obszaru", "polilinia z zamknietego obszaru",
                         "boundary from a point", "trace the outline of an area",
                         "obwiednia wskazanego obszaru", "get the outline around this point" },
        RequiresPlugin = true)]
    public static Task<BoundaryResult> BoundaryFromPoint(IPluginGateway gw, BoundaryArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<BoundaryArgs, BoundaryResult>(gw, "acad.geometry2d.boundary_from_point", args, T_SLOW, ct);

    [McpTool("region_from_boundary", "Point at an enclosed area and get a REGION - a filled 2D area with a real area value, not just an outline. Reports area and perimeter, and the result can be combined with union_regions, subtract_regions and intersect_regions in acad-boolean-ops. The traced curves are not left behind. Use boundary_from_point when a polyline outline is what you actually want.", "geometry-2d",
        Intent = new[] { "utworz region z obszaru", "zamien zamkniety obszar na region",
                         "region from a boundary", "make a region from this area",
                         "pole powierzchni obszaru jako region", "region for boolean operations" },
        RequiresPlugin = true)]
    public static Task<RegionFromBoundaryResult> RegionFromBoundary(IPluginGateway gw, BoundaryArgs args, CancellationToken ct)
        => Geometry2dProxy.CallAsync<BoundaryArgs, RegionFromBoundaryResult>(gw, "acad.geometry2d.region_from_boundary", args, T_SLOW, ct);
}
