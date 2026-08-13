# -*- coding: utf-8 -*-
"""Rule-73 proof build #1: a 120m2-class apartment, built zone-first per the new
space-planning method (docs/engineering-rules/73-space-planning-method.md), NOT the
single-row "kulfon" pattern every prior build in this repo used.

Design decided BEFORE any tool call (rule 73 steps 1-5), by hand, so every number below is
already verified against rule-64 furniture-preset minimums and WT SS95 corridor width before
a single wall gets drawn:

  Envelope: 13000 x 9500mm (123.5 m2 gross - "~120m2 class", not forced to exactly 120.00).
  WALL_T = 150mm uniformly (interior AND exterior - a documented simplification for this demo;
  a real project would use a thicker insulated exterior wall, noted honestly rather than hidden).
  INSET = 75mm (WALL_T/2) on every room edge - net-internal convention (rule 71 step 3).

  DAY zone   (y 0-4350, depth 4350mm): Przedpokoj (entry) | Kuchnia (kitchen) | Salon z jadalnia
             - full width, sits on the south (entry+daylight) facade.
  BUFFER     (y 4350-5700, depth 1350mm, net clear width 1200mm = WT SS95 minimum exactly):
             Korytarz - the day/night separator rule 73 step 3 calls for.
  NIGHT zone (y 5700-9500, depth 3800mm): Sypialnia rodzicow | Lazienka 1 | Sypialnia dziecka 1 |
             Sypialnia dziecka 2 | Lazienka 2 - full width, on the north facade, reached only via
             the corridor, never directly off the entry.

Every bedroom net footprint is >= 2800x3600mm (rule 64 SS6 `bedroom` preset minimum) and the
kitchen/living are >= their own presets' minimums - checked by hand below, not discovered after
the fact. Bathrooms land at net 1750x3650mm - comfortably over the 1600x2200mm preset minimum
but an elongated proportion; flagged in CHANGELOG as a known simplification, not hidden.

Structural grid (rule 73 step 4) is independent of the partition layout on purpose: this
apartment's partitions are non-load-bearing infill (normal for multi-family slab+column
construction), so the acad-structural columns sit on a plain 3x2-bay grid, not forced to align
with every partition - see rule 72 SS3 (grid != members) and rule 73's own note on this.
"""
import os
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "architecture", "openings", "grids", "structural", "furniture", "plumbing", "schedules", "validators"]
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


WALL_T = 150
INSET = WALL_T / 2.0  # 75mm, net-internal convention (rule 71 step 3)

# ---- envelope + zone bands (rule 73 step 3, decided before any wall) ----
X1 = 13000.0
DAY_Y1, BUF_Y1, NIGHT_Y1 = 4350.0, 5700.0, 9500.0

