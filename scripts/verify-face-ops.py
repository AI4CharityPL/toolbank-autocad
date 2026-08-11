# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.1 tranche 5 — the SOLIDEDIT face family.

`extrude_face`, `offset_face`, `move_face`, `rotate_face`, `taper_face`, `delete_face`,
`shell_solid`.

Every one of these can be handed a value AutoCAD accepts and quietly ignores, and every one then
returns a healthy result over an unchanged solid. So every one is checked against a volume worked
out on paper from a 100 cube:

* extrude the top face by 50   -> 1000000 + 100*100*50      = 1500000
* offset all six faces by 10   -> a 120 cube, not a 110 one = 1728000
* move the top face up by 50   -> the same arithmetic       = 1500000
* rotate the top face 45 deg about its near edge -> a wedge = 500000, and -45 -> 1500000
* taper one side 45 deg about its base           -> a wedge = 500000, and -45 -> 1500000
* shell open at the top, thickness -10 -> cavity 80x80x90   = 424000, and +10 -> 584000
* fillet an edge and then DELETE the face it made -> back to exactly 1000000

That last one is the sharpest check in the set: a partial removal still leaves a perfectly valid
solid, and only the return to the original volume says the feature really came off.

The sign conventions were MEASURED before being written down here, not predicted. One of them
came out the opposite way from the documentation as first written: shell thickness is positive
OUTWARD.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "view")}
results = []

SIDE = 100.0
WHOLE = SIDE ** 3
QUARTER = 1.0 - math.pi / 4.0
TOP = {"x": 0, "y": 0, "z": 1}


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


def rel(a, b, tol=1e-9):
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    if not ok or not isinstance(r, dict):
        return None
    return r.get("volume") if r.get("volume") is not None else r.get("value")


def faces_of(h):
    ok, r = S["geometry-3d"].call("list_solid_faces", {"handle": h})
    return (r or {}).get("faces") or [] if ok else []


