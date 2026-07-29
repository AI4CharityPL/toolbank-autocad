# MCP Nexus AutoCAD (v2025)

> A large-scale MCP (Model Context Protocol) ecosystem for AutoCAD: 1000+ tools spread across dozens of specialized micro-servers, registered in MCP Nexus, sharing a single C# .NET backend (NETLOAD plugin + COM fallback + LISP), a Python vision/OCR sidecar, and a standards-validation engine (PL/EU/ISO norms).

**Goal:** an AI agent that produces production-grade AutoCAD drawings without human intervention.

## Architecture in one sentence

Cursor / Claude only ever sees `acad-router` (meta-tools) plus MCP Nexus. Everything else is one of ~30 specialized MCP micro-servers, loaded on demand via `mcpd_find` → `mcpd_connect`. All of them connect to a **single** .NET plugin injected into AutoCAD via NETLOAD.

```
AI Agent ── static ──> acad-router (9 meta-tools)
         ── static ──> nexus-server + nexus-gateway (MCP Nexus discovery/dynamic modes)
                            │ mcpd_find / mcpd_connect (lazy)
                            ▼
        ┌───────────────────┴────────────────────────┐
        │  acad-geometry-2d, -geometry-3d, -modify,   │
        │  -annotations, -blocks, -layers, -files,    │
        │  -architecture, -mechanical, -civil,        │
        │  -electrical, -vision, -validators, ...     │
        └───────────────────┬────────────────────────┘
                             ▼
              AcadMcp.Backend.exe --category <name>
                             │ Named Pipe
                             ▼
              AcadMcp.Plugin (in-process AutoCAD, via NETLOAD)
```

## Key components

| Component            | Role                                                                 |
| --------------------- | --------------------------------------------------------------------- |
| `AcadMcp.Backend`     | A single stdio MCP binary, parameterized by `--category`             |
| `AcadMcp.Plugin`      | AutoCAD extension (`IExtensionApplication`), named pipe server        |
| `AcadMcp.ComBridge`   | COM/ActiveX fallback for AutoCAD LT and recovery scenarios            |
| `AcadMcp.Lisp`        | LISP/SCR scripts and loader                                           |
| `AcadMcp.Shared`      | DTOs, pipe contracts, `ToolRequest`/`ToolResponse`                     |
| `AcadMcp.SourceGen`   | Roslyn source generator enforcing `[McpTool]` with an `Intent` field  |
| `AcadMcp.Vision`      | Python sidecar (FastAPI + gRPC, Claude Vision/GPT-4V, PaddleOCR)       |
| `mcpbank-manifests/`  | JSON manifests for auto-registration in MCP Nexus                     |
| `bin-launchers/`      | Per-category `.cmd` launch scripts                                    |
| `.cursor/rules/`      | A growing rulebook enforcing architectural invariants                 |

## Verified

Before this was closed off as its own repository, the full system was built and smoke-tested end to end:

- **Build:** 0 errors, 0 warnings across the whole `.sln` (29 code categories / 30 manifests, consistency check clean).
- **Unit tests:** 138/138 passing.
- **Category sweep:** all 30 MCP category backends spawned over stdio and checked against their manifests — 29/30 exact tool-count matches. (`router` reports 9 tools in its manifest vs. 10 live; likely one dynamically-added tool not reflected in the static manifest — worth a follow-up, not a functional failure.)
- **Live AutoCAD integration:** with AutoCAD 2025 actually running, `acad_status` confirmed a live named-pipe connection to the in-process plugin (real document, real layer data), `list_layers` returned the real layer table of the open drawing, and `list_entities_in_window` correctly validated required arguments and then returned real (empty) results for the active drawing.

## Quickstart

```powershell
# 1. Detect the installed AutoCAD
pwsh scripts/detect-autocad.ps1

# 2. Build everything
dotnet build src/AcadMcp.sln -c Release

# 3. Generate launchers for every category
pwsh scripts/package.ps1

# 4. Register every category with your local MCP Nexus instance
pwsh scripts/register-mcps.ps1

# 5. Inject ONLY acad-router into Cursor (MCP Nexus is assumed already configured)
pwsh scripts/install-cursor-config.ps1

# 6. Install the plugin into AutoCAD -- see "Installing into AutoCAD 2025" below
pwsh scripts/install-plugin.ps1
```

## Installing into AutoCAD 2025

