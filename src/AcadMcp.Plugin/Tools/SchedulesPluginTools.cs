// Plugin handlers for acad-schedules: TableStyle presets, room-label
// enumeration and schedule-table discovery. These primitives are consumed by
// backend composite tools under Categories/Schedules/.
//
// Rules: 11 (transactions), 12 (error mapping), 19 (impl pattern), 27 (text
// and table traps — GenerateLayout MUST be called before Append, we do not
// touch that here because we only create/modify TableStyle, not Table).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using AcadMcp.Shared.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class SchedulesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.schedules.ensure_table_style",     EnsureTableStyle);
        host.Register("acad.schedules.list_table_styles",      ListTableStyles);
        host.Register("acad.schedules.list_room_labels",       ListRoomLabels);
        host.Register("acad.schedules.get_room_region",        GetRoomRegion);
        host.Register("acad.schedules.find_schedule_tables",   FindScheduleTables);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> RunW(string tk, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(tk, ct, work);

    private static Task<ToolDispatchResult> RunR(string tk, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunReadAsync(tk, ct, work);

    // ─────────── ensure_table_style ───────────
    //
    // Creates or updates a named TableStyle in the drawing's TableStyleDictionary.
    // Sets title/header/data row text heights and optional ACI fill colors. Used
    // by the HOSPITAL-DEF / OFFICE-DEF presets defined in the Backend palette.

    private static Task<ToolDispatchResult> EnsureTableStyle(JsonObject args, CancellationToken ct) =>
        RunW("acad.schedules.ensure_table_style", args, ct, (doc, db, tr) =>
        {
            var a = Read<EnsureTableStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name required.");

            var dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
            var tsTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            ObjectId textStyleId = tsTable.Has(a.TextStyle) ? tsTable[a.TextStyle] : tsTable["Standard"];

            TableStyle style;
            bool created = false, updated = false;

            if (dict.Contains(a.Name))
            {
                style = (TableStyle)tr.GetObject((ObjectId)dict.GetAt(a.Name), OpenMode.ForWrite);
                updated = true;
            }
            else
            {
                dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForWrite);
                style = new TableStyle();
                dict.SetAt(a.Name, style);
                tr.AddNewlyCreatedDBObject(style, true);
                created = true;
            }

            ConfigureStyle(style, a, textStyleId);

            bool madeCurrent = false;
            if (a.MakeCurrent)
            {
                db.Tablestyle = style.ObjectId;
                madeCurrent = true;
            }

            return Wrap(new EnsureTableStyleResultDto(a.Name, created, updated && !created, madeCurrent));
        });

    private static void ConfigureStyle(TableStyle style, EnsureTableStyleArgsDto a, ObjectId textStyleId)
    {
        // AutoCAD 2018+ uses cell-style names as strings. Default built-in names are
        // "_TITLE" / "_HEADER" / "_DATA". Any fill colors below 1 are treated as "skip".
        SetRow(style, "_TITLE",  a.TitleTextHeight,  textStyleId, a.TitleFillAci);
        SetRow(style, "_HEADER", a.HeaderTextHeight, textStyleId, a.HeaderFillAci);
        SetRow(style, "_DATA",   a.BodyTextHeight,   textStyleId, 0);
    }

    private static void SetRow(TableStyle style, string cellStyleName, double height, ObjectId textStyleId, int fillAci)
    {
        style.SetTextHeight(height, cellStyleName);
        style.SetTextStyle(textStyleId, cellStyleName);
        if (fillAci > 0 && fillAci < 256)
        {
            // SetBackgroundColor in 2018+ takes (Color, int rowTypesMask). Translate the
            // cell-style name to the legacy row-type bitmask so we can apply a fill color.
            int mask = cellStyleName switch
            {
                "_TITLE"  => 1, // TitleRow
                "_HEADER" => 2, // HeaderRow
                _         => 4, // DataRow
            };
            try { style.SetBackgroundColor(Color.FromColorIndex(ColorMethod.ByAci, (short)fillAci), mask); }
            catch { /* fill color is optional, don't fail style creation */ }
        }
    }

    // ─────────── list_table_styles ───────────

    private static Task<ToolDispatchResult> ListTableStyles(JsonObject args, CancellationToken ct) =>
        RunR("acad.schedules.list_table_styles", args, ct, (doc, db, tr) =>
        {
            var dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
            var names = new List<string>();
            foreach (var entry in dict)
            {
                names.Add(entry.Key);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return Wrap(new ListTableStylesResultDto(names));
        });

    // ─────────── list_room_labels ───────────
    //
    // Enumerates DBText / MText entities on the specified layers (default
    // A-ROOM-IDEN) and, if boundaryLayer is set, computes each room's area
    // from the enclosing closed polyline on that layer. Used by
    // generate_room_schedule to populate a Pomieszczenia table.

    private static Task<ToolDispatchResult> ListRoomLabels(JsonObject args, CancellationToken ct) =>
        RunR("acad.schedules.list_room_labels", args, ct, (doc, db, tr) =>
        {
            var a = Read<ListRoomLabelsArgsDto>(args);
            // allLayers (or an explicit "*" layer) => accept text on any layer.
            bool allLayers = a.AllLayers
                || (a.LabelLayers is { Count: > 0 } && a.LabelLayers.Contains("*"));
            var layers = a.LabelLayers is { Count: > 0 }
                ? new HashSet<string>(a.LabelLayers, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-ROOM-IDEN", "A-ANNO-ROOM" };

            // Collect candidate closed-polyline boundaries. With an explicit boundaryLayer, restrict to
            // it; otherwise (allLayers, or no boundaryLayer) treat every closed polyline as a candidate.
            bool anyBoundaryLayer = string.IsNullOrWhiteSpace(a.BoundaryLayer);
            var boundaries = new List<(Polyline p, double area)>();
            if (!anyBoundaryLayer || allLayers)
            {
                foreach (var pl in EnumerateModelSpace<Polyline>(db, tr))
                {
                    if (!anyBoundaryLayer &&
                        !string.Equals(pl.Layer, a.BoundaryLayer, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!pl.Closed) continue;
                    double area = 0.0;
                    try { area = pl.Area; } catch { continue; }
                    if (area <= 0.0) continue;
                    boundaries.Add((pl, area));
                }
            }

            // Universal fallback: walls are usually lines/polyline edges, not one closed loop per room.
            // Collect all such segments so we can fit a room rectangle around a label by ray-casting.
            var wallSegs = CollectWallSegments(db, tr);

            var rooms = new List<RoomLabelDto>();
            foreach (var ent in EnumerateModelSpace<Entity>(db, tr))
            {
                if (!allLayers && !layers.Contains(ent.Layer)) continue;
                string? text = null;
                Point2dDto? pos = null;
                double textHeight = 0.0;
                string? kind = null;
                if (ent is DBText dbt) { text = dbt.TextString; pos = new Point2dDto(dbt.Position.X, dbt.Position.Y); textHeight = dbt.Height; kind = "dbtext"; }
                else if (ent is MText mt) { text = mt.Text; pos = new Point2dDto(mt.Location.X, mt.Location.Y); textHeight = mt.TextHeight; kind = "mtext"; }
                if (text is null || pos is null) continue;

                var resolved = ResolveBoundary(pos.X, pos.Y, boundaries, wallSegs, ParseAreaFromText(text));

                rooms.Add(new RoomLabelDto(
                    AcadEnv.ToHandle(ent).Handle,
                    text,
                    ent.Layer,
                    pos,
                    textHeight,
                    resolved?.AreaM2,
                    kind,
                    resolved?.MinX, resolved?.MinY, resolved?.MaxX, resolved?.MaxY));
            }
            rooms.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Text, y.Text));
            return Wrap(new ListRoomLabelsResultDto(rooms, rooms.Count));
        });

    // ─────────── get_room_region ───────────
    //
    // Universal room-boundary detector. Rasterizes wall geometry, seals door/window openings and
    // flood-fills the cell containing (x,y). Falls back to wall ray-casting and then to the smallest
    // closed polyline. Returns the measured area, bbox and a traced outline polygon.

    private static Task<ToolDispatchResult> GetRoomRegion(JsonObject args, CancellationToken ct) =>
        RunR("acad.schedules.get_room_region", args, ct, (doc, db, tr) =>
        {
            var a = Read<GetRoomRegionArgsDto>(args);
            var walls = CollectWallSegments(db, tr);

            // 1) Wall-aware flood-fill (handles non-rectangular rooms; sealed at door/window blocks).
            var wsegs = new List<WallSeg>(walls.Count);
            foreach (var s in walls) wsegs.Add(new WallSeg(s.Ax, s.Ay, s.Bx, s.By));
            var openings = CollectOpeningSeeds(db, tr, a.X, a.Y, a.SealAllDoors);
            var region = RoomRegionSolver.SolveFlood(wsegs, openings, a.X, a.Y, a.CellMm ?? 0.0,
                labelAreaM2: a.LabelAreaM2);
            if (region is not null)
            {
                var outline = new List<OutlinePointDto>(region.Outline.Count);
                foreach (var p in region.Outline) outline.Add(new OutlinePointDto(p.X, p.Y));
                return Wrap(new RoomRegionResultDto(true, "flood", region.AreaMm2 / 1_000_000.0,
                    region.MinX, region.MinY, region.MaxX, region.MaxY, outline));
            }

            // 2) Ray-cast rectangle on the filtered walls (open/leaky region).
            var rect = RoomRectFromWalls(a.X, a.Y, walls);
            if (rect is not null)
                return Wrap(new RoomRegionResultDto(true, "raycast", rect.AreaM2,
                    rect.MinX, rect.MinY, rect.MaxX, rect.MaxY, RectOutline(rect)));

            // 3) Smallest closed polyline that contains the point.
            var polys = CollectClosedPolylines(db, tr);
            var byPoly = ResolveBoundary(a.X, a.Y, polys, walls, null);
            if (byPoly is not null)
                return Wrap(new RoomRegionResultDto(true, "polyline", byPoly.AreaM2,
                    byPoly.MinX, byPoly.MinY, byPoly.MaxX, byPoly.MaxY, RectOutline(byPoly)));

            return Wrap(new RoomRegionResultDto(false, "none", null, null, null, null, null,
                Array.Empty<OutlinePointDto>()));
        });

    private static List<OutlinePointDto> RectOutline(ResolvedBounds b) => new()
    {
        new OutlinePointDto(b.MinX, b.MinY),
        new OutlinePointDto(b.MaxX, b.MinY),
        new OutlinePointDto(b.MaxX, b.MaxY),
        new OutlinePointDto(b.MinX, b.MaxY),
    };

    private static bool PointInsidePolygonBbox(Polyline pl, Point3d p)
    {
        // Bbox pre-filter so we avoid the polygon-in-polygon classifier on every label.
        try
        {
            var e = pl.GeometricExtents;
            if (p.X < e.MinPoint.X || p.X > e.MaxPoint.X) return false;
            if (p.Y < e.MinPoint.Y || p.Y > e.MaxPoint.Y) return false;
        }
        catch { return false; }
        return RayCastInside(pl, p);
    }

    private static bool RayCastInside(Polyline pl, Point3d p)
    {
        int cross = 0;
        int n = pl.NumberOfVertices;
        for (int i = 0; i < n; i++)
        {
            var a = pl.GetPoint2dAt(i);
            var b = pl.GetPoint2dAt((i + 1) % n);
            if ((a.Y > p.Y) != (b.Y > p.Y))
            {
                double t = (p.Y - a.Y) / (b.Y - a.Y);
                double xCross = a.X + t * (b.X - a.X);
                if (p.X < xCross) cross++;
            }
        }
        return (cross & 1) == 1;
    }

    // ─────────── universal boundary resolution ───────────

    private sealed record Seg(double Ax, double Ay, double Bx, double By);

    /// <summary>Result of fitting a boundary to a label point.</summary>
    private sealed record ResolvedBounds(double MinX, double MinY, double MaxX, double MaxY, double AreaM2);

    // A label-to-wall ray longer than this (mm) is treated as "no wall" (open side / gap).
    private const double MaxWallReachMm = 60_000.0;

    private static readonly string[] BoundaryTokens =
        { "WALL", "GLAZ", "MUR", "SCIAN", "ŚCIAN", "PARTITION", "CURTAIN", "FACAD", "ELEW" };

    private static readonly string[] NoiseTokens =
        { "GRID", "AXIS", "OSIE", "DIM", "ANNO", "TEXT", "IDEN", "NOTE", "SYMB", "LEGN",
          "HATCH", "FURN", "PLMB", "EQPM", "AREA", "DEFPOINTS", "STRS", "RAMP", "DETL", "VPRT" };

    /// <summary>True when a layer name looks like a wall / glazing / partition boundary.</summary>
    private static bool IsBoundaryLayer(string layer)
    {
        if (string.IsNullOrEmpty(layer)) return false;
        foreach (var t in BoundaryTokens)
            if (layer.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    /// <summary>True when a layer name is annotation / grid / furniture noise (never a room boundary).</summary>
    private static bool IsNoiseLayer(string layer)
    {
        if (string.IsNullOrEmpty(layer)) return true;
        foreach (var t in NoiseTokens)
            if (layer.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    /// <summary>
    /// Collect wall segments (Lines + Polyline edges). Prefer geometry on boundary layers
    /// (WALL/GLAZ/…); if too few are found, fall back to every non-noise segment so the solver
    /// still has something to enclose with. Construction grid (S-GRID) etc. is excluded as noise.
    /// </summary>
    private static List<Seg> CollectWallSegments(Database db, Transaction tr)
    {
        var boundary = new List<Seg>();
        var nonNoise = new List<Seg>();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            var obj = tr.GetObject(id, OpenMode.ForRead);
            if (obj is not Entity ent) continue;
            bool isBoundary = IsBoundaryLayer(ent.Layer);
            bool isNoise = IsNoiseLayer(ent.Layer);
            if (obj is Line ln)
            {
                var s = new Seg(ln.StartPoint.X, ln.StartPoint.Y, ln.EndPoint.X, ln.EndPoint.Y);
                if (isBoundary) boundary.Add(s);
                if (!isNoise) nonNoise.Add(s);
            }
            else if (obj is Polyline pl)
            {
                int n = pl.NumberOfVertices;
                int last = pl.Closed ? n : n - 1;
                for (int i = 0; i < last; i++)
                {
                    var a = pl.GetPoint2dAt(i);
                    var b = pl.GetPoint2dAt((i + 1) % n);
                    var s = new Seg(a.X, a.Y, b.X, b.Y);
                    if (isBoundary) boundary.Add(s);
                    if (!isNoise) nonNoise.Add(s);
                }
            }
        }
        return boundary.Count >= 8 ? boundary : nonNoise;
    }

    /// <summary>
    /// Collect door/window block insertion points (with an estimated opening width) so the flood-fill
    /// can seal wall gaps. When sealAllDoors is true every opening in model space is sealed; otherwise
    /// only those within sealRadiusMm of (seedX, seedY).
    /// </summary>
    private static List<OpeningSeed> CollectOpeningSeeds(Database db, Transaction tr,
        double seedX, double seedY, bool sealAllDoors, double sealRadiusMm = 45_000.0)
    {
        var seeds = new List<OpeningSeed>();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
            string layer = br.Layer ?? string.Empty;
            string name = br.Name ?? string.Empty;
            bool isOpening =
                layer.IndexOf("DOOR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                layer.IndexOf("GLAZ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                layer.IndexOf("WIN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("DOOR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("WIN", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isOpening) continue;
            if (!sealAllDoors)
            {
                double dx = br.Position.X - seedX, dy = br.Position.Y - seedY;
                if (dx * dx + dy * dy > sealRadiusMm * sealRadiusMm) continue;
            }
            double width = 1200.0;
            try
            {
                var e = br.GeometricExtents;
                width = Math.Max(e.MaxPoint.X - e.MinPoint.X, e.MaxPoint.Y - e.MinPoint.Y);
                if (width <= 1.0 || width > 5000.0) width = 1200.0;
            }
            catch { /* extents unavailable for some anonymous/dynamic blocks */ }
            seeds.Add(new OpeningSeed(br.Position.X, br.Position.Y, width));
        }
        return seeds;
    }

    /// <summary>Collect closed polylines (excluding noise layers) as a last-resort room boundary set.</summary>
    private static List<(Polyline p, double area)> CollectClosedPolylines(Database db, Transaction tr)
    {
        var polys = new List<(Polyline, double)>();
        foreach (var pl in EnumerateModelSpace<Polyline>(db, tr))
        {
            if (!pl.Closed || IsNoiseLayer(pl.Layer)) continue;
            double area;
            try { area = pl.Area; } catch { continue; }
            if (area <= 0.0) continue;
            polys.Add((pl, area));
        }
        return polys;
    }

    /// <summary>
    /// Choose the best-fitting boundary for a label: prefer the closed polyline / wall-rectangle whose
    /// area is closest to the label's stated area (when present), otherwise the tightest enclosing one.
    /// </summary>
    private static ResolvedBounds? ResolveBoundary(
        double px, double py, List<(Polyline p, double area)> polys, List<Seg> walls, double? textAreaM2)
    {
        var candidates = new List<ResolvedBounds>();

        // 1) Smallest closed polyline that truly contains the label point.
        var pWorld = new Point3d(px, py, 0.0);
        foreach (var b in polys)
        {
            if (!PointInsidePolygonBbox(b.p, pWorld)) continue;
            try
            {
                var e = b.p.GeometricExtents;
                candidates.Add(new ResolvedBounds(e.MinPoint.X, e.MinPoint.Y, e.MaxPoint.X, e.MaxPoint.Y, b.area / 1_000_000.0));
            }
            catch { /* degenerate */ }
        }

        // 2) Wall-rectangle from ray-casting (handles rooms bounded by wall lines, not a closed loop).
        var rect = RoomRectFromWalls(px, py, walls);
        if (rect is not null) candidates.Add(rect);

        if (candidates.Count == 0) return null;

        // Pick: closest to the stated area when known; otherwise the tightest (smallest) enclosing box.
        if (textAreaM2 is { } target && target > 0)
            return candidates.OrderBy(c => Math.Abs(c.AreaM2 - target) / target).First();
        return candidates.OrderBy(c => c.AreaM2).First();
    }

    /// <summary>
    /// Fit a room rectangle around a point by casting axis rays to the nearest wall in each direction.
    /// A small fan per direction bridges doorway gaps (picks the closest real wall). Null if any side is open.
    /// </summary>
    private static ResolvedBounds? RoomRectFromWalls(double px, double py, List<Seg> walls)
    {
        if (walls.Count == 0) return null;
        const double fan = 800.0; // mm lateral offset to dodge door openings

        double right = Math.Min(Math.Min(NearestRight(walls, px, py), NearestRight(walls, px, py + fan)), NearestRight(walls, px, py - fan));
        double left  = Math.Min(Math.Min(NearestLeft(walls, px, py),  NearestLeft(walls, px, py + fan)),  NearestLeft(walls, px, py - fan));
        double up    = Math.Min(Math.Min(NearestUp(walls, px, py),    NearestUp(walls, px + fan, py)),    NearestUp(walls, px - fan, py));
        double down  = Math.Min(Math.Min(NearestDown(walls, px, py),  NearestDown(walls, px + fan, py)),  NearestDown(walls, px - fan, py));

        if (right >= MaxWallReachMm || left >= MaxWallReachMm || up >= MaxWallReachMm || down >= MaxWallReachMm)
            return null;

        double minX = px - left, maxX = px + right, minY = py - down, maxY = py + up;
        double w = maxX - minX, h = maxY - minY;
        if (w <= 1.0 || h <= 1.0) return null;
        return new ResolvedBounds(minX, minY, maxX, maxY, w * h / 1_000_000.0);
    }

    private static double NearestRight(List<Seg> segs, double px, double py)
    {
        double best = double.PositiveInfinity;
        foreach (var s in segs)
        {
            if ((s.Ay > py) == (s.By > py)) continue;       // segment does not cross horizontal line y=py
            double t = (py - s.Ay) / (s.By - s.Ay);
            double x = s.Ax + t * (s.Bx - s.Ax);
            double d = x - px;
            if (d > 1.0 && d < best) best = d;
        }
        return best;
    }

    private static double NearestLeft(List<Seg> segs, double px, double py)
    {
        double best = double.PositiveInfinity;
        foreach (var s in segs)
        {
            if ((s.Ay > py) == (s.By > py)) continue;
            double t = (py - s.Ay) / (s.By - s.Ay);
            double x = s.Ax + t * (s.Bx - s.Ax);
            double d = px - x;
            if (d > 1.0 && d < best) best = d;
        }
        return best;
    }

    private static double NearestUp(List<Seg> segs, double px, double py)
    {
        double best = double.PositiveInfinity;
        foreach (var s in segs)
        {
            if ((s.Ax > px) == (s.Bx > px)) continue;       // segment does not cross vertical line x=px
            double t = (px - s.Ax) / (s.Bx - s.Ax);
            double y = s.Ay + t * (s.By - s.Ay);
            double d = y - py;
            if (d > 1.0 && d < best) best = d;
        }
        return best;
    }

    private static double NearestDown(List<Seg> segs, double px, double py)
    {
        double best = double.PositiveInfinity;
        foreach (var s in segs)
        {
            if ((s.Ax > px) == (s.Bx > px)) continue;
            double t = (px - s.Ax) / (s.Bx - s.Ax);
            double y = s.Ay + t * (s.By - s.Ay);
            double d = py - y;
            if (d > 1.0 && d < best) best = d;
        }
        return best;
    }

    /// <summary>Parse an area in m² from a room label such as "200 m²" or "45,5 m2". Null if none.</summary>
    private static double? ParseAreaFromText(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            text, @"(\d+(?:[.,]\d+)?)\s*m(?:²|2|\^2)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var raw = m.Groups[1].Value.Replace(',', '.');
        return double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    // ─────────── find_schedule_tables ───────────

    private static Task<ToolDispatchResult> FindScheduleTables(JsonObject args, CancellationToken ct) =>
        RunR("acad.schedules.find_schedule_tables", args, ct, (doc, db, tr) =>
        {
            var a = Read<FindScheduleTablesArgsDto>(args);
            var tables = new List<ScheduleTableDto>();
            foreach (var t in EnumerateModelSpace<Table>(db, tr))
            {
                if (!string.IsNullOrWhiteSpace(a.LayerFilter) &&
                    !string.Equals(t.Layer, a.LayerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                string title = "";
                try { title = t.Cells[0, 0].TextString ?? ""; } catch { }
                if (!string.IsNullOrWhiteSpace(a.TitleContains) &&
                    title.IndexOf(a.TitleContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                tables.Add(new ScheduleTableDto(
                    AcadEnv.ToHandle(t).Handle,
                    title,
                    t.Rows.Count,
                    t.Columns.Count,
                    t.Layer,
                    new Point2dDto(t.Position.X, t.Position.Y)));
            }
            return Wrap(new FindScheduleTablesResultDto(tables, tables.Count));
        });

    // ─────────── helpers ───────────

    private static IEnumerable<T> EnumerateModelSpace<T>(Database db, Transaction tr) where T : Entity
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is T ent) yield return ent;
        }
    }
}
