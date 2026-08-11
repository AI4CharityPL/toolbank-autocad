# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — splines.

Three tools: draw_spline_cv, edit_spline_fit_point, spline_to_polyline.

The claim that needs the most care is the one that separates the two kinds of spline:

* a FIT spline passes through its points,
* a CONTROL VERTEX spline is pulled by vertices it does not touch, except the two ends.

Both produce a smooth curve and a plausible result object, so "it drew a spline" proves
nothing about which kind. This measures it: for the CV spline the mid vertex must NOT lie on
the curve, while the first and last must. That is a property of the geometry, not of the call.

The conversion is checked the same way. A tool that returned the original spline's length
would look right; the point of the approximation is that the length CHANGES, slightly.
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
    return abs(a - b) <= tol


def on_curve(d, tol=1e-6):
    """Is this distance a real zero?

    NOT `d or 9`. A distance of exactly 0 - which is precisely the answer that means "the point
    lies on the curve" - is falsy in Python, so `d or 9` turns the correct result into 9 and the
    check into a failure. The first run of this script reported "the curve TOUCHES the first
    vertex" as FAILED with `got 0`, which is the one value that proves it does.
    """
    return d is not None and abs(d) <= tol


def dist_to(handle, x, y):
    """Distance from a point to the nearest point ON the entity."""
    ok, r = S["geometry-2d"].call("get_distance_to_entity",
                                  {"handle": handle, "point": {"x": x, "y": y}})
    return (r or {}).get("value") if ok else None


def length_of(h):
    ok, r = S["geometry-2d"].call("get_curve_length", {"handle": h})
    return (r or {}).get("value") if ok else None


print("== fresh drawing ==")
do("files", "new_document", {})

# ── draw_spline_cv ────────────────────────────────────────────────────────────
print("\n== draw_spline_cv: the vertices PULL the curve, they are not on it ==")
CV = [(0, 0), (50, 120), (150, -40), (200, 60)]
r = do("geometry-2d", "draw_spline_cv",
       {"controlPoints": [{"x": x, "y": y} for x, y in CV]})
cv_h = ((r or {}).get("entity") or {}).get("handle")
if isinstance(r, dict):
    check("four control points reported back", r.get("controlPoints") == 4, str(r)[:220])
    check("degree defaulted to 3", r.get("degree") == 3, str(r)[:220])
    check("it has a length", (r.get("length") or 0) > 0, str(r)[:220])

# The distinguishing measurement. A fit spline would sit ON every one of these.
d_first = dist_to(cv_h, *CV[0])
d_mid1 = dist_to(cv_h, *CV[1])
d_mid2 = dist_to(cv_h, *CV[2])
d_last = dist_to(cv_h, *CV[3])
print(f"  distances from the control points to the curve: {d_first}, {d_mid1}, {d_mid2}, {d_last}")
check("the curve TOUCHES the first vertex", on_curve(d_first), f"got {d_first}")
check("the curve TOUCHES the last vertex", on_curve(d_last), f"got {d_last}")
check("but is PULLED AWAY from the middle ones — this is what makes it a CV spline",
      (d_mid1 or 0) > 1.0 and (d_mid2 or 0) > 1.0,
      f"middle distances {d_mid1}, {d_mid2} — a fit spline would sit on them")

print("\n-- a fit spline through the SAME points sits on all of them --")
r = do("geometry-2d", "draw_spline", {"fitPoints": [{"x": x, "y": y} for x, y in CV]},
       label="draw_spline through the same points")
fit_h = ((r or {}).get("entity") or {}).get("handle")
f_mid = dist_to(fit_h, *CV[1])
check("the fit spline DOES pass through the middle point", on_curve(f_mid),
      f"got {f_mid} — if this is non-zero the comparison above proves nothing")

print("\n-- refusals --")
do("geometry-2d", "draw_spline_cv", {"controlPoints": [{"x": 0, "y": 0}]},
   label="one control point is refused", expect_fail=True)
r = do("geometry-2d", "draw_spline_cv",
       {"controlPoints": [{"x": 0, "y": 0}, {"x": 10, "y": 10}, {"x": 20, "y": 0}], "degree": 3},
       label="degree 3 with only 3 points is refused", expect_fail=True)
check("and the refusal explains what degree means",
      "degree 1 is a polyline" in str(r), str(r)[:250])
do("geometry-2d", "draw_spline_cv",
   {"controlPoints": [{"x": 0, "y": 0}, {"x": 10, "y": 10}], "degree": 99},
   label="an absurd degree is refused", expect_fail=True)

print("\n-- degree 2 with 3 points is fine --")
r = do("geometry-2d", "draw_spline_cv",
       {"controlPoints": [{"x": 250, "y": 0}, {"x": 300, "y": 100}, {"x": 350, "y": 0}],
        "degree": 2}, label="a quadratic CV spline")
