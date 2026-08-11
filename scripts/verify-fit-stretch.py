# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — fit_polyline and stretch_window.

Both tools have a failure mode that produces a perfectly reasonable-looking result:

* **fit_polyline's two modes are two different curves.** `fit` runs THROUGH the vertices;
  `spline` treats them as control points and touches only the two ends. Swap them and you still
  get a smooth curve, just not the one asked for. The only thing that tells them apart from
  outside is the distance from the original vertices, so that is measured for both — near zero
  for one and demonstrably large for the other. A test that only checked "a curve came back"
  would pass on either.

* **stretch_window has to leave things alone.** A stretch that moved every entity the window
  touches would pass any check that only looks at what moved. So the decisive case here is a
  line the window crosses with NEITHER endpoint inside: it must not move at all, and its bbox
  is read before and after to prove it.

The closed-polyline case is checked by LENGTH rather than by vertex count, because a fit that
silently dropped the closing segment would still return a valid spline with the right number of
fit points.
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


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return (r or {}).get("bbox") or {} if ok else {}


def length_of(h):
    ok, r = S["geometry-2d"].call("get_curve_length", {"handle": h})
    return (r or {}).get("value") if ok else None


def dist_to(h, x, y):
    ok, r = S["geometry-2d"].call("get_distance_to_entity",
                                  {"handle": h, "point": {"x": x, "y": y}})
    return (r or {}).get("value") if ok else None


def on_curve(d, tol=1e-4):
    # Written out rather than `d < tol or ...` because 0.0 is falsy and a correct answer of 0
    # must not fall through to whatever comes next.
    return d is not None and abs(d) <= tol


def poly(verts, closed=False, layer=None):
    args = {"vertices": [{"x": x, "y": y} for x, y in verts]}
    if closed: args["closed"] = True
    if layer: args["layer"] = layer
    return hnd(S["geometry-2d"].call("draw_polyline", args)[1])


print("== fresh drawing ==")
do("files", "new_document", {})

# ── fit_polyline ──────────────────────────────────────────────────────────────
# A zig-zag, so the smoothed curve is visibly different from the polyline and the interior
# vertices are far from any straight line between the ends.
ZIG = [(0, 0), (50, 80), (100, 0), (150, 80), (200, 0)]

print("\n== fit_polyline, mode='fit' — the curve must pass THROUGH every vertex ==")
p1 = poly(ZIG)
len_before = length_of(p1)
r = do("geometry-2d", "fit_polyline", {"handle": p1, "mode": "fit", "keepOriginal": True})
f_fit = hnd(r)
if isinstance(r, dict):
    check("it is a Spline", r.get("type") == "Spline", str(r)[:220])
    check("mode reported back as fit", r.get("mode") == "fit", str(r)[:220])
    check("it counted the 5 source vertices", r.get("verticesBefore") == 5, str(r)[:220])
    check("it reports the length before", close(r.get("lengthBefore"), len_before, 1e-6),
          f"{r.get('lengthBefore')} vs measured {len_before}")
    # NOT `(x or 1) < 1e-6`: a perfect fit measures exactly 0, which is falsy, and the `or`
    # would replace the right answer with a sentinel that fails.
    _d = r.get("maxDistanceFromOriginalVertices")
    check("PROVEN by the tool: max distance from the original vertices is ~0",
          _d is not None and abs(_d) < 1e-6, f"got {_d!r}")

print("\n-- and MEASURED INDEPENDENTLY, vertex by vertex --")
for (x, y) in ZIG:
    d = dist_to(f_fit, x, y)
    check(f"({x},{y}) lies on the fitted curve", on_curve(d), f"distance {d}")

print("\n== fit_polyline, mode='spline' — the SAME points as control vertices ==")
p2 = poly(ZIG)
r = do("geometry-2d", "fit_polyline", {"handle": p2, "mode": "spline", "keepOriginal": True})
f_cv = hnd(r)
if isinstance(r, dict):
    check("mode reported back as spline", r.get("mode") == "spline", str(r)[:220])
    check("degree defaulted to 3", r.get("degree") == 3, str(r)[:220])
    # THE control. If this came back ~0 the two modes would be the same tool twice.
    check("PROVEN: it does NOT pass through the vertices — the distance is large",
          (r.get("maxDistanceFromOriginalVertices") or 0) > 1.0,
          f"got {r.get('maxDistanceFromOriginalVertices')} — near zero would mean "
          f"mode='spline' quietly did a fit")

