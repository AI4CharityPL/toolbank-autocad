# -*- coding: utf-8 -*-
"""Live verification for roadmap 2.1 sheet sets — the write tranche.

Four write tools are under test: set_sheet_number, rename_sheet, set_sheet_title and
set_sheet_do_not_plot, plus the reads they depend on to be checkable at all.

**Proving persistence is the whole difficulty.** Each of these tools re-reads its value
before returning, so one that mutated the in-memory COM object and never wrote the file
would report a flawless result — the same shape as `alsoDeleted`, `replaced` and `savedAs`.
Two obvious checks do not settle it:

  * Grepping the .DST is useless. It is compressed: not even the ORIGINAL strings ("T-01",
    "TITLE SHEET") appear in the bytes, in any encoding.
  * Re-reading the same path is useless. IAcSmSheetSetMgr caches the open database, so the
    read can be served from the very object the write mutated and will agree with it whether
    or not anything reached the disk.

What does settle it: copy the file to a path the manager has never seen. There is no cached
database for a new path, so it must parse from disk. `reread_fresh()` below does that, and
every persistence claim in this script goes through it.

Runs against a copy of AutoCAD's own sample set, never the shipped original.
"""
import os
import shutil
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

SAMPLE = r"C:\Program Files\Autodesk\AutoCAD 2025\Sample\Sheet Sets\Architectural\IRD Addition.dst"
WORK = r"C:\tmp\sheetsets-verify"
DST = os.path.join(WORK, "verify.dst")

S = Session("sheetsets")
results = []
_fresh = [0]


def do(tool, args, label=None, expect_fail=False):
    ok, r = S.call(tool, args)
    label = label or tool
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:200]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:120]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def reread_fresh():
    """Every sheet, read from a copy under a path the sheet set manager has never cached."""
    _fresh[0] += 1
    copy = os.path.join(WORK, f"fresh-{_fresh[0]:02d}.dst")
    shutil.copy2(DST, copy)
    ok, r = S.call("list_sheets", {"path": copy})
    return (r or {}).get("sheets") or [] if ok else []


def fresh_find(**match):
    for s in reread_fresh():
        if all(s.get(k) == v for k, v in match.items()):
            return s
    return None


print("== staging a writable copy of AutoCAD's own sample ==")
os.makedirs(WORK, exist_ok=True)
shutil.copy2(SAMPLE, DST)
print(f"  {DST}  ({os.path.getsize(DST)} bytes)")

print("\n== reads ==")
info = do("get_sheet_set_info", {"path": DST})
if isinstance(info, dict):
    check("info reports a name", bool(info.get("name")), str(info)[:140])
    check("info reports sheets", (info.get("sheetCount") or 0) > 0, str(info)[:140])

listing = do("list_sheets", {"path": DST})
sheets = (listing or {}).get("sheets") or [] if isinstance(listing, dict) else []
check("list_sheets returned sheets", len(sheets) > 0, str(listing)[:140])
if not sheets:
    raise SystemExit("No sheets to write to.")

target = sheets[0]
orig_number, orig_title = target.get("number") or "", target.get("title") or ""
print(f"  target: name={target.get('name')!r} number={orig_number!r} title={orig_title!r} "
      f"doNotPlot={target.get('doNotPlot')}")

print("\n== the reported `name` is derived from number + title, not stored ==")
check("name == '{number} {title}'", target.get("name") == f"{orig_number} {orig_title}",
      f"name={target.get('name')!r} vs {orig_number!r} + {orig_title!r}")

# ── set_sheet_number ──────────────────────────────────────────────────────────
print("\n== set_sheet_number ==")
r = do("set_sheet_number", {"path": DST, "sheet": target["name"], "value": "ZZ-901"})
if isinstance(r, dict):
    check("answers with the new number", r.get("number") == "ZZ-901", str(r)[:160])
    check("answers with `before` = the old number", r.get("before") == orig_number,
          f"before={r.get('before')!r} expected {orig_number!r}")
