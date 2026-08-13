# -*- coding: utf-8 -*-
"""Rule-73 proof build #2: a small dental clinic (gabinet stomatologiczny), built zone-first
per docs/engineering-rules/73-space-planning-method.md, proving the method generalizes past
residential — and that hospital-derived capability (A-WALL-LEAD radiation shielding) transfers
to a much smaller, different typology rather than being hospital-only.

Design decided BEFORE any tool call (rule 73 steps 1-5), grounded in
docs/knowledge-base/dental-clinic/{ROOM-PROGRAM,AREA-CONVENTION,GRID-AND-LAYERS,STANDARDS}.md:

  Envelope: an L/Z-shaped (not a plain rectangle) 14000mm-wide footprint, ~139 m2 gross, within
  this typology's own 150-250 m2 scope band at the compact end (2 treatment rooms, the low end of
  "2-3" the room program allows). WALL_T=120mm (typical lightweight fit-out partition per
  AREA-CONVENTION.md), INSET=60mm net-internal - EXCEPT the RTG room's primary-beam wall, which
  is 200mm on layer A-WALL-LEAD (REUSED from the hospital typology, not reinvented - GRID-AND-
  LAYERS.md is explicit that this bank has exactly one shielded-wall layer, typology-agnostic).

  PUBLIC row   (y 0-4000, x0-9500 only - the building is narrower at the front): WC pacjentow |
               Poczekalnia (centred - borders BOTH its neighbours so both adjacency-table
               requirements, Poczekalnia-WC and Poczekalnia-Rejestracja, are satisfiable as
               direct doors) | Rejestracja.
  CORRIDOR-H   (y 4000-5500, full width): the "korytarz zabiegowy" the room program's own
               circulation-pattern section names.
  TREATMENT row (y 5500-9500, full width): Gabinet1 | Sterylizacja/Magazyn (west of the spine) |
               Gabinet2 | Gabinet RTG (east of the spine) - CORRIDOR-V (a vertical spine, x
               6250-7750) runs through the middle, open to CORRIDOR-H at y=5500 (no wall in that
               gap - a real T-junction, not a doored connection) so this is genuinely a T-shaped
               2D circulation system, not one more single row.
  STAFF row    (y 9500-11500, x0-6250 only - the building steps narrower again here): Pomieszczenie
               socjalne | WC personelu, reached via CORRIDOR-V's own continuation north.

  No room in this typology is declared daylight-required (per STANDARDS.md: WT SS93's daylight
  rule is a residential-room provision, does not apply here) - so this build has NO windows at
  all, a deliberate, documented choice, not an omission. Rule 60 SS1a criterion 19 is checked
  against an EMPTY daylight-required set - vacuously satisfied by design, not by luck.

  Furniture: `populate_room`/`populate_bathroom` called ONLY where this bank has a genuinely
  matching preset (Poczekalnia -> "waiting"; both WCs -> plumbing presets). Rejestracja, both
  Gabinety, Sterylizacja, Magazyn, RTG, Socjalne and both corridors get NO furniture call - this
  bank has no dental-chair or small-reception preset yet, and forcing the oversized "consult"
  (min 3500x4500mm) or "reception" (min 3000x4500mm) hospital-scale presets onto much smaller
  rooms would be a worse fit than leaving them honestly unfurnished. Flagged as a real catalog
  gap in the project README, not hidden.
"""
import os
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "architecture", "openings", "grids", "structural", "furniture", "plumbing", "schedules"]
S = {c: Session(c) for c in CATS}


def call(cat, tool, args, label=None):
    label = label or f"{cat}.{tool}"
    ok, r = S[cat].call(tool, args)
    print(f"{'OK  ' if ok else 'FAIL'} {label}" + ("" if ok else f"  -> {str(r)[:400]}"))
    if not ok:
        raise SystemExit(f"aborting: {label} failed -> {r}")
    return r


def P(x, y):
    return {"x": x, "y": y}


WALL_T = 120.0
INSET = WALL_T / 2.0       # 60mm
RTG_WALL_T = 200.0
RTG_INSET = RTG_WALL_T / 2.0  # 100mm, west (shielded) side of the RTG room only

