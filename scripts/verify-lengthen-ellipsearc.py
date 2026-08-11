# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — lengthen_curve and draw_ellipse_arc.

lengthen_curve has to work on curves whose parameter space is NOT length: an arc's parameter
is an angle, a spline's is neither. So it is tested on a Line, an Arc and a Polyline, and the
resulting length is measured with get_curve_length rather than taken from the tool's answer.
The tool already re-measures internally and refuses to report a length it did not achieve —
this checks that guard from outside, which is the only way to know the guard itself works.

draw_ellipse_arc carries a claim its own result cannot settle: that its angles are ELLIPSE
PARAMETERS, not bearings. On a ratio of 1 the two agree, so a comparison between ratio=1 and
ratio=0.5 is what shows the difference is real and not a rounding error.
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


def length_of(h):
    ok, r = S["geometry-2d"].call("get_curve_length", {"handle": h})
    return (r or {}).get("value") if ok else None


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return (r or {}).get("bbox") or {} if ok else {}


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


print("== fresh drawing ==")
do("files", "new_document", {})

# ── lengthen_curve ────────────────────────────────────────────────────────────
print("\n== lengthen_curve on a LINE ==")
ln = hnd(do("geometry-2d", "draw_line",
            {"start": {"x": 0, "y": 0}, "end": {"x": 100, "y": 0}}, label="a 100 line"))
r = do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "delta", "value": 50})
if isinstance(r, dict):
    check("reports the length before", close(r.get("lengthBefore"), 100), str(r)[:200])
    check("reports 150 after", close(r.get("length"), 150), str(r)[:200])
    check("reports what it changed by", close(r.get("changedBy"), 50), str(r)[:200])
check("MEASURED INDEPENDENTLY: the line really is 150", close(length_of(ln), 150, 1e-6),
      f"got {length_of(ln)}")
b = bbox(ln)
check("the START stayed at 0 by default", close((b.get("min") or {}).get("x"), 0), str(b)[:200])
check("and the END moved to 150", close((b.get("max") or {}).get("x"), 150), str(b)[:200])

print("\n-- atStart moves the other end --")
r = do("geometry-2d", "lengthen_curve",
       {"handle": ln, "mode": "delta", "value": 50, "atStart": True},
       label="add 50 at the start")
b = bbox(ln)
check("MEASURED: now 200 long", close(length_of(ln), 200, 1e-6), f"got {length_of(ln)}")
check("the start moved back to -50", close((b.get("min") or {}).get("x"), -50), str(b)[:200])
check("and the end stayed at 150", close((b.get("max") or {}).get("x"), 150), str(b)[:200])

print("\n-- total and percent --")
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "total", "value": 100},
   label="set the total to 100")
check("MEASURED: exactly 100", close(length_of(ln), 100, 1e-6), f"got {length_of(ln)}")
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "percent", "value": 150},
   label="then 150 percent of that")
check("MEASURED: 150", close(length_of(ln), 150, 1e-6), f"got {length_of(ln)}")

print("\n-- shortening --")
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "delta", "value": -100},
   label="take 100 off")
check("MEASURED: 50", close(length_of(ln), 50, 1e-6), f"got {length_of(ln)}")

print("\n== lengthen_curve on an ARC — whose parameter is an ANGLE, not a length ==")
arc = hnd(do("geometry-2d", "draw_arc",
             {"center": {"x": 0, "y": 300}, "radius": 100,
              "startAngleDeg": 0, "endAngleDeg": 90}, label="a quarter arc of radius 100"))
arc_len = length_of(arc)
check("the quarter arc is pi*r/2 long", close(arc_len, math.pi * 100 / 2, 1e-4),
      f"got {arc_len}")
r = do("geometry-2d", "lengthen_curve", {"handle": arc, "mode": "delta", "value": 50})
check("MEASURED: the arc grew by exactly 50",
      close(length_of(arc), (arc_len or 0) + 50, 1e-4), f"got {length_of(arc)}")

print("\n== lengthen_curve on a POLYLINE ==")
pl = hnd(do("geometry-2d", "draw_polyline",
            {"vertices": [{"x": 300, "y": 0}, {"x": 400, "y": 0}, {"x": 400, "y": 100}]},
            label="a 200-long polyline"))
pl_len = length_of(pl)
r = do("geometry-2d", "lengthen_curve", {"handle": pl, "mode": "total", "value": 250})
check("MEASURED: the polyline is 250", close(length_of(pl), 250, 1e-4), f"got {length_of(pl)}")

print("\n-- refusals --")
circ = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 600, "y": 0}, "radius": 40},
              label="a circle"))
r = do("geometry-2d", "lengthen_curve", {"handle": circ, "mode": "delta", "value": 10},
       label="a closed curve is refused", expect_fail=True)
