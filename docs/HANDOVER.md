# Handover — ToolBank-AutoCAD

**For the next agent, on any machine or account.** Written 2026-08-11. Everything below has been
run on the machine it was written on; where something is untested elsewhere it says so.

---

## 1. What this is, in one paragraph

An MCP tool bank that drives AutoCAD 2025. A C# **plugin** loads inside AutoCAD and does the work;
a **backend** process per category speaks MCP over stdio and forwards to the plugin over a named
pipe. There are **686 tools in 50 categories**. The product language is **English**; every tool
also carries Polish intent phrases so a Polish-speaking router can find it.

The distinguishing thing about this project is not the tool count. It is that **every tool has
been proven correct against live AutoCAD by a check that would fail if the tool were wrong**. A
return code is never the evidence. If you take one thing from this document, take §6.

---

## 2. Read these first, in this order

| # | File | Why |
| --- | --- | --- |
| 1 | **this file** | orientation and what is left |
| 2 | `docs/engineering-rules/26-acad-api-traps.md` | **19 sections of measured API traps.** Each one cost at least one build cycle. §12c, §15, §16, §17, §18, §19 are the expensive ones |
| 3 | `docs/engineering-rules/41-new-category-flow.md` | the working loop, with the numbers that justify it |
| 4 | `docs/COVERAGE-ROADMAP.md` | what exists, what is struck and why, what is left. **The totals table near the end is the map** |
| 5 | `CHANGELOG.md` (top ~200 lines) | the last ten tranches, each with its findings |
| 6 | any `scripts/verify-*.py` | what "verified" means here. `verify-data.py` and `verify-geo.py` are the best examples |

Do **not** start by reading source. Read rule 26, then the roadmap.

---

## 3. Environment

Needed:

* **AutoCAD 2025**, English or Polish UI (this machine runs Polish; layout names come back as
  `Układ1`, which is why nothing matches on layout names)
* **.NET 8 SDK**
* **Python 3.9+** (only `json`, `subprocess`, `threading` — no packages to install)
* **PowerShell** for the scripts; a POSIX shell works for git and dotnet

Paths that are **machine-specific** and will differ on another system:

* the repo is at `C:\Users\DELL\Dev\autocad-mcp` here. Nothing in the build or the verification
  suite depends on that any more — but grep for it once after cloning, in case something new crept in.
* the AutoCAD managed assemblies (`acmgd.dll`, `acdbmgd.dll`, `accoremgd.dll`, `acdbmgdbrep.dll`)
  are referenced by `HintPath` from `$(AcadInstallPath)`. That is **auto-detected** — run
  `scripts/detect-autocad.ps1` on a new machine, which probes the registry, filesystem and COM
  ProgIDs and also tells you if the install is LT (LT has no .NET plugin support, so nothing here
  would work). Override with the `AcadInstallPath` MSBuild property if detection is wrong. A bad
  path shows up as hundreds of missing-type errors, so check this before believing any other
  build failure.
* the plugin deploys to
  `%APPDATA%\Autodesk\ApplicationPlugins\AcadMcp.bundle\Contents`, which `scripts/deploy-plugin.ps1`
  resolves for the current user.

**Verification portability was fixed on 2026-08-11.** Every `verify-*.py` used to import a helper
from a session-scratch temp directory, which existed on exactly one machine for one user — the
whole suite was unrunnable by anyone else. `scripts/mcpcall.py` now lives in the repo and resolves
the backend from its own location; override with `ACADMCP_BACKEND_EXE` if you need to.

---

## 4. The loop, every time

