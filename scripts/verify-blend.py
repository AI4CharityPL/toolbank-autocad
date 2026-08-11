# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — blend_curves.

A blend is easy to get plausibly wrong. Three things a return code cannot settle:

* **Which ends were joined.** AutoCAD's BLEND blends the ends you pick; there is no pick in an
  MCP call, so the tool takes the nearest pair. A blend across the FAR ends produces a
  perfectly valid spline looping over the whole drawing. Checked by placing two lines whose
  near ends are unambiguous and asserting the reported ends and points.
* **Whether it actually reaches both curves.** A spline that stops short still looks fine in
  JSON, and on screen at a small zoom. Measured as a distance from each curve's end.
* **Whether the tangents point INTO the gap.** GetFirstDerivative runs in the curve's own
  direction, so at a start point it faces away and has to be flipped. Get it wrong and the
  blend leaves each end backwards, making a visible hook. Measured: the blend's own start
  direction must agree with the direction of the curve it leaves.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "view")}
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


def close(a, b, tol=1e-6):
    return a is not None and b is not None and abs(a - b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def line(x1, y1, x2, y2):
    return hnd(S["geometry-2d"].call("draw_line", {"start": {"x": x1, "y": y1},
                                                   "end": {"x": x2, "y": y2}})[1])


def dist_to(handle, x, y):
    ok, r = S["geometry-2d"].call("get_distance_to_entity",
                                  {"handle": handle, "point": {"x": x, "y": y}})
    return (r or {}).get("value") if ok else None


def on_curve(d, tol=1e-4):
    return d is not None and abs(d) <= tol


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return (r or {}).get("bbox") or {} if ok else {}


print("== fresh drawing ==")
do("files", "new_document", {})

# Two horizontal lines with a 60-unit gap. Line A runs left-to-right and ENDS at (100,0);
# line B runs left-to-right and STARTS at (160,0). The near pair is unambiguous.
print("\n== two lines with a 60 gap between A's end and B's start ==")
a = line(0, 0, 100, 0)
b = line(160, 40, 260, 40)

r = do("geometry-2d", "blend_curves", {"handle1": a, "handle2": b})
bl = hnd(r)
if isinstance(r, dict):
    j = r.get("joinedAt") or {}
    check("it says it used A's END", j.get("end1") == "end", str(j)[:220])
    check("and B's START", j.get("end2") == "start", str(j)[:220])
    check("naming the actual point on A", close((j.get("point1") or [0])[0], 100), str(j)[:220])
    check("and on B", close((j.get("point2") or [0])[0], 160), str(j)[:220])
    check("the gap is reported", close(r.get("gap"), math.hypot(60, 40), 1e-6),
          f"got {r.get('gap')}")
    check("continuity defaulted to tangent", r.get("continuity") == "tangent", str(r)[:220])

print("\n-- it REACHES both curves --")
check("PROVEN: the blend touches A's end at (100,0)", on_curve(dist_to(bl, 100, 0)),
      f"got {dist_to(bl, 100, 0)}")
check("PROVEN: and B's start at (160,40)", on_curve(dist_to(bl, 160, 40)),
      f"got {dist_to(bl, 160, 40)}")

print("\n-- it does not loop off across the drawing --")
bb = bbox(bl)
# A correct blend lives inside the gap. One joining the FAR ends would span 0..260.
check("PROVEN: the blend stays between x=100 and x=160",
      close((bb.get("min") or {}).get("x"), 100, 0.5) and close((bb.get("max") or {}).get("x"), 160, 0.5),
      f"{bb} — a blend across the wrong ends would span the whole drawing")

print("\n-- a tangent blend does not OVERSHOOT either join --")
# The check that would have caught the first version. It placed interior fit points along the
# tangents AND imposed the tangents, over-constraining the fit: the blend left (100,0) heading
# DOWNWARDS and reached y = -9.7 before rising. Every other assertion here passed on that
# curve. A G1 blend between two horizontal lines has to stay between them.
check("PROVEN: it stays within y 0..40, the two joins",
      close((bb.get("min") or {}).get("y"), 0, 1e-4) and close((bb.get("max") or {}).get("y"), 40, 1e-4),
      f"{bb} - dipping below 0 means it left the lower line going the wrong way")

print("\n-- the tangents point INTO the gap, so there is no hook --")
# A hooked blend leaves (100,0) heading LEFT, so it would reach x below 100. The bbox check
# above already rules that out; this measures the direction directly at a small step.
near = dist_to(bl, 101, 0)
check("PROVEN: the blend continues to the RIGHT of A's end, not back over it",
      near is not None and near < 1.0,
      f"distance from (101,0) to the blend is {near} — a hook would leave it far from there")

print("\n== smooth continuity is refused, with its measurements ==")
# Withdrawn after two measured attempts: interior fit points made the blend detour outside
# its joins, and a longer tangent vector did nothing because Spline normalises it - tangent
# and smooth came out identical at 74.374. A silently ignored argument is worse than a
# missing one, so it refuses rather than quietly behaving like tangent.
r = do("geometry-2d", "blend_curves",
       {"handle1": line(0, 200, 100, 200), "handle2": line(160, 240, 260, 240),
        "continuity": "smooth"},
       label="smooth is refused rather than aliased to tangent", expect_fail=True)
check("and the refusal carries the numbers, not just a no",
      "74.374" in str(r) and "normalises" in str(r), str(r)[:280])

print("\n== refusals ==")
do("geometry-2d", "blend_curves", {"handle1": a, "handle2": a},
   label="blending a curve with itself is refused", expect_fail=True)
circ = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 500, "y": 0}, "radius": 30},
              label="a circle"))
r = do("geometry-2d", "blend_curves", {"handle1": a, "handle2": circ},
       label="a closed curve is refused", expect_fail=True)
check("and the refusal says it has no free end", "free end" in str(r), str(r)[:220])
touching1 = line(0, 500, 100, 500)
touching2 = line(100, 500, 200, 500)
r = do("geometry-2d", "blend_curves", {"handle1": touching1, "handle2": touching2},
       label="curves that already meet are refused", expect_fail=True)
check("and the refusal points at join_curves", "join_curves" in str(r), str(r)[:220])
do("geometry-2d", "blend_curves", {"handle1": a, "handle2": b, "continuity": "wobbly"},
   label="an unknown continuity is refused", expect_fail=True)
do("geometry-2d", "blend_curves", {"handle1": a},
   label="a missing second handle is refused", expect_fail=True)

print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "blend.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -20, "yMin": -40, "xMax": 300, "yMax": 400},
                            "widthPx": 1500, "heightPx": 1500})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm three pairs of offset lines, each bridged by an S-curve that leaves and")
print("     meets the lines smoothly. NO hooks or cusps at the joins, and no curve looping")
print("     back over its own line.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