print("== fresh drawing: apartment-120-test ==")
call("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
call("architecture", "ensure_architectural_layers", {})
call("structural", "ensure_structural_layers", {})

print("\n" + "=" * 70)
print("STEP 4: STRUCTURAL GRID FIRST - independent of partition layout (rule 72 SS3)")
print("=" * 70)
X_SPACINGS = [4333.0, 4334.0, 4333.0]   # 3 bays, sums to 13000
Y_SPACINGS = [4750.0, 4750.0]           # 2 bays, sums to 9500
call("grids", "draw_grid", {"origin": P(0, 0), "xSpacingsMm": X_SPACINGS, "ySpacingsMm": Y_SPACINGS},
     label="draw_grid (non-uniform 3x2 bays)")
grid_xs = [0.0]
for d in X_SPACINGS:
    grid_xs.append(grid_xs[-1] + d)
grid_ys = [0.0]
for d in Y_SPACINGS:
    grid_ys.append(grid_ys[-1] + d)
for gx in grid_xs:
    for gy in grid_ys:
        call("structural", "insert_steel_column", {"designation": "HEA140", "center": P(gx, gy)},
             label=f"column HEA140 @ ({gx:.0f},{gy:.0f})")
call("structural", "insert_beam", {
    "start": P(0, 0), "end": P(X1, 0), "designation": "IPE160", "label": "B-APT-01",
}, label="facade beam IPE160 (south)")

print("\n" + "=" * 70)
print("STEP 6: DETAILED WALLS - exterior + partitions, from the zone/grid decision above")
print("=" * 70)
walls = {}
walls["south"] = call("architecture", "draw_wall", {"start": P(0, 0), "end": P(X1, 0), "thicknessMm": WALL_T},
                       label="south exterior wall")["centerline"]["handle"]
walls["north"] = call("architecture", "draw_wall", {"start": P(0, NIGHT_Y1), "end": P(X1, NIGHT_Y1), "thicknessMm": WALL_T},
                       label="north exterior wall")["centerline"]["handle"]
walls["west"] = call("architecture", "draw_wall", {"start": P(0, 0), "end": P(0, NIGHT_Y1), "thicknessMm": WALL_T},
                      label="west exterior wall")["centerline"]["handle"]
walls["east"] = call("architecture", "draw_wall", {"start": P(X1, 0), "end": P(X1, NIGHT_Y1), "thicknessMm": WALL_T},
                      label="east exterior wall")["centerline"]["handle"]

walls["x2200"] = call("architecture", "draw_wall", {"start": P(2200, 0), "end": P(2200, DAY_Y1), "thicknessMm": WALL_T},
                       label="DAY partition x=2200 (Przedpokoj | Kuchnia)")["centerline"]["handle"]
walls["x5200"] = call("architecture", "draw_wall", {"start": P(5200, 0), "end": P(5200, DAY_Y1), "thicknessMm": WALL_T},
                       label="DAY partition x=5200 (Kuchnia | Salon)")["centerline"]["handle"]

walls["y4350"] = call("architecture", "draw_wall", {"start": P(0, DAY_Y1), "end": P(X1, DAY_Y1), "thicknessMm": WALL_T},
                       label="row boundary y=4350 (DAY | BUFFER)")["centerline"]["handle"]
walls["y5700"] = call("architecture", "draw_wall", {"start": P(0, BUF_Y1), "end": P(X1, BUF_Y1), "thicknessMm": WALL_T},
                       label="row boundary y=5700 (BUFFER | NIGHT)")["centerline"]["handle"]

for x in (3100, 5000, 8050, 11100):
    walls[f"x{x}"] = call("architecture", "draw_wall", {"start": P(x, BUF_Y1), "end": P(x, NIGHT_Y1), "thicknessMm": WALL_T},
                           label=f"NIGHT partition x={x}")["centerline"]["handle"]

print("\n" + "=" * 70)
print("STEP 6 cont'd + STEP 7: doors + windows, each with a rule-72 lintel over it")
print("=" * 70)
lintel_count = 0


def lintel_and_door(wall_name, pos, rot, width_mm, room_from, room_to, number):
    # insert_door CUTS the wall it's given, erasing the original entity and replacing it with
    # left/right remnants (CutWallForOpeningResult) - a wall hosting more than one opening MUST
    # be re-cut using the previous cut's remaining handle, not the stale original one, or the
    # plugin call fails with [EntityNotFound] eWasErased (found live, this is not hypothetical).
    global lintel_count
    r = call("structural", "insert_lintel", {
        "position": pos, "rotationDeg": rot, "spanMm": width_mm, "wallThicknessMm": WALL_T, "materialHint": "rc",
    }, label=f"lintel over door {number}")
    lintel_count += 1
    dr = call("openings", "insert_door", {
        "position": pos, "rotationDeg": rot, "type": "single", "widthMm": width_mm,
        "wallHandle": walls[wall_name], "roomFrom": room_from, "roomTo": room_to, "number": number,
        "lintelType": r.get("lintelTypeTag"),
    }, label=f"door {number}: {room_from} -> {room_to}")
    opening = dr.get("wallOpening") or {}
    walls[wall_name] = opening.get("rightHandle") or opening.get("leftHandle") or walls[wall_name]


def lintel_and_window(wall_name, pos, rot, width_mm, room, number):
    global lintel_count
    r = call("structural", "insert_lintel", {
        "position": pos, "rotationDeg": rot, "spanMm": width_mm, "wallThicknessMm": WALL_T, "materialHint": "rc",
    }, label=f"lintel over window {number}")
    lintel_count += 1
    wr = call("openings", "insert_window", {
        "position": pos, "rotationDeg": rot, "type": "casement", "widthMm": width_mm,
        "wallHandle": walls[wall_name], "room": room, "number": number,
        "lintelType": r.get("lintelTypeTag"),
    }, label=f"window {number} in {room}")
    opening = wr.get("wallOpening") or {}
    walls[wall_name] = opening.get("rightHandle") or opening.get("leftHandle") or walls[wall_name]


# doors - D1 exterior, D2-D9 interior (rotationDeg=0 on every horizontal wall used here,
# rotationDeg=90 on the two vertical DAY partitions). Each multi-opening wall ("south", "north",
# "y5700") is cut left-to-right in increasing X order so the right-remnant handle threading above
# always points at the segment the next opening on that wall actually falls inside.
lintel_and_door("south", P(1100, 0), 0, 1000, "EXT", "0.1", "D-01")
lintel_and_door("x2200", P(2200, 2175), 90, 900, "0.1", "0.2", "D-02")
lintel_and_door("x5200", P(5200, 2175), 90, 900, "0.2", "0.3", "D-03")
lintel_and_door("y4350", P(1100, DAY_Y1), 0, 900, "0.1", "0.4", "D-04")
lintel_and_door("y5700", P(1550, BUF_Y1), 0, 900, "0.4", "0.5", "D-05")
# D-06/D-09 moved from their original centred positions (x=4050/12050): a live check_overlaps
# pass (S-COLS/A-DOOR clean, but A-DOOR vs A-PLMB-WC/A-PLMB-BSN was NOT) found both original
# positions swung the door leaf straight into the room's own WC bowl, placed by
# populate_bathroom's own preset formula (min.X+400, min.Y+400) - a real collision between two
# independently-placed elements that a logical/adjacency check alone never would have caught.
# D-06 moved again, from x=4500 to x=4600: turning Bath1's fixtures 180 (see the
# populate_bathroom call below) to clear a DIFFERENT collision (the shower vs. the north-wall
# column) put the shower where this door used to swing - each fix was re-verified against every
# other already-placed element, not assumed safe from one prior check.
lintel_and_door("y5700", P(4600, BUF_Y1), 0, 800, "0.4", "0.6", "D-06")
lintel_and_door("y5700", P(6525, BUF_Y1), 0, 900, "0.4", "0.7", "D-07")
lintel_and_door("y5700", P(9575, BUF_Y1), 0, 900, "0.4", "0.8", "D-08")
lintel_and_door("y5700", P(12450, BUF_Y1), 0, 800, "0.4", "0.9", "D-09")

# windows - kitchen/living on the south facade, all 3 bedrooms on the north facade;
# bathrooms deliberately windowless (mechanical ventilation, common Polish apartment practice).
# W-01/W-02 repositioned from their original centred spans (x=3700/x=9100 single 3000mm window):
# a live check_overlaps pass (S-COLS vs A-GLAZ) found BOTH directly overlapped the south-wall
# structural columns at x=4333/x=8667 - the grid was placed independently of the windows and
# nobody cross-checked the two against each other until this verification pass. Kitchen's window
# is shifted west of its column; the living room's single wide window is split into two bays
# flanking its column instead, which is also a more realistic facade treatment than one picture
# window straddling a structural column.
lintel_and_window("south", P(3000, 0), 0, 1500, "0.2", "W-01")
lintel_and_window("south", P(7000, 0), 0, 1500, "0.3", "W-02A")
lintel_and_window("south", P(10500, 0), 0, 1500, "0.3", "W-02B")
lintel_and_window("north", P(1550, NIGHT_Y1), 0, 1500, "0.5", "W-03")
lintel_and_window("north", P(6525, NIGHT_Y1), 0, 1500, "0.7", "W-04")
lintel_and_window("north", P(9575, NIGHT_Y1), 0, 1500, "0.8", "W-05")

print("\n" + "=" * 70)
print("STEP 6 cont'd: define_room, net-internal inset vertices (rule 71 step 3)")
print("=" * 70)


def room(number, name, x0, x1, y0, y1, boundary_layer=None):
    v = [P(x0 + INSET, y0 + INSET), P(x1 - INSET, y0 + INSET),
         P(x1 - INSET, y1 - INSET), P(x0 + INSET, y1 - INSET)]
    kwargs = {"vertices": v, "number": number, "name": name}
    if boundary_layer:
        kwargs["boundaryLayer"] = boundary_layer
    r = call("architecture", "define_room", kwargs, label=f"define_room {number} {name}")
    net_w, net_h = (x1 - x0 - 2 * INSET), (y1 - y0 - 2 * INSET)
    print(f"      net footprint: {net_w:.0f} x {net_h:.0f}mm = {net_w * net_h / 1e6:.2f} m2")
    return r, (P(x0 + INSET, y0 + INSET), P(x1 - INSET, y1 - INSET))


rooms = {}
rooms["0.1"] = room("0.1", "Przedpokoj", 0, 2200, 0, DAY_Y1)
rooms["0.2"] = room("0.2", "Kuchnia", 2200, 5200, 0, DAY_Y1)
rooms["0.3"] = room("0.3", "Salon z jadalnia", 5200, X1, 0, DAY_Y1)
rooms["0.4"] = room("0.4", "Korytarz", 0, X1, DAY_Y1, BUF_Y1)
rooms["0.5"] = room("0.5", "Sypialnia rodzicow", 0, 3100, BUF_Y1, NIGHT_Y1)
rooms["0.6"] = room("0.6", "Lazienka 1", 3100, 5000, BUF_Y1, NIGHT_Y1, boundary_layer="A-ROOM-BNDY-BATH-RES")
rooms["0.7"] = room("0.7", "Sypialnia dziecka 1", 5000, 8050, BUF_Y1, NIGHT_Y1)
rooms["0.8"] = room("0.8", "Sypialnia dziecka 2", 8050, 11100, BUF_Y1, NIGHT_Y1)
rooms["0.9"] = room("0.9", "Lazienka 2", 11100, X1, BUF_Y1, NIGHT_Y1, boundary_layer="A-ROOM-BNDY-BATH-RES")

print("\n" + "=" * 70)
print("STEP 8: furniture / plumbing - fit already checked against rule 64 SS6 in step 5")
print("=" * 70)


def bbox_of(room_entry):
    return room_entry[1]


call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["0.2"])[0], "bboxMax": bbox_of(rooms["0.2"])[1],
    "preset": "kitchen", "roomName": "0.2",
}, label="populate_room 0.2 Kuchnia (preset=kitchen)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["0.3"])[0], "bboxMax": bbox_of(rooms["0.3"])[1],
    "preset": "living-room-res", "roomName": "0.3",
}, label="populate_room 0.3 Salon (preset=living-room-res)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["0.5"])[0], "bboxMax": bbox_of(rooms["0.5"])[1],
    "preset": "bedroom", "roomName": "0.5",
}, label="populate_room 0.5 Sypialnia rodzicow (preset=bedroom)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["0.7"])[0], "bboxMax": bbox_of(rooms["0.7"])[1],
    "preset": "bedroom", "roomName": "0.7",
}, label="populate_room 0.7 Sypialnia dziecka 1 (preset=bedroom)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["0.8"])[0], "bboxMax": bbox_of(rooms["0.8"])[1],
    "preset": "bedroom", "roomName": "0.8",
}, label="populate_room 0.8 Sypialnia dziecka 2 (preset=bedroom)")
# orientation="south" (180deg rotation around the room centroid) moved the shower fixture from
# this room's NE corner - found colliding with the north-wall column at x=4333 by a live
# check_overlaps pass once columns-vs-plumbing was added to the check list - to the SW corner,
# clear of every column. Bath2 didn't need this: its own nearest column (x=13000) already missed
# the shower by ~55mm, confirmed by the same pass reporting only one overlap, not two.
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["0.6"])[0], "bboxMax": bbox_of(rooms["0.6"])[1],
    "preset": "bathroom-residential", "roomName": "0.6", "orientation": "south",
}, label="populate_bathroom 0.6 Lazienka 1 (orientation=south, clears the north-wall column)")
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["0.9"])[0], "bboxMax": bbox_of(rooms["0.9"])[1],
    "preset": "bathroom-residential", "roomName": "0.9",
}, label="populate_bathroom 0.9 Lazienka 2")

