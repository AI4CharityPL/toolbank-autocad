# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — edit_mline_vertex and mline_join.

A multiline is drawn from a STYLE: the vertices are a spine, and every element is offset from
it. That is why this needs looking at as well as counting.

* Moving a vertex recomputes both segments meeting at that corner. A vertex count that still
  reads 3 says nothing about whether the elements followed.
* Joining refuses a mismatch in style, scale or justification. Each of those decides where the
  elements sit relative to the spine, so a join across one produces a single wall that changes
  thickness at an invisible seam — a result whose JSON looks perfect.

The join is also checked in BOTH directions, because appending blind would leave a multiline
that doubles back on itself and still reports a sensible vertex count.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "styles", "view")}
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


def mline(verts, style=None, scale=None, just=None, layer=None):
    args = {"vertices": [{"x": x, "y": y} for x, y in verts]}
    if style: args["style"] = style
    if scale is not None: args["scale"] = scale
    if just: args["justification"] = just
    if layer: args["layer"] = layer
    return hnd(S["geometry-2d"].call("draw_mline", args)[1])


print("== fresh drawing ==")
do("files", "new_document", {})

print("\n== a 200mm wall style to draw with ==")
do("styles", "create_mlinestyle", {
    "name": "ACADMCP-W200",
    "elements": [{"offset": 100, "colorIndex": 1}, {"offset": -100, "colorIndex": 1}],
    "description": "200 wall", "showMiters": True})

# ── edit_mline_vertex ─────────────────────────────────────────────────────────
print("\n== edit_mline_vertex ==")
w = mline([(0, 0), (300, 0), (300, 200)], style="ACADMCP-W200")
check("a 3-vertex wall was drawn", bool(w), f"handle {w}")
b0 = bbox(w)

r = do("geometry-2d", "edit_mline_vertex", {"handle": w, "index": 1, "point": {"x": 500, "y": 0}})
if isinstance(r, dict):
    check("reports the vertex as it was", close((r.get("before") or [0])[0], 300), str(r)[:220])
    check("reports it where it now is", close((r.get("point") or [0])[0], 500), str(r)[:220])
    check("still 3 vertices", r.get("vertices") == 3, str(r)[:220])
    check("the note explains both segments change",
          "both segments" in (r.get("note") or ""), str(r.get("note"))[:200])

b1 = bbox(w)
# A vertex count of 3 would be unchanged whether the geometry moved or not. The extent is not.
# NOT an exact figure. The wall runs (0,0) -> (500,0) -> (300,200), so it turns back on
# itself at an acute angle and the MITRE at that corner reaches well past the vertex - measured
# at 741.4, where an earlier version of this check expected 600 by adding half the wall width
# and forgetting mitring entirely. The tool was right and the expectation was wrong.
#
# What is defensible: the extent must now reach past the vertex the corner was moved to.
check("PROVEN: the wall's extent reaches past the vertex's new x of 500",
      (b1.get("max") or {}).get("x", 0) > 500,
      f"{b1} — the corner moved to x=500, so the mitred outer face must be beyond it")
check("PROVEN: and it is wider than before the move",
      (b1.get("max") or {}).get("x", 0) > (b0.get("max") or {}).get("x", 0),
      f"{b0} -> {b1}")

print("\n-- refusals --")
do("geometry-2d", "edit_mline_vertex", {"handle": w, "index": 99, "point": {"x": 0, "y": 0}},
   label="an out-of-range index is refused", expect_fail=True)
do("geometry-2d", "edit_mline_vertex", {"handle": w, "index": 0},
   label="a missing point is refused", expect_fail=True)
pl = hnd(do("geometry-2d", "draw_polyline",
            {"vertices": [{"x": 0, "y": 900}, {"x": 100, "y": 900}]}, label="a polyline"))
r = do("geometry-2d", "edit_mline_vertex", {"handle": pl, "index": 0, "point": {"x": 0, "y": 0}},
       label="a polyline is refused by name", expect_fail=True)
check("and the refusal points at edit_polyline_vertex",
      "edit_polyline_vertex" in str(r), str(r)[:220])

# ── mline_join ────────────────────────────────────────────────────────────────
print("\n== mline_join, forward: A ends where B starts ==")
a1 = mline([(0, 400), (200, 400)], style="ACADMCP-W200")
a2 = mline([(200, 400), (200, 600)], style="ACADMCP-W200")
r = do("geometry-2d", "mline_join", {"handle1": a1, "handle2": a2})
if isinstance(r, dict):
    check("it went forward", r.get("direction") == "forward", str(r)[:220])
    check("2 + 2 vertices become 3, not 4 — the shared one is not duplicated",
          r.get("vertices") == 3, f"got {r.get('vertices')} from {r.get('verticesBefore')}")
    check("it names the point they met at", close((r.get("joinedAt") or [0])[0], 200), str(r)[:220])
