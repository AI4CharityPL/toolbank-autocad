// Typed DTOs for the acad-materials category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Materials;

public sealed record MaterialCreateArgs(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("description")]   string? Description = null,
    [property: JsonPropertyName("diffuse")]       ColorDto? Diffuse = null,
    [property: JsonPropertyName("diffuseFactor")] double? DiffuseFactor = null,
    [property: JsonPropertyName("specular")]      ColorDto? Specular = null,
    [property: JsonPropertyName("gloss")]         double? Gloss = null,
    [property: JsonPropertyName("opacity")]       double? Opacity = null);

public sealed record MaterialModifyArgs(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("description")]   string? Description = null,
    [property: JsonPropertyName("diffuse")]       ColorDto? Diffuse = null,
    [property: JsonPropertyName("diffuseFactor")] double? DiffuseFactor = null,
    [property: JsonPropertyName("specular")]      ColorDto? Specular = null,
    [property: JsonPropertyName("gloss")]         double? Gloss = null,
    [property: JsonPropertyName("opacity")]       double? Opacity = null);

public sealed record MaterialsNoArgs();

public sealed record MaterialAssignArgs(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record MaterialHandlesArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record MaterialDeleteArgs(
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("force")] bool? Force = null);

public sealed record MaterialChannelColor(
    [property: JsonPropertyName("r")]      int R,
    [property: JsonPropertyName("g")]      int G,
    [property: JsonPropertyName("b")]      int B,
    [property: JsonPropertyName("factor")] double Factor,
    [property: JsonPropertyName("method")] string Method);

public sealed record MaterialInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("diffuse")]     MaterialChannelColor Diffuse,
    [property: JsonPropertyName("specular")]    MaterialChannelColor Specular,
    [property: JsonPropertyName("gloss")]       double Gloss,
    [property: JsonPropertyName("opacity")]     double Opacity,
    [property: JsonPropertyName("handle")]      string Handle);

public sealed record MaterialResult(
    [property: JsonPropertyName("material")] MaterialInfo Material,
    [property: JsonPropertyName("note")]     string Note);

public sealed record MaterialModifyResult(
    [property: JsonPropertyName("changed")]  IReadOnlyList<string> Changed,
    [property: JsonPropertyName("before")]   MaterialInfo Before,
    [property: JsonPropertyName("material")] MaterialInfo Material,
    [property: JsonPropertyName("note")]     string Note);

public sealed record MaterialListResult(
    [property: JsonPropertyName("count")]     int Count,
    [property: JsonPropertyName("materials")] IReadOnlyList<MaterialInfo> Materials,
    [property: JsonPropertyName("note")]      string Note);

public sealed record MaterialAssignment(
    [property: JsonPropertyName("handle")]         string Handle,
    [property: JsonPropertyName("materialBefore")] string? MaterialBefore,
    [property: JsonPropertyName("material")]       string? Material);

public sealed record MaterialAssignResult(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("count")]    int Count,
    [property: JsonPropertyName("entities")] IReadOnlyList<MaterialAssignment> Entities,
    [property: JsonPropertyName("note")]     string Note);

public sealed record MaterialUnassignResult(
    [property: JsonPropertyName("count")]    int Count,
    [property: JsonPropertyName("entities")] IReadOnlyList<MaterialAssignment> Entities,
    [property: JsonPropertyName("note")]     string Note);

public sealed record MaterialDeleteResult(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("deleted")]   bool Deleted,
    [property: JsonPropertyName("wasUsedBy")] int WasUsedBy,
    [property: JsonPropertyName("remaining")] int Remaining,
    [property: JsonPropertyName("note")]      string Note);