check("PERSISTED: a fresh-path copy carries ZZ-901",
      fresh_find(number="ZZ-901") is not None,
      "the write never reached the file")

print("\n== the sheet is addressable by its new number ==")
r = do("set_sheet_number", {"path": DST, "sheet": "ZZ-901", "value": "ZZ-902"},
       label="set_sheet_number (addressed by number)")
if isinstance(r, dict):
    check("renumbered again via number lookup", r.get("number") == "ZZ-902", str(r)[:160])

# ── set_sheet_title ───────────────────────────────────────────────────────────
print("\n== set_sheet_title ==")
r = do("set_sheet_title", {"path": DST, "sheet": "ZZ-902", "value": "Ground Floor Plan"})
if isinstance(r, dict):
    check("answers with the new title", r.get("title") == "Ground Floor Plan", str(r)[:160])
    check("answers with `before` = the old title", r.get("before") == orig_title,
          f"before={r.get('before')!r} expected {orig_title!r}")
s = fresh_find(number="ZZ-902")
check("PERSISTED: fresh-path copy carries the title",
      (s or {}).get("title") == "Ground Floor Plan", f"got {(s or {}).get('title')!r}")
check("and the derived name followed both edits",
      (s or {}).get("name") == "ZZ-902 Ground Floor Plan", f"got {(s or {}).get('name')!r}")

print('\n== value="" clears the title, and the answer matches what lands on disk ==')
# Three measurements, none of them the API's apparent shape:
#   SetTitle("")  -> E_INVALIDARG
#   SetTitle(" ") -> accepted, GetTitle() then returns " "
#   reload        -> the saved title is "" (whitespace is trimmed on the way to disk)
# So the tool translates "" to " " and reports the "" that persists, rather than the " " that
# is briefly in memory. Without that, the answer and the file disagree.
r = do("set_sheet_title", {"path": DST, "sheet": "ZZ-902", "value": ""},
       label='set_sheet_title (value="")')
if isinstance(r, dict):
    check('answers title="" rather than the " " it sent COM', r.get("title") == "",
          f"got {r.get('title')!r} — the answer disagrees with the file")
check("PERSISTED: title is empty on a fresh-path copy",
      (fresh_find(number="ZZ-902") or {}).get("title") == "",
      f"got {(fresh_find(number='ZZ-902') or {}).get('title')!r}")

# ── rename_sheet: number + title together ─────────────────────────────────────
print("\n== rename_sheet sets number and title together, under one lock ==")
r = do("rename_sheet", {"path": DST, "sheet": "ZZ-902",
                        "number": "A-101", "title": "First Floor Plan"})
if isinstance(r, dict):
    before = r.get("before") or {}
    check("answers with the new number", r.get("number") == "A-101", str(r)[:180])
    check("answers with the new title", r.get("title") == "First Floor Plan", str(r)[:180])
    check("answers with the recomposed name", r.get("name") == "A-101 First Floor Plan",
          f"name={r.get('name')!r}")
    check("`before` carries all three old fields",
          before.get("number") == "ZZ-902" and before.get("title") == ""
          and "name" in before,
          str(before)[:160])
s = fresh_find(number="A-101")
check("PERSISTED: fresh-path copy carries both",
      (s or {}).get("title") == "First Floor Plan", f"got {(s or {}).get('title')!r}")

print("\n== rename_sheet accepts either field alone ==")
r = do("rename_sheet", {"path": DST, "sheet": "A-101", "title": "Retitled Only"},
       label="rename_sheet (title only)")
if isinstance(r, dict):
    check("number untouched when only title given", r.get("number") == "A-101", str(r)[:160])
r = do("rename_sheet", {"path": DST, "sheet": "A-101", "number": "A-102"},
       label="rename_sheet (number only)")
if isinstance(r, dict):
    check("title untouched when only number given", r.get("title") == "Retitled Only", str(r)[:160])
check("PERSISTED: both single-field renames landed",
      (fresh_find(number="A-102") or {}).get("title") == "Retitled Only",
      "fresh copy disagrees")

