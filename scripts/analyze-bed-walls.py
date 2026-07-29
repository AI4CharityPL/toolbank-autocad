"""For each equipment crossing pair, find enclosing room bounds and compute
the smallest-magnitude translation that gets the bed fully inside a room.

Strategy:
  * Walls in A-WALL-INT / A-WALL-EXT form a grid. For a bed at (cx, cy), find the
    two vertical walls straddling cx (west wall x_W, east wall x_E) and the two
    horizontal walls straddling cy (south y_S, north y_N). That rectangle is the
    room.
  * Bed target position: centered inside the room, 600mm clearance from the
    closest wall (WT 2024 + MZ Dz.U.2019).
  * Emit (handle, from, to) records.
"""
from __future__ import annotations
import json, pathlib, collections

AUDIT = pathlib.Path(r"C:\Users\DELL\.cursor\projects\c-Users-DELL-Dev-autocad-mcp\agent-tools\4f963b47-7f85-4441-8edb-6d0aa608f388.txt")
BEDS_AUDIT = None  # we already have bed geometry in-memory? just read from bbox of overlap
OUT = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\bed-wall-fix-plan.json")

d = json.loads(AUDIT.read_text(encoding="utf-8"))
steps = d["IterationLogs"][0]["Steps"]
poly_pairs = steps[0]["output"]["overlaps"]
w_ents = steps[3]["output"]["entities"]

# Load A-AREA-IDEN room labels from the text inventory built by analyze-text-overlaps.py
_inv = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\labels-inventory.json")
_lbls = json.loads(_inv.read_text(encoding="utf-8"))["labels"]
room_labels = [l for l in _lbls if l["layer"] == "A-AREA-IDEN"]
print(f"# room labels (A-AREA-IDEN): {len(room_labels)}")

# Extract vertical and horizontal wall segments from A-WALL-INT/EXT polylines.
# A polyline is a sequence of vertices; we derive segments from vertices or
# from bbox if the polyline is axis-aligned single-segment.
wall_segments = []  # each: {dir: 'v'|'h', x/y: coord, start, end, handle}
for e in w_ents:
    if e.get("layer") not in ("A-WALL-INT", "A-WALL-EXT"):
        continue
    bb = e.get("bboxMin"), e.get("bboxMax")
    if not bb[0] or not bb[1]:
        continue
    x0, y0, _ = bb[0]
    x1, y1, _ = bb[1]
    # Straight axis-aligned single segment?
    if abs(x1 - x0) < 1e-6 and abs(y1 - y0) > 1e-6:
        wall_segments.append({"dir": "v", "x": x0, "y0": y0, "y1": y1, "handle": e["handle"], "layer": e["layer"]})
    elif abs(y1 - y0) < 1e-6 and abs(x1 - x0) > 1e-6:
        wall_segments.append({"dir": "h", "y": y0, "x0": x0, "x1": x1, "handle": e["handle"], "layer": e["layer"]})
    else:
        # multi-segment polyline (L-shape, rectangle, etc.) - split into axis-aligned
        # segments by walking vertices from the collected data
        verts = e.get("vertices") or []
        for i in range(len(verts) - 1):
            p0, p1 = verts[i], verts[i + 1]
            if abs(p0[0] - p1[0]) < 1e-6:
                y_a, y_b = sorted((p0[1], p1[1]))
                wall_segments.append({"dir": "v", "x": p0[0], "y0": y_a, "y1": y_b, "handle": e["handle"], "layer": e["layer"]})
            elif abs(p0[1] - p1[1]) < 1e-6:
                x_a, x_b = sorted((p0[0], p1[0]))
                wall_segments.append({"dir": "h", "y": p0[1], "x0": x_a, "x1": x_b, "handle": e["handle"], "layer": e["layer"]})

v_walls = [w for w in wall_segments if w["dir"] == "v"]
h_walls = [w for w in wall_segments if w["dir"] == "h"]
print(f"# vertical walls: {len(v_walls)}, horizontal walls: {len(h_walls)}")

BED_HANDLES = ["24A", "24D", "252", "253", "256", "259", "25C", "25F", "266"]
# pull bed bboxes from poly_pairs
bed_bbox = {}
for o in poly_pairs:
    if o["handleA"] in BED_HANDLES and o["handleA"] not in bed_bbox:
        bed_bbox[o["handleA"]] = o["bboxA"]

