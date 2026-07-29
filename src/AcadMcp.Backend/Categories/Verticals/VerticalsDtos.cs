// Typed DTOs for acad-verticals. Every verb here drives a composite tool that
// orchestrates acad.geometry2d.* / acad.annotations.* / acad.layers.*
// primitives through ArchitectureProxy. See rule 67 (grid axes + verticals).

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Verticals;

// ─────────── stairs ───────────

public sealed record StraightStairArgs(
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("runLengthMm")]  double RunLengthMm,
    [property: JsonPropertyName("widthMm")]      double WidthMm,
    [property: JsonPropertyName("treadCount")]   int TreadCount,
    [property: JsonPropertyName("riserHeightMm")] double RiserHeightMm,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("directionUp")]  bool DirectionUp = true,
    [property: JsonPropertyName("label")]        string Label = "UP",
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerStairs,
    [property: JsonPropertyName("arrowLayer")]   string ArrowLayer = VerticalsPalette.LayerStairsDir);

public sealed record StraightStairResult(
    [property: JsonPropertyName("outline")]       EntityHandle Outline,
    [property: JsonPropertyName("treadLines")]    IReadOnlyList<EntityHandle> TreadLines,
    [property: JsonPropertyName("directionArrow")] EntityHandle DirectionArrow,
    [property: JsonPropertyName("label")]         EntityHandle LabelHandle,
    [property: JsonPropertyName("treadDepthMm")]  double TreadDepthMm,
    [property: JsonPropertyName("complianceWarnings")] IReadOnlyList<string> ComplianceWarnings);

public sealed record SpiralStairArgs(
    [property: JsonPropertyName("center")]       Point2dDto Center,
    [property: JsonPropertyName("radiusMm")]     double RadiusMm,
    [property: JsonPropertyName("innerRadiusMm")] double InnerRadiusMm = 150.0,
    [property: JsonPropertyName("treadCount")]   int TreadCount = 12,
    [property: JsonPropertyName("startAngleDeg")] double StartAngleDeg = 0.0,
    [property: JsonPropertyName("sweepDeg")]     double SweepDeg = 270.0,
    [property: JsonPropertyName("directionUp")]  bool DirectionUp = true,
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerStairs,
    [property: JsonPropertyName("arrowLayer")]   string ArrowLayer = VerticalsPalette.LayerStairsDir);

public sealed record SpiralStairResult(
    [property: JsonPropertyName("outerArc")]    EntityHandle OuterArc,
    [property: JsonPropertyName("innerArc")]    EntityHandle InnerArc,
    [property: JsonPropertyName("treadLines")]  IReadOnlyList<EntityHandle> TreadLines,
    [property: JsonPropertyName("label")]       EntityHandle LabelHandle);

public sealed record UShapedStairArgs(
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("runLengthMm")]  double RunLengthMm,
    [property: JsonPropertyName("widthMm")]      double WidthMm,
    [property: JsonPropertyName("landingDepthMm")] double LandingDepthMm,
    [property: JsonPropertyName("gapMm")]        double GapMm,
    [property: JsonPropertyName("treadsPerRun")] int TreadsPerRun,
    [property: JsonPropertyName("riserHeightMm")] double RiserHeightMm,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("directionUp")]  bool DirectionUp = true,
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerStairs,
    [property: JsonPropertyName("arrowLayer")]   string ArrowLayer = VerticalsPalette.LayerStairsDir);

public sealed record UShapedStairResult(
    [property: JsonPropertyName("firstRun")]   StraightStairResult FirstRun,
    [property: JsonPropertyName("secondRun")]  StraightStairResult SecondRun,
    [property: JsonPropertyName("landing")]    EntityHandle Landing);

// ─────────── ramp ───────────

public sealed record RampArgs(
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("lengthMm")]     double LengthMm,
    [property: JsonPropertyName("widthMm")]      double WidthMm,
    [property: JsonPropertyName("riseMm")]       double RiseMm,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("directionUp")]  bool DirectionUp = true,
    [property: JsonPropertyName("accessible")]   bool Accessible = true,
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerRamp,
    [property: JsonPropertyName("arrowLayer")]   string ArrowLayer = VerticalsPalette.LayerRampDir);