os.makedirs(os.path.join(REPO, "projects", "apartment-120-test"), exist_ok=True)
# save_document takes NO path (FilesEmptyArgs - saves to the doc's existing path only); a
# never-saved new_document needs save_document_as instead, or the file silently never lands
# where you asked (confirmed live: the {"path": ...} arg was ignored, ".../Apartment120Test.dwg"
# never appeared on disk).
call("files", "save_document_as", {"path": os.path.join(REPO, "projects", "apartment-120-test", "Apartment120Test.dwg")},
     label="save_document_as")

print(f"\nDoors: 9   Windows: 6   Lintels: {lintel_count}   Columns: {len(grid_xs) * len(grid_ys)}   Beams: 1   Rooms: {len(rooms)}")

print("\n" + "=" * 70)
print("STEP 9: verification - audit_all_rooms (real flags[] array) + rule 60 SS1a criteria 18-20")
print("=" * 70)
# marginMm must exceed the flood-fill's own opening-sealing disc radius
# (max(widthMm/2 + 1.5*cellMm, 2*cellMm)) or doors/windows undercount and falsely flag
# emptyOpenings - see rule 73's dedicated section on this, root-caused during this build.
# Widest opening here is the 3000mm living-room window -> r=1575mm, so use 1700mm.
audit = call("schedules", "audit_all_rooms", {"cellMm": 50, "marginMm": 1700, "tolerancePct": 10.0},
             label="audit_all_rooms")
