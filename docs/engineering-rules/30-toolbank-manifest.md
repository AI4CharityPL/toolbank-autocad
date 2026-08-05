# ToolBank manifest format

ToolBank manifest format. One file per category. Required fields and validation.

Each category has exactly one manifest file: `toolbank-manifests/acad-<name>.json`. The router has its own (`acad-router.json`).

These files are merged into `C:/Users/DELL/toolbank/registry/mcpd-registry.json` by `scripts/register-mcps.ps1`.

## Required JSON shape

```json
{
  "id": "acad-geometry-2d",
  "name": "acad-geometry-2d",
  "description": "AutoCAD MCP - 2D geometry primitives: line, polyline, circle, arc, ellipse, spline, point, text. Powered by .NET plugin via named pipe.",
  "transport": {
    "type": "stdio",
    "command": "C:\\Users\\DELL\\Dev\\autocad-mcp\\bin-launchers\\acad-geometry-2d.cmd",
    "args": [],
    "env": {}
  },
  "lazy_mode": true,
  "tags": [
    "autocad", "cad", "geometry", "2d", "drawing",
    "line", "polyline", "circle", "arc", "ellipse", "spline",
    "rysunek", "okrag", "linia", "polilinia", "luk"
  ],
  "intent_examples": [
    "narysuj linie",
    "stworz okrag w punkcie",
    "draw a circle at origin",
    "create polyline from points",
    "add arc to drawing",
    "wstaw splajn"
  ],
  "tools_summary": [
    {
      "name": "draw_line",
      "description": "Draw a line segment between two 3D points. Honors current layer and INSUNITS.",
      "tags": ["draw", "line", "geometry"]
    }
  ],
  "metadata": {
    "category": "geometry-2d",
    "tool_count_target": 30,
    "requires_plugin": true,
    "supported_acad_versions": ["2020", "2021", "2022", "2023", "2024", "2025"],
    "supported_lt": false,
    "owner": "AutoCAD MCP Megasystem"
  }
}
```

## Required fields

| Field                 | Required? | Purpose                                                  |
| --------------------- | --------- | -------------------------------------------------------- |
| `id`                  | YES       | Unique within registry. MUST be `acad-<name>`.           |
| `name`                | YES       | Display name (== `id` for our manifests).                |
| `description`         | YES       | One paragraph for human + LLM. English.                  |
| `transport.type`      | YES       | Always `"stdio"` for our categories.                     |
| `transport.command`   | YES       | Path to the launcher `.cmd`.                             |
| `lazy_mode`           | YES       | Always `true` - never connect at startup.                |
| `tags`                | YES       | 10+ entries. Mix English and Polish.                     |
| `intent_examples`     | YES       | **5+ entries, mix PL+EN**. Used by `mcpd_find` for ranking. |
| `tools_summary`       | YES       | One entry per tool. Auto-derived from `[McpTool]` by `BankAutoRegister`. |
| `metadata.category`   | YES       | Category id without `acad-` prefix.                      |
| `metadata.requires_plugin` | YES  | If true, manifest also says LT not supported.            |

## Forbidden patterns

- Hand-editing `tools_summary` after the category has tools - it MUST be derived from code (`BankAutoRegister.GenerateAsync`). Stale tool lists = `MF1003/MF1004` errors in `check-manifests.ps1`.
- Cross-category tool entries. A `acad-blocks` manifest cannot list tools from `acad-layers`.
- Hardcoded absolute paths to AutoCAD or .NET runtime. Use the launcher `.cmd` as the indirection.
- Sensitive data (API keys, tokens) in manifest. They go in launcher env if needed.

## Auto-generation

`scripts/new-category.ps1 -Name geometry-2d` generates a starter manifest with placeholder `tools_summary: []`. After tools are written, run `dotnet run --project src/AcadMcp.Backend -- --category geometry-2d --regenerate-manifest` (Phase 1 feature) to fill `tools_summary` from `[McpTool]` metadata.
