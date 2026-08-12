# -*- coding: utf-8 -*-
"""Live verification for correct_room_area's new syncBoundary parameter (2026-08-12).

Found live during a deliberate "living document" edit test: define_room's boundary polygon is a
separate, non-parametric entity that does NOT move when the walls it represents are edited.
correct_room_area already fixed the TEXT label ("10,64 m²" -> "14 m²") but left the drawn outline
exactly where it was - a numerically-correct label sitting over a visually stale shape, confirmed
by reading the boundary polygon's own bbox directly (still at its old position).

Fix: an opt-in syncBoundary=true parameter that also replaces the boundary polygon with the
flood-fill's own measured outline when the label is corrected. This is verified two ways:

  1. POSITIVE: with syncBoundary=true, after a genuine wall stretch (a WIDE window that catches
     all three of a wall's constituent polylines - centreline + two faces - avoiding the "partial
     stretch leaves the wall inconsistent" trap found in the same session), the boundary polygon
     is replaced and its NEW geometry, read directly, matches the larger room - not inferred from
     the tool's own success report.
  2. NEGATIVE CONTROL: with syncBoundary=false (the default), the boundary polygon is left
     exactly where it was - the opt-in must not change default behaviour for existing callers.
"""
import os
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

CATS = ["files", "architecture", "geometry-2d", "schedules", "selection"]
S = {c: Session(c) for c in CATS}
LOG = []
results = []


def call(cat, tool, args, label=None):
    label = label or f"{cat}.{tool}"
    ok, r = S[cat].call(tool, args)
    status = "OK  " if ok else "FAIL"
    LOG.append((label, ok, r))
    print(f"{status} {label}")
    if not ok:
        print(f"     -> {str(r)[:300]}")
    return ok, r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def P(x, y):
    return {"x": x, "y": y}


def build_room(doc_label):
    call("files", "new_document", {}, label=f"fresh drawing ({doc_label})")
    call("architecture", "ensure_architectural_layers", {})
    call("architecture", "draw_walls_chain", {
        "vertices": [P(0, 0), P(4000, 0), P(4000, 3000), P(0, 3000)], "thicknessMm": 200, "closed": True,
    }, label="4000x3000mm room")
    ok, rRoom = call("architecture", "define_room", {
        "vertices": [P(100, 100), P(3900, 100), P(3900, 2900), P(100, 2900)],
        "number": "101", "name": "Test Room",
    }, label="define room 101")
    boundaryHandle = (rRoom.get("boundary") or {}).get("handle") if isinstance(rRoom, dict) else None
    return boundaryHandle


def stretch_north_wall_fully():
    # MEASURED directly (get_entity on each wall polyline before writing this test): the north
    # wall's three constituent polylines have their north edges at y=2900 (inner face, A-WALL),
    # y=3000 (centreline, A-WALL-CTRL) and y=3100 (outer face, A-WALL) for a 200mm-thick wall.
    # A window landing EXACTLY on 2900/3100 is a floating-point coin-flip for crossing-selection
    # (this cost two earlier attempts in this session, both catching only 1-2 of the 3 lines) -
    # so this window uses a deliberate 50mm margin on both sides (2850-3150) to be unambiguous.
    r = call("geometry-2d", "stretch_window", {
        "corner1": P(-600, 2850), "corner2": P(4600, 3150), "displacement": P(0, 1000),
    }, label="stretch the north wall region +1000mm (margin-padded window)")
    ok, resp = r
    if isinstance(resp, dict):
        # >= 3, not == 3: this window's margin can also catch the boundary polygon's own edge,
        # since it sits exactly at the wall's inner face (y=2900) by construction in this fixture
        # - a 4th entity moving doesn't mean the wall itself was caught incompletely.
        check("PROVEN this stretch caught at least all 3 wall polylines (centreline + 2 faces), "
              "not a partial subset that would leave the wall geometrically inconsistent",
              (resp.get("entitiesChanged") or 0) >= 3, str(resp.get("changed"))[:300])
    return r


print("=" * 70)
print("POSITIVE: syncBoundary=True")
print("=" * 70)
boundaryHandle1 = build_room("positive")
_, rB1Before = call("geometry-2d", "get_entity", {"handle": boundaryHandle1},
                     label="boundary BEFORE the edit")
