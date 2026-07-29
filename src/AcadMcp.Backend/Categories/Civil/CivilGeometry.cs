// Surveyor / civil-engineering numerics — bearings, stationing, parcel
// closure. Pure C#, no AutoCAD types. Lives in the Backend so the source
// generator and tests can validate it independently of a running AutoCAD.
//
// Single source of truth referenced from rule 38 §1, §2, §3.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Civil;

public enum BearingQuadrant { NE = 0, SE = 1, SW = 2, NW = 3 }

public enum StationingSystem
{
    /// <summary>Polish / EU "0+020" (km + metres past).</summary>
    MetricKm = 0,
    /// <summary>US "0+20" (stations of 100 ft).</summary>
    UsFeet   = 1,
}

/// <summary>
/// A surveyor bearing (rule 38 §2). Quadrant + degrees + minutes + seconds.
/// </summary>
public sealed record Bearing(BearingQuadrant Quadrant, int Degrees, int Minutes, double Seconds)
{
    /// <summary>Total magnitude in decimal degrees within the quadrant (0..90).</summary>
    public double DecimalDegrees => Degrees + Minutes / 60.0 + Seconds / 3600.0;

    /// <summary>Convert the bearing to a planar unit vector. +X is East, +Y is
    /// North (true north — see rule 38 §8 for drawing-rotation handling).</summary>
    public (double X, double Y) ToVector()
    {
        // dx = sin(theta) for E/W component, dy = cos(theta) for N/S component,
        // with the quadrant deciding the SIGN of each component.
        double dec = DecimalDegrees * Math.PI / 180.0;
        double sin = Math.Sin(dec);
        double cos = Math.Cos(dec);
        return Quadrant switch
        {
            BearingQuadrant.NE => ( sin,  cos),
            BearingQuadrant.SE => ( sin, -cos),
            BearingQuadrant.SW => (-sin, -cos),
            BearingQuadrant.NW => (-sin,  cos),
            _ => throw new InvalidOperationException("unknown quadrant"),
        };
    }

    /// <summary>Surveyor textual form: <c>N 45° 30' 15" E</c>.</summary>
    public string ToSurveyorString()
    {
        var ns = Quadrant is BearingQuadrant.NE or BearingQuadrant.NW ? "N" : "S";
        var ew = Quadrant is BearingQuadrant.NE or BearingQuadrant.SE ? "E" : "W";
        // Two-decimal seconds — Polish geodetic conventional precision.
        return string.Format(CultureInfo.InvariantCulture,
            "{0} {1}° {2:00}' {3:00.00}\" {4}", ns, Degrees, Minutes, Seconds, ew);
    }

    /// <summary>Parse the surveyor textual form. Accepts ASCII <c>deg/min/sec</c>
    /// markers or the Unicode glyphs (° ′ ″). Tolerates extra whitespace.</summary>
    public static Bearing Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("bearing text empty");

        // Normalise punctuation so a single regex covers both ASCII and Unicode.
        var t = text.Trim()
                    .Replace('°', 'd').Replace("deg", "d", StringComparison.OrdinalIgnoreCase)
                    .Replace('′', '\'').Replace('\u2032', '\'')
                    .Replace('″', '"').Replace('\u2033', '"');

        // Accept either the symbolic surveyor form `N 45d 30' 15" E` or the
        // whitespace-separated DMS form `N 45 30 15 E` (used by most test
        // vectors and ACAD table copy-paste). The `d` / `'` / `"` markers are
        // each optional; components separated by whitespace are required.
        var m = Regex.Match(t,
            @"^([NSns])\s+(\d+(?:\.\d+)?)\s*d?\s*(\d+(?:\.\d+)?)?\s*'?\s*(\d+(?:\.\d+)?)?\s*""?\s*([EWew])$",
            RegexOptions.CultureInvariant);
        if (!m.Success)
            throw new FormatException($"bearing '{text}' is not in the form 'N 45d 30' 15\" E' or 'N 45 30 15 E'");

        var ns = m.Groups[1].Value.ToUpperInvariant();
        var ew = m.Groups[5].Value.ToUpperInvariant();
        int deg     = (int)double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        int min     = m.Groups[3].Success && m.Groups[3].Length > 0
                      ? (int)double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        double sec  = m.Groups[4].Success && m.Groups[4].Length > 0
                      ? double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0.0;

        if (deg < 0 || deg > 90)
            throw new FormatException($"bearing degrees {deg} out of range [0,90]");
        if (min < 0 || min >= 60)
            throw new FormatException($"bearing minutes {min} out of range [0,60)");
        if (sec < 0 || sec >= 60)
            throw new FormatException($"bearing seconds {sec} out of range [0,60)");

        var q = (ns, ew) switch
        {
            ("N", "E") => BearingQuadrant.NE,
            ("S", "E") => BearingQuadrant.SE,
            ("S", "W") => BearingQuadrant.SW,
            ("N", "W") => BearingQuadrant.NW,
            _          => throw new FormatException($"unknown quadrant '{ns}{ew}'"),
        };
        return new Bearing(q, deg, min, sec);
    }
}

/// <summary>Stationing notation per rule 38 §1.</summary>
public static class CivilStationing
{
    public static string Format(double metresFromStart, StationingSystem system)
    {
        if (metresFromStart < 0)
            throw new ArgumentOutOfRangeException(nameof(metresFromStart), "stationing must be >= 0");

        if (system == StationingSystem.MetricKm)
        {
            int km     = (int)Math.Floor(metresFromStart / 1000.0);
            double rem = metresFromStart - km * 1000.0;
            // 3-digit metres past kilometre, with one decimal for sub-metre precision.
            return string.Format(CultureInfo.InvariantCulture, "{0}+{1:000.0}", km, rem);
        }
        // US: 1 station = 100 ft = 30.48 m. Treat the input as metres still — caller picks system.
        double feet = metresFromStart / 0.3048;
        int stations = (int)Math.Floor(feet / 100.0);
        double remFt = feet - stations * 100.0;
        return string.Format(CultureInfo.InvariantCulture, "{0}+{1:00.0}", stations, remFt);
    }
}

/// <summary>Walks a list of (bearing, distance) legs from a start point and
/// returns the resulting vertices + closure error (distance from the last
/// vertex back to the start). Per rule 38 §3 the tool MUST report the error
/// rather than silently snap.</summary>
public static class CivilParcel
{
    public sealed record TraverseResult(
        IReadOnlyList<Point2dDto> Vertices,
        double ClosureErrorM,
        bool WithinTolerance,
        double ToleranceM);

    public static TraverseResult Traverse(
        Point2dDto start,
        IReadOnlyList<(Bearing Bearing, double DistanceM)> legs,
        double toleranceM)
    {
        if (legs == null || legs.Count < 3)
            throw new ArgumentException("a parcel needs at least 3 legs");

        var vertices = new List<Point2dDto>(capacity: legs.Count + 1) { start };
        double x = start.X, y = start.Y;
        foreach (var (bearing, distance) in legs)
        {
            if (distance <= 0) throw new ArgumentException($"leg distance must be > 0 (got {distance})");
            var (dx, dy) = bearing.ToVector();
            x += dx * distance;
            y += dy * distance;
            vertices.Add(new Point2dDto(x, y));
        }
        double dxClose = x - start.X;
        double dyClose = y - start.Y;
        double err     = Math.Sqrt(dxClose * dxClose + dyClose * dyClose);
        return new TraverseResult(vertices, err, err <= toleranceM, toleranceM);
    }
}
