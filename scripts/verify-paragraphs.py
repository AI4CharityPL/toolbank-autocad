# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.3 tranche 6 — paragraph format, bullets, MText frame.

All three write into an MText and can be "set" without anything happening on the drawing, so
each is measured on something outside the property that was written:

* **set_paragraph_format** — the drawn EXTENT. An indent pushes the text right; ranging it right
  inside its own width pushes the left edge right. Both are read before and after. Alignment only
  means anything when the MText HAS a width, so the zero-width case is asserted to be refused
  rather than quietly doing nothing.

* **mtext_bullets_numbering** — the RENDERED text. The markers must be in front of the words, not
  written over them, and switching styles must not leave the old marker behind. The second is the
  one a count cannot catch: "1.  * ITEM" is one paragraph with one marker as far as any tally is
  concerned.

* **set_mtext_frame** went on trial and LOST. `MText.ShowBorders` is the only frame property the
  2025 managed API has, and it accepts the assignment, reads back true, and draws nothing: the
  extents were 300 x 10 before and after, and the image showed no border. It is withdrawn - the
  same call made on blend_curves' smooth continuity - and this file now asserts it stays
  unadvertised rather than exercising it.

One thing this file got wrong first, worth keeping: an indent was measured on the MText's own
extents, which are its WIDTH BOX and not its ink. That read 0 -> 0 for an indent the picture
plainly showed working. The measurement now explodes a copy and reads where the lines land.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "annotations", "view")}
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


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def box(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    b = (r or {}).get("bbox") or {} if ok else {}
    mn, mx = b.get("min") or {}, b.get("max") or {}
    return (mn.get("x"), mn.get("y"), mx.get("x"), mx.get("y"))


def rendered(h):
    ok, r = S["annotations"].call("list_text_by_pattern",
                                  {"pattern": ".", "regex": True, "handles": [h]})
    for hit in ((r or {}).get("results") or []):
        return hit.get("text")
    return None


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — see verify-textgeom.py for why."""
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    if not ok or not isinstance(r, dict):
        raise SystemExit(f"cannot list documents - is AutoCAD running with the plugin loaded?\n  {r}")
    for d in (r.get("documents") or [])[:-1]:
        S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
    ok, r = S["files"].call("list_documents", {})
    left = (r or {}).get("documents") or []
    check("exactly one drawing is open, so no two sessions can be on different ones",
          len(left) == 1, f"{[d.get('name') for d in left]}")
    okp, rp = S["annotations"].call(
        "add_dbtext", {"position": {"x": 0, "y": 9000}, "height": 10, "contents": "SESSIONPROBE"})
    probe = hnd(rp) if okp else None
    ok2, bb = S["geometry-2d"].call("get_bounding_box", {"handle": probe}) if probe else (False, None)
    seen = ((bb or {}).get("bbox") or {}).get("min") or {} if isinstance(bb, dict) else {}
    check("the annotations and geometry-2d sessions are on the SAME drawing",
          bool(ok2) and abs((seen.get("y") or 0) - 9000) < 1e-6,
          f"probe={probe} placed at y=9000; geometry-2d answered {str(bb)[:160]}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})



def ink_left(mtext_handle):
    """Where the text actually STARTS, measured by exploding a copy.

    An MText's own extents are its WIDTH BOX, not its ink: one 400 wide at x=0 measures 0..400
    whether the text inside is flush left, indented 60, or ranged right. The first version of
    this file measured that box and read 0 -> 0 for an indent the exported image plainly shows
    working - the wrong instrument, not a broken tool.

    Exploding gives one DBText per line, and those sit where the ink sits. The copies are
    deleted again so they cannot pollute the next measurement or the picture.
    """
    ok, r = S["annotations"].call("explode_mtext_to_text",
                                  {"handle": mtext_handle, "keepOriginal": True})
    if not ok or not isinstance(r, dict):
        return None
    lefts = []
    for e in (r.get("entities") or []):
        h = e.get("handle")
        b = box(h)
        if b[0] is not None:
            lefts.append(b[0])
        S["geometry-2d"].call("delete_entities", {"handles": [h]})
    return min(lefts) if lefts else None

def mtext(x, y, contents, width=0, h=10):
    return hnd(S["annotations"].call(
        "add_mtext", {"position": {"x": x, "y": y}, "textHeight": h,
                      "width": width, "contents": contents})[1])


print("== fresh drawing ==")
fresh_drawing()

THREE = "ALPHA ITEM\\PBETA ITEM\\PGAMMA ITEM"

# ── set_paragraph_format ──────────────────────────────────────────────────────
print("\n== set_paragraph_format: an indent has to MOVE the text ==")
p1 = mtext(0, 400, THREE, width=400)
i0 = ink_left(p1)
r = do("annotations", "set_paragraph_format", {"handle": p1, "indentLeft": 60})
i1 = ink_left(p1)
if isinstance(r, dict):
    check("it wrote a paragraph code", (r.get("code") or "").startswith("\\p"), str(r)[:250])
    check("PROVEN: on all three paragraphs, not just the first",
          (r.get("stored") or "").count("\\pl60;") == 3, repr(r.get("stored"))[:250])
# THE measurement. A code that was stored and not applied leaves the left edge where it was.
check("PROVEN: the text moved right by the 60 indent",
      i1 is not None and i0 is not None and abs((i1 - i0) - 60) < 1.0,
      f"ink left {i0} -> {i1}")
check("and the words survived the code",
      "ALPHA ITEM" in (rendered(p1) or ""), f"{rendered(p1)!r}")

print("\n-- setting it twice does not stack the codes --")
r = do("annotations", "set_paragraph_format", {"handle": p1, "indentLeft": 20})
if isinstance(r, dict):
    check("PROVEN: one code per paragraph, the old one replaced not appended",
          (r.get("stored") or "").count("\\p") == 3, repr(r.get("stored"))[:250])
i2 = ink_left(p1)
check("PROVEN: and the text moved back to a 20 indent",
      i2 is not None and i0 is not None and abs((i2 - i0) - 20) < 1.0, f"{i0} -> {i2}")

print("\n-- align right needs a width, and moves the text within it --")
p2 = mtext(600, 400, "SHORT\\PA MUCH LONGER LINE OF TEXT", width=400)
k0 = ink_left(p2)
r = do("annotations", "set_paragraph_format", {"handle": p2, "align": "right"})
k1 = ink_left(p2)
check("PROVEN: ranging right pushed the text right, inside the same width box",
      k1 is not None and k0 is not None and k1 > k0 + 1, f"ink left {k0} -> {k1}")

print("\n-- a zero-width mtext has nothing to align WITHIN, and says so --")
p3 = mtext(0, 200, "NO WIDTH HERE")
r = do("annotations", "set_paragraph_format", {"handle": p3, "align": "right"},
       label="align right on a zero-width mtext is refused", expect_fail=True)
check("and the refusal explains why rather than doing nothing",
      "no width" in str(r) and "longest line" in str(r), str(r)[:280])
do("annotations", "set_paragraph_format", {"handle": p3, "align": "left"},
   label="but align left is fine, since it moves nothing anyway")

print("\n-- refusals --")
do("annotations", "set_paragraph_format", {"handle": p1},
   label="a call that changes nothing is refused", expect_fail=True)
do("annotations", "set_paragraph_format", {"handle": p1, "align": "sideways"},
   label="an unknown alignment is refused", expect_fail=True)
do("annotations", "set_paragraph_format", {"handle": p1, "lineSpacing": 9},
   label="a line spacing outside 0.25..4 is refused", expect_fail=True)

# ── mtext_bullets_numbering ───────────────────────────────────────────────────
print("\n== mtext_bullets_numbering ==")
b1h = mtext(0, 0, THREE, width=400)
r = do("annotations", "mtext_bullets_numbering", {"handle": b1h, "style": "bullet"})
if isinstance(r, dict):
    check("three paragraphs got a marker", r.get("paragraphs") == 3, str(r)[:250])
    check("PROVEN: the bullets are in the rendered text",
          (r.get("rendered") or "").count("•") == 3, repr(r.get("rendered"))[:250])
    check("and the words are still there, in front of nothing",
          all(w in (r.get("rendered") or "") for w in ("ALPHA ITEM", "BETA ITEM", "GAMMA ITEM")),
          repr(r.get("rendered"))[:250])

print("\n-- switching to numbers must not leave the bullets behind --")
r = do("annotations", "mtext_bullets_numbering", {"handle": b1h, "style": "numbered"})
got = (r or {}).get("rendered") if isinstance(r, dict) else rendered(b1h)
# THE check a count cannot make. "1.  • ALPHA ITEM" is one paragraph with one marker as far as
# any tally is concerned, and reads as a mess on the sheet.
check("PROVEN: no bullet survives alongside the numbers",
      "•" not in (got or ""), repr(got)[:250])
check("PROVEN: and they are numbered 1, 2, 3",
      all(f"{i}." in (got or "") for i in (1, 2, 3)), repr(got)[:250])

print("\n-- lettered, and then off again --")
r = do("annotations", "mtext_bullets_numbering", {"handle": b1h, "style": "lettered"})
got = (r or {}).get("rendered") if isinstance(r, dict) else ""
check("PROVEN: a, b, c and no leftover digits",
      all(f"{c}." in (got or "") for c in "abc") and "1." not in (got or ""), repr(got)[:250])
r = do("annotations", "mtext_bullets_numbering", {"handle": b1h, "style": "none"})
got = (r or {}).get("rendered") if isinstance(r, dict) else ""
check("PROVEN: every marker is gone and the words remain",
      "•" not in (got or "") and "a." not in (got or "")
      and "ALPHA ITEM" in (got or ""), repr(got)[:250])

print("\n-- refusals --")
do("annotations", "mtext_bullets_numbering", {"handle": b1h, "style": "roman"},
   label="an unknown style is refused", expect_fail=True)
t1 = hnd(do("annotations", "add_dbtext",
            {"position": {"x": 0, "y": -200}, "height": 10, "contents": "SINGLE"},
            label="a single-line text"))
do("annotations", "mtext_bullets_numbering", {"handle": t1},
   label="single-line text is refused by name", expect_fail=True)

# ── set_mtext_frame: tried, measured, WITHDRAWN ───────────────────────────────
print("\n== set_mtext_frame was withdrawn, and this asserts it stays withdrawn ==")
# It was built and it does not work. MText.ShowBorders is the only frame property the 2025
# managed API exposes - TextFrame and DrawFrame do not exist - and it accepts the assignment,
# reads back true, and DRAWS NOTHING. Measured two ways that agreed: the entity's extents were
# 300 x 10 before and 300 x 10 after, and the exported image showed FRAMED TEXT with no border.
#
# A tool that sets a property and changes the drawing not at all is worse than a missing one,
# because it reports success. The [McpTool] attribute is off, so the tool is no longer
# advertised - asserted here so it cannot come back unnoticed.
ok_, r_ = S["annotations"].call("set_mtext_frame", {"handles": [p1], "enabled": True})
check("PROVEN: set_mtext_frame is no longer advertised — it draws nothing and was withdrawn",
      (not ok_) and ("not found" in str(r_) or "UnknownTool" in str(r_)), str(r_)[:250])

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "paragraphs.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -80, "yMin": -260, "xMax": 1600, "yMax": 500},
                            "widthPx": 2000, "heightPx": 900})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: top left, three lines indented from the left margin. Middle, SHORT and A")
print("     MUCH LONGER LINE ranged RIGHT so their right edges align. Bottom left, ALPHA /")
print("     BETA / GAMMA with their markers stripped again — no bullets, no numbers, no")
print("     letters, and no leftovers of any of them.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
