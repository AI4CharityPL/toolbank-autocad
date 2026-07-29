// AutoCAD acad-vision category. 9 tools wrapping the AcadMcp.Vision Python sidecar
// for OCR (Paddle / Easy / Tesseract), YOLO custom CAD-symbol detection, title-block extraction,
// dimension OCR, drawing classification, vision-LLM description, OCR-vs-DXF cross validation,
// plus sidecar health/version probes. None of these tools require AutoCAD to be running -
// they consume image / PDF / string inputs and call the Python sidecar over loopback HTTP.
//
// Rules: 19, 22, 29-acad-vision-architecture.mdc, 32-acad-vision-traps.mdc.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Sidecar;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Vision;

public static class VisionTools
{
    private const int T_FAST     = 5_000;
    private const int T_OCR      = 60_000;
    private const int T_YOLO     = 30_000;
    private const int T_LLM      = 60_000;

    [McpTool("ocr_image",
        "Run OCR on a raster image or a single PDF page. Returns recognised text tokens with per-token confidence and pixel bounding boxes (top-left origin). Engine is one of \"paddleocr\" (default, best on CAD), \"easyocr\", \"tesseract\". Image is referenced by absolute file path or base64 data URL; for PDFs supply page (1-based) and dpi. Uses on-disk cache keyed by content hash + engine + version.",
        "vision",
        Intent = new[]
        {
            "ocr na rysunku", "rozpoznaj tekst z rysunku",
            "ocr a drawing", "extract text from drawing",
            "ocr pdf page", "rozpoznaj tekst na stronie pdf",
        },
        ReadOnly = true)]
    public static Task<OcrResult> OcrImage(IVisionSidecarClient sidecar, OcrArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<OcrArgs, OcrResult>(sidecar, "/v1/ocr", args, T_OCR, ct);

    [McpTool("detect_symbols",
        "Run a custom YOLO CAD-symbol detector on a raster image. Discipline picks the per-discipline weights file: \"arch\" / \"mech\" / \"elec\" / \"pid\". Returns labelled bounding boxes (pixel coords) with confidence. If weights are missing returns 503 with an installHint pointing to scripts/setup-vision-models.ps1.",
        "vision",
        Intent = new[]
        {
            "wykryj symbole na rysunku", "znajdz drzwi i okna",
            "detect cad symbols", "find doors and windows",
            "yolo on drawing", "znajdz symbole p&id",
        },
        ReadOnly = true)]
    public static Task<DetectSymbolsResult> DetectSymbols(IVisionSidecarClient sidecar, DetectSymbolsArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<DetectSymbolsArgs, DetectSymbolsResult>(sidecar, "/v1/detect-symbols", args, T_YOLO, ct);

    [McpTool("extract_titleblock",
        "Extract title-block fields (drawing_no, title, scale, date, drawn_by, checked_by, project, rev, sheet, ...) from a raster drawing. Pass a discipline hint (\"architectural-eu\" default, \"architectural-us\", \"mechanical\", \"electrical\", \"civil\") so the right field-alias dictionary is used. Returns canonical field keys + raw OCR labels + values with confidence and the panel rectangle.",
        "vision",
        Intent = new[]
        {
            "wyciagnij tabelke rysunkowa", "rozpoznaj tabliczke tytulowa",
            "extract title block", "parse drawing title block",
            "get drawing number scale and date",
        },
        ReadOnly = true)]
    public static Task<ExtractTitleblockResult> ExtractTitleblock(IVisionSidecarClient sidecar, ExtractTitleblockArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<ExtractTitleblockArgs, ExtractTitleblockResult>(sidecar, "/v1/extract-titleblock", args, T_OCR, ct);

    [McpTool("extract_dimensions",
        "Extract dimension callouts from a raster drawing. Filters OCR tokens that look like dimensions (e.g. 1234, 12.5 mm, 12'-6\"), parses them to a numeric value in millimetres when possible, and returns each token with its pixel box and confidence. Units may be \"mm\", \"cm\", \"m\", \"in\", \"ft\" or \"auto\" (rely on the OCR'd unit suffix).",
        "vision",
        Intent = new[]
        {
            "wyciagnij wymiary z rysunku", "rozpoznaj wymiary z rysunku",
            "extract dimensions from drawing",
            "find numeric callouts on drawing",
            "ocr the dimensions on this drawing",
        },
        ReadOnly = true)]
    public static Task<ExtractDimensionsResult> ExtractDimensions(IVisionSidecarClient sidecar, ExtractDimensionsArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<ExtractDimensionsArgs, ExtractDimensionsResult>(sidecar, "/v1/extract-dimensions", args, T_OCR, ct);

    [McpTool("classify_drawing",
        "Use a vision LLM (Anthropic Claude or OpenAI GPT-4o, whichever has an API key) to classify a drawing's discipline (arch / mech / elec / pid / civil / unknown) and sheet type (plan / section / detail / schedule / title / isometric / unknown). Returns a JSON verdict with confidence and a one-line rationale. Cached by image content hash.",
        "vision",
        Intent = new[]
        {
            "rozpoznaj typ rysunku", "co to za rysunek",
            "classify this drawing", "what kind of drawing is this",
            "is this a plan or a section",
        },
        ReadOnly = true)]
    public static Task<ClassifyDrawingResult> ClassifyDrawing(IVisionSidecarClient sidecar, ClassifyDrawingArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<ClassifyDrawingArgs, ClassifyDrawingResult>(sidecar, "/v1/classify-drawing", args, T_LLM, ct);

    [McpTool("describe_image",
        "Free-form vision LLM description of any image (defaults to a CAD-reviewer prompt). Provider is \"auto\" (prefer Anthropic if ANTHROPIC_API_KEY is set, else OpenAI), \"anthropic\" or \"openai\". Image is downscaled to <=1568 px long side and JPEG-compressed q85 before sending. " +
        "OPTIONAL persona argument selects a curated review template: \"architect-reviewer\" (EN, Polish licensed architect reviewing a hospital floor plan at 1:100, structured output under walls-and-openings / doors / labels / code-compliance / visual-craft, with severity critical|major|minor and suggested MCP fix), \"architect-reviewer-pl\" (same in Polish), \"delta-compare\" (before/after regression check). When persona is set and prompt is left at default, the persona template replaces the prompt; otherwise the prompt is appended as \"User focus: ...\". " +
        "Cached by content hash + provider + persona + first 64 chars of the composed prompt.",
        "vision",
        Intent = new[]
        {
            "opisz ten rysunek", "co widac na tym rysunku",
            "describe this drawing",
            "ai vision review of drawing",
            "vision llm describe image",
        },
        ReadOnly = true)]
    public static Task<DescribeImageResult> DescribeImage(IVisionSidecarClient sidecar, DescribeImageArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<DescribeImageArgs, DescribeImageResult>(sidecar, "/v1/describe-image", args, T_LLM, ct);

    [McpTool("cross_validate_with_dxf",
        "Compare a list of OCR'd strings against a list of DXF text strings (the latter typically harvested by exporting the active document via acad.files.export_file -> DXF and walking the entity stream). Returns matched / only-in-OCR / only-in-DXF buckets after case + whitespace normalisation. Optional numericTolerance (>0) treats numeric tokens within the tolerance as matched (e.g. 12.5 vs 12.6).",
        "vision",
        Intent = new[]
        {
            "porownaj ocr z dxf", "sprawdz zgodnosc tekstu z dxf",
            "cross validate ocr against dxf",
            "diff ocr text and dxf strings",
            "weryfikuj rozpoznany tekst wzgledem rysunku",
        },
        ReadOnly = true)]
    public static Task<CrossValidateResult> CrossValidateWithDxf(IVisionSidecarClient sidecar, CrossValidateArgs args, CancellationToken ct)
        => VisionProxy.PostAsync<CrossValidateArgs, CrossValidateResult>(sidecar, "/v1/cross-validate-with-dxf", args, T_FAST, ct);

    [McpTool("vision_health",
        "Probe the AcadMcp.Vision Python sidecar at its discovered base URL (env ACADMCP_VISION_PORT, then %LOCALAPPDATA%\\AcadMcp\\vision.port, then default 50062). Returns status, version, phase and uptime. Use this to confirm the sidecar is reachable before calling other vision tools.",
        "vision",
        Intent = new[]
        {
            "sprawdz status sidecara vision", "czy sidecar dziala",
            "vision sidecar health", "ping vision sidecar",
            "is vision sidecar running",
        },
        ReadOnly = true)]
    public static async Task<VisionHealthResult> VisionHealth(IVisionSidecarClient sidecar, VisionEmptyArgs _args, CancellationToken ct)
    {
        var baseHealth = await VisionProxy.GetAsync<VisionHealthRaw>(sidecar, "/health", T_FAST, ct).ConfigureAwait(false);
        return new VisionHealthResult(
            baseHealth.Status, baseHealth.Version, baseHealth.Phase, baseHealth.UptimeSec,
            sidecar.BaseUrl);
    }

    [McpTool("vision_version",
        "Return the AcadMcp.Vision sidecar version, phase, the availability flags of every optional ML dep (paddleocr, easyocr, tesseract, ultralytics, torch, sam2, anthropic, openai, pypdfium2) and whether vision-LLM API keys are present. Use this to tell the user exactly which install command they need.",
        "vision",
        Intent = new[]
        {
            "co potrafi sidecar vision", "ktore modele sa zaladowane",
            "vision sidecar capabilities", "what vision models are available",
            "what vision providers are configured",
        },
        ReadOnly = true)]
    public static Task<VisionVersionResult> VisionVersion(IVisionSidecarClient sidecar, VisionEmptyArgs _args, CancellationToken ct)
        => VisionProxy.GetAsync<VisionVersionResult>(sidecar, "/version", T_FAST, ct);
}

internal sealed record VisionHealthRaw(
    [property: System.Text.Json.Serialization.JsonPropertyName("status")]     string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("version")]    string Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("phase")]      string Phase,
    [property: System.Text.Json.Serialization.JsonPropertyName("uptime_sec")] double UptimeSec);
