# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.2 tranche 1 — jogged radius, oblique, dimension text.

All three change how a dimension LOOKS. The one thing none of them may change is what it
MEASURES, and that is invisible in a return code: a tool that moved the geometry instead of the
presentation would place a perfectly good dimension reporting a different number, and every
"did it work" check would pass.

So the measurement is read before and after in every case here, and the two tools that could
plausibly get it wrong are given a control:

* `dimension_jogged_radius` draws from a FALSE centre. The measurement must still be the real
  radius of the curve, not the distance from that false centre - which is what a naive
  implementation would report and which looks entirely reasonable on screen.
* `edit_dimension_text` has three AutoCAD conventions that are easy to get backwards: "" is not
  "blank" but "show the measurement", "<>" embeds it, and a single SPACE is what suppresses the
  text. Each is asserted separately, because a tool that treated "" as "clear" would look
  correct until someone read the sheet.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "dimensions", "view")}
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


print("== fresh drawing ==")
do("files", "new_document", {})

# ── dimension_jogged_radius ───────────────────────────────────────────────────
print("\n== dimension_jogged_radius on an arc of radius 400 ==")
arc = hnd(do("geometry-2d", "draw_arc",
             {"center": {"x": 0, "y": 0}, "radius": 400,
              "startAngleDeg": 60, "endAngleDeg": 120}, label="a 400 arc"))
r = do("dimensions", "dimension_jogged_radius",
       {"curveHandle": arc, "chordPoint": {"x": 0, "y": 400}})
jog = hnd(r)
if isinstance(r, dict):
    check("it is a RadialDimensionLarge, not a plain RadialDimension",
          r.get("type") == "RadialDimensionLarge", str(r)[:220])
    check("it reports the curve's radius as 400", close(r.get("radius"), 400, 1e-6), str(r)[:220])
    # THE claim. The dimension is DRAWN from a false centre; if the measurement followed that
    # false centre it would read about 200 here and look completely plausible on screen.
    check("PROVEN: the measurement is the REAL radius, not the distance from the false centre",
          close(r.get("measurement"), 400, 1e-6),
          f"got {r.get('measurement')} — a measurement near 200 means it measured from "
          f"overrideCenter instead")
    check("the false centre is NOT the real one",
          not close((r.get("overrideCenter") or [0])[1], 0, 1e-6), str(r)[:250])
    check("the jog angle defaulted to 45", close(r.get("jogAngleDeg"), 45, 1e-9), str(r)[:220])
    check("and it reports how far the jog sits off the leader",
          (r.get("jogOffset") or 0) > 1, f"got {r.get('jogOffset')}")

print("\n-- THE check that the first version of this file did not have --")
# Everything above passed on a version whose jog point was COLLINEAR with the centre-to-chord
# line. The entity was a RadialDimensionLarge, the measurement was 400, the false centre was
# not the real one - and it drew as a dead straight leader with no jog at all. Only the picture
# showed it, and only after zooming in far enough that a jog could not have hidden.
#
# So: measure the drawn width, against a PLAIN radial dimension on an identical arc as the
# control. A jogged dimension that does not bend is as wide as its own text and no wider.
ctrl_arc = hnd(do("geometry-2d", "draw_arc",
                  {"center": {"x": 2400, "y": 0}, "radius": 400,
                   "startAngleDeg": 60, "endAngleDeg": 120}, label="a control arc"))
ctrl = hnd(do("dimensions", "dimension_radial",
              {"curveHandle": ctrl_arc, "chordPoint": {"x": 2400, "y": 400},
               "leaderLength": 50}, label="a PLAIN radial dimension on it"))
w_jog = ((bbox(jog).get("max") or {}).get("x", 0)) - ((bbox(jog).get("min") or {}).get("x", 0))
w_ctrl = ((bbox(ctrl).get("max") or {}).get("x", 0)) - ((bbox(ctrl).get("min") or {}).get("x", 0))
check("PROVEN: the jogged dimension is drawn WIDER than a plain radial one",
      w_jog > w_ctrl * 3,
      f"jogged {round(w_jog, 3)} vs plain {round(w_ctrl, 3)} — a collinear jog measured 3.542 "
      f"here, which is the width of the text and nothing else")
check("PROVEN: and wide enough that the bend is a real one, not a rounding error",
      w_jog > 20, f"got {round(w_jog, 3)}")

print("\n-- an explicit jog angle --")
arc2 = hnd(do("geometry-2d", "draw_arc",
              {"center": {"x": 1200, "y": 0}, "radius": 400,
               "startAngleDeg": 60, "endAngleDeg": 120}, label="a second 400 arc"))
r = do("dimensions", "dimension_jogged_radius",
       {"curveHandle": arc2, "chordPoint": {"x": 1200, "y": 400}, "jogAngleDeg": 30})
