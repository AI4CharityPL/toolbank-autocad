// DTOs for the acad-styles category. Wire names are [JsonPropertyName]; see rule 22.
//
// `properties` is a DICTIONARY of property name -> value, not an array. The schema builder
// checks dictionaries before collections for exactly this shape; getting that order wrong is
// what once advertised dictionary parameters as arrays across the bank.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Styles;

public sealed record EmptyStylesArgs();

public sealed record DimStyleNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record CreateDimStyleArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record ModifyDimStyleArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record CopyDimStyleArgs(
    [property: JsonPropertyName("sourceName")]  string SourceName,
    [property: JsonPropertyName("newName")]     string NewName,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false);

// ─────────────── results ───────────────

public sealed record DimStylePropertyInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("dimVar")]      string DimVar,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("min")]         double? Min,
    [property: JsonPropertyName("max")]         double? Max);

public sealed record DimStylePropertyListResult(
    [property: JsonPropertyName("properties")] IReadOnlyList<DimStylePropertyInfo> Properties,
    [property: JsonPropertyName("count")]      int Count);

public sealed record DimStyleInfo(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("isCurrent")]  bool IsCurrent,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record DimStyleResult(
    [property: JsonPropertyName("dimStyle")] DimStyleInfo DimStyle,
    [property: JsonPropertyName("created")]  bool? Created = null,
    [property: JsonPropertyName("applied")]  IReadOnlyList<string>? Applied = null,
    [property: JsonPropertyName("note")]     string? Note = null,
    [property: JsonPropertyName("copiedFrom")] string? CopiedFrom = null);

public sealed record StylesAffected(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("name")]     string? Name = null);

public sealed record MLeaderStyleNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record CreateMLeaderStyleArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record ModifyMLeaderStyleArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record MLeaderStylePropertyInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("apiName")]     string ApiName,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("min")]         double? Min,
    [property: JsonPropertyName("max")]         double? Max);

public sealed record MLeaderStylePropertyListResult(
    [property: JsonPropertyName("properties")] IReadOnlyList<MLeaderStylePropertyInfo> Properties,
    [property: JsonPropertyName("count")]      int Count);

public sealed record MLeaderStyleInfo(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("isCurrent")]  bool IsCurrent,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record MLeaderStyleResult(
    [property: JsonPropertyName("mleaderStyle")] MLeaderStyleInfo MLeaderStyle,
    [property: JsonPropertyName("created")]      bool? Created = null,
    [property: JsonPropertyName("applied")]      IReadOnlyList<string>? Applied = null,
    [property: JsonPropertyName("note")]         string? Note = null);

public sealed record MLeaderStyleListResult(
    [property: JsonPropertyName("mleaderStyles")] IReadOnlyList<MLeaderStyleInfo> MLeaderStyles,
    [property: JsonPropertyName("count")]         int Count);

public sealed record TableStyleNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record CreateTableStyleArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("properties")]  IReadOnlyDictionary<string, double>? Properties = null,
    [property: JsonPropertyName("makeCurrent")] bool MakeCurrent = false,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record ModifyTableStyleArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record TableStylePropertyInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("apiName")]     string ApiName,
    [property: JsonPropertyName("rowType")]     string? RowType,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("min")]         double? Min,
    [property: JsonPropertyName("max")]         double? Max);

public sealed record TableStylePropertyListResult(
    [property: JsonPropertyName("properties")] IReadOnlyList<TableStylePropertyInfo> Properties,
    [property: JsonPropertyName("count")]      int Count);

public sealed record TableStyleInfo(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("isCurrent")]  bool IsCurrent,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record TableStyleResult(
    [property: JsonPropertyName("tableStyle")] TableStyleInfo TableStyle,
    [property: JsonPropertyName("created")]    bool? Created = null,
    [property: JsonPropertyName("applied")]    IReadOnlyList<string>? Applied = null,
    [property: JsonPropertyName("note")]       string? Note = null);

public sealed record TableStyleListResult(
    [property: JsonPropertyName("tableStyles")] IReadOnlyList<TableStyleInfo> TableStyles,
    [property: JsonPropertyName("count")]       int Count);

// ─────────────── multiline styles (roadmap 2.3) ───────────────
//
// An MLINE style is a different shape from the three above: it is not a bag of scalar
// properties but an ordered list of parallel line ELEMENTS, each with its own offset from the
// centreline, colour and linetype. A wall drawn as one MLINE is exactly that - two elements at
// +100 and -100 for a 200mm wall. So these take an array, not a properties dictionary, and no
// list_mlinestyle_properties exists because there is no property catalogue to advertise.

