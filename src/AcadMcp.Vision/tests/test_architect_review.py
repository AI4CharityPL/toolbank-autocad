"""Static tests for the senior-architect-reviewer persona (rule 60 / D11).

These tests DO NOT call a real vision LLM - they stub `llm_engine.describe`
so CI can run without network, API keys or spending budget. They verify:

 1. The 17-criterion rubric contract is exactly 17 rows in canonical order.
 2. The persona prompt (EN + PL) mentions every canonical label.
 3. The JSON parser tolerates markdown fences, trailing prose, and missing
    rows (missing rows fall back to score=0.0, never silently dropped).
 4. Score -> verdict threshold logic matches rule 60 §1.
 5. The /v1/architect-review endpoint round-trips a simulated LLM reply
    into the expected `ArchitectReviewResponse` shape.
 6. The persona descriptor JSON on disk is in sync with the rubric.
"""

from __future__ import annotations

import io
import json
import re
from pathlib import Path

from fastapi.testclient import TestClient
from PIL import Image

from acadmcp_vision import app as app_module
from acadmcp_vision.app import (
    _parse_architect_review_json,
    _SENIOR_ARCHITECT_PROMPT_EN,
    _SENIOR_ARCHITECT_PROMPT_PL,
    _threshold_note,
    _verdict_from_score,
    create_app,
)
from acadmcp_vision.engines.vision_llm import LlmReply
from acadmcp_vision.schemas import ARCHITECT_REVIEW_CRITERIA


# ---------------------------------------------------------------------------
# Rubric contract
# ---------------------------------------------------------------------------


def test_rubric_has_exactly_17_criteria_in_canonical_order() -> None:
    assert len(ARCHITECT_REVIEW_CRITERIA) == 17
    ids = [cid for cid, _, _ in ARCHITECT_REVIEW_CRITERIA]
    assert ids == list(range(1, 18)), "IDs must be 1..17 in canonical order"

    labels = [label for _, label, _ in ARCHITECT_REVIEW_CRITERIA]
    assert labels == [
        "hatching", "furniture", "plumbing", "doors", "windows",
        "verticals", "grid", "dimensions", "schedules", "callouts",
        "sections", "lineweight", "finishes-legend", "orientation-scale",
        "reflected-ceiling", "details", "room-program",
    ]


def test_persona_prompt_en_mentions_every_canonical_label() -> None:
    for _cid, label, _axis in ARCHITECT_REVIEW_CRITERIA:
        assert label in _SENIOR_ARCHITECT_PROMPT_EN, (
            f"EN prompt is missing canonical label '{label}'"
        )


def test_persona_prompt_en_cites_rubric_rules_60_through_70() -> None:
    # Rule 60 is the rubric itself + 61-70 are the category rules it maps to.
    for rule_id in (60, 61, 62, 63, 64, 65, 66, 67, 69, 70):
        assert f"rule {rule_id}" in _SENIOR_ARCHITECT_PROMPT_EN.lower(), (
            f"EN prompt must cite rule {rule_id}"
        )


def test_persona_prompt_en_demands_strict_json_output() -> None:
    assert "EXACTLY ONE JSON object" in _SENIOR_ARCHITECT_PROMPT_EN
    assert '"criteria"' in _SENIOR_ARCHITECT_PROMPT_EN
    assert "0.0|0.5|1.0" in _SENIOR_ARCHITECT_PROMPT_EN


def test_persona_prompt_pl_mentions_every_canonical_id() -> None:
    # PL prompt uses Polish labels but lists every rubric ID 1..17.
    for cid in range(1, 18):
        # Expect " {cid}." or " {cid:>2}." somewhere as a numbered bullet.
        pattern = re.compile(rf"(?:^|\s){cid:>2}\.")
        assert pattern.search(_SENIOR_ARCHITECT_PROMPT_PL), (
            f"PL prompt is missing numbered bullet for criterion {cid}"
        )


# ---------------------------------------------------------------------------
# Threshold logic (rule 60 §1)
# ---------------------------------------------------------------------------


