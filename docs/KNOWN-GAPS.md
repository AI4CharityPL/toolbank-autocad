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

### ~~A2. `modify.undo` / `modify.redo`~~
**Withdrawn 2026-08-04.** The decision this entry asked for, settled by measurement.

Draw a circle, call `undo`, look for the entity: still there at 0.0 s, 0.5 s, 1.5 s and 3.0 s.
The queued UNDO never runs in this dispatch context. The tool was honest — `{queued: true}`
with a note saying it could not confirm anything, which was already an improvement on the
fabricated `affected: 1` it used to return. But *honest about doing nothing* is still a tool
called `undo` that does not undo, and an agent will reach for that name at exactly the moment
it most needs the drawing put back.

It is also redundant. `acad_undo_checkpoint` / `acad_restore_checkpoint` work, verified the
same afternoon: checkpoint, draw a circle, restore, circle gone
(`strategy=reopened_snapshot`).

The `[McpTool]` attributes are removed so the tools are no longer advertised; the plugin
handlers and proxy methods stay in place, exactly as the parametric constraint tools were
withheld. Re-adding two attributes brings them back if the command channel ever becomes
reliable.

### ~~A6. `acad-router` reports errors as successful results~~
**Fixed 2026-08-05, verified live at the JSON-RPC level.**
`acad_restore_checkpoint` called without `id` or `label` returns
`"[router-error] acad_restore_checkpoint requires 'id' or 'label'."` — as **content of a
successful tool call**. MCP's `isError` is not set, so a client sees success and must
string-match `[router-error]` to notice otherwise. Likely affects every router meta-tool.
This is the failure shape the whole sweep has been removing, in the one category every agent
talks to first.

