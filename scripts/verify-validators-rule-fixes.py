# -*- coding: utf-8 -*-
"""Test #3: validators against DELIBERATE violations - not a clean drawing, on purpose.

Reads the actual rule YAMLs first (not just descriptions) to build violations the rules'
real `scope`/`checks` will genuinely fire on, and a control that must NOT fire.

This is the SECOND pass, after fixing two rules found by the first: arch.doors.must-have-room-tag
checked a ROOM_NUMBER attribute insert_door never sets (it sets ROOM_FROM/ROOM_TO), and
arch.columns.on-s-cols-layer's scope pattern included A-WALL, colliding with
arch.walls.on-walls-layer's own correct target layer. Both YAMLs were corrected; this run proves
the fix works BOTH ways - catches genuine violations, does not flag correct usage - not just that
it stopped complaining.

Violations planted:
  A. a wall FACE polyline on legacy layer "WALLS" instead of A-WALL         (hasFix=true)
  B. a wall CENTERLINE line on legacy layer "CENTERLINE" instead of A-WALL-CTRL (hasFix=true)
  C. real geometry on layer "0"                                             (hasFix=false)
  D-good. a door via openings.insert_door WITH roomFrom filled in - must now PASS
  D-bad.  a door via openings.insert_door WITHOUT roomFrom - must still FAIL
  (no title block anywhere -> arch.titleblock.must-be-defined, hasFix=false)

Control: one wall drawn normally via architecture.draw_wall (lands on A-WALL/A-WALL-CTRL by
default) - must NOT appear in any violation list. A validator that flags everything is as
useless as one that flags nothing.

Then: auto_fix_violations on the two hasFix=true rules, re-validate, and confirm by DIRECTLY
reading the fixed entities' own layer property - not by trusting the validator's own second
opinion of itself.
"""
import os
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "architecture", "openings", "geometry-2d", "validators", "layers"]
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