public sealed record MlineElementSpec(
    [property: JsonPropertyName("offset")]      double Offset,
    [property: JsonPropertyName("colorIndex")]  int? ColorIndex = null,
    [property: JsonPropertyName("linetype")]    string? Linetype = null);

public sealed record CreateMlineStyleArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("elements")]    IReadOnlyList<MlineElementSpec> Elements,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("showMiters")]  bool ShowMiters = false,
    [property: JsonPropertyName("startAngle")]  double? StartAngle = null,
    [property: JsonPropertyName("endAngle")]    double? EndAngle = null,
    [property: JsonPropertyName("startCap")]    string? StartCap = null,
    [property: JsonPropertyName("endCap")]      string? EndCap = null,
    [property: JsonPropertyName("fillColorIndex")] int? FillColorIndex = null,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record ModifyMlineStyleArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("elements")]    IReadOnlyList<MlineElementSpec>? Elements = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("showMiters")]  bool? ShowMiters = null,
    [property: JsonPropertyName("startAngle")]  double? StartAngle = null,
    [property: JsonPropertyName("endAngle")]    double? EndAngle = null,
    [property: JsonPropertyName("startCap")]    string? StartCap = null,
    [property: JsonPropertyName("endCap")]      string? EndCap = null,
    [property: JsonPropertyName("fillColorIndex")] int? FillColorIndex = null);

public sealed record MlineStyleInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("elements")]    IReadOnlyList<MlineElementSpec> Elements,
    [property: JsonPropertyName("totalWidth")]  double TotalWidth,
    [property: JsonPropertyName("showMiters")]  bool ShowMiters,
    [property: JsonPropertyName("startAngle")]  double StartAngle,
    [property: JsonPropertyName("endAngle")]    double EndAngle,
    [property: JsonPropertyName("startCap")]    string StartCap,
    [property: JsonPropertyName("endCap")]      string EndCap,
    [property: JsonPropertyName("filled")]      bool Filled,
    [property: JsonPropertyName("inUse")]       bool InUse);

public sealed record MlineStyleListResult(
    [property: JsonPropertyName("styles")] IReadOnlyList<MlineStyleInfo> Styles,
    [property: JsonPropertyName("count")]  int Count);

public sealed record MlineStyleResult(
    [property: JsonPropertyName("mlineStyle")] MlineStyleInfo MlineStyle,
    [property: JsonPropertyName("created")]    bool Created,
    [property: JsonPropertyName("note")]       string? Note = null);

// ─────────────── layer filters (roadmap 2.3) ───────────────
//
// Two kinds, and the difference matters to a caller. A PROPERTY filter carries an expression
// evaluated against every layer as the drawing changes, so a new layer named A-WALL-NEW joins
// it automatically. A GROUP filter holds a fixed list of layers and never changes on its own.
// Both live in one tree, which is why list_layer_filters reports `kind` rather than splitting
// into two tools.

public sealed record CreateLayerFilterArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("expression")] string Expression,
    [property: JsonPropertyName("parent")]     string? Parent = null,
    [property: JsonPropertyName("overwrite")]  bool Overwrite = false);

public sealed record CreateLayerGroupFilterArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("layers")]      IReadOnlyList<string> Layers,
    [property: JsonPropertyName("parent")]      string? Parent = null,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record LayerFilterNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record LayerFilterInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("expression")]  string? Expression,
    [property: JsonPropertyName("layers")]      IReadOnlyList<string>? Layers,
    [property: JsonPropertyName("matchCount")]  int MatchCount,
    [property: JsonPropertyName("parent")]      string? Parent,
    [property: JsonPropertyName("isCurrent")]   bool IsCurrent,
    [property: JsonPropertyName("allowDelete")] bool AllowDelete);

public sealed record LayerFilterListResult(
    [property: JsonPropertyName("filters")] IReadOnlyList<LayerFilterInfo> Filters,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("current")] string? Current);

public sealed record LayerFilterResult(
    [property: JsonPropertyName("filter")]  LayerFilterInfo Filter,
    [property: JsonPropertyName("created")] bool Created);

// alsoDeleted is not optional decoration. Deleting a filter removes everything nested under it,
// and the plugin computes that list - but this record originally omitted the field, so the value
// was produced and then thrown away by deserialisation. The caller saw {name, deleted:true} and
// no indication that a second filter had gone with it. A cascade nobody is told about is the
// defect; reporting it is the fix.
public sealed record LayerFilterDeleteResult(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("deleted")]     bool Deleted,
    [property: JsonPropertyName("alsoDeleted")] IReadOnlyList<string> AlsoDeleted,
    [property: JsonPropertyName("current")]     string? Current);

