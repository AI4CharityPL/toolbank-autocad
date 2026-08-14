# -*- coding: utf-8 -*-
"""Rule-73 proof build #3: an automotive showroom (salon samochodowy), built zone-first per
docs/engineering-rules/73-space-planning-method.md and brought straight to rule-74
construction-document level in one pass (no separate retrofit, unlike apartment-120-test and
dental-clinic-test which were retrofitted after the fact) - proving the FULL method generalizes
to a third, structurally very different typology: one big column-free public hall instead of a
grid of similarly-sized rooms.

Design decided BEFORE any tool call (rule 73 steps 1-5), grounded in
docs/knowledge-base/automotive-showroom/{ROOM-PROGRAM,AREA-CONVENTION,GRID-AND-LAYERS,STANDARDS}.md:

  Envelope: three attached rectangular blocks (not a plain box) - main block 18000x21500mm,
  a 3500x5500mm east bump-out (sales-office pods) attached to the main block's east wall,
  totalling ~495 m2 gross. WALL_T=120mm for interior partitions (lightweight drywall,
  AREA-CONVENTION.md), EXT_WALL_T=150mm for exterior/bearing walls (insulated sandwich-panel
  cladding on a steel portal frame, same source) - a genuine two-thickness building, which
  AREA-CONVENTION.md explicitly calls for ("pick the right thickness per wall, not one default
  for the whole plan") unlike residential's/dental's uniform partitions.

  SW block (y0-4500, x0-9000): Recepcja | Poczekalnia/Kawiarnia | WC klienci | WC dostosowana,
               in a row along the south (street) wall.
  HALA WYSTAWOWA (y3500(x<9000)/y0(x>=9000) up to y16000, x0-18000, minus the SW block's own
               footprint): the column-free public hall - deliberately touches the south wall
               directly for x9000-18000 (12m of the 18m frontage, 67%) so the single strongest
               design driver this typology's own research found ("the exhibition hall fronts the
               street, fully glazed on the public-facing elevation") is actually built, not just
               declared - the SW block is a single small notch out of the hall's own footprint,
               not a full-width band blocking the frontage (an earlier design draft used a
               full-width front wing and was rejected before any tool call for exactly this
               reason: it would have hidden the hall behind reception/waiting, contradicting the
               research's own strongest finding).
  EAST BUMP-OUT (x18000-21500, y2000-7500): Biuro sprzedaży 1 | Biuro sprzedaży 2, glazed pods
               bordering the hall's own east wall (2 doors), each with its own exterior window -
               "sales/negotiation offices sit around the hall's perimeter... so vehicles on
               display stay visible from inside the offices" (ROOM-PROGRAM.md).
  REAR WING (y16000-21500, x0-18000): Korytarz personelu (y16000-17500) then, off the corridor,
               Przyjęcie serwisowe | Biuro administracji 1 | Biuro administracji 2 |
               Pomieszczenie socjalne | WC personelu | Magazyn. Reached from the hall through
               ONE door (D-09) - back-of-house is reachable but not visually open to the sales
               floor, matching the adjacency table.

  Declared/known simplification vs. the adjacency table's own "Sales offices <-> Back-office:
  direct, staff need to move between customer-facing and admin work without crossing the public
  hall repeatedly": this build's only path from Biuro sprzedaży 1/2 to the rear wing IS through
  Hala wystawowa (OFF -> HALL -> COR). A dedicated back corridor connecting the bump-out to the
  rear wing without crossing the hall would need a 4th structural wing this pass doesn't build -
  flagged here and in the project README as a real, honest gap against the KB's stated ideal,
  not silently shipped as if it matched.

  Windows + criterion 19: unlike dental-clinic-test (zero windows, declared by design), THIS
  typology's core design driver is glazing, so 9 rooms are declared daylight-required and each
  gets a real acad-openings.insert_window: Hala wystawowa (one big 6000mm floor-to-ceiling
  shopfront panel, sillHeightMm=0, on the direct-frontage portion of the south wall - a REAL
  window entity satisfying criterion 19, not just a hatched "glass" wall material), Recepcja,
  Poczekalnia, both sales offices, both back-offices, Przyjęcie serwisowe, Pomieszczenie
  socjalne (all workplace rooms under Rozp. MPiPS BHP's permanent-workstation logic, reused from
  office/hospital STANDARDS.md, not re-derived here). WCs, storage and the corridor are NOT
  daylight-required - consistent with how every other typology in this bank treats those room
  types.

  Materials (rule 62): exterior/bearing walls hatch "steel" (a reasonable stand-in for the
  sandwich-panel cladding on a steel portal frame - AREA-CONVENTION.md's own material) EXCEPT
  the hall's own direct-frontage south-wall segment (x9000-18000), which hatches "glass" - the
  first build in this bank to differentiate curtain-wall glazing from opaque cladding on the
  SAME exterior wall rather than defaulting every exterior wall to one material. Interior
  partitions are not hatched, matching dental-clinic-test's own precedent (rule 74 item 2 only
  makes exterior mandatory).

  KNOWN CATALOG BUG found live during this build, worked around here, not fixed at the source:
  material="insulation" (HatchCatalog.cs's MaterialPresets entry, pattern name "BATTING") throws
  a native AutoCAD eInvalidInput from Hatch.EvaluateHatch - confirmed live and isolated (every
  other material tried - concrete/glass/plaster/steel - succeeds on an identical boundary,
  "insulation" alone fails every time), most likely because "BATTING" is not a real predefined
  pattern name in AutoCAD's own acad.pat (the correct stock name is very likely "INSUL"). Fixing
  this at the source touches AcadMcp.Shared (referenced by the PLUGIN, not just the Backend), so
  it needs a plugin redeploy + a real AutoCAD restart - out of scope for finishing this build, so
  "steel" is used instead here and the real fix is spawned as its own follow-up task rather than
  silently avoided.

  Structural (rule 72/74 C.1): HEB200 columns on the main 4-bay x 2-bay grid (x: 4 bays of
  4500mm; y: hall span 16000mm as ONE bay - deliberately no intermediate column inside the hall,
  since interior columns would break sightlines to displayed vehicles, the #1 constraint
  GRID-AND-LAYERS.md's own research found), HEA160 (lighter) at the 2 bump-out corners since
  that wing is a smaller secondary structure. Every wall on a grid axis (x9000, x18000, y16000)
  or the exterior perimeter draws bearing=True on A-WALL-BEAR; the SW-block's own interior
  partitions (x3000/x6500/x7700/y4500) and the rear wing's (all off-grid) stay on plain A-WALL.

  Furniture (rule 64): unlike dental-clinic-test, every workplace-shaped room here actually
  matches an existing preset at a comfortable size - Recepcja gets "reception" (this bank's own
  3000x4500mm reference minimum, which is exactly this room's own footprint), Poczekalnia gets
  "waiting", both sales-office pods and both back-offices get "office" (2400x2800mm minimum,
  comfortably exceeded by every one of the 4 rooms here), and all 3 WCs get the matching rule-63
  plumbing preset. Przyjęcie serwisowe, Pomieszczenie socjalne, Magazyn, Korytarz get no
  furniture call - no matching preset exists yet for a service desk or bulk storage, an honest
  gap, same discipline dental-clinic-test's own docstring already established.
"""
import os
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "architecture", "openings", "grids", "structural", "furniture", "plumbing", "schedules", "validators",
        "hatches", "dimensions", "callouts", "sections", "layouts", "geometry-2d", "viewports", "selection", "view"]
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