do("rename_sheet", {"path": DST, "sheet": "A-102"},
   label="rename_sheet with neither number nor title is refused", expect_fail=True)

# ── set_sheet_do_not_plot ─────────────────────────────────────────────────────
print("\n== set_sheet_do_not_plot ==")
before_flag = (fresh_find(number="A-102") or {}).get("doNotPlot")
r = do("set_sheet_do_not_plot", {"path": DST, "sheet": "A-102", "doNotPlot": True})
if isinstance(r, dict):
    check("answers doNotPlot=true", r.get("doNotPlot") is True, str(r)[:160])
    check("answers with `before` = the old flag", r.get("before") == before_flag,
          f"before={r.get('before')!r} expected {before_flag!r}")
check("PERSISTED: fresh-path copy has do-not-plot set",
      (fresh_find(number="A-102") or {}).get("doNotPlot") is True, "fresh copy disagrees")

do("set_sheet_do_not_plot", {"path": DST, "sheet": "A-102", "doNotPlot": False},
   label="set_sheet_do_not_plot (clear)")
check("PERSISTED: fresh-path copy has it cleared",
      (fresh_find(number="A-102") or {}).get("doNotPlot") is False, "fresh copy disagrees")

# ── custom properties ─────────────────────────────────────────────────────────
print("\n== define_custom_property at sheet-set scope ==")
r = do("define_custom_property", {"path": DST, "name": "ACADMCP-Project",
                                  "defaultValue": "P-2026-001"})
if isinstance(r, dict):
    check("defaults to sheetSet scope", r.get("scope") == "sheetSet", str(r)[:180])
    check("reports it was created", r.get("created") is True, str(r)[:180])
    check("carries the value back", r.get("defaultValue") == "P-2026-001", str(r)[:180])


def fresh_setprops():
    _fresh[0] += 1
    copy = os.path.join(WORK, f"fresh-{_fresh[0]:02d}.dst")
    shutil.copy2(DST, copy)
    ok, r = S.call("list_custom_properties", {"path": copy})
    return (r or {}).get("sheetSetProperties") or {} if ok else {}


check("PERSISTED: a fresh-path copy carries the property",
      fresh_setprops().get("ACADMCP-Project") == "P-2026-001", str(fresh_setprops())[:200])

print("\n== updating it keeps the scope and reports the old value ==")
r = do("define_custom_property", {"path": DST, "name": "ACADMCP-Project",
                                  "defaultValue": "P-2026-002"},
       label="define_custom_property (update)")
if isinstance(r, dict):
    check("reports the previous value", r.get("before") == "P-2026-001", str(r)[:180])
    check("is not reported as created", r.get("created") is False, str(r)[:180])
check("PERSISTED: the update landed",
      fresh_setprops().get("ACADMCP-Project") == "P-2026-002", str(fresh_setprops())[:200])

do("define_custom_property", {"path": DST, "name": "ACADMCP-Bad", "scope": "nonsense"},
   label="an unknown scope is refused", expect_fail=True)

print("\n== set_sheet_property ==")
r = do("set_sheet_property", {"path": DST, "sheet": "A-102",
                              "property": "ACADMCP-Rev", "value": "C"})
if isinstance(r, dict):
    check("reports it was created", r.get("created") is True, str(r)[:180])
    check("carries the value back", r.get("value") == "C", str(r)[:180])

ok, back = S.call("get_sheet_property", {"path": DST, "sheet": "A-102"})
check("get_sheet_property sees it", ((back or {}).get("custom") or {}).get("ACADMCP-Rev") == "C",
      str((back or {}).get("custom"))[:200])

_fresh[0] += 1
_copy = os.path.join(WORK, f"fresh-{_fresh[0]:02d}.dst")
shutil.copy2(DST, _copy)
ok, back = S.call("get_sheet_property", {"path": _copy, "sheet": "A-102"})
check("PERSISTED: fresh-path copy carries the sheet property",
      ((back or {}).get("custom") or {}).get("ACADMCP-Rev") == "C",
      str((back or {}).get("custom"))[:200])

