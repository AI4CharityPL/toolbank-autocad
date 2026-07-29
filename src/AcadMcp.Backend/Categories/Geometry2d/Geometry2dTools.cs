// AutoCAD acad-geometry-2d category. ~30 tools covering 2D primitive creation,
// queries and modifications. Each method is a thin proxy through IPluginGateway
// to the matching plugin handler under the key "acad.geometry2d.<verb>".
//
// Rules: 19-tool-implementation-pattern.mdc, 20..25 (tool authoring).
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
}
