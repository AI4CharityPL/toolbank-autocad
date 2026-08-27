# 7 Architectural Invariants

Seven sacred invariants of the ToolBank AutoCAD architecture. Read this before touching ANY code.

These are non-negotiable. Breaking any of them = system corruption. If you must violate one, stop, ask the user, and update this rule.

## 1. ONE Backend binary, parameterized by `--category`

There is exactly **one** built artifact: `AcadMcp.Backend.exe`. It is launched in N separate processes, each with `--category <name> --transport stdio`. Each process exposes ONLY tools from one category.

**NEVER** create a second host project. **NEVER** copy `Backend.exe` per category - launchers in `bin-launchers/` are thin `.cmd` wrappers calling the same binary with different args.

## 2. ONE Plugin, in-process AutoCAD

`AcadMcp.Plugin.dll` is loaded into AutoCAD via NETLOAD (or APPLOAD startup suite). It is the **single** source of truth for AutoCAD database operations. All Backend processes connect to it through ONE named pipe (`\\.\pipe\acadmcp`).

**NEVER** spawn parallel plugins. **NEVER** call AutoCAD COM from Backend if the plugin can do it - COM is fallback only (and only on AutoCAD LT).

## 3. Named Pipe is the ONLY bridge Backend ↔ Plugin

Communication Backend → Plugin: named pipe with JSON-RPC. No HTTP, no gRPC, no shared memory, no DLL injection beyond the plugin itself.

The pipe is local-user only (security). Plugin serializes requests from N Backend processes FIFO per Document handle.

## 4. Microservers are THIN skins

Files in `src/AcadMcp.Backend/Categories/<X>/` contain ONLY:
- `[McpTool]`-decorated methods (the public contract)
- Argument/result records
- Simple validation and routing to `Shared` or `_Shared` helpers

**No business logic in tool methods.** Real work happens in helpers or via plugin RPC. Tool method = parse args → call helper / pipe → return DTO.

## 5. ToolBank is the ONLY discovery mechanism

We do NOT register all 50 category microservers in the user's `mcp.json`. Only `acad-router` lives there permanently (alongside `toolbank-discovery` and `toolbank-dynamic` which the user already has).

Categories are discovered via `mcpd_find` (semantic) and connected via `mcpd_connect(lazy_mode=true)`. Every category MUST have a manifest in `toolbank-manifests/acad-<name>.json` with rich `intent_examples` PL+EN. Without this, the LLM cannot find your tools.

## 6. Router stays connected in the MCP client permanently

`acad-router` exposes 10 meta-tools: `acad_status`, `acad_find_tools`, `acad_load_category`, `acad_recommend_categories`, `acad_explain_capabilities`, `acad_call`, `acad_describe_drawing`, `acad_undo_checkpoint`, `acad_restore_checkpoint`, `acad_design_iterate`. This list, `toolbank-manifests/acad-router.json`, and `RouterServer.cs`'s tool stubs must always agree (Phase 7.4) -- verified 2026-07-29: manifest was missing `acad_call`, now fixed; count and names now match code exactly.

Router does NOT do AutoCAD work itself - it orchestrates. Adding heavy tools to the router = anti-pattern. Such tools belong in their own category.

## 7. Vision sidecar communicates ONLY via gRPC

`AcadMcp.Vision` (Python) exposes gRPC over localhost. HTTP is allowed only for `/health` and `/metrics`. All vision tools in `Categories/Vision/` are thin proxies that call the gRPC stubs.

**NEVER** call vision LLMs (Claude, GPT-4V) directly from C# Backend - all model calls go through the sidecar so we have one place for caching, rate limiting, and key management.

---

## Enforcement

Violations of invariants 1, 4, and 5 are caught by `ArchitectureTests` (NetArchTest) in `tests/AcadMcp.Tests/`. Violations of 2, 3, 6, 7 are runtime-detectable and the plugin/router will refuse non-conforming connections.