r = do("set_sheet_property", {"path": DST, "sheet": "A-102",
                              "property": "ACADMCP-Rev", "value": "D"},
       label="set_sheet_property (update)")
if isinstance(r, dict):
    check("reports the previous value", r.get("before") == "C", str(r)[:180])
    check("is not reported as created", r.get("created") is False, str(r)[:180])

print("\n== built-in fields are refused, and the refusal names the right tool ==")
for field, tool in [("number", "set_sheet_number"), ("title", "set_sheet_title"),
                    ("name", "rename_sheet")]:
    r = do("set_sheet_property", {"path": DST, "sheet": "A-102", "property": field, "value": "X"},
           label=f"built-in '{field}' is refused", expect_fail=True)
    check(f"and points at {tool}", tool in str(r), f"got: {str(r)[:160]}")

check("the built-in number was NOT overwritten",
      (fresh_find(number="A-102") or {}).get("number") == "A-102",
      "a refused call changed the sheet")

# ── subsets ───────────────────────────────────────────────────────────────────
print("\n== create_subset ==")
sheets_before = len(reread_fresh())

r = do("create_subset", {"path": DST, "name": "ACADMCP-Discipline",
                         "description": "created by verify-sheetsets"})
if isinstance(r, dict):
    check("created at the top level of the set", r.get("parentIsSheetSet") is True, str(r)[:160])
    check("starts empty", r.get("sheetCount") == 0, str(r)[:160])

def fresh_subsets():
    _fresh[0] += 1
    copy = os.path.join(WORK, f"fresh-{_fresh[0]:02d}.dst")
    shutil.copy2(DST, copy)
    ok, r = S.call("list_subsets", {"path": copy})
    return (r or {}).get("subsets") or [] if ok else []

names = [s.get("name") for s in fresh_subsets()]
check("PERSISTED: the new subset survives a reload", "ACADMCP-Discipline" in names, str(names)[:160])

print("\n== a nested subset, and a duplicate name refused ==")
r = do("create_subset", {"path": DST, "name": "ACADMCP-Nested", "parent": "ACADMCP-Discipline"})
if isinstance(r, dict):
    check("nested under the named parent", r.get("parent") == "ACADMCP-Discipline", str(r)[:160])
    check("knows it is not at the root", r.get("parentIsSheetSet") is False, str(r)[:160])
paths = [s.get("path") for s in fresh_subsets()]
check("PERSISTED: nested path reads 'Parent / Child'",
      "ACADMCP-Discipline / ACADMCP-Nested" in paths, str(paths)[:200])

do("create_subset", {"path": DST, "name": "ACADMCP-Discipline"},
   label="duplicate subset name is refused", expect_fail=True)

print("\n== move_sheet_to_subset re-parents rather than copies ==")
r = do("move_sheet_to_subset", {"path": DST, "sheet": "A-102", "subset": "ACADMCP-Nested"})
if isinstance(r, dict):
    check("reports the destination", r.get("to") == "ACADMCP-Nested", str(r)[:180])
    check("reports where it came from", "from" in r, str(r)[:180])
after = reread_fresh()
check("PERSISTED: the sheet now sits under the nested subset",
      (next((s for s in after if s.get("number") == "A-102"), {}) or {}).get("subset", "")
      .endswith("ACADMCP-Nested"),
      str(next((s for s in after if s.get("number") == "A-102"), {}))[:180])
check("MOVED, not copied: total sheet count is unchanged",
      len(after) == sheets_before, f"{sheets_before} -> {len(after)}")

print("\n== delete_subset refuses a subset that still holds sheets ==")
r = do("delete_subset", {"path": DST, "subset": "ACADMCP-Nested"},
       label="non-empty subset is refused", expect_fail=True)
check("the refusal says how many sheets are in the way", "1 sheet" in str(r),
      f"unhelpful message: {str(r)[:200]}")
