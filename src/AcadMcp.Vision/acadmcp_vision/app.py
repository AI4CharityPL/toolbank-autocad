"""AcadMcp.Vision FastAPI sidecar (Phase 4).

Wire-up only. All real work lives in engines/* and is lazy-imported per
rule 32 trap #9. Bind to 127.0.0.1 only per rule 29 §2. Idle-shutdown
per rule 29 §3.
"""

from __future__ import annotations

import argparse
import asyncio
import os
import sys
import time
from collections.abc import Awaitable, Callable
from typing import Any

import structlog
import uvicorn
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse

from . import __phase__, __version__
from . import _cache, _loaders
from .config import SETTINGS
from .engines import dimensions as dim_engine
from .engines import ocr as ocr_engine
from .engines import titleblock as tb_engine
from .engines import vision_llm as llm_engine
from .engines import yolo as yolo_engine
from .engines.ocr import EngineUnavailable
from .engines.yolo import WeightsMissing
from .schemas import (
    ARCHITECT_REVIEW_CRITERIA,
    ArchitectReviewCriterion,
    ArchitectReviewRequest,
    ArchitectReviewResponse,
    ClassifyDrawingRequest,
    ClassifyDrawingResponse,
    CrossValidateRequest,
    CrossValidateResponse,
    DescribeImageRequest,
    DescribeImageResponse,
    DetectSymbolsRequest,
    DetectSymbolsResponse,
    ExtractDimensionsRequest,
    ExtractDimensionsResponse,
    ExtractTitleblockRequest,
    ExtractTitleblockResponse,
    HealthResponse,
    OcrRequest,
    OcrResponse,
    ServiceUnavailable,
    VersionResponse,
)

logger = structlog.get_logger(__name__)

START_TIME = time.monotonic()
LAST_HIT = time.monotonic()
ENGINE_LOCKS: dict[str, asyncio.Semaphore] = {
    "ocr": asyncio.Semaphore(1),
    "yolo": asyncio.Semaphore(1),
    "llm": asyncio.Semaphore(1),
}


def _touch() -> None:
    global LAST_HIT
    LAST_HIT = time.monotonic()


def _service_unavailable(engine: str, install_hint: str) -> JSONResponse:
    return JSONResponse(
        status_code=503,
        content=ServiceUnavailable(engine=engine, install_hint=install_hint).model_dump(),
    )


async def _with_lock(name: str, fn: Callable[[], Awaitable[Any]]) -> Any:
    sem = ENGINE_LOCKS[name]
    async with sem:
        return await fn()


