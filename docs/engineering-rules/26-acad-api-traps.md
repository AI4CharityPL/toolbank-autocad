# AutoCAD .NET API traps – read before touching Solid3d / Brep / lineweights

Known landmines in the AutoCAD .NET API. Read BEFORE touching Solid3d, Region, Brep, PlaneSurface, MassProperties, lineweights, layer/plot props.

Discovered while building Phase 1 + Phase 2. Each item below cost build cycles to find. **DO NOT "FIX" these — they are the actual public surface.**

## 1. `Solid3dMassProperties` has a documented misspelling

The struct returned by `Solid3d.MassProperties` exposes its principal-moments and inertia tensor under typo'd names that AutoCAD has shipped for ~20 years and cannot break:

| field      | property             |
| ---------- | -------------------- |
| `volume`   | `Volume`             |
| `centroid` | `Centroid`           |
| `momInertia` | **`MomentsOfIntertia`** (sic – Inertia → Intertia) |
| `prodInertia` | **`ProductsOfIntertia`** (same typo) |
| `radiiGyration` | `RadiiOfGyration` (correct, no typo) |
| `prinMoments` | `PrincipalMoments`  |
| `PrinAxes` | `PrincipalAxes`      |
| `extents`  | `Extents`            |

**Always include a comment when using these so the next agent doesn't "correct" them.**

```csharp
// NOTE: AutoCAD .NET API has a typo: 'MomentsOfIntertia' (sic). Don't 'fix' it - it is the actual public name.
var moi = new[] { mp.MomentsOfIntertia.X, mp.MomentsOfIntertia.Y, mp.MomentsOfIntertia.Z };
```

## 2. Brep API lives in a separate assembly

`Autodesk.AutoCAD.BoundaryRepresentation.Brep` (used for face enumeration and surface-area sums on `Solid3d`/`Surface`) is in **`acdbmgdbrep.dll`**, not `acdbmgd.dll`. Add it to `AcadMcp.Plugin.csproj` exactly like the others (`HintPath` + `Private=false` + `ExcludeAssets=runtime`):

```xml
<Reference Include="acdbmgdbrep">
  <HintPath>$(AcadInstallPath)acdbmgdbrep.dll</HintPath>
  <Private>false</Private>
  <ExcludeAssets>runtime</ExcludeAssets>
</Reference>
```

Always alias it (`using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;`) – the unaliased name collides with `Database.BoundaryRep`-style helpers in some samples.

## 3. `PlaneSurface.CreateFromCurves` does NOT exist

