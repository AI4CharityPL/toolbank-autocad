# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.2 tranche 2 — tolerance, update, space, arc symbol.

The load-bearing experiment in this file is the one that decides whether `dimension_update`
deserves to exist at all.

`set_entity_dimstyle` has been in the bank since the start and assigns a dimension style. If
`dimension_update` did only that, it would be a second name for one action and the bank would be
worse for having it — the same reasoning that struck `align_objects` and
`draw_construction_geometry` from phase 3.1. The claimed difference is that `SetDimstyleData`
re-applies the style's own values and RESETS per-entity overrides, where a plain assignment
leaves them standing.

That is measurable, and it is measured here: a tolerance override is put on two identical
dimensions, one goes through each tool, and the override state is read afterwards. If both come
back the same, the tool is a duplicate and should be withdrawn rather than shipped.

`dimension_space` is checked by OFFSET rather than by "it moved": a spacing tool that moved
everything to one place, or that reversed the order of a chain, would still report a tidy list
of new positions and a plausible affected count.
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


def linear(x1, y1, x2, y2, dy, rot=0.0):
    return hnd(S["dimensions"].call("dimension_linear", {
        "p1": {"x": x1, "y": y1}, "p2": {"x": x2, "y": y2},
        "dimLinePoint": {"x": (x1 + x2) / 2, "y": dy}, "rotationDeg": rot})[1])


print("== fresh drawing ==")
do("files", "new_document", {})

# ── dimension_tolerance ───────────────────────────────────────────────────────
print("\n== dimension_tolerance ==")
d1 = linear(0, 0, 250, 0, 100)
r = do("dimensions", "dimension_tolerance", {"handles": [d1], "upper": 0.5})
if isinstance(r, dict):
    dims = r.get("dimensions") or []
    check("mode defaulted to symmetrical", r.get("mode") == "symmetrical", str(r)[:220])
    check("dimtol is on and dimlim is off", bool(dims) and dims[0].get("dimtol") is True
          and dims[0].get("dimlim") is False, str(dims[:1])[:220])
    # Symmetrical means ONE plus-minus value, so both sides must carry it. Setting only the
    # upper would print an asymmetric tolerance under a symmetrical label.
    check("PROVEN: both upper and lower are 0.5, which is what makes it symmetrical",
          bool(dims) and close(dims[0].get("upper"), 0.5) and close(dims[0].get("lower"), 0.5),
          str(dims[:1])[:220])
    check("PROVEN: the measurement is untouched at 250",
          bool(dims) and close(dims[0].get("measurement"), 250, 1e-6), str(dims[:1])[:220])

print("\n-- deviation and limits are different things --")
d2 = linear(0, 300, 250, 300, 400)
r = do("dimensions", "dimension_tolerance",
       {"handles": [d2], "mode": "deviation", "upper": 0.5, "lower": 0.2, "decimals": 2})
if isinstance(r, dict):
    dims = r.get("dimensions") or []
    check("deviation keeps upper and lower apart",
          bool(dims) and close(dims[0].get("upper"), 0.5) and close(dims[0].get("lower"), 0.2),
          str(dims[:1])[:220])
    check("and decimals took", bool(dims) and dims[0].get("decimals") == 2, str(dims[:1])[:220])
d3 = linear(0, 600, 250, 600, 700)
r = do("dimensions", "dimension_tolerance",
       {"handles": [d3], "mode": "limits", "upper": 0.5, "lower": 0.2})
if isinstance(r, dict):
    dims = r.get("dimensions") or []
    # limits and deviation are NOT the same flag - one sets Dimlim, the other Dimtol.
    check("PROVEN: limits sets dimlim and clears dimtol, the opposite of deviation",
          bool(dims) and dims[0].get("dimlim") is True and dims[0].get("dimtol") is False,
          str(dims[:1])[:220])

print("\n-- refusals --")
do("dimensions", "dimension_tolerance", {"handles": [d1], "mode": "symmetrical"},
   label="symmetrical without upper is refused", expect_fail=True)
do("dimensions", "dimension_tolerance", {"handles": [d1], "mode": "deviation", "upper": 1},
   label="deviation without lower is refused", expect_fail=True)
do("dimensions", "dimension_tolerance", {"handles": [d1], "mode": "wobbly", "upper": 1},
   label="an unknown mode is refused", expect_fail=True)
do("dimensions", "dimension_tolerance", {"handles": [d1], "upper": 1, "decimals": 99},
   label="99 decimals is refused", expect_fail=True)

