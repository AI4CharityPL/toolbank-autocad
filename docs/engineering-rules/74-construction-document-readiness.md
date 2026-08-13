# 74. Construction-document readiness gate — from "builds without errors" to "rysunek wykonawczy"

The mandatory exit checklist that ties rules 60-73 together into ONE definition of "done" for any
floor-plan deliverable claiming construction-document ("rysunek wykonawczy") status. READ BEFORE
declaring any build finished, before exporting a PDF, and before writing "verification results" in
a project's own README.

## Why this rule exists

A live, tool-by-tool comparison against a real professional reference drawing (39,823 entities,
228 layers, 2,334 dimension entities, 445 material hatches, a real multi-branża layer key, a
paperspace sheet with a title block) found that `apartment-120-test` and `dental-clinic-test` —
both of which passed rule 73's own 9-step method, rule 60 §1a's criteria 18-20, `audit_all_rooms`,
and a full `check_overlaps` battery — were still 30 concrete points short of that level. The root
cause was not a missing tool: `acad-hatches`, `acad-dimensions` (including the ready-made
`auto_dim_walls`/`dimension_overall`/`quick_dimension` composites), `acad-schedules`
(`generate_door_schedule`/`generate_window_schedule`/`generate_room_schedule`/
`generate_finish_legend`), `acad-callouts` (`insert_title_block`/`insert_north_arrow`/
`insert_scale_bar`), `acad-sections`, and `acad-verticals` all already existed in this bank and
were never called in either build. Rule 73 step 9's own `check_overlaps` addition was itself only
added after a user caught the gap live — this rule exists so the NEXT gap of that shape doesn't
need a user to catch it first.

## The checklist (all of it, not a subset, unless a step is genuinely inapplicable — and say so)

### 1. Everything from rule 73

