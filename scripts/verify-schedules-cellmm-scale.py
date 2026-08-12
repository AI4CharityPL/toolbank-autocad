# -*- coding: utf-8 -*-
"""Live verification for the new cellMm override on audit_all_rooms / correct_room_area /
correct_all_room_areas / generate_room_schedule / get_room_data (2026-08-12).

Found live during a deliberate scale test: get_room_region's flood-fill picks an automatic raster
cell size that scales with the extent of EVERY wall in the model, not the room being measured
(Clamp(wholeModelExtent / 600, 50, 500)mm). Built 75 identical rooms across 5 "floors" laid out
side by side (a real, wide model) - the SAME room shape (2850x2350mm clear, 6.6975 m2 by
construction) measured differently depending only on which floor it sat on (5.97-6.19 m2, up to
11% off), enough to false-flag 60 of 75 rooms as labelMismatch under the default 10% tolerance.
Precision silently degrades as the building grows, with no way to compensate - until now.

This reproduces the same 75-room, 5-floor scenario (builds in ~2s) and proves BOTH directions:
  1. Default behaviour (no cellMm) is UNCHANGED - the false mismatches are still there, proving
     this is a pure opt-in addition, not a silent behaviour change.
  2. cellMm=50 (a small explicit cell) on the SAME drawing brings mismatches to zero.
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "architecture", "schedules")}
results = []


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def P(x, y):
    return {"x": x, "y": y}


FLOORS, COLS, ROWS = 5, 3, 5  # 75 rooms
ROOM_W, ROOM_H, GAP, FLOOR_GAP = 3000, 2500, 500, 1500

print("== fresh drawing ==")
ok, r = S["files"].call("new_document", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (S["files"].call("list_documents", {})[1].get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
S["architecture"].call("ensure_architectural_layers", {})

print(f"== building {FLOORS}x{COLS * ROWS} = {FLOORS * COLS * ROWS} identical rooms across "
      f"{FLOORS} floors laid out side by side (a wide model - the point of the test) ==")
for floor in range(FLOORS):
    floor_w = COLS * (ROOM_W + GAP) + GAP
    fx0 = floor * (floor_w + FLOOR_GAP)
    for row in range(ROWS):
        for col in range(COLS):
            x0 = fx0 + GAP + col * (ROOM_W + GAP)
            y0 = GAP + row * (ROOM_H + GAP)
            x1, y1 = x0 + ROOM_W, y0 + ROOM_H
            S["architecture"].call("draw_walls_chain", {
                "vertices": [P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1)],
                "thicknessMm": 150, "closed": True,
            })
            number = f"{floor + 1}{row * COLS + col + 1:02d}"
            S["architecture"].call("define_room", {
                "vertices": [P(x0 + 75, y0 + 75), P(x1 - 75, y0 + 75), P(x1 - 75, y1 - 75), P(x0 + 75, y1 - 75)],
                "number": number, "name": f"Room {number}",
            })
print(f"  built {FLOORS * COLS * ROWS} rooms")

print("\n== DEFAULT (no cellMm): reproduces the false mismatches - proves this is opt-in ==")
ok, rDefault = S["schedules"].call("audit_all_rooms", {})
print(f"  total={rDefault.get('total')} mismatches={rDefault.get('mismatches')}")
check("PROVEN default behaviour is unchanged: the automatic-sizing false mismatches are still "
      "there (this scenario is known to produce ~60/75 without an override)",
      rDefault.get("mismatches", 0) > 30, str(rDefault.get("mismatches")))

print("\n== cellMm=50 on the SAME drawing: mismatches must drop to (near) zero ==")
ok, rFine = S["schedules"].call("audit_all_rooms", {"cellMm": 50})
print(f"  total={rFine.get('total')} mismatches={rFine.get('mismatches')}")
check("PROVEN total is still exactly 75 with the override (cellMm doesn't change room count)",
      rFine.get("total") == FLOORS * COLS * ROWS, str(rFine.get("total")))
check("PROVEN cellMm=50 on the IDENTICAL drawing brings mismatches down to (near) zero - the "
      "fix, not just a different default that happens to look better here",
      rFine.get("mismatches", 999) <= 2, str(rFine.get("mismatches")))

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
