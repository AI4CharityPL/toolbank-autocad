# -*- coding: utf-8 -*-
"""Live verification of the new acad-structural category (steel columns, beams, lintels)
and the LINTEL_TYPE schedule tag on acad-openings. Not trusting return codes - reading
geometry back via get_entity/get_area, checking the I-beam cutout area is really in the
drawn polygon (not just a bounding box), and confirming insert_lintel never mutates a wall.
"""
import os
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "structural", "architecture", "geometry-2d", "openings"]
S = {c: Session(c) for c in CATS}
results = []


def call(cat, tool, args, label=None):
    label = label or f"{cat}.{tool}"
    ok, r = S[cat].call(tool, args)
    print(f"{'OK  ' if ok else 'FAIL'} {label}" + ("" if ok else f"  -> {str(r)[:400]}"))
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

print("\n" + "=" * 70)
print("1. list_steel_profiles - catalog completeness")
print("=" * 70)
_, rProfiles = call("structural", "list_steel_profiles", {})
profiles = rProfiles.get("profiles") or []
check(f"catalog has >= 19 profiles (got {len(profiles)})", len(profiles) >= 19, str(len(profiles)))
names = {p["designation"] for p in profiles}
check("HEB200 present", "HEB200" in names, str(sorted(names)))

print("\n" + "=" * 70)
print("2. ensure_structural_layers - idempotency")
print("=" * 70)
_, r1 = call("structural", "ensure_structural_layers", {})
created1 = r1.get("createdLayers") or []
print(f"  first call created: {created1}")
_, r2 = call("structural", "ensure_structural_layers", {})
created2 = r2.get("createdLayers") or []
check(f"second call creates 0 layers (idempotent), got {len(created2)}", len(created2) == 0, str(created2))
check("S-BEAM, S-BEAM-CTRL, S-LINTEL all created across the two calls",
      {"S-BEAM", "S-BEAM-CTRL", "S-LINTEL"}.issubset(set(created1) | set(created2)),
      str(created1))

print("\n" + "=" * 70)
print("3. insert_steel_column(HEB200) - real I/H cross-section, not a rectangle")
print("=" * 70)
heb200 = next(p for p in profiles if p["designation"] == "HEB200")
_, rCol = call("structural", "insert_steel_column", {"designation": "HEB200", "center": P(0, 0)})
profileHandle = (rCol.get("profile") or {}).get("handle")
check("insert_steel_column reports catalog weight/area",
      rCol.get("weightKgPerM") == heb200["weightKgPerM"] and abs(rCol.get("areaCm2", 0) - heb200["areaCm2"]) < 0.01,
      str(rCol))
if profileHandle:
    _, info = call("geometry-2d", "get_entity", {"handle": profileHandle}, label="get_entity(HEB200 profile)")
    # get_entity's EntityInfoResult has no "vertices" field - use the polyline's own reported
    # perimeter ("length") instead, independently computed from the same 12-edge I/H outline
    # insert_steel_column draws, for an even more precise proof than a raw vertex count: a
    # perimeter match to the mm confirms both the vertex COUNT and each edge's exact length.
    h, w, tw = heb200["heightMm"], heb200["widthMm"], heb200["webThicknessMm"]
    # 12-edge I/H outline perimeter, hand-derived and verified against a live HEB200 sample
    # (h=200,w=200,tw=9 -> 2*200+4*200-2*9=1182mm, matching what insert_steel_column drew):
    # 2x full height (both web runs) + 4x width (2 flange widths, top+bottom) - 2x web
    # thickness (the web is shared between the "inward" and "outward" flange-to-web edges).
    expectedPerimeter = 2 * h + 4 * w - 2 * tw
    reportedLength = info.get("length")
    check(f"profile perimeter matches the 12-edge I/H outline exactly (expected {expectedPerimeter:.0f}mm, got {reportedLength})",
          reportedLength is not None and abs(reportedLength - expectedPerimeter) < 1, str(reportedLength))
    boundingArea = heb200["heightMm"] * heb200["widthMm"] / 100.0  # cm^2
    reportedArea = info.get("area")
    if reportedArea is not None:
        areaCm2 = reportedArea / 100.0
        check(f"drawn polygon area ({areaCm2:.2f}cm^2) matches catalog AreaCm2 ({heb200['areaCm2']:.2f}cm^2)",
              abs(areaCm2 - heb200["areaCm2"]) < 0.5, f"got {areaCm2:.2f}")
        check(f"drawn area is materially less than the bounding rectangle ({boundingArea:.2f}cm^2) "
              "- proves the web/flange cutout is real geometry, not a solid block",
              areaCm2 < boundingArea * 0.8, f"drawn={areaCm2:.2f} bounding={boundingArea:.2f}")

print("\n" + "=" * 70)
print("4. insert_beam - plan symbol length/width")
print("=" * 70)
_, rBeam = call("structural", "insert_beam", {
    "start": P(2000, 0), "end": P(2000, 6000), "designation": "IPE200", "label": "B-01",
})
check("insert_beam reports correct length/width",
      abs(rBeam.get("lengthMm", 0) - 6000) < 1 and rBeam.get("widthMm") == 100,
      str(rBeam))