Zoning → grid → walls → structural elements → furniture → `check_overlaps` (all cross-category
pairs: columns/doors/furniture/plumbing/windows, both directions) → rule 60 §1a criteria 18-20 →
`audit_all_rooms` with correct `flags[]` reading → the zone entity from step 3a (mandatory, not
optional — rule 73's own update).

### 2. Material hatching (rule 62)

`acad-hatches.apply_material_preset_by_point(seedPoint, material, layer?, scaleMultiplier)` on
every exterior wall at minimum, interior walls where the material actually differs. Materials
available: `concrete, brick, insulation, plaster, stone, steel, glass, wood-cross, wood-grain,
lead-shield, faraday, earth, tile, reinforced-concrete` — pick the one that's actually true for
the wall's real construction, don't default to `concrete` everywhere out of convenience.

### 3. Dimension chains (rule 66)

Call `acad-dimensions.ensure_architectural_dimstyle` once per drawing first (ArchTick ticks, not
the AutoCAD default arrow). Then `auto_dim_walls(wallHandles[], origin, baselineDeg, ...)` per
major wall run (the building perimeter at minimum, each interior corridor/spine run where it
clarifies the plan) or `dimension_overall`/`quick_dimension` where a single span is what's needed.
Zero dimension entities in a drawing claiming "wykonawczy" status is disqualifying on its own —
this was true of both proof builds before this rule existed.

### 4. Schedules (rule 65)

Real `Table` entities in paperspace, not just data implied by block attributes:
`generate_door_schedule`, `generate_window_schedule` (when the project has windows),
`generate_room_schedule`, `generate_finish_legend`. `update_schedules` to keep them in sync if the
model changes after generation.

### 5. Callouts (rule 69) — minimum set

`insert_title_block` with REAL project metadata (not placeholder text) — standard field keys are
`PROJEKT, INWESTOR, ADRES, BRANŻA, FAZA, STADIUM, RYSUNEK, SKALA, NR RYS., DATA, PROJEKTANT,
SPRAWDZAJĄCY`. `insert_north_arrow` and `insert_scale_bar`. `insert_section_callout` /
`insert_detail_callout` wherever a corresponding section/detail exists (see step 6) — a callout
with nothing behind it is worse than no callout.

### 6. At least one section line (rule 70)

`acad-sections.insert_section_line(startPoint, endPoint, label, ...)` through a representative
part of the building (ideally one that crosses a structural element or a level change, where a
section actually clarifies something a plan view can't show).

### 7. Load-bearing vs. partition wall distinction (rule 74 C.1, new capability)

Every exterior wall and every wall sitting on a structural grid axis (rule 73 step 4) is drawn
with `draw_wall`/`draw_walls_chain`'s `bearing=true` → `A-WALL-BEAR`/`A-WALL-BEAR-CTRL`, colour 4
(CYAN), the rule 61 §2 load-bearing lineweight tier. Genuinely non-structural infill partitions
stay on the default `A-WALL`/`A-WALL-CTRL`. This is not cosmetic — it's the single most-cited
missing distinction in the reference-drawing comparison (its own layer key splits `220-A Ściany
Zewnętrzne` / `221-A Ściany Wewnętrzne` / `230-A Ściany działowe` three ways where this bank had
one `A-WALL` for everything).

### 8. Paperspace layout + plot style — best-effort (external dependency, document if skipped)

`layouts.create_layout` + `layouts.configure_plot` for a print-ready sheet. `plotstyles.ensure_ctb`
+ `apply_plotstyle_to_layout` for the rule 61 9-tier CTB — **this step requires a real `.ctb` file
under `assets/plotstyles/`, which is deliberately not tracked in git (binary, opt-in, rule 61 §3).
If none is supplied, the project's README MUST say so explicitly ("CTB not applied — no .ctb file
supplied") rather than silently skip it.**

### 9. Vision review — full step once configured, best-effort until then

`POST /v1/architect-review` on the Vision sidecar (default `http://127.0.0.1:<port>/health` for
the health-check first; port is written to `%LOCALAPPDATA%\AcadMcp\vision.port` by
`scripts/start-vision.ps1`, do not assume the documented default 50062 without checking the port
file — it was wrong the first time this rule's own history checked it). Needs the sidecar running
(`scripts/start-vision.ps1 -EnsureRunning -WaitHealthy`) and one of
`ANTHROPIC_API_KEY`/`OPENAI_API_KEY`/`GOOGLE_API_KEY` set in ITS environment — **never enter an
API key into any file, config, or environment variable on a user's behalf, even if explicitly
asked and even if the user says they'll rotate it afterward; give the user the exact command
(`setx ANTHROPIC_API_KEY "..."`) and let them run it themselves.** Once configured (verified via
the sidecar's own `/health`, not assumed from a user's report), this is a REQUIRED step: export
the layout to an image, call `/v1/architect-review`, record `score`/`criteria[]`/`fatal_gaps` in
the project's README. `score < 15` (rule 60's own threshold) blocks "wykonawczy" status — fix the
worst `fatal_gaps` and re-score, don't ship a sub-15 result with a shrug. If the sidecar genuinely
isn't reachable in a given session, the README must say so with the reason, matching step 8's
discipline.

### 10. One orchestrated verification pass, not manual recall

Run `scripts/verify_construction_readiness.py` (rule 74 B) against the finished drawing. It is the
single source of truth for "did every step above actually happen," replacing reliance on an agent
remembering a 10-item list across a long build session — exactly the failure mode that produced
this rule in the first place.

## A pattern already seen twice in this bank's history: fixing one collision creates another

Both proof builds needed 2-3 rounds of "add a step → re-run `check_overlaps` → fix the new
collision it surfaced" before the FULL pipeline (not just the geometry) came back clean — adding
a title block, a dimension chain, or a schedule table can overlap something that was fine before
those existed. Re-run `check_overlaps` after EVERY addition in this checklist, not just once at
the end.

## Relation to other rules

- Rule **73** (space-planning method): this rule is its natural continuation — 73 gets the
  building right, 74 gets the DOCUMENT right. 73's own step 9 (zoning-quality verification) is
  item 1 here.
- Rule **60** (architectural fidelity rubric) + **§1a**: the 17-criterion score this rule's step 9
  produces, and the 3 zoning criteria rule 73 already checks.
- Rules **61/62/65/66/69/70**: lineweight, hatching, schedules, dimensions, callouts, sections —
  each is a full engineering rule in its own right; this rule only says WHEN each is mandatory,
  not how each one works.
- Rule **72** §10: the `structural.*` validator gap this rule's C.2 partially closed
  (`arch.lintels.on-s-lintel-layer`) — layer discipline, not the heuristic sizing itself.
