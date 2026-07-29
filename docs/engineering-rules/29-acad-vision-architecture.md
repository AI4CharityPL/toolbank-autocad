# AcadMcp.Vision sidecar architecture (Phase 4)

AcadMcp.Vision sidecar architecture - read BEFORE touching Python sidecar, Categories/Vision, or any acad-vision MCP tool. Defines the HTTP contract, lifecycle, transport boundary and crash isolation.

The Vision pipeline is a **separate Python process** that the C# Backend talks to over **localhost HTTP**, NOT a DLL loaded into AutoCAD and NOT something the MCP client speaks to directly.

```
┌─────────────┐  stdio  ┌────────────────┐ named pipe ┌──────────────┐
│ MCP client  ├────────►│ acad-vision    ├───────────►│ AutoCAD      │
│ (AI client) │         │ Backend        │ (optional) │ + .Plugin    │
└─────────────┘         │  (.NET 8)      │            └──────────────┘
                        │                │
                        │   HTTP/1.1     │
                        ▼                │
                ┌──────────────┐         │
                │ AcadMcp      │         │
                │ Vision       │         │
                │ (Python 3.11)│         │
                └──────────────┘         │
```

## 1. Boundary: HTTP, not gRPC, not subprocess pipes

We picked **HTTP/JSON** over gRPC for v1 because:

- Easy curl-debug from PowerShell on Windows (no `protoc` install for ops).
- FastAPI gives us an OpenAPI schema for free, which the C# client can codegen later.
- Latency budget for vision tools is 200 ms - 30 s; gRPC's framing wins are noise at that scale.
- We never share the sidecar across machines; localhost loopback HTTP is fine.

**Do NOT add gRPC, RabbitMQ, ZeroMQ, Unix sockets, or shared memory** without an architectural-invariant amendment. If we hit a real perf wall, we revisit per rule 02 (no breaking changes without ADR).

## 2. The sidecar binds to **127.0.0.1 only**

Hard rule:

```python
uvicorn.run(app, host="127.0.0.1", port=...)   # ✓
uvicorn.run(app, host="0.0.0.0",  port=...)    # ✗ exposes vision LLM keys to the LAN
```

The Python sidecar caches API keys (Anthropic / OpenAI Vision) and OCR results that may contain confidential drawing text. **Never** bind to `0.0.0.0` or accept connections from non-loopback IPs.

## 3. Lifecycle: spawned by acad-vision Backend, killed when Backend exits

The `bin-launchers/acad-vision.cmd` does:

1. `scripts/start-vision.ps1 -EnsureRunning` — idempotent: starts the sidecar only if its `vision.pid` file is missing or the named PID isn't alive.
2. Wait until `GET /health` returns 200 (max 20 s).
3. `dotnet AcadMcp.Backend --category vision`.
4. On Backend exit, the launcher script does **NOT** kill the sidecar — the next acad-vision Backend should reuse it. The sidecar self-terminates after `--idle-timeout-sec` of no requests (default 300 s) so we never leak.

There MUST be at most one vision sidecar per Windows user session. Use `vision.pid` + `vision.port` files under `%LOCALAPPDATA%\AcadMcp\` for discovery (matches plugin lifecycle in rule 16).

## 4. Versioned URL prefix

All real endpoints live under `/v1/`. Liveness / introspection live at root:

| Path | Method | Purpose |
| ---- | ------ | ------- |
| `/health` | GET | liveness; returns `{status:"ok"}` even when ML deps are missing |
| `/version` | GET | version, loaded models, optional-deps availability flags |
| `/v1/ocr` | POST | OCR engine over an image / PDF page |
| `/v1/detect-symbols` | POST | YOLO custom CAD-symbol detector |
| `/v1/extract-titleblock` | POST | parse title-block fields from raster |
| `/v1/extract-dimensions` | POST | OCR + heuristic for dim text + arrows |
| `/v1/classify-drawing` | POST | discipline / sheet-type classifier |
| `/v1/describe-image` | POST | vision LLM description (Anthropic / OpenAI) |
| `/v1/segment` | POST | SAM2 segmentation masks |
| `/v1/cross-validate-with-dxf` | POST | OCR string set vs DXF text strings diff |

All POST bodies are JSON. Image data is sent as **either** an absolute file path on the same machine, **or** a `base64` data URL. Never multipart.

## 5. Optional-deps degradation: 503 with `installHint`

The sidecar must `pip install` cleanly with only the core deps in `pyproject.toml` (FastAPI, uvicorn, pillow, pydantic, structlog). Heavy deps (Torch, Paddle, Ultralytics, Anthropic, OpenAI) are optional.

If an endpoint is hit but the corresponding ML library is not importable, return:

```json
HTTP 503
{
  "error": "model_not_available",
  "engine": "paddleocr",
  "installHint": "pip install -e .[ml]"
}
```

The C# Backend MUST surface this as a friendly MCP error suggesting the install command — never as a 500.

## 6. C# side: `IVisionSidecarClient`, NOT `HttpClient` raw

`Categories/Vision/VisionSidecarClient.cs` wraps `HttpClient` and exposes typed `PostAsync<TRequest, TResponse>`. Tools must depend on the interface so we can stub it in tests (analogue of `IPluginGateway` per rule 18).

Same error mapping discipline as plugin tools (rule 12): map 503/4xx into `VisionUnavailableException` / `VisionToolException`. Never leak HTTP stack traces to the MCP client.

## 7. No AutoCAD plugin dependency for v1 vision tools

acad-vision tools work over raster files on disk + DXF text export (a string set). They do NOT require AutoCAD to be running. This is intentional: the AI agent should be able to OCR a customer's PDF/PNG drawing, then propose how to redraw it via acad-geometry-2d in a fresh AutoCAD session.

If a tool needs the active drawing (e.g. screenshot the model space + OCR it), it must declare `RequiresPlugin = true` AND have its own non-Vision pre-step that calls `acad.files.export_file` first; the vision tool itself only takes a file path.

## 8. Caching keys

Cache OCR / YOLO / vision-LLM responses keyed by **content hash + engine name + engine version**. The cache lives under `%LOCALAPPDATA%\AcadMcp\vision-cache\`. TTL: 7 days. Configurable via `--cache-dir` and `--cache-ttl-days`. **Never** key by file path — paths change, content does not.

## 9. Anthropic / OpenAI keys

Read from environment variables (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`). If missing, `/v1/describe-image` returns 503 with `installHint: "set ANTHROPIC_API_KEY=..."`. Do NOT load keys from disk inside the sidecar — that's the operator's job through systemd / Windows session env.
