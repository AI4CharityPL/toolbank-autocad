"""run-architect-review.py - budget-aware live driver for `/v1/architect-review`.

Scope:
- Takes one or more images (PNG/JPG/PDF) of an AutoCAD floor plan.
- Calls the local Vision sidecar `/v1/architect-review` endpoint against
  each one (or the `describe-image` endpoint for quick smoke in
  `--smoke` mode).
- Enforces a cumulative USD budget. Aborts before the next call when the
  running total would exceed the cap. Default cap = $10, override with
  `--budget-usd` or `ACADMCP_GEMINI_BUDGET_USD`.
- Writes a timestamped JSON report to `artifacts/architect-review/`
  containing every scorecard, aggregate stats, cost estimate and the
  verdict tier per image.

Rationale:
  The user funded this work with ~300 PLN of Google AI credit
  (2026-04-24). At `gemini-3.1-pro-preview` pricing one architect-review
  costs roughly $0.025; at high-thinking + max tokens it can climb to
  ~$0.05. A hard budget gate means we can run dozens of tile + whole-plan
  reviews in Phase D12 without fear of blowing the cap.

Dependencies:
  - Local Vision sidecar running on http://127.0.0.1:<port> (the script
    reads `%LOCALAPPDATA%/acadmcp/vision/.port` by default, same contract
    as the C# backend's VisionSidecarClient).
  - `.env` file at the repo root supplies `GOOGLE_API_KEY`. The Vision
    sidecar itself must have the env var; this script only uses `.env`
    for convenience when launching the sidecar via `--start-sidecar`.
  - `pip install httpx tiktoken` is NOT required - we rely on the
    sidecar's own reply token counts for cost accounting.

Example:
  python scripts/run-architect-review.py ^
      --image artifacts/phaseD/overview.png ^
      --image artifacts/phaseD/tile_01.png ^
      --budget-usd 10 --brief-file docs/HOSPITAL-2026-BRIEF.md
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Iterable

import urllib.error
import urllib.request


# ---------------------------------------------------------------------------
# Pricing table (USD per 1M tokens). Update when Google re-prices.
# ---------------------------------------------------------------------------

# Ref: https://ai.google.dev/gemini-api/docs/gemini-3 (April 2026).
# Values are the published pay-as-you-go rates; preview models have separate
# monthly caps on Google AI Studio that we do NOT model here.
GEMINI_PRICING: dict[str, tuple[float, float]] = {
    "gemini-3.1-pro-preview":            (2.00, 12.00),
    "gemini-3.1-pro-preview-customtools": (2.00, 12.00),
    "gemini-3-flash-preview":            (0.50, 3.00),
    "gemini-3.1-flash-lite-preview":     (0.25, 1.50),
    "gemini-2.5-pro":                    (1.25, 10.00),
}


@dataclass
class Usage:
    """Cumulative spend tracker."""

    total_usd: float = 0.0
    total_input_tokens: int = 0
    total_output_tokens: int = 0
    calls: int = 0
    per_model: dict[str, float] = field(default_factory=dict)

    def charge(self, model: str, input_tokens: int, output_tokens: int) -> float:
        in_rate, out_rate = GEMINI_PRICING.get(model, (2.00, 12.00))
        cost = (input_tokens / 1_000_000.0) * in_rate + (output_tokens / 1_000_000.0) * out_rate
        self.total_usd += cost
        self.total_input_tokens += input_tokens
        self.total_output_tokens += output_tokens
        self.calls += 1
        self.per_model[model] = self.per_model.get(model, 0.0) + cost
        return cost


# ---------------------------------------------------------------------------
# Sidecar discovery
# ---------------------------------------------------------------------------


def _discover_sidecar() -> str:
    """Return `http://127.0.0.1:<port>` for the local Vision sidecar."""
    base_dir = Path(os.environ.get("LOCALAPPDATA", Path.home() / ".local"))
    port_file = base_dir / "acadmcp" / "vision" / ".port"
    if port_file.exists():
        port = port_file.read_text(encoding="utf-8").strip()
        if port.isdigit():
            return f"http://127.0.0.1:{port}"
    # Fallback: let the caller specify.
    raise RuntimeError(
        f"Could not find sidecar port file at {port_file}. "
        "Start the sidecar first with `python -m acadmcp_vision.app` "
        "or pass --base-url http://127.0.0.1:<port>."
    )


def _post_json(base_url: str, path: str, body: dict, timeout: float) -> dict:
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        url=base_url.rstrip("/") + path,
        data=data,
        headers={"Content-Type": "application/json", "Accept": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read()
            return json.loads(raw.decode("utf-8"))
    except urllib.error.HTTPError as ex:
        try:
            body = json.loads(ex.read().decode("utf-8"))
        except Exception:
            body = {"error": "http_error", "status": ex.code, "reason": ex.reason}
        return {"_http_status": ex.code, **body}


def _get_json(base_url: str, path: str, timeout: float) -> dict:
    req = urllib.request.Request(
        url=base_url.rstrip("/") + path,
        headers={"Accept": "application/json"},
        method="GET",
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


# ---------------------------------------------------------------------------
# Token accounting (approximation)
# ---------------------------------------------------------------------------

def _approx_tokens_text(s: str) -> int:
    """Rough token count - Gemini uses ~4 chars / token for English text."""
    if not s:
        return 0
    return max(1, len(s) // 4)


def _approx_tokens_image(path: Path) -> int:
    """Gemini 3.x images cost ~258 tokens at 1568px long side. We approximate
    from file size: tiny thumbnails count as one tile (258), larger rasters
    still normalise to 1568 px in the sidecar so stay at 258."""
    _ = path  # placeholder for potential future size-based scaling
    return 258


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def _load_dotenv(path: Path) -> None:
    if not path.exists():
        return
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            continue
        k, v = line.split("=", 1)
        k = k.strip()
        v = v.strip().strip('"').strip("'")
        if k and k not in os.environ:
            os.environ[k] = v


def _print_row(row: str, sep: str = "") -> None:
    sys.stdout.write(row + "\n")
    sys.stdout.flush()


def _iter_images(paths: Iterable[str]) -> Iterable[Path]:
    for p in paths:
        pp = Path(p)
        if not pp.exists():
            print(f"[warn] image not found, skipping: {pp}", file=sys.stderr)
            continue
        yield pp.resolve()


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--image", action="append", default=[], help="Image path. Can be repeated.")
    ap.add_argument("--base-url", default=None, help="Vision sidecar URL. Auto-discovered if omitted.")
    ap.add_argument("--language", choices=["en", "pl"], default="en")
    ap.add_argument("--provider", choices=["auto", "google", "anthropic", "openai"], default="google")
    ap.add_argument("--max-tokens", type=int, default=1600)
    ap.add_argument("--brief", default="", help="Short project brief appended to the prompt.")
    ap.add_argument("--brief-file", type=Path, default=None, help="Read brief from file (overrides --brief).")
    ap.add_argument(
        "--budget-usd", type=float,
        default=float(os.environ.get("ACADMCP_GEMINI_BUDGET_USD", "10.00")),
        help="Hard USD cap. Script aborts BEFORE the call that would exceed it.",
    )
    ap.add_argument(
        "--per-call-cap-usd", type=float, default=0.25,
        help="Safety cap on a single call. Abort before any call whose upper estimate exceeds this.",
    )
    ap.add_argument("--smoke", action="store_true",
        help="Quick round-trip: only validate /health + /version + 1 call.")
    ap.add_argument("--dry-run", action="store_true",
        help="Do not call the sidecar; just compute and print cost estimates.")
    ap.add_argument("--out-dir", type=Path, default=Path("artifacts/architect-review"))
    ap.add_argument("--env-file", type=Path, default=Path(".env"))
    args = ap.parse_args()

    _load_dotenv(args.env_file)

    brief = args.brief
    if args.brief_file and args.brief_file.exists():
        brief = args.brief_file.read_text(encoding="utf-8")
        # Keep brief under 4000 chars (matches schema).
        brief = brief[:4000]

    if not args.image:
        print("error: no --image paths given", file=sys.stderr)
        return 2

    base_url = args.base_url or _discover_sidecar()
    out_dir = args.out_dir.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    if not args.dry_run:
        health = _get_json(base_url, "/health", timeout=5.0)
        version = _get_json(base_url, "/version", timeout=5.0)
        _print_row(f"sidecar: {base_url}")
        _print_row(f"  version={version.get('version')} phase={version.get('phase')}")
        _print_row(f"  api_keys={version.get('api_keys')}")
        _print_row(f"  status={health.get('status')} uptime_sec={health.get('uptime_sec', 0):.0f}")
        if args.provider == "google" and not version.get("api_keys", {}).get("google"):
            print("error: sidecar reports no google api key loaded. "
                  "Set GOOGLE_API_KEY in the sidecar's env and restart it.",
                  file=sys.stderr)
            return 3

    usage = Usage()
    results: list[dict] = []
    model = os.environ.get("ACADMCP_GOOGLE_MODEL", "gemini-3.1-pro-preview")
    prompt_token_floor = _approx_tokens_text(brief) + 2000  # persona prompt ~2k tokens

    paths = list(_iter_images(args.image))
    if args.smoke:
        paths = paths[:1]

    _print_row(f"planning: {len(paths)} image(s), model={model}, budget cap=${args.budget_usd:.2f}")

    for i, img_path in enumerate(paths, start=1):
        # Upper-bound cost estimate: persona prompt + image + max_tokens out.
        est_in = prompt_token_floor + _approx_tokens_image(img_path)
        est_out = args.max_tokens
        in_rate, out_rate = GEMINI_PRICING.get(model, (2.0, 12.0))
        est_call_usd = (est_in / 1_000_000.0) * in_rate + (est_out / 1_000_000.0) * out_rate

        if est_call_usd > args.per_call_cap_usd:
            _print_row(f"  [{i}/{len(paths)}] ABORT: estimated ${est_call_usd:.4f} "
                       f"> --per-call-cap-usd ${args.per_call_cap_usd:.2f}")
            break
        if usage.total_usd + est_call_usd > args.budget_usd:
            _print_row(f"  [{i}/{len(paths)}] BUDGET STOP: running ${usage.total_usd:.4f} "
                       f"+ est ${est_call_usd:.4f} > --budget-usd ${args.budget_usd:.2f}")
            break

        _print_row(f"  [{i}/{len(paths)}] {img_path.name}  est ~${est_call_usd:.4f}")

        if args.dry_run:
            continue

        t0 = time.monotonic()
        body = _post_json(
            base_url, "/v1/architect-review",
            body={
                "image": {"path": str(img_path)},
                "language": args.language,
                "brief": brief,
                "provider": args.provider,
                "max_tokens": args.max_tokens,
            },
            timeout=180.0,
        )
        dt_ms = int((time.monotonic() - t0) * 1000)

        # Charge cumulative usage. We don't have exact token counts from the
        # sidecar response today (the Gemini SDK returns `usage_metadata`
        # which we currently do NOT surface through the HTTP layer), so we
        # charge the upper-bound estimate. This is intentionally pessimistic
        # so the budget gate keeps headroom; refine once the sidecar exposes
        # real counts.
        cost = usage.charge(model, est_in, est_out)
        results.append({
            "image": str(img_path),
            "latency_ms": dt_ms,
            "cost_usd_est": cost,
            "score": body.get("score"),
            "verdict": body.get("verdict"),
            "fatal_gaps": body.get("fatal_gaps"),
            "threshold_note": body.get("threshold_note"),
            "criteria": body.get("criteria"),
            "http_status": body.get("_http_status", 200),
            "error": body.get("error"),
        })
        score = body.get("score", "-")
        verdict = body.get("verdict", "-")
        _print_row(f"     -> score={score} verdict={verdict} "
                   f"gaps={len(body.get('fatal_gaps') or [])} "
                   f"latency={dt_ms}ms spend=${usage.total_usd:.4f}")

        if body.get("_http_status"):
            _print_row(f"     !! HTTP {body['_http_status']}: {body.get('error')} "
                       f"{body.get('install_hint','')}")
            break  # don't keep burning budget on a broken sidecar

    # ---- Persist report -------------------------------------------------
    stamp = time.strftime("%Y%m%dT%H%M%S")
    out = {
        "run": {
            "timestamp": stamp,
            "model": model,
            "language": args.language,
            "provider": args.provider,
            "budget_usd_cap": args.budget_usd,
            "per_call_cap_usd": args.per_call_cap_usd,
            "base_url": base_url,
            "brief_chars": len(brief),
        },
        "usage": asdict(usage),
        "images": results,
    }
    report_path = out_dir / f"architect-review-{stamp}.json"
    report_path.write_text(json.dumps(out, indent=2), encoding="utf-8")
    _print_row("")
    _print_row(f"wrote {report_path}")
    _print_row(f"cumulative: calls={usage.calls} input_tokens={usage.total_input_tokens} "
               f"output_tokens={usage.total_output_tokens} est_usd=${usage.total_usd:.4f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
