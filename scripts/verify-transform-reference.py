# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — transforming by reference.

Two tools: modify.scale_by_reference and modify.rotate_by_reference. They differ from the
existing `scale` and `rotate` in taking a MEASUREMENT rather than a factor or an angle, which
is how the operation is actually reached: nobody knows a scanned plan is out by 1.0477, they
know a door that should be 900 measures 859.

What has to be proved, and what a return code cannot show:

* **The computed factor is right.** A tool that scaled by the newLength instead of by
  newLength/referenceLength would return a perfectly plausible object. So geometry is measured
  before and after with get_distance_points and get_bounding_box.
* **The base point does not move.** That is the whole contract of a base point, and it is the
  easiest thing to get wrong with a transform matrix.
* **Both ways of giving the reference agree.** The number form and the two-point form must
  produce identical geometry, or one of them is lying.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "modify", "view")}
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
    return abs(a - b) <= tol


def line(x1, y1, x2, y2):
    r = S["geometry-2d"].call("draw_line", {"start": {"x": x1, "y": y1},
                                            "end": {"x": x2, "y": y2}})[1]
    return ((r or {}).get("entity") or {}).get("handle")


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return (r or {}).get("bbox") or {} if ok else {}


def length_of(h):
    # get_curve_length answers with `value`, not `length`. Reading the wrong field returned
    # None and looked like four tool failures - the fourth time this session that an argument
    # or field-name mistake of mine read as a broken tool.
    ok, r = S["geometry-2d"].call("get_curve_length", {"handle": h})
    return (r or {}).get("value") if ok else None


print("== fresh drawing ==")
do("files", "new_document", {})

# ── scale_by_reference ────────────────────────────────────────────────────────
print("\n== scale_by_reference: the door that should be 900 and measures 859 ==")
h = line(0, 0, 859, 0)
check("the reference line is 859 long", close(length_of(h) or 0, 859), f"got {length_of(h)}")

r = do("modify", "scale_by_reference",
       {"handles": [h], "basePoint": {"x": 0, "y": 0},
        "referenceLength": 859, "newLength": 900})
if isinstance(r, dict):
    check("one entity affected", r.get("affected") == 1, str(r)[:200])
    check("the factor was COMPUTED, not given",
          close(r.get("factor", 0), 900 / 859, 1e-9), f"got {r.get('factor')}")
    check("it reports the reference it used", close(r.get("referenceLength", 0), 859),
          str(r)[:200])
check("PROVEN: the line now measures 900", close(length_of(h) or 0, 900, 1e-6),
      f"got {length_of(h)}")
b = bbox(h)
check("PROVEN: the base point did not move",
      close((b.get("min") or {}).get("x", 1), 0), str(b)[:200])
check("PROVEN: the far end moved to 900",
      close((b.get("max") or {}).get("x", 0), 900, 1e-6), str(b)[:200])

print("\n-- the two-point form must agree with the number form --")
h2 = line(0, 100, 859, 100)
r = do("modify", "scale_by_reference",
       {"handles": [h2], "basePoint": {"x": 0, "y": 100},
        "referenceStart": {"x": 0, "y": 100}, "referenceEnd": {"x": 859, "y": 100},
        "newLength": 900},
       label="reference measured between two points")
if isinstance(r, dict):
    check("the same factor came out", close(r.get("factor", 0), 900 / 859, 1e-9),
          f"got {r.get('factor')}")
check("PROVEN: identical geometry to the number form",
      close(length_of(h2) or 0, length_of(h) or -1, 1e-9),
      f"{length_of(h2)} vs {length_of(h)}")

print("\n-- a base point away from the geometry still holds still --")
h3 = line(100, 200, 200, 200)
r = do("modify", "scale_by_reference",
       {"handles": [h3], "basePoint": {"x": 0, "y": 200},
        "referenceLength": 100, "newLength": 200},
       label="scale x2 about a point 100 away")
b = bbox(h3)
check("PROVEN: the near end moved 100 -> 200",
      close((b.get("min") or {}).get("x", 0), 200), str(b)[:200])
check("PROVEN: the far end moved 200 -> 400",
      close((b.get("max") or {}).get("x", 0), 400), str(b)[:200])

print("\n-- refusals --")
do("modify", "scale_by_reference",
   {"handles": [h3], "basePoint": {"x": 0, "y": 0}, "newLength": 100},
   label="no reference at all is refused", expect_fail=True)