```bash
# 1. PROBE the whole category against the compiler.  No AutoCAD needed, so this is free.
#    Write src/AcadMcp.Plugin/Tools/_ApiProbe.cs guarded by #if ACADMCP_API_PROBE, then:
dotnet build src/AcadMcp.Plugin/AcadMcp.Plugin.csproj -c Debug -p:DefineConstants=ACADMCP_API_PROBE
#    Read the errors WITH LINE NUMBERS.  CS0219 "assigned but never used" means the name EXISTS.
#    Delete the probe afterwards.

# 2. Write the WHOLE category in one pass: plugin handler, DTOs both sides, backend tools, tests.
dotnet build src/AcadMcp.sln -c Debug

# 3. Regenerate the manifest and the reference, then check the descriptions.
dotnet run --project src/AcadMcp.Backend -c Debug -- --category <name> --regenerate-manifest
python scripts/generate-tools-reference.py
python scripts/audit-tool-descriptions.py      # must report 0 objective failures

# 4. Write scripts/verify-<name>.py WITH ITS CONTROLS before deploying.  See §6.

# 5. ONE deploy.  This kills AutoCAD; you are authorised to do that, the user restarts it.
dotnet build src/AcadMcp.sln -c Release
.\scripts\deploy-plugin.ps1 -Kill
#    ... ask the user to start AutoCAD, then:
python scripts/verify-<name>.py

# 6. Fold docs, roadmap, changelog and the commit into the same turn as the passing run.
.\scripts\pre-commit.ps1 -All                  # must say "OK - safe to commit"
```

**Why this order.** Measured over ten categories: probe-first costs **one** AutoCAD restart per
category; build-first cost **three**, and `acad-lisp` shipped 5 of 12 tools that way. The scarce
resource is restart cycles, because each one interrupts the user.

---

## 5. Working with the user

They run AutoCAD; you may close it whenever a deploy needs it. The rhythm is: you deploy, you say
*"please start AutoCAD"*, they reply *"autocad odpalony"*, you run the verification. They write in
Polish and are content with English replies.

**Do not** ask them to type things into AutoCAD unless there is no alternative — it has happened
twice and both times it was genuinely necessary (`ACADMCP_CMDTEST`).

---

## 6. What "verified" means here — the important section

A tool that reports success while doing nothing is worse than one that is absent. Nine times in a
single session a check passed while proving nothing. Every one was caught by a control that had to
**fail** if the tool were wrong. Learn these shapes:

| trap | real example |
| --- | --- |
| **the test shape cannot discriminate** | a plane through a 100 **cube** and one 5000 units away both answer 400, because a cube's cut and its silhouette are the same square. A **sphere cut off-centre** tells them apart |
| **the assertion is weaker than the claim** | checking `\|y\| = 1` cannot see a **sign** error. Assert the vector |
| **the check never ran** | a handle came back under a different key, an `if handle:` guard skipped thirteen checks, and the script reported 33/33. **Make preconditions their own assertion** |
| **the refusal fired for the wrong reason** | validation ran after a lookup that itself threw, so "bad arguments are refused" passed on an unrelated error |
| **an omitted value became a valid one** | a non-nullable `double` turned a missing latitude into `0.0` — the Gulf of Guinea. **Make anything with a plausible zero nullable** |
| **symmetry hides a transposition** | a square grid passes even if rows and columns are swapped. Use **3×2, 300×200, 100×80×60** |
| **one entity cannot show isolation** | a tool writing globally passes every single-entity test. Always include **a second object that must NOT change** |
| **the neighbour got clobbered** | channels and structs are rebuilt on write, so changing one silently resets another. Assert the **untouched** ones |
| **the loud failures were symptoms** | six checks failed looking like conversion bugs; the one that named the cause was the quietest line in the list |

Practical rules that follow:

* **Arithmetic beats inspection.** Pappus, `2πr`, `V − E + F − R = 2(S − G)`, a 100×50 rectangle
  having area 5000 and perimeter 300. A number computed outside the tool is the strongest evidence
  there is.
* **Every check needs a negative half** — something that must be found *and* something that must not.
* **Read back through a different route** than the one that wrote. `Entity.Material` (name) after
  setting `MaterialId`. `list_dictionaries` after `SetAt`.
* **A guard built on an unchecked assumption is worse than no guard** — it rejects correct results
  while looking rigorous. This happened three times with mesh smoothing; the fix was to measure
  first, and once to delete the guard and record why.
* **Do not ship what you cannot demonstrate.** Several tools are withheld for exactly this, and
  each one says so in the roadmap.

---

## 7. What is left

**686 built.** Roughly 19 identified as remaining, and nearly all of it is gated rather than hard —
this session cleared everything that wasn't.

