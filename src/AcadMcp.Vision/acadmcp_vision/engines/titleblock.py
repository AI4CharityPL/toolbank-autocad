"""Heuristic title-block extractor: take OCR tokens + a discipline hint and pick
the right rectangle + label-vs-value field associations.

This is a pragmatic v1: per-discipline templates are dictionaries of
field_key -> list of accepted label aliases, plus a panel hint
(bottom_right / right_strip / bottom_strip). Phase 8 will train a real model.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

from ..schemas import OcrToken, PixelBox, TitleblockField


@dataclass(frozen=True)
class _Template:
    panel: str  # "bottom_right" | "right_strip" | "bottom_strip"
    fields: dict[str, list[str]]


TEMPLATES: dict[str, _Template] = {
    "architectural-eu": _Template(
        panel="bottom_right",
        fields={
            "drawing_no": ["nr rys", "nr rysunku", "drawing no", "rys nr", "no"],
            "title": ["tytul", "tytuł", "title", "nazwa rysunku"],
            "scale": ["skala", "scale"],
            "date": ["data", "date"],
            "drawn_by": ["rysował", "rysowal", "drawn by", "wykonal", "wykonał"],
            "checked_by": ["sprawdził", "sprawdzil", "checked by", "kontrola"],
            "project": ["projekt", "project", "nazwa projektu"],
            "investor": ["inwestor", "investor", "client"],
            "phase": ["faza", "phase", "stage"],
            "rev": ["rew", "rev", "wersja", "version"],
            "sheet": ["arkusz", "sheet"],
        },
    ),
    "architectural-us": _Template(
        panel="bottom_right",
        fields={
            "drawing_no": ["dwg no", "drawing no", "sheet no", "no"],
            "title": ["title", "drawing title"],
            "scale": ["scale"],
            "date": ["date", "issued"],
            "drawn_by": ["drawn", "drawn by"],
            "checked_by": ["checked", "checked by", "approved by"],
            "project": ["project"],
            "client": ["client", "owner"],
            "rev": ["rev", "revision"],
            "sheet": ["sheet"],
        },
    ),
    "mechanical": _Template(
        panel="bottom_strip",
        fields={
            "drawing_no": ["drawing no", "dwg no", "part no", "no"],
            "title": ["title"],
            "scale": ["scale"],
            "material": ["material"],
            "weight": ["weight", "mass"],
            "tolerance": ["tolerance"],
            "rev": ["rev"],
            "sheet": ["sheet"],
        },
    ),
    "electrical": _Template(
        panel="bottom_right",
        fields={
            "drawing_no": ["drawing no", "dwg no", "no"],
            "title": ["title"],
            "scale": ["scale"],
            "date": ["date"],
            "drawn_by": ["drn", "drn by", "drawn"],
            "checked_by": ["chk", "chk by", "checked"],
            "project": ["project"],
            "rev": ["rev"],
            "sheet": ["sheet"],
        },
    ),
    "civil": _Template(
        panel="bottom_right",
        fields={
            "drawing_no": ["drawing no", "dwg no", "no"],
            "title": ["title"],
            "scale": ["scale"],
            "date": ["date"],
            "drawn_by": ["drawn by"],
            "checked_by": ["checked by"],
            "rev": ["rev"],
        },
    ),
}


def _normalise(s: str) -> str:
    return re.sub(r"\s+", " ", s.strip().lower())


def _select_panel_tokens(
    tokens: list[OcrToken], width: int, height: int, panel: str
) -> tuple[list[OcrToken], PixelBox | None]:
    """Filter tokens to the side / strip we expect the title block in."""
    if panel == "bottom_right":
        x_min = int(width * 0.55)
        y_min = int(height * 0.55)
        keep = [t for t in tokens if t.box.x >= x_min and t.box.y >= y_min]
        bx = PixelBox(x=x_min, y=y_min, width=width - x_min, height=height - y_min)
    elif panel == "right_strip":
        x_min = int(width * 0.65)
        keep = [t for t in tokens if t.box.x >= x_min]
        bx = PixelBox(x=x_min, y=0, width=width - x_min, height=height)
    elif panel == "bottom_strip":
        y_min = int(height * 0.70)
        keep = [t for t in tokens if t.box.y >= y_min]
        bx = PixelBox(x=0, y=y_min, width=width, height=height - y_min)
    else:
        keep, bx = tokens, None
    return keep, bx


def extract(
    tokens: list[OcrToken],
    discipline: str,
    image_width: int,
    image_height: int,
) -> tuple[list[TitleblockField], PixelBox | None, bool]:
    """Return (fields, panel_box, low_confidence)."""
    template = TEMPLATES.get(discipline) or TEMPLATES["architectural-eu"]
    panel_tokens, panel_box = _select_panel_tokens(
        tokens, image_width, image_height, template.panel
    )
    fields: list[TitleblockField] = []
    used_value_indexes: set[int] = set()
    overall_low = False

    for canon, aliases in template.fields.items():
        # Find a label token (left side of the row, usually) by alias match.
        label_idx = _find_label(panel_tokens, aliases)
        if label_idx is None:
            continue
        label_tok = panel_tokens[label_idx]
        # Find the nearest value token to the right (or directly below) of the label.
        value_idx, value_tok = _find_value(panel_tokens, label_tok, used_value_indexes)
        if value_idx is None or value_tok is None:
            continue
        used_value_indexes.add(value_idx)
        if value_tok.confidence < 0.70:
            overall_low = True
        fields.append(
            TitleblockField(
                field=canon,
                raw_label=label_tok.text,
                value=value_tok.text,
                confidence=value_tok.confidence,
                box=value_tok.box,
            )
        )

    return fields, panel_box, overall_low


def _find_label(tokens: list[OcrToken], aliases: list[str]) -> int | None:
    norm_aliases = [_normalise(a) for a in aliases]
    for i, t in enumerate(tokens):
        n = _normalise(t.text).rstrip(":").rstrip(".")
        if n in norm_aliases:
            return i
        for a in norm_aliases:
            if a in n and len(n) - len(a) <= 3:  # allow short trailing punctuation
                return i
    return None


def _find_value(
    tokens: list[OcrToken], label: OcrToken, used: set[int]
) -> tuple[int | None, OcrToken | None]:
    """Pick the closest token that's either to the right OR directly below the label."""
    candidates: list[tuple[float, int, OcrToken]] = []
    lx_center = label.box.x + label.box.width / 2.0
    ly_center = label.box.y + label.box.height / 2.0
    for i, t in enumerate(tokens):
        if i in used or t is label:
            continue
        if _normalise(t.text) in {_normalise(label.text)}:
            continue
        tx_center = t.box.x + t.box.width / 2.0
        ty_center = t.box.y + t.box.height / 2.0
        right = tx_center > lx_center and abs(ty_center - ly_center) < label.box.height * 1.2
        below = ty_center > ly_center and abs(tx_center - lx_center) < label.box.width * 1.5
        if not (right or below):
            continue
        dx = tx_center - lx_center
        dy = ty_center - ly_center
        dist = (dx * dx + dy * dy) ** 0.5
        candidates.append((dist, i, t))
    if not candidates:
        return None, None
    candidates.sort(key=lambda c: c[0])
    _, idx, tok = candidates[0]
    return idx, tok
