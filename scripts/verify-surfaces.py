# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.2, first tranche — the acad-surfaces category.

`extrude_surface`, `revolve_surface`, `sweep_surface`, `offset_surface`,
`convert_to_surface`, `convert_to_solid`, `get_surface_info`.

A surface is a SHELL: it has area and no volume. That is the whole distinction from
acad-geometry-3d, and it is also what every check here rests on, because a surface tool that
quietly made nothing still hands back a perfectly good handle.

Arithmetic, all worked out here rather than taken from the tool:

* extruding a line of length 100 through 50   -> exactly 5000 of surface
* revolving that line about a parallel axis 200 away, a full turn -> Pappus: 2*pi*200*100
* sweeping a 40 profile along a straight 300  -> 12000
* offsetting a FLAT surface                   -> the area does not change
* converting a 100 cube to a surface          -> 6 * 100 * 100 = 60000, and the volume is gone
* converting it back                          -> the volume returns to 1000000

The last pair is the sharpest: a conversion that produced an empty shell, or an empty solid,
would return a valid handle either way, and only the numbers coming back to where they started
say the round trip was real.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "surfaces", "modify", "view")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "no tool registered" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:190]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:120]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def rel(a, b, tol=1e-6):
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def area_of(h):
    ok, r = S["surfaces"].call("get_surface_info", {"handle": h})
    return (r or {}).get("area") if ok else None


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    return (r or {}).get("volume") if ok and isinstance(r, dict) else None


def fresh_drawing():
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    if not ok or not isinstance(r, dict):
        raise SystemExit(f"cannot list documents - is AutoCAD running with the plugin loaded?\n  {r}")
    for d in (r.get("documents") or [])[:-1]:
        S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
    ok, r = S["files"].call("list_documents", {})
    left = (r or {}).get("documents") or []
    check("exactly one drawing is open, so no two sessions can be on different ones",
          len(left) == 1, f"{[d.get('name') for d in left]}")
    probe = hnd(S["geometry-2d"].call("draw_line", {"start": {"x": 0, "y": 9000},
                                                    "end": {"x": 100, "y": 9000}})[1])
    r2 = S["surfaces"].call("extrude_surface", {"handle": probe, "height": 10})
    check("the geometry-2d and surfaces sessions are on the SAME drawing",
          r2[0] is True, f"probe={probe} -> {str(r2[1])[:150]}")
    if r2[0]:
        S["geometry-2d"].call("delete_entities", {"handles": [hnd(r2[1]), probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── extrude_surface, against length x height ─────────────────────────────────
print("\n== extrude_surface: a 100 line swept 50 upward ==")
L, H = 100.0, 50.0
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 0}, "end": {"x": L, "y": 0}},
            label="a 100 line"))
r = do("surfaces", "extrude_surface", {"handle": ln, "height": H})
sheet = hnd(r)
if isinstance(r, dict):
    check(f"it measured the curve at {L:.0f}", rel(r.get("curveLength"), L), f"{r.get('curveLength')}")
    check("it made a surface, not a solid - the type says so",
          "Surface" in str(r.get("type")), f"{r.get('type')}")
check(f"PROVEN against arithmetic: length x height = {L * H:.0f} of surface",
      rel(area_of(sheet), L * H), f"{area_of(sheet)}")
# THE distinction this whole category rests on.
check("PROVEN: it has NO volume - a sheet is skin, not material, and get_volume says so",
      volume_of(sheet) in (None, 0) or (volume_of(sheet) or 0) < 1e-9,
      f"{volume_of(sheet)}")

print("\n-- a closed curve gives a tube, and the arithmetic still holds --")
circ = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 400, "y": 0}, "radius": 40},
              label="a circle of radius 40"))
r = do("surfaces", "extrude_surface", {"handle": circ, "height": H})
tube = hnd(r)
check(f"PROVEN: circumference 2*pi*40 times {H:.0f} = {2 * math.pi * 40 * H:.4f}",
      rel(area_of(tube), 2 * math.pi * 40 * H, 1e-4), f"{area_of(tube)}")
check("and a tube has no volume either - it is open at both ends",
      (volume_of(tube) or 0) < 1e-9, f"{volume_of(tube)}")

