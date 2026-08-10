# Changelog

All notable changes to this project will be documented in this file. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- **Renamed to ToolBank throughout, and `mcpbank-manifests/` → `toolbank-manifests/`.**
  The routing project this repo publishes its manifests for is now called ToolBank; the
  full lineage is `mcpd` → `mcpbank` → `mcpnexus` → `toolbank`, so anything in this repo
  still saying "MCPBank" or "MCP Nexus" was one or two renames stale. ("ToolVault" appears in
  neither the lineage nor this repo: it was the intended name, got as far as a local commit,
  and was dropped when an availability check found `toolvault` already on npm from a project
  in the same niche. Checked before publishing, not after.) 304 occurrences over
  92 files, plus four paths moved: the manifest directory, `MCPBANK-TECHNICAL-PROOF.md`, and
  engineering rules 30 and 31 (numbers unchanged — they are the stable identifiers, only the
  slugs moved).

  The one part of this that was not cosmetic: `RepoRootDetector` treats the existence of the
  manifest directory as one of its two repo-root markers, the other being `.git/`. Renaming
  the directory without that literal would have kept working in every git checkout and failed
  only in an installed layout, where there is no `.git/` to fall back on — a break that hides
  in development and surfaces at a customer. The literal moved with the directory.

  Also corrected three places where a blanket search-and-replace had quietly rewritten
  statements of fact rather than names: the changelog below (whose dated entries name the
  package as it was called at the time, and which had been made to cite `toolbank-discovery`
  and `toolbank-dynamic` — commands that have never existed under any name), the
  hospital-2026 findings (a record of a past fix, including a machine-local path), and a
  `KNOWN-GAPS` line that would have claimed `toolbank` is on PyPI when what is published is
  `mcpnexus` 1.0.7.

  Verified: `AcadMcp.Backend` and the test project build with 0 warnings — which exercises the
  renamed path, since the `CheckManifestSync` MSBuild target resolves it — `check-manifests.ps1`
  reports 38 categories / 39 manifests / 0 problems, and 219/219 tests pass.

### Fixed

- **Three checks in the mesh verification were passing for the wrong reason.** After
  `extrude_mesh_face` was withdrawn from the bank, the refusal checks aimed at it — a face index
  out of range, a missing index, a line passed as a mesh — all went green because the tool is
  **absent**, not because its validation works. A check that passes for the wrong reason is worth
  nothing, so they were deleted rather than left to look healthy, and the one remaining assertion
  now states the shipped fact: `extrude_mesh_face` is not offered, and the box it would have been
  called on is still 8 vertices and 6 faces.

  With that corrected the tranche runs **28/28** on a freshly started AutoCAD, and the image
  carries what no number does: the 12×6 sphere as a visible twelve-sided polygon beside the 32×16
  one that reads as round, which is the faceting argument made visual.

### Changed

- **Phase 4.4 reconnaissance: the Section entity.** Eleven of the twelve planned tools are
  reachable, asked of the compiler while AutoCAD was down so the next live slot is not spent on
  discovery.

  **The construction route is the constructor, not a factory** — there is no
  `Section.CreateSectionPlane` in either form, and `Section.Boundary` is read-only with no
  `SetBoundary`, so the cut line goes in when the object is made:
  `new Section(Point3dCollection, Vector3d)`, or the three-argument form that also takes the
  vertical direction.

  Present and callable: `State` and `IsLiveSectionEnabled` (both get and set), `Elevation`,
  `VerticalDirection`, `IndicatorTransparency`, `Height(SectionHeight)` with
  `SetHeight(SectionHeight, double)`, `Settings`, `SectionSettings.CurrentSectionType` and
  `GenerationOptions`, and
  `GenerateSectionGeometry(Entity, out Array, out Array, out Array, out Array, out Array)` — five
  output arrays, which is what makes the 2D, 3D and block generators one implementation driven by
  a setting rather than three separate ones.

  Two consequences for the tool set, both worth knowing before anything is written.
  `add_section_jog` cannot ADD a vertex, because the boundary is immutable: it has to rebuild the
  Section from a new point list, so it will ship as a replace and say so. And
  `create_section_from_object` is **struck** — nothing in the managed API derives a section line
  from an existing entity.

### Added

- **Phase 5.2, second tranche — tagging, querying and CSV.** `acad-data` 13 → 18, bank 617 → 622.
  Live: **108/108 over the whole category, no defects.** `tag_entities`, `list_tagged_entities`,
  `query_by_property`, `export_table_to_csv`, `import_csv_to_table`.

  **A tag IS xdata**, under the reserved application name `TOOLBANK_TAG` rather than a private
  format — so it survives copying with the entity, `get_xdata` reads it like any other extended
  data, and it appears in `list_registered_apps`. The verification asserts that rather than
  trusting the description.

  **`Table.ExportToCsv` and `Table.ImportFromCsv` do not exist** in the managed API, so the CSV
  pair is own work and says so. Two things a caller needs to know and both are documented: cell
  text is taken AS DISPLAYED, so a formula exports as its result; and quoting is handled here, so
  a cell containing a comma stays one cell. The round trip is verified on an ASYMMETRIC 3×2 grid
  with a comma inside a cell — a symmetric grid would pass even with rows and columns transposed.

  `query_by_property` is checked against a filter that matches SOME entities and one that matches
  NONE, because a query ignoring its filters would pass the first check and fail only the second.

  Also corrected a description that pointed at `annotations.draw_table`, which does not exist —
  the tool is `add_table`. A router sent there would have found nothing.

- **Phase 5.2, first tranche — `acad-data`: xdata, dictionaries and xrecords.** New category,
  13 tools, bank 604 → 617. Live: **72/72, first run, no defects** — the plugin compiled on the
  first attempt and the live run found nothing wrong, which the reconnaissance bought.

  Two storage mechanisms that are not interchangeable, and every description says which to pick:
  XDATA hangs off one entity, is filed under a registered application name, holds a flat list and
  is capped at 16 KB per entity per application; DICTIONARIES are drawing-wide or per-entity, hold
  named entries, nest, and have no cap worth worrying about. A few values belonging to one object
  are xdata; a structure, or anything shared, is a dictionary.

  **Every value carries an explicit `type`** — string, real, int, point, layer or handle — rather
  than one inferred from the JSON. JSON cannot tell `1` from `1.0` and AutoCAD very much can: an
  int stored where a real was meant reads back as a different type and breaks the round trip
  silently. The verification asserts the type alongside the value on every value returned.

  Three controls carry the verification, each catching a wrong implementation that would otherwise
  look perfect: **application isolation** (two applications on the SAME entity must not see each
  other — a tool storing per-entity passes every single-application check and fails only here),
  **entity isolation** (two entities under the same application name), and **a cross-tool control
  on `lisp.purge_regapps`** — a name referenced by xdata must survive a purge and become purgeable
  once its xdata is deleted, which is the claim that tool makes about itself and had never been
  checked from outside it.

  `delete_xdata` uses AutoCAD's documented mechanism of writing a buffer holding only the
  application name, which looks like a mistake and is not; it is verified by reading back, and the
  other applications present before and after are reported so "left untouched" can be checked
  rather than trusted.

- **Phase 5.1 — `acad-lisp`, 5 tools shipped of 11 attempted, and one root cause behind the six
  that were not.** New category, bank 599 → 604. Live: **34/34** on what ships.

  Built and verified: `get_system_variable`, `set_system_variable`, `list_system_variables`,
  `list_loaded_applications`, `purge_regapps`. `set_system_variable` reads back and refuses when
  the value did not change, which is what a read-only variable does — it accepts the assignment
  and quietly keeps its old value, indistinguishable from success without the check.
  `purge_regapps` is run twice in verification: the second run must find nothing, or the first was
  simply erasing whatever it could reach.

  **Six were built, deployed, measured and withdrawn on ONE root cause**: `eval_lisp`,
  `load_lisp_file`, `list_loaded_lisp`, `run_command_sequence`, `run_script_file` and
  `netload_assembly`. `Application.Invoke`, `Editor.Command` and `DynamicLinker.LoadModule` all
  answer a bare `eInvalidInput` from where this plugin dispatches — the **application** context,
  on the UI thread inside a document lock and an open transaction. They require a **command**
  context. Three separate LISP formulations were tried (`Invoke` with `(read)`+`(eval)`, splicing
  the parsed form, and the command line wrapped to write its value to a file) and all three failed
  identically. When tools that share no code fail with the same status, the context is the suspect
  and not the arguments. Rule 26 gains §15. The plugin handlers stay registered so the finding
  stays reproducible; they are simply not offered in the bank.

  **The obvious fix was then built, measured and reverted.**
  `DocumentCollection.ExecuteInCommandContextAsync` supplies a command context, and a runner was
  written around it withholding all three suspects — no UI-thread dispatch, no document lock, no
  transaction across the call. All six tools still answered the same `eInvalidInput`, and the run
  hung AutoCAD badly enough that the process had to be killed. Reverted to the five that are
  verified. The next step is deliberately smaller: a throwaway `[CommandMethod]` in this plugin
  that calls `Editor.Command` and reports what happens, which separates the plugin from the
  dispatch path — the one thing none of the attempts so far has done.

  **`list_loaded_applications` ships documenting a measured limit.** `GetLoadedModules` returns
  about 25 ARX/CRX/DBX modules plus AutoCAD's own managed core and does NOT report netloaded .NET
  assemblies: this very plugin is running and is absent from its own list. The description says
  so, and the verification asserts the absence, so a short answer is not mistaken for a broken
  tool.

  `define_command_alias` is struck: no API, and the only route would be editing the user's global
  `acad.pgp` — a permanent change to the AutoCAD installation rather than to a drawing.

- **Phase 4.4 complete — `create_section_orthographic`, `generate_section_block`,
  `set_section_settings`.** `acad-sections-3d` 6 → 9, bank 596 → 599. Live: **111/111** over the
  whole category. Phase 4.4 is done at 9 built and 1 struck.

  **`create_section_orthographic` is not an API wrapper.** `Section` has neither
  `CreateOrthographic` nor `SetOrthographic` in any form, so the plane is placed by arithmetic over
  the model's own extents — which makes the result exactly predictable and is what the verification
  checks against. The three standard views on a 100×80×60 box give **320 / 280 / 360**, three
  different numbers, so an orientation that silently defaulted cannot pass; `offset` is proved on a
  sphere, where 30 off centre must give 251.327 rather than the great circle's 314.159 (on a box an
  ignored offset would look identical). The extents actually used are reported back.

  **It refused itself on the first live run, and that is the tool working.** AutoCAD derives the
  normal as `up × along`, not `along × up` — the two differ only in sign. The first tranche's check
  asked only that `|y| = 1`, so a sign error walked straight through it; the check now pins the
  exact vector. The fix was a deletion: `along = view × up` needs no correction, since
  `up × (view × up) = view`.

  **`set_section_settings` ships with a MEASURED validity matrix.** AutoCAD answers an unsupported
  combination with a bare `eInvalidInput` naming neither the field nor the reason, so each field was
  probed against each part of each section type. Colour, layer and linetypeScale work on all four
  parts of a 2d or 3d section; `visible` everywhere except the cut of a 2d section — the cut outline
  IS the section — and the background of a 3d one; `divisionLines` only on the 2d cut;
  `hiddenLine` only on 2d background and foreground; and **nothing whatever can be set on `live`**.
  Unsupported requests are refused with the reason rather than passing the bare error through, and
  one check exists specifically to stop that guard being a blanket refusal that would look just as
  green: a supported 3d combination must still go through.

  `faceTransparency` and `edgeTransparency` are **reported but no longer accepted**: the setters
  refuse every value from 0 to 255, on every part, in every kind. The getters work — which is how
  the setter was identified as the culprit, since colour-only calls succeed while running the same
  read-back block. A parameter that always fails is worse than an absent one.

  `SectionGeometry` has exactly four members, so there is no tangency PART to style even though
  `GenerateSectionGeometry` returns tangency curves; the tool says so when asked for one.
  `generate_section_block` builds the block itself — `GenerateSectionGeometry` hands back loose
  entities whatever `SectionGeneration.DestinationNewBlock` is set to — and asserts the definition
  holds exactly the entities generated, since an empty definition would still insert and look like
  a success while drawing nothing.

- **Phase 4.4, first tranche — `acad-sections-3d`, and a green verification that was measuring
  nothing.** New category, 6 tools, bank 590 → 596: `create_section_plane`,
  `list_section_planes`, `set_section_state`, `set_live_section`, `set_section_height`,
  `generate_section`. Live: **57/57**.

  A section plane **cuts nothing**. It is an object that reports what a cut would look like, and
  the solids it crosses come out untouched — the whole difference from `geometry_3d.slice_solid`,
  and invisible in a JSON result, so the source volume is measured after every operation in the
  verification.

  Three tools collapse into one where the API has one code path.
  `GenerateSectionGeometry` returns five arrays from a single call and the output type is chosen
  through `SectionSettings.CurrentSectionType`, so `generate_section` takes `kind` = 2d / 3d /
  live rather than shipping three names for the same thing. Likewise `set_live_section` covers
  both the setter and the toggle. `add_section_jog` is subsumed by `create_section_plane` with
  more than two vertices, because `Section.Boundary` is read-only and a jog therefore means
  rebuilding; `create_section_from_object` is struck, nothing in the managed API deriving a
  section line from an entity.

  **The constructor's second `Vector3d` is `verticalDir`, not the normal — and this shipped wrong
  before it shipped right.** The cut plane is the one containing the section line and that vector,
  so the normal comes out as `line × up` and cannot be supplied at all: `Section.Normal` is
  read-only. Passing the intended normal (horizontal, for a plan line) makes the plane contain the
  plan line *and* a horizontal direction — the XY plane — so every "vertical" section was taken
  horizontally at z=0. AutoCAD reported success throughout. `create_section_plane` no longer
  accepts `normal`; it takes `verticalDirection` (default Z, and a horizontal vector now
  deliberately gives a flat cut), reports the normal it worked out, and refuses if that normal is
  not square to both inputs.

  Both overloads take `Vector3d`, so the compiler could not answer by type — but it answered by
  **name**. Named arguments made it an oracle: `verticalDir:` compiles, `normal:` and
  `verticalDirection:` are CS1739; the same rounds proved `Normal` read-only and
  `VerticalDirection` settable after construction.

  **What let this survive is worth more than the trap: the test shape could not tell a cut from a
  silhouette.** The first verification ran 41/43 with the tool broken, and the two failures were
  the ones that mattered. A plane through the middle of a 100 cube and a plane 5000 units clear of
  it both answered 400, because a cube's cross-section and its outline are the same square — and
  the script called 400 proof. The checks now use shapes where the two differ: a sphere of r=50
  cut 30 off-centre gives `2π·40 = 251.327` against `314.159` for the great circle, and a
  100×80×60 box gives `2(100+60) = 320` upright against `2(100+80) = 360` flat, so neither
  position nor orientation can be mistaken. Rule 26 §14.

- **Phase 4.3, third tranche — the curved primitives, and a tool withdrawn after being built.**
  `acad-mesh` 8 → 10, bank 588 → 590: `create_mesh_sphere`, `create_mesh_cone`. Phase 4.3 is
  complete at 10 built and 3 struck.

  A lat/long sphere cage is **exactly** `2 + (rings-1)·segments` vertices and `rings·segments`
  faces — 62 and 72 at 12×6, 482 and 512 at 32×16 — and a mesh cone is a **pyramid** over an
  n-gon, so its volume is exactly one third of base area times height: 235702.2604 against the
  261799.3878 a true cone holds. Both are inscribed polyhedra and both report the round figure
  they fall short of, so the gap is visible rather than surprising; a finer tessellation closing
  it is the control that says the shortfall is faceting rather than a fault.

  **`extrude_mesh_face` was built, deployed, measured — and withdrawn.** `ExtrudeFaces`,
  `SplitFace` and `MergeFaces` all exist on `SubDMesh` and all want a `FullSubentityPath[]`,
  which `SubDMesh` offers no way to produce: the `GetSubentityPathsAt*` family that made the
  entire solid face-and-edge family reachable is absent here. The hypothesis was that a path
  could simply be constructed with the face index in the pointer field of a `SubentityId`. It
  cannot — AutoCAD accepts the call, **returns success**, and leaves the cage at 8 vertices and
  6 faces.

  The tool is out of the bank because one that silently does nothing while reporting success is
  worse than an absent one. Its plugin handler stays, guarded, so the finding is reproducible,
  and the live check asserts the refusal so nobody spends the same build cycle again.
  `split_mesh_face`, `merge_mesh_faces` and `refine_mesh` are struck with it.

### Fixed

- **A harness failure that looked like arithmetic, and two wrong diagnoses before the right one.**
  Verification runs began failing every cross-category measurement: a solid minted by the mesh
  session came back *"Handle not found"* from `geometry-3d`. Each category is its own backend
  process bound to a document, and `deploy-plugin.ps1 -Kill` restarts them all — some came back
  bound to a drawing that a later `new_document` replaced. The giveaway was handles **climbing
  across three consecutive `new_document` calls**: 7F, 82, 84, which is a session that saw none
  of them.

  Two diagnoses were wrong first and both were discarded by measurement rather than argument:
  `eraseSource` erasing the result (the failure occurs without it), and sessions binding late (a
  warm-up loop was added, changed nothing, and was **removed rather than left in looking
  useful**). The remedy is a clean AutoCAD start before each run, which is why every live check
  in this project is preceded by one. Now written up as rule 26 §13a with the symptom to
  recognise, alongside §13 for the un-addressable mesh sub-entities.

### Added

- **Phase 4.3, second tranche — creasing and two more primitives.** `acad-mesh` 5 → 8, bank
  585 → 588: `set_mesh_crease`, `create_mesh_cylinder`, `create_mesh_wedge`. Verified live
  **46/46** and confirmed on an exported PNG.

  A mesh wedge is exactly **half** the box on the same corners — 500000 for a 100 cube — and its
  cage is six vertices over five faces, two triangles and three quads, so unlike a box it mixes
  face sizes. A mesh cylinder is a **prism, not a circle**, and the tool says so rather than
  letting the caller assume otherwise: eight flat sides inscribed in radius 50 give
  `(8/2)·50²·sin(2π/8)·100` = 707106.78, noticeably less than the `πr²h` = 785398.16 a true
  cylinder holds. Both figures are reported, and 32 sides closing the gap is the control that
  says the shortfall is faceting rather than a fault.

  **A defect found by a control, in tools that individually work.** The crease check first came
  back at 349547.26189088693 against an uncreased 349547.2618908878 — identical to ten
  significant figures, so the crease had done nothing at all. Measuring four orderings found why:
  `set_mesh_smoothness` rebuilds the mesh through `SetSubDMesh`, which carries no crease data and
  **silently discards every crease**. Crease *after* smoothing and it comes back to exactly
  1000000; crease before and it is lost.

  The tools cannot detect this and warn, because reading a crease back needs a
  `FullSubentityPath` and `SubDMesh` exposes no way to obtain one. So both descriptions state the
  ordering, and the verification asserts **both orders** — the one that works and the trap — so
  the claim is checkable rather than merely written down.

  This is the failure a build count never shows: two tools that each pass on their own and give a
  silently wrong result when used in the obvious order. It was only visible because the check ran
  the uncreased case first as a control; a single number would have looked like success either
  way. `set_mesh_crease` works on all edges at once, for the same addressing reason as before —
  a tool cannot select what the API will not address.

### Added

- **Phase 4.3 opens: the `acad-mesh` category.** Five tools, bank 580 → 585, and the forty-first
  category: `create_mesh_box`, `get_mesh_info`, `set_mesh_smoothness`, `convert_mesh_to_solid`,
  `convert_mesh_to_surface`. Verified live **47/47** and confirmed on an exported PNG.

  A mesh is a CAGE of flat faces AutoCAD can smooth — neither a solid nor a surface. `SubDMesh`
  carries no volume, no surface area and no watertight flag, so **converting a mesh is the only
  way to measure one**, which makes that conversion its own check: an unsmoothed box mesh coming
  out at exactly its side cubed proves the cage was built *and wound* correctly, and a face wound
  the wrong way looks perfectly normal on screen while enclosing nothing. There are also no
  factory methods for mesh primitives at all — no `SubDMesh.CreateBox` to match `Solid3d.CreateBox`
  — so the box cage is written out by hand, eight corners and six quads, which is why its counts
  are known before the call.

  **Three assumptions of mine were demolished by the live run, and all three were mine rather
  than AutoCAD's.**

  - **`NumberOfFaces` reports the CAGE, not the subdivided surface.** A box smoothed to level 3
    still answers 6 faces. Guards asserting 6 → 24 → 96 → 384 fired on a perfectly good mesh
    three times running.
  - **`GeometricExtents` reports the cage too.** Smoothing 0 → 1 left the diagonal at exactly
    100·√3. So the second guard, written to replace the first, was equally wrong.
  - So the guard was **removed rather than replaced a third time**, and the reason is recorded in
    the code and in the tool description: what is checked is the level reading back, and the
    shape change is visible only by converting to a solid. A guard founded on an unchecked
    assumption is worse than no guard — it rejects correct results while looking rigorous.

  A fourth, in the verification rather than the tools: it asserted a smoothed box is "still more
  than half" the sharp one. A level-2 Catmull-Clark cube is about a **third** — 349547 against
  1000000 — and the exported image shows why, because it has visibly become a faceted **ball**.
  Replaced with a check that is derivable and carries its own control: level 2 shrinks the box
  more than level 1 does, monotonically.

  The reversibility check stands and is the sharpest here: smoothing to level 3 and back to 0
  reconverts to **exactly** 1000000, because the cage is kept. A smoothing that rebuilt from the
  subdivided form instead would look identical on the way up and could never come back.

### Changed

- **Phase 4.3 reconnaissance: the mesh API, and a probe that was wrong about six names at once.**
  Thirteen of the sixteen planned mesh tools are reachable. Present and callable: `SetSubDMesh`,
  **`SmoothLevel`** (get and set), `SetCrease` in both forms, `GetCrease`, `SplitFace`,
  `MergeFaces`, `ExtrudeFaces`, `Vertices`, `FaceArray`, `NumberOfFaces`, `NumberOfVertices`,
  `ConvertToSolid` and `ConvertToSurface`.

  **The seven mesh primitives have no factory methods at all.** `SubDMesh` has no `CreateBox` or
  `CreateSphere` — unlike `Solid3d`, which has the lot. The only construction route is
  `SetSubDMesh(vertices, faces, smoothLevel)`, so each primitive must be tessellated by hand:
  eight vertices and six quads for a box, a ring-by-ring sweep for a sphere. That is real work,
  but it makes the vertex and face counts exactly predictable, which is the arithmetic those
  tools will be checked against. Also confirmed absent: `SubDMesh.Volume`, `SurfaceArea` and
  `IsWatertight`, so a mesh has to be converted to a solid to be measured — which is itself the
  natural check on the conversion.

  **The first probe of this phase was wrong about six names and would have struck half the work.**
  It asked for `SubDivisionLevel`, `IncreaseSubDivisionLevel`, `DecreaseSubDivisionLevel`,
  `Refine`, `Unrefine` and `Splitface`; every one returned CS1061, which reads as "mesh smoothing
  and face editing are not exposed at all". The real names are `SmoothLevel`, `SplitFace` and
  `MergeFaces`. Six absences in a row is not corroboration — it is one guess about naming, made
  six times.

  That is the **fourth** time in this session a single probe round nearly struck buildable tools,
  after `ShellSolid`/`ShellBody`, `Profile3d`, and `Surface.Trim` answering as
  `MemoryExtensions.Trim`. Rule 26 §12c now carries all four as a table, with the rule that
  follows: a struck row is a decision never to look again, and it costs one build to avoid making
  that decision on a typo.

### Added

- **Phase 4.2, second tranche — joining, projecting, and the NURBS cage.** `acad-surfaces`
  7 → 12, bank 575 → 580: `blend_surfaces`, `project_to_surface`, `convert_to_nurbs`,
  `get_nurbs_info`, `edit_nurbs_point`. Verified live **44/44** and confirmed on an exported PNG.

  The arithmetic: blending two parallel 100 curves across a 60 gap gives a flat ruled sheet of
  exactly 6000; a 100 line projected straight down onto a flat horizontal surface stays 100 long,
  because a shadow cast square onto a plane is the size of the thing casting it.

  **Two checks here exist because the tool would otherwise return something valid and wrong.**
  Converting a surface to NURBS must leave the AREA alone — re-describing a shape must not
  reshape it, and a badly approximated conversion still hands back a perfectly valid
  `NurbSurface`, so the area equality is the only thing that says it was faithful. And moving a
  control point that steers nothing is reported by AutoCAD as a **successful move**, so
  `edit_nurbs_point` refuses when the area did not change — proved non-vacuous by first showing a
  move that does change it, then a **round trip**: move the point out, move it straight back, and
  the area must return to exactly what it was. Both halves have to be real for that to hold, which
  neither half alone can show.

  **A claim in a tool description turned out to be invented rather than measured.**
  `project_to_surface` said that when the projection misses the surface AutoCAD returns an empty
  result rather than an error, and that the tool guards that case. It does not: it throws
  `GeneralModelingFailure`, and the empty-result guard has never been observed to fire. The
  description now says what was measured, the refusal names the likely cause instead of only the
  status code, and the guard stays as a backstop with a comment saying so — a success over no
  geometry is exactly the shape of failure this project keeps finding, even if this is not the
  path that produces it.

  **`surface_trim` is struck: `Surface.Trim` does not exist.** The probe that seemed to say
  otherwise — *"no overload takes 3 arguments"* — was resolving against `MemoryExtensions.Trim`,
  the string extension that is in scope in every file. A CS1501 naming a method says nothing
  about which type owns it. This is the third variant in two days of the same mistake: a probe
  answered against something other than what was asked. Rule 26 §12c now covers all three.

- **Phase 4.2 opens: the `acad-surfaces` category.** Seven tools, bank 568 → 575, and the
  fortieth category: `extrude_surface`, `revolve_surface`, `sweep_surface`, `offset_surface`,
  `convert_to_surface`, `convert_to_solid`, `get_surface_info`. Verified live **58/58** and
  confirmed on an exported PNG.

  **A surface is a shell: area and no volume.** That is the whole reason this is not part of
  `acad-geometry-3d`, and it is what every check here rests on, because a surface tool that
  quietly produced nothing hands back a perfectly good handle. So the arithmetic is areas:
  extruding a 100 line through 50 gives exactly 5000; a circle of radius 40 through 50 gives
  2·π·40·50; revolving a line about a parallel axis 200 away gives Pappus' 2·π·200·100 **exactly**,
  because a line parallel to the axis keeps a constant distance from it; half a turn gives half
  as much, which is the control on the angle; a 40 profile along a straight 300 gives 12000; and
  offsetting a flat surface leaves the area alone.

  The sharpest check is the round trip: a 100 cube converted to a surface has the cube's surface
  area of 60000 and **no volume**, and converting it back returns **exactly** 1000000. A
  conversion that produced an empty shell or an empty solid would return a valid handle either
  way, and only the numbers coming back to where they started say the trip was real. The tools
  are also asserted not to have made the wrong kind of thing at all — a sheet must report no
  volume, which a tool that silently built a solid would fail.

  `get_surface_info` exists because nothing else in the category is usable without it: a
  `PlaneSurface`, `ExtrudedSurface`, `RevolvedSurface`, `SweptSurface` and `NurbSurface` each
  accept different edits, and asking for one the surface does not support is the commonest
  failure here. It reports the concrete type, the area, the face and edge counts, and whether
  the whole thing is planar — checked against a flat sheet (planar, one face) *and* a tube (not
  planar), so the flag distinguishes rather than always agreeing.

  Two corrections during the build, both mine rather than the API's. Three of the surface
  constructors are **static** factories and `Solid3d.CreateFrom` is an **instance** method — the
  opposite of how they were first written, and the compiler named every signature to the
  argument. And in the verification, a refusal test aimed at an already-erased solid **passed on
  `eWasErased`** rather than on the type check it was written to make; a refusal that fires for
  the wrong reason is not evidence about the thing being tested, so it now uses a live solid and
  asserts the message names the type.

