"""Pydantic v2 request / response schemas for the acad-vision HTTP API.

Conventions (rule 29):
- Image input is EITHER an absolute file path on the same machine, OR a base64
  data URL string (`data:image/png;base64,...`). Never multipart.
- Pixel coordinates are top-left origin (rule 32, trap #7). Conversion to
  drawing units is the C# Backend's job.
- Confidence is in [0,1]. Engines that don't report confidence return 1.0.
"""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field


# ---------------------------------------------------------------------------
# Common
# ---------------------------------------------------------------------------


class ImageRef(BaseModel):
    """Canonical reference to an image. Exactly one of `path` / `base64` must be set."""

    path: str | None = Field(
        default=None,
        description="Absolute path on the same machine. Preferred for large files.",
    )
    base64: str | None = Field(
        default=None,
        description="Either raw base64 or a data URL (`data:image/...;base64,...`).",
    )
    page: int | None = Field(
        default=None,
        ge=1,
        description="1-based page number, only used for PDFs (rule 32, trap #2).",
    )
    dpi: int = Field(
        default=300,
        ge=72,
        le=1200,
        description="Rasterisation DPI for PDFs.",
    )


class PixelBox(BaseModel):
    """Axis-aligned bounding box in image-pixel space (top-left origin)."""

    x: int
    y: int
    width: int
    height: int


class ServiceUnavailable(BaseModel):
    """503 envelope. Same shape used by every endpoint when an ML dep is missing."""

    error: Literal["model_not_available"] = "model_not_available"
    engine: str
    install_hint: str


# ---------------------------------------------------------------------------
# OCR
# ---------------------------------------------------------------------------


class OcrRequest(BaseModel):
    image: ImageRef
    engine: Literal["paddleocr", "easyocr", "tesseract"] = "paddleocr"
    languages: list[str] = Field(default_factory=lambda: ["en", "pl"])
    min_confidence: float = Field(default=0.0, ge=0.0, le=1.0)


class OcrToken(BaseModel):
    text: str
    confidence: float
    box: PixelBox
    low_confidence: bool = False  # rule 32, trap #3


class OcrResponse(BaseModel):
    engine: str
    engine_version: str
    image_width: int
    image_height: int
    tokens: list[OcrToken]
    cached: bool = False


# ---------------------------------------------------------------------------
# YOLO symbol detection
# ---------------------------------------------------------------------------


class DetectSymbolsRequest(BaseModel):
    image: ImageRef
    discipline: Literal["arch", "mech", "elec", "pid"] = "arch"
    min_confidence: float = Field(default=0.30, ge=0.0, le=1.0)


class SymbolDetection(BaseModel):
    label: str
    confidence: float
    box: PixelBox


class DetectSymbolsResponse(BaseModel):
    discipline: str
    weights: str
    image_width: int
    image_height: int
    detections: list[SymbolDetection]
    cached: bool = False


# ---------------------------------------------------------------------------
# Title block extraction
# ---------------------------------------------------------------------------


class ExtractTitleblockRequest(BaseModel):
    image: ImageRef
    discipline: Literal[
        "architectural-eu", "architectural-us", "mechanical", "electrical", "civil"
    ] = "architectural-eu"


class TitleblockField(BaseModel):
    field: str  # canonical field key (e.g. "drawing_no", "scale", "rev")
    raw_label: str | None = None  # the OCR'd label that matched
    value: str
    confidence: float
    box: PixelBox | None = None


class ExtractTitleblockResponse(BaseModel):
    discipline: str
    fields: list[TitleblockField]
    panel_box: PixelBox | None = None  # the rectangle we identified as the title block
    low_confidence: bool = False
    cached: bool = False


# ---------------------------------------------------------------------------
# Dimensions extraction
# ---------------------------------------------------------------------------


class ExtractDimensionsRequest(BaseModel):
    image: ImageRef
    units: Literal["mm", "cm", "m", "in", "ft", "auto"] = "auto"
    min_confidence: float = Field(default=0.55, ge=0.0, le=1.0)