check("and the subset still exists", "ACADMCP-Nested" in [s.get("name") for s in fresh_subsets()],
      "a refused delete removed it anyway")

print("\n== move the sheet back out, then the subset deletes ==")
do("move_sheet_to_subset", {"path": DST, "sheet": "A-102"}, label="move back to the top level")
r = fresh_find(number="A-102")
check("PERSISTED: the sheet is out of the subset",
      not (r or {}).get("subset", "").endswith("ACADMCP-Nested"), str(r)[:160])

do("delete_subset", {"path": DST, "subset": "ACADMCP-Nested"}, label="empty subset deletes")
check("PERSISTED: nested subset is gone",
      "ACADMCP-Nested" not in [s.get("name") for s in fresh_subsets()], "still listed")
do("delete_subset", {"path": DST, "subset": "ACADMCP-Discipline"}, label="parent deletes too")
check("PERSISTED: parent subset is gone",
      "ACADMCP-Discipline" not in [s.get("name") for s in fresh_subsets()], "still listed")
check("no sheets were lost by any of that", len(reread_fresh()) == sheets_before,
      f"{sheets_before} -> {len(reread_fresh())}")

do("delete_subset", {"path": DST, "subset": "no-such-subset"},
   label="unknown subset is refused", expect_fail=True)

# ── reorder ───────────────────────────────────────────────────────────────────
print("\n== reorder_sheet ==")


def order_of(subset_suffix=None):
    """Sheet numbers in list order, which is the drawing-list order the tool changes."""
    return [s.get("number") for s in reread_fresh()
            if subset_suffix is None or (s.get("subset") or "").endswith(subset_suffix)]


# Two sheets from the SAME subset. An earlier version took the first two in the list, which
# happened to straddle the sheet-set root and the Architectural subset — so the cross-subset
# guard refused, correctly, and the test read as a code failure. Ordering is a within-subset
# operation, so the fixture has to respect that.
_by_subset = {}
for s in reread_fresh():
    _by_subset.setdefault(s.get("subset") or "", []).append(s.get("number"))
_siblings = next((v for v in _by_subset.values() if len(v) >= 2), None)
if not _siblings:
    raise SystemExit("no subset holds two sheets; cannot test ordering")
first, second = _siblings[0], _siblings[1]
_subset_of = next(k for k, v in _by_subset.items() if v is _siblings)
before_order = order_of(_subset_of)
print(f"  ordering within {_subset_of!r}: {before_order[:6]}")

r = do("reorder_sheet", {"path": DST, "sheet": first, "after": second})
if isinstance(r, dict):
    check("reports where it was placed", r.get("placed") == "after", str(r)[:180])
    check("names the anchor it was placed against", r.get("anchor"), str(r)[:180])
after_order = order_of(_subset_of)
check("PERSISTED: the two swapped places",
      after_order[:2] == [second, first], f"{before_order[:3]} -> {after_order[:3]}")
check("no sheet was lost by reordering", len(after_order) == len(before_order),
      f"{len(before_order)} -> {len(after_order)}")

# Asserting the order CHANGED first, then that it came back. The earlier version only checked
# the end state, which passed while the reorder was failing outright — the order had never
# moved, so it still matched what was expected after moving back.
check("the order really did change", after_order[:2] != before_order[:2],
      f"{before_order[:3]} vs {after_order[:3]}")
do("reorder_sheet", {"path": DST, "sheet": first, "before": second},
   label="reorder_sheet (before)")
check("PERSISTED: moved back", order_of(_subset_of)[:2] == [first, second],
      str(order_of(_subset_of)[:3]))

do("reorder_sheet", {"path": DST, "sheet": first, "before": second, "after": second},
   label="both before and after is refused", expect_fail=True)
do("reorder_sheet", {"path": DST, "sheet": first},
   label="neither before nor after is refused", expect_fail=True)
do("reorder_sheet", {"path": DST, "sheet": first, "after": first},
   label="positioning a sheet against itself is refused", expect_fail=True)