# If bed missing from overlap output, we had earlier explicit get_entity returns.
# Fallback: hard-code from earlier query (all 9 returned bbox).
HARDCODED = {
    "24A": [66500, 31200, 68700, 32200],
    "24D": [66500, 42800, 68700, 43800],
    "252": [48500, 12000, 50700, 13000],
    "253": [51000, 12000, 53200, 13000],
    "256": [66000, 21000, 68200, 22000],
    "259": [66000, 25500, 68200, 26500],
    "25C": [66800, 30200, 68400, 30400],
    "25F": [66800, 44600, 68400, 44800],
    "266": [67800, 16200, 69700, 16800],
}
bed_bbox.update({h: HARDCODED[h] for h in BED_HANDLES})

PAD = 600.0  # mm clearance from walls per WT + MZ

def find_enclosing(cx, cy, bed_w, bed_h):
    # vertical walls crossing cy
    verts = [w for w in v_walls if w["y0"] - 100 <= cy <= w["y1"] + 100]
    xs = [w["x"] for w in verts]
    xs_west = [x for x in xs if x < cx]
    xs_east = [x for x in xs if x > cx]
    # horizontal walls crossing cx
    hors = [w for w in h_walls if w["x0"] - 100 <= cx <= w["x1"] + 100]
    ys = [w["y"] for w in hors]
    ys_south = [y for y in ys if y < cy]
    ys_north = [y for y in ys if y > cy]
    # Also include walls whose axis is NEAR bed center (within bed dimension) so
    # that beds crossing walls still pick the OUTER pair:
    # Take max of west/min of east etc.
    x_W = max(xs_west) if xs_west else None
    x_E = min(xs_east) if xs_east else None
    y_S = max(ys_south) if ys_south else None
    y_N = min(ys_north) if ys_north else None
    return x_W, x_E, y_S, y_N

fix_records = []
for h in BED_HANDLES:
    bb = bed_bbox[h]
    x0, y0, x1, y1 = bb
    cx = (x0 + x1) / 2.0
    cy = (y0 + y1) / 2.0
    bed_w = x1 - x0
    bed_h = y1 - y0
    x_W, x_E, y_S, y_N = find_enclosing(cx, cy, bed_w, bed_h)

    # Resolve crossings: if wall 134 (x=68000) crosses bed, the bed may have been
    # placed straddling it. We need to decide which side is the real room.
    # Heuristic: pick the side with wider available space.
    # Candidate rooms around wall 134: (x_W, 68000) and (68000, x_E)
    # Use enclosing bounds or the crossing wall to reason:
    # If bed crosses vertical wall w.x, candidate left = (x_W, w.x), right=(w.x, x_E).
    # Pick whichever is wider than bed and preferred on right side (more standard).
    crossing_v = [w for w in v_walls if x0 < w["x"] < x1 and w["y0"] - 100 <= cy <= w["y1"] + 100]
    crossing_h = [w for w in h_walls if y0 < w["y"] < y1 and w["x0"] - 100 <= cx <= w["x1"] + 100]

    target_x_W, target_x_E = x_W, x_E
    target_y_S, target_y_N = y_S, y_N
    if crossing_v:
        w = crossing_v[0]["x"]
        left_w = (w - x_W) if x_W is not None else float("inf")
        right_w = (x_E - w) if x_E is not None else float("inf")
        viable_left = left_w >= bed_w + 2 * PAD
        viable_right = right_w >= bed_w + 2 * PAD
        # Prefer whichever side has the nearest room LABEL on the same y-stripe.
        # y-stripe = 2m band around bed center.
        y_band = 2000
        lbl_left = [L for L in room_labels
                    if L["pos"][0] < w and abs((L["pos"][1] + L["size"][1] / 2) - cy) < y_band]
        lbl_right = [L for L in room_labels
                    if L["pos"][0] > w and abs((L["pos"][1] + L["size"][1] / 2) - cy) < y_band]
        pick_right = None
        if lbl_right and not lbl_left:
            pick_right = True
        elif lbl_left and not lbl_right:
            pick_right = False
        elif lbl_right and lbl_left:
            # Choose side whose nearest label is CLOSER in x and room viable.
            d_right = min(abs(L["pos"][0] - w) for L in lbl_right)
            d_left = min(abs(L["pos"][0] - w) for L in lbl_left)
            if viable_right and viable_left:
                pick_right = d_right <= d_left
            elif viable_right:
                pick_right = True
            elif viable_left:
                pick_right = False
            else:
                pick_right = d_right <= d_left
        else:
            # no label info - fall back to width heuristic
            pick_right = viable_right and (not viable_left or right_w >= left_w)
        if pick_right:
            target_x_W = w
        else:
            target_x_E = w
    if crossing_h:
        w = crossing_h[0]["y"]
        below_h = (w - y_S) if y_S is not None else float("inf")
        above_h = (y_N - w) if y_N is not None else float("inf")
        viable_below = below_h >= bed_h + 2 * PAD
        viable_above = above_h >= bed_h + 2 * PAD
        x_band = 2500
        lbl_below = [L for L in room_labels
                     if L["pos"][1] < w and abs((L["pos"][0] + L["size"][0] / 2) - cx) < x_band]
        lbl_above = [L for L in room_labels
                     if L["pos"][1] > w and abs((L["pos"][0] + L["size"][0] / 2) - cx) < x_band]
        pick_above = None
        if lbl_above and not lbl_below:
            pick_above = True
        elif lbl_below and not lbl_above:
            pick_above = False
        elif lbl_above and lbl_below:
            d_above = min(abs(L["pos"][1] - w) for L in lbl_above)
            d_below = min(abs(L["pos"][1] - w) for L in lbl_below)
            if viable_above and viable_below:
                pick_above = d_above <= d_below
            elif viable_above:
                pick_above = True
            elif viable_below:
                pick_above = False
            else:
                pick_above = d_above <= d_below
        else:
            pick_above = viable_above and (not viable_below or above_h >= below_h)
        if pick_above:
            target_y_S = w
        else:
            target_y_N = w

    # Now compute final translation: center bed in target_x_W..target_x_E and target_y_S..target_y_N
    def fit(old_lo, old_hi, room_lo, room_hi, pad):
        size = old_hi - old_lo
        if room_lo is None or room_hi is None:
            return 0.0
        avail = room_hi - room_lo - 2 * pad
        if avail <= 0:
            # can't fit with pad - put it flush against room_lo side
            return room_lo + pad - old_lo
        # center inside room
        new_lo = (room_lo + room_hi - size) / 2.0
        return new_lo - old_lo

    dx = fit(x0, x1, target_x_W, target_x_E, PAD)
    dy = fit(y0, y1, target_y_S, target_y_N, PAD)
    fix_records.append({
        "handle": h,
        "bbox": bb,
        "cx": cx,
        "cy": cy,
        "enclosing_W": target_x_W,
        "enclosing_E": target_x_E,
        "enclosing_S": target_y_S,
        "enclosing_N": target_y_N,
        "delta": [dx, dy],
        "from": [cx, cy, 0.0],
        "to": [cx + dx, cy + dy, 0.0],
    })

