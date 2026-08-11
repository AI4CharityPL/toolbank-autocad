# -*- coding: utf-8 -*-
"""Live verification for `modify.align` — after a measured 180-degree bug.

`align` computed its rotation axis as the CROSS PRODUCT of the source and target directions.
That vanishes when the two are parallel, which covers two opposite cases: already aligned, and
exactly REVERSED. The guard `axis.Length > 1e-9` treated both as "no rotation needed", so a
180-degree align moved nothing, turned nothing and returned affected: 1.

Measured before the fix, against a 90-degree control so that "it did not move" could not be
confused with "the tool is broken":

    90 deg : (0,0)-(100,0)  ->  x 0..0,   y 0..100    correct
    180 deg: (0,500)-(100,500) -> x 0..100, y 500..500  UNCHANGED, and reported as success

Both cases are asserted below, and the file also pins the distinction the `scale` flag makes:
without it source B only POINTS at target B, with it B lands ON target B. A test that checked
"the selection moved" would pass either way.
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
    return a is not None and b is not None and abs(a - b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return (r or {}).get("bbox") or {} if ok else {}


def length_of(h):
    ok, r = S["geometry-2d"].call("get_curve_length", {"handle": h})
    return (r or {}).get("value") if ok else None


def line(x1, y1, x2, y2):
    return hnd(S["geometry-2d"].call("draw_line", {"start": {"x": x1, "y": y1},
                                                   "end": {"x": x2, "y": y2}})[1])


def align(h, sa, sb, ta, tb, scale=None, label=None, expect_fail=False):
    args = {"handles": [h] if isinstance(h, str) else h,
            "sourceA": {"x": sa[0], "y": sa[1]}, "sourceB": {"x": sb[0], "y": sb[1]},
            "targetA": {"x": ta[0], "y": ta[1]}, "targetB": {"x": tb[0], "y": tb[1]}}
    if scale is not None:
        args["scale"] = scale
    return do("modify", "align", args, label=label, expect_fail=expect_fail)


print("== fresh drawing ==")
do("files", "new_document", {})

# ── the control ───────────────────────────────────────────────────────────────
print("\n== CONTROL: a 90 degree align, where the cross product is well defined ==")
# This exists so that a failure of the 180 case below cannot be read as "align is broken".
h90 = line(0, 0, 100, 0)
r = align(h90, (0, 0), (100, 0), (0, 0), (0, 100), label="align 90 degrees")
if isinstance(r, dict):
    check("it reports a 90 degree turn", close(r.get("rotatedByDeg"), 90, 1e-9), str(r)[:220])
    check("and that it did not scale", r.get("scaled") is False, str(r)[:220])
b = bbox(h90)
check("PROVEN: the line is now vertical, 0..100 in y",
      close((b.get("max") or {}).get("y"), 100, 1e-6)
      and close((b.get("max") or {}).get("x"), 0, 1e-6), str(b)[:220])

# ── the case that was broken ──────────────────────────────────────────────────
print("\n== 180 degrees: parallel directions, so the cross product is ZERO ==")
h180 = line(0, 500, 100, 500)
before = bbox(h180)
r = align(h180, (0, 500), (100, 500), (0, 500), (-100, 500),
          label="align exactly reversed")
if isinstance(r, dict):
    check("it reports a 180 degree turn rather than 0",
          close(r.get("rotatedByDeg"), 180, 1e-9), str(r)[:220])
after = bbox(h180)
# Before the fix this read x 0..100 - completely unchanged - with affected: 1.
check("PROVEN: the line flipped to x -100..0",
      close((after.get("min") or {}).get("x"), -100, 1e-6)
      and close((after.get("max") or {}).get("x"), 0, 1e-6),
      f"{before} -> {after} — unchanged means the zero cross product skipped the rotation")

print("\n-- and the opposite parallel case still does nothing, correctly --")
h0 = line(0, 700, 100, 700)
r = align(h0, (0, 700), (100, 700), (0, 700), (100, 700), label="align onto itself")
if isinstance(r, dict):
    check("0 degrees, not 180", close(r.get("rotatedByDeg"), 0, 1e-9), str(r)[:220])
b0 = bbox(h0)
check("PROVEN: it stayed put", close((b0.get("min") or {}).get("x"), 0, 1e-6)
      and close((b0.get("max") or {}).get("x"), 100, 1e-6), str(b0)[:220])

# ── what the scale flag actually buys ─────────────────────────────────────────
print("\n== scale=false: B points AT its target and stops short ==")
# Source pair is 100 long, target pair is 200. Without scale the line must stay 100 long.
hn = line(0, 900, 100, 900)
len_before = length_of(hn)
r = align(hn, (0, 900), (100, 900), (0, 900), (200, 900), label="align, no scale")
if isinstance(r, dict):
    check("factor stays 1", close(r.get("factor"), 1, 1e-12), str(r)[:220])
    check("and it says how far B stopped short — 100",
          close(r.get("distanceToTargetB"), 100, 1e-6), str(r)[:220])
check("PROVEN: the line is still 100 long", close(length_of(hn), len_before, 1e-6),
      f"{len_before} -> {length_of(hn)}")

print("\n== scale=true: B lands ON its target ==")
hs = line(0, 1100, 100, 1100)
r = align(hs, (0, 1100), (100, 1100), (0, 1100), (200, 1100), scale=True,
          label="align with scale")
if isinstance(r, dict):
    check("factor is 2", close(r.get("factor"), 2, 1e-12), str(r)[:220])
    # THE control for the flag. Without this, scale=true and scale=false both "work".
    check("PROVEN: B landed ON target B, distance 0",
          close(r.get("distanceToTargetB"), 0, 1e-9), str(r)[:220])
check("PROVEN: and the line really is 200 long now", close(length_of(hs), 200, 1e-6),
      f"got {length_of(hs)}")

print("\n== rotate and scale together ==")
hb = line(0, 1300, 100, 1300)
r = align(hb, (0, 1300), (100, 1300), (0, 1300), (0, 1500), scale=True,
          label="turn 90 and double")
if isinstance(r, dict):
    check("90 degrees and factor 2",
          close(r.get("rotatedByDeg"), 90, 1e-9) and close(r.get("factor"), 2, 1e-12),
          str(r)[:220])
bb2 = bbox(hb)
check("PROVEN: vertical and 200 long — y 1300..1500, x 0..0",
      close((bb2.get("max") or {}).get("y"), 1500, 1e-6)
      and close((bb2.get("max") or {}).get("x"), 0, 1e-6), str(bb2)[:220])

print("\n== more than one entity moves as one rigid selection ==")
p1 = line(0, 1700, 100, 1700)
p2 = line(0, 1750, 100, 1750)
gap_before = 50
align([p1, p2], (0, 1700), (100, 1700), (500, 1700), (600, 1700),
      label="align both together")
g1, g2 = bbox(p1), bbox(p2)
check("PROVEN: both moved to x 500..600",
      close((g1.get("min") or {}).get("x"), 500, 1e-6)
      and close((g2.get("min") or {}).get("x"), 500, 1e-6), f"{g1} / {g2}")
check("PROVEN: and their 50 separation is intact — they did not move independently",
      close((g2.get("min") or {}).get("y", 0) - (g1.get("min") or {}).get("y", 0),
            gap_before, 1e-6), f"{g1} / {g2}")

print("\n-- refusals --")
hz = line(0, 1900, 100, 1900)
r = align(hz, (0, 1900), (0, 1900), (0, 1900), (100, 1900),
          label="a zero-length source pair is refused", expect_fail=True)
check("and the refusal says the pairs must define non-zero vectors",
      "non-zero" in str(r), str(r)[:250])
align(hz, (0, 1900), (100, 1900), (0, 1900), (0, 1900),
      label="a zero-length target pair is refused", expect_fail=True)
do("modify", "align", {"handles": [], "sourceA": {"x": 0, "y": 0}, "sourceB": {"x": 1, "y": 0},
                       "targetA": {"x": 0, "y": 0}, "targetB": {"x": 0, "y": 1}},
   label="an empty handle list is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "align.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -150, "yMin": -50, "xMax": 650, "yMax": 1950},
                            "widthPx": 1200, "heightPx": 2200})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: a VERTICAL line at the bottom (the 90 degree control), a horizontal line")
print("     to the LEFT of the origin (the 180 flip - if it sits to the right, the reversal")
print("     was skipped), a short line and a twice-as-long one above it, and near the top a")
print("     PAIR of parallel lines that moved together without changing their spacing.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
