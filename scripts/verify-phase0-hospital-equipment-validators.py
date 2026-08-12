# -*- coding: utf-8 -*-
"""Live verification of Faza 0a (medical equipment catalog) and Faza 0b
(hospital-baseline validator rules) for HospitalPrime2026. Not trusting
return codes - reading dimensions back via get_entity, and proving each
new validator rule actually flags a deliberately-built violation.
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "furniture", "geometry-2d", "architecture", "validators", "modify"]
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
print("FAZA 0a: 7 new FURN-EQP-* blocks - insert + read dimensions back")
print("=" * 70)
EQUIPMENT = {
    "FURN-EQP-CT":    (2100, 1600),
    "FURN-EQP-MRI":   (2800, 2000),
    "FURN-EQP-CARM":  (2000, 2000),
    "FURN-EQP-LIGHT": (700, 700),
    "FURN-EQP-CRASH": (500, 600),
    "FURN-EQP-VENT":  (400, 500),
    "FURN-EQP-MON":   (350, 400),
}
x = 0
for name, (w, d) in EQUIPMENT.items():
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
            bmin, bmax = info.get("bboxMin"), info.get("bboxMax")
            if bmin and bmax:
                measuredW = round(bmax[0] - bmin[0])
                measuredD = round(bmax[1] - bmin[1])
                check(f"{name} bbox ~ {w}x{d}mm (measured {measuredW}x{measuredD})",
                      abs(measuredW - w) <= 5 and abs(measuredD - d) <= 5,
                      f"got {measuredW}x{measuredD}")
    else:
        check(f"{name} inserted with a resolvable handle", False, str(r)[:200])
    x += 3500

_, rList = call("furniture", "list_furniture_in_model", {}, label="list_furniture_in_model")
if isinstance(rList, dict):
    refs = rList.get("references") or []
    names = [it.get("blockName") for it in refs] if isinstance(refs, list) else []
    for name in EQUIPMENT:
        check(f"{name} appears in list_furniture_in_model", name in names, f"names seen: {names}")

print("\n" + "=" * 70)
print("FAZA 0b: 6 new hospital-baseline rules - each must catch a deliberate violation")
print("=" * 70)

# 1) patient room too small (10 m^2 < 15 m^2 required) on A-ROOM-BNDY-WARD
call("architecture", "define_room", {
    "vertices": [P(20000, 0), P(23162, 0), P(23162, 3162), P(20000, 3162)],
    "number": "W-BAD", "name": "Undersized ward room", "boundaryLayer": "A-ROOM-BNDY-WARD",
}, label="define undersized ward room (~10 m^2)")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.rooms.patient-room-min-area"},
              label="validate hospital.rooms.patient-room-min-area")
if isinstance(rep, dict):
    check("patient-room-min-area FLAGS the undersized room", rep.get("violationCount", 0) >= 1, str(rep)[:300])

# 2) ICU bay too small (12 m^2 < 15 m^2) on A-ROOM-BNDY-ICU
call("architecture", "define_room", {
    "vertices": [P(30000, 0), P(33464, 0), P(33464, 3464), P(30000, 3464)],
    "number": "ICU-BAD", "name": "Undersized ICU bay", "boundaryLayer": "A-ROOM-BNDY-ICU",
}, label="define undersized ICU bay (~12 m^2)")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.rooms.icu-room-min-area"},
              label="validate hospital.rooms.icu-room-min-area")
if isinstance(rep, dict):
    check("icu-room-min-area FLAGS the undersized bay", rep.get("violationCount", 0) >= 1, str(rep)[:300])

# 3) OR too small (20 m^2 < 36 m^2) on A-ROOM-BNDY-OR
call("architecture", "define_room", {
    "vertices": [P(40000, 0), P(44472, 0), P(44472, 4472), P(40000, 4472)],
    "number": "OR-BAD", "name": "Undersized OR", "boundaryLayer": "A-ROOM-BNDY-OR",
}, label="define undersized OR (~20 m^2)")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.rooms.or-min-area"},
              label="validate hospital.rooms.or-min-area")
if isinstance(rep, dict):
    check("or-min-area FLAGS the undersized OR", rep.get("violationCount", 0) >= 1, str(rep)[:300])

# 4) ensuite too small (3 m^2 < 4.5 m^2) on A-ROOM-BNDY-ENSUITE
call("architecture", "define_room", {
    "vertices": [P(50000, 0), P(51732, 0), P(51732, 1732), P(50000, 1732)],
    "number": "WC-BAD", "name": "Undersized ensuite", "boundaryLayer": "A-ROOM-BNDY-ENSUITE",
}, label="define undersized ensuite (~3 m^2)")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.rooms.ensuite-min-area"},
              label="validate hospital.rooms.ensuite-min-area")
if isinstance(rep, dict):
    check("ensuite-min-area FLAGS the undersized bathroom", rep.get("violationCount", 0) >= 1, str(rep)[:300])

# 5) a wall on a legacy lead-shield layer name, not yet migrated to A-WALL-LEAD
call("architecture", "draw_wall", {"start": P(60000, 0), "end": P(63000, 0), "thicknessMm": 200,
                                    "faceLayer": "RTG-SHIELD"}, label="draw wall on legacy RTG-SHIELD layer")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.walls.lead-shield-on-layer"},
              label="validate hospital.walls.lead-shield-on-layer")
if isinstance(rep, dict):
    check("lead-shield-on-layer FLAGS the legacy-layer wall", rep.get("violationCount", 0) >= 1, str(rep)[:300])

# 6) a wall on a legacy Faraday layer name, not yet migrated to A-WALL-FARA
call("architecture", "draw_wall", {"start": P(70000, 0), "end": P(73000, 0), "thicknessMm": 200,
                                    "faceLayer": "MRI-SHIELD"}, label="draw wall on legacy MRI-SHIELD layer")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.walls.faraday-on-layer"},
              label="validate hospital.walls.faraday-on-layer")
if isinstance(rep, dict):
    check("faraday-on-layer FLAGS the legacy-layer wall", rep.get("violationCount", 0) >= 1, str(rep)[:300])

print("\n" + "=" * 70)
print("Sanity: a COMPLIANT patient room must NOT be flagged (no false positive)")
print("=" * 70)
call("architecture", "define_room", {
    "vertices": [P(20000, 10000), P(24000, 10000), P(24000, 13750), P(20000, 13750)],
    "number": "W-OK", "name": "Compliant ward room", "boundaryLayer": "A-ROOM-BNDY-WARD",
}, label="define compliant ward room (15 m^2)")
_, rep = call("validators", "validate_with_rule", {"ruleId": "hospital.rooms.patient-room-min-area"},
              label="re-validate hospital.rooms.patient-room-min-area")
if isinstance(rep, dict):
    check("patient-room-min-area still flags ONLY the original bad room (count==1, not 2)",
          rep.get("violationCount", 0) == 1, str(rep)[:300])

passed = sum(1 for _, ok in results if ok)
total = len(results)
print(f"\n==== {passed}/{total} checks OK ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