### Changed

- **Phase 4.2 reconnaissance: the surface API asked of the compiler before the category exists.**
  Thirteen of the eighteen planned tools are reachable; five are struck. Present and callable:
  `CreateExtrudedSurface`, `CreateRevolvedSurface` (six arguments), `CreateSweptSurface`,
  `CreateLoftedSurface`, `CreateNetworkSurface`, `CreateBlendSurface`, `CreatePatchSurface`,
  `CreateOffsetSurface`, `CreateSectionObjects`, `Trim`, `ProjectOnToSurface`,
  `ConvertToNurbSurface`, `Surface.CreateFrom(Entity)`, `Solid3d.CreateFrom(Entity)`, and
  `NurbSurface`'s control-point accessors. Absent, and struck: `Surface.Extend`,
  `Surface.Sculpt`, `Surface.RebuildNurbSurface`, `Surface.ConvertToSolid` — the conversion runs
  the other way, through `Solid3d.CreateFrom` — and `Surface.Associativity`/`IsAssociative`.

  **A probe-reading trap that would have struck six buildable tools, and the second of its kind.**
  `new Profile3d("x")` answers *"cannot convert from string to Profile3d"*, which reads as a type
  with nothing but a copy constructor — and six of the surface constructors want a `Profile3d` or
  a `LoftProfile`, so that reading condemns all six. It is wrong: `new Profile3d(entity)` and
  `new LoftProfile(curve)` both compile. When several overloads exist the compiler reports
  against the one it picked, so a CS1503 naming a type says *that overload wants this*, never
  *this is the only overload*. The mirror of the `ShellSolid`/`ShellBody` mistake in 4.1: there a
  probe with the wrong NAME struck a buildable tool, here a probe with an implausible ARGUMENT
  nearly struck six. Written up as rule 26 §12c, with the practical rule — when a probe says
  absent, spend one more round before striking anything.

### Fixed

- **Tool descriptions a routing agent can actually choose on.** A tool the router never reaches
  is as unavailable as one that was never built, and nothing in the build caught it: a stub
  description compiles, satisfies the manifest check and passes its unit test. A sweep of all
  568 tools found, and this fixes:

  - **15 tools whose entire description was one short sentence** — `modify.move` said "Translate
    one or more entities by the vector from-to (WCS)", which does not tell an agent that the two
    points are a direction and a distance rather than a destination. Also `draw_circle`,
    `draw_point`, `select_by_color`, the layer-state pair and nine others. All rewritten to say
    what the tool is for, what it is NOT, and which tool to use instead.
  - **8 intent phrases claimed by two tools at once.** "stworz pierscien" was offered by both
    `draw_donut` (flat) and `draw_torus` (a solid); "usun obiekty" by both `delete_entities` and
    `erase`. The router had nothing to choose on. This is the failure that gets worse as the bank
    grows, so each phrase now says which of the two it means.
  - **5 tool names living in two categories with neither description mentioning the other** —
    `draw_hatch`, `insert_door`, `insert_window`. Each now names its twin and the difference:
    `openings.insert_door` places a numbered block a schedule can read, `architecture.insert_door`
    draws primitives on the layer standard.
  - **11 confusable sibling pairs cross-referenced**, the sharpest being 2D against 3D:
    `fillet_corner`/`fillet_edge`, `chamfer_corner`/`chamfer_edge`, `offset_curve`/`offset_face`,
    `extrude_curve`/`extrude_face`, and the three arrays, which now say they are three.

  **The audit itself had to be corrected twice before its output was worth anything**, which is
  the more useful half of this entry:

  - Its language check reported **170 tools as having no Polish intent**, including ones whose
    intents read `maska tla pod tekstem`. The detector demanded diacritics or a short list of
    function words. A check that fires on healthy tools buries the real ones, so it was replaced
    with a classifier bootstrapped from the corpus: seed on tokens that can only be one language,
    learn the rest, and abstain rather than guess. It now prints a labelled sample so the
    classifier can be checked by eye before its verdicts are believed. After the fix: 0.
  - Its collision check reported `layer state to file` and `layer state from file` as the same
    phrase — export and import collapsed into one — because the normaliser stripped directional
    prepositions. It stripped the very words that told them apart.
  - It reported **10 router tools as having no intents**. They are exposed straight to the agent
    as MCP tools with their own descriptions and are never reached by intent matching, so an
    empty list is correct. Excluded, with the reason written down.
  - A sixth check, "does the description say what the tool is for", flagged **305 tools**, nearly
    all well described. It was **deleted rather than tuned**: there was no version of it that
    measured the thing it claimed to.

  The audit is now step 9 of the pre-commit gate, and was checked the only way a gate can be —
  by breaking a description on purpose and confirming it fails, then restoring it and confirming
  it passes.

### Added

- **Phase 4.1, last tranche — shape and health. 4.1 is complete.** `acad-geometry-3d` 32 → 36,
  bank 564 → 568: `draw_polysolid`, `presspull`, `clean_solid`, `check_solid`. Verified live
  **66/66**, and confirmed on an exported PNG.

  `draw_polysolid` sweeps a rectangular section along a path — a wall, a kerb, a skirting.
  `presspull` turns a closed area into a solid of exactly area×distance, or presses it straight
  into an existing solid, where the **sign** decides: negative cuts a pocket, positive adds a
  boss. It refuses when the push never meets the target, which AutoCAD otherwise reports as a
  successful boolean over an unchanged solid.

  **`check_solid` answers with arithmetic rather than an opinion,** because `CheckSolidNature`
  does not exist in the managed API. It walks the boundary and tests Euler–Poincaré in the form
  a B-rep actually needs: **V − E + F − R = 2(S − G)**, where R counts the inner loops of faces,
  S the shells and G the genus — the number of holes running right through. A box gives
  8 − 12 + 6 − 0 = 2 and genus 0; drill through it and the same arithmetic gives
  11 − 16 + 7 − 2 = 0 and genus 1. That is a statement about the boundary closing, which a
  plausible-looking volume cannot make.

  **Three defects, and two of them were mine rather than AutoCAD's:**

  - **`check_solid` reported genus 0 for a box with a hole right through it** — failing to notice
    the one thing it exists to notice — because the first version left the **ring term out of
    Euler–Poincaré**. The volume said the hole was there; the topology said no hole. The two
    extra loops, one on the top face and one on the bottom, are exactly what make the formula
    balance. Leaving R out is not a simplification, it is wrong.
  - **The `draw_polysolid` note had the corner case backwards, in sign as well as size.** It said
    a wall round a bend holds less than its legs. Measured on a right-angle turn: a **centred**
    wall comes to exactly width×height×length, because the mitre gives back on the outside of the
    turn precisely what it takes on the inside; justified **left** it is short by width²×height,
    the corner block it no longer reaches, and justified **right** it is over by the same amount.
    The verification now checks all three, which is what stops the centred equality from being
    read as the corner simply going unnoticed.
  - **`clean_solid` removes nothing, and now says so.** Two constructed cases where SOLIDEDIT
    Clean should have applied both came back empty: it does not undo an imprint, because those
    edges separate faces the modeller treats as distinct even when they lie in one plane, and it
    finds nothing after a union, because AutoCAD merges coplanar faces during the boolean itself.
    History recording is turned off before the call so that "nothing was redundant" means the
    geometry, not a setting. The tool ships describing what it does not do rather than claiming
    an effect that cannot be demonstrated — and the image shows the imprinted line still on the
    box after the clean. The guarantee that does hold is enforced: the volume must not move.

  `copy_face` and `color_face` are struck — `Solid3d.CopyFaces` does not exist and per-face
  colour needs a `FullSubentityPath` route the managed API does not expose.
  `convert_to_solid`/`convert_to_surface` move to 4.2, where the other half of the conversion
  lives.

- **Phase 4.1, fifth tranche — the rest of SOLIDEDIT.** `acad-geometry-3d` 25 → 32, bank
  557 → 564: `extrude_face`, `offset_face`, `move_face`, `rotate_face`, `taper_face`,
  `delete_face`, `shell_solid`. Verified live **57/57**, and confirmed on an exported PNG.

  All seven name their faces the same way — `faceIndexes` from `list_solid_faces`, `nearPoints`
  which snap to the closest face, or **`facing`**, which picks the face pointing a given way so
  that "extrude the top face by 50" needs no list call first. A direction aimed equally at two
  faces is refused, exactly as a point equidistant from two edges is.

  Every one of these can be handed a value AutoCAD accepts and quietly ignores, and every one
  then returns a healthy result over an unchanged solid, so a shared tail measures volume and
  topology before and after and refuses to call it a success when nothing moved. Each is checked
  against a volume worked out on paper from a 100 cube: extrude the top face by 50 → 1500000;
  offset all six faces by 10 → a **120** cube, not a 110 one, because each face follows its own
  normal; rotate the top face 45° about its near edge → the wedge 500000, and −45° → 1500000;
  taper one side 45° → the same pair. The sharpest is `delete_face`: fillet an edge, then delete
  the face the fillet made, and the volume must come back to **exactly** 1000000 — a partial
  removal leaves a perfectly valid solid and only that return says the feature really came off.

  **Three defects, two of them in work already committed:**

  - **`list_solid_faces` was reporting normals with an arbitrary sign, and the check that should
    have caught it compared `abs()` of each component** — so a normal pointing *into* the solid
    passed exactly as well as one pointing out. It surfaced on a filleted box: seven faces, with
    `(0,0,-1)` twice and `(0,0,1)` never. Three arbitrary sampled points give a plane but not a
    side. Normals now come from **Newell's method over the ordered vertices of the exterior
    loop** — a Brep traverses that loop with the material on one consistent side, so the winding
    carries the outward direction. This mattered beyond the report: `facing` picks by normal, so
    "the top face" could have acted on the bottom one.
  - A normal is now reported **only when the boundary is genuinely flat**, checked by sampling
    along every edge rather than at the corners, because a fillet's quarter-cylinder can have
    coplanar corners with a curved face between them. A curved face has no single normal and gets
    `null`; `facing` then refuses rather than picking a side. The verification uses this to find
    the fillet face — by the absence of a normal, not by a guessed index.
  - **`shell_solid` promised something that does not work.** It advertised "name no faces and the
    void is sealed inside"; `ShellBody` throws `IndexOutOfRange` on an empty selection. The claim
    was withdrawn rather than the failure papered over, and the description now points at
    `boolean_ops.subtract_solids` for that shape.

  And one documentation defect caught by measuring the sign conventions before writing them down:
  **shell thickness is positive OUTWARD**, the opposite of what the description first said. On a
  100 cube open at the top, −10 leaves a cavity of 80×80×90 and comes to 424000 while +10 grows
  the outside to 120×120×110 and comes to 584000. Both are valid shells and only the sign tells
  them apart, so someone asking for a 10 mm wall would have got a part 20 mm bigger in every
  direction with no error. The verification now checks **both** numbers.

  `shell_solid` ships here after being wrongly struck as unbuildable — see the previous entry.
  `copy_face` is struck for real: `Solid3d.CopyFaces` does not exist.

- **Phase 4.1, fourth tranche — the face/edge family, and the addressing scheme it was blocked
  on.** `acad-geometry-3d` 21 → 25, bank 553 → 557: `list_solid_edges`, `list_solid_faces`,
  `fillet_edge`, `chamfer_edge`. Verified live **65/65**, and confirmed on an exported PNG.

  The roadmap listed this family as needing "a `SubentityId[]` addressing scheme first", and that
  is the real work here. Every SOLIDEDIT operation in the managed API — `FilletEdges`,
  `ChamferEdges`, `ExtrudeFaces`, `TaperFaces`, `OffsetFaces`, `RemoveFaces`, `TransformFaces`,
  `ShellBody` — takes a `SubentityId[]`, and a `SubentityId` is an opaque handle into the
  boundary representation that a caller on the other end of a JSON pipe cannot spell and that
  would not survive the round trip. So the scheme is: enumerate the Brep, hand back an **index
  plus the geometry of every slot**, and take back either that index or a point in space to snap
  to. Reporting the geometry is what makes the choice checkable — an index alone is a number the
  caller has to trust, and one pointing at the wrong edge is still an integer in range. The first
  thing the verification does is prove the twelve midpoints reported for a 0..100 cube *are* the
  twelve midpoints of a 0..100 cube, and that the six normals are the three axis directions.

  A point equidistant from two edges is **refused**, not snapped to whichever sorted first.

  Both operations are checked against arithmetic: filleting a straight edge of length L with
  radius r removes exactly `L·r²·(1 − π/4)`, so 100 and 10 must leave 997853.9816; chamfering
  with equal distances d removes `L·d²/2` = 5000. Doubling the radius must remove four times as
  much, which is the control showing the radius drives the cut rather than merely being accepted.

  **Three defects and one API trap, all found by measuring:**

  - `new Brep(entity)` gives faces and edges that throw `MissingSubentity` the moment you ask for
    `SubentityPath`. Since that is the one thing the whole family needs, the entity constructor is
    useless for anything but counting — which is why `imprint_edges` never hit it. The Brep must
    be built from a `FullSubentityPath` rooted on the solid's `ObjectId`. Found in **one** deploy
    by wrapping each step of the Brep walk with its own label and rethrowing with the step name
    and the `ErrorStatus`; six candidate causes would otherwise have cost six deploys.
  - **An oversized fillet destroys faces and AutoCAD returns success.** Measured on a pristine
    cube: radius 300 on a 100 face was accepted, swallowed a whole face — six down to five — and
    reported a volume a third smaller with no complaint. `fillet_edge` and `chamfer_edge` now
    refuse when the face count drops, and because the refusal throws, the transaction is aborted
    rather than committed and the solid is left exactly as it was. That rollback is **asserted**,
    not assumed: after the refusal the verification re-measures 1000000 and twelve edges.
  - The refusal reports **the largest size that does fit**, found by bisection on throwaway
    clones appended inside the same transaction that is then aborted, so they never existed. The
    search checks that the index→edge mapping on a clone is the same mapping before trusting it,
    and the verification then fillets at the reported maximum to prove the number is not a
    fiction. `allowFaceLoss: true` remains for the deliberate case, and still flags what it did.
  - The identity `L·r²·(1 − π/4)` has a **domain**: it holds only while the fillet fits inside
    both faces meeting at the edge. At r=150 on a 100 face the arc runs off them and the shape is
    no longer that prism. Checked on both sides of that boundary.

  Two of my own expectations were wrong before the tool was: r=60 on a 100 face fits perfectly
  well, and r=300 is not refused by AutoCAD at all. Both were written as "too large is refused"
  and both would have condemned a working tool.

  Also corrected in the roadmap: **`shell_solid` is buildable after all.** It was struck on a
  probe that asked for `ShellSolid`; the method is `ShellBody(SubentityId[], double)`. A struck
  row is a decision never to revisit something, so a name typo in a probe is expensive in a way a
  compile error is not. The same sweep confirmed `SubDMesh.CreateBox` genuinely does not exist,
  which lands on phase 4.3.

- **Phase 4.1, third tranche — the third array, and imprinting.** `acad-modify` 18 → 19 and
  `acad-geometry-3d` 20 → 21, bank 551 → 553: `array_path`, `imprint_edges`. Verified live
  **60/60**, and confirmed on an exported PNG.

  `array_path` completes the array family — `array_rectangular` and `array_polar` have been
  here since the start and the path one simply was not. Its whole difficulty is that copies
  must be spaced by distance measured **along the curve**, not by the straight-line gap between
  neighbours. On a bend those are different numbers, and a tool that used the wrong one still
  returns the count that was asked for, still puts every copy on the curve, and still looks
  right in a JSON result — it just bunches the copies round the outside of every turn. So the
  check is arithmetic: on a quarter circle of radius 200 the arc length is π·200/2 = 314.1593
  and every gap between neighbours must be that over four, 78.5398. Measured from the drawing
  rather than from the tool's own report, and read a second way through
  `get_distance_to_entity`. The **control** that gives that number meaning: on this bend the
  chord between neighbours is measurably shorter than the arc, so the two answers really are
  distinguishable here — and on a straight path, where they must agree, the copies land at
  0, 100, 200, 300, 400.

  Alignment is **relative** to the tangent where the path starts, not absolute. Rotating each
  copy by its own tangent angle is the obvious implementation and it is wrong: array a post
  along a road drawn leaving at 45° and every post comes out lying on its side, including the
  one sitting exactly where the source was. Relative means the first copy keeps the source's
  orientation and the rest turn only by how much the path has turned since. Visible in the
  export: the bar at the arc's start is still horizontal, the one at the far end has turned the
  90° the quarter circle turns, and the middle one sits at 45°, spanning 21.2132 each way.
  `alignToPath: false` is the control — all four copies stay horizontal.

  `imprint_edges` presses a curve lying on a face into that face, dividing it. The claim that
  separates it from a cut is that it adds **edges, not material** — and a tool that quietly cut
  would report more faces too, so face counts alone prove nothing. The volume is what gives it
  away, so the handler reads the boundary representation and the mass properties before and
  after and refuses to report an imprint if the volume moved, or if the face and edge counts did
  not. A box's six faces become seven; the volume stays at 4000000. Confirmed on the image with
  the source curve **erased**: the second box's top face is still divided, and that edge can
  only have come from the imprint.

  `array_path` lives in `acad-modify`, not in a 3D category — the same reasoning that struck the
  six 3D transforms in the 4.1 review. A router choosing between three arrays should find all
  three in one place, and the path can be a 3D polyline or a helix as easily as a 2D arc.

- **Phase 4.1, second tranche — slicing and interference.** `acad-geometry-3d` 18 → 20, bank
  549 → 551: `slice_solid`, `interfere_solids`. Verified live **39/39**.

  Together because both are checkable against **arithmetic**. Cutting conserves volume: a 100
  cube cut at z=30 must give 700000 and 300000 and they must sum back to the million that went
  in — the only place a cut that lost or duplicated material shows up, since both halves are
  perfectly good-looking solids either way. The tool refuses to report a cut whose halves do not
  add up, and a diagonal plane is checked too, where the halves are wedges and a mistake is more
  likely than on a square cut. Interference between boxes spanning 0..100 and 40..140 has an
  overlap of exactly 60³ = 216000.

  `interfere_solids` is **not** `boolean_ops.intersect_solids`. That one *replaces* the target
  with the common volume — it answers the question by destroying the thing you asked about.
  This leaves both parties standing and hands the clash back as a third solid, which is what a
  services-coordination check needs. Proved with a **control**: the same pair of boxes put
  through `intersect_solids` afterwards, showing the target really is consumed. Without that arm,
  "the originals survived" could just mean nothing in this drawing ever gets consumed.

  One defect found and fixed: **a cutting plane that misses the solid entirely reported success.**
  `Slice` leaves the solid whole and returns no second half, so the result read `volumeBefore:
  125000, keptVolume: 125000` with a note saying the other half was gone — every number honest,
  the whole useless, and that sentence simply false. The guard checked for a *zero* kept volume,
  which a miss never produces. It now refuses, naming the plane and the solid's own extents so
  the caller can see where the plane should have been.

- **Phase 4.1, first tranche — sweep, loft and helix.** `acad-geometry-3d` 15 → 18, bank
  546 → 549: `sweep_curve`, `loft_curves`, `draw_helix`. Verified live **50/50**.

  `extrude_curve` pushes a profile in a straight line and `revolve_curve` spins it; these three
  are the rest of how a solid is made from a curve — along an arbitrary **path**, between
  **cross sections**, and the helix that is the usual path for a spring or a thread.

  These are unusual in this project: they can be checked against **arithmetic** rather than
  against another call of the same code, and that is what the verification does. Pappus gives the
  swept volume as the profile area times the distance its centroid travels; two equal loft
  sections a distance apart make a prism and a taper must match the frustum formula; a
  constant-radius helix unrolls into a right triangle. None of those numbers come from AutoCAD.

  It earned its keep immediately. **A helix asked for 5 turns came back with 300**, measuring
  75364 against the 1291.95 the arithmetic called for — a factor of 58 that no "did a curve
  appear" check would ever notice. `Height`, `Turns` and `TurnHeight` are three views of one
  geometry and setting any one recomputes another:

  | order | result |
  |---|---|
  | `Turns=5` then `Height=300` | **300 turns** — height ÷ the default turn height of 1 |
  | `Height=300` then `Turns=5` | **height 5** — turns × that same turn height |
  | `TurnHeight=60` then `Turns=5` | 5 turns over 300, correct |

  The turn height is the one that drives the other two. A **read-back guard** now refuses to
  report a helix that is not the one asked for, and it is what caught both wrong orders rather
  than shipping either.

  Two smaller findings. `Region.CreateFromCurves` **throws** on an open curve instead of
  returning an empty collection, so a count check never ran and callers got a bare
  `eInvalidInput` where a sentence about closed profiles belonged. And one verification
  expectation was wrong rather than the tool: it demanded that a bent path give a volume
  *differing* from area × length, when the profile rides the path and Pappus makes them equal —
  measured 74021.917 against 74022.033, agreement to six figures. The check now asserts that
  equality, which is the stronger test.

- **Phase 3.3 COMPLETE — paragraph format and bullets.** `acad-annotations` 24 → 26, bank
  544 → 546: `set_paragraph_format`, `mtext_bullets_numbering`. Verified live **37/37**. The
  phase closes at **14 built, 3 blocked by the API, 1 struck as a duplicate**.

  `set_paragraph_format` writes its code onto **every** paragraph, not just the first —
  formatting one and leaving the rest reads as a tool that half worked — and replaces any code
  already there rather than stacking them. Alignment needs a **width** to align within: a
  zero-width MText is exactly as wide as its longest line, so ranging it right would move
  nothing, and that case is refused with the reason instead of quietly doing nothing.

  `mtext_bullets_numbering` gives each item a **hanging indent** as well as its marker, so a
  wrapped line lines up under the words and a two-line item does not read as two items. Any
  marker already present is stripped first — the check a count cannot make, since
  `1.  • ALPHA ITEM` is one paragraph with one marker as far as any tally is concerned.

  **The measurement was wrong before the tools were.** An indent was checked against the MText's
  own extents, which read `0 → 0`. Those extents are the **width box**, not the ink: an MText 400
  wide at x=0 measures 0..400 whether the text inside is flush left, indented or ranged right.
  The exported image plainly showed the indent working. The verification now explodes a copy with
  `explode_mtext_to_text` — built in the previous tranche — and reads where the lines actually
  land.

- **Phase 3.3, fifth tranche — converting between text and MText.** `acad-annotations` 22 → 24,
  bank 542 → 544: `text_to_mtext`, `explode_mtext_to_text`. Verified live **38/38**.

  These two are inverses, so the strongest available check is a **round trip**: three lines
  become one MText, that MText becomes three lines again, and the words come back in the same
  order. Either tool alone can be wrong in a way that looks fine; both wrong in exactly
  compensating ways is a great deal less likely.

  What the round trip alone would **not** catch is **order**. Combining lines in whatever order
  the handles arrive gives a paragraph with its sentences shuffled, and every count stays
  correct — three in, one out, all the words present. So the tool sorts into reading order, down
  the page and then across, reports the order it used, and the verification creates its three
  texts deliberately scrambled (middle, bottom, top) and asserts the MText reads top to bottom
  anyway. Lines within half a text height of each other count as one line, or two labels side by
  side would be split into separate paragraphs by a rounding error.

  `explode_mtext_to_text` gives one piece per **line**, not per word, and says plainly what does
  not survive: columns, a background mask and a stacked fraction have nowhere to go on a
  single-line text.

- **Phase 3.3, fourth tranche — symbols and stacked fractions.** `acad-annotations` 20 → 22, bank
  540 → 542: `insert_symbol`, `stack_fraction`. Verified live **59/59**.

  Both write **control codes** into a string, so both can succeed at writing and fail at meaning
  — and each needs a different proof. Getting that right took one wrong assumption first.

  `insert_symbol` splits into two cases that can be proved to different depths. Where the symbol
  goes in as the **character** — every MText, and single-line text for anything outside
  `%%c`/`%%d`/`%%p` — the entity is read back and the glyph has to be there. Where it goes in as
  a **control code** there is nothing to read: `DBText.TextString` returns what is *stored*, so
  `%%c` stays `"%%c"` and becomes a diameter sign only when AutoCAD draws it. The first version
  demanded the glyph back from DBText and failed six checks on a tool that was working correctly.
  The result now reports `viaControlCode`, and those cases are confirmed on the exported image —
  the same honest limit as the background mask.

  `stack_fraction` cannot be proved on text at all: a stacked `1/2` renders as the same three
  characters as an unstacked one. It is proved on the drawn **extent**, against an identical
  MText left unstacked as a control, and the tool refuses to report success unless the text got
  taller — which is what putting the halves on two levels does.

  DBText and MText do **not** take the same codes, and the tool keeps them apart: writing the
  MText form into a DBText would leave a literal `\U+2205` on the sheet.

- **Phase 3.3, third tranche — how an MText presents itself.** `acad-annotations` 18 → 20, bank
  538 → 540: `background_mask_mtext`, `mtext_column_settings`. Verified live **49/49**.

  These two differ in **what can be proved about them at all**. A background mask is drawn behind
  the text and changes no extent, so there is nothing geometric to measure — saying so is better
  than inventing a number, and the verification asserts the extent is *unchanged* so the claim in
  the tool's own note stays true. The mask is proved on an exported image instead: a hatch behind
  the text, and the hatching is visibly interrupted around the letters and unbroken everywhere
  else. Columns are the opposite: they must **reflow** the text, so the drawn extent is read
  before and after — one 200-wide block becomes 640 across three columns of 200 plus two gutters
  of 20.

  Getting columns working took three wrong turns, and the shape of the mistake is worth keeping:

  - The first `eNotApplicable` came from **reading `ColumnCount` to build the result**, before any
    edit ran. Those getters throw when the MText has no columns — they are *unanswerable*, not
    unset, exactly like `Polyline.ConstantWidth` on a polyline whose segments differ.
  - Reading that error as "AutoCAD rejects `ColumnType`", I **reordered the assignments on a
    guess**. That fixed nothing and added a second, real failure.
  - Only after making each assignment report *which property* was refused did the answer appear:
    `ColumnWidth throws NotApplicable while the MText is still NoColumns`. The original order was
    right; the type must be set first and the column geometry after it.

  A `SafeColumn<T>()` helper and the per-assignment attribution both stay, so the next
  `eNotApplicable` in this family says where it came from.

  Also measured and reported rather than hidden: **`mode='none'` does not restore the MText's
  original wrap width.** It keeps whatever the columns made it — 640, not the 200 it was created
  with — so the text returns as one *wide* block. The result carries `mtextWidthBefore` and
  `mtextWidth` and the note says this outright.