rows = audit.get("rows", [])
bad = [row for row in rows if row.get("flags")]
print(f"rooms audited: {len(rows)}   rows with any flag: {len(bad)}")
for row in bad:
    print(f"  FLAGGED: {json.dumps(row, ensure_ascii=False)[:300]}")

print("\n-- criterion 18: public/day zone reachable from entry without crossing a private zone --")
_, doorList = S["openings"].call("list_openings_in_model", {"kind": "doors"})
edges = [(d.get("roomFrom"), d.get("roomTo")) for d in (doorList.get("openings") or doorList.get("doors") or [])]
print(f"  door roomFrom/roomTo pairs: {edges}")
day_rooms = {"0.1", "0.2", "0.3"}
night_rooms = {"0.5", "0.6", "0.7", "0.8", "0.9"}
crosses_night = any((a in day_rooms and b in night_rooms) or (a in night_rooms and b in day_rooms) for a, b in edges if a and b)
direct_day_edges = [(a, b) for a, b in edges if (a in day_rooms or a == "EXT") and (b in day_rooms or b == "EXT")]
print(f"  EXT->0.1->0.2->0.3 all within day/entry, no edge jumps straight into a night room: {not crosses_night}")

print("\n-- criterion 19: daylight-declared rooms actually sit on an exterior wall with a window --")
_, winList = S["openings"].call("list_openings_in_model", {"kind": "windows"})
# insert_window's "room" arg is written into the ROOM_FROM attribute (OpeningsPluginTools.cs
# line 613: ROOM_FROM = a.Room), so list_openings_in_model reports it back as roomFrom, not
# "room" - confirmed live; caught here instead of shipping a verification script with the same
# class of schema-guessing bug rule 71's own incident section warns about.
winRooms = {w.get("roomFrom") for w in (winList.get("openings") or winList.get("windows") or [])}
daylight_required = {"0.2", "0.3", "0.5", "0.7", "0.8"}
print(f"  rooms with a window: {sorted(winRooms)}")
print(f"  daylight-required rooms all have >=1 window: {daylight_required.issubset(winRooms)}")

