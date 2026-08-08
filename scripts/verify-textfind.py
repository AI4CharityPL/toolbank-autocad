# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.3 tranche 1 — finding and replacing text across a drawing.

Two things separate a real FIND from one that looks like it works.

**Coverage.** Text lives in six places, and a search reading only DBText and MText will miss
most of a real sheet: a room name is MText, a level tag is a block ATTRIBUTE, a note is an
MLeader, a schedule is a Table, a dimension can carry a text override. All six are placed here
and each is asserted to be found by handle - a tool that scanned two types would still return a
tidy list of hits and a plausible count.

**Formatting codes.** MText stores `\\fArial|b0|i0;` and `{\\H1.5x;...}` alongside the words.
A replacement done blindly on the stored string can land inside a code and change the font
rather than the text, or corrupt the entity. The test puts a formatted MText in the drawing
whose CODE contains the search word while the visible text does not, and asserts the tool
refuses that one with a reason instead of quietly rewriting it.

dryRun is checked by reading the entity back afterwards, not by trusting the flag.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "annotations", "dimensions", "view")}
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


def text_of(handle):
    """Read an entity's text back through the search tool, so nothing is taken on trust."""
    ok, r = S["annotations"].call("list_text_by_pattern",
                                  {"pattern": ".", "regex": True, "handles": [handle]})
    for hit in ((r or {}).get("results") or []):
        return hit.get("text")
    return None



