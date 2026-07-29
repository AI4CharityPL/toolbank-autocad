"""Generate add-door plan for rooms that lack a door.
Each door = 1 Line (1100mm leaf) + 1 Arc (1100mm radius, 90 deg swing) on layer A-DOOR.
Door is placed centered on an enclosing wall chosen by heuristic.
"""
from __future__ import annotations
import json, pathlib, math

INV = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\room-door-v2.json")
FULL = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\room-door-inventory.json")

v2 = json.loads(INV.read_text(encoding="utf-8"))
full = json.loads(FULL.read_text(encoding="utf-8"))
doors = full["doors"]
wall_segments = full["wall_segments"]

no_door = v2["no_door"]

# Targeted door placements per room (knowing layout)
# For each room: (side, pos_x_or_y, along_y_or_x)
# side: 'N','S','E','W' wall of enclosing rect
DOOR_PLAN = {
    "A-001": ("N", 10000, 10000),   # corridor at y=10000
    "A-002": ("N", 10000, 20000),
    "A-201": ("S", 39400, 19000),   # corridor at y=39400
    "A-202": ("S", 39400, 29500),
    "A-203": ("S", 39400, 41000),
    "A-301": ("E", 3500, 52500),    # door to A-303 vestibule at x=3500
    "A-302": ("E", 3500, 57500),
    "A-303": ("S", 50000, 5750),    # corridor south of A-3xx at y=50000
    "A-304": ("S", 50000, 18000),
    "A-305": ("S", 50000, 38000),
    "A-401": ("E", 13000, 17000),   # corridor at x=13000
    "A-402": ("E", 13000, 25000),
    "A-403": ("E", 13000, 33000),
    "A-404": ("E", 13000, 41000),
    "B-101": ("N", 8000, 51000),    # corridor at y=8000
    "B-102": ("N", 8000, 64000),
    "B-205": ("E", 56000, 14500),   # corridor to east
    "B-201": ("W", 56500, 13000),   # open to B-205 corridor
    "B-203": ("W", 56500, 15575),
    "B-302": ("N", 28100, 54375),   # corridor to north at y=28100 is actually another room. Use south side to corridor y=20100
    "B-303": ("N", 28100, 58625),
    "B-402": ("S", 48000, 52500),   # corridor south
    "B-410": ("S", 52000, 53000),
    "B-501": ("N", 10000, 68750),   # sterile corridor at y=10000
    "B-502": ("N", 10000, 76250),
}

# Fix B-302 / B-303: corridor at y=20100 is the south wall. User intended access.
# Actually B-302 enc=(52250..56000)/(20100..28100). Its SOUTH wall at y=20100 borders the B-205 corridor.
# Let's place doors there:
DOOR_PLAN["B-302"] = ("S", 20100, 54375)
DOOR_PLAN["B-303"] = ("S", 20100, 58625)

# A-301/A-302: WCs have corridor on east (x=3500) into vestibule A-303
# A-303: corridor south at y=50000? No, A-303 borders A-304 on east at x=8000.
#   Actually A-303 (3500..8000)/(50000..59600) — south wall y=50000 faces central corridor.
# A-001: corridor on N at y=10000 ✓

# B-205 has enc y top=17100. East wall at x=56000. Use east.
# B-201 (56500..60750)/(12000..14050): sharing W wall at 56500 with B-205 (48000..56000)/(12000..17100)?
#   Actually 56500 ≠ 56000, so there's a partition. Place door on W=56500 facing corridor at x=56000-56500? Place at W.

def door_endpoints(side, line, center):
    """Return (line_start, line_end, arc_center, arc_start, arc_end) for a door.
    side: N/S/E/W wall (the wall that has the opening)
    line: the coordinate of the wall (y for N/S, x for E/W)
    center: the coordinate along the wall (x for N/S, y for E/W)
    Door leaf 1100mm perpendicular to wall, hinged toward room interior (away from corridor).
    """
    L = 1100
    if side == "N":
        # corridor to north (y > line). Door hangs DOWN into room: leaf from (center, line) to (center, line - L).
        # hinge on left jamb at (center-L/2, line). Swing into room (down-left).
        # Simpler: leaf AS a line from (x0, line) to (x0, line-L) where x0 = center + L/2
        # arc centered at (x0, line) from start (x0-L, line) swinging to (x0, line-L) = 90 deg
        x0 = center + L/2
        line_start = (x0, line)
        line_end = (x0, line - L)
        arc_center = (x0, line)
        arc_start_angle = 180  # pointing to (x0-L, line)
        arc_end_angle = 270    # pointing to (x0, line-L)
        return "V", line_start, line_end, arc_center, arc_start_angle, arc_end_angle
    if side == "S":
        # corridor to south (y < line). Door hangs UP into room.
        x0 = center + L/2
        line_start = (x0, line)
        line_end = (x0, line + L)
        arc_center = (x0, line)
        arc_start_angle = 90
        arc_end_angle = 180
        return "V", line_start, line_end, arc_center, arc_start_angle, arc_end_angle
    if side == "E":
        # corridor to east (x > line). Door hangs LEFT into room.
        y0 = center + L/2
        line_start = (line, y0)
        line_end = (line - L, y0)
        arc_center = (line, y0)
        arc_start_angle = 180
        arc_end_angle = 270
        return "H", line_start, line_end, arc_center, arc_start_angle, arc_end_angle
    if side == "W":
        # corridor to west (x < line). Door hangs RIGHT into room.
        y0 = center + L/2
        line_start = (line, y0)
        line_end = (line + L, y0)
        arc_center = (line, y0)
        arc_start_angle = 270
        arc_end_angle = 360
        return "H", line_start, line_end, arc_center, arc_start_angle, arc_end_angle
    raise ValueError(f"bad side {side}")

plan_steps = []
added_count = 0
for r in no_door:
    txt = r["text"]
    code = txt.split("|")[0].strip()
    if code not in DOOR_PLAN:
        print(f"  SKIP {code}: no plan defined")
        continue
    side, line, center = DOOR_PLAN[code]
    orient, ls, le, ac, sa, ea = door_endpoints(side, line, center)
    plan_steps.append({
        "category": "geometry2d",
        "tool": "acad.geometry2d.draw_line",
        "args": {
            "start": {"x": ls[0], "y": ls[1]},
            "end":   {"x": le[0], "y": le[1]},
            "layer": "A-DOOR",
        }
    })
    plan_steps.append({
        "category": "geometry2d",
        "tool": "acad.geometry2d.draw_arc",
        "args": {
            "center": {"x": ac[0], "y": ac[1]},
            "radius": 1100.0,
            "startAngleDeg": float(sa),
            "endAngleDeg": float(ea),
            "layer": "A-DOOR",
        }
    })
    added_count += 1
    print(f"  + door for {code}: side={side} leaf=({ls[0]:.0f},{ls[1]:.0f})->({le[0]:.0f},{le[1]:.0f}) arc@({ac[0]:.0f},{ac[1]:.0f}) {sa}->{ea}")

print(f"\n# total doors to add: {added_count}")

task = {
    "task": f"Add {added_count} missing doors (1100mm leaf + 90deg swing arc)",
    "plan": plan_steps,
}
pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\missing-doors-plan.json").write_text(
    json.dumps(task, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("# plan saved")