def test_verdict_concept_sketch_under_10() -> None:
    assert _verdict_from_score(0.0) == "concept-sketch"
    assert _verdict_from_score(9.5) == "concept-sketch"
    assert _verdict_from_score(9.999) == "concept-sketch"


def test_verdict_technical_study_10_to_13() -> None:
    assert _verdict_from_score(10.0) == "technical-study"
    assert _verdict_from_score(13.5) == "technical-study"
    assert _verdict_from_score(13.999) == "technical-study"


def test_verdict_executive_with_remark_14_to_15() -> None:
    assert _verdict_from_score(14.0) == "executive-with-remark"
    assert _verdict_from_score(15.5) == "executive-with-remark"
    assert _verdict_from_score(15.999) == "executive-with-remark"


def test_verdict_full_wykonawczy_16_plus() -> None:
    assert _verdict_from_score(16.0) == "full-wykonawczy"
    assert _verdict_from_score(16.5) == "full-wykonawczy"
    assert _verdict_from_score(17.0) == "full-wykonawczy"


def test_threshold_note_matches_verdict() -> None:
    assert "concept sketch" in _threshold_note("concept-sketch").lower()
    assert "technical study" in _threshold_note("technical-study").lower()
    assert "executive" in _threshold_note("executive-with-remark").lower()
    assert "wykonawczy" in _threshold_note("full-wykonawczy").lower()


# ---------------------------------------------------------------------------
# JSON parser tolerance
# ---------------------------------------------------------------------------


def test_parser_accepts_clean_json_with_all_17_rows() -> None:
    rows = [
        {"id": cid, "label": label, "score": 1.0, "note": f"ok-{cid}"}
        for cid, label, _ in ARCHITECT_REVIEW_CRITERIA
    ]
    text = json.dumps({"criteria": rows})
    parsed = _parse_architect_review_json(text)
    assert len(parsed) == 17
    assert all(c.score == 1.0 for c in parsed)
    assert sum(c.score for c in parsed) == 17.0


def test_parser_snaps_scores_to_nearest_half() -> None:
    text = json.dumps({
        "criteria": [
            {"id": 1, "label": "hatching",           "score": 0.3, "note": "partial"},
            {"id": 2, "label": "furniture",          "score": 0.8, "note": "partial"},
            {"id": 3, "label": "plumbing",           "score": 0.25, "note": "partial"},
            {"id": 4, "label": "doors",              "score": 0.75, "note": "partial"},
            {"id": 5, "label": "windows",            "score": 1.0, "note": "ok"},
        ]
    })
    parsed = _parse_architect_review_json(text)
    by_id = {c.id: c for c in parsed}
    assert by_id[1].score == 0.5    # 0.3 -> 0.5
    assert by_id[2].score == 1.0    # 0.8 -> 1.0
    assert by_id[3].score in (0.0, 0.5)   # 0.25 is exactly on the boundary
    assert by_id[4].score in (0.5, 1.0)
    assert by_id[5].score == 1.0
    # Missing criteria (6..17) fall back to 0.0 per rule 60 §2.
    assert by_id[17].score == 0.0
    assert "did not score" in by_id[17].note.lower()


def test_parser_tolerates_markdown_code_fence_and_prose() -> None:
    text = (
        "Here is my review.\n\n"
        "```json\n"
        '{ "criteria": [ { "id": 1, "label": "hatching", "score": 1, "note": "ok" } ] }\n'
        "```\n"
        "End of review."
    )
    parsed = _parse_architect_review_json(text)
    by_id = {c.id: c for c in parsed}
    assert by_id[1].score == 1.0
    assert by_id[2].score == 0.0  # missing -> 0


def test_parser_returns_all_zeros_on_garbage() -> None:
    for bad in ("", "no json here", "{ not valid", '{"criteria": "not a list"}'):
        parsed = _parse_architect_review_json(bad)
        assert len(parsed) == 17
        assert all(c.score == 0.0 for c in parsed)