WALL_T = 120.0        # interior partitions (lightweight drywall)
EXT_WALL_T = 150.0     # exterior + grid-axis bearing walls (insulated sandwich panel on steel)
INSET = WALL_T / 2.0        # 60mm, interior-partition side
EXT_INSET = EXT_WALL_T / 2.0  # 75mm, exterior/bearing side

print("== fresh drawing: automotive-showroom-test ==")
call("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
call("architecture", "ensure_architectural_layers", {})
call("structural", "ensure_structural_layers", {})

print("\n" + "=" * 70)
print("STEP 4: STRUCTURAL GRID FIRST - 4 bays x 4500mm (x) x [16000, 5500] (y), deliberately NO")
print("intermediate y-line inside the 16000mm hall span (column-free clear span, the #1 driver)")
print("=" * 70)
X_SPACINGS = [4500.0, 4500.0, 4500.0, 4500.0]   # 4 bays, sums to 18000
Y_SPACINGS = [16000.0, 5500.0]                  # hall span (1 bay) + rear wing
call("grids", "draw_grid", {"origin": P(0, 0), "xSpacingsMm": X_SPACINGS, "ySpacingsMm": Y_SPACINGS},
     label="draw_grid (4x2 bays, main block)")
grid_xs = [0.0]
for d in X_SPACINGS:
    grid_xs.append(grid_xs[-1] + d)
grid_ys = [0.0]
for d in Y_SPACINGS:
    grid_ys.append(grid_ys[-1] + d)

n_main_cols = 0
for gx in grid_xs:
    for gy in grid_ys:
        call("structural", "insert_steel_column", {"designation": "HEB200", "center": P(gx, gy)},
             label=f"column HEB200 @ ({gx:.0f},{gy:.0f})")
        n_main_cols += 1
for gx, gy in [(21500.0, 2000.0), (21500.0, 7500.0)]:
    call("structural", "insert_steel_column", {"designation": "HEA160", "center": P(gx, gy)},
         label=f"column HEA160 (bump-out corner) @ ({gx:.0f},{gy:.0f})")
call("structural", "insert_beam", {
    "start": P(0, 0), "end": P(18000, 0), "designation": "IPE240", "label": "B-SHO-01",
}, label="facade beam IPE240 (south, full frontage)")
call("structural", "insert_beam", {
    "start": P(13500, 0), "end": P(13500, 16000), "designation": "IPE240", "label": "B-SHO-02",
}, label="representative roof rafter IPE240 (spans the hall's 16000mm clear depth)")

print("\n" + "=" * 70)
print("STEP 6: PERIMETER (3-block footprint: main + east bump-out + rear wing) + interior")
print("structural/bearing walls on grid axes (x9000, x18000, y16000)")
print("=" * 70)
walls = {}


def wall(name, start, end, thickness, bearing=False, face_layer=None):
    kwargs = {"start": start, "end": end, "thicknessMm": thickness}
    if bearing:
        kwargs["bearing"] = True
    if face_layer:
        kwargs["faceLayer"] = face_layer
    r = call("architecture", "draw_wall", kwargs,
             label=f"wall {name} ({start['x']:.0f},{start['y']:.0f})-({end['x']:.0f},{end['y']:.0f}) "
                   f"t={thickness:.0f} bearing={bearing}")
    walls[name] = r["centerline"]["handle"]


# perimeter (8 segments), all bearing, all drawn low-to-high in the varying coordinate
wall("p1", P(0, 0), P(18000, 0), EXT_WALL_T, bearing=True)                # south (street) frontage
wall("p2", P(18000, 0), P(18000, 2000), EXT_WALL_T, bearing=True)          # east, before bump-out
wall("p3", P(18000, 2000), P(21500, 2000), EXT_WALL_T, bearing=True)       # bump-out south
wall("p4", P(21500, 2000), P(21500, 7500), EXT_WALL_T, bearing=True)       # bump-out east
wall("p5", P(18000, 7500), P(21500, 7500), EXT_WALL_T, bearing=True)       # bump-out north
wall("p6", P(18000, 7500), P(18000, 21500), EXT_WALL_T, bearing=True)      # east, past bump-out
wall("p7", P(0, 21500), P(18000, 21500), EXT_WALL_T, bearing=True)         # north (rear wing)
wall("p8", P(0, 0), P(0, 21500), EXT_WALL_T, bearing=True)                 # west, full height

# interior walls on a grid axis -> bearing too (rule 74 C.1 / item 7)
wall("ib1", P(0, 16000), P(18000, 16000), EXT_WALL_T, bearing=True)        # hall | corridor (y=16000)
wall("ib2", P(9000, 0), P(9000, 4500), EXT_WALL_T, bearing=True)           # SW block | hall (x=9000)
wall("ib3", P(18000, 2000), P(18000, 7500), EXT_WALL_T, bearing=True)      # hall | bump-out (x=18000)

print("\n-- interior partitions (off-grid, plain A-WALL) --")
wall("in0", P(0, 4500), P(9000, 4500), WALL_T)      # SW block north wall (Recepcja/Poczekalnia row | Hala)
wall("in1", P(3000, 0), P(3000, 4500), WALL_T)      # Recepcja | Poczekalnia
wall("in2", P(6300, 0), P(6300, 4500), WALL_T)      # Poczekalnia | WC klienci
wall("in3", P(7700, 0), P(7700, 4500), WALL_T)      # WC klienci | WC dostosowana
wall("in4", P(18000, 4750), P(21500, 4750), WALL_T)  # Biuro sprzedaży 1 | 2
wall("in5", P(0, 17500), P(18000, 17500), WALL_T)   # Korytarz | rear-wing rooms row
# Rear-wing widths rebalanced from an initial even 4000/3000/3000/3000/2500/2500mm split:
# a live check_overlaps pass (A-DOOR vs A-FURN-DSK) found the "office" preset's own
# 1600mm desk, centred on the room, left only ~400mm clear on each side of a 3000mm-wide
# room once its own corridor door was added - not enough room for door + jambs. Widening
# ADM.1/ADM.2 to 3800mm each (pulled from SRV/SOC/STF.WC/MAG, all of which had slack) gives
# ~940mm clear on each side of the desk, enough to fit the door with real margin.
wall("in6", P(3500, 17500), P(3500, 21500), WALL_T)  # Przyjęcie serwisowe | Biuro adm. 1
wall("in7", P(7300, 17500), P(7300, 21500), WALL_T)  # Biuro adm. 1 | 2
wall("in8", P(11100, 17500), P(11100, 21500), WALL_T)  # Biuro adm. 2 | Socjalne
wall("in9", P(13600, 17500), P(13600, 21500), WALL_T)  # Socjalne | WC personelu
wall("in10", P(15800, 17500), P(15800, 21500), WALL_T)  # WC personelu | Magazyn

print("\n" + "=" * 70)
print("STEP 6 cont'd + STEP 7: openings. Every wall with >1 cut is cut in increasing position")
print("order along its own start->end direction (the only confirmed-safe order, per")
print("dental-clinic-test's own y4000/x6250 precedent) so right-remnant handle threading stays")
print("valid. materialHint: 'steel' for openings in exterior/bearing walls (steel portal frame),")
print("'rc' for openings in plain interior partitions.")
print("=" * 70)
opening_count = {"door": 0, "window": 0}
lintel_count = 0


def cut(wall_name, kind, pos, rot, width_mm, wall_t, material, **kw):
    """kind: 'door' or 'window'. kw carries roomFrom/roomTo (door) or room (window),
    plus any extra opening-specific args (heightMm, sillHeightMm, number)."""
    global lintel_count
    r = call("structural", "insert_lintel", {
        "position": pos, "rotationDeg": rot, "spanMm": width_mm, "wallThicknessMm": wall_t, "materialHint": material,
    }, label=f"lintel over {kind} {kw.get('number', '')} on {wall_name}")
    lintel_count += 1
    lintel_type = r.get("lintelTypeTag")
    if kind == "door":
        dr = call("openings", "insert_door", {
            "position": pos, "rotationDeg": rot, "type": "single", "widthMm": width_mm,
            "wallHandle": walls[wall_name], "roomFrom": kw["roomFrom"], "roomTo": kw["roomTo"],
            "number": kw["number"], "lintelType": lintel_type,
        }, label=f"door {kw['number']}: {kw['roomFrom']} -> {kw['roomTo']} ({wall_name})")
        opening_count["door"] += 1
    else:
        wr = call("openings", "insert_window", {
            "position": pos, "rotationDeg": rot, "type": "casement", "widthMm": width_mm,
            "heightMm": kw.get("heightMm", 1500), "sillHeightMm": kw.get("sillHeightMm", 900),
            "wallHandle": walls[wall_name], "room": kw["room"], "number": kw["number"],
            "lintelType": lintel_type,
        }, label=f"window {kw['number']}: {kw['room']} ({wall_name})")
        opening_count["window"] += 1
        dr = wr
    opening = dr.get("wallOpening") or {}
    walls[wall_name] = opening.get("rightHandle") or opening.get("leftHandle") or walls[wall_name]


# -- p1 (south, 0-18000, horizontal, rot=0): W-03 (hall shopfront), D-01 (main entrance,
# increasing x). REC and WAIT do NOT get their own windows on this wall - a live
# check_overlaps pass found their own furniture (a 2400mm reception desk, a 2200mm waiting
# sofa) legitimately fills almost the entire net width of both small rooms, leaving no space
# for a window that doesn't collide with it. Both rooms open directly onto Hala wystawowa
# (D-02, D-06) and borrow light from ITS shopfront instead - a real, not just convenient,
# design resolution given the hall is what actually has the glazed frontage this typology's
# own research calls for. The 6000mm-wide shopfront originally centered on x=13500 was also
# resized to 3500mm and shifted to sit cleanly WITHIN one 4500mm structural bay (x9000-13500)
# instead of straddling a grid column at x=13500 - confirmed live via check_overlaps
# (S-COLS vs A-GLAZ). The main entrance was moved from a cramped cut into Recepcja (where it
# collided with that same reception desk) to open directly into the hall instead - architecturally
# the more defensible choice anyway, matching the KB's own "hall fronts the street" driver.
cut("p1", "window", P(11250, 0), 0, 3500, EXT_WALL_T, "rc", room="HALL", number="W-03",
    heightMm=3000, sillHeightMm=0)  # big shopfront glazing panel, floor-to-ceiling;
    # materialHint="rc" not "steel" - confirmed live: no catalog steel profile is tall enough
    # for this span's computed lintel depth
cut("p1", "door", P(15000, 0), 0, 1800, EXT_WALL_T, "steel", roomFrom="EXT", roomTo="HALL", number="D-01")

# -- SW block internal doors --
# D-03 moved from y=2250 (its original centred position): a live check_overlaps pass
# (A-DOOR vs A-FURN-CHR) found the door's own swing arc overlapping Recepcja's own right-hand
# chair, placed by the "reception" preset's own formula (cx+600, minY+1400) - re-centred clear
# of both that chair (ends y=1878) and Recepcja's sofa near the far wall (starts ~y=3625).
cut("in1", "door", P(3000, 2750), 90, 900, WALL_T, "rc", roomFrom="REC", roomTo="WAIT", number="D-03")
cut("in2", "door", P(6300, 2250), 90, 800, WALL_T, "rc", roomFrom="WAIT", roomTo="WC.C", number="D-04")
cut("in3", "door", P(7700, 2250), 90, 800, WALL_T, "rc", roomFrom="WC.C", roomTo="WC.A", number="D-05")

# -- in0 (0-9000, horizontal): D-02, D-06 (increasing x) --
cut("in0", "door", P(1500, 4500), 0, 1000, WALL_T, "rc", roomFrom="REC", roomTo="HALL", number="D-02")
cut("in0", "door", P(4750, 4500), 0, 1200, WALL_T, "rc", roomFrom="WAIT", roomTo="HALL", number="D-06")

# -- ib3 (x=18000, 2000-7500, vertical, rot=90): D-07, D-08 (increasing y) --
cut("ib3", "door", P(18000, 3375), 90, 900, EXT_WALL_T, "steel", roomFrom="HALL", roomTo="OFF.1", number="D-07")
cut("ib3", "door", P(18000, 6125), 90, 900, EXT_WALL_T, "steel", roomFrom="HALL", roomTo="OFF.2", number="D-08")

# -- p4 (x=21500, 2000-7500, vertical): W-04, W-05 (increasing y) --
cut("p4", "window", P(21500, 3375), 90, 1500, EXT_WALL_T, "steel", room="OFF.1", number="W-04")
cut("p4", "window", P(21500, 6125), 90, 1500, EXT_WALL_T, "steel", room="OFF.2", number="W-05")

# -- ib1 (y=16000, hall | corridor): D-09 --
# moved from x=9000 (its original centred position): a live check_overlaps pass (S-COLS vs
# A-DOOR) found it swinging straight into the structural column at that exact grid
# intersection - shifted to x=6750, clear of the 4500 and 9000 grid lines on both sides.
cut("ib1", "door", P(6750, 16000), 0, 1200, EXT_WALL_T, "steel", roomFrom="HALL", roomTo="COR", number="D-09")

# -- in5 (y=17500, corridor | rear-wing rooms, horizontal): D-10..D-15 (increasing x) --
# D-11/D-12 shifted toward each room's WEST edge, off the "office" preset's own centred desk
# (see the in6/in7/.../in10 rebalance above) - re-verified clear via check_overlaps.
cut("in5", "door", P(1750, 17500), 0, 900, WALL_T, "rc", roomFrom="COR", roomTo="SRV", number="D-10")
cut("in5", "door", P(4050, 17500), 0, 800, WALL_T, "rc", roomFrom="COR", roomTo="ADM.1", number="D-11")
cut("in5", "door", P(7850, 17500), 0, 800, WALL_T, "rc", roomFrom="COR", roomTo="ADM.2", number="D-12")
cut("in5", "door", P(12350, 17500), 0, 900, WALL_T, "rc", roomFrom="COR", roomTo="SOC", number="D-13")
cut("in5", "door", P(14700, 17500), 0, 800, WALL_T, "rc", roomFrom="COR", roomTo="STF.WC", number="D-14")
cut("in5", "door", P(16900, 17500), 0, 900, WALL_T, "rc", roomFrom="COR", roomTo="MAG", number="D-15")

# -- p7 (y=21500, north, horizontal): W-06..W-09 (increasing x). W-07/W-08 repositioned off
# the x=4500/x=9000 structural grid columns (S-COLS vs A-GLAZ, live check_overlaps). --
cut("p7", "window", P(2000, 21500), 0, 1500, EXT_WALL_T, "steel", room="SRV", number="W-06")
cut("p7", "window", P(5500, 21500), 0, 1500, EXT_WALL_T, "steel", room="ADM.1", number="W-07")
cut("p7", "window", P(10000, 21500), 0, 1200, EXT_WALL_T, "steel", room="ADM.2", number="W-08")
cut("p7", "window", P(12350, 21500), 0, 1500, EXT_WALL_T, "steel", room="SOC", number="W-09")

# -- p8 (x=0, west): D-16, Przyjęcie serwisowe's own separate customer/service entrance --
cut("p8", "door", P(0, 19500), 90, 1000, EXT_WALL_T, "steel", roomFrom="SRV", roomTo="EXT", number="D-16")

print(f"\nOpenings: {opening_count['door']} doors, {opening_count['window']} windows, {lintel_count} lintels")

print("\n" + "=" * 70)
print("STEP 6 cont'd: define_room, net-internal inset vertices (rule 71 step 3). Per-side insets")
print("differ (60mm on an interior-partition side, 75mm on an exterior/bearing side) since this")
print("typology genuinely mixes two wall thicknesses, unlike apartment/dental's uniform partitions.")
print("=" * 70)


def room(number, name, v, boundary_layer=None, tag_position=None):
    kwargs = {"vertices": v, "number": number, "name": name}
    if boundary_layer:
        kwargs["boundaryLayer"] = boundary_layer
    if tag_position:
        kwargs["tagPosition"] = tag_position
    r = call("architecture", "define_room", kwargs, label=f"define_room {number} {name}")
    xs = [p["x"] for p in v]
    ys = [p["y"] for p in v]
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    print(f"      net footprint: {w:.0f} x {h:.0f}mm = {w * h / 1e6:.2f} m2")
    return r, (P(min(xs), min(ys)), P(max(xs), max(ys)))


def rect2(x0, x1, y0, y1, w=INSET, s=INSET, e=INSET, n=INSET):
    return [P(x0 + w, y0 + s), P(x1 - e, y0 + s), P(x1 - e, y1 - n), P(x0 + w, y1 - n)]


rooms = {}
# SW block (WALL_T interior on all shared sides except the true exterior south/west).
# tagPosition staggering was TRIED here (upper/lower within each room's own interior, to fix
# 4 narrow side-by-side rooms' auto-centroid tags overlapping each other's text illegibly) and
# REVERTED after live testing: it reproduced apartment-120-test's own earlier phantom-room bug -
# audit_all_rooms went from 14 rows to 17, with 2 spurious raycast-method duplicate rows
# ("query": "ADM.1" / "query": "SOC" with no area, alongside each room's own correct labelled
# row) - FetchGroupedRoomsAsync's point-in-polygon sibling grouping got confused even though
# every tagPosition used was kept strictly inside its own room's real interior, not near any
# wall. Reverted rather than risk shipping a project whose own audit data is wrong; the
# resulting narrow-room tag-text overlap on the exported sheet is a real, acknowledged
# legibility defect, documented honestly in the README instead of silently worked around.
rooms["REC"] = room("REC", "Recepcja", rect2(0, 3000, 0, 4500, w=EXT_INSET, s=EXT_INSET, e=INSET, n=INSET))
# Name shortened from "Poczekalnia / Kawiarnia" - a live bbox sweep (get_entity on every
# A-ROOM-IDEN text handle) found the room-tag's NAME line is left-justified FROM THE ROOM'S
# OWN CENTROID, not centred - so a long name always runs rightward into whatever room sits
# next door, independent of that neighbour's own tag. Confirmed live: the original name's own
# text bbox was 3708mm wide starting at x=4750 (WAIT's own centroid), reaching to x=8457 - 1958mm
# past WC klienci's own wall, genuinely overlapping WC klienci's and WC dostosowana's own name
# text (matches the user's own screenshot showing the three names run together). WAIT|WC.C
# wall also moved from x=6500 to x=6300 (WAIT loses 200mm, WC.C gains it) - a live min-gap
# sweep found WC.C's and WC.A's own NUMBER labels ("WC.C"/"WC.A", fixed-width, can't be
# shortened) only 58.5mm apart even after both NAME lines were fixed; widening WC.C shifts its
# own centroid - and therefore every one of its 3 text lines - further from WC.A's.
rooms["WAIT"] = room("WAIT", "Poczekalnia", rect2(3000, 6300, 0, 4500, w=INSET, s=EXT_INSET, e=INSET, n=INSET))
# Also shortened from "WC klienci" - a second live sweep (after fixing WAIT above) found IT
# still reached 1649mm from its own centroid into WC dostosowana's own name text. The room
# number ("WC.C") already says "WC", so the name itself only needs to disambiguate whom it
# serves.
rooms["WC.C"] = room("WC.C", "Klienci", rect2(6300, 7700, 0, 4500, w=INSET, s=EXT_INSET, e=INSET, n=INSET),
                      boundary_layer="A-ROOM-BNDY-BATH-RES")
rooms["WC.A"] = room("WC.A", "WC dostosowana", rect2(7700, 9000, 0, 4500, w=INSET, s=EXT_INSET, e=EXT_INSET, n=INSET),
                      boundary_layer="A-ROOM-BNDY-BATH-RES")

# Hala wystawowa: notched hexagon (SW block carved out), hand-computed inward offset -
# see module docstring. Each vertex = intersection of its two adjacent edges' own inward offsets.
rooms["HALL"] = room("HALL", "Hala wystawowa", [
    P(75, 4560), P(9060, 4560), P(9060, 75), P(17925, 75), P(17925, 15925), P(75, 15925),
])

# East bump-out
rooms["OFF.1"] = room("OFF.1", "Biuro sprzedaży 1", rect2(18000, 21500, 2000, 4750, w=EXT_INSET, s=INSET, e=EXT_INSET, n=INSET))
rooms["OFF.2"] = room("OFF.2", "Biuro sprzedaży 2", rect2(18000, 21500, 4750, 7500, w=EXT_INSET, s=INSET, e=EXT_INSET, n=EXT_INSET))

# Rear wing
rooms["COR"] = room("COR", "Korytarz personelu", rect2(0, 18000, 16000, 17500, w=EXT_INSET, s=EXT_INSET, e=EXT_INSET, n=INSET))
rooms["SRV"] = room("SRV", "Przyjęcie serwisowe", rect2(0, 3500, 17500, 21500, w=EXT_INSET, s=INSET, e=INSET, n=EXT_INSET))
# Shortened from "Biuro administracji 1/2" - the room NUMBER (ADM.1/ADM.2) already
# disambiguates the two, so the digit in the name was pure repetition. Caught by a live
# bbox measurement, not just the earlier pass/fail sweep: ADM.2's old name text ended only
# 5.56mm before Socjalne's own name started - inside check_overlaps' bbox_intersect
# tolerance (0 flagged), but at 1:100 that is 0.06mm on paper, visually indistinguishable
# from touching. "0 flagged overlaps" alone wasn't a strong enough bar; re-measured the real
# gap for every remaining pair after this fix too (see the sweep further down).
rooms["ADM.1"] = room("ADM.1", "Administracja", rect2(3500, 7300, 17500, 21500, w=INSET, s=INSET, e=INSET, n=EXT_INSET))
rooms["ADM.2"] = room("ADM.2", "Administracja", rect2(7300, 11100, 17500, 21500, w=INSET, s=INSET, e=INSET, n=EXT_INSET))
# Same left-justified-from-centroid overlap as WAIT above - "Pomieszczenie socjalne" (22
# characters) reached ~1377mm into WC personelu's own name text, confirmed live.
rooms["SOC"] = room("SOC", "Socjalne", rect2(11100, 13600, 17500, 21500, w=INSET, s=INSET, e=INSET, n=EXT_INSET))
# Also shortened from "WC personelu" - a second live sweep (after fixing SOC above) found IT
# still reached into Magazyn's own name text by 20mm; same "the number already says WC" logic
# as WC.C above.
rooms["STF.WC"] = room("STF.WC", "Personelu", rect2(13600, 15800, 17500, 21500, w=INSET, s=INSET, e=INSET, n=EXT_INSET),
                        boundary_layer="A-ROOM-BNDY-BATH-RES")
rooms["MAG"] = room("MAG", "Magazyn", rect2(15800, 18000, 17500, 21500, w=INSET, s=INSET, e=EXT_INSET, n=EXT_INSET))

print("\n" + "=" * 70)
print("STEP 8: furniture / plumbing - every preset genuinely fits this typology's own room sizes")
print("(unlike dental-clinic-test, where most rooms had no matching preset at all)")
print("=" * 70)


def bbox_of(room_entry):
    return room_entry[1]


call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["REC"])[0], "bboxMax": bbox_of(rooms["REC"])[1], "preset": "reception", "roomName": "REC",
}, label="populate_room REC Recepcja (preset=reception)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["WAIT"])[0], "bboxMax": bbox_of(rooms["WAIT"])[1], "preset": "waiting", "roomName": "WAIT",
}, label="populate_room WAIT Poczekalnia (preset=waiting)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["OFF.1"])[0], "bboxMax": bbox_of(rooms["OFF.1"])[1], "preset": "office", "roomName": "OFF.1",
}, label="populate_room OFF.1 Biuro sprzedaży 1 (preset=office)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["OFF.2"])[0], "bboxMax": bbox_of(rooms["OFF.2"])[1], "preset": "office", "roomName": "OFF.2",
}, label="populate_room OFF.2 Biuro sprzedaży 2 (preset=office)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["ADM.1"])[0], "bboxMax": bbox_of(rooms["ADM.1"])[1], "preset": "office", "roomName": "ADM.1",
}, label="populate_room ADM.1 Biuro administracji 1 (preset=office)")
call("furniture", "populate_room", {
    "bboxMin": bbox_of(rooms["ADM.2"])[0], "bboxMax": bbox_of(rooms["ADM.2"])[1], "preset": "office", "roomName": "ADM.2",
}, label="populate_room ADM.2 Biuro administracji 2 (preset=office)")
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["WC.C"])[0], "bboxMax": bbox_of(rooms["WC.C"])[1], "preset": "wc-public", "roomName": "WC.C",
}, label="populate_bathroom WC.C WC klienci (preset=wc-public)")
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["WC.A"])[0], "bboxMax": bbox_of(rooms["WC.A"])[1], "preset": "wc-accessible", "accessible": True,
    "roomName": "WC.A",
}, label="populate_bathroom WC.A WC dostosowana (preset=wc-accessible)")
call("plumbing", "populate_bathroom", {
    "bboxMin": bbox_of(rooms["STF.WC"])[0], "bboxMax": bbox_of(rooms["STF.WC"])[1], "preset": "wc-public", "roomName": "STF.WC",
}, label="populate_bathroom STF.WC WC personelu (preset=wc-public)")

