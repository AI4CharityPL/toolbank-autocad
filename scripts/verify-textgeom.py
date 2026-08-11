# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.3 tranche 2 — justification, fitting, scaling in place.

What these three have in common is that the obvious implementation MOVES THE TEXT and reports
success either way. So every check here is a position or a size read back off the entity, never
the tool's own account of itself:

* **set_text_justification.** Setting `DBText.Justify` on its own relocates the text, because
  the justification decides which point of it sits on the alignment point - same anchor, new
  meaning. AutoCAD has JUSTIFYTEXT precisely because the naive version is wrong. Asserted by
  comparing the bounding box before and after, for every justification in turn.

* **text_fit** must change the WIDTH and leave the HEIGHT. A tool that scaled the text would
  span the two points perfectly and be wrong, so both are measured.

* **scale_text_in_place** must leave each text where it is. The control is `modify.scale` on an
  identical pair: that one drags them towards a common base point, and the difference between
  the two results is the entire reason this tool exists.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "annotations", "modify", "view")}
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


def at_most(v, limit):
    """v is present and <= limit.

    Written out because `(v or SENTINEL) <= limit` is wrong whenever 0 is the CORRECT answer:
    0 is falsy, so the `or` throws the real measurement away and substitutes the sentinel,
    turning a perfect result into a failure. That has now happened three times in this project
    - approximationError, movedBy, and jogOffset all legitimately measure exactly 0.
    """
    return v is not None and abs(v) <= limit


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return (r or {}).get("bbox") or {} if ok else {}


def box(h):
    """(minx, miny, maxx, maxy) — read off the entity, not off any tool's report."""
    b = bbox(h)
    mn, mx = b.get("min") or {}, b.get("max") or {}
    return (mn.get("x"), mn.get("y"), mx.get("x"), mx.get("y"))


def same_box(a, b, tol=1e-6):
    return all(x is not None and y is not None and abs(x - y) <= tol for x, y in zip(a, b))


def text(x, y, s, h=10, layer=None):
    args = {"position": {"x": x, "y": y}, "height": h, "contents": s}
    if layer: args["layer"] = layer
    return hnd(S["annotations"].call("add_dbtext", args)[1])



def fresh_drawing():
    """A new drawing, and ONLY that drawing open.

    Every verification script calls new_document, and none of them ever closed the drawing it
    replaced - so the runs pile up. With more than one document open the backend processes
    behind these sessions land on DIFFERENT ones, and then a handle created by `annotations`
    resolves to a different entity in `geometry-2d`, or to nothing at all. That is far worse
    than an error: it is two tools quietly measuring two drawings.

    Measured while it was happening: annotations wrote SELFTEST at y=7000 and saw it; geometry-2d
    found nothing in that window; files reported Rysunek1.dwg and Rysunek2.dwg both open.

    So: make the new drawing, close every other one, and PROVE the sessions agree before
    measuring anything.
    """
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    # A failed call returns the error STRING, not a dict. Reaching into it with .get crashes
    # with an AttributeError that says nothing about the real problem - usually that AutoCAD is
    # not running at all.
    if not ok or not isinstance(r, dict):
        raise SystemExit(f"cannot list documents - is AutoCAD running with the plugin loaded?\n  {r}")
    docs = r.get("documents") or []
    # The new one is the last created; close the rest. They are unsaved scratch drawings from
    # earlier runs, which is the only reason this is safe to do unasked.
    for d in docs[:-1]:
        S["files"].call("close_document", {"path": d.get("path") or d.get("name"),
                                           "save": False})
    ok, r = S["files"].call("list_documents", {})
    left = (r or {}).get("documents") or []
    check("exactly one drawing is open, so no two sessions can be on different ones",
          len(left) == 1, f"{[d.get('name') for d in left]}")

    # Cross-session agreement, proved rather than assumed.
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

# ── set_text_justification ────────────────────────────────────────────────────
print("\n== set_text_justification: the anchor changes, the text does not ==")
t1 = text(0, 0, "ANCHOR TEST")
b0 = box(t1)
check("the text has an extent to start with", None not in b0, f"{b0}")