# ── revolve_surface, against Pappus ──────────────────────────────────────────
print("\n== revolve_surface: the same line spun about an axis 200 away ==")
R = 200.0
ln2 = hnd(do("geometry-2d", "draw_line", {"start": {"x": R, "y": 600}, "end": {"x": R, "y": 600 + L}},
             label="a 100 line at x=200"))
r = do("surfaces", "revolve_surface", {
    "handle": ln2, "axisStart": {"x": 0, "y": 600}, "axisEnd": {"x": 0, "y": 600 + L},
    "angleDeg": 360})
cyl = hnd(r)
PAPPUS = 2 * math.pi * R * L
check(f"PROVEN against Pappus: 2*pi*R*L = {PAPPUS:.4f} - and this is EXACT here, because a line "
      f"parallel to the axis keeps a constant distance from it",
      rel(area_of(cyl), PAPPUS, 1e-4), f"{area_of(cyl)} vs {PAPPUS}")
if isinstance(r, dict):
    check("and the tool reports the same figure it was checked against",
          rel(r.get("pappusArea"), PAPPUS, 1e-6), f"{r.get('pappusArea')}")

print("\n-- half a turn covers half as much, which is the control on the angle --")
ln3 = hnd(do("geometry-2d", "draw_line", {"start": {"x": R, "y": 1000}, "end": {"x": R, "y": 1000 + L}},
             label="another line"))
r = do("surfaces", "revolve_surface", {
    "handle": ln3, "axisStart": {"x": 0, "y": 1000}, "axisEnd": {"x": 0, "y": 1000 + L},
    "angleDeg": 180})
check(f"PROVEN: {PAPPUS / 2:.4f} - the angle really drives the sweep rather than being accepted",
      rel(area_of(hnd(r)), PAPPUS / 2, 1e-4), f"{area_of(hnd(r))}")

# ── sweep_surface ────────────────────────────────────────────────────────────
print("\n== sweep_surface: a 40 profile along a straight 300 ==")
prof = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 1400},
                                           "end": {"x": 0, "y": 1440}}, label="a 40 profile"))
path = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 1400},
                                           "end": {"x": 300, "y": 1400}}, label="a 300 path"))
r = do("surfaces", "sweep_surface", {"profileHandle": prof, "pathHandle": path})
check("PROVEN: on a straight path the area is profile x path = 12000",
      rel(area_of(hnd(r)), 40.0 * 300.0, 1e-4), f"{area_of(hnd(r))}")
do("surfaces", "sweep_surface", {"profileHandle": path, "pathHandle": path},
   label="sweeping a curve along itself is refused", expect_fail=True)

# ── offset_surface ───────────────────────────────────────────────────────────
print("\n== offset_surface: a flat sheet moved 25 aside ==")
before = area_of(sheet)
r = do("surfaces", "offset_surface", {"handle": sheet, "distance": 25.0})
off = hnd(r)
check("PROVEN: offsetting a FLAT surface is a translation, so the area is unchanged",
      rel(area_of(off), before), f"{area_of(off)} vs {before}")
check("PROVEN: and the original is left alone - offset makes a new surface, it does not move one",
      rel(area_of(sheet), before), f"{area_of(sheet)}")
do("surfaces", "offset_surface", {"handle": sheet, "distance": 0},
   label="a distance of 0 is refused", expect_fail=True)
do("surfaces", "offset_surface", {"handle": ln, "distance": 10},
   label="offsetting a line is refused, and points at offset_curve", expect_fail=True)

# ── the conversions, as a round trip ─────────────────────────────────────────
print("\n== convert_to_surface then back: the numbers must return to where they started ==")
SIDE = 100.0
box = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 0, "y": 2000, "z": 0},
                                         "corner2": {"x": SIDE, "y": 2000 + SIDE, "z": SIDE}},
             label="a 100 cube"))
