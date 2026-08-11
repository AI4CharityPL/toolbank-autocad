# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.4 — the acad-selection extensions, 11 tools.

A selection tool fails by returning a plausible-looking set that is not the right one, and the
worst version is a tool that ignores its criteria and returns everything. So every check here has
a NEGATIVE half: something that must be found AND something that must not.

  * select_similar is given a drawing seeded so that the answer is neither "one" nor "all": three
    lines on one layer, two on another, plus circles. Matching on class alone, class+layer, and
    class+layer+colour must give THREE DIFFERENT counts, which a tool ignoring its flags cannot do.
  * the range selectors are checked against arithmetic - a 100x50 rectangle has area 5000 and
    perimeter 300 - and against `measurable`, since a count of zero has two very different causes.
  * select_duplicates gets a deliberate exact copy AND a near-copy just outside tolerance: it must
    report the first and not the second.
  * isolate/hide/unisolate are checked by reading `visible` back off the entities themselves
    through a different tool, not by trusting the counts the writer reported.
  * a saved filter is applied AFTER the drawing has changed, so it must select by its stored
    criteria rather than by anything remembered from when it was made.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "geometry-2d", "selection", "layers", "modify")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:190]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[-105:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def hnd(r):
    if not isinstance(r, dict):
        return None
    e = r.get("entity")
    return e.get("handle") if isinstance(e, dict) else None


def rel(a, b, tol=1e-6):
    return a is not None and b is not None and abs(a - b) <= tol * max(1.0, abs(b))


print("== a drawing seeded so no answer is trivially 'one' or 'all' ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

do("layers", "create_layer", {"name": "L-OTHER"}, label="a second layer")

# three lines on layer 0, one of them a different colour; two lines on L-OTHER; two circles.
lines0 = [hnd(do("geometry-2d", "draw_line",
                 {"start": {"x": 0, "y": i * 10}, "end": {"x": 100, "y": i * 10}},
                 label=f"line {i} on layer 0")) for i in range(3)]
do("modify", "set_color", {"handles": [lines0[2]], "color": {"r": 0, "g": 255, "b": 0, "aciIndex": 3}},
   label="make the third line green")
linesB = [hnd(do("geometry-2d", "draw_line",
                 {"start": {"x": 0, "y": 200 + i * 10}, "end": {"x": 50, "y": 200 + i * 10},
                  "layer": "L-OTHER"},
                 label=f"line {i} on L-OTHER")) for i in range(2)]
circle = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 300, "y": 300}, "radius": 25},
                label="a circle"))

# ── select_similar: three flag settings, three different answers ────────────
print("\n== select_similar: the flags must actually change the answer ==")
r1 = do("selection", "select_similar", {"handle": lines0[0], "matchLayer": False},
        label="class only")
r2 = do("selection", "select_similar", {"handle": lines0[0]}, label="class + layer (the default)")
r3 = do("selection", "select_similar", {"handle": lines0[0], "matchColor": True},
        label="class + layer + colour")
c1 = r1.get("count") if isinstance(r1, dict) else None
c2 = r2.get("count") if isinstance(r2, dict) else None
c3 = r3.get("count") if isinstance(r3, dict) else None
check("PROVEN the flags are honoured, not ignored: class-only finds all 5 lines, adding layer "
      "narrows to the 3 on layer 0, adding colour narrows again to the 2 that are white. A tool "
      "that ignored its flags would return the same number three times",
      c1 == 5 and c2 == 3 and c3 == 2, f"classOnly={c1} +layer={c2} +colour={c3}")
if isinstance(r2, dict):
    check("and the circle is not among them",
          circle not in [e.get("handle") for e in (r2.get("entities") or [])], "circle leaked in")
    check("the reference entity IS included, as documented",
          lines0[0] in [e.get("handle") for e in (r2.get("entities") or [])], "reference missing")

# ── ranges, against arithmetic ──────────────────────────────────────────────
print("\n== select_by_area_range / select_by_length_range ==")
rect = hnd(do("geometry-2d", "draw_rectangle",
              {"corner1": {"x": 500, "y": 0}, "corner2": {"x": 600, "y": 50}},
              label="a 100 by 50 rectangle: area 5000, perimeter 300"))
