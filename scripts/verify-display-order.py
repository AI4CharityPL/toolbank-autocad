# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — draw order, transparency and wipeouts.

Four tools: set_draworder, set_object_transparency, create_wipeout, set_wipeout_frame.

**This is the tranche where a return code proves least.** Every claim here is about what
COVERS what, and draw order has no queryable "position" an assertion can read — it is a list
whose only observable effect is which colour a shared pixel ends up. So the PNG is not a
supplementary check here; for the ordering claims it is the only one.

What the numbers can still settle: the alpha stored for a percentage (AutoCAD keeps the
inverse, and getting that backwards makes "10% transparent" nearly invisible), the wipeout's
vertex count, and the WIPEOUTFRAME sysvar read back after being set.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "hatches", "view", "layers")}
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


def rect(x, y, w, h, layer=None):
    args = {"corner1": {"x": x, "y": y}, "corner2": {"x": x + w, "y": y + h}}
    if layer:
        args["layer"] = layer
    r = S["geometry-2d"].call("draw_rectangle", args)[1]
    return ((r or {}).get("entity") or {}).get("handle")


def entity_colour(h):
    ok, r = S["geometry-2d"].call("get_entity", {"handle": h})
    return (r or {}).get("color") if ok else None


print("== fresh drawing ==")
do("files", "new_document", {})

# Two overlapping filled rectangles: the only way draw order becomes observable.
print("\n== two overlapping solid areas ==")
r1 = rect(0, 0, 100, 100)
r2 = rect(50, 50, 100, 100)
check("two rectangles drawn", bool(r1) and bool(r2), f"{r1} / {r2}")
h1 = do("hatches", "draw_hatch",
        {"boundaryHandles": [r1], "pattern": "SOLID", "color": {"r": 255, "g": 0, "b": 0}},
        label="fill the first RED")
h2 = do("hatches", "draw_hatch",
        {"boundaryHandles": [r2], "pattern": "SOLID", "color": {"r": 0, "g": 0, "b": 255}},
        label="fill the second BLUE")
hh1 = ((h1 or {}).get("entity") or {}).get("handle") or (h1 or {}).get("handle")
hh2 = ((h2 or {}).get("entity") or {}).get("handle") or (h2 or {}).get("handle")
check("both hatches have handles", bool(hh1) and bool(hh2), f"{hh1} / {hh2}")
c1, c2 = entity_colour(hh1), entity_colour(hh2)
check("the first hatch really IS red", (c1 or {}).get("r") == 255 and (c1 or {}).get("b") == 0,
      f"got {c1} - a black hatch makes every ordering claim below unverifiable")
check("the second hatch really IS blue", (c2 or {}).get("b") == 255 and (c2 or {}).get("r") == 0,
      f"got {c2}")

print("\n== set_draworder ==")
r = do("geometry-2d", "set_draworder", {"handles": [hh1], "position": "front"},
       label="bring the RED one to the front")
if isinstance(r, dict):
    check("reports one affected", r.get("affected") == 1, str(r)[:200])
    check("reports the position asked for", r.get("position") == "front", str(r)[:200])
    check("the note says draw order is per space", "per" in (r.get("note") or "").lower(),
          str(r.get("note"))[:200])

r = do("geometry-2d", "set_draworder", {"handles": [hh1], "position": "below",
                                        "relativeTo": hh2},
       label="then put it below the blue one")
if isinstance(r, dict):
    check("reports the reference handle back", r.get("relativeTo") == hh2, str(r)[:200])

print("\n-- refusals --")
do("geometry-2d", "set_draworder", {"handles": [hh1], "position": "above"},
   label="'above' without relativeTo is refused", expect_fail=True)
do("geometry-2d", "set_draworder", {"handles": [hh1], "position": "sideways"},
   label="an unknown position is refused", expect_fail=True)
do("geometry-2d", "set_draworder", {"handles": [], "position": "front"},
   label="an empty handle list is refused", expect_fail=True)