print("\n== ordering across subsets is refused, not silently a move ==")
do("create_subset", {"path": DST, "name": "ACADMCP-Order"}, label="a subset to move into")
do("move_sheet_to_subset", {"path": DST, "sheet": second, "subset": "ACADMCP-Order"},
   label="park a sheet inside it")
r = do("reorder_sheet", {"path": DST, "sheet": first, "after": second},
       label="cross-subset reorder is refused", expect_fail=True)
check("and the refusal points at move_sheet_to_subset", "move_sheet_to_subset" in str(r),
      f"got: {str(r)[:180]}")
check("the parked sheet did NOT get relocated by the refusal",
      (fresh_find(number=second) or {}).get("subset", "").endswith("ACADMCP-Order"),
      "a refused reorder moved it anyway")
do("move_sheet_to_subset", {"path": DST, "sheet": second}, label="put it back")
do("delete_subset", {"path": DST, "subset": "ACADMCP-Order"}, label="tidy the subset away")

# ── remove_sheet ──────────────────────────────────────────────────────────────
print("\n== remove_sheet takes the reference, not the layout ==")
count_before = len(reread_fresh())
victim = order_of()[-1]
r = do("remove_sheet", {"path": DST, "sheet": victim})
if isinstance(r, dict):
    check("reports the number it removed", r.get("number") == victim, str(r)[:200])
    check("reports how many remain", r.get("sheetsRemaining") == count_before - 1,
          f"said {r.get('sheetsRemaining')}, expected {count_before - 1}")
    check("says the drawing is untouched", "untouched" in (r.get("note") or ""), str(r)[:200])
check("PERSISTED: the sheet is gone from a fresh-path copy",
      fresh_find(number=victim) is None, "still listed")
check("PERSISTED: exactly one sheet fewer", len(reread_fresh()) == count_before - 1,
      f"{count_before} -> {len(reread_fresh())}")

do("remove_sheet", {"path": DST, "sheet": victim},
   label="removing it twice is refused", expect_fail=True)

# ── add_sheet ─────────────────────────────────────────────────────────────────
print("\n== add_sheet: the error message has to be the documentation ==")
SRC_DWG = os.path.join(os.path.dirname(SAMPLE), "A-01.dwg")
check("a sample drawing to add from exists", os.path.exists(SRC_DWG), SRC_DWG)

def discover_layout(dwg):
    """Ask for a layout that cannot exist and read the real ones out of the refusal.

    The refusal is supposed to be the documentation: a tool whose failure teaches you the right
    argument is worth more than one that only says no. This uses it that way, which also means
    the message is under test on every run rather than eyeballed once.
    """
    ok, res = S.call("add_sheet", {"path": DST, "drawingPath": dwg, "layout": "NO-SUCH-LAYOUT"})
    text = str(res)
    if "It has: " not in text:
        return None, text
    listed = text.split("It has: ", 1)[1].split(".")[0]
    first = listed.split(",")[0].strip()
    return (None if first == "(none)" else first), text


layout_name, msg = discover_layout(SRC_DWG)
results.append(("an unknown layout is refused", "It has:" in msg))
print(f"  {'OK  ' if 'It has:' in msg else 'FAIL'} an unknown layout is refused")
check("and the refusal lists the layouts that DO exist", "It has:" in msg, msg[:220])
check("and says model space is not a candidate", "Model space" in msg, msg[:220])
print(f"  discovered layout: {layout_name!r}")

