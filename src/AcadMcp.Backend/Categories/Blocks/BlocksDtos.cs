// Typed DTOs for the acad-blocks category.
// Mirrors the wire shape consumed by the plugin under "acad.blocks.<verb>".
// See rule 19, rule 28-acad-blocks-layers-files-traps.mdc.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Blocks;

public sealed record BlocksEmptyArgs();

public sealed record BlockNameArg(
    [property: JsonPropertyName("name")] string Name);

// ─────────── definition ───────────

public sealed record DefineBlockArgs(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("origin")]    Point3dDto Origin,
    [property: JsonPropertyName("members")]   IReadOnlyList<string> MemberHandles,
    [property: JsonPropertyName("eraseSource")] bool EraseSource = true,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("units")]     string? Units = null);

public sealed record DefineBlockFromFileArgs(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("dwgPath")] string DwgPath);

public sealed record RedefineBlockArgs(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("origin")]    Point3dDto Origin,
    [property: JsonPropertyName("members")]   IReadOnlyList<string> MemberHandles,
    [property: JsonPropertyName("eraseSource")] bool EraseSource = true);

// ─────────── insertion / explode ───────────

public sealed record InsertBlockArgs(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("scaleX")]      double ScaleX = 1.0,
    [property: JsonPropertyName("scaleY")]      double ScaleY = 1.0,
    [property: JsonPropertyName("scaleZ")]      double ScaleZ = 1.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("attributes")]  IReadOnlyDictionary<string, string>? Attributes = null,
    [property: JsonPropertyName("layer")]       string? Layer = null);

public sealed record ExplodeBlockRefArgs(
    [property: JsonPropertyName("handle")] string Handle);

// ─────────── attributes ───────────

public sealed record SetBlockRefAttributesArgs(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string> Attributes);

public sealed record GetBlockRefAttributesArgs(
    [property: JsonPropertyName("handle")] string Handle);

// ─────────── inventory / extract ───────────

public sealed record ExtractBlockReferencesArgs(
    [property: JsonPropertyName("name")] string? Name = null);

// ─────────── results ───────────

public sealed record BlockDefResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ownerHandle")] string OwnerHandle);

public sealed record BlockEntityResult(
    [property: JsonPropertyName("entity")] EntityHandle Entity);

public sealed record BlockRefInfo(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("scaleX")]      double ScaleX,
    [property: JsonPropertyName("scaleY")]      double ScaleY,
    [property: JsonPropertyName("scaleZ")]      double ScaleZ,
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("attributes")]  IReadOnlyDictionary<string, string> Attributes);

public sealed record BlockDefSummary(
    [property: JsonPropertyName("name")]            string Name,
    [property: JsonPropertyName("isAnonymous")]     bool IsAnonymous,
    [property: JsonPropertyName("isDynamic")]       bool IsDynamic,
    [property: JsonPropertyName("isXref")]          bool IsXref,
    [property: JsonPropertyName("entityCount")]     int EntityCount,
    [property: JsonPropertyName("description")]     string? Description);

public sealed record BlockListResult(
    [property: JsonPropertyName("blocks")] IReadOnlyList<BlockDefSummary> Blocks);

public sealed record BlockReferencesResult(
    [property: JsonPropertyName("references")] IReadOnlyList<BlockRefInfo> References);

public sealed record BlockEntitiesResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

public sealed record BlockAttributesResult(
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string> Attributes);

public sealed record BlockAffectedCount(
    [property: JsonPropertyName("affected")] int Affected);

// ─────────── library / bulk / swap (D6) ───────────

public sealed record LibraryRegisterArgs(
    [property: JsonPropertyName("libraryName")] string LibraryName,
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("recursive")]   bool Recursive = true,
    [property: JsonPropertyName("overwrite")]   bool Overwrite = false);

public sealed record LibraryRegisterResult(
    [property: JsonPropertyName("libraryName")] string LibraryName,
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("dwgCount")]    int DwgCount,
    [property: JsonPropertyName("registered")]  bool Registered);

public sealed record LibraryListArgs(
    [property: JsonPropertyName("libraryName")] string? LibraryName = null,
    [property: JsonPropertyName("includeFiles")] bool IncludeFiles = true);

public sealed record LibrarySummary(
    [property: JsonPropertyName("libraryName")] string LibraryName,
    [property: JsonPropertyName("path")]        string Path,
    [property: JsonPropertyName("recursive")]   bool Recursive,
    [property: JsonPropertyName("dwgCount")]    int DwgCount,
    [property: JsonPropertyName("files")]       IReadOnlyList<string>? Files);

public sealed record LibraryListResult(
    [property: JsonPropertyName("libraries")] IReadOnlyList<LibrarySummary> Libraries);

public sealed record BulkInsertItem(
    [property: JsonPropertyName("blockName")]   string BlockName,
    [property: JsonPropertyName("position")]    Point3dDto Position,
    [property: JsonPropertyName("scaleX")]      double ScaleX = 1.0,
    [property: JsonPropertyName("scaleY")]      double ScaleY = 1.0,
    [property: JsonPropertyName("scaleZ")]      double ScaleZ = 1.0,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]       string? Layer = null,
    [property: JsonPropertyName("attributes")]  IReadOnlyDictionary<string, string>? Attributes = null);

public sealed record BulkInsertArgs(
    [property: JsonPropertyName("libraryName")] string? LibraryName = null,
    [property: JsonPropertyName("items")]       IReadOnlyList<BulkInsertItem> Items = default!,
    [property: JsonPropertyName("autoImport")]  bool AutoImport = true);

public sealed record BulkInsertResult(
    [property: JsonPropertyName("inserted")]  IReadOnlyList<EntityHandle> Inserted,
    [property: JsonPropertyName("imported")]  IReadOnlyList<string> Imported,
    [property: JsonPropertyName("skipped")]   int Skipped);

public sealed record SwapBlockArgs(
    [property: JsonPropertyName("oldName")]        string OldName,
    [property: JsonPropertyName("newName")]        string NewName,
    [property: JsonPropertyName("keepAttributes")] bool KeepAttributes = true,
    [property: JsonPropertyName("autoImport")]     bool AutoImport = true,
    [property: JsonPropertyName("libraryName")]    string? LibraryName = null);

public sealed record SwapBlockResult(
    [property: JsonPropertyName("replaced")] int Replaced,
    [property: JsonPropertyName("imported")] bool Imported);