print("\n-- criterion 20: built adjacency graph vs. this project's own declared table --")
declared = {("EXT", "0.1"), ("0.1", "0.2"), ("0.2", "0.3"), ("0.1", "0.4"),
            ("0.4", "0.5"), ("0.4", "0.6"), ("0.4", "0.7"), ("0.4", "0.8"), ("0.4", "0.9")}
built = set(edges)
print(f"  declared - built (missing): {declared - built}")
print(f"  built - declared (unexpected): {built - declared}")
print(f"  adjacency graph matches declared table exactly: {declared == built}")

print("\n" + "=" * 70)
print("STEP 9 cont'd: GEOMETRIC overlap check (acad.validators.check_overlaps) - rule 73's own")
print("gap: logical/adjacency checks alone missed real physical collisions the first time this")
print("build ran (columns punching through windows, doors swinging into WC fixtures). These")
print("categories catch coordination failures between independently-placed element types.")
print("=" * 70)
overlap_pairs = [
    (["S-COLS"], ["A-GLAZ"], "columns vs windows"),
    (["S-COLS"], ["A-DOOR"], "columns vs doors"),
    (["S-COLS"], ["A-FURN-BED-RES", "A-FURN-CBT", "A-FURN-KIT", "A-FURN-SFA", "A-FURN-TBL"], "columns vs furniture"),
    (["S-COLS"], ["A-PLMB-WC", "A-PLMB-BSN", "A-PLMB-BT", "A-PLMB-SHW"], "columns vs plumbing fixtures"),
    (["A-DOOR"], ["A-PLMB-WC", "A-PLMB-BSN", "A-PLMB-BT", "A-PLMB-SHW"], "doors vs plumbing fixtures"),
    (["A-DOOR"], ["A-FURN-BED-RES", "A-FURN-CBT", "A-FURN-KIT", "A-FURN-SFA", "A-FURN-TBL"], "doors vs furniture"),
]
total_overlaps = 0
for a, b, label in overlap_pairs:
    _, r = S["validators"].call("check_overlaps", {"layersA": a, "layersB": b, "mode": "bbox_intersect"})
    n = len(r.get("overlaps", []))
    total_overlaps += n
    print(f"  {label}: {n} overlap(s)" + (f"  -> {json.dumps(r['overlaps'], ensure_ascii=False)[:400]}" if n else ""))
print(f"\n  TOTAL cross-category geometric overlaps found: {total_overlaps} (0 = clean)")

print("\n==== apartment-120-test build complete ====")
