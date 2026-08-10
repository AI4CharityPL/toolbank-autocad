// Typed DTOs for the acad-data category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Data;

/// One typed value. `type` is explicit rather than inferred from the JSON, because JSON cannot
/// tell 1 from 1.0 and AutoCAD very much can - an int written where a real was meant reads back
/// as a different type and breaks the round trip.
public sealed record DataValue(
    [property: JsonPropertyName("type")]  string Type,
    [property: JsonPropertyName("value")] object? Value = null,
    [property: JsonPropertyName("point")] Point3dDto? Point = null);

public sealed record XdataAttachArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("appName")] string AppName,
    [property: JsonPropertyName("data")]    IReadOnlyList<DataValue> Data);

public sealed record XdataGetArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("appName")] string? AppName = null);

public sealed record XdataDeleteArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("appName")] string AppName);

public sealed record AppNameArgs(
    [property: JsonPropertyName("appName")] string AppName);

public sealed record DataNoArgs();

public sealed record EntityHandleArgs(
    [property: JsonPropertyName("handle")] string Handle);

public sealed record DictListArgs(
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("path")]   string? Path = null);

public sealed record DictEntryArgs(
    [property: JsonPropertyName("key")]    string Key,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("path")]   string? Path = null);

public sealed record DictSetArgs(
    [property: JsonPropertyName("key")]    string Key,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("path")]   string? Path = null,
    [property: JsonPropertyName("data")]   IReadOnlyList<DataValue>? Data = null,
    [property: JsonPropertyName("nested")] bool? Nested = null);

public sealed record DictDeleteArgs(
    [property: JsonPropertyName("key")]    string Key,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("path")]   string? Path = null,
    [property: JsonPropertyName("force")]  bool? Force = null);

public sealed record XrecordCreateArgs(
    [property: JsonPropertyName("key")]             string Key,
    [property: JsonPropertyName("data")]            IReadOnlyList<DataValue> Data,
    [property: JsonPropertyName("handle")]          string? Handle = null,
    [property: JsonPropertyName("path")]            string? Path = null,
    [property: JsonPropertyName("xlateReferences")] bool? XlateReferences = null);

public sealed record XrecordReadArgs(
    [property: JsonPropertyName("key")]    string Key,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("path")]   string? Path = null);

public sealed record XrecordUpdateArgs(
    [property: JsonPropertyName("key")]    string Key,
    [property: JsonPropertyName("data")]   IReadOnlyList<DataValue> Data,
    [property: JsonPropertyName("handle")] string? Handle = null,
    [property: JsonPropertyName("path")]   string? Path = null);

// ── results ──

public sealed record XdataAttachResult(
    [property: JsonPropertyName("handle")]        string Handle,
    [property: JsonPropertyName("appName")]       string AppName,
    [property: JsonPropertyName("count")]         int Count,
    [property: JsonPropertyName("data")]          IReadOnlyList<DataValue> Data,
    [property: JsonPropertyName("appRegistered")] bool AppRegistered,
    [property: JsonPropertyName("otherApps")]     IReadOnlyList<string> OtherApps,
    [property: JsonPropertyName("note")]          string Note);

public sealed record XdataAppGroup(
    [property: JsonPropertyName("appName")] string AppName,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("data")]    IReadOnlyList<DataValue> Data);

public sealed record XdataGetResult(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("appName")] string? AppName,
    [property: JsonPropertyName("apps")]    IReadOnlyList<XdataAppGroup> Apps,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("note")]    string Note);

public sealed record XdataDeleteResult(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("appName")]         string AppName,
    [property: JsonPropertyName("deletedCount")]    int DeletedCount,
    [property: JsonPropertyName("otherAppsBefore")] IReadOnlyList<string> OtherAppsBefore,
    [property: JsonPropertyName("otherAppsAfter")]  IReadOnlyList<string> OtherAppsAfter,
    [property: JsonPropertyName("note")]            string Note);

public sealed record AppRegisterResult(
    [property: JsonPropertyName("appName")]    string AppName,
    [property: JsonPropertyName("registered")] bool Registered,
    [property: JsonPropertyName("note")]       string Note);

public sealed record AppListResult(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("apps")]  IReadOnlyList<string> Apps,
    [property: JsonPropertyName("note")]  string Note);

public sealed record ExtDictResult(
    [property: JsonPropertyName("handle")]           string Handle,
    [property: JsonPropertyName("dictionaryHandle")] string DictionaryHandle,
    [property: JsonPropertyName("entryCount")]       int EntryCount,
    [property: JsonPropertyName("note")]             string Note);

public sealed record DictEntryInfo(
    [property: JsonPropertyName("key")]          string Key,
    [property: JsonPropertyName("objectClass")]  string ObjectClass,
    [property: JsonPropertyName("isDictionary")] bool IsDictionary,
    [property: JsonPropertyName("handle")]       string Handle);

public sealed record DictListResult(
    [property: JsonPropertyName("scope")]   string Scope,
    [property: JsonPropertyName("path")]    string? Path,
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("entries")] IReadOnlyList<DictEntryInfo> Entries,
    [property: JsonPropertyName("note")]    string Note);

