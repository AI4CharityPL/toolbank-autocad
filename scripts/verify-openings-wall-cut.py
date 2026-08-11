# -*- coding: utf-8 -*-
"""Live verification for openings.insert_door/insert_window's new optional wallHandle (2026-08-12).

Before this fix, openings.insert_door/insert_window had no wallHandle at all - a caller who
needed BOTH a schedule-visible door/window block AND an actual wall opening had no single tool
for it. architecture.insert_door/insert_window cut the wall but draw primitives invisible to
list_openings_in_model/generate_door_schedule/generate_window_schedule/audit_all_rooms/
get_room_data - discovered live while building a real two-level test structure through this
bank: every door/window schedule came back with 0 rows despite doors and windows being clearly
visible in the drawing.

This proves the fix two ways:
  1. POSITIVE: with wallHandle, the wall is genuinely cut (two surviving segments with a gap
     matching the opening width) AND the block is schedule-visible (list_openings_in_model
     finds it, export_schedule emits a real row).
  2. NEGATIVE CONTROL: without wallHandle, the wall is left whole (regression safety - the new
     optional argument must not change behaviour when omitted, matching every other opt-in
     wallHandle in this bank).
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "architecture", "openings", "schedules")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:300]}"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def P(x, y):
    return {"x": x, "y": y}


print("== fresh drawing, cross-session probe (rule 26 section 13a) ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

do("architecture", "ensure_architectural_layers", {})

print("\n== POSITIVE: openings.insert_door with wallHandle cuts the wall AND is schedule-visible ==")
wDoor = do("architecture", "draw_wall", {"start": P(0, 0), "end": P(5000, 0), "thicknessMm": 200},
           label="wall #1, for the door")
wallHandle1 = (wDoor.get("centerline") or {}).get("handle") if isinstance(wDoor, dict) else None

rDoor = do("openings", "insert_door", {
    "position": P(2000, 0), "rotationDeg": 0, "type": "single", "widthMm": 900,
    "wallHandle": wallHandle1,
}, label="insert_door at (2000,0), width 900, wallHandle set")
if isinstance(rDoor, dict):
    wo = rDoor.get("wallOpening")
    check("PROVEN the wall was actually cut - wallOpening is present with a real gap",
          wo is not None and 850 < wo.get("gapLengthMm", 0) < 950, str(wo)[:250])
    check("PROVEN left AND right wall segments both survived (opening not at an end)",
          wo is not None and wo.get("leftHandle") and wo.get("rightHandle"), str(wo)[:250])

rList = do("openings", "list_openings_in_model", {"kind": "doors"})
if isinstance(rList, dict):
    check("PROVEN the door is schedule-visible via list_openings_in_model (was impossible with "
          "architecture.insert_door)", rList.get("count") == 1, str(rList)[:250])

rSched = do("schedules", "generate_door_schedule", {"position": P(0, -3000)})
if isinstance(rSched, dict):
    summary = rSched.get("summary") or {}
    check("PROVEN generate_door_schedule's table actually has a data row for this door "
          "(rows > the 2 header/title rows)", summary.get("rows", 0) > 2, str(rSched)[:250])

print("\n== POSITIVE: openings.insert_window with wallHandle cuts the wall too ==")
wWin = do("architecture", "draw_wall", {"start": P(0, 3000), "end": P(5000, 3000), "thicknessMm": 200},
          label="wall #2, for the window")
wallHandle2 = (wWin.get("centerline") or {}).get("handle") if isinstance(wWin, dict) else None

rWin = do("openings", "insert_window", {
    "position": P(2500, 3000), "rotationDeg": 0, "type": "casement", "widthMm": 1200,
    "wallHandle": wallHandle2,
}, label="insert_window at (2500,3000), width 1200, wallHandle set")
if isinstance(rWin, dict):
    wo = rWin.get("wallOpening")
    check("PROVEN the wall was actually cut for the window too",
          wo is not None and 1150 < wo.get("gapLengthMm", 0) < 1250, str(wo)[:250])

rListW = do("openings", "list_openings_in_model", {"kind": "windows"})
if isinstance(rListW, dict):
    check("PROVEN the window is schedule-visible", rListW.get("count") == 1, str(rListW)[:250])

print("\n== NEGATIVE CONTROL: omitting wallHandle leaves the wall whole (no regression) ==")
wCtrl = do("architecture", "draw_wall", {"start": P(0, 6000), "end": P(5000, 6000), "thicknessMm": 200},
           label="wall #3, control - door placed WITHOUT wallHandle")
rCtrl = do("openings", "insert_door", {
    "position": P(2000, 6000), "rotationDeg": 0, "type": "single", "widthMm": 900,
}, label="insert_door WITHOUT wallHandle")
if isinstance(rCtrl, dict):
    check("PROVEN wallOpening is absent when wallHandle is omitted (opt-in, not automatic)",
          rCtrl.get("wallOpening") is None, str(rCtrl)[:250])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
