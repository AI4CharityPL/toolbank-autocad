# acad-structural domain traps

Structural-discipline domain traps — steel profile catalog scope, the S-* layer key shared
with acad-architecture, lintel sizing is a heuristic not a calculation, `insert_lintel` never
touches wall geometry, `LINTEL_TYPE` is a schedule tag only. Read BEFORE adding a tool to
acad-structural, extending `SteelProfileCatalog.cs`, or touching the door/window `LINTEL_TYPE`
attribute in `OpeningsPluginTools.cs`.

Triggered by a real-drawing comparison (see the artifact from that analysis) that found this
bank had no way to represent a real steel column profile, no lintel tooling at all, and only a
narrow structural layer annex — while the reference drawing had 24 real HEB-tagged columns and
174 individually-sized lintels, roughly one per opening.

## 1. `S-*` is the ONE structural layer key — do not fork it

`S-COLS`/`S-COLS-CTRL`/`S-SLAB`/`S-SLAB-HATCH` already existed in
`src/AcadMcp.Backend/Categories/Architecture/ArchitecturePalette.cs` before this category did.
`S-BEAM`/`S-BEAM-CTRL`/`S-LINTEL` were added to that SAME file, not a new `StructuralPalette.cs`.
A real reference drawing this bank was compared against used a Polish multi-branża numbered
layer key (`410-K`, `440-K`, `450-K`, ...) for the same concepts — that numbering stays
**documentation-only**, in `docs/knowledge-base/hospital/GRID-AND-LAYERS.md`'s cross-reference
table, for READING a real drawing. It is not a second authoring convention this bank writes in.
Two competing layer conventions in one bank is a worse state than one incomplete one — if you
are tempted to add a `K-410` layer anywhere in code, stop and re-read this section.

`ensure_architectural_layers` (acad-architecture) and `ensure_structural_layers`
(acad-structural) both create every layer in `ArchitecturePalette.All` filtered differently
(the latter to `Structural: true` entries) — they are not two different layer sets, just two
convenience entry points into the same one.

## 2. Why this category is almost plugin-free

`insert_steel_column`, `insert_beam` and `ensure_structural_layers` compose
`ArchitectureProxy`'s already-deployed generic verbs (`DrawPolylineAsync`, `DrawLineAsync`,
`EnsureLayerAsync`, `AddDBTextAsync` — all `acad.geometry2d.*`/`acad.layers.*`/
`acad.annotations.*` plugin calls that existed long before this category) rather than a
dedicated `acad.structural.*` plugin handler. `ArchitectureProxy` is deliberately `public` for
exactly this kind of cross-category composition (its own doc comment, rule 35 §2). This means
adding or tweaking those three tools needs **no plugin rebuild and no AutoCAD restart** — only
`LINTEL_TYPE` (§6) touches the plugin, because it's the one place this feature writes a live
block `AttributeDefinition`.

## 3. Do not confuse this with `acad-grids`

`acad-grids` draws structural **axis gridlines** (the lettered/numbered reference lines a plan
is dimensioned from — column line "A", gridline "3"). `acad-structural` sizes and draws
structural **members** (a column, a beam, a lintel) that typically SIT ON a grid intersection
but is a different concern entirely. An agent asked to "add the structural grid" wants
`acad-grids`; asked to "add the columns" wants `acad-structural`.

## 4. Steel column geometry is a documented simplification, not the full mill profile

`insert_steel_column` draws a 12-vertex closed polyline (flange + web outline) with **no root
radius** — the real fillet where web meets flange is omitted. This is deliberate: this bank is
2D-plan-symbolic throughout (see §5), and a filleted outline needs arc segments for a benefit
that doesn't matter at 1:50/1:100 plan scale. Consequence: `SteelProfileCatalog.AreaCm2` is
computed from the SAME no-fillet formula
(`2*WidthMm*FlangeThicknessMm + (HeightMm-2*FlangeThicknessMm)*WebThicknessMm`, in cm²) so a
live area check on the drawn polygon can be compared directly against the catalog value — do
not "fix" `AreaCm2` to match a certified mill figure without also changing the drawn geometry,
or the two will silently diverge and a future area-based verification will fail for the wrong
reason.

## 5. Everything here is 2D-plan-symbolic — there is no wall-height datum anywhere

Confirmed by direct inspection of `OpeningsPluginTools.cs`: doors/windows are drawn at
`Point3d(x, y, 0)`, `heightMm` is a schedule-label attribute never used in any cut/geometry
logic, and no `topOfWallMm`/elevation concept exists anywhere in this bank. `insert_beam` and
`insert_lintel` inherit this scope on purpose — they draw what a beam/lintel looks like from
directly above (a plan projection: dashed outline + centreline/tag), not a real 3D member with
a bearing elevation. Don't add a Z-coordinate to these tools without first deciding how the
whole bank would represent building sections — that is a much bigger change than this category.

