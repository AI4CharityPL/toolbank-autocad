# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — breaking and dividing curves.

Four tools: break_at_point, break_between_points, divide_object, measure_object.

Two things here cannot be settled by a return code:

* **The break tools ERASE the original.** A tool that reported two pieces while leaving the
  original in place would look identical in JSON, so the old handle is asked for again
  afterwards and must be gone.
* **Lengths have to add up.** Two pieces whose lengths sum to the original prove the split
  happened where it was asked for; a marker count alone would not.

Everything is also put on screen, because a marker placed at the right distance along the
wrong curve, or a block rotated square to the world instead of following the curve, produces
the same numbers as a correct one.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "view", "selection")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    # An expected failure must be the failure that was expected. Without this, every
    # `expect_fail` check passes when the tool is not registered at all — which is exactly what
    # happened on the first run of this script: four tools were missing from the plugin and the
    # refusal tests reported OK, because "UnknownTool" is a failure too.
    missing = "UnknownTool" in str(r) or "no tool registered" in str(r)
    if missing:
        good = False
    else:
        good = (not ok) if expect_fail else ok
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


def draw_line(x1, y1, x2, y2):
    r = S["geometry-2d"].call("draw_line", {"start": {"x": x1, "y": y1},
                                            "end": {"x": x2, "y": y2}})[1]
    return ((r or {}).get("entity") or {}).get("handle")


print("== fresh drawing ==")
do("files", "new_document", {})

# ── break_at_point ────────────────────────────────────────────────────────────
print("\n== break_at_point ==")
h = draw_line(0, 0, 100, 0)
r = do("geometry-2d", "break_at_point", {"handle": h, "point": {"x": 30, "y": 0}})
if isinstance(r, dict):
    check("two pieces", r.get("count") == 2, str(r)[:220])
    check("reports the original length", close(r.get("lengthBefore", 0), 100), str(r)[:220])
    lens = sorted(p.get("length", 0) for p in r.get("pieces") or [])
    check("the pieces are 30 and 70", [round(x, 6) for x in lens] == [30.0, 70.0], str(lens))
    check("their lengths sum to the original",
          close(sum(p.get("length", 0) for p in r.get("pieces") or []), 100), str(lens))
    check("the break landed where asked", close((r.get("brokenAt") or [0])[0], 30), str(r)[:220])
    check("offset from the requested point is zero",
          close(r.get("offsetFromRequested", 1), 0), str(r)[:220])

# The original must be GONE. A tool that split a copy and left the original would report
# exactly the same thing.
r = do("geometry-2d", "get_entity", {"handle": h},
       label="the ORIGINAL handle is gone", expect_fail=True)

print("\n-- a point off the curve is snapped onto it, and the distance reported --")
h2 = draw_line(0, 50, 100, 50)
r = do("geometry-2d", "break_at_point", {"handle": h2, "point": {"x": 40, "y": 57}},
       label="break near, not on, the line")
if isinstance(r, dict):
    check("snapped onto the curve at y=50", close((r.get("brokenAt") or [0, 1])[1], 50),
          str(r.get("brokenAt")))
    check("and reported the 7-unit offset", close(r.get("offsetFromRequested", 0), 7),
          f"got {r.get('offsetFromRequested')}")

print("\n-- breaking at an endpoint is refused, not silently a no-op --")
h3 = draw_line(0, 100, 100, 100)
r = do("geometry-2d", "break_at_point", {"handle": h3, "point": {"x": 0, "y": 100}},
       label="break exactly at the start point", expect_fail=True)
do("geometry-2d", "get_entity", {"handle": h3}, label="and the line still exists")

print("\n-- a closed curve is refused with a reason --")
r = do("geometry-2d", "draw_circle", {"center": {"x": 300, "y": 0}, "radius": 40})
ch = ((r or {}).get("entity") or {}).get("handle")
r = do("geometry-2d", "break_at_point", {"handle": ch, "point": {"x": 340, "y": 0}},
       label="breaking a circle at one point is refused", expect_fail=True)
check("and the refusal points at break_between_points",
      "break_between_points" in str(r), str(r)[:220])

# ── break_between_points ──────────────────────────────────────────────────────
print("\n== break_between_points ==")
h4 = draw_line(0, 150, 100, 150)
r = do("geometry-2d", "break_between_points",
       {"handle": h4, "point1": {"x": 30, "y": 150}, "point2": {"x": 70, "y": 150}})
if isinstance(r, dict):
    check("two pieces remain", r.get("count") == 2, str(r)[:220])
    check("40 units were removed", close(r.get("removedLength", 0), 40), str(r)[:220])
    lens = sorted(p.get("length", 0) for p in r.get("pieces") or [])
    check("the remaining pieces are 30 and 30",
          [round(x, 6) for x in lens] == [30.0, 30.0], str(lens))
    check("kept + removed equals the original",
          close(sum(p.get("length", 0) for p in r.get("pieces") or []) + r.get("removedLength", 0),
                r.get("lengthBefore", 0)), str(r)[:220])
