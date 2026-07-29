// Typed DTOs for the acad-vision category.
// Mirrors the JSON wire shape of the AcadMcp.Vision Python sidecar HTTP API.
// See rule 29-acad-vision-architecture.md and rule 32-acad-vision-traps.md.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Vision;

// ---------------------------------------------------------------------------
// Common
// ---------------------------------------------------------------------------

/// <summary>Reference to an image: either an absolute path on this machine OR a base64 data URL.</summary>
public sealed record ImageRefDto(
    [property: JsonPropertyName("path")]   string? Path = null,
    [property: JsonPropertyName("base64")] string? Base64 = null,
    [property: JsonPropertyName("page")]   int? Page = null,
    [property: JsonPropertyName("dpi")]    int Dpi = 300);

public sealed record PixelBoxDto(
    [property: JsonPropertyName("x")]      int X,
    [property: JsonPropertyName("y")]      int Y,
    [property: JsonPropertyName("width")]  int Width,
    [property: JsonPropertyName("height")] int Height);

// ---------------------------------------------------------------------------
// OCR
// ---------------------------------------------------------------------------

public sealed record OcrArgs(
    [property: JsonPropertyName("image")]          ImageRefDto Image,
    [property: JsonPropertyName("engine")]         string Engine = "paddleocr",
    [property: JsonPropertyName("languages")]      List<string>? Languages = null,
    [property: JsonPropertyName("min_confidence")] double MinConfidence = 0.0);

public sealed record OcrTokenDto(
    [property: JsonPropertyName("text")]           string Text,
    [property: JsonPropertyName("confidence")]     double Confidence,
    [property: JsonPropertyName("box")]            PixelBoxDto Box,
    [property: JsonPropertyName("low_confidence")] bool LowConfidence);

public sealed record OcrResult(
    [property: JsonPropertyName("engine")]         string Engine,
    [property: JsonPropertyName("engine_version")] string EngineVersion,
    [property: JsonPropertyName("image_width")]    int ImageWidth,
    [property: JsonPropertyName("image_height")]   int ImageHeight,
    [property: JsonPropertyName("tokens")]         List<OcrTokenDto> Tokens,
    [property: JsonPropertyName("cached")]         bool Cached);

// ---------------------------------------------------------------------------
// YOLO symbol detection
// ---------------------------------------------------------------------------

public sealed record DetectSymbolsArgs(
    [property: JsonPropertyName("image")]          ImageRefDto Image,
    [property: JsonPropertyName("discipline")]     string Discipline = "arch",
    [property: JsonPropertyName("min_confidence")] double MinConfidence = 0.30);

public sealed record SymbolDetectionDto(
    [property: JsonPropertyName("label")]      string Label,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("box")]        PixelBoxDto Box);

public sealed record DetectSymbolsResult(
    [property: JsonPropertyName("discipline")]   string Discipline,
    [property: JsonPropertyName("weights")]      string Weights,
    [property: JsonPropertyName("image_width")]  int ImageWidth,
    [property: JsonPropertyName("image_height")] int ImageHeight,
    [property: JsonPropertyName("detections")]   List<SymbolDetectionDto> Detections,
    [property: JsonPropertyName("cached")]       bool Cached);

// ---------------------------------------------------------------------------
// Title block
// ---------------------------------------------------------------------------

public sealed record ExtractTitleblockArgs(
    [property: JsonPropertyName("image")]      ImageRefDto Image,
    [property: JsonPropertyName("discipline")] string Discipline = "architectural-eu");

public sealed record TitleblockFieldDto(
    [property: JsonPropertyName("field")]      string Field,
    [property: JsonPropertyName("raw_label")]  string? RawLabel,
    [property: JsonPropertyName("value")]      string Value,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("box")]        PixelBoxDto? Box);

public sealed record ExtractTitleblockResult(
    [property: JsonPropertyName("discipline")]      string Discipline,
    [property: JsonPropertyName("fields")]          List<TitleblockFieldDto> Fields,
    [property: JsonPropertyName("panel_box")]       PixelBoxDto? PanelBox,
    [property: JsonPropertyName("low_confidence")]  bool LowConfidence,
    [property: JsonPropertyName("cached")]          bool Cached);

