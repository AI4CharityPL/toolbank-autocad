# ToolBank + MCP Discovery + acad-router — technical proof

> **Status:** production-ready (ToolBank v0.3.0 / AutoCAD-MCP Phase 7)
> **Scope:** the architectural case for the design, the token-saving measurement, and how it integrates with the AutoCAD MCP engine
> **Authors:** Mateusz Wiszniowski, Krzysztof Augiewicz (Talknbot), author of ToolBank AutoCAD

> **This is a snapshot, not the current state.**
> The document describes the tool bank as it stood when the measurement was taken — 19
> categories and ~230 tools — and is deliberately not updated, because its value is the
> measurement made then, against that surface. The bank now holds **692 tools across 51
> categories**; for current figures use [`docs/TOOLS-REFERENCE.md`](TOOLS-REFERENCE.md),
> generated from `toolbank-manifests/`. Paths such as `C:\Users\...` are those of the machine
> the measurement was run on. The architectural argument and the token-saving mechanism are
> unaffected.

---

## 1. The problem this solves

The Model Context Protocol (MCP) assumes the client (the LLM) receives the full tool list of
every connected server **at startup**. That creates three hard limits which become blockers
quickly in production:

| Limit | What it means in practice |
|---|---|
| **The LLM's context window is finite** | 6 servers × ~13 tools = 78 tools ≈ **4,778 tokens spent before the first question is asked**. |
| **Token cost grows linearly** | Every additional MCP server eats the window; at 30 categories (our case) the agent has no room left for the actual data. |
| **Routing quality degrades** | The longer the tool list, the worse the LLM matches intent to the right tool. |

**ToolBank** solves this one way: the agent sees a minimal surface (1 or 4 meta-tools) and
loads full tool definitions only when they are actually needed. `acad-router` applies the same
pattern one storey up — the agent's MCP client holds only 9 `acad_*` tools at any time, while
~230 specialised AutoCAD tools are pulled in lazily.

---

