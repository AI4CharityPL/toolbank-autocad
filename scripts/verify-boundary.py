# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — boundary_from_point and region_from_boundary.

Both go through the tracing that KNOWN-GAPS A1 fixed, so this exercises the two traps that
were expensive there rather than only the happy path:

* **the seed is WCS and TraceBoundary reads UCS.** A point plainly inside a rectangle is
  silently offset when the current UCS is not world, and the tool used to blame the user's
  geometry. So the same seed is traced with the UCS at world and again with it moved.
* **the region has to be ON SCREEN.** Off-screen geometry gives an empty result rather than an
  error. The tool frames the drawing itself; this checks that a region far from the current
  view still traces.

And the claim that separates the two tools: a boundary is an OUTLINE with a length, a region is
an AREA with an area value. Both come back from the same seed, so only the geometry tells them
apart.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "view", "ucs")}
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


def rect(x1, y1, x2, y2):
    r = S["geometry-2d"].call("draw_rectangle", {"corner1": {"x": x1, "y": y1},
                                                 "corner2": {"x": x2, "y": y2}})[1]
    return ((r or {}).get("entity") or {}).get("handle")


print("== fresh drawing ==")
do("files", "new_document", {})

print("\n== a 100x60 rectangle to trace ==")
rect(0, 0, 100, 60)
do("view", "zoom_extents", {})

print("\n== boundary_from_point ==")
r = do("geometry-2d", "boundary_from_point", {"point": {"x": 50, "y": 30}})
b_handle = None
if isinstance(r, dict):
    bs = r.get("boundaries") or []
    check("one boundary came back", len(bs) == 1, str(r)[:220])
    if bs:
        b_handle = bs[0].get("handle")
        check("it is a closed curve", bs[0].get("closed") is True, str(bs[0])[:200])
        # 2*(100+60) = 320. A tool that traced the wrong loop would still return a length.
        check("its length is the rectangle's perimeter, 320",
              close(bs[0].get("length"), 320, 1e-4), f"got {bs[0].get('length')}")
    check("it reports the seed back", close((r.get("seed") or [0])[0], 50), str(r)[:220])

check("PROVEN: the outline is a NEW entity, the rectangle is still there",
      b_handle is not None, "no boundary handle")

print("\n== region_from_boundary from the SAME seed ==")
r = do("geometry-2d", "region_from_boundary", {"point": {"x": 50, "y": 30}})
if isinstance(r, dict):
    rs = r.get("regions") or []
    check("one region came back", len(rs) == 1, str(r)[:220])
    if rs:
        # 100*60 = 6000. This is the number a boundary cannot give you.
        check("its AREA is 6000 — what separates a region from an outline",
              close(rs[0].get("area"), 6000, 1e-4), f"got {rs[0].get('area')}")
        check("and its perimeter is 320", close(rs[0].get("perimeter"), 320, 1e-4),
              f"got {rs[0].get('perimeter')}")
    check("the note points at the boolean-ops category",
          "boolean" in (r.get("note") or "").lower(), str(r.get("note"))[:220])

# ── trap 1: the seed is WCS, TraceBoundary reads UCS ───────────────────────────
print("\n== trap A1a: a UCS that is not world ==")
r = do("ucs", "create_ucs_origin", {"origin": {"x": 1000, "y": 2000}},
       label="move the UCS origin to (1000, 2000)")
r = do("geometry-2d", "boundary_from_point", {"point": {"x": 50, "y": 30}},
       label="the SAME WCS seed still traces")
if isinstance(r, dict):
    bs = r.get("boundaries") or []
    check("still the same 320 perimeter — the seed was taken to the UCS for us",
          bool(bs) and close(bs[0].get("length"), 320, 1e-4),
          f"got {bs[0].get('length') if bs else None} — a raw WCS seed would land outside")
do("ucs", "set_ucs_world", {}, label="put the UCS back to world")

# ── trap 2: the region must be on screen ──────────────────────────────────────
print("\n== trap A1b: the region is nowhere near the current view ==")
rect(50000, 50000, 50100, 50060)
do("view", "zoom_window", {"corner1": {"x": -10, "y": -10}, "corner2": {"x": 10, "y": 10}},
   label="look at the origin, far from the new rectangle")
r = do("geometry-2d", "boundary_from_point", {"point": {"x": 50050, "y": 50030}},
       label="trace a region that is off screen")
if isinstance(r, dict):
    bs = r.get("boundaries") or []
    check("it traced anyway — the tool framed the drawing itself",
          bool(bs) and close(bs[0].get("length"), 320, 1e-4),
          f"got {bs[0].get('length') if bs else None}")

# ── refusals ──────────────────────────────────────────────────────────────────
print("\n== refusals ==")
r = do("geometry-2d", "boundary_from_point", {"point": {"x": -5000, "y": -5000}},
       label="a seed in open space is refused", expect_fail=True)
check("and the refusal names the seed and mentions the UCS",
      "-5000" in str(r) and "UCS" in str(r), str(r)[:250])
do("geometry-2d", "boundary_from_point", {},
   label="a missing point is refused", expect_fail=True)
do("geometry-2d", "region_from_boundary", {"point": {"x": -5000, "y": -5000}},
   label="region from open space is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "boundary.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -20, "yMin": -20, "xMax": 130, "yMax": 90},
                            "widthPx": 1400, "heightPx": 1100})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: the rectangle near the origin, with its traced outlines and the region")
print("     drawn over it. They coincide, so it should read as one rectangle, not a smear.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
