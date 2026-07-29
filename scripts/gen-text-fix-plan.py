"""Generate an acad_design_iterate plan that clears text overlaps.

Rules:
 * In every overlapping pair we prefer moving the A-ANNO-NOTE (supplementary
   equipment/door callout) away from the A-AREA-IDEN / A-ANNO-TEXT primary
   label. Room codes and strefa labels are load-bearing; notes are supplementary.
 * For pairs that are duplicate tiny (textHeight < 3) labels we ERASE the tiny
   one instead of moving it (it was invisible anyway).
 * For cluster 6 (B-602) the note is a redundant pictographic callout - the bed
   and headwall are already drawn as entities in the drawing. The user called
   this out explicitly as unreadable. We erase it.
 * For each move we use the smallest-magnitude axis-aligned translation that
   pushes the note fully clear of the label plus a 400mm pad.
"""
from __future__ import annotations

import json
import pathlib

INVENTORY = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\labels-inventory.json")
OUT = pathlib.Path(r"C:\Users\DELL\Dev\autocad-mcp\assets\review-2026-04-23\text-fix-plan.json")

PAD = 400.0  # mm clearance

data = json.loads(INVENTORY.read_text(encoding="utf-8"))
labels = data["labels"]
pairs = data["overlapping_pairs"]

# Explicit overrides (erase because redundant / invisible / pictographic)
ERASE_HANDLES = {
    "13D",  # PATIO duplicate at textHeight 2.5 mm - invisible
    "262",  # B-602 redundant equipment callout - reported unreadable
    "4E2",  # SOR AUTO-DOOR note glued on top of B-102 label
    "280",  # LAB POC bench callout duplicating equipment tag
    "242",  # SKYLIGHT note overlapping B-510 corridor title
    "27D",  # NURSES STATION note overlapping KORYTARZ ODDZIAŁOWY
    "4A4",  # DRZWI PPOZ callout overlapping corridor label
}

LAYER_PRIORITY = {
    "S-GRID-IDEN": 0,          # axis letters/numbers - hardest reference, never move
    "A-ANNO-TTLB": 1,          # title block content
    "A-AREA-IDEN": 2,          # room code + name + area
    "A-ANNO-TEXT": 3,          # zone / corridor labels
    "A-ANNO-SYMB-EGRS": 4,     # egress markers
    "A-ANNO-SYMB": 5,
    "A-ANNO-NOTE": 9,          # supplementary callouts - first to move
}

def layer_rank(layer: str) -> int:
    return LAYER_PRIORITY.get(layer, 7)

def choose_primary(a, b):
    """Return (primary_index, secondary_index). Primary (lower rank) stays put."""
    la, lb = labels[a]["layer"], labels[b]["layer"]
    ra, rb = layer_rank(la), layer_rank(lb)
    if ra < rb:
        return a, b
    if rb < ra:
        return b, a
    sa, sb = len(labels[a]["text"]), len(labels[b]["text"])
    return (a, b) if sa >= sb else (b, a)


def smallest_delta(primary_bbox, secondary_bbox):
    px0, py0, px1, py1 = primary_bbox
    sx0, sy0, sx1, sy1 = secondary_bbox
    candidates = [
        (0.0, py1 - sy0 + PAD, "up"),        # push secondary above primary
        (0.0, py0 - sy1 - PAD, "down"),      # push below
        (px0 - sx1 - PAD, 0.0, "left"),
        (px1 - sx0 + PAD, 0.0, "right"),
    ]
    candidates.sort(key=lambda c: abs(c[0]) + abs(c[1]))
    return candidates[0]


plan_steps: list[dict] = []

# 1) Build erase batches (group by MCP call)
erases = [lbl for lbl in labels if lbl["handle"] in ERASE_HANDLES]
if erases:
    plan_steps.append({
        "category": "modify",
        "tool": "acad.modify.erase",
        "args": {"handles": [l["handle"] for l in erases]},
    })

# 2) Build moves for remaining pairs where neither handle was erased
handled_pairs = []
for (a, b) in pairs:
    ha, hb = labels[a]["handle"], labels[b]["handle"]
    if ha in ERASE_HANDLES or hb in ERASE_HANDLES:
        continue
    primary, secondary = choose_primary(a, b)
    dx, dy, direction = smallest_delta(labels[primary]["bbox"], labels[secondary]["bbox"])
    sx0, sy0 = labels[secondary]["pos"]
    plan_steps.append({
        "category": "modify",
        "tool": "acad.modify.move",
        "args": {
            "handles": [labels[secondary]["handle"]],
            "from": [sx0, sy0, 0.0],
            "to": [sx0 + dx, sy0 + dy, 0.0],
        },
    })
    handled_pairs.append({
        "primary": labels[primary]["handle"],
        "primary_layer": labels[primary]["layer"],
        "primary_text": labels[primary]["text"][:60],
        "secondary": labels[secondary]["handle"],
        "secondary_layer": labels[secondary]["layer"],
        "secondary_text": labels[secondary]["text"][:60],
        "delta": [dx, dy],
        "direction": direction,
    })

# 3) save current document after the move batch
plan_steps.append({
    "category": "files",
    "tool": "acad.files.save_document",
    "args": {},
})

plan = {
    "task": "Phase C-Anno: clear text overlaps",
    "plan": plan_steps,
}

OUT.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"# plan saved: {OUT}")
print(f"# erase batch: {len(erases)} handles -> {[l['handle'] for l in erases]}")
print(f"# move batch: {len(handled_pairs)} pairs")
for hp in handled_pairs:
    print(f"  move {hp['secondary']:>5} [{hp['secondary_layer']}] {hp['direction']:<5} dx={hp['delta'][0]:.0f} dy={hp['delta'][1]:.0f}  -- away from {hp['primary']:>5} [{hp['primary_layer']}]")
print("\n# erase candidates & rationale")
for lbl in erases:
    print(f"  {lbl['handle']:>5} h={lbl['textHeight']} lyr={lbl['layer']:<15} txt={lbl['text'][:80]!r}")
