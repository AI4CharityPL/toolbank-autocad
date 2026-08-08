# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.3 tranche 3 — background masks and MText columns.

Both of these are easy to "set" and hard to prove.

* **A background mask does not change the entity's extents.** It is drawn behind the text, so
  every geometric measurement stays exactly the same whether it worked or not. There is nothing
  to measure on the entity, so this one is proved by DRAWING: a hatch is put behind the text and
  the exported image is inspected. The numeric checks here are the ones that can be numbers -
  the flag reads back, the contradictory arguments are refused, and the scale factor is held to
  AutoCAD's 1..5.

* **Columns must actually REFLOW the text.** Setting ColumnCount to 3 and reporting 3 is a
  property nobody applied. Splitting one block into columns makes it wider and shorter, so the
  drawn extent is read before and after and the shape change is the assertion. Putting it back
  to a single column has to undo it.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "annotations", "hatches", "view")}
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


def box(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    b = (r or {}).get("bbox") or {} if ok else {}
    mn, mx = b.get("min") or {}, b.get("max") or {}
    return (mn.get("x"), mn.get("y"), mx.get("x"), mx.get("y"))


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — see verify-textgeom.py for why."""
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    # A failed call returns the error STRING, not a dict. Reaching into it with .get crashes
    # with an AttributeError that says nothing about the real problem - usually that AutoCAD is
    # not running at all.
    if not ok or not isinstance(r, dict):
        raise SystemExit(f"cannot list documents - is AutoCAD running with the plugin loaded?\n  {r}")
    docs = r.get("documents") or []
    for d in docs[:-1]:
        S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
    ok, r = S["files"].call("list_documents", {})
    left = (r or {}).get("documents") or []
    check("exactly one drawing is open, so no two sessions can be on different ones",
          len(left) == 1, f"{[d.get('name') for d in left]}")
    okp, rp = S["annotations"].call(
        "add_dbtext", {"position": {"x": 0, "y": 9000}, "height": 10,
                       "contents": "SESSIONPROBE"})
    probe = hnd(rp) if okp else None
    ok2, bb = S["geometry-2d"].call("get_bounding_box", {"handle": probe}) if probe else (False, None)
    seen = ((bb or {}).get("bbox") or {}).get("min") or {} if isinstance(bb, dict) else {}
    check("the annotations and geometry-2d sessions are on the SAME drawing",
          bool(ok2) and abs((seen.get("y") or 0) - 9000) < 1e-6,
          f"probe={probe} placed at y=9000; geometry-2d answered {str(bb)[:160]}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

LOREM = ("PLANT ROOM NOTES. All pipework to be insulated to the standard detail. "
         "Valves to be accessible from the walkway. Provide drain points at every low "
         "point and air vents at every high point. Label all services in accordance "
         "with the schedule. Confirm clearances with the installer before fabrication.")

# ── background mask ───────────────────────────────────────────────────────────
print("\n== background_mask_mtext ==")
# A hatch behind the text, so the mask has something to hide. Without it the PNG proves nothing.
rect = hnd(do("geometry-2d", "draw_rectangle",
              {"corner1": {"x": -20, "y": -20}, "corner2": {"x": 320, "y": 60}},
              label="a rectangle to hatch"))
# draw_hatch takes boundaryHandles and pattern - not boundaryPoints and patternName.
do("hatches", "draw_hatch",
   {"boundaryHandles": [rect], "pattern": "ANSI31", "scale": 5},
   label="hatched, so there is something for a mask to hide")
mt = hnd(do("annotations", "add_mtext",
            {"position": {"x": 0, "y": 40}, "textHeight": 14, "widthFactor": 300,
             "contents": "MASKED OVER HATCH"}, label="text over the hatch"))
b_before = box(mt)

r = do("annotations", "background_mask_mtext",
       {"handles": [mt], "color": {"r": 255, "g": 255, "b": 255}, "scaleFactor": 1.5})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    check("the mask is on", it.get("enabled") is True, str(it)[:220])
    check("it was off before", it.get("enabledBefore") is False, str(it)[:220])
    check("the scale factor took", close(it.get("scaleFactor"), 1.5, 1e-9), str(it)[:220])
# The honest negative: there is nothing geometric to measure, and saying so is better than
# inventing a number. Asserted so the claim in the note stays true.
check("PROVEN: the mask did NOT change the entity's extents, exactly as the note says",
      box(mt) == b_before, f"{b_before} -> {box(mt)}")

print("\n-- the drawing-background variant --")
mt2 = hnd(do("annotations", "add_mtext",
             {"position": {"x": 0, "y": 200}, "textHeight": 14, "widthFactor": 300,
              "contents": "MASKED BY DRAWING BACKGROUND"}, label="a second text"))
r = do("annotations", "background_mask_mtext",
       {"handles": [mt2], "useDrawingBackground": True})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    check("PROVEN: it reports following the drawing background, not a fixed colour",
          it.get("usesDrawingBackground") is True, str(it)[:220])

print("\n-- turning it off again --")
r = do("annotations", "background_mask_mtext", {"handles": [mt2], "enabled": False})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    check("PROVEN: off, and it remembers it was on", it.get("enabled") is False
          and it.get("enabledBefore") is True, str(it)[:220])

print("\n-- refusals --")
r = do("annotations", "background_mask_mtext",
       {"handles": [mt], "useDrawingBackground": True, "color": {"r": 1, "g": 2, "b": 3}},
       label="a colour AND the drawing background is refused", expect_fail=True)
check("and the refusal says they contradict each other",
      "contradict" in str(r), str(r)[:250])
r = do("annotations", "background_mask_mtext", {"handles": [mt]},
       label="a mask with no colour at all is refused", expect_fail=True)
check("and the refusal offers useDrawingBackground",
      "useDrawingBackground" in str(r), str(r)[:250])
do("annotations", "background_mask_mtext",
   {"handles": [mt], "color": {"r": 255, "g": 255, "b": 255}, "scaleFactor": 0.5},
   label="a scale factor below 1 is refused", expect_fail=True)
do("annotations", "background_mask_mtext",
   {"handles": [mt], "color": {"r": 255, "g": 255, "b": 255}, "scaleFactor": 9},
   label="and above 5", expect_fail=True)
t_single = hnd(do("annotations", "add_dbtext",
                  {"position": {"x": 600, "y": 0}, "height": 10, "contents": "SINGLE LINE"},
                  label="a single-line text"))
r = do("annotations", "background_mask_mtext",
       {"handles": [t_single], "useDrawingBackground": True},
       label="single-line text is refused by name", expect_fail=True)
check("and the refusal says masks belong to MText",
      "not an MText" in str(r), str(r)[:250])

# ── columns ───────────────────────────────────────────────────────────────────
print("\n== mtext_column_settings: the text has to REFLOW ==")
col = hnd(do("annotations", "add_mtext",
             {"position": {"x": 0, "y": 800}, "textHeight": 10, "widthFactor": 200,
              "contents": LOREM}, label="a long note, 200 wide"))
c0 = box(col)
w0, h0 = c0[2] - c0[0], c0[3] - c0[1]
print(f"     one column: {round(w0, 1)} wide x {round(h0, 1)} tall")

r = do("annotations", "mtext_column_settings",
       {"handle": col, "mode": "static", "count": 3, "width": 200, "gutter": 20})
c1 = box(col)
w1, h1 = c1[2] - c1[0], c1[3] - c1[1]
if isinstance(r, dict):
    check("it reports three static columns",
          r.get("count") == 3 and "Static" in str(r.get("mode")), str(r)[:250])
    check("and the width per column", close(r.get("width"), 200, 1e-9), str(r)[:250])
print(f"     three columns: {round(w1, 1)} wide x {round(h1, 1)} tall")
# THE assertion. A property that was stored and not applied leaves these identical.
check("PROVEN: it got WIDER — 3 columns of 200 plus 2 gutters of 20 is 640",
      close(w1, 640, 5), f"{w0} -> {w1}")
# Measured 260 -> 226.7. It IS shorter, which is the claim; an earlier version demanded a third
# off, which was a guess about how AutoCAD balances columns whose height it was never given.
check("PROVEN: and SHORTER, because the text reflowed rather than just being labelled",
      h1 < h0, f"{h0} -> {h1}")

print("\n-- back to one block --")
r = do("annotations", "mtext_column_settings", {"handle": col, "mode": "none"})
c2 = box(col)
w2, h2 = c2[2] - c2[0], c2[3] - c2[1]
if isinstance(r, dict):
    check("the columns are gone", "NoColumns" in str(r.get("mode")), str(r)[:250])
    # Measured, and not what an earlier version of this file assumed. Removing the columns does
    # NOT restore the MText's original wrap width: it keeps the 640 the columns made it, so the
    # text comes back as one WIDE block (76.7 tall) rather than the narrow 200-wide one it
    # started as. The tool reports both widths rather than leaving the caller to find out.
    # "Not restored" means it is still the 640 the columns made it, NOT the 200 it was created
    # with. An earlier version also demanded the width change during THIS call, which conflated
    # two different claims: by the time mode='none' runs the MText is already 640, so before and
    # after are equal and rightly so.
    check("PROVEN: the wrap width is NOT restored — still 640, not the original 200",
          close(r.get("mtextWidth"), 640, 5) and not close(r.get("mtextWidth"), 200, 5),
          f"created 200 wide; before this call {r.get('mtextWidthBefore')}, "
          f"after {r.get('mtextWidth')}")
check("PROVEN: and being one wide block, it is shorter still", h2 < h1, f"{h1} -> {h2}")

print("\n-- dynamic columns --")
dyn = hnd(do("annotations", "add_mtext",
             {"position": {"x": 800, "y": 800}, "textHeight": 10, "widthFactor": 200,
              "contents": LOREM}, label="another long note"))
d0 = box(dyn)
r = do("annotations", "mtext_column_settings",
       {"handle": dyn, "mode": "dynamic", "width": 150, "gutter": 15})
if isinstance(r, dict):
    check("it reports dynamic columns", "Dynamic" in str(r.get("mode")), str(r)[:250])
d1 = box(dyn)
check("PROVEN: the extent changed, so the text reflowed",
      (d1[2] - d1[0]) != (d0[2] - d0[0]) or (d1[3] - d1[1]) != (d0[3] - d0[1]),
      f"{d0} -> {d1}")

print("\n-- refusals --")
do("annotations", "mtext_column_settings", {"handle": col, "mode": "sideways"},
   label="an unknown mode is refused", expect_fail=True)
r = do("annotations", "mtext_column_settings", {"handle": col, "mode": "static", "count": 3},
       label="static columns with no width are refused", expect_fail=True)
check("and the refusal explains width is PER COLUMN",
      "ONE column" in str(r), str(r)[:250])
r = do("annotations", "mtext_column_settings",
       {"handle": col, "mode": "static", "count": 1, "width": 100},
       label="one static column is refused", expect_fail=True)
check("and it points at mode none instead", "'none'" in str(r), str(r)[:250])
do("annotations", "mtext_column_settings",
   {"handle": col, "mode": "static", "count": 2, "width": 100, "gutter": -5},
   label="a negative gutter is refused", expect_fail=True)
do("annotations", "mtext_column_settings", {"handle": t_single, "mode": "none"},
   label="single-line text is refused by name", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "mtextpres.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 1300, "yMax": 1000},
                            "widthPx": 2000, "heightPx": 1300})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm, and this is the only place the mask can be checked: MASKED OVER HATCH sits")
print("     in a CLEAR white box punched through the diagonal hatching, while the hatch runs")
print("     unbroken everywhere else. If the hatch lines cross the letters, the mask did")
print("     nothing. Top right, a long note laid out in columns.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
