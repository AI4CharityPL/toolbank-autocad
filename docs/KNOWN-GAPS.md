# Known gaps — what does not work, what was withheld, what is untested

Running list. Everything here is either **broken**, **deliberately not shipped**, or **shipped
but never verified**. Kept separate from [COVERAGE-ROADMAP.md](COVERAGE-ROADMAP.md), which is
about capability that was never attempted; this is about work already touched.

Ordered by what would hurt a real project first.

---

## A. Broken or unreliable

### ~~A1. `hatches.draw_hatch_by_boundary` / `hatches.apply_material_preset_by_point`~~
**Fixed 2026-08-04, verified 10/10.** Two independent causes, both of which
[rule 26](engineering-rules/26-acad-api-traps.md) trap 11d already described — as mitigations
the code was said to perform. Neither was in the code.

1. **`Editor.TraceBoundary` reads its seed in the current UCS.** Arguments in this codebase are
   WCS (rule 43), so the seed was silently offset by whatever the current UCS happened to be.
   With the UCS at world the two agree, which is why it passed every casual test.
2. **The region must be visible in the current view.** Off-screen geometry yields an empty
   result, not an error — indistinguishable from "your geometry is not closed".

Measured by varying them separately; only *view on region* **and** a transformed seed succeeds.
The tool now transforms the seed, frames the drawing through a `ViewTableRecord` (not the
command layer, which is what broke `zoom_extents` itself), traces, and **restores the caller's
view** — verified before/after, since an agent asking for a hatch did not ask for its camera to
move. The failure message now reports the WCS seed, the UCS it was taken to, and says so
explicitly when the two differ.

Verification asserts the hatch's bounding box equals the target rectangle, not merely that a
handle came back.

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

### A5. `ucs.create_ucs_origin` reports the wrong name back
**Status:** works, reports poorly.
Called with `name: "MCP-ROT"` it **does** save the UCS under that name — proved independently,
because `viewports.set_viewport_ucs` then finds `MCP-ROT` in the table. But its own result
reports `name: "*CURRENT"`, AutoCAD's label for the unnamed current UCS, so a caller cannot
confirm from the response that the name took. Found while verifying `set_viewport_ucs`. Same
family as the fabricated `affected: 1` in `undo`: the tool is right and the report is not.
Likely affects the other `create_ucs_*` tools; not checked.

---

## B. Withheld deliberately

Each of these was written or specced and then held back rather than shipped guessed-at, per the
precedent set by the parametric constraint tools.

| Tool(s) | Category | Why |
|---|---|---|
| `refedit_begin` / `_save` / `_discard` | xrefs | Modal, stateful command sequence on the channel that produced `eInvalidInput` in `zoom_extents` and silent queueing in `undo`. Needs a supervised contract for that channel first. |
| `set_viewport_layer_override`, `list_viewport_layer_overrides`, `clear_viewport_layer_overrides` | viewports | 2025 SDK exposes `LayerTableRecord.HasOverrides` as a plain bool with no viewport argument, and none of the `Set*InViewport` / `Get*InViewport` accessors. The capability exists in AutoCAD — this is finding the right API, not a limitation. **Per-viewport freeze, the larger half, ships and works.** |
| `maximize_viewport` | viewports | `MAXACT`/`MSPACE` — command layer. |
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

1. ~~**Catalogue-vs-consumer contract test.**~~ **Done, and without the plugin test project
   this entry used to demand.** The premise was wrong: the blocker was never that the test
   needed AutoCAD, it was that the catalogues *happened to live* next to code that does. They
   are pure data — names, millimetre dimensions, prose. Moved to
   `AcadMcp.Shared/Catalogs/`, together with the name resolution, so `AcadMcp.Tests` reaches
   them and CI runs the contract on every push. The plugin keeps only what genuinely needs
   AutoCAD: turning a resolution into geometry.

   `CatalogContractTests` — 27 tests over all three catalogues (furniture, plumbing, hatches).
   Verified against the real defect: reintroducing the missing family lookup makes it fail
   with *"list_furniture_catalog publishes 26 names; insert_furniture rejects 11 of them"*,
   naming every one. The hatch catalogue also gets a cross-reference check that the other two
   cannot have — every material preset must name a pattern the pattern catalogue lists.

   The fourth defect of the four, dictionary parameters advertised as arrays, is schema-level
   rather than catalogue-level and is covered by `SchemaContractTests`.
2. **Invalid-argument coverage.** `FullToolAuditTests` calls every tool with **empty** arguments.
   That blind spot hid both the empty input schemas and the exception that killed the server —
   two tools failed on *session state*, not on arguments.
3. **Lint forbidding bare `catch { }`.** Two defects survived precisely because a silent catch
   swallowed the signal; one of them was introduced mid-sweep and only found through added
   logging.
4. **Automated licence scan** (`dotnet-project-licenses`, `pip-licenses`).
   [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) is hand-maintained and says so.
5. **`mypy --strict` on the vision sidecar is advisory, not blocking.** `pyproject.toml`
   has declared `strict = true` since the sidecar was written, but nothing ran it, so it
   drifted to **26 errors**. Most are missing return annotations on FastAPI route handlers,
   which is not the mechanical fix it looks like — FastAPI derives response serialisation
   from the return annotation. Two are substantive: `app.py` passes a `str` where
   `ArchitectReviewResponse.verdict` wants a `Literal`, and `main()` is annotated as
   returning a value it never returns. The CI step runs with `continue-on-error: true`;
   fixing these and removing that flag is the task.
6. **No plugin-side CI is possible at all.** Worth stating next to item 1: runners have no
   AutoCAD and Autodesk does not redistribute the managed assemblies, so `AcadMcp.Plugin`
   and `Companion.Host` are excluded via `src/AcadMcp.NoAcad.slnf`. A green CI check on a
   plugin change means the server still compiles and nothing more. Every plugin change
   needs a human with AutoCAD open — which is why the PR template asks which case applies.

---

## D. Release work not done

From the original public-release plan; phases 1–2 landed, the rest did not.

- ~~**CI/CD — `.github/` does not exist at all.**~~ **Done.** `ci.yml` (build, test,
  manifest sync, whole-tree gate, Python sidecar), `codeql.yml`, `dependabot.yml`,
  `CODEOWNERS`, three issue forms, a PR template, plus `SECURITY.md`, `CONTRIBUTING.md` and
  `CODE_OF_CONDUCT.md`. Caveats above: the plugin is out of CI's reach (C6) and mypy is
  advisory (C5).
- ~~**`PATTERN.md`**~~ **Written.** Eleven sections, each a claim with the failure that
  produced it, plus a checklist. Section E below is its source material and now duplicates
  it; E stays as the working record, `PATTERN.md` is the version for people who do not use
  AutoCAD.
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