- **Phase 3.3, second tranche — where text sits and how big it is.** `acad-annotations` 15 → 18,
  bank 535 → 538: `set_text_justification`, `text_fit`, `scale_text_in_place`. Verified live
  **60/60**. `justify_text` from the roadmap list is struck as a second name for
  `set_text_justification`.

  What the three have in common is that **the obvious implementation moves the text** and reports
  success either way, so every assertion is a position or a size read back off the entity.

  `set_text_justification` first tried to compute where the anchor *ought* to go, reading the
  corner of the extents box each justification names. That was guessing, and it worked by luck:
  `BottomRight` moved the text by **3.333** — exactly the descender depth, because "ANCHOR TEST"
  is all capitals so the bottom of its box *is* the baseline while Bottom justification anchors
  below descenders — and `BaseLeft` threw `eNotApplicable`, since that is the default
  justification and AutoCAD uses `Position` rather than `AlignmentPoint` for it. The box tells
  you where the ink is, not where a justification line is. Now the displacement is **measured**:
  set the justification, see where the text landed, move it back by the difference. Exact for
  every justification, and it needs to know nothing about what any of them mean. `correctedBy`
  reports how far it jumped, which is the size of the mistake the tool exists to undo.

  `scale_text_in_place` is verified against a **control** — `modify.scale` on an identical pair.
  That one drags the far text out to 1000 while this leaves its twin at 500, visible as two rows
  in the PNG. Without the control arm, "it did not move" proves nothing.

- **Phase 3.3, first tranche — finding text across a drawing.** `acad-annotations` 12 → 15,
  bank 532 → 535: `list_text_by_pattern`, `find_replace_text`, `export_text_content`. Verified
  live **63/63**.

  All three share one scanner, because the whole difficulty is in it. **Text lives in six
  places**, and a search reading only DBText and MText misses most of a real sheet: a room name
  is MText, a level tag is a block **attribute**, a note is an MLeader, a schedule is a **table**,
  a dimension can carry a text override. Every one is read, and `scannedByType` is reported so
  "no matches" can be told from "no matches in the two types I bothered to look at". Each type is
  asserted by handle in the verification, and the exported CSV was read back outside the tool.

  The compiler caught a trap nothing downstream would have: **`Table` derives from
  `BlockReference`**, so the block branch swallowed every table and schedule text would never
  have been scanned. The table case has to come first.

  The second difficulty is **formatting codes**. `MText.Contents` stores `\fArial|b1|i0;`
  alongside the words, so a replacement made on that string can land inside a code — changing a
  font instead of the text, or breaking the entity. Searching therefore runs on the **rendered**
  text, and replacement only touches an item when the pattern matches the same number of times
  in the stored string as in the rendered one; otherwise it goes on the skipped list with that
  reason. Verified with an MText whose *code* contains the search word while its visible text
  does not: the tool changes nothing and the entity still renders `PLAN`.

  `dryRun` is checked by reading the entities back, not by trusting the flag.

- **`dimensions.quick_dimension` — phase 3.2 COMPLETE.** `acad-dimensions` 24 → 25, bank
  531 → 532. Verified live **34/34**. Phase 3.2 finishes at 8 built and 6 blocked by the managed
  API, every one of the six recorded in [KNOWN-GAPS](docs/KNOWN-GAPS.md) §B with the compiler
  error behind it.

  The chain tools already in the bank are handed a list of points; this one reads them off
  lines, polylines, arcs, circles and points, projects them onto whichever axis the geometry is
  spread along, and builds the chain. That is the part a caller cannot do from outside without
  first reading every entity.

  Its trap is duplicates. Three walls laid end to end give **six** key points of which only
  **four** are distinct — each shared corner is contributed twice — and a chain built without
  merging them contains zero-length dimensions: drawn as nothing, read as 0 if you go looking,
  and counted all the same. Coordinates within a tolerance are merged and **both** counts are
  reported. In continuous mode the measurements are additionally checked to sum to the
  geometry's own span, because a dropped or doubled dimension otherwise leaves a perfectly
  plausible list of numbers.

  `auto` picks the axis rather than assuming X: a tool that always chose X would look correct on
  every horizontal test and be wrong at the first vertical wall, so both are verified. `baseline`
  mode is asserted by its **values** (300, 800, 1000) against continuous (300, 500, 200) — the
  difference between the modes is invisible in a count of dimensions. Entities with no key
  points go on a `skipped` list with a reason instead of being passed over silently.

- **Phase 3.2, second tranche — tolerances, style reset, spacing, arc symbol.**
  `acad-dimensions` 20 → 24, bank 527 → 531: `dimension_tolerance`, `dimension_update`,
  `dimension_space`, `dimension_arc_symbol`. Verified live **56/56**.

  `dimension_update` was nearly struck as a second name for `set_entity_dimstyle`, and the
  difference was **measured rather than argued**: a tolerance override went on two identical
  dimensions, one through each tool. `set_entity_dimstyle` left the override standing — it only
  assigns `DimensionStyle` — while `dimension_update` re-applies the style's own values through
  `SetDimstyleData` and clears it. A dimension can otherwise wear the right style name and still
  print the wrong thing. Both `toleranceOverrideBefore` and `toleranceOverrideAfter` are
  reported so the distinction stays checkable from outside rather than living in a description.

  The first run of that experiment was **void and would have passed anyway**. It hardcoded the
  style name `Standard`, which a metric template does not have, so the control arm errored and
  only the `dimension_update` arm ran: "the override is gone" read as proof of a difference while
  showing nothing whatever about the other tool. The script now reads the drawing's own styles.

  `dimension_space` is asserted by **offset**, not by "it moved" — a tool that piled every
  dimension in one place, or reversed the order of a chain, would still return a tidy list of new
  positions. Two of the four test dimensions start 10 apart so a mis-sort would swap them.
  `dimension_tolerance` separates `limits` from `deviation`, which set **opposite** flags
  (`Dimlim` versus `Dimtol`), and each is asserted on its own.

- **Phase 3.2, first tranche — editing a dimension after it is placed.** `acad-dimensions`
  17 → 20, bank 524 → 527: `dimension_jogged_radius`, `dimension_oblique`,
  `edit_dimension_text`. Verified live **70/70**.

  The category could place eleven kinds of dimension and change none of them afterwards. All
  three of these change how a dimension **looks**, and the thing none of them may change is what
  it **measures** — so each reads `Measurement` before and after and refuses to report success if
  it moved. A tool that edited the geometry instead of the presentation would place a perfectly
  good dimension reporting a different number, and every "did it work" check would pass.

  `dimension_jogged_radius` shipped broken once and the tally did not notice. It produced a
  `RadialDimensionLarge` with the true radius as its measurement, a false centre distinct from
  the real one, and the requested jog angle — 63 checks green — and **drew as a dead straight
  leader with no jog at all**, because the default jog point came out collinear with the
  centre-to-chord line and a bend needs somewhere to bend to. Measured against a plain radial
  dimension on an identical arc:

  | jog point | drawn width |
  |---|---|
  | collinear (the shipped default) | **3.542** — the width of the text and nothing else |
  | 60 aside | 63.1 |
  | 120 aside | 123.1 |

  Fixed three ways: the default jog point is now offset perpendicular by `0.15 × radius`; a
  caller-supplied collinear point is **refused**, because the result is indistinguishable on the
  sheet from `dimensions.radial`; and `jogOffset` is reported so the bend is checkable from
  outside. The verification gained the check that would have caught it — drawn width against a
  plain radial control, since 3.542 means nothing alone and everything next to 63.1.

  `edit_dimension_text` carries AutoCAD's three text conventions rather than leaving them to be
  discovered on a plotted sheet: `""` means *show the measurement* (the default state, not
  blank), `"<>"` embeds the measurement in your own text, and a **single space** is what
  suppresses it. Each is asserted separately.

- **Phase 3.1 — 2D geometry editing: 29 tools over ten tranches.** Bank 495 → 524;
  `acad-geometry-2d` 33 → 60, `acad-modify` 16 → 18. Every tranche was verified live against a
  running AutoCAD and confirmed on an exported PNG, never on a return code:
  polyline vertex editing (51/51), break/divide/measure (55/55), scale and rotate by reference
  (37/37), draw order, transparency and wipeouts (43/43), splines (42/42), `lengthen_curve` and
  `draw_ellipse_arc` (52/52), `boundary_from_point` and `region_from_boundary` (27/27),
  `blend_curves` (26/26), multiline editing (41/41), and `fit_polyline` with `stretch_window`
  (72/72).

  **Phase 3.1 is complete.** Two of its thirty entries are struck rather than deferred, both
  because they were listed under a name the bank does not use and a scan for the literal name
  reported a gap where the capability already existed: `draw_construction_geometry` (AutoCAD has
  exactly two construction entities and `draw_xline`/`draw_ray` already draw both) and
  `align_objects` (`modify.align` has done this since the original 18).

  Three things this phase established, each of which had first produced a green tally that
  meant nothing:

  - **A check that passes when the tool is absent is not a check.** The runner counted
    `UnknownTool` as a refusal, so four unregistered tools read as three passing `expect_fail`
    assertions. The runner now fails a missing tool loudly, and a separate idempotence guard
    that was matching a tool's name inside a *handler body* — and therefore skipping its
    registration — was removed.
  - **A comparison without a control still produces a number.** `draw_ellipse_arc` was
    "proven" by comparing a 200-major ellipse against a 100-radius circle: true, and irrelevant.
    Both arcs now share a major axis, so the length difference is the ratio and nothing else.
  - **A tally cannot see the drawing.** `blend_curves` passed every numeric assertion while
    detouring to y = −9.7 outside its own joins; only the PNG showed it. `draw_hatch` silently
    ignored a `colorIndex` that is really `color: {r,g,b}`, so a draw-order run scored 41/41 on
    an all-black image proving nothing about draw order. Both verifications now assert that the
    thing they intend to look at is actually distinguishable before counting anything.

  Three tools were **withdrawn rather than shipped approximate**, each with the measurement
  behind it recorded in [KNOWN-GAPS](docs/KNOWN-GAPS.md) §B: `blend_curves` `continuity=smooth`
  (two implementations, one detoured outside its joins and the other was a silent no-op because
  `Spline` normalises tangent vectors — both came out at 74.374), `set_object_transparency`
  `byLayer`/`byBlock` (the constructor compiles and throws `eInvalidKey`), and `add_sheet_view`.

- **`viewports.set_viewport_ucs` and `viewports.set_viewport_annotation_scale`** — the two
  tools withheld when `acad-viewports` shipped, waiting on `acad-ucs` and `acad-annotative`
  respectively. Both of those exist and are verified, so a UCS or a scale is now something the
  caller lists by name rather than something the tool has to guess at. Bank 413 → 415.
  - `set_viewport_ucs` gives one paperspace viewport its own coordinate system, so a sheet can
    annotate a rotated wing of a building in that wing's own coordinates while the neighbouring
    viewport stays on the world axes. `ucs` is **required** here — everywhere else in the
    codebase an absent `ucs` means WCS (rule 43), but on the tool whose only job is to set the
    coordinate system that default would silently undo a deliberate setting. Pass `"world"` to
    clear. `"current"` is rejected: binding a saved sheet to transient session state would make
    the viewport's meaning depend on what the user last clicked.
  - `set_viewport_annotation_scale` sets what annotative text and dimensions plot at in that
    window, and which annotative objects appear in it at all. The viewport's zoom scale follows
    by default, the way AutoCAD's own UI keeps the two linked — a viewport whose annotation
    scale disagrees with its zoom means text sized for 1:50 on a window drawn at 1:100.
    `syncViewScale: false` for the deliberate case; the result always reports both numbers.
  - Both refuse an unknown name and list what is available, rather than falling back to WCS or
    to a default scale.
  - The contract was written into [rule 43](docs/engineering-rules/43-coordinate-systems.md)
    before the code, which is the practice that got `acad-ucs` to 13/13 first time.
  - Verified live: **23/23**, including an independent re-read through `get_viewport_info`
    rather than trusting what `set` returned, and both branches of `syncViewScale`.
  - `ViewportInfo` gained `ucs` and `annotationScale`. Without declaring them on the backend
    DTO, System.Text.Json drops them and the client sees a viewport with no UCS, reported as a
    success — the failure this codebase has hit three times.
  - Honest note on `UcsPerViewport`: the tool sets it explicitly, because without it AutoCAD
    does not store the UCS against the viewport. It turns out to be `true` on a freshly created
    viewport anyway, so this is belt-and-braces rather than a demonstrated necessity.

- **The catalogue-vs-consumer contract is now a test, and CI runs it.** This was the
  highest-value missing test in `docs/KNOWN-GAPS.md`, recorded there as blocked on a
  plugin-level test project that could never exist because CI has no AutoCAD.
  **That premise was wrong.** The blocker was not that the test needed AutoCAD; it was that
  the catalogues happened to sit next to code that does. They are pure data — names,
  millimetre dimensions, prose, standards citations.
  - New `src/AcadMcp.Shared/Catalogs/`: `FurnitureCatalog`, `PlumbingCatalog`, `HatchCatalog`,
    plus the shared `PREFIX-FAMILY-SUBTYPE-W-D` naming helper and `CatalogNameException`.
    Each catalogue owns both the listing a discovery tool publishes and the resolution its
    action tool performs, so the two cannot drift apart in the first place. The plugin keeps
    what genuinely needs AutoCAD — turning a resolution into geometry — and
    `BuildBlockGeometry` is now one `Resolve` call followed by dispatch.
  - New `CatalogContractTests` — 27 tests. The load-bearing ones assert that every name each
    listing publishes is accepted by the tool it points at. **Verified against the real
    defect:** reintroducing the missing family lookup makes the suite fail with
    `list_furniture_catalog publishes 26 names; insert_furniture rejects 11 of them`, naming
    every one — matching the historical record exactly.
  - The hatch catalogue gets a check the other two cannot have: every material preset names a
    pattern, and that pattern must exist in the pattern catalogue. A dangling preset is
    invisible — `list_hatch_patterns` would not show it while `apply_material_preset` would
    ask AutoCAD to load it anyway.
  - `MaterialPreset.Angle` became `AngleDeg`. The unit belongs in the name; hatch angles are
    stored in radians on the entity and degrees in the catalogue, which is exactly the
    confusion a bare `Angle` invites.
  - 148 → 175 tests, 0 warnings, `AcadMcp.Shared` still clean on both `net8.0` and `net48`
    (the catalogue code avoids `System.Index` and `string.Join(char, …)`, neither of which
    exists on the latter).

- **Continuous integration — `.github/` now exists.** `ci.yml` builds and tests on Windows,
  checks manifest/code sync, runs the whole-tree repository gate, and lints/type-checks/tests
  the Python vision sidecar on Linux. `codeql.yml` runs `security-and-quality` over C# and
  Python weekly and on every PR. Plus `dependabot.yml` (NuGet, pip, Actions — with the AGPL
  `[ml]` extra deliberately excluded), `CODEOWNERS`, three issue forms, a PR template, and
  `SECURITY.md` / `CONTRIBUTING.md` / `CODE_OF_CONDUCT.md`.
  - New `src/AcadMcp.NoAcad.slnf`. CI cannot build `AcadMcp.Plugin` or
    `Companion.Host`: they reference AutoCAD's managed assemblies, which Autodesk does not
    redistribute and no runner has. Nothing else in the tree depends on them, so the filter
    is clean — but a green check on a plugin change means only that the server compiles.
    Said plainly at the top of `ci.yml`, in the PR template and in KNOWN-GAPS C6, because it
    is the kind of limit that otherwise gets rediscovered as a surprise.
  - `mypy --strict` on the sidecar runs advisory (`continue-on-error`). It was configured
    from the start but never enforced, and had drifted to 26 errors. Tracked as KNOWN-GAPS C5.

### Fixed

- **`annotations.set_mtext_frame` was built, measured and withdrawn.** The 2025 managed API has
  no `TextFrame` or `DrawFrame` on `MText` — both fail `CS0246` — and the only frame-ish property
  it does expose, `ShowBorders`, accepts the assignment, reads back `true`, and **draws nothing**.
  Measured two ways that agree: the entity's extents were 300 × 10 before and 300 × 10 after (and
  400 × 43.3 unchanged on a second MText), and the exported image shows `FRAMED TEXT` with no
  border around it. A frame is drawn *around* text, so it would have to push the extents out.

  A tool that sets a property and changes the drawing not at all is worse than a missing one,
  because it reports success. The `[McpTool]` attribute is removed so it is not advertised — the
  same course taken with `modify.undo`/`redo` — while the handler and proxy stay, so restoring one
  attribute brings it back if a later AutoCAD makes `ShowBorders` mean what its name suggests. The
  catalogue test now asserts it **absent**, so it cannot return unnoticed. Recorded in
  [KNOWN-GAPS](docs/KNOWN-GAPS.md) §B.

- **`annotations.add_mtext` exposed its wrap width under the name `widthFactor`.** In AutoCAD a
  width *factor* is horizontal letter compression; this argument is the width in drawing units at
  which the text wraps. A caller passing `widthFactor: 0.8` expecting condensed text got an MText
  0.8 units wide — narrow, wrong, and entirely plausible-looking. The tool now accepts **`width`**,
  which wins when both are given; `widthFactor` still works so nothing breaks. Found while writing
  a verification that passed `width` and got an unwrapped 2728-unit line.

- **Verification scripts could silently measure two different drawings.** Every MCP category runs
  in its **own backend process**, and when more than one drawing is open those processes can bind
  to **different documents** — so a handle created through the `annotations` session resolves, in
  the `geometry-2d` session, to a different entity or to nothing at all. Two tools measure two
  drawings and every assertion still returns a number.

  Measured while it was happening: `annotations` wrote `SELFTEST` at y=7000 and read it back
  fine, `geometry-2d` found nothing in that window, and `files.list_documents` reported
  `Rysunek1.dwg` and `Rysunek2.dwg` both open. The cause is the scripts themselves — each calls
  `new_document` and none ever closed the drawing it replaced, so runs accumulated. Latent in
  every verification written so far; it had simply never mattered because the documents happened
  to agree.

  `scripts/verify-*.py` now start from a `fresh_drawing()` helper that makes the new drawing,
  **closes every other one**, and then **proves** cross-session agreement by placing a probe in
  one category and reading its bounding box in another before anything is measured.

  Noted while investigating and not yet chased: `files.list_documents` reports `isActive` as
  null for every document, so it cannot currently answer which drawing is the active one.

- **An unknown `dimStyle` name was silently swapped for the current style, in all 13 dimension
  tools that take one.** `AcadEnv.ResolveDimStyleOrCurrent` fell through to `db.Dimstyle`
  whenever the name it was given did not exist, so a caller asking for a style that is not in the
  drawing got a different one and a success. Measured on `dimension_update` with
  `dimStyle: "NoSuchStyle"`, which returned `affected: 1` having applied ISO-25.

  An absent name still means the current style — that is the `OrCurrent` in the helper's name and
  a legitimate default. A name that was typed and cannot be found never is: it is now refused,
  and the refusal lists the styles the drawing actually has plus the current one. Found while
  verifying a single new tool; it reached every tool in the category.

- **`modify.align` skipped the rotation whenever it was exactly 180°.** The axis came from
  `sV.CrossProduct(tV)`, guarded by `axis.Length > 1e-9`. That cross product vanishes when the
  two directions are **parallel**, which covers two opposite cases — already aligned, and
  exactly reversed — and the guard treated both as "no rotation needed". So a reversal moved
  nothing, turned nothing and returned `affected: 1`, the only field the tool reported.

  Measured against a 90° control, so that "it did not move" could not be read as "the tool is
  broken":

  | | before | after |
  |---|---|---|
  | 90°, `(0,0)-(100,0)` → `(0,0)-(0,100)` | x 0..0, y 0..100 — correct | unchanged |
  | 180°, `(0,500)-(100,500)` → `(0,500)-(-100,500)` | **x 0..100, untouched** | x -100..0 |

  Any axis perpendicular to the source direction turns it through π; Z is the right one for the
  2D case, with a fallback to X when the source direction is itself along Z.

  `align` now also reports **what it did** rather than only how many entities it touched:
  `rotatedByDeg`, `factor`, `scaled`, and `sourceBLandedAt` with `distanceToTargetB`. That last
  pair is what makes the `scale` flag checkable from outside — without scale, source B only
  *points at* target B and stops short of it by a stated distance; with scale it lands on it,
  and the tool now refuses to report success if it did not. Verified live **32/32**.

  Found while writing `align_objects` from the phase 3.1 list, which turned out to be this tool
  under another name. The duplicate was dropped; the bug it exposed was not.

- **`hatches.draw_hatch_by_boundary` and `apply_material_preset_by_point` work again**
  (KNOWN-GAPS A1, the oldest entry on the broken-on-valid-input list). `Editor.TraceBoundary`
  has **two** independent requirements, and the code met neither:
  - It reads its seed point in the **current UCS**. Arguments here are WCS (rule 43), so the
    seed was silently offset by whatever the current UCS happened to be. Reproduced with a
    rectangle at (50000,50000)–(56000,54000), a seed at (53000,52000) plainly inside it and a
    UCS origin of (1000,2000): the seed was read as (54000,54000), exactly on the top edge, so
    TraceBoundary correctly found nothing — and the tool blamed the caller's geometry.
  - The region must be **visible in the current view**. Off-screen geometry returns an empty
    result rather than an error, which is indistinguishable from an unclosed boundary.

  Both conditions were measured separately; only a transformed seed *and* a view on the region
  succeeds. The tool now does both, and **restores the caller's view afterwards** — an agent
  asking for a hatch did not ask for its camera to move. Framing goes through a
  `ViewTableRecord`, not the command layer, which is what made `zoom_extents` itself fail with
  `eInvalidInput`. The failure message now reports the WCS seed, the UCS point it was taken to,
  and says so explicitly when they differ.

  Verified 10/10, asserting the hatch's bounding box equals the target rectangle rather than
  that a handle came back.

  **[Rule 26](docs/engineering-rules/26-acad-api-traps.md) trap 11d already described both
  mitigations as things the code performed.** It never did. The rule has been corrected, with
  the measurement table and the lesson: a rule claiming the code handles something is a claim
  to check, not evidence — this one sent every reader, including me, to look elsewhere for
  months.

- **The pre-commit gate's `Intent=` check reported tools that were not broken.** It used the
  regex `\[\s*McpTool\s*\((?![^\]]*Intent\s*=)`, and `[^\]]` stops at the first `]` — which
  in `callouts.insert_title_block` appears inside the description itself
  (`"Pass fields=[{key, value}, ...]"`). Two files failed the gate with no defect in either.
  Replaced with a scan that walks the attribute by paren depth while skipping string
  literals, so punctuation in prose is irrelevant. It now also names the offending tool and
  line instead of just the file, and reports the total checked: 401 attributes across 278
  files, all carrying `Intent=`.
- **The gate aborted on its own logging.** Windows PowerShell 5.1 wraps a native
  executable's stderr in `ErrorRecord` objects under `2>&1`, and the script runs with
  `$ErrorActionPreference = 'Stop'` — so `AcadMcp.Backend`'s startup banner, correctly
  written to stderr because stdout carries JSON-RPC frames, killed the validator self-check
  every time. Both native calls now go through one `Invoke-NativeCapture` helper.
- **The gate could run a stale test assembly and report the result as current.** `--no-build`
  keeps it inside its 60 s budget but will happily execute a months-old DLL; this surfaced as
  a failing schedules test that had been fixed long before. Added a staleness check that
  compares the newest source file against the assembly. A stale *pass* was the real risk.
- **Unit tests now run against `AcadMcp.Tests.csproj`, not `src/AcadMcp.sln`.** The solution
  also contains the two AutoCAD-dependent projects; the tests never touch them.
- **The gate located the test assembly in one configuration and then asked dotnet for
  another.** It searched `Debug` then `Release`, but ran `dotnet test --no-build` without
  `-c`, which defaults to Debug regardless. On a developer machine both exist so it never
  showed; CI builds Release only, so the gate's very first run failed asking for a Debug
  build that was never produced — and reported it as `dotnet test failed (exit 1):` with
  nothing after the colon. Now it remembers which configuration it found, prefers the newer
  of the two, passes `-c`, and when no line matches the failure patterns it falls back to the
  tail of the output instead of an empty message.
- **Four MCPBank category descriptions were too thin to choose from.** `acad-callouts`,
  `acad-schedules`, `acad-sections` and `acad-verticals` were 26–29 words of feature list.
  Rewritten as prose that also says what each category does *not* cover and which sibling to
  use instead — that description is the only thing an agent reads before picking a category.
  Also repaired a mojibake in `acad-verticals` (`WT �54`).
- **Vision sidecar: seven `raise HTTPException(...)` inside `except` blocks discarded the
  original exception.** Added `from ex` throughout, so the cause survives into the traceback.
  Same failure mode as a silent `catch {}`, which has cost this project a day twice.
- Vision sidecar: `EngineUnavailable` → `EngineUnavailableError`, `WeightsMissing` →
  `WeightsMissingError` (PEP 8); two best-effort cache writes made explicit with
  `contextlib.suppress`; imports sorted; `ruff format` applied across 8 files. `ruff check`
  and `ruff format --check` are clean and both gate CI. All 31 sidecar tests pass.

- **Companion: chat no longer freezes ("zacina się") in either plan or non-plan mode.** Root cause: the entire network path to the LLM had no time guard — the shared `HttpClient` used `Timeout.InfiniteTimeSpan` and the SSE read loop (`SseStream.ReadDataLinesAsync` → `ReadLineAsync`) only ended on stream close or user Cancel, so any silent provider stall (rate-limit hold, proxy/VPN, mid-stream hiccup) hung the chat forever on "Analizuję…/Kontynuuję…". Added: (1) an inter-token **idle timeout** in the SSE reader (`ChatRequest.StreamIdleTimeout`, default 90 s, wired into all three providers) that turns a stalled stream into a clean error; (2) a per-turn **wall-clock timeout with one automatic retry** in `AgentOrchestrator.SendTurnWithTimeoutAsync` (`CompanionSettings.TurnTimeoutSeconds`, default 240 s); (3) a 20 s bound on `RefreshModelsAsync` so startup/provider-switch can't hang the panel. New settings `StreamIdleTimeoutSeconds` / `TurnTimeoutSeconds`.
- **Companion: agent works incrementally instead of one giant turn.** System prompt now instructs the model to split long work/answers across turns and process many rooms/elements in small batches, complementing the existing token-limit auto-continuation — keeps the chat responsive and avoids oversized single requests.
- **Companion: plan mode now produces a final summary.** After all plan steps execute, a dedicated text-only synthesis pass runs in a fresh chat section (`OnSectionBreak`) and streams the closing answer; `SetFinalText` no longer silently drops the result when earlier steps already streamed text into the assistant bubble.

### Added

- New standalone product **in-app AutoCAD AI Assistant** (`src/Companion/`), a separate, installable WPF chat palette (command `ACADAI`) independent of the editor integration. Lets clients chat with OpenAI / Anthropic / Gemini directly inside AutoCAD using their own API key (BYOK, encrypted with Windows DPAPI), attach images/PDF/text to the chat, count elements and generate reports (layer/block schedules, BOM) with Markdown rendering and CSV export.
  - `AcadMcp.Companion.Mcp`: embedded stdio MCP client that spawns the existing `AcadMcp.Backend --category router` as a child process and reuses the entire tool bank (router + composites + plugin primitives) with zero re-implementation. Coexists with any other MCP client (the plugin pipe server already accepts multiple sessions).
  - `AcadMcp.Companion.Agent`: provider-agnostic tool-calling loop with SSE streaming (`OpenAiProvider`, `AnthropicProvider`, `GeminiProvider`), multimodal message building per vendor, DPAPI key store, settings, and report flow templates.
  - `AcadMcp.Companion.Host`: `IExtensionApplication` + singleton `PaletteSet` hosting an MVVM WPF chat view (streaming, attachments, settings tab, quick reports). Does not load `AcadMcp.Backend` in-process (rule 16); does not surface the internal protocol name in the UI.
  - Packaging: `installer/PackageContents.companion.xml`, per-user Inno Setup installer `installer/AcadMcpCompanion.iss`, `scripts/deploy-companion.ps1` (dev deploy) and `scripts/build-companion-installer.ps1` (client `.exe`/zip). The bundle ships Host + tool host (`AcadMcp.Plugin`) + backend server so a single install is self-contained; the client enters their API key on first run.
  - Build clean: `dotnet build -c Release` 0 err / 0 warn across all three Companion projects.

