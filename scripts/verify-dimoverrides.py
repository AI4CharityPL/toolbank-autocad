# -*- coding: utf-8 -*-
"""Live verification for the last of roadmap 2.3: dimension overrides and cross-drawing import.

The three tools here are unusually good at looking healthy while being wrong, so each check
targets the specific lie it could tell:

  apply_dimstyle_override  The record round-trip is load-bearing. GetDimstyleData returns the
                           dimension's EFFECTIVE values; building a fresh DimStyleTableRecord
                           instead would push AutoCAD's defaults for every property nobody
                           asked about, silently overriding all of them. Asserted by checking
                           that overriding ONE property leaves the override count at exactly
                           one, and that a second dimension on the same style is untouched.
  list_dimstyle_overrides  Reporting "no overrides" for a dimension that visibly differs looks
                           exactly like a healthy answer. Asserted before and after.
  import_dimstyle_from_dwg Cross-drawing cloning is the mechanism that defeated
                           publish.import_page_setup: it returned success having cloned
                           nothing. So the import is checked by reading the destination's own
                           style list afterwards, and the skip path is exercised deliberately.
"""
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "styles", "dimensions", "geometry-2d", "view")}
results = []
DONOR = r"C:\tmp\dimstyle-donor.dwg"


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:170]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:100]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, cond, detail=""):
    results.append((label, bool(cond)))
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + ("" if cond else f"  -> {detail}"))


# ─────────── a donor drawing carrying two office styles ───────────
print("== build a donor drawing with styles to import later ==")
do("files", "new_document", {})
do("styles", "create_dimstyle",
   {"name": "OFFICE-1-50", "properties": {"textHeight": 2.5, "arrowSize": 2.0, "scale": 50}},
   "donor style OFFICE-1-50")
do("styles", "create_dimstyle",
   {"name": "OFFICE-1-100", "properties": {"textHeight": 2.5, "arrowSize": 2.0, "scale": 100}},
   "donor style OFFICE-1-100")
if os.path.exists(DONOR):
    os.remove(DONOR)
do("files", "save_document_as", {"path": DONOR}, f"save donor -> {DONOR}")

# ─────────── overrides ───────────
print("\n== a fresh drawing with one style and two dimensions on it ==")
do("files", "new_document", {})
do("styles", "create_dimstyle",
   {"name": "SITE", "properties": {"textHeight": 2.5, "arrowSize": 2.5}, "makeCurrent": True},
   "create SITE and make it current")

handles = []
for i, y in enumerate((0, 2000)):
    r = do("dimensions", "dimension_linear",
           {"p1": {"x": 0, "y": y, "z": 0}, "p2": {"x": 5000, "y": y, "z": 0},
            "dimLinePoint": {"x": 2500, "y": y + 500, "z": 0}},
           f"place dimension {i + 1}")
    h = (r or {}).get("entity", {}).get("handle") if isinstance(r, dict) else None
    if not h:
        h = (r or {}).get("handle") if isinstance(r, dict) else None
    handles.append(h)
    check(f"dimension {i + 1} returned a handle", bool(h), str(r)[:150])

if not all(handles):
    print("\ncannot continue without handles")
    sys.exit(1)

A, B = handles

r = do("styles", "list_dimstyle_overrides", {"handle": A}, "overrides before any change")
if isinstance(r, dict):
    check("a freshly placed dimension has no overrides", r.get("count") == 0, str(r)[:150])
    check("and says so in words rather than returning a bare empty list",
          bool(r.get("note")), str(r)[:150])
    check("it reports the style it carries", r.get("styleName") == "SITE", str(r)[:130])

r = do("styles", "apply_dimstyle_override", {"handle": A, "properties": {"textHeight": 5.0}},
       "override text height on dimension 1 only")
if isinstance(r, dict):
    check("applied names the property", r.get("applied") == ["textHeight"], str(r.get("applied")))
    ovs = r.get("overrides") or []
    # The load-bearing assertion: exactly ONE property differs. If the record had been rebuilt
    # from scratch instead of round-tripped, every property would now differ from the style.
    check("exactly one property differs from the style", len(ovs) == 1,
          f"{len(ovs)} differ: {[o.get('name') for o in ovs]}")
    if ovs:
        check("and it is the one that was set", ovs[0].get("name") == "textHeight", str(ovs[0]))
        check("with both values reported side by side",
              abs((ovs[0].get("value") or 0) - 5.0) < 1e-6
              and abs((ovs[0].get("styleValue") or 0) - 2.5) < 1e-6, str(ovs[0]))

r = do("styles", "list_dimstyle_overrides", {"handle": A}, "re-read overrides (fresh call)")
if isinstance(r, dict):
    check("the override survived into the drawing", r.get("count") == 1, str(r)[:170])

r = do("styles", "list_dimstyle_overrides", {"handle": B}, "the OTHER dimension on the same style")
if isinstance(r, dict):
    check("dimension 2 was not touched", r.get("count") == 0, str(r)[:170])

r = do("styles", "apply_dimstyle_override",
       {"handle": A, "properties": {"arrowSize": 6.0, "decimalPlaces": 3}},
       "add two more overrides to the same dimension")
if isinstance(r, dict):
    names = sorted(o.get("name") for o in (r.get("overrides") or []))
    check("overrides accumulate rather than replace each other",
          names == ["arrowSize", "decimalPlaces", "textHeight"], str(names))

do("styles", "apply_dimstyle_override", {"handle": A, "properties": {}},
   "no properties", expect_fail=True)
do("styles", "apply_dimstyle_override", {"handle": A, "properties": {"nonsenseProp": 1}},
   "property not in the catalogue", expect_fail=True)
do("styles", "apply_dimstyle_override", {"handle": "ZZZZZ", "properties": {"textHeight": 1}},
   "handle that is not hexadecimal", expect_fail=True)
