#!/usr/bin/env python3
"""Regenerate docs/TOOLS-REFERENCE.md from the manifests.

README says this reference is generated and so "can't drift out of sync the way
hand-written tool lists do". That was true of the file and false of the repo: there was
no generator. The file was produced once, by hand or by something that was never
committed, and by the time anyone checked it claimed 31 categories and 340 tools against
an actual 39 and 478 — a 138-tool gap in the document a reader consults to find out what
exists.

A claim about how a file is maintained is only as good as the thing that maintains it.

Usage:
    python scripts/generate-tools-reference.py            # rewrite the file
    python scripts/generate-tools-reference.py --check    # exit 1 if stale, write nothing
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
MANIFESTS = REPO / "toolbank-manifests"
OUT = REPO / "docs" / "TOOLS-REFERENCE.md"


def cell(text: str) -> str:
    """Make a description safe inside a Markdown table cell.

    A raw `|` ends the cell and silently shifts every following column; ten descriptions
    in this bank contain one. Newlines end the row outright.
    """
    return " ".join((text or "").split()).replace("|", "\\|")


def render() -> str:
    categories: list[tuple[str, list[dict]]] = []
    for path in sorted(MANIFESTS.glob("acad-*.json")):
        manifest = json.loads(path.read_text(encoding="utf-8"))
        tools = sorted(
            manifest.get("tools_summary") or [], key=lambda t: t.get("name", "")
        )
        categories.append((manifest["id"], tools))

    total = sum(len(tools) for _, tools in categories)

    lines = [
        "# ToolBank AutoCAD — Full Tool Reference",
        "",
        f"Auto-generated from `toolbank-manifests/acad-*.json` by "
        f"`scripts/generate-tools-reference.py`. {len(categories)} categories, "
        f"{total} tools total.",
        "",
        "## Categories",
        "",
    ]
    for cat, tools in categories:
        lines.append(f"- [{cat}](#{cat}) ({len(tools)} tools)")
    lines += ["", "---", ""]

    for cat, tools in categories:
        lines += [f"## {cat}", "", "| Tool | Description |", "|---|---|"]
        for tool in tools:
            lines.append(f"| `{tool['name']}` | {cell(tool.get('description', ''))} |")
        lines.append("")

    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--check",
        action="store_true",
        help="Fail if the committed file differs from what the manifests produce. "
        "Use it in the pre-commit gate so the drift cannot come back.",
    )
    args = ap.parse_args()

    if not MANIFESTS.is_dir():
        print(f"error: no manifest directory at {MANIFESTS}", file=sys.stderr)
        return 2

    generated = render()

    if args.check:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""
        if current != generated:
            print(
                f"{OUT.relative_to(REPO)} is out of date. "
                "Run: python scripts/generate-tools-reference.py",
                file=sys.stderr,
            )
            return 1
        print(f"{OUT.relative_to(REPO)}: up to date")
        return 0

    OUT.write_text(generated, encoding="utf-8")
    cats = generated.count("\n## acad-")
    total = generated.count("\n| `")
    print(f"{OUT.relative_to(REPO)}: {cats} categories, {total} tools")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