def create_app() -> FastAPI:
    SETTINGS.ensure_paths()
    app = FastAPI(
        title="AcadMcp.Vision",
        version=__version__,
        description="AutoCAD MCP Vision sidecar (Phase 4: real OCR / YOLO / LLM endpoints).",
    )

    @app.middleware("http")
    async def touch_idle(request: Request, call_next):  # type: ignore[no-untyped-def]
        _touch()
        return await call_next(request)

    # --- Liveness / introspection -----------------------------------------
    @app.get("/health", response_model=HealthResponse)
    async def health() -> HealthResponse:
        return HealthResponse(
            version=__version__,
            phase=__phase__,
            uptime_sec=time.monotonic() - START_TIME,
        )

    @app.get("/version", response_model=VersionResponse)
    async def version() -> VersionResponse:
        return VersionResponse(
            version=__version__,
            phase=__phase__,
            optional_deps=_loaders.list_optional_deps(),
            loaded_models=_loaded_model_names(),
            api_keys={
                "anthropic": llm_engine.has_anthropic_key(),
                "openai": llm_engine.has_openai_key(),
                "google": llm_engine.has_google_key(),
            },
        )

    # --- OCR ---------------------------------------------------------------
    @app.post("/v1/ocr", response_model=OcrResponse, responses={503: {"model": ServiceUnavailable}})
    async def ocr(req: OcrRequest):
        try:
            loaded = _loaders.load_image(req.image)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))
        except ImportError as ex:
            return _service_unavailable("pypdfium2", str(ex))

        eng_v = ocr_engine.engine_version(req.engine)
        cached = _cache.get(loaded.sha256, req.engine, eng_v, extra="|".join(req.languages))
        if cached:
            return OcrResponse.model_validate({**cached, "cached": True})

        async def _run():
            return ocr_engine.run_ocr(req.engine, loaded.pil, req.languages)

        try:
            tokens = await _with_lock("ocr", _run)
        except EngineUnavailable as ex:
            return _service_unavailable(ex.engine, ex.install_hint)

        kept = [t for t in tokens if t.confidence >= req.min_confidence]
        rsp = OcrResponse(
            engine=req.engine,
            engine_version=eng_v,
            image_width=loaded.pil.size[0],
            image_height=loaded.pil.size[1],
            tokens=kept,
            cached=False,
        )
        _cache.put(
            loaded.sha256, req.engine, eng_v,
            rsp.model_dump(),
            extra="|".join(req.languages),
        )
        return rsp

    # --- YOLO symbol detection --------------------------------------------
    @app.post(
        "/v1/detect-symbols",
        response_model=DetectSymbolsResponse,
        responses={503: {"model": ServiceUnavailable}},
    )
    async def detect_symbols(req: DetectSymbolsRequest):
        try:
            loaded = _loaders.load_image(req.image)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))

        eng_v = yolo_engine.engine_version()
        cached = _cache.get(
            loaded.sha256, f"yolo-{req.discipline}", eng_v,
            extra=f"{req.min_confidence}",
        )
        if cached:
            return DetectSymbolsResponse.model_validate({**cached, "cached": True})

        async def _run():
            return yolo_engine.detect(loaded.pil, req.discipline, req.min_confidence)

        try:
            dets = await _with_lock("yolo", _run)
        except ImportError as ex:
            return _service_unavailable("ultralytics", str(ex))
        except WeightsMissing as ex:
            return _service_unavailable(
                f"yolo-{ex.discipline}",
                f"Run scripts/setup-vision-models.ps1 to download weights to {ex.expected_path}.",
            )

        rsp = DetectSymbolsResponse(
            discipline=req.discipline,
            weights=str(yolo_engine.weights_path(req.discipline)),
            image_width=loaded.pil.size[0],
            image_height=loaded.pil.size[1],
            detections=dets,
            cached=False,
        )
        _cache.put(
            loaded.sha256, f"yolo-{req.discipline}", eng_v,
            rsp.model_dump(),
            extra=f"{req.min_confidence}",
        )
        return rsp

    # --- Title-block extraction (OCR + heuristic) -------------------------
    @app.post(
        "/v1/extract-titleblock",
        response_model=ExtractTitleblockResponse,
        responses={503: {"model": ServiceUnavailable}},
    )
    async def extract_titleblock(req: ExtractTitleblockRequest):
        try:
            loaded = _loaders.load_image(req.image)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))

        eng_v = ocr_engine.engine_version("paddleocr")
        cached = _cache.get(loaded.sha256, "titleblock", eng_v, extra=req.discipline)
        if cached:
            return ExtractTitleblockResponse.model_validate({**cached, "cached": True})

        async def _run():
            return ocr_engine.run_ocr("paddleocr", loaded.pil, ["en", "pl"])

        try:
            tokens = await _with_lock("ocr", _run)
        except EngineUnavailable as ex:
            return _service_unavailable(ex.engine, ex.install_hint)

        fields, panel_box, low = tb_engine.extract(
            tokens, req.discipline, loaded.pil.size[0], loaded.pil.size[1]
        )
        rsp = ExtractTitleblockResponse(
            discipline=req.discipline,
            fields=fields,
            panel_box=panel_box,
            low_confidence=low,
            cached=False,
        )
        _cache.put(loaded.sha256, "titleblock", eng_v, rsp.model_dump(), extra=req.discipline)
        return rsp

    # --- Dimensions extraction --------------------------------------------
    @app.post(
        "/v1/extract-dimensions",
        response_model=ExtractDimensionsResponse,
        responses={503: {"model": ServiceUnavailable}},
    )
    async def extract_dimensions(req: ExtractDimensionsRequest):
        try:
            loaded = _loaders.load_image(req.image)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))

        eng_v = ocr_engine.engine_version("paddleocr")
        cached = _cache.get(loaded.sha256, "dimensions", eng_v, extra=req.units)
        if cached:
            return ExtractDimensionsResponse.model_validate({**cached, "cached": True})

        async def _run():
            return ocr_engine.run_ocr("paddleocr", loaded.pil, ["en"])

        try:
            tokens = await _with_lock("ocr", _run)
        except EngineUnavailable as ex:
            return _service_unavailable(ex.engine, ex.install_hint)

        dims = dim_engine.from_ocr(tokens, req.units, req.min_confidence)
        rsp = ExtractDimensionsResponse(
            image_width=loaded.pil.size[0],
            image_height=loaded.pil.size[1],
            dimensions=dims,
            cached=False,
        )
        _cache.put(loaded.sha256, "dimensions", eng_v, rsp.model_dump(), extra=req.units)
        return rsp

    # --- Drawing classification (LLM-backed) ------------------------------
    @app.post(
        "/v1/classify-drawing",
        response_model=ClassifyDrawingResponse,
        responses={503: {"model": ServiceUnavailable}},
    )
    async def classify_drawing(req: ClassifyDrawingRequest):
        try:
            loaded = _loaders.load_image(req.image, max_long_side=1568)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))

        cached = _cache.get(loaded.sha256, "classify-drawing", "v1")
        if cached:
            return ClassifyDrawingResponse.model_validate({**cached, "cached": True})

        prompt = (
            "Classify this CAD drawing. Reply STRICTLY as JSON with keys: "
            'discipline (one of "arch","mech","elec","pid","civil","unknown"), '
            'sheet_type (one of "plan","section","detail","schedule","title","isometric","unknown"), '
            "confidence (0..1), rationale (one short sentence)."
        )

        async def _run():
            return llm_engine.describe(loaded.pil, prompt, "auto", max_tokens=300)

        try:
            reply = await _with_lock("llm", _run)
        except (RuntimeError, ImportError) as ex:
            return _service_unavailable("vision_llm", str(ex))

        parsed = _parse_classification_json(reply.text)
        rsp = ClassifyDrawingResponse(**parsed, cached=False)
        _cache.put(loaded.sha256, "classify-drawing", "v1", rsp.model_dump())
        return rsp

    # --- Vision LLM describe ----------------------------------------------
    @app.post(
        "/v1/describe-image",
        response_model=DescribeImageResponse,
        responses={503: {"model": ServiceUnavailable}},
    )
    async def describe_image(req: DescribeImageRequest):
        try:
            loaded = _loaders.load_image(req.image, max_long_side=1568)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))

        final_prompt = _compose_prompt(req.persona, req.prompt)

        cached = _cache.get(
            loaded.sha256, f"describe-{req.provider}", "v2",
            extra=f"{req.max_tokens}|{(req.persona or 'none')}|{final_prompt[:64]}",
        )
        if cached:
            return DescribeImageResponse.model_validate({**cached, "cached": True})

        async def _run():
            return llm_engine.describe(loaded.pil, final_prompt, req.provider, req.max_tokens)

        try:
            reply = await _with_lock("llm", _run)
        except (RuntimeError, ImportError) as ex:
            return _service_unavailable("vision_llm", str(ex))

        rsp = DescribeImageResponse(
            provider=reply.provider, model=reply.model,
            description=reply.text, cached=False,
        )
        _cache.put(
            loaded.sha256, f"describe-{req.provider}", "v2",
            rsp.model_dump(),
            extra=f"{req.max_tokens}|{(req.persona or 'none')}|{final_prompt[:64]}",
        )
        return rsp

    # --- Cross-validate OCR vs DXF ----------------------------------------
    @app.post("/v1/cross-validate-with-dxf", response_model=CrossValidateResponse)
    async def cross_validate(req: CrossValidateRequest):
        return _cross_validate(req)

    # --- Architect review (17-criterion rubric, rule 60) ------------------
    @app.post(
        "/v1/architect-review",
        response_model=ArchitectReviewResponse,
        responses={503: {"model": ServiceUnavailable}},
    )
    async def architect_review(req: ArchitectReviewRequest):
        try:
            loaded = _loaders.load_image(req.image, max_long_side=1568)
        except (FileNotFoundError, ValueError) as ex:
            raise HTTPException(status_code=400, detail=str(ex))

        prompt_base = (
            _SENIOR_ARCHITECT_PROMPT_PL
            if req.language == "pl"
            else _SENIOR_ARCHITECT_PROMPT_EN
        )
        prompt = prompt_base
        if req.brief.strip():
            prompt = f"{prompt_base}\n\nProject brief:\n{req.brief.strip()}"

        cached = _cache.get(
            loaded.sha256, "architect-review", "v1",
            extra=f"{req.language}|{req.provider}|{req.max_tokens}|{len(req.brief)}",
        )
        if cached:
            return ArchitectReviewResponse.model_validate({**cached, "cached": True})

        async def _run():
            return llm_engine.describe(loaded.pil, prompt, req.provider, req.max_tokens)

        try:
            reply = await _with_lock("llm", _run)
        except (RuntimeError, ImportError) as ex:
            return _service_unavailable("vision_llm", str(ex))

        scorecard = _parse_architect_review_json(reply.text)
        score = sum(c.score for c in scorecard)
        fatal_gaps = [c.id for c in scorecard if c.score < 1.0]
        verdict = _verdict_from_score(score)
        rsp = ArchitectReviewResponse(
            score=round(score, 2),
            verdict=verdict,
            criteria=scorecard,
            fatal_gaps=fatal_gaps,
            threshold_note=_threshold_note(verdict),
            raw_text=reply.text[:4000],
            provider=reply.provider,
            model=reply.model,
            cached=False,
        )
        _cache.put(
            loaded.sha256, "architect-review", "v1",
            rsp.model_dump(),
            extra=f"{req.language}|{req.provider}|{req.max_tokens}|{len(req.brief)}",
        )
        return rsp

    return app


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _loaded_model_names() -> list[str]:
    names: list[str] = []
    if ocr_engine._paddle_engine is not None:  # noqa: SLF001 - introspection
        names.append("paddleocr")
    if ocr_engine._easyocr_reader is not None:  # noqa: SLF001
        names.append("easyocr")
    for k in yolo_engine._models:  # noqa: SLF001
        names.append(f"yolo:{k}")
    return names