print("\n-- the ends are clamped, the middle is not --")
check("PROVEN: the first vertex IS on the curve", on_curve(dist_to(f_cv, *ZIG[0])),
      f"distance {dist_to(f_cv, *ZIG[0])}")
check("PROVEN: the last vertex IS on the curve", on_curve(dist_to(f_cv, *ZIG[-1])),
      f"distance {dist_to(f_cv, *ZIG[-1])}")
d_mid = dist_to(f_cv, *ZIG[2])
check("PROVEN: the MIDDLE vertex is not — the curve is only pulled towards it",
      d_mid is not None and d_mid > 1.0,
      f"distance {d_mid} — a clamped CV spline touches only its two end points")

print("\n-- the two modes really are different curves --")
l_fit, l_cv = length_of(f_fit), length_of(f_cv)
check("PROVEN: the control-vertex curve is SHORTER, it cuts the corners",
      l_cv is not None and l_fit is not None and l_cv < l_fit,
      f"fit {l_fit} vs spline {l_cv}")

print("\n== output='polyline' converts back, and says what that costs ==")
p3 = poly(ZIG)
r = do("geometry-2d", "fit_polyline", {"handle": p3, "mode": "fit", "output": "polyline"})
f_pl = hnd(r)
if isinstance(r, dict):
    check("it is a Polyline now", r.get("type") == "Polyline", str(r)[:220])
    check("and it reports the approximation error rather than claiming exactness",
          r.get("approximationError") is not None, str(r)[:220])
    # Written out rather than `(x or 99) < 1.0`: the measured answer here is exactly 0.0, which
    # is falsy, so the `or` would have thrown the real value away and replaced it with a
    # failure. That is the same trap this file's on_curve() exists to avoid.
    ae = r.get("approximationError")
    check("which measures 0 — ToPolyline puts its vertices ON the fit points",
          ae is not None and 0 <= ae < 1.0, f"got {ae!r}")
do("geometry-2d", "get_entity", {"handle": p3},
   label="PROVEN: the source polyline was erased (keepOriginal defaults to false)",
   expect_fail=True)
do("geometry-2d", "get_entity", {"handle": p1},
   label="and with keepOriginal it survives")

print("\n== a CLOSED polyline keeps its closing segment ==")
# A 100 square: perimeter 400. A smooth curve through its four corners bulges outside them,
# so it is LONGER than 400 - the circle through those corners is 444. A fit that dropped the
# implied closing segment would come back around 330 and still look like a valid spline.
sq = poly([(0, 300), (100, 300), (100, 400), (0, 400)], closed=True)
r = do("geometry-2d", "fit_polyline", {"handle": sq, "mode": "fit"})
f_sq = hnd(r)
if isinstance(r, dict):
    check("it noticed the source was closed", r.get("sourceClosed") is True, str(r)[:220])
l_sq = length_of(f_sq)
check("PROVEN: the fitted curve is longer than the 400 perimeter, so no segment was lost",
      l_sq is not None and l_sq > 400,
      f"got {l_sq} — dropping the closing segment would give roughly 330")
check("and it is not absurdly long either", l_sq is not None and l_sq < 600, f"got {l_sq}")

print("\n== arcs in the source are DISCARDED, and said so ==")
# A fit runs through the vertices only. Silently throwing away the bulges would change the
# shape between them and look like a smoothing.
#
# The bulge has to be applied with edit_polyline_vertex. draw_polyline takes {x, y} and
# NOTHING else, so an earlier version of this that passed {"x":…, "y":…, "bulge":1.0} had the
# bulge silently dropped and then checked that arcs were counted on a polyline with no arcs.
# The check failed, which is the only reason the mistake was found - so the bulge is now
# proved to have landed BEFORE it is relied on.
arcpl = poly([(0, 600), (100, 600), (200, 600), (300, 700)])
straight_len = length_of(arcpl)
do("geometry-2d", "edit_polyline_vertex", {"handle": arcpl, "index": 1, "bulge": 1.0},
   label="put a bulge on segment 1 so there is a real arc to discard")
