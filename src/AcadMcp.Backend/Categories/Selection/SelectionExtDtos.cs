// Typed DTOs for the phase 3.4 selection extensions. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Selection;

public sealed record SimilarArgs(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("matchLayer")]    bool? MatchLayer = null,
    [property: JsonPropertyName("matchColor")]    bool? MatchColor = null,
    [property: JsonPropertyName("matchLinetype")] bool? MatchLinetype = null);

public sealed record RangeArgs(
    [property: JsonPropertyName("min")] double? Min = null,
    [property: JsonPropertyName("max")] double? Max = null);

public sealed record DuplicatesArgs(
    [property: JsonPropertyName("tolerance")] double? Tolerance = null);

public sealed record LastArgs(
    [property: JsonPropertyName("count")] int? Count = null);

public sealed record HandlesArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles);

public sealed record SelExtNoArgs();

public sealed record FilterCreateArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("layer")]       string? Layer = null,
    [property: JsonPropertyName("objectClass")] string? ObjectClass = null,
    [property: JsonPropertyName("colorIndex")]  int? ColorIndex = null,
    [property: JsonPropertyName("min")]         double? Min = null,
    [property: JsonPropertyName("max")]         double? Max = null,
    [property: JsonPropertyName("rangeKind")]   string? RangeKind = null);

public sealed record FilterNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record SelEntity(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("colorIndex")]  int ColorIndex,
    [property: JsonPropertyName("visible")]     bool Visible);

public sealed record SimilarMatchedOn(
    [property: JsonPropertyName("objectClass")] bool ObjectClass,
    [property: JsonPropertyName("layer")]       bool Layer,
    [property: JsonPropertyName("color")]       bool Color,
    [property: JsonPropertyName("linetype")]    bool Linetype);

public sealed record SimilarResult(
    [property: JsonPropertyName("referenceHandle")] string ReferenceHandle,
    [property: JsonPropertyName("referenceClass")]  string ReferenceClass,
    [property: JsonPropertyName("matchedOn")]       SimilarMatchedOn MatchedOn,
    [property: JsonPropertyName("count")]           int Count,
    [property: JsonPropertyName("entities")]        IReadOnlyList<SelEntity> Entities,
    [property: JsonPropertyName("note")]            string Note);

public sealed record MeasuredEntity(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("area")]        double? Area = null,
    [property: JsonPropertyName("length")]      double? Length = null);

public sealed record RangeResult(
    [property: JsonPropertyName("min")]        double? Min,
    [property: JsonPropertyName("max")]        double? Max,
    [property: JsonPropertyName("scanned")]    int Scanned,
    [property: JsonPropertyName("measurable")] int Measurable,
    [property: JsonPropertyName("count")]      int Count,
    [property: JsonPropertyName("entities")]   IReadOnlyList<MeasuredEntity> Entities,
    [property: JsonPropertyName("note")]       string Note);

public sealed record DuplicateGroup(
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("count")]       int Count,
    [property: JsonPropertyName("keep")]        string Keep,
    [property: JsonPropertyName("duplicates")]  IReadOnlyList<string> Duplicates);

public sealed record DuplicatesResult(
    [property: JsonPropertyName("scanned")]        int Scanned,
    [property: JsonPropertyName("groupCount")]     int GroupCount,
    [property: JsonPropertyName("duplicateCount")] int DuplicateCount,
    [property: JsonPropertyName("groups")]         IReadOnlyList<DuplicateGroup> Groups,
    [property: JsonPropertyName("tolerance")]      double Tolerance,
    [property: JsonPropertyName("note")]           string Note);

public sealed record LastResult(
    [property: JsonPropertyName("count")]                 int Count,
    [property: JsonPropertyName("entities")]              IReadOnlyList<SelEntity> Entities,
    [property: JsonPropertyName("editorSelectLastCount")] int EditorSelectLastCount,
    [property: JsonPropertyName("editorSelectLastNote")]  string? EditorSelectLastNote,
    [property: JsonPropertyName("note")]                  string Note);

public sealed record HideResult(
    [property: JsonPropertyName("hidden")]        int Hidden,
    [property: JsonPropertyName("alreadyHidden")] int AlreadyHidden,
    [property: JsonPropertyName("requested")]     int Requested,
    [property: JsonPropertyName("note")]          string Note);

public sealed record IsolateResult(
    [property: JsonPropertyName("kept")]   int Kept,
    [property: JsonPropertyName("hidden")] int Hidden,
    [property: JsonPropertyName("note")]   string Note);

public sealed record UnisolateResult(
    [property: JsonPropertyName("shown")]          int Shown,
    [property: JsonPropertyName("alreadyVisible")] int AlreadyVisible,
    [property: JsonPropertyName("note")]           string Note);

public sealed record FilterInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("objectClass")] string? ObjectClass,
    [property: JsonPropertyName("colorIndex")]  int? ColorIndex,
    [property: JsonPropertyName("min")]         double? Min,
    [property: JsonPropertyName("max")]         double? Max,
    [property: JsonPropertyName("rangeKind")]   string? RangeKind);

public sealed record FilterCreateResult(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("objectClass")] string? ObjectClass,
    [property: JsonPropertyName("colorIndex")]  int? ColorIndex,
    [property: JsonPropertyName("min")]         double? Min,
    [property: JsonPropertyName("max")]         double? Max,
    [property: JsonPropertyName("rangeKind")]   string? RangeKind,
    [property: JsonPropertyName("note")]        string Note);

public sealed record FilterListResult(
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("filters")] IReadOnlyList<FilterInfo> Filters,
    [property: JsonPropertyName("note")]    string Note);

public sealed record FilterApplyResult(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("criteria")] FilterInfo Criteria,
    [property: JsonPropertyName("scanned")]  int Scanned,
    [property: JsonPropertyName("count")]    int Count,
    [property: JsonPropertyName("entities")] IReadOnlyList<SelEntity> Entities,
    [property: JsonPropertyName("note")]     string Note);