# ---------------------------------------------------------------------------
# Persona prompt templates for /v1/describe-image
# ---------------------------------------------------------------------------

_SENIOR_ARCHITECT_PROMPT_EN = (
    "You are a SENIOR LICENSED ARCHITECT (IARP uprawnienia, 20+ years) "
    "reviewing an AutoCAD hospital floor-plan rendered at 1:100, units "
    "millimetres. You apply the 17-criterion architectural-fidelity rubric "
    "(rule 60). You MUST score every criterion on a strict 0 / 0.5 / 1 "
    "scale:\n"
    "  0   - not present, or present but fundamentally broken.\n"
    "  0.5 - present but violates at least one sub-rule "
    "        (wrong scale / wrong layer / missing attribute / too sparse).\n"
    "  1   - fully compliant with the cited rule.\n\n"
    "RUBRIC (canonical order, id and label are contract - do not rename):\n"
    "  1. hatching          - Wall hatching per material (concrete, brick, "
    "insulation, plaster, lead, faraday) per rule 62.\n"
    "  2. furniture         - Furniture in every inhabited room (beds, "
    "chairs, desks, cabinets) at correct density per rule 64.\n"
    "  3. plumbing          - WC, basin, shower, bath in every bathroom; "
    "accessible fixtures per PN-EN 17210 (rule 63).\n"
    "  4. doors             - Jamb ticks + leaf + swing arc + visible "
    "NUMBER attribute; REI / lead / RC per rule 65.\n"
    "  5. windows           - Frame + glass lines + centre + sill attribute "
    "+ sash/tilt marker + RC where required.\n"
    "  6. verticals         - Stairs / lifts / ramps with tread lines, "
    "numbered treads, arrow, handrail, shaft outline.\n"
    "  7. grid              - Y1/Y3/C/F bubble-labels + continuous grid "
    "lines + cumulative spacing dimensions (rule 67).\n"
    "  8. dimensions        - Main / sub / cumulative chains on all four "
    "sides with 45-degree ticks (rule 66).\n"
    "  9. schedules         - Door / window / room schedules as paperspace "
    "Tables via HOSPITAL-DEF TableStyle, linked to block attributes.\n"
    " 10. callouts          - K1/K6/K10 column-profile leaders, section "
    "bubbles, north arrow, scale bar, title block (rule 69).\n"
    " 11. sections          - A-A / B-B section cut lines with markers + "
    "direction arrows on A-ANNO-SECT (rule 70).\n"
    " 12. lineweight        - CTB/STB plot style in use with ACI 1..9 -> "
    "0.13..0.70 mm tiering (rule 61).\n"
    " 13. finishes-legend   - LEGENDA WYKONCZEN table mapping F-xx/W-xx/"
    "C-xx codes to materials.\n"
    " 14. orientation-scale - North arrow + scale bar + compass at bottom-"
    "right of sheet (rule 69).\n"
    " 15. reflected-ceiling - Optional RCP sheet with luminaires / HVAC "
    "diffusers / smoke detectors on E-LITE / M-HVAC.\n"
    " 16. details           - Jamb / sill / lintel blow-up details at "
    "1:10 / 1:20 in paperspace viewport tagged DET-01 ...\n"
    " 17. room-program      - Every labelled room exists in the programme "
    "checklist and has area within +-10%% of brief.\n\n"
    "OUTPUT FORMAT (STRICT). Reply with EXACTLY ONE JSON object, no "
    "commentary before or after, no markdown code fences. Schema:\n"
    "{\n"
    '  "criteria": [\n'
    '    { "id": 1,  "label": "hatching",          "score": 0.0|0.5|1.0, "note": "..." },\n'
    '    { "id": 2,  "label": "furniture",         "score": 0.0|0.5|1.0, "note": "..." },\n'
    "    ... all 17 entries in this exact order ...\n"
    "  ]\n"
    "}\n"
    "Every `note` MUST be a short phrase (<= 200 chars). For score < 1.0 "
    "the note MUST cite a fix tool in the form 'fix: acad.<category>."
    "<tool>' taken from rule 60 §3 (generator map). Do NOT invent tools.\n\n"
    "Be honest. A drawing that scores 15-17 is ready for tender; 10-14 is "
    "a technical study; under 10 is a concept sketch. Err on the side of "
    "0.5 when evidence is ambiguous."
)