## 6. `LINTEL_TYPE` is a schedule tag ONLY — `insert_lintel` never writes it itself

`insert_lintel` computes a `lintelTypeTag` (e.g. `RC-150x250` or a steel designation like
`HEB160`) and returns it. It does **not** call `insert_door`/`insert_window` and does **not**
write to any opening block's attributes — the caller passes the returned tag to
`insert_door(..., lintelType=...)` / `insert_window(..., lintelType=...)` explicitly (rule 65
§4, 15th attribute tag). This keeps the two tools' responsibilities separate: sizing vs.
recording.

**Known, accepted limitation** (same class as every other attribute this bank has added to an
existing family — nothing new): `LINTEL_TYPE` only appears on block definitions created AFTER
this change shipped. A drawing with a pre-existing `DOOR-SINGLE-900-2100` block definition
(created before `LINTEL_TYPE` was added to `s_attrTags`) will not retroactively gain the
attribute until that block definition is redefined. Do not treat a missing `LINTEL_TYPE` on an
old drawing as a bug.

## 7. `insert_lintel` NEVER cuts, resizes, or otherwise mutates a wall — this is an invariant, not an accident

The tool takes wall thickness and bearing length as INPUT (to size the plan symbol and total
length) but performs zero writes to any wall entity. This was a deliberate scope decision, not
an oversight: the wall is already cut by `cut_wall_for_opening` / `insert_door`'s own
`wallHandle` path before a lintel is ever relevant, and giving a second, unrelated tool the
ability to also touch wall geometry would create exactly the kind of double-cut hazard this
bank has already been burned by elsewhere in openings handling. If a future change makes
`insert_lintel` wall-aware, it MUST NOT call `CutWallCore` — cut once, at the opening, not
again at the lintel.

## 8. Lintel sizing is a heuristic — say so every time, not just once

The span→depth rule (`computedDepthMm`, rule 72 disclaimer text: *"Heuristic span/depth sizing
only. This is NOT a substitute for a structural engineer's calculation against actual loads,
material properties, and the applicable Eurocode/PN-EN. Verify before construction."*) is a
rough rule of thumb, not a calculation against any load case. The disclaimer is carried as an
explicit `disclaimer` field in `InsertLintelResult`'s own JSON — not only in the `[McpTool]`
description — specifically so it survives being read by something other than a human (a
downstream script, a schedule export) without depending on prose nobody re-reads on the tenth
call.

## 9. Steel profile catalog — sourcing confidence

`src/AcadMcp.Shared/Catalogs/SteelProfileCatalog.cs` ships a REPRESENTATIVE subset (6 HEA + 6
HEB + 7 IPE sizes), not the full EN 10365 range. Sourcing, honestly recorded (same discipline as
`docs/knowledge-base/residential/STANDARDS.md`'s Confirmed/Probable/Unconfirmed tags):

| Series | Source | Confidence |
|---|---|---|
| HEA, HEB | A named structural-steel dimension reference citing NEN-EN 10025-1/2 | Confirmed-from-named-secondary-source — real numbers, fetched live, not invented; not cross-checked against a mill certificate or the raw standard text |
| IPE | A separate Eurocode design-properties reference citing EN 10365 | Same confidence tier, different source page from HEA/HEB |

Both `isap.sejm.gov.pl`-style primary-text access attempts failed with a CAPTCHA wall earlier in
this bank's own history (see `docs/knowledge-base/residential/STANDARDS.md`'s sourcing note) —
steel dimension tables are an EU/manufacturer standard, not a Polish statute, so this catalog
deliberately sourced from manufacturer/handbook references instead of repeating that dead end.
**Before using this catalog's numbers on anything beyond a demonstration project, verify against
an actual mill certificate or the primary EN 10365 text.**

## 10. Cross-reference with validators

There is currently **no** `structural.*` validator rule — this category shipped tools before a
matching `validators/architectural/structural-*.yaml` because nothing in this pass produced a
checkable numeric requirement the way `hospital.rooms.or-min-area` did (a lintel's heuristic
depth is explicitly not a code minimum to validate against). If a future change adds a real,
citable structural requirement (e.g. a minimum bearing length per a specific standard), add the
rule under `validators/architectural/` following rule 33's format, same as every other typology
rule in this bank — do not skip that step just because this category's own tools already return
a disclaimer.
