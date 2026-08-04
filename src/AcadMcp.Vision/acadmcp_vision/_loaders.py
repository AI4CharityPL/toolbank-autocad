"""Lazy loaders for image / PDF input + ML engine probes.

Per rule 32:
- trap #1  - normalise everything to RGB uint8 with capped long side.
- trap #2  - PDFs are page-by-page; multi-page PDFs MUST be rasterised explicitly.
- trap #9  - Heavy ML imports MUST be lazy.
"""

from __future__ import annotations

import base64
import hashlib
import importlib
import io
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from .schemas import ImageRef

# ---------------------------------------------------------------------------
# Image loading
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class LoadedImage:
    pil: Image.Image
    sha256: str  # of the *original* bytes, used as cache key
    source_label: str


def _read_bytes(image: ImageRef) -> tuple[bytes, str]:
    """Return (bytes, label) from either a path or a base64 data URL."""
    if image.path:
        p = Path(image.path)
        if not p.exists():
            raise FileNotFoundError(f"Image not found: {image.path}")
        return p.read_bytes(), p.name
    if image.base64:
        s = image.base64
        if s.startswith("data:"):
            s = s.split(",", 1)[1]
        return base64.b64decode(s), "<base64>"
    raise ValueError("ImageRef requires either path or base64 to be set.")


def load_image(image: ImageRef, max_long_side: int | None = 1920) -> LoadedImage:
    """Load any supported source into RGB PIL.Image, optionally downscaled.

    Supports raw image formats AND PDFs (one page at a time per ImageRef.page).
    """
    raw, label = _read_bytes(image)
    sha = hashlib.sha256(raw).hexdigest()

    # PDF branch (trap #2): rasterise one page.
    if label.lower().endswith(".pdf") or raw[:4] == b"%PDF":
        page = image.page or 1
        pil = _rasterise_pdf_page(raw, page=page, dpi=image.dpi)
    else:
        pil = Image.open(io.BytesIO(raw))

    # Convert to RGB uint8 (trap #1).
    if pil.mode != "RGB":
        pil = pil.convert("RGB")

    # Downscale (trap #1).
    if max_long_side is not None:
        long_side = max(pil.size)
        if long_side > max_long_side:
            scale = max_long_side / long_side
            new_size = (int(pil.size[0] * scale), int(pil.size[1] * scale))
            pil = pil.resize(new_size, Image.LANCZOS)

    return LoadedImage(pil=pil, sha256=sha, source_label=label)


def _rasterise_pdf_page(pdf_bytes: bytes, *, page: int, dpi: int) -> Image.Image:
    """Use pypdfium2 to rasterise a single page. Falls back with a clear error."""
    try:
        pdfium = importlib.import_module("pypdfium2")
    except ImportError as ex:
        raise ImportError("PDF input requires pypdfium2. Run `pip install pypdfium2`.") from ex

    doc = pdfium.PdfDocument(pdf_bytes)
    if page < 1 or page > len(doc):
        raise ValueError(f"PDF has {len(doc)} pages; page={page} is out of range.")
    pdf_page = doc[page - 1]
    bitmap = pdf_page.render(scale=dpi / 72.0)
    return bitmap.to_pil().convert("RGB")


# ---------------------------------------------------------------------------
# Optional-dep probing (rule 32, trap #9)
# ---------------------------------------------------------------------------

OPTIONAL_DEPS = {
    "paddleocr": "paddleocr",
    "easyocr": "easyocr",
    "tesseract": "pytesseract",
    "ultralytics": "ultralytics",
    "torch": "torch",
    "anthropic": "anthropic",
    "openai": "openai",
    "pypdfium2": "pypdfium2",
    "sam2": "sam2",
}


def is_dep_available(key: str) -> bool:
    mod = OPTIONAL_DEPS.get(key)
    if not mod:
        return False
    try:
        importlib.import_module(mod)
        return True
    except Exception:  # noqa: BLE001 - third-party imports can throw RuntimeError
        return False


def list_optional_deps() -> dict[str, bool]:
    return {k: is_dep_available(k) for k in OPTIONAL_DEPS}