# Deduplicate: headwalls 25C/25F and beds 24A/24D belong to same rooms - make
# sure the headwall uses the SAME dx as its bed (so pair stays attached).
PAIRED = {"25C": "24A", "25F": "24D"}
by_handle = {r["handle"]: r for r in fix_records}
for hw, bd in PAIRED.items():
    if hw in by_handle and bd in by_handle:
        by_handle[hw]["delta"][0] = by_handle[bd]["delta"][0]
        bb = by_handle[hw]["bbox"]
        cx = (bb[0] + bb[2]) / 2.0
        cy = (bb[1] + bb[3]) / 2.0
        by_handle[hw]["from"] = [cx, cy, 0.0]
        by_handle[hw]["to"] = [cx + by_handle[hw]["delta"][0], cy + by_handle[hw]["delta"][1], 0.0]

# Build plan
plan = {"task": "Phase C-Equip: move beds inside rooms", "plan": []}
for r in fix_records:
    print(f"  bed {r['handle']} delta=({r['delta'][0]:.0f}, {r['delta'][1]:.0f})  room W={r['enclosing_W']}..{r['enclosing_E']}, S={r['enclosing_S']}..{r['enclosing_N']}")
    if abs(r["delta"][0]) < 1 and abs(r["delta"][1]) < 1:
        continue
    plan["plan"].append({
        "category": "modify",
        "tool": "acad.modify.move",
        "args": {
            "handles": [r["handle"]],
            "from": {"x": r["from"][0], "y": r["from"][1], "z": 0},
            "to": {"x": r["to"][0], "y": r["to"][1], "z": 0},
        },
    })

plan["plan"].append({"category": "files", "tool": "acad.files.save_document_as",
                     "args": {"path": "C:\\Users\\DELL\\Dev\\autocad-mcp\\assets\\Rysunek4_AFTER_BED_FIX.dwg"}})

OUT.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"\n# plan saved: {OUT}  steps={len(plan['plan'])}")
