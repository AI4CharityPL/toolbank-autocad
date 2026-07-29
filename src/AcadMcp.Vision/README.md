# AcadMcp.Vision

Python sidecar for vision/OCR/segmentation/vision-LLM. Spoken to over **localhost HTTP/JSON** by the C# Backend's `Categories/Vision/` tools.

> Architectural invariants: see `.cursor/rules/29-acad-vision-architecture.mdc` and `.cursor/rules/32-acad-vision-traps.mdc`. All vision LLM calls (Anthropic Claude / OpenAI GPT-4o) are made HERE, never from C#.

## Why a separate process?

- ML deps (Torch, PaddleOCR, YOLO, SAM2) are huge and Python-native
- One place to cache, rate-limit, and key-manage vision LLM calls
- Crash isolation - a YOLO segfault must not bring down the AutoCAD plugin
- Independent scaling/restart cycle
- Idle-shutdown: sidecar self-terminates after 5 min of no requests so we never leak

## Quickstart

```powershell
cd src/AcadMcp.Vision
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e .[dev]

acadmcp-vision --http-port 50062
```

## Optional ML extras

```powershell
pip install -e .[ml]
pwsh ../../scripts/setup-vision-models.ps1 -All
```

## Endpoints (HTTP/JSON, all on `127.0.0.1`)

| Method | Path                              | Description                           |
| ------ | --------------------------------- | ------------------------------------- |
| GET    | `/health`                         | Liveness (always 200, even sans ML)   |
| GET    | `/version`                        | Version, loaded models, opt-deps, keys|
| POST   | `/v1/ocr`                         | PaddleOCR / EasyOCR / Tesseract       |
| POST   | `/v1/detect-symbols`              | YOLO per-discipline CAD-symbol detect |
| POST   | `/v1/extract-titleblock`          | Title-block field extraction          |
| POST   | `/v1/extract-dimensions`          | Dimension OCR + mm normalisation      |
| POST   | `/v1/classify-drawing`            | Vision LLM discipline + sheet-type    |
| POST   | `/v1/describe-image`              | Free-form vision LLM description      |
| POST   | `/v1/cross-validate-with-dxf`     | OCR vs DXF string-set diff            |

When an ML dep is missing, endpoints return **503** with `{ error, engine, install_hint }`.
