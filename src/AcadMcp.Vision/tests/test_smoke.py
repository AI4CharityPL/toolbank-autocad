"""Smoke tests for AcadMcp.Vision Phase 4 sidecar.

These run with ZERO heavy ML deps installed - the endpoints must either
return a real result (for endpoints that don't need ML, e.g. cross-validate)
or 503 with an installHint (for endpoints that do).
"""

from __future__ import annotations

import io

from fastapi.testclient import TestClient
from PIL import Image

from acadmcp_vision import __version__
from acadmcp_vision.app import create_app


def _make_png_bytes() -> bytes:
    img = Image.new("RGB", (200, 100), "white")
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def test_health() -> None:
    app = create_app()
    with TestClient(app) as client:
        r = client.get("/health")
        assert r.status_code == 200
        body = r.json()
        assert body["status"] == "ok"
        assert body["version"] == __version__


def test_version_lists_optional_deps() -> None:
    app = create_app()
    with TestClient(app) as client:
        r = client.get("/version")
        assert r.status_code == 200
        body = r.json()
        assert "optional_deps" in body
        assert "api_keys" in body
        assert isinstance(body["api_keys"]["anthropic"], bool)


def test_ocr_returns_503_when_engine_missing(tmp_path) -> None:
    p = tmp_path / "blank.png"
    p.write_bytes(_make_png_bytes())
    app = create_app()
    with TestClient(app) as client:
        r = client.post(
            "/v1/ocr",
            json={"image": {"path": str(p)}, "engine": "paddleocr"},
        )
    # Either OCR is installed locally (200) or it's not (503 with installHint).
    assert r.status_code in (200, 503)
    if r.status_code == 503:
        body = r.json()
        assert body["error"] == "model_not_available"
        assert "install" in body["install_hint"].lower()


def test_detect_symbols_503_when_weights_missing(tmp_path) -> None:
    p = tmp_path / "blank.png"
    p.write_bytes(_make_png_bytes())
    app = create_app()
    with TestClient(app) as client:
        r = client.post(
            "/v1/detect-symbols",
            json={"image": {"path": str(p)}, "discipline": "arch"},
        )
    assert r.status_code in (200, 503)
    if r.status_code == 503:
        body = r.json()
        assert body["error"] == "model_not_available"


def test_cross_validate_no_deps_required() -> None:
    app = create_app()
    with TestClient(app) as client:
        r = client.post(
            "/v1/cross-validate-with-dxf",
            json={
                "ocr_strings": ["12.5 mm", "Title", "Drawing No"],
                "dxf_strings": ["12.5 mm", "Title"],
            },
        )
        assert r.status_code == 200
        body = r.json()
        assert "title" in body["matched"]
        assert "12.5 mm" in body["matched"]
        assert "drawing no" in body["only_in_ocr"]


def test_cross_validate_numeric_tolerance() -> None:
    app = create_app()
    with TestClient(app) as client:
        r = client.post(
            "/v1/cross-validate-with-dxf",
            json={
                "ocr_strings": ["1234.5"],
                "dxf_strings": ["1234.6"],
                "numeric_tolerance": 0.2,
            },
        )
        assert r.status_code == 200
        body = r.json()
        assert "1234.5" in body["matched"]


def test_image_not_found_is_400(tmp_path) -> None:
    app = create_app()
    with TestClient(app) as client:
        r = client.post(
            "/v1/ocr",
            json={"image": {"path": str(tmp_path / "missing.png")}},
        )
        assert r.status_code == 400
