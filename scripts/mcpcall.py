"""Minimal MCP stdio client: speaks straight to AcadMcp.Backend.exe --category <cat>.

Deliberately not going through an MCP client library. This is test equipment: it has to show
exactly what the server sent, including the fields a client might normalise away.

MOVED INTO THE REPO 2026-08-11. It used to live in a session-scratch directory, and every
verify-*.py hard-coded that path - which meant the whole verification suite only ran on one
machine, in one session, for one user. The backend path is resolved from this file's own location
now, so `python scripts/verify-<category>.py` works from a fresh clone.
"""
import json
import os
import subprocess
import sys
import threading

# scripts/ -> repo root -> the Release build the launchers also use.
_REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.environ.get("ACADMCP_BACKEND_EXE") or os.path.join(
    _REPO, "src", "AcadMcp.Backend", "bin", "Release", "net8.0", "AcadMcp.Backend.exe")


class Session:
    def __init__(self, category, exe=None, timeout=90):
        exe = exe or EXE
        if not os.path.exists(exe):
            raise SystemExit(
                f"No backend at {exe}\n"
                "Build it first:  dotnet build src/AcadMcp.sln -c Release\n"
                "Or point ACADMCP_BACKEND_EXE at one.")
        self.p = subprocess.Popen(
            [exe, "--category", category],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, encoding="utf-8", bufsize=1,
        )
        self._id = 0
        self.timeout = timeout
        self.stderr_lines = []
        threading.Thread(target=self._drain_stderr, daemon=True).start()
        self._rpc("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "mcpcall", "version": "1"},
        })
        self._notify("notifications/initialized")

    def _drain_stderr(self):
        for line in self.p.stderr:
            self.stderr_lines.append(line.rstrip())

    def _send(self, obj):
        self.p.stdin.write(json.dumps(obj) + "\n")
        self.p.stdin.flush()

    def _notify(self, method, params=None):
        self._send({"jsonrpc": "2.0", "method": method, "params": params or {}})

    def _rpc(self, method, params=None):
        self._id += 1
        rid = self._id
        self._send({"jsonrpc": "2.0", "id": rid, "method": method, "params": params or {}})
        while True:
            line = self.p.stdout.readline()
            if not line:
                raise RuntimeError("server closed the pipe. stderr tail:\n" +
                                   "\n".join(self.stderr_lines[-15:]))
            line = line.strip()
            if not line:
                continue
            msg = json.loads(line)
            if msg.get("id") == rid:
                return msg

    def tools(self):
        r = self._rpc("tools/list")
        return [t["name"] for t in r["result"]["tools"]]

    def schema(self, name):
        r = self._rpc("tools/list")
        for t in r["result"]["tools"]:
            if t["name"] == name:
                return t.get("inputSchema")
        return None

    def call(self, name, args=None):
        """Return (ok, payload). payload is the parsed JSON result, or the error text."""
        r = self._rpc("tools/call", {"name": name, "arguments": args or {}})
        if "error" in r:
            return False, r["error"].get("message", json.dumps(r["error"]))
        res = r["result"]
        if res.get("isError"):
            return False, "".join(c.get("text", "") for c in res.get("content", []))
        text = "".join(c.get("text", "") for c in res.get("content", []))
        try:
            return True, json.loads(text)
        except Exception:
            return True, text

    def close(self):
        try:
            self.p.stdin.close()
            self.p.wait(timeout=5)
        except Exception:
            self.p.kill()


def j(x):
    return json.dumps(x, ensure_ascii=False, sort_keys=True)
