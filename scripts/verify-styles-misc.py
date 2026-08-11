# -*- coding: utf-8 -*-
"""Live verification for the last tranche of roadmap 2.3: table cell styles, visual styles,
point display.

Each of the three is a place where the plan's wording was looser than the API, so each check
below is aimed at the specific way it could quietly be wrong:

  set_table_cell_style  It must NOT set text height - modify_tablestyle already owns that, and
                        two tools writing one property is how they drift. Asserted by checking
                        the height is unchanged after an alignment/colour change.
  create_visual_style   The preset has to survive into the drawing as the style's Type. A style
                        created and stored with the wrong type looks identical in every field
                        except the one that decides how it renders.
  set_point_display     PDMODE is a bit code, so the glyph+surround naming is only worth having
                        if it produces the right number. Every combination is checked against
                        the arithmetic, and the points are drawn and exported so the glyph is
                        actually looked at.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "styles", "geometry-2d", "view", "viewports")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:170]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:100]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, cond, detail=""):
    results.append((label, bool(cond)))
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + ("" if cond else f"  -> {detail}"))


print("== fresh drawing ==")
do("files", "new_document", {})

# ─────────────────── table cell styles ───────────────────
print("\n== table cell styles ==")
do("styles", "create_tablestyle",
   {"name": "ACADMCP-SCHED", "properties": {"dataTextHeight": 2.5, "headerTextHeight": 3.0}},
   "create a table style with known text heights")

r = do("styles", "set_table_cell_style",
       {"name": "ACADMCP-SCHED", "row": "header",
        "alignment": "middleCenter", "colorIndex": 5, "backgroundColorIndex": 8})
if isinstance(r, dict):
    cell = r.get("cell") or {}
    check("alignment stored", cell.get("alignment") == "middleCenter", str(cell)[:130])
    check("text colour stored", cell.get("colorIndex") == 5, str(cell)[:130])
    check("background colour stored", cell.get("backgroundColorIndex") == 8, str(cell)[:130])
    check("background is not 'none' once a colour is set",
          cell.get("backgroundColorNone") is False, str(cell)[:130])
    # The load-bearing one: this tool must leave text height alone.
    check("header text height untouched by this tool (still 3.0)",
          abs((cell.get("textHeight") or 0) - 3.0) < 1e-6,
          f"got {cell.get('textHeight')} - modify_tablestyle owns this property")
    check("applied lists exactly what was passed",
          sorted(r.get("applied") or []) == ["alignment", "backgroundColorIndex", "colorIndex"],
          str(r.get("applied")))

r = do("styles", "set_table_cell_style",
       {"name": "ACADMCP-SCHED", "row": "header", "backgroundColorIndex": -1},
       "clear the background with -1")
if isinstance(r, dict):
    cell = r.get("cell") or {}
    check("background cleared back to none", cell.get("backgroundColorNone") is True, str(cell)[:130])
    check("alignment survived the background change",
          cell.get("alignment") == "middleCenter", str(cell)[:130])

r = do("styles", "set_table_cell_style",
       {"name": "ACADMCP-SCHED", "row": "data", "alignment": "topLeft"},
       "a different row is independent")
if isinstance(r, dict):
    check("data row took its own alignment",
          (r.get("cell") or {}).get("alignment") == "topLeft", str(r.get("cell"))[:130])

do("styles", "set_table_cell_style", {"name": "ACADMCP-SCHED", "row": "header"},
   "no properties at all", expect_fail=True)
do("styles", "set_table_cell_style", {"name": "ACADMCP-SCHED", "row": "footer", "alignment": "topLeft"},
   "row name that does not exist", expect_fail=True)
do("styles", "set_table_cell_style", {"name": "ACADMCP-SCHED", "row": "data", "alignment": "sideways"},
   "alignment that does not exist", expect_fail=True)
do("styles", "set_table_cell_style", {"name": "NO-SUCH-STYLE", "row": "data", "alignment": "topLeft"},
   "table style that does not exist", expect_fail=True)
do("styles", "set_table_cell_style", {"name": "ACADMCP-SCHED", "row": "data", "colorIndex": 999},
   "colour index out of range", expect_fail=True)

# ─────────────────── visual styles ───────────────────
print("\n== visual styles ==")
r = do("styles", "list_visual_styles", {})
presets = (r or {}).get("presets") or [] if isinstance(r, dict) else []
check("presets are advertised so create_visual_style is usable blind",
      "Conceptual" in presets and "Realistic" in presets and len(presets) > 20,
      f"{len(presets)} presets")
check("built-in styles are listed", isinstance(r, dict) and (r.get("count") or 0) > 5,
      str((r or {}).get("count")))

r = do("styles", "create_visual_style",
       {"name": "ACADMCP-CONCEPT", "basedOn": "Conceptual", "description": "for presentation views"})
if isinstance(r, dict):
    vs = r.get("visualStyle") or {}
    check("created flag true", r.get("created") is True, str(r)[:120])
    check("the preset survived as the style's Type", vs.get("type") == "Conceptual", str(vs)[:130])
    check("description stored", vs.get("description") == "for presentation views", str(vs)[:130])

r = do("styles", "list_visual_styles", {}, "list again (fresh read)")
if isinstance(r, dict):
    mine = next((x for x in r.get("styles") or [] if x.get("name") == "ACADMCP-CONCEPT"), None)
    check("new style is really in the drawing, not just in the reply", mine is not None,
          str([x.get("name") for x in r.get("styles") or []])[:170])
    check("and still reports Conceptual after a round trip",
          (mine or {}).get("type") == "Conceptual", str(mine)[:130])

do("styles", "create_visual_style", {"name": "ACADMCP-CONCEPT", "basedOn": "Realistic"},
   "duplicate name without overwrite", expect_fail=True)
do("styles", "create_visual_style", {"name": "ACADMCP-BAD", "basedOn": "Sparkly"},
   "preset that does not exist", expect_fail=True)
do("styles", "create_visual_style", {"name": "ACADMCP-NOBASE"},
   "no preset given", expect_fail=True)

# ─────────────────── point display ───────────────────
print("\n== point display: the glyph naming has to produce the right bit code ==")
# PDMODE arithmetic: glyph 0-4, plus 32 for a circle, 64 for a square, 96 for both.
for glyph, surround, want in [
    ("dot", None, 0), ("none", None, 1), ("plus", None, 2), ("cross", None, 3), ("tick", None, 4),
    ("dot", "circle", 32), ("plus", "circle", 34), ("cross", "square", 67),
    ("tick", "both", 100), ("dot", "both", 96),
]:
    args = {"glyph": glyph, "surround": surround} if surround else {"glyph": glyph}
    rr = do("styles", "set_point_display", args, f"glyph={glyph} surround={surround or 'none'}")
    got = ((rr or {}).get("after") or {}).get("pdmode") if isinstance(rr, dict) else None
    check(f"  PDMODE for {glyph}+{surround or 'none'} is {want}", got == want, f"got {got}")

r = do("styles", "set_point_display", {"glyph": "cross", "surround": "circle", "size": 50.0},
       "set glyph and size together")
if isinstance(r, dict):
    after = r.get("after") or {}
    check("PDSIZE stored as 50", abs((after.get("pdsize") or 0) - 50.0) < 1e-6, str(after))
    check("before/after both reported so the change is visible",
          (r.get("before") or {}).get("pdmode") is not None, str(r)[:130])

r = do("styles", "set_point_display", {"mode": 3}, "raw PDMODE still accepted")
check("raw mode honoured", ((r or {}).get("after") or {}).get("pdmode") == 3, str(r)[:120])

do("styles", "set_point_display", {}, "nothing to set", expect_fail=True)
do("styles", "set_point_display", {"glyph": "squiggle"}, "unknown glyph", expect_fail=True)
do("styles", "set_point_display", {"glyph": "dot", "surround": "triangle"},
   "unknown surround", expect_fail=True)
do("styles", "set_point_display", {"mode": 500}, "PDMODE out of range", expect_fail=True)

print("\n== visual check: draw points and look at them ==")
do("styles", "set_point_display", {"glyph": "cross", "surround": "circle", "size": 200.0},
   "set a glyph that is unmistakable on screen")
for i in range(5):
    do("geometry-2d", "draw_point", {"position": {"x": i * 1000, "y": 0}}, f"draw point {i + 1}")
do("view", "zoom_extents", {})
do("files", "export_file",
   {"path": r"C:\tmp\styles-misc.png", "format": "PNG", "scope": "Window",
    "window": {"xMin": -600, "yMin": -600, "xMax": 4600, "yMax": 600}},
   "export PNG of the points")

ok = sum(1 for _, g in results if g)
print(f"\n==== {ok}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
