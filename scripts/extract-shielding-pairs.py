"""Phase C-prep: extract overlap pairs involving shielding/fire walls from
the B3 log and emit them as an acad_design_iterate plan (JSON) with a
`get_intersections` step per pair. Feed the emitted plan into CallMcpTool
to fetch authoritative intersection points for classification.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


SHIELD = {"A-WALL-LEAD", "A-WALL-FARA", "A-WALL-FIRE"}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("b3", type=Path)
    ap.add_argument("--out-pairs-json", type=Path, required=True)
    ap.add_argument("--out-plan-json", type=Path, required=True)
    args = ap.parse_args()

    d = json.loads(args.b3.read_text(encoding="utf-8"))
    pairs: list[dict] = []
    seen: set[tuple[str, str]] = set()
    for step in d["IterationLogs"][0]["Steps"]:
        if not step.get("Ok"):
            continue
        for ov in step.get("output", {}).get("overlaps", []):
            if ov.get("layerA") not in SHIELD and ov.get("layerB") not in SHIELD:
                continue
            key = tuple(sorted([ov["handleA"], ov["handleB"]]))
            if key in seen:
                continue
            seen.add(key)
            pairs.append(ov)

    plan = [
        {
            "category": "geometry2d",
            "tool": "acad.geometry2d.get_intersections",
            "args": {"a": ov["handleA"], "b": ov["handleB"]},
        }
        for ov in pairs
    ]
    args.out_pairs_json.write_text(
        json.dumps(pairs, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    args.out_plan_json.write_text(
        json.dumps(plan, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    print(f"Extracted {len(pairs)} unique shielding/fire overlap pairs.")
    print(f"  pairs -> {args.out_pairs_json}")
    print(f"  plan  -> {args.out_plan_json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
