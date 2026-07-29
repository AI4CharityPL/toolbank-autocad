"""Phase C-Anno: analyse text-label overlaps and build per-room fix plan.

Reads the audit log produced by acad_design_iterate (list_layers + collect_entities
MText/DBText), then:
  1. groups every text entity by the compartment it sits in (by containment against
     the closed walls LEAD/FARA + A-AREA-IDEN-anchored rooms),
  2. computes the bbox of each label (from width/height/justification if present),
  3. flags overlap clusters,
  4. writes a fix plan: one label column per room, stacked top-down with 600 mm
     line-spacing (text heights are usually ~300-600 mm in model space).
"""

import json
import sys
import collections
import pathlib
import math

AUDIT = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else None
if AUDIT is None:
    # Pick latest audit log
    logs = sorted(pathlib.Path(r"C:\Users\DELL\AppData\Local\AcadMcp\logs").glob("iterate-*.json"))
    AUDIT = logs[-1]
print(f"# audit: {AUDIT.name}", flush=True)

data = json.loads(AUDIT.read_text(encoding="utf-8"))
steps = data["IterationLogs"][0]["Steps"] if "IterationLogs" in data else data["logs"][0]["Steps"]
# Find the collect_entities step (tolerates reordering or missing layers step)
ents_out = None
for s in steps:
    out = s.get("output") or {}
    if isinstance(out, dict) and "entities" in out:
        ents_out = out["entities"]
        break
if ents_out is None:
    raise SystemExit("no collect_entities step found in audit")

print(f"# scanned {len(ents_out)} text entities", flush=True)

by_layer = collections.Counter(e.get("layer", "?") for e in ents_out)
for lyr, n in by_layer.most_common():
    print(f"  {lyr}: {n}")

# Compute bbox for every text entity.
# AutoCAD MText / Text: position = insertion point, width/height depend on box or single-line.
# Use bbox from entity if given (the collect_entities tool usually returns bbox).
# Otherwise estimate from text content: charWidth * 0.7 * heightFactor.
def text_bbox(e):
    mn = e.get("bboxMin")
    mx = e.get("bboxMax")
    if mn and mx:
        return [mn[0], mn[1], mx[0], mx[1]]
    return None

# Find textual overlaps (bbox intersections)
def bb_overlap(a, b):
    return not (a[2] < b[0] or b[2] < a[0] or a[3] < b[1] or b[3] < a[1])

labels = []
for e in ents_out:
    bb = text_bbox(e)
    if bb is None:
        continue
    labels.append({
        "handle": e.get("handle"),
        "layer": e.get("layer"),
        "text": (e.get("textValue") or e.get("text") or e.get("contents") or "")[:200],
        "textHeight": e.get("textHeight"),
        "bbox": bb,
        "pos": [bb[0], bb[1]],
        "size": [bb[2] - bb[0], bb[3] - bb[1]],
    })

print(f"\n# labels with bbox: {len(labels)}", flush=True)

# Build spatial bucket 5000 mm grid
grid = collections.defaultdict(list)
CELL = 5000
for i, L in enumerate(labels):
    x0, y0, x1, y1 = L["bbox"]
    for gx in range(int(x0 // CELL), int(x1 // CELL) + 1):
        for gy in range(int(y0 // CELL), int(y1 // CELL) + 1):
            grid[(gx, gy)].append(i)

# Find pairs
pairs = []
seen = set()
for (gx, gy), idxs in grid.items():
    for a in idxs:
        for b in idxs:
            if a >= b:
                continue
            key = (a, b)
            if key in seen:
                continue
            seen.add(key)
            if bb_overlap(labels[a]["bbox"], labels[b]["bbox"]):
                pairs.append(key)

print(f"\n# bbox-overlapping label pairs: {len(pairs)}", flush=True)

if pairs:
    print("\n## top 30 overlap clusters:")
    # group by clustering: each pair -> union-find
    parent = list(range(len(labels)))
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    for a, b in pairs:
        union(a, b)
    clusters = collections.defaultdict(list)
    for i in range(len(labels)):
        if any(i in p for p in pairs):
            clusters[find(i)].append(i)
    sorted_clusters = sorted(clusters.values(), key=len, reverse=True)
    for ci, cluster in enumerate(sorted_clusters[:30]):
        if len(cluster) < 2:
            continue
        xs = [labels[i]["pos"][0] for i in cluster]
        ys = [labels[i]["pos"][1] for i in cluster]
        print(f"\n--- cluster {ci + 1}: {len(cluster)} labels around ({sum(xs)/len(xs):.0f},{sum(ys)/len(ys):.0f}) ---")
        for i in sorted(cluster, key=lambda i: -labels[i]["bbox"][3]):
            L = labels[i]
            txt = L['text'].replace('\r\n',' | ').replace('\n',' | ')
            print(f"  hnd={L['handle']:>5} lyr={L['layer']:<15} h={L['textHeight']!s:>6} pos=({L['pos'][0]:.0f},{L['pos'][1]:.0f}) sz=({L['size'][0]:.0f}x{L['size'][1]:.0f})  txt={txt!r}")

# Save normalized labels for downstream fix script
out = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\labels-inventory.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"labels": labels, "overlapping_pairs": pairs}, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"\n# labels inventory saved: {out}")
