# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.2, second tranche — joining, projecting, NURBS.

`blend_surfaces`, `project_to_surface`, `convert_to_nurbs`, `get_nurbs_info`,
`edit_nurbs_point`.

Arithmetic, worked out here rather than taken from the tools:

* blending two PARALLEL straight curves 100 long across a 60 gap gives a flat ruled sheet of
  exactly 100*60 = 6000
* projecting a 100 line straight down onto a FLAT horizontal surface leaves the length at 100 —
  a shadow cast square onto a plane is the same size as the thing casting it
* converting a surface to NURBS must leave the AREA alone. Re-describing a shape must not
  reshape it, and a badly approximated conversion still hands back a perfectly valid surface,
  so the area equality is the only thing that says the conversion was faithful
* moving a control point and moving it straight back must return the area to what it was —
  a round trip, which is the sharpest form of this check because both halves must be real

The last one also guards a trap: a control point that steers nothing reports a successful move.
The tool refuses when the area does not change, and this proves the refusal is not vacuous by
first showing a move that DOES change it.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
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


def first(r):
    es = (r or {}).get("entities") or []
    return es[0].get("handle") if es else None


def area_of(h):
    ok, r = S["surfaces"].call("get_surface_info", {"handle": h})
    return (r or {}).get("area") if ok else None


def lift(h, dz):
    return S["modify"].call("move", {"handles": [h], "from": {"x": 0, "y": 0, "z": 0},
                                     "to": {"x": 0, "y": 0, "z": dz}})[0]


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
    ok2, _ = S["surfaces"].call("extrude_surface", {"handle": probe, "height": 10})
    check("the geometry-2d, surfaces and modify sessions are on the SAME drawing",
          ok2 and S["modify"].call("set_layer", {"handles": [probe], "layer": "0"})[0],
          f"probe={probe}")


print("== fresh drawing ==")
fresh_drawing()

# ── blend_surfaces, against the ruled-sheet area ─────────────────────────────
print("\n== blend_surfaces: two parallel 100 lines across a 60 gap ==")
L, GAP = 100.0, 60.0
a1 = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 0}, "end": {"x": L, "y": 0}},
            label="the first edge"))
a2 = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": GAP}, "end": {"x": L, "y": GAP}},
            label="the second edge, 60 away"))
r = do("surfaces", "blend_surfaces", {"handle1": a1, "handle2": a2})
blend = hnd(r)
if isinstance(r, dict):
    check("it measured both edges at 100",
          rel(r.get("length1"), L) and rel(r.get("length2"), L),
          f"{r.get('length1')} / {r.get('length2')}")
check(f"PROVEN against arithmetic: a flat ruled sheet between them is {L * GAP:.0f}",
      rel(area_of(blend), L * GAP, 1e-4), f"{area_of(blend)} vs {L * GAP}")
do("surfaces", "blend_surfaces", {"handle1": a1, "handle2": a1},
   label="blending an edge to itself is refused", expect_fail=True)

# ── project_to_surface ───────────────────────────────────────────────────────
print("\n== project_to_surface: a 100 line dropped onto a flat sheet below it ==")
# A horizontal sheet: extrude a line along Y to get a flat surface lying in the XY plane.
base_ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 400}, "end": {"x": 300, "y": 400}},
                 label="a line for the ground"))
ground = hnd(do("surfaces", "extrude_surface",
                {"handle": base_ln, "height": 300, "direction": {"x": 0, "y": 1, "z": 0}},
                label="extruded sideways into a horizontal sheet"))
check("the ground sheet measures 300 x 300", rel(area_of(ground), 300.0 * 300.0, 1e-4),
      f"{area_of(ground)}")

shadow_src = hnd(do("geometry-2d", "draw_line", {"start": {"x": 50, "y": 500},
                                                 "end": {"x": 150, "y": 500}},
                    label="a 100 line above it"))
lift(shadow_src, 200)
r = do("surfaces", "project_to_surface", {"handle": shadow_src, "surfaceHandle": ground,
                                          "direction": {"x": 0, "y": 0, "z": -1}})
if isinstance(r, dict):
    check("PROVEN: a shadow cast SQUARE onto a flat surface is the same size as the thing "
          "casting it - 100 in, 100 out",
          rel(r.get("projectedLength"), L, 1e-6) and rel(r.get("sourceLength"), L, 1e-6),
          f"source {r.get('sourceLength')} -> projected {r.get('projectedLength')}")

print("\n-- a projection that misses the surface is refused, not reported as a success --")
away = hnd(do("geometry-2d", "draw_line", {"start": {"x": 2000, "y": 500},
                                           "end": {"x": 2100, "y": 500}},
              label="a line well off to one side"))
lift(away, 200)
r = do("surfaces", "project_to_surface", {"handle": away, "surfaceHandle": ground,
                                          "direction": {"x": 0, "y": 0, "z": -1}},
       label="projecting past the edge of the surface is refused", expect_fail=True)
# CORRECTED. The tool claimed AutoCAD answers a missed projection with an empty result; it
# does not - it throws GeneralModelingFailure. That claim was invented rather than observed,
# and this check is what caught it. The description now says what was measured.
check("and the refusal names the likely cause rather than only the error status",
      "MISSES the surface" in str(r) and "GeneralModelingFailure" in str(r), str(r)[:300])

