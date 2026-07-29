// Wire DTOs for the acad-electrical domain category. Mirrors plugin readers
// 1-to-1 (rule 22). Every JsonPropertyName matches a field on a primitive
// plugin handler used by ElectricalProxy.
//
// Rules: 35-domain-categories-design.md, 39-electrical-domain-traps.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Electrical;

#region infrastructure

public sealed record LayerEnsureOutcome(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("status")]  string Status,
    [property: JsonPropertyName("color")]   int? AciColor,
    [property: JsonPropertyName("linetype")] string? Linetype,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm,
    [property: JsonPropertyName("error")]   string? Error = null);

public sealed record EnsureElectricalLayersArgs(
    [property: JsonPropertyName("includePanel")] bool IncludePanel = false);

public sealed record EnsureElectricalLayersResult(
    [property: JsonPropertyName("layers")]        IReadOnlyList<LayerEnsureOutcome> Layers,
    [property: JsonPropertyName("createdCount")]  int CreatedCount,
    [property: JsonPropertyName("existingCount")] int ExistingCount);

#endregion

#region terminals

/// <summary>A point on a symbol where wires may legally connect (rule 39 §7).</summary>
public sealed record Terminal(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("position")] Point2dDto Position);

#endregion

#region ladder

public sealed record DrawLadderRailsArgs(
    [property: JsonPropertyName("topLeft")]    Point2dDto TopLeft,
    [property: JsonPropertyName("widthMm")]    double WidthMm  = 200.0,
    [property: JsonPropertyName("heightMm")]   double HeightMm = 250.0,
    [property: JsonPropertyName("leftRailLabel")]  string LeftRailLabel  = "L1",
    [property: JsonPropertyName("rightRailLabel")] string RightRailLabel = "N",
    [property: JsonPropertyName("textHeightMm")]   double TextHeightMm = 5.0,
    [property: JsonPropertyName("railLayer")]      string RailLayer  = ElectricalPalette.LayerWirePwr,
    [property: JsonPropertyName("labelLayer")]     string LabelLayer = ElectricalPalette.LayerLblWire);