do("geometry-2d", "get_entity", {"handle": a2},
   label="PROVEN: the second multiline is erased", expect_fail=True)
do("geometry-2d", "get_entity", {"handle": a1}, label="and the first survives")

ab = bbox(a1)
# The claim a join can quietly break: that the result is still a WALL and not a bare spine.
# Measured on this drawing - a styled L occupies x 0..300 (the elements sit 100 either side and
# the 90 degree corner mitres out to 300), while the same L on the STANDARD style occupies
# x 0..200.5, because STANDARD is 1 unit wide. So this separates a 200 wall from a lost style
# by 100 units, not by eye.
check("PROVEN: the joined wall is still 200 wide, so the style survived the join",
      close((ab.get("max") or {}).get("x"), 300, 1e-6)
      and close((ab.get("min") or {}).get("y"), 300, 1e-6),
      f"{ab} — a spine that lost its elements stops at x=200.5, y=399.5")

print("\n== mline_join, reversed: A's end meets B's END ==")
b1h = mline([(0, 700), (200, 700)], style="ACADMCP-W200")
b2h = mline([(200, 900), (200, 700)], style="ACADMCP-W200")
r = do("geometry-2d", "mline_join", {"handle1": b1h, "handle2": b2h})
if isinstance(r, dict):
    check("it noticed and went reversed", r.get("direction") == "reversed", str(r)[:220])
    check("again 3 vertices", r.get("vertices") == 3, str(r)[:220])
bb = bbox(b1h)
# If it had appended blind, the wall would double back to x=200,y=900 AND still read 3 vertices.
check("PROVEN: the joined wall reaches y=900, so B was appended the right way round",
      close((bb.get("max") or {}).get("y", 0), 900, 1e-6),
      f"{bb} — appending blind would fold the wall back on itself")
check("PROVEN: and this one is still 200 wide too",
      close((bb.get("max") or {}).get("x"), 300, 1e-6)
      and close((bb.get("min") or {}).get("y"), 600, 1e-6),
      f"{bb} — a lost style would stop at x=200.5")

print("\n== a mismatch is refused rather than silently seamed ==")
do("styles", "create_mlinestyle", {
    "name": "ACADMCP-W400",
    "elements": [{"offset": 200, "colorIndex": 3}, {"offset": -200, "colorIndex": 3}],
    "description": "400 wall", "showMiters": True}, label="a second, wider style")
c1 = mline([(0, 1100), (200, 1100)], style="ACADMCP-W200")
c2 = mline([(200, 1100), (400, 1100)], style="ACADMCP-W400")
r = do("geometry-2d", "mline_join", {"handle1": c1, "handle2": c2},
       label="different STYLES are refused", expect_fail=True)
check("and the refusal explains the seam",
      "element offsets" in str(r) or "different styles" in str(r), str(r)[:250])

d1 = mline([(0, 1300), (200, 1300)], style="ACADMCP-W200", scale=1)
d2 = mline([(200, 1300), (400, 1300)], style="ACADMCP-W200", scale=2)
r = do("geometry-2d", "mline_join", {"handle1": d1, "handle2": d2},
       label="different SCALES are refused", expect_fail=True)
check("and the refusal says the width would change",
      "width" in str(r), str(r)[:250])

e1 = mline([(0, 1500), (200, 1500)], style="ACADMCP-W200", just="zero")
e2 = mline([(200, 1500), (400, 1500)], style="ACADMCP-W200", just="top")
r = do("geometry-2d", "mline_join", {"handle1": e1, "handle2": e2},
       label="different JUSTIFICATION is refused", expect_fail=True)
check("and the refusal says it would step sideways",
      "sideways" in str(r), str(r)[:250])

print("\n-- ends that do not meet --")
f1 = mline([(0, 1700), (200, 1700)], style="ACADMCP-W200")
f2 = mline([(500, 1700), (700, 1700)], style="ACADMCP-W200")
r = do("geometry-2d", "mline_join", {"handle1": f1, "handle2": f2},
       label="a 300 gap is refused", expect_fail=True)
check("and the refusal names both candidate ends and the tolerance",
      "tolerance" in str(r), str(r)[:250])
do("geometry-2d", "mline_join", {"handle1": f1, "handle2": f2, "tolerance": 400},
   label="but a big enough tolerance lets it through")

do("geometry-2d", "mline_join", {"handle1": a1, "handle2": a1},
   label="joining a multiline to itself is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "mline-edit.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -100, "yMin": -150, "xMax": 800, "yMax": 1850},
                            "widthPx": 1200, "heightPx": 2200})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: every wall is drawn as TWO parallel lines with mitred corners, the joined")
print("     ones turn a clean corner with no doubled-back stub, and none has collapsed to a")
print("     single line - which is what a lost style looks like.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
