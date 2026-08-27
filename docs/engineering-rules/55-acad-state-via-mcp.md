# AutoCAD state is verified through MCP, and nowhere else

AutoCAD state (alive, version, active document, layer, entities) MUST be checked via MCP (acad_status). NEVER via Get-Process, tasklist, ps or terminal.

This entire project exists so that an agent never has to infer AutoCAD's state from the operating system. Whenever you need to know:

- whether AutoCAD is running,
- which version / vertical it is (AutoCAD, Architecture, Mechanical, Civil 3D),
- which document (`.dwg`) is active,
- which layer is active,
- how many entities the model holds,
- whether the plugin is loaded (pipe `\\.\pipe\acadmcp`),

you **always** call `acad_status` on `user-acad-router` (Invariant #6 in `00-architecture-invariants.md`).

## Why not Get-Process / tasklist / ps

1. `Get-Process -Name acad` shows that a process exists. It does not say whether `AcadMcp.Plugin.dll` is NETLOAD'ed, whether the pipe is alive, or which document is active.
2. Educational licences raise invisible modal dialogs: `acad.exe` is alive while the UI thread is blocked — a visible process over a CAD session that is effectively dead. Only `acad_status`, which round-trips through the pipe, detects that.
3. Several `acad.exe` processes can run in one Windows session. `Get-Process` in a terminal cannot tell you which one is your target; the pipe answers that unambiguously.
4. MCP is the system's only official contract (`00-architecture-invariants.md`, Invariant #3: Named Pipe = ONLY bridge). Every other source of truth drifts.

## Correct

```jsonc
// calling the meta-tool
CallMcpTool server="user-acad-router" toolName="acad_status" arguments={}
// returns:
{
  "alive": true,
  "acadProductName": "AutoCAD",
  "acadVersion": "25.0.0.0",
  "documentName": "C:\\...\\[REDACTED-REFERENCE-DWG]",
  "activeLayer": "0",
  "entityCount": 231019,
  "isLT": false,
  "vertical": null,
  "modeBanner": "full"
}
```

Only this is evidence that the agent may begin any drawing operation at all.

## Incorrect (rejected in review)

```powershell
Get-Process -Name "acad","accoreconsole","AcadMcp.Backend"   # ❌ says nothing about the pipe
tasklist | findstr acad                                       # ❌ same
ps aux | grep acad                                            # ❌ same, and the wrong platform
```

If an agent reaches for any of the above, treat it as a breach of Invariant #3 and require the fix that replaces it with `acad_status`.

## When `acad_status` returns `alive: false` or an error

Only then may you:
1. Run `scripts/deploy-plugin.ps1 -Kill` (when you suspect a hung `acad.exe` behind an invisible modal).
2. Ask the user to restart AutoCAD by hand.
3. Read the logs under `%LOCALAPPDATA%\AcadMcp\logs\pipe-*.log`.

## Extensions of this rule

Every other piece of drawing state is treated the same way — the first step is always MCP, never the terminal:

| What you want to know | Tool |
|---|---|
| Does the drawing hold any entities? How many? | `acad_status` or `acad.validators.doc_summary` |
| What layers exist? Does `A-WALL-EXT`? | `acad.layers.list_layers` |
| What blocks, attributes or dynamic blocks exist? | `acad.blocks.list_blocks` |
| What layouts exist? | `acad.layouts.list_layouts` |
| Does a specific entity still exist, by handle? | `acad.selection.get_entity_info` |

All of it lives in the `acad-*` namespace, reachable through `acad_load_category` / ToolBank — never from a shell.