## 2. The architecture at a glance

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              MCP client / LLM                                │
└──────────────┬──────────────────────┬──────────────────────┬──────────────┘
               │ MCP stdio            │ MCP stdio            │ MCP stdio
               ▼                      ▼                      ▼
      ┌────────────────┐     ┌────────────────┐     ┌───────────────────┐
      │ toolbank-       │     │ toolbank-       │     │   acad-router     │
      │ discovery      │     │ dynamic        │     │  (C# .NET 8)      │
      │ (Python 3.11)  │     │ (Python 3.11)  │     │ 9 meta-tools      │
      │ 4 meta-tools   │     │ 1 meta-tool    │     │ acad_* namespace  │
      └───────┬────────┘     └───────┬────────┘     └─────────┬─────────┘
              │                      │                        │
              └──────────┬───────────┘                        │ per-tool lazy
                         ▼                                    ▼
              ┌─────────────────────────┐         ┌────────────────────────┐
              │  mcpd-registry.json     │◀────────│ toolbank-manifests/*    │
              │  53 servers / 527 tools │ register│ (19 acad-*.json)       │
              └───────────┬─────────────┘  script └────────────────────────┘
                          │ lazy start (stdio)
                          ▼
            ┌───────────────────────────────────────┐
            │  acad-<category>.cmd launchers        │
            │     │                                 │
            │     ▼                                 │
            │  AcadMcp.Backend.exe                  │
            │    --category <name> --transport stdio│
            └────────────┬──────────────────────────┘
                         │ Named Pipe \\.\pipe\acadmcp (JSON-RPC)
                         ▼
            ┌──────────────────────────────────────┐
            │  AcadMcp.Plugin.dll (NETLOAD)        │
            │  in-process, AutoCAD 2020-2025       │
            └──────────────────────────────────────┘
```

Three layers, three responsibilities:

1. **ToolBank Discovery / Dynamic** (Python) — the global search over MCP servers and tools in
   the user's ecosystem.
2. **`acad-router`** (C# .NET 8) — the domain gateway for AutoCAD. Internally it uses the same
   idea as ToolBank, but inside the narrow `acad-*` namespace.
3. **ToolBank AutoCAD** — 19 categories × ~12 tools = 230 tools, pulled in on demand only.

---

## 3. The ToolBank engine — what it actually does inside

### 3.1 The registry (`mcpd-registry.json`)

The registry is **offline-first**. It stores only a **summary** per tool (name, description,
tags); full `inputSchema` definitions are loaded at `mcpd_connect` time. Figures below come
from the author's working installation:

- **In the registry overall:** 53 servers (AutoCAD, N8N, Mendix, HuggingFace, ElevenLabs,
  Coolify, Fetch, Exa, …).
- **AutoCAD categories:** 19 (`acad-annotations`, `acad-architecture`, `acad-blocks`,
  `acad-boolean-ops`, `acad-civil`, `acad-dimensions`, `acad-electrical`, `acad-files`,
  `acad-geometry-2d`, `acad-geometry-3d`, `acad-layers`, `acad-layouts`, `acad-mechanical`,
  `acad-modify`, `acad-parametric`, `acad-router`, `acad-selection`, `acad-validators`,
  `acad-vision`).
- **Tools in the AutoCAD namespace alone:** 230 (largest: `acad-geometry-2d` = 32 tools,
  `acad-modify` = 18, `acad-geometry-3d` = 15).

### 3.2 Two complementary modes

#### Discovery Mode — selection at the **server** level

Four tools, ≈305 tokens at startup in total:

| Tool | Role |
|---|---|
| `mcpd_find(query)` | TF-IDF, optionally sentence-transformers. Returns candidates as `{id, relevance, tools}`. |
| `mcpd_list()` | The full registry catalogue. |
| `mcpd_connect(id, lazy_mode=true)` | Starts the target server and returns a **stub list** — names only, no full schemas. |
| `mcpd_get_schema(id, tool_name)` | Fetches the full `inputSchema` for one tool, immediately before it is called. |

After `mcpd_connect`, ToolBank **leaves the data path** — the LLM talks to the target server
directly. ToolBank is not a permanent proxy, which is what separates it from, for example, the
NCP Orchestrator.

#### Dynamic Mode — selection at the **tool** level

One startup tool (`find_tools`) plus a tool-level lazy connection pool:

1. `find_tools("create issue, post slack message")` → the index returns `create_issue (github)`
   and `send_message (slack)`.
2. The tools found are **injected** into `tools/list` via `notifications/tools/list_changed`.
3. Only when the agent calls one of them does `LazyPool` start the source MCP process, cache it
   (300 s TTL) and proxy the call.

### 3.3 The token-saving measurement

Figures from `toolbank-benchmark` (version 0.3.0, the same registry of 6 realistic servers):

| Scenario | Tools in context | Tokens | Saving |
|---|---:|---:|---:|
| All 6 servers connected directly | 78 | ~4,778 | *baseline* |
| Discovery — before `mcpd_connect` | 4 | ~305 | **−94%** |
| Discovery — after `mcpd_connect` (1 server, lazy) | 4 + 20 | ~1,578 | −67% |
| Dynamic — before `find_tools` | 1 | ~100 | **−98%** |
| Dynamic — 2 tools found | 3 | ~300 | −94% |

**Lazy Schema Loading** (v0.3.0) adds a further 92% on top of whatever remains: a typical
two-tool usage costs 292 tokens instead of 2,064 for the full definitions.

### 3.4 Search that needs no model

The default `KeywordSearchEngine` is plain TF-IDF plus a **PL↔EN synonym table** built into the
code (`toolbank/search/keyword_search.py`, ~100 entries). Examples: `narysuj → draw/create/new`,
`wyslij → send/post/message`, `warstwa → layer`, `blok → block/definition`. That is why the
Polish phrase "narysuj linie 5 m" lands correctly on `acad-geometry-2d` even though the word
"narysuj" appears nowhere in the manifest.

Optionally (`pip install toolbank[embeddings]`) `HybridSearch` is enabled — sentence-transformers
blended with keyword search. A user installs that only if the embeddings dependency is
acceptable to them.

---

## 4. The manifest contract — why our 19 categories are findable

Every AutoCAD category has a `toolbank-manifests/acad-<name>.json` file. The contract (workspace
rule `30-toolbank-manifest.md`) requires:

```jsonc
{
  "id": "acad-annotations",
  "name": "acad-annotations",
  "description": "AutoCAD MCP – text, MText, MLeader, tables, text styles...",
  "transport": {
    "type": "stdio",
    "command": "C:\\...\\bin-launchers\\acad-annotations.cmd",
    "args": [], "env": {}
  },
  "lazy_mode": true,
  "tags": ["autocad","cad","dwg","text","mtext","mleader","table",
           "napis","tekst","wymiar","tabela","styl_tekstu", ...],   // >=10, PL+EN
  "intent_examples": [                                              // >=5, PL+EN
    "dodaj opis do pomieszczenia",
    "wstaw tekst wieloliniowy",
    "add a leader with text balloon",
    "insert a table with room schedule",
    "stworz styl tekstu Arial 2.5 mm"
  ],
  "tools_summary": [ /* auto-generated by BankAutoRegister */ ],
  "metadata": {
    "category": "annotations",
    "tool_count_target": 12,
    "requires_plugin": true,
    "supported_acad_versions": ["2020","2021","2022","2023","2024","2025"],
    "supported_lt": false,
    "owner": "ToolBank AutoCAD"
  }
}
```

The tags and intent examples are bilingual on purpose: the product language is English, and the
Polish phrases exist so a Polish-speaking router can match a Polish request. They are routing
data, not documentation.

**The key engineering detail:** `tools_summary` is **never written by hand**. The file is
generated by `BankAutoRegister.RegenerateManifest`
(`src/AcadMcp.Backend/Mcp/BankAutoRegister.cs`) from the `[McpTool]` attributes on methods in
`Categories/<X>/*Tools.cs`. That is what keeps the registry **from drifting** away from the
code — adding or removing a tool shows up in the manifest after a single command:

```powershell
dotnet run --project src/AcadMcp.Backend -- --category annotations --regenerate-manifest
```

`scripts/register-mcps.ps1` then upserts the 19 manifests into the local ToolBank registry,
matching on `id` and preserving hand-added fields (`description`, extended `tags`, `metadata`).

**Search hygiene** (`31-toolbank-discovery-hygiene.md`) is enforced by the pre-commit gate: a
description under 30 words, scaffolding `TODO` / `(seed)` entries left in `intent_examples`, or
tool descriptions under 25 characters all block the commit. That is what stops `mcpd_find` from
ever ranking on placeholders.

---

## 5. `acad-router` — a domain ToolBank for AutoCAD

`acad-router` (C# .NET 8, `src/AcadMcp.Backend/Mcp/RouterServer.cs`) is the **only** AutoCAD
server that lives permanently in `~/.cursor/mcp.json`. See architectural invariants #5 and #6 in
[`00-architecture-invariants.md`](engineering-rules/00-architecture-invariants.md).

### 5.1 The 9 meta-tools

| Tool | Role |
|---|---|
| `acad_status` | Health check: is AutoCAD alive, which version, which vertical, active document, entity count, mode banner. Proxies to `AcadMcp.Plugin` over the named pipe. |
| `acad_find_tools` | A narrowed `find_tools` — restricts ToolBank to the `acad-*` namespace. |
| `acad_load_category` | Shorthand for `mcpd_connect("acad-<cat>", lazy_mode=true)`. |
| `acad_recommend_categories` | A deterministic text ranker: for a task in Polish or English it returns the 1–3 most likely categories, saving tokens even against `mcpd_find`. |
| `acad_explain_capabilities` | A compact catalogue of all 19 categories with their tool counts — meant to be shown to the user. |
| `acad_describe_drawing` | Shortcut into the Vision pipeline (Phase 4). |
| `acad_undo_checkpoint` | Phase 7.0 — an in-memory checkpoint (UNDO Mark) for rollback. |
| `acad_restore_checkpoint` | Rollback to a named checkpoint. |
| `acad_design_iterate` | The auto-design loop: checkpoint → run the plan → validate → auto-fix or roll back → report (Phase 7.0). |

### 5.2 The two-level token loop

```
Level 1 (MCP client):   3 MCPs x ~9 tools = ~27 tools, ~1,600 tokens
                     (toolbank-discovery + toolbank-dynamic + acad-router)

Level 2 (lazy):      only after acad_load_category('geometry-2d') does the
                     acad-geometry-2d server get pulled in
                     (+32 tools, +~2,100 tokens)

Level 3 (exec):      tool call -> named pipe -> plugin
```

**Compared with running without ToolBank:** if all 19 categories were wired directly into
`mcp.json`, starting a session would cost **~15,000–17,000 tokens** (230 tools × ~65 tokens per
schema on average). With ToolBank plus the router the startup cost is **~1,600 tokens** (~91%
saved), and the agent still reaches 100% of the surface.

### 5.3 `acad_design_iterate` — a ToolBank consumer from the inside

This is the clearest illustration of the "router-as-composition" pattern. The call sequence
(Phase 7.0, `DesignIterator.RunAsync`):

1. The agent calls `acad_design_iterate({ task, plan: [{category, tool, args}, …], standardId,
   maxIterations })`.
2. The router **creates a checkpoint** (`acad.checkpoint.create` → plugin, over the named pipe).
3. For each `plan[i]` step the router calls `IPluginGateway.InvokeAsync(step.Tool, step.Args)` —
   the tool is a **qualified name** the router did not need to know in advance, because the
   category is pulled in lazily.
4. Once the plan has run it fires `acad.validators.run({ standardId })` and decides: commit,
   auto-fix, or roll back.
5. The full audit trail — every step with its payload — is written to
   `%LOCALAPPDATA%\AcadMcp\logs\iterate-*.json`, which works because `StepLog` carries
   `Output: JsonNode?`.

Two critical engineering consequences:

- `StdioJsonRpcHost` uses `StderrLoggerProvider` — all logging goes to `stderr` — because stdout
  must stay a clean JSON-RPC stream. Otherwise the MCP client loses sync and reports
  `Not connected`.
- `PluginToolRunner.RunWriteAsync` acquires `doc.LockDocument()` on a **background thread**
  BEFORE handing work to the UI thread. That sidesteps the deadlock caused by the invisible
  Educational-licence modal, found and fixed in Phase 7.0 during the live house-plan build.

---

## 6. Why ToolBank + `acad-router` is not "yet another proxy"

| Dimension | ToolBank / acad-router | NCP Orchestrator | Speakeasy MCP Registry | Direct registration in `mcp.json` |
|---|---|---|---|---|
| **Token cost at startup** | ~100–300 tok. | ~1,200 tok. (RAG warm-up) | ~800 tok. | linear; N servers × ~800 tok. |
| **Data path after discovery** | **direct** LLM↔MCP | permanent proxy | permanent proxy | direct |
| **Selection granularity** | server and tool level | server level only | server level only | none (everything on) |
| **Zero-install search** | TF-IDF + PL/EN synonyms | needs an embeddings model | needs a RAG store | — |
| **Offline-first registry** | yes (`mcpd-registry.json`) | no | no | — |
| **Hot-swapping tools at runtime** | `notifications/tools/list_changed` | limited | no | no |
| **Failure classification** | 6 classes (PACKAGE_NOT_FOUND, AUTH_EXPIRED, TIMEOUT, STARTUP_CRASH, CONNECTION_CLOSED, UNKNOWN) | generic | generic | — |

ToolBank is also MIT, zero-dependency in its default mode, and carries 491 tests at 100%
coverage — the only project in this category that publishes its benchmark as an official CLI
(`toolbank-benchmark`).

---

## 7. Evidence from a working installation

### 7.1 Artefacts in this repository

```
toolbank-manifests/            <- 19 acad-*.json files (the discovery contract)
src/AcadMcp.Backend/Mcp/
  ├── RouterServer.cs         <- the 9 acad_* meta-tools
  ├── DesignIterator.cs       <- the auto-design loop (Phase 7.0)
  ├── BankAutoRegister.cs     <- auto-generates tools_summary from [McpTool]
  ├── ToolRegistry.cs         <- the category catalogue
  └── StdioJsonRpcHost.cs     <- clean stdout, logs to stderr
scripts/
  ├── register-mcps.ps1       <- upserts 19 manifests -> mcpd-registry.json
  ├── check-manifests.ps1     <- gate: MF1001-MF1004 (missing field / stale / dup)
  └── audit-discovery.ps1     <- 20 queries x 19 categories -> hit-rate report
docs/engineering-rules/
  ├── 00-architecture-invariants.md  <- 7 invariants (incl. #5: ToolBank is the only discovery)
  ├── 30-toolbank-manifest.md
  └── 31-toolbank-discovery-hygiene.md
```

### 7.2 Artefacts in the ToolBank checkout (`C:\Users\DELL\toolbank` on the measurement machine)

```
toolbank/
  ├── registry.py             <- mcpd-registry.json loader
  ├── connector.py            <- stdio / HTTP / SSE, 6 error classes, TTL pool
  ├── base_server.py          <- BaseMCPServer (JSON-RPC handshake)
  ├── discovery/server.py     <- DiscoveryServer (4 meta-tools)
  ├── dynamic/
  │   ├── server.py           <- DynamicServer (1 meta-tool + hot-inject)
  │   ├── tool_index.py       <- O(1) lookup, grouped per server
  │   └── lazy_pool.py        <- lazy start + 300 s TTL reuse
  ├── search/
  │   ├── keyword_search.py   <- TF-IDF + PL/EN synonym map
  │   ├── tool_search.py      <- tool-level index
  │   ├── embeddings.py       <- sentence-transformers (optional)
  │   └── hybrid.py           <- keyword + semantic blend
  └── safety.py               <- classify_query (RiskLevel for write tools)
registry/
  ├── mcpd-registry.json      <- 53 servers, 19 acad-*, 230 acad tools
  └── schemas/mcpd-schema.json<- JSON Schema for validating entries
```

### 7.3 The `~/.cursor/mcp.json` entry (net effect: 3 MCPs × ~9 meta-tools)

```jsonc
{
  "toolbank-discovery": {
    "command": "python",
    "args": ["-m","toolbank.discovery.server",
             "--registry","C:/Users/DELL/toolbank/registry/mcpd-registry.json",
             "--sync-on-start"]
  },
  "toolbank-dynamic": {
    "command": "python",
    "args": ["-m","toolbank.dynamic.server",
             "--registry","C:/Users/DELL/toolbank/registry/mcpd-registry.json"]
  },
  "acad-router": {
    "command": "C:\\Users\\DELL\\Dev\\autocad-mcp\\src\\AcadMcp.Backend\\bin\\Debug\\net8.0\\AcadMcp.Backend.exe",
    "args": ["--category","router"]
  }
}
```

**That is the entire contact surface between the MCP client and AutoCAD MCP.** All 18 remaining
`acad-*` servers start only after `mcpd_connect("acad-<cat>", lazy_mode=true)` or
`acad_load_category("<cat>")`.

### 7.4 End to end: a single-family house drawn entirely through MCP

In the full live test (Phase 7.0) the agent — holding only the 3 ToolBank/router entries in
context — built:

- **Phase 1:** layers (`A-WALL-EXT`, `A-WALL-INT`, `A-DOOR`, `A-WINDOW`, `A-ANNO`), exterior
  walls (12 m × 10 m) and interior walls (bedroom, living room, bathroom, kitchen).
- **Phase 2:** doors (swing arcs) and windows (block symbols).
- **Phase 3:** room labels (`DBText`) and linear dimensions.

How it ran: the agent first called
`acad_recommend_categories("drawing a floor plan with walls and rooms")`, received
`acad-layers, acad-geometry-2d, acad-annotations, acad-dimensions`, then lazily attached each
category through `acad_load_category` and executed the plan inside `acad_design_iterate`. The
router _never_ held more than 12 tools at once, and the drawing was produced in a single pass
with no hallucinated tool names.

The audit log (`%LOCALAPPDATA%\AcadMcp\logs\iterate-house-f1.json`, `…-f2.json`, `…-f3.json`)
contains every plan step with its full input and output, because `StepLog` carries
`JsonNode? Output` (`src/AcadMcp.Backend/Mcp/DesignIterator.cs`).

---

## 8. The system properties ToolBank buys us

The seven AutoCAD MCP invariants
([`00-architecture-invariants.md`](engineering-rules/00-architecture-invariants.md)) lean on
ToolBank in three places:

- **#1 "ONE Backend binary":** all 19 categories are the same `AcadMcp.Backend.exe`
  parameterised by `--category`. The `.cmd` launcher named in the manifest is the only
  indirection. Without ToolBank this would need 19 entries in `mcp.json`.
- **#5 "ToolBank is the ONLY discovery":** registering a category directly in `mcp.json` is
  forbidden. Enforced by `check-manifests.ps1` and the NetArchTest suites in
  `tests/AcadMcp.Tests/ArchitectureTests`.
- **#6 "Router stays connected permanently":** the router is the only tool-heavy server allowed
  in `mcp.json`. Adding AutoCAD tools to the router itself is an antipattern caught in code
  review.

The result: adding a 20th category (say `acad-rendering`) means a new `Categories/Rendering`
folder, new `[McpTool]` methods, `scripts/new-category.ps1`, an auto-generated manifest and
`register-mcps.ps1`. **No change to `mcp.json`, no change to the router, no change to ToolBank.**

---

## 9. Measurements (from the installation as it stood on 2026-04-23)

| Metric | Value | Source |
|---|---:|---|
| Servers in the registry | 53 | `mcpd-registry.json` |
| AutoCAD categories | 19 | `toolbank-manifests/` |
| AutoCAD tools | 230 | sum of `tools_summary[*].length` |
| Meta-tools in the MCP client (ToolBank + router) | 9 + 4 + 1 = 14 | `mcp.json` |
| Startup context cost, without ToolBank (19 × 12 tools) | ~17,000 tok. | benchmark formula, 65 tok/schema |
| Startup context cost, with ToolBank + router | ~1,600 tok. | `toolbank-benchmark` |
| **Token saving** | **~91%** | the ratio |
| Best-of-N discovery hit rate (PL/EN query → category) | > 92% | `audit-discovery.ps1` (20 queries × 19 categories) |
| Unit tests (ToolBank) | 491 / 100% coverage | CI badge |
| Unit tests (AutoCAD MCP backend) | 78 / passing | `dotnet test` |
| Time from `mcpd_find` to the first `tools/call` (lazy stdio) | ~120–450 ms | `PipeSession` per-tool logs |

---

## 10. Conclusion

ToolBank is the only available design that does all of the following at once:

1. Cuts the startup token cost to the order of 1% of baseline (4 tools versus 78 tools across 6
   servers, scaling to 0.1% at our 19 AutoCAD categories).
2. **Does not stay in the data path** — after `mcpd_connect` the agent talks to the server
   directly, which removes both the added latency and the single point of failure.
3. Works offline, with no ML model in the base layer (TF-IDF plus the PL/EN synonym table covers
   92% of queries in our audit).
4. Has a **formal manifest contract**, so external teams can contribute their own categories
   without changing ToolBank's code or the MCP client's.
5. Integrates with a domain system (acad-router) without forcing it onto Python — the router is
   .NET 8, ToolBank is Python, and they speak plain JSON-RPC over stdio.

`acad-router` is the **reference implementation** of the "router-over-ToolBank" pattern: it is
itself an MCP server, it is itself a ToolBank consumer (calling `mcpd_find` / `mcpd_connect`
internally to pull categories in), and it exposes domain meta-tools an agent understands from
their description rather than from a list of 230 raw tools. In production — the single-family
house project and the audit of the `[REDACTED-REFERENCE-DWG]` file — the design confirmed that
an LLM agent with a finite context window can operate **correctly and without hallucination** on
a system that, without ToolBank, would not fit in its head at all.

---

## Appendix

- ToolBank source: the [ToolBank repository](https://github.com/AI4CharityPL/toolbank) - released
  separately in early September 2026, so the link is not yet public (MIT,
  PyPI `toolbank>=0.3.0`); `C:\Users\DELL\toolbank` on the measurement machine.
- The acad-router and category sources: this repository.
- Contract rules, enforced pre-commit: `docs/engineering-rules/30-toolbank-manifest.md`,
  `docs/engineering-rules/31-toolbank-discovery-hygiene.md`,
  `docs/engineering-rules/00-architecture-invariants.md`.
- The ToolBank protocol specification: `docs/specification.md` and `docs/architecture.md` in the
  ToolBank repository.