for j in ("TopLeft", "MiddleCenter", "BottomRight", "BaseCenter", "TopRight", "BaseLeft"):
    r = do("annotations", "set_text_justification", {"handles": [t1], "justification": j},
           label=f"justify {j}")
    now = box(t1)
    # THE claim, measured off the entity each time. A naive implementation passes every other
    # assertion in this file and leaves the text somewhere else on the sheet.
    check(f"  PROVEN: it did not move ({j})", same_box(b0, now),
          f"{b0} -> {now}")
    if isinstance(r, dict):
        it = (r.get("items") or [{}])[0]
        check(f"  and it reports the drift it measured as ~0 ({j})",
              at_most(it.get("movedBy"), 1e-6), str(it)[:200])

print("\n-- an MText re-justifies too --")
m1 = hnd(do("annotations", "add_mtext",
            {"position": {"x": 0, "y": 200}, "textHeight": 10, "contents": "MTEXT ANCHOR"},
            label="an mtext"))
mb = box(m1)
do("annotations", "set_text_justification", {"handles": [m1], "justification": "MiddleCenter"},
   label="justify it to MiddleCenter")
check("PROVEN: the mtext did not move either", same_box(mb, box(m1)), f"{mb} -> {box(m1)}")

print("\n-- refusals --")
do("annotations", "set_text_justification", {"handles": [t1], "justification": "Sideways"},
   label="an unknown justification is refused", expect_fail=True)
do("annotations", "set_text_justification", {"handles": [t1]},
   label="a missing justification is refused", expect_fail=True)
ln = hnd(do("geometry-2d", "draw_line",
            {"start": {"x": 0, "y": -100}, "end": {"x": 100, "y": -100}}, label="a line"))
r = do("annotations", "set_text_justification", {"handles": [ln], "justification": "TopLeft"},
       label="a line is refused by name", expect_fail=True)

# ── text_fit ──────────────────────────────────────────────────────────────────
print("\n== text_fit: the width changes, the height does not ==")
t2 = text(0, 400, "FIT ME", h=10)
w_before = box(t2)[2] - box(t2)[0]
h_before = box(t2)[3] - box(t2)[1]
r = do("annotations", "text_fit",
       {"handle": t2, "point1": {"x": 0, "y": 400}, "point2": {"x": 500, "y": 400}})
fb = box(t2)
if isinstance(r, dict):
    check("it reports the span it was given", close(r.get("span"), 500, 1e-6), str(r)[:220])
    check("and that the height did not change",
          close(r.get("height"), r.get("heightBefore"), 1e-9), str(r)[:220])
# MEASURED off the entity: a fit stretches sideways only.
check("PROVEN: the text now spans 500 in x", close(fb[2] - fb[0], 500, 0.5),
      f"width {fb[2] - fb[0]} from {w_before}")
check("PROVEN: and its height is untouched — this is a fit, not a scale",
      close(fb[3] - fb[1], h_before, 1e-6), f"{h_before} -> {fb[3] - fb[1]}")

print("\n-- squeezing works as well as stretching --")
t3 = text(0, 600, "SQUEEZE THIS LONG TEXT", h=10)
w3 = box(t3)[2] - box(t3)[0]
h3 = box(t3)[3] - box(t3)[1]
do("annotations", "text_fit",
   {"handle": t3, "point1": {"x": 0, "y": 600}, "point2": {"x": 60, "y": 600}},
   label="fit a long text into 60")
sb = box(t3)
check("PROVEN: it narrowed to 60", close(sb[2] - sb[0], 60, 0.5),
      f"{w3} -> {sb[2] - sb[0]}")
check("PROVEN: and is still the same height", close(sb[3] - sb[1], h3, 1e-6),
      f"{h3} -> {sb[3] - sb[1]}")

print("\n-- refusals --")
do("annotations", "text_fit", {"handle": t2, "point1": {"x": 0, "y": 0}},
   label="a missing point2 is refused", expect_fail=True)
do("annotations", "text_fit",
   {"handle": t2, "point1": {"x": 5, "y": 5}, "point2": {"x": 5, "y": 5}},
   label="two identical points are refused", expect_fail=True)
r = do("annotations", "text_fit",
       {"handle": m1, "point1": {"x": 0, "y": 0}, "point2": {"x": 100, "y": 0}},
       label="an MText is refused by name", expect_fail=True)