_SENIOR_ARCHITECT_PROMPT_PL = (
    "Jestes SENIOR architektem z uprawnieniami IARP (20+ lat), robisz "
    "przeglad rysunku wykonawczego szpitala w AutoCAD, skala 1:100, "
    "jednostki mm. Stosujesz 17-punktowa skale wiernosci architektonicznej "
    "(regula 60). KAZDE kryterium oceniasz SCISLE w skali 0 / 0.5 / 1.\n\n"
    "KRYTERIA (kanoniczna kolejnosc, id i label to kontrakt - nie zmieniaj):\n"
    "  1. hatching / hatching-scian\n"
    "  2. furniture / meble\n"
    "  3. plumbing / bialy-montaz\n"
    "  4. doors / drzwi\n"
    "  5. windows / okna\n"
    "  6. verticals / komunikacja-pionowa\n"
    "  7. grid / siatka-osi\n"
    "  8. dimensions / wymiary\n"
    "  9. schedules / zestawienia\n"
    " 10. callouts / oznaczenia\n"
    " 11. sections / linie-przekrojow\n"
    " 12. lineweight / grubosc-linii\n"
    " 13. finishes-legend / legenda-wykonczen\n"
    " 14. orientation-scale / polnoc-i-skala\n"
    " 15. reflected-ceiling / sufit-podwieszany\n"
    " 16. details / detale-otworow\n"
    " 17. room-program / program-pokojow\n\n"
    "WYJSCIE: dokladnie jeden obiekt JSON, bez markdownu, bez komentarza. "
    "Schemat taki sam jak w wersji angielskiej - `criteria` to lista 17 "
    "wpisow z id, label (uzyj labela po polsku w ASCII, bez spacji), "
    "score w {0.0, 0.5, 1.0} i note (<=200 znakow). Kazdy note dla score<1 "
    "MUSI podac 'fix: acad.<kategoria>.<narzedzie>' wg regula 60 §3."
)


