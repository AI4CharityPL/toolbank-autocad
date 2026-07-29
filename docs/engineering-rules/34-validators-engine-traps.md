# acad-validators engine traps

Pitfalls of the acad-validators engine - performance, transactions, false-positives, baseline diffs. Read BEFORE editing the validator engine, the plugin collector, or any auto-fix code path.

These are the recurring mistakes when changing anything in the validation
pipeline (rule loader → plugin collector → predicate evaluator → fix
applier). Read this file before any change to that pipeline.

## 1. **Collect entities ONCE per validation run, not once per rule**

`acad.validators.collect_entities` round-trips through the named pipe and
does a full ModelSpace walk inside a transaction. A 50k-entity drawing
takes seconds. Naive engines call the collector once per rule and then
spend a minute on a 30-rule run.

The Backend engine MUST:

- Build the **union scope** of every selected rule first
  (`UNION(entity_types)`, `UNION(layer_in)`, the most permissive
  `layer_pattern`, model-vs-paper bucket).
- Make **one** plugin round-trip per scope bucket (one for model space,
  one for paper space when needed).
- Cache the resulting `EntitySnapshotDto[]` for the duration of the
  validation request.
- Apply per-rule scope filters in-memory in C#.

Never put a `collect_entities` call inside the per-rule loop.

## 2. The plugin collector is **read-only** - never mutate during collection

`ValidatorsPluginTools.CollectEntities` opens entities `OpenMode.ForRead`
inside a transaction it commits. **Do not** upgrade to `ForWrite`, do not
call `entity.UpgradeOpen()`, do not invoke fixes from inside the collector.
Mutation happens exclusively in `apply_fixes`, which uses its own
transaction and explicit `DocumentLock`. Mixing the two breaks rule 11.

## 3. Auto-fix runs in a **single grouped transaction**, not one per fix

`ApplyFixes` opens **one** transaction per call and applies every fix
inside it. Reasons:

- Per-fix transactions thrash undo and corrupt user expectations
  ("I undid one click and it only undid one of 200 fixes").
- A single transaction means one undo step, one commit, one chance to
  abort cleanly on the first failure.
- If any fix throws, abort the transaction (`tr.Abort()`), report
  `outcome: error` for that fix, and report `outcome: rolled_back` for
  every fix that came after. **Never** half-commit a batch of fixes.

## 4. Map ACI ↔ true-color carefully

- `EntitySnapshotDto.ColorAci` is set ONLY when `entity.Color.IsByAci`.
- `EntitySnapshotDto.ColorRgb` is set ONLY when `entity.Color.ColorMethod == ColorMethod.ByColor` (true-color).
- `ByLayer` / `ByBlock` produce both fields = null. Predicates
  `color_equals` / `color_in` MUST treat null as "inherits, can't compare"
  and emit `Pass` (you can't fail a rule the entity didn't opt into).
- The `set_color` fix that supplies `aci` MUST clear any true-color and
  vice-versa - never leave stale fields.

## 5. Length / area / radius are conditional

- `Length` is only meaningful for `Line`, `Polyline` (open or closed),
  `Polyline2d`, `Arc`, `Spline`. For `Polyline` we use `Polyline.Length`,
  not `GeometricExtents` distances.
- `Area` is only meaningful for closed `Polyline`, `Region`, `Hatch`,
  `Circle`. The collector returns `null` if the polyline is open.
- `Radius` is only meaningful for `Circle`, `Arc`. For `Ellipse` use a
  separate primitive (don't shoehorn).

A check that depends on a field MUST emit `Pass` (not `Fail`) when the
field is null on the candidate entity. A `length_at_least` rule scoped
to `Polyline` should not detonate when it accidentally hits a `Hatch`
through bad scope.

## 6. Regex compilation is hot - compile once per rule

The same `layer_matches` / `text_matches` regex runs against every
candidate entity. Build a `Regex(pattern, RegexOptions.Compiled |
RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)` per rule
when the rule is loaded, and reuse it. Don't recompile per entity.

## 7. Doc-level checks need a **separate** doc snapshot

`acad.validators.doc_summary` returns:

- per-DXF-type counts (`{ Line: 1234, Circle: 89, Polyline: 256, ... }`),
- layer table names,
- block table names,
- text style names,
- INSUNITS / drawing units,
- LimMin / LimMax extents.

This is a single, cheap pipe call separate from `collect_entities`.
Doc-level checks MUST consume this snapshot and never enumerate model
space themselves.

## 8. Violation messages must be **agent-actionable**

A violation message is read by an LLM. Bad: `"layer mismatch"`. Good:
`"entity #34A2 (Line on layer M-WALLS, length 4221.5 mm) violates
arch.walls.on-walls-layer: expected layer 'WALLS', got 'M-WALLS'"`.

The engine MUST always include in every violation:

- `ruleId`
- `severity`
- `entityHandle` (or `null` for doc-level)
- `dxfType`, `layer` (or `null` for doc-level)
- `expected` (the rule's expectation as a short string)
- `observed` (what the engine actually found)
- `fixAvailable: bool`

## 9. **Last-report cache** has a single key: the active document handle

`list_violations` returns the most recent report. The cache MUST be keyed
by `Document.Database.UnmanagedObject` (or similar stable doc id). If the
user opens a different DWG, the cache is invalidated. Returning a stale
report for a different drawing is a correctness bug - it leads to
`auto_fix_violations` operating on the wrong drawing entirely.

## 10. `compare_to_baseline` opens the baseline **side-by-side**

The baseline DWG is opened via `Database.ReadDwgFile` into a side-by-side
`Database` instance (NOT through `DocumentManager.Open`, which would
flash a window and reset the user's UI focus). The baseline database
must be `Dispose()`-d in a `using`. We compare by **handle equality** for
entities that exist in both, by **handle delta** for entities that are
new / removed. We never `WblockClone` the baseline into the active doc.

## 11. Fix primitives are idempotent and self-checking

Every fix MUST:

1. Verify the precondition still holds (e.g. `move_to_layer` re-reads the
   entity layer; if it's already on the target layer, return
   `outcome: already_satisfied`, do nothing).
2. Validate inputs (target layer name passes
   `SymbolUtilityServices.ValidateSymbolName`; ACI in `[0..256]`).
3. Use `entity.UpgradeOpen()` once, mutate, do NOT downgrade back.
4. Touch only the documented properties - never reach for
   `entity.Properties[...]` shortcuts that bypass the AutoCAD
   notification system.

## 12. Standards are presets, not magic

`validate_against_standard` resolves a `standardId` to a list of rule ids
and delegates to `validate_drawing`. There is no separate "standards
engine". A standard preset lives in
`src/AcadMcp.Backend/Validators/Standards/<name>.yaml`:

```yaml
id: iso-cad-baseline
name: ISO baseline CAD hygiene
rules:
  - general.layers.no-zero-named-entities
  - general.layers.no-defpoints-geometry
  - general.units.must-be-mm
```

Keep them small (< 30 rules). If you need 100 rules in a "standard",
that's two standards.