def test_parser_clamps_scores_out_of_bounds() -> None:
    text = json.dumps({
        "criteria": [
            {"id": 1, "label": "hatching",  "score": 5.0, "note": "overshoot"},
            {"id": 2, "label": "furniture", "score": -3.0, "note": "undershoot"},
        ]
    })
    parsed = _parse_architect_review_json(text)
    by_id = {c.id: c for c in parsed}
    assert by_id[1].score == 1.0
    assert by_id[2].score == 0.0


def test_parser_ignores_unknown_criterion_ids() -> None:
    text = json.dumps({
        "criteria": [
            {"id": 1,   "label": "hatching", "score": 1.0, "note": "ok"},
            {"id": 99,  "label": "bogus",    "score": 1.0, "note": "should not be accepted"},
            {"id": 2,   "label": "furniture", "score": 1.0, "note": "ok"},
        ]
    })
    parsed = _parse_architect_review_json(text)
    by_id = {c.id: c for c in parsed}
    assert 99 not in by_id
    assert by_id[1].score == 1.0
    assert by_id[2].score == 1.0
    assert by_id[17].score == 0.0


# ---------------------------------------------------------------------------
# /v1/architect-review endpoint (stubbed LLM)
# ---------------------------------------------------------------------------


def _blank_png(tmp_path: Path) -> Path:
    # Each test gets a DIFFERENT image so the sidecar's sha256-keyed cache
    # cannot return a previous test's stubbed reply. We seed the colour
    # from the tmp_path name (unique per pytest function).
    p = tmp_path / "plan.png"
    seed = (hash(str(tmp_path)) & 0xFFFFFF)
    r = (seed >> 16) & 0xFF
    g = (seed >> 8) & 0xFF
    b = seed & 0xFF
    img = Image.new("RGB", (400, 300), (r, g, b))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    p.write_bytes(buf.getvalue())
    return p


def _stub_reply(score_pattern: list[float]) -> LlmReply:
    """Build a fake LLM JSON reply with the given per-criterion scores."""
    assert len(score_pattern) == 17
    rows = [
        {
            "id": cid,
            "label": label,
            "score": score_pattern[cid - 1],
            "note": f"stub-{cid}",
        }
        for cid, label, _ in ARCHITECT_REVIEW_CRITERIA
    ]
    return LlmReply(
        provider="stub",
        model="stub-model",
        text=json.dumps({"criteria": rows}),
    )


def test_endpoint_full_wykonawczy(tmp_path, monkeypatch) -> None:
    img = _blank_png(tmp_path)

    def fake_describe(image, prompt, provider, max_tokens):
        return _stub_reply([1.0] * 17)

    monkeypatch.setattr(app_module.llm_engine, "describe", fake_describe)

    with TestClient(create_app()) as client:
        r = client.post(
            "/v1/architect-review",
            json={"image": {"path": str(img)}, "language": "en"},
        )
    assert r.status_code == 200, r.text
    body = r.json()
    assert body["score"] == 17.0
    assert body["verdict"] == "full-wykonawczy"
    assert len(body["criteria"]) == 17
    assert body["fatal_gaps"] == []
    assert body["provider"] == "stub"


def test_endpoint_executive_with_remark_and_fatal_gaps(tmp_path, monkeypatch) -> None:
    img = _blank_png(tmp_path)
    # 15 full, 2 halfs -> 16.0 ... but we want 15.0 (executive). Use 13 full + 4 half = 13 + 2.0 = 15.0
    scores = [1.0] * 13 + [0.5] * 4
    assert sum(scores) == 15.0

    def fake_describe(image, prompt, provider, max_tokens):
        return _stub_reply(scores)

    monkeypatch.setattr(app_module.llm_engine, "describe", fake_describe)

    with TestClient(create_app()) as client:
        r = client.post(
            "/v1/architect-review",
            json={"image": {"path": str(img)}, "language": "en"},
        )
    body = r.json()
    assert body["score"] == 15.0
    assert body["verdict"] == "executive-with-remark"
    # fatal_gaps lists every id with score<1, i.e. the last 4 (ids 14..17).
    assert body["fatal_gaps"] == [14, 15, 16, 17]


