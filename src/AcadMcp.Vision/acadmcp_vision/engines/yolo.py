"""YOLO custom CAD-symbol detector via Ultralytics.

Per rule 32, trap #5: weights are per-discipline. We look in
%LOCALAPPDATA%\\AcadMcp\\vision-models\\cad-symbols-{discipline}.pt.
"""

from __future__ import annotations

import importlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image

from ..config import SETTINGS
from ..schemas import PixelBox, SymbolDetection

_models: dict[str, Any] = {}


@dataclass(frozen=True)
class WeightsMissingError(Exception):
    discipline: str
    expected_path: str

    def __str__(self) -> str:  # pragma: no cover - trivial
        return f"YOLO weights for discipline={self.discipline} not found at {self.expected_path}"


def weights_path(discipline: str) -> Path:
    return SETTINGS.model_dir / f"cad-symbols-{discipline}.pt"


def engine_version() -> str:
    try:
        return importlib.import_module("ultralytics").__version__
    except Exception:
        return "unavailable"


def _get_model(discipline: str):
    if discipline in _models:
        return _models[discipline]
    try:
        ult = importlib.import_module("ultralytics")
    except ImportError as ex:
        raise ImportError("Ultralytics not installed. Run `pip install ultralytics`.") from ex
    p = weights_path(discipline)
    if not p.exists():
        raise WeightsMissingError(discipline=discipline, expected_path=str(p))
    model = ult.YOLO(str(p))
    _models[discipline] = model
    return model


def detect(image: Image.Image, discipline: str, min_confidence: float) -> list[SymbolDetection]:
    model = _get_model(discipline)
    res = model.predict(image, conf=min_confidence, verbose=False)
    out: list[SymbolDetection] = []
    for r in res:
        names = getattr(r, "names", {})
        if r.boxes is None:
            continue
        for box in r.boxes:
            xyxy = box.xyxy[0].tolist()
            conf = float(box.conf[0].item())
            cls_id = int(box.cls[0].item())
            label = str(names.get(cls_id, str(cls_id)))
            x1, y1, x2, y2 = (int(v) for v in xyxy)
            out.append(
                SymbolDetection(
                    label=label,
                    confidence=conf,
                    box=PixelBox(x=x1, y=y1, width=x2 - x1, height=y2 - y1),
                )
            )
    return out
