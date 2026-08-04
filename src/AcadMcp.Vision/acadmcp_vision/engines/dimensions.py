"""Filter OCR tokens that look like dimension callouts and parse them.

Heuristic v1: a dimension token is something like:
    1234, 1234.5, 1.234,5  + optional unit + optional fractional inches.

We deliberately don't try to find the leader / arrow geometry yet; that's a
Phase 8 vision-model job. We only return the *text* + a parsed numeric value
in mm if we can figure out the unit.
"""

from __future__ import annotations

import re

from ..schemas import DimensionToken, OcrToken

# Examples we want to match:
#   "1234"
#   "1234.5"
#   "12,50"
#   "1 234"
#   "1234 mm"
#   "12.5 cm"
#   "12'-6\""
#   "12.5\""
DIM_RE = re.compile(
    r"""
    ^\s*
    (?P<number>
        (?:\d{1,3}(?:[\s.,]\d{3})+|\d+)        # 1 234 / 1234 / 1,234 / 1.234
        (?:[.,]\d+)?                            # optional decimal
    )
    \s*
    (?P<unit>mm|cm|m|in|ft|"|''|')?
    \s*$
    """,
    re.VERBOSE | re.IGNORECASE,
)

UNIT_TO_MM: dict[str, float] = {
    "mm": 1.0,
    "cm": 10.0,
    "m": 1000.0,
    "in": 25.4,
    '"': 25.4,
    "''": 25.4,
    "ft": 304.8,
    "'": 304.8,
}


def _parse(text: str, units_hint: str) -> tuple[float | None, str | None]:
    m = DIM_RE.match(text)
    if not m:
        return None, None
    raw = m.group("number")
    unit = (m.group("unit") or "").lower() or None
    cleaned = raw.replace(" ", "")
    if cleaned.count(",") == 1 and cleaned.count(".") == 0:
        cleaned = cleaned.replace(",", ".")
    elif cleaned.count(",") >= 1 and cleaned.count(".") >= 1:
        # European 1.234,56 -> 1234.56
        cleaned = cleaned.replace(".", "").replace(",", ".")
    elif cleaned.count(",") > 1:
        cleaned = cleaned.replace(",", "")
    try:
        val = float(cleaned)
    except ValueError:
        return None, None
    eff_unit = unit or (None if units_hint == "auto" else units_hint)
    if eff_unit and eff_unit in UNIT_TO_MM:
        return val * UNIT_TO_MM[eff_unit], eff_unit
    return val if eff_unit is None else None, eff_unit


def from_ocr(tokens: list[OcrToken], units_hint: str, min_confidence: float) -> list[DimensionToken]:
    out: list[DimensionToken] = []
    for t in tokens:
        if t.confidence < min_confidence:
            continue
        val_mm, unit = _parse(t.text, units_hint)
        if val_mm is None and unit is None:
            continue
        out.append(
            DimensionToken(
                text=t.text,
                value_mm=val_mm,
                units=unit,
                confidence=t.confidence,
                box=t.box,
            )
        )
    return out