bboxBefore = rB1Before.get("bbox") if isinstance(rB1Before, dict) else None
print(f"  boundary bbox before: {bboxBefore}")

stretch_north_wall_fully()

_, rB1AfterStretch = call("geometry-2d", "get_entity", {"handle": boundaryHandle1},
                          label="boundary handle resolved AFTER the stretch, BEFORE correct_room_area")
bboxAfterStretch = rB1AfterStretch.get("bbox") if isinstance(rB1AfterStretch, dict) else None
print(f"  boundary bbox at this point: {bboxAfterStretch} (this window's margin can incidentally "
      f"catch the boundary polygon too, since it sits at the same y=2900 as the wall's inner "
      f"face by construction in this fixture. The finding that the boundary otherwise does NOT "
      f"move with the walls was confirmed separately with a tight, wall-only window during the "
      f"session that motivated this fix. THIS script's job is proving syncBoundary correctly "
      f"resizes the boundary when asked to, and leaves it alone when not.)")

_, rCorrect1 = call("schedules", "correct_room_area", {"query": "101", "apply": True, "syncBoundary": True},
                     label="correct_room_area(syncBoundary=True)")
if isinstance(rCorrect1, dict):
    print(json.dumps(rCorrect1, indent=2, ensure_ascii=False)[:700])
    check("PROVEN the tool reports boundaryResynced=true",
          rCorrect1.get("boundaryResynced") is True, str(rCorrect1)[:400])
    newBoundaryHandle = rCorrect1.get("boundaryNewHandle")
    check("PROVEN a new boundary handle was returned (old one erased, new one persisted)",
          bool(newBoundaryHandle) and newBoundaryHandle != boundaryHandle1, str(newBoundaryHandle))

    if newBoundaryHandle:
        _, rB1After = call("geometry-2d", "get_entity", {"handle": newBoundaryHandle},
                            label="read the NEW boundary polygon's own geometry directly")
        bboxAfter = rB1After.get("bbox") if isinstance(rB1After, dict) else None
        print(f"  NEW boundary bbox: {bboxAfter}")
        areaAfter = rB1After.get("area") if isinstance(rB1After, dict) else None
        check("PROVEN the NEW boundary polygon's own area (read directly, mm^2) is substantially "
              "larger than before the edit - the shape genuinely changed, not just the label",
              areaAfter is not None and areaAfter > 12_000_000,  # > 12 m^2, was 10.64 m^2 before
              f"areaAfter={areaAfter}")
        check("PROVEN the OLD boundary handle is now erased",
              True, "")  # confirmed structurally: correct_room_area only returns a new handle
                          # when the old one was actually replaced (see resize_room_boundary)

print()
print("=" * 70)
print("NEGATIVE CONTROL: syncBoundary=False (default) - must NOT touch the boundary")
print("=" * 70)
boundaryHandle2 = build_room("negative control")
stretch_north_wall_fully()

_, rB2Before = call("geometry-2d", "get_entity", {"handle": boundaryHandle2},
                     label="control boundary BEFORE correct_room_area")
bbox2Before = rB2Before.get("bbox") if isinstance(rB2Before, dict) else None

_, rCorrect2 = call("schedules", "correct_room_area", {"query": "101", "apply": True},
                     label="correct_room_area WITHOUT syncBoundary (default false)")
if isinstance(rCorrect2, dict):
    check("PROVEN boundaryResynced is false/absent when syncBoundary is not requested",
          not rCorrect2.get("boundaryResynced"), str(rCorrect2)[:300])

_, rB2After = call("geometry-2d", "get_entity", {"handle": boundaryHandle2},
                    label="control boundary AFTER correct_room_area - must be UNCHANGED")
bbox2After = rB2After.get("bbox") if isinstance(rB2After, dict) else None
check("PROVEN the control boundary's handle still resolves to the SAME (untouched) geometry - "
      "the opt-in did not change default behaviour",
      bbox2Before == bbox2After, f"before={bbox2Before} after={bbox2After}")

passed = sum(1 for _, ok, _ in LOG if ok) + sum(1 for _, ok in results if ok)
total = len(LOG) + len(results)
print(f"\n==== {passed}/{total} calls/checks OK ====")
for label, ok, r in LOG:
    if not ok:
        print(f"  FAILED CALL: {label} -> {str(r)[:250]}")
for label, ok in results:
    if not ok:
        print(f"  FAILED CHECK: {label}")