// ---------------------------------------------------------------------------
// Dimensions
// ---------------------------------------------------------------------------

public sealed record ExtractDimensionsArgs(
    [property: JsonPropertyName("image")]          ImageRefDto Image,
    [property: JsonPropertyName("units")]          string Units = "auto",
    [property: JsonPropertyName("min_confidence")] double MinConfidence = 0.55);

public sealed record DimensionTokenDto(
    [property: JsonPropertyName("text")]       string Text,
    [property: JsonPropertyName("value_mm")]   double? ValueMm,
    [property: JsonPropertyName("units")]      string? Units,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("box")]        PixelBoxDto Box);

public sealed record ExtractDimensionsResult(
    [property: JsonPropertyName("image_width")]  int ImageWidth,
    [property: JsonPropertyName("image_height")] int ImageHeight,
    [property: JsonPropertyName("dimensions")]   List<DimensionTokenDto> Dimensions,
    [property: JsonPropertyName("cached")]       bool Cached);

// ---------------------------------------------------------------------------
// Drawing classification
// ---------------------------------------------------------------------------

public sealed record ClassifyDrawingArgs(
    [property: JsonPropertyName("image")] ImageRefDto Image);

public sealed record ClassifyDrawingResult(
    [property: JsonPropertyName("discipline")] string Discipline,
    [property: JsonPropertyName("sheet_type")] string SheetType,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("rationale")]  string Rationale,
    [property: JsonPropertyName("cached")]     bool Cached);

// ---------------------------------------------------------------------------
// Vision LLM describe
// ---------------------------------------------------------------------------

public sealed record DescribeImageArgs(
    [property: JsonPropertyName("image")]      ImageRefDto Image,
    [property: JsonPropertyName("prompt")]     string? Prompt = null,
    [property: JsonPropertyName("persona")]    string? Persona = null,
    [property: JsonPropertyName("provider")]   string Provider = "auto",
    [property: JsonPropertyName("max_tokens")] int MaxTokens = 400);

public sealed record DescribeImageResult(
    [property: JsonPropertyName("provider")]    string Provider,
    [property: JsonPropertyName("model")]       string Model,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("cached")]      bool Cached);

// ---------------------------------------------------------------------------
// Cross-validate OCR vs DXF
// ---------------------------------------------------------------------------

public sealed record CrossValidateArgs(
    [property: JsonPropertyName("ocr_strings")]       List<string> OcrStrings,
    [property: JsonPropertyName("dxf_strings")]       List<string> DxfStrings,
    [property: JsonPropertyName("case_insensitive")]  bool CaseInsensitive = true,
    [property: JsonPropertyName("numeric_tolerance")] double NumericTolerance = 0.0);

public sealed record CrossValidateResult(
    [property: JsonPropertyName("matched")]      List<string> Matched,
    [property: JsonPropertyName("only_in_ocr")]  List<string> OnlyInOcr,
    [property: JsonPropertyName("only_in_dxf")]  List<string> OnlyInDxf);

// ---------------------------------------------------------------------------
// Sidecar lifecycle / introspection
// ---------------------------------------------------------------------------

public sealed record VisionEmptyArgs();

public sealed record VisionVersionResult(
    [property: JsonPropertyName("version")]        string Version,
    [property: JsonPropertyName("phase")]          string Phase,
    [property: JsonPropertyName("optional_deps")]  Dictionary<string, bool> OptionalDeps,
    [property: JsonPropertyName("loaded_models")]  List<string> LoadedModels,
    [property: JsonPropertyName("api_keys")]       Dictionary<string, bool> ApiKeys);

public sealed record VisionHealthResult(
    [property: JsonPropertyName("status")]      string Status,
    [property: JsonPropertyName("version")]     string Version,
    [property: JsonPropertyName("phase")]       string Phase,
    [property: JsonPropertyName("uptime_sec")]  double UptimeSec,
    [property: JsonPropertyName("base_url")]    string BaseUrl);