if isinstance(r, dict):
    check("30 degrees was taken", close(r.get("jogAngleDeg"), 30, 1e-9), str(r)[:220])
    check("and the measurement is still 400", close(r.get("measurement"), 400, 1e-6), str(r)[:220])

print("\n-- refusals --")
r = do("dimensions", "dimension_jogged_radius",
       {"curveHandle": arc, "chordPoint": {"x": 0, "y": 400}, "jogAngleDeg": 0},
       label="a jog angle of 0 is refused", expect_fail=True)
check("and the refusal points at dimensions.radial",
      "dimensions.radial" in str(r), str(r)[:250])
do("dimensions", "dimension_jogged_radius",
   {"curveHandle": arc, "chordPoint": {"x": 0, "y": 400}, "jogAngleDeg": 180},
   label="180 degrees is refused too", expect_fail=True)
r = do("dimensions", "dimension_jogged_radius",
       {"curveHandle": arc, "chordPoint": {"x": 0, "y": 400},
        "jogPoint": {"x": 0, "y": 300}},
       label="a COLLINEAR jogPoint is refused - it would draw straight", expect_fail=True)
check("and the refusal says it would be indistinguishable from dimensions.radial",
      "dimensions.radial" in str(r), str(r)[:250])
ln = hnd(do("geometry-2d", "draw_line",
            {"start": {"x": 0, "y": -300}, "end": {"x": 100, "y": -300}}, label="a line"))
r = do("dimensions", "dimension_jogged_radius",
       {"curveHandle": ln, "chordPoint": {"x": 50, "y": -300}},
       label="a line has no radius, so it is refused", expect_fail=True)

# ── dimension_oblique ─────────────────────────────────────────────────────────
print("\n== dimension_oblique ==")
d1 = hnd(do("dimensions", "dimension_linear",
            {"p1": {"x": 0, "y": 1000}, "p2": {"x": 300, "y": 1000},
             "dimLinePoint": {"x": 150, "y": 1100}}, label="a 300 linear dimension"))
b_before = bbox(d1)
r = do("dimensions", "dimension_oblique", {"handles": [d1], "obliqueDeg": 60})
if isinstance(r, dict):
    check("one dimension was obliqued", r.get("affected") == 1, str(r)[:220])
    check("it reports the angle", close(r.get("obliqueDeg"), 60, 1e-9), str(r)[:220])
    dims = r.get("dimensions") or []
    # The control for this tool: obliquing is presentation. 300 must still be 300.
    check("PROVEN: the measurement is still 300 after leaning the extension lines",
          bool(dims) and close(dims[0].get("measurement"), 300, 1e-6),
          f"got {dims[0].get('measurement') if dims else None}")
b_after = bbox(d1)
# Leaning the extension lines has to move the entity's extent - otherwise nothing happened.
check("PROVEN: and the drawn extent DID change, so it is not a no-op",
      b_before != b_after, f"{b_before} -> {b_after}")

print("\n-- 0 straightens it again --")
r = do("dimensions", "dimension_oblique", {"handles": [d1], "obliqueDeg": 0})
b_reset = bbox(d1)
check("PROVEN: back to the extent it had before it was leaned",
      close((b_reset.get("min") or {}).get("x"), (b_before.get("min") or {}).get("x"), 1e-6)
      and close((b_reset.get("max") or {}).get("x"), (b_before.get("max") or {}).get("x"), 1e-6),
      f"{b_before} -> {b_after} -> {b_reset}")

print("\n-- an aligned dimension obliques too --")
d2 = hnd(do("dimensions", "dimension_aligned",
            {"p1": {"x": 600, "y": 1000}, "p2": {"x": 900, "y": 1200},
             "dimLinePoint": {"x": 750, "y": 1200}}, label="an aligned dimension"))
do("dimensions", "dimension_oblique", {"handles": [d2], "obliqueDeg": 75},
   label="aligned dimensions are accepted")

print("\n-- refusals --")
rad = hnd(do("dimensions", "dimension_radial",
             {"curveHandle": arc, "chordPoint": {"x": 0, "y": 400}, "leaderLength": 50},
             label="a radial dimension"))
r = do("dimensions", "dimension_oblique", {"handles": [rad], "obliqueDeg": 45},
       label="a radial dimension is refused - it has no extension lines", expect_fail=True)
check("and the refusal says why",
      "extension lines" in str(r), str(r)[:250])
do("dimensions", "dimension_oblique", {"handles": [d1]},
   label="a missing angle is refused", expect_fail=True)
do("dimensions", "dimension_oblique", {"handles": [], "obliqueDeg": 45},
   label="an empty handle list is refused", expect_fail=True)
r = do("dimensions", "dimension_oblique", {"handles": [ln], "obliqueDeg": 45},
       label="a plain line is refused by name", expect_fail=True)