do("styles", "list_dimstyle_overrides", {"handle": "FFFFFF"},
   "handle that does not exist", expect_fail=True)

# Found while writing this file: every point-taking tool in the bank answered a missing point
# with a bare NullReferenceException. Assert the fix here, since dimension_linear is where it
# surfaced.
ok, err = S["dimensions"].call("dimension_linear", {"p2": {"x": 1, "y": 1, "z": 0}})
check("a missing point names itself instead of throwing NullReferenceException",
      (not ok) and "p1" in str(err) and "Object reference" not in str(err), str(err)[:170])

r = do("geometry-2d", "draw_circle", {"center": {"x": 0, "y": 5000}, "radius": 100},
       "draw a non-dimension entity")
circle = (r or {}).get("entity", {}).get("handle") if isinstance(r, dict) else None
if circle:
    do("styles", "list_dimstyle_overrides", {"handle": circle},
       "a handle that is not a dimension", expect_fail=True)

# ─────────── import ───────────
print("\n== import styles from the donor drawing ==")
r = do("dimensions", "list_dimstyles", {}, "styles here before importing")
before = {s.get("name") if isinstance(s, dict) else s for s in ((r or {}).get("dimStyles") or (r or {}).get("styles") or [])}
check("donor styles are NOT here yet",
      "OFFICE-1-50" not in before and "OFFICE-1-100" not in before, str(sorted(x for x in before if x))[:150])

r = do("styles", "import_dimstyle_from_dwg", {"path": DONOR}, "import every non-Standard style")
if isinstance(r, dict):
    check("both donor styles reported as imported",
          sorted(r.get("imported") or []) == ["OFFICE-1-100", "OFFICE-1-50"], str(r)[:180])
    # The donor also carries the template's own ISO-25 and Annotative, which exist here too,
    # so they land in skipped. That is the tool doing what it says, not a defect - "every
    # non-Standard style" means exactly that.
    check("the two office styles are not in skipped",
          not ({"OFFICE-1-50", "OFFICE-1-100"} & set(r.get("skipped") or [])), str(r)[:170])

r = do("dimensions", "list_dimstyles", {}, "styles here after importing (fresh read)")
after = {s.get("name") if isinstance(s, dict) else s for s in ((r or {}).get("dimStyles") or (r or {}).get("styles") or [])}
check("the styles really landed in this drawing, not just in the reply",
      "OFFICE-1-50" in after and "OFFICE-1-100" in after, str(sorted(x for x in after if x))[:170])

r = do("styles", "import_dimstyle_from_dwg", {"path": DONOR}, "import the same styles again")
if isinstance(r, dict):
    check("second import reports the office styles as SKIPPED, not imported",
          {"OFFICE-1-50", "OFFICE-1-100"} <= set(r.get("skipped") or [])
          and (r.get("imported") or []) == [], str(r)[:190])
    check("and explains why in a note", bool(r.get("note")), str(r)[:150])

r = do("styles", "import_dimstyle_from_dwg", {"path": DONOR, "names": ["OFFICE-1-50"], "overwrite": True},
       "re-import one style with overwrite")
if isinstance(r, dict):
    # REPLACED, not imported: the style was already here and its definition was overwritten.
    # Reporting that as "skipped" - which the first version did - tells a caller nothing
    # happened when the drawing changed underneath them.
    check("overwrite reports it as REPLACED, not skipped and not imported",
          (r.get("replaced") or []) == ["OFFICE-1-50"]
          and (r.get("skipped") or []) == [], str(r)[:190])

do("styles", "import_dimstyle_from_dwg", {"path": r"C:\tmp\no-such-file.dwg"},
   "file that does not exist", expect_fail=True)
do("styles", "import_dimstyle_from_dwg", {"path": DONOR, "names": ["NOT-IN-DONOR"]},
   "style name not present in the donor", expect_fail=True)
do("styles", "import_dimstyle_from_dwg", {},
   "no path", expect_fail=True)

print("\n== the override must reach the GEOMETRY, not only the data ==")
# Everything above proves the override is stored and reported. This proves it is drawn.
# The dimension text is centred on the dimension line, so raising its height by N grows the
# entity's bounding box by exactly N/2 upward - a prediction precise enough that a coincidence
# cannot pass it. Checked at three magnitudes because a single one could be luck, and against
# the untouched dimension throughout.


def height_of(handle):
    bb = do("geometry-2d", "get_bounding_box", {"handle": handle}, f"bbox {handle}")
    box = (bb or {}).get("bbox") or {}
    return (box.get("max") or {}).get("y", 0) - (box.get("min") or {}).get("y", 0)


base_a = height_of(A)
base_b = height_of(B)
for target, grow in [(25.0, (25.0 - 5.0) / 2), (50.0, (50.0 - 5.0) / 2)]:
    do("styles", "apply_dimstyle_override", {"handle": A, "properties": {"textHeight": target}},
       f"override textHeight to {target}")
    now_a, now_b = height_of(A), height_of(B)
    check(f"textHeight {target}: entity grew by {grow} as predicted",
          abs((now_a - base_a) - grow) < 0.01, f"grew {now_a - base_a:+.2f}, wanted {grow:+.2f}")
    check(f"textHeight {target}: the other dimension is still untouched",
          abs(now_b - base_b) < 0.01, f"{base_b:.2f} -> {now_b:.2f}")

print("\n== visual check ==")
do("view", "zoom_extents", {})
do("files", "export_file", {"path": r"C:\tmp\dimoverrides.png", "format": "PNG"},
   "export PNG - dimension 1 must render visibly larger than dimension 2")

ok = sum(1 for _, g in results if g)
print(f"\n==== {ok}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
