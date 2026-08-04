"""Runtime config + on-disk paths for the AcadMcp.Vision sidecar.

Per rule 29: bind 127.0.0.1 only, single sidecar per user session, idle-timeout
self-shutdown, model weights cached under %LOCALAPPDATA%\\AcadMcp.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


def _local_app_data_dir() -> Path:
    """Return %LOCALAPPDATA%\\AcadMcp on Windows, ~/.local/share/AcadMcp elsewhere."""
    base = os.environ.get("LOCALAPPDATA")
    root = Path(base) / "AcadMcp" if base else Path.home() / ".local" / "share" / "AcadMcp"
    root.mkdir(parents=True, exist_ok=True)
    return root


@dataclass(frozen=True)
class Settings:
    host: str = "127.0.0.1"
    http_port: int = 50062
    log_level: str = "info"
    idle_timeout_sec: int = 300
    cache_ttl_days: int = 7
    cache_dir: Path = _local_app_data_dir() / "vision-cache"
    model_dir: Path = _local_app_data_dir() / "vision-models"
    pid_file: Path = _local_app_data_dir() / "vision.pid"
    port_file: Path = _local_app_data_dir() / "vision.port"
    log_file: Path = _local_app_data_dir() / "logs" / "vision.log"

    def ensure_paths(self) -> None:
        for p in (self.cache_dir, self.model_dir, self.log_file.parent):
            p.mkdir(parents=True, exist_ok=True)


SETTINGS = Settings()
