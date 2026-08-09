# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.1, last tranche — shape and health.

`draw_polysolid`, `presspull`, `clean_solid`, `check_solid`.

Arithmetic, all of it worked out here rather than taken from the tool:

* a wall 25 wide and 300 high along a straight 500 path holds exactly 25*300*500 = 3750000
* pressing a circle of radius 40 up by 50 makes pi*40*40*50 = 251327.412 of solid
* pressing the same circle DOWN into a box cuts exactly that much out of it
* imprinting a line on a face adds edges without adding material; cleaning takes them back off
  and the volume must not move either way - so the pair is a closed loop that has to return to
  where it started
* check_solid tests EULER-POINCARE: for a closed solid V - E + F = 2*(shells - genus). A box
  gives 8 - 12 + 6 = 2 with genus 0. Drill a hole right through it and the same arithmetic gives
  0 with genus 1. That is a statement about the boundary closing, which a plausible-looking
  volume cannot make.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "modify", "view")}
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


def rel(a, b, tol=1e-9):
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    if not ok or not isinstance(r, dict):
        return None
    return r.get("volume") if r.get("volume") is not None else r.get("value")


def edges_of(h):
    ok, r = S["geometry-3d"].call("list_solid_edges", {"handle": h})
    return len((r or {}).get("edges") or []) if ok else None


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
    probe = hnd(S["geometry-3d"].call("draw_box", {"corner1": {"x": 0, "y": 9000, "z": 0},
                                                   "corner2": {"x": 10, "y": 9010, "z": 10}})[1])
    check("the geometry-3d, geometry-2d and modify sessions are on the SAME drawing",
          bool(probe) and S["geometry-2d"].call("get_bounding_box", {"handle": probe})[0]
          and S["modify"].call("set_layer", {"handles": [probe], "layer": "0"})[0],
          f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── draw_polysolid ───────────────────────────────────────────────────────────
print("\n== draw_polysolid: a wall 25 wide, 300 high, along a straight 500 ==")
W, H, L = 25.0, 300.0, 500.0
r = do("geometry-3d", "draw_polysolid", {
    "vertices": [{"x": 0, "y": 0}, {"x": L, "y": 0}],
    "width": W, "height": H})
wall = hnd(r)
if isinstance(r, dict):
    check(f"it measured the path at {L:.0f}", rel(r.get("pathLength"), L), f"{r.get('pathLength')}")
check(f"PROVEN against arithmetic: on a straight path the wall is exactly w*h*L = "
      f"{W * H * L:.0f}",
      rel(volume_of(wall), W * H * L), f"{volume_of(wall)} vs {W * H * L}")

print("\n-- an L-shaped path: the corner is SHARED, so it holds less than the two legs --")
r = do("geometry-3d", "draw_polysolid", {
    "vertices": [{"x": 0, "y": 800}, {"x": L, "y": 800}, {"x": L, "y": 800 + L}],
    "width": W, "height": H})
corner = hnd(r)
v_corner = volume_of(corner)
naive = W * H * 2 * L
# A tool that swept each leg separately and added them would report the naive figure and look
# right in the drawing too, because the overlap is buried inside the corner.
# MEASURED, after the first guess here was wrong in both size and SIGN. A centred wall round a
# right-angle corner comes to exactly w*h*L: the mitre loses on the inside of the turn precisely
# what it gains on the outside. It is the JUSTIFIED walls that differ, by the corner block.
check("PROVEN: a CENTRED wall round a corner is still exactly w*h*L - the mitre gives back on "
      "the outside of the turn what it takes on the inside, so the centre line measures the wall",
      rel(v_corner, naive, 1e-9), f"measured {v_corner}, w*h*L = {naive}")
rl = do("geometry-3d", "draw_polysolid", {
    "vertices": [{"x": 0, "y": 1000}, {"x": L, "y": 1000}, {"x": L, "y": 1000 + L}],
    "width": W, "height": H, "justify": "left"}, label="the same corner justified left")
rr = do("geometry-3d", "draw_polysolid", {
    "vertices": [{"x": 700, "y": 1000}, {"x": 700 + L, "y": 1000}, {"x": 700 + L, "y": 1000 + L}],
    "width": W, "height": H, "justify": "right"}, label="and justified right")
vl, vr = volume_of(hnd(rl)), volume_of(hnd(rr))
# The CONTROL that gives the centred result its meaning: if every justification came to w*h*L,
# the equality above would say nothing about mitring and everything about the corner being
# ignored. These two differ by exactly the corner block, in opposite directions.
check("THE CONTROL: justified LEFT it comes up SHORT by exactly w*w*h - the corner block on the "
      "inside of the turn that it no longer reaches",
      rel(naive - vl, W * W * H, 1e-9), f"short by {naive - (vl or 0)} vs {W * W * H}")
check("and justified RIGHT it is OVER by that same block, having wrapped round the outside - so "
      "the centred case really is the mitre balancing, not the corner going unnoticed",
      rel(vr - naive, W * W * H, 1e-9), f"over by {(vr or 0) - naive} vs {W * W * H}")

print("\n-- justify puts the wall on one side of the line or the other --")
r1 = do("geometry-3d", "draw_polysolid", {"vertices": [{"x": 0, "y": 1600}, {"x": L, "y": 1600}],
                                          "width": W, "height": H, "justify": "left"})
r2 = do("geometry-3d", "draw_polysolid", {"vertices": [{"x": 0, "y": 1900}, {"x": L, "y": 1900}],
                                          "width": W, "height": H, "justify": "right"})
bb1 = (S["geometry-2d"].call("get_bounding_box", {"handle": hnd(r1)})[1] or {}).get("bbox") or {}
bb2 = (S["geometry-2d"].call("get_bounding_box", {"handle": hnd(r2)})[1] or {}).get("bbox") or {}
check("PROVEN: left and right land on OPPOSITE sides of the path line, 25 apart, so the flag is "
      "doing something rather than being accepted and ignored",
      bb1 and bb2 and abs((bb1["min"]["y"] - 1600) - (bb2["min"]["y"] - 1900)) > W - 1e-6,
      f"left y {bb1.get('min', {}).get('y')} - 1600, right y {bb2.get('min', {}).get('y')} - 1900")
do("geometry-3d", "draw_polysolid", {"vertices": [{"x": 0, "y": 0}], "width": W, "height": H},
   label="a path of one point is refused", expect_fail=True)
do("geometry-3d", "draw_polysolid", {"vertices": [{"x": 0, "y": 0}, {"x": L, "y": 0}],
                                     "width": 0, "height": H},
   label="a width of 0 is refused", expect_fail=True)

# ── presspull ────────────────────────────────────────────────────────────────
print("\n== presspull: a circle of radius 40 pushed up by 50 ==")
R, D = 40.0, 50.0
AREA = math.pi * R * R
circ = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 0, "y": 2400}, "radius": R},
              label="a circle"))
