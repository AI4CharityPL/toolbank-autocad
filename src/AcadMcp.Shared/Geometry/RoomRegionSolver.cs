// Pure (AutoCAD-free) geometry: determine the enclosed region of a room from wall segments by
// rasterizing the walls, sealing door/window openings, and flood-filling from the label point.
// Lives in Shared so both the Plugin (which feeds AutoCAD geometry) and unit tests can use it.
// Coordinates are in drawing units (millimetres in our DWGs); all logic is plain arithmetic.

using System;
using System.Collections.Generic;

namespace AcadMcp.Shared.Geometry;

/// <summary>A 2D point (drawing units).</summary>
public readonly struct PointXY
{
    public PointXY(double x, double y) { X = x; Y = y; }
    public double X { get; }
    public double Y { get; }
}

/// <summary>A wall segment (drawing units).</summary>
public readonly struct WallSeg
{
    public WallSeg(double ax, double ay, double bx, double by) { Ax = ax; Ay = ay; Bx = bx; By = by; }
    public double Ax { get; }
    public double Ay { get; }
    public double Bx { get; }
    public double By { get; }
}

/// <summary>A door/window centre used to seal the wall opening so the flood does not leak through.</summary>
public readonly struct OpeningSeed
{
    public OpeningSeed(double x, double y, double widthMm) { X = x; Y = y; WidthMm = widthMm; }
    public double X { get; }
    public double Y { get; }
    public double WidthMm { get; }
}

/// <summary>Result of a successful flood-fill: measured area, bbox and a traced outline polygon.</summary>
public sealed class RoomRegion
{
    public RoomRegion(double areaMm2, double minX, double minY, double maxX, double maxY, IReadOnlyList<PointXY> outline)
    {
        AreaMm2 = areaMm2;
        MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        Outline = outline;
    }

    public double AreaMm2 { get; }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public IReadOnlyList<PointXY> Outline { get; }
}

public static class RoomRegionSolver
{
    /// <summary>
    /// Flood-fill the room that contains (seedX, seedY). Returns null when the region escapes to the
    /// drawing bounds (open plan / unsealed gap) or is degenerate — the caller should then fall back.
    /// </summary>
    /// <param name="walls">Wall/boundary segments (already filtered to wall-ish layers by the caller).</param>
    /// <param name="openings">Door/window centres + widths to seal so the fill stays inside the room.</param>
    /// <param name="cellMm">Raster cell size; pass 0 for automatic sizing.</param>
    /// <param name="maxCellsPerSide">Upper bound on grid resolution per axis (memory guard).</param>
    /// <param name="labelAreaM2">When set, reject flood results &gt; 3× the labelled area (corridor leak heuristic).</param>
    public static RoomRegion? SolveFlood(
        IReadOnlyList<WallSeg> walls,
        IReadOnlyList<OpeningSeed> openings,
        double seedX, double seedY,
        double cellMm = 0,
        int maxCellsPerSide = 1000,
        double? labelAreaM2 = null)
    {
        if (walls == null || walls.Count == 0) return null;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var w in walls)
        {
            minX = Math.Min(minX, Math.Min(w.Ax, w.Bx));
            minY = Math.Min(minY, Math.Min(w.Ay, w.By));
            maxX = Math.Max(maxX, Math.Max(w.Ax, w.Bx));
            maxY = Math.Max(maxY, Math.Max(w.Ay, w.By));
        }
        double extentX = maxX - minX, extentY = maxY - minY;
        if (extentX <= 0 || extentY <= 0) return null;
        // Seed must lie inside the wall bounding box.
        if (seedX < minX || seedX > maxX || seedY < minY || seedY > maxY) return null;

        double maxExtent = Math.Max(extentX, extentY);
        double cell = cellMm > 0 ? cellMm : Clamp(maxExtent / 600.0, 50.0, 500.0);
        // Keep the grid within the memory guard by enlarging the cell if needed.
        while (extentX / cell > maxCellsPerSide || extentY / cell > maxCellsPerSide) cell *= 1.5;

        // One-cell padding ring so "filled touches border" reliably detects an open region.
        double originX = minX - cell;
        double originY = minY - cell;
        int nx = (int)Math.Ceiling(extentX / cell) + 3;
        int ny = (int)Math.Ceiling(extentY / cell) + 3;

        var blocked = new bool[nx * ny];

        // Rasterize walls (sampled along each segment).
        foreach (var w in walls)
        {
            double len = Math.Sqrt((w.Bx - w.Ax) * (w.Bx - w.Ax) + (w.By - w.Ay) * (w.By - w.Ay));
            int steps = (int)Math.Ceiling(len / (cell * 0.5)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                double t = steps == 0 ? 0 : (double)i / steps;
                Mark(blocked, nx, ny, Gx(w.Ax + t * (w.Bx - w.Ax), originX, cell), Gy(w.Ay + t * (w.By - w.Ay), originY, cell));
            }
        }