check("and the refusal says it has no end", "no end" in str(r), str(r)[:220])
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "delta", "value": -1000},
   label="shortening past zero is refused", expect_fail=True)
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "delta", "value": 0},
   label="a no-op change is refused", expect_fail=True)
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "sideways", "value": 10},
   label="an unknown mode is refused", expect_fail=True)
do("geometry-2d", "lengthen_curve", {"handle": ln, "mode": "total"},
   label="a missing value is refused", expect_fail=True)

# ── draw_ellipse_arc ──────────────────────────────────────────────────────────
print("\n== draw_ellipse_arc ==")
r = do("geometry-2d", "draw_ellipse_arc",
       {"center": {"x": 0, "y": 600}, "majorAxis": {"x": 200, "y": 600},
        "ratio": 0.5, "startAngleDeg": 0, "endAngleDeg": 90})
ea = hnd(r)
if isinstance(r, dict):
    check("ratio reported back", close(r.get("ratio"), 0.5), str(r)[:220])
    check("major axis length is 200", close(r.get("majorLength"), 200), str(r)[:220])
    check("it is NOT closed - this is an arc", r.get("closed") is False, str(r)[:220])
    check("it has a length", (r.get("length") or 0) > 0, str(r)[:220])
    check("the note warns that angles are parameters, not bearings",
          "bearing" in (r.get("note") or "").lower(), str(r.get("note"))[:220])

b = bbox(ea)
# A quarter of an ellipse from parameter 0 to 90 spans the major radius in x and the minor in y.
check("PROVEN: it spans 200 in x, the major radius",
      close((b.get("max") or {}).get("x"), 200, 1e-4) and close((b.get("min") or {}).get("x"), 0, 1e-4),
      str(b)[:220])
check("PROVEN: and 100 in y, the MINOR radius — so the ratio was applied",
      close((b.get("max") or {}).get("y", 0) - (b.get("min") or {}).get("y", 0), 100, 1e-4),
      str(b)[:220])

print("\n-- ratio 1 makes a circular arc, where parameter and bearing agree --")
# The SAME major axis as the squashed one - 200 - or the comparison below says nothing about
# the ratio. An earlier version used 100 here and "proved" that a 200-major ellipse is longer
# than a 100-radius circle, which is true and irrelevant.
r = do("geometry-2d", "draw_ellipse_arc",
       {"center": {"x": 400, "y": 600}, "majorAxis": {"x": 600, "y": 600},
        "ratio": 1, "startAngleDeg": 0, "endAngleDeg": 90},
       label="a quarter CIRCULAR arc of the same major axis")
ca = hnd(r)
check("PROVEN: its length is pi*r/2, the circular quarter",
      close(length_of(ca), math.pi * 200 / 2, 1e-3), f"got {length_of(ca)}")
check("PROVEN: at the same major axis the squashed arc is SHORTER — the ratio is real",
      (length_of(ea) or 0) < (length_of(ca) or 0),
      f"squashed {length_of(ea)} vs circular {length_of(ca)}")

print("\n-- refusals --")
do("geometry-2d", "draw_ellipse_arc",
   {"center": {"x": 0, "y": 0}, "majorAxis": {"x": 100, "y": 0}, "ratio": 0.5},
   label="missing angles are refused - that would be a full ellipse", expect_fail=True)
r = do("geometry-2d", "draw_ellipse_arc",
       {"center": {"x": 0, "y": 0}, "majorAxis": {"x": 0, "y": 0}, "ratio": 0.5,
        "startAngleDeg": 0, "endAngleDeg": 90},
       label="a zero-length major axis is refused", expect_fail=True)
check("and the refusal explains majorAxis is a POINT, not a length",
      "END POINT" in str(r), str(r)[:250])
do("geometry-2d", "draw_ellipse_arc",
   {"center": {"x": 0, "y": 0}, "majorAxis": {"x": 100, "y": 0}, "ratio": 2,
    "startAngleDeg": 0, "endAngleDeg": 90},
   label="a ratio above 1 is refused", expect_fail=True)
do("geometry-2d", "draw_ellipse_arc",
   {"center": {"x": 0, "y": 0}, "majorAxis": {"x": 100, "y": 0}, "ratio": 0.5,
    "startAngleDeg": 45, "endAngleDeg": 45},
   label="equal start and end angles are refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "lengthen-ellipsearc.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -100, "yMin": -60, "xMax": 700, "yMax": 760},
                            "widthPx": 1500, "heightPx": 1500})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: a short line near the origin, a more-than-quarter arc, an L polyline,")
print("     a circle, and TWO arcs top right of which the left one is visibly flatter.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
