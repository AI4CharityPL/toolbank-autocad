"""Build a room-door inventory from a snapshot audit.
For every A-AREA-IDEN label (room), determine its enclosing walls, then check
whether any A-DOOR / A-DOOR-FIRE entity's bbox is ADJACENT to any of those walls.
"""
from __future__ import annotations
import json, pathlib, sys, collections

AUDIT = pathlib.Path(sys.argv[1])
OUT = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\room-door-inventory.json")

d = json.loads(AUDIT.read_text(encoding="utf-8"))
steps = d["IterationLogs"][0]["Steps"]
ents = steps[0]["output"]["entities"]
door_wall_cross = steps[1]["output"]["overlaps"]
door_door_cross = steps[2]["output"]["overlaps"]

print(f"# entities: {len(ents)}")
by_layer = collections.Counter(e.get("layer") for e in ents)
print(f"# layers: {len(by_layer)}")

rooms = [e for e in ents if e.get("layer") == "A-AREA-IDEN" and e.get("dxfType") == "MText"]
doors_int = [e for e in ents if e.get("layer") == "A-DOOR"]
doors_fire = [e for e in ents if e.get("layer") == "A-DOOR-FIRE"]
doors_all = doors_int + doors_fire
walls = [e for e in ents if e.get("layer") in ("A-WALL-INT", "A-WALL-EXT", "A-WALL-FIRE", "A-WALL-LEAD", "A-WALL-FARA")]
print(f"# rooms: {len(rooms)}, doors (A-DOOR): {len(doors_int)}, doors-fire: {len(doors_fire)}, walls: {len(walls)}")

# Extract axis-aligned wall segments from polylines (both single-seg and multi-seg)
wall_segments = []
for e in walls:
    mn = e.get("bboxMin"); mx = e.get("bboxMax")
    if not mn or not mx:
        continue
    verts = e.get("vertices") or []
    if len(verts) < 2:
        if abs(mx[0] - mn[0]) < 1e-6:
            wall_segments.append({"dir":"v","x":mn[0],"y0":mn[1],"y1":mx[1],"handle":e["handle"],"layer":e["layer"]})
        elif abs(mx[1] - mn[1]) < 1e-6:
            wall_segments.append({"dir":"h","y":mn[1],"x0":mn[0],"x1":mx[0],"handle":e["handle"],"layer":e["layer"]})
        continue
    for i in range(len(verts)-1):
        p0, p1 = verts[i], verts[i+1]
        if abs(p0[0]-p1[0]) < 1e-6:
            y_a, y_b = sorted((p0[1], p1[1]))
            wall_segments.append({"dir":"v","x":p0[0],"y0":y_a,"y1":y_b,"handle":e["handle"],"layer":e["layer"]})
        elif abs(p0[1]-p1[1]) < 1e-6:
            x_a, x_b = sorted((p0[0], p1[0]))
            wall_segments.append({"dir":"h","y":p0[1],"x0":x_a,"x1":x_b,"handle":e["handle"],"layer":e["layer"]})

v_walls = [w for w in wall_segments if w["dir"]=="v"]
h_walls = [w for w in wall_segments if w["dir"]=="h"]
print(f"# wall segments: {len(wall_segments)} (v={len(v_walls)}, h={len(h_walls)})")

# Build door centroids
door_info = []
for d_ in doors_all:
    mn = d_.get("bboxMin"); mx = d_.get("bboxMax")
    if not mn or not mx: continue
    cx = (mn[0]+mx[0])/2; cy=(mn[1]+mx[1])/2
    w = mx[0]-mn[0]; h = mx[1]-mn[1]
    door_info.append({"handle":d_["handle"],"layer":d_["layer"],"cx":cx,"cy":cy,"w":w,"h":h,"bbox":[mn[0],mn[1],mx[0],mx[1]]})

# For each room, find enclosing box + count nearby doors
def enclose(cx, cy):
    verts = [w for w in v_walls if w["y0"]-300 <= cy <= w["y1"]+300]
    hors = [w for w in h_walls if w["x0"]-300 <= cx <= w["x1"]+300]
    xs_W = [w["x"] for w in verts if w["x"] < cx]
    xs_E = [w["x"] for w in verts if w["x"] > cx]
    ys_S = [w["y"] for w in hors if w["y"] < cy]
    ys_N = [w["y"] for w in hors if w["y"] > cy]
    return (max(xs_W) if xs_W else None,
            min(xs_E) if xs_E else None,
            max(ys_S) if ys_S else None,
            min(ys_N) if ys_N else None)

room_records = []
for r in rooms:
    mn = r.get("bboxMin"); mx = r.get("bboxMax")
    if not mn: continue
    cx=(mn[0]+mx[0])/2; cy=(mn[1]+mx[1])/2
    xW,xE,yS,yN = enclose(cx, cy)
    txt = (r.get("textValue") or "").replace("\r\n"," | ").replace("\n"," | ")
    # doors whose center lies inside/touches the enclosing rect OR near its walls
    near_doors = []
    if xW is not None and xE is not None and yS is not None and yN is not None:
        padded = (xW-1200, yS-1200, xE+1200, yN+1200)  # 1.2m outside wall allowed
        for D in door_info:
            if padded[0] <= D["cx"] <= padded[2] and padded[1] <= D["cy"] <= padded[3]:
                # Also require door to be within 600mm of SOME enclosing wall edge (not interior of room)
                near_wall = (abs(D["cx"]-xW) < 900 or abs(D["cx"]-xE) < 900 or
                             abs(D["cy"]-yS) < 900 or abs(D["cy"]-yN) < 900)
                if near_wall:
                    near_doors.append(D["handle"])
    room_records.append({
        "handle": r["handle"],
        "text": txt,
        "cx": cx, "cy": cy,
        "bbox_label":[mn[0],mn[1],mx[0],mx[1]],
        "enclose": [xW,xE,yS,yN],
        "doors_near": near_doors,
    })

# Report
no_door = [r for r in room_records if not r["doors_near"]]
one_door = [r for r in room_records if len(r["doors_near"])==1]
many_door = [r for r in room_records if len(r["doors_near"])>1]
print(f"\n# ROOM-DOOR MATRIX")
print(f"  rooms without a detected door: {len(no_door)}")
for r in no_door:
    xW,xE,yS,yN = r["enclose"]
    print(f"    - {r['text'][:70]!r} at ({r['cx']:.0f},{r['cy']:.0f}) enc={xW}..{xE} / {yS}..{yN}")
print(f"  rooms with exactly 1 door: {len(one_door)}")
print(f"  rooms with >= 2 doors: {len(many_door)}")
print(f"\n# door-wall crossings ({len(door_wall_cross)}):")
for o in door_wall_cross[:30]:
    print(f"  {o.get('handleA')} ({o.get('layerA')}) x {o.get('handleB')} ({o.get('layerB')})  bboxA={o.get('bboxA')}")
print(f"\n# door-door bbox overlaps ({len(door_door_cross)}):")
for o in door_door_cross[:15]:
    print(f"  {o.get('handleA')} x {o.get('handleB')}")

OUT.write_text(json.dumps({
    "rooms": room_records,
    "doors": door_info,
    "wall_segments": wall_segments,
    "door_wall_crossings": door_wall_cross,
    "door_door_bbox": door_door_cross,
}, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"\n# saved {OUT}")
