// Plugin-side DTOs for the acad-lisp category.
// Mirrors src/AcadMcp.Backend/Categories/Lisp/LispDtos.cs wire shape.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Plugin.Tools;

internal sealed record LispEvalArgsDto(
    [property: JsonPropertyName("source")] string? Source);

internal sealed record LispLoadArgsDto(
    [property: JsonPropertyName("path")] string? Path);

internal sealed record LispSymbolsArgsDto(
    [property: JsonPropertyName("pattern")] string? Pattern,
    [property: JsonPropertyName("limit")]   int? Limit);

internal sealed record LispCommandArgsDto(
    [property: JsonPropertyName("tokens")] List<string>? Tokens);

internal sealed record LispSysvarArgsDto(
    [property: JsonPropertyName("name")]  string? Name,
    [property: JsonPropertyName("value")] object? Value);

internal sealed record LispPurgeArgsDto();