        // Seal openings: block a small disc so the fill cannot leak through a doorway / window gap.
        if (openings != null)
        {
            foreach (var o in openings)
            {
                double r = Math.Max(o.WidthMm * 0.5 + 1.5 * cell, 2.0 * cell);
                int rc = (int)Math.Ceiling(r / cell);
                int cx = Gx(o.X, originX, cell), cy = Gy(o.Y, originY, cell);
                for (int dy = -rc; dy <= rc; dy++)
                    for (int dx = -rc; dx <= rc; dx++)
                        if (dx * dx + dy * dy <= rc * rc) Mark(blocked, nx, ny, cx + dx, cy + dy);
            }
        }

        int sx = Gx(seedX, originX, cell), sy = Gy(seedY, originY, cell);
        if (!InBounds(nx, ny, sx, sy)) return null;
        // If the label happens to fall on a wall/seal cell, nudge to the nearest free cell.
        if (blocked[sy * nx + sx] && !FindFreeNear(blocked, nx, ny, ref sx, ref sy, 4)) return null;

        // BFS flood fill (4-connectivity). Abort if it reaches the padded border ring.
        var visited = new bool[nx * ny];
        var queue = new Queue<int>();
        int start = sy * nx + sx;
        visited[start] = true;
        queue.Enqueue(start);
        long filled = 0;
        int fMinX = sx, fMinY = sy, fMaxX = sx, fMaxY = sy;
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x = idx % nx, y = idx / nx;
            filled++;
            if (x < fMinX) fMinX = x; if (x > fMaxX) fMaxX = x;
            if (y < fMinY) fMinY = y; if (y > fMaxY) fMaxY = y;
            // Reached the outer ring => region is open; bail out for fallback.
            if (x == 0 || y == 0 || x == nx - 1 || y == ny - 1) return null;
            // Early abort when the fill already exceeds 3× the labelled area (corridor leak).
            if (labelAreaM2 is { } laEarly && laEarly > 0 && (filled % 512) == 0)
            {
                double areaM2Early = filled * cell * cell / 1_000_000.0;
                if (areaM2Early > laEarly * 3.0) return null;
            }
            Enqueue(blocked, visited, queue, nx, ny, x + 1, y);
            Enqueue(blocked, visited, queue, nx, ny, x - 1, y);
            Enqueue(blocked, visited, queue, nx, ny, x, y + 1);
            Enqueue(blocked, visited, queue, nx, ny, x, y - 1);
        }

        if (filled < 4) return null;
        double areaMm2 = filled * cell * cell;
        double areaM2 = areaMm2 / 1_000_000.0;
        // Reject obvious corridor leaks when the label states a much smaller area.
        if (labelAreaM2 is { } la && la > 0 && areaM2 > la * 3.0) return null;

        var outline = TraceOutline(visited, nx, ny, originX, originY, cell);
        if (outline.Count < 4) return null;

        return new RoomRegion(
            areaMm2,
            originX + fMinX * cell, originY + fMinY * cell,
            originX + (fMaxX + 1) * cell, originY + (fMaxY + 1) * cell,
            outline);
    }

    /// <summary>Ray-cast even-odd point-in-polygon test (for membership checks by callers).</summary>
    public static bool PointInPolygon(IReadOnlyList<PointXY> poly, double x, double y)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double yi = poly[i].Y, yj = poly[j].Y, xi = poly[i].X, xj = poly[j].X;
            if ((yi > y) != (yj > y))
            {
                double xCross = xi + (y - yi) / (yj - yi) * (xj - xi);
                if (x < xCross) inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>True when (x,y) lies inside the polygon or within marginMm of any edge (for openings on walls).</summary>
    public static bool InsideOrNearBoundary(IReadOnlyList<PointXY> poly, double x, double y, double marginMm)
    {
        if (poly.Count < 3) return false;
        if (PointInPolygon(poly, x, y)) return true;
        if (marginMm <= 0) return false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (DistancePointToSegment(x, y, poly[j].X, poly[j].Y, poly[i].X, poly[i].Y) <= marginMm)
                return true;
        }
        return false;
    }

    private static double DistancePointToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-12) return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        double t = Math.Max(0.0, Math.Min(1.0, ((px - ax) * dx + (py - ay) * dy) / lenSq));
        double qx = ax + t * dx, qy = ay + t * dy;
        return Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
    }

    // ─────────── internals ───────────

    private static void Enqueue(bool[] blocked, bool[] visited, Queue<int> q, int nx, int ny, int x, int y)
    {
        if (!InBounds(nx, ny, x, y)) return;
        int idx = y * nx + x;
        if (visited[idx] || blocked[idx]) return;
        visited[idx] = true;
        q.Enqueue(idx);
    }

    private static bool FindFreeNear(bool[] blocked, int nx, int ny, ref int sx, ref int sy, int radius)
    {
        for (int r = 1; r <= radius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = sx + dx, y = sy + dy;
                    if (InBounds(nx, ny, x, y) && !blocked[y * nx + x]) { sx = x; sy = y; return true; }
                }
        return false;
    }

    /// <summary>
    /// Trace the outer boundary of the filled region as a rectilinear polygon by emitting CCW
    /// directed unit edges (interior on the left) and stitching them into the largest loop.
    /// </summary>
    private static List<PointXY> TraceOutline(bool[] filled, int nx, int ny, double originX, double originY, double cell)
    {
        // Corner coordinates range 0..nx and 0..ny, so the key base must exceed nx.
        int baseN = nx + 2;
        // Directed boundary edges keyed by start corner -> end corner (grid-corner integer coords).
        var edges = new Dictionary<long, List<long>>();
        void AddEdge(int x0, int y0, int x1, int y1)
        {
            long k = Key(x0, y0, baseN);
            if (!edges.TryGetValue(k, out var list)) { list = new List<long>(1); edges[k] = list; }
            list.Add(Key(x1, y1, baseN));
        }

        for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                if (!filled[y * nx + x]) continue;
                // For each empty 4-neighbor, emit the shared edge oriented CCW (interior on the left).
                if (!IsFilled(filled, nx, ny, x, y - 1)) AddEdge(x, y, x + 1, y);         // bottom
                if (!IsFilled(filled, nx, ny, x + 1, y)) AddEdge(x + 1, y, x + 1, y + 1); // right
                if (!IsFilled(filled, nx, ny, x, y + 1)) AddEdge(x + 1, y + 1, x, y + 1); // top
                if (!IsFilled(filled, nx, ny, x - 1, y)) AddEdge(x, y + 1, x, y);         // left
            }

        var best = new List<PointXY>();
        double bestArea = 0;
        var visitedStart = new HashSet<long>();
        foreach (var kv in edges)
        {
            if (visitedStart.Contains(kv.Key)) continue;
            var loopCorners = WalkLoop(edges, kv.Key, visitedStart);
            if (loopCorners.Count < 4) continue;
            var poly = SimplifyToWorld(loopCorners, baseN, originX, originY, cell);
            double area = Math.Abs(SignedArea(poly));
            if (area > bestArea) { bestArea = area; best = poly; }
        }
        return best;
    }

    private static List<long> WalkLoop(Dictionary<long, List<long>> edges, long startKey, HashSet<long> visitedStart)
    {
        var corners = new List<long>();
        long cur = startKey;
        int guard = 0, maxSteps = edges.Count * 4 + 16;
        while (guard++ < maxSteps)
        {
            corners.Add(cur);
            visitedStart.Add(cur);
            if (!edges.TryGetValue(cur, out var outs) || outs.Count == 0) break;
            long next = outs[0];
            outs.RemoveAt(0);
            if (next == startKey) { corners.Add(next); break; }
            cur = next;
        }
        return corners;
    }

    private static List<PointXY> SimplifyToWorld(List<long> corners, int baseN, double originX, double originY, double cell)
    {
        // Decode + drop consecutive collinear points (rectilinear runs collapse to corners).
        var raw = new List<PointXY>(corners.Count);
        foreach (var k in corners)
        {
            int cx = (int)(k % baseN);
            int cy = (int)(k / baseN);
            raw.Add(new PointXY(originX + cx * cell, originY + cy * cell));
        }
        if (raw.Count > 1 && Same(raw[0], raw[raw.Count - 1])) raw.RemoveAt(raw.Count - 1);

        var outp = new List<PointXY>(raw.Count);
        int n = raw.Count;
        for (int i = 0; i < n; i++)
        {
            var prev = raw[(i - 1 + n) % n];
            var cur = raw[i];
            var next = raw[(i + 1) % n];
            double c = (cur.X - prev.X) * (next.Y - prev.Y) - (cur.Y - prev.Y) * (next.X - prev.X);
            if (Math.Abs(c) > 1e-6) outp.Add(cur); // keep only true corners
        }
        return outp.Count >= 3 ? outp : raw;
    }

    private static double SignedArea(IReadOnlyList<PointXY> p)
    {
        double a = 0;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            a += (p[j].X + p[i].X) * (p[j].Y - p[i].Y);
        return a * 0.5;
    }

    private static bool Same(PointXY a, PointXY b) => Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;
    private static bool IsFilled(bool[] f, int nx, int ny, int x, int y) => InBounds(nx, ny, x, y) && f[y * nx + x];
    private static long Key(int x, int y, int baseN) => (long)y * baseN + x;
    private static bool InBounds(int nx, int ny, int x, int y) => x >= 0 && y >= 0 && x < nx && y < ny;
    private static int Gx(double x, double originX, double cell) => (int)Math.Floor((x - originX) / cell);
    private static int Gy(double y, double originY, double cell) => (int)Math.Floor((y - originY) / cell);
    private static void Mark(bool[] b, int nx, int ny, int x, int y) { if (InBounds(nx, ny, x, y)) b[y * nx + x] = true; }
    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
}
