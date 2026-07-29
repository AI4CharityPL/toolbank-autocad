"""OCR adapters: PaddleOCR (default), EasyOCR, Tesseract.

All three return the same canonical shape: list[OcrToken]. PaddleOCR + EasyOCR
report confidence in [0,1]; Tesseract reports 0-100 which we rescale.
Per rule 32, trap #10: each engine instance is shared via a module-global,
guarded by an asyncio.Semaphore in app.py.
"""

from __future__ import annotations

import importlib
from dataclasses import dataclass
from typing import Any

from PIL import Image

from ..schemas import OcrToken, PixelBox

_paddle_engine: Any = None
_easyocr_reader: Any = None


@dataclass(frozen=True)
class EngineUnavailable(Exception):
    engine: str
    install_hint: str

    def __str__(self) -> str:  # pragma: no cover - trivial
        return f"{self.engine}: {self.install_hint}"


def engine_version(name: str) -> str:
    try:
        mod = importlib.import_module(_module_for(name))
        return getattr(mod, "__version__", "unknown")
    except Exception:
        return "unavailable"


def _module_for(name: str) -> str:
    return {
        "paddleocr": "paddleocr",
        "easyocr": "easyocr",
        "tesseract": "pytesseract",
    }[name]


# ---------------------------------------------------------------------------
# PaddleOCR
# ---------------------------------------------------------------------------


def _get_paddle(languages: list[str]):
    """Construct (or reuse) a PaddleOCR engine. Runs on CPU by default."""
    global _paddle_engine
    if _paddle_engine is not None:
        return _paddle_engine
    try:
        paddle = importlib.import_module("paddleocr")
    except ImportError as ex:
        raise EngineUnavailable(
            engine="paddleocr",
            install_hint="pip install paddleocr paddlepaddle",
        ) from ex
    lang = languages[0] if languages else "en"
    _paddle_engine = paddle.PaddleOCR(use_angle_cls=True, lang=lang, show_log=False)
    return _paddle_engine


def _run_paddle(image: Image.Image, languages: list[str]) -> list[OcrToken]:
    engine = _get_paddle(languages)
    import numpy as np  # paddleocr expects numpy

    arr = np.array(image)
    result = engine.ocr(arr, cls=True)
    tokens: list[OcrToken] = []
    for line_set in result or []:
        for entry in line_set or []:
            box_pts, (text, conf) = entry
            xs = [p[0] for p in box_pts]
            ys = [p[1] for p in box_pts]
            x, y = int(min(xs)), int(min(ys))
            w, h = int(max(xs) - x), int(max(ys) - y)
            tokens.append(
                OcrToken(
                    text=str(text),
                    confidence=float(conf),
                    box=PixelBox(x=x, y=y, width=w, height=h),
                    low_confidence=float(conf) < 0.70,  # rule 32, trap #3
                )
            )
    return tokens


# ---------------------------------------------------------------------------
# EasyOCR
# ---------------------------------------------------------------------------


def _get_easy(languages: list[str]):
    global _easyocr_reader
    if _easyocr_reader is not None:
        return _easyocr_reader
    try:
        easy = importlib.import_module("easyocr")
    except ImportError as ex:
        raise EngineUnavailable(
            engine="easyocr",
            install_hint="pip install easyocr",
        ) from ex
    _easyocr_reader = easy.Reader(languages or ["en"], gpu=False)
    return _easyocr_reader


def _run_easy(image: Image.Image, languages: list[str]) -> list[OcrToken]:
    reader = _get_easy(languages)
    import numpy as np

    result = reader.readtext(np.array(image))
    tokens: list[OcrToken] = []
    for box_pts, text, conf in result:
        xs = [p[0] for p in box_pts]
        ys = [p[1] for p in box_pts]
        x, y = int(min(xs)), int(min(ys))
        w, h = int(max(xs) - x), int(max(ys) - y)
        tokens.append(
            OcrToken(
                text=str(text),
                confidence=float(conf),
                box=PixelBox(x=x, y=y, width=w, height=h),
                low_confidence=float(conf) < 0.70,
            )
        )
    return tokens


# ---------------------------------------------------------------------------
# Tesseract
# ---------------------------------------------------------------------------


def _run_tesseract(image: Image.Image, languages: list[str]) -> list[OcrToken]:
    try:
        pyt = importlib.import_module("pytesseract")
    except ImportError as ex:
        raise EngineUnavailable(
            engine="tesseract",
            install_hint="pip install pytesseract  (and install Tesseract OCR binary)",
        ) from ex
    lang_arg = "+".join(_tess_lang(code) for code in (languages or ["en"]))
    data = pyt.image_to_data(image, lang=lang_arg, output_type=pyt.Output.DICT)
    tokens: list[OcrToken] = []
    for i, txt in enumerate(data["text"]):
        if not txt or not txt.strip():
            continue
        conf_raw = data["conf"][i]
        try:
            conf_pct = float(conf_raw)
        except (TypeError, ValueError):
            conf_pct = -1.0
        if conf_pct < 0:
            continue
        conf = conf_pct / 100.0
        tokens.append(
            OcrToken(
                text=txt,
                confidence=conf,
                box=PixelBox(
                    x=int(data["left"][i]),
                    y=int(data["top"][i]),
                    width=int(data["width"][i]),
                    height=int(data["height"][i]),
                ),
                low_confidence=conf < 0.70,
            )
        )
    return tokens


def _tess_lang(code: str) -> str:
    # ISO-639-1 -> Tesseract 3-letter map (subset).
    return {
        "en": "eng",
        "pl": "pol",
        "de": "deu",
        "fr": "fra",
        "es": "spa",
        "it": "ita",
        "cs": "ces",
        "ru": "rus",
    }.get(code.lower(), code)


# ---------------------------------------------------------------------------
# Public dispatch
# ---------------------------------------------------------------------------


def run_ocr(engine: str, image: Image.Image, languages: list[str]) -> list[OcrToken]:
    if engine == "paddleocr":
        return _run_paddle(image, languages)
    if engine == "easyocr":
        return _run_easy(image, languages)
    if engine == "tesseract":
        return _run_tesseract(image, languages)
    raise ValueError(f"Unknown OCR engine: {engine!r}")
