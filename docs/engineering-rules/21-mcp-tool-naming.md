# Tool naming convention

Tool naming convention. snake_case, max 5 words, verb-first.

Tool names are PUBLIC contract. Once shipped to MCPBank, renaming = breaking change.

## Rules

1. **`snake_case`** - lowercase letters, digits, underscores. Must start with a letter. Source generator regex: `^[a-z][a-z0-9_]*$`.
2. **Max 5 words** separated by underscore. Source generator emits `ACAD0002` for >5.
3. **Verb first.** `draw_circle`, `move_entities`, `set_layer_color`. NOT `circle_draw` or `entities_move`.
4. **English verb.** Even when the domain is Polish, the API is English. Polish goes in `Intent` examples.
5. **No abbreviations** unless industry-standard (`ocr`, `pdf`, `dxf`, `iso`, `pn` for Polish norm OK).
6. **No version suffixes** (`draw_circle_v2`). If a v2 is needed, deprecate v1, name the new one `draw_circle_with_diameter` or whatever the actual difference is.
7. **No category prefix** in the name. The category is implicit from the file's folder. `Categories/Geometry2D/CircleTools.cs : draw_circle` (NOT `geom2d_draw_circle`).

## Standard verbs (use these consistently)

| Verb         | Meaning                                        | Example                       |
| ------------ | ---------------------------------------------- | ----------------------------- |
| `draw_*`     | Create new geometry entity                     | `draw_circle`, `draw_polyline`|
| `create_*`   | Create non-geometry (layer, block, layout)     | `create_layer`, `create_block`|
| `set_*`      | Set a property on existing entity/setting      | `set_layer_color`             |
| `get_*`      | Read a property                                | `get_entity_handle`           |
| `list_*`     | Enumerate                                      | `list_layers`, `list_blocks`  |
| `move_*`     | Move entity                                    | `move_entities`               |
| `rotate_*`   | Rotate entity                                  | `rotate_entities`             |
| `scale_*`    | Scale entity                                   | `scale_entities`              |
| `mirror_*`   | Mirror entity                                  | `mirror_entities`             |
| `delete_*`   | Erase from database                            | `delete_entities`             |
| `find_*`     | Search/locate                                  | `find_entities_by_layer`      |
| `validate_*` | Run validation rule                            | `validate_line_weights`       |
| `apply_*`    | Apply a fix or transformation                  | `apply_layer_standard`        |
| `export_*`   | Output to non-DWG format                       | `export_to_pdf`               |
| `import_*`   | Bring in from non-DWG format                   | `import_dxf`                  |
| `describe_*` | Returns a textual/structured description       | `describe_drawing`            |

## Plurality

Tools that operate on collections use plural: `move_entities` (takes `EntityHandle[]`). Tools on single entity use singular only when there's no batched variant: prefer always-batched `delete_entities` over `delete_entity`.

## Bad → Good examples

| Bad                          | Good                          | Why                                |
| ---------------------------- | ----------------------------- | ---------------------------------- |
| `geom2d_draw_circle`         | `draw_circle`                 | No category prefix                 |
| `circle_create`              | `draw_circle`                 | Verb first                         |
| `drawCircleByThreePts`       | `draw_circle_by_3_points`     | snake_case                         |
| `do_the_thing_with_circles`  | `draw_circle_at_point`        | Specific verb, max 5 words         |
| `cir`                        | `draw_circle`                 | Spell it out                       |