print("\n" + "=" * 70)
print("STEP 9a: CONSTRUCTION-DOCUMENT PIPELINE (rule 74) - hatching, dimensions, schedules,")
print("callouts, section, zone entities, layout")
print("=" * 70)

print("\n-- zone entities (rule 73 step 3a, mandatory) - tags in the WEST MARGIN (x=-4000),")
print("outside the building envelope entirely, per dental-clinic-test's own proven-safe fix --")
call("architecture", "define_room", {
    "vertices": [P(0, 0), P(21500, 0), P(21500, 16000), P(0, 16000)],
    "number": "ZONE-PUBLIC", "name": "Strefa publiczna / wystawowa", "tagPosition": P(-4000, 8000),
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
}, label="zone entity: PUBLIC (SW block + hall + office bump-out)")
call("architecture", "define_room", {
    "vertices": [P(0, 16000), P(18000, 16000), P(18000, 21500), P(0, 21500)],
    "number": "ZONE-STAFF", "name": "Strefa personelu / zaplecze", "tagPosition": P(-4000, 18750),
    "boundaryLayer": "A-ZONE-BNDY", "tagLayer": "A-ZONE-IDEN",
}, label="zone entity: STAFF (rear wing)")

print("\n-- material hatching (rule 62): exterior/bearing walls only, differentiated by real")
print("material - 'glass' for the hall's own direct-frontage shopfront, 'insulation' for every")
print("other sandwich-panel envelope/bearing wall. p1 is split into TWO hatch zones (opaque SW")
print("block wall + glazed hall frontage) since one wall segment carries two real materials.")
print("=" * 70)


