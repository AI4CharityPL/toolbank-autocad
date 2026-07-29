# acad-validators rule format

Canonical YAML schema for acad-validators rules. Read BEFORE adding a new rule, a new check primitive, a new fix primitive, or touching the validator loader / engine.

Validator rules live in `validators/<discipline>/<id>.yaml`. The engine in
`src/AcadMcp.Backend/Validators/` loads every YAML it finds, indexes them by
`id`, and exposes them via the `acad-validators` MCP category.

## 1. File layout (one rule per file)

```yaml
id: arch.walls.on-walls-layer            # required, kebab-segments + dots
name: Walls must be on the WALLS layer   # required, human readable
discipline: architectural                # required: general | architectural | mechanical | electrical | civil | mep
severity: error                          # required: error | warning | info
description: |                           # required, 1-3 short sentences
  Every LINE / POLYLINE used as a wall must live on the WALLS layer
  per company standard CAD-001.
references:                              # optional, list of URLs / standard ids
  - https://docs.example.com/CAD-001
scope:                                   # optional; missing = whole document
  entity_types: [Line, Polyline]         # canonical names: Line, Polyline, Circle, Arc, Hatch, DBText, MText, BlockReference, Region, Spline, Ellipse, Solid, Polyline3d
  layer_pattern: "^WALL.*$"              # optional regex
  layer_in: [WALLS, WALLS_NEW]           # optional explicit list
  in_paperspace: false                   # default false (model space only)
checks:                                  # required, 1+ predicate(s); ALL must pass
  - { type: layer_equals, value: WALLS }
fix:                                     # optional; missing = manual-only
  type: move_to_layer
  layer: WALLS
  create_if_missing: true
```

## 2. The `id` rules

- Always lowercase `discipline.<area>.<short-slug>`, dot-separated, segments are kebab-case.
- One `id` ⇔ one YAML file. **Never** two files with the same `id`.
- Built-in rules ship under `validators/<discipline>/`.
- User rules created via `add_validator_rule` go to `validators/_user/<discipline>/`.
- `id` is the wire identifier used by every MCP tool: `validate_with_rule`, `explain_rule`, `auto_fix_violations`, etc. Renaming it is a breaking change.

## 3. Severity has a hard meaning - don't water it down

- **error** = the drawing is wrong; agent / human MUST fix before delivery.
- **warning** = unusual but not strictly wrong (e.g. very long polyline, suspicious layer name).
- **info** = informational hint (e.g. "consider adding a title block").

Tools `validate_drawing` / `validate_against_standard` accept a `min_severity`
filter (default `warning`). When in doubt: pick lower severity, not higher.
False positives at `error` destroy agent trust.

## 4. Scope semantics (entity-level rules)

Scope filters are AND-combined:

1. `entity_types` -- if missing, every entity matches. Names are canonical
   .NET runtime class names (`Line`, `Polyline`, `Circle`, `Arc`, `Hatch`,
   `DBText`, `MText`, `BlockReference`, `Region`, `Spline`, `Ellipse`,
   `Solid` (3D), `Polyline3d`). Use `*` for "any type".
2. `layer_pattern` -- .NET regex against `entity.Layer`.
3. `layer_in` -- explicit list, case-insensitive comparison.
4. `in_paperspace` -- defaults to `false`. Set `true` to validate the
   layouts (paper space) instead of model space; use a separate rule for
   each space. **Never** scope a single rule across both spaces - it
   makes auto-fix ambiguous.

Doc-level checks (see §5) ignore `scope` entirely.

## 5. Check primitives (engine v1)

### Entity-level (one entity at a time)