r = do("selection", "select_by_area_range", {"min": 4999, "max": 5001})
if isinstance(r, dict):
    check("PROVEN against arithmetic: the 100 by 50 rectangle is found by an area window around "
          "5000, and it is the ONLY thing found - the lines and circle are not closed or not that "
          "size",
          r.get("count") == 1 and (r.get("entities") or [{}])[0].get("handle") == rect,
          str(r)[:250])
    check("and `measurable` shows how many entities could have an area at all, which is what "
          "tells 'nothing in range' apart from 'nothing has an area'",
          (r.get("measurable") or 0) >= 1 and (r.get("scanned") or 0) > (r.get("measurable") or 0),
          f"scanned={r.get('scanned')} measurable={r.get('measurable')}")
r = do("selection", "select_by_area_range", {"min": 1e9}, label="an area no shape has")
check("PROVEN the range filters: an impossible minimum finds nothing while still scanning",
      isinstance(r, dict) and r.get("count") == 0 and (r.get("scanned") or 0) > 0, str(r)[:200])

r = do("selection", "select_by_length_range", {"min": 299, "max": 301})
if isinstance(r, dict):
    check("PROVEN a CLOSED curve reports its perimeter, 300, and not zero",
          r.get("count") == 1 and rel((r.get("entities") or [{}])[0].get("length"), 300, 1e-3),
          str(r)[:250])
r = do("selection", "select_by_length_range", {"min": 99, "max": 101})
check("and the three 100-unit lines are found by length",
      isinstance(r, dict) and r.get("count") == 3, str(r)[:200])
do("selection", "select_by_area_range", {}, label="a range with neither bound is refused",
   expect_fail=True)
do("selection", "select_by_length_range", {"min": 50, "max": 10},
   label="min above max is refused", expect_fail=True)

# ── duplicates: one real, one near-miss ─────────────────────────────────────
print("\n== select_duplicates: an exact copy AND a near-copy that must NOT count ==")
dup = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 0}, "end": {"x": 100, "y": 0}},
             label="an EXACT copy of the first line"))
near = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 0.5}, "end": {"x": 100, "y": 0.5}},
              label="a near-copy, 0.5 away"))
r = do("selection", "select_duplicates", {"tolerance": 1e-6})
if isinstance(r, dict):
    all_dups = [h for g in (r.get("groups") or []) for h in (g.get("duplicates") or [])]
    check("PROVEN it finds the exact copy and NOT the one 0.5 away - a tolerance that was ignored "
          "would have swept the near-copy in too",
          r.get("duplicateCount") == 1 and near not in all_dups
          and (dup in all_dups or lines0[0] in all_dups),
          f"groups={r.get('groups')}")
    check("and it reports rather than deletes: one entity is named to keep in each group",
          all(g.get("keep") for g in (r.get("groups") or [])), str(r)[:200])
r = do("selection", "select_duplicates", {"tolerance": 5.0},
       label="with a loose tolerance the near-copy joins in")
check("PROVEN the tolerance is real: widening it to 5 catches the 0.5 offset as well",
      isinstance(r, dict) and (r.get("duplicateCount") or 0) >= 2, str(r)[:200])
do("selection", "select_duplicates", {"tolerance": 0}, label="a tolerance of zero is refused",
   expect_fail=True)

# ── select_last ─────────────────────────────────────────────────────────────
print("\n== select_last ==")
newest = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 900, "y": 900}, "radius": 5},
                label="draw one more thing"))
r = do("selection", "select_last", {})
if isinstance(r, dict):
    check("PROVEN 'last' means the most recently ADDED entity, which is the circle just drawn",
          r.get("count") == 1 and (r.get("entities") or [{}])[0].get("handle") == newest,
          str(r)[:220])
    print(f"       Editor.SelectLast said: count={r.get('editorSelectLastCount')} "
          f"note={r.get('editorSelectLastNote')!r}")
r = do("selection", "select_last", {"count": 3})
check("and asking for three gives three, the newest last",
      isinstance(r, dict) and r.get("count") == 3
      and (r.get("entities") or [{}])[-1].get("handle") == newest, str(r)[:200])

