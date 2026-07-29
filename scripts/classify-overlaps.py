"""Phase C classifier: read a check_overlaps JSON + a sibling log of per-pair
get_intersections, and classify each pair as T-junction / collinear-overlap /
through-wall-breach, based on whether the 2 intersection points lie on the
same edge of the "closed" participant.

Usage:
    python scripts/classify-overlaps.py <b3-overlap-log.json> <intersections-log.json> [--out FILE.md]

Inputs:
  b3-overlap-log.json      - output of acad_design_iterate containing
                             acad.validators.check_overlaps steps (has
                             handleA/handleB + bboxes + intersectionCount).
  intersections-log.json   - output of acad_design_iterate containing
                             acad.geometry2d.get_intersections steps
                             (one per overlap pair). The pairs must be
                             in the same order as they appear in B3.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


TOL = 1.0


def pairs_from_b3(b3_path: Path) -> list[dict]:
    d = json.loads(b3_path.read_text(encoding="utf-8"))
    iter_list = d.get("IterationLogs") or d.get("logs") or []
    out: list[dict] = []
    for step in iter_list[0]["Steps"]:
        if not step.get("Ok"):
            continue
        for ov in step.get("output", {}).get("overlaps", []):
            out.append(ov)
    return out


def on_edges(p: tuple[float, float], bbox: list[float]) -> frozenset[str]:
    """Return ALL edges of bbox=[x0,y0,x1,y1] on which the point lies.
    Corner points return a 2-element set."""
    x, y = p
    x0, y0, x1, y1 = bbox
    es: set[str] = set()
    if abs(x - x0) < TOL and y0 - TOL <= y <= y1 + TOL: es.add("left")
    if abs(x - x1) < TOL and y0 - TOL <= y <= y1 + TOL: es.add("right")
    if abs(y - y0) < TOL and x0 - TOL <= x <= x1 + TOL: es.add("bottom")
    if abs(y - y1) < TOL and x0 - TOL <= x <= x1 + TOL: es.add("top")
    return frozenset(es)


def classify(pts: list[dict], bbox_closed: list[float] | None) -> str:
    if not pts:
        return "no_intersection"
    if len(pts) == 1:
        return "t_junction"
    if bbox_closed is None:
        return "multi_point"
    # Each point reports the set of edges it belongs to (corners belong to 2).
    # If the intersection-point set shares at least one common edge, both points
    # lie on that SAME edge of the compartment -> collinear overlap. Otherwise
    # points live on different edges -> through-wall breach.
    point_edges = [on_edges((p["x"], p["y"]), bbox_closed) for p in pts]
    if not point_edges or any(not e for e in point_edges):
        return "not_on_boundary"
    common = set(point_edges[0])
    for e in point_edges[1:]:
        common &= e
    if common:
        return "collinear_overlap"
    return "through_wall_breach"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("b3", type=Path)
    ap.add_argument("intersections", type=Path)
    ap.add_argument("--out", type=Path)
    args = ap.parse_args()

    overlaps = pairs_from_b3(args.b3)
    ilog = json.loads(args.intersections.read_text(encoding="utf-8"))
    iter_list = ilog.get("IterationLogs") or ilog.get("logs") or []
    isteps_all = iter_list[0]["Steps"]
    iplan = ilog.get("plan") or []
    isteps_by_pair: dict[tuple[str, str], dict] = {}
    for i, step in enumerate(isteps_all):
        if step.get("Tool") != "acad.geometry2d.get_intersections":
            continue
        if i < len(iplan):
            a = iplan[i]["args"].get("a")
            b = iplan[i]["args"].get("b")
            if a and b:
                isteps_by_pair[(a, b)] = step
                isteps_by_pair[(b, a)] = step

    rows: list[dict] = []
    analysed = 0
    for ov in overlaps:
        step = isteps_by_pair.get((ov["handleA"], ov["handleB"]))
        if step is None:
            continue
        analysed += 1
        if not step.get("Ok"):
            cat = "error"
            pts = []
        else:
            pts = step["output"].get("points", [])
            # The "closed" participant is whichever has consistent bbox-as-rectangle
            # semantics. Use layer heuristics: LEAD/FARA/FIRE compartments are
            # usually the closed ones. If neither side is shielding, fall back to
            # layer count (we can't tell without geometry).
            closed_bbox = None
            if ov.get("layerB") in {"A-WALL-LEAD", "A-WALL-FARA", "A-WALL-FIRE"}:
                closed_bbox = ov.get("bboxB")
            elif ov.get("layerA") in {"A-WALL-LEAD", "A-WALL-FARA", "A-WALL-FIRE"}:
                closed_bbox = ov.get("bboxA")
            cat = classify(pts, closed_bbox)
        rows.append({
            "handleA": ov["handleA"],
            "handleB": ov["handleB"],
            "layerA":  ov["layerA"],
            "layerB":  ov["layerB"],
            "count":   ov.get("intersectionCount", len(pts)),
            "classification": cat,
        })

    from collections import Counter
    cnt = Counter(r["classification"] for r in rows)
    lines: list[str] = []
    lines.append(f"# Phase C overlap classification ({len(rows)} pairs analysed of {len(overlaps)} total)\n")
    lines.append("## Histogram\n")
    for k, v in cnt.most_common():
        lines.append(f"- **{k}**: {v}")
    lines.append("\n## Through-wall breaches (code-critical)\n")
    lines.append("| Handle A | Layer A | Handle B | Layer B | pts |")
    lines.append("|---|---|---|---|---|")
    for r in rows:
        if r["classification"] == "through_wall_breach":
            lines.append(f"| `{r['handleA']}` | {r['layerA']} | `{r['handleB']}` | {r['layerB']} | {r['count']} |")
    lines.append("\n## Collinear overlaps (drafting duplication)\n")
    lines.append(f"{sum(1 for r in rows if r['classification'] == 'collinear_overlap')} pairs.\n")
    lines.append("## T-junctions (normal drafting, no action)\n")
    lines.append(f"{sum(1 for r in rows if r['classification'] == 't_junction')} pairs.\n")

    out_text = "\n".join(lines)
    if args.out:
        args.out.write_text(out_text, encoding="utf-8")
        print(f"Wrote {args.out}")
    else:
        print(out_text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