class DimensionToken(BaseModel):
    text: str
    value_mm: float | None  # parsed numeric value, normalised to mm if possible
    units: str | None  # unit found in the OCR text, if any
    confidence: float
    box: PixelBox


class ExtractDimensionsResponse(BaseModel):
    image_width: int
    image_height: int
    dimensions: list[DimensionToken]
    cached: bool = False


# ---------------------------------------------------------------------------
# Drawing classification
# ---------------------------------------------------------------------------


class ClassifyDrawingRequest(BaseModel):
    image: ImageRef


class ClassifyDrawingResponse(BaseModel):
    discipline: Literal["arch", "mech", "elec", "pid", "civil", "unknown"]
    sheet_type: str  # "plan", "section", "detail", "schedule", "title", "isometric", "unknown"
    confidence: float
    rationale: str  # short text explaining the verdict
    cached: bool = False


# ---------------------------------------------------------------------------
# Vision LLM describe-image
# ---------------------------------------------------------------------------


class DescribeImageRequest(BaseModel):
    image: ImageRef
    prompt: str = (
        "You are an experienced CAD reviewer. Describe this drawing concisely: "
        "discipline, sheet type, key elements visible, and any obvious quality issues."
    )
    persona: (
        Literal[
            "architect-reviewer",
            "architect-reviewer-pl",
            "senior-architect-reviewer",
            "delta-compare",
            "none",
        ]
        | None
    ) = None
    """Optional reviewer persona. When set (and not "none"), the server replaces/
    augments `prompt` with a curated template tuned for CAD review work:

    * architect-reviewer     - English Polish-licensed architect reviewing an
                               AutoCAD hospital floor plan at 1:100. Structured
                               output under walls/doors/labels/code/craft.
    * architect-reviewer-pl  - Same, Polish response.
    * delta-compare          - Before/after regression check prompt (2 images
                               side by side or sequentially).
    * none / null            - Use `prompt` verbatim.

    If `prompt` is also supplied and differs from the default, it is appended
    to the persona preamble as user-supplied focus instructions.
    """
    provider: Literal["anthropic", "openai", "auto"] = "auto"
    max_tokens: int = Field(default=400, ge=64, le=4000)


class DescribeImageResponse(BaseModel):
    provider: str
    model: str
    description: str
    cached: bool = False


# ---------------------------------------------------------------------------
# SAM segmentation
# ---------------------------------------------------------------------------


class SegmentRequest(BaseModel):
    image: ImageRef
    points: list[tuple[int, int]] | None = None  # foreground prompt points
    box: PixelBox | None = None  # bounding-box prompt


class SegmentMask(BaseModel):
    score: float
    rle: str  # COCO RLE-encoded mask (compact + JSON-safe)
    box: PixelBox


class SegmentResponse(BaseModel):
    image_width: int
    image_height: int
    masks: list[SegmentMask]
    cached: bool = False


# ---------------------------------------------------------------------------
# Cross-validation OCR vs DXF
# ---------------------------------------------------------------------------


class CrossValidateRequest(BaseModel):
    ocr_strings: list[str]
    dxf_strings: list[str]
    case_insensitive: bool = True
    numeric_tolerance: float = Field(
        default=0.0,
        description=(
            "If > 0, numeric tokens differing by no more than this absolute amount "
            "are considered matched (rule 32, trap #8)."
        ),
    )


class CrossValidateResponse(BaseModel):
    matched: list[str]
    only_in_ocr: list[str]
    only_in_dxf: list[str]


# ---------------------------------------------------------------------------
# Liveness / introspection
# ---------------------------------------------------------------------------


class HealthResponse(BaseModel):
    status: Literal["ok"] = "ok"
    version: str
    phase: str
    uptime_sec: float


class VersionResponse(BaseModel):
    version: str
    phase: str
    optional_deps: dict[str, bool]
    loaded_models: list[str]
    api_keys: dict[str, bool]


# ---------------------------------------------------------------------------
# Architect review (senior-architect-reviewer, 17-criterion rubric, rule 60)
# ---------------------------------------------------------------------------