_PERSONA_PROMPTS: dict[str, str] = {
    "senior-architect-reviewer": _SENIOR_ARCHITECT_PROMPT_EN,
    "architect-reviewer": (
        "You are a Polish licensed architect (IARP uprawnienia) reviewing an "
        "AutoCAD hospital floor plan rendered at nominal scale 1:100. Drawing "
        "units are millimetres.\n\n"
        "Report issues under these five headings exactly (use these literal "
        "headings in your output):\n"
        "(1) walls-and-openings - missing openings where doors should cut the "
        "wall, continuous wall polylines running through doors, wall thickness "
        "inconsistencies, wall intersections not cleaned up.\n"
        "(2) doors - hinge side wrong, swing direction violates egress "
        "(egress doors must open in evacuation direction), swing blocked by "
        "furniture/equipment, missing door where corridor meets room, "
        "double-leaf where single is drawn (or vice versa), missing fire-door "
        "markers, missing AIIR/airlock interlocks.\n"
        "(3) labels - mtext/attext that overlaps another label, label outside "
        "its room bounding box, truncated text running off geometry, duplicate "
        "labels, illegible size, missing room label.\n"
        "(4) code-compliance - WT (Warunki Techniczne 2022) §62/§232 "
        "(corridor widths, escape routes), fire compartments and fire doors "
        "(REI ratings visible/missing), AIIR airlock staging, Pb/radiological "
        "shielding continuity, patient-room privacy, accessible route.\n"
        "(5) visual-craft - duplicate entities on top of each other, "
        "mis-alignment against 100-mm grid, stacked annotations, orphan "
        "geometry, linework not on the expected layer.\n\n"
        "For EACH issue give a bullet line in this exact machine-friendly "
        "shape:\n"
        "  [severity] (zone/coord hint) - what is wrong -> fix: <one-line MCP "
        "tool-call outline>\n"
        "Severity is one of critical | major | minor.\n"
        "If a heading has no issues write 'no issues'.\n"
        "End with a single summary line: TOTAL critical=<n> major=<n> "
        "minor=<n>."
    ),
    "architect-reviewer-pl": (
        "Jestes polskim architektem z uprawnieniami IARP, robisz przeglad "
        "rysunku wykonawczego szpitala w AutoCAD, skala 1:100, jednostki "
        "milimetry.\n\n"
        "Zgłaszaj problemy pod naglowkami (uzyj dokladnie tych naglowkow):\n"
        "(1) sciany-i-otwory\n"
        "(2) drzwi\n"
        "(3) opisy\n"
        "(4) zgodnosc-z-norma (WT §62/§232, strefy pozarowe, AIIR, Pb)\n"
        "(5) estetyka (duplikaty, wyrownanie, stacking, warstwy)\n\n"
        "Dla kazdego zgloszenia linijka w formacie:\n"
        "  [severity] (strefa/wsp) - opis -> fix: <zarys wywolania MCP>\n"
        "severity: critical | major | minor. Brak -> 'brak uwag'.\n"
        "Na koncu linia: RAZEM critical=<n> major=<n> minor=<n>."
    ),
    "delta-compare": (
        "You are reviewing an AutoCAD drawing AFTER a fix has been applied. "
        "The BEFORE state is described in the user message; the current image "
        "is the AFTER state. Answer in three sections:\n"
        "(1) resolved - which reported issues are now gone (list each).\n"
        "(2) remaining - issues still visible.\n"
        "(3) regressions - new issues introduced by the fix.\n"
        "Keep each bullet to one line with severity in brackets. End with "
        "'VERDICT: ok' if (1) is non-empty and (3) is empty, else "
        "'VERDICT: needs-retry'."
    ),
}


