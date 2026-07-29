// Plugin-side DTOs for the acad-blocks category.
// Mirror Backend/Categories/Blocks/BlocksDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record BlocksEmptyArgsDto();

internal sealed record BlockNameArgDto(
    [property: JsonPropertyName("name")] string Name);

internal sealed record DefineBlockArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("members")]     IReadOnlyList<string> MemberHandles,
    [property: JsonPropertyName("eraseSource")] bool EraseSource = true,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("units")]       string? Units = null);

internal sealed record DefineBlockFromFileArgsDto(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("dwgPath")] string DwgPath);

internal sealed record RedefineBlockArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("origin")]      Point3dDto Origin,
    [property: JsonPropertyName("members")]     IReadOnlyList<string> MemberHandles,
    [property: JsonPropertyName("eraseSource")] bool EraseSource = true);

internal sealed record InsertBlockArgsDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("scaleX")]      double ScaleX = 1.0,
    [property: JsonPropertyName("scaleY")]      double ScaleY = 1.0,
    [property: JsonPropertyName("scaleZ")]      double ScaleZ = 1.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("attributes")]  IReadOnlyDictionary<string, string>? Attributes = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

internal sealed record ExplodeBlockRefArgsDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record SetBlockRefAttributesArgsDto(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string> Attributes);

internal sealed record GetBlockRefAttributesArgsDto(
    [property: JsonPropertyName("handle")] string Handle);

internal sealed record ExtractBlockReferencesArgsDto(
    [property: JsonPropertyName("name")] string? Name = null);

internal sealed record RenameBlockArgsDto(
    [property: JsonPropertyName("oldName")] string OldName,
    [property: JsonPropertyName("newName")] string NewName);

// ─────────── library / bulk / swap (D6) ───────────

internal sealed record LibraryRegisterArgsDto(
    [property: JsonPropertyName("libraryName")] string LibraryName,
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("recursive")]   bool Recursive = true,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

internal sealed record LibraryListArgsDto(
    [property: JsonPropertyName("libraryName")]  string? LibraryName = null,
    [property: JsonPropertyName("includeFiles")] bool IncludeFiles = true);

internal sealed record BulkInsertItemDto(
    [property: JsonPropertyName("blockName")]   string BlockName,
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("scaleX")]      double ScaleX = 1.0,
    [property: JsonPropertyName("scaleY")]      double ScaleY = 1.0,
    [property: JsonPropertyName("scaleZ")]      double ScaleZ = 1.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]       string? Layer = null,
    [property: JsonPropertyName("attributes")]  IReadOnlyDictionary<string, string>? Attributes = null);

internal sealed record BulkInsertArgsDto(
    [property: JsonPropertyName("libraryName")] string? LibraryName = null,
    [property: JsonPropertyName("items")]       IReadOnlyList<BulkInsertItemDto> Items = default!,
    [property: JsonPropertyName("autoImport")]  bool AutoImport = true);

internal sealed record SwapBlockArgsDto(
    [property: JsonPropertyName("oldName")]        string OldName,
    [property: JsonPropertyName("newName")]        string NewName,
    [property: JsonPropertyName("keepAttributes")] bool KeepAttributes = true,
    [property: JsonPropertyName("autoImport")]     bool AutoImport = true,
    [property: JsonPropertyName("libraryName")]    string? LibraryName = null);
