// DTOs for the acad-fields category.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Fields;

public sealed record EmptyFieldsArgs();

/// <summary>Common placement options shared by every insert_field_* tool.</summary>
public sealed record FieldPlacementArgs(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

public sealed record FieldDateArgs(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("format")]    string Format = "yyyy-MM-dd",
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

public sealed record FieldFilenameArgs(
    [property: JsonPropertyName("position")]     Point3dDto Position,
    [property: JsonPropertyName("includePath")]  bool IncludePath = false,
    [property: JsonPropertyName("includeExtension")] bool IncludeExtension = false,
    [property: JsonPropertyName("height")]       double? Height = null,
    [property: JsonPropertyName("layer")]        string? Layer = null,
    [property: JsonPropertyName("textStyle")]    string? TextStyle = null,
    [property: JsonPropertyName("prefix")]       string? Prefix = null,
    [property: JsonPropertyName("suffix")]       string? Suffix = null);

public sealed record FieldObjectPropertyArgs(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("property")]  string Property,
    [property: JsonPropertyName("format")]    string? Format = null,
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

public sealed record FieldSystemVariableArgs(
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("variable")]  string Variable,
    [property: JsonPropertyName("height")]    double? Height = null,
    [property: JsonPropertyName("layer")]     string? Layer = null,
    [property: JsonPropertyName("textStyle")] string? TextStyle = null,
    [property: JsonPropertyName("prefix")]    string? Prefix = null,
    [property: JsonPropertyName("suffix")]    string? Suffix = null);

public sealed record FieldRawArgs(
    [property: JsonPropertyName("position")]   Point3dDto Position,
    [property: JsonPropertyName("expression")] string Expression,
    [property: JsonPropertyName("height")]     double? Height = null,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    [property: JsonPropertyName("textStyle")]  string? TextStyle = null);

public sealed record FieldHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record UpdateFieldsArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string>? Handles = null);

public sealed record FieldEvalModeArgs(
    [property: JsonPropertyName("onOpen")]  bool OnOpen = true,
    [property: JsonPropertyName("onSave")]  bool OnSave = true,
    [property: JsonPropertyName("onPlot")]  bool OnPlot = true,
    [property: JsonPropertyName("onRegen")] bool OnRegen = true);

// ─────────────── results ───────────────

public sealed record FieldInfo(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("layer")]      string Layer,
    [property: JsonPropertyName("expression")] string Expression,
    [property: JsonPropertyName("evaluated")]  string Evaluated,
    [property: JsonPropertyName("kind")]       string Kind);

public sealed record FieldResult(
    [property: JsonPropertyName("field")] FieldInfo Field);

public sealed record FieldListResult(
    [property: JsonPropertyName("fields")] IReadOnlyList<FieldInfo> Fields,
    [property: JsonPropertyName("count")]  int Count);

public sealed record FieldAffected(
    [property: JsonPropertyName("affected")] int Affected,
    [property: JsonPropertyName("handle")]   string? Handle = null);

public sealed record FieldEvalModeResult(
    [property: JsonPropertyName("onOpen")]   bool OnOpen,
    [property: JsonPropertyName("onSave")]   bool OnSave,
    [property: JsonPropertyName("onPlot")]   bool OnPlot,
    [property: JsonPropertyName("onRegen")]  bool OnRegen,
    [property: JsonPropertyName("fieldEval")] int FieldEval);