# ── transparency ──────────────────────────────────────────────────────────────
print("\n== set_object_transparency: the percentage is NOT the alpha ==")
r = do("geometry-2d", "set_object_transparency", {"handles": [hh2], "percent": 50})
if isinstance(r, dict):
    check("reports the percentage given", r.get("percent") == 50, str(r)[:200])
    ent = (r.get("entities") or [{}])[0]
    # 50% transparent is alpha 128, not alpha 50. Getting this inverted is the whole reason
    # the tool takes a percentage rather than passing a number through.
    check("alpha is ~128 for 50 percent, not 50", abs(ent.get("alpha", 0) - 128) <= 1,
          f"alpha={ent.get('alpha')} — the percentage was stored as alpha")
    check("the note explains the inversion", "inverse" in (r.get("note") or "").lower(),
          str(r.get("note"))[:200])

r = do("geometry-2d", "set_object_transparency", {"handles": [hh2], "percent": 0},
       label="0 percent is fully opaque")
if isinstance(r, dict):
    check("alpha is 255 at 0 percent",
          (r.get("entities") or [{}])[0].get("alpha") == 255, str(r)[:200])

# byLayer/byBlock are withdrawn: measured to compile and then throw eInvalidKey on assignment
# across Line, Circle, Polyline and Hatch, while the percentage form worked on all four.
r = do("geometry-2d", "set_object_transparency", {"handles": [hh2], "mode": "byLayer"},
       label="byLayer is refused, not silently made opaque", expect_fail=True)
check("and the refusal carries the measurement rather than just saying no",
      "eInvalidKey" in str(r) and "percent" in str(r), str(r)[:250])
do("geometry-2d", "set_object_transparency", {"handles": [hh2], "percent": 95},
   label="above 90 percent is refused", expect_fail=True)
do("geometry-2d", "set_object_transparency", {"handles": [hh2]},
   label="value mode without a percent is refused", expect_fail=True)
do("geometry-2d", "set_object_transparency", {"handles": [hh2], "mode": "sometimes"},
   label="an unknown mode is refused", expect_fail=True)

# Put it back to something visible for the screenshot.
do("geometry-2d", "set_object_transparency", {"handles": [hh2], "percent": 40},
   label="40 percent for the picture")

# ── wipeout ───────────────────────────────────────────────────────────────────
print("\n== create_wipeout ==")
r = do("geometry-2d", "create_wipeout",
       {"vertices": [{"x": 20, "y": 110}, {"x": 130, "y": 110},
                     {"x": 130, "y": 140}, {"x": 20, "y": 140}]})
if isinstance(r, dict):
    check("it came back with a handle", bool((r.get("entity") or {}).get("handle")), str(r)[:200])
    check("the loop was closed for us: 5 points from 4",
          r.get("vertices") == 5, f"got {r.get('vertices')}")
    check("brought to the front by default", r.get("broughtToFront") is True, str(r)[:200])
    check("the note explains why front is the default",
          "behind" in (r.get("note") or "").lower(), str(r.get("note"))[:200])

do("geometry-2d", "create_wipeout", {"vertices": [{"x": 0, "y": 0}, {"x": 10, "y": 0}]},
   label="two points are refused - an area needs three", expect_fail=True)

print("\n== set_wipeout_frame ==")
r = do("geometry-2d", "set_wipeout_frame", {"mode": "shown"})
if isinstance(r, dict):
    check("WIPEOUTFRAME reads 1 for 'shown'", r.get("wipeoutframe") == 1, str(r)[:200])
    check("it reports what it was before", r.get("before") is not None, str(r)[:200])
r = do("geometry-2d", "set_wipeout_frame", {"mode": "displayedNotPlotted"})
if isinstance(r, dict):
    check("and 2 for 'displayedNotPlotted'", r.get("wipeoutframe") == 2, str(r)[:200])
do("geometry-2d", "set_wipeout_frame", {"mode": "maybe"},
   label="an unknown frame mode is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen — the ONLY evidence for the ordering claims ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "display-order.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -20, "yMin": -20, "xMax": 170, "yMax": 160},
                            "widthPx": 1400, "heightPx": 1300})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> The picture PROVES two things and cannot prove the third:")
print("     * draw order: BLUE covers RED in the overlap, because red was sent below it.")
print("     * the wipeout: a white band across the top hides part of the blue square,")
print("       which only happens if it is in front.")
print("     * transparency is NOT visible here and that is expected. The alpha is stored")
print("       correctly - asserted above as 128 for 50% and 255 for 0% - but plotted output")
print("       ignores transparency unless PLOTTRANSPARENCYOVERRIDE is 1, and this export")
print("       goes through the plot engine. Do not read the solid blue as a failure.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