print("== fresh drawing: dental-clinic-test ==")
call("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
call("architecture", "ensure_architectural_layers", {})
call("structural", "ensure_structural_layers", {})

print("\n" + "=" * 70)
print("STEP 4: STRUCTURAL GRID FIRST - inherited-from-shell in reality (GRID-AND-LAYERS.md),")
print("placed anyway to demonstrate the tool/step; independent of the partition layout")
print("=" * 70)
X_SPACINGS = [4667.0, 4666.0, 4667.0]   # 3 bays, sums to 14000
Y_SPACINGS = [5750.0, 5750.0]           # 2 bays, sums to 11500
call("grids", "draw_grid", {"origin": P(0, 0), "xSpacingsMm": X_SPACINGS, "ySpacingsMm": Y_SPACINGS},
     label="draw_grid (3x2 bays)")
grid_xs = [0.0]
for d in X_SPACINGS:
    grid_xs.append(grid_xs[-1] + d)
grid_ys = [0.0]
for d in Y_SPACINGS:
    grid_ys.append(grid_ys[-1] + d)
for gx in grid_xs:
    for gy in grid_ys:
        call("structural", "insert_steel_column", {"designation": "HEA100", "center": P(gx, gy)},
             label=f"column HEA100 @ ({gx:.0f},{gy:.0f})")
call("structural", "insert_beam", {
    "start": P(0, 0), "end": P(9500, 0), "designation": "IPE100", "label": "B-DEN-01",
}, label="facade beam IPE100 (south, public row)")

print("\n" + "=" * 70)
print("STEP 6: L/Z-SHAPED EXTERIOR PERIMETER")
print("=" * 70)
# draw_walls_chain was tried first (one call, 8 vertices) but insert_door's cut_wall_for_opening
# only supports 2-vertex wall polylines - confirmed live: "polyline has 8 vertices; only 2-vertex
# polylines supported... use split_wall_at_opening for multi-segment walls." Rather than pull in
# a second wall-cutting tool for the one perimeter door, draw the 8-vertex L/Z perimeter as 8
# individual draw_wall segments instead - same 2-vertex-per-wall shape every other wall in this
# build already uses, and the one exterior door (D-01) only needs its own single segment cut.
PERIMETER = [P(0, 0), P(9500, 0), P(9500, 4000), P(14000, 4000), P(14000, 9500),
             P(7750, 9500), P(7750, 11500), P(0, 11500)]
walls = {}
perim_names = ["south_pub", "east_step_a", "north_step_a", "east_treat", "north_step_b",
               "east_step_b", "north_staff", "west"]
for i, name in enumerate(perim_names):
    a, b = PERIMETER[i], PERIMETER[(i + 1) % len(PERIMETER)]
    walls[name] = call("architecture", "draw_wall", {"start": a, "end": b, "thicknessMm": WALL_T},
                        label=f"perimeter segment {name} ({a['x']:.0f},{a['y']:.0f})-({b['x']:.0f},{b['y']:.0f})")["centerline"]["handle"]

print("\n-- interior partitions --")
walls["x2000"] = call("architecture", "draw_wall", {"start": P(2000, 0), "end": P(2000, 4000), "thicknessMm": WALL_T},
                       label="public row x=2000 (WC pacjentow | Poczekalnia)")["centerline"]["handle"]
walls["x7000"] = call("architecture", "draw_wall", {"start": P(7000, 0), "end": P(7000, 4000), "thicknessMm": WALL_T},
                       label="public row x=7000 (Poczekalnia | Rejestracja)")["centerline"]["handle"]
walls["y4000"] = call("architecture", "draw_wall", {"start": P(0, 4000), "end": P(9500, 4000), "thicknessMm": WALL_T},
                       label="y=4000 (public row | Korytarz-H)")["centerline"]["handle"]
walls["y5500a"] = call("architecture", "draw_wall", {"start": P(0, 5500), "end": P(6250, 5500), "thicknessMm": WALL_T},
                        label="y=5500 west (Korytarz-H | Gabinet1/Sterylizacja)")["centerline"]["handle"]
walls["y5500b"] = call("architecture", "draw_wall", {"start": P(7750, 5500), "end": P(14000, 5500), "thicknessMm": WALL_T},
                        label="y=5500 east (Korytarz-H | Gabinet2/RTG)")["centerline"]["handle"]
# x=6250..7750 at y=5500 is deliberately left OPEN - the T-junction where Korytarz-V meets
# Korytarz-H, a real open threshold, not a doored connection.
walls["x6250"] = call("architecture", "draw_wall", {"start": P(6250, 5500), "end": P(6250, 11500), "thicknessMm": WALL_T},
                       label="x=6250 (West wing + staff row | Korytarz-V)")["centerline"]["handle"]
walls["x7750"] = call("architecture", "draw_wall", {"start": P(7750, 5500), "end": P(7750, 9500), "thicknessMm": WALL_T},
                       label="x=7750 (Korytarz-V | Gabinet2)")["centerline"]["handle"]
walls["x3125"] = call("architecture", "draw_wall", {"start": P(3125, 5500), "end": P(3125, 9500), "thicknessMm": WALL_T},
                       label="x=3125 (Gabinet1 | Sterylizacja/Magazyn)")["centerline"]["handle"]
walls["y8000"] = call("architecture", "draw_wall", {"start": P(3125, 8000), "end": P(6250, 8000), "thicknessMm": WALL_T},
                       label="y=8000 (Sterylizacja | Magazyn)")["centerline"]["handle"]
walls["y9500"] = call("architecture", "draw_wall", {"start": P(0, 9500), "end": P(6250, 9500), "thicknessMm": WALL_T},
                       label="y=9500 (Treatment row | Staff row, west only - no door, staff reached via corridor)")["centerline"]["handle"]
walls["x4000s"] = call("architecture", "draw_wall", {"start": P(4000, 9500), "end": P(4000, 11500), "thicknessMm": WALL_T},
                        label="staff row x=4000 (Socjalne | WC personelu)")["centerline"]["handle"]
# The RTG room's primary-beam wall: shielded, thicker, on A-WALL-LEAD - REUSED hospital layer
# (docs/knowledge-base/hospital/GRID-AND-LAYERS.md), not a new one. Only this ONE wall is
# shielded ("wall bearing the primary beam direction" per STANDARDS.md - not all 4 walls; a
# deliberately less conservative, more accurate choice than blanket-shielding the whole room).
walls["x10875"] = call("architecture", "draw_wall", {
    "start": P(10875, 5500), "end": P(10875, 9500), "thicknessMm": RTG_WALL_T, "faceLayer": "A-WALL-LEAD",
}, label="x=10875 SHIELDED (Gabinet2 | Gabinet RTG, A-WALL-LEAD)")["centerline"]["handle"]

lintel_count = 0


def lintel_and_door(wall_name, pos, rot, width_mm, room_from, room_to, number, wall_t=WALL_T):
    global lintel_count
    r = call("structural", "insert_lintel", {
        "position": pos, "rotationDeg": rot, "spanMm": width_mm, "wallThicknessMm": wall_t, "materialHint": "rc",
    }, label=f"lintel over door {number}")
    lintel_count += 1
    dr = call("openings", "insert_door", {
        "position": pos, "rotationDeg": rot, "type": "single", "widthMm": width_mm,
        "wallHandle": walls[wall_name], "roomFrom": room_from, "roomTo": room_to, "number": number,
        "lintelType": r.get("lintelTypeTag"),
    }, label=f"door {number}: {room_from} -> {room_to}")
    opening = dr.get("wallOpening") or {}
    walls[wall_name] = opening.get("rightHandle") or opening.get("leftHandle") or walls[wall_name]


print("\n" + "=" * 70)
print("STEP 6 cont'd + STEP 7: doors, each with a rule-72 lintel. Multi-door walls (y4000, x6250)")
print("cut in increasing-position order so the right-remnant handle threading stays valid.")
print("=" * 70)
lintel_and_door("south_pub", P(4500, 0), 0, 1000, "EXT", "PUB.1", "D-01")
lintel_and_door("x2000", P(2000, 2000), 90, 900, "PUB.1", "PUB.2", "D-02")
lintel_and_door("x7000", P(7000, 2000), 90, 900, "PUB.1", "PUB.3", "D-03")
lintel_and_door("y4000", P(4500, 4000), 0, 900, "PUB.1", "COR.H", "D-04")
lintel_and_door("y4000", P(8250, 4000), 0, 900, "PUB.3", "COR.H", "D-05")
lintel_and_door("x6250", P(6250, 6750), 90, 900, "TRT.STR", "COR.V", "D-06")
# TRT.1 (Gabinet1, x0-3125) does NOT border x=6250 - the Sterylizacja/Magazyn column (x3125-6250)
# sits between them. Its real shared wall is y5500a (its own south edge, onto Korytarz-H) -
# caught live: the original x=6250 placement failed cut_wall_for_opening with jambs projecting
# outside the wall segment, which is what a door on a wall the room doesn't actually touch looks
# like from the tool's side.
lintel_and_door("y5500a", P(1500, 5500), 0, 900, "TRT.1", "COR.H", "D-07")
lintel_and_door("x6250", P(6250, 10500), 90, 800, "STF.WC", "COR.V", "D-08")
lintel_and_door("x7750", P(7750, 7500), 90, 900, "TRT.2", "COR.V", "D-09")
lintel_and_door("y5500b", P(12440, 5500), 0, 900, "TRT.RTG", "COR.H", "D-10")
lintel_and_door("y8000", P(4700, 8000), 0, 800, "TRT.STR", "TRT.MAG", "D-11")
lintel_and_door("x4000s", P(4000, 10500), 90, 800, "STF.SOC", "STF.WC", "D-12")

print("\n" + "=" * 70)
print("STEP 6 cont'd: define_room, net-internal inset vertices (rule 71 step 3)")
print("RTG room uses an ASYMMETRIC inset - 100mm on its shielded (west) side, 60mm elsewhere -")
print("since that one wall is genuinely thicker, not a uniform-inset shortcut.")
print("=" * 70)


def room(number, name, v, boundary_layer=None):
    kwargs = {"vertices": v, "number": number, "name": name}
    if boundary_layer:
        kwargs["boundaryLayer"] = boundary_layer
    r = call("architecture", "define_room", kwargs, label=f"define_room {number} {name}")
    xs = [p["x"] for p in v]
    ys = [p["y"] for p in v]
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    print(f"      net footprint: {w:.0f} x {h:.0f}mm = {w * h / 1e6:.2f} m2")
    return r, (P(min(xs), min(ys)), P(max(xs), max(ys)))


def rect(x0, x1, y0, y1, inset=INSET):
    return [P(x0 + inset, y0 + inset), P(x1 - inset, y0 + inset), P(x1 - inset, y1 - inset), P(x0 + inset, y1 - inset)]


rooms = {}
rooms["PUB.2"] = room("PUB.2", "WC pacjentow", rect(0, 2000, 0, 4000), boundary_layer="A-ROOM-BNDY-BATH-RES")
rooms["PUB.1"] = room("PUB.1", "Poczekalnia", rect(2000, 7000, 0, 4000))
rooms["PUB.3"] = room("PUB.3", "Rejestracja", rect(7000, 9500, 0, 4000))
rooms["COR.H"] = room("COR.H", "Korytarz zabiegowy (poziomy)", rect(0, 14000, 4000, 5500))
rooms["COR.V"] = room("COR.V", "Korytarz zabiegowy (trzon pionowy)", rect(6250, 7750, 5500, 11500))
rooms["TRT.1"] = room("TRT.1", "Gabinet zabiegowy 1", rect(0, 3125, 5500, 9500))
rooms["TRT.STR"] = room("TRT.STR", "Sterylizacja", rect(3125, 6250, 5500, 8000))
rooms["TRT.MAG"] = room("TRT.MAG", "Magazyn", rect(3125, 6250, 8000, 9500))
rooms["TRT.2"] = room("TRT.2", "Gabinet zabiegowy 2", rect(7750, 10875, 5500, 9500))
rooms["TRT.RTG"] = room("TRT.RTG", "Gabinet RTG punktowe", [
    P(10875 + RTG_INSET, 5500 + INSET), P(14000 - INSET, 5500 + INSET),
    P(14000 - INSET, 9500 - INSET), P(10875 + RTG_INSET, 9500 - INSET),
], boundary_layer="A-ROOM-BNDY")
rooms["STF.SOC"] = room("STF.SOC", "Pomieszczenie socjalne", rect(0, 4000, 9500, 11500))
rooms["STF.WC"] = room("STF.WC", "WC personelu", rect(4000, 6250, 9500, 11500), boundary_layer="A-ROOM-BNDY-BATH-RES")

print("\n" + "=" * 70)
print("STEP 8: furniture / plumbing - ONLY where a genuinely matching preset exists (see docstring)")
print("=" * 70)


def bbox_of(room_entry):
    return room_entry[1]


call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["PUB.1"])[0], "bboxMax": bbox_of(rooms["PUB.1"])[1],
    "preset": "waiting", "roomName": "PUB.1",
}, label="populate_room PUB.1 Poczekalnia (preset=waiting)")
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["PUB.2"])[0], "bboxMax": bbox_of(rooms["PUB.2"])[1],
    "preset": "wc-accessible", "accessible": True, "roomName": "PUB.2",
}, label="populate_bathroom PUB.2 WC pacjentow (preset=wc-accessible)")
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["STF.WC"])[0], "bboxMax": bbox_of(rooms["STF.WC"])[1],
    "preset": "wc-public", "roomName": "STF.WC",
}, label="populate_bathroom STF.WC WC personelu (preset=wc-public)")

