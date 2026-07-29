"""Phase C-prep: from a full collect_entities router-log, build
`docs/HOSPITAL-2026-GEOMETRY-TARGET.md` — the reverse-engineered target
geometry that Phase C will rebuild from scratch.

Heuristics:
* Each wall Polyline defines a potential room (closed polyline on A-WALL-*).
* A label (MText/DBText on A-AREA-IDEN) whose insertion point lies inside
  the wall-polyline bbox is that room's name.
* Doors on A-DOOR* with bbox inside/adjacent to a wall define openings.
* The union of ALL wall bboxes gives the outer building envelope.

This does NOT fix any geometry - it only builds a human-readable target list
that the Phase C-Wall EXECUTE step will use as the rebuild plan.
"""
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path


def load_steps(path: Path) -> list:
    return json.loads(path.read_text(encoding="utf-8"))["IterationLogs"][0]["Steps"]


def entities_from(steps: list, step_idx: int) -> list:
    s = steps[step_idx]
    if not s.get("Ok"):
        return []
    return s.get("output", {}).get("entities", [])


def bbox(e: dict):
    mn, mx = e.get("bboxMin"), e.get("bboxMax")
    if not mn or not mx:
        return None
    return float(mn[0]), float(mn[1]), float(mx[0]), float(mx[1])


def anchor(t: dict):
    for k in ("insertionPoint", "position"):
        v = t.get(k)
        if isinstance(v, list) and len(v) >= 2:
            return float(v[0]), float(v[1])
    v = t.get("vertices")
    if isinstance(v, list) and v and isinstance(v[0], list) and len(v[0]) >= 2:
        return float(v[0][0]), float(v[0][1])
    return None


def is_closed(p: dict) -> bool:
    v = p.get("vertices")
    if not isinstance(v, list) or len(v) < 3:
        return False
    return p.get("isClosed", False) or (
        abs(v[0][0] - v[-1][0]) < 1.0 and abs(v[0][1] - v[-1][1]) < 1.0
    )


def area_m2(bb) -> float:
    if not bb:
        return 0.0
    return abs((bb[2] - bb[0]) * (bb[3] - bb[1])) / 1_000_000.0


