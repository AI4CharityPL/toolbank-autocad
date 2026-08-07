// Typed DTOs for the acad-dimensions category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Dimensions;

public sealed record DimEmptyArgs();

public sealed record LinearDimArgs(
    [property: JsonPropertyName("p1")]            Point3dDto P1,
    [property: JsonPropertyName("p2")]            Point3dDto P2,
    [property: JsonPropertyName("dimLinePoint")]  Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]   double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("textOverride")]  string? TextOverride = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record AlignedDimArgs(
    [property: JsonPropertyName("p1")]            Point3dDto P1,
    [property: JsonPropertyName("p2")]            Point3dDto P2,
    [property: JsonPropertyName("dimLinePoint")]  Point3dDto DimLinePoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("textOverride")]  string? TextOverride = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record AngularDim3pArgs(
    [property: JsonPropertyName("center")]        Point3dDto Center,
    [property: JsonPropertyName("first")]         Point3dDto First,
    [property: JsonPropertyName("second")]        Point3dDto Second,
    [property: JsonPropertyName("arcPoint")]      Point3dDto ArcPoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record AngularDim2lArgs(
    [property: JsonPropertyName("line1Handle")]   string Line1Handle,
    [property: JsonPropertyName("line2Handle")]   string Line2Handle,
    [property: JsonPropertyName("arcPoint")]      Point3dDto ArcPoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record RadialDimArgs(
    [property: JsonPropertyName("curveHandle")]      string CurveHandle,
    [property: JsonPropertyName("chordPoint")]       Point3dDto ChordPoint,
    [property: JsonPropertyName("leaderLength")]     double LeaderLength = 0.0,
    [property: JsonPropertyName("dimStyle")]         string? DimStyle = null,
    [property: JsonPropertyName("layer")]            string? Layer = null);

public sealed record DiametricDimArgs(
    [property: JsonPropertyName("curveHandle")]   string CurveHandle,
    [property: JsonPropertyName("farPoint")]      Point3dDto FarPoint,
    [property: JsonPropertyName("leaderLength")]  double LeaderLength = 0.0,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record ArcLengthDimArgs(
    [property: JsonPropertyName("arcHandle")]     string ArcHandle,
    [property: JsonPropertyName("arcPoint")]      Point3dDto ArcPoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record OrdinateDimArgs(
    [property: JsonPropertyName("definingPoint")] Point3dDto DefiningPoint,
    [property: JsonPropertyName("leaderEnd")]     Point3dDto LeaderEnd,
    [property: JsonPropertyName("useXAxis")]      bool UseXAxis = true,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

public sealed record BaselineDimArgs(
    [property: JsonPropertyName("baselinePoint")]  Point3dDto BaselinePoint,
    [property: JsonPropertyName("subsequentPoints")] IReadOnlyList<Point3dDto> SubsequentPoints,
    [property: JsonPropertyName("dimLinePoint")]   Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]    double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]       string? DimStyle = null,
    [property: JsonPropertyName("layer")]          string? Layer = null);

public sealed record ContinuedDimArgs(
    [property: JsonPropertyName("startPoint")]     Point3dDto StartPoint,
    [property: JsonPropertyName("subsequentPoints")] IReadOnlyList<Point3dDto> SubsequentPoints,
    [property: JsonPropertyName("dimLinePoint")]   Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]    double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]       string? DimStyle = null,
    [property: JsonPropertyName("layer")]          string? Layer = null);

public sealed record SetDimStyleArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("dimStyle")] string DimStyle);

// ─────────── D6 additions ───────────

/// <summary>Create (or idempotently update) the ARCH-ISO dimension style with architectural ticks, mm units, no trailing zeros.</summary>
public sealed record EnsureArchitecturalDimStyleArgs(
    [property: JsonPropertyName("styleName")] string StyleName = "ARCH-ISO",
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 2.5,
    [property: JsonPropertyName("arrowSizeMm")]  double ArrowSizeMm = 2.5,
    [property: JsonPropertyName("scale")]        double Scale = 100.0,     // plot scale denominator (1:100 → 100)
    [property: JsonPropertyName("roundToMm")]    double RoundToMm = 1.0,
    [property: JsonPropertyName("decimalPlaces")] int DecimalPlaces = 0,
    [property: JsonPropertyName("suppressZeros")] bool SuppressZeros = true,
    [property: JsonPropertyName("makeCurrent")]  bool MakeCurrent = false);

public sealed record EnsureArchitecturalDimStyleResult(
    [property: JsonPropertyName("styleName")]   string StyleName,
    [property: JsonPropertyName("created")]     bool Created,
    [property: JsonPropertyName("updated")]     bool Updated,
    [property: JsonPropertyName("madeCurrent")] bool MadeCurrent);

/// <summary>
/// Cumulative chain: all dimensions share a SINGLE dimension line (no stagger) and each
/// shows the running distance from the baseline point to point_i. Differs from baseline_chain
/// which staggers successive dimensions by DIMDLI, and from continued_chain which lays them end-to-end.
/// </summary>
public sealed record CumulativeDimArgs(
    [property: JsonPropertyName("baselinePoint")]   Point3dDto BaselinePoint,
    [property: JsonPropertyName("subsequentPoints")] IReadOnlyList<Point3dDto> SubsequentPoints,
    [property: JsonPropertyName("dimLinePoint")]    Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]     double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]        string? DimStyle = null,
    [property: JsonPropertyName("layer")]           string? Layer = null);

