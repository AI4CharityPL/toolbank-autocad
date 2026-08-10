# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.3, first tranche — the acad-mesh category.

`create_mesh_box`, `get_mesh_info`, `set_mesh_smoothness`, `convert_mesh_to_solid`,
`convert_mesh_to_surface`.

A mesh carries no volume, no surface area and no watertight flag — SubDMesh exposes none of them.
What it does carry EXACTLY is its vertex and face counts, so that is the arithmetic here:

* a box mesh is 8 vertices and 6 faces. Not about eight; eight — and MEASURED, it stays 8 and 6
  at every smooth level, because AutoCAD reports the CAGE rather than the subdivided surface. A
  first version of these tools asserted 6, 24, 96, 384 and fired on a perfectly good mesh.
* what smoothing changes is the SHAPE, so it is measured on the shape: subdividing pulls the
  corners in and the mesh shrinks inside its own cage
* converting the unsmoothed box gives a solid of exactly side cubed — which is the only way to
  measure a mesh at all, and therefore the proof that the cage was built and WOUND correctly. A
  face wound the wrong way looks perfectly normal on screen and encloses nothing.
* smoothing rounds the corners off, so the smoothed mesh converts to a SMALLER volume — and
  coming back down to level 0 restores the original exactly, because the cage was kept