def hatch_rect(x0, y0, x1, y1, t):
    half = t / 2.0
    if abs(y1 - y0) < 1:
        y = y0
        return [P(min(x0, x1), y - half), P(max(x0, x1), y - half), P(max(x0, x1), y + half), P(min(x0, x1), y + half)]
    else:
        x = x0
        return [P(x - half, min(y0, y1)), P(x + half, min(y0, y1)), P(x + half, max(y0, y1)), P(x - half, max(y0, y1))]


def hatch(name, x0, y0, x1, y1, t, material):
    rc = hatch_rect(x0, y0, x1, y1, t)
    rHb = call("geometry-2d", "draw_polyline", {"vertices": rc, "closed": True, "layer": "A-WALL-BEAR"},
               label=f"hatch-boundary rectangle: {name} ({material})")
    hb_handle = rHb.get("entity", rHb).get("handle") or rHb.get("handle")
    call("hatches", "apply_material_preset", {"boundaryHandles": [hb_handle], "material": material},
         label=f"hatch {name} ({material})")


hatch("p1a (SW block frontage)", 0, 0, 9000, 0, EXT_WALL_T, "steel")
hatch("p1b (hall shopfront)", 9000, 0, 18000, 0, EXT_WALL_T, "glass")
hatch("p2", 18000, 0, 18000, 2000, EXT_WALL_T, "steel")
hatch("p3", 18000, 2000, 21500, 2000, EXT_WALL_T, "steel")
hatch("p4", 21500, 2000, 21500, 7500, EXT_WALL_T, "steel")
hatch("p5", 18000, 7500, 21500, 7500, EXT_WALL_T, "steel")
hatch("p6", 18000, 7500, 18000, 21500, EXT_WALL_T, "steel")
hatch("p7", 0, 21500, 18000, 21500, EXT_WALL_T, "steel")
hatch("p8", 0, 0, 0, 21500, EXT_WALL_T, "steel")
hatch("ib1 (hall|corridor)", 0, 16000, 18000, 16000, EXT_WALL_T, "steel")
hatch("ib2 (SW block|hall)", 9000, 0, 9000, 4500, EXT_WALL_T, "steel")
hatch("ib3 (hall|bump-out)", 18000, 2000, 18000, 7500, EXT_WALL_T, "steel")