def _compose_prompt(persona: str | None, user_prompt: str) -> str:
    """Merge the persona template (if any) with the caller's prompt.

    Rules:
      - persona in (None, "", "none") -> return user_prompt unchanged.
      - unknown persona -> use user_prompt unchanged (client-side mis-key is
        surfaced by the request model's Literal validation, so this only fires
        if someone bypasses pydantic).
      - known persona + default user_prompt -> use persona template alone.
      - known persona + custom user_prompt -> persona template + "\\n\\nUser
        focus:" + user_prompt.
    """
    if not persona or persona == "none":
        return user_prompt
    template = _PERSONA_PROMPTS.get(persona)
    if template is None:
        return user_prompt
    # Default prompt from schemas.DescribeImageRequest starts with this phrase.
    default_marker = "You are an experienced CAD reviewer."
    if user_prompt.startswith(default_marker):
        return template
    return f"{template}\n\nUser focus: {user_prompt.strip()}"


def _parse_classification_json(text: str) -> dict[str, Any]:
    """Pull JSON out of the LLM reply tolerantly. Falls back to 'unknown' on noise."""
    import json
    import re

    fallback = {
        "discipline": "unknown",
        "sheet_type": "unknown",
        "confidence": 0.0,
        "rationale": text[:200] if text else "no_reply",
    }
    if not text:
        return fallback
    m = re.search(r"\{.*\}", text, flags=re.DOTALL)
    if not m:
        return fallback
    try:
        data = json.loads(m.group(0))
    except json.JSONDecodeError:
        return fallback
    return {
        "discipline": str(data.get("discipline", "unknown")).lower(),
        "sheet_type": str(data.get("sheet_type", "unknown")).lower(),
        "confidence": float(data.get("confidence", 0.0)),
        "rationale": str(data.get("rationale", ""))[:400],
    }


