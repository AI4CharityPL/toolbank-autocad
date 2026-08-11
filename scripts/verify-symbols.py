# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.3 tranche 4 — symbols and stacked fractions.

Both tools write CONTROL CODES into a string, and both can therefore succeed at writing while
failing at meaning. They need different proofs, and getting that right is the whole point:

* **insert_symbol** splits into two cases that can be proved to different depths, and pretending
  otherwise is the mistake this file made first. Where the symbol goes in as the CHARACTER -
  every MText, and single-line text for anything outside %%c/%%d/%%p - the entity is read back
  and the glyph has to be there. Where it goes in as a CONTROL CODE there is nothing to read:
  `DBText.TextString` returns what is STORED, so %%c stays "%%c" and becomes a diameter sign only
  when AutoCAD draws it. Those are confirmed on the exported image, the same honest limit as the
  background mask. An earlier version of this file demanded the glyph back from DBText and failed
  six checks on a tool that was working correctly.

* **stack_fraction** cannot be judged that way at all. A stacked "1/2" renders as the same three
  characters as an unstacked one, so the rendered text is identical either way. It is judged on
  the drawn EXTENT instead: two levels of digits make the text taller. The control is an
  identical MText left unstacked, so "it got taller" is measured against something.
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


def close(a, b, tol=1e-6):
    return a is not None and b is not None and abs(a - b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def box(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    b = (r or {}).get("bbox") or {} if ok else {}
    mn, mx = b.get("min") or {}, b.get("max") or {}
    return (mn.get("x"), mn.get("y"), mx.get("x"), mx.get("y"))


def rendered(h):
    """What the entity actually shows, read back through the search tool."""
    ok, r = S["annotations"].call("list_text_by_pattern",
                                  {"pattern": ".", "regex": True, "handles": [h]})
    for hit in ((r or {}).get("results") or []):
        return hit.get("text")
    return None


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — see verify-textgeom.py for why."""
    do("files", "new_document", {})
    ok, r = S["files"].call("list_documents", {})
    # A failed call returns the error STRING, not a dict. Reaching into it with .get crashes
    # with an AttributeError that says nothing about the real problem - usually that AutoCAD is
    # not running at all.
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


print("== fresh drawing ==")
fresh_drawing()

# ── insert_symbol ─────────────────────────────────────────────────────────────
print("\n== insert_symbol into SINGLE-LINE text, which takes %% codes ==")
t1 = hnd(do("annotations", "add_dbtext",
            {"position": {"x": 0, "y": 0}, "height": 20, "contents": "PIPE 150"},
            label="a single-line text"))
r = do("annotations", "insert_symbol", {"handles": [t1], "symbol": "diameter",
                                        "where": "start"})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    check("it stored the %%c control code, which is what DBText understands",
          "%%c" in (it.get("stored") or ""), str(it)[:220])
    # An earlier version of this file demanded the diameter GLYPH back from the entity here, and
    # that is a question DBText cannot answer: TextString returns what is STORED, codes and all,
    # and %%c becomes a symbol only when AutoCAD draws it. The tool now says which case it is
    # in, and the glyph for a control code is confirmed on the exported image instead - the same
    # honest limit as the background mask.
    check("and it says the symbol went in as a CONTROL CODE, not a character",
          it.get("viaControlCode") is True, str(it)[:250])

print("\n-- degrees and plusminus too --")
t2 = hnd(do("annotations", "add_dbtext",
            {"position": {"x": 0, "y": 60}, "height": 20, "contents": "FALL 2"},
            label="another"))
r = do("annotations", "insert_symbol", {"handles": [t2], "symbol": "degrees"})
check("the degree code is stored, and flagged as a code",
      isinstance(r, dict) and "%%d" in str((r.get("items") or [{}])[0].get("stored"))
      and (r.get("items") or [{}])[0].get("viaControlCode") is True, str(r)[:250])
t3 = hnd(do("annotations", "add_dbtext",
            {"position": {"x": 0, "y": 120}, "height": 20, "contents": "TOL 5"},
            label="a third"))
r = do("annotations", "insert_symbol", {"handles": [t3], "symbol": "plusminus",
                                        "where": "start"})
check("and the plus-minus code likewise",
      isinstance(r, dict) and str((r.get("items") or [{}])[0].get("stored")).startswith("%%p"),
      str(r)[:250])

print("\n-- a symbol with NO %% code goes into DBText as the character, and IS checkable --")
# The style is created FIRST, unconditionally. An earlier version created it only if the text
# failed without it - which it does not: add_dbtext falls back to the current style instead.
# The delta then went into a text drawn with the default SHX font, which has no glyph for it, so
# the character was correctly in the string and drew as garbage on the sheet. That is exactly
# the trap this tool's own note warns about (and KNOWN-GAPS A8, where m2 came out as m?), and
# the point of a verification image is not to reproduce it by accident.
do("annotations", "create_text_style",
   {"name": "ACADMCP-SYM", "font": "Arial"},
   label="a TrueType style, because the default SHX font has no delta glyph")
t4 = hnd(do("annotations", "add_dbtext",
            {"position": {"x": 0, "y": 180}, "height": 20, "contents": "ANGLE 45",
             "textStyle": "ACADMCP-SYM"}, label="a single-line text on it"))
r = do("annotations", "insert_symbol", {"handles": [t4], "symbol": "delta", "where": "start"})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    check("PROVEN: it went in as the CHARACTER, not a code",
          it.get("viaControlCode") is False and "Δ" in (it.get("stored") or ""), str(it)[:250])
check("PROVEN, read back off the entity: the glyph is there",
      "Δ" in (rendered(t4) or ""), f"{rendered(t4)!r}")

print("\n== insert_symbol into MTEXT, which takes the character ==")
m1 = hnd(do("annotations", "add_mtext",
            # Clear of the single-line texts below. A DBText sits ON its baseline and grows
            # UPWARDS; an MText hangs DOWN from its top-left corner by default - so an MText at
            # y=200 lands on a DBText at y=180 and the verification image comes out unreadable.
            # Found by counting two entities in the same band, not by squinting at the picture.
            {"position": {"x": 0, "y": 320}, "textHeight": 20, "contents": "DUCT 400"},
            label="an mtext"))
r = do("annotations", "insert_symbol", {"handles": [m1], "symbol": "diameter",
                                        "where": "start"})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    # The MText branch must NOT write %%c - that is the DBText code and would show literally.
    check("PROVEN: it stored the character, not a %% code — MText is not DBText",
          "%%c" not in (it.get("stored") or "") and "∅" in (it.get("stored") or ""),
          f"stored {it.get('stored')!r}")
check("PROVEN: and the mtext renders it", "∅" in (rendered(m1) or ""), f"{rendered(m1)!r}")

print("\n-- a placeholder is replaced wherever it appears --")
m2 = hnd(do("annotations", "add_mtext",
            {"position": {"x": 0, "y": 440}, "textHeight": 20,
             "contents": "BAR <D>12 AND <D>16"}, label="text with two placeholders"))
r = do("annotations", "insert_symbol",
       {"handles": [m2], "symbol": "diameter", "replace": "<D>"})
if isinstance(r, dict):
    it = (r.get("items") or [{}])[0]
    check("PROVEN: both placeholders were replaced, not just the first",
          it.get("insertions") == 2, str(it)[:250])
check("PROVEN: and no placeholder survives in what is rendered",
      "<D>" not in (rendered(m2) or "") and (rendered(m2) or "").count("∅") == 2,
      f"{rendered(m2)!r}")

print("\n-- a Unicode code point, for anything not on the list --")
m3 = hnd(do("annotations", "add_mtext",
            {"position": {"x": 0, "y": 560}, "textHeight": 20, "contents": "AREA 12"},
            label="an mtext"))
do("annotations", "insert_symbol", {"handles": [m3], "symbol": "U+00B2"},
   label="U+00B2, superscript two")
check("PROVEN: the squared sign is rendered", "²" in (rendered(m3) or ""), f"{rendered(m3)!r}")

print("\n-- refusals --")
do("annotations", "insert_symbol", {"handles": [m3]},
   label="a missing symbol is refused", expect_fail=True)
r = do("annotations", "insert_symbol", {"handles": [m3], "symbol": "sparkle"},
       label="an unknown symbol name is refused", expect_fail=True)
check("and the refusal lists the names it does know",
      "diameter" in str(r), str(r)[:250])
do("annotations", "insert_symbol", {"handles": [m3], "symbol": "degrees", "where": "middle"},
   label="an unknown position is refused", expect_fail=True)
r = do("annotations", "insert_symbol",
       {"handles": [m3], "symbol": "degrees", "replace": "<NOPE>"},
       label="a placeholder that is not there is refused", expect_fail=True)
check("and the refusal quotes the text it looked in",
      "does not appear" in str(r), str(r)[:250])
ln = hnd(do("geometry-2d", "draw_line",
            {"start": {"x": 0, "y": -100}, "end": {"x": 100, "y": -100}}, label="a line"))
do("annotations", "insert_symbol", {"handles": [ln], "symbol": "degrees"},
   label="a line is refused by name", expect_fail=True)

# ── stack_fraction ────────────────────────────────────────────────────────────
print("\n== stack_fraction, against an unstacked control ==")
# Two identical MTexts. One gets stacked, the other does not - so "it got taller" is measured
# against something rather than asserted on its own.
f1 = hnd(do("annotations", "add_mtext",
            {"position": {"x": 600, "y": 0}, "textHeight": 20, "contents": "PIPE 1/2 INCH"},
            label="an mtext with a fraction"))
ctrl = hnd(do("annotations", "add_mtext",
              {"position": {"x": 600, "y": 200}, "textHeight": 20,
               "contents": "PIPE 1/2 INCH"}, label="an identical CONTROL, left alone"))
h_ctrl = box(ctrl)[3] - box(ctrl)[1]

r = do("annotations", "stack_fraction", {"handle": f1})
if isinstance(r, dict):
    check("it found the fraction", r.get("stacked") == 1 and r.get("fractions") == ["1/2"],
          str(r)[:250])
    check("and stored a \\S stacking code", "\\S" in (r.get("stored") or ""), str(r)[:250])
    check("style defaulted to horizontal", r.get("style") == "horizontal", str(r)[:250])
h_f1 = box(f1)[3] - box(f1)[1]
# THE measurement. The rendered text is "PIPE 1/2 INCH" either way, so this is the only thing
# that can tell a stacked fraction from an unstacked one.
check("PROVEN: the stacked one is TALLER than the untouched control",
      h_f1 > h_ctrl, f"stacked {h_f1} vs control {h_ctrl}")
check("and the rendered text is unchanged, which is exactly why height had to be measured",
      rendered(f1) == rendered(ctrl), f"{rendered(f1)!r} vs {rendered(ctrl)!r}")

print("\n-- the three styles are three different codes --")
for style, code in (("diagonal", "#"), ("tolerance", "^")):
    fx = hnd(do("annotations", "add_mtext",
                {"position": {"x": 600, "y": 400 if style == "diagonal" else 600},
                 "textHeight": 20, "contents": "SIZE 3/4"}, label=f"an mtext for {style}"))
    r = do("annotations", "stack_fraction", {"handle": fx, "style": style})
    if isinstance(r, dict):
        check(f"  {style} uses '{code}' as the separator",
              ("\\S3" + code + "4;") in (r.get("stored") or ""), str(r.get("stored"))[:200])

print("\n-- refusals --")
noneed = hnd(do("annotations", "add_mtext",
                {"position": {"x": 600, "y": 800}, "textHeight": 20, "contents": "NO FRACTION"},
                label="an mtext with nothing to stack"))
r = do("annotations", "stack_fraction", {"handle": noneed},
       label="text with no fraction is refused", expect_fail=True)
check("and the refusal quotes the text and offers a pattern",
      "NO FRACTION" in str(r) and "pattern" in str(r), str(r)[:250])
do("annotations", "stack_fraction", {"handle": f1, "style": "sideways"},
   label="an unknown style is refused", expect_fail=True)
do("annotations", "stack_fraction", {"handle": t1},
   label="single-line text is refused by name", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "symbols.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -50, "yMin": -60, "xMax": 1100, "yMax": 1000},
                            "widthPx": 2000, "heightPx": 1700})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm, and for the %% codes this is the ONLY place they can be confirmed: a")
print("     diameter sign before PIPE 150, a degree sign after FALL 2, plus-minus before TOL 5")
print("     — NOT the literal text %%c, %%d, %%p. BAR shows TWO diameter signs and no <D>")
print("     placeholder. On the right, PIPE 1/2 INCH twice: the LOWER one has its half stacked")
print("     over a bar, the upper control still reads it inline; SIZE with a diagonal 3/4 and a")
print("     bar-less tolerance 3 over 4. And DANGLE 45 shows a real delta, drawn in a TrueType")
print("     style — the same text in the default SHX font draws the delta as garbage, which is")
print("     the limit the tool's note names and this image would otherwise have reproduced.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
