# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.3, second tranche — creasing and two more primitives.

`set_mesh_crease`, `create_mesh_cylinder`, `create_mesh_wedge`.

Arithmetic, worked out here rather than taken from the tools:

* a mesh wedge is exactly HALF the box on the same two corners: 500000 for a 100 cube
* a mesh cylinder is a PRISM, not a circle. Eight flat sides inscribed in radius 50 give a base
  area of (8/2)*50*50*sin(2*pi/8) = 7071.0678, so a 100-tall one holds 707106.78 — noticeably
  LESS than the pi*r*r*h = 785398.16 of a true cylinder, because the flat sides cut the corners
  off the circle. Sixteen sides closes most of that gap, which is the control.
* creasing is the sharpest check in the tranche. Smooth a box to level 2 and it rounds down to
  about a third of its volume; smooth it and THEN crease every edge, and it comes back to
  EXACTLY 1000000. A crease that did nothing would leave the rounded figure, and a crease tool
  that silently reset the smoothing would give the same 1000000 for the wrong reason - so the
  rounded case is run first, as the control that tells those two apart.

  That control earned its keep on the first run: creasing BEFORE smoothing gave
  349547.26189088693 against an uncreased 349547.2618908878 - identical to ten significant
  figures, so the crease had done nothing whatever. Changing the smoothness rebuilds the mesh
  through SetSubDMesh, which carries no crease data and silently discards it. Both orders are
  asserted below: the one that works, and the trap.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "mesh", "view")}
results = []
SIDE = 100.0


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


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    return (r or {}).get("volume") if ok and isinstance(r, dict) else None