# ── dimension_update: the experiment that decides whether it should exist ─────
print("\n== dimension_update vs set_entity_dimstyle — the duplicate test ==")
# Two identical dimensions, both given the same tolerance override.
a = linear(0, 900, 250, 900, 1000)
b = linear(0, 1200, 250, 1200, 1300)
do("dimensions", "dimension_tolerance", {"handles": [a, b], "upper": 0.5},
   label="both get a tolerance override")

# Which styles this drawing actually has. An earlier version hardcoded "Standard", which does
# not exist in a metric template - the control arm failed, and the experiment then "proved" that
# dimension_update clears the override without ever showing that set_entity_dimstyle does not.
ok, styles = S["dimensions"].call("list_dimstyles", {})
names = [s.get("name") if isinstance(s, dict) else s
         for s in ((styles or {}).get("styles") or (styles or {}).get("dimstyles") or [])]
style = (styles or {}).get("current") or (names[0] if names else "ISO-25")
if isinstance(style, dict):
    style = style.get("name")
check("the drawing's dimension styles were read, so the control uses a real one",
      bool(style), f"{styles}")
print(f"     using style: {style}")

print("\n-- THE CONTROL: one through set_entity_dimstyle --")
do("dimensions", "set_entity_dimstyle", {"handles": [a], "dimStyle": style})
r = do("dimensions", "dimension_update", {"handles": [a], "dimStyle": style},
       label="then read A's override state back through dimension_update")
if isinstance(r, dict):
    dims = r.get("dimensions") or []
    # This is the control. set_entity_dimstyle ran on A first; if it had cleared the override,
    # dimension_update would now see False going in and the two tools would be the same thing.
    check("PROVEN: set_entity_dimstyle LEFT the override standing — it only assigns a style",
          bool(dims) and dims[0].get("toleranceOverrideBefore") is True,
          f"{str(dims[:1])[:250]} — False here means both tools clear overrides, so "
          f"dimension_update is a duplicate and must be withdrawn rather than shipped")

print("\n-- the other through dimension_update --")
r = do("dimensions", "dimension_update", {"handles": [b], "dimStyle": style})
if isinstance(r, dict):
    dims = r.get("dimensions") or []
    check("it reports the override state BEFORE it acted",
          bool(dims) and dims[0].get("toleranceOverrideBefore") is True, str(dims[:1])[:250])
    # THE assertion. If this comes back True, SetDimstyleData did not reset the override, the
    # two tools do the same thing, and dimension_update is a duplicate that should be withdrawn
    # rather than shipped - the same call made on align_objects in phase 3.1.
    check("PROVEN: and that the override is GONE afterwards — this is not set_entity_dimstyle",
          bool(dims) and dims[0].get("toleranceOverrideAfter") is False,
          f"{str(dims[:1])[:250]} — if this is True the two tools are the same and this one "
          f"must be withdrawn, not shipped")
    check("the measurement survived", bool(dims) and close(dims[0].get("measurement"), 250, 1e-6),
          str(dims[:1])[:220])

print("\n-- refusals --")
do("dimensions", "dimension_update", {"handles": []},
   label="an empty handle list is refused", expect_fail=True)
r = do("dimensions", "dimension_update", {"handles": [b], "dimStyle": "NoSuchStyle"},
       label="an unknown style is refused", expect_fail=True)
# It was NOT refused on the first run: the shared resolver fell through to the current style,
# so a caller asking for a style that does not exist got a different one and affected: 1. That
# silent substitution reached all 13 tools in this category that take a style name.
check("and the refusal lists the styles that DO exist, rather than picking one",
      "does not exist" in str(r) and "This drawing has" in str(r), str(r)[:280])

# ── dimension_space ───────────────────────────────────────────────────────────
print("\n== dimension_space ==")
# Four parallel linear dimensions at deliberately UNEVEN offsets above the same measured span.
base = linear(0, 2000, 300, 2000, 2100)
s1 = linear(0, 2000, 300, 2000, 2160)
s2 = linear(0, 2000, 300, 2000, 2400)
s3 = linear(0, 2000, 300, 2000, 2410)
r = do("dimensions", "dimension_space",
       {"handles": [base, s1, s2, s3], "baseHandle": base, "spacing": 80})