This is the part that actually gets the plugin running inside AutoCAD. The steps below are the ones verified live on AutoCAD 2025 (see [Verified](#verified)) — not just what the scripts claim to do.

### Prerequisites

- AutoCAD 2025 installed (`SeriesMin="R25"` in the plugin manifest — this targets 2025 and later; run `pwsh scripts/detect-autocad.ps1` to confirm your install is detected).
- .NET SDK 8.0 or later (`dotnet --version`).

### 1. Build the plugin

```powershell
dotnet build src/AcadMcp.sln -c Release
```

This produces `src/AcadMcp.Plugin/bin/Release/net8.0-windows/AcadMcp.Plugin.dll`.

### 2. Install as an auto-loading bundle (recommended)

```powershell
pwsh scripts/install-plugin.ps1
```

This copies the built plugin DLL (and its dependencies) into `%APPDATA%\Autodesk\ApplicationPlugins\AcadMcp.bundle\Contents\` and writes a `PackageContents.xml` with `LoadOnAutoCADStartup="True"`. AutoCAD reads this location on every launch — no manual `NETLOAD` needed from here on.

If a bundle is already installed, add `-Force` to overwrite it:

```powershell
pwsh scripts/install-plugin.ps1 -Force
```

Prefer to load it once per drawing instead of on every AutoCAD startup? Use `-Mode Acaddoc` instead — it patches `acaddoc.lsp` to `NETLOAD` the plugin whenever a drawing is opened.

### 3. Start (or restart) AutoCAD 2025

The bundle only takes effect on the next AutoCAD launch. If AutoCAD is already running, restart it once after installing.

### 4. Verify it's actually loaded

Inside AutoCAD, type at the command line:

```
ACADMCP_PING
```
Expected: `AcadMcp pong`.

```
ACADMCP_STATUS
```
Expected: pipe state, uptime, and the count of registered tools.

From outside AutoCAD, in a separate terminal, you can verify the same thing without touching the AutoCAD UI at all — this is exactly how the live checks in [Verified](#verified) were run:

```powershell
src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe --category router --ping-plugin
```

Or drive it through the actual MCP protocol (`acad_status` is one of `acad-router`'s meta-tools) — send `initialize` then `tools/call acad_status` over stdio to the same executable with `--category router`. A real response looks like:

```json
{"alive": true, "acadProductName": "AutoCAD", "acadVersion": "25.0.0.0", "documentName": "Drawing1.dwg", ...}
```

### Uninstalling

```powershell
pwsh scripts/install-plugin.ps1 -Uninstall
```

Removes the bundle directory and cleans up any `acaddoc.lsp` snippet it added.

## Status

**Phases 0-6** are delivered per this repository's roadmap (backend, plugin, categories, validators, Phase 6 domains, vision scaffold). Version details and full history: [CHANGELOG.md](CHANGELOG.md).

**Active development track: Phases 7-8** — the design iteration loop (iterate, checkpoints), livestream/events, validator and domain extensions, per-discipline vision/YOLO, E2E and runbook work. **Start here:** [docs/PHASE-7-8-ROADMAP.md](docs/PHASE-7-8-ROADMAP.md).

## Development / where to start (Cursor)

1. Open [docs/PHASE-7-8-ROADMAP.md](docs/PHASE-7-8-ROADMAP.md) — the single source of truth for Phase 7-8 scope.
2. The **always-apply** rule [`.cursor/rules/54-phase-7-8-current-work.mdc`](.cursor/rules/54-phase-7-8-current-work.mdc) keeps the agent from treating the repository as an "empty Phase 0 bootstrap" or prematurely assuming the project is functionally closed.

## Conventions

- **Code:** English. **Domain comments (CAD/standards):** Polish is fine.
- **MCP tool names:** `snake_case`, max 5 words, `<verb>_<entity>_<modifier?>` format.
- **Every tool MUST carry `[McpTool]` with an `Intent` field (5+ examples, PL and EN)** — enforced by the source generator.
- **Every category** has its own manifest at `mcpbank-manifests/acad-<category>.json`.
- **Cross-category references are forbidden** — shared helpers live in `Categories/_Shared/`.

## Authors

- **Krzysztof Augiewicz** — Lead Architect & Creator
- **Kacper Pisarczyk** — Core Contributor
- **Sebastian Pawłowski** — Advisory & QA Support (testing, hardware/software provisioning)

Full details in [AUTHORS.md](AUTHORS.md).

## License

Proprietary — work in progress. All rights reserved.