def solid_volume_of_mesh(mesh_handle):
    """Convert a copy-free mesh to a solid and measure it — the only way to measure a mesh."""
    return volume_of(hnd(do("mesh", "convert_mesh_to_solid", {"handle": mesh_handle},
                            label="converted to measure it")))


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
    probe = hnd(S["mesh"].call("create_mesh_box", {"corner1": {"x": 0, "y": 9000, "z": 0},
                                                   "corner2": {"x": 10, "y": 9010, "z": 10}})[1])
    check("the mesh and geometry-3d sessions are on the SAME drawing",
          bool(probe) and S["geometry-2d"].call("get_bounding_box", {"handle": probe})[0],
          f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── create_mesh_wedge: exactly half the box ─────────────────────────────────
print("\n== create_mesh_wedge: half the box on the same corners ==")
r = do("mesh", "create_mesh_wedge", {"corner1": {"x": 0, "y": 0, "z": 0},
                                     "corner2": {"x": SIDE, "y": SIDE, "z": SIDE}})
wedge = hnd(r)
if isinstance(r, dict):
    check("PROVEN: the cage is 6 vertices and 5 faces - two triangles and three quads, so it "
          "mixes face sizes where a box does not",
          r.get("vertices") == 6 and r.get("faces") == 5, str(r)[:250])
    check(f"and it reports the half-box figure, {0.5 * SIDE ** 3:.0f}",
          rel(r.get("halfBoxVolume"), 0.5 * SIDE ** 3), f"{r.get('halfBoxVolume')}")
check(f"PROVEN against arithmetic: converted, it measures exactly half the box, "
      f"{0.5 * SIDE ** 3:.0f}",
      rel(solid_volume_of_mesh(wedge), 0.5 * SIDE ** 3, 1e-9), "")

# ── create_mesh_cylinder: a prism, and honest about it ──────────────────────
print("\n== create_mesh_cylinder: a PRISM of 8 sides, not a circle ==")
R, H, N = 50.0, 100.0, 8
PRISM = 0.5 * N * R * R * math.sin(2 * math.pi / N) * H
CIRCLE = math.pi * R * R * H
r = do("mesh", "create_mesh_cylinder", {"basePoint": {"x": 300, "y": 0, "z": 0},
                                        "radius": R, "height": H, "sides": N})
cyl = hnd(r)
if isinstance(r, dict):
    check(f"PROVEN: the cage is 2n vertices and n+2 faces - {2 * N} and {N + 2} - two caps and "
          f"a wall of quads",
          r.get("vertices") == 2 * N and r.get("faces") == N + 2, str(r)[:250])
    check(f"and it reports BOTH figures: the prism it really is, {PRISM:.4f}, and the circle it "
          f"is not, {CIRCLE:.4f}",
          rel(r.get("prismVolume"), PRISM) and rel(r.get("circleVolume"), CIRCLE), str(r)[:300])
v_cyl = solid_volume_of_mesh(cyl)
check(f"PROVEN against arithmetic: it measures the PRISM figure {PRISM:.4f}, not the circle "
      f"figure - the flat sides cut the corners off the circle and the tool says so rather than "
      f"letting the caller assume otherwise",
      rel(v_cyl, PRISM, 1e-6), f"{v_cyl}")
check("and that really is less than a true cylinder would hold",
      v_cyl is not None and v_cyl < CIRCLE, f"{v_cyl} vs {CIRCLE}")

print("\n-- THE CONTROL: more sides closes the gap, which is what says the shortfall is the "
      "faceting rather than a bug --")
N2 = 32
PRISM2 = 0.5 * N2 * R * R * math.sin(2 * math.pi / N2) * H
cyl2 = hnd(do("mesh", "create_mesh_cylinder", {"basePoint": {"x": 600, "y": 0, "z": 0},
                                               "radius": R, "height": H, "sides": N2}))
v_cyl2 = solid_volume_of_mesh(cyl2)
check(f"PROVEN: 32 sides gives {PRISM2:.4f}, much closer to the circle's {CIRCLE:.4f} than 8 "
      f"sides was - the shortfall shrinks as the faceting does",
      rel(v_cyl2, PRISM2, 1e-6) and v_cyl2 > v_cyl and v_cyl2 < CIRCLE,
      f"8 sides {v_cyl}, 32 sides {v_cyl2}, circle {CIRCLE}")

# ── set_mesh_crease: the sharpest check in the tranche ──────────────────────
print("\n== set_mesh_crease: a creased box stays a box however much you smooth it ==")
print("-- first the CONTROL: smoothed and NOT creased, it rounds right down --")
plain = hnd(do("mesh", "create_mesh_box", {"corner1": {"x": 900, "y": 0, "z": 0},
                                           "corner2": {"x": 900 + SIDE, "y": SIDE, "z": SIDE}},
               label="a plain box mesh"))
do("mesh", "set_mesh_smoothness", {"handle": plain, "level": 2})
v_round = solid_volume_of_mesh(plain)
check("PROVEN: smoothed to level 2 and left uncreased, it is far below the sharp figure",
      v_round is not None and v_round < 0.6 * SIDE ** 3,
      f"{v_round} against a sharp {SIDE ** 3}")

print("\n-- now creased, smoothed the SAME amount, in the ORDER THAT WORKS --")
# MEASURED, and it cost a failing run to find: the order matters, and it is the opposite of the
# obvious one. Creasing and THEN smoothing loses the crease entirely - 349547, identical to the
# uncreased control to ten significant figures - because changing the smoothness rebuilds the
# mesh through SetSubDMesh, which carries no crease data. Smooth first, crease second.
creased = hnd(do("mesh", "create_mesh_box", {"corner1": {"x": 1200, "y": 0, "z": 0},
                                             "corner2": {"x": 1200 + SIDE, "y": SIDE, "z": SIDE}},
                 label="a second box mesh"))
do("mesh", "set_mesh_smoothness", {"handle": creased, "level": 2})
r = do("mesh", "set_mesh_crease", {"handle": creased, "level": -1})
if isinstance(r, dict):
    check("it creased every edge, and says so rather than implying a selection",
          r.get("allEdges") is True and r.get("creaseLevel") == -1, str(r)[:250])
v_creased = solid_volume_of_mesh(creased)
# THE check. Without the control above, 1000000 here could equally mean the crease worked or the
# smoothing was silently discarded; the rounded figure is what tells those two apart.
check(f"PROVEN against arithmetic: creased and smoothed to the SAME level 2, it comes back to "
      f"exactly {SIDE ** 3:.0f} - the crease held every edge against the smoothing",
      rel(v_creased, SIDE ** 3, 1e-9), f"{v_creased}")
check("and that is meaningful only because the uncreased control rounded down - otherwise this "
      "figure would equally fit a crease tool that quietly threw the smoothing away",
      v_round is not None and v_creased is not None and v_creased > v_round * 1.5,
      f"creased {v_creased}, uncreased {v_round}")

print("\n-- THE ORDER TRAP, asserted so it cannot quietly change --")
wrong_order = hnd(do("mesh", "create_mesh_box", {"corner1": {"x": 1500, "y": 0, "z": 0},
                                                 "corner2": {"x": 1500 + SIDE, "y": SIDE, "z": SIDE}},
                     label="a third box mesh"))
do("mesh", "set_mesh_crease", {"handle": wrong_order, "level": -1})
do("mesh", "set_mesh_smoothness", {"handle": wrong_order, "level": 2})
v_wrong = solid_volume_of_mesh(wrong_order)
check("PROVEN: creasing BEFORE smoothing loses the crease completely - the same rounded volume as "
      "no crease at all, because changing smoothness rebuilds the mesh and SetSubDMesh carries no "
      "crease data. Both tool descriptions say so; this is what makes that claim checkable",
      v_wrong is not None and v_round is not None and rel(v_wrong, v_round, 1e-9),
      f"crease-then-smooth {v_wrong}, no crease at all {v_round}")

print("\n-- level 0 takes the crease off again --")
r = do("mesh", "set_mesh_crease", {"handle": creased, "level": 0})
v_uncreased = solid_volume_of_mesh(creased)
check("PROVEN: uncreased, the same mesh at the same smoothness rounds down again - so the crease "
      "is doing the work, and it is reversible",
      v_uncreased is not None and v_uncreased < 0.6 * SIDE ** 3, f"{v_uncreased}")

print("\n-- refusals --")
do("mesh", "set_mesh_crease", {"handle": creased},
   label="a crease with no level is refused", expect_fail=True)
do("mesh", "set_mesh_crease", {"handle": creased, "level": -5},
   label="a level below -1 is refused", expect_fail=True)
do("mesh", "create_mesh_cylinder", {"basePoint": {"x": 0, "y": 0, "z": 0},
                                    "radius": 50, "height": 100, "sides": 2},
   label="a cylinder of 2 sides is refused - that is not a prism", expect_fail=True)
do("mesh", "create_mesh_cylinder", {"basePoint": {"x": 0, "y": 0, "z": 0},
                                    "radius": 0, "height": 100},
   label="a radius of 0 is refused", expect_fail=True)
do("mesh", "create_mesh_wedge", {"corner1": {"x": 0, "y": 0, "z": 0},
                                 "corner2": {"x": 100, "y": 100, "z": 0}},
   label="a wedge with a zero side is refused", expect_fail=True)
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 500}, "end": {"x": 100, "y": 500}},
            label="a line"))
do("mesh", "set_mesh_crease", {"handle": ln, "level": -1},
   label="a line is refused by name", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "mesh-crease.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 1360, "yMax": 160},
                            "widthPx": 2600, "heightPx": 420})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 4000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> in plan view, left to right: the wedge as a square, the 8-sided cylinder as a visible")
print("     OCTAGON rather than a circle - which is the whole point of the prism arithmetic - the")
print("     32-sided one looking round, then the smoothed box as a ball and the creased one still")
print("     square. That last pair side by side is the crease doing its work.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