There is no static factory on `PlaneSurface`. To convert closed planar curves to a 2D bounded entity, use `Region.CreateFromCurves(DBObjectCollection)` and persist the resulting `Region` (which IS the canonical "planar surface" in AutoCAD's data model). If you genuinely need a `PlaneSurface` instance, build a `Region` first, then call the **instance** method `new PlaneSurface().CreateFromRegion(region)`.

## 4. `Solid3d.Revolve` not `RevolveAroundAxis`

The 3D-revolve method on `Solid3d` is **`Revolve(Region, Point3d axisPoint, Vector3d axisDir, double angleRad)`** — there is no `RevolveAroundAxis`. `RevolveOptions` overloads exist for surfaces only.

## 5. `Curve.Closed` and `Spline.Closed` are read-only on some curves

You cannot do `spline.Closed = true;`. Build the closed spline by re-adding the first fit point as the last point (the curve is then geometrically closed and `IsClosed` becomes `true`).

## 6. Lineweights are an enum, not millimeters

`Entity.LineWeight` takes the `LineWeight` enum (`LineWeight000`..`LineWeight211` representing 0.00–2.11 mm). Convert user-supplied millimeters to the nearest standard value — **do not** cast a `double` to `LineWeight` and hope. See `ModifyPluginTools.NearestLineweight`.

## 7. `Solid3d.CreateBox/Cylinder/Cone/...` create the primitive at the **origin**

All `Solid3d.CreateXxx` factory methods build the primitive **centered on the WCS origin**, with the axis on Z. To place it at a user-supplied point you MUST follow up with `solid.TransformBy(Matrix3d.Displacement(centerOfPrimitive - Point3d.Origin))`. The "center of primitive" depends on the shape:

| primitive | center for translation |
| --------- | ---------------------- |
| Box / Wedge / Sphere / Torus | midpoint of corner1/corner2 (or the user-supplied center) |
| Cylinder / Cone / Pyramid    | `basePoint + (0, 0, height/2)` (Z+ axis convention) |

## 8. `BlockTableRecord` enumeration semantics

`foreach (ObjectId id in modelSpace)` only yields graphical entities; iterating includes erased ids — always check `id.IsErased` and skip them, otherwise downstream `IntersectWith`/`TransformBy` calls throw `eWasErased`.

## 9. Don't use interactive `Editor.Get*` from MCP tools

Selection helpers (`SelectWindow`, `SelectFence`, …) on the AutoCAD `Editor` open a modal command-line prompt that **blocks the AutoCAD UI thread** and is invisible to the MCP agent. The selection category enumerates ModelSpace + applies geometric predicates instead. See rule 14.

## 10. `BooleanOperation` consumes the tool entity

`Solid3d.BooleanOperation(BoolUnite, toolSolid)` and `Region.BooleanOperation(...)` mutate `target` and leave `toolSolid` empty/erased depending on the AutoCAD version. After the call, the safe pattern is:

```csharp
target.BooleanOperation(BooleanOperationType.BoolUnite, toolSolid);
if (eraseTools && !toolSolid.IsErased) toolSolid.Erase(true);
```

Never reference `toolSolid` after the boolean call without a fresh `IsErased` check.

## 11. `Hatch` entity — associativity + boundary + pattern pitfalls

The `Hatch` entity (used by `acad-hatches`, rule 62) is the single most
booby-trapped object in the AutoCAD .NET API. Every one of these
sub-traps has cost at least one build cycle.

### 11a. Boundary orientation MUST be CCW for the outer loop

`Hatch.AppendLoop(HatchLoopTypes.Outermost, ids)` silently "succeeds"
on a clockwise outer loop but renders **empty** (or flips + renders the
whole drawing as hatched). Always validate polyline direction first:

```csharp
// Compute signed area; if < 0 the polyline is CW and must be reversed.
double signedArea = ComputeSignedArea(pl);
if (signedArea < 0) pl.ReverseCurve();
```

Inner (island) loops MUST be **opposite** orientation to the outer
loop. If you have mixed-orientation islands the hatch fill flips on
whichever loop is "wrong" and shows a chequerboard.

### 11b. Associativity requires the boundary entities to stay alive

`Hatch.Associative = true` binds the hatch to the `ObjectId`s you
passed to `AppendLoop`. If the caller erases the boundary polyline
after the hatch is created, AutoCAD does NOT erase the hatch — it
leaves a **ghost hatch** with a stale `ObjectId` list. On the next
`REGEN` the hatch disappears silently. Two safe options:

1. Non-associative hatch (`Associative = false`) — the only safe
   default for `apply_material_preset_by_point` where we synthesise a
   temporary boundary from a seed point and discard the temp polyline.
2. Associative + caller keeps the boundary alive — used by
   `apply_material_preset` when the caller passes a real wall/room
   polyline they intend to keep.

Never flip associativity after-the-fact; re-create the hatch instead.

### 11c. Pattern file resolution (`.pat`) is per-profile

`HatchPatternType.PreDefined` only finds the built-in 68-pattern list
that ships with AutoCAD. Anything else (e.g. `AR-HBONE`, `AR-PARQ1`,
`AR-RROOF`) is in **`acad.pat` / `acadiso.pat`** which lives in the
current user's Support File Search Path. Two consequences:

- If the user has a stripped profile (no `Support` folder on the path)
  `SetHatchPattern` silently falls back to `SOLID` and the hatch
  becomes an all-black polygon. Always call
  `HostApplicationServices.Current.FindFile("acadiso.pat", db, FindFileHint.Default)`
  and bail with a clear error if null.
- Custom patterns (`.pat` in repo assets) need
  `SetHatchPattern(HatchPatternType.CustomDefined, "MY-PATTERN")` NOT
  `PreDefined`. If you ship a hospital-only pattern, drop the file into
  `assets/hatch-patterns/` and prepend that folder to
  `HostApplicationServices.Current.SupportPath` at plugin startup.

### 11d. Seed point must be INSIDE a closed boundary on the current UCS plane

`Editor.TraceBoundary(seedPoint, detectIslands)` **reads its seed in the
current UCS.** Every argument in this codebase is WCS unless a `ucs`
argument says otherwise (rule 43), so the seed must be transformed with
`Ed.CurrentUserCoordinateSystem.Inverse()` before the call. That matrix
maps UCS → WCS, so its inverse is the direction you want.

When the current UCS is world the two agree, which is why a missing
transform passes every casual test and then fails on a real drawing.

**Reproduced, 2026-08-04.** Rectangle (50000,50000)–(56000,54000), seed
(53000,52000) plainly inside it, current UCS origin (1000,2000). The
seed was read as WCS (54000,54000) — exactly on the top edge — so
TraceBoundary correctly found no enclosing region, and the tool blamed
the caller's geometry. `ucs.set_ucs_world` made the identical call
succeed.

**There are two independent conditions, and both are real.** The seed
must be in the current UCS, *and* the region must be visible in the
current view. Measured by varying them separately:

| | view away from region | view framing the region |
|---|---|---|
| **UCS = world** | fails | works |
| **UCS offset (1000,2000)** | fails | works¹ |

¹ only once the seed is transformed. Before that fix, framing the region
made no difference at all — which is how the two conditions hid each
other for so long, and how a first pass at this concluded, wrongly, that
visibility did not matter.

So `TraceBoundaryAsHandles` now does both: transform the seed, frame the
drawing extents through a `ViewTableRecord` (never the command layer —
that is what made `zoom_extents` itself fail with `eInvalidInput`), trace,
then **put the caller's view back**. An agent calling a hatch tool did
not ask for its view to move.

**Correction to what this rule used to say.** It previously stated that
`apply_material_preset_by_point` already did both of these. Neither was
in the code. The rule described the intent as if it were the state.

That is the lesson worth more than the trap: **a rule describing a
mitigation the code does not implement is worse than no rule**, because
it sends the next person to look somewhere else. This entry is why
`draw_hatch_by_boundary` sat on the broken-on-valid-input list for
months — every reader, including me, believed the mitigations were there.
When a rule claims the code handles something, the rule is a claim to be
checked, not evidence.

### 11e. Hatch scale in mm is literal — no drawing-unit auto-scaling

`Hatch.PatternScale = 50` means "scale pattern 50x". In a millimetre
drawing the preset table (rule 62 §2) pre-multiplies scale so visible
spacing is ~2-5 mm. In a metre drawing the same `50` value produces an
entirely invisible hatch. The tool does NOT auto-detect drawing units
— the caller MUST ensure `files.set_units('mm')` before calling hatch
tools. See rule 13.

### 11f. Clip (`HatchObjectFormGraph`) is NOT exposed via `Hatch.Clip*`

There is no public `Hatch.ClipBoundary` setter. To "clip" an existing
hatch to a smaller polygon you must:

1. Read the hatch's pattern + scale + angle + colour + layer.
2. Erase the old hatch.
3. Create a new hatch with the new boundary + same parameters.

`acad.hatches.clip_hatch` does exactly this — do NOT try to mutate
`Hatch.GetLoopAt(0)` boundaries in-place, the API accepts the mutation
but AutoCAD silently regenerates the ORIGINAL boundary on the next
REGEN.

### 11g. `HatchStyle.Outer / Ignore / Normal` = island detection, not clipping

A common mistake: setting `HatchStyle.Ignore` to "clip" an island.
`HatchStyle` controls **island detection** (how nested loops are
rendered), not the outer boundary. `Outer` = only outermost loop is
hatched, `Ignore` = everything outside outer is hatched (inverse),
`Normal` = alternating fill. If you genuinely need to clip, see 11f.

### 12. `new Brep(entity)` gives sub-objects that cannot be addressed

`Brep` has two constructors and they are not interchangeable. Built from an
`Entity`, its faces and edges enumerate fine and can be counted — but ask any of
them for `SubentityPath` and you get `MissingSubentity`:

```csharp
using var brep = new Brep(solid);          // enumerates happily
foreach (Edge e in brep.Edges)
    var id = e.SubentityPath.SubentId;     // throws: MissingSubentity
```

Since **every** SOLIDEDIT operation in the managed API takes `SubentityId[]` —
`FilletEdges`, `ChamferEdges`, `ExtrudeFaces`, `TaperFaces`, `OffsetFaces`,
`RemoveFaces`, `TransformFaces`, `ShellBody` — the entity constructor is useless
for anything except counting. Root the Brep on the solid's `ObjectId` instead:

```csharp
var root = new SubentityId(SubentityType.Null, IntPtr.Zero);
using var brep = new Brep(new FullSubentityPath(new[] { solid.ObjectId }, root));
```

`imprint_edges` never hit this because it only counts faces and edges. The
failure surfaces as a bare `Autodesk.AutoCAD.BoundaryRepresentation.Exception`
with an empty message, which names neither the step nor the status — wrap each
step of a Brep walk in a labelled helper that rethrows with the label and
`ex.ErrorStatus` and six candidate causes become one deploy instead of six.

### 12a. An oversized `FilletEdges` / `ChamferEdges` destroys faces and returns success

AutoCAD does **not** refuse a radius too large for the geometry. Measured on a
pristine 100 cube: `FilletEdges` with radius 300 on a 100-wide face was accepted,
swallowed a whole face — six faces down to five — and left a volume a third
smaller, with no error of any kind. `ChamferEdges` does the same at distance 100
exactly; above about 120 it does refuse, with `eGeneralModelingFailure` (20062).

Compare the face count before and after and treat a **drop** as a failure. The
handler runs inside `RunWriteAsync`'s transaction, so throwing skips
`tr.Commit()` and the aborted transaction restores the solid — but assert that in
a live check rather than assuming it. The related arithmetic identity
`L·r²·(1 − π/4)` for the removed volume only holds while the fillet fits inside
both faces meeting at the edge.

### 12b. `ShellSolid` does not exist; `ShellBody` does

A probe that asked the compiler for `Solid3d.ShellSolid` came back CS1061 and
`shell_solid` was struck from the roadmap as unbuildable. The method is
`ShellBody(SubentityId[], double)` and it is there. When a probe reports absent,
check the name against the SDK before striking the row — a struck row is a
decision never to revisit something, so a typo in a probe is expensive in a way
a compile error is not. Confirmed genuinely absent in the same sweep:
`Solid3d.Separate`, `Solid3d.CopyFaces`, `Body.ConvertFrom`, `SubDMesh.CreateBox`.

### 12c. Reading the compiler-probe wrong, in both directions

The compiler is the only reliable oracle for this SDK, but its answers have to be read carefully,
and this has now gone wrong twice in opposite ways.

**A CS1503 naming a type does not mean that is the only overload.** Probing
`new Profile3d("x")` gives *"cannot convert from string to Profile3d"*, which reads as a
copy-constructor-only type — and six surface constructors want a `Profile3d`, so that reading
would have struck all six. But `new Profile3d(entity)` compiles perfectly well. With several
overloads the compiler reports against whichever one it picked as closest. **Probe with a
plausible argument type, not an implausible one.**

**A CS1061 means absent only if the name is right.** `Solid3d.ShellSolid` came back CS1061 and
`shell_solid` was struck from the roadmap as unbuildable. The method is `ShellBody`. A struck row
is a decision never to look again, so a typo in a probe is expensive in a way a compile error
never is.

**A CS1501 about a method name says nothing about which TYPE owns it.** Probing `srf.Trim(a, b, c)`
answers *"no overload of Trim takes 3 arguments"*, which reads as `Surface.Trim` existing with a
different arity. It does not exist at all: the compiler was resolving against
`MemoryExtensions.Trim`, the string extension, which is in scope in every file. Asking with one
argument produced CS1929 and named the extension method outright. Extension methods on
`ReadOnlySpan<char>` will happily answer for any name they share.

**A CS1061 for six names at once still means only that those six names are wrong.** Probing
`SubDMesh` for `SubDivisionLevel`, `IncreaseSubDivisionLevel`, `DecreaseSubDivisionLevel`,
`Refine`, `Unrefine` and `Splitface` returned CS1061 on every one, which reads as "mesh smoothing
and face editing are not exposed at all" — a conclusion strong enough to strike half a phase. The
real names are `SmoothLevel`, `SplitFace` and `MergeFaces`, and all of them work. Six absences in
a row is not corroboration; it is one guess about naming, made six times.

**Practical rule, and the reason it is worth the round trip.** Four separate times in one session
a single probe round nearly struck buildable tools:

| what was probed | what was concluded | what was true |
|---|---|---|
| `Solid3d.ShellSolid` | shell_solid unbuildable | it is `ShellBody` |
| `new Profile3d("x")` | copy-constructor only, six surface tools dead | `new Profile3d(entity)` compiles |
| `srf.Trim(a,b,c)` | exists with another arity | `Surface.Trim` does not exist; that was `MemoryExtensions.Trim` |
| `SubDMesh.SubDivisionLevel` et al. | mesh smoothing not exposed | it is `SmoothLevel` |

So: when a probe says a capability is absent, spend one more round confirming it before striking
anything. When it says a type is unconstructible, try the constructor you would actually want.
When it reports an arity mismatch, check the error names the type you asked about — one argument
is usually enough to force the compiler to say. A struck row is a decision never to look again,
and it costs one build to avoid making that decision on a typo.

### 13. `SubDMesh` sub-entities cannot be addressed, and the failure is a SUCCESS

`ExtrudeFaces`, `SplitFace` and `MergeFaces` all exist on `SubDMesh` and all want a
`FullSubentityPath[]`. There is no way to obtain one: the `GetSubentityPathsAt*` family that
`Solid3d` carries — and which is exactly what made the solid face-and-edge tools reachable — is
absent here. Building a path by hand does not work either:

```csharp
var sid  = new SubentityId(SubentityType.Face, (IntPtr)(faceIndex + 1));
var path = new FullSubentityPath(new[] { mesh.ObjectId }, sid);
mesh.ExtrudeFaces(new[] { path }, 50.0, Vector3d.ZAxis, 0.0);
// no exception, no error - and the cage is still 8 vertices and 6 faces
```

AutoCAD **reports success** and changes nothing. Any tool built on this must compare the vertex
and face counts before and after and refuse when neither moved, because the return value will
not tell you. `acad-mesh` ships without those three for this reason.

### 13a. Backends restarted mid-session bind to a document that may be replaced

Each category runs in its own `AcadMcp.Backend.exe`, and each binds to a document. Kill them —
which every `deploy-plugin.ps1 -Kill` does — and the ones that come back can be bound to a
drawing that a later `new_document` replaces. The symptom is *"Handle 'XX' not found"* from one
category for a handle another category has just minted, and the giveaway is handles that keep
climbing across consecutive `new_document` calls: a session minting 7F, 82, 84 over three new
drawings never saw any of them.

This is not fixable from the verification side — a warm-up call was tried and does nothing. The
remedy is a **clean AutoCAD start before each verification run**, which is why every live check
in this project is preceded by one. Put the cross-session probe at the top of every script so
this shows up as a named failure rather than as arithmetic that mysteriously will not add up.

### 14. `new Section(pts, v)` — that vector is `verticalDir`, NOT the normal

```csharp
sec = new Section(pts, normal.GetNormal());   // WRONG - and it compiles, runs, and reports success
sec = new Section(pts, up.GetNormal());       // right: which way is UP in the section
```

The cut plane is the one **containing the section line and that vector**, so the normal falls out
as `line × up` and cannot be supplied at all — `Section.Normal` is read-only. Pass the intended
normal (a horizontal vector, for a plan line) and the plane becomes the one containing the plan
line *and* a horizontal direction: the **XY plane**. Every "vertical" section is then taken
horizontally at z=0, and nothing complains.

**Both overloads take `Vector3d`, so the compiler cannot tell you which is which by type — but it
will by NAME.** Named arguments turn it into an oracle: `verticalDir:` compiles, `normal:` and
`verticalDirection:` are CS1739. The same round proved `Normal` read-only (CS0200) and
`VerticalDirection` settable after construction. When several parameters share a type, probe the
names.

**What made this survive a green verification run is worth more than the trap itself: the test
shape could not tell a cut from a silhouette.** A plane through the middle of a 100 cube and a
plane 5000 units away both answered 400, because a cube's cross-section and its outline are the
same square — and the script called 400 proof. Two controls fix it, and both belong in any
section or projection test:

* a **sphere cut off-centre** — r=50 cut at y=30 gives `2π·40 = 251.327`, against `314.159` for
  the great circle. One number says the plane was used, the other says it was ignored.
* a **box with three different edge lengths** — 100×80×60 gives `2(100+60) = 320` upright and
  `2(100+80) = 360` flat, so the two orientations cannot be confused with each other either.

Then assert the post-condition in the tool: the normal it reports back must be perpendicular to
**both** the section line and the up vector. That is what catches the plane silently being
somewhere else, and it is now in `create_section_plane`.

### 15. Editor.Command, Application.Invoke and LoadModule need a COMMAND context

```csharp
ed.Command("_.CIRCLE", "0,0", "10");                  // eInvalidInput from a tool handler
AcadApp.Invoke(new ResultBuffer(...));                // eInvalidInput, every expression
SystemObjects.DynamicLinker.LoadModule(path, ...);    // InvalidOperationException
```

All three compile, all three are the documented API, and all three fail from where this plugin
dispatches: the **application** context, on the UI thread inside a document lock and an open
transaction. They require a **command** context. The failure is a bare `eInvalidInput` naming
nothing, so it reads like a bad argument and invites an afternoon of trying different arguments.

**One root cause behind six unrelated-looking failures.** `eval_lisp`, `load_lisp_file`,
`list_loaded_lisp`, `run_command_sequence`, `run_script_file` and `netload_assembly` were built,
deployed and all failed identically; three different LISP formulations were tried
(`Application.Invoke` with `(read)`+`(eval)`, splicing the parsed form, and the command line
wrapped to write its value to a file) and every one produced the same error. When several tools
that share no code fail with the same status, stop varying the arguments and suspect the CONTEXT.

**`ExecuteInCommandContextAsync` was then BUILT and MEASURED, and it does not fix this on its
own.** A runner was written around it withholding all three suspects — no `UiThreadDispatcher`, no
`LockDocument`, no transaction spanning the call. Every tool still answered `eInvalidInput` and the
run **hung AutoCAD**, so it was reverted.

**The isolating experiment, and it should have been the FIRST thing done.** A plain
`[CommandMethod]` in this same plugin (`ACADMCP_CMDTEST`, kept in
`Tools/CommandContextProbe.cs`) separates the plugin from the dispatch path — the one variable
none of the earlier attempts moved. Run from a genuine command context
(`IsApplicationContext=False`):

| probe | result |
| --- | --- |
| `Editor.Command("_.CIRCLE", "0,0", "10")` | **OK**, 0 → 1 entity |
| the same **inside an open transaction** | **OK** |
| `Editor.Command("(setq x (+ 1 2))")` — a LISP expression | FAIL `eInvalidInput` |
| `Application.Invoke((getvar "CLAYER"))` | FAIL `eInvalidInput` |
| the same inside a transaction | FAIL `eInvalidInput` |

Three conclusions, two of which overturn what had been assumed for two whole attempts:

1. **The transaction was never the problem.** `Editor.Command` works perfectly well inside one.
   Both earlier attempts treated it as a prime suspect and were wrong.
2. **`Editor.Command` does not take LISP.** It tokenises COMMAND input; a parenthesised expression
   is invalid to it. The `(progn (vl-load-com) (setq %tbf (open ...)))` wrapper built for
   `eval_lisp` was never going to work — and feeding it very probably left AutoCAD waiting for
   input, which is the hang that had to be killed. The hang was self-inflicted, not an
   `ExecuteInCommandContextAsync` defect.
3. **`Application.Invoke` is unusable from this plugin**, in a command context or out of one, with
   a transaction or without. Treat it as absent.

So the six split rather than standing or falling together:

* `run_command_sequence` and `run_script_file` need only a COMMAND context and real command
  tokens — `Editor.Command` demonstrably works there.
* `netload_assembly` has a command route that was never tried: `_.NETLOAD` with `FILEDIA` set to
  0 so it takes the path on the command line instead of opening a file dialog.
* `eval_lisp`, `load_lisp_file` and `list_loaded_lisp` need LISP evaluation, and neither surviving
  route provides it. The remaining candidate is `SendStringToExecute` with the expression wrapped
  to write its value to a FILE, then waiting for that file — which keeps the honesty rule, because
  the evidence becomes the file's contents rather than the fact that something was queued.

**The lesson is about method, not about AutoCAD.** Two attempts were spent varying arguments and
dispatch strategy against a category of eleven tools. One `[CommandMethod]` and five lines answered
it. When several tools fail identically, shrink the experiment until exactly one variable moves —
and do it before building, not after.

`SendStringToExecute` remains what it was: it queues, so a tool that merely reports "sent" is
still forbidden. Waiting on a file it writes is a different thing and is allowed.

**The boundary is NARROWER than "Editor methods need a command context", measured 2026-08-11.**
`selection.select_last` calls `Editor.SelectLast()` from the ordinary application-context runner
and it WORKS — it returned a selection set with one entity. So the rule is not about the `Editor`
class: `Editor.Command` needs a command context, while the non-interactive selection methods do
not. Do not generalise from one failing member to its whole type.

**What did work, and why it is worth noting:** `Application.GetSystemVariable`/`SetSystemVariable`
and the ordinary `Database`/`Transaction` API are unaffected. The dividing line is not "old API vs
new" but whether the call needs to run as if a user had typed it.

### 16. `Database.GeoDataObject` THROWS when there is no geographic location

```csharp
if (db.GeoDataObject.IsNull) { ... }        // never runs - the GETTER throws eNullObjectId
```

It does not return a null `ObjectId`; it raises `eNullObjectId`. So the obvious guard cannot
execute, and every tool in the category fails with an error about a null object id instead of
saying what is actually wrong — including the tool whose whole job is to CREATE the location, if
it checks for an existing one first. Asking whether a drawing is geolocated means **catching, not
testing**:

```csharp
ObjectId id;
try { id = db.GeoDataObject; } catch (AcadRt.Exception) { return null; }
```

Two more measured facts about the same object: **`GeoLocationData` is the class**, not `GeoData`,
though `GeoDataObject` is the property that points at it; **`NorthDirection` is read-only and is
a `double` — an ANGLE, not a vector**; and **`CoordinateSystem` cannot be set before `PostToDb`**,
raising `eNoDatabase` if you try.

### 17. A non-nullable `double` in a DTO turns an OMITTED argument into a valid one

```csharp
public sealed record GeoSetArgs(
    [property: JsonPropertyName("latitude")] double Latitude,   // omitted -> 0.0
    ...
```

`0` is a perfectly good latitude and longitude — the Gulf of Guinea. So a caller who forgets the
latitude does not get an error: the tool sets a location nobody asked for and reports success, and
a plugin-side `is null` check never fires because the backend already filled the gap.

**What makes this worth its own section is how it presented.** The offending call sat before a
block of conversion checks, and silently moved the drawing's location to the equator. Six checks
then failed, five of them looking exactly like conversion bugs — wrong latitude, wrong direction,
markers in the wrong place. The single check that pointed at the cause, *"a location with no
latitude is refused"*, was one line in that list and the least dramatic of them.

**Make every argument that has a plausible zero nullable**, and refuse the null. A missing value
must not be indistinguishable from a valid one. Coordinates, angles, indices and scales are all in
this class; a count or a tolerance, where 0 is already invalid, is not.

### 18. Some properties do not stick until the object is IN the database

Two independent instances, one session apart:

| property | symptom |
| --- | --- |
| `GeoLocationData.CoordinateSystem` | throws `eNoDatabase` if set before `PostToDb` — loud |
| `Light.HasTarget` | silently reads back `false` if set before the entity is appended — quiet |

The second is the dangerous one. `Position`, `Intensity`, `LightColor` and the cone angles all
survive being set on an unappended `Light`, so the object looks fine; only `HasTarget` reverts, and
a spot light that is not aimed at anything renders as though it were switched off. Nothing in the
result says so.

**The rule: append or `PostToDb` FIRST, then set anything relational** — a target, a coordinate
system, an association with another object. Plain scalar fields can go either way, which is
precisely why the failure is hard to spot: most of the object works.

**And assert it.** The check that caught this was a one-liner that looked redundant next to the
cone arithmetic — *"a spot light DOES have a target"*. It was the only thing between the bank and
a spot light that silently pointed nowhere.

---

If you hit a new trap, add it here in the same form (section + minimal repro snippet) BEFORE landing the workaround in code. That's the whole point of this rule.