def _parse_architect_review_json(text: str) -> list[ArchitectReviewCriterion]:
    """Parse the 17-row JSON scorecard emitted by `senior-architect-reviewer`.

    The LLM sometimes wraps JSON in chatter or ```json fences. We tolerate
    both. Any criterion the model failed to emit is surfaced as score=0.0
    with a note 'persona did not score this criterion' (rule 60 §2: never
    silently drop a row).

    Scores are clamped to {0.0, 0.5, 1.0} by rounding to the nearest half,
    per rule 60 §1 ("strict 0/0.5/1 scale").
    """
    import json
    import re

    canonical_by_id = {cid: (cid, label, axis) for cid, label, axis in ARCHITECT_REVIEW_CRITERIA}
    rows_by_id: dict[int, ArchitectReviewCriterion] = {}

    def _finalise() -> list[ArchitectReviewCriterion]:
        out: list[ArchitectReviewCriterion] = []
        for cid, label, _axis in ARCHITECT_REVIEW_CRITERIA:
            got = rows_by_id.get(cid)
            if got is None:
                out.append(ArchitectReviewCriterion(
                    id=cid,
                    label=label,
                    score=0.0,
                    note="persona did not score this criterion",
                ))
            else:
                out.append(got)
        return out

    if not text:
        return _finalise()

    match = re.search(r"\{.*\}", text, flags=re.DOTALL)
    if not match:
        return _finalise()

    try:
        data = json.loads(match.group(0))
    except json.JSONDecodeError:
        return _finalise()

    crits = data.get("criteria") if isinstance(data, dict) else None
    if not isinstance(crits, list):
        return _finalise()

    for row in crits:
        if not isinstance(row, dict):
            continue
        try:
            cid_raw = row.get("id")
            cid = int(cid_raw) if cid_raw is not None else None
        except (TypeError, ValueError):
            cid = None
        if cid is None or cid not in canonical_by_id:
            continue
        try:
            raw_score = float(row.get("score", 0.0))
        except (TypeError, ValueError):
            raw_score = 0.0
        snapped = round(max(0.0, min(1.0, raw_score)) * 2.0) / 2.0
        note = str(row.get("note", ""))[:300]
        label_hint = str(row.get("label", canonical_by_id[cid][1]))[:60]
        rows_by_id[cid] = ArchitectReviewCriterion(
            id=cid,
            label=label_hint or canonical_by_id[cid][1],
            score=snapped,
            note=note,
        )

    return _finalise()


def _verdict_from_score(score: float) -> str:
    """Map score in [0, 17] to rule 60 threshold tier."""
    if score < 10.0:
        return "concept-sketch"
    if score < 14.0:
        return "technical-study"
    if score < 16.0:
        return "executive-with-remark"
    return "full-wykonawczy"


