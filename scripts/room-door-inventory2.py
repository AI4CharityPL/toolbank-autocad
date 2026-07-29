"""Room-door inventory v2: relaxed detection + classification."""
from __future__ import annotations
import json, pathlib, sys, collections

INV = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\room-door-inventory.json")
data = json.loads(INV.read_text(encoding="utf-8"))

rooms = data["rooms"]
doors = data["doors"]
wall_segments = data["wall_segments"]
door_wall = data["door_wall_crossings"]

# Relaxed: a door belongs to a room if door bbox intersects enclosing rectangle
# expanded by 2500mm outward AND its center is within 2000mm of an enclosing wall.
def bbox_overlap(a, b):
    return not (a[2] < b[0] or b[2] < a[0] or a[3] < b[1] or b[3] < a[1])

no_door = []
one_door = []
many_door = []
for r in rooms:
    xW, xE, yS, yN = r["enclose"]
    if None in (xW, xE, yS, yN):
        # Open boundary (exterior room) - accept any door touching known sides
        pass
    # Build expanded rect (fall back to label bbox inflated 8m if no enclose)
    if None in (xW, xE, yS, yN):
        lb = r["bbox_label"]
        cx = r["cx"]; cy = r["cy"]
        er = [cx - 4000, cy - 3000, cx + 4000, cy + 3000]
    else:
        er = [xW - 1500, yS - 1500, xE + 1500, yN + 1500]
    detected = []
    for D in doors:
        # overlap test
        if bbox_overlap(D["bbox"], er):
            # Close to one of the walls
            cx2, cy2 = D["cx"], D["cy"]
            close_x = (xW is not None and abs(cx2 - xW) < 2000) or (xE is not None and abs(cx2 - xE) < 2000)
            close_y = (yS is not None and abs(cy2 - yS) < 2000) or (yN is not None and abs(cy2 - yN) < 2000)
            if close_x or close_y or None in (xW, xE, yS, yN):
                detected.append(D["handle"])
    r["doors_near_v2"] = detected
    if not detected:
        no_door.append(r)
    elif len(detected) == 1:
        one_door.append(r)
    else:
        many_door.append(r)

print(f"# rooms without a door (relaxed): {len(no_door)}")
for r in no_door:
    xW,xE,yS,yN = r["enclose"]
    print(f"  - {r['text'][:70]!r} at ({r['cx']:.0f},{r['cy']:.0f}) enc=({xW}..{xE})/({yS}..{yN})")
print(f"# rooms with 1 door: {len(one_door)}, with >=2 doors: {len(many_door)}")

# For each door-wall crossing, classify:
# - 0-width door bbox (height=0 too) => degenerate line? unlikely
# - bboxA with width OR height 0 and perpendicular to wall => T-junction (endpoint)
# - else: if bboxA both dims > 200mm and wall line passes through bbox interior => through-wall crossing
TJ = 0; ILLEGAL = 0; dw_illegal = []
for o in door_wall:
    bb = o.get("bboxA") or []
    if len(bb) != 4:
        continue
    w = bb[2] - bb[0]
    h = bb[3] - bb[1]
    # Door polylines are often drawn as a single line (w=0 or h=0)
    if w < 1 or h < 1:
        TJ += 1
    else:
        # Check if this is a likely jamb T-junction: the door leaf bbox touches
        # the wall at an EDGE. We consider this illegal only when wall line is
        # strictly interior of bbox.
        # Simpler: if bbox area > 300x300 this is definitely a door leaf
        # and if it crosses a wall line the wall passes THROUGH it.
        ILLEGAL += 1
        dw_illegal.append(o)

print(f"\n# door-wall crossings: T-junction(line doors) = {TJ}, potential through-wall = {ILLEGAL}")
for o in dw_illegal[:20]:
    print(f"  IL: A={o.get('handleA')} ({o.get('layerA')}) x B={o.get('handleB')} ({o.get('layerB')}) bboxA={o.get('bboxA')}")

out = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\room-door-v2.json")
out.write_text(json.dumps({"no_door": no_door, "door_wall_illegal": dw_illegal}, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"\n# saved {out}")
