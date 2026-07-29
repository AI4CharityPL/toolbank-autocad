"""Classify door-door bbox overlaps: (a) leaf+arc of SAME door, (b) double-leaf pair, (c) conflicting swings."""
import json, pathlib, sys, collections

AUDIT = pathlib.Path(sys.argv[1])
d = json.loads(AUDIT.read_text(encoding="utf-8"))
pairs = d["IterationLogs"][0]["Steps"][1]["output"]["overlaps"]
print(f"# door-door bbox pairs: {len(pairs)}")

arc_arc = []; arc_line = []; line_line = []
for p in pairs:
    a, b = p["dxfTypeA"], p["dxfTypeB"]
    if a == "Arc" and b == "Arc":
        arc_arc.append(p)
    elif a == "Line" and b == "Line":
        line_line.append(p)
    else:
        arc_line.append(p)

print(f"  arc-arc: {len(arc_arc)}")
print(f"  arc-line: {len(arc_line)}")
print(f"  line-line: {len(line_line)}")

print("\n# ARC-ARC pairs (usually double-door leaves or adjacent singles):")
for p in arc_arc:
    bbA = p["bboxA"]; bbB = p["bboxB"]
    # overlap region
    ox = max(bbA[0], bbB[0]); oy = max(bbA[1], bbB[1])
    ox2 = min(bbA[2], bbB[2]); oy2 = min(bbA[3], bbB[3])
    area_pct = ((ox2-ox)*(oy2-oy)) / ((bbA[2]-bbA[0])*(bbA[3]-bbA[1]))
    print(f"  {p['handleA']} x {p['handleB']}: A={bbA}, B={bbB}, overlap={area_pct:.0%}")

print("\n# ARC-LINE (likely leaf+arc of SAME door if centers match):")
for p in arc_line:
    print(f"  {p['handleA']} x {p['handleB']}: bboxA={p['bboxA']} bboxB={p['bboxB']}")

print("\n# LINE-LINE pairs:")
for p in line_line:
    print(f"  {p['handleA']} x {p['handleB']}: bboxA={p['bboxA']} bboxB={p['bboxB']}")
