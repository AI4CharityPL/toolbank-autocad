# Domain MCP categories — design contract

Design contract for DOMAIN MCP categories (acad-architecture / -mechanical / -civil / -electrical / -parametric). Read BEFORE adding a new domain category, a new domain tool, or a new domain block library.

Domain categories (`acad-architecture`, `acad-mechanical`, `acad-civil`,
`acad-electrical`, `acad-parametric`) are the **intent layer** that AI agents
actually want to talk to. They turn `"draw a 200 mm exterior wall from A to B"`
into the right combination of primitives (polyline, layer, hatch, dimension,
block insert) without the agent having to remember which layer or which block
or which linetype.

This rule pins the universal contract for every domain category. A
discipline-specific traps file (rule 36 architecture, 37 mechanical, …) lists
the discipline-specific pitfalls.

## 1. Layer of abstraction

A domain tool MUST express **what** to draw, never **how**. Bad name:
`draw_two_parallel_polylines_with_hatch`. Good name: `draw_wall`. The "how"
lives inside the tool implementation by composing primitives from
`acad-geometry-2d`, `acad-blocks`, `acad-layers`, `acad-annotations`,
`acad-dimensions`.

If the agent has to pre-compute the polyline coordinates, the tool is too
low-level — push the geometry inside.

## 2. Compose primitives, don't reinvent them

Each domain tool MUST be implemented by orchestrating existing
`acad.<primitive>.*` plugin handlers via `IPluginGateway`. **Do NOT** add
duplicate plugin handlers for "wall geometry", "door geometry" etc. The plugin
already knows how to draw a polyline / insert a block / set a layer.

Allowed exceptions:
- A new plugin handler is ok when the operation has no decomposition into
  existing primitives (e.g. `acad.parametric.add_constraint` — there is no
  primitive for a geometric constraint).
- Performance: if a wall draws 6 primitives and we'd issue 6 IPC round-trips,
  add ONE plugin batch handler (`acad.architecture._draw_wall_atomic`) that
  runs all 6 inside a single transaction. Document why in the handler header.

## 3. Auto-create the world the tool needs

A domain tool MUST be able to run on an empty drawing. If `draw_wall` needs
the `WALLS` layer, the tool MUST ensure that layer exists (idempotent
`create_layer` if missing) before drawing. Same for linetypes, text styles,
dimension styles, block definitions.

Conventions:
- Layer names follow `35a-domain-layer-conventions.md` (per discipline).
- A tool that auto-creates infrastructure MUST report it in the result
  (`createdLayers: [...]`, `createdBlocks: [...]`) so the agent can audit.
- Auto-creation MUST NOT silently overwrite an existing definition. If the
  block `WINDOW_900` already exists with different geometry, FAIL with
  `E_DOMAIN_BLOCK_CONFLICT` rather than redefining it.

## 4. Idempotency and "draw the same thing twice"

Per rule 23, every tool returns the new entity handle(s). A domain tool MUST
NOT silently dedupe — if the agent calls `draw_wall` twice with the same
endpoints, you get two coincident walls (the second call returns its OWN new
handle). Dedup is an `acad-validators` concern, not a draw-tool concern.

Counter-example: `ensure_titleblock_for_layout` IS idempotent — it's an
`ensure` verb. Reserve idempotent ensure-style verbs for **infrastructure**
(layers, blocks, dimstyles), never for **content** (walls, doors, parts).

## 5. Discipline isolation in the manifest

Every domain manifest in `toolbank-manifests/` MUST set:

```json
{
  "tags": ["acad", "architecture"],
  "metadata": {
    "discipline": "architectural",
    "depends_on_categories": ["geometry-2d", "blocks", "layers", "annotations"]
  }
}
```

`depends_on_categories` is consumed by `acad-router.discover_categories` so
the router can warm-start the right primitives when the agent loads a domain
category.

## 6. Units assumption

Domain tools assume the drawing is in millimetres unless the discipline
canonically uses something else (civil = metres, US-arch = inches). The tool
MUST declare its expected units in the description AND check
`doc_summary.units` first. If the drawing is in the wrong units, FAIL with
`E_DOMAIN_UNITS_MISMATCH` — never silently scale.

## 7. Block libraries live under `blocks/<discipline>/`

Discipline blocks (door templates, window templates, mechanical fasteners,
electrical symbols) ship as `.dwg` files under `blocks/<discipline>/` in the
repo. The first call that needs a block calls
`acad.blocks.define_block_from_file` against the bundled DWG, then inserts.
Resolved block paths MUST be sanitised — never pass an agent-supplied
`blockName` straight into a filesystem `Path.Combine`.

## 8. Validators pair

Every domain category SHOULD ship at least 3 validator rules under
`validators/<discipline>/` that the new tools were designed to satisfy. This
is the closing loop: the AI draws with `acad-architecture`, then validates
with `acad-validators` against the same conventions. Mismatches between the
two are bugs in the domain category, not in the rules.

## 9. Tool-count budget

Domain categories grow large. To keep `tools/list` payloads under MCP's soft
2 KB JSON budget per category:

- **Cap each domain category at 30 tools.** If you need more, split by
  sub-discipline (`acad-architecture-walls`, `acad-architecture-mep`).
- Tools that don't fit the cap go into specialised subcategories — the router
  knows about them.

## 10. Adding a new domain category — checklist

1. Write the discipline traps `.md` (rule 36 / 37 / 38 / 39 / 42 …) FIRST.
2. Add 1–3 `validators/<discipline>/*.yaml` rules so the new tools have a
   compliance partner.
3. `pwsh scripts/new-category.ps1 -Name <discipline>` to scaffold.
4. Implement tools in `Backend/Categories/<Discipline>/*Tools.cs` composing
   primitives (rule §2).
5. If a batch plugin handler is justified (§2 exception), add it under
   `Plugin/Tools/Domain<Discipline>PluginTools.cs` and register it in
   `PluginEntryPoint.cs`.
6. Ship at least one bundled block library DWG when the discipline needs it.
7. `dotnet build -c Release` (regenerates manifest).
8. Update CHANGELOG and run `pwsh scripts/pre-commit.ps1 -All`.
