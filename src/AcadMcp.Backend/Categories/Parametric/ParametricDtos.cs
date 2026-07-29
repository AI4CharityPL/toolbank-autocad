// DTOs for acad-parametric. JsonPropertyName matches plugin + gateway payloads (rule 22).

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Parametric;

#region infrastructure

public sealed record LayerEnsureOutcome(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("status")]  string Status,
    [property: JsonPropertyName("color")]   int? AciColor,
    [property: JsonPropertyName("linetype")] string? Linetype,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm,
    [property: JsonPropertyName("error")]   string? Error = null);

public sealed record EnsureParametricLayersArgs();

public sealed record EnsureParametricLayersResult(
    [property: JsonPropertyName("layers")]        IReadOnlyList<LayerEnsureOutcome> Layers,
    [property: JsonPropertyName("createdCount")]  int CreatedCount,
    [property: JsonPropertyName("existingCount")] int ExistingCount);

#endregion

#region inventory

public sealed record ListConstraintEntitiesArgs(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null);

public sealed record ConstraintEntityInfo(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("className")] string ClassName,
    [property: JsonPropertyName("layer")]     string Layer);

public sealed record ListConstraintEntitiesResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<ConstraintEntityInfo> Entities,
    [property: JsonPropertyName("count")]    int Count);

#endregion

#region dynamic blocks

public sealed record DynamicBlockPropertyInfo(
    [property: JsonPropertyName("propertyName")] string PropertyName,
    [property: JsonPropertyName("value")]        JsonElement? Value,
    [property: JsonPropertyName("readOnly")]     bool ReadOnly,
    [property: JsonPropertyName("unitsType")]    string UnitsType,
    [property: JsonPropertyName("clrType")]      string ClrType);

public sealed record GetDynamicBlockPropertiesArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record GetDynamicBlockPropertiesResult(
    [property: JsonPropertyName("isDynamicBlock")]     bool IsDynamicBlock,
    [property: JsonPropertyName("effectiveBlockName")] string? EffectiveBlockName,
    [property: JsonPropertyName("properties")]         IReadOnlyList<DynamicBlockPropertyInfo> Properties);

public sealed record SetDynamicBlockPropertyArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("propertyName")]  string PropertyName,
    [property: JsonPropertyName("value")]         JsonElement Value);

public sealed record SetDynamicBlockPropertyResult(
    [property: JsonPropertyName("ok")]           bool Ok,
    [property: JsonPropertyName("propertyName")] string PropertyName,
    [property: JsonPropertyName("value")]        JsonElement? Value);

#endregion

#region introspection

public sealed record ParametricHealthArgs();

public sealed record ParametricLayerSpec(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("aciColor")]     int AciColor,
    [property: JsonPropertyName("linetype")]     string Linetype,
    [property: JsonPropertyName("lineweightMm")] double LineweightMm,
    [property: JsonPropertyName("plottable")]    bool Plottable,
    [property: JsonPropertyName("purpose")]      string Purpose);

public sealed record ParametricHealthResult(
    [property: JsonPropertyName("layerKey")]          IReadOnlyList<ParametricLayerSpec> LayerKey,
    [property: JsonPropertyName("bundledBlocks")]     IReadOnlyList<string> BundledBlocks,
    [property: JsonPropertyName("category")]          string Category,
    [property: JsonPropertyName("version")]           string Version,
    [property: JsonPropertyName("dynamicAnglePolicy")] string DynamicAnglePolicy);

#endregion
