// Plugin-side DTOs for the acad-dimensions category.
// Mirror Backend/Categories/Dimensions/DimensionsDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record DimEmptyArgsDto();

internal sealed record LinearDimArgsDto(
    [property: JsonPropertyName("p1")]            Point3dDto P1,
    [property: JsonPropertyName("p2")]            Point3dDto P2,
    [property: JsonPropertyName("dimLinePoint")]  Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]   double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("textOverride")]  string? TextOverride = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record AlignedDimArgsDto(
    [property: JsonPropertyName("p1")]            Point3dDto P1,
    [property: JsonPropertyName("p2")]            Point3dDto P2,
    [property: JsonPropertyName("dimLinePoint")]  Point3dDto DimLinePoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("textOverride")]  string? TextOverride = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record AngularDim3pArgsDto(
    [property: JsonPropertyName("center")]        Point3dDto Center,
    [property: JsonPropertyName("first")]         Point3dDto First,
    [property: JsonPropertyName("second")]        Point3dDto Second,
    [property: JsonPropertyName("arcPoint")]      Point3dDto ArcPoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record AngularDim2lArgsDto(
    [property: JsonPropertyName("line1Handle")]   string Line1Handle,
    [property: JsonPropertyName("line2Handle")]   string Line2Handle,
    [property: JsonPropertyName("arcPoint")]      Point3dDto ArcPoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record RadialDimArgsDto(
    [property: JsonPropertyName("curveHandle")]   string CurveHandle,
    [property: JsonPropertyName("chordPoint")]    Point3dDto ChordPoint,
    [property: JsonPropertyName("leaderLength")]  double LeaderLength = 0.0,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record DiametricDimArgsDto(
    [property: JsonPropertyName("curveHandle")]   string CurveHandle,
    [property: JsonPropertyName("farPoint")]      Point3dDto FarPoint,
    [property: JsonPropertyName("leaderLength")]  double LeaderLength = 0.0,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record ArcLengthDimArgsDto(
    [property: JsonPropertyName("arcHandle")]     string ArcHandle,
    [property: JsonPropertyName("arcPoint")]      Point3dDto ArcPoint,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record OrdinateDimArgsDto(
    [property: JsonPropertyName("definingPoint")] Point3dDto DefiningPoint,
    [property: JsonPropertyName("leaderEnd")]     Point3dDto LeaderEnd,
    [property: JsonPropertyName("useXAxis")]      bool UseXAxis = true,
    [property: JsonPropertyName("dimStyle")]      string? DimStyle = null,
    [property: JsonPropertyName("layer")]         string? Layer = null);

internal sealed record BaselineDimArgsDto(
    [property: JsonPropertyName("baselinePoint")]    Point3dDto BaselinePoint,
    [property: JsonPropertyName("subsequentPoints")] IReadOnlyList<Point3dDto> SubsequentPoints,
    [property: JsonPropertyName("dimLinePoint")]     Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]      double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]         string? DimStyle = null,
    [property: JsonPropertyName("layer")]            string? Layer = null);

internal sealed record ContinuedDimArgsDto(
    [property: JsonPropertyName("startPoint")]       Point3dDto StartPoint,
    [property: JsonPropertyName("subsequentPoints")] IReadOnlyList<Point3dDto> SubsequentPoints,
    [property: JsonPropertyName("dimLinePoint")]     Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]      double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]         string? DimStyle = null,
    [property: JsonPropertyName("layer")]            string? Layer = null);

internal sealed record SetDimStyleArgsDto(
    [property: JsonPropertyName("handles")]   IReadOnlyList<string> Handles,
    [property: JsonPropertyName("dimStyle")]  string DimStyle);

internal sealed record EnsureArchitecturalDimStyleArgsDto(
    [property: JsonPropertyName("styleName")]    string StyleName = "ARCH-ISO",
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 2.5,
    [property: JsonPropertyName("arrowSizeMm")]  double ArrowSizeMm = 2.5,
    [property: JsonPropertyName("scale")]        double Scale = 100.0,
    [property: JsonPropertyName("roundToMm")]    double RoundToMm = 1.0,
    [property: JsonPropertyName("decimalPlaces")] int DecimalPlaces = 0,
    [property: JsonPropertyName("suppressZeros")] bool SuppressZeros = true,
    [property: JsonPropertyName("makeCurrent")]  bool MakeCurrent = false);

internal sealed record CumulativeDimArgsDto(
    [property: JsonPropertyName("baselinePoint")]    Point3dDto BaselinePoint,
    [property: JsonPropertyName("subsequentPoints")] IReadOnlyList<Point3dDto> SubsequentPoints,
    [property: JsonPropertyName("dimLinePoint")]     Point3dDto DimLinePoint,
    [property: JsonPropertyName("rotationDeg")]      double RotationDeg = 0.0,
    [property: JsonPropertyName("dimStyle")]         string? DimStyle = null,
    [property: JsonPropertyName("layer")]            string? Layer = null);

internal sealed record ApplyArchTickStyleArgsDto(
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("dimStyle")]    string DimStyle = "ARCH-ISO",
    [property: JsonPropertyName("ensureStyle")] bool EnsureStyle = true);

internal sealed record EnsureArchDimStyleResultDto(
    [property: JsonPropertyName("styleName")]   string StyleName,
    [property: JsonPropertyName("created")]     bool Created,
    [property: JsonPropertyName("updated")]     bool Updated,
    [property: JsonPropertyName("madeCurrent")] bool MadeCurrent);