def main(log_path: str, out_md: str) -> int:
    steps = load_steps(Path(log_path))
    walls = entities_from(steps, 0)
    doors = entities_from(steps, 1)
    equip = entities_from(steps, 2)
    labels = entities_from(steps, 3)

    with open(out_md, "w", encoding="utf-8") as out:
        p = lambda s="": out.write(s + "\n")

        p("# Phase C Geometry Target — Rebuild Plan\n")
        p(
            "Source: `acad.validators.collect_entities` on "
            "`Rysunek4.dwg` (checkpoint `phaseC_start_pre_fix`).\n"
        )

        wcnt = Counter(w.get("layer", "?") for w in walls)
        p(f"## Walls: {len(walls)} polylines\n")
        p("| Layer | Count |")
        p("|---|---|")
        for lay, c in wcnt.most_common():
            p(f"| {lay} | {c} |")

        dcnt = Counter(d.get("layer", "?") for d in doors)
        p(f"\n## Doors: {len(doors)} entities\n")
        p("| Layer | Count |")
        p("|---|---|")
        for lay, c in dcnt.most_common():
            p(f"| {lay} | {c} |")

        ecnt = Counter(e.get("layer", "?") for e in equip)
        p(f"\n## Equipment/fixtures: {len(equip)} entities\n")
        p("| Layer | Count |")
        p("|---|---|")
        for lay, c in ecnt.most_common():
            p(f"| {lay} | {c} |")

        p(f"\n## Annotation labels: {len(labels)}\n")
        lcnt = Counter(x.get("layer", "?") for x in labels)
        for lay, c in lcnt.most_common():
            p(f"- `{lay}`: {c}")

        p("\n## Building envelope\n")
        all_bb = [bbox(w) for w in walls if bbox(w)]
        if all_bb:
            x0 = min(b[0] for b in all_bb)
            y0 = min(b[1] for b in all_bb)
            x1 = max(b[2] for b in all_bb)
            y1 = max(b[3] for b in all_bb)
            p(f"- min = ({x0:.0f}, {y0:.0f}) mm")
            p(f"- max = ({x1:.0f}, {y1:.0f}) mm")
            p(f"- size = {(x1-x0)/1000:.1f} × {(y1-y0)/1000:.1f} m")
            p(f"- area = {area_m2((x0,y0,x1,y1)):.0f} m²")

        p("\n## Rooms (derived from closed wall polylines + A-AREA-IDEN labels)\n")
        closed = [w for w in walls if is_closed(w)]
        p(f"{len(closed)} closed wall polylines of {len(walls)} total.\n")

        area_iden = [l for l in labels if l.get("layer") == "A-AREA-IDEN"]
        p("| # | Handle | Layer | Bbox (mm) | Area m² | Labels inside |")
        p("|---|---|---|---|---|---|")
        rooms = []
        for w in closed:
            bb = bbox(w)
            if not bb:
                continue
            x0, y0, x1, y1 = bb
            lbl_in = []
            for lbl in area_iden:
                p2 = anchor(lbl)
                if p2 and x0 <= p2[0] <= x1 and y0 <= p2[1] <= y1:
                    txt = (lbl.get("text") or lbl.get("contents") or "").strip()
                    if txt:
                        lbl_in.append(txt)
            rooms.append({
                "handle": w.get("handle"),
                "layer": w.get("layer"),
                "bbox": bb,
                "area_m2": area_m2(bb),
                "labels": lbl_in,
            })
        rooms.sort(key=lambda r: (r["layer"], -r["area_m2"]))
        for i, r in enumerate(rooms, 1):
            x0, y0, x1, y1 = r["bbox"]
            lbls = " / ".join(r["labels"][:2])[:70].replace("|", "\\|")
            p(
                f"| {i} | `{r['handle']}` | {r['layer']} "
                f"| ({x0:.0f},{y0:.0f})–({x1:.0f},{y1:.0f}) "
                f"| {r['area_m2']:.1f} | {lbls} |"
            )

        p("\n## Unclosed wall polylines (through-walls, corridor spines)\n")
        unclosed = [w for w in walls if not is_closed(w)]
        p(f"{len(unclosed)} unclosed polylines. These are the TROUBLEMAKERS — long")
        p("corridor walls that pass THROUGH multiple rooms' bboxes.\n")
        p("| # | Handle | Layer | Bbox (mm) | Bbox diag (m) |")
        p("|---|---|---|---|---|")
        for i, w in enumerate(unclosed, 1):
            bb = bbox(w)
            if not bb:
                continue
            x0, y0, x1, y1 = bb
            diag = (((x1-x0)**2 + (y1-y0)**2) ** 0.5) / 1000.0
            p(
                f"| {i} | `{w.get('handle')}` | {w.get('layer')} "
                f"| ({x0:.0f},{y0:.0f})–({x1:.0f},{y1:.0f}) | {diag:.1f} |"
            )

        p("\n## Doors — bbox list\n")
        p("| # | Handle | Layer | Bbox (mm) | Size (mm) |")
        p("|---|---|---|---|---|")
        for i, d in enumerate(doors, 1):
            bb = bbox(d)
            if not bb:
                continue
            x0, y0, x1, y1 = bb
            p(
                f"| {i} | `{d.get('handle')}` | {d.get('layer')} "
                f"| ({x0:.0f},{y0:.0f})–({x1:.0f},{y1:.0f}) "
                f"| {x1-x0:.0f}×{y1-y0:.0f} |"
            )

        p("\n## Medical equipment — bbox list\n")
        p("| # | Handle | Layer | DxfType | Bbox (mm) |")
        p("|---|---|---|---|---|")
        for i, e in enumerate(equip, 1):
            bb = bbox(e)
            if not bb:
                continue
            x0, y0, x1, y1 = bb
            p(
                f"| {i} | `{e.get('handle')}` | {e.get('layer')} | {e.get('dxfType')} "
                f"| ({x0:.0f},{y0:.0f})–({x1:.0f},{y1:.0f}) |"
            )

        p("\n## Next actions (Phase C-Wall EXECUTE will consume this inventory)\n")
        p("1. Preserve: axis grid (S-GRID), annotations (A-ANNO-*), A-AREA* polylines "
          "(keep as zone boundaries), titleblock, dimension chains.")
        p("2. Drop: ALL wall polylines (both closed and unclosed) — rebuild cleanly.")
        p("3. Drop: ALL door entities (A-DOOR, A-DOOR-FIRE) — rebuild after walls.")
        p("4. Drop: medical equipment that crosses walls (9 A-EQPM-MED × A-WALL-INT "
          "from B3) — reposition with 600 mm wall-clearance.")
        p("5. Rebuild walls as: one closed polyline per compartment, choosing the "
          "most-specific wall layer (LEAD > FARA > FIRE > STRUCT > EXT > INT).")
        p("6. Break openings: cut 2 × 1200 mm gaps per door in wall polylines, "
          "then draw door leaf (rectangle 50 × 1000 mm) + swing arc (90°) on "
          "A-DOOR / A-DOOR-FIRE.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1], sys.argv[2]))