def _threshold_note(verdict: str) -> str:
    notes = {
        "concept-sketch": (
            "score < 10 / 17 - concept sketch; DO NOT export for tender. "
            "Re-run the generators for every criterion with score < 1."
        ),
        "technical-study": (
            "score 10..13 / 17 - technical study; OK for internal review "
            "but NOT for tender / pozwolenie na budowe."
        ),
        "executive-with-remark": (
            "score 14..15 / 17 - executive-grade with remark; sign-off "
            "allowed but optional axes remain for the fabricator."
        ),
        "full-wykonawczy": (
            "score 16..17 / 17 - full rysunek wykonawczy; clear for export."
        ),
        "unknown": "score out of expected range.",
    }
    return notes.get(verdict, notes["unknown"])


def _cross_validate(req: CrossValidateRequest) -> CrossValidateResponse:
    import re

    def norm(s: str) -> str:
        s = s.strip()
        if req.case_insensitive:
            s = s.lower()
        s = re.sub(r"\s+", " ", s)
        return s.strip(" .,:;-_/\\\"'")

    ocr_norm = {norm(s): s for s in req.ocr_strings if s.strip()}
    dxf_norm = {norm(s): s for s in req.dxf_strings if s.strip()}

    matched = sorted(ocr_norm.keys() & dxf_norm.keys())
    only_ocr = sorted(ocr_norm.keys() - dxf_norm.keys())
    only_dxf = sorted(dxf_norm.keys() - ocr_norm.keys())

    if req.numeric_tolerance > 0.0:
        # Promote near-matches between numeric tokens.
        def to_num(s: str) -> float | None:
            try:
                return float(s.replace(",", ".").replace(" ", ""))
            except ValueError:
                return None
        ocr_nums = [(s, to_num(s)) for s in only_ocr]
        dxf_nums = [(s, to_num(s)) for s in only_dxf]
        promoted: list[str] = []
        for o_s, o_v in ocr_nums:
            if o_v is None:
                continue
            for d_s, d_v in dxf_nums:
                if d_v is None:
                    continue
                if abs(o_v - d_v) <= req.numeric_tolerance:
                    promoted.append(o_s)
                    only_dxf.remove(d_s)
                    break
        for s in promoted:
            only_ocr.remove(s)
            matched.append(s)
        matched.sort()

    return CrossValidateResponse(
        matched=matched,
        only_in_ocr=only_ocr,
        only_in_dxf=only_dxf,
    )


# ---------------------------------------------------------------------------
# Idle-shutdown loop + entry point
# ---------------------------------------------------------------------------


async def _idle_watchdog(idle_timeout_sec: int) -> None:
    while True:
        await asyncio.sleep(30)
        if time.monotonic() - LAST_HIT > idle_timeout_sec:
            logger.info("vision_sidecar_idle_shutdown", idle_timeout_sec=idle_timeout_sec)
            os._exit(0)  # uvicorn loop is in the same process; exit cleanly.


def _write_pid_and_port(port: int) -> None:
    try:
        SETTINGS.pid_file.write_text(str(os.getpid()), encoding="utf-8")
        SETTINGS.port_file.write_text(str(port), encoding="utf-8")
    except OSError as ex:
        logger.warning("vision_sidecar_pid_write_failed", error=str(ex))


def main() -> None:
    parser = argparse.ArgumentParser(description="AcadMcp.Vision sidecar")
    parser.add_argument("--http-port", type=int, default=SETTINGS.http_port)
    parser.add_argument("--host", default=SETTINGS.host)
    parser.add_argument("--log-level", default=SETTINGS.log_level)
    parser.add_argument("--idle-timeout-sec", type=int, default=SETTINGS.idle_timeout_sec)
    args = parser.parse_args()

    if args.host != "127.0.0.1":
        # rule 29 §2 - hard refuse non-loopback bind.
        print(
            "ERROR: --host must be 127.0.0.1 (rule 29 §2 forbids non-loopback bind).",
            file=sys.stderr,
        )
        sys.exit(2)

    SETTINGS.ensure_paths()
    _write_pid_and_port(args.http_port)
    logger.info(
        "vision_sidecar_starting",
        http_port=args.http_port,
        version=__version__,
        phase=__phase__,
    )

    app = create_app()

    @app.on_event("startup")
    async def _start_idle_loop() -> None:
        asyncio.create_task(_idle_watchdog(args.idle_timeout_sec))

    uvicorn.run(
        app,
        host=args.host,
        port=args.http_port,
        log_level=args.log_level,
        access_log=False,
    )


if __name__ == "__main__":
    sys.exit(main())