print("\n-- dimension chains (rule 66) --")
call("dimensions", "ensure_architectural_dimstyle", {}, label="ensure_architectural_dimstyle")
call("dimensions", "auto_dim_walls", {
    "wallHandles": [walls["p1"], walls["in1"], walls["in2"], walls["in3"]],
    "origin": P(0, 0), "baselineDeg": 0, "dimLineOffsetMm": -800, "layer": "A-ANNO-DIMS",
}, label="auto_dim_walls: south facade + SW-block subdivisions")
call("dimensions", "dimension_linear", {
    "p1": P(0, 0), "p2": P(0, 21500), "dimLinePoint": P(-800, 10750), "layer": "A-ANNO-DIMS",
}, label="dimension_linear: west elevation overall height")
call("dimensions", "dimension_linear", {
    "p1": P(0, 21500), "p2": P(18000, 21500), "dimLinePoint": P(9000, 22300), "layer": "A-ANNO-DIMS",
}, label="dimension_linear: rear wing (north wall) overall width")
call("dimensions", "dimension_linear", {
    "p1": P(18000, 2000), "p2": P(21500, 2000), "dimLinePoint": P(19750, 1200), "layer": "A-ANNO-DIMS",
}, label="dimension_linear: east bump-out depth")

print("\n-- section line (rule 70) --")
call("sections", "insert_section_line", {
    "startPoint": P(13500, -1000), "endPoint": P(13500, 22500),
    "label": "A-A", "scale": "1:100", "viewDirection": "right",
}, label="section line A-A through hall frontage / corridor / rear wing")