if isinstance(r, dict):
    check("degree 2 reported back", r.get("degree") == 2, str(r)[:220])

# ── edit_spline_fit_point ─────────────────────────────────────────────────────
print("\n== edit_spline_fit_point ==")
before_len = length_of(fit_h)
r = do("geometry-2d", "edit_spline_fit_point",
       {"handle": fit_h, "index": 1, "point": {"x": 50, "y": 200}})
if isinstance(r, dict):
    check("reports the point as it was", close((r.get("before") or [0])[1], 120), str(r)[:220])
    check("reports it where it now is", close((r.get("point") or [0])[1], 200), str(r)[:220])
    check("the curve's length changed with it",
          not close(r.get("length", 0), r.get("lengthBefore", 0), 1e-6),
          f"{r.get('lengthBefore')} -> {r.get('length')} — moving a fit point must reshape it")
check("PROVEN: the curve now passes through the new position",
      on_curve(dist_to(fit_h, 50, 200)), f"got {dist_to(fit_h, 50, 200)}")

print("\n-- a CV spline has no fit points, and says so --")
r = do("geometry-2d", "edit_spline_fit_point",
       {"handle": cv_h, "index": 0, "point": {"x": 0, "y": 0}},
       label="editing a fit point on a CV spline is refused", expect_fail=True)
check("and the refusal explains the difference rather than passing an HRESULT",
      "control vertices" in str(r), str(r)[:250])

do("geometry-2d", "edit_spline_fit_point", {"handle": fit_h, "index": 99,
                                            "point": {"x": 0, "y": 0}},
   label="an out-of-range index is refused", expect_fail=True)
do("geometry-2d", "edit_spline_fit_point", {"handle": fit_h, "index": 0},
   label="a missing point is refused", expect_fail=True)

# ── spline_to_polyline ────────────────────────────────────────────────────────
print("\n== spline_to_polyline ==")
r = do("geometry-2d", "draw_spline_cv",
       {"controlPoints": [{"x": 0, "y": 300}, {"x": 60, "y": 400},
                          {"x": 140, "y": 250}, {"x": 200, "y": 350}]},
       label="a spline to convert")
conv_src = ((r or {}).get("entity") or {}).get("handle")
src_len = length_of(conv_src)

r = do("geometry-2d", "spline_to_polyline", {"handle": conv_src})
new_h = ((r or {}).get("entity") or {}).get("handle")
if isinstance(r, dict):
    check("reports the type it produced", bool(r.get("type")), str(r)[:220])
    check("reports the original length", close(r.get("lengthBefore", 0), src_len or -1, 1e-6),
          f"{r.get('lengthBefore')} vs {src_len}")
    check("the approximation changed the length, as it must",
          r.get("length") is not None and not close(r.get("length"), r.get("lengthBefore"), 1e-9),
          f"{r.get('lengthBefore')} -> {r.get('length')} — identical would mean nothing was approximated")
    check("but only slightly: within 1 percent",
          abs((r.get("length") or 0) - (r.get("lengthBefore") or 1)) / (r.get("lengthBefore") or 1) < 0.01,
          f"{r.get('lengthBefore')} -> {r.get('length')}")
    check("the original was erased by default", r.get("originalKept") is False, str(r)[:220])
do("geometry-2d", "get_entity", {"handle": conv_src},
   label="PROVEN: the original spline is gone", expect_fail=True)

print("\n-- keepOriginal leaves both --")
r = do("geometry-2d", "draw_spline_cv",
       {"controlPoints": [{"x": 250, "y": 300}, {"x": 310, "y": 400},
                          {"x": 390, "y": 250}, {"x": 450, "y": 350}]},
       label="another spline")
keep_src = ((r or {}).get("entity") or {}).get("handle")
r = do("geometry-2d", "spline_to_polyline", {"handle": keep_src, "keepOriginal": True},
       label="convert with keepOriginal")
if isinstance(r, dict):
    check("reports the original was kept", r.get("originalKept") is True, str(r)[:220])
    check("and hands back its handle", r.get("originalHandle") == keep_src, str(r)[:220])
do("geometry-2d", "get_entity", {"handle": keep_src},
   label="PROVEN: the original spline is still there")

do("geometry-2d", "spline_to_polyline", {"handle": new_h},
   label="converting a polyline is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "splines.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -40, "yMin": -90, "xMax": 500, "yMax": 450},
                            "widthPx": 1600, "heightPx": 1600})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: smooth curves, none of them kinked or collapsed to a straight line,")
print("     and the converted polyline visually following its spline's path.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