| `type`                       | params                                  | passes when                                        |
| ---------------------------- | --------------------------------------- | -------------------------------------------------- |
| `layer_equals`               | `value: string`                         | `entity.Layer == value` (case-insensitive)         |
| `layer_in`                   | `values: [string]`                      | `entity.Layer` is in `values`                      |
| `layer_matches`              | `pattern: string` (.NET regex)          | `Regex.IsMatch(entity.Layer, pattern)`             |
| `color_equals`               | `aci: int` OR `rgb: [r,g,b]`            | matches ACI or true-color                          |
| `color_in`                   | `aci: [int]`                            | ACI in list                                        |
| `linetype_equals`            | `value: string`                         | linetype name matches (Continuous / DASHED / ...)  |
| `lineweight_at_least`        | `value_mm: number`                      | `entity.LineWeightMm >= value`                     |
| `length_at_least`            | `value: number`                         | `Length >= value` (Line / Polyline / Arc)          |
| `length_at_most`             | `value: number`                         | `Length <= value`                                  |
| `area_at_least`              | `value: number`                         | `Area >= value` (closed Polyline / Region / Hatch) |
| `area_at_most`               | `value: number`                         | `Area <= value`                                    |
| `radius_at_least`            | `value: number`                         | `Radius >= value` (Circle / Arc)                   |
| `radius_at_most`             | `value: number`                         | `Radius <= value`                                  |
| `text_matches`               | `pattern: string`                       | text contents match regex                          |
| `text_height_at_least`       | `value: number`                         | `TextHeight >= value`                              |
| `attribute_present`          | `tag: string`                           | BlockReference has attribute with `tag`            |
| `attribute_value_matches`    | `tag: string`, `pattern: string`        | attribute value matches regex                      |
| `bbox_inside`                | `min: [x,y]`, `max: [x,y]`              | entity bbox fully inside the rectangle             |
| `bbox_outside`               | `min: [x,y]`, `max: [x,y]`              | entity bbox does not intersect the rectangle       |
| `not`                        | `check: <check>`                        | inverts                                            |
| `any_of`                     | `checks: [<check>...]`                  | at least one passes (OR)                           |
| `all_of`                     | `checks: [<check>...]`                  | every one passes (AND - same as the top-level)     |

### Document-level (one verdict per drawing)

| `type`                       | params                                  | passes when                                        |
| ---------------------------- | --------------------------------------- | -------------------------------------------------- |
| `entity_count_at_least`      | `entity_types: [...]`, `value: int`     | total count of those types ≥ value                 |
| `entity_count_at_most`       | `entity_types: [...]`, `value: int`     | total count ≤ value                                |
| `layer_must_exist`           | `name: string`                          | layer table contains the layer                     |
| `block_must_be_defined`      | `name: string`                          | block table contains the block definition          |
| `text_style_must_exist`      | `name: string`                          | text style table contains it                       |
| `units_must_be`              | `value: mm \| cm \| m \| in \| ft`      | drawing's INSUNITS matches                         |

Doc-level rules go in the same `checks:` list. The engine routes each check
to the right evaluator.

## 6. Fix primitives (engine v1)

| `type`              | params                                              | semantics                                       |
| ------------------- | --------------------------------------------------- | ----------------------------------------------- |
| `move_to_layer`     | `layer: string`, `create_if_missing: bool` (false)  | sets `entity.Layer = layer`                     |
| `set_color`         | `aci: int` OR `rgb: [r,g,b]`                        | sets entity color                               |
| `set_linetype`      | `value: string`, `create_if_missing: bool` (false)  | sets linetype                                   |
| `set_lineweight`    | `value_mm: number`                                  | mapped to nearest enum LineWeight value         |
| `delete_entity`     | `{}`                                                | erases the entity (transactional)               |
| `set_attribute`     | `tag: string`, `value: string`                      | for BlockReference attributes only              |

If `fix` is missing, `auto_fix_violations` simply skips that violation with
`outcome: manual_only`. NEVER ship a fix you wouldn't apply blindly across
1000 drawings - false-fix is much worse than a missing fix.

## 7. Hard rules

- One YAML file per `id`. The loader rejects duplicates.
- `severity` MUST be `error | warning | info`. Anything else is rejected at load.
- `discipline` MUST be in the documented enum. New disciplines require an ADR + an engineering-rules update (rule 53).
- `description` MUST be non-empty and ≥ 25 characters. Pre-commit gate enforces.
- A rule with `fix` but **no** `scope` and **no** scoping check is rejected
  by the loader: blanket entity-deletion rules are forbidden by safety design.
- All entity-level checks see the entity as **read-only**. Mutation only
  happens during `auto_fix_violations` and goes through fix primitives.

## 8. Adding a new primitive

1. Add the primitive name + params + semantics to the table in §5 / §6 here FIRST.
2. Implement `IEntityCheck` / `IDocCheck` / `IFixOperation` in
   `Backend/Validators/Predicates/` or `Backend/Validators/Fixes/`.
3. Register it in `CheckFactory` / `FixFactory`.
4. Add at least one bundled YAML rule that exercises the new primitive.
5. Add a unit test in `tests/AcadMcp.Validators.Tests` (Phase 5 deliverable).

## 9. Self-check

`AcadMcp.Backend.exe --validators-self-check` loads every embedded + repo + user
YAML through `RuleLoader`/`StandardLibrary` and exits non-zero on the first
parse failure. The pre-commit gate (rule 40 §6) invokes it whenever a
`validators/**/*.yaml` is staged, so a malformed rule never reaches main.
Run it manually after editing rules:

```powershell
dotnet build src/AcadMcp.Backend/AcadMcp.Backend.csproj -c Release
src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe --validators-self-check
```