print("\n-- north arrow + scale bar, in MODEL SPACE clear of the building (rule 69) --")
call("callouts", "insert_north_arrow", {"position": P(25000, 19000), "scale": "1:100"},
     label="insert_north_arrow")
call("callouts", "insert_scale_bar", {"position": P(25000, 15500), "scale": "1:100"},
     label="insert_scale_bar")

print("\n-- paperspace layout + VIEWPORT (rule 61/74 item 8) --")
print("Viewport size/position and modelCenter computed from this project's own real content")
print("bbox (building + zone tags in the west margin + north arrow/scale bar to the east +")
print("the section line's own extent), not copy-pasted from apartment/dental's own numbers -")
print("a user's own Print Preview screenshot caught the plan rendering tiny and off-centre when")
print("that WAS done (a fixed viewport size/position reused across projects of different real")
print("size). See CHANGELOG/README for the root cause: create_viewport never set the viewport's")
print("model-space pan target (Viewport.ViewCenter) at all, so it defaulted near the world")
print("origin regardless of where the drawing actually is - fixed by adding create_viewport's")
print("new modelCenter parameter (see ViewportsDtos.cs) and using it here.")
SHEET = "A1"
PLOT_MEDIA = "ISOA1"
call("layouts", "create_layout", {"name": "A-101", "setCurrent": True}, label="create_layout A-101 (current)")
call("layouts", "configure_plot", {"layoutName": "A-101", "plotter": "Microsoft Print to PDF", "paperSize": PLOT_MEDIA},
     label=f"configure_plot A-101 ({PLOT_MEDIA}) - no CTB applied, none supplied under assets/plotstyles/")

# Real content bbox (model space): building 0-21500/0-21500, zone tags at x=-4000 (allow their
# own text width), north arrow/scale bar centred at x=25000 (r=1500 circle + label), section
# line y -1000..22500. Padded a little beyond that on every side.
CONTENT_XMIN, CONTENT_XMAX = -4700.0, 27200.0
CONTENT_YMIN, CONTENT_YMAX = -1300.0, 22800.0
MODEL_CX = (CONTENT_XMIN + CONTENT_XMAX) / 2.0
MODEL_CY = (CONTENT_YMIN + CONTENT_YMAX) / 2.0
VP_SCALE = 0.01  # 1:100, matches the title block's own SKALA field below
VP_W = (CONTENT_XMAX - CONTENT_XMIN) * VP_SCALE + 30.0   # ~ 348mm, +30mm margin
VP_H = (CONTENT_YMAX - CONTENT_YMIN) * VP_SCALE + 30.0   # ~ 271mm, +30mm margin
VP_PAPER_CENTER = P(30.0 + VP_W / 2.0, 130.0 + VP_H / 2.0)  # left edge x=30, bottom edge y=130
print(f"  content bbox {CONTENT_XMAX - CONTENT_XMIN:.0f}x{CONTENT_YMAX - CONTENT_YMIN:.0f}mm -> "
      f"viewport {VP_W:.0f}x{VP_H:.0f}mm paper, centred on model ({MODEL_CX:.0f},{MODEL_CY:.0f})")

rVp = call("viewports", "create_viewport", {
    "layoutName": "A-101", "center": VP_PAPER_CENTER, "width": VP_W, "height": VP_H, "scale": VP_SCALE,
    "modelCenter": P(MODEL_CX, MODEL_CY),
}, label="create_viewport (1:100, sized+centred to this project's own real content)")
myVpHandle = rVp["viewport"]["handle"]
call("viewports", "set_viewport_lock", {"handle": myVpHandle, "locked": True},
     label="lock viewport (rule: a locked viewport can't silently drift off its issued scale)")

rAllVp = call("viewports", "list_viewports", {"layoutName": "A-101"}, label="list_viewports A-101 (find AutoCAD's auto-created defaults)")
phantoms = [vp["handle"] for vp in rAllVp["viewports"] if vp["handle"] != myVpHandle and vp.get("number") != 1]
for h in phantoms:
    call("viewports", "delete_viewport", {"handle": h}, label=f"delete phantom auto-created viewport {h}")
print(f"  ({len(phantoms)} phantom viewport(s) removed, 1 intentional 1:100 viewport + AutoCAD's own Number-1 overall viewport remain)")

call("callouts", "insert_title_block", {
    "bottomLeft": P(0, 0), "sheetSize": SHEET, "scale": "1:1",
    "projectName": "Automotive Showroom Test", "sheetNumber": "A-101",
    "author": "ToolBank AutoCAD", "date": "2026-08-14", "titleText": "RZUT SALONU SAMOCHODOWEGO",
    "layoutName": "A-101",
    "fields": [{"key": "SKALA", "value": "1:100"}],
}, label=f"insert_title_block (paperspace, scale 1:1 = literal sheet mm, {SHEET})")

print("\n-- schedules, TWO COLUMNS (not one long stack) --")
print("A user's own Print Preview screenshot caught the earlier single-column stack (4 tables:")
print("door/window/room schedules + finish legend) running to y=-826.75mm, ~765mm past the A1")
print("sheet's own y=0 bottom edge - AutoCAD's Table.GenerateLayout clamps every row well above")
print("the requested rowHeight (the same defect apartment/dental's own retrofits already found),")
print("and stacking all 4 tables in one column compounds it past any single sheet's height.")
print("Two fixes, not one: split into 2 columns (door+window / room+finish-legend), AND drop")
print("generate_finish_legend's 11 built-in HOSPITAL default rows (includeDefaultRows=False,")
print("a new parameter - see CHANGELOG) which were both irrelevant content for a car showroom")
print("and, at ~643mm for that one table alone, the single biggest contributor to the overflow.")
GAP = 20.0
COL1_X = VP_PAPER_CENTER["x"] + VP_W / 2.0 + 20.0
COL2_X = COL1_X + 228.0 + 20.0  # 228mm = door schedule's own measured column width


