# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.2 — quick_dimension, the last entry of the phase.

The chain tools already in the bank are handed a list of POINTS. This one works them out from
the geometry, and that is where it can go wrong in ways a return code cannot show:

* **Duplicates.** Walls meeting at a corner contribute the same coordinate twice. A chain built
  without merging them contains a ZERO-LENGTH dimension - it draws as nothing, reads as 0 if you
  go looking, and the tool still reports one more dimension than it made anything of. Checked by
  feeding it a run of lines that share every intermediate endpoint, and asserting pointsFound is
  larger than pointsUsed and that no measurement is 0.

* **A chain that does not add up.** A dropped or doubled dimension leaves a perfectly plausible
  list of numbers. In continuous mode the measurements must sum to the geometry's own span, and
  that is asserted here from outside as well as inside the tool.

* **The wrong axis.** 'auto' has to pick the axis the geometry is spread along. A tool that
  always chose X would look right on every horizontal test and wrong the moment it met a
  vertical wall, so both are checked, with a case where the answer is unambiguous.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
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


def line(x1, y1, x2, y2):
    return hnd(S["geometry-2d"].call("draw_line", {"start": {"x": x1, "y": y1},
                                                   "end": {"x": x2, "y": y2}})[1])


print("== fresh drawing ==")
do("files", "new_document", {})

# ── the duplicate trap ────────────────────────────────────────────────────────
print("\n== a run of four walls sharing every corner ==")
# Endpoints at x = 0, 300, 800, 1000. Four lines laid end to end give EIGHT key points, of
# which only four are distinct - each interior corner is contributed twice.
walls = [line(0, 0, 300, 0), line(300, 0, 800, 0), line(800, 0, 1000, 0)]
r = do("dimensions", "quick_dimension", {"handles": walls})
if isinstance(r, dict):
    check("it read 6 key points off the three lines", r.get("pointsFound") == 6, str(r)[:250])
    # THE claim. Without merging, this would be 6 coordinates and 5 dimensions, two of them
    # spanning nothing at all.
    check("PROVEN: only 4 survive the merge — the shared corners collapsed",
          r.get("pointsUsed") == 4,
          f"pointsFound {r.get('pointsFound')} pointsUsed {r.get('pointsUsed')} — 6 here means "
          f"every shared corner became a zero-length dimension")
    check("so 3 dimensions were placed, not 5", len(r.get("entities") or []) == 3, str(r)[:250])
    ms = r.get("measurements") or []
    check("PROVEN: none of them measures zero",
          bool(ms) and all(abs(m) > 1e-9 for m in ms), f"{ms}")
    check("PROVEN: and they are the real gaps — 300, 500, 200",
          [round(m, 6) for m in ms] == [300, 500, 200], f"{ms}")
    # Checked from outside as well as inside the tool: a dropped or doubled dimension leaves a
    # plausible list of numbers that does not add up.
    check("PROVEN: they sum to the 1000 span",
          close(sum(ms), 1000, 1e-6) and close(r.get("span"), 1000, 1e-6),
          f"sum {sum(ms)} vs span {r.get('span')}")
    check("and it picked the horizontal axis", r.get("direction") == "horizontal", str(r)[:250])

print("\n== baseline mode measures every dimension from the FIRST point ==")
r = do("dimensions", "quick_dimension", {"handles": walls, "mode": "baseline",
                                         "dimLineCoord": 400})
if isinstance(r, dict):
    ms = r.get("measurements") or []
    # Continuous gave 300/500/200. Baseline must give the running totals, which is the whole
    # difference between the two modes and is invisible in a count of dimensions.
    check("PROVEN: 300, 800, 1000 — cumulative, not the individual gaps",
          [round(m, 6) for m in ms] == [300, 800, 1000], f"{ms}")

# ── the axis ──────────────────────────────────────────────────────────────────
print("\n== auto picks the axis the geometry is spread along ==")
tall = [line(2000, 0, 2000, 400), line(2000, 400, 2000, 900)]
r = do("dimensions", "quick_dimension", {"handles": tall})
if isinstance(r, dict):
    # A tool that always used X would report a horizontal chain here and measure nothing.
    check("PROVEN: it chose vertical for a vertical run", r.get("direction") == "vertical",
          str(r)[:250])
    check("and measured 400 and 500",
          [round(m, 6) for m in (r.get("measurements") or [])] == [400, 500],
          str(r.get("measurements")))

print("\n-- direction can be forced --")
r = do("dimensions", "quick_dimension",
       {"handles": walls, "direction": "vertical", "dimLineCoord": -400},
       label="forcing vertical on a horizontal run is refused", expect_fail=True)
check("and the refusal says everything collapses to one coordinate",
      "single coordinate" in str(r), str(r)[:250])

# ── mixed geometry ────────────────────────────────────────────────────────────
print("\n== circles and arcs contribute their own key points ==")
c1 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 3000, "y": 0}, "radius": 50},
            label="a circle at x=3000"))
c2 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 3400, "y": 0}, "radius": 50},
            label="another at x=3400"))
r = do("dimensions", "quick_dimension", {"handles": [c1, c2]})
if isinstance(r, dict):
    check("PROVEN: a circle contributes its CENTRE, so the gap is 400 centre to centre",
          [round(m, 6) for m in (r.get("measurements") or [])] == [400],
          str(r.get("measurements")))

print("\n-- a type with no key points is reported, not passed over --")
hatchless = hnd(do("geometry-2d", "draw_text",
                   {"position": {"x": 3000, "y": 500}, "text": "NOTE", "height": 20},
                   label="a text entity"))
r = do("dimensions", "quick_dimension", {"handles": [c1, c2, hatchless]})
if isinstance(r, dict):
    sk = r.get("skipped") or []
    check("PROVEN: the text is on the skipped list WITH a reason",
          len(sk) == 1 and "key points" in (sk[0].get("reason") or ""), str(sk)[:250])
    check("and the dimensions are unchanged by its presence",
          [round(m, 6) for m in (r.get("measurements") or [])] == [400],
          str(r.get("measurements")))

# ── refusals ──────────────────────────────────────────────────────────────────
print("\n== refusals ==")
do("dimensions", "quick_dimension", {"handles": []},
   label="an empty handle list is refused", expect_fail=True)
do("dimensions", "quick_dimension", {"handles": walls, "mode": "sideways"},
   label="an unknown mode is refused", expect_fail=True)
do("dimensions", "quick_dimension", {"handles": walls, "direction": "diagonal"},
   label="an unknown direction is refused", expect_fail=True)
r = do("dimensions", "quick_dimension", {"handles": [hatchless]},
       label="nothing but a text entity is refused", expect_fail=True)
check("and the refusal names what this tool does read",
      "lines, polylines, arcs, circles" in str(r), str(r)[:250])
one = line(5000, 0, 5000, 0)
r = do("dimensions", "quick_dimension", {"handles": [c1]},
       label="a single circle has one point and nothing to span", expect_fail=True)
do("dimensions", "quick_dimension", {"handles": walls, "dimStyle": "NoSuchStyle"},
   label="an unknown style is refused, as everywhere else now", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "quickdim.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -200, "yMin": -500, "xMax": 3700, "yMax": 1100},
                            "widthPx": 2200, "heightPx": 900})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: a run of three walls carrying THREE dimensions above it, not five - there")
print("     is no stub of a dimension at either shared corner. A second, cumulative chain above")
print("     that. A vertical pair on the right with its dimensions turned to match, and two")
print("     circles with one dimension between their centres.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
