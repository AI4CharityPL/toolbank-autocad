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
