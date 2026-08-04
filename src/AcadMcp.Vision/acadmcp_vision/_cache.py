"""Disk-backed JSON cache keyed by (sha256(content) + engine + version).

Per rule 32, trap #11: NEVER cache by file path - same content via different
paths must hit the cache. TTL applied via mtime.
"""

from __future__ import annotations

import contextlib
import hashlib
import json
import time
from pathlib import Path
from typing import Any

from .config import SETTINGS


def _key(content_sha: str, engine: str, version: str, extra: str = "") -> str:
    raw = f"{content_sha}|{engine}|{version}|{extra}"
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def _path_for(key: str) -> Path:
    SETTINGS.cache_dir.mkdir(parents=True, exist_ok=True)
    return SETTINGS.cache_dir / f"{key}.json"


def get(content_sha: str, engine: str, version: str, extra: str = "") -> dict[str, Any] | None:
    """Return cached payload or None on miss / expiry."""
    p = _path_for(_key(content_sha, engine, version, extra))
    if not p.exists():
        return None
    age_days = (time.time() - p.stat().st_mtime) / 86_400.0
    if age_days > SETTINGS.cache_ttl_days:
        with contextlib.suppress(OSError):
            p.unlink()
        return None
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def put(
    content_sha: str,
    engine: str,
    version: str,
    payload: dict[str, Any],
    extra: str = "",
) -> None:
    p = _path_for(_key(content_sha, engine, version, extra))
    # A cache write that fails is not an error worth surfacing: the caller gets the
    # freshly computed answer either way, just without the speed-up next time.
    with contextlib.suppress(OSError):
        p.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
