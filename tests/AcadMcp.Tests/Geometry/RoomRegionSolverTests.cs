// Unit tests for the AutoCAD-free RoomRegionSolver flood-fill (room boundary detection).
// All coordinates are in millimetres, mirroring the DWG drawing units.

using System.Collections.Generic;
using AcadMcp.Shared.Geometry;
using Xunit;

namespace AcadMcp.Tests.Geometry;

public class RoomRegionSolverTests
{
    private static List<WallSeg> Rect(double x0, double y0, double x1, double y1) => new()
    {
        new WallSeg(x0, y0, x1, y0),
        new WallSeg(x1, y0, x1, y1),
        new WallSeg(x1, y1, x0, y1),
        new WallSeg(x0, y1, x0, y0),
    };

    [Fact]
    public void ClosedRectangle_measures_area_close_to_truth()
    {
        // 10 m x 8 m room => 80 m^2 = 80_000_000 mm^2.
        var walls = Rect(0, 0, 10000, 8000);
        var region = RoomRegionSolver.SolveFlood(walls, new List<OpeningSeed>(), 5000, 4000);

        Assert.NotNull(region);
        double areaM2 = region!.AreaMm2 / 1_000_000.0;
        Assert.InRange(areaM2, 72.0, 80.0); // raster underestimates slightly near walls
        Assert.True(region.Outline.Count >= 4);
    }

    [Fact]
    public void OpenSide_without_seal_escapes_and_returns_null()
    {
        // Three walls only (right side open) => flood reaches the border ring => fallback.
        var walls = new List<WallSeg>
        {
            new WallSeg(0, 0, 10000, 0),
            new WallSeg(10000, 0, 10000, 8000),   // keep right closed...
            new WallSeg(0, 8000, 0, 0),
        };
        // Top wall missing entirely -> open.
        var region = RoomRegionSolver.SolveFlood(walls, new List<OpeningSeed>(), 5000, 4000);
        Assert.Null(region);
    }

    [Fact]
    public void DoorGap_leaks_without_seal_but_holds_when_sealed()
    {
        // Rectangle with a 1 m gap in the bottom wall (a doorway).
        var walls = new List<WallSeg>
        {
            new WallSeg(0, 0, 4500, 0),       // bottom-left part
            new WallSeg(5500, 0, 10000, 0),   // bottom-right part (gap 4500..5500)
            new WallSeg(10000, 0, 10000, 8000),
            new WallSeg(10000, 8000, 0, 8000),
            new WallSeg(0, 8000, 0, 0),
        };

        // Without sealing the doorway the flood leaks out -> null.
        var leaked = RoomRegionSolver.SolveFlood(walls, new List<OpeningSeed>(), 5000, 4000);
        Assert.Null(leaked);

        // Sealing the doorway opening keeps the region enclosed.
        var sealedSeed = new List<OpeningSeed> { new OpeningSeed(5000, 0, 1000) };
        var region = RoomRegionSolver.SolveFlood(walls, sealedSeed, 5000, 4000);
        Assert.NotNull(region);
        double areaM2 = region!.AreaMm2 / 1_000_000.0;
        Assert.InRange(areaM2, 72.0, 80.0);
    }

    [Fact]
    public void LShape_is_enclosed_and_smaller_than_bounding_box()
    {
        // L-shaped room within a 10x8 bbox; notch cut from the top-right.
        var walls = new List<WallSeg>
        {
            new WallSeg(0, 0, 10000, 0),
            new WallSeg(10000, 0, 10000, 4000),
            new WallSeg(10000, 4000, 5000, 4000),
            new WallSeg(5000, 4000, 5000, 8000),
            new WallSeg(5000, 8000, 0, 8000),
            new WallSeg(0, 8000, 0, 0),
        };
        var region = RoomRegionSolver.SolveFlood(walls, new List<OpeningSeed>(), 2000, 2000);

        Assert.NotNull(region);
        double areaM2 = region!.AreaMm2 / 1_000_000.0;
        // Full bbox would be 80 m^2; the L removes a 5x4 = 20 m^2 notch => ~60 m^2.
        Assert.InRange(areaM2, 52.0, 64.0);
    }

    [Fact]
    public void PointInPolygon_inside_and_outside()
    {
        var square = new List<PointXY>
        {
            new PointXY(0, 0), new PointXY(10, 0), new PointXY(10, 10), new PointXY(0, 10),
        };
        Assert.True(RoomRegionSolver.PointInPolygon(square, 5, 5));
        Assert.False(RoomRegionSolver.PointInPolygon(square, 15, 5));
        Assert.False(RoomRegionSolver.PointInPolygon(square, -1, -1));
    }

    [Fact]
    public void SeedOutsideWallBounds_returns_null()
    {
        var walls = Rect(0, 0, 10000, 8000);
        var region = RoomRegionSolver.SolveFlood(walls, new List<OpeningSeed>(), 50000, 50000);
        Assert.Null(region);
    }

    [Fact]
    public void Flood_rejected_when_measured_exceeds_three_times_label()
    {
        // 10x8 m room ~80 m²; label says 20 m² => measured > 3× label => reject flood.
        var walls = Rect(0, 0, 10000, 8000);
        var region = RoomRegionSolver.SolveFlood(walls, new List<OpeningSeed>(), 5000, 4000, labelAreaM2: 20.0);
        Assert.Null(region);
    }
}
