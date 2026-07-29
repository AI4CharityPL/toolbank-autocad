// Typed DTOs for the acad-boolean-ops category.
// See rule 19-tool-implementation-pattern.mdc.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.BooleanOps;

public sealed record SolidBooleanArgs(
    [property: JsonPropertyName("targetHandle")] string TargetHandle,
    [property: JsonPropertyName("toolHandles")]  IReadOnlyList<string> ToolHandles,
    [property: JsonPropertyName("eraseTools")]   bool EraseTools = true);

public sealed record RegionBooleanArgs(
    [property: JsonPropertyName("targetHandle")] string TargetHandle,
    [property: JsonPropertyName("toolHandles")]  IReadOnlyList<string> ToolHandles,
    [property: JsonPropertyName("eraseTools")]   bool EraseTools = true);

public sealed record CreateRegionArgs(
    [property: JsonPropertyName("curveHandles")] IReadOnlyList<string> CurveHandles,
    [property: JsonPropertyName("eraseSource")]  bool EraseSource = false,
    [property: JsonPropertyName("layer")]        string? Layer = null);

public sealed record CheckIntersectArgs(
    [property: JsonPropertyName("handleA")] string HandleA,
    [property: JsonPropertyName("handleB")] string HandleB);

public sealed record SeparateSolidArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("layer")]  string? Layer = null);

public sealed record BooleanResult(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

public sealed record EntitiesResultBool(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record IntersectCheckResult(
    [property: JsonPropertyName("intersect")] bool Intersect,
    [property: JsonPropertyName("relation")]  string Relation);