# ── visibility, read back through a different tool ──────────────────────────
print("\n== hide / isolate / unisolate, read back independently ==")
r = do("selection", "hide_objects", {"handles": [circle]})
check("one entity hidden", isinstance(r, dict) and r.get("hidden") == 1, str(r)[:200])
r = do("selection", "select_similar", {"handle": circle, "matchLayer": False},
       label="read the circle back through ANOTHER tool")
check("PROVEN hidden means invisible but STILL THERE: the circle is still found by a selection "
      "tool and reports visible=false - a tool that erased instead of hiding would fail here",
      isinstance(r, dict) and any(e.get("handle") == circle and e.get("visible") is False
                                  for e in (r.get("entities") or [])), str(r)[:250])

r = do("selection", "isolate_objects", {"handles": [lines0[0], lines0[1]]})
if isinstance(r, dict):
    check("isolate keeps two and hides the rest", r.get("kept") == 2 and (r.get("hidden") or 0) > 0,
          str(r)[:200])
r = do("selection", "select_similar", {"handle": lines0[0], "matchLayer": False},
       label="check visibility across all the lines")
if isinstance(r, dict):
    vis = {e.get("handle"): e.get("visible") for e in (r.get("entities") or [])}
    check("PROVEN isolate did the right thing to BOTH sides: the two named lines are visible and "
          "the others are not",
          vis.get(lines0[0]) is True and vis.get(lines0[1]) is True
          and vis.get(linesB[0]) is False, str(vis)[:250])

r = do("selection", "unisolate_objects", {})
check("unisolate shows things again", isinstance(r, dict) and (r.get("shown") or 0) > 0, str(r)[:200])
r = do("selection", "select_similar", {"handle": lines0[0], "matchLayer": False},
       label="check visibility again")
if isinstance(r, dict):
    check("PROVEN everything is visible again - including the circle hidden BEFORE the isolate, "
          "which is the documented behaviour rather than a restore of the previous state",
          all(e.get("visible") is True for e in (r.get("entities") or [])), str(r)[:250])
r = do("selection", "select_similar", {"handle": circle, "matchLayer": False})
check("and the circle that was hidden separately is visible too",
      isinstance(r, dict) and all(e.get("visible") is True for e in (r.get("entities") or [])),
      str(r)[:200])
do("selection", "hide_objects", {"handles": []}, label="hiding nothing is refused", expect_fail=True)
do("selection", "isolate_objects", {"handles": ["ZZZZ"]},
   label="isolating an unknown handle is refused", expect_fail=True)

# ── saved filters ───────────────────────────────────────────────────────────
print("\n== create / list / apply_saved_filter ==")
do("selection", "create_selection_filter",
   {"name": "TB_OTHER_LINES", "layer": "L-OTHER", "objectClass": "AcDbLine"})
r = do("selection", "list_selection_filters", {})
check("the filter is saved in the drawing and lists its criteria",
      isinstance(r, dict) and r.get("count") == 1
      and (r.get("filters") or [{}])[0].get("layer") == "L-OTHER", str(r)[:250])

r = do("selection", "apply_saved_filter", {"name": "TB_OTHER_LINES"})
if isinstance(r, dict):
    check("PROVEN it selects by its STORED criteria and reports them back: the two lines on "
          "L-OTHER and nothing else",
          r.get("count") == 2
          and (r.get("criteria") or {}).get("layer") == "L-OTHER", str(r)[:280])

# THE control: change the drawing, then apply again. A filter that remembered a result rather
# than criteria would give the old answer.
do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 500}, "end": {"x": 50, "y": 500},
                                "layer": "L-OTHER"}, label="add a THIRD line on L-OTHER")
r = do("selection", "apply_saved_filter", {"name": "TB_OTHER_LINES"},
       label="apply the same filter again")
check("PROVEN the filter re-evaluates rather than remembering: it now finds three, having found "
      "two a moment ago on the same stored criteria",
      isinstance(r, dict) and r.get("count") == 3, str(r)[:250])

do("selection", "create_selection_filter", {"name": "TB_OTHER_LINES", "layer": "0"},
   label="a duplicate filter name is refused", expect_fail=True)
do("selection", "create_selection_filter", {"name": "TB_EMPTY"},
   label="a filter with no criteria is refused", expect_fail=True)
do("selection", "apply_saved_filter", {"name": "NO_SUCH_FILTER"},
   label="an unknown filter name is refused", expect_fail=True)

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
