# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.1 tranche 2 — slice_solid and interfere_solids.

Both are checkable against arithmetic, which is why they are verified together:

* **slice_solid** — cutting CONSERVES volume. A 100x100x100 box cut at z=30 must give halves of
  300000 and 700000, and they must sum to the 1000000 that went in. A slice that lost material
  or counted the overlap twice leaves two perfectly good-looking solids and only the sum says so.

* **interfere_solids** — two boxes overlapping in a known region have an overlap volume you can
  work out on paper. Here 60x60x60 of it. The tool must also leave BOTH originals whole, which
  is its entire difference from boolean_ops.intersect_solids - so both are measured before and
  after, and the destructive tool is run afterwards on copies as a CONTROL to show it really does
  behave differently.

Volumes are read back with get_volume rather than taken from the creating tool's own report.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "boolean-ops", "view")}
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


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    if not ok or not isinstance(r, dict):
        return None
    for k in ("volume", "value"):
        if r.get(k) is not None:
            return r[k]
    return None


def box(x0, y0, z0, x1, y1, z1):
    return hnd(S["geometry-3d"].call("draw_box", {
        "corner1": {"x": x0, "y": y0, "z": z0},
        "corner2": {"x": x1, "y": y1, "z": z1}})[1])


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — see verify-textgeom.py for why."""
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
    probe = box(0, 9000, 0, 10, 9010, 10)
    check("the geometry-3d and boolean-ops sessions are on the SAME drawing",
          S["boolean-ops"].call("check_intersection",
                                {"handle1": probe, "handle2": probe})[0] is not None,
          f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── slice_solid, against conservation of volume ───────────────────────────────
print("\n== slice_solid: a 100 cube cut at z=30 ==")
SIDE = 100.0
WHOLE = SIDE ** 3
b = box(0, 0, 0, SIDE, SIDE, SIDE)
v0 = volume_of(b)
check(f"the cube measures {WHOLE:.0f}", rel(v0, WHOLE), f"{v0}")

r = do("geometry-3d", "slice_solid", {
    "handle": b, "planePoint": {"x": 0, "y": 0, "z": 30},
    "planeNormal": {"x": 0, "y": 0, "z": 1}, "keepBoth": True})
other = ((r or {}).get("otherHalf") or {}).get("handle") if isinstance(r, dict) else None
vk = volume_of(b)
vo = volume_of(other) if other else None
# The normal points +Z, so the KEPT half is the upper one: 100x100x70.
check("PROVEN against arithmetic: the kept half is the upper 70, so 700000",
      rel(vk, SIDE * SIDE * 70), f"measured {vk} vs computed {SIDE * SIDE * 70}")
check("PROVEN: and the other half is the lower 30, so 300000",
      rel(vo, SIDE * SIDE * 30), f"measured {vo} vs computed {SIDE * SIDE * 30}")
# THE check. Both halves are perfectly good solids whether or not the cut conserved anything.
check(f"PROVEN: they sum back to the {WHOLE:.0f} that went in",
      vk is not None and vo is not None and rel(vk + vo, WHOLE),
      f"{vk} + {vo} = {(vk or 0) + (vo or 0)} vs {WHOLE}")

print("\n-- without keepBoth the other half is gone --")
b2 = box(300, 0, 0, 300 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "slice_solid", {
    "handle": b2, "planePoint": {"x": 0, "y": 0, "z": 50},
    "planeNormal": {"x": 0, "y": 0, "z": 1}})
if isinstance(r, dict):
    check("no second solid came back", r.get("otherHalf") is None, str(r)[:250])
    check("and it says so rather than reporting a sum it does not have",
          r.get("volumesSum") is None, str(r)[:250])
check("PROVEN: half the cube is left, 500000", rel(volume_of(b2), SIDE * SIDE * 50),
      f"{volume_of(b2)}")

print("\n-- a slanted plane, where the halves are wedges --")
b3 = box(600, 0, 0, 600 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "slice_solid", {
    "handle": b3, "planePoint": {"x": 650, "y": 0, "z": 50},
    "planeNormal": {"x": 1, "y": 0, "z": 1}, "keepBoth": True})
o3 = ((r or {}).get("otherHalf") or {}).get("handle") if isinstance(r, dict) else None
v3a, v3b = volume_of(b3), volume_of(o3) if o3 else None
check("PROVEN: a diagonal cut still conserves volume",
      v3a is not None and v3b is not None and rel(v3a + v3b, WHOLE),
      f"{v3a} + {v3b} = {(v3a or 0) + (v3b or 0)} vs {WHOLE}")
check("and it really was cut in two, not left whole",
      v3a is not None and v3b is not None and v3a > 0 and v3b > 0, f"{v3a} / {v3b}")

print("\n-- refusals --")
b4 = box(0, 300, 0, 50, 350, 50)
do("geometry-3d", "slice_solid", {"handle": b4, "planePoint": {"x": 0, "y": 0, "z": 0}},
   label="a missing normal is refused", expect_fail=True)
r = do("geometry-3d", "slice_solid", {
    "handle": b4, "planePoint": {"x": 0, "y": 0, "z": 0},
    "planeNormal": {"x": 0, "y": 0, "z": 0}},
    label="a zero-length normal is refused", expect_fail=True)
check("and the refusal says it names no direction", "no direction" in str(r), str(r)[:250])
r = do("geometry-3d", "slice_solid", {
    "handle": b4, "planePoint": {"x": 0, "y": 0, "z": 9999},
    "planeNormal": {"x": 0, "y": 0, "z": 1}},
    label="a plane that misses the solid entirely is refused", expect_fail=True)
# It did NOT refuse on the first run: Slice leaves the solid whole and returns no second half,
# so every number came back honest and useless - 125000 before, 125000 kept, and a note claiming
# the other half was gone when there never was one.
check("and the refusal names the solid's own extents, so the plane can be placed properly",
      "does not pass through" in str(r) and "spans" in str(r), str(r)[:280])
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 500},
                                         "end": {"x": 100, "y": 500}}, label="a line"))
do("geometry-3d", "slice_solid", {"handle": ln, "planePoint": {"x": 0, "y": 0, "z": 0},
                                  "planeNormal": {"x": 0, "y": 0, "z": 1}},
   label="a line is refused by name", expect_fail=True)

# ── interfere_solids, against a hand-computed overlap ─────────────────────────
print("\n== interfere_solids: two boxes overlapping in a known 60 cube ==")
# 0..100 and 40..140 in every axis, so the common region is 40..100 = 60 on a side.
p1 = box(0, 700, 0, 100, 800, 100)
p2 = box(40, 740, 40, 140, 840, 140)
OVERLAP = 60.0 ** 3
v1, v2 = volume_of(p1), volume_of(p2)
r = do("geometry-3d", "interfere_solids", {"handle1": p1, "handle2": p2})
clash = hnd(r)
if isinstance(r, dict):
    check("it says they clash", r.get("interferes") is True, str(r)[:250])
    check("PROVEN against arithmetic: the overlap is a 60 cube, 216000",
          rel(r.get("interferenceVolume"), OVERLAP, 1e-4),
          f"reported {r.get('interferenceVolume')} vs computed {OVERLAP}")
check("MEASURED INDEPENDENTLY: the clash solid really is 216000",
      rel(volume_of(clash), OVERLAP, 1e-4), f"{volume_of(clash)}")
# THE claim that separates this from intersect_solids.
check("PROVEN: the first original is untouched", rel(volume_of(p1), v1), f"{v1} -> {volume_of(p1)}")
check("PROVEN: and so is the second", rel(volume_of(p2), v2), f"{v2} -> {volume_of(p2)}")

print("\n-- THE CONTROL: intersect_solids on an identical pair DOES consume them --")
q1 = box(0, 1000, 0, 100, 1100, 100)
q2 = box(40, 1040, 40, 140, 1140, 140)
qv1 = volume_of(q1)
do("boolean-ops", "intersect_solids", {"targetHandle": q1, "toolHandles": [q2]},
   label="intersecting the copies")
# Without this arm, "the originals survived" proves nothing - it could be that nothing in this
# drawing ever gets consumed.
check("PROVEN: the target was REPLACED by the overlap, not left whole",
      rel(volume_of(q1), OVERLAP, 1e-4) and not rel(volume_of(q1), qv1, 1e-4),
      f"was {qv1}, now {volume_of(q1)}")

print("\n-- two solids that do not touch --")
f1 = box(0, 1300, 0, 50, 1350, 50)
f2 = box(500, 1300, 0, 550, 1350, 50)
r = do("geometry-3d", "interfere_solids", {"handle1": f1, "handle2": f2})
if isinstance(r, dict):
    check("PROVEN: it reports no clash", r.get("interferes") is False, str(r)[:250])
    check("and makes no solid rather than an empty one",
          r.get("entity") is None and r.get("interferenceVolume") is None, str(r)[:250])

print("\n-- refusals --")
do("geometry-3d", "interfere_solids", {"handle1": f1, "handle2": f1},
   label="a solid against itself is refused", expect_fail=True)
do("geometry-3d", "interfere_solids", {"handle1": f1},
   label="a missing second handle is refused", expect_fail=True)
do("geometry-3d", "interfere_solids", {"handle1": ln, "handle2": f1},
   label="a line is refused by name", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "slice-interfere.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -80, "yMin": -80, "xMax": 800, "yMax": 1450},
                            "widthPx": 1400, "heightPx": 2200})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm in plan view: three cut cubes along the bottom, two overlapping pairs above")
print("     them with a smaller cube of overlap sitting inside the first pair, and at the top")
print("     two boxes far apart that do not touch.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
