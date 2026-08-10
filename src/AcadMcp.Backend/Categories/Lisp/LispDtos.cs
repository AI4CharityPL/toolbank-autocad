// Typed DTOs for the acad-lisp category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Lisp;

public sealed record LispEvalArgs(
    [property: JsonPropertyName("source")] string Source);

public sealed record LispLoadArgs(
    [property: JsonPropertyName("path")] string Path);

public sealed record LispSymbolsArgs(
    [property: JsonPropertyName("pattern")] string? Pattern = null,
    [property: JsonPropertyName("limit")]   int? Limit = null);

public sealed record LispCommandArgs(
    [property: JsonPropertyName("tokens")] IReadOnlyList<string> Tokens);

public sealed record LispSysvarGetArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record LispSysvarSetArgs(
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("value")] JsonNode Value);

public sealed record LispPurgeArgs();

public sealed record LispEvalResult(
    [property: JsonPropertyName("source")]     string Source,
    [property: JsonPropertyName("value")]      JsonNode? Value,
    [property: JsonPropertyName("valueTypes")] string ValueTypes,
    [property: JsonPropertyName("printed")]    string? Printed,
    [property: JsonPropertyName("route")]      string Route,
    [property: JsonPropertyName("routeNotes")] string? RouteNotes,
    [property: JsonPropertyName("note")]       string Note);

public sealed record LispLoadResult(
    [property: JsonPropertyName("path")]       string Path,
    [property: JsonPropertyName("value")]      JsonNode? Value,
    [property: JsonPropertyName("valueTypes")] string ValueTypes,
    [property: JsonPropertyName("route")]      string? Route,
    [property: JsonPropertyName("note")]       string Note);

public sealed record LispSymbolsResult(
    [property: JsonPropertyName("total")]          int Total,
    [property: JsonPropertyName("count")]          int Count,
    [property: JsonPropertyName("symbols")]        IReadOnlyList<string> Symbols,
    [property: JsonPropertyName("commandSymbols")] IReadOnlyList<string> CommandSymbols,
    [property: JsonPropertyName("truncated")]      bool Truncated,
    [property: JsonPropertyName("note")]           string Note);

public sealed record LispCommandResult(
    [property: JsonPropertyName("tokens")]          IReadOnlyList<string> Tokens,
    [property: JsonPropertyName("entitiesBefore")]  int EntitiesBefore,
    [property: JsonPropertyName("entitiesAfter")]   int EntitiesAfter,
    [property: JsonPropertyName("entitiesAdded")]   int EntitiesAdded,
    [property: JsonPropertyName("note")]            string Note);

public sealed record LispScriptResult(
    [property: JsonPropertyName("path")]           string Path,
    [property: JsonPropertyName("entitiesBefore")] int EntitiesBefore,
    [property: JsonPropertyName("entitiesAfter")]  int EntitiesAfter,
    [property: JsonPropertyName("entitiesAdded")]  int EntitiesAdded,
    [property: JsonPropertyName("note")]           string Note);

public sealed record LispNetloadResult(
    [property: JsonPropertyName("path")]   string Path,
    [property: JsonPropertyName("loaded")] bool Loaded,
    [property: JsonPropertyName("note")]   string Note);

public sealed record LispModulesResult(
    [property: JsonPropertyName("total")]   int Total,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("modules")] IReadOnlyList<string> Modules,
    [property: JsonPropertyName("note")]    string Note);

public sealed record LispSysvarResult(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("value")]       JsonNode? Value,
    [property: JsonPropertyName("clrType")]     string? ClrType,
    [property: JsonPropertyName("note")]        string Note);

public sealed record LispSysvarSetResult(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("valueBefore")] string? ValueBefore,
    [property: JsonPropertyName("value")]       string? Value,
    [property: JsonPropertyName("clrType")]     string? ClrType,
    [property: JsonPropertyName("note")]        string Note);

public sealed record LispSysvarInfo(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("group")]   string Group,
    [property: JsonPropertyName("value")]   string? Value,
    [property: JsonPropertyName("clrType")] string? ClrType);

public sealed record LispSysvarListResult(
    [property: JsonPropertyName("count")]      int Count,
    [property: JsonPropertyName("variables")]  IReadOnlyList<LispSysvarInfo> Variables,
    [property: JsonPropertyName("notPresent")] int NotPresent,
    [property: JsonPropertyName("note")]       string Note);

public sealed record LispPurgeResult(
    [property: JsonPropertyName("registeredBefore")] int RegisteredBefore,
    [property: JsonPropertyName("registeredAfter")]  int RegisteredAfter,
    [property: JsonPropertyName("purgedCount")]      int PurgedCount,
    [property: JsonPropertyName("purged")]           IReadOnlyList<string> Purged,
    [property: JsonPropertyName("remaining")]        IReadOnlyList<string> Remaining,
    [property: JsonPropertyName("note")]             string Note);