**2026-08-11: `acad-lisp`'s `netload_assembly` built, 11/11 live, WITH THE USER'S EXPLICIT
GO-AHEAD** for this specific capability — dynamic assembly loading is a materially different risk
from evaluating LISP or queuing drawing commands, and this tranche asked before writing it, after
an earlier attempt was blocked by this session's own safety classifier. Loading a `.dll` is the
`_.NETLOAD` **command**, not a LISP form, so it reuses the `run_command_sequence`
`[CommandMethod]` bridge rather than the LISP-eval one, with `FILEDIA` forced to 0 — the same
file-dialog precedent already fixed for `run_script_file`'s `SCRIPT`. `DynamicLinker.LoadModule`
needs a command context exactly like `Editor.Command`; and `DynamicLinker.GetLoadedModules`/
`IsModuleLoaded` do **not** report netloaded .NET assemblies at all, so "already loaded" and
"loaded" are both read back through `AppDomain.CurrentDomain.GetAssemblies()` instead. Verified
against a fully-controlled test fixture built for this check (`scripts/fixtures/
netload-test-assembly`), never an ambient or found DLL — the DynamicLinker blind spot proven
directly, not just cited. `acad-lisp` is now complete: 10 of 12 originally planned, with
`run_script_file` withdrawn and `define_command_alias` struck as the only two decisions rather
than gaps.

**2026-08-11: `acad-lisp`'s escape-hatch trio built, 27/27 live — `eval_lisp`, `load_lisp_file`,
`list_loaded_lisp`, with the user's explicit go-ahead** (arbitrary LISP evaluation is a materially
different risk from the bounded `run_command_sequence`). Architecturally simpler than that bridge:
the command line accepts raw LISP directly when typed, so no `[CommandMethod]`/`GetString` relay
is needed - just `SendStringToExecute` queuing a wrapper that reads the caller's expression from a
request file (never embedded in the queued text, so no LISP string-escaping to get wrong) and
writes the result to a response file. One real defect found live: AutoLISP's `(read)` takes a
STRING, not a file object - the fix is `(read (read-line file))`. Rule 26 §24. Proven against real
content, not synthetic one-liners: `load_lisp_file` loads AutoCAD's own `afact.lsp` sample and the
proof it worked is calling the loaded `fact1(5)` afterward and getting `120` back.

**2026-08-11: `acad-underlays` built, 5 tools, 42/42 live**, plus `acad-lights.create_web_light`,
7/7. Both were "blocked on a file the user must supply" until the user pointed at AutoCAD's own
install tree instead of supplying anything: real DGN seed templates, a Sheet Set publish DWF and
IES photometric fixtures were already sitting there, unused. `attach_dgn_underlay` stays withheld
— the only `.dgn` files found are empty export seeds, not real content — but `attach_dwf_underlay`
proves the shared code path is correct. Worth remembering for `4.5 acad-pointclouds`: no `.rcp`/
`.rcs` turned up anywhere on this machine, so that one is still genuinely blocked, not just
under-searched.

**2026-08-11: `acad-images` (raster half) built, 7 tools, 48/48 live.** `attach_image`,
`list_images`, `detach_image`, `clip_image`, `set_image_adjust`, `set_image_frame`,
`set_image_path`. `set_image_transparency` and `reorder_image_draworder` struck — see
KNOWN-GAPS §B and rule 26 §20-21 for two real defects found and fixed live (a squared aspect-ratio
bug, and a fatal `AssociateRasterDef`/`ForRead` crash that needed an AutoCAD restart mid-session).

**2026-08-11: `acad-lisp`'s `run_command_sequence` built, 17/17 live**, via a queued
`[CommandMethod]` bridge that gives `Editor.Command` a genuine command context (rule 26 §15/§22).
`run_script_file` was built on the same bridge and withdrawn after two measured fix attempts —
see rule 26 §22.

**2026-08-11: 6.1 `set_render_environment` (fog) reconnaissance completed and struck**, not
pending — see KNOWN-GAPS §B. `RenderEnvironment` has no found persistence route, and even a
working one would be unverifiable: nothing this bank can produce renders fog. Sun turned out to
already be shipped (`acad-lights`), undercounted in this doc until this pass found it.

### Buildable now — nothing blocking