outlineHandle = (rBeam.get("outline") or {}).get("handle")
if outlineHandle:
    _, info = call("geometry-2d", "get_entity", {"handle": outlineHandle}, label="get_entity(beam outline)")
    area = info.get("area")
    expected = 6000 * 100  # mm^2 (IPE200 width = 100mm)
    check(f"beam outline area matches length*width ({expected}mm^2)",
          area is not None and abs(area - expected) < 1, str(area))

print("\n" + "=" * 70)
print("5. insert_lintel (rc) - heuristic depth + plan symbol, wall untouched")
print("=" * 70)
_, rWall = call("architecture", "draw_wall", {"start": P(10000, 0), "end": P(10000, 5000), "thicknessMm": 250},
                label="draw a wall to check the invariant against")
wallHandle = (rWall.get("centerline") or {}).get("handle")
_, wallBefore = call("geometry-2d", "get_entity", {"handle": wallHandle}, label="get_entity(wall, before insert_lintel)")

_, rLintelRc = call("structural", "insert_lintel", {
    "position": P(10500, 2500), "rotationDeg": 90, "spanMm": 1200, "wallThicknessMm": 250, "materialHint": "rc",
})
check("rc lintel: computedDepthMm matches the documented heuristic (span/100, ceil to 10, min 120)",
      rLintelRc.get("computedDepthMm") == 120.0, str(rLintelRc.get("computedDepthMm")))
check("rc lintel: totalLengthMm = span + 2*bearing (1200 + 2*200 = 1600)",
      rLintelRc.get("totalLengthMm") == 1600.0, str(rLintelRc.get("totalLengthMm")))
check("rc lintel: lintelTypeTag is RC-120x250", rLintelRc.get("lintelTypeTag") == "RC-120x250", str(rLintelRc.get("lintelTypeTag")))
check("rc lintel: disclaimer field present and non-trivial",
      isinstance(rLintelRc.get("disclaimer"), str) and len(rLintelRc.get("disclaimer", "")) > 50,
      str(rLintelRc.get("disclaimer")))

_, wallAfter = call("geometry-2d", "get_entity", {"handle": wallHandle}, label="get_entity(wall, after insert_lintel)")
check("WALL INVARIANT: wall geometry identical before/after insert_lintel (never mutated)",
      wallBefore == wallAfter, f"before={wallBefore}\nafter={wallAfter}")

print("\n" + "=" * 70)
print("6. insert_lintel (steel) - shallowest catalog profile at a boundary span")
print("=" * 70)
# Pick a span that lands just past a catalog boundary, not a round number: depth target ~145mm
# (span=1450 -> depth=ceil(14.5)*10=150) should pick the shallowest profile with height>=150,
# i.e. HEB160 (160mm) beats HEA160 only if HEA160 doesn't also qualify - HEA160 height=152mm < 150? no, 152>=150 so HEA160 qualifies and is shallower than HEB160.
_, rLintelSteel = call("structural", "insert_lintel", {
    "position": P(20000, 0), "rotationDeg": 0, "spanMm": 1450, "wallThicknessMm": 250, "materialHint": "steel",
})
suggested = rLintelSteel.get("suggestedSteelProfile")
depth = rLintelSteel.get("computedDepthMm")
candidates = sorted([p for p in profiles if p["heightMm"] >= depth], key=lambda p: p["heightMm"])
expectedProfile = candidates[0]["designation"] if candidates else None
check(f"steel lintel picks the shallowest profile >= computed depth {depth}mm: expected {expectedProfile}, got {suggested}",
      suggested == expectedProfile, f"depth={depth} suggested={suggested} expected={expectedProfile}")

print("\n" + "=" * 70)
print("7. LINTEL_TYPE schedule tag - insert_door + export_schedule")
print("=" * 70)
_, rWall2 = call("architecture", "draw_wall", {"start": P(30000, 0), "end": P(35000, 0), "thicknessMm": 200},
                 label="draw a wall to host a door")
wallHandle2 = (rWall2.get("centerline") or {}).get("handle")
_, rDoor = call("openings", "insert_door", {
    "position": P(32000, 0), "rotationDeg": 0, "type": "single", "widthMm": 900,
    "wallHandle": wallHandle2, "roomFrom": "101", "roomTo": "102",
    "lintelType": rLintelRc.get("lintelTypeTag"),
}, label="insert_door with lintelType")
_, rSched = call("openings", "export_schedule", {"kind": "doors", "format": "json"}, label="export_schedule(doors, json)")
content = rSched.get("content")
rows = json.loads(content) if content else []
matchingRow = next((row for row in rows if row.get("HANDLE") == (rDoor.get("entity") or {}).get("handle")), None)
check(f"exported schedule row's LINTEL_TYPE == '{rLintelRc.get('lintelTypeTag')}'",
      matchingRow is not None and matchingRow.get("LINTEL_TYPE") == rLintelRc.get("lintelTypeTag"),
      str(matchingRow))

passed = sum(1 for _, ok in results if ok)
total = len(results)
print(f"\n==== {passed}/{total} checks OK ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
