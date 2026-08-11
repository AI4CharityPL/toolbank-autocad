# -*- coding: utf-8 -*-
"""Live verification for the acad-schedules room-grouping fix (2026-08-12).

`define_room` places THREE separate DBText entities per room (number, name, area — all on
A-ROOM-IDEN). `generate_room_schedule`/`audit_all_rooms`/`correct_all_room_areas` used to treat
each of the three lines as its own "room": a drawing with N real rooms reported 3N rows and
summed each room's (correctly-measured) area three times over. Found live while auditing a
6-room test building: 144.625 m² real, 433.875 m² reported — exactly 3x.

Fixed by grouping every label whose position falls inside the SAME detected boundary polygon
before extracting number/name/area, generalising the algorithm get_room_data/correct_room_area
already used correctly for a single queried room to the whole drawing in one batch pass.

A second, related defect surfaced while checking the first fix: SplitNumberName's query-substring
fallback could win over a clean room-number match when the grouping SEED (picked in arbitrary
enumeration order) happened to be the area-token label itself - "17,81 m²" trivially "contains"
itself as a substring, so it beat the real "202" match reached later in the label list. Fixed by
searching all labels for a strict regex number match FIRST, only falling back to the fuzzy
query-substring heuristic if no label matches cleanly.

Fixture: two rectangles with deliberately DIFFERENT, easy-to-eyeball areas (12 m² and 10 m²,
total 22 m²) - a lingering 3x bug would report ~66 m², not a coincidence with 22.
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "architecture", "schedules")}
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

print("\n== two rooms, deliberately different areas (12 m2 and 10 m2, total 22 m2) ==")
do("architecture", "ensure_architectural_layers", {})
do("architecture", "draw_walls_chain", {
    "vertices": [P(0, 0), P(9000, 0), P(9000, 3200), P(0, 3200)], "thicknessMm": 200, "closed": True,
}, label="exterior envelope")
do("architecture", "draw_wall", {"start": P(4000, 0), "end": P(4000, 3200), "thicknessMm": 100},
   label="partition")

r1 = do("architecture", "define_room", {
    "vertices": [P(100, 100), P(3900, 100), P(3900, 3100), P(100, 3100)],
    "number": "A1", "name": "Room A1",
}, label="define room A1 (~12 m2)")
r2 = do("architecture", "define_room", {
    "vertices": [P(4100, 100), P(8900, 100), P(8900, 2100), P(4100, 2100)],
    "number": "A2", "name": "Room A2",
}, label="define room A2 (~10 m2)")

if isinstance(r1, dict):
    check("room A1 measured area is ~12 m2", 11.0 < r1.get("areaM2", 0) < 13.0, str(r1)[:200])
if isinstance(r2, dict):
    check("room A2 measured area is ~10 m2", 9.0 < r2.get("areaM2", 0) < 11.0, str(r2)[:200])

print("\n== audit_all_rooms: PROVEN to report 2 rooms, not 6 (one per label line) ==")
rAudit = do("schedules", "audit_all_rooms", {})
if isinstance(rAudit, dict):
    check("total is exactly 2 (grouped), not 6 (one row per DBText label)",
          rAudit.get("total") == 2, str(rAudit)[:300])
    queries = sorted(row.get("query") for row in rAudit.get("rows", []))
    check("PROVEN the two queries are the real room numbers A1/A2, not an area token like '12,0 m²' "
          "(the second defect found while verifying the first)",
          queries == ["A1", "A2"], f"queries={queries}")

print("\n== generate_room_schedule: PROVEN total area is NOT tripled (~66-78 m2 signature) ==")
rSched = do("schedules", "generate_room_schedule", {"position": P(0, -6000)})
if isinstance(rSched, dict):
    check("roomCount is exactly 2, not 6", rSched.get("roomCount") == 2, str(rSched)[:200])
    total = rSched.get("totalAreaM2", 0)
    # generate_room_schedule sums the FLOOD-FILL measured area (actual wall faces), which is a bit
    # larger than the hand-picked inset polygon passed to define_room above - a real, expected
    # difference between two different measurement methods, not the bug. The bound that actually
    # matters is staying far below the unmistakable ~66-78 m2 a 3x/tripled count would produce.
    check(f"PROVEN totalAreaM2 ({total}) is nowhere near the 3x-bug signature (~66-78 m2)",
          15.0 < total < 40.0, str(rSched)[:200])

print("\n== correct_all_room_areas: PROVEN dry-run scans 2 rooms, not 6 ==")
rCorrect = do("schedules", "correct_all_room_areas", {"apply": False})
if isinstance(rCorrect, dict):
    check("scanned is exactly 2, not 6", rCorrect.get("scanned") == 2, str(rCorrect)[:200])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