public sealed record DrawLadderRailsResult(
    [property: JsonPropertyName("leftRail")]   EntityHandle LeftRail,
    [property: JsonPropertyName("rightRail")]  EntityHandle RightRail,
    [property: JsonPropertyName("leftLabel")]  EntityHandle LeftLabel,
    [property: JsonPropertyName("rightLabel")] EntityHandle RightLabel,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record DrawLadderRungArgs(
    [property: JsonPropertyName("leftRailX")]    double LeftRailX,
    [property: JsonPropertyName("rightRailX")]   double RightRailX,
    [property: JsonPropertyName("y")]            double Y,
    [property: JsonPropertyName("rungNumber")]   int RungNumber,
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 4.0,
    [property: JsonPropertyName("labelOffsetMm")] double LabelOffsetMm = 8.0,
    [property: JsonPropertyName("rungLayer")]    string RungLayer  = ElectricalPalette.LayerWire,
    [property: JsonPropertyName("labelLayer")]   string LabelLayer = ElectricalPalette.LayerLblRung);

public sealed record DrawLadderRungResult(
    [property: JsonPropertyName("rung")]          EntityHandle Rung,
    [property: JsonPropertyName("rungLabel")]     EntityHandle RungLabel,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region wires

public sealed record DrawWireArgs(
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("kind")]     string Kind = "signal", // "signal" | "power" | "control"
    [property: JsonPropertyName("layer")]    string? Layer = null);   // overrides kind→layer routing

public sealed record DrawWireResult(
    [property: JsonPropertyName("wire")]          EntityHandle Wire,
    [property: JsonPropertyName("layer")]         string Layer,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record DrawWireJunctionArgs(
    [property: JsonPropertyName("position")] Point2dDto Position,
    [property: JsonPropertyName("dotRadiusMm")] double DotRadiusMm = 0.6,
    [property: JsonPropertyName("layer")]    string Layer = ElectricalPalette.LayerWire);

public sealed record DrawWireJunctionResult(
    [property: JsonPropertyName("dot")]           EntityHandle Dot,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region symbols

public sealed record PlaceResistorArgs(
    [property: JsonPropertyName("position")]    Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("style")]       string Style = "iec",        // "iec" | "ansi"
    [property: JsonPropertyName("unitSizeMm")]  double UnitSizeMm = 5.0,
    [property: JsonPropertyName("layer")]       string Layer = ElectricalPalette.LayerSymbol);

public sealed record PlaceResistorResult(
    [property: JsonPropertyName("body")]          IReadOnlyList<EntityHandle> Body,
    [property: JsonPropertyName("terminals")]     IReadOnlyList<Terminal> Terminals,
    [property: JsonPropertyName("style")]         string Style,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record PlaceContactArgs(
    [property: JsonPropertyName("position")]    Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("unitSizeMm")]  double UnitSizeMm = 5.0,
    [property: JsonPropertyName("layer")]       string Layer = ElectricalPalette.LayerSymbol);

public sealed record PlaceContactResult(
    [property: JsonPropertyName("body")]          IReadOnlyList<EntityHandle> Body,
    [property: JsonPropertyName("terminals")]     IReadOnlyList<Terminal> Terminals,
    [property: JsonPropertyName("kind")]          string Kind, // "no" | "nc"
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record PlaceCoilArgs(
    [property: JsonPropertyName("position")]      Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")]   double RotationDeg = 0.0,
    [property: JsonPropertyName("style")]         string Style = "iec",        // "iec" | "ansi"
    [property: JsonPropertyName("unitSizeMm")]    double UnitSizeMm = 5.0,
    [property: JsonPropertyName("tag")]           string? Tag = null,           // e.g. "-K1"; rendered if non-null
    [property: JsonPropertyName("contactRungs")]  IReadOnlyList<int>? ContactRungs = null, // rule 39 §5
    [property: JsonPropertyName("textHeightMm")]  double TextHeightMm = 3.0,
    [property: JsonPropertyName("layer")]         string Layer = ElectricalPalette.LayerSymbol,
    [property: JsonPropertyName("tagLayer")]      string TagLayer  = ElectricalPalette.LayerLblDev,
    [property: JsonPropertyName("xrefLayer")]     string XrefLayer = ElectricalPalette.LayerXref);

public sealed record PlaceCoilResult(
    [property: JsonPropertyName("body")]          IReadOnlyList<EntityHandle> Body,
    [property: JsonPropertyName("terminals")]     IReadOnlyList<Terminal> Terminals,
    [property: JsonPropertyName("tagText")]       EntityHandle? TagText,
    [property: JsonPropertyName("xrefText")]      EntityHandle? XrefText,
    [property: JsonPropertyName("style")]         string Style,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region terminal block

public sealed record PlaceTerminalBlockArgs(
    [property: JsonPropertyName("origin")]        Point2dDto Origin,
    [property: JsonPropertyName("count")]         int Count = 8,
    [property: JsonPropertyName("pitchMm")]       double PitchMm = 6.0,
    [property: JsonPropertyName("heightMm")]      double HeightMm = 12.0,
    [property: JsonPropertyName("startNumber")]   int StartNumber = 1,
    [property: JsonPropertyName("textHeightMm")]  double TextHeightMm = 2.5,
    [property: JsonPropertyName("layer")]         string Layer    = ElectricalPalette.LayerTerm,
    [property: JsonPropertyName("labelLayer")]    string LabelLayer = ElectricalPalette.LayerLblWire);

public sealed record TerminalBlockSlot(
    [property: JsonPropertyName("number")]   int Number,
    [property: JsonPropertyName("body")]     EntityHandle Body,
    [property: JsonPropertyName("label")]    EntityHandle Label,
    [property: JsonPropertyName("topPosition")]    Point2dDto TopPosition,
    [property: JsonPropertyName("bottomPosition")] Point2dDto BottomPosition);

public sealed record PlaceTerminalBlockResult(
    [property: JsonPropertyName("slots")]         IReadOnlyList<TerminalBlockSlot> Slots,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region device tag

public sealed record PlaceDeviceTagArgs(
    [property: JsonPropertyName("position")]    Point2dDto Position,
    [property: JsonPropertyName("tag")]         string Tag,                  // "-K1" | "+CAB1-K1" | "=PWR+CAB1-K1"
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 3.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]       string Layer = ElectricalPalette.LayerLblDev);

public sealed record PlaceDeviceTagResult(
    [property: JsonPropertyName("text")]          EntityHandle Text,
    [property: JsonPropertyName("canonical")]     string Canonical,    // "=FUNC+LOC-PREFIXSEQ" form
    [property: JsonPropertyName("prefix")]        char Prefix,
    [property: JsonPropertyName("prefixMeaning")] string PrefixMeaning,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region panel layout

public sealed record PlaceDinRailArgs(
    [property: JsonPropertyName("start")]       Point2dDto Start,
    [property: JsonPropertyName("lengthMm")]    double LengthMm,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("railWidthMm")] double RailWidthMm = 35.0,  // standard EN 50022 top-hat rail width
    [property: JsonPropertyName("slotPitchMm")] double? SlotPitchMm = null, // draw tick marks every N mm if set
    [property: JsonPropertyName("layer")]       string Layer = ElectricalPalette.LayerPanel);

public sealed record PlaceDinRailResult(
    [property: JsonPropertyName("outline")]       EntityHandle Outline,
    [property: JsonPropertyName("slotTicks")]     IReadOnlyList<EntityHandle> SlotTicks,
    [property: JsonPropertyName("end")]           Point2dDto End,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record PlacePanelDeviceOutlineArgs(
    [property: JsonPropertyName("origin")]      Point2dDto Origin,   // top-left corner
    [property: JsonPropertyName("widthMm")]     double WidthMm,
    [property: JsonPropertyName("heightMm")]    double HeightMm,
    [property: JsonPropertyName("tag")]         string? Tag = null,
    [property: JsonPropertyName("textHeightMm")] double TextHeightMm = 2.5,
    [property: JsonPropertyName("layer")]       string Layer = ElectricalPalette.LayerPanel,
    [property: JsonPropertyName("tagLayer")]    string TagLayer = ElectricalPalette.LayerLblDev);

public sealed record PlacePanelDeviceOutlineResult(
    [property: JsonPropertyName("outline")]       EntityHandle Outline,
    [property: JsonPropertyName("tagText")]       EntityHandle? TagText,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

public sealed record RouteWirewayArgs(
    [property: JsonPropertyName("path")]        IReadOnlyList<Point2dDto> Path,
    [property: JsonPropertyName("widthMm")]     double WidthMm = 40.0,
    [property: JsonPropertyName("layer")]       string Layer = ElectricalPalette.LayerPanel);

public sealed record RouteWirewayResult(
    [property: JsonPropertyName("centerline")]    EntityHandle Centerline,
    [property: JsonPropertyName("leftEdge")]      EntityHandle LeftEdge,
    [property: JsonPropertyName("rightEdge")]     EntityHandle RightEdge,
    [property: JsonPropertyName("widthMm")]       double WidthMm,
    [property: JsonPropertyName("createdLayers")] IReadOnlyList<string> CreatedLayers);

#endregion

#region introspection

public sealed record ElectricalHealthArgs();

public sealed record ElectricalLayerSpec(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("aciColor")]     int AciColor,
    [property: JsonPropertyName("linetype")]     string Linetype,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm,
    [property: JsonPropertyName("plottable")]    bool Plottable,
    [property: JsonPropertyName("purpose")]      string Purpose);

public sealed record IecPrefixSpec(
    [property: JsonPropertyName("prefix")]  char Prefix,
    [property: JsonPropertyName("meaning")] string Meaning);

public sealed record ElectricalHealthResult(
    [property: JsonPropertyName("layerKey")]            IReadOnlyList<ElectricalLayerSpec> LayerKey,
    [property: JsonPropertyName("iecPrefixes")]         IReadOnlyList<IecPrefixSpec> IecPrefixes,
    [property: JsonPropertyName("supportedStyles")]     IReadOnlyList<string> SupportedStyles,
    [property: JsonPropertyName("defaultStyle")]        string DefaultStyle,
    [property: JsonPropertyName("defaultUnitSizeMm")]   double DefaultUnitSizeMm,
    [property: JsonPropertyName("bundledBlocks")]       IReadOnlyList<string> BundledBlocks,
    [property: JsonPropertyName("category")]            string Category,
    [property: JsonPropertyName("version")]             string Version);

#endregion
