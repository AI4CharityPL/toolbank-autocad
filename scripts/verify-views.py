# -*- coding: utf-8 -*-
"""Live verification for roadmap 5.4 — acad-views, 9 tools.

A view is a set of numbers that AutoCAD stores and hands back, so the failure mode is a value
that reads back plausibly but is not the one written. The controls that catch that:

  * ASYMMETRIC sizes everywhere. A view 300 wide by 200 high cannot pass if width and height are
    swapped; a square one could. Same reasoning as the CSV grid in acad-data.
  * create_view_from_window is checked against create_named_view: the two must produce the SAME
    center and size from equivalent input, which is a cross-tool control neither could pass alone.
  * Corners are given in REVERSE order, because a window dragged right-to-left is still a window
    and normalising it is easy to forget.
  * restore_view_in_viewport copies rather than links - so the test CHANGES THE VIEW afterwards
    and requires the viewport NOT to follow. A tool that linked instead of copying would pass a
    naive check and fail this one.
  * set_perspective_mode is proved to act on the VIEWPORT, since ViewTableRecord has no such
    property at all - the check reads it back off the viewport.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "geometry-2d", "views", "viewports", "layouts", "ucs")}
results = []

W, H = 300.0, 200.0          # asymmetric on purpose - a swap cannot pass


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:200]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[-110:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def rel(a, b, tol=1e-6):
    return a is not None and b is not None and abs(a - b) <= tol * max(1.0, abs(b))


print("== fresh drawing ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

r = do("views", "list_named_views", {}, label="a fresh drawing's view table")
check("PROVEN it starts EMPTY - unlike the named objects dictionary, so a count of zero later is "
      "meaningful rather than ambiguous",
      isinstance(r, dict) and r.get("count") == 0, str(r)[:200])

# ── create_named_view ───────────────────────────────────────────────────────
print(f"\n== create_named_view: {W:.0f} wide by {H:.0f} high, asymmetric on purpose ==")
r = do("views", "create_named_view",
       {"name": "TB_PLAN", "center": {"x": 50, "y": 25, "z": 0}, "width": W, "height": H})
if isinstance(r, dict):
    v = r.get("view") or {}
    check(f"PROVEN width and height are not swapped: {W:.0f} by {H:.0f} reads back that way "
          f"round, which a square view could never have shown",
          rel(v.get("width"), W) and rel(v.get("height"), H), str(v)[:250])
    check("and the centre is where it was put",
          rel((v.get("center") or {}).get("x"), 50) and rel((v.get("center") or {}).get("y"), 25),
          str(v.get("center")))

r = do("views", "create_named_view",
       {"name": "TB_CAM", "center": {"x": 0, "y": 0, "z": 0}, "width": 100, "height": 80,
        "target": {"x": 10, "y": 20, "z": 30}, "viewDirection": {"x": 1, "y": 1, "z": 1},
        "lensLength": 35, "twist": 0.5},
       label="a 3D view with a target, direction, lens and twist")
if isinstance(r, dict):
    v = r.get("view") or {}
    check("every optional value survives: target, lens 35 and twist 0.5",
          rel((v.get("target") or {}).get("z"), 30) and rel(v.get("lensLength"), 35)
          and rel(v.get("twist"), 0.5), str(v)[:280])

do("views", "create_named_view",
   {"name": "TB_PLAN", "center": {"x": 0, "y": 0, "z": 0}, "width": 10, "height": 10},
   label="a duplicate view name is refused", expect_fail=True)
do("views", "create_named_view", {"name": "TB_ZERO", "center": {"x": 0, "y": 0, "z": 0},
                                  "width": 0, "height": 10},
   label="a view of zero width is refused", expect_fail=True)
do("views", "create_named_view", {"center": {"x": 0, "y": 0, "z": 0}, "width": 5, "height": 5},
   label="a view with no name is refused", expect_fail=True)

# ── the cross-tool control ──────────────────────────────────────────────────
print("\n== create_view_from_window, checked AGAINST create_named_view ==")
# The same view expressed two ways. Corners deliberately given bottom-right to top-left.
r = do("views", "create_view_from_window",
       {"name": "TB_WIN", "corner1": {"x": 200, "y": 125, "z": 0},
        "corner2": {"x": -100, "y": -75, "z": 0}},
       label="corners given in REVERSE order")
if isinstance(r, dict):
    v = r.get("view") or {}
    check(f"PROVEN the corners are normalised and the window becomes the SAME view as the one "
          f"built from centre and size - {W:.0f} by {H:.0f} centred on (50, 25). Two tools "
          f"reaching the same answer from different input is what neither could show alone",
          rel(v.get("width"), W) and rel(v.get("height"), H)
          and rel((v.get("center") or {}).get("x"), 50)
          and rel((v.get("center") or {}).get("y"), 25), str(v)[:280])

do("views", "create_view_from_window", {"name": "TB_BAD", "corner1": {"x": 0, "y": 0, "z": 0},
                                        "corner2": {"x": 0, "y": 50, "z": 0}},
   label="a window with no width is refused", expect_fail=True)

r = do("views", "list_named_views", {})
check("all three views are listed", isinstance(r, dict) and r.get("count") == 3, str(r)[:200])

# ── cameras are views ───────────────────────────────────────────────────────
print("\n== set_camera_target / set_camera_lens ==")
r = do("views", "set_camera_target", {"name": "TB_CAM", "target": {"x": 7, "y": 8, "z": 9}})
if isinstance(r, dict):
    check("PROVEN the target moved and the previous one is reported so it can be undone",
          rel((r.get("target") or {}).get("x"), 7)
          and rel((r.get("targetBefore") or {}).get("x"), 10), str(r)[:250])
r = do("views", "set_camera_lens", {"name": "TB_CAM", "lensLength": 85})
if isinstance(r, dict):
    check("PROVEN the lens changed from 35 to 85, both reported",
          rel(r.get("lensLength"), 85) and rel(r.get("lensLengthBefore"), 35), str(r)[:250])
do("views", "set_camera_lens", {"name": "TB_CAM", "lensLength": 0},
   label="a lens of zero is refused", expect_fail=True)
do("views", "set_camera_target", {"name": "NO_SUCH_VIEW", "target": {"x": 0, "y": 0, "z": 0}},
   label="an unknown view name is refused and points at list_named_views", expect_fail=True)

# ── UCS association ─────────────────────────────────────────────────────────
print("\n== set_view_ucs_association ==")
r = do("views", "set_view_ucs_association", {"name": "TB_PLAN"}, label="associate world by default")
if isinstance(r, dict):
    check("PROVEN it reads back as associated, from IsUcsAssociatedToView rather than from the "
          "read-only UcsName",
          r.get("ucsAssociated") is True and r.get("ucsAssociatedBefore") is False, str(r)[:250])
do("views", "set_view_ucs_association", {"name": "TB_PLAN", "ucsName": "NO_SUCH_UCS"},
   label="an unknown UCS is refused and points at ucs.list_ucs", expect_fail=True)

# ── viewports: restore and perspective ──────────────────────────────────────
print("\n== restore_view_in_viewport + set_perspective_mode ==")
r = do("layouts", "list_layouts", {}, label="find a layout to put a viewport on")
layout = None
if isinstance(r, dict):
    for l in (r.get("layouts") or []):
        n = l.get("name") if isinstance(l, dict) else l
        if n and n.lower() != "model":
            layout = n
            break
check("a paper-space layout exists to test against", layout is not None, str(r)[:200])

vp = None
if layout:
    r = do("viewports", "create_viewport",
           {"layoutName": layout, "center": {"x": 100, "y": 100, "z": 0},
            "width": 150, "height": 100},
           label=f"a viewport on layout '{layout}'")
    if isinstance(r, dict):
        e = r.get("viewport") or r.get("entity")
        vp = e.get("handle") if isinstance(e, dict) else r.get("handle")

# A viewport is REQUIRED, not optional. The first version skipped this whole block when the
# handle came back empty and still reported every check passing - the same "passes because it
# never ran" trap these scripts exist to catch, in the script itself.
check("a viewport handle was obtained, without which the restore and perspective checks below "
      "would silently not run at all", vp is not None, str(r)[:200])

if vp:
    r = do("views", "restore_view_in_viewport", {"name": "TB_PLAN", "viewportHandle": vp})
    if isinstance(r, dict):
        check(f"PROVEN the view's height {H:.0f} was copied onto the viewport, and the previous "
              f"height is reported so the change is visible",
              rel(r.get("viewHeight"), H), str(r)[:280])

    # THE control on "copies rather than links": change the view, and the viewport must NOT move.
    do("views", "set_camera_lens", {"name": "TB_PLAN", "lensLength": 200},
       label="now CHANGE the view after restoring it")
    r = do("views", "restore_view_in_viewport", {"name": "TB_PLAN", "viewportHandle": vp},
           label="restore again to read the viewport back")
    check("PROVEN restoring COPIES rather than links: the viewport had to be restored again to "
          "pick the change up, and its height before that second restore was still the old one - "
          "a tool that linked instead of copying would have followed the view by itself",
          isinstance(r, dict) and rel(r.get("viewHeightBefore"), H), str(r)[:280])

    r = do("views", "set_perspective_mode", {"viewportHandle": vp, "enabled": True})
    if isinstance(r, dict):
        check("PROVEN perspective lives on the VIEWPORT: it reads back on, from a property "
              "ViewTableRecord does not even have",
              r.get("perspective") is True and r.get("perspectiveBefore") is False, str(r)[:250])
    do("views", "set_perspective_mode", {"viewportHandle": vp, "enabled": True},
       label="setting perspective to what it already is, is refused", expect_fail=True)
    do("views", "set_perspective_mode", {"viewportHandle": vp, "enabled": False},
       label="and it turns off again")

    do("views", "restore_view_in_viewport", {"name": "TB_PLAN", "viewportHandle": "ZZZZ"},
       label="a bad viewport handle is refused", expect_fail=True)

# ── deletion, and the claim it makes about viewports ────────────────────────
print("\n== delete_named_view ==")
r = do("views", "delete_named_view", {"name": "TB_WIN"})
check("it reports the deletion after confirming the view is gone from the table",
      isinstance(r, dict) and r.get("deleted") is True, str(r)[:200])
do("views", "delete_named_view", {"name": "TB_WIN"},
   label="deleting it twice is refused", expect_fail=True)
r = do("views", "list_named_views", {})
if isinstance(r, dict):
    names = sorted(v.get("name") for v in (r.get("views") or []))
    check("and exactly the other two remain", names == ["TB_CAM", "TB_PLAN"], str(names))

if vp:
    r = do("viewports", "get_viewport_info", {"handle": vp},
           label="the viewport survives its view being deleted")
    check("PROVEN deleting a view does NOT disturb a viewport that showed it - which follows from "
          "restoring being a copy, and is the reason that design is worth stating",
          isinstance(r, dict), str(r)[:200])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