### ~~A7. `acad_undo_checkpoint` ignores its own required argument~~
**Fixed 2026-08-05, verified live.**
Its schema declares `label` as **required**. Called with a different property name instead, it
succeeds and creates a checkpoint with `label='(none)'` rather than refusing. A caller who
mistypes the argument gets a checkpoint they cannot then find by label. Advertised as
required, treated as optional — the same catalogue-vs-consumer disagreement as
[section C1](#c-missing-test-machinery), but between schema and handler.

### ~~A8. Room area tags render `m?` instead of `m²`~~
**Fixed 2026-08-04, verified visually.** The string was never wrong — proper UTF-8 in the
source, read as UTF-8. AutoCAD's default text style is backed by an SHX font with no glyph for
the superscript, so it drew a question mark on every room tag.

Setting a TrueType style as *current* does not help: `DBText` takes the style it is given, and
`define_room` was giving none. It now names one. `ACADMCP-ROOM` (Arial) is created on demand,
idempotently, and used for all three tag lines.

New `tagTextStyle` argument so this is not imposed: pass your own style name and nothing is
created; pass `""` to opt out entirely and take the drawing's current style. Default `null`
gets the working behaviour.

Worth recording separately: `create_text_style` takes a TrueType **typeface name** ("Arial"),
not a file name. Passing `"arial.ttf"` succeeds and produces a style bound to a typeface that
does not exist, which renders as the fallback and looks exactly like the style was ignored.
Cost me one diagnostic cycle.

### ~~A3. `xrefs.set_xref_clip_display`~~
**Fixed 2026-08-05 by renaming to `set_clip_frame_display` and dropping the handle.**
`XCLIPFRAME` is a **drawing-wide** system variable, not per-insert. The tool takes a handle and
logs that the setting is global, but a caller reading the signature will reasonably expect it to
scope to one reference. Either rename it or drop the handle argument.

### ~~A9. `DBMOD` stays non-zero after `files.save_document_as`~~
**Resolved 2026-08-05 by investigating it. DBMOD was right; the tool's report was wrong.**

`Database.SaveAs` writes a **copy**. It is not AutoCAD's SAVEAS command and it does not re-point
the open document, which keeps its own name and its own unsaved state — and the managed API
offers no way to rename an open document. So the drawing in memory genuinely still had unsaved
changes, because it was never saved: a copy of it was written elsewhere. `publish_sheets` reading
DBMOD and refusing was correct behaviour, not a false positive, and the `allowUnsaved` argument
added to work around it is still the right escape hatch for publishing a copy.

The real defect was the report. `save_document_as` returned only `BuildDocumentInfo(doc)`, whose
`path` is the document's own name — so a caller asking to save to a path got back `Rysunek7.dwg`
and **no indication that anything had been written anywhere**. The file was there, 14,173 bytes,
unmentioned. The result now carries `savedTo`, `bytes` and the document's own path side by side,
and the description states outright that `save_document` afterwards still writes to the original.

Noted while measuring, and left open: the `isModified` field in document info is a **reflection
fallback, not a measurement**. `Document.IsModified` is not public, the lookup fails, and the
field reports `false` regardless of state — which is why it disagreed with DBMOD. It should
either read something real or stop being reported.

**Original entry, for the record:**
**Status:** found 2026-08-04 while building `publish_sheets`. Not investigated.
`publish_sheets` guards against publishing a drawing with unsaved changes, because the Publisher
reads the DWG from disk rather than from memory. The guard reads `DBMOD`, and `DBMOD` was still
`1` immediately after a successful `files.save_document_as` — so the guard refused a drawing that
had just been saved. Worked around with an explicit `allowUnsaved` argument and a message that
names this entry. Whether `save_document_as` fails to clear the flag, or `DBMOD` means something
narrower than "has unsaved changes", is unresolved.

### ~~A10. `publish_sheets` happy path is unverified~~ — **single AND multi-sheet now verified**

**2026-08-05. The original entry was wrong about the cause, and wrong about the machine.**
It said this needed "a machine with a real plot device" because "the only plot devices here are
Brak and OneNote". AutoCAD reports **ten**, including `DWG To PDF.pc3` and four `AutoCAD PDF`
variants. The blocker was never hardware. It was four separate defects in a row:

1. **`create_page_setup` was broken on every explicit call.** It threw a bare `eInvalidInput` and
   worked only through `fromLayout` — i.e. only by copying a configuration a human had already
   made by hand, which is why no test ever had a configured layout to publish. Cause was
   ordering: the validator cannot centre or scale a plot whose **type** is unset, and a fresh
   `PlotSettings` carries none. One missing `SetPlotType(Extents)` before the other three calls.
   Each validator call now names itself on failure, because a bare `eInvalidInput` out of a
   helper making four calls in a row cost a full diagnostic cycle.
2. **`PublishExecute` opened a modal "specify PDF file" dialog and waited for a human.**
   `DestinationName` was ignored, nothing was written, no error was raised, and the call never
   returned. Fixed with `DsdData.PromptForDwfName = false`. **This was found by looking at the
   screen** — there was no return value to inspect, because AutoCAD was blocked on a dialog.
3. **No diagnostic channel at all.** The Publisher keeps its own log and says nothing anywhere
   else. `DsdData.LogFilePath` is now always set and its tail is quoted into the failure message.
4. **A `DsdData` built purely in memory makes `PublishExecute` return having done nothing** — no
   file, no error, and no entry in its own log. Writing the DSD to disk and reading it back is
   what makes the Publisher act on it. `InitializeLayouts = true` was added at the same time.

**Verified working:** one layout to PDF produces a real file — `%PDF-1.7`, 3,092 bytes, valid
trailer, one page.

**Multi-sheet: fixed, and it was a fifth defect rather than the paper-size theory.** Publishing
two layouts hung for ten minutes behind a modal AutoCAD dialog — *"one or more sheets could not be
processed, the plot job was cancelled, remove the non-plottable sheets"*. The Publisher log said
exactly why: `BLAD: Nie znaleziono ukladu` (layout not found) for every sheet. The layouts existed
in the OPEN drawing but not in the FILE ON DISK, which is what the Publisher reads — and the
DBMOD guard had been warning about precisely that until `allowUnsaved:true` overrode it. The guard
was the one part of the chain that was right all along.

`publish_sheets` now reads the file on disk before publishing and refuses by name, in 0.0 s
instead of ten minutes, quoting what is actually on disk and pointing at the `save_document_as`
copy semantics as the likely cause. Verified both ways: two layouts saved to disk publish into one
4,936-byte 3-page PDF; a layout created after the save is refused without a dialog, a hang or a
file.

**And a bank-wide defect this exposed.** `PluginPipeClient.CallToolAsync` packed `timeoutMs` into
the request and left the plugin to honour it. The plugin does — until a handler is blocked on a
modal dialog, at which point it never reaches its own timeout check, never replies, and the client
`await` had only the caller's `CancellationToken` to wake it. **Every timeout in the bank was
advisory.** `publish_sheets` declares 300 s and hung for ten minutes until the process was killed
by hand. The client now enforces the timeout too, with a 2 s grace so the plugin's own better
message wins when it can produce one, and the timeout text says to look at the AutoCAD window -
because the tool cannot.

**Original entry, for the record:**
**Status:** shipped with its guards verified and its success path not.
Every refusal is verified live — empty layout list, unknown format, unknown layout, unsaved
drawing, unconfigured layouts. **A publish that actually produces a file has never succeeded
here**, because the only plot devices on this machine are "Brak" and OneNote and the test
drawing's layouts carry no page setup. `PublishExecute` answered `eNullPtr`, which names nothing;
the unconfigured-layout precondition added afterwards is the most likely cause but is **not
confirmed to be the only one**. Needs a machine with a real plot device and a configured layout.

### A4. Vision category (9 tools) — **2 verified, 1 verified, 6 blocked on backends**
**2026-08-05: the sidecar was started and every tool was called. `scripts/verify-vision.py`, 19/19.**

The entry said all nine needed "the sidecar started and at least one provider API key". Those are
separable, and only four of the nine actually need a key.

**Verified working.** `vision_health` and `vision_version` against a LIVE sidecar rather than an
absent one — the second is the useful one: it reports every optional backend and every API key
separately, so a caller knows what will work before trying it. `cross_validate_with_dxf` needs no
model and no key at all and was never tested for no reason; it works, and it earns its keep — on
a title-block sample it caught `POM. 1.03` against `POM 1.03`, exactly the dot-level OCR
discrepancy it exists to find.

**The important result, given nothing is installed here: every backend-requiring tool REFUSES and
names what to install.** No hollow successes, no empty result that reads like an analysis which
found nothing — which for an OCR tool would be the worst failure in this bank:

    ocr_image, extract_titleblock, extract_dimensions
        "Vision engine 'paddleocr' is not available. pip install paddleocr paddlepaddle"
    detect_symbols
        "Vision engine 'ultralytics' is not available. Run `pip install ultralytics`."
    describe_image, classify_drawing
        "No vision LLM provider available. Set ANTHROPIC_API_KEY, OPENAI_API_KEY or GOOGLE_API_KEY."

A missing FILE is also refused before any backend is consulted, with the path named.

**Still genuinely unverified: the six paths that need a backend present.** OCR needs
`paddleocr`/`easyocr`/`tesseract`, symbol detection needs `ultralytics` and weights, and the two
LLM tools need a provider key. Nothing about their behaviour with a working backend has been
observed. Untested is still not the same as working — but "untested" now means six tools, not
nine, and the failure mode of all six is known and honest.

**Original entry, for the record:**
**Status:** never verified.
`vision_health` / `vision_version` correctly report the sidecar is unreachable. The other seven
have not been run **at all** — they need the Python sidecar started and at least one provider API
key. Untested is not the same as working.

### ~~A5. `ucs.create_ucs_origin` reports the wrong name back~~
**Fixed 2026-08-05. It was the whole `create_ucs_*` family, and there was a second defect underneath: `makeCurrent:false` did nothing at all.**
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
| `blend_curves` continuity=smooth | geometry-2d | **Two implementations measured, both wrong, so it is refused.** (1) Interior fit points along the tangents *as well as* the imposed tangents over-constrains the fit: the blend left (100,0) heading DOWNWARDS and reached y = -9.7 before rising. It was longer than the tangent blend, which is exactly what an assertion of "smooth is longer" measured — longer because it detoured. Every numeric check passed; only the PNG showed the wiggle. (2) A longer tangent VECTOR instead: tangent and smooth came out identical at length 74.374, because `Spline(fitPoints, startTangent, endTangent, …)` **normalises** the tangents and ignores their magnitude. That left the argument a silent no-op, which is worse than a missing feature — it reports a continuity it did not apply. **`tangent` ships and is a true G1 blend**, verified to stay exactly within its two joins. |
| `set_object_transparency` mode=byLayer / byBlock | geometry-2d | **Compiles and throws.** `new Transparency(TransparencyMethod.ByLayer)` is accepted by the compiler — the enum member and that constructor both exist — but assigning the result to `Entity.Transparency` raises `eInvalidKey`. Measured on Line, Circle, Polyline and Hatch in one run; the percentage form succeeded on all four, so it is the constructor rather than the entity or the transaction. Probing found no constructor taking a raw DXF value (`0x01000000` is the ByLayer sentinel) and no static `Transparency.ByLayer`. The tool refuses these two modes with that measurement rather than silently mapping them to opaque, which would look like success and quietly break inheritance. **The percentage form ships and works**, which is the whole of what the tool is for. |
| `add_sheet_view` | sheetsets | **Not buildable through this COM surface, and that is not a gap in the API.** `IAcSmSheetViews` exposes only an enumerator and `Sync`, and `Sync` needs AXDBLib, which is not installed. More to the point, a sheet view comes into existence when a NAMED VIEW is placed on a sheet's layout in AutoCAD — a drawing operation — and the sheet set then discovers it. A tool of this name in `acad-sheetsets` would promise something the category does not do. `list_sheet_views`, `create_view_category` and `set_sheet_view_category` all ship and work on the 59 views in AutoCAD's sample set. |
| `dimension_center_mark`, `dimension_centerline` | dimensions | **The classes are not in the 2025 managed API.** `AcDbCenterMark` and `AcDbCenterLine` exist in ObjectARX, but `Autodesk.AutoCAD.DatabaseServices.CenterMark` and `.CenterLine` do not resolve against `acdbmgd.dll` — asked of the compiler, not of memory: both fail `CS0246 (type not found)` while `RadialDimensionLarge`, `ArcDimension` and the rest in the same probe compile clean. Drawing two crossing lines instead would produce something that LOOKS like a centre mark and is not one: it would not be associative, would not follow the circle when it moves, and would not respond to CENTERMARK's own settings. Blocked on the command layer, like `refedit_*` below. |
| `dimension_inspect` | dimensions | **Same probe, same answer.** `Inspection`, `InspectionLabel`, `InspectionRate` and `InspectionFrame` all fail `CS1061` on `RotatedDimension`, while `Dimtol`, `Dimtp`, `Dimtm` and `Dimtdec` on the same object compile — so this is these four properties being absent, not the dimension or the probe. |
| `set_mtext_frame` | annotations | **Built, measured, withdrawn.** The 2025 managed API has no `TextFrame` or `DrawFrame` on `MText` - both fail `CS0246` - and the only frame-ish property it does expose, `ShowBorders`, accepts the assignment, reads back `true`, and **draws nothing**. Measured two ways that agree: the entity's extents were 300 x 10 before and 300 x 10 after, and the exported image shows FRAMED TEXT with no border around it. A frame is drawn AROUND text, so it would have to push the extents out. A tool that sets a property and changes the drawing not at all is worse than a missing one, because it reports success. The `[McpTool]` attribute is removed so it is not advertised; the handler and proxy stay, exactly as with `modify.undo`/`redo`. |
| `arc_aligned_text`, `spell_check` | annotations | **Neither class is in the 2025 managed assemblies.** `Autodesk.AutoCAD.DatabaseServices.ArcAlignedText` and `SpellChecker` both fail `CS0246 (type not found)`, while `DBText.Justify`, `MText.BackgroundFill`, `ColumnType` and the rest of the 3.3 surface compile clean in the same probe. ARCTEXT is an Express Tool and spelling is a UI feature (SPELL); neither is exposed to .NET. Drawing curved text as individual rotated DBText entities would look similar and be a different thing - not one object, not editable as text, and not following the arc if it moves. |
| `dimension_break`, `dimension_reassociate` | dimensions | **Same probe, same answer.** `Dimension.Dimbreak` fails `CS1061`, and neither `Dimension.Dimassoc` nor a `DimAssoc` type resolves — while `DimLinePoint`, `ArcSymbolType`, `GetDimstyleData`/`SetDimstyleData` in the same probe compile clean and ship as `dimension_space`, `dimension_arc_symbol` and `dimension_update`. Both are command-layer features (DIMBREAK, DIMREASSOCIATE), so they sit behind the same blocker as `refedit_*` below. |
| `dimension_jog_linear` | dimensions | **Same.** `JogSymbolHeight` and `JogSymbolPosition` fail `CS1061`. Note this is the jog on a LINEAR dimension (DIMJOGLINE); the jogged RADIUS dimension is a different class, `RadialDimensionLarge`, which does exist and ships as `dimension_jogged_radius`. |
| `draw_construction_geometry` | geometry-2d | **Struck as a duplicate, not deferred.** AutoCAD has exactly two construction entities — `AcDbXline` and `AcDbRay` — and `draw_xline` and `draw_ray` already draw both. A third tool over the same two classes would give the router two ways to spell one action, and a bank that answers one intent with two tools is worse than one that answers it with one. What the pair genuinely does NOT cover is the XLINE command's **Bisect** and **Offset** modes: both compute a base point and direction from existing geometry rather than making a new kind of entity, so they belong as arguments to `draw_xline` if they are ever wanted, and are recorded here so a ticked roadmap box does not imply them. |
| `refedit_begin` / `_save` / `_discard` | xrefs | Modal, stateful command sequence on the channel that produced `eInvalidInput` in `zoom_extents` and silent queueing in `undo`. Needs a supervised contract for that channel first. |
| `set_viewport_layer_override`, `list_viewport_layer_overrides`, `clear_viewport_layer_overrides` | viewports | 2025 SDK exposes `LayerTableRecord.HasOverrides` as a plain bool with no viewport argument, and none of the `Set*InViewport` / `Get*InViewport` accessors. The capability exists in AutoCAD — this is finding the right API, not a limitation. **Per-viewport freeze, the larger half, ships and works.** |
| `maximize_viewport` | viewports | `MAXACT`/`MSPACE` — command layer. |
| `set_viewport_render_mode` | viewports | `Viewport.RenderMode` exists in `acdbmgd`'s metadata, but its enum is in neither `Autodesk.AutoCAD.DatabaseServices` nor `Autodesk.AutoCAD.GraphicsInterface` — the compiler rejects both. Withheld rather than guessing further namespaces. `set_viewport_visual_style` covers the same ground on any modern drawing and `set_viewport_shade_plot` already controls hidden-line removal at plot time. |
| `ucs_from_face` | ucs | Needs subentity picking; no non-interactive form. |
| `ucs_icon` | ucs | Display-only; nothing an API caller can observe. |
| `set_xref_demand_load` | xrefs | Specced in the roadmap, not built. Low value. |
| `flowDirection` (table style property) | styles | `TableStyle.FlowDirection` reads fine and throws `eInvalidInput` on **every** write — on a freshly created style and on a database-resident one alike. Isolated by setting each of the six candidate properties alone and watching only this one fail; a first guess that it was an ordering problem was wrong and is recorded as such in the code. Withheld from `TableStyleProperties` rather than advertised and broken. The settable flow direction may live on the `Table` entity rather than on the style; not confirmed. |
| `import_page_setup` | publish | Written and it does not work. `WblockCloneObjects` reports no error and the page setup is absent from the target drawing afterwards. The first attempt called it on the destination with source ids — it is called on the database that OWNS the objects, with the owner id in the destination — and correcting that direction changed nothing. Withheld rather than guessed at further. Worth noting how it was caught: the tool verifies the target dictionary after cloning instead of echoing back the name it was given, and without that post-condition it would have shipped reporting success. |
| `set_scale_position_for_scale` | annotative | Moving ONE scale representation of an annotative object needs a per-context transform. `ObjectContexts` exposes only Has/Add/Remove; in AutoCAD this is done by grip-editing while that scale is current, and there is no managed equivalent. `sync_scale_positions` (reset all representations from the current one) ships and works. |
| `set_paperspace_scale_link` | annotative | No API behind the roadmap's name. The link between a viewport's zoom and its annotation scale is implicit in AutoCAD, not a stored setting — `viewports.set_viewport_annotation_scale` (syncViewScale) and `viewports.sync_viewport_to_annotation_scale` are what actually control it. Name withdrawn rather than invented. |
| Parametric constraint application | parametric | Pre-existing. Every attempt failed with `eInvalidInput` from `Editor.Command` across four approaches; implementation preserved unregistered in `ParametricPluginTools.cs`. |

**Note on `overriddenLayers`:** `viewports.get_viewport_info` reports it from the bool
`HasOverrides`, so it means *"this layer is overridden in some viewport"*, not *"in this one"*.
The XML comment says so; the field name still reads more precise than it is.

---

## C. Missing test machinery

Four defects in this sweep were a **discovery tool advertising what the action tool refuses**
(dictionary params described as arrays; three catalogues). Nothing tests that the two agree.

### C0. A field the plugin emits and the backend DTO does not declare vanishes silently

**Status: open, and it has now happened three times.** Every category proxy ends in
`resultNode.Deserialize<TResult>(Opts)`. `System.Text.Json` drops JSON members the target record
does not declare — no exception, no warning, nothing in a log. The tool returns a healthy result
that is quietly missing the field it computed.

| Where | What vanished | Consequence |
|---|---|---|
| `delete_layer_filter` | `alsoDeleted` | a cascade that removed two nested filters reported `{name, deleted:true}` and nothing else |
| `import_dimstyle_from_dwg` | `replaced` | added while fixing the same class of bug in that tool |
| `create_ucs_*` | `savedAs`, `isCurrent` | a UCS saved-but-not-made-current was indistinguishable from one made current |

All three were caught by a live verification asserting on the field, and **only** because the
verification asserted on it. Nothing structural would have found them: the plugin builds
anonymous objects, so there is no type to compare against the record at compile time.

**Proposed mechanism, not built.** `JsonSerializerOptions.UnmappedMemberHandling =
JsonUnmappedMemberHandling.Disallow` (.NET 8) makes deserialisation throw on exactly this. Too
strict to ship — a plugin sending an extra diagnostic field would break a working tool — but
correct for verification. An env var such as `ACADMCP_STRICT_DTO=1`, read where each proxy builds
its `JsonSerializerOptions`, would turn every live verification run into a contract check for
free. The obstacle is that all ~38 proxies declare their own `Opts`; they need one shared
factory first, which is a mechanical change worth doing before phase 3 adds more of them.

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

- ~~**Multi-sheet publishing.**~~ **Done 2026-08-05.** Two layouts into one 3-page PDF, verified;
  the cause was layouts present in memory and absent from the file on disk, not the paper-size
  theory this entry proposed. See A10.

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
- ~~**`Megasystem` placeholder branding**~~ — **closed during the ToolBank rename.** It was 26
  occurrences across 23 files, and the earlier note that "every shipped artefact was fixed"
  was wrong: ten manifests carried `"owner": "AutoCAD MCP Megasystem"` and
  `BankAutoRegister.cs` re-emitted it, so regenerating a manifest put the placeholder straight
  back. Generator and manifests now both say `ToolBank AutoCAD`.
- **ToolBank (the Python repo)** — the `safety.py` blocker and cross-repo install docs.
  Version sync is done (both now read one `__version__`).

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