check("and the refusal says it is not a Dimension", "not a Dimension" in str(r), str(r)[:250])

# ── edit_dimension_text ───────────────────────────────────────────────────────
print("\n== edit_dimension_text ==")
d3 = hnd(do("dimensions", "dimension_linear",
            {"p1": {"x": 0, "y": 1500}, "p2": {"x": 250, "y": 1500},
             "dimLinePoint": {"x": 125, "y": 1600}}, label="a 250 linear dimension"))

print("\n-- a plain override --")
r = do("dimensions", "edit_dimension_text", {"handle": d3, "text": "TYP"})
if isinstance(r, dict):
    check("the text is now TYP", r.get("text") == "TYP", str(r)[:220])
    # THE claim of this tool. An override changes what it SAYS, never what it measured.
    check("PROVEN: the measurement is untouched at 250",
          close(r.get("measurement"), 250, 1e-6), f"got {r.get('measurement')}")
    check("and it says the measurement is no longer displayed",
          r.get("displaysMeasurement") is False, str(r)[:220])

print("\n-- <> embeds the measurement rather than replacing it --")
r = do("dimensions", "edit_dimension_text", {"handle": d3, "text": "<> mm CLEAR"})
if isinstance(r, dict):
    check("the override is stored verbatim", r.get("text") == "<> mm CLEAR", str(r)[:220])
    check("PROVEN: and it is reported as still displaying the measurement",
          r.get("displaysMeasurement") is True, str(r)[:220])
    check("the measurement is still 250", close(r.get("measurement"), 250, 1e-6), str(r)[:220])

print("\n-- a single SPACE suppresses the text; \"\" does NOT --")
r = do("dimensions", "edit_dimension_text", {"handle": d3, "text": " "})
if isinstance(r, dict):
    check("PROVEN: a space is reported as suppressed", r.get("textSuppressed") is True,
          str(r)[:220])
    check("and NOT as displaying the measurement", r.get("displaysMeasurement") is False,
          str(r)[:220])
r = do("dimensions", "edit_dimension_text", {"handle": d3, "text": ""})
if isinstance(r, dict):
    # The trap. Passing "" to mean "clear it" gives the measurement back, not a blank dimension.
    check("PROVEN: an empty string RESTORES the measurement, it does not clear the text",
          r.get("displaysMeasurement") is True and r.get("textSuppressed") is False,
          str(r)[:250])

print("\n-- moving the text, and putting it back --")
r = do("dimensions", "edit_dimension_text",
       {"handle": d3, "textPosition": {"x": 400, "y": 1700}})
if isinstance(r, dict):
    check("it went where it was told",
          close((r.get("textPosition") or [0])[0], 400, 1e-6), str(r)[:220])
    check("and it is no longer at the style's default position",
          r.get("usingDefaultTextPosition") is False, str(r)[:220])
    check("the measurement survived the move", close(r.get("measurement"), 250, 1e-6),
          str(r)[:220])
r = do("dimensions", "edit_dimension_text", {"handle": d3, "resetPosition": True})
if isinstance(r, dict):
    check("PROVEN: resetPosition puts it back under the style's control",
          r.get("usingDefaultTextPosition") is True, str(r)[:220])

print("\n-- rotation --")
r = do("dimensions", "edit_dimension_text", {"handle": d3, "textRotationDeg": 30})
check("a rotated text still measures 250",
      isinstance(r, dict) and close(r.get("measurement"), 250, 1e-6), str(r)[:220])

print("\n-- refusals --")
do("dimensions", "edit_dimension_text", {"handle": d3},
   label="a call that changes nothing is refused", expect_fail=True)
r = do("dimensions", "edit_dimension_text",
       {"handle": d3, "resetPosition": True, "textPosition": {"x": 0, "y": 0}},
       label="resetPosition and textPosition together are refused", expect_fail=True)
check("and the refusal says they contradict each other",
      "contradict" in str(r), str(r)[:250])
do("dimensions", "edit_dimension_text", {"handle": ln, "text": "x"},
   label="a plain line is refused by name", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "dim-edit.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -300, "yMin": -400, "xMax": 1700, "yMax": 1800},
                            "widthPx": 1900, "heightPx": 2100})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm, and this is what the drawing shows once you zoom in far enough that a jog")
print("     could not hide: the jogged radius dimensions read R400 and their leaders BEND")
print("     sideways before meeting the arc. The control beside them - a plain radial on an")
print("     identical arc - runs dead straight, which is exactly what the jogged one looked")
print("     like until the collinear default was fixed. Also: a linear dimension with UPRIGHT")
print("     extension lines (leaned to 60 and straightened back), an aligned one visibly")
print("     SLANTED, and a dimension reading 250.")
print("     NOTE the text is ~2.5 units on a 400 arc, so it is invisible unless you zoom.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
