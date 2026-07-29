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

`Hatch.AppendLoop(HatchLoopTypes.Default, boundaryFromSeed)` calls
`Editor.TraceBoundary(seedPoint, detectIslands)` under the hood, which
requires:

- A closed region around the seed point **visible on screen** (off-
  screen polylines are ignored — this is why silent failures happen in
  headless / scripted contexts where there is no viewport).
- The seed is in current-UCS coordinates; WCS leaks produce "invalid
  seed" errors on UCSs that aren't identity.

`apply_material_preset_by_point` fights both by:
1. Calling `ZoomExtents`-equivalent via a temp view before the trace.
2. Transforming seed from WCS → UCS with `Ed.CurrentUserCoordinateSystem`.

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

---

If you hit a new trap, add it here in the same form (section + minimal repro snippet) BEFORE landing the workaround in code. That's the whole point of this rule.