os.makedirs(os.path.join(REPO, "projects", "dental-clinic-test"), exist_ok=True)
save_path = os.path.join(REPO, "projects", "dental-clinic-test", "DentalClinicTest.dwg")
call("files", "save_document_as", {"path": save_path}, label="save_document_as")

print(f"\nDoors: 12   Lintels: {lintel_count}   Columns: {len(grid_xs) * len(grid_ys)}   Beams: 1   Rooms: {len(rooms)}")

print("\n" + "=" * 70)
print("STEP 9: verification - audit_all_rooms + rule 60 SS1a criteria 18-20")
print("marginMm must exceed max(widthMm)/2 + 1.5*cellMm (rule 73's own root-caused note) -")
print("widest opening here is 1000mm (D-01) -> r=575mm at cellMm=50; use 700mm for slack.")
print("=" * 70)
audit = call("schedules", "audit_all_rooms", {"cellMm": 50, "marginMm": 700, "tolerancePct": 10.0},
             label="audit_all_rooms")
rows = audit.get("rows", [])
bad = [row for row in rows if row.get("flags")]
print(f"rooms audited: {len(rows)}   rows with any flag: {len(bad)}")
for row in bad:
    print(f"  FLAGGED: {json.dumps(row, ensure_ascii=False)[:300]}")