public sealed record RampResult(
    [property: JsonPropertyName("outline")]     EntityHandle Outline,
    [property: JsonPropertyName("slopeArrow")]  EntityHandle SlopeArrow,
    [property: JsonPropertyName("label")]       EntityHandle LabelHandle,
    [property: JsonPropertyName("slopePct")]    double SlopePct,
    [property: JsonPropertyName("complianceWarnings")] IReadOnlyList<string> ComplianceWarnings);

// ─────────── elevator ───────────

public sealed record ElevatorArgs(
    [property: JsonPropertyName("center")]       Point2dDto Center,
    [property: JsonPropertyName("widthMm")]      double WidthMm,
    [property: JsonPropertyName("depthMm")]      double DepthMm,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("kind")]         string Kind = "passenger", // passenger | bed | goods
    [property: JsonPropertyName("capacityKg")]   int CapacityKg = 1000,
    [property: JsonPropertyName("label")]        string? Label = null,
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerElevator);

public sealed record ElevatorResult(
    [property: JsonPropertyName("shaft")]        EntityHandle Shaft,
    [property: JsonPropertyName("diagonals")]    IReadOnlyList<EntityHandle> Diagonals,
    [property: JsonPropertyName("label")]        EntityHandle LabelHandle,
    [property: JsonPropertyName("complianceWarnings")] IReadOnlyList<string> ComplianceWarnings);

// ─────────── escalator ───────────

public sealed record EscalatorArgs(
    [property: JsonPropertyName("origin")]       Point2dDto Origin,
    [property: JsonPropertyName("lengthMm")]     double LengthMm,
    [property: JsonPropertyName("widthMm")]      double WidthMm = 1000.0,
    [property: JsonPropertyName("rotationDeg")]  double RotationDeg = 0.0,
    [property: JsonPropertyName("directionUp")]  bool DirectionUp = true,
    [property: JsonPropertyName("stepCount")]    int StepCount = 16,
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerEscalator,
    [property: JsonPropertyName("arrowLayer")]   string ArrowLayer = VerticalsPalette.LayerStairsDir);

public sealed record EscalatorResult(
    [property: JsonPropertyName("outline")]     EntityHandle Outline,
    [property: JsonPropertyName("stepLines")]   IReadOnlyList<EntityHandle> StepLines,
    [property: JsonPropertyName("directionArrow")] EntityHandle DirectionArrow,
    [property: JsonPropertyName("label")]       EntityHandle LabelHandle);

// ─────────── platform lift ───────────

public sealed record PlatformLiftArgs(
    [property: JsonPropertyName("center")]      Point2dDto Center,
    [property: JsonPropertyName("widthMm")]     double WidthMm = 1100.0,
    [property: JsonPropertyName("depthMm")]     double DepthMm = 1400.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("label")]       string Label = "PL",
    [property: JsonPropertyName("layer")]       string Layer = VerticalsPalette.LayerPlatformLift);

public sealed record PlatformLiftResult(
    [property: JsonPropertyName("outline")]   EntityHandle Outline,
    [property: JsonPropertyName("label")]     EntityHandle LabelHandle);

// ─────────── handrail ───────────

public sealed record HandrailArgs(
    [property: JsonPropertyName("path")]         IReadOnlyList<Point2dDto> Path,
    [property: JsonPropertyName("heightMm")]     double HeightMm = 1000.0,
    [property: JsonPropertyName("layer")]        string Layer = VerticalsPalette.LayerHandrail,
    [property: JsonPropertyName("annotate")]     bool Annotate = true);

public sealed record HandrailResult(
    [property: JsonPropertyName("polyline")] EntityHandle Polyline,
    [property: JsonPropertyName("label")]    EntityHandle? LabelHandle,
    [property: JsonPropertyName("complianceWarnings")] IReadOnlyList<string> ComplianceWarnings);