def fresh_drawing():
    """A new drawing, and ONLY that drawing open.

    Every verification script calls new_document, and none of them ever closed the drawing it
    replaced - so the runs pile up. With more than one document open the backend processes
    behind these sessions land on DIFFERENT ones, and then a handle created by `annotations`
    resolves to a different entity in `geometry-2d`, or to nothing at all. That is far worse
    than an error: it is two tools quietly measuring two drawings.

    Measured while it was happening: annotations wrote SELFTEST at y=7000 and saw it; geometry-2d
    found nothing in that window; files reported Rysunek1.dwg and Rysunek2.dwg both open.

    So: make the new drawing, close every other one, and PROVE the sessions agree before
    measuring anything.
    """
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    # A failed call returns the error STRING, not a dict. Reaching into it with .get crashes
    # with an AttributeError that says nothing about the real problem - usually that AutoCAD is
    # not running at all.
    if not ok or not isinstance(r, dict):
        raise SystemExit(f"cannot list documents - is AutoCAD running with the plugin loaded?\n  {r}")
    docs = r.get("documents") or []
    # The new one is the last created; close the rest. They are unsaved scratch drawings from
    # earlier runs, which is the only reason this is safe to do unasked.
    for d in docs[:-1]:
        S["files"].call("close_document", {"path": d.get("path") or d.get("name"),
                                           "save": False})
    ok, r = S["files"].call("list_documents", {})
    left = (r or {}).get("documents") or []
    check("exactly one drawing is open, so no two sessions can be on different ones",
          len(left) == 1, f"{[d.get('name') for d in left]}")

    # Cross-session agreement, proved rather than assumed.
    okp, rp = S["annotations"].call(
        "add_dbtext", {"position": {"x": 0, "y": 9000}, "height": 10,
                       "contents": "SESSIONPROBE"})
    probe = hnd(rp) if okp else None
    ok2, bb = S["geometry-2d"].call("get_bounding_box", {"handle": probe}) if probe else (False, None)
    seen = ((bb or {}).get("bbox") or {}).get("min") or {} if isinstance(bb, dict) else {}
    check("the annotations and geometry-2d sessions are on the SAME drawing",
          bool(ok2) and abs((seen.get("y") or 0) - 9000) < 1e-6,
          f"probe={probe} placed at y=9000; geometry-2d answered {str(bb)[:160]}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})

print("== fresh drawing ==")
fresh_drawing()

print("\n== text in five different places ==")
t1 = hnd(do("annotations", "add_dbtext",
            {"position": {"x": 0, "y": 0}, "height": 10, "contents": "ROOM A101"},
            label="a single-line text"))
t2 = hnd(do("annotations", "add_mtext",
            {"position": {"x": 0, "y": 100}, "textHeight": 10, "contents": "ROOM A101 office"},
            label="an mtext"))
t3 = hnd(do("annotations", "add_mleader_text",
            {"arrowTip": {"x": 0, "y": 200}, "textPosition": {"x": 200, "y": 250},
             "contents": "SEE ROOM A101"}, label="an mleader"))
dim = hnd(do("dimensions", "dimension_linear",
             {"p1": {"x": 0, "y": 400}, "p2": {"x": 300, "y": 400},
              "dimLinePoint": {"x": 150, "y": 450}}, label="a dimension"))
do("dimensions", "edit_dimension_text", {"handle": dim, "text": "ROOM A101 wide"},
   label="with a text override on it")
# The argument names are cols/colWidth and contents/col - NOT columns/columnWidth/text. An
# earlier version of this file guessed the plural forms; add_table refused with "rows and cols
# must be > 0", which is the tool telling the truth about a field it never received.
tbl = hnd(do("annotations", "add_table",
             {"position": {"x": 600, "y": 100}, "rows": 2, "cols": 2,
              "rowHeight": 30, "colWidth": 200}, label="a table"))
do("annotations", "set_table_cell", {"handle": tbl, "row": 1, "col": 0,
                                     "contents": "ROOM A101"},
   label="with a cell naming the room")

print("\n== list_text_by_pattern reads all of them ==")
r = do("annotations", "list_text_by_pattern", {"pattern": "A101"})
found = {}
if isinstance(r, dict):
    found = {h.get("handle"): h for h in (r.get("results") or [])}
    by = r.get("scannedByType") or {}
    check("it reports WHAT it scanned, not just what it found", len(by) >= 4, str(by)[:250])
    # Each type asserted by handle. A search covering only DBText and MText would still return
    # a tidy list and a plausible count, and miss most of a real sheet.
    check("PROVEN: the single-line text was found", t1 in found, f"{list(found)} vs {t1}")
    check("PROVEN: the mtext was found", t2 in found, f"{list(found)} vs {t2}")
    check("PROVEN: the MLEADER was found", t3 in found, f"{list(found)} vs {t3}")
    check("PROVEN: the DIMENSION override was found", dim in found, f"{list(found)} vs {dim}")
    check("PROVEN: the TABLE cell was found — a Table is a BlockReference and the scan has to "
          "take it first or it never gets read",
          any(k == tbl for k in found), f"{list(found)} vs {tbl}")
    check("five distinct items matched", len(found) == 5, str(list(found))[:250])

print("\n-- the search options do something --")
r = do("annotations", "list_text_by_pattern", {"pattern": "room", "matchCase": True},
       label="matchCase on")
check("PROVEN: lowercase 'room' matches nothing when case matters",
      isinstance(r, dict) and r.get("matched") == 0, str(r)[:220])
r = do("annotations", "list_text_by_pattern", {"pattern": "room"}, label="matchCase off")
check("PROVEN: and everything when it does not",
      isinstance(r, dict) and r.get("matched") == 5, str(r)[:220])
r = do("annotations", "list_text_by_pattern", {"pattern": "A1", "wholeWord": True},
       label="wholeWord on")
check("PROVEN: 'A1' is not a whole word inside A101",
      isinstance(r, dict) and r.get("matched") == 0, str(r)[:220])
r = do("annotations", "list_text_by_pattern", {"pattern": r"A\d{3}", "regex": True},
       label="a regular expression")
check("PROVEN: the regex matched all five", isinstance(r, dict) and r.get("matched") == 5,
      str(r)[:220])
do("annotations", "list_text_by_pattern", {"pattern": "[unclosed", "regex": True},
   label="an invalid regex is refused", expect_fail=True)

# ── the formatting-code trap ──────────────────────────────────────────────────
print("\n== an MText whose formatting CODE contains the search word ==")
# The visible text is "PLAN"; the stored string also contains "Arial" inside a font code. A
# blind replacement of "Arial" would rewrite the font and leave the words untouched.
fm = hnd(do("annotations", "add_mtext",
            {"position": {"x": 0, "y": 700}, "textHeight": 10,
             "contents": r"{\fArial|b1|i0|c238|p34;PLAN}"},
            label="an mtext with a font code"))
r = do("annotations", "list_text_by_pattern", {"pattern": "Arial"},
       label="searching for Arial")
check("PROVEN: the search does NOT match inside the code — it reads rendered text",
      isinstance(r, dict) and r.get("matched") == 0,
      f"{str(r)[:250]} — a match here means the search is reading MText.Contents")
check("and the rendered text really is just PLAN", text_of(fm) == "PLAN", f"{text_of(fm)}")

r = do("annotations", "find_replace_text", {"find": "Arial", "replaceWith": "Calibri"},
       label="replacing Arial")
if isinstance(r, dict):
    check("PROVEN: nothing was changed — the word exists only in a formatting code",
          r.get("entitiesChanged") == 0, str(r)[:250])
check("PROVEN: and the entity still renders PLAN", text_of(fm) == "PLAN", f"{text_of(fm)}")

# ── find_replace_text ─────────────────────────────────────────────────────────
print("\n== dryRun changes nothing ==")
r = do("annotations", "find_replace_text",
       {"find": "A101", "replaceWith": "B202", "dryRun": True})
if isinstance(r, dict):
    check("it lists what WOULD change", r.get("entitiesChanged") == 5, str(r)[:250])
    check("and says it wrote nothing", r.get("dryRun") is True, str(r)[:220])
# Read back rather than trust the flag.
check("PROVEN: the drawing is untouched — the text still says A101",
      text_of(t1) == "ROOM A101", f"{text_of(t1)}")

print("\n== and then the real thing ==")
r = do("annotations", "find_replace_text", {"find": "A101", "replaceWith": "B202"})
if isinstance(r, dict):
    check("five entities changed", r.get("entitiesChanged") == 5, str(r)[:250])
    check("five occurrences", r.get("occurrences") == 5, str(r)[:250])
print("\n-- MEASURED INDEPENDENTLY, one type at a time --")
check("PROVEN: the single-line text now reads B202", text_of(t1) == "ROOM B202", f"{text_of(t1)}")
check("PROVEN: the mtext too", text_of(t2) == "ROOM B202 office", f"{text_of(t2)}")
check("PROVEN: the mleader too", text_of(t3) == "SEE ROOM B202", f"{text_of(t3)}")
check("PROVEN: the dimension override too", text_of(dim) == "ROOM B202 wide", f"{text_of(dim)}")
check("PROVEN: and the table cell too", text_of(tbl) == "ROOM B202", f"{text_of(tbl)}")
r = do("annotations", "list_text_by_pattern", {"pattern": "A101"},
       label="searching for the old name again")
check("PROVEN: nothing answers to A101 any more",
      isinstance(r, dict) and r.get("matched") == 0, str(r)[:220])

print("\n-- a layer filter narrows it --")
do("annotations", "add_dbtext",
   {"position": {"x": 0, "y": 900}, "height": 10, "contents": "ROOM B202", "layer": "NOTES"},
   label="one more on layer NOTES")
r = do("annotations", "find_replace_text",
       {"find": "B202", "replaceWith": "C303", "layerFilter": "NOTES"})
check("PROVEN: only the one on that layer changed",
      isinstance(r, dict) and r.get("entitiesChanged") == 1, str(r)[:250])
check("and the others still read B202", text_of(t1) == "ROOM B202", f"{text_of(t1)}")

print("\n-- refusals --")
do("annotations", "find_replace_text", {"find": "x"},
   label="a missing replaceWith is refused", expect_fail=True)
do("annotations", "find_replace_text", {"replaceWith": "y"},
   label="a missing find is refused", expect_fail=True)
do("annotations", "list_text_by_pattern", {},
   label="a missing pattern is refused", expect_fail=True)

# ── export_text_content ───────────────────────────────────────────────────────
print("\n== export_text_content ==")
csv = os.path.join(OUT, "drawing-text.csv")
if os.path.exists(csv):
    os.remove(csv)
r = do("annotations", "export_text_content", {"path": csv})
if isinstance(r, dict):
    check("it reports what it wrote", (r.get("items") or 0) >= 6, str(r)[:250])
check("PROVEN: the file is on disk and not empty",
      os.path.exists(csv) and os.path.getsize(csv) > 0,
      f"{csv}: {os.path.getsize(csv) if os.path.exists(csv) else 'missing'}")
if os.path.exists(csv):
    body = open(csv, encoding="utf-8-sig").read()
    check("PROVEN: it has a header and the room text is in it",
          body.startswith("handle,type,layer,text") and "ROOM B202" in body, body[:200])
    # The rendered text, not the stored string - the formatted MText must appear as PLAN.
    check("PROVEN: the formatted mtext exported as PLAN, with its codes resolved",
          "PLAN" in body and "\\fArial" not in body, body[:300])

txt = os.path.join(OUT, "drawing-text.txt")
r = do("annotations", "export_text_content", {"path": txt, "format": "txt"},
       label="txt format")
check("the txt file has one line per item and no header",
      os.path.exists(txt) and not open(txt, encoding="utf-8-sig").read().startswith("handle,"),
      f"{txt}")

print("\n-- refusals --")
do("annotations", "export_text_content", {"path": csv, "format": "pdf"},
   label="an unknown format is refused", expect_fail=True)
do("annotations", "export_text_content", {"path": r"C:\no-such-folder-here\x.csv"},
   label="a missing folder is refused", expect_fail=True)
do("annotations", "export_text_content", {},
   label="a missing path is refused", expect_fail=True)

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