do("geometry-2d", "get_entity", {"handle": h4},
   label="the ORIGINAL handle is gone", expect_fail=True)

print("\n-- the points may be given in either order --")
h5 = draw_line(0, 200, 100, 200)
r = do("geometry-2d", "break_between_points",
       {"handle": h5, "point1": {"x": 80, "y": 200}, "point2": {"x": 20, "y": 200}},
       label="point1 after point2 along the curve")
if isinstance(r, dict):
    check("still removes the middle 60", close(r.get("removedLength", 0), 60), str(r)[:220])

print("\n-- two identical points are refused --")
h6 = draw_line(0, 250, 100, 250)
r = do("geometry-2d", "break_between_points",
       {"handle": h6, "point1": {"x": 50, "y": 250}, "point2": {"x": 50, "y": 250}},
       label="both points the same", expect_fail=True)
check("and the refusal points at break_at_point", "break_at_point" in str(r), str(r)[:220])

print("\n-- a circle is refused, with the ambiguity explained --")
r = do("geometry-2d", "break_between_points",
       {"handle": ch, "point1": {"x": 340, "y": 0}, "point2": {"x": 300, "y": 40}},
       label="breaking a circle between two points is refused", expect_fail=True)
check("and the refusal explains WHY rather than just saying no",
      "direction" in str(r), str(r)[:250])

# ── divide_object ─────────────────────────────────────────────────────────────
print("\n== divide_object ==")
h7 = draw_line(0, 300, 100, 300)
r = do("geometry-2d", "divide_object", {"handle": h7, "segments": 5})
if isinstance(r, dict):
    check("5 segments produce 4 markers", r.get("count") == 4, str(r)[:220])
    check("segment length is 20", close(r.get("segmentLength", 0), 20), str(r)[:220])
    check("markers are points", r.get("placed") == "points", str(r)[:220])
    ds = [m.get("distance") for m in r.get("markers") or []]
    check("markers sit at 20/40/60/80", [round(d, 6) for d in ds] == [20.0, 40.0, 60.0, 80.0],
          str(ds))
    xs = [round((m.get("point") or [0])[0], 6) for m in r.get("markers") or []]
    check("and their x coordinates match", xs == [20.0, 40.0, 60.0, 80.0], str(xs))
do("geometry-2d", "get_entity", {"handle": h7}, label="the curve itself SURVIVES a divide")

do("geometry-2d", "divide_object", {"handle": h7, "segments": 1},
   label="dividing into 1 is refused", expect_fail=True)
do("geometry-2d", "divide_object", {"handle": h7, "block": "NO-SUCH-BLOCK", "segments": 3},
   label="an unknown block is refused", expect_fail=True)

# ── measure_object ────────────────────────────────────────────────────────────
print("\n== measure_object ==")
h8 = draw_line(0, 350, 100, 350)
r = do("geometry-2d", "measure_object", {"handle": h8, "distance": 30})
if isinstance(r, dict):
    check("3 markers for 30 along 100", r.get("count") == 3, str(r)[:220])
    ds = [m.get("distance") for m in r.get("markers") or []]
    check("markers at 30/60/90", [round(d, 6) for d in ds] == [30.0, 60.0, 90.0], str(ds))
    check("the 10-unit remainder is reported", close(r.get("remainder", 0), 10),
          f"got {r.get('remainder')}")
    check("the note explains how this differs from divide_object",
          "divide_object" in (r.get("note") or ""), str(r.get("note"))[:200])

do("geometry-2d", "measure_object", {"handle": h8, "distance": 500},
   label="an interval longer than the curve is refused", expect_fail=True)
do("geometry-2d", "measure_object", {"handle": h8, "distance": 0},
   label="a zero interval is refused", expect_fail=True)

# ── set_point_style ───────────────────────────────────────────────────────────
print("\n== set_point_style: without it the markers above are invisible ==")
# Found by looking at the first PNG: the lines were there, the marker counts were right, and
# nothing was on screen. DBPoints at AutoCAD's default PDMODE of 0 draw as a single pixel.
r = do("geometry-2d", "set_point_style", {"mode": "circleX", "size": 4})
if isinstance(r, dict):
    check("pdmode is now 35 (circle + X)", r.get("pdmode") == 35, str(r)[:220])
    check("reports what it was before", r.get("beforePdmode") is not None, str(r)[:220])
    check("pdsize took the value given", close(r.get("pdsize", 0), 4), str(r)[:220])
    check("the note warns it is drawing-wide", "DRAWING-WIDE" in (r.get("note") or ""),
          str(r.get("note"))[:200])
do("geometry-2d", "set_point_style", {"mode": "no-such-style"},
   label="an unknown style name is refused", expect_fail=True)
do("geometry-2d", "set_point_style", {},
   label="giving neither mode nor pdmode is refused", expect_fail=True)

# ─── on screen ───
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "break-divide.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -30, "yMin": -60, "xMax": 380, "yMax": 390},
                            "widthPx": 1500, "heightPx": 1500})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
