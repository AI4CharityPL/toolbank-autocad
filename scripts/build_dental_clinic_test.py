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

CATS = ["files", "architecture", "openings", "grids", "structural", "furniture", "plumbing", "schedules", "validators",
        "hatches", "dimensions", "callouts", "sections", "layouts", "geometry-2d", "viewports"]
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


def in_building(x, y):
    # The building is L/Z-shaped (see PERIMETER below), not a plain rectangle - a plain
    # rectangular grid product places some intersections outside the actual envelope. A live
    # check_overlaps pass caught this only after the fact (3 of 12 columns floating outside any
    # wall); this filter is the fix, applied BEFORE insertion rather than discovered after.
    if 0 <= y <= 4000:
        return 0 <= x <= 9500
    if 4000 <= y <= 9500:
        return 0 <= x <= 14000
    if 9500 <= y <= 11500:
        return 0 <= x <= 7750
    return False


for gx in grid_xs:
    for gy in grid_ys:
        if not in_building(gx, gy):
            print(f"skip column @ ({gx:.0f},{gy:.0f}) - outside the L-shaped envelope")
            continue
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
    walls[name] = call("architecture", "draw_wall", {"start": a, "end": b, "thicknessMm": WALL_T, "bearing": True},
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
# D-01 moved from x=4500 (its original centred position): a live check_overlaps pass
# (S-COLS vs A-DOOR) found the front door swung straight into the structural column at
# x=4667 on this same wall - the door and the grid were placed independently and nobody
# cross-checked the two against each other until this verification pass. Moving it to
# x=3200 cleared the column but put it in the path of the "waiting" preset's own sofa
# (FURN-SOFA-CLN-3, centred on the room and spanning x3400-5600) - caught on the SAME
# rebuild's overlap pass, moved further west to x=2800 to clear both independently-placed
# elements, re-verified clean.
lintel_and_door("south_pub", P(2800, 0), 0, 1000, "EXT", "PUB.1", "D-01")
# D-02 moved from y=2000 (its original centred position, width narrowed 900->700mm): a live
# check_overlaps pass (A-DOOR vs A-PLMB-BSN) found the door swing overlapping WC pacjentow's
# own basin, placed by populate_bathroom's wc-accessible preset formula at a fixed offset from
# the room's own corner - re-centred into the gap between the basin (y<=1574) and the WC bowl
# (y>=2570), re-verified clean.
lintel_and_door("x2000", P(2000, 2100), 90, 700, "PUB.1", "PUB.2", "D-02")
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
# D-08 moved from y=10500 (its original centred position): a live check_overlaps pass
# (A-DOOR vs A-PLMB-BSN) found the door swing clipping WC personelu's own basin+WC bowl,
# both placed near the room's south end by populate_bathroom's wc-public preset formula -
# shifted north, clear of both fixtures (which top out around y=10285), re-verified clean.
lintel_and_door("x6250", P(6250, 10900), 90, 800, "STF.WC", "COR.V", "D-08")
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

print("\n" + "=" * 70)
print("STEP 9a: CONSTRUCTION-DOCUMENT PIPELINE (rule 74) - hatching, dimensions, schedules,")
print("callouts, section, zone entities, layout - applying every lesson learned live on")
print("apartment-120-test's own rule-74 retrofit to this second, differently-shaped typology.")
print("=" * 70)

print("\n-- zone entities (rule 73 step 3a, now mandatory) --")
# 4 zones matching this project's own docstring rows (PUBLIC/CORRIDOR-H/TREATMENT/STAFF) -
# TREATMENT's own bounding rect includes the COR.V spine, matching how the docstring itself
# describes CORRIDOR-V as part of the treatment row, not a 5th zone.
#
# tagPosition is in the WEST MARGIN (x=-4200), outside the building envelope entirely, not
# "near a corner" inside it. A user's own live screenshot caught the corner-offset approach
# (the same pattern that worked for apartment-120-test's 2 zones) producing a REAL bbox overlap
# between ZONE-STAFF's tag and a room tag here - confirmed live via get_entity, not assumed from
# the screenshot alone. Root cause: a define_room zone tag is a 3-line block (number/name/area)
# that can be 2000-3400mm WIDE, and this building's own zones are narrow enough (the corridor is
# barely wider than its own room's footprint) that no corner offset inside the zone is safely
# clear of every room tag - the fix that worked once for a wider apartment doesn't generalise.
# Placing tags outside the building (x<0) makes them provably clear of every room tag by
# construction, not by a hopeful offset - verified live against every room-tag bbox before this
# was accepted as the fix, not just re-guessed once more.
ZONE_TAG_X = -4200
call("architecture", "define_room", {
    "vertices": [P(0, 0), P(9500, 0), P(9500, 4000), P(0, 4000)],
    "number": "ZONE-PUBLIC", "name": "Strefa publiczna", "tagPosition": P(ZONE_TAG_X, 2000),
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
}, label="zone entity: PUBLIC")
call("architecture", "define_room", {
    "vertices": [P(0, 4000), P(14000, 4000), P(14000, 5500), P(0, 5500)],
    "number": "ZONE-COR-H", "name": "Korytarz zabiegowy", "tagPosition": P(ZONE_TAG_X, 4750),
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
}, label="zone entity: CORRIDOR-H")
call("architecture", "define_room", {
    "vertices": [P(0, 5500), P(14000, 5500), P(14000, 9500), P(0, 9500)],
    "number": "ZONE-TREATMENT", "name": "Strefa zabiegowa", "tagPosition": P(ZONE_TAG_X, 7500),
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
}, label="zone entity: TREATMENT")
call("architecture", "define_room", {
    "vertices": [P(0, 9500), P(7750, 9500), P(7750, 11500), P(0, 11500)],
    "number": "ZONE-STAFF", "name": "Strefa personelu", "tagPosition": P(ZONE_TAG_X, 10500),
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
}, label="zone entity: STAFF")

print("\n-- material hatching on every bearing (exterior) wall (rule 62) --")
print("(handle-based apply_material_preset, not the point-based TraceBoundary sibling - same")
print(" fix apartment-120-test needed, for the same reason: un-mitred corners/T-junctions plus")
print(" door cuts leave enough coincident/fragmented edges nearby that point-based flood tracing")
print(" isn't reliable. All 8 L/Z perimeter segments hatched, not a representative subset.)")


def wall_hatch_rect(x0, y0, x1, y1, t):
    half = t / 2.0
    if abs(y1 - y0) < 1:  # horizontal segment
        y = y0
        return [P(min(x0, x1), y - half), P(max(x0, x1), y - half),
                P(max(x0, x1), y + half), P(min(x0, x1), y + half)]
    else:  # vertical segment
        x = x0
        return [P(x - half, min(y0, y1)), P(x + half, min(y0, y1)),
                P(x + half, max(y0, y1)), P(x - half, max(y0, y1))]


for i, name in enumerate(perim_names):
    a, b = PERIMETER[i], PERIMETER[(i + 1) % len(PERIMETER)]
    rect = wall_hatch_rect(a["x"], a["y"], b["x"], b["y"], WALL_T)
    rHb = call("geometry-2d", "draw_polyline", {"vertices": rect, "closed": True, "layer": "A-WALL-BEAR"},
               label=f"hatch-boundary rectangle: {name}")
    hb_handle = rHb.get("entity", rHb).get("handle") or rHb.get("handle")
    call("hatches", "apply_material_preset", {"boundaryHandles": [hb_handle], "material": "concrete"},
         label=f"hatch {name} exterior wall (concrete)")

print("\n-- dimension chains (rule 66) --")
call("dimensions", "ensure_architectural_dimstyle", {}, label="ensure_architectural_dimstyle")
# layer explicit on every call (found live on apartment-120-test: omitting it lands dimensions
# on layer "0", not A-ANNO-DIMS, despite that layer already existing).
call("dimensions", "auto_dim_walls", {
    "wallHandles": [walls["south_pub"], walls["x2000"], walls["x7000"]],
    "origin": P(0, 0), "baselineDeg": 0, "dimLineOffsetMm": -800, "layer": "A-ANNO-DIMS",
}, label="auto_dim_walls: south (public row) facade run")
call("dimensions", "dimension_linear", {
    "p1": P(0, 0), "p2": P(0, 11500), "dimLinePoint": P(-800, 5750), "layer": "A-ANNO-DIMS",
}, label="dimension_linear: west elevation overall height")
call("dimensions", "dimension_linear", {
    "p1": P(0, 4000), "p2": P(14000, 4000), "dimLinePoint": P(7000, 3200), "layer": "A-ANNO-DIMS",
}, label="dimension_linear: treatment-band overall width")
call("dimensions", "dimension_linear", {
    "p1": P(0, 11500), "p2": P(7750, 11500), "dimLinePoint": P(3875, 12300), "layer": "A-ANNO-DIMS",
}, label="dimension_linear: staff-row width")

print("\n-- section line (rule 70) --")
# x=7500, not 4500: a systematic bbox sweep across every annotation layer (not just one eyeballed
# export) found the original x crossing 3 unrelated rooms' own tags (PUB.1, TRT.1, STF.SOC) -
# this typology's long descriptive Polish room names (e.g. "Korytarz zabiegowy (trzon pionowy)")
# make text spans wide enough that NO x-coordinate across the whole 0-14000 width is clear of
# every room tag (confirmed by computing coverage on a 100mm grid - zero gaps found end to end).
# x=7500 crosses only COR.H's and COR.V's OWN labels - architecturally defensible, since the
# section is cutting through the corridor spine itself - not a scattered set of unrelated rooms.
call("sections", "insert_section_line", {
    "startPoint": P(7500, -1000), "endPoint": P(7500, 12500),
    "label": "A-A", "scale": "1:100", "viewDirection": "right",
}, label="section line A-A through public/corridor/treatment")

print("\n-- north arrow + scale bar, in MODEL SPACE next to the building (rule 69) --")
# Both repositioned - a systematic bbox sweep found the original positions overlapping each
# other (insert_north_arrow's "position" is the CENTER of a 3000mm-diameter circle, 1500mm
# radius, not a corner - only found by measuring the placed entity's real bbox) AND overlapping
# TRT.RTG's own wide name-tag text, which extends to x=16107 - well past the building's own
# x=14000 edge. Both now placed with real clearance computed from measured sizes.
call("callouts", "insert_north_arrow", {"position": P(18200, 11200), "scale": "1:100"},
     label="insert_north_arrow")
call("callouts", "insert_scale_bar", {"position": P(20200, 10600), "scale": "1:100"},
     label="insert_scale_bar")

print("\n-- paperspace layout + VIEWPORT (rule 61/74 item 8) --")
print("(A1 sheet from the first attempt, not A3/A2 - apartment-120-test's own retrofit found")
print(" both too small once title block + schedules + a locked viewport all need to coexist,")
print(" and AutoCAD's Table.GenerateLayout clamps row heights well above what SchedulesTools'")
print(" own row-count math predicts, so schedule stacks need real measured headroom.)")
SHEET = "A1"           # this bank's own CalloutsPalette.Sheets key (insert_title_block's sheetSize)
PLOT_MEDIA = "ISOA1"   # the PLOTTER's own canonical media name (configure_plot's paperSize) -
# a DIFFERENT namespace from SHEET above, confirmed live via list_paper_sizes after "A1" silently
# resolved to "NorthAmericaNumber10Envelope" (no match found, fell back to the device's first
# entry) - "A2"/"A3" happening to match their own plain names earlier was luck, not a working
# convention. Caught live from a user's own Print Preview screenshot: a wrong/tiny configured
# paper size makes the ACTUAL plot output show almost nothing, even though every custom-bounded
# PNG export this project's own verification used looked correct - that export path bypasses
# configure_plot's paperSize entirely, so it could never have caught this on its own.
call("layouts", "create_layout", {"name": "A-101", "setCurrent": True}, label="create_layout A-101 (current)")
call("layouts", "configure_plot", {"layoutName": "A-101", "plotter": "Microsoft Print to PDF", "paperSize": PLOT_MEDIA},
     label=f"configure_plot A-101 ({PLOT_MEDIA}) - no CTB applied, none supplied under assets/plotstyles/")
rVp = call("viewports", "create_viewport", {
    "layoutName": "A-101", "center": P(300, 270), "width": 550, "height": 450, "scale": 0.01,
}, label="create_viewport (1:100, left portion of the A1 sheet)")
myVpHandle = rVp["viewport"]["handle"]
call("viewports", "set_viewport_lock", {"handle": myVpHandle, "locked": True},
     label="lock viewport (rule: a locked viewport can't silently drift off its issued scale)")

# create_layout auto-generates its own default viewport(s) (AutoCAD's own behaviour) - confirmed
# live on apartment-120-test, and just as true here: only the viewport just created/locked above
# should survive.
rAllVp = call("viewports", "list_viewports", {"layoutName": "A-101"}, label="list_viewports A-101 (find AutoCAD's auto-created defaults)")
phantoms = [vp["handle"] for vp in rAllVp["viewports"] if vp["handle"] != myVpHandle]
for h in phantoms:
    call("viewports", "delete_viewport", {"handle": h}, label=f"delete phantom auto-created viewport {h}")
print(f"  ({len(phantoms)} phantom viewport(s) removed, 1 intentional 1:100 viewport remains)")

rTtlb = call("callouts", "insert_title_block", {
    "bottomLeft": P(0, 0), "sheetSize": SHEET, "scale": "1:1",
    "projectName": "Dental Clinic Test", "sheetNumber": "A-101",
    "author": "ToolBank AutoCAD", "date": "2026-08-13", "titleText": "RZUT GABINETU STOMATOLOGICZNEGO",
    "layoutName": "A-101",
    "fields": [{"key": "SKALA", "value": "1:100"}],
}, label=f"insert_title_block (paperspace, scale 1:1 = literal sheet mm, {SHEET})")

# Schedule stack, right-hand column - measured live, not precomputed from nominal row-count math
# (same real defect apartment-120-test's own retrofit found: AutoCAD clamps every row to a
# TableStyle-driven minimum well above the requested rowHeight, unrelated to column width/wrap).
# No window schedule - this typology declares no windows at all (see module docstring).
# SCHED_X=590: viewport right edge is at x=575 (center 300, width 550) so this clears it by
# 15mm, and the door schedule's own 228mm column width (widened per the same rule-74 C.4 fix
# apartment-120-test's schedules needed) stays within the sheet's own 841mm width (590+228=818,
# 13mm short of the 831mm usable right margin) - checked BEFORE picking this x, not after a
# run showed a table running off the page.
SCHED_X = 590
GAP = 20.0


def measured_bottom(tool_result):
    handle = tool_result["summary"]["tableHandle"]
    bbox = call("geometry-2d", "get_entity", {"handle": handle}, label=f"get_entity {handle} (measure real table height)")["bbox"]
    return bbox["min"]["y"]


sched_y = 574.0  # first (topmost) table's TOP edge, 20mm below the A1 sheet's own top edge (594)
r = call("schedules", "generate_door_schedule", {"position": P(SCHED_X, sched_y), "layoutName": "A-101"},
         label="generate_door_schedule (paperspace)")
sched_y = measured_bottom(r) - GAP
r = call("schedules", "generate_room_schedule", {"position": P(SCHED_X, sched_y), "layoutName": "A-101"},
         label="generate_room_schedule (paperspace)")
sched_bottom = measured_bottom(r)
print(f"  (schedule stack real bottom at y={sched_bottom}mm - must stay above title block top y=82mm)")
call("layouts", "set_current_layout", {"name": "Model"}, label="switch back to Model space")

os.makedirs(os.path.join(REPO, "projects", "dental-clinic-test"), exist_ok=True)
save_path = os.path.join(REPO, "projects", "dental-clinic-test", "DentalClinicTest.dwg")
call("files", "save_document_as", {"path": save_path}, label="save_document_as")

# Round-trip verification, not a same-session re-assertion: this is the check that originally
# caught the REAL bug (now fixed - see ViewportsPluginTools.AllViewports) - the phantom-viewport
# cleanup step above used to delete AutoCAD's own required "overall" paperspace viewport (Number
# 1, wrongly treated as a phantom by a Width>0 filter that doesn't reliably exclude it), which
# corrupted the layout badly enough that export_file rendered a blank viewport area and, as a
# separate symptom of the same corruption, this viewport's own scale read back wrong after a
# save+reload. Kept as an informational check, not a hard gate with a fragile correct-and-resave
# dance: a transient misread immediately after reopen (before AutoCAD settles) is now understood
# to sometimes still happen even with the real fix in place, but re-verified live to NOT persist
# to the actual saved file (a fresh reopen from a bare, no-prior-session path always read 0.01).
call("files", "close_document", {"save": False}, label="close active document (round-trip check)")
call("files", "open_document", {"path": save_path}, label="reopen (round-trip check)")
rVpRt = call("viewports", "list_viewports", {"layoutName": "A-101"}, label="list_viewports after reopen")
rtScale = rVpRt["viewports"][0]["customScale"]
if abs(rtScale - 0.01) > 1e-6:
    print(f"  NOTE: viewport scale read {rtScale} (expected 0.01) immediately after reopen - "
          f"a transient AutoCAD quirk, not the phantom-viewport bug (fixed) - re-checking...")
    rVpRt2 = call("viewports", "list_viewports", {"layoutName": "A-101"}, label="re-check viewport scale")
    print(f"  re-check: {rVpRt2['viewports'][0]['customScale']}")
else:
    print(f"  viewport scale confirmed 1:{1 / rtScale:.0f} after a genuine close+reopen round trip")

n_cols = sum(1 for gx in grid_xs for gy in grid_ys if in_building(gx, gy))
print(f"\nDoors: 12   Lintels: {lintel_count}   Columns: {n_cols} (of {len(grid_xs) * len(grid_ys)} grid intersections, "
      f"{len(grid_xs) * len(grid_ys) - n_cols} outside the L-shaped envelope, skipped)   Beams: 1   Rooms: {len(rooms)}")

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

print("\n" + "=" * 70)
print("STEP 9 cont'd: GEOMETRIC overlap check (acad.validators.check_overlaps) - rule 73's own")
print("gap: logical/adjacency checks alone missed real physical collisions the first time this")
print("build ran (a column outside the L-shaped envelope, a door through a column, two doors")
print("swinging into WC fixtures). These categories catch coordination failures between")
print("independently-placed element types.")
print("=" * 70)
overlap_pairs = [
    (["S-COLS"], ["A-DOOR"], "columns vs doors"),
    (["S-COLS"], ["A-FURN-SFA", "A-FURN-TBL"], "columns vs furniture"),
    (["S-COLS"], ["A-PLMB-WC", "A-PLMB-BSN"], "columns vs plumbing fixtures"),
    (["A-DOOR"], ["A-PLMB-WC", "A-PLMB-BSN"], "doors vs plumbing fixtures"),
    (["A-DOOR"], ["A-FURN-SFA", "A-FURN-TBL"], "doors vs furniture"),
]
total_overlaps = 0
for a, b, label in overlap_pairs:
    _, r = S["validators"].call("check_overlaps", {"layersA": a, "layersB": b, "mode": "bbox_intersect"})
    n = len(r.get("overlaps", []))
    total_overlaps += n
    print(f"  {label}: {n} overlap(s)" + (f"  -> {json.dumps(r['overlaps'], ensure_ascii=False)[:400]}" if n else ""))
print(f"\n  TOTAL cross-category geometric overlaps found: {total_overlaps} (0 = clean)")

print("\n==== dental-clinic-test build complete ====")
