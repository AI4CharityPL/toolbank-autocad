# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.3 tranche 5 — text_to_mtext and explode_mtext_to_text.

These two are inverses, so the strongest check available is a ROUND TRIP: three lines become one
MText, that MText becomes three lines again, and the words come back in the same order. Either
tool alone can be wrong in a way that looks fine; both wrong in exactly compensating ways is a
great deal less likely.

The trap the round trip alone would NOT catch is order. Combining lines in whatever order their
handles arrive produces a paragraph with the sentences shuffled, and every count stays correct:
three in, one out, all the words present. So the three texts here are CREATED in deliberately
scrambled order — middle, bottom, top — and the assertion is that the MText reads top to bottom
anyway.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "annotations", "view")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "no tool registered" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:190]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:120]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def rendered(h):
    ok, r = S["annotations"].call("list_text_by_pattern",
                                  {"pattern": ".", "regex": True, "handles": [h]})
    for hit in ((r or {}).get("results") or []):
        return hit.get("text")
    return None


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — see verify-textgeom.py for why."""
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    if not ok or not isinstance(r, dict):
        raise SystemExit(f"cannot list documents - is AutoCAD running with the plugin loaded?\n  {r}")
    for d in (r.get("documents") or [])[:-1]:
        S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})
    ok, r = S["files"].call("list_documents", {})
    left = (r or {}).get("documents") or []
    check("exactly one drawing is open, so no two sessions can be on different ones",
          len(left) == 1, f"{[d.get('name') for d in left]}")
    okp, rp = S["annotations"].call(
        "add_dbtext", {"position": {"x": 0, "y": 9000}, "height": 10, "contents": "SESSIONPROBE"})
    probe = hnd(rp) if okp else None
    ok2, bb = S["geometry-2d"].call("get_bounding_box", {"handle": probe}) if probe else (False, None)
    seen = ((bb or {}).get("bbox") or {}).get("min") or {} if isinstance(bb, dict) else {}
    check("the annotations and geometry-2d sessions are on the SAME drawing",
          bool(ok2) and abs((seen.get("y") or 0) - 9000) < 1e-6,
          f"probe={probe} placed at y=9000; geometry-2d answered {str(bb)[:160]}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


def text(x, y, s, h=10):
    return hnd(S["annotations"].call(
        "add_dbtext", {"position": {"x": x, "y": y}, "height": h, "contents": s})[1])


print("== fresh drawing ==")
fresh_drawing()

# ── text_to_mtext, with the lines created OUT of reading order ────────────────
print("\n== three lines, created middle / bottom / top ==")
TOP, MID, BOT = "FIRST LINE", "SECOND LINE", "THIRD LINE"
# Deliberately scrambled. A tool that combined by handle order would produce
# SECOND / THIRD / FIRST and report three-in-one-out with every word present.
b = text(0, 200, MID)
c = text(0, 100, BOT)
a = text(0, 300, TOP)
check("three texts were placed", all([a, b, c]), f"{a} {b} {c}")

r = do("annotations", "text_to_mtext", {"handles": [b, c, a]})
mt = hnd(r)
if isinstance(r, dict):
    check("three were combined into one", r.get("combined") == 3, str(r)[:250])
    # THE assertion. The count would be right either way; the order is the only thing that
    # separates a working combine from a shuffled one.
    check("PROVEN: reading order is top to bottom, NOT the order the handles came in",
          r.get("readingOrder") == [TOP, MID, BOT],
          f"got {r.get('readingOrder')} — handle order would give {[MID, BOT, TOP]}")
    check("and the paragraphs are joined with MText's own break",
          (r.get("contents") or "").count("\\P") == 2, repr(r.get("contents")))

print("\n-- the sources are gone and the words are in the mtext --")
for h, name in ((a, "top"), (b, "middle"), (c, "bottom")):
    ok_, r_ = S["annotations"].call("list_text_by_pattern",
                                    {"pattern": ".", "regex": True, "handles": [h]})
    # keepOriginal defaults to false, so each source must be ERASED. An earlier version of this
    # file labelled the same call "still resolves as an entity", which asserted the opposite of
    # the contract and would have passed whichever way the tool behaved.
    check(f"PROVEN: the {name} source was erased",
          ok_ and (r_ or {}).get("matched") == 0, f"{str(r_)[:200]}")
got = rendered(mt) or ""
check("PROVEN: all three lines survive into what the MText renders",
      all(s in got for s in (TOP, MID, BOT)), repr(got))
check("PROVEN: and in that order inside the rendered text",
      got.find(TOP) < got.find(MID) < got.find(BOT), repr(got))

print("\n-- keepOriginal leaves them alone --")
k1 = text(400, 200, "KEEP A")
k2 = text(400, 100, "KEEP B")
r = do("annotations", "text_to_mtext", {"handles": [k1, k2], "keepOriginal": True})
kept = hnd(r)
check("PROVEN: the first source is still there",
      (rendered(k1) or "") == "KEEP A", f"{rendered(k1)!r}")
check("and the combined mtext exists alongside it",
      "KEEP A" in (rendered(kept) or ""), f"{rendered(kept)!r}")

print("\n-- refusals --")
do("annotations", "text_to_mtext", {"handles": []},
   label="an empty handle list is refused", expect_fail=True)
r = do("annotations", "text_to_mtext", {"handles": [kept]},
       label="an MText is refused by name", expect_fail=True)
check("and the refusal says an MText is already one",
      "already one" in str(r), str(r)[:250])
ln = hnd(do("geometry-2d", "draw_line",
            {"start": {"x": 0, "y": -100}, "end": {"x": 100, "y": -100}}, label="a line"))
do("annotations", "text_to_mtext", {"handles": [ln]},
   label="a line is refused by name", expect_fail=True)

# ── explode_mtext_to_text, closing the round trip ─────────────────────────────
print("\n== explode_mtext_to_text — and the round trip closes ==")
r = do("annotations", "explode_mtext_to_text", {"handle": mt})
back = []
if isinstance(r, dict):
    back = [e.get("text") for e in (r.get("entities") or [])]
    check("three pieces came back out", r.get("pieces") == 3, str(r)[:250])
    check("it reports what it started from", r.get("before") == got, repr(r.get("before")))
    # THE round trip. Three in, one MText, three out, same words in the same order. Either tool
    # alone could be wrong in a way that looks fine; both wrong in compensating ways is a great
    # deal less likely.
    check("PROVEN: the round trip returns the same three lines in the same order",
          back == [TOP, MID, BOT], f"got {back}")
    handles = [e.get("handle") for e in (r.get("entities") or [])]
    check("each piece is a single-line text again",
          all(e.get("type") == "DBText" for e in (r.get("entities") or [])),
          str(r.get("entities"))[:250])
do("annotations", "list_text_by_pattern", {"pattern": ".", "regex": True, "handles": [mt]},
   label="the source MText was consumed")

print("\n-- keepOriginal on the way back too --")
m2 = hnd(do("annotations", "add_mtext",
            {"position": {"x": 800, "y": 300}, "textHeight": 10,
             "contents": "ALPHA\\PBETA"}, label="an mtext of two paragraphs"))
r = do("annotations", "explode_mtext_to_text", {"handle": m2, "keepOriginal": True})
if isinstance(r, dict):
    check("two pieces, one per LINE and not per word",
          r.get("pieces") == 2, str(r)[:250])
    check("PROVEN: the words came through",
          sorted(e.get("text") for e in (r.get("entities") or [])) == ["ALPHA", "BETA"],
          str(r.get("entities"))[:250])
check("PROVEN: and the original MText survives", "ALPHA" in (rendered(m2) or ""),
      f"{rendered(m2)!r}")

print("\n-- refusals --")
do("annotations", "explode_mtext_to_text", {"handle": ln},
   label="a line is refused by name", expect_fail=True)
r = do("annotations", "explode_mtext_to_text", {"handle": k1},
       label="single-line text is refused by name", expect_fail=True)
check("and the refusal says explode takes an MText",
      "not an MText" in str(r), str(r)[:250])

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "textconv.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 1100, "yMax": 400},
                            "widthPx": 2000, "heightPx": 800})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm: FIRST / SECOND / THIRD LINE reading DOWN the page in that order — they were")
print("     created middle, bottom, top, so any other order means the combine used handle order.")
print("     KEEP A / KEEP B appear twice, overlapping, because keepOriginal left the sources in")
print("     place next to the new MText. ALPHA / BETA likewise.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