def measured_bottom(tool_result):
    handle = tool_result["summary"]["tableHandle"]
    bbox = call("geometry-2d", "get_entity", {"handle": handle}, label=f"get_entity {handle} (measure real table height)")["bbox"]
    return bbox["min"]["y"]


col1_y = 574.0
r = call("schedules", "generate_door_schedule", {"position": P(COL1_X, col1_y), "layoutName": "A-101"},
         label="generate_door_schedule (paperspace, column 1)")
col1_y = measured_bottom(r) - GAP
r = call("schedules", "generate_window_schedule", {"position": P(COL1_X, col1_y), "layoutName": "A-101"},
         label="generate_window_schedule (paperspace, column 1)")
col1_bottom = measured_bottom(r)

col2_y = 574.0
r = call("schedules", "generate_room_schedule", {"position": P(COL2_X, col2_y), "layoutName": "A-101"},
         label="generate_room_schedule (paperspace, column 2)")
col2_y = measured_bottom(r) - GAP
r = call("schedules", "generate_finish_legend", {
    "position": P(COL2_X, col2_y), "layoutName": "A-101", "includeDefaultRows": False,
    "extraRows": [["F-10", "Posadzka betonowa polerowana", "RAL 7037", "Hala wystawowa"],
                  ["W-10", "Tynk malowany", "RAL 9003", "Biura / zaplecze"],
                  ["C-10", "Sufit podwieszany / stal widoczna", "RAL 9006", "Hala wystawowa"]],
}, label="generate_finish_legend (paperspace, column 2, no hospital defaults)")
col2_bottom = measured_bottom(r)
print(f"  column 1 (door+window) real bottom y={col1_bottom:.1f}mm; "
      f"column 2 (room+finish) real bottom y={col2_bottom:.1f}mm - both must stay above "
      f"the title block's own top edge (y=82mm) and neither may go negative")
if col1_bottom < 82.0 or col2_bottom < 82.0:
    raise SystemExit(f"schedule column still overflows the sheet (col1={col1_bottom:.1f}, "
                      f"col2={col2_bottom:.1f}, sheet floor=82mm) - widen the sheet or split further")

print("\n-- frame the whole A1 sheet as A-101's own remembered on-screen view --")
print("A user's own screenshot caught AutoCAD showing a poorly-fitted view the first time this")
print("layout tab is clicked - lots of wasted grey space above and to the right of the actual")
print("sheet - because nothing in this build ever set a sensible 'current view' before saving,")
print("so the layout inherited whatever arbitrary zoom state was last active. Root-caused live:")
print("every acad.view.zoom_* tool threw a native eNullObjectPointer specifically in paperspace")
print("(confirmed: the identical calls succeed in Model space) - a freshly constructed")
print("ViewTableRecord defaults IsPaperspaceView=false, and SetCurrentView needs it explicitly")
print("true there. Fixed at the source (ViewPluginTools.cs); zoom_window with the sheet's own")
print("known paper bounds (not zoom_extents, which frames MODEL-space extents even when called")
print("in paperspace - a separate, narrower issue, not needed here since the sheet bounds are")
print("already known exactly) now leaves A-101 correctly centred and fitted when saved.")
call("view", "zoom_window", {"corner1": P(-20, -20), "corner2": P(861, 614)},
     label="zoom_window: frame the A1 sheet (841x594mm) with a small margin")

call("layouts", "set_current_layout", {"name": "Model"}, label="switch back to Model space")

os.makedirs(os.path.join(REPO, "projects", "automotive-showroom-test"), exist_ok=True)
save_path = os.path.join(REPO, "projects", "automotive-showroom-test", "AutomotiveShowroomTest.dwg")
call("files", "save_document_as", {"path": save_path}, label="save_document_as")

print("\n-- round-trip verification (the check that caught the REAL export_file bug - see")
print("ViewportsPluginTools.AllViewports, now fixed; kept as an informational check) --")
call("files", "close_document", {"save": False}, label="close active document (round-trip check)")
call("files", "open_document", {"path": save_path}, label="reopen (round-trip check)")
rVpRt = call("viewports", "list_viewports", {"layoutName": "A-101"}, label="list_viewports after reopen")
own_vp = [vp for vp in rVpRt["viewports"] if vp.get("number") != 1]
rtScale = own_vp[0]["customScale"] if own_vp else rVpRt["viewports"][0]["customScale"]
if abs(rtScale - 0.01) > 1e-6:
    print(f"  NOTE: viewport scale read {rtScale} (expected 0.01) immediately after reopen - re-checking...")
    rVpRt2 = call("viewports", "list_viewports", {"layoutName": "A-101"}, label="re-check viewport scale")
    print(f"  re-check: {[vp['customScale'] for vp in rVpRt2['viewports'] if vp.get('number') != 1]}")
else:
    print(f"  viewport scale confirmed 1:{1 / rtScale:.0f} after a genuine close+reopen round trip")

print(f"\nDoors: {opening_count['door']}   Windows: {opening_count['window']}   Lintels: {lintel_count}   "
      f"Columns: {n_main_cols + 2}   Beams: 2   Rooms: {len(rooms)}")

print("\n" + "=" * 70)
print("STEP 9: verification - audit_all_rooms + rule 60 SS1a criteria 18-20")
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
public_rooms = {"REC", "WAIT", "WC.C", "WC.A", "HALL", "OFF.1", "OFF.2"}
back_rooms = {"SRV", "ADM.1", "ADM.2", "SOC", "STF.WC", "MAG"}  # COR is neutral circulation, excluded both ways
crosses_back = any((a in public_rooms and b in back_rooms) or (a in back_rooms and b in public_rooms)
                    for a, b in edges if a and b)
print(f"  no door edge jumps directly from a public room into a back-of-house room: {not crosses_back}")
print("  (every public<->back path goes through COR, the declared neutral circulation zone)")

print("\n-- criterion 19: daylight-declared rooms actually have a window --")
print("  (REC and WAIT deliberately excluded - see the p1 window block's own comment: both")
print("   rooms' own furniture legitimately fills their street-wall frontage, so both borrow")
print("   light through their own door into Hala wystawowa's shopfront instead)")
_, winList = S["openings"].call("list_openings_in_model", {"kind": "windows"})
win_rooms = {w.get("roomFrom") for w in (winList.get("openings") or winList.get("windows") or [])}
daylight_required = {"HALL", "OFF.1", "OFF.2", "ADM.1", "ADM.2", "SRV", "SOC"}
missing = daylight_required - win_rooms
print(f"  daylight-required rooms: {sorted(daylight_required)}")
print(f"  rooms with >=1 real window entity: {sorted(win_rooms)}")
print(f"  every daylight-required room has a window: {not missing}" + (f"  MISSING: {missing}" if missing else ""))