bulged_len = length_of(arcpl)
# A bulge of 1 turns the 100-long segment into a semicircle: pi*50 = 157.08, so the polyline
# grows by 57.08. If the bulge had been ignored the length would not have moved at all.
check("PROVEN: the bulge landed — the polyline grew from 100 to a pi*50 semicircle",
      bulged_len is not None and straight_len is not None
      and close(bulged_len - straight_len, math.pi * 50 - 100, 1e-4),
      f"{straight_len} -> {bulged_len}")

r = do("geometry-2d", "fit_polyline", {"handle": arcpl, "mode": "fit"})
if isinstance(r, dict):
    check("it counted the arc segment it is about to discard",
          (r.get("arcSegmentsDiscarded") or 0) >= 1, str(r)[:250])
    check("and the note says the shape between those vertices has changed",
          "arcs are gone" in (r.get("note") or ""), str(r.get("note"))[:250])

print("\n-- refusals --")
two = poly([(0, 800), (100, 800)])
r = do("geometry-2d", "fit_polyline", {"handle": two},
       label="a 2-vertex polyline is refused", expect_fail=True)
check("and the refusal says there is nothing between them to smooth",
      "nothing between them" in str(r), str(r)[:250])
circ = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 500, "y": 800}, "radius": 40},
              label="a circle"))
r = do("geometry-2d", "fit_polyline", {"handle": circ},
       label="a circle is refused by name", expect_fail=True)
do("geometry-2d", "fit_polyline", {"handle": poly(ZIG), "mode": "wobbly"},
   label="an unknown mode is refused", expect_fail=True)
do("geometry-2d", "fit_polyline", {"handle": poly(ZIG), "output": "dxf"},
   label="an unknown output is refused", expect_fail=True)
r = do("geometry-2d", "fit_polyline",
       {"handle": poly([(0, 900), (50, 950), (100, 900)]), "mode": "spline", "degree": 5},
       label="degree 5 with only 3 control points is refused", expect_fail=True)
check("and the refusal offers mode='fit', which has no such limit",
      "mode='fit'" in str(r), str(r)[:250])

# ── stretch_window ────────────────────────────────────────────────────────────
print("\n== stretch_window: a room widened by dragging its right-hand wall ==")
# (0,1000) (200,1000) (200,1100) (0,1100). The window catches ONLY the two right-hand
# vertices, so the right wall moves and the left one stays attached to it.
room = poly([(0, 1000), (200, 1000), (200, 1100), (0, 1100)], closed=True)
b_before = bbox(room)
r = do("geometry-2d", "stretch_window", {
    "corner1": {"x": 150, "y": 950}, "corner2": {"x": 250, "y": 1150},
    "displacement": {"x": 100, "y": 0}})
if isinstance(r, dict):
    ch = [c for c in (r.get("changed") or []) if c.get("handle") == room]
    check("the room is listed as changed", len(ch) == 1, str(r)[:250])
    if ch:
        check("2 of its 4 points moved — this is a stretch, not a move",
              ch[0].get("pointsMoved") == 2 and ch[0].get("pointsTotal") == 4, str(ch[0])[:220])
        check("and it is NOT reported as moved whole", ch[0].get("movedWhole") is False,
              str(ch[0])[:220])
b_after = bbox(room)
check("PROVEN: the right wall moved out to 300",
      close((b_after.get("max") or {}).get("x"), 300, 1e-6), f"{b_before} -> {b_after}")
check("PROVEN: and the left wall stayed at 0 — the room got wider, it did not slide",
      close((b_after.get("min") or {}).get("x"), 0, 1e-6), f"{b_before} -> {b_after}")

print("\n== the decisive case: crossed by the window, but no vertex inside ==")
# A long line straight through the window with both ends well outside. STRETCH leaves it
# alone. A tool that moved everything the window touches would move this and pass every
# other check in this file.
thru = hnd(do("geometry-2d", "draw_line",
              {"start": {"x": -200, "y": 1300}, "end": {"x": 400, "y": 1300}},
              label="a line running right through the window"))
t_before = bbox(thru)
r = do("geometry-2d", "stretch_window", {
    "corner1": {"x": 0, "y": 1250}, "corner2": {"x": 200, "y": 1350},
    "displacement": {"x": 0, "y": 500}})