r = do("modify", "scale_by_reference",
       {"handles": [h3], "basePoint": {"x": 0, "y": 0}, "referenceLength": 10,
        "referenceStart": {"x": 0, "y": 0}, "referenceEnd": {"x": 10, "y": 0},
        "newLength": 100},
       label="giving BOTH forms is refused", expect_fail=True)
check("and the refusal says not both and not neither", "not both" in str(r), str(r)[:220])
do("modify", "scale_by_reference",
   {"handles": [h3], "basePoint": {"x": 0, "y": 0}, "referenceLength": 100},
   label="a missing newLength is refused", expect_fail=True)
do("modify", "scale_by_reference",
   {"handles": [h3], "basePoint": {"x": 0, "y": 0}, "referenceLength": 0, "newLength": 100},
   label="a zero reference is refused", expect_fail=True)
r = do("modify", "scale_by_reference",
       {"handles": [h3], "basePoint": {"x": 0, "y": 0},
        "referenceStart": {"x": 5, "y": 5}, "referenceEnd": {"x": 5, "y": 5},
        "newLength": 100},
       label="two identical reference points are refused", expect_fail=True)
check("and the refusal says the distance is zero", "zero" in str(r), str(r)[:220])

# ── rotate_by_reference ───────────────────────────────────────────────────────
print("\n== rotate_by_reference: straighten an edge drawn at an unknown skew ==")
# A line at exactly 30 degrees, 100 long.
x2, y2 = 100 * math.cos(math.radians(30)), 100 * math.sin(math.radians(30))
h4 = line(0, 400, x2, 400 + y2)
r = do("modify", "rotate_by_reference",
       {"handles": [h4], "basePoint": {"x": 0, "y": 400},
        "referenceAngleDeg": 30, "newAngleDeg": 0})
if isinstance(r, dict):
    check("turned by -30, the DIFFERENCE not the target",
          close(r.get("rotatedByDeg", 0), -30, 1e-9), f"got {r.get('rotatedByDeg')}")
b = bbox(h4)
check("PROVEN: the line is now horizontal",
      close((b.get("max") or {}).get("y", 1) - (b.get("min") or {}).get("y", 0), 0, 1e-6),
      str(b)[:200])
check("PROVEN: it is still 100 long", close(length_of(h4) or 0, 100, 1e-6),
      f"got {length_of(h4)}")
check("PROVEN: the base point held still",
      close((b.get("min") or {}).get("x", 1), 0, 1e-9), str(b)[:200])

print("\n-- the reference direction can be measured from two points --")
h5 = line(0, 500, x2, 500 + y2)
r = do("modify", "rotate_by_reference",
       {"handles": [h5], "basePoint": {"x": 0, "y": 500},
        "referenceStart": {"x": 0, "y": 500}, "referenceEnd": {"x": x2, "y": 500 + y2},
        "newAngleDeg": 90},
       label="reference direction taken from two points")
if isinstance(r, dict):
    check("it measured the 30 degrees itself",
          close(r.get("referenceAngleDeg", 0), 30, 1e-6), f"got {r.get('referenceAngleDeg')}")
    check("and turned by 60", close(r.get("rotatedByDeg", 0), 60, 1e-6),
          f"got {r.get('rotatedByDeg')}")
b = bbox(h5)
check("PROVEN: the line is now vertical",
      close((b.get("max") or {}).get("x", 1) - (b.get("min") or {}).get("x", 0), 0, 1e-6),
      str(b)[:200])

print("\n-- refusals --")
do("modify", "rotate_by_reference",
   {"handles": [h5], "basePoint": {"x": 0, "y": 0}, "newAngleDeg": 0},
   label="no reference at all is refused", expect_fail=True)
do("modify", "rotate_by_reference",
   {"handles": [h5], "basePoint": {"x": 0, "y": 0}, "referenceAngleDeg": 0},
   label="a missing newAngleDeg is refused", expect_fail=True)
do("modify", "rotate_by_reference",
   {"handles": [], "basePoint": {"x": 0, "y": 0}, "referenceAngleDeg": 0, "newAngleDeg": 90},
   label="an empty handle list is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "transform-reference.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -50, "yMin": -50, "xMax": 950, "yMax": 650},
                            "widthPx": 1600, "heightPx": 1100})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