check("and the refusal explains that MText wraps instead of stretching",
      "wraps" in str(r), str(r)[:250])

# ── scale_text_in_place, against a control ────────────────────────────────────
print("\n== scale_text_in_place vs modify.scale — the control that gives it a reason ==")
a1 = text(0, 900, "NEAR", h=10)
a2 = text(500, 900, "FAR", h=10)
b1 = text(0, 1100, "NEAR", h=10)
b2 = text(500, 1100, "FAR", h=10)
a1_before, a2_before = box(a1), box(a2)
b1_before, b2_before = box(b1), box(b2)

r = do("annotations", "scale_text_in_place", {"handles": [a1, a2], "factor": 2})
if isinstance(r, dict):
    items = {i.get("handle"): i for i in (r.get("items") or [])}
    check("both heights doubled to 20",
          all(close(i.get("height"), 20, 1e-9) for i in items.values()), str(items)[:250])
    check("and it reports no drift",
          all(at_most(i.get("movedBy"), 1e-6) for i in items.values()), str(items)[:250])
# MEASURED: the left edge of each is where it was.
check("PROVEN: the near text did not move", close(box(a1)[0], a1_before[0], 1e-6),
      f"{a1_before[0]} -> {box(a1)[0]}")
check("PROVEN: nor did the far one, 500 away from it",
      close(box(a2)[0], a2_before[0], 1e-6), f"{a2_before[0]} -> {box(a2)[0]}")
check("PROVEN: and both really are taller",
      box(a1)[3] - box(a1)[1] > (a1_before[3] - a1_before[1]) * 1.5,
      f"{a1_before[3] - a1_before[1]} -> {box(a1)[3] - box(a1)[1]}")

print("\n-- THE CONTROL: modify.scale on the identical pair --")
do("modify", "scale", {"handles": [b1, b2], "center": {"x": 0, "y": 1100}, "factor": 2},
   label="scaling them about a common base point")
check("PROVEN: the near one held, because it sits ON the base point",
      close(box(b1)[0], b1_before[0], 1e-6), f"{b1_before[0]} -> {box(b1)[0]}")
# This is the difference. modify.scale drags the far one out to 1000; scale_text_in_place
# left its twin at 500. Without this arm, "it did not move" proves nothing about the tool.
check("PROVEN: the far one was DRAGGED to about 1000 — which is what this tool must not do",
      box(b2)[0] > 900, f"{b2_before[0]} -> {box(b2)[0]}")

print("\n-- newHeight sets an absolute size --")
c1 = text(0, 1300, "SMALL", h=5)
c2 = text(200, 1300, "BIG", h=25)
do("annotations", "scale_text_in_place", {"handles": [c1, c2], "newHeight": 12},
   label="both to height 12")
r = do("annotations", "scale_text_in_place", {"handles": [c1, c2], "factor": 1},
       label="(reading the heights back)")
if isinstance(r, dict):
    check("PROVEN: both are now 12, regardless of what they were",
          all(close(i.get("height"), 12, 1e-9) for i in (r.get("items") or [])),
          str(r.get("items"))[:250])

print("\n-- refusals --")
do("annotations", "scale_text_in_place", {"handles": [c1]},
   label="neither factor nor newHeight is refused", expect_fail=True)
do("annotations", "scale_text_in_place", {"handles": [c1], "factor": 2, "newHeight": 5},
   label="both together are refused", expect_fail=True)
do("annotations", "scale_text_in_place", {"handles": [c1], "factor": 0},
   label="a zero factor is refused", expect_fail=True)
do("annotations", "scale_text_in_place", {"handles": [ln], "factor": 2},
   label="a line is refused by name", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "textgeom.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -100, "yMin": -200, "xMax": 1100, "yMax": 1400},
                            "widthPx": 1600, "heightPx": 2000})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: FIT ME stretched wide and thin over 500 while keeping its letter height,")
print("     SQUEEZE THIS LONG TEXT crushed narrow at the same height, and two rows of")
print("     NEAR/FAR: the lower row still 500 apart after doubling, the upper row (the")
print("     control) dragged out to about 1000. ANCHOR TEST sits exactly where it started.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