print("== fresh drawing ==")
call("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
call("architecture", "ensure_architectural_layers", {})

print("\n== control: ONE correctly-placed wall (must NOT be flagged by anything) ==")
_, rCtrl = call("architecture", "draw_wall", {"start": P(0, 0), "end": P(4000, 0), "thicknessMm": 200},
                label="control wall via draw_wall (lands on A-WALL/A-WALL-CTRL)")
ctrlFace = (rCtrl.get("leftFace") or {}).get("handle") if isinstance(rCtrl, dict) else None
ctrlCenter = (rCtrl.get("centerline") or {}).get("handle") if isinstance(rCtrl, dict) else None

print("\n== violation A: wall face on legacy layer 'WALLS' instead of A-WALL ==")
_, rA = call("geometry-2d", "draw_polyline", {
    "vertices": [P(0, 2000), P(3000, 2000), P(3000, 2100), P(0, 2100)], "closed": True, "layer": "WALLS",
}, label="wall-shaped polyline on layer WALLS (wrong)")
handleA = rA["entity"]["handle"] if isinstance(rA, dict) and "entity" in rA else None

print("\n== violation B: wall centerline on legacy layer 'CENTERLINE' instead of A-WALL-CTRL ==")
_, rB = call("geometry-2d", "draw_line", {"start": P(0, 2050), "end": P(3000, 2050), "layer": "CENTERLINE"},
             label="centreline on layer CENTERLINE (wrong)")
handleB = rB["entity"]["handle"] if isinstance(rB, dict) and "entity" in rB else None

print("\n== door WITH roomFrom/roomTo filled in - must now PASS (the fix target) ==")
_, rWall = call("architecture", "draw_wall", {"start": P(0, 4000), "end": P(4000, 4000), "thicknessMm": 200},
                label="a wall to host the door")
_, rDGood = call("openings", "insert_door", {
    "position": P(2000, 4000), "rotationDeg": 0, "type": "single", "widthMm": 900,
    "roomFrom": "101", "roomTo": "CORRIDOR",
}, label="door WITH roomFrom/roomTo filled in (a reasonable caller's good-faith attempt)")
handleDGood = rDGood["entity"]["handle"] if isinstance(rDGood, dict) and "entity" in rDGood else None

print("\n== NEGATIVE CONTROL: door WITHOUT roomFrom - the rule must still catch a genuine violation ==")
_, rWall2 = call("architecture", "draw_wall", {"start": P(0, 5000), "end": P(4000, 5000), "thicknessMm": 200},
                 label="a second wall to host the control door")
_, rDBad = call("openings", "insert_door", {
    "position": P(2000, 5000), "rotationDeg": 0, "type": "single", "widthMm": 900,
}, label="door WITHOUT roomFrom/roomTo (genuinely incomplete)")
handleDBad = rDBad["entity"]["handle"] if isinstance(rDBad, dict) and "entity" in rDBad else None

print("\n== violation C: real geometry on layer '0' ==")
_, rC = call("geometry-2d", "draw_circle", {"center": P(5000, 2000), "radius": 300, "layer": "0"},
             label="circle on layer 0 (wrong)")

print("\n== validate_drawing: catch everything, but ONLY what's really wrong (no discipline filter this ==")
print("   time - general.layers.no-construction-on-layer-zero is discipline=general, not architectural)")
_, rVal = call("validators", "validate_drawing", {})
if isinstance(rVal, dict):
    byRule = {}
    for v in rVal.get("violations", []):
        byRule.setdefault(v.get("ruleId"), []).append(v.get("entityHandle"))
    print(f"  violationCount={rVal.get('violationCount')}  errorCount={rVal.get('errorCount')}  warningCount={rVal.get('warningCount')}")
    for rid, handles in byRule.items():
        print(f"    {rid}: {handles}")

    check("PROVEN violation A fires on the exact entity (wall-on-legacy-layer)",
          handleA in byRule.get("arch.walls.on-walls-layer", []), str(byRule.get("arch.walls.on-walls-layer")))
    check("PROVEN violation B fires on the exact entity (centreline-on-legacy-layer)",
          handleB in byRule.get("arch.walls.centerline-on-a-wall-ctrl", []), str(byRule.get("arch.walls.centerline-on-a-wall-ctrl")))
    check("PROVEN violation C fires (geometry on layer 0)",
          "general.layers.no-construction-on-layer-zero" in byRule, str(list(byRule.keys())))
    check("PROVEN the FIXED rule now PASSES for a door with roomFrom genuinely filled in "
          "(the bug is gone, not just relaxed into always-passing)",
          handleDGood not in byRule.get("arch.doors.must-have-room-tag", []),
          str(byRule.get("arch.doors.must-have-room-tag")))
    check("PROVEN the FIXED rule still CATCHES a genuinely incomplete door (no roomFrom) - "
          "not disabled, just correctly targeted now",
          handleDBad in byRule.get("arch.doors.must-have-room-tag", []),
          str(byRule.get("arch.doors.must-have-room-tag")))
    check("PROVEN the title-block rule fires (none was ever inserted)",
          "arch.titleblock.must-be-defined" in byRule, str(list(byRule.keys())))
    check("PROVEN the CONTROL wall is NOT flagged by the wall-layer rule",
          ctrlFace not in byRule.get("arch.walls.on-walls-layer", []), str(byRule.get("arch.walls.on-walls-layer")))
    check("PROVEN the CONTROL centreline is NOT flagged by the centreline rule",
          ctrlCenter not in byRule.get("arch.walls.centerline-on-a-wall-ctrl", []),
          str(byRule.get("arch.walls.centerline-on-a-wall-ctrl")))

print("\n== auto_fix_violations: only the two hasFix=true rules ==")
_, rFix = call("validators", "auto_fix_violations", {
    "ruleIds": ["arch.walls.on-walls-layer", "arch.walls.centerline-on-a-wall-ctrl"], "dryRun": False,
})
if isinstance(rFix, dict):
    print(json.dumps(rFix, indent=2, ensure_ascii=False)[:800])
    check("PROVEN auto_fix reports both fixes applied", rFix.get("applied") == 2, str(rFix)[:300])

print("\n== VERIFY THE FIX DIRECTLY - read the entities' own layer property, not the validator's opinion ==")
_, rEntA = call("geometry-2d", "get_entity", {"handle": handleA}, label="get_entity on the fixed wall-face polyline")
_, rEntB = call("geometry-2d", "get_entity", {"handle": handleB}, label="get_entity on the fixed centreline")
layerA = rEntA.get("layer") if isinstance(rEntA, dict) else None
layerB = rEntB.get("layer") if isinstance(rEntB, dict) else None
check("PROVEN entity A's layer is now literally 'A-WALL', read directly, not inferred from the "
      "validator's own re-scan", layerA == "A-WALL", f"layer={layerA}")
check("PROVEN entity B's layer is now literally 'A-WALL-CTRL', read directly",
      layerB == "A-WALL-CTRL", f"layer={layerB}")

print("\n== re-validate: A and B must be GONE, C/D-bad/titleblock must STILL be there (auto_fix didn't touch them) ==")
_, rVal2 = call("validators", "validate_drawing", {})
if isinstance(rVal2, dict):
    byRule2 = {}
    for v in rVal2.get("violations", []):
        byRule2.setdefault(v.get("ruleId"), []).append(v.get("entityHandle"))
    check("PROVEN violation A is GONE after auto_fix", handleA not in byRule2.get("arch.walls.on-walls-layer", []),
          str(byRule2.get("arch.walls.on-walls-layer")))
    check("PROVEN violation B is GONE after auto_fix",
          handleB not in byRule2.get("arch.walls.centerline-on-a-wall-ctrl", []),
          str(byRule2.get("arch.walls.centerline-on-a-wall-ctrl")))
    check("PROVEN violation C (no auto-fix available) is UNCHANGED - still present",
          "general.layers.no-construction-on-layer-zero" in byRule2, str(list(byRule2.keys())))
    check("PROVEN the incomplete door (D-bad) is UNCHANGED - still present (no auto-fix available)",
          handleDBad in byRule2.get("arch.doors.must-have-room-tag", []),
          str(byRule2.get("arch.doors.must-have-room-tag")))
    check("PROVEN the good door (D-good) is STILL passing - unaffected by auto_fix",
          handleDGood not in byRule2.get("arch.doors.must-have-room-tag", []),
          str(byRule2.get("arch.doors.must-have-room-tag")))
    check("PROVEN arch.columns.on-s-cols-layer does NOT fire on entity A now that auto_fix moved "
          "it onto A-WALL - the exact collision this fix removed",
          "arch.columns.on-s-cols-layer" not in byRule2 or handleA not in byRule2.get("arch.columns.on-s-cols-layer", []),
          str(byRule2.get("arch.columns.on-s-cols-layer")))
    check("PROVEN arch.columns.on-s-cols-layer does NOT fire on the control wall either",
          "arch.columns.on-s-cols-layer" not in byRule2 or ctrlFace not in byRule2.get("arch.columns.on-s-cols-layer", []),
          str(byRule2.get("arch.columns.on-s-cols-layer")))

passed = sum(1 for _, ok, _ in LOG if ok) + sum(1 for _, ok in results if ok)
total = len(LOG) + len(results)
print(f"\n==== {passed}/{total} checks passed ====")
for label, ok, r in LOG:
    if not ok:
        print(f"  FAILED: {label} -> {str(r)[:250]}")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
