"""Render and crop the D12 plan from PDF to focus the judge on the building only."""
import sys
from pathlib import Path
import pypdfium2 as pdfium
from PIL import Image

src = Path(sys.argv[1])
dst = Path(sys.argv[2])

pdf = pdfium.PdfDocument(str(src))
img = pdf[0].render(scale=6.0).to_pil()
w, h = img.size
print(f"full render: {w}x{h}")

gray = img.convert("L")
bbox = gray.point(lambda p: 0 if p < 230 else 255).getbbox()
if bbox is None:
    print("no ink detected")
    sys.exit(1)
print(f"ink bbox: {bbox}")

pad = 100
x0 = max(0, bbox[0] - pad)
y0 = max(0, bbox[1] - pad)
x1 = min(w, bbox[2] + pad)
y1 = min(h, bbox[3] + pad)
crop = img.crop((x0, y0, x1, y1))
crop.save(str(dst))
print(f"saved: {dst} size={crop.size}")