public sealed record DictGetResult(
    [property: JsonPropertyName("key")]          string Key,
    [property: JsonPropertyName("objectClass")]  string ObjectClass,
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("isDictionary")] bool IsDictionary,
    [property: JsonPropertyName("entryCount")]   int? EntryCount,
    [property: JsonPropertyName("data")]         IReadOnlyList<DataValue>? Data,
    [property: JsonPropertyName("note")]         string Note);

public sealed record DictSetResult(
    [property: JsonPropertyName("key")]         string Key,
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("replaced")]    bool Replaced,
    [property: JsonPropertyName("valueCount")]  int ValueCount,
    [property: JsonPropertyName("note")]        string Note);

public sealed record DictDeleteResult(
    [property: JsonPropertyName("key")]                  string Key,
    [property: JsonPropertyName("wasDictionary")]        bool WasDictionary,
    [property: JsonPropertyName("nestedEntriesRemoved")] int NestedEntriesRemoved,
    [property: JsonPropertyName("remaining")]            int Remaining,
    [property: JsonPropertyName("note")]                 string Note);

public sealed record XrecordCreateResult(
    [property: JsonPropertyName("key")]             string Key,
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("count")]           int Count,
    [property: JsonPropertyName("data")]            IReadOnlyList<DataValue> Data,
    [property: JsonPropertyName("xlateReferences")] bool XlateReferences,
    [property: JsonPropertyName("note")]            string Note);

public sealed record XrecordReadResult(
    [property: JsonPropertyName("key")]   string Key,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("data")]  IReadOnlyList<DataValue> Data,
    [property: JsonPropertyName("note")]  string Note);

public sealed record XrecordUpdateResult(
    [property: JsonPropertyName("key")]         string Key,
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("countBefore")] int CountBefore,
    [property: JsonPropertyName("count")]       int Count,
    [property: JsonPropertyName("data")]        IReadOnlyList<DataValue> Data,
    [property: JsonPropertyName("note")]        string Note);

// ── second tranche: tagging, querying, CSV ──

public sealed record TagArgs(
    [property: JsonPropertyName("handles")] IReadOnlyList<string> Handles,
    [property: JsonPropertyName("tag")]     string Tag,
    [property: JsonPropertyName("value")]   string? Value = null);

public sealed record TagListArgs(
    [property: JsonPropertyName("tag")] string? Tag = null);

public sealed record QueryArgs(
    [property: JsonPropertyName("layer")]       string? Layer = null,
    [property: JsonPropertyName("objectClass")] string? ObjectClass = null,
    [property: JsonPropertyName("colorIndex")]  int? ColorIndex = null,
    [property: JsonPropertyName("linetype")]    string? Linetype = null,
    [property: JsonPropertyName("hasXdataApp")] string? HasXdataApp = null);

public sealed record TableCsvArgs(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("overwrite")] bool? Overwrite = null);

public sealed record TaggedEntity(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("tag")]         string Tag,
    [property: JsonPropertyName("value")]       string? Value,
    [property: JsonPropertyName("previousTag")] string? PreviousTag = null);

public sealed record TagResult(
    [property: JsonPropertyName("count")]               int Count,
    [property: JsonPropertyName("tag")]                 string Tag,
    [property: JsonPropertyName("value")]               string? Value,
    [property: JsonPropertyName("replacedExistingTag")] int ReplacedExistingTag,
    [property: JsonPropertyName("entities")]            IReadOnlyList<TaggedEntity> Entities,
    [property: JsonPropertyName("note")]                string Note);

public sealed record TagCount(
    [property: JsonPropertyName("tag")]   string Tag,
    [property: JsonPropertyName("count")] int Count);

public sealed record TagListResult(
    [property: JsonPropertyName("tag")]            string? Tag,
    [property: JsonPropertyName("count")]          int Count,
    [property: JsonPropertyName("entities")]       IReadOnlyList<TaggedEntity> Entities,
    [property: JsonPropertyName("tagsInDrawing")]  IReadOnlyList<TagCount> TagsInDrawing,
    [property: JsonPropertyName("note")]           string Note);

public sealed record QueriedEntity(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")]       string? Layer,
    [property: JsonPropertyName("colorIndex")]  int ColorIndex,
    [property: JsonPropertyName("linetype")]    string? Linetype);

public sealed record QueryResult(
    [property: JsonPropertyName("scanned")]  int Scanned,
    [property: JsonPropertyName("count")]    int Count,
    [property: JsonPropertyName("entities")] IReadOnlyList<QueriedEntity> Entities,
    [property: JsonPropertyName("filters")]  QueryArgs Filters,
    [property: JsonPropertyName("note")]     string Note);

public sealed record CsvExportResult(
    [property: JsonPropertyName("path")]    string Path,
    [property: JsonPropertyName("rows")]    int Rows,
    [property: JsonPropertyName("columns")] int Columns,
    [property: JsonPropertyName("bytes")]   long Bytes,
    [property: JsonPropertyName("note")]    string Note);

public sealed record CsvImportResult(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("path")]      string Path,
    [property: JsonPropertyName("rows")]      int Rows,
    [property: JsonPropertyName("columns")]   int Columns,
    [property: JsonPropertyName("firstCell")] string? FirstCell,
    [property: JsonPropertyName("note")]      string Note);
