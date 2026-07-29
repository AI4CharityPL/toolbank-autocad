"""Rasterise an exported AutoCAD PDF into (a) one full-page PNG overview and (b) a grid of tile PNGs.

Phase B helper when PublishToWeb PNG.pc3 is missing on the host. It turns the
PDF produced by `acad.files.export_file format=PDF scope=Extents` into the
same visual corpus that the `describe_image architect-reviewer` pass would
have analysed, so the agent can read the tile PNGs directly with its own
vision capability while the LLM-side persona path remains unavailable.

Usage:
    python scripts/rasterize-pdf-tiles.py <pdf> <out-dir> [--rows 3] [--cols 4] [--dpi 300] [--overview-dpi 150]

Exit codes:
    0 - success
    2 - bad arguments
    3 - rasterisation failed (pypdfium2 missing, malformed PDF, ...)
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    import pypdfium2 as pdfium
except ImportError:
    sys.stderr.write("pypdfium2 not installed. pip install pypdfium2\n")
    sys.exit(3)

from PIL import Image


def _render_page(pdf_path: Path, page_index: int, dpi: int) -> Image.Image:
    doc = pdfium.PdfDocument(str(pdf_path))
    if page_index < 0 or page_index >= len(doc):
        raise ValueError(f"page {page_index + 1} out of range (total {len(doc)}).")
    page = doc[page_index]
    bitmap = page.render(scale=dpi / 72.0)
    return bitmap.to_pil().convert("RGB")


def rasterise(pdf: Path, out_dir: Path, rows: int, cols: int, dpi: int, overview_dpi: int) -> dict:
    out_dir.mkdir(parents=True, exist_ok=True)

    overview = _render_page(pdf, 0, overview_dpi)
    overview_path = out_dir / "overview.png"
    overview.save(overview_path, format="PNG", optimize=True)

    hi = _render_page(pdf, 0, dpi)
    w, h = hi.size
    tile_w = w // cols
    tile_h = h // rows

    tiles = []
    for r in range(rows):
        for c in range(cols):
            left = c * tile_w
            top = r * tile_h
            right = w if c == cols - 1 else left + tile_w
            bottom = h if r == rows - 1 else top + tile_h
            cropped = hi.crop((left, top, right, bottom))
            max_side = 1600
            cw, ch = cropped.size
            scale = min(1.0, max_side / max(cw, ch))
            if scale < 1.0:
                cropped = cropped.resize((int(cw * scale), int(ch * scale)), Image.LANCZOS)
            name = f"tile-r{r}c{c}.png"
            p = out_dir / name
            cropped.save(p, format="PNG", optimize=True)
            tiles.append({
                "name": name,
                "row": r,
                "col": c,
                "px_bbox": [left, top, right, bottom],
                "size_px": cropped.size,
            })

    manifest = {
        "pdf": str(pdf),
        "overview": str(overview_path),
        "overview_size_px": overview.size,
        "dpi_tiles": dpi,
        "dpi_overview": overview_dpi,
        "rows": rows,
        "cols": cols,
        "tiles": tiles,
    }
    (out_dir / "tiles-manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    return manifest


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("pdf", type=Path)
    ap.add_argument("out_dir", type=Path)
    ap.add_argument("--rows", type=int, default=3)
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--dpi", type=int, default=300)
    ap.add_argument("--overview-dpi", type=int, default=150)
    args = ap.parse_args()

    if not args.pdf.exists():
        sys.stderr.write(f"PDF not found: {args.pdf}\n")
        return 2

    try:
        manifest = rasterise(
            args.pdf, args.out_dir, args.rows, args.cols, args.dpi, args.overview_dpi
        )
    except Exception as ex:
        sys.stderr.write(f"rasterisation failed: {ex}\n")
        return 3

    print(json.dumps(manifest, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