if isinstance(r, dict):
    check("three were moved, the base was not", r.get("affected") == 3, str(r)[:250])
    dims = {d.get("handle"): d for d in (r.get("dimensions") or [])}
    # Measured by OFFSET. "It moved" would be true of a tool that piled them all in one place.
    check("PROVEN: they sit at exactly 80, 160 and 240 from the base",
          close((dims.get(s1) or {}).get("offset"), 80, 1e-6)
          and close((dims.get(s2) or {}).get("offset"), 160, 1e-6)
          and close((dims.get(s3) or {}).get("offset"), 240, 1e-6),
          str([(k[-3:], v.get("offsetBefore"), v.get("offset")) for k, v in dims.items()]))
    # s2 started at 300 and s3 at 310, so an implementation that sorted wrongly would swap them.
    check("PROVEN: the chain kept its order — the one that was nearer is still nearer",
          ((dims.get(s2) or {}).get("offset") or 0) < ((dims.get(s3) or {}).get("offset") or 0),
          str(dims)[:250])
    check("and nothing changed what it measures",
          all(close(d.get("measurement"), 300, 1e-6) for d in dims.values()),
          str(dims)[:250])

print("\n-- spacing 0 ALIGNS them onto the base's line --")
r = do("dimensions", "dimension_space", {"handles": [base, s1, s2, s3], "spacing": 0})
if isinstance(r, dict):
    check("it says it aligned rather than spaced", r.get("aligned") is True, str(r)[:250])
    check("PROVEN: every offset is now 0",
          all(close(d.get("offset"), 0, 1e-6) for d in (r.get("dimensions") or [])),
          str(r.get("dimensions"))[:250])

print("\n-- refusals --")
r = do("dimensions", "dimension_space", {"handles": [base], "spacing": 50},
       label="one dimension alone is refused", expect_fail=True)
do("dimensions", "dimension_space", {"handles": [base, s1]},
   label="a missing spacing is refused", expect_fail=True)
do("dimensions", "dimension_space", {"handles": [base, s1], "spacing": -10},
   label="a negative spacing is refused", expect_fail=True)
perp = linear(0, 3000, 0, 3300, 3150, rot=90)
r = do("dimensions", "dimension_space", {"handles": [base, perp], "spacing": 50},
       label="a perpendicular dimension is refused", expect_fail=True)
check("and the refusal states the angle between them",
      "degrees" in str(r), str(r)[:250])
do("dimensions", "dimension_space",
   {"handles": [base, s1], "baseHandle": s3, "spacing": 50},
   label="a baseHandle outside handles is refused", expect_fail=True)

# ── dimension_arc_symbol ──────────────────────────────────────────────────────
print("\n== dimension_arc_symbol ==")
arc = hnd(do("geometry-2d", "draw_arc",
             {"center": {"x": 1500, "y": 0}, "radius": 200,
              "startAngleDeg": 20, "endAngleDeg": 160}, label="an arc"))
al = hnd(do("dimensions", "dimension_arc_length",
            {"arcHandle": arc, "arcPoint": {"x": 1500, "y": 260}},
            label="an arc LENGTH dimension on it"))
for name, val in (("above", 1), ("none", 2), ("preceding", 0)):
    r = do("dimensions", "dimension_arc_symbol", {"handles": [al], "position": name},
           label=f"position={name}")
    if isinstance(r, dict):
        dims = r.get("dimensions") or []
        check(f"  it round-trips as {val}",
              r.get("arcSymbolType") == val and bool(dims) and dims[0].get("arcSymbol") == val,
              str(r)[:220])

print("\n-- refusals --")
rad = hnd(do("dimensions", "dimension_radial",
             {"curveHandle": arc, "chordPoint": {"x": 1500, "y": 200}, "leaderLength": 40},
             label="a radial dimension on the SAME arc"))
r = do("dimensions", "dimension_arc_symbol", {"handles": [rad], "position": "above"},
       label="the radial one is refused - it is a different entity", expect_fail=True)
check("and the refusal says so by name",
      "not an ArcDimension" in str(r), str(r)[:250])
do("dimensions", "dimension_arc_symbol", {"handles": [al], "position": "sideways"},
   label="an unknown position is refused", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "dim-space.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -200, "yMin": -200, "xMax": 1800, "yMax": 3400},
                            "widthPx": 1600, "heightPx": 2400})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: the four spaced dimensions ended up ALIGNED on one line (the last call")
print("     used spacing 0), an arc with an arc-length dimension and a radial one, and the")
print("     tolerance dimensions near the bottom. Text is small - zoom to read any of it.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
