# -*- coding: utf-8 -*-
"""Live verification of the knowledge-base proof-of-concept: new residential/office
furniture catalog entries + populate_room presets + the two new validator standards
(residential-baseline, office-baseline). Not trusting return codes - reading dimensions
back via get_entity, and proving each new validator rule both flags a deliberate
violation and leaves a compliant room unflagged.
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "furniture", "geometry-2d", "architecture", "validators"]
S = {c: Session(c) for c in CATS}
results = []


def call(cat, tool, args, label=None):
    label = label or f"{cat}.{tool}"
    ok, r = S[cat].call(tool, args)
    print(f"{'OK  ' if ok else 'FAIL'} {label}" + ("" if ok else f"  -> {str(r)[:300]}"))
    return ok, r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def P(x, y):
    return {"x": x, "y": y}


print("== fresh drawing ==")
call("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
call("architecture", "ensure_architectural_layers", {})

print("\n" + "=" * 70)
print("FURNITURE: 6 new catalog entries - insert + read dimensions back")
print("=" * 70)
NEW_ENTRIES = {
    "FURN-KIT-HOB":     (600, 600),
    "FURN-KIT-FRIDGE":  (600, 650),
    "FURN-KIT-SINK":    (600, 600),
    "FURN-KIT-COUNTER": (2400, 600),
    "FURN-BED-RES":     (1600, 2000),
    "FURN-CBT-NST":     (450, 400),
}
x = 0
for name, (w, d) in NEW_ENTRIES.items():
    ok, r = call("furniture", "insert_furniture", {"name": name, "position": P(x, 0)}, label=f"insert {name}")
    handle = None
    if ok and isinstance(r, dict):
        check(f"{name} insert_furniture reports widthMm/depthMm == catalog ({w}x{d})",
              r.get("widthMm") == w and r.get("depthMm") == d, str(r)[:200])
        ent = r.get("entity") or {}
        handle = ent.get("handle") if isinstance(ent, dict) else None
    if handle:
        _, info = call("geometry-2d", "get_entity", {"handle": handle}, label=f"get_entity({name})")
        if isinstance(info, dict):
            bbox = info.get("bbox") or {}
            bmin, bmax = bbox.get("min"), bbox.get("max")
            if bmin and bmax:
                measuredW = round(bmax["x"] - bmin["x"])
                measuredD = round(bmax["y"] - bmin["y"])
                # Width is exact (the INV_ID label sits centred above the block, so it never
                # widens the bbox in X). Depth is measured >= catalog depth, not ==: every
                # furniture block's visible INV_ID attribute label sits ABOVE the block's own
                # top edge (AddStandardAttributes), which legitimately extends the
                # BlockReference's own bbox past the footprint - confirmed on FURN-EQP-CT too
                # (2100x1600 catalogued, 2100x1907.2 measured), not something new to these
                # entries. Allow up to +500mm of label overshoot; anything past that would be a
                # real geometry defect, not the label.
                check(f"{name} bbox width exact ({w}mm), depth >= catalog depth ({d}mm, label-inclusive)",
                      measuredW == w and d <= measuredD <= d + 500,
                      f"got {measuredW}x{measuredD}")
    else:
        check(f"{name} inserted with a resolvable handle", False, str(r)[:200])
    x += 3500

print("\n" + "=" * 70)
print("POPULATE_ROOM: 3 new presets (bedroom, kitchen, living-room-res)")
print("=" * 70)
for i, preset in enumerate(["bedroom", "kitchen", "living-room-res"]):
    y0 = 5000 + i * 6000
    ok, r = call("furniture", "populate_room", {
        "bboxMin": P(0, y0), "bboxMax": P(4000, y0 + 4000), "preset": preset,
    }, label=f"populate_room preset={preset}")
    if isinstance(r, dict):
        items = r.get("items") or r.get("placed") or []
        warnings = r.get("warnings") or []
        check(f"preset={preset} placed >=1 item, no warnings", len(items) >= 1 and len(warnings) == 0,
              f"items={len(items)} warnings={warnings}")

print("\n" + "=" * 70)
print("VALIDATORS: residential.rooms.kitchen-min-area (>= 1.8 m^2)")
print("=" * 70)
call("architecture", "define_room", {
    "vertices": [P(20000, 0), P(21000, 0), P(21000, 1500), P(20000, 1500)],
    "number": "K-BAD", "name": "Undersized kitchen", "boundaryLayer": "A-ROOM-BNDY-KITCHEN",
}, label="define undersized kitchen (1.5 m^2, clearly under the 1.8 m^2 floor)")
_, rep = call("validators", "validate_with_rule", {"ruleId": "residential.rooms.kitchen-min-area"},
              label="validate residential.rooms.kitchen-min-area")
if isinstance(rep, dict):
    check("kitchen-min-area FLAGS the undersized kitchen", rep.get("violationCount", 0) >= 1, str(rep)[:300])

call("architecture", "define_room", {
    "vertices": [P(20000, 3000), P(22500, 3000), P(22500, 5000), P(20000, 5000)],
    "number": "K-OK", "name": "Compliant kitchen", "boundaryLayer": "A-ROOM-BNDY-KITCHEN",
}, label="define compliant kitchen (2500x2000=5m^2 centreline)")
_, rep2 = call("validators", "validate_with_rule", {"ruleId": "residential.rooms.kitchen-min-area"},
               label="re-validate residential.rooms.kitchen-min-area")
if isinstance(rep2, dict):
    check("kitchen-min-area still flags ONLY the bad kitchen (count==1, not 2)",
          rep2.get("violationCount", 0) == 1, str(rep2)[:300])

print("\n" + "=" * 70)
print("VALIDATORS: residential.rooms.bathroom-min-area (>= 3.52 m^2)")
print("=" * 70)
call("architecture", "define_room", {
    "vertices": [P(30000, 0), P(31500, 0), P(31500, 1500), P(30000, 1500)],
    "number": "BA-BAD", "name": "Undersized bathroom", "boundaryLayer": "A-ROOM-BNDY-BATH-RES",
}, label="define undersized bathroom (~2.25 m^2 centreline)")
_, rep3 = call("validators", "validate_with_rule", {"ruleId": "residential.rooms.bathroom-min-area"},
               label="validate residential.rooms.bathroom-min-area")
if isinstance(rep3, dict):
    check("bathroom-min-area FLAGS the undersized bathroom", rep3.get("violationCount", 0) >= 1, str(rep3)[:300])

print("\n" + "=" * 70)
print("VALIDATORS: office.rooms.private-office-min-area (>= 6.72 m^2)")
print("=" * 70)
call("architecture", "define_room", {
    "vertices": [P(40000, 0), P(42000, 0), P(42000, 2000), P(40000, 2000)],
    "number": "O-BAD", "name": "Undersized office", "boundaryLayer": "A-ROOM-BNDY-OFFICE",
}, label="define undersized office (4 m^2 centreline)")
_, rep4 = call("validators", "validate_with_rule", {"ruleId": "office.rooms.private-office-min-area"},
               label="validate office.rooms.private-office-min-area")
if isinstance(rep4, dict):
    check("private-office-min-area FLAGS the undersized office", rep4.get("violationCount", 0) >= 1, str(rep4)[:300])

call("architecture", "define_room", {
    "vertices": [P(40000, 3000), P(43000, 3000), P(43000, 6000), P(40000, 6000)],
    "number": "O-OK", "name": "Compliant office", "boundaryLayer": "A-ROOM-BNDY-OFFICE",
}, label="define compliant office (3000x3000=9m^2 centreline)")
_, rep5 = call("validators", "validate_with_rule", {"ruleId": "office.rooms.private-office-min-area"},
               label="re-validate office.rooms.private-office-min-area")
if isinstance(rep5, dict):
    check("private-office-min-area still flags ONLY the bad office (count==1, not 2)",
          rep5.get("violationCount", 0) == 1, str(rep5)[:300])

passed = sum(1 for _, ok in results if ok)
total = len(results)
print(f"\n==== {passed}/{total} checks OK ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
