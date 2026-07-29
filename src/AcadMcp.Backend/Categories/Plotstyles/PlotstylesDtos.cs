// DTOs for the 3 composite tools in the acad-plotstyles category.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Plotstyles;

// ─────────── ensure_ctb ───────────

public sealed record EnsureCtbArgs(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("sourcePath")]    string? SourcePath = null,
    [property: JsonPropertyName("overwrite")]     bool Overwrite = false);

public sealed record EnsureCtbResult(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("directory")]     string? Directory,
    [property: JsonPropertyName("targetPath")]    string? TargetPath,
    [property: JsonPropertyName("existedBefore")] bool ExistedBefore,
    [property: JsonPropertyName("copied")]        bool Copied,
    [property: JsonPropertyName("sourceResolved")] string? SourceResolved,
    [property: JsonPropertyName("listedAfter")]   bool ListedAfter,
    [property: JsonPropertyName("notes")]         IReadOnlyList<string> Notes);

// ─────────── apply_plotstyle_to_layout ───────────

public sealed record ApplyPlotstyleToLayoutArgs(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("plotstyle")]  string Plotstyle,
    [property: JsonPropertyName("ensure")]     bool Ensure = true,
    [property: JsonPropertyName("sourcePath")] string? SourcePath = null);

public sealed record ApplyPlotstyleToLayoutResult(
    [property: JsonPropertyName("layoutName")] string LayoutName,
    [property: JsonPropertyName("plotstyle")]  string Plotstyle,
    [property: JsonPropertyName("applied")]    bool Applied,
    [property: JsonPropertyName("ensureResult")] EnsureCtbResult? EnsureResult,
    [property: JsonPropertyName("notes")]      IReadOnlyList<string> Notes);

// ─────────── list_plotstyles ───────────

public sealed record ListPlotstylesArgs(
    [property: JsonPropertyName("filter")] string? Filter = null);  // "ctb" | "stb" | null = all

public sealed record ListPlotstylesResult(
    [property: JsonPropertyName("names")]       IReadOnlyList<string> Names,
    [property: JsonPropertyName("ctb")]         IReadOnlyList<string> Ctb,
    [property: JsonPropertyName("stb")]         IReadOnlyList<string> Stb,
    [property: JsonPropertyName("presets")]     IReadOnlyList<string> Presets,      // HOSPITAL-ISO + ISO-Standard + monochrome
    [property: JsonPropertyName("directory")]   string? Directory,
    [property: JsonPropertyName("assetsDir")]   string AssetsDir,
    [property: JsonPropertyName("count")]       int Count);