t_after = bbox(thru)
check("PROVEN: it did not move — neither endpoint was inside",
      close((t_after.get("min") or {}).get("x"), (t_before.get("min") or {}).get("x"), 1e-9)
      and close((t_after.get("min") or {}).get("y"), (t_before.get("min") or {}).get("y"), 1e-9),
      f"{t_before} -> {t_after} — a window that moves what it merely crosses is a move, not a stretch")
if isinstance(r, dict):
    check("and it is not in the changed list",
          all(c.get("handle") != thru for c in (r.get("changed") or [])), str(r)[:250])

print("\n== an entity entirely inside moves whole ==")
small = hnd(do("geometry-2d", "draw_line",
               {"start": {"x": 20, "y": 1600}, "end": {"x": 80, "y": 1600}},
               label="a short line inside the window"))
r = do("geometry-2d", "stretch_window", {
    "corner1": {"x": 0, "y": 1550}, "corner2": {"x": 100, "y": 1650},
    "displacement": {"x": 0, "y": 100}, "handles": [small]})
if isinstance(r, dict):
    ch = [c for c in (r.get("changed") or []) if c.get("handle") == small]
    check("it is reported as moved whole", bool(ch) and ch[0].get("movedWhole") is True,
          str(r)[:250])
sb = bbox(small)
check("PROVEN: both ends went up by 100", close((sb.get("min") or {}).get("y"), 1700, 1e-6),
      str(sb)[:200])

print("\n== a circle has no vertex to drag, so it moves whole or not at all ==")
c_in = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 50, "y": 1900}, "radius": 20},
              label="a circle with its centre in the window"))
c_out = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 160, "y": 1900}, "radius": 50},
               label="one overlapping the window but centred outside it"))
co_before = bbox(c_out)
r = do("geometry-2d", "stretch_window", {
    "corner1": {"x": 0, "y": 1850}, "corner2": {"x": 100, "y": 1950},
    "displacement": {"x": 0, "y": 200}})
ci_after, co_after = bbox(c_in), bbox(c_out)
check("PROVEN: the one centred inside moved",
      close((ci_after.get("min") or {}).get("y"), 2080, 1e-6), str(ci_after)[:200])
check("PROVEN: the one merely overlapping did not",
      close((co_after.get("min") or {}).get("y"), (co_before.get("min") or {}).get("y"), 1e-9),
      f"{co_before} -> {co_after}")

print("\n-- refusals --")
do("geometry-2d", "stretch_window",
   {"corner1": {"x": 0, "y": 0}, "displacement": {"x": 10, "y": 0}},
   label="a missing corner2 is refused", expect_fail=True)
r = do("geometry-2d", "stretch_window",
       {"corner1": {"x": 0, "y": 0}, "corner2": {"x": 100, "y": 100},
        "displacement": {"x": 0, "y": 0}},
       label="a zero displacement is refused", expect_fail=True)
check("and the refusal says nothing would move", "nothing would move" in str(r), str(r)[:250])
r = do("geometry-2d", "stretch_window",
       {"corner1": {"x": 0, "y": 0}, "corner2": {"x": 0, "y": 100},
        "displacement": {"x": 10, "y": 0}},
       label="a window with no area is refused", expect_fail=True)
check("and the refusal says it could never catch a vertex",
      "never catch a vertex" in str(r), str(r)[:250])
r = do("geometry-2d", "stretch_window",
       {"corner1": {"x": 90000, "y": 90000}, "corner2": {"x": 91000, "y": 91000},
        "displacement": {"x": 10, "y": 0}},
       label="a window catching nothing is refused rather than reported as success",
       expect_fail=True)
check("and it says how many entities it looked at", "were examined" in str(r), str(r)[:250])

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "fit-stretch.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -250, "yMin": -100, "xMax": 550, "yMax": 2150},
                            "widthPx": 1300, "heightPx": 2400})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm, and this is what the drawing actually shows: at the bottom the zig-zag is")
print("     overlaid by TWO smooth curves. One touches the apex of both peaks and the bottom of")
print("     the trough; the other reaches only about half the peak height and dips shallowly in")
print("     between. Both start and end on the zig-zag's two end points - clamped ends, free")
print("     middle. Above that, a rounded square (the closed fit, still closed), a curve with")
print("     NO semicircular hump where the discarded bulge was, a rectangle whose left edge is")
print("     still at 0 while its right reaches 300, and a long horizontal line that has NOT")
print("     jumped upwards.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