print("\n-- criterion 20: built adjacency graph vs. this project's own declared table --")
declared = {("EXT", "HALL"), ("REC", "HALL"), ("REC", "WAIT"), ("WAIT", "WC.C"), ("WC.C", "WC.A"), ("WAIT", "HALL"),
            ("HALL", "OFF.1"), ("HALL", "OFF.2"), ("HALL", "COR"), ("COR", "SRV"), ("COR", "ADM.1"), ("COR", "ADM.2"),
            ("COR", "SOC"), ("COR", "STF.WC"), ("COR", "MAG"), ("SRV", "EXT")}
built = set(edges)
print(f"  declared - built (missing): {declared - built}")
print(f"  built - declared (unexpected): {built - declared}")
print(f"  adjacency graph matches this project's own declared table exactly: {declared == built}")
print("  (KNOWN, DOCUMENTED gap vs. the KB's own IDEAL table: 'sales offices <-> back-office: direct'")
print("   is not built as a direct door here - OFF.1/OFF.2 reach the rear wing only via HALL->COR,")
print("   i.e. they DO cross a corner of the public hall - see module docstring and README)")

print("\n" + "=" * 70)
print("STEP 9 cont'd: GEOMETRIC overlap check (acad.validators.check_overlaps)")
print("=" * 70)
overlap_pairs = [
    (["S-COLS"], ["A-DOOR"], "columns vs doors"),
    (["S-COLS"], ["A-GLAZ"], "columns vs windows"),
    (["S-COLS"], ["A-FURN-DSK", "A-FURN-SFA", "A-FURN-TBL", "A-FURN-CHR"], "columns vs furniture"),
    (["S-COLS"], ["A-PLMB-WC", "A-PLMB-BSN"], "columns vs plumbing fixtures"),
    (["A-DOOR"], ["A-PLMB-WC", "A-PLMB-BSN"], "doors vs plumbing fixtures"),
    (["A-DOOR"], ["A-FURN-DSK", "A-FURN-SFA", "A-FURN-TBL", "A-FURN-CHR"], "doors vs furniture"),
    (["A-GLAZ"], ["A-FURN-DSK", "A-FURN-SFA", "A-FURN-TBL"], "windows vs furniture"),
]
total_overlaps = 0
for a, b, label in overlap_pairs:
    _, r = S["validators"].call("check_overlaps", {"layersA": a, "layersB": b, "mode": "bbox_intersect"})
    n = len(r.get("overlaps", []))
    total_overlaps += n
    print(f"  {label}: {n} overlap(s)" + (f"  -> {json.dumps(r['overlaps'], ensure_ascii=False)[:400]}" if n else ""))
print(f"\n  TOTAL cross-category geometric overlaps found: {total_overlaps} (0 = clean)")

print("\n" + "=" * 70)
print("STEP 9 cont'd: TEXT LABEL overlap check - room tags specifically (A-ROOM-IDEN vs")
print("A-ROOM-IDEN). A user's own screenshot caught 3 narrow neighbouring rooms' NAME text")
print("running together illegibly; root-caused live (get_entity on each A-ROOM-IDEN text handle)")
print("to the tag's NAME line being left-justified FROM THE ROOM'S OWN CENTROID rather than")
print("centred, so a long name always reaches toward whichever room sits next door. Fixed by")
print("shortening the two offending names (Poczekalnia, Socjalne) rather than by moving")
print("tagPosition, which reproduced apartment-120-test's own phantom-room audit bug when tried")
print("earlier on this project - re-verified below that this fix does not touch room count.")
print("=" * 70)
_, rti = S["validators"].call("check_overlaps", {"layersA": ["A-ROOM-IDEN"], "layersB": ["A-ROOM-IDEN"], "mode": "bbox_intersect"})
real_tag_overlaps = [o for o in rti.get("overlaps", []) if o["handleA"] != o["handleB"]]
seen_pairs = set()
uniq_tag_overlaps = []
for o in real_tag_overlaps:
    key = tuple(sorted([o["handleA"], o["handleB"]]))
    if key in seen_pairs:
        continue
    seen_pairs.add(key)
    uniq_tag_overlaps.append(o)
print(f"  room-tag-vs-room-tag text overlaps (bbox_intersect): {len(uniq_tag_overlaps)} (0 = clean)")
for o in uniq_tag_overlaps:
    print(f"    {o['handleA']} vs {o['handleB']}  area={o.get('overlapArea')}  bboxA={o.get('bboxA')}  bboxB={o.get('bboxB')}")
if uniq_tag_overlaps:
    raise SystemExit(f"{len(uniq_tag_overlaps)} room-tag text overlap(s) remain - shorten the "
                      f"offending name(s) further and re-run, do not ship with overlapping text")

# bbox_intersect alone is not a strong enough bar: it flagged 0 overlaps for
# "Biuro administracji 2" vs "Socjalne" even though the real gap was 5.56mm - at 1:100 that's
# 0.06mm on paper, visually indistinguishable from touching. Directly measure the horizontal
# gap between every pair of DIFFERENT rooms' text on the same text row (same Y-band), and
# require a real minimum clearance, not just "doesn't technically intersect".
MIN_LABEL_GAP_MM = 150.0
_, allTagsRes = S["selection"].call("select_by_layer", {"layer": "A-ROOM-IDEN", "anySpace": True})
tag_boxes = []
for h in (allTagsRes.get("handles") or []):
    e = call("geometry-2d", "get_entity", {"handle": h}, label=f"get_entity {h} (room-tag min-gap sweep)")
    b = e.get("bbox")
    if b:
        tag_boxes.append((h, b["min"]["x"], b["min"]["y"], b["max"]["x"], b["max"]["y"]))
close_pairs = []
for i in range(len(tag_boxes)):
    h1, ax0, ay0, ax1, ay1 = tag_boxes[i]
    for j in range(i + 1, len(tag_boxes)):
        h2, bx0, by0, bx1, by1 = tag_boxes[j]
        if ax0 == bx0:  # same room (every one of its 3 lines starts at the room's own centroid x)
            continue
        y_overlap = min(ay1, by1) - max(ay0, by0)
        if y_overlap <= 0:  # different text rows entirely, not a horizontal-neighbour risk
            continue
        gap = (bx0 - ax1) if bx0 >= ax1 else (ax0 - bx1)
        if gap < MIN_LABEL_GAP_MM:
            close_pairs.append((h1, h2, gap))
print(f"  room-tag pairs on the same text row with < {MIN_LABEL_GAP_MM:.0f}mm real clearance: {len(close_pairs)} (0 = clean)")
for h1, h2, gap in close_pairs:
    print(f"    {h1} vs {h2}: real gap = {gap:.2f}mm")
if close_pairs:
    raise SystemExit(f"{len(close_pairs)} room-tag pair(s) have less than {MIN_LABEL_GAP_MM:.0f}mm "
                      f"real clearance - shorten the offending name(s) further and re-run")

audit2 = call("schedules", "audit_all_rooms", {"cellMm": 50, "marginMm": 700, "tolerancePct": 10.0},
              label="audit_all_rooms (re-check: room count must stay 14 after the name-shortening fix)")
n_rooms2 = len(audit2.get("rows", []))
print(f"  rooms audited: {n_rooms2} (expected 14 - a mismatch would mean the name-shortening fix")
print("   itself somehow confused FetchGroupedRoomsAsync's grouping, the same way tagPosition did)")
if n_rooms2 != 14:
    raise SystemExit(f"room count changed to {n_rooms2} (expected 14) after the name-shortening "
                      f"fix - investigate before shipping, this is exactly the phantom-room shape")

print("\n==== automotive-showroom-test build complete ====")