# ── convert_to_nurbs: the shape must survive being re-described ──────────────
print("\n== convert_to_nurbs: re-describing must not reshape ==")
flat_ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 1000}, "end": {"x": 200, "y": 1000}},
                 label="a 200 line"))
sheet = hnd(do("surfaces", "extrude_surface",
               {"handle": flat_ln, "height": 150, "direction": {"x": 0, "y": 1, "z": 0}},
               label="extruded into a 200 x 150 sheet"))
area0 = area_of(sheet)
check("the sheet measures 30000", rel(area0, 200.0 * 150.0, 1e-4), f"{area0}")

r = do("surfaces", "convert_to_nurbs", {"handle": sheet, "eraseSource": True})
nurb = first(r)
if isinstance(r, dict):
    check("it reports what the source was", "Extruded" in str(r.get("wasType")), str(r)[:200])
# THE check: a badly approximated conversion returns a perfectly valid NURBS surface.
check("PROVEN: the area is unchanged by the conversion - the only thing that says the shape "
      "survived being re-described",
      rel(area_of(nurb), area0, 1e-6), f"{area_of(nurb)} vs {area0}")

# ── the control cage, and a round trip through it ───────────────────────────
print("\n== get_nurbs_info and edit_nurbs_point ==")
r = do("surfaces", "get_nurbs_info", {"handle": nurb})
cu = cv = None
if isinstance(r, dict):
    cu, cv = r.get("controlPointsU"), r.get("controlPointsV")
    check("it reports a cage with points in both directions",
          (cu or 0) >= 2 and (cv or 0) >= 2, f"{cu} x {cv}")
    check("and lists exactly that many points",
          len(r.get("controlPoints") or []) == (cu or 0) * (cv or 0),
          f"{len(r.get('controlPoints') or [])} for {cu}x{cv}")

MOVE = 80.0
r = do("surfaces", "edit_nurbs_point", {"handle": nurb, "u": 0, "v": 0,
                                        "by": {"x": 0, "y": 0, "z": MOVE}})
area1 = area_of(nurb)
if isinstance(r, dict):
    check("PROVEN: the point moved exactly as far as it was told to",
          rel(r.get("moved"), MOVE, 1e-6), f"{r.get('moved')}")
    check("PROVEN: and the surface changed shape - a cage point that steered nothing would be "
          "reported by AutoCAD as a successful move",
          abs((r.get("areaChange") or 0)) > 1e-6, f"{r.get('areaChange')}")
check("PROVEN: the surface is BIGGER now, because pulling a corner out of plane stretches it",
      area1 is not None and area0 is not None and area1 > area0, f"{area0} -> {area1}")
# The surface is pulled TOWARDS the point, not through it: the area grows by far less than
# moving a corner 80 would suggest if the surface followed it exactly.
check("and by less than moving a corner 80 would give if the surface followed it exactly - the "
      "cage steers, it does not place",
      area1 - area0 < 200.0 * MOVE, f"grew by {area1 - area0}, a followed corner would add up to "
                                    f"{200.0 * MOVE}")

print("\n-- move it straight back: the area must return to where it started --")
r = do("surfaces", "edit_nurbs_point", {"handle": nurb, "u": 0, "v": 0,
                                        "by": {"x": 0, "y": 0, "z": -MOVE}})
check("PROVEN: the round trip returns the area to exactly what it was - both halves of it were "
      "real, which neither half alone can show",
      rel(area_of(nurb), area0, 1e-6), f"{area_of(nurb)} vs {area0}")

print("\n-- refusals --")
do("surfaces", "edit_nurbs_point", {"handle": nurb, "u": 0, "v": 0},
   label="an edit with neither to nor by is refused", expect_fail=True)
do("surfaces", "edit_nurbs_point", {"handle": nurb, "u": 0, "v": 0,
                                    "to": {"x": 0, "y": 0, "z": 0}, "by": {"x": 0, "y": 0, "z": 1}},
   label="giving both to and by is refused", expect_fail=True)
r = do("surfaces", "edit_nurbs_point", {"handle": nurb, "u": 99, "v": 0,
                                        "by": {"x": 0, "y": 0, "z": 10}},
       label="an index outside the cage is refused", expect_fail=True)
check("and the refusal reports the cage size and the valid range",
      "outside the cage" in str(r) and "runs 0 to" in str(r), str(r)[:280])
do("surfaces", "get_nurbs_info", {"handle": ground},
   label="asking a non-NURBS surface for its cage is refused, and points at convert_to_nurbs",
   expect_fail=True)
do("surfaces", "convert_to_nurbs", {"handle": flat_ln},
   label="converting a line is refused by name", expect_fail=True)
do("surfaces", "blend_surfaces", {"handle1": a1, "handle2": ground},
   label="blending to a surface rather than a curve is refused by name", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "surfaces-nurbs.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 400, "yMax": 1220},
                            "widthPx": 1200, "heightPx": 2600})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> in plan view: the blend at the bottom as a 100 x 60 rectangle of isolines, the ground")
print("     sheet above it as a 300 square with the projected shadow line lying on it, and the")
print("     NURBS sheet at the top - back to a plain rectangle after the control point was")
print("     moved out and back again.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
