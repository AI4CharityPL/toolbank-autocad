# Known gaps — what does not work, what was withheld, what is untested

Running list. Everything here is either **broken**, **deliberately not shipped**, or **shipped
but never verified**. Kept separate from [COVERAGE-ROADMAP.md](COVERAGE-ROADMAP.md), which is
about capability that was never attempted; this is about work already touched.

Ordered by what would hurt a real project first.

---

## A. Broken or unreliable

### A1. `hatches.draw_hatch_by_boundary` / `hatches.apply_material_preset_by_point`
**Status:** fail on valid input.
`TraceBoundary found no closed region around seed point` for a seed plainly inside a rectangle.
This is trap 11d in [rule 26](engineering-rules/26-acad-api-traps.md) — TraceBoundary needs the
region **visible on screen**. Rule 26 states that `apply_material_preset_by_point` mitigates it
by zooming first; `zoom_extents` was broken until this sweep fixed it, so **that mitigation may
never have worked**. Needs its own investigation, not a guess.

### A2. `modify.undo` / `modify.redo`
**Status:** honest but arguably should not ship.
Now correctly report `{queued: true}` instead of a fabricated count. Still built on
`SendStringToExecute`, the queued-command mechanism that
[PHASE-7-STATUS.md](PHASE-7-STATUS.md) records as having deadlocked checkpoint rollback — which
is why that moved to `.dwg` snapshots. **Decision needed:** withdraw them like the parametric
tools, or keep them as an explicitly best-effort escape hatch.

### A3. `xrefs.set_xref_clip_display`
**Status:** works, but the name over-promises.
`XCLIPFRAME` is a **drawing-wide** system variable, not per-insert. The tool takes a handle and
logs that the setting is global, but a caller reading the signature will reasonably expect it to
scope to one reference. Either rename it or drop the handle argument.

### A4. Vision category (9 tools)
**Status:** never verified.
`vision_health` / `vision_version` correctly report the sidecar is unreachable. The other seven
have not been run **at all** — they need the Python sidecar started and at least one provider API
key. Untested is not the same as working.

---

## B. Withheld deliberately

Each of these was written or specced and then held back rather than shipped guessed-at, per the
precedent set by the parametric constraint tools.

| Tool(s) | Category | Why |
|---|---|---|
| `refedit_begin` / `_save` / `_discard` | xrefs | Modal, stateful command sequence on the channel that produced `eInvalidInput` in `zoom_extents` and silent queueing in `undo`. Needs a supervised contract for that channel first. |
| `set_viewport_layer_override`, `list_viewport_layer_overrides`, `clear_viewport_layer_overrides` | viewports | 2025 SDK exposes `LayerTableRecord.HasOverrides` as a plain bool with no viewport argument, and none of the `Set*InViewport` / `Get*InViewport` accessors. The capability exists in AutoCAD — this is finding the right API, not a limitation. **Per-viewport freeze, the larger half, ships and works.** |
| `maximize_viewport` | viewports | `MAXACT`/`MSPACE` — command layer. |
| `set_viewport_ucs` | viewports | Was waiting on `acad-ucs`. **That now exists — this is buildable today.** |
| `set_viewport_annotation_scale` | viewports | Was waiting on Phase 1.5. **`acad-annotative` now exists and is verified — this is buildable today.** |
| `ucs_from_face` | ucs | Needs subentity picking; no non-interactive form. |
| `ucs_icon` | ucs | Display-only; nothing an API caller can observe. |
| `set_xref_demand_load` | xrefs | Specced in the roadmap, not built. Low value. |
| Parametric constraint application | parametric | Pre-existing. Every attempt failed with `eInvalidInput` from `Editor.Command` across four approaches; implementation preserved unregistered in `ParametricPluginTools.cs`. |

**Note on `overriddenLayers`:** `viewports.get_viewport_info` reports it from the bool
`HasOverrides`, so it means *"this layer is overridden in some viewport"*, not *"in this one"*.
The XML comment says so; the field name still reads more precise than it is.

---

## C. Missing test machinery

Four defects in this sweep were a **discovery tool advertising what the action tool refuses**
(dictionary params described as arrays; three catalogues). Nothing tests that the two agree.

1. **Catalogue-vs-consumer contract test.** Would have caught all four at once. **Cannot live in
   `AcadMcp.Tests`** — the catalogues are static data inside the plugin assembly, which needs the
   AutoCAD managed references. **A plugin-level test project is needed and does not exist.**
   This is the single highest-value item in this document.
2. **Invalid-argument coverage.** `FullToolAuditTests` calls every tool with **empty** arguments.
   That blind spot hid both the empty input schemas and the exception that killed the server —
   two tools failed on *session state*, not on arguments.
3. **Lint forbidding bare `catch { }`.** Two defects survived precisely because a silent catch
   swallowed the signal; one of them was introduced mid-sweep and only found through added
   logging.
4. **Automated licence scan** (`dotnet-project-licenses`, `pip-licenses`).
   [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) is hand-maintained and says so.

---

## D. Release work not done

From the original public-release plan; phases 1–2 landed, the rest did not.

- **CI/CD — `.github/` does not exist at all.** No workflow, no dependabot, no CODEOWNERS,
  SECURITY.md, CONTRIBUTING.md or issue templates. The MCPNexus repo has all of it; this one has
  none. Biggest formal gap before going public.
- **`PATTERN.md`** — the "how to wrap a thick desktop app in MCP" write-up. Highest-reach
  artefact in the repository and still unwritten. Material for it accumulated all through this
  sweep (see E below).
- **Docs reorganisation** — split `docs/engineering-rules/` (universal) from a
  `docs/case-studies/hospital-2026/` (one project's history). `README.md` is 21 KB with no
  screenshots despite `assets/report/` holding finished renders.
- **`Megasystem` placeholder branding** still in ~15 files (prose only; every shipped artefact
  was fixed in the licensing commit).
- **MCPNexus (the Python repo)** — version sync 1.0.0→1.0.6, the `safety.py` blocker,
  cross-repo install docs. None started; that repo is still private while `mcpnexus` is on PyPI
  with 404 links.

---

## E. Findings worth carrying into `PATTERN.md`

Not gaps — the reusable lessons, recorded here so they are not lost when the commits scroll away.

1. **Three of the worst defects were invisible in the JSON.** `draw_revcloud` returned a healthy
   `AcDbPolyline` handle while drawing a plain rectangle; `dimension_diametric` returned a valid
   handle while labelling every circle `Ø0`; `select_by_color` returned a plausible handle list
   containing the entire drawing. **Verify by looking at the output, not the return code.**
2. **`Viewport.Number` is unassigned until AutoCAD activates the layout.** Any logic keyed on it
   works on hand-opened viewports and fails on API-created ones — i.e. exactly the ones an agent
   makes. Cost three restart cycles.
3. **Localised AutoCAD renames the support-file symbols.** On a Polish install `DASHED` is
   `KRESKOWA`, `CENTER` is `ŚRODEK`. It must be an alias **table**, not a rule: on that same
   install `CENTER` and `CENTERX2` are translated but `CENTER2` is not.
4. **Settle the contract before writing the code.** `acad-ucs` passed 13/13 first time because
   [rule 43](engineering-rules/43-coordinate-systems.md) existed before line one. `acad-viewports`
   needed three attempts because it did not.
5. **A silent `catch {}` will cost you a day.** Twice.