That last pair is the sharpest: a smoothing that silently rebuilt the mesh from its subdivided
form instead of its cage would look identical going up and be irreversible coming down.
"""
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "mesh", "surfaces", "view")}
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


def rel(a, b, tol=1e-9):
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    return (r or {}).get("volume") if ok and isinstance(r, dict) else None


def mesh_box(x0, smooth=None):
    args = {"corner1": {"x": x0, "y": 0, "z": 0},
            "corner2": {"x": x0 + SIDE, "y": SIDE, "z": SIDE}}
    if smooth is not None:
        args["smoothLevel"] = smooth
    return do("mesh", "create_mesh_box", args, label=f"a mesh box at x={x0:.0f}"
                                                     + (f", smooth {smooth}" if smooth else ""))


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
    check("the mesh, geometry-2d and geometry-3d sessions are on the SAME drawing",
          bool(probe) and S["geometry-2d"].call("get_bounding_box", {"handle": probe})[0],
          f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── create_mesh_box: the counts are exact ────────────────────────────────────
print("\n== create_mesh_box: a 100 cube as a mesh ==")
r = mesh_box(0)
box0 = hnd(r)
if isinstance(r, dict):
    check("PROVEN: an unsmoothed box mesh is EXACTLY 8 vertices and 6 faces - the cage is written "
          "out by hand, so these are known before the call rather than read off afterwards",
          r.get("vertices") == 8 and r.get("faces") == 6, str(r)[:250])
    check("and it is at smooth level 0", r.get("smoothLevel") == 0, str(r)[:200])
r = do("mesh", "get_mesh_info", {"handle": box0})
if isinstance(r, dict):
    check("get_mesh_info agrees, reading it back from the drawing",
          r.get("vertices") == 8 and r.get("faces") == 6 and r.get("smoothLevel") == 0,
          str(r)[:250])

# ── the only way to measure a mesh ───────────────────────────────────────────
print("\n== convert_mesh_to_solid: the only way to measure a mesh at all ==")
r = do("mesh", "convert_mesh_to_solid", {"handle": box0})
solid0 = hnd(r)
check(f"PROVEN against arithmetic: the solid is exactly side cubed, {SIDE ** 3:.0f} - which is "
      f"also the proof the cage was WOUND correctly, since a face wound the wrong way looks "
      f"normal on screen and encloses nothing",
      rel(volume_of(solid0), SIDE ** 3, 1e-9), f"{volume_of(solid0)}")

# ── smoothing: four times the faces per level ────────────────────────────────
print("\n== set_mesh_smoothness: every level divides each face into four ==")
box1 = hnd(mesh_box(200))
r = do("mesh", "set_mesh_smoothness", {"handle": box1, "by": 1})
if isinstance(r, dict):
    check("the level reads back as 1", r.get("smoothLevel") == 1, str(r)[:200])
    # MEASURED, and the opposite of what was first assumed: the face count does NOT multiply,
    # because NumberOfFaces reports the cage. So the face count cannot say whether smoothing
    # happened - which is exactly why the tool measures the extents instead.
    check("PROVEN: the face count stays at the CAGE - 6 either way - so it cannot be what says "
          "the smoothing took",
          r.get("faces") == 6 and r.get("cageFaces") == 6, str(r)[:250])
r = do("mesh", "set_mesh_smoothness", {"handle": box1, "level": 2})
if isinstance(r, dict):
    check("level 2 reads back too", r.get("smoothLevel") == 2, str(r)[:250])

print("\n-- a smoothed box is ROUNDER, so it holds less --")
r = do("mesh", "convert_mesh_to_solid", {"handle": box1})
smoothed = hnd(r)
v_smooth = volume_of(smoothed)
check("PROVEN: smoothing rounds the corners off, so the volume is LESS than the sharp box - "
      "which is what says the smoothing changed the SHAPE and not just the face count",
      v_smooth is not None and 0 < v_smooth < SIDE ** 3,
      f"{v_smooth} against a sharp {SIDE ** 3}")
# The first version asserted "still more than half the box", which was invented rather than
# derived - a level 2 Catmull-Clark cube is about a THIRD of it, and the check failed on a
# correct result. Replaced by something that IS derivable and carries its own control: more
# smoothing shrinks it further, monotonically.
box1b = hnd(mesh_box(1000))
do("mesh", "set_mesh_smoothness", {"handle": box1b, "level": 1})
v_level1 = volume_of(hnd(do("mesh", "convert_mesh_to_solid", {"handle": box1b})))
check("PROVEN: level 2 shrinks the box MORE than level 1 does - each round of subdivision pulls "
      "the corners further in, and that ordering is a property of the algorithm rather than a "
      "number guessed at",
      v_level1 is not None and v_smooth is not None and SIDE ** 3 > v_level1 > v_smooth > 0,
      f"sharp {SIDE ** 3}, level 1 {v_level1}, level 2 {v_smooth}")

# ── reversibility: the cage is kept ──────────────────────────────────────────
print("\n== smoothing is REVERSIBLE, because the cage is kept ==")
box2 = hnd(mesh_box(400))
do("mesh", "set_mesh_smoothness", {"handle": box2, "level": 3})
r = do("mesh", "get_mesh_info", {"handle": box2})
check("the mesh is at level 3", isinstance(r, dict) and r.get("smoothLevel") == 3, str(r)[:200])
r = do("mesh", "set_mesh_smoothness", {"handle": box2, "level": 0})
check("and back down to 0, with the cage still 6 faces and 8 vertices",
      isinstance(r, dict) and r.get("smoothLevel") == 0 and r.get("faces") == 6
      and r.get("verticesNow") == 8, str(r)[:250])
# THE check on reversibility. A smoothing that rebuilt the mesh from its SUBDIVIDED form rather
# than its cage would look identical on the way up and be impossible to undo - the volume coming
# back to exactly side cubed is what separates the two.
r = do("mesh", "convert_mesh_to_solid", {"handle": box2})
check(f"PROVEN: and it converts to exactly {SIDE ** 3:.0f} again - the original box, not an "
      f"approximation of it. A smoothing that rebuilt from the subdivided form would look the "
      f"same going up and could never come back",
      rel(volume_of(hnd(r)), SIDE ** 3, 1e-9), f"{volume_of(hnd(r))}")

# ── convert_mesh_to_surface ──────────────────────────────────────────────────
print("\n== convert_mesh_to_surface ==")
box3 = hnd(mesh_box(600))
r = do("mesh", "convert_mesh_to_surface", {"handle": box3, "eraseSource": True})
srf = hnd(r)
ok, info = S["surfaces"].call("get_surface_info", {"handle": srf})
check(f"PROVEN: the surface has the box's own area, 6 x 100 x 100 = {6 * SIDE * SIDE:.0f}",
      ok and rel((info or {}).get("area"), 6 * SIDE * SIDE, 1e-6), f"{(info or {}).get('area')}")
check("PROVEN: and it has no volume - a surface is a shell, whatever it was made from",
      (volume_of(srf) or 0) < 1e-9, f"{volume_of(srf)}")

print("\n-- refusals --")
do("mesh", "create_mesh_box", {"corner1": {"x": 0, "y": 0, "z": 0},
                               "corner2": {"x": 100, "y": 100, "z": 0}},
   label="a box with a zero side is refused", expect_fail=True)
do("mesh", "create_mesh_box", {"corner1": {"x": 0, "y": 0, "z": 0},
                               "corner2": {"x": 10, "y": 10, "z": 10}, "smoothLevel": 9},
   label="a smooth level above 4 is refused", expect_fail=True)
box4 = hnd(mesh_box(800))
do("mesh", "set_mesh_smoothness", {"handle": box4},
   label="a smoothness change with neither level nor by is refused", expect_fail=True)
do("mesh", "set_mesh_smoothness", {"handle": box4, "level": 1, "by": 1},
   label="giving both level and by is refused", expect_fail=True)
do("mesh", "set_mesh_smoothness", {"handle": box4, "level": 0},
   label="setting the level it already has is refused rather than reported as a change",
   expect_fail=True)
do("mesh", "set_mesh_smoothness", {"handle": box4, "by": -1},
   label="going below 0 is refused", expect_fail=True)
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 500}, "end": {"x": 100, "y": 500}},
            label="a line"))
do("mesh", "get_mesh_info", {"handle": ln},
   label="a line is refused by name", expect_fail=True)
do("mesh", "convert_mesh_to_solid", {"handle": ln},
   label="and cannot be converted either", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "mesh.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -40, "yMin": -40, "xMax": 940, "yMax": 140},
                            "widthPx": 2400, "heightPx": 460})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 4000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> in plan view: the first box as a plain square, the second as a ROUNDED square with")
print("     its facets showing - that is the smoothing, and it is the one thing here no number")
print("     conveys - then a square again where the smoothing was undone, and the surface.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