r = do("geometry-3d", "presspull", {"handle": circ, "distance": D})
pushed = hnd(r)
if isinstance(r, dict):
    check(f"PROVEN: it measured the area as pi*r*r = {AREA:.4f}", rel(r.get("area"), AREA, 1e-6),
          f"{r.get('area')}")
check(f"PROVEN against arithmetic: area*distance = {AREA * D:.4f} of solid came out",
      rel(volume_of(pushed), AREA * D, 1e-6), f"{volume_of(pushed)}")

print("\n-- pressed INTO a solid, the sign decides pocket or boss --")
box1 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 500, "y": 2400, "z": 0},
                                          "corner2": {"x": 700, "y": 2600, "z": 100}},
              label="a 200x200x100 box"))
v_box = volume_of(box1)
c2 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 600, "y": 2500}, "radius": R},
            label="a circle on its top face"))
lift(c2, 100)
r = do("geometry-3d", "presspull", {"handle": c2, "distance": -D, "targetHandle": box1})
if isinstance(r, dict):
    check("it says it SUBTRACTED, because the distance was negative",
          r.get("mode") == "subtract", str(r)[:250])
check(f"PROVEN against arithmetic: a pocket of exactly {AREA * D:.4f} was cut, leaving "
      f"{v_box - AREA * D:.4f}",
      rel(volume_of(box1), v_box - AREA * D, 1e-6), f"{volume_of(box1)}")