#: Canonical list of the 17 rubric criteria. The ORDER and LABELS are the
#: authoritative contract declared by rule 60 §1 and must not drift.
#: Each tuple is (id, label, axis).
ARCHITECT_REVIEW_CRITERIA: tuple[tuple[int, str, str], ...] = (
    (1,  "hatching",           "Material expression"),
    (2,  "furniture",          "Interior furnishing"),
    (3,  "plumbing",           "Sanitary fixtures"),
    (4,  "doors",              "Door quality"),
    (5,  "windows",            "Window quality"),
    (6,  "verticals",          "Vertical circulation"),
    (7,  "grid",               "Structural grid"),
    (8,  "dimensions",         "Dimensioning"),
    (9,  "schedules",          "Schedules"),
    (10, "callouts",           "Callouts"),
    (11, "sections",           "Section lines"),
    (12, "lineweight",         "Lineweight / plot style"),
    (13, "finishes-legend",    "Finishes legend"),
    (14, "orientation-scale",  "Orientation + scale"),
    (15, "reflected-ceiling",  "Reflected ceiling plan"),
    (16, "details",            "Jamb / sill / lintel details"),
    (17, "room-program",       "Room program fidelity"),
)


ArchitectReviewVerdict = Literal[
    "concept-sketch",
    "technical-study",
    "executive-with-remark",
    "full-wykonawczy",
    "unknown",
]


class ArchitectReviewCriterion(BaseModel):
    """One row of the 17-criterion scorecard."""

    id: int = Field(ge=1, le=17, description="1-based rubric ID per rule 60 §1.")
    label: str = Field(
        description="Short canonical label (see ARCHITECT_REVIEW_CRITERIA).",
    )
    score: float = Field(
        ge=0.0, le=1.0,
        description="0 / 0.5 / 1 per rule 60. Server rounds to nearest half.",
    )
    note: str = Field(
        default="",
        description=(
            "Short explanation of the verdict. MUST cite a fix tool when score<1 "
            "(e.g. 'fix: acad.hatches.apply_material_preset_by_point')."
        ),
    )


class ArchitectReviewRequest(BaseModel):
    image: ImageRef
    #: Optional language of the rubric response. Polish if set to 'pl'.
    language: Literal["en", "pl"] = "en"
    #: Optional project brief (room programme, areas, compliance cues). When
    #: provided it is appended to the persona prompt as "Project brief:" so the
    #: persona can score criterion 17 (room-program fidelity) correctly.
    brief: str = Field(default="", max_length=4000)
    #: Max tokens for the underlying LLM call.
    #:
    #: Rationale for the 16000 upper bound: Gemini 3.x thinking models
    #: (gemini-3.1-pro-preview in particular) include reasoning tokens in
    #: max_output_tokens, so a too-tight cap truncates the 17-row JSON
    #: scorecard mid-row. 4000 is a safe default that leaves room for
    #: ~1500 reasoning + ~1500 JSON at thinking_level="low".
    max_tokens: int = Field(default=4000, ge=400, le=16000)
    #: Vision provider, forwarded to vision_llm.describe().
    provider: Literal["anthropic", "openai", "google", "auto"] = "auto"


class ArchitectReviewResponse(BaseModel):
    #: Sum of 17 criterion scores, out of 17.0.
    score: float = Field(ge=0.0, le=17.0)
    verdict: ArchitectReviewVerdict
    #: The 17 criteria in canonical order. Any criterion the LLM failed to score
    #: is surfaced as score=0.0 + note='persona did not score this criterion'
    #: (rule 60 §2: never silently skip).
    criteria: list[ArchitectReviewCriterion]
    #: Rubric IDs that scored < 1.0. Callers MUST treat len(fatal_gaps) as the
    #: number of generator re-runs needed to reach full wykonawczy.
    fatal_gaps: list[int]
    #: Threshold note ("drawing is concept-sketch; cannot be exported", etc.).
    threshold_note: str
    #: Raw LLM output for debugging; clients may ignore.
    raw_text: str = ""
    provider: str = ""
    model: str = ""
    cached: bool = False
