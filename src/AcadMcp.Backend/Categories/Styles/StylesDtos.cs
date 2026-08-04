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