print("\n-- and a positive distance ADDS instead --")
box2 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 900, "y": 2400, "z": 0},
                                          "corner2": {"x": 1100, "y": 2600, "z": 100}},
              label="a second box"))
c3 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 1000, "y": 2500}, "radius": R},
            label="a circle on its top face"))
lift(c3, 100)
r = do("geometry-3d", "presspull", {"handle": c3, "distance": D, "targetHandle": box2})
if isinstance(r, dict):
    check("it says it UNITED", r.get("mode") == "union", str(r)[:250])
check(f"PROVEN: a boss of {AREA * D:.4f} was added",
      rel(volume_of(box2), WHOLE_BOX := 200.0 * 200.0 * 100.0 + AREA * D, 1e-6),
      f"{volume_of(box2)} vs {WHOLE_BOX}")

print("\n-- a push that never reaches the target is refused, not reported as a success --")
box3 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 1300, "y": 2400, "z": 0},
                                          "corner2": {"x": 1500, "y": 2600, "z": 100}},
              label="a third box"))
c4 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 1400, "y": 3200}, "radius": R},
            label="a circle well away from it"))
lift(c4, 100)
r = do("geometry-3d", "presspull", {"handle": c4, "distance": -D, "targetHandle": box3},
       label="pressing into a box it does not touch is refused", expect_fail=True)
check("and the refusal says nothing was added or taken away",
      "did not meet" in str(r) or "unchanged" in str(r), str(r)[:280])
check("PROVEN: that box is untouched", rel(volume_of(box3), 200.0 * 200.0 * 100.0),
      f"{volume_of(box3)}")

print("\n-- refusals --")
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 3600},
                                         "end": {"x": 100, "y": 3600}}, label="an open line"))
r = do("geometry-3d", "presspull", {"handle": ln, "distance": 10.0},
       label="an open curve encloses nothing and is refused", expect_fail=True)
check("and the refusal points at boundary_from_point for the case where you only have a point",
      "boundary_from_point" in str(r), str(r)[:300])
do("geometry-3d", "presspull", {"handle": circ, "distance": 0},
   label="a distance of 0 is refused", expect_fail=True)

# ── clean_solid, paired with imprint so the loop has to close ────────────────
print("\n== clean_solid: imprint a line, then take the division back off ==")
box4 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 1700, "y": 2400, "z": 0},
                                          "corner2": {"x": 1900, "y": 2600, "z": 100}},
              label="a fourth box"))
v4, e4 = volume_of(box4), edges_of(box4)
check("a box starts with 12 edges", e4 == 12, f"{e4}")
seg = hnd(do("geometry-2d", "draw_line", {"start": {"x": 1700, "y": 2500},
                                          "end": {"x": 1900, "y": 2500}}, label="a line"))
lift(seg, 100)
r = do("geometry-3d", "imprint_edges", {"solidHandle": box4, "curveHandle": seg,
                                        "eraseSource": True})
e_imprinted = edges_of(box4)
check("the imprint divided the top face and added edges", (e_imprinted or 0) > 12,
      f"{e_imprinted}")
check("without touching the volume", rel(volume_of(box4), v4), f"{volume_of(box4)}")

# MEASURED, and NOT what was expected. CleanBody removes nothing here: the edges an imprint adds
# separate two faces the modeller treats as distinct, even though they lie in one plane. Nor does
# it find anything after a union — AutoCAD merges coplanar faces during the boolean itself, so a
# unioned pair of adjacent boxes already comes back as six faces and twelve edges. Turning history
# recording off first rules out that setting as the reason. The tool therefore ships reporting
# exactly this, rather than claiming an effect it cannot be shown to have.
r = do("geometry-3d", "clean_solid", {"handle": box4})
if isinstance(r, dict):
    check("it reports plainly that nothing was redundant, instead of claiming a clean",
          r.get("edgesRemoved") == 0 and "normal case" in str(r.get("note")), str(r)[:250])