v0 = volume_of(box)
check("the cube measures 1000000", rel(v0, SIDE ** 3), f"{v0}")
r = do("surfaces", "convert_to_surface", {"handle": box, "eraseSource": True})
shell = hnd(r)
if isinstance(r, dict):
    # AcDb3dSolid, not Solid3d: the tool reports the AutoCAD RXClass name, which is what a
    # caller sees everywhere else in this bank. My first expectation used the .NET type name.
    check("it reports what the source was, by its AutoCAD class name",
          r.get("wasType") == "AcDb3dSolid", str(r)[:200])
    check(f"PROVEN: the shell has the cube's surface area, 6 x 100 x 100 = {6 * SIDE * SIDE:.0f}",
          rel(r.get("area"), 6 * SIDE * SIDE, 1e-6), f"{r.get('area')}")
check("PROVEN: and the shell has no volume - the inside was thrown away",
      (volume_of(shell) or 0) < 1e-9, f"{volume_of(shell)}")

r = do("surfaces", "convert_to_solid", {"handle": shell, "eraseSource": True})
back = hnd(r)
check("PROVEN: converting back returns EXACTLY the volume we started with, 1000000 - which is "
      "the only thing that says the round trip was real rather than two valid handles",
      rel(volume_of(back), SIDE ** 3, 1e-9), f"{volume_of(back)} vs {SIDE ** 3}")

print("\n-- an open sheet encloses nothing and cannot become a solid --")
ln4 = hnd(do("geometry-2d", "draw_line", {"start": {"x": 600, "y": 2000},
                                          "end": {"x": 700, "y": 2000}}, label="a line"))
open_sheet = hnd(do("surfaces", "extrude_surface", {"handle": ln4, "height": 50},
                    label="extruded into an open sheet"))
r = do("surfaces", "convert_to_solid", {"handle": open_sheet},
       label="converting an open sheet to a solid is refused", expect_fail=True)
check("and the refusal explains it is the geometry, not the tool",
      "encloses" in str(r).lower() or "watertight" in str(r).lower(), str(r)[:280])
check("PROVEN: the sheet is untouched", rel(area_of(open_sheet), 100.0 * 50.0), f"{area_of(open_sheet)}")

# ── get_surface_info ─────────────────────────────────────────────────────────
print("\n== get_surface_info ==")
r = do("surfaces", "get_surface_info", {"handle": open_sheet})
if isinstance(r, dict):
    check("PROVEN: it names the concrete type, which decides what edits the surface accepts",
          "Extruded" in str(r.get("type")), f"{r.get('type')}")
    check("a flat extruded sheet is planar and has one face",
          r.get("isPlanar") is True and r.get("faces") == 1, str(r)[:250])
r = do("surfaces", "get_surface_info", {"handle": tube})
if isinstance(r, dict):
    check("PROVEN: a tube is NOT planar, so the flag distinguishes rather than always agreeing",
          r.get("isPlanar") is False, str(r)[:250])
do("surfaces", "get_surface_info", {"handle": ln4},
   label="a line is refused by name", expect_fail=True)

print("\n-- refusals --")
do("surfaces", "extrude_surface", {"handle": ln4},
   label="an extrude with no height is refused", expect_fail=True)
do("surfaces", "extrude_surface", {"handle": ln4, "height": 0},
   label="a height of 0 is refused", expect_fail=True)
do("surfaces", "revolve_surface", {"handle": ln4, "angleDeg": 360},
   label="a revolve with no axis is refused - a point names no axis in 3D", expect_fail=True)
# This first pointed at `box`, which the round trip above had already erased - so it "passed"
# on eWasErased rather than on the type check it was written to exercise. A refusal that fires
# for the wrong reason is not evidence about the thing being tested.
live_box = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 900, "y": 2000, "z": 0},
                                              "corner2": {"x": 1000, "y": 2100, "z": 100}},
                  label="a solid that has NOT been erased"))
r = do("surfaces", "extrude_surface", {"handle": live_box, "height": 10},
       label="extruding a solid is refused by name", expect_fail=True)
check("and the refusal names the type and points at extrude_curve, rather than saying it was erased",
      "not a curve" in str(r) and "extrude_curve" in str(r), str(r)[:300])

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "surfaces.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -260, "yMin": -60, "xMax": 800, "yMax": 2160},
                            "widthPx": 1400, "heightPx": 2400})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> in plan view the sheets read as lines, because a sheet standing on edge has no width")
print("     from above - what IS visible is the two revolved cylinders as full and half rings,")
print("     the swept sheet as a rectangle, and the cube at the top having survived the round")
print("     trip to a surface and back.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