### Added

- **Companion: multi-agent planning + autonomous continuation.** New "Tryb planowania" toggle: a planner pass produces a numbered step plan (inspecting the drawing read-only first), then executor passes run each step sequentially (a planner/executor split). The agent loop now auto-continues when a response is truncated by the token limit (provider `finish_reason`/`stop_reason`/`finishReason` parsed for OpenAI/Anthropic/Gemini), so long builds and answers finish even past the per-response character limit. Raised default `MaxToolIterations` 12 → 24.
- **Companion: AI room visualizations in chat.** New client-side tool `render_visualization` lets the agent generate a rendered image of an indicated room from drawing data (dimensions, room type, furniture) + conversation context (e.g. hospital ward) using the active provider's image model (OpenAI `gpt-image-1`, Gemini `gemini-2.5-flash-image`). The image is shown inline in the chat (`MessageBubble.Image`, `IImageGenerator`, `OnImage` observer callback). Anthropic returns a hint to switch provider (no image model).

- New read-only tool `get_room_data` in `acad-schedules`: locate ONE room by number or name (substring) and return its number, name, area (m²), bbox dimensions (width × depth mm), plus the doors, windows and furniture whose insertion point lies inside the room boundary (each opening tagged with its wall N/S/E/W). Backs accurate "find room A-304" lookups and grounds AI room visualizations. (#ACAD-26)
- New write tool `correct_room_area` in `acad-schedules`: measures the REAL area with the wall-aware region detector and rewrites the `N m²` token on the label when it diverges from the measured value (or to an explicit value), reusing `update_dbtext`/`update_mtext`. Supports `apply=false` dry-run and a `tolerancePct`. Treats the measured geometry — not the (possibly AI-generated, wrong) label — as the source of truth. (#ACAD-29)
- New read-only batch tool `audit_all_rooms` in `acad-schedules`: scans every room label, measures area via `get_room_region`, compares to label m², counts doors/windows/furniture in the detected region, flags `leakSuspected` / `labelMismatch` / `emptyOpenings` / `furnitureMismatch`, optional CSV export to `%LOCALAPPDATA%\AcadMcp\reports\`. (#ACAD-29)
- New batch write tool `correct_all_room_areas` in `acad-schedules`: wrapper around `correct_room_area` for all mismatched labels (`apply=false` dry-run by default). (#ACAD-29)
- `RoomRegionSolver` in `AcadMcp.Shared/Geometry` (AutoCAD-free, unit-tested): rasterized wall-aware flood-fill that returns a room's measured area, bbox and traced outline polygon, plus an even-odd point-in-polygon test. New plugin primitive `acad.schedules.get_room_region` wraps it (with raycast / closed-polyline fallbacks). (#ACAD-29)

### Changed

- **Universal room region detector (flood-fill).** Boundary detection no longer trusts the smallest enclosing polyline (caught the whole-floor outline ~4656 m²) or a naive raycast over ALL lines (caught `S-GRID` construction lines → ~65 m² for the 200 m² `A-304`). `get_room_data` now calls the new `acad.schedules.get_room_region`, which classifies layers (wall/glazing boundary vs grid/annotation/furniture noise), rasterizes only wall geometry, seals door/window openings and flood-fills from the label point — returning the MEASURED area, bbox and a traced outline. Furniture is tested against that outline (point-in-polygon), openings against the perimeter. Falls back to wall-raycast then smallest closed polyline, and reports the detection `method`. `get_room_data` now returns the measured `areaM2` plus `labelAreaM2` (parsed from the label) so the measured geometry — not the stated figure — is authoritative. (#ACAD-29)
- **Hospital audit hardening.** Sibling labels grouped by point-in-polygon on the detected outline (not bbox overlap). Openings filtered with `InsideOrNearBoundary` on the outline perimeter. Flood-fill rejects results >3× labelled area (`labelAreaM2` heuristic). Plugin `get_room_region` seals all door/window blocks within region radius (`sealAllDoors`, wider seal width). (#ACAD-29)
- **Universal boundary fitting for rooms.** `list_room_labels` no longer just grabs the smallest enclosing closed polyline (which picked the whole-floor outline, ~4656 m², when rooms aren't drawn as closed loops). It now also fits a room rectangle by ray-casting from the label to the nearest wall in each direction (Line/Polyline edges, with a small fan to bridge doorways) and chooses the boundary whose area best matches the label's stated area (e.g. "200 m²"), else the tightest enclosing one. `CollectWallSegments` now filters to boundary layers (excluding `S-GRID` and other noise). (#ACAD-28)
- **Universal, layer-agnostic room search.** `get_room_data` now scans labels on ALL layers by default (`allLayers=true`) and treats any closed polyline as a candidate boundary, so it finds spaces regardless of layer naming (not just `A-ROOM-IDEN`/`A-ROOM-BNDY`). `acad.schedules.list_room_labels` gained an additive `allLayers` flag (and accepts `"*"` in `labelLayers`). The Companion system prompt now mandates all-layer search for finding spaces, counting objects and analyzing the project, and retrying without a layer filter when a filtered search is empty. (#ACAD-27)
- **Visualizations are domain-neutral.** `render_visualization` no longer assumes hospital/interior: added a `space_kind` field (interior/exterior/garden/yard) so renders of offices, apartments, classrooms, gardens or yards aren't forced into a clinical interior, and the composed prompt explicitly forbids adding medical elements unless the type says so. The agent infers the space type from its name and the conversation. (#ACAD-27)
- **Companion chat: tool calls collapse into one group.** Multiple tool invocations during a turn now accumulate in a single collapsible "Użyto N narzędzi" group (expand to see each tool + ✓/✗), and the assistant's answer renders BELOW the tool group instead of above it, keeping the transcript readable. (#ACAD-27)

### Fixed

- **Schedules: `generate_room_schedule` threw `NullReferenceException`** when called without a `position` argument (the required `Position` deserialized to null and `InsertTableAsync` dereferenced it). It now anchors at origin when no position is supplied. (#ACAD-26)
- **Companion: AI room visualizations did not match the drawing.** The agent sent a guessed scene description (e.g. "approx. 8 m × 5 m" for a 200 m² room). `render_visualization` now takes a structured room spec (dimensions, windows + daylight, doors, furniture) which the Companion composes into one photoreal prompt, and the agent is instructed to fill it from `get_room_data` (real dimensions/openings/furniture) instead of guessing. The agent no longer uses the non-existent `filter_entities` `textContains` filter to find rooms. (#ACAD-26)
- **Companion: agent could not "see" the drawing.** The spawned `AcadMcp.Backend` logs to stderr; the Companion redirected stderr but never drained it, so once the OS pipe buffer (~4 KB) filled the backend blocked on its next log write and stopped answering JSON-RPC, hanging every tool call. Added a stderr drain loop in `McpStdioClient`, full call/result + lifecycle logging to `%LOCALAPPDATA%\AcadMcp\logs\companion-*.log`, agent-turn logging, and a sharper system prompt that makes the model call `acad_status` / `acad_call` first instead of guessing.
- **Companion: `ACADAI` palette not visible.** WPF view now hosted via `ElementHost`; palette docks to the bottom as a resizable bar; bumped the PaletteSet GUID so AutoCAD stops restoring a stale off-screen position.

### Changed

- **Companion UI: theme-aware + live models.** Chat palette now reads AutoCAD `COLORTHEME` and applies a dark/light brush set so it stays readable in both modes (`ThemePalette` + DynamicResource brushes). The Settings model field is a live, editable dropdown populated from the provider's `/models` API using the user's key (`ModelCatalog`, with curated fallback), and saving the API key shows an explicit "saved" confirmation and refreshes the model list.
- **Companion deploy:** `scripts/deploy-companion.ps1 -SkipPlugin` omits the duplicate `AcadMcp.Plugin.dll`/ComponentEntry on dev machines that already run the pipe via `AcadMcp.bundle`, avoiding a pipe-name conflict at AutoCAD startup.
- Added always-on workflow rule `56-jira-for-important-work.md` requiring important work to be reflected in Jira and durable documentation, after Atlassian project setup drifted through chat/Confluence before the final Jira structure was corrected.

### Changed - Phase D12 Hospital2026 regeneration with all new categories (2026-04-24)

Closes the D12 entry on `docs/PLAN-PROFESSIONAL-UPGRADE-2026.md` with a
**partial target**: score 8 / 17 (concept-sketch tier) against the
senior-architect-reviewer rubric, vs. the 15 / 17 goal. Blocker documented
below. See `artifacts/architect-review/D12-status-2026-04-24.md` for the
full audit trail and recommendations.

1. **Regenerated Hospital2026 DWG** via 60+ composite tool calls across
   callouts, plotstyles, dimensions, grids, schedules, sections, hatches,
   openings (windows), furniture, plumbing, verticals, detail callouts.
   Final DWG `assets/Hospital2026_D12_FINAL.dwg` (230 KB, 926 entities)
   + baseline backup `assets/Hospital2026_D12_BEFORE_REGEN.dwg`
   + checkpoints `ckpt-20260424-140029636` / `ckpt-20260424-141603189`.
2. **Quantified 4 live Gemini 3.1 Pro architect-review passes** ($0.41
   spent, $74.59 / $75 budget remaining). Scorecards written to
   `artifacts/architect-review/architect-review-20260424T*.json`.
3. **Identified paperspace blocker**: 6 of 17 rubric criteria (schedules,
   callouts, finishes-legend, orientation-scale, reflected-ceiling,
   details) structurally require paperspace layouts, but
   `layouts.configure_plot paperSize` rejects every ISO A0 variant we try
   (`eInvalidInput`) on this workstation. Paperspace pass needs a new
   plugin primitive `acad.layouts.list_available_paper_sizes` before
   `configure_plot` can hand-pick a size registered with the active
   plotter - see status doc for the unblock recipe.

### Added - Phase D11 senior-architect-reviewer persona + Gemini 3.1 Pro integration (2026-04-24)

Closes Phase D11 from the master plan (`docs/PLAN-PROFESSIONAL-UPGRADE-2026.md`).
Unlocks Phase D12 (Hospital2026 regeneration with rule-60 scorecard).
Build still **0 err / 0 warn (Release)**; tests **131 / 131 C# + 31 / 31 Python**;
**first live end-to-end Gemini 3.1 Pro call measured at 12.6 s / $0.0121**
against a real Hospital2026 poster (see
`artifacts/architect-review/D11-smoke-2026-04-24.md`).

1. **New persona `senior-architect-reviewer`** in the Vision sidecar.
   - Applies rule 60's 17-criterion scorecard to any floor-plan raster.
   - Emits strict JSON (17 rows, `{id, label, score, note}`).
   - Scores are snapped to the 0 / 0.5 / 1.0 grid; verdict ladder is
     `<10 concept-sketch | 10-13 technical-study | 14-15 executive-with-remark | >=16 full-wykonawczy`.
   - Files: `src/AcadMcp.Vision/acadmcp_vision/schemas.py`,
     `src/AcadMcp.Vision/acadmcp_vision/app.py`,
     `src/AcadMcp.Vision/personas/senior-architect-reviewer.md`,
     `src/AcadMcp.Vision/personas/senior-architect-reviewer.json`.

2. **New endpoint `POST /v1/architect-review`** alongside `/v1/describe-image`.
   - Request: `{ image, language, brief, max_tokens, provider }`.
   - Response: `{ score, verdict, criteria[17], fatal_gaps, threshold_note,
     raw_text, provider, model, cached }`.
   - 503 with `install_hint` when no vision LLM key is configured.
   - `max_tokens` ceiling raised to 16000 (was 4000) + default raised to
     4000 (was 1600). Rationale: Gemini 3.x thinking models count reasoning
     tokens against `max_output_tokens`, so a too-tight cap truncates the
     17-row JSON mid-row.

3. **Google Gemini 3.1 Pro integration** in `engines/vision_llm.py`.
   - Default model: `gemini-3.1-pro-preview` (released 2026-02-19 - the
     April 2026 frontier vision model; MMMU-Pro 75.1%, Video-MME 78.2%,
     DocVQA 95.7%).
   - Accepts either `GOOGLE_API_KEY` or `GEMINI_API_KEY`.
   - Supports `ACADMCP_GOOGLE_MODEL` + `ACADMCP_GOOGLE_THINKING`
     (low/medium/high/max). Default thinking = `low` because reasoning
     tokens share the output budget with the JSON scorecard.
   - Provider auto-selection order: Anthropic -> OpenAI -> Google.
   - Fallback: both `google-genai` (preferred) and legacy
     `google-generativeai` SDKs are supported.
   - `pyproject.toml` `[ml]` extras now include `google-genai>=1.0`.
   - `/version` endpoint reports `api_keys.google` alongside
     `anthropic` / `openai`.

4. **Budget-aware live driver** `scripts/run-architect-review.py`.
   - Discovers the sidecar port via `%LOCALAPPDATA%/acadmcp/vision/.port`.
   - Enforces a cumulative USD budget (`--budget-usd`, default $10) AND a
     per-call safety cap (`--per-call-cap-usd`, default $0.25). Aborts
     BEFORE the call that would exceed either.
   - Writes a timestamped JSON report per run to
     `artifacts/architect-review/`.
   - Supports `.env` loading, `--smoke` and `--dry-run` modes, brief file
     input, and multi-image batches.

5. **Secrets hygiene.**
   - Added `.env.example` (committed) documenting every env var.
   - Confirmed `.env` is gitignored via the existing `.env` rule and
     exempted `.env.example` via `!.env.example` in `.gitignore`.

6. **Tests** (new file `src/AcadMcp.Vision/tests/test_architect_review.py`).
   - 24 static tests with no network / API dependency:
     - Rubric has exactly 17 canonical criteria in order.
     - Persona prompt EN / PL cite every label / id + rules 60-70.
     - Threshold ladder maps score -> verdict correctly at every boundary.
     - JSON parser tolerates markdown fences, trailing prose, missing
       rows (default 0.0), out-of-range scores (clamped), unknown ids
       (ignored).
     - `/v1/architect-review` endpoint round-trips stubbed replies into
       the expected response shape for all four verdicts + LLM-unavailable.
     - Descriptor `personas/senior-architect-reviewer.json` stays in sync
       with `ARCHITECT_REVIEW_CRITERIA` and verdict ladder.
   - **Full Vision suite: 31 / 31 green** (24 new + 7 existing).

7. **Live smoke proof** (`artifacts/architect-review/D11-smoke-2026-04-24.md`).
   - Reviewed `assets/Hospital2026_POSTER_6000x4500.png` via Gemini 3.1 Pro.
   - Result: **2.0 / 17 -> concept-sketch verdict** in 12.6 s for $0.0121.
   - All 17 criteria produced actionable notes with `fix:` directives
     mapped to backend tools (e.g. `acad.hatches.draw_hatch`,
     `acad.furniture.populate_room`, `acad.annotate.dimlinear`).
   - This score is factually correct: the poster has shielding, corridors,
     doors (swings only), beds, north arrow and room labels - but no
     hatching, no furniture, no dimension chains, no schedules, no
     sections, no finishes legend. That is exactly the gap Phase D12 is
     designed to close.

### Fixed - 3 UX polish items from the full tool audit (2026-04-23)

Closes the `audit-ux-better-errors` follow-up item from the full tool
audit. No regressions; full suite still **131 / 131 green**; Debug +
Release both **0 err / 0 warn**.

1. **`files.audit_database`** (`AuditInfo` ctor bug on AutoCAD 2025).
   The plugin handler used `Activator.CreateInstance(AuditInfo)` which
   fails because `AuditInfo` is `sealed` with no public parameterless
   constructor in the modern ObjectARX managed API, and
   `Database.Audit(AuditInfo)` is no longer exposed — only the
   extension `Database.Audit(bool fixErrors, bool bCmdLnEcho)` is.
   - New strategy in `src/AcadMcp.Plugin/Tools/FilesPluginTools.cs`:
     reflectively probe `Database.Audit(AuditInfo)` first (legacy
     AutoCAD 2020-2024 path) and construct `AuditInfo` via either
     `.ctor()` or `.ctor(bool fix)` if found; fall back to the
     `Database.Audit(bool, bool)` extension on modern builds (AutoCAD
     2025+) and return `{ ran: true, fix, mode: "extension-no-counters" }`.
     The reflective legacy path additionally returns `errorsFound` /
     `errorsFixed`.
   - `mode` in the result tells the caller which path was taken so
     downstream automation can decide whether counters are trustworthy.

2. **`geometry-2d.list_entities_in_window` + `grids.snap_to_grid`**
   emitted a cryptic `NullReferenceException` on missing point args
   (`corner1` / `corner2` / `point` / `origin`) because `WindowArgDto`
   / `SnapToGridArgs` don't mark them as nullable — an empty JSON body
   produced a record whose corner fields were `null`, and the body
   dereferenced `a.Corner1.X` immediately.
   - `list_entities_in_window` (`Geometry2dPluginTools.cs`) now throws
     `ArgumentException("corner1 required (expected { x, y }).")` /
     `"corner2 required ..."` before indexing.
   - `snap_to_grid` (`GridsTools.cs`) grew explicit guards for
     `point`, `origin`, `xSpacingsMm`, `ySpacingsMm` (including a
     non-empty-collection guard) with matching messages.

3. **`callouts.insert_*`** (5 composite tools) now validate required
   args **before** any plugin call so a partial failure can't leave
   AutoCAD with a wedged transaction mid-symbol. Added top-of-method
   guards to `CalloutsTools.cs` for:
   - `insert_north_arrow` — `position`, `scale`, `layer`, `label`
   - `insert_scale_bar` — `position`, `scale`, `layer`
   - `insert_section_callout` — `startPoint`, `endPoint`, `scale`,
     `label`, `viewDirection`, `layer`
   - `insert_detail_callout` — `center`, `radiusMm > 0`, `scale`,
     `label`, `layer`
   - `insert_title_block` — `bottomLeft`, `sheetSize`, `scale`,
     `layer`, `borderLayer`
   Each guard throws `ArgumentException` with a concrete example
   (`"position required (expected { x, y })"` /
   `"sheetSize required (A0/A1/A2/A3/A4)"`) so agents can self-correct
   without reading code.

### Added - Full tool audit across 29 categories (2026-04-23)

- **New doc `docs/TOOL-AUDIT-2026-04-23.md`** — proof that literally every
  one of the 322 registered MCP tools dispatches correctly. Headline:
  0 uncaught throws, 0 surfaced runtime errors, 8 PASS (catalogs with
  optional args), 311 GATED (correctly short-circuit on missing
  gateway), 3 VALIDATES (cleanly reject empty args with validation
  error). Per-category table, reproduction command, known-but-benign
  follow-ups.
- **New xUnit tests `tests/AcadMcp.Tests/FullToolAuditTests.cs`** —
  two audit tests:
  1. `Every_tool_in_every_category_has_complete_metadata` — asserts
     every tool has non-empty Name/Description/DeclaringType/Method and
     that `ToolRegistry.ResolveMethod` returns a live `MethodInfo`.
  2. `Every_tool_dispatches_without_hanging_or_uncaught_throw` — calls
     `ToolInvoker.InvokeAsync` on every tool with empty args and null
     gateways, classifies the result, fails if any tool throws or hangs
     (3 s per tool). Fast (~ 2 s total) so it can sit in pre-commit.
- **Test count**: 129 → **131 / 131 green**.

### Fixed - `ToolInvoker` NRE for tools taking `IVisionSidecarClient` / unflagged plugin (2026-04-23)

- **Root cause**: `ToolInvoker.InvokeAsync` had a `RequiresPlugin`
  metadata guard, but not every tool that dereferences `IPluginGateway`
  declares `RequiresPlugin = true` (e.g. `geometry-2d.get_distance_points`),
  and there was **no guard at all** for `IVisionSidecarClient`. Nine
  vision tools + one geometry tool surfaced as `INVOKER-BUG`
  (`NullReferenceException`) in the audit.
- **Fix**: added a **parameter-driven gateway guard** in
  `src/AcadMcp.Backend/Mcp/ToolInvoker.cs`. Before invoking the method,
  iterates `method.GetParameters()`; if any parameter is
  `IPluginGateway` (and `plugin` arg is null) or `IVisionSidecarClient`
  (and `vision` arg is null), returns a clean
  `InvokeResult(IsError = true, "requires the AutoCAD plugin gateway" /
  "requires the Vision sidecar")` instead of letting the tool body
  dereference the null. Complements (does not replace) the existing
  `RequiresPlugin` metadata check.
- **Result**: 0 uncaught throws across all 322 tools.

### Added - Phase D D10: architectural-fidelity rubric + rule enrichments (2026-04-23)

- **Rule `60-architectural-fidelity.md`** — the 17-criterion scorecard
  that `senior-architect-reviewer` (D11 Vision persona) applies to every
  floor plan before it qualifies as rysunek wykonawczy. Criteria span
  material expression (hatching), furnishing, sanitary fixtures, door +
  window quality, vertical circulation, structural grid, dimension
  chains, schedules, callouts, section lines, lineweight, finishes
  legend, north + scale + compass, RCP, jamb/sill/lintel details and
  room-program fidelity. Each criterion maps to one generator tool AND
  one detector tool + the matching category rule. Threshold policy:
  `< 10` = concept, `10-13` = technical study, `14-15` = executive with
  remark, `16-17` = full wykonawczy.
- **Rule `26-acad-api-traps.md`** extended with **7 hatching pitfalls**
  (§11a-g): boundary orientation CCW, associativity + ghost hatches,
  pattern file (`.pat`) resolution via SupportPath, seed-point UCS
  rules, literal mm scale, clip-via-rebuild pattern, and `HatchStyle`
  = island detection (not clipping). Companion to rule 62.
- **Rule `22-mcp-tool-args-results.md`** extended with the **block
  attribute contract**: tag constants in a single `static class`,
  exactly ONE visible tag + rest invisible, string values with strict
  encoding (`"900"` / `"0"` / `""`), DTO surface via
  `IReadOnlyDictionary<string,string> Attributes`, checklist for
  adding new tags. Reference impl = `OpeningsPluginTools` (rule 65).
- **Build**: solution Debug 0 err / 0 warn, `dotnet test` 129/129 green.
- **Outstanding D10 gap**: block-attribute contract now documented, but
  no central `OpeningAttrTags` static class yet exists — existing
  plugin code still uses string literals. Follow-up task to refactor
  logged separately.

### Fixed - IPC deserialization: PropertyNameCaseInsensitive across 25 proxies (2026-04-23)

- **Systemic bug** discovered during the full tool audit: plugin-side
  primitives that return typed records with PascalCase properties (e.g.
  `FurnCatalogEntry`, `PlumbEntry`) serialize to PascalCase JSON, but the
  backend proxies and tool facades used `JsonSerializerOptions` **without**
  `PropertyNameCaseInsensitive = true`. As a result, proxy-side
  deserialization silently dropped every string/enum field and zeroed
  numeric fields. Reproduced end-to-end with
  `acad_call { category:furniture, tool:list_furniture_catalog }` — every
  entry came back `{ widthMm:0, depthMm:0 }` with no `name` / `category`
  / `description` / `domain`.
- **Fix**: added `PropertyNameCaseInsensitive = true` to the shared
  `Opts` `JsonSerializerOptions` in **25 files**: `FurnitureProxy`,
  `PlumbingProxy`, `ViewProxy`, `ArchitectureProxy`, `OpeningsProxy`,
  `HatchesProxy`, `LayersProxy`, `FilesProxy`, `ModifyProxy`,
  `LayoutsProxy`, `CivilProxy`, `SelectionProxy`, `AnnotationsProxy`,
  `Geometry3dProxy`, `DimensionsProxy`, `MechanicalProxy`,
  `BlocksProxy`, `VisionProxy`, `ElectricalProxy`, `ValidatorsProxy`,
  `BooleanOpsProxy`, `Geometry2dProxy`, `PlotstylesTools`,
  `SectionsTools`, `SchedulesTools`. `ParametricProxy` already had it
  (treated as the reference implementation).
- **Tests**: full backend build Release + Debug = 0 err / 0 warn;
  `dotnet test` = 129/129 green; deferred E2E smoke pending MCP
  reconnect.

### Added - Phase D D9c: `acad-plotstyles` (2026-04-24)

- **`acad-plotstyles`** — 3 composite tools for CTB/STB plot-style
  management (rule 61):
  - `ensure_ctb` — install a colour-dependent plot-style into AutoCAD's
    Plot Styles directory. Queries `acad.layouts.list_plot_styles` to
    resolve the target folder, then file-copies from `sourcePath`
    (caller override) or the repo asset `<repo>/assets/plotstyles/<name>`.
    Idempotent under `overwrite=false`; reports `existedBefore`,
    `copied`, `sourceResolved`, `listedAfter` so callers can diff before /
    after states. Emits a human-readable note when AutoCAD does not
    disclose the plot-styles directory (e.g. roaming profiles).
  - `apply_plotstyle_to_layout` — assign a named CTB/STB to a paperspace
    layout; dispatches `acad.layouts.configure_plot { layoutName,
    plotStyle, rotation:0 }`. When `ensure=true` (the default) it calls
    `ensure_ctb` first so the sheet is guaranteed loaded before
    `SetCurrentStyleSheet` runs.
  - `list_plotstyles` — enumerate all plot-styles AutoCAD currently sees
    (CTB + STB). `filter='ctb'|'stb'|null` narrows the `names` result;
    `ctb` + `stb` arrays always return the full per-extension lists.
    Also returns repo presets (`HOSPITAL-ISO.ctb`, `ISO-Standard.ctb`,
    `monochrome.ctb`), the AutoCAD Plot Styles directory, and the
    backend asset directory so callers can pre-stage files.
- **`acad.layouts.list_plot_styles`** — new plugin primitive. Calls
  `PlotSettingsValidator.Current.RefreshLists(new PlotSettings(false))`
  then `GetPlotStyleSheetList()`, splits results into `ctb` + `stb`
  buckets, and probes `%APPDATA%\Autodesk\AutoCAD *\R*\<locale>\Plotters
  \Plot Styles` (AutoCAD 2018+) with a fallback to the legacy `Plot
  Styles` location for older installs.
- **`PlotstylesPalette`** — canonical preset names (HOSPITAL-ISO,
  ISO-Standard, monochrome), the full **rule 61 §2 lineweight tier
  table** (ACI 1-9 → 0.13–0.70 mm) for architectural drawings, and
  `AssetsDirectory()` which walks up from the test binary to find
  `<repo>/assets/plotstyles/` via the `AcadMcp.Backend.csproj` marker.
- **`rule 61-lineweight-policy.md`** — 6-section policy covering the
  scope of `acad-plotstyles`, the mandatory colour → lineweight tier
  table, canonical CTB presets, directory resolution strategy (best-
  effort probe + graceful fallback), `apply_plotstyle_to_layout`
  contract (`ensure=true` default), and unit-test expectations.
- **`assets/plotstyles/README.md`** — operator guide explaining how to
  drop a pre-authored CTB into the repo and have `ensure_ctb` pick it
  up automatically. Binary CTBs remain opt-in (not tracked by default).
- **`PlotstylesTests`** — 7 new unit tests (129 total, +6 after dedup):
  catalog binding, category declaration (all 3 `RequiresPlugin=true`),
  default-preset completeness, lineweight tier coverage (all 9 ACIs),
  literal rule 61 §2 table values, assets-directory resolution,
  monotonic visual priority (fire-wall > section > wall > frame).

Build 0 err / 0 warn (Release + Debug). Manifest regenerated via
`AcadMcp.Backend.dll --category plotstyles --regenerate-manifest`
(3 tools; retires `plotstyles_placeholder`).

**Full E2E via `acad_call` router dispatch (2026-04-24, 4/4 green):**

1.  `acad_load_category plotstyles` → 3 composites returned with full
    descriptions + PL/EN intent arrays + JSON schema.
2.  `acad_call plotstyles list_plotstyles {}` → 13 plot-styles enumerated
    (9 CTB + 4 STB: `acad.ctb`, `DWF Virtual Pens.ctb`, `Fill Patterns.
    ctb`, `Grayscale.ctb`, `monochrome.ctb`, `Screening 25/50/75/100%
    .ctb` + `acad.stb`, `Autodesk-Color.stb`, `Autodesk-MONO.stb`,
    `monochrome.stb`); `assetsDir` resolved to `<repo>/assets/plotstyles`;
    `presets` lists HOSPITAL-ISO + ISO-Standard + monochrome.
3.  `acad_call plotstyles list_plotstyles { filter: "stb" }` → narrows to
    4 STB sheets, `count=4`, other buckets unchanged.
4.  `acad_call plotstyles ensure_ctb { name: "monochrome.ctb" }` →
    `existedBefore=true, copied=false, listedAfter=true` (graceful no-op
    because the target already lives in AutoCAD's plot-style table).
5.  `acad_call plotstyles apply_plotstyle_to_layout { layoutName:
    "Układ1", plotstyle: "monochrome.ctb", ensure: false }` →
    `applied=true`, no notes.
6.  `acad_call plotstyles apply_plotstyle_to_layout { layoutName: "A0-001
    RZUT PARTERU", plotstyle: "Grayscale.ctb", ensure: false }` →
    `applied=true` (second layout, different CTB — confirms the composite
    round-trips `psv.SetCurrentStyleSheet` correctly).

**Known refinement pending (non-blocking):** on AutoCAD 2025 (Polish
locale, this workstation) the plugin's directory probe via
`Environment.SpecialFolder.ApplicationData` does NOT surface
`%APPDATA%\Autodesk\AutoCAD 2025\R25.0\plk\Plotters\Plot Styles` even
though the folder exists. `list_plotstyles` returns `directory=null` and
`ensure_ctb` falls back to the documented note "AutoCAD did not disclose
a Plot Styles directory — nothing was copied". Enumeration +
`apply_plotstyle_to_layout` are unaffected. Will be fixed in a follow-up
iteration by probing `HostApplicationServices.Current.FindFile("acad.
ctb", null, FindFileHint.Default)` which AutoCAD resolves against its
own support paths and by reading `LOCALROOTPREFIX` system var.

### Added - Phase D D9b: `acad-sections` (2026-04-24)

- **`acad-sections`** — 4 composite tools that add section / elevation
  symbology on top of the `acad-callouts` end-marker primitives (rule 70):
  - `insert_section_line` — draws a DASHED2 cut line on `A-DETL-SECT`,
    6 mm plotted perpendicular offset ticks at both endpoints, and
    delegates the labelled end markers to
    `acad-callouts.insert_section_callout` with `drawCutLine=false` so
    the two categories never duplicate the circle+triangle marker
    geometry (rule 69 §4). Returns the cut-line handle plus all marker
    handles for one-shot rollback.
  - `insert_section_title` — caption (`PRZEKRÓJ A-A`) + 80 mm plotted
    underline + scale line (`SKALA 1:50`) on `A-DETL-TITL`. `caption` is
    overridable (`ELEWACJA`, `WIDOK`, `FRAGMENT`, `DETAL`, …); `viewScale`
    is independent of the plan scale so a 1:100 plan can host a 1:50
    section's title text.
  - `insert_elevation_marker` — filled triangle (8 mm plotted) pointing
    in the requested compass direction (N/E/S/W + diagonals, or bare
    degrees) over a 30 mm plotted baseline, with a `ELEWACJA <dir>` label
    and optional sheet reference. Kept distinct from section callouts so
    the "which face is this?" symbology is unambiguous.
  - `list_section_lines` — inventories all entities on `A-DETL-SECT` (or
    a caller-supplied layer), returning per-handle object class and
    curve length (when available). Degrades gracefully if the layer is
    absent — returns an empty list instead of throwing.
- **`SectionsPalette`** — canonical layer set (`A-DETL-SECT/TITL/ELEV`),
  DASHED2 cut linetype with scaled `ltscale`, plotted sizes for ticks /
  title underline / elevation triangle + baseline, and an 8-entry
  compass-direction map with `ResolveDirectionDeg` (accepts names or
  bare numeric strings).
- **`rule 70-sections-elevations.md`** — 7-section policy covering the
  layer set, cut-line contract (DASHED2 + ticks + delegated end markers),
  section-title contract, elevation-marker contract, inventory contract,
  composite-of-composite pattern (Sections → Callouts is an explicit
  exception to rule 35 §2), and test coverage expectations.
- **`SectionsTests`** — 9 new unit tests (123 total, +8 after dedup):
  catalog binding, category declaration, compass direction resolution
  (names + bare degrees + fallback), layer naming convention (`A-DETL-`
  prefix), DASHED2 linetype contract, plotted-size sanity checks,
  directions dictionary completeness.

`acad-sections` is the first composite category to legally call another
composite category (`CalloutsTools.InsertSectionCallout`). Rule 70 §6
documents the cross-composite dispatch contract.

Build 0 err / 0 warn (Release + Debug). Manifest regenerated via
`AcadMcp.Backend.dll --category sections --regenerate-manifest` to reflect
the 4 new tools and retire the scaffolded `sections_placeholder`.

### Added - Phase D D9a: `acad-callouts` (2026-04-24)

- **`acad-callouts`** — 5 composite tools for plan-symbol drawing, composed
  entirely from existing primitives (rule 35 §2, rule 69):
  - `insert_north_arrow` — ISO 5455 circle + diamond arrow + "N" label;
    plotted Ø30 mm scaled by the user-declared plan scale (1:100 → 3000 mm
    drawing-unit). `rotationDeg` lets the arrow track project north.
  - `insert_scale_bar` — chequered 5-segment graphic scale bar (50 mm
    plotted total) with metre labels + `SKALA 1:100` caption. Segment
    metres auto-scale from the plan scale (0.5 m @ 1:20-25, 1 m @ 1:50-100,
    2 m @ 1:200, 5 m @ 1:500).
  - `insert_section_callout` — two circle+triangle markers with section
    letter (default `A`) + cut line + view-direction arrows. `sheetReference`
    optional bottom-half label ("1/5"). `drawCutLine=false` lets callers
    supply their own dashed cut.
  - `insert_detail_callout` — area circle + leader + bubble with detail
    number on top, target scale on bottom; bubble position defaults NE of
    the feature, overridable via `leaderEndPoint`.
  - `insert_title_block` — ISO 7200 sheet border + 12-row PL title block
    (`PROJEKT`/`INWESTOR`/…/`SPRAWDZAJĄCY`) in the bottom-right corner.
    Accepts A0–A4 sheet sizes and both an explicit `fields` list and
    shorthand `projectName`/`sheetNumber`/`author`/`date`/`titleText`.
- **`CalloutsPalette`** — canonical layer set (`A-ANNO-NORT/SBAR/SYMB/TTLB/
  BORD/TEXT`), ISO 5455 plotted sizes, ISO A0–A4 sheet table, scale-bar
  preset table, default 12-row title block keys, scale-factor resolver.
- **`rule 69-callouts-leaders.md`** — layer / plotted-size / leader
  hierarchy contract. Defines the PL title block rows, the section marker
  geometry (PN-EN ISO 128), the chequered scale bar rules, and test
  coverage expectations.
- **`CalloutsTests`** — 9 new unit tests (115 total, +8): catalog binding,
  category declaration, scale-factor resolver, scale-bar preset table,
  ISO sheet table, title-block row contract, layer naming convention.

Build 0 err / 0 warn (Release + Debug). All composite drawing goes through
existing primitives: `acad.geometry2d.draw_polyline / draw_line /
draw_circle`, `acad.annotations.add_dbtext`, `acad.layers.create_layer`
(via `ArchitectureProxy`). No new plugin handlers were added.

### Added - Router: universal `acad_call` dispatch (2026-04-24)

- **`acad_call { category, tool, args }`** — new meta-tool on the `acad-router`
  that dispatches in-process to **any** backend composite (via
  `ToolRegistry` + reflection) or **any** plugin primitive by dotted name
  (`acad.<cat>.<name>` routed through `IPluginGateway`). Replaces the old
  "lazy MCPBank connect" workflow: the MCP client no longer needs `mcpbank-discovery`
  / `mcpbank-dynamic` to reach category tools — the router owns the full
  catalog directly. No subprocess spawn, single stdio hop, shared
  plugin/vision clients.
- **`acad_load_category`** — now returns the real tool list of a category
  (name, description, intent, input schema) pulled from `ToolRegistry`,
  instead of the previous stub that only said "would call `mcpd_connect`".
  New `includeSchema: bool` flag for compact listings.
- **`acad_find_tools`** — real keyword search across every loaded category's
  `McpTool` metadata (name + description + intent, with name-hit scoring
  boost), replacing the previous MCPBank stub. Returns `{category, tool,
  description, readOnly, requiresPlugin, score}` tuples.
- **`acad_explain_capabilities`** — rebuilt from the live `ToolRegistry`
  (per-category tool counts), eliminating drift between documentation and
  actual loaded categories.
- **`acad_recommend_categories`** — extended keyword set to cover the Phase D
  categories (schedules / openings / hatches / furniture / plumbing / grids /
  verticals) and recommends the `acad_call` invocation contract.

### Changed

- **`RouterServer`** now takes `ToolRegistry` + `IVisionSidecarClient` via
  DI (was plugin-only), enabling the new in-process dispatch.
- **`Program.cs`** registers `IVisionSidecarClient` for the router process
  too (was category-only), so `acad_call` can reach vision composites.
- **`ToolInvoker`** — extracted from `CategoryServer.HandleToolsCallAsync`
  into a shared helper (`src/AcadMcp.Backend/Mcp/ToolInvoker.cs`). Both the
  category server and the router now use it for reflection-based tool
  invocation (parameter binding, plugin/vision injection, typed exception
  mapping). Removes ~100 lines of duplicated logic.
- **`acad_load_category.json`** descriptor updated under
  `.cursor/projects/<ws>/mcps/user-acad-router/tools/`.
- **`acad_call.json`** descriptor added.
- **`HandleInitialize.instructions`** — rewrote the router's advertised
  capabilities message to reflect the new `acad_call` flow and drop the
  obsolete `mcpbank-discovery` references.

### Notes

- The MCP client must restart the `user-acad-router` MCP server to pick up the new
  binary (rebuild produced a fresh `AcadMcp.Backend.dll`; the client caches the
  stdio process).
- Plugin-side primitives are unchanged; `acad_call` only adds a new routing
  layer above them.

### Added - Phase D D8: acad-schedules (2026-04-24)

- **acad-schedules +5 composite tools** — Polish hospital-grade schedule
  generators that assemble AutoCAD `Table` entities from existing drawing
  content. Every tool is a backend composite that orchestrates the new
  plugin primitives plus `acad.annotations.add_table`, so the drawing
  reflects live model data without a second source of truth.
  - `generate_door_schedule` — *"ZESTAWIENIE STOLARKI DRZWIOWEJ"*.
    Pulls every opening with `kind=door` via
    `acad.openings.list_openings_in_model` and renders a 10-column table
    (NR / TYP / SZER. / WYS. / REI / OGNIOOCH. / RC / DB / POM. OD /
    POM. DO). Emits `complianceNotes` for lead-shielded doors and for
    REI values below the WT §232 evacuation minimum.
  - `generate_window_schedule` — *"ZESTAWIENIE STOLARKI OKIENNEJ"*. Nine
    columns (NR / TYP / SZER. / WYS. / PARAPET / SZYBA / RC / DB / POM.).
    Sill height comes from `SILL_MM` on the window attribute contract.
  - `generate_room_schedule` — *"ZESTAWIENIE POMIESZCZEŃ"*. Uses the new
    `acad.schedules.list_room_labels` primitive to enumerate DBText /
    MText on `A-ROOM-IDEN` + `A-ANNO-ROOM`, optionally joining each
    label against a closed polyline on `boundaryLayer` (default
    `A-ROOM-BNDY`) for an m² figure. Auto-numbers rows starting at 101
    when `autoNumber = true`. Strips MText formatting codes from labels.
  - `generate_finish_legend` — *"LEGENDA WYKOŃCZEŃ"*. Ships with 11
    default hospital rows (F-01 PVC homogeniczne, F-02 PVC antystatyczna
    sale OR, F-03 gres techniczny, F-04 epoksyd 2K, W-01..W-04 HPL /
    tynk / glazura / GKF, C-01..C-03 sufity higieniczne) plus RAL codes
    and locations. Extra rows can be appended through `extraRows`.
  - `update_schedules` — finds every `Table` whose title cell contains
    "ZESTAWIENIE / LEGENDA / WYKOŃCZEŃ / SCHEDULE / LEGEND" via the new
    `acad.schedules.find_schedule_tables` primitive, classifies each
    hit as doors / windows / rooms / finish and rebuilds it at the same
    insertion point. The old table is erased *after* the replacement
    is successfully committed so a mid-flight failure cannot leave the
    drawing without the schedule.
- **TableStyle presets HOSPITAL-DEF + OFFICE-DEF** — committed as real
  AutoCAD `TableStyle` objects via the new
  `acad.schedules.ensure_table_style` primitive (title/header/body text
  heights + title & header ACI fill colors, mapped onto the 2018+
  cell-style API using `_TITLE` / `_HEADER` / `_DATA` names). HOSPITAL
  is dense (5 / 3.5 / 2.5 mm, red title, light-orange header); OFFICE
  is lighter (4 / 3 / 2.5 mm, blue title, grey header). Every generator
  runs `ensure_table_style` once unless the caller opts out via
  `ensureStyle = false`.
- **`SchedulesPalette`** (`src/AcadMcp.Backend/Categories/Schedules/`)
  centralises every magic constant: Polish titles, layer names
  (`A-ANNO-TBLS`, `A-ANNO-LEGN`), preset specs, default column widths,
  row heights and the header row for each schedule kind. Tests assert
  that header + column counts stay in sync so a future column addition
  is a compile/test-time error, not a runtime layout bug.
- **Plugin primitives +4** (new `SchedulesPluginTools.cs`, registered in
  `PluginEntryPoint`):
  - `acad.schedules.ensure_table_style` — write handler. Creates or
    updates a TableStyle in the TableStyleDictionary, applies text
    heights per `_TITLE` / `_HEADER` / `_DATA`, optionally switches the
    current database style.
  - `acad.schedules.list_table_styles` — read-only enumeration for
    tooling + diagnostics.
  - `acad.schedules.list_room_labels` — read-only DBText/MText walker
    on configurable label layers (default `A-ROOM-IDEN`, `A-ANNO-ROOM`)
    with optional bbox-pre-filtered ray-cast against closed polylines
    on `boundaryLayer` to derive per-room area (m²).
  - `acad.schedules.find_schedule_tables` — read-only Table walker
    returning `handle / title / rows / cols / layer / position` for
    every schedule in model space, optionally filtered by
    `titleContains` / `layerFilter`.

### Tests

- `tests/AcadMcp.Tests/Categories/SchedulesTests.cs` — 5 facts (replace
  the stub placeholder): catalog has exactly the 5 D8 tools; both
  presets exist with the expected text heights and fill ACIs; every
  schedule's header array stays the same length as its column-width
  array; the default finish-legend rows are all 5-wide and there are at
  least ten of them; every Polish title constant is non-empty and
  contains the expected diacritic-bearing keyword.
- Full suite: **107 / 107 passing** (was 103/103 after D7 → +4 from
  D8 schedules). `check-manifests.ps1`: 0 problems (29 code
  categories, 30 manifests, the +1 is the legacy `files` helper).

### Build

- `dotnet build src\AcadMcp.sln -c Release`: **0 errors, 0 warnings**.
  The `acad-schedules` manifest is regenerated to list all 5 composite
  tools; the placeholder stub `schedules_placeholder` is removed.

### Follow-ups

- The `acad.schedules.*` primitives are new, so the in-process plugin
  must be redeployed (`scripts\deploy-plugin.ps1`) before the 5 backend
  tools will succeed at runtime. The router stays fine — it only reads
  the regenerated manifest.
- Rendering polish deferred to D12: currently the title row is written
  into the leftmost cell; horizontal merging of the title row across
  the full column span will be added when we bind the real AutoCAD
  `SetMergeCell` API in an `add_table` follow-up.

### Added - Phase D D7: acad-verticals + acad-grids (2026-04-24)

- **acad-verticals +8 composite tools** (rule 67):
  - `draw_stair_straight` — straight-run stair outline + tread lines +
    UP/DN direction arrow + label on `A-STRS` / `A-STRS-DIR`. Computes
    tread depth from `runLengthMm / treadCount` and emits
    `complianceWarnings` for WT §54 riser (150–175 mm), tread (250–350
    mm), clear width (≥ 1200 mm) and the Blondel ratio `2r + t`
    (600–650 mm comfort band).
  - `draw_stair_spiral` — outer arc + inner arc (column) + radial
    tread lines sweeping `sweepDeg` degrees from `startAngleDeg`,
    centred on `center` with centre UP/DN label.
  - `draw_stair_u_shaped` — two straight runs + 180° landing rectangle,
    forwarding each run's warnings individually.
  - `draw_ramp` — rectangle + slope arrow + percentage label on
    `A-RAMP`; warns when `accessible = true` **and** rise > 500 mm
    **and** slope > 6 % (WT §66), or when width < 1200 mm.
  - `insert_elevator_v` — shaft rectangle + diagonals (ISO 7001 lift
    symbol) + kind label; enforces kind-specific minimums
    (bed-lift 1600×2600 mm per WT §193; passenger lift 1100×1400 mm
    per PN-EN 81-70).
  - `insert_escalator` — outline + step lines + direction arrow +
    UP/DN label on `A-VTRN-ESCL`.
  - `insert_platform_lift` — 1100×1400 mm default rectangle + PL label
    on `A-VTRN-LIFT` (PN-EN 81-41).
  - `draw_handrail` — polyline on `A-RAIL` with optional height
    annotation; warns when height is outside WT §298 900–1100 mm
    range for public stairs.
  All eight are composite — they go through `ArchitectureProxy`'s
  primitive gateways (`draw_line` / `draw_polyline` / `draw_circle` /
  `draw_arc` / `add_dbtext`) and do NOT introduce a new plugin-side
  handler file.
- **acad-grids +6 composite tools** (rule 67):
  - `draw_grid` — orthogonal column grid from two spacing lists;
    auto-generates letter labels (A, B, C… for X-axes) and numeric
    labels (1, 2, 3… for Y-axes) with configurable bubble sides
    (default: north + west). Bubbles are circle + DBText placed at
    `extendMm + bubbleRadiusMm` outside the grid box.
  - `add_grid_axis` — single labeled axis line with optional
    start/end bubbles; direction vector inferred from endpoints.
  - `add_grid_bubble` — standalone bubble (circle + text) at a point.
  - `list_grid_axes` — read-only inventory via
    `acad.selection.select_by_layer` on `A-GRID` + `A-GRID-BUB`.
  - `snap_to_grid` — **pure backend maths** (no plugin call,
    `RequiresPlugin = false`). Finds the nearest intersection given an
    origin + two spacing lists; returns snapped point, axis labels
    (A/1, B/2, …) and `cellLabel = "{x}/{y}"`. Used by other tools to
    align entities to structural axes.
  - `delete_grid` — erases entities on axis + bubble layers **or** a
    provided handle list; delegates to `acad.modify.erase`.
- **`GridsPalette`** — helper with spreadsheet-style `LetterLabel`
  (A, B…, Z, AA, AB…, AZ, BA…), numeric labels, `CumulativeOffsets`
  for spacing lists, and default bubble radius / extend values.
- **`VerticalsPalette`** — layer names + WT numeric constants
  (riser/tread/width thresholds, ramp slope cap, lift cabin minimums,
  handrail height band) used by every `complianceWarnings` emitter.
- **`ArchitectureProxy` visibility** — promoted from `internal` to
  `public` so sibling composite categories (Verticals, Grids,
  Callouts, Sections) can reuse the `draw_line` / `draw_polyline` /
  `draw_circle` / `draw_arc` / `add_dbtext` / `dimension_linear` /
  `dimension_aligned` / `create_layer` gateways without duplicating
  JSON-object plumbing (rule 35 §2).
- **rule 67-grid-axes.md** — single source of truth for the
  column-grid + vertical-circulation policy: layer key (A-GRID /
  A-GRID-MINOR / A-GRID-BUB / A-GRID-ID / A-STRS / A-STRS-DIR /
  A-RAMP / A-RAMP-DIR / A-VTRN-ELEV / A-VTRN-ESCL / A-VTRN-LIFT /
  A-RAIL), Polish X=letters / Y=numbers convention, bubble geometry
  (text height = 0.9 · radius, centred-offset DBText for 2019
  portability), typical module sizes per building type, hospital 7.2
  m reference, `snap_to_grid` validator tolerance (50 mm for
  modular, `A-WALL-NONMOD` escape hatch), WT threshold table, U-shape
  landing constraint, handrail-coverage convention, grid ↔
  dimension-chain interplay with rule 66 (L3 overall measured between
  outermost bubbles, L2 axis stations locked to bubble centres),
  `delete_grid` safety guidance, and the standard "Do NOT" list.
- **Tests**: `VerticalsTests` (8 tool names pinned) + `GridsTests`
  (6 tool names + `LetterLabel` spreadsheet-style generation,
  `CumulativeOffsets` sums, `SnapToGrid` intersection math). Full
  suite: **103/103 passing** (was 100/100 after D6 + 5 new D7 tests).
- **Manifests regenerated** for `acad-verticals` (1→8 tools) and
  `acad-grids` (1→6 tools). `check-manifests.ps1`: **0 problems**
  across 29 code categories / 30 manifests.
- **Build**: `dotnet build src\AcadMcp.sln -c Release` → 0 errors /
  0 warnings.

### Added - Phase D D6: Architecture + Dimensions + Blocks extension (2026-04-24)

- **acad-architecture +6 composite tools** (rule 66):
  - `draw_ceiling_grid` — grid layout of suspended ceiling tiles with rotation.
  - `insert_stair` — straight stair run with tread lines + up-direction arrow.
  - `insert_ramp` — ramp outline with slope arrow + percentage label.
  - `insert_elevator` — elevator shaft rectangle with diagonals + centre label.
  - `attach_room_tag` — 3-line number/name/area room tag on `A-ANNO-NOTE`.
  - `split_wall_at_opening` — wraps `acad.openings.cut_wall_for_opening`
    for consistent wall-splitting semantics (2-vertex walls only; multi-
    vertex polyline wall splitting queued for D7).
  All six are **composite** — they call `acad.geometry2d.*`, `acad.layers.*`,
  `acad.annotations.*` and `acad.openings.*` primitives through
  `ArchitectureProxy` without introducing a new plugin-side handler file.
- **acad-dimensions +5 tools** (rule 66):
  - **Plugin primitives** (3) — `ensure_architectural_dimstyle` (creates /
    updates `ARCH-ISO` with tick marks, 2.5 mm text, DIMSCALE=100,
    DIMRND=1 mm, DIMDEC=0), `dimension_cumulative_chain` (running-total
    segments on a single dim-line point) and `apply_arch_tick_style`
    (sweeps every `Dimension` on a layer, reassigns to target style).
  - **Backend composites** (2) — `auto_dim_walls` (projects wall end-points
    onto a baseline, merges T-junctions within `mergeToleranceMm`, hands
    off to `acad.dimensions.continued_chain`) and `dimension_overall`
    (bounding-box extents along a rotation axis, hands off to
    `acad.dimensions.linear`).
- **acad-blocks +4 tools** (rule 28 extension):
  - `library_register` / `library_list` — file-persistent catalog of block
    libraries under `%LocalAppData%/AcadMcp/block-libraries.json`. Each
    library = named folder of `.dwg` files, scanned recursively by default.
  - `bulk_insert` — multi-item insert with auto-import from registered
    libraries for missing block names; returns per-item handles + list of
    imported block names + skipped count.
  - `swap_block` — globally replaces every `BlockReference` of `oldName`
    with `newName`, preserving position/rotation/scale/layer and copying
    matching attribute tag/value pairs onto the new definition.
- **rule 66-dimension-chains.md** — documents the 3-level chain
  hierarchy (L1 opening / L2 axis / L3 overall), ARCH-ISO DIM var table,
  continued vs baseline vs cumulative semantics, `auto_dim_walls`
  projection + T-junction-merge algorithm, layer conventions
  (`A-ANNO-DIMS`, `S-ANNO-DIMS`, `A-ANNO-DIMS-EGRESS`) and compliance
  tie-in to PN-B-01025 / PN-EN ISO 129-1.
- **Tests updated**: `ArchitectureTests`, `DimensionsTests`, `BlocksTests`
  now pin every D6 tool by name. `dotnet test`: **100/100 passing**.
- **Manifests regenerated** for `acad-architecture` (10→16 tools),
  `acad-dimensions` (12→17), `acad-blocks` (12→16). `check-manifests`:
  29 code categories / 30 manifests / **0 problems**.
- **Build**: `dotnet build src/AcadMcp.sln -c Release` → 0 errors /
  0 warnings across Plugin + Backend + Tests.

### Added - Phase D D0..D2: Scaffolding + Hatches category in flight

- **D0 — baseline frozen** (2026-04-24):
  - `assets/Hospital2026_A0-001_BEFORE_PHASE_D.dwg` (61 686 B) backup copy.
  - AutoCAD router checkpoint `ckpt-20260424-075427802` labelled
    `phaseD_start_baseline` — rollback anchor if D1..D12 fails.
- **D1 — 10 new MCP categories scaffolded**:
  `acad-hatches`, `acad-furniture`, `acad-plumbing`, `acad-openings`,
  `acad-verticals`, `acad-grids`, `acad-schedules`, `acad-sections`,
  `acad-plotstyles`, `acad-callouts`. Each one got:
  - stub `[McpTool]` class under `src/AcadMcp.Backend/Categories/<Folder>/`
  - mcpbank manifest under `mcpbank-manifests/acad-*.json`
  - launcher under `bin-launchers/acad-*.cmd`
  - smoke test under `tests/AcadMcp.Tests/Categories/<Folder>Tests.cs`
  - `_README.md` with planned tool list
  - Single source of truth: `scripts/new-category.ps1` (rule 41).
  - Post-scaffold `check-manifests.ps1`: 29 code categories / 30 manifests,
    0 problems. `dotnet build src/AcadMcp.sln -c Release`: 0 errors, 0 warnings.
- **D2 — acad-hatches (P0) fully implemented** (8 tools):
  - `draw_hatch` — boundary-handle fill with color / bg / associative / annotative.
  - `draw_hatch_by_boundary` — seed-point auto-boundary via
    `Editor.TraceBoundary(detectIslands)`, persisting temp boundaries to
    non-plottable layer `A-BNDRY-TEMP`.
  - `list_patterns` — read-only, 45+ patterns (ANSI31..38, ISO02/03/09,
    AR-CONC / BRSTD / BRELM / B816 / B88 / RROOF / HBONE / PARQ1 / SAND /
    RSHKE, BATTING, EARTH, CORK, NET / NET3, GRAVEL, SWAMP, GRASS, HONEY,
    TRIANG, DOTS, CROSS, ESCHER, FLEX, ZIGZAG, CLAY, SACNCR, SOLID, LINE)
    with default scale/angle hints.
  - `apply_material_preset` + `apply_material_preset_by_point` — 23 material
    keys (`concrete`, `reinforced-concrete`, `concrete-block`, `brick`,
    `brick-elm`, `insulation`, `plaster`, `stone`, `earth`/`soil`, `steel`,
    `glass`, `wood-cross`, `wood-grain`, `parquet`, `tile`, `lead-shield`,
    `faraday`, `sand`, `cork`, `gravel`, `grass`) each mapped to
    (pattern, scale, angle°, ACI color) per new rule 62.
  - `clip_hatch` — replace loops on an existing hatch and re-evaluate.
  - `regenerate_hatches` — scoped re-eval: explicit handles, layer filter,
    or `allInModelSpace=true` after bulk wall edits.
  - `list_hatches` — read-only enumeration with layer/pattern filter
    returning handle / layer / pattern / scale / angle / area / loopCount /
    associative for every Hatch in model space.
  - Plugin side (`HatchesPluginTools.cs`) fully wired through
    `PluginEntryPoint.Initialize` and `ToolHost.Register(...)`; all
    handlers go through `PluginToolRunner.RunWriteAsync` (or `RunReadAsync`
    for read-only tools) per rules 10/11/19.
- **Rule 62 (`docs/engineering-rules/62-hatching-policy.md`)**: authoritative
  material -> (pattern, scale, angle, color) table, drawing-unit mm
  assumption, associativity rules, `A-BNDRY-TEMP` layer contract,
  TraceBoundary failure modes, plot-lineweight decoupling from hatches,
  per-tool performance budget (< 120 ms single hatch, < 800 ms seed-point
  fill, < 8 s regenerate-all on a 5 000 m² plan with 200 hatches).
- **Manifest sync**: `mcpbank-manifests/acad-hatches.json` regenerated via
  `dotnet run --project src/AcadMcp.Backend -- --category hatches
  --regenerate-manifest` — tools_summary now lists all 8 real tools,
  intent_examples auto-populated from `[McpTool] Intent=` arrays
  (40 PL+EN entries total).
- **Build + test gate**: `dotnet build -c Release` = 0 err 0 warn.
  `dotnet test --filter HatchesTests` = 1/1 passed. `check-manifests.ps1`
  = 29 code / 30 manifests / 0 problems.

### Added - Phase D D5: acad-openings (P0) fully implemented (2026-04-24)

- **10 tools** covering professional doors + windows with fire (REI),
  burglary (RC per PN-EN 1627), acoustic (Rw dB) and lead-shield ratings,
  automatic numbering and schedule export:
  - `list_opening_catalog` — read-only, 11 door + window families with
    `supportsFire`, `supportsBurglary`, `supportsLeadShield` capability
    flags surfaced to callers + validators.
  - `insert_door` — types `single` / `double` / `sliding` / `fire` /
    `hospital` (double-swing) / `lead` (radiological shielding).
    Auto-numbers `D-001` unless `number=` or `autoNumber=false` supplied.
    Full attribute contract written: `NUMBER`, `TYPE`, `WIDTH_MM`,
    `HEIGHT_MM`, `REI`, `LEAF_DIR`, `SWING_DIR`, `ACOUSTIC_DB`, `LEAD`,
    `ROOM_FROM`, `ROOM_TO`.
  - `insert_window` — types `fixed` / `casement` / `tilt` / `hospital`
    (fire-rated) / `fire`. Auto-numbers `W-001`. Writes `RC`, `FIRE_CLASS`,
    `SILL_MM`, `ROOM`.
  - `insert_opening_generic` — escape-hatch: insert any `DOOR-*` / `WIN-*`
    block by canonical name with explicit attribute map.
  - `draw_door_by_points` — quick-sketch line + 90° swing arc between
    hinge and leaf-end, no block / no attributes (concept studies only).
  - `draw_window_by_points` — quick-sketch 2 parallel lines + centre glass
    line between two jamb points (`wallThickness` configurable).
  - `cut_wall_for_opening` — surgical wall split: projects jamb points
    onto wall axis, erases original Line / 2-vertex Polyline, creates
    up to 2 surviving segments; reports `gapLengthMm` + new handles.
    Multi-segment polyline walls rejected with a clear message (D6 scope).
  - `renumber_openings` — `kind=doors|windows|all`,
    `order=insertion|spatial` (Y↓ X→), overridable prefixes and
    zero-padding. Returns per-entity change log.
  - `list_openings_in_model` — read-only, full attribute decoding,
    filter by kind and layer. 18 fields per opening.
  - `export_schedule` — read-only CSV / JSON (18-column schema).
    Optional `outputPath=` for on-disk write (UTF-8). Sorted by `NUMBER`.
- **Block library (parametric, in-code)** — 11 sized families keyed by
  `<FAMILY>-<W>-<H>`. Geometry at block origin = centre of the opening on
  the wall axis. Block draws jamb ticks + leaf / swing-arc / glass-lines;
  the wall gap itself is carved separately via `cut_wall_for_opening` —
  a two-step workflow that keeps wall surgery auditable.
- **Unified 14-tag attribute contract** (every opening carries all tags;
  empty-string when irrelevant for the kind): `NUMBER` (visible), `TYPE`,
  `WIDTH_MM`, `HEIGHT_MM`, `REI`, `RC`, `FIRE_CLASS`, `LEAF_DIR`,
  `SWING_DIR`, `SILL_MM`, `ACOUSTIC_DB`, `LEAD`, `ROOM_FROM`, `ROOM_TO`.
  Visibility restricted to `NUMBER` so 1:100 plans stay readable.
- **Layer split** (AIA-2017): `A-DOOR`, `A-DOOR-FIRE`, `A-DOOR-HOSP`,
  `A-DOOR-LEAD`, `A-GLAZ`, `A-GLAZ-FIRE`. Inferred from block family;
  caller override supported.
- **Automatic numbering**: each insert scans model-space for max
  `D-nnn` / `W-nnn` and increments; callers can pin `number=` explicitly
  (e.g. `D-EVAC-01` for evacuation routes); `renumber_openings` resets.
- **Rule 65 (`docs/engineering-rules/65-door-window-schedule.md`)**: block naming,
  origin semantics, the 14-tag attribute contract (table form with
  visibility + per-kind applicability), numbering rules, layer split,
  wall-cutting projection semantics with cautionary notes about jamb
  location, schedule column order (18 columns), interaction with
  architecture / hatches / furniture / plumbing / schedules / sections,
  per-tool perf budget (< 60 ms single insert, < 300 ms schedule for
  ≈ 200 openings), and a 7-step checklist for adding new families.
- **Plugin side** (`OpeningsPluginTools.cs`, ~820 LOC) wired in
  `PluginEntryPoint.Initialize`; every handler goes through
  `PluginToolRunner.RunWriteAsync` / `RunReadAsync` per rule 10 / 11 / 19.
- **Manifest sync**: `mcpbank-manifests/acad-openings.json` regenerated;
  tools_summary lists all 10 real tools, intent_examples auto-populated
  from `[McpTool] Intent=` arrays (60+ PL + EN entries).
- **xUnit coverage**: `tests/AcadMcp.Tests/Categories/OpeningsTests.cs`
  replaced stub with 3 tests — 1 structural (10 tools registered) + 1
  theory asserting each tool name is present + 1 flag-semantics test
  (`list_*` and `export_schedule` = read-only; `insert_*`, `cut_wall_*`,
  `renumber_*` = write). All 15 tests across D2 + D3 + D4 + D5 pass.
- **Build gate**: `dotnet build src/AcadMcp.sln -c Release` = 0 err 0 warn,
  `check-manifests.ps1` = 29 code / 30 manifests / 0 problems.

### Added - Phase D D4: acad-plumbing (P0) fully implemented (2026-04-24)

- **9 tools** covering sanitary fixtures for hospitals, offices and
  residential buildings (WT-2019 + PN-EN 17210 accessibility):
  - `list_plumbing_catalog` (14 catalogue entries with Polish / EN standard
    references — PN-EN 997, 14528, 14688, 13407, 232, 14527, 17210).
    Filter by category / domain / `accessibleOnly`.
  - `insert_plumbing` — generic by fully-qualified name.
  - `insert_wc` — floor-standing / wall-hung / bidet-combo + accessible
    800×800 variant with grab-bar L-marker (PN-EN 17210 §T.4).
  - `insert_basin` — standard / double / accessible (knee-clearance marker
    per §U.2).
  - `insert_shower` — square/rect shower tray or walk-in barrier-free
    (curtain indicator + centre drain per §S.3).
  - `insert_bathtub` — standard 1700×700 / mini 1500×700 / corner
    1400×1400 quarter-round + drain + faucet indicators.
  - `insert_urinal` — wall-hung + accessible lower-rim variant (§U.4).
  - `populate_bathroom` — 6 presets: `wc-public`, `wc-accessible`,
    `bathroom-residential`, `bathroom-hospital-patient`, `shower-room`,
    `wc-block-staff`. All orientation-aware (north/east/south/west).
  - `list_plumbing_in_model` — read-only, filters by layer / block name,
    returns INV_ID / TYPE / ACCESSIBLE attribute values.
- **Block library (parametric, in-code)** — 8 fixed + 6 sized families,
  every block with origin at geometric centre and four attribute
  definitions (INV_ID visible, TYPE / ACCESSIBLE / NOTE hidden). Sized
  families key each unique (W, D) into a distinct cached
  `BlockTableRecord` named `<family>-<W>-<D>`.
- **Layer split** (AIA-2017):
  `A-PLMB-WC / -BSN / -SHW / -BT / -UR`, fallback `A-PLMB`.
- **Rule 63 (`docs/engineering-rules/63-sanitary-fixtures-wt.md`)**: canonical
  block-name convention, fixed catalogue + sized-family tables,
  attribute contract (ACCESSIBLE flag fed to downstream schedules /
  validators), layer split table, the 6 populate_bathroom presets with
  minimum room sizes (WT-2019 §82 1600×2200 residential, PN-EN 17210 §T.1
  1500×1800 accessible WC, §S.3 walk-in shower clearance), clearance budget
  (basin front clearance, WC grab-bar envelope, wheelchair turning Ø
  1500 mm, urinal centre-to-centre 760 mm), interaction with
  architecture / furniture / hatches / validators, per-tool perf budget
  (< 150 ms single insert, < 500 ms full preset).
- **Plugin side** (`PlumbingPluginTools.cs`, ~610 LOC) wired in
  `PluginEntryPoint.Initialize`; every handler goes through
  `PluginToolRunner.RunWriteAsync` / `RunReadAsync` per rule 10/11/19.
- **Manifest sync**: `mcpbank-manifests/acad-plumbing.json` regenerated;
  tools_summary lists all 9 real tools, intent_examples auto-populated
  (45+ PL+EN entries).
- **Build gate**: `dotnet build src/AcadMcp.sln -c Release` = 0 err 0 warn,
  `check-manifests.ps1` = 29 code / 30 manifests / 0 problems.

### Added - Phase D D3: acad-furniture (P0) fully implemented (2026-04-24)

- **10 tools** covering hospital + office + residential furniture with
  parametric, zero-asset block generation (first-call creates
  `BlockTableRecord`, subsequent calls insert `BlockReference`s):
  - `list_furniture_catalog` — read-only, 15 fixed + 11 sized families
    (bed / chair / desk / cabinet / sofa / table) filterable by category or
    domain (hospital / office / residential).
  - `insert_furniture` — generic, fully-qualified block name (e.g.
    `FURN-DESK-OFF-1600-800`), supports independent `scaleX`/`scaleY`.
  - `insert_bed` (standard / ICU / bariatric / pediatric / OR / labour)
  - `insert_chair` (office / armchair / stool / exam / wheelchair)
  - `insert_desk` (office / reception / nurse-station, configurable W × D)
  - `insert_cabinet` (storage / medical / file / wardrobe, configurable W × D)
  - `insert_sofa` (2/3-seat lounge or clinical)
  - `insert_table` (rectangle / round / square / exam, configurable W × D)
  - `populate_room` — auto-fill a room (closed polyline handle OR bbox) with
    7 presets: `ward-room` / `icu-room` / `or-room` / `office` /
    `reception` / `waiting` / `consult`; supports orientation
    north / east / south / west (whole plan rotates around centroid).
  - `list_furniture_in_model` — read-only, enumerates all `FURN-*`
    BlockReferences with INV_ID / TYPE / NOTE attributes, filterable by layer
    or block name.
- **Block library (parametric, in-code)**:
  - 15 **fixed** blocks: 6 beds, 5 chairs, 4 sofas (all with geometric centre
    origin — rotations spin around centre for predictable placement).
  - 11 **sized** families — each unique (W mm, D mm) pair produces a
    distinct cached `BlockTableRecord` named
    `<family>-<W>-<D>` (e.g. `FURN-DESK-OFF-1600-800`,
    `FURN-CBT-MED-800-500`, `FURN-TBL-ROUND-1200-1200`).
  - Every block carries four attribute definitions: `INV_ID` (visible),
    `TYPE` / `ROOM` / `NOTE` (hidden, editable) — schedule-ready (D8).
- **Layer split** (AIA-2017): `A-FURN-BED / -CHR / -DSK / -CBT / -SFA / -TBL`
  auto-applied by block prefix, fallback `A-FURN`. Callers may override.
- **Rule 64 (`docs/engineering-rules/64-furniture-density-per-room.md`)**:
  drawing-unit (mm) assumption, block-name convention, attribute contract,
  layer split table, the 7 preset definitions with minimum room sizes,
  clearance budget per PN-EN 17210 / WT-2019 §95 / §234 (bed side access
  ≥ 900 mm, wheelchair turning Ø ≥ 1500 mm, corridor with bed transit
  ≥ 2200 mm), scaling/rotation rules, cross-category interactions
  (architecture, openings, hatches, schedules), per-tool performance budget
  (< 200 ms single insert, < 500 ms 5-item preset, < 1.5 s 15-item preset).
- **Plugin side** (`FurniturePluginTools.cs`, ~630 LOC) wired in
  `PluginEntryPoint.Initialize`; every handler goes through
  `PluginToolRunner.RunWriteAsync` / `RunReadAsync` per rule 10/11/19.
- **Manifest sync**: `mcpbank-manifests/acad-furniture.json` regenerated via
  `dotnet run --project src/AcadMcp.Backend -- --category furniture
  --regenerate-manifest` — tools_summary now lists all 10 tools,
  intent_examples auto-populated (50+ PL+EN entries).
- **Build gate**: `dotnet build src/AcadMcp.sln -c Release` = 0 err 0 warn,
  `check-manifests.ps1` = 29 code / 30 manifests / 0 problems.

### Planned - Phase D: Architectural Fidelity Upgrade (awaits green-light)

- **Plan document**: `docs/PLAN-PROFESSIONAL-UPGRADE-2026.md` — 13-section roadmap to
  bring generated drawings from parametric-MVP level (walls + rectangles + bed-rectangles)
  up to executive-grade output comparable to a Polish architectural practice
  (reference: user-supplied plan excerpt 2026-04-23 with axis grids Y1/Y3, chain
  dimensions, spiral staircase, full furniture, sanitary fixtures, K1/K6/K10
  profile callouts).
- **Gap analysis**: 17 checklist items, 4 CRITICAL + 7 MAJOR + 6 MINOR gaps
  identified (hatching, furniture, plumbing, windows, stairs/elevators, grids,
  dimension chains, schedules, sections, lineweights, callouts, symbols, RCP,
  construction details).
- **New MCP categories (7 proposed)**: `acad-hatches` (8 tools), `acad-furniture`
  (10), `acad-plumbing` (8), `acad-openings` (10), `acad-verticals` (7),
  `acad-grids` (6), `acad-schedules` (5), `acad-sections` (4),
  `acad-plotstyles` (3), `acad-callouts` (4) — **48 new tools** respecting
  the single-backend-per-category invariant (`00-architecture-invariants.md` §1).
- **Extensions (3 existing categories)**: `Architecture` +6 tools (draw_window,
  draw_stair, draw_elevator, draw_ramp, draw_ceiling_grid, split_wall_at_opening),
  `Dimensions` +5 tools (chain, cumulative, baseline, tick policy, auto_dim_walls),
  `Blocks` +4 tools (library_register, library_list, bulk_insert, swap_block).
- **Block library (80+ DWG assets planned)**: `assets/blocks/{furniture-hospital,
  furniture-office, plumbing, openings, verticals, symbols}/` with per-folder
  `manifest.json`.
- **10 new `docs/engineering-rules/` entries (60-69)**: architectural fidelity minimum,
  9-tier lineweight policy, per-layer hatching policy, WT §78 sanitary fixtures,
  per-room furniture density, door/window schedule atts, 3-level dimension chains,
  grid axes with bubble labels, plan symbols (north + scale bar + title block
  per PN-B-01025), K1/K6/K10 callout convention.
- **New vision persona** `senior-architect-reviewer` with 17-criterion rubric
  (PL+EN) covering WT 2022, MZ 2019, PN-B-01025, PN-EN-ISO-129/128/5457, AIA
  Layer Guidelines. Output: deterministic JSON `{score, grade, findings[],
  overall}`.
- **Phase D execution plan (D0..D12)**: scaffold → P0 categories (hatches,
  furniture, plumbing, openings) → P1 (verticals, grids, schedules) → P2/P3
  (sections, plotstyles, callouts) → rules → persona → regenerate
  Hospital 2026 plan from Phase C checkpoint → target `senior-architect-reviewer`
  score ≥ 15/17 on overview and ≥ 13/17 on every zoom tile.
- **12 / 10 rubric refined**: 4 axes (safety 12 @ 40% + arch-fidelity 17 @ 40%
  + legal compliance 10 @ 15% + docs 5 @ 5%) = 44 pts total. Current state:
  30/44 = 8.2/10. Target post-D: 43/44 = 11.7/10.
- **Effort estimate**: ~47.5 person-days (27 dev + 9 QA + 11.5 block-authoring)
  or ~25 calendar days at 2 FTE.
- **Risks + rollback**: per-D-step checkpoints (`ckpt-phaseD-<step>`),
  `check_overlaps` × 5 gate before/after D12 regeneration, manifest sync via
  existing `CheckManifestSync` MSBuild target.

### Fixed - Phase C-Doors: 25 missing doors added to the Hospital 2026 A0-001 (2026-04-23)

- **Diagnosis tooling** (`scripts/room-door-inventory.py`, `room-door-inventory2.py`,
  `analyze-door-swings.py`): build a 53-room × 61-door matrix from a
  `collect_entities` + `check_overlaps` snapshot, decompose 99 wall polylines
  into 127 axis-aligned segments, and classify door-door / door-wall bbox
  collisions into `{T-junction, leaf-in-frame, same-door leaf+arc, double-leaf,
  swing-conflict}` buckets.
- **Fix generator** `scripts/gen-missing-doors.py`: emits a ready-to-run
  `acad_design_iterate` plan (50 steps: 25 × `acad.geometry2d.draw_line` +
  25 × `acad.geometry2d.draw_arc`) for every room the inventory flagged as
  door-less, picking the corridor-facing wall and placing a 1100 mm leaf
  hinged on the inside jamb with a 90° swing arc (radius 1100 mm).
- **Execution** (router checkpoint `ckpt-20260423-230845856`): 25 new doors
  created on layer `A-DOOR` at handles `528..559`. Post-fix re-inventory
  reports 0 rooms without a door.
- **Compliance** (`docs/HOSPITAL-2026-REVIEW-FINDINGS.md` §4d/§4e):
  0 through-wall shielding breaches, 0 wall-through-bed crossings,
  0 text-text overlaps, 0 real door-door swing conflicts, every room
  carries at least one door → **12 / 12** on the safety axis of the review
  rubric. Deliverables: `assets/Hospital2026_A0-001.dwg` (61 686 B,
  534 entities, AC1032), `assets/Hospital2026_FINAL.pdf` (ISO A0, 180 047 B),
  `assets/Hospital2026_POSTER_6000x4500.png` (670 082 B).

### Added - Phase 7.4 acad-router meta-tools catch-up (9th tool + loop phase)

- **Router**: ninth meta-tool `acad_design_iterate` added as a first-class
  stub in `tools/list` (RouterServer.cs) — acts as the entry point for the
  Phase 7.0 plan→checkpoint→execute→validate→auto-fix→rollback loop.
- **Manifest** `mcpbank-manifests/acad-router.json`: `tool_count: 9`,
  `tools_summary` extended with `acad_design_iterate`, and
  `metadata.phase` bumped to `phase7_loop` so discovery surfaces the loop
  capability.
- **Tests**: wired `tests/AcadMcp.Tests/AcadMcp.Tests.csproj` into
  `src/AcadMcp.sln`, added `[InternalsVisibleTo]` from
  `src/AcadMcp.Backend/Properties/AssemblyInfo.cs`, and made `ToolRegistry`
  default-constructible (NullLogger). `scripts/pre-commit.ps1` grew a
  `[7/7] dotnet test` gate; suite is currently 78 green.

### Added - Phase 7.2 validators cross-entity primitives + 4 new discipline rules

- **Entity snapshot enrichment**: `ValidatorsPluginTools.BuildSnapshot`
  now collects `className` (from `ent.GetRXClass().Name`) and `vertices`
  for `Line`, `Polyline`, `Polyline2d`, `Polyline3d`. DTOs
  `EntitySnapshotPluginDto` / `EntitySnapshotDto` extended symmetrically.
- **CheckEvaluator**: 4 new check-types
  - `entity_class_equals` — true AutoCAD RX-class equality (catches
    `AcDbCircle` where a thread representation demands `AcDbArc`).
  - `text_matches_regex` — regex against `TextValue` or a named block
    `attribute` (e.g. `TAG`).
  - `polyline_closure_within` — first-vertex-to-last-vertex distance
    check with tolerance (mm).
  - `polyline_endpoints_share` — **cross-entity**: each polyline
    endpoint must share coordinates with another entity within a
    tolerance, optionally filtered by `block_name` or `layer`. Requires
    `EvalContext.AllEntities`; `ValidationEngine.RunAsync` now populates
    it per rule from the correct model- or paper-space bucket.
- **New YAML rules** (4): `validators/mechanical/thread-is-arc-not-circle`,
  `validators/electrical/tag-format-iec-81346`,
  `validators/civil/parcel-closure-within-tolerance`,
  `validators/electrical/wire-crossing-needs-junction`.
- **Tests** `ValidatorsCoreTests.cs`: every YAML under `validators/`
  parses; all four new Phase 7.2 rules are discoverable; one unit test
  per new primitive, including the negative cross-entity case (junction
  only at one endpoint = violation) and the positive one (junction at
  both endpoints = clean).

### Added - Phase 7.0 checkpoint sub-system + acad_design_iterate loop

- **Plugin** `CheckpointPluginTools.cs`: 4 tools
  `acad.checkpoint.create/restore/list/clear` on an in-memory LIFO stack
  per-document, issuing AutoCAD `UNDO _Mark` / `UNDO _Back <n>` on the
  UI thread via `doc.SendStringToExecute` (rule 15). Optional DWG file
  snapshot to `%LOCALAPPDATA%\AcadMcp\checkpoints\<docKey>\<id>.dwg` with
  automatic best-effort fallback when the UNDO stack cannot be trusted
  (e.g. after a document reload).
- **Router**: `acad_status`, `acad_undo_checkpoint`,
  `acad_restore_checkpoint` and `acad_design_iterate` are no longer
  stubs — they invoke the plugin through `IPluginGateway.InvokeAsync`.
  `PluginUnavailableException` / `PluginToolException` are mapped to
  MCP `isError:true` content instead of JSON-RPC errors, so agents get
  actionable messages.
- **DI**: `Program.cs` now always registers `IPluginGateway`
  (router-mode included) so the router can drive plugin tools without
  losing its "no local tool-catalog" invariant.
- **Design loop** `Mcp/DesignIterator.cs`: wraps a user-supplied
  `PlanStep[]` + `standardId` into the 6-stage loop —
  create checkpoint → execute every step → `ValidationEngine.RunAsync`
  → auto-apply `fix:` blocks where present → re-validate →
  bail out / succeed / rollback to the checkpoint. A structured audit
  log is written to `%LOCALAPPDATA%\AcadMcp\logs\iterate-<ts>.json` on
  every run (success or abort).
- **E2E regression** `scripts/e2e-smoke.ps1`: spawns all 19 MCP servers
  over stdio, runs `initialize → tools/list → shutdown → exit`, and
  asserts the tool count matches each `mcpbank-manifests/acad-*.json`.
  Currently 19/19 green + `-Live` adds a router→plugin `acad_status`
  round-trip (20/20 with AutoCAD running).

### Added
- Documented the development status log (`docs/PHASE-7-STATUS.md`), always-apply engineering rule `54-development-status.md`, and README Status / onboarding section so agents treat the documented foundation as delivered and check current status before assuming a tool works.

### Added - Phase 6.5 acad-parametric (rule 42 + 12 tools + plugin acad.parametric.* + parametric-baseline)

- **Rule first** (rule 53): `42-parametric-domain-traps.md` — Block Editor vs
  model space, Fix as datum anchor, over-constraining, explode destroys
  constraints, dynamic-block value typing plus anonymous `*U` block names,
  geometric vs dimensional strategy, hatch vs boundary polylines, 6-layer
  P-* key, Phase-7 block library note.
- **Plugin** `ParametricPluginTools.cs`: 10 handlers on `acad.parametric.*`
  — `-GEOCONSTRAINT` via `Editor.Command` (transaction committed before the
  command so AutoCAD owns the command transaction), `-DELCONSTRAINT`, model-
  space constraint-entity scan (class name contains `Constraint`),
  dynamic-block property get/set with degree-to-radian coercion when
  `UnitsType.ToString()` contains `angl` (portable across AutoCAD enum
  spellings).
- **Backend** `Categories/Parametric/`: `ensure_parametric_layers`,
  `apply_geom_horizontal` / `vertical` / `parallel` / `perpendicular` /
  `coincident` / `fix`, `delete_entity_constraints`,
  `list_constraint_entities`, `get_dynamic_block_properties`,
  `set_dynamic_block_property`, `parametric_health`; `ParametricProxy`,
  `ParametricPalette`, DTOs; registered in `PluginEntryPoint`.
- **Validators**: `parametric.sketch.on-p-sketch`,
  `parametric.profile.on-p-constrained`; standard
  `validators/_standards/parametric-baseline.yaml` (5 rules including
  general ISO hygiene). Validators self-check now loads **29 rules across 6
  standards** (was 27 / 5 after Phase 6.4 electrical).
- **Manifest** `mcpbank-manifests/acad-parametric.json` — `phase6_domains`,
  `discipline: parametric`, `depends_on_categories` layers+blocks,
  explicit `v1_limitations`.
- **Tests** `tests/AcadMcp.Tests/Categories/ParametricTests.cs` — 12-tool
  catalog, 6 palette layers, `parametric_health` ReadOnly + pluginless,
  non-empty `DynamicAnglePolicy` string.

### Added - Phase 6.4 acad-electrical (rule 39 + 12 tools + 5 paired validators + electrical-baseline)

- **Rule first** (rule 53): `39-electrical-domain-traps.md` BEFORE any
  electrical code. 13 traps: IEC vs ANSI symbol style (rectangle vs zig-zag
  for resistors, rectangle vs circle for coils — pick one per drawing);
  NO vs NC contact symbols (the horizontal slash IS the NC marker, so the
  category ships TWO separate tools `place_contact_no` and
  `place_contact_nc` — never one with a `kind` flag); junction dot vs
  crossing semantics (filled circle = electrical connection, no dot =
  pass-through); ladder rail + rung numbering (sequential rung-numbers on
  the LEFT rail, coil at the RIGHT end of each rung); coil → contact
  cross-reference text below the coil (`5: 12, 14, 18`); IEC 81346
  device-tag prefix lookup (`-K`/`-Q`/`-F`/`-S`/`-B`/`-M`/`-T`/`-G`/`-X`/
  `-W`/`-H` — agents who invent prefixes get a fail-fast); tag syntax
  with optional `=FUNC+LOC-PREFIXSEQ` aspects; wires-connect-at-symbol-
  terminals (every symbol exposes named terminals); schematic ≠ panel
  layout split (panel side deferred to Phase 7); power-rail colour
  convention (L1 brown, N blue, PE green/yellow — collapsed to layer
  `E-WIRE-PWR` ACI 1, label disambiguates); per-drawing symbol unit size
  (5 mm office default); the 12-layer IEC + JIC hybrid key; planned
  `blocks/electrical/` library; validator pairing rules.

- **acad-electrical MCP category — 12 tools**
  (`src/AcadMcp.Backend/Categories/Electrical/`):
  - infrastructure: `ensure_electrical_layers` (idempotent, ships the
    12-layer E-* key with full lineweight metadata; `includePanel=true`
    flag for cross-sheet drawings, default false because v1 ships
    schematic side only);
  - ladder: `draw_ladder_rails` (two vertical rails on `E-WIRE-PWR` with
    labelled tops `L1` / `N` per rule 39 §9), `draw_ladder_rung` (one
    horizontal rung + sequential rung-number text on the LEFT rail per
    rule 39 §4);
  - wires: `draw_wire` (poly-line routed to `E-WIRE` / `E-WIRE-PWR` /
    `E-WIRE-CTRL` by `kind` flag, with explicit override),
    `draw_wire_junction` (filled SOLID-hatched circle dot per rule 39 §3
    — separate tool by design, NOT auto-added by `draw_wire` because
    crossings without a junction are valid);
  - symbols: `place_resistor` (IEC rectangle / ANSI zig-zag with terminals
    `1`/`2`), `place_contact_no` (no slash, terminals `in`/`out`),
    `place_contact_nc` (slash present, terminals `in`/`out`), `place_coil`
    (IEC rectangle / ANSI circle with optional `tag` and `contactRungs`
    cross-reference text on `E-XREF`, terminals `A1`/`A2` per IEC);
  - terminals: `place_terminal_block` (row of N numbered rectangles on
    `E-TERM` with sequential labels on `E-LBL-WIRE`, exposes top + bottom
    centre points so wires snap to either side);
  - tags: `place_device_tag` (parses IEC 81346 short / location-qualified /
    fully-qualified form, validates the prefix letter against the
    11-letter set at write time, returns canonical
    `=FUNC+LOC-PREFIXSEQ`);
  - introspection: `electrical_health` (read-only — returns the layer key,
    the IEC prefix table, supported styles, default unit size,
    planned-block roster).

- **`ElectricalPalette.cs`** — single source of truth for the 12-layer
  electrical key (mirrors rule 39 §11) and the planned-block roster.
- **`IecDeviceTagPrefixes.Allowed`** — the `K`/`Q`/`F`/`S`/`B`/`M`/`T`/
  `G`/`X`/`W`/`H` lookup that powers `place_device_tag` validation.
- **`DeviceTag.cs`** — pure C# IEC 81346 parser, AutoCAD-free; validated
  by the `ElectricalTests` theory data (positive + negative cases, lower-
  case prefix coercion, dash-inferred short form, full canonical
  round-trip).
- **`ElectricalProxy.cs`** — IPC composition layer (rule 35 §2). Wraps
  primitive plugin handlers (`acad.layers.create_layer`,
  `acad.geometry2d.draw_*`, `acad.geometry2d.draw_hatch`,
  `acad.annotations.add_dbtext`) with full lineweight-aware layer
  metadata; centralises the JSON-shape mapping so the Plugin DTO contract
  doesn't leak into the tools.
- **5 paired validators** under `validators/electrical/`:
  `elec.symbol.on-e-symbol-layer`, `elec.wire.on-e-wire-layer`,
  `elec.wire.power-on-e-wire-pwr`, `elec.rung.label-on-e-lbl-rung`,
  `elec.tag.device-on-e-lbl-dev`. All ship with a `move_to_layer` fix.
- **New standard** `validators/_standards/electrical-baseline.yaml`
  bundles the three general ISO hygiene rules with the five electrical
  rules (8 rules total). Self-check now reports **27 rules across 5
  standards** (was 22 / 4 in Phase 6.3).
- **Conscious gap (documented)**: the prefix-format and missing-junction-
  dot validators are deferred until the YAML engine grows
  `text_matches_regex` and `polyline_endpoints_share` check primitives.
  Both conventions are enforced AT-WRITE-TIME by `place_device_tag` and
  by the explicit `draw_wire_junction` tool respectively.
- **Manifest** `mcpbank-manifests/acad-electrical.json` regenerated to
  reflect the 12 tools and updated metadata (`phase: phase6_domains`,
  `discipline: electrical`, `depends_on_categories`,
  `paired_validators_dir`, explicit `v1_limitations`).
- **Tests** in `AcadMcp.Tests/Categories/ElectricalTests.cs`: tool
  catalog (12 tools), layer key (12 layers, `E-WIRE-PWR` ACI 1 0.50 mm),
  IEC prefix table (11 letters), `electrical_health` ReadOnly +
  pluginless, `DeviceTag.Parse` accepts/rejects matrix, canonical
  round-trip.
- **Pre-commit gate** passes (6/6) — the manifest-sync + Intent-attribute
  + secret + CHANGELOG + validators-self-check checks are all green.

### Added - Phase 6.3 acad-civil (rule 38 + 10 tools + 5 paired validators + civil-baseline)

- **Rule first** (rule 53): `38-civil-domain-traps.md` BEFORE any civil
  code. 11 traps: stationing notation (Polish/EU `0+020` vs US `0+20`,
  single-source `format_station`), surveyor bearings (`N 45° 30' 15" E`,
  quadrant-driven sign — never `degrees * π / 180`), parcel closure
  tolerance (residential 0.02 m / commercial 0.05 m / agricultural 0.20 m /
  forest 0.50 m, never silently snap), contour intervals (major every
  5 / 10 m on `C-TOPO-MAJR` LABELLED, minor every 1 / 2 m on
  `C-TOPO-MINR` UNLABELLED), spot elevation = cross + signed two-decimal
  text on `C-TOPO-SPOT`, road centreline `CENTER` vs edge of pavement
  `Continuous` linetype split, station ticks perpendicular to LOCAL
  tangent (not page +X), true-north arrow (rotates with drawing rotation,
  not page +Y), the 12-layer civil key, the planned `blocks/civil/` library,
  and validator pairing.
- **acad-civil MCP category — 10 tools** (`Backend/Categories/Civil/`):
  - infrastructure: `ensure_civil_layers` (idempotent, ships the 12-layer
    key with full lineweight metadata; `includeRoad` / `includeProperty` /
    `includeTopo` flags so survey-only drawings can skip road layers);
  - alignment: `draw_alignment_tangent` (Line on `C-ROAD-CNTR` w/ CENTER),
    `draw_alignment_curve` (Arc on `C-ROAD-CNTR`);
  - corridor: `draw_road_corridor` (centreline + two parallel edges
    offset by `widthM/2` with mitred internal vertices via average-normal
    method);
  - stationing: `place_station_labels` (walks the centreline, drops a
    perpendicular tick + tangent-rotated label every `intervalM`, system
    `"metric_km"` or `"us_feet"`, recomputes tangent at every vertex per
    rule 38 §7);
  - parcel: `draw_parcel` (parses `(bearing, distance)` legs via
    `Bearing.Parse`, walks them via `CivilParcel.Traverse`, reports
    `closureErrorM` + `closureStatus` against the `kind` tolerance, optional
    `autoClose` to snap geometrically while still reporting the original
    error);
  - topography: `draw_contour_line` (routes to `C-TOPO-MAJR` LABELLED or
    `C-TOPO-MINR` UNLABELLED per `isMajor`), `place_spot_elevation`
    (cross + signed two-decimal text per PN-EN ISO 6709);
  - orientation: `draw_north_arrow` (synthesised triangle + optional `N`
    letter, rotated by `trueNorthDegFromPageNorth`, rule 38 §8);
  - introspection: `civil_health` (ReadOnly, RequiresPlugin=false; reports
    layer key + parcel-tolerance presets + supported stationing systems +
    planned bundled blocks).
- **CivilGeometry.cs** — pure-C# surveyor numerics: `Bearing` record (parse
  + format + `ToVector()` with quadrant-driven sign), `CivilStationing.Format`
  (single source of truth for `"0+020"` / `"0+20"`), `CivilParcel.Traverse`
  (returns vertices + closure error + within-tolerance flag).
- **CivilPalette.cs** — single source of truth for the 12-layer civil key
  (mirrors rule 38 §9), parcel-kind enum + `CivilTolerances.ClosureMetresFor`,
  and `PlannedBlocks` listing the Phase-7 DWG library
  (NORTH_ARROW_BASIC / COMPASS, BENCHMARK_GEODETIC, MANHOLE_CIRCULAR,
  CATCH_BASIN_GRATE, TREE_DECIDUOUS / CONIFEROUS, STATION_TICK_MAJOR).
- **CivilProxy.cs** — composition over `acad.layers.create_layer`,
  `acad.geometry2d.draw_line | draw_polyline | draw_arc`, and
  `acad.annotations.add_dbtext`. Same shape as MechanicalProxy
  (`Point2dDto → Point3dDto` JSON promotion via `ToPoint3dNode`,
  `EnsureLayerAsync` carrying full metadata).
- **Manifest** `mcpbank-manifests/acad-civil.json` regenerated to 10 tools
  and back-filled with `phase=phase6_domains`, `discipline=civil`,
  `depends_on_categories=[geometry-2d, layers, annotations]`,
  `paired_validators_dir=validators/civil/`, and an explicit
  `v1_limitations` list (vertical alignments / profile views deferred to
  Phase 7, spirals not in v1, north-arrow basic synthesised inline, road
  corridor uses average-normal mitre).
- **Paired validators under `validators/civil/`** — 5 NEW rules:
  - `civil.road.centerline-on-c-road-cntr` (legacy CENTERLINE / ROAD / AXIS
    → C-ROAD-CNTR with auto-create);
  - `civil.road.centerline-must-be-dashed` (CENTER linetype on
    C-ROAD-CNTR);
  - `civil.road.edge-on-c-road-edge` (legacy EOP / EDGE-OF-PAVEMENT →
    C-ROAD-EDGE);
  - `civil.topo.spot-on-c-topo-spot` (legacy SPOT-ELEV / RZEDNA → C-TOPO-SPOT);
  - `civil.parcel.on-c-prop` (legacy PROPERTY / LOT / PARCEL / BNDY →
    C-PROP).
- **`validators/_standards/civil-baseline.yaml`** (NEW) — bundles the
  5 civil traps + 3 ISO general baseline rules into one drop-in standard
  for civil deliverables.
- **Tests** `tests/AcadMcp.Tests/Categories/CivilTests.cs` — 10-tool inventory
  check, palette consistency (12 layers), `civil_health` is `ReadOnly +
  RequiresPlugin=false`, six bearing → vector parametric cases (covering
  all four quadrants + cardinals), bearing string round-trip, four
  metric-stationing format cases, and TWO `CivilParcel.Traverse` cases
  (perfect-closure 10×10 m square AND a deliberately-short last leg that
  triggers `WithinTolerance=false`).
- **Validators self-check** now reports **22 rules across 4 standards** (was
  17 / 3 after Phase 6.2).
- **Pre-commit gate**: 6/6 green.

#### Known gap

`civil.parcel.must-close` (rule 38 §3) waits on the validator engine
growing a `polyline_closure_within` check primitive. Convention is enforced
AT-WRITE-TIME by `draw_parcel` itself — the result includes
`closureErrorM` + `closureStatus` so the agent (or a downstream rule
runner) can flag out-of-tolerance parcels immediately.

### Added - Phase 6.2 acad-mechanical (rule 37 + 12 tools + 4 paired validators)

- **Rule first** (rule 53): `37-mechanical-domain-traps.md` BEFORE any
  mechanical code. 11 traps: edge-class linetypes (visible / hidden / centre
  each on its own layer + linetype), centreline shape (extension past the
  feature, not just a `+`), section cutting plane = thick PHANTOM polyline +
  arrow heads + labels (NOT the hatch — separate call), the four hole
  representations (through, counterbore, countersink, blind, threaded), the
  threaded-hole 3/4 minor-Ø arc convention (rule 37 §4a), bolt-head top view
  as flat-to-flat hexagon (NOT "size string"), filled-equilateral revision
  triangle, mechanical dimstyle (ISO-25, not architectural), ISO 128-50
  material → hatch pattern map, the 11-layer ISO-mechanical key, and validator
  pairing.
- **acad-mechanical MCP category — 12 tools** (`Backend/Categories/Mechanical/`):
  - infrastructure: `ensure_mechanical_layers` (idempotent, ships the 11-layer
    ISO key with full lineweight metadata, optional include flags for
    `ME-CONSTRUCTION` and `ME-REV`);
  - edge classes: `draw_visible_edge`, `draw_hidden_edge`, `draw_centerline`,
    `draw_centerline_cross` (round-feature crosshair sized by
    `featureRadiusMm + extensionMm`, rotation supported);
  - section: `draw_section_cut_line` (thick PHANTOM polyline + two
    arrow-head triangles + two DBText labels — 5 entity handles in one call);
  - holes: `draw_through_hole`, `draw_counterbore_hole` (cbore Ø must exceed
    through Ø, fails fast otherwise), `draw_threaded_hole` (FULL major circle
    on `ME-VISIBLE` + 3/4 arc on `ME-THREAD` HIDDEN, configurable gap angle
    and start, default 270° span);
  - fasteners: `draw_bolt_head_top_view` (regular hex flat-to-flat, optional
    inscribed shank circle, reports across-corners diameter; `nominalDiameterMm`
    is documentation-only);
  - revisions: `draw_revision_triangle` (closed equilateral polyline + SOLID
    hatch + Middle-aligned DBText, returns both triangle and text handles);
  - introspection: `mechanical_health` (ReadOnly, RequiresPlugin=false; reports
    layer key, ISO 128-50 material → hatch table, planned bundled blocks).
- **MechanicalPalette.cs** — single source of truth for the 11-layer ISO
  mechanical key (mirrors rule 37 §9). Each entry carries `Name`, `AciColor`,
  `Linetype`, `LineweightMm`, `Plottable`, `Purpose`. Plus
  `MechanicalPalette.PlannedBlocks` listing the Phase-7 DWG library
  (BOLT_HEX_M*, WASHER_FLAT_M*, BEARING_RADIAL_*, SURFACE_FINISH_BASIC,
  WELD_SYMBOL_BASIC).
- **MechanicalPatterns.ByMaterial** — ISO 128-50 material → `(pattern, scale,
  angle)` lookup so agents say `material: "steel"` instead of
  `pattern: "ANSI31"`. Currently covers cast iron, steel, bronze, brass,
  aluminium, glass, soil, concrete.
- **MechanicalProxy.cs** — composition helper that wraps
  `acad.layers.create_layer`, `acad.geometry2d.draw_line | draw_polyline |
  draw_circle | draw_arc | draw_hatch`, and `acad.annotations.add_dbtext`.
  Re-applies the lessons from `ArchitectureProxy` (parameter-name fixes,
  `Point2dDto → Point3dDto` JSON promotion via `ToPoint3dNode`,
  `EnsureLayerAsync` shape with full metadata).
- **Manifest** `mcpbank-manifests/acad-mechanical.json` regenerated to 12
  tools and back-filled with `phase=phase6_domains`,
  `discipline=mechanical`, `depends_on_categories=[geometry-2d, layers,
  annotations]`, `paired_validators_dir=validators/mechanical/`, and an
  explicit `v1_limitations` list (side-view holes deferred to Phase 7,
  section hatch is a separate call, bundled blocks ship in Phase 7).
- **Paired validators under `validators/mechanical/`** — 4 rules:
  - `mech.hidden.must-be-dashed` (existing — extended layer pattern to
    accept both `M-HIDDEN` and `ME-HIDDEN`);
  - `mech.hidden.on-me-hidden-layer` (NEW — moves legacy hidden geometry to
    `ME-HIDDEN`, with auto-create);
  - `mech.centerlines.must-be-dashed` (existing — extended layer pattern);
  - `mech.centerlines.on-me-center-layer` (NEW — moves legacy centrelines to
    `ME-CENTER`, with auto-create).
- **`validators/_standards/iso-mechanical-baseline.yaml`** (NEW) — bundles
  the 4 mechanical traps + 3 ISO general baseline rules into one
  drop-in standard for mechanical deliverables.
- **Tests** `tests/AcadMcp.Tests/Categories/MechanicalTests.cs` rewritten:
  asserts the exact 12 tool names, validates that `MechanicalPalette.All`
  has the canonical 11-layer key with `ME-CONSTRUCTION` as the only
  non-plottable entry, asserts `MechanicalPatterns.ByMaterial` covers the
  four ISO 128-50 anchor materials, and confirms `mechanical_health` is
  `ReadOnly + RequiresPlugin=false` (rules 19 + 22).
- **Validators self-check** now reports **17 rules across 3 standards** (was
  15 / 2 after Phase 6.1).
- **Pre-commit gate**: 6/6 green (build, manifest sync, forbidden patterns,
  secret scan, CHANGELOG, validators self-check).

#### Known gap

`mech.threads.minor-arc-not-full-circle` (rule 37 §4a) cannot be expressed
with the current YAML check primitives (need `entity_class_equals`).
Convention is enforced AT-WRITE-TIME by `draw_threaded_hole` always
emitting an `Arc` (never a `Circle`); the post-hoc validator will land
together with the new check primitive in a future phase. Documented in
rule 37 §4a and the category `_README.md`.

### Added - Phase 6.1 acad-architecture (rule 35 + rule 36 + 10 tools)

- **Rules first** (rule 53): two new `.md` files BEFORE any domain code:
  - `35-domain-categories-design.md` — universal contract for the 5 planned
    domain categories (`acad-architecture`, `acad-mechanical`, `acad-civil`,
    `acad-electrical`, `acad-parametric`): intent layer (not raw geometry),
    compose primitives, auto-create infrastructure, idempotent infrastructure
    + non-idempotent content, units check, paired validators, 30-tool budget,
    new-category checklist.
  - `36-architecture-domain-traps.md` — 13 architecture-specific pitfalls
    (walls = centreline + 2 faces, mitre/butt/square wall ends, doors are
    panel + swing + opening, windows are sill + glass + header, columns belong
    on `S-COLS` not `A-WALL`, rooms = closed boundary + tag with computed
    area, slabs on `S-SLAB`, stairs need stringers + treads + arrow + break
    line, linear vs aligned dim heuristic, hatches last, the AIA layer key
    table, bundled `blocks/architectural/` plan, validator pairing).
- **acad-architecture MCP category — 10 tools** (`Backend/Categories/Architecture/`):
  - `ensure_architectural_layers` (idempotent, ships the 16-layer AIA key),
    `draw_wall`, `draw_walls_chain` (mitred-corner offset polylines),
    `insert_door` (panel + swing arc), `insert_window` (sill + glass +
    header + 2 jambs), `insert_rect_column`, `insert_round_column`
    (column profile + crosshair on `S-COLS-CTRL`), `define_room` (closed
    boundary + 3 text labels with shoelace-formula area in m²),
    `dimension_wall` (auto-pick linear vs aligned per rule 36 §9),
    `architecture_health` (read-only metadata).
  - `ArchitecturePalette.cs` — single source of truth for the AIA layer
    key + planned bundled-block list (mirrors rule 36 §11/§12).
  - `ArchitectureProxy.cs` — composition helper that calls
    `acad.layers.create_layer`, `acad.geometry2d.draw_*`,
    `acad.annotations.add_dbtext`, `acad.dimensions.linear`/`aligned`
    via `IPluginGateway` so tools never duplicate plugin handlers
    (rule 35 §2). Sends `Point3dDto`-shaped payloads where the plugin
    expects them (e.g. `add_dbtext.position`, `dimensions.linear.p1/p2`).
- **Paired validators** (`validators/architectural/`):
  - Updated `walls-on-walls-layer.yaml` to migrate from legacy `WALLS` to
    AIA-canonical `A-WALL` (scope still catches `WALL`, `WALLS`, `M-WALL`,
    `WALLS_NEW`, `A-WALL-NEW`, `A-WALL-OLD` and offers a `move_to_layer` fix).
  - New `wall-centerlines-on-a-wall-ctrl.yaml` (warning, fixable) — pairs with
    `draw_wall` / `draw_walls_chain`.
  - New `columns-on-s-cols-layer.yaml` (error, fixable) — pairs with
    `insert_rect_column` / `insert_round_column`.
  - `polish-arch-baseline.yaml` updated to bundle the two new rules
    (now 9 rules vs the previous 7).
- `bin-launchers/acad-architecture.cmd` and
  `mcpbank-manifests/acad-architecture.json` (10 tools, `phase: phase6_domains`,
  `discipline: architectural`, `depends_on_categories: [geometry-2d, layers,
  annotations, dimensions]`, `paired_validators_dir`, explicit
  `v1_limitations` list calling out the door/window wall-cut deferral).
- Validator self-check passes with **15 rules / 2 standards** (was 13/2).

### Added - Phase 5 phase5_validators (in progress)

- **Rules first** (rule 53): two new `.md` files BEFORE any code:
  - `33-validators-rule-format.md` - canonical YAML schema for validator rules (`id`, `discipline`, `severity`, `scope`, `checks`, `fix`), check + fix primitive tables, hard rules (no blanket fixes, ≥25-char descriptions), and the `--validators-self-check` workflow.
  - `34-validators-engine-traps.md` - 11 documented engine pitfalls (collect entities once per run, read-only collectors, single grouped fix transaction, ACI-vs-true-color mapping, conditional geometric checks, regex compilation cache, separate doc snapshot, agent-actionable violation messages, last-report cache, baseline diffing, idempotent fixes).
- **Backend validator engine** (`src/AcadMcp.Backend/Validators/`):
  - `Rule.cs` POCOs (`Severity`, `Discipline`, `RuleScope`, `CheckSpec`, `FixSpec`).
  - `RuleLoader.cs` - YamlDotNet-based parser enforcing rule 33 §7 hard rules (id format, severity enum, description length, no blanket-fix).
  - `RuleRegistry.cs` - 3-tier discovery (embedded resources → `<repo>/validators/` → `%LOCALAPPDATA%\AcadMcp\validators\`) with later sources overriding earlier IDs.
  - `StandardLibrary.cs` - presets that bundle multiple rule IDs (`iso-cad-baseline`, `polish-arch-baseline`).
  - `CheckEvaluator.cs` - entity-level + document-level predicate dispatcher with cached compiled regex (rule 34 §6) and pass-on-null semantics for missing geometric properties (rule 34 §5).
  - `ValidationEngine.cs` - orchestrator that computes union scope, makes ONE `acad.validators.collect_entities` call per space (rule 34 §1), evaluates rules, builds `ValidationReport`.
  - `ValidationReport.cs` - structured `Violation` records with `entityHandle`, `expected`, `observed`, `fixAvailable` for agent-actionable feedback (rule 34 §8).
- **acad-validators MCP category - 10 tools** (`Backend/Categories/Validators/`):
  - `list_validators`, `explain_rule`, `list_standards`, `validate_drawing`, `validate_with_rule`, `validate_against_standard`, `list_violations`, `auto_fix_violations`, `add_validator_rule`, `reload_validator_rules`.
  - `ValidatorsRuntime` - process-singleton registry + last-report cache keyed by active doc (rule 34 §9).
  - `ValidatorsProxy` - generic IPC layer to plugin tools.
- **Plugin handlers** (`src/AcadMcp.Plugin/Tools/ValidatorsPluginTools.cs`):
  - `acad.validators.collect_entities` - read-only entity snapshot collector (model + paper space) with computed length/area/radius and block attribute extraction.
  - `acad.validators.doc_summary` - document-level metadata (layers, blocks, text styles, units, entity counts).
  - `acad.validators.apply_fixes` - SINGLE grouped transaction with full rollback on any failure (rule 34 §3).
- **Bundled rule library**: 13 YAML rules across `general` / `architectural` / `mechanical` disciplines + 2 standard presets, all embedded as resources in `AcadMcp.Backend.dll`.
- **`AcadMcp.Backend.exe --validators-self-check`** - new diagnostic CLI flag that loads every embedded + repo + user rule and standard, prints a summary, exits non-zero on any parse failure. No AutoCAD needed. Wired into pre-commit gate as check `[6/6]` (rule 40 §6).
- `bin-launchers/acad-validators.cmd` and `mcpbank-manifests/acad-validators.json` (10 tools, rich description, `phase`, `rule_library_size: 13`, `bundled_standards`, `user_rules_dir` metadata).

### Added - Phase 4 phase4_vision_sidecar (in progress)

- **Rules first** (rule 53): two new `.md` files BEFORE any code:
  - `29-acad-vision-architecture.md` - HTTP/JSON not gRPC, 127.0.0.1-only bind, idle-shutdown sidecar lifecycle, `IVisionSidecarClient` not raw `HttpClient`, no AutoCAD-plugin dependency in v1, content-hash cache keys.
  - `32-acad-vision-traps.md` - 11 documented vision/OCR/ML pitfalls (image normalisation, PDF page-by-page, OCR confidence cutoffs, per-discipline title-block templates, per-discipline YOLO weights, vision-LLM cost/latency budget, pixel coords vs drawing units, cross-validate is a string-set diff, lazy ML imports, single-engine semaphore, cache invalidation).
- **AcadMcp.Vision Python sidecar (v0.2.0)** - real FastAPI HTTP API replaces Phase 0 stub:
  - `acadmcp_vision/app.py` - FastAPI wire-up, idle-shutdown watchdog (default 300 s), pid/port discovery files under `%LOCALAPPDATA%\AcadMcp\`, hard-refuses non-loopback `--host`, per-engine `asyncio.Semaphore(1)` queues.
  - `schemas.py` - Pydantic v2 request/response shapes for `ImageRef` / OCR / detect-symbols / titleblock / dimensions / classify / describe / segment / cross-validate plus health/version envelopes.
  - `_loaders.py` - tolerant image loading (path or base64 data URL), PDF page rasterisation via `pypdfium2`, RGB-uint8 normalisation with capped long side, optional-dep probing.
  - `_cache.py` - disk JSON cache keyed by `sha256(content)+engine+version`, 7-day TTL.
  - `engines/ocr.py` - PaddleOCR (default), EasyOCR, Tesseract; canonical `OcrToken` shape; per-engine module globals.
  - `engines/yolo.py` - Ultralytics adapter with per-discipline `cad-symbols-{arch,mech,elec,pid}.pt` weights under `%LOCALAPPDATA%\AcadMcp\vision-models\`.
  - `engines/vision_llm.py` - Anthropic Claude 3.5 Sonnet + OpenAI GPT-4o adapters with auto-pick by API-key presence, JPEG q85 + 1568 px long-side cap, 5 MB payload refuse.
  - `engines/titleblock.py` - per-discipline label-alias templates (architectural-eu/us, mechanical, electrical, civil) + panel-region selection (bottom_right / right_strip / bottom_strip) + label/value spatial pairing.
  - `engines/dimensions.py` - regex parser for dimension callouts (`1234`, `12.5 mm`, `12'-6"`, EU `1.234,56`) with normalisation to millimetres.
  - HTTP endpoints: `GET /health`, `GET /version`, `POST /v1/ocr`, `/v1/detect-symbols`, `/v1/extract-titleblock`, `/v1/extract-dimensions`, `/v1/classify-drawing`, `/v1/describe-image`, `/v1/cross-validate-with-dxf`. All ML endpoints return 503 + `installHint` when their dep is missing (rule 32 trap #9).
  - Tests rewritten to verify health, optional-dep introspection, 503 envelope shape and the dep-free cross-validate endpoint (incl. numeric tolerance).
- **acad-vision MCP category - 9 tools** (Backend `Categories/Vision/`):
  - `ocr_image`, `detect_symbols`, `extract_titleblock`, `extract_dimensions`, `classify_drawing`, `describe_image`, `cross_validate_with_dxf`, `vision_health`, `vision_version`.
  - DTOs (`VisionDtos.cs`) mirror the Python schemas one-for-one (snake_case JSON via `[JsonPropertyName]`).
  - `VisionProxy` - generic `Post/GetAsync<TArgs,TResult>` over `IVisionSidecarClient`.
  - All tools require `IVisionSidecarClient` injection (NOT `IPluginGateway`); they work without AutoCAD running.
- **Backend plumbing** for the new sidecar:
  - `Backend/Sidecar/IVisionSidecarClient.cs` + `VisionSidecarClient.cs` - HTTP/1.1 client with port discovery (`ACADMCP_VISION_PORT` env, then `%LOCALAPPDATA%\AcadMcp\vision.port`, then default `50062`), strongly-typed `VisionUnavailableException` / `VisionEngineUnavailableException` / `VisionToolException`.
  - `Program.cs` registers `IVisionSidecarClient` for every non-router category.
  - `CategoryServer.BuildCallArgs` injects `IVisionSidecarClient` when a tool parameter requests it (analogous to `IPluginGateway`).
  - `CategoryServer` catches the three vision exception types and surfaces them as MCP `isError: true` results with the `installHint` text intact.
- **Lifecycle scripts**:
  - `scripts/start-vision.ps1` - idempotent sidecar launcher (`-EnsureRunning`, `-WaitHealthy`, `-Force`, `-Stop`, `-Port`); writes `vision.pid` + `vision.port` discovery files; auto-detects stale PIDs.
  - `scripts/setup-vision-models.ps1` - installs ML extras (`pip install -e .[ml]` or OCR-only) and reports YOLO weights presence per discipline.
  - `bin-launchers/acad-vision.cmd` - calls `start-vision.ps1 -EnsureRunning -WaitHealthy` then launches the .NET host bound to `--category vision`.
- **Manifest**: `mcpbank-manifests/acad-vision.json` regenerated from `[McpTool]` metadata. Hand-tuned `description` (no auto-stub), `requires_plugin=false`, new `requires_python_sidecar=true` block referencing the start/setup scripts; `metadata.phase=phase4_vision_sidecar`. Pre-commit gate passes for all 13 manifests, total **172 MCP tools across 13 categories**.

### Added - Phase 3 phase3_annotations_blocks (complete)

- **acad-files category - 11 tools** (DWG / DXF lifecycle and conversion):
  - **Documents**: `list_documents` (every open DWG, active flag, modified, read-only, entity count), `get_active_document`, `new_document` (acad.dwt template).
  - **Lifecycle**: `open_document` (`readOnly` and `password`, deduped against already-open docs by full path), `save_document` (existing path, current native DwgVersion), `save_document_as` (new path + reflection-resolved `DwgVersion` token / year alias), `close_document` (by path or active, optional save-before-close).
  - **Import**: `import_file` (`.DWG` via `WblockCloneObjects` per trap #5, optional displacement to insertion; `.DXF` via `db.DxfIn`).
  - **Export**: `export_file` for `DWG` (`SaveAs`), `DXF` (`db.DxfOut`), `PDF` / `DWF` / `DWFX` / `IMAGE` (`PNG`) via `PlotEngine` with paired `Begin*` / `End*` calls per trap #11. Supports `layout` and `scope` (`Display` / `Extents` / `Limits` / `Window` / `Layout`).
  - **Maintenance**: `purge_database` (cascading purge across `BlockTable`, `LayerTable`, `LinetypeTable`, `TextStyleTable`, `DimStyleTable`, `RegAppTable`, `UcsTable`, `ViewTable`; built-in symbols `0`, `Defpoints`, `ByLayer`, `ByBlock`, `Continuous`, `Standard`, anonymous `*` records skipped; trap #12), `audit_database` (reflects into `AuditInfo` so this compiles across verticals; default `fix=false`).
  - DTOs (`Backend/Categories/Files/FilesDtos.cs` + `Plugin/Tools/FilesDtos.cs`), proxy, plugin handlers (file-ops use a UI-thread runner WITHOUT a wrapping transaction since `Database.SaveAs`, `DxfOut`, `WblockCloneObjects` and `LayoutManager` own their own txn).
  - Manifest regenerated from `[McpTool]` metadata: 11 tools, 19 tags, 54 PL+EN intent examples, `metadata.phase=phase3_annotations_blocks`.

### Fixed - Phase 3 plugin compilation gaps

- `AcadEnv.ValidateSymbolName`: `SymbolUtilityServices` is in `Autodesk.AutoCAD.DatabaseServices`, not `Autodesk.AutoCAD.Runtime`.
- `AnnotationsPluginTools.AddMLeaderBlock`: `MLeader.BlockScale` is `Scale3d`, not `double`.
- `AnnotationsPluginTools.AddTable`: switched from deprecated `Table.SetRowHeight` / `Table.SetColumnWidth` to `Table.Rows[i].Height` / `Table.Columns[i].Width`; replaced non-existent `Table.TextStyleId` with per-cell `Cells[r,c].TextStyleId` assignment.
- `AnnotationsPluginTools.CreateTextStyle`: `FontDescriptor` constructor on this SDK uses positional args, not `typeface:` keyword.
- `LayersPluginTools`: `LayerStateMasks.All` is not defined — added explicit `AllLayerStateMasks` const ORing every documented bit (`On|Frozen|Locked|Plot|NewViewport|Color|LineType|LineWeight|PlotStyle|CurrentViewport`).
- `LayoutsPluginTools.ConfigurePlot`: `PlotSettingsValidator` lives in `Autodesk.AutoCAD.DatabaseServices`, not `PlottingServices`; switched to the `.Current` singleton (constructor private on AutoCAD 2025).
- Full solution `dotnet build src/AcadMcp.sln` now produces **0 warnings, 0 errors**.

### Added - Phase 0 bootstrap (in progress)

- Initial project skeleton: folder structure, `git init`, `.gitignore`, `README.md`, this `CHANGELOG.md`
- `NuGet.config` pinning nuget.org as the only source
- `docs/engineering-rules/` "growing rulebook":
  - **Foundation (always-apply):** `00-architecture-invariants`, `01-folder-layout`, `02-no-breaking-changes`, `03-language-and-style`, `04-build-and-test-gates`, `50-task-flow`, `51-changelog`, `52-no-yolo-changes`, `53-rules-update-mandate`
  - **Plugin invariants (10-15):** `10-acad-ui-thread`, `11-acad-transactions`, `12-acad-error-mapping`, `13-acad-units-coords`, `14-acad-no-blocking-prompts`, `15-acad-sendcommand`
  - **MCP tool authoring (20-25):** `20-mcp-tool-attribute`, `21-mcp-tool-naming`, `22-mcp-tool-args-results`, `23-mcp-tool-idempotency`, `24-mcp-tool-category-binding`, `25-mcp-tool-tests`
- `scripts/detect-autocad.ps1` - wykrywa AutoCAD, LT, wertyki, mapuje na TFM (net48 / net8.0-windows). Wykryto: AutoCAD 2025 PL, full mode, net8.0-windows
- .NET solution `src/AcadMcp.sln` z 6 projektami:
  - `AcadMcp.Shared` (net8.0 + net48) - DTOs, pipe contracts, `[McpTool]` attribute
  - `AcadMcp.SourceGen` (netstandard2.0) - Roslyn source generator with diagnostics ACAD0001..ACAD0005
  - `AcadMcp.Backend` (net8.0) - stdio MCP host parameterized by `--category` (and `--category router`)
  - `AcadMcp.Plugin` (net8.0-windows + net48) - referenced AutoCAD via HintPath to AcadInstallPath
  - `AcadMcp.ComBridge` (net8.0-windows) - COM/ROT fallback with custom MarshalCompat (replaces removed Marshal.GetActiveObject)
  - `AcadMcp.Lisp` (net8.0) - LISP script library
- `AcadMcp.Vision` Python sidecar (Phase 0 stub: FastAPI /health, /version)
- Backend MCP framework: `StdioJsonRpcHost`, `ToolRegistry`, `CategoryServer`, `RouterServer` with all 8 meta-tools (acad_status, acad_find_tools, acad_load_category, acad_recommend_categories, acad_explain_capabilities, acad_describe_drawing, acad_undo_checkpoint, acad_restore_checkpoint)
- E2E smoke test passed: `initialize` → `tools/list` (8 tools) → `tools/call(acad_recommend_categories)` returns correct PL keyword routing
- Source generator diagnostics verified end-to-end: ACAD0001 fires on missing/short `Intent`, ACAD0002 fires on bad tool names (PascalCase, > 5 words). Tested with throwaway `_Probe` category.
- MSBuild target `CheckManifestSync` wired into `AcadMcp.Backend.csproj` runs after Release build, calling `scripts/check-manifests.ps1`. Bypass with `-p:SkipManifestCheck=true`.
- MCPBank integration:
  - **Rules:** `30-mcpbank-manifest`, `31-mcpbank-discovery-hygiene` (manifest shape + discoverability quality)
  - `mcpbank-manifests/acad-router.json` - router manifest (8 meta-tools, lazy_mode=false, 12 PL+EN intent_examples)
  - `bin-launchers/acad-router.cmd` - router launcher (Release build)
  - `BankAutoRegister.cs` - regenerates `tools_summary` + `intent_examples` from `[McpTool]` metadata while preserving human-edited fields (description, tags, metadata.author, etc.)
  - `RepoRootDetector.cs` - walks up from binary to find repo root (looks for `mcpbank-manifests/` or `.git/`)
  - `Program.cs` `--regenerate-manifest` flag: dotnet AcadMcp.Backend --category <name> --regenerate-manifest writes/updates the matching `mcpbank-manifests/acad-<name>.json`
  - `scripts/register-mcps.ps1` - upserts every `acad-*.json` into the user's MCPBank registry (auto-detected from `~/.cursor/mcp.json` `mcpbank-dynamic.--registry` arg, fallback to `~/mcpbank/registry/mcpd-registry.json`). Validates required fields, supports `-DryRun`. Smoke-tested: detects acad-router as ADD with 8 tools.
  - `scripts/install-cursor-config.ps1` - inserts/updates ONLY `acad-router` in `~/.cursor/mcp.json`, leaves all other MCP servers untouched, takes timestamped backup. Smoke-tested: 30+ existing MCP servers preserved, acad-router appended cleanly.
- Pre-commit gate + category scaffolder:
  - **Rules:** `40-pre-commit-gates.md` (what the hook MUST/MUST NOT check, <60s budget), `41-new-category-flow.md` (mandatory naming map for adding any acad-* category)
  - `scripts/pre-commit.ps1` - 5-section gate (rules YAML, manifest validation, forbidden C# patterns, secret regex, CHANGELOG gate), supports `-Install` (writes `.git/hooks/pre-commit` shim), `-All` (full tree), `-FailFast`. Smoke-tested at 0.62-0.74 s on warm cache.
  - `scripts/new-category.ps1` - single source of truth for adding `acad-<name>` categories. Generates: `Categories/<Folder>/<Folder>Tools.cs` (compilable stub `[McpTool]`), `_README.md`, `mcpbank-manifests/acad-<name>.json` (with TODO placeholders), `bin-launchers/acad-<name>.cmd`, `tests/AcadMcp.Tests/Categories/<Folder>Tests.cs`. Refuses to overwrite without `-Force`. Validates kebab-case input.
  - `scripts/check-manifests.ps1` fixed: now reads `tools_summary` (not the obsolete `tools` key), and reports the actual manifest filename (kebab-case) instead of the PascalCase folder name.
  - **E2E test passed:** `new-category.ps1 -Name probe-temp` → build (green) → `--regenerate-manifest` (manifest tools_summary updated, intent_examples merged with placeholders, last_regenerated_utc stamped, all human-edited fields preserved) → cleanup → `pre-commit -All` (0 errors).

### Added - Phase 2 phase2_3d_modify (in progress)

- **acad-geometry-3d category - 15 tools**:
  - DTOs (`Geometry3dDtos.cs`, plugin-side `Geometry3dDtos.cs`): `DrawBoxArgs`, `DrawSphereArgs`, `DrawCylinderArgs`, `DrawConeArgs`, `DrawTorusArgs`, `DrawPyramidArgs`, `DrawWedgeArgs`, `ExtrudeCurveArgs`, `RevolveCurveArgs`, `PlanarSurfaceArgs`, `HandleArg3`, plus result types (`EntityResult3`, `VolumeResult`, `AreaResult3`, `CentroidResult`, `BoundingBox3Result`, `MassPropertiesResult`).
  - Backend `Geometry3dTools.cs` (15 `[McpTool]`s) + `Geometry3dProxy.cs`.
  - Plugin `Geometry3dPluginTools.cs`: full Solid3d primitive set (`CreateBox/Sphere/Frustum/Torus/Pyramid/Wedge`), `Solid3d.Extrude` (with auto-Region build for raw curves) and `Solid3d.Revolve` around arbitrary axis. `DrawPlanarSurface` builds Region(s) from curve handles. Mass-properties query goes through `Solid3d.MassProperties` (note: AutoCAD .NET API has the documented misspelling `MomentsOfIntertia` – preserved intentionally with a comment to stop future "fixes" from breaking the build). Surface-area for solids walks `Brep.Faces` via `acdbmgdbrep.dll`.
  - `AcadEnv.cs` extended: `ToPoint3d(Point3dDto)`, `ToVector3d(Vector3dDto)`, `FromPoint3d(Point3d)`.
  - Plugin `.csproj` now references `acdbmgdbrep.dll` (HintPath, Private=false) for Brep face enumeration.
  - `mcpbank-manifests/acad-geometry-3d.json` regenerated with all 15 tools, full description, snake_case tags.
  - `tests/AcadMcp.Tests/Categories/Geometry3dTests.cs` – 5-fact regression suite (catalog completeness, naming, RequiresPlugin, ReadOnly tagging on `get_*` tools, ≥5 intents per tool).

- **acad-boolean-ops category - 8 tools**:
  - `union_solids`, `subtract_solids`, `intersect_solids` (Solid3d.BooleanOperation with optional erase-tools).
  - `union_regions`, `subtract_regions`, `intersect_regions` (Region.BooleanOperation).
  - `create_region` from closed planar curves (with optional `eraseSource`).
  - `check_intersection` – read-only probe: bbox prefilter → `Curve.IntersectWith` for curves → generic `Entity.IntersectWith` for solids/regions, returns `{intersect, relation}` tag (`disjoint_bbox`, `curves_cross`, `boundaries_cross`, `bbox_overlap_no_boundary_cross`, `bbox_overlap_unverified`).
  - DTOs, proxy, plugin handlers, manifest (8 tools), test suite.

- **acad-modify category - 18 tools**:
  - **Transforms**: `move`, `rotate` (3D-axis aware), `scale` (uniform), `mirror` (3D plane via point + normal, optional `eraseSource`), `copy` (with chained `count`), `array_rectangular` (rows × cols × levels), `array_polar` (N items over total angle, optional rotate-with-path), `align` (2-point with optional uniform scale).
  - **Properties**: `set_layer` (auto-creates layer), `set_color` (RGB or ACI), `set_linetype` (must be loaded), `set_lineweight` (snaps to nearest standard ISO `LineWeight` enum), `match_properties` (layer/color/linetype/lineweight/ltscale).
  - **Lifecycle**: `erase` (soft-erase via `Entity.Erase`), `undo` and `redo` via `doc.SendStringToExecute("_U "/"_REDO ", true, false, false)`.
  - **Grouping**: `create_group` (named, selectable flag) and `ungroup` via `db.GroupDictionaryId`.
  - DTOs, proxy, plugin handlers, manifest (18 tools), test suite.

- **acad-selection category - 12 tools** (avoids interactive `Editor.SelectXxx` per rule 14 – pure scripted enumeration over ModelSpace):
  - `select_all`, `select_by_layer` (with optional frozen filter), `select_by_color` (RGB or ACI), `select_by_type` (DXF name), `select_by_handle` (validated lookup).
  - `select_window` (full-inside or crossing AABB), `select_fence` (curve-curve `IntersectWith` + bbox-vs-segment fallback), `select_polygon` (4-corners-in-poly, even-odd rule).
  - `filter_entities` – generic post-filter (layer + DXF type + color), can take an upstream handle list.
  - `save_selection_set` / `load_selection_set` – named selection sets persisted in `db.NamedObjectsDictionaryId/ACADMCP_SELECTION_SETS` as `Xrecord` strings (one comma-delimited handle list per name); load auto-prunes erased handles.
  - `count_entities` – fast count, optionally filtered by DXF type.
  - DTOs, proxy, plugin handlers, manifest (12 tools), test suite.

- `PluginEntryPoint.Initialize()` now registers all four new tool sets in addition to `BuiltinTools` and `Geometry2dPluginTools`.
- Total Phase 2 surface: **53 new MCP tools** across 4 categories (running total: **+1 router + 32 + 15 + 8 + 18 + 12 = 86 tools**).
- Pre-commit gate clean: 6 manifests validated, 63 staged C# files OK, 0 errors, 1.48 s runtime.

### Added - Phase 1 plugin pipe + backend host (in progress)

- AutoCAD `.NET` plugin (`AcadMcp.Plugin`, net8.0-windows for AutoCAD 2025+):
  - `IExtensionApplication` (`PluginEntryPoint`) - captures UI `SynchronizationContext`, registers `_echo` + `acad_status` built-ins, starts named pipe server, writes heartbeat every 30 s, exposes `ACADMCP_STATUS` and `ACADMCP_PING` AutoCAD commands.
  - `NamedPipeServer` + `PipeSession` - one session per Backend process, length-prefixed `MessageEnvelope` framing, per-request `CancellationToken`.
  - `UiThreadDispatcher` - safely marshals `Func`/`Action` to AutoCAD UI thread (mandatory per rule 10).
  - `HeartbeatFile` at `%LOCALAPPDATA%\AcadMcp\plugin.alive` for external liveness probes.
  - Lightweight rolling file logger at `%LOCALAPPDATA%\AcadMcp\logs\plugin-yyyymmdd.log` (7-day retention).
  - Conditional `IsExternalInit` polyfill (kept for future net48 multi-targeting).
  - `[CommandMethod("ACADMCP_PING")]` returns "AcadMcp pong v0.1.0", `[CommandMethod("ACADMCP_STATUS")]` prints pipe state + uptime + tool count.
- Backend ↔ Plugin pipe client:
  - `AcadMcp.Backend/Pipe/PluginPipeClient.cs` - persistent pipe connection, handshake via dedicated `TaskCompletionSource`, correlationId-based response demux, write lock, cancellation forwarding.
  - `AcadMcp.Shared/Pipe/PipeFraming.cs` - shared length-prefixed JSON envelope reader/writer, 16 MiB max payload.
  - `AcadMcp.Shared/Contracts.cs` additive: `CancelRequest`, `MessageKind`, `MessageEnvelope` (kept backward-compatible per rule 02).
- Backend stdio host + plugin gateway (`phase1_mcp_host`):
  - **Rule 18-backend-host-and-gateway.md** - mandatory `IPluginGateway` abstraction, "no direct AutoCAD calls from Backend", lazy connect, single-reconnect policy, error mapping.
  - `IPluginGateway` + `PluginGateway` (singleton, lazy-connect, ONE reconnect on dropped pipe, typed `PluginUnavailableException` / `PluginToolException`).
  - Wired into DI: registered ONLY when `--category != router` (router is plugin-free).
  - `CategoryServer.BuildCallArgs` now injects `IPluginGateway` into any tool parameter typed as such; `RequiresPlugin = true` tools without a registered gateway return MCP error -32603.
  - `tools/call` error path maps `PluginUnavailableException` and `PluginToolException` to MCP `isError: true` content with the user-facing message (no stack traces leaked).
- Operator diagnostics:
  - `AcadMcp.Backend.exe --ping-plugin` - end-to-end E2E check (handshake + `_echo` round-trip + `acad_status`); precise error message if plugin not reachable.
  - `scripts/install-plugin.ps1` - two install modes: `Bundle` (default; writes `%APPDATA%\Autodesk\ApplicationPlugins\AcadMcp.bundle` with `PackageContents.xml`, auto-loaded on AutoCAD start) and `Acaddoc` (patches `acaddoc.lsp` with NETLOAD line). Supports `-Force` and `-Uninstall`. Smoke-tested: bundle deployed with 5 files + manifest.
- E2E smoke checkpoints (validated):
  - `--ping-plugin` without AutoCAD returns the exact remediation message ("NETLOAD AcadMcp.Plugin.dll inside an open AutoCAD session").
  - Router stdio: `initialize` returns `protocolVersion 2025-06-18`, 8 meta-tools, correct `serverInfo.name="acad-router"`.
  - Category stdio: `--category geometry-2d` `tools/list` returns empty array (no catalogs yet, expected pre-Phase 1.3).
  - Full `dotnet build` of solution: 0 warnings, 0 errors; `CheckManifestSync` post-target green.

### Added - Phase 1 first real category: acad-geometry-2d (32 tools)

- **Rule 19-tool-implementation-pattern.md** - mandatory Backend↔Plugin tool split, naming map (`draw_line` ↔ `acad.geometry2d.draw_line`), forbidden patterns (no `Autodesk.AutoCAD.*` in Backend), per-call timeout defaults.
- Backend declarations (`src/AcadMcp.Backend/Categories/Geometry2d/`):
  - `Geometry2dDtos.cs` - 27 typed records (creation args, query args, modify args, results) with `JsonPropertyName` matching the wire shape.
  - `Geometry2dProxy.cs` - one-line gateway proxy `CallAsync<TArgs,TResult>(gw, "acad.geometry2d.<verb>", args, timeoutMs, ct)`.
  - `Geometry2dTools.cs` - **32 `[McpTool]` methods** in three groups:
    - **Creation (16):** `draw_line`, `draw_polyline`, `draw_circle`, `draw_arc`, `draw_ellipse`, `draw_rectangle`, `draw_polygon`, `draw_spline`, `draw_point`, `draw_donut`, `draw_xline`, `draw_ray`, `draw_text`, `draw_mtext`, `draw_hatch`, `draw_revcloud`.
    - **Queries (8, all `ReadOnly`):** `get_entity`, `list_entities_in_window`, `get_curve_length`, `get_area`, `get_bounding_box`, `get_intersections`, `get_distance_points`, `get_distance_to_entity`.
    - **Modifications (8):** `offset_curve`, `trim_curve`, `extend_curve`, `join_curves`, `explode_entity`, `fillet_corner`, `chamfer_corner`, `delete_entities`.
  - Each tool has 5 PL+EN `Intent` examples (10 in source-gen-validated min). Per-call timeouts: 5 s read-only / 15 s single-entity create / 30 s batch & trim/extend.
- Plugin implementations (`src/AcadMcp.Plugin/Tools/`):
  - `AcadEnv.cs` - `RequireActiveDocument`, `EnsureLayer`, `Persist`, `ResolveHandle`, `ToHandle`, point/bbox/color converters.
  - `AcadErrorMapper.cs` - `Autodesk.AutoCAD.Runtime.Exception` → typed `AcadErrorCode` (rule 12), never leaks stack traces.
  - `Geometry2dDtos.cs` - 27 plugin-side mirror DTOs (kept out of Shared to keep that assembly small).
  - `Geometry2dPluginTools.cs` - **all 32 handlers** wrapped in `UiThreadDispatcher.Run` + `LockDocument` + `StartTransaction` + `Commit`. Real AutoCAD .NET API: `Line`, `Polyline`, `Circle`, `Arc`, `Ellipse`, `Spline`, `Hatch`, `MText`, `DBText`, `Xline`, `Ray`, `DBPoint`. Boolean ops via `IntersectWith`. Trim implemented via `GetSplitCurves` + boundary-param sort. Fillet computed analytically (no `FilletAll`). Chamfer drawn as a `Line` between the two distance offsets.
- `PluginEntryPoint.Initialize()` registers `Geometry2dPluginTools` after `BuiltinTools`. Total registered tools after load: 34 (`_echo`, `acad_status` + 32 geometry).
- `mcpbank-manifests/acad-geometry-2d.json` - regenerated from `[McpTool]` metadata. 32 `tools_summary` entries, full `tags` set (auto-extracted), per-tool `intent_examples` merged. Description rewritten from TODO to a complete one-paragraph summary.
- `bin-launchers/acad-geometry-2d.cmd` - launcher script (`AcadMcp.Backend.exe --category geometry-2d`).
- `tests/AcadMcp.Tests/Categories/Geometry2dTests.cs` - rewritten to a 5-`[Fact]` regression suite (catalog completeness, snake_case names, RequiresPlugin flags, ReadOnly flags, 5+ intents per tool). Compiles when xunit project is added (Phase 1.4+).
- E2E smoke checkpoints (validated):
  - `tools/list` over stdio returns all 32 tools with descriptions, intent examples and `[Requires AutoCAD .NET plugin]` markers.
  - `tools/call draw_circle` without AutoCAD running: backend connects to `\\.\pipe\acadmcp` via `PluginGateway`, gets typed `PluginUnavailableException`, maps to MCP `isError: true` with the exact remediation message ("NETLOAD AcadMcp.Plugin.dll inside an open AutoCAD session"). Round-trip latency: ~5 s (the connect timeout).
  - Plugin bundle reinstalled with new DLL (5 files + `PackageContents.xml`) at `%APPDATA%\Autodesk\ApplicationPlugins\AcadMcp.bundle\`.
  - Full `dotnet build` of solution: 0 warnings, 0 errors; `CheckManifestSync` post-target green (1 code category, 2 manifests, 0 problems).

### Phase 1 (remaining)

- xunit test project wired into solution + run on every Release build.
- E2E end-to-end test with AutoCAD running (manual): NETLOAD, draw line/circle, query, delete; verify undo/redo from MCP client.