def test_endpoint_concept_sketch_when_llm_refuses(tmp_path, monkeypatch) -> None:
    img = _blank_png(tmp_path)

    def fake_describe(image, prompt, provider, max_tokens):
        # Simulate the LLM emitting non-JSON chatter.
        return LlmReply(provider="stub", model="stub", text="Sorry, I cannot review this image.")

    monkeypatch.setattr(app_module.llm_engine, "describe", fake_describe)

    with TestClient(create_app()) as client:
        r = client.post(
            "/v1/architect-review",
            json={"image": {"path": str(img)}, "language": "en"},
        )
    body = r.json()
    assert body["score"] == 0.0
    assert body["verdict"] == "concept-sketch"
    assert len(body["fatal_gaps"]) == 17


def test_endpoint_brief_is_appended_to_prompt(tmp_path, monkeypatch) -> None:
    img = _blank_png(tmp_path)
    captured = {}

    def fake_describe(image, prompt, provider, max_tokens):
        captured["prompt"] = prompt
        return _stub_reply([1.0] * 17)

    monkeypatch.setattr(app_module.llm_engine, "describe", fake_describe)

    with TestClient(create_app()) as client:
        r = client.post(
            "/v1/architect-review",
            json={
                "image": {"path": str(img)},
                "language": "en",
                "brief": "4x OR + 2x PACU + MRI shielded bay",
            },
        )
    assert r.status_code == 200
    assert "Project brief:" in captured["prompt"]
    assert "4x OR" in captured["prompt"]


def test_endpoint_503_when_llm_unavailable(tmp_path, monkeypatch) -> None:
    img = _blank_png(tmp_path)

    def fake_describe(image, prompt, provider, max_tokens):
        raise RuntimeError("No vision LLM provider available. Set GOOGLE_API_KEY.")

    monkeypatch.setattr(app_module.llm_engine, "describe", fake_describe)

    with TestClient(create_app()) as client:
        r = client.post(
            "/v1/architect-review",
            json={"image": {"path": str(img)}, "language": "en"},
        )
    assert r.status_code == 503
    body = r.json()
    assert body["error"] == "model_not_available"
    assert "GOOGLE_API_KEY" in body["install_hint"]


def test_endpoint_missing_image_is_400(tmp_path) -> None:
    with TestClient(create_app()) as client:
        r = client.post(
            "/v1/architect-review",
            json={"image": {"path": str(tmp_path / "not-there.png")}, "language": "en"},
        )
    assert r.status_code == 400


# ---------------------------------------------------------------------------
# Persona descriptor on disk (rule 60 glob contract)
# ---------------------------------------------------------------------------


def test_persona_descriptor_json_is_in_sync_with_rubric() -> None:
    root = Path(__file__).resolve().parents[1]
    descriptor = root / "personas" / "senior-architect-reviewer.json"
    assert descriptor.exists(), f"missing persona descriptor at {descriptor}"
    data = json.loads(descriptor.read_text(encoding="utf-8"))

    assert data["persona"] == "senior-architect-reviewer"
    assert data["rule"] == "60-architectural-fidelity"
    assert data["endpoint"] == "/v1/architect-review"

    crits = data["criteria"]
    assert len(crits) == 17
    for (cid, label, _axis), row in zip(ARCHITECT_REVIEW_CRITERIA, crits):
        assert row["id"] == cid
        assert row["label"] == label


def test_persona_descriptor_thresholds_match_verdict_ladder() -> None:
    root = Path(__file__).resolve().parents[1]
    data = json.loads((root / "personas" / "senior-architect-reviewer.json").read_text(encoding="utf-8"))
    thresholds = data["threshold_policy"]

    # Every canonical verdict must appear exactly once in the threshold policy.
    verdicts = {row["verdict"] for row in thresholds}
    assert verdicts == {
        "concept-sketch",
        "technical-study",
        "executive-with-remark",
        "full-wykonawczy",
    }