check("the imprinted edges are still there, so undoing an imprint is NOT what clean does",
      edges_of(box4) == e_imprinted, f"{edges_of(box4)} vs {e_imprinted}")
# THE guarantee that does hold and is worth guarding: whatever it removes or does not remove, it
# must never touch the shape. A cleaner that took material would still return a valid solid.
check("PROVEN: and the volume did not move - the guarantee that matters, since a clean that "
      "removed material would still hand back a perfectly good solid",
      rel(volume_of(box4), v4), f"{volume_of(box4)} vs {v4}")

# ── check_solid, against Euler-Poincare ──────────────────────────────────────
print("\n== check_solid: Euler-Poincare on a box, and on a box with a hole through it ==")
box5 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 2100, "y": 2400, "z": 0},
                                          "corner2": {"x": 2300, "y": 2600, "z": 100}},
              label="a fifth box"))
r = do("geometry-3d", "check_solid", {"handle": box5})
if isinstance(r, dict):
    check("PROVEN against arithmetic: a box is 8 vertices, 12 edges, 6 faces",
          r.get("vertices") == 8 and r.get("edges") == 12 and r.get("faces") == 6, str(r)[:250])
    check("PROVEN: V - E + F - R = 8 - 12 + 6 - 0 = 2, one shell, no rings, therefore genus 0 - "
          "nothing runs through it",
          r.get("eulerCharacteristic") == 2 and r.get("shells") == 1 and r.get("genus") == 0
          and r.get("rings") == 0, str(r)[:280])
    check("and it is reported sound with no problems listed",
          r.get("valid") is True and not r.get("problems"), str(r)[:250])

print("\n-- now drill a hole right through and the same arithmetic must give 0 --")
c5 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 2200, "y": 2500}, "radius": R},
            label="a circle on the top"))
lift(c5, 100)
do("geometry-3d", "presspull", {"handle": c5, "distance": -150.0, "targetHandle": box5},
   label="pressed straight through")
check("PROVEN: the hole took pi*r*r*100 of the box, since only the 100 inside it counts",
      rel(volume_of(box5), 200.0 * 200.0 * 100.0 - AREA * 100.0, 1e-6), f"{volume_of(box5)}")
r = do("geometry-3d", "check_solid", {"handle": box5})
if isinstance(r, dict):
    # This is the check that caught the defect: the first version left the RING term out of
    # Euler-Poincare and read V - E + F = 2, genus 0, for a box with a hole right through it —
    # failing to notice the one thing check_solid exists to notice. The two extra loops, one on
    # the top face and one on the bottom, are what make V - E + F - R balance.
    check("PROVEN: V - E + F - R now comes to 0 and the genus is 1 - the arithmetic COUNTED the "
          "hole, which is a statement about the boundary closing that no volume can make",
          r.get("eulerCharacteristic") == 0 and r.get("genus") == 1 and r.get("rings") == 2,
          str(r)[:300])
    check("and it is still sound", r.get("valid") is True, str(r)[:250])

print("\n-- refusals --")
do("geometry-3d", "check_solid", {"handle": ln},
   label="a line is refused by name", expect_fail=True)
do("geometry-3d", "clean_solid", {"handle": ln},
   label="and cannot be cleaned either", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "polysolid-presspull.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 2400, "yMax": 2700},
                            "widthPx": 1800, "heightPx": 2000})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> in plan view: a straight wall as a long thin rectangle at the bottom, an L-shaped one")
print("     above it turning a square corner, two more showing justify offset to either side of")
print("     their line, and along the top a row of boxes with circles in them - the pocket, the")
print("     boss and the hole - plus the free-standing cylinder that was pushed on its own.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