// ─────────────── table cell styles, visual styles, point display (roadmap 2.3) ───────────────

public sealed record SetTableCellStyleArgs(
    [property: JsonPropertyName("name")]                 string Name,
    [property: JsonPropertyName("row")]                  string Row,
    [property: JsonPropertyName("alignment")]            string? Alignment = null,
    [property: JsonPropertyName("colorIndex")]           int? ColorIndex = null,
    [property: JsonPropertyName("backgroundColorIndex")] int? BackgroundColorIndex = null);

public sealed record TableCellStyleInfo(
    [property: JsonPropertyName("row")]                  string Row,
    [property: JsonPropertyName("textHeight")]           double TextHeight,
    [property: JsonPropertyName("alignment")]            string Alignment,
    [property: JsonPropertyName("colorIndex")]           int? ColorIndex,
    [property: JsonPropertyName("backgroundColorNone")]  bool BackgroundColorNone,
    [property: JsonPropertyName("backgroundColorIndex")] int? BackgroundColorIndex);

public sealed record TableCellStyleResult(
    [property: JsonPropertyName("cell")]       TableCellStyleInfo Cell,
    [property: JsonPropertyName("applied")]    IReadOnlyList<string> Applied,
    [property: JsonPropertyName("tableStyle")] string TableStyle);

public sealed record VisualStyleInfo(
    [property: JsonPropertyName("name")]            string Name,
    [property: JsonPropertyName("type")]            string Type,
    [property: JsonPropertyName("description")]     string Description,
    [property: JsonPropertyName("internalUseOnly")] bool InternalUseOnly);

public sealed record VisualStyleListResult(
    [property: JsonPropertyName("styles")]  IReadOnlyList<VisualStyleInfo> Styles,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("presets")] IReadOnlyList<string> Presets);

public sealed record CreateVisualStyleArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("basedOn")]     string BasedOn,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record VisualStyleResult(
    [property: JsonPropertyName("visualStyle")] VisualStyleInfo VisualStyle,
    [property: JsonPropertyName("created")]     bool Created);

public sealed record SetPointDisplayArgs(
    [property: JsonPropertyName("glyph")]    string? Glyph = null,
    [property: JsonPropertyName("surround")] string? Surround = null,
    [property: JsonPropertyName("mode")]     int? Mode = null,
    [property: JsonPropertyName("size")]     double? Size = null);

public sealed record PointDisplayState(
    [property: JsonPropertyName("pdmode")] short Pdmode,
    [property: JsonPropertyName("pdsize")] double Pdsize);

public sealed record PointDisplayResult(
    [property: JsonPropertyName("before")] PointDisplayState Before,
    [property: JsonPropertyName("after")]  PointDisplayState After,
    [property: JsonPropertyName("note")]   string Note);

// ─────────────── dimension overrides + cross-drawing import (roadmap 2.3) ───────────────

public sealed record DimOverrideQueryArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record ApplyDimOverrideArgs(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, double> Properties);

public sealed record DimOverrideInfo(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("dimVar")]     string DimVar,
    [property: JsonPropertyName("value")]      double Value,
    [property: JsonPropertyName("styleValue")] double StyleValue);

public sealed record DimOverrideListResult(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("styleName")] string StyleName,
    [property: JsonPropertyName("overrides")] IReadOnlyList<DimOverrideInfo> Overrides,
    [property: JsonPropertyName("count")]     int Count,
    [property: JsonPropertyName("note")]      string? Note = null);

public sealed record DimOverrideApplyResult(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("styleName")] string StyleName,
    [property: JsonPropertyName("applied")]   IReadOnlyList<string> Applied,
    [property: JsonPropertyName("overrides")] IReadOnlyList<DimOverrideInfo> Overrides);

public sealed record ImportDimStyleArgs(
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("names")]     IReadOnlyList<string>? Names = null,
    [property: JsonPropertyName("overwrite")] bool Overwrite = false);

// Three outcomes, not two. A style can be new here, or already present and overwritten, or
// already present and left alone - and collapsing the middle case into either of the others
// misreports what happened to the drawing. The first version had only imported/skipped and
// reported an honest overwrite as "skipped".
public sealed record ImportDimStyleResult(
    [property: JsonPropertyName("source")]    string Source,
    [property: JsonPropertyName("requested")] IReadOnlyList<string> Requested,
    [property: JsonPropertyName("imported")]  IReadOnlyList<string> Imported,
    [property: JsonPropertyName("replaced")]  IReadOnlyList<string> Replaced,
    [property: JsonPropertyName("skipped")]   IReadOnlyList<string> Skipped,
    [property: JsonPropertyName("note")]      string? Note = null);