print("\n-- criterion 18: public zone reachable from entry without crossing a private/back zone --")
_, doorList = S["openings"].call("list_openings_in_model", {"kind": "doors"})
edges = [(d.get("roomFrom"), d.get("roomTo")) for d in (doorList.get("openings") or doorList.get("doors") or [])]
print(f"  door roomFrom/roomTo pairs: {edges}")
public_rooms = {"PUB.1", "PUB.2", "PUB.3"}
back_rooms = {"TRT.1", "TRT.STR", "TRT.MAG", "TRT.2", "TRT.RTG", "STF.SOC", "STF.WC"}
crosses_back = any((a in public_rooms and b in back_rooms) or (a in back_rooms and b in public_rooms)
                    for a, b in edges if a and b)
print(f"  no door edge jumps directly from a public room into a treatment/staff room: {not crosses_back}")
print("  (public rooms only ever touch EXT or COR.H/COR.V, matching the declared adjacency table)")

print("\n-- criterion 19: daylight-declared rooms actually have a window --")
print("  this typology declares NO room as daylight-required (STANDARDS.md: WT SS93 doesn't apply")
print("  to non-residential rooms) - vacuously satisfied by an empty required set, by design: True")

print("\n-- criterion 20: built adjacency graph vs. this project's own declared table --")
declared = {("EXT", "PUB.1"), ("PUB.1", "PUB.2"), ("PUB.1", "PUB.3"), ("PUB.1", "COR.H"), ("PUB.3", "COR.H"),
            ("TRT.STR", "COR.V"), ("TRT.1", "COR.H"), ("STF.WC", "COR.V"), ("TRT.2", "COR.V"),
            ("TRT.RTG", "COR.H"), ("TRT.STR", "TRT.MAG"), ("STF.SOC", "STF.WC")}
built = set(edges)
print(f"  declared - built (missing): {declared - built}")
print(f"  built - declared (unexpected): {built - declared}")
print(f"  adjacency graph matches declared table exactly: {declared == built}")

print("\n==== dental-clinic-test build complete ====")