| item | tools | notes |
| --- | ---: | --- |
| a stale-accounting sweep | — | Phase 1's built column was wrong by 13 and a tool recorded as existing never did. This session found the roadmap's own total two tools stale before touching anything. **Assume other phases are stale too**; count the manifests, do not trust the plan |

### Blocked on a file the user must supply

| item | tools | needs |
| --- | ---: | --- |
| 4.5 `acad-pointclouds` | 12 | one `.rcp` or `.rcs`. The API is fine — reconnaissance is in the roadmap. ReCap is not installed, and none was found anywhere on this machine either (searched 2026-08-11) |
| `attach_dgn_underlay` | 1 | one real (non-seed) `.dgn` — every one found on this machine is an empty export template. `attach_dwf_underlay` (built) proves the code path works |
| material maps / library | 3 | a texture image and an `.adsklib` — checked 2026-08-11 alongside the DGN/DWF/IES search, neither exists anywhere on this machine |

### Blocked on something harder

| item | tools | the actual blocker |
| --- | ---: | --- |
| `insert_field_sheet_set_property` | 1 | `AcSm` fields resolve against the sheet set open in the **Sheet Set Manager**, which nothing here can establish. Needs the `SHEETSET` command or `AcSmSheetSetMgr` COM. **This is the last item in Phase 1.** Note the recorded blocker was wrong for months — see the changelog |
| `acad-lisp`: `run_script_file` | 1 | **withdrawn, not blocked** — built on the working command-context bridge and still drew nothing from a one-line `.scr`; two measured fix attempts, both wrong. Rule 26 §22 |

### Struck — do not build these

`render_view`, `render_region`, `render_to_file` and the render settings tools: image capture is
**already** `files.export_file`, and photorealistic rendering needs the `RENDER` command which
cannot be driven from here. `acad-animation` (6.2) has no managed API at all. `set_view_category`,
`create_camera`, `list_cameras`, `set_north_direction`, `insert_map_image`, `quick_select_by_property`,
`select_previous`, `define_command_alias` — all struck with reasons in the roadmap. **Read the
reason before resurrecting any of them.**

---

## 8. Things that will bite you

* **`Editor.Command` does not work** from a tool handler — application context, not command
  context (rule 26 §15). But `Editor.SelectLast` does. Do not generalise from one member to a type.
* **Some properties do not stick until the object is in the database** (§18). `Light.HasTarget`
  reverts *silently*; `GeoLocationData.CoordinateSystem` throws. Append first, then set anything
  relational.
* **`Database.GeoDataObject` throws** when there is no geo data rather than returning null (§16).
* **Structs are read and written whole** — material channels, `SkyParameters`. Mutating what a
  property hands back does nothing.
* **One probe round is not evidence of absence** (§12c). Four times a wrong guess nearly struck a
  buildable tool. Guess names generously and in bulk; if several fail, suspect the **namespace**
  (`GraphicsInterface`, not `DatabaseServices`) before concluding absence.
* **Bash heredocs mangle backslashes and `\n`.** This cost three separate repairs in one session.
  For anything with escapes, write a patch script with the Write tool and run it.
* **Two open drawings mean two backends can be on different documents** (§13a). Every verification
  starts with a fresh-drawing + cross-session probe. Keep that.

---

## 9. State of the repo right now

* branch `fix/tool-input-schemas`, everything committed, working tree clean
* `pre-commit.ps1 -All` → **0 errors**, 115 items
* `dotnet test` → **266 passed**
* `audit-tool-descriptions.py` → **0 objective failures, 0 Polish gaps**
* 50 manifests in `toolbank-manifests/`, matching `docs/TOOLS-REFERENCE.md` — **686 tools**

**Not done and deliberately left to the user:** the npm publish (needs their 2FA), and revoking
the PyPI token at the end of the project. The token must never be written into either repository.
The OpenAI key was granted for this project only and is to be rotated.

---

## 10. Starting a new agent

`docs/AGENT-PROMPT.md` holds a copy-pasteable prompt that points a fresh agent at this document
and sets the verification standard before it writes anything. Use it rather than describing the
project from memory.

---

## 11. If you only do one thing

Pick a category from §7, **probe it against the compiler before writing anything**, write the
verification with its controls before you deploy, and deploy once. That single habit is the
difference between the categories that shipped clean and the one that shipped 5 of 12.