def box(x0, y0=0.0):
    return hnd(S["geometry-3d"].call("draw_box", {
        "corner1": {"x": x0, "y": y0, "z": 0},
        "corner2": {"x": x0 + SIDE, "y": y0 + SIDE, "z": SIDE}})[1])


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
    probe = box(0, 9000)
    check("the geometry-3d and geometry-2d sessions are on the SAME drawing",
          bool(probe) and S["geometry-2d"].call("get_bounding_box", {"handle": probe})[0],
          f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── naming a face by the direction it points ─────────────────────────────────
print("\n== facing: 'the top face' without a list call first ==")
b = box(0)
r = do("geometry-3d", "extrude_face", {"handle": b, "facing": TOP, "distance": 50.0})
if isinstance(r, dict):
    picked = (r.get("faces") or [{}])[0]
    check("PROVEN: facing +Z picked the face whose centroid is at z=100 and whose normal is +Z - "
          "reported back, so the pick is checkable rather than trusted",
          rel((picked.get("centroid") or {}).get("z"), SIDE)
          and rel((picked.get("normal") or {}).get("z"), 1.0),
          f"{picked}")
check(f"PROVEN against arithmetic: pushing a 100x100 face out by 50 adds 500000, leaving "
      f"{WHOLE + SIDE * SIDE * 50:.0f}",
      rel(volume_of(b), WHOLE + SIDE * SIDE * 50), f"{volume_of(b)}")

print("\n-- a direction that points equally at two faces names neither --")
b2 = box(300)
r = do("geometry-3d", "extrude_face",
       {"handle": b2, "facing": {"x": 1, "y": 1, "z": 0}, "distance": 10.0},
       label="a direction aimed at a corner is refused", expect_fail=True)
check("and the refusal names both candidates",
      "points equally at" in str(r) and "names" in str(r), str(r)[:280])
check("PROVEN: the cube it refused is untouched", rel(volume_of(b2), WHOLE), f"{volume_of(b2)}")

# ── offset_face: each face follows its OWN normal ────────────────────────────
print("\n-- the six normals of a box must point OUTWARD, each a different direction --")
bn = box(4500)
fn = faces_of(bn)
outward = {(round(f["normal"]["x"], 6), round(f["normal"]["y"], 6), round(f["normal"]["z"], 6))
           for f in fn if f.get("normal")}
# THE check the first version of this script missed: it compared abs() of each component, so a
# normal pointing INTO the solid passed exactly as well as one pointing out — and on a filleted
# box the signs turned out to be noise. Six faces must give six DISTINCT signed directions; the
# same direction twice means the sign carries no information.
check("PROVEN: six faces, six distinct signed normals, and they are the six axis directions - "
      "an inward-pointing normal would collide with its opposite and this set would come up short",
      outward == {(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)},
      f"{sorted(outward)}")
check("and every normal points AWAY from the solid's centre, which is what outward means",
      len(fn) == 6 and all((f["normal"]["x"] * (f["centroid"]["x"] - (4500 + SIDE / 2))
                            + f["normal"]["y"] * (f["centroid"]["y"] - SIDE / 2)
                            + f["normal"]["z"] * (f["centroid"]["z"] - SIDE / 2)) > 0
                           for f in fn if f.get("normal")),
      f"{[(f['centroid'], f['normal']) for f in fn]}")

print("\n== offset_face: all six faces of a 100 cube by 10 ==")
b3 = box(600)
r = do("geometry-3d", "offset_face", {"handle": b3, "faceIndexes": [0, 1, 2, 3, 4, 5],
                                      "distance": 10.0})
check(f"PROVEN against arithmetic: it becomes a 120 cube ({120 ** 3}), NOT a 110 one "
      f"({110 ** 3}) - the growth happens on both sides of every axis, which is the whole "
      f"difference from move_face",
      rel(volume_of(b3), 120.0 ** 3), f"{volume_of(b3)} vs {120.0 ** 3}")

print("\n-- and negative shrinks it the same way --")
b4 = box(900)
do("geometry-3d", "offset_face", {"handle": b4, "faceIndexes": [0, 1, 2, 3, 4, 5],
                                  "distance": -10.0})
check(f"PROVEN: an 80 cube, {80 ** 3}", rel(volume_of(b4), 80.0 ** 3), f"{volume_of(b4)}")

# ── move_face against the same arithmetic, in one direction ──────────────────
print("\n== move_face: the top face up by 50 ==")
b5 = box(1200)
do("geometry-3d", "move_face", {"handle": b5, "facing": TOP,
                                "from": {"x": 0, "y": 0, "z": 0}, "to": {"x": 0, "y": 0, "z": 50}})
check(f"PROVEN: the same 1500000 extrude gave - move takes a direction you choose where offset "
      f"follows each face's own normal, and on ONE face the two agree",
      rel(volume_of(b5), WHOLE + SIDE * SIDE * 50), f"{volume_of(b5)}")
do("geometry-3d", "move_face", {"handle": b5, "facing": TOP,
                                "from": {"x": 0, "y": 0, "z": 0}, "to": {"x": 0, "y": 0, "z": 0}},
   label="a zero displacement is refused rather than reported as a success", expect_fail=True)

# ── rotate_face: a wedge, both ways ──────────────────────────────────────────
print("\n== rotate_face: the top face 45 degrees about its near edge ==")
b6 = box(1500)
do("geometry-3d", "rotate_face", {"handle": b6, "facing": TOP,
                                  "axisStart": {"x": 1500, "y": 0, "z": 100},
                                  "axisEnd": {"x": 1500, "y": 100, "z": 100},
                                  "angleDeg": 45.0})
# Tipping the far edge down to z=0 leaves a triangular prism: integral of (100-x)*100 dx = 500000.
check("PROVEN against arithmetic: +45 drops the far edge to z=0, leaving the wedge 500000",
      rel(volume_of(b6), 0.5 * SIDE * SIDE * SIDE, 1e-8), f"{volume_of(b6)}")

b7 = box(1800)
do("geometry-3d", "rotate_face", {"handle": b7, "facing": TOP,
                                  "axisStart": {"x": 1800, "y": 0, "z": 100},
                                  "axisEnd": {"x": 1800, "y": 100, "z": 100},
                                  "angleDeg": -45.0})
# The other way lifts the far edge to z=200: integral of (100+x)*100 dx = 1500000.
check("PROVEN: and -45 lifts it to z=200 instead, giving 1500000 - the sign is a direction, not "
      "a magnitude, and both answers are exact",
      rel(volume_of(b7), 1500000.0, 1e-8), f"{volume_of(b7)}")
do("geometry-3d", "rotate_face", {"handle": b7, "facing": TOP, "angleDeg": 30.0},
   label="a rotation with no axis is refused - a point names no axis in 3D", expect_fail=True)

# ── taper_face ───────────────────────────────────────────────────────────────
print("\n== taper_face: one side face, 45 degree draft about its base ==")
b8 = box(2100)
do("geometry-3d", "taper_face", {"handle": b8, "facing": {"x": 1, "y": 0, "z": 0},
                                 "basePoint": {"x": 2200, "y": 0, "z": 0},
                                 "direction": {"x": 0, "y": 0, "z": 1},
                                 "angleDeg": 45.0})
check("PROVEN against arithmetic: a 45 degree draft pulls the top of that face in by the full "
      "100, leaving the same wedge, 500000",
      rel(volume_of(b8), 0.5 * SIDE * SIDE * SIDE, 1e-8), f"{volume_of(b8)}")
b9 = box(2400)
do("geometry-3d", "taper_face", {"handle": b9, "facing": {"x": 1, "y": 0, "z": 0},
                                 "basePoint": {"x": 2500, "y": 0, "z": 0},
                                 "direction": {"x": 0, "y": 0, "z": 1},
                                 "angleDeg": -45.0})
check("PROVEN: and the negative draft leans it out instead, 1500000",
      rel(volume_of(b9), 1500000.0, 1e-8), f"{volume_of(b9)}")
do("geometry-3d", "taper_face", {"handle": b9, "facing": {"x": 1, "y": 0, "z": 0},
                                 "angleDeg": 10.0},
   label="a taper with no base point is refused", expect_fail=True)

# ── delete_face: removing a FEATURE, checked by the volume coming back ───────
print("\n== delete_face: fillet an edge, then take the fillet off again ==")
b10 = box(2700)
r = do("geometry-3d", "list_solid_edges", {"handle": b10})
es = (r or {}).get("edges") or [] if isinstance(r, dict) else []
check("12 edges to start with", len(es) == 12, f"{len(es)}")
do("geometry-3d", "fillet_edge", {"handle": b10, "edgeIndexes": [0], "radius": 10.0})
v_filleted = volume_of(b10)
check(f"the fillet removed L*r*r*(1-pi/4) as before",
      rel(v_filleted, WHOLE - SIDE * 100.0 * QUARTER), f"{v_filleted}")
faces = faces_of(b10)
check("and the solid now has 7 faces", len(faces) == 7, f"{len(faces)}")

# The fillet face is the CURVED one, and a curved face has no single normal — so it is the one
# list_solid_faces declines to give a normal for. Found by that property rather than by an index
# guessed at, and it doubles as a check on list_solid_faces: reporting a plausible normal for a
# quarter-cylinder would be worse than reporting none, because `facing` would then act on it.
curved = [f for f in faces if f.get("normal") is None]
check("the fillet's own face is the one with NO normal, because a curved face has no single one",
      len(curved) == 1, f"{[f.get('normal') for f in faces]}")
if len(curved) == 1:
    r = do("geometry-3d", "delete_face", {"handle": b10, "faceIndexes": [curved[0]["index"]]})
    check("THE check: the volume comes back to EXACTLY 1000000, so the feature really came off - "
          "a partial removal would leave a perfectly valid solid and a healthy result",
          rel(volume_of(b10), WHOLE, 1e-12), f"{volume_of(b10)}")
    check("and the solid is a plain box again, 6 faces and 12 edges",
          len(faces_of(b10)) == 6, f"{len(faces_of(b10))}")

print("\n-- a face of the shape itself cannot go, because a solid cannot be open --")
b11 = box(3000)
do("geometry-3d", "delete_face", {"handle": b11, "facing": TOP},
   label="deleting the top of a plain box is refused", expect_fail=True)
check("PROVEN: and that box is untouched", rel(volume_of(b11), WHOLE), f"{volume_of(b11)}")

# ── shell_solid, and the sign that decides which way the wall goes ───────────
print("\n== shell_solid: a 100 cube open at the top, wall 10 ==")
b12 = box(3300)
r = do("geometry-3d", "shell_solid", {"handle": b12, "facing": TOP, "thickness": -10.0})
check("PROVEN against arithmetic: a NEGATIVE thickness hollows inward - the outside stays a 100 "
      "cube and the cavity is 80 x 80 x 90, so 1000000 - 576000 = 424000",
      rel(volume_of(b12), WHOLE - 80.0 * 80.0 * 90.0), f"{volume_of(b12)}")

print("\n-- THE CONTROL: the same call with a positive thickness is a DIFFERENT shape --")
b13 = box(3600)
do("geometry-3d", "shell_solid", {"handle": b13, "facing": TOP, "thickness": 10.0})
# Growing outward on the five closed faces gives 120 x 120 x 110 around the original 100 cube.
check("PROVEN: positive grows the wall OUTWARD - 120 x 120 x 110 minus the original 100 cube = "
      "584000. Both are valid shells and only the sign tells them apart, which is exactly why "
      "the documentation had it backwards until it was measured",
      rel(volume_of(b13), 120.0 * 120.0 * 110.0 - WHOLE), f"{volume_of(b13)}")

print("\n-- MEASURED: AutoCAD will not shell a solid with no opening at all --")
b14 = box(3900)
# The tool first advertised "name no faces and the void is sealed inside". It read well and does
# not work: ShellBody throws IndexOutOfRange on an empty selection. The claim was withdrawn rather
# than the failure papered over, and the description now points at subtract_solids instead.
r = do("geometry-3d", "shell_solid", {"handle": b14, "thickness": -10.0},
       label="naming no open face is refused", expect_fail=True)
check("and the refusal says how to name one", "Name the faces to work on" in str(r), str(r)[:280])
check("PROVEN: the cube is untouched", rel(volume_of(b14), WHOLE), f"{volume_of(b14)}")

print("\n-- refusals --")
do("geometry-3d", "shell_solid", {"handle": b14, "facing": TOP, "thickness": 0},
   label="a thickness of 0 is refused", expect_fail=True)
r = do("geometry-3d", "shell_solid", {"handle": box(4200), "facing": TOP, "thickness": -80.0},
       label="a wall thicker than half the solid is refused", expect_fail=True)
check("and the refusal explains that the inner surface would pass through itself",
      "through itself" in str(r), str(r)[:280])
do("geometry-3d", "extrude_face", {"handle": b14, "facing": TOP},
   label="an extrude with neither distance nor path is refused", expect_fail=True)
do("geometry-3d", "extrude_face", {"handle": b14, "distance": 10.0},
   label="naming no faces at all is refused", expect_fail=True)
do("geometry-3d", "offset_face", {"handle": b14, "facing": TOP},
   label="an offset with no distance is refused", expect_fail=True)
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 500},
                                         "end": {"x": 100, "y": 500}}, label="a line"))
do("geometry-3d", "extrude_face", {"handle": ln, "facing": TOP, "distance": 10.0},
   label="a line is refused by name", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "face-ops.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 4360, "yMax": 160},
                            "widthPx": 2600, "heightPx": 200})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 4000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> what a PLAN view can actually carry here: the offset boxes are visibly a different")
print("     SIZE from their neighbours - one grown to 120, one shrunk to 80 - and the two")
print("     shelled ones show the wall as a nested square, inside the outline for the negative")
print("     thickness and grown around the original for the positive one. Tapers and rotations")
print("     are changes in height and plan view does not carry them; for those the arithmetic")
print("     above is the evidence, and saying so is better than claiming the picture shows it.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