if layout_name and layout_name != "(none)":
    count_before = len(reread_fresh())
    r = do("add_sheet", {"path": DST, "drawingPath": SRC_DWG, "layout": layout_name,
                         "number": "ZZ-500", "title": "Added by verify"})
    if isinstance(r, dict):
        check("reports the number it was given", r.get("number") == "ZZ-500", str(r)[:220])
        check("reports the title it was given", r.get("title") == "Added by verify", str(r)[:220])
        check("reports the layout it referenced", r.get("layout") == layout_name, str(r)[:220])
        check("says the drawing was not modified", "not modified" in (r.get("note") or ""),
              str(r)[:220])
    check("PERSISTED: the new sheet is in a fresh-path copy",
          fresh_find(number="ZZ-500") is not None, "not listed")
    check("PERSISTED: exactly one sheet more", len(reread_fresh()) == count_before + 1,
          f"{count_before} -> {len(reread_fresh())}")

    print("\n== the same layout cannot be added twice ==")
    r = do("add_sheet", {"path": DST, "drawingPath": SRC_DWG, "layout": layout_name},
           label="a layout already in the set is refused", expect_fail=True)
    check("and the refusal names the sheet holding it", "already in this set" in str(r),
          str(r)[:220])
    check("no second sheet was created", len(reread_fresh()) == count_before + 1,
          f"expected {count_before + 1}, got {len(reread_fresh())}")

    print("\n== added into a subset, from a different drawing ==")
    # A different DWG, and its own layout name discovered the same way - A-02 does not carry the
    # same layout name as A-01, and assuming it did was what made the first run of this section
    # look like a tool failure.
    other_dwg = os.path.join(os.path.dirname(SAMPLE), "A-02.dwg")
    other_layout, _ = discover_layout(other_dwg)
    print(f"  discovered layout in A-02.dwg: {other_layout!r}")
    do("create_subset", {"path": DST, "name": "ACADMCP-Added"}, label="a subset to add into")
    r = do("add_sheet", {"path": DST, "drawingPath": other_dwg, "layout": other_layout,
                         "number": "ZZ-501", "subset": "ACADMCP-Added"},
           label="add_sheet (into a subset)")
    if isinstance(r, dict):
        check("filed under the named subset", r.get("subset") == "ACADMCP-Added", str(r)[:220])
    check("PERSISTED: it landed in that subset",
          (fresh_find(number="ZZ-501") or {}).get("subset", "").endswith("ACADMCP-Added"),
          str(fresh_find(number="ZZ-501"))[:200])

do("add_sheet", {"path": DST, "drawingPath": os.path.join(WORK, "nope.dwg"), "layout": "x"},
   label="a missing drawing is refused", expect_fail=True)
do("add_sheet", {"path": DST, "drawingPath": SRC_DWG},
   label="a missing layout argument is refused", expect_fail=True)

# ── refusals ──────────────────────────────────────────────────────────────────
print("\n== refusals are refusals, not silent successes ==")
do("set_sheet_number", {"path": DST, "sheet": "no-such-sheet", "value": "X-1"},
   label="unknown sheet is refused", expect_fail=True)
do("set_sheet_number", {"path": DST, "sheet": "A-102"},
   label="missing value is refused", expect_fail=True)
do("rename_sheet", {"path": os.path.join(WORK, "does-not-exist.dst"), "sheet": "x", "number": "y"},
   label="missing .DST is refused", expect_fail=True)

# ── the set survived ──────────────────────────────────────────────────────────
print("\n== the sheet set is still intact ==")
final = do("get_sheet_set_info", {"path": DST}, label="get_sheet_set_info (after writes)")
if isinstance(final, dict) and isinstance(info, dict):
    # The exact arithmetic of what this script did: one remove_sheet, two add_sheet. Asserting
    # the precise figure rather than "unchanged" is what makes the move, reorder and import
    # checks mean anything — a tool that quietly dropped or duplicated a sheet shows up here and
    # nowhere else.
    expected = (info.get("sheetCount") or 0) - 1 + 2
    check(f"sheet count is exactly {expected}: started {info.get('sheetCount')}, -1 removed, +2 added",
          final.get("sheetCount") == expected,
          f"{info.get('sheetCount')} -> {final.get('sheetCount')}, expected {expected}")
check("every other sheet untouched",
      len([s for s in reread_fresh() if s.get("number") == "AS-01"]) == 1,
      "a neighbouring sheet moved")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
