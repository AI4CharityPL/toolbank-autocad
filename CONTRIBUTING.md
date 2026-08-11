# Contributing

Thank you for looking. This project is MIT and meant to be useful to anyone wrapping a
heavy desktop application in MCP, not only to people who draw in AutoCAD.

## The one structural thing to know

**CI cannot verify the AutoCAD plugin.**

`AcadMcp.Plugin` and `AcadMcp.Companion.Host` reference `acmgd.dll`, `acdbmgd.dll` and
`accoremgd.dll` from a local AutoCAD install. Autodesk does not redistribute those, and no
GitHub runner has AutoCAD, so both projects are excluded from CI through
`src/AcadMcp.NoAcad.slnf`.

So a green check on a plugin change means the *server* still compiles. It says nothing about
whether the plugin works. Someone with AutoCAD open has to look. If that someone is you,
say so in the pull request and paste what you saw.

Everything else — tool schemas, the generator's parameter contract, manifest/code agreement,
validator rules, the Python sidecar — is covered by CI.

## Getting set up

```bash
pwsh scripts/setup.ps1
```

That detects your AutoCAD install, restores, builds and tells you what is missing. Then:

```bash
pwsh scripts/deploy-plugin.ps1
```

and `NETLOAD` the built DLL in AutoCAD. `scripts/deploy-plugin.ps1` defaults to Release and
warns if the binary is older than the sources — it did not always, and a stale April build
once cost an afternoon of debugging behaviour that had been fixed weeks earlier.

Before every commit:

```bash
pwsh scripts/pre-commit.ps1 -All
```

Under 10 seconds warm. Install it as a hook with `-Install`.

## Adding a tool

```bash
pwsh scripts/new-category.ps1 -Name mycategory
```

Then, in order, because the order is what makes it work:

**1. Settle the contract before writing code.** Write down what the tool does at the edges —
what happens with no argument, with an unknown name, with a value that is legal but absurd —
and put it in `docs/engineering-rules/` if it generalises. This is not ceremony. The `ucs`
category passed every test first time because
[rule 43](docs/engineering-rules/43-coordinate-systems.md) existed before line one of code.
The `viewports` category needed three attempts because it did not.

**2. Name it for the task, not the API.** An agent finds tools from a plain-language
request. `add_annotation_scale` is findable; `set_object_context_collection` is not. Give
every `[McpTool]` an `Intent =` array with **both English and Polish** phrasings — the gate
enforces its presence, and discovery quality depends on it.

**3. Fail loudly.** An unknown linetype name is an error, not a silent fall back to
`Continuous`. A plausible default is worse than a refusal, because the caller cannot tell it
happened. The worst defect found in this repository was a colour filter that quietly matched
every entity in the drawing and returned a perfectly reasonable list of handles.

**4. Keep the catalogue and the tool in agreement.** If a discovery tool advertises a
parameter shape, the action tool must accept exactly that shape. Four separate defects in
one sweep were a catalogue promising what the tool then refused. Nothing tests this yet — it
is the top item in [docs/KNOWN-GAPS.md](docs/KNOWN-GAPS.md) section C — so for now it is on
you.

**5. Update the manifest.** Every `Categories/<Name>/` needs a matching
`toolbank-manifests/acad-<name>.json`. `scripts/check-manifests.ps1` enforces it, and the
category description is the only thing an agent reads before choosing you, so write it as
prose that also says what the category does *not* cover.

**6. Verify by looking.** Then attach the screenshot.

## Why we keep saying "look at it"

Three of the worst defects ever found here returned completely healthy JSON:

- `draw_revcloud` returned a valid `AcDbPolyline` handle. It had drawn a plain rectangle.
- `dimension_diametric` returned a valid handle. It labelled every circle `Ø0`.
- `select_by_color` returned a plausible list of handles. It was the entire drawing.

All three reported success. A test asserting `result.handle != null` passes on all three.
The return code is not the evidence; the drawing is.

## Things that will bite you

Collected the hard way, and expanded on in `docs/engineering-rules/`:

- **AutoCAD system variables are `Int16`.** `SetSystemVariable("BACKGROUNDPLOT", 0)` throws
  `eInvalidInput`; you need `(short)0`. A `catch { }` hid this one for two debug cycles.
- **Never write `catch { }`.** Twice this cost a day. If you truly must swallow, log first.
- **The command layer is unreliable.** `Editor.Command` and `SendStringToExecute` produce
  `eInvalidInput` or queue silently and return before anything happened. Prefer the object
  model. Where a tool genuinely has no alternative, it must say `queued: true` rather than
  invent an affected count.
- **Localised AutoCAD renames the support-file symbols.** On a Polish install `DASHED` is
  `KRESKOWA` and `CENTER` is `ŚRODEK` — but `CENTER2` stays English while `CENTERX2`
  translates. It has to be a lookup table; any rule you invent will be wrong.
- **`Viewport.Number` is unassigned until AutoCAD activates the layout.** Logic keyed on it
  works on hand-made viewports and fails on API-made ones — exactly the ones an agent
  creates.
- **Backend DTOs silently drop fields the plugin sends.** If a result field vanishes between
  plugin and client, check the backend record first. This happened three times.
- **Run one experiment, not ten guesses.** When the field syntax was unknown, ten candidate
  expressions went into a single run. Guessing one at a time would have cost ten AutoCAD
  restarts.

## Pull requests

Branch from `main`, one topic per PR. Conventional-commit subjects
(`feat(annotative):`, `fix(xrefs):`). `CHANGELOG.md` must be updated when `src/` changes —
the gate blocks the commit otherwise.

Write the commit message for someone who will read it in a year without the diff in front of
them. Say what was wrong, not only what is now right.

## Blocked only on a file — good first contribution

Some tools are fully coded and reconnaissance-complete, but withheld because nothing on the
machines that built this bank could prove them correct — no return code is shipped as evidence
here, so an unverifiable tool stays unshipped rather than guessed at. If you have the file, this
is the fastest way to add real tools:

- **`acad-pointclouds`, 12 tools** — need one `.rcp`/`.rcs` scan file. Autodesk publishes free
  ReCap sample scans; any single-scan file is enough. Details: [KNOWN-GAPS.md §B](docs/KNOWN-GAPS.md).
- **`set_material_map`, `set_material_mapping`, `load_material_library`, 3 tools** — need a
  texture image and one `.adsklib`. Details: [KNOWN-GAPS.md §B](docs/KNOWN-GAPS.md).
- **`attach_dgn_underlay`** — needs one real (non-seed-template) `.dgn`. Details:
  [KNOWN-GAPS.md §B](docs/KNOWN-GAPS.md).

Each entry says exactly what was measured, why the API is not the blocker, and what "unblocked"
looks like — bring the file, run the same probe, and these are candidates for the next release.

## Reporting instead of fixing

Entirely welcome, and the issue templates ask the questions that matter. A tool defect
report with a screenshot is more useful than most patches.

## Licence

Contributions are MIT, per [LICENSE](LICENSE). If you add a dependency, add it to
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) — and check its licence. The optional
`[ml]` extra already pulls AGPL-3.0 code, which is why it is optional, lazily imported and
never installed by CI.