/// <summary>Convert every dimension on a layer to the target dimension style (default ARCH-ISO).</summary>
public sealed record ApplyArchTickStyleArgs(
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("dimStyle")]    string DimStyle = "ARCH-ISO",
    [property: JsonPropertyName("ensureStyle")] bool EnsureStyle = true);

public sealed record ApplyArchTickStyleResult(
    [property: JsonPropertyName("scanned")]   int Scanned,
    [property: JsonPropertyName("updated")]   int Updated,
    [property: JsonPropertyName("styleEnsured")] bool StyleEnsured);

/// <summary>
/// Build continued-chain dimensions across a list of wall handles projected onto a baseline direction.
/// Each wall's start/end point is projected onto the baseline through <c>origin</c> + <c>baselineDeg</c>;
/// unique projected values are sorted and fed to acad.dimensions.continued_chain.
/// </summary>
public sealed record AutoDimWallsArgs(
    [property: JsonPropertyName("wallHandles")] IReadOnlyList<string> WallHandles,
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("baselineDeg")] double BaselineDeg = 0.0,
    [property: JsonPropertyName("dimLineOffsetMm")] double DimLineOffsetMm = 1500.0,
    [property: JsonPropertyName("mergeToleranceMm")] double MergeToleranceMm = 5.0,
    [property: JsonPropertyName("dimStyle")]    string? DimStyle = "ARCH-ISO",
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record AutoDimWallsResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities,
    [property: JsonPropertyName("pointCount")] int PointCount,
    [property: JsonPropertyName("skippedHandles")] IReadOnlyList<string> SkippedHandles);

/// <summary>Place ONE linear dimension spanning the overall bbox of a list of entity handles, projected onto rotationDeg.</summary>
public sealed record DimOverallArgs(
    [property: JsonPropertyName("handles")]     IReadOnlyList<string> Handles,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("dimLineOffsetMm")] double DimLineOffsetMm = 2500.0,
    [property: JsonPropertyName("dimStyle")]    string? DimStyle = "ARCH-ISO",
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record DimEntityResult(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

public sealed record DimEntitiesResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record DimStyleListResult(
    [property: JsonPropertyName("styles")]  IReadOnlyList<string> Styles,
    [property: JsonPropertyName("current")] string Current);

public sealed record DimAffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

// ─────────── roadmap 3.2, first tranche ───────────

public sealed record JoggedRadiusArgs(
    [property: JsonPropertyName("curveHandle")]    string CurveHandle,
    [property: JsonPropertyName("chordPoint")]     Point3dDto ChordPoint,
    [property: JsonPropertyName("overrideCenter")] Point3dDto? OverrideCenter = null,
    [property: JsonPropertyName("jogPoint")]       Point3dDto? JogPoint = null,
    [property: JsonPropertyName("jogAngleDeg")]    double? JogAngleDeg = null,
    [property: JsonPropertyName("dimStyle")]       string? DimStyle = null,
    [property: JsonPropertyName("layer")]          string? Layer = null);

public sealed record JoggedRadiusResult(
    [property: JsonPropertyName("entity")]         EntityHandle Entity,
    [property: JsonPropertyName("type")]           string Type,
    [property: JsonPropertyName("radius")]         double Radius,
    [property: JsonPropertyName("measurement")]    double Measurement,
    [property: JsonPropertyName("jogAngleDeg")]    double JogAngleDeg,
    [property: JsonPropertyName("center")]         IReadOnlyList<double> Center,
    [property: JsonPropertyName("overrideCenter")] IReadOnlyList<double> OverrideCenter,
    [property: JsonPropertyName("jogPoint")]       IReadOnlyList<double> JogPoint,
    // Added after it came back null through this DTO while the plugin was emitting it - the
    // silent-drop of KNOWN-GAPS C0, caught here only because a check asked for the value.
    [property: JsonPropertyName("jogOffset")]      double JogOffset,
    [property: JsonPropertyName("note")]           string Note);

public sealed record ObliqueArgs(
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("obliqueDeg")] double? ObliqueDeg = null);

public sealed record ObliquedDimDto(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("measurement")] double Measurement);

public sealed record ObliqueResult(
    [property: JsonPropertyName("affected")]   int Affected,
    [property: JsonPropertyName("obliqueDeg")] double ObliqueDeg,
    [property: JsonPropertyName("dimensions")] IReadOnlyList<ObliquedDimDto> Dimensions,
    [property: JsonPropertyName("note")]       string Note);

public sealed record EditDimTextArgs(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("text")]            string? Text = null,
    [property: JsonPropertyName("textPosition")]    Point3dDto? TextPosition = null,
    [property: JsonPropertyName("textRotationDeg")] double? TextRotationDeg = null,
    [property: JsonPropertyName("resetPosition")]   bool? ResetPosition = null);

public sealed record EditDimTextResult(
    [property: JsonPropertyName("handle")]              string Handle,
    [property: JsonPropertyName("type")]                string Type,
    [property: JsonPropertyName("measurement")]         double Measurement,
    [property: JsonPropertyName("textBefore")]          string? TextBefore,
    [property: JsonPropertyName("text")]                string? Text,
    [property: JsonPropertyName("displaysMeasurement")] bool DisplaysMeasurement,
    [property: JsonPropertyName("textSuppressed")]      bool TextSuppressed,
    [property: JsonPropertyName("positionBefore")]      IReadOnlyList<double> PositionBefore,
    [property: JsonPropertyName("textPosition")]        IReadOnlyList<double> TextPosition,
    [property: JsonPropertyName("usingDefaultTextPosition")] bool UsingDefaultTextPosition,
    [property: JsonPropertyName("note")]                string Note);
