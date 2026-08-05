# -*- coding: utf-8 -*-
"""Live verification for roadmap 2.3 multiline styles + draw_mline pulled forward from 3.1.

Four tools are under test: create_mlinestyle, modify_mlinestyle, list_mlinestyles and
draw_mline. The last one is here because without it the first three are unverifiable — a
style nothing can draw with cannot be looked at, and this project's whole method is that the
return code is not the evidence.

So every claim below is checked twice: once by reading the value back, and once by putting
it on screen and exporting a PNG. A tool that returns healthy JSON and draws the wrong thing
is the failure mode that has produced most of this bank's real defects.
"""
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "styles", "geometry-2d", "view", "layers")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    mark = "OK  " if good else "FAIL"
    detail = "" if good else f"  -> {str(r)[:180]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:110]})"
    print(f"  {mark} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


print("== fresh drawing ==")
do("files", "new_document", {})

print("\n== create: a 200mm wall style ==")
r = do("styles", "create_mlinestyle", {
    "name": "ACADMCP-WALL-200",
    "elements": [{"offset": 100, "colorIndex": 1}, {"offset": -100, "colorIndex": 1}],
    "description": "200mm wall, two faces",
    "showMiters": True,
})
if isinstance(r, dict):
    ms = r.get("mlineStyle") or {}
    check("created flag is true", r.get("created") is True, str(r)[:120])
    check("totalWidth reads back as 200", ms.get("totalWidth") == 200, f"got {ms.get('totalWidth')}")
    check("two elements stored", len(ms.get("elements") or []) == 2, str(ms.get("elements"))[:120])
    check("outermost element first (+100)",
          (ms.get("elements") or [{}])[0].get("offset") == 100, str(ms.get("elements"))[:120])
    check("showMiters survived the write", ms.get("showMiters") is True, str(ms)[:120])
    # The mitre angle is the reason this file exists in its current form. A freshly built
    # MlineStyle carries 0 degrees, which makes an OPEN multiline run to roughly offset x 10,000
    # units while every returned value still reads correct. Assert the default explicitly.
    check("mitre angles default to AutoCAD's 90, not the constructor's 0",
          ms.get("startAngle") == 90 and ms.get("endAngle") == 90,
          f"start={ms.get('startAngle')} end={ms.get('endAngle')}")

print("\n== list: does it come back? ==")
r = do("styles", "list_mlinestyles", {})
if isinstance(r, dict):
    names = [s.get("name") for s in (r.get("styles") or [])]
    lower = [n.lower() for n in names if n]
    check("the default style and the new one are both listed",
          "acadmcp-wall-200" in lower and "standard" in lower, str(names)[:150])
    mine = next((s for s in r["styles"] if s.get("name") == "ACADMCP-WALL-200"), {})
    check("inUse is false before anything is drawn", mine.get("inUse") is False, str(mine)[:120])

print("\n== draw: four walls forming a room ==")
room = [{"x": 0, "y": 0}, {"x": 6000, "y": 0}, {"x": 6000, "y": 4000}, {"x": 0, "y": 4000}]
do("geometry-2d", "draw_mline",
   {"vertices": room, "style": "ACADMCP-WALL-200", "closed": True, "justification": "zero"},
   "draw_mline closed room, zero justification")

print("\n== justification must move the wall relative to the points, not just be accepted ==")
# Three identical runs whose vertices all sit on the same y. A 200-wide style means the bounding
# box tells you unambiguously which of the style's parallel lines the vertices landed on, which
# is the only thing that makes the argument's documentation checkable rather than decorative.
for i, (just, want_lo, want_hi) in enumerate([
    ("top", -200, 0),      # vertices ON the top line, wall hangs below
    ("zero", -100, 100),   # centred
    ("bottom", 0, 200),    # vertices ON the bottom line, wall sits above
]):
    y = 6000 + i * 1000
    rr = do("geometry-2d", "draw_mline", {
        "vertices": [{"x": 0, "y": y}, {"x": 6000, "y": y}],
        "style": "ACADMCP-WALL-200", "justification": just,
    }, f"draw_mline justification={just}")
    handle = (rr or {}).get("entity", {}).get("handle") if isinstance(rr, dict) else None
    if not handle:
        check(f"justification={just} offset", False, "no handle")
        continue
    bb = do("geometry-2d", "get_bounding_box", {"handle": handle}, f"bbox justification={just}")
    box = (bb or {}).get("bbox") or {}
    lo = round((box.get("min") or {}).get("y", 0) - y)
    hi = round((box.get("max") or {}).get("y", 0) - y)
    check(f"justification={just} puts the wall at [{want_lo},{want_hi}] around the points",
          abs(lo - want_lo) <= 1 and abs(hi - want_hi) <= 1, f"got [{lo},{hi}]")

print("\n== scale multiplies the offsets ==")
do("geometry-2d", "draw_mline", {
    "vertices": [{"x": 0, "y": 10000}, {"x": 6000, "y": 10000}],
    "style": "ACADMCP-WALL-200", "scale": 2.0,
}, "draw_mline scale=2 (should read 400 wide)")

print("\n== inUse must now be true, and redefinition must be refused ==")
r = do("styles", "list_mlinestyles", {})
if isinstance(r, dict):
    mine = next((s for s in r.get("styles") or [] if s.get("name") == "ACADMCP-WALL-200"), {})
    check("inUse flipped to true once entities reference it", mine.get("inUse") is True, str(mine)[:120])

do("styles", "modify_mlinestyle",
   {"name": "ACADMCP-WALL-200", "elements": [{"offset": 60}, {"offset": -60}]},
   "modify refused while in use", expect_fail=True)
do("styles", "create_mlinestyle",
   {"name": "ACADMCP-WALL-200", "elements": [{"offset": 60}, {"offset": -60}], "overwrite": True},
   "overwrite refused while in use", expect_fail=True)

print("\n== modify a style nothing uses ==")
do("styles", "create_mlinestyle", {
    "name": "ACADMCP-PARTITION-120",
    "elements": [{"offset": 60}, {"offset": -60}],
}, "create partition style")
r = do("styles", "modify_mlinestyle", {
    "name": "ACADMCP-PARTITION-120",
    "elements": [{"offset": 75}, {"offset": 0, "colorIndex": 3}, {"offset": -75}],
    "description": "150mm partition with a centre line",
    "startCap": "round", "endCap": "square",
})
if isinstance(r, dict):
    ms = r.get("mlineStyle") or {}
    check("element list REPLACED, not merged (3 elements)",
          len(ms.get("elements") or []) == 3, str(ms.get("elements"))[:150])
    check("totalWidth now 150", ms.get("totalWidth") == 150, f"got {ms.get('totalWidth')}")
    check("startCap stored as round", ms.get("startCap") == "round", str(ms)[:120])
    check("endCap stored as square", ms.get("endCap") == "square", str(ms)[:120])

do("geometry-2d", "draw_mline", {
    "vertices": [{"x": 0, "y": 12000}, {"x": 6000, "y": 12000}],
    "style": "ACADMCP-PARTITION-120",
}, "draw partition to show caps and centre line")

print("\n== arguments that must be refused, not silently accepted ==")
do("geometry-2d", "draw_mline",
   {"vertices": [{"x": 0, "y": 0}, {"x": 1, "y": 1}], "style": "NO-SUCH-STYLE"},
   "unknown style name", expect_fail=True)
do("geometry-2d", "draw_mline",
   {"vertices": [{"x": 0, "y": 0}], "style": "ACADMCP-WALL-200"},
   "single vertex", expect_fail=True)
do("geometry-2d", "draw_mline",
   {"vertices": [{"x": 0, "y": 0}, {"x": 1, "y": 1}], "justification": "sideways"},
   "nonsense justification", expect_fail=True)
do("styles", "create_mlinestyle",
   {"name": "ACADMCP-EMPTY", "elements": []},
   "empty element list", expect_fail=True)
do("styles", "create_mlinestyle",
   {"name": "ACADMCP-BADANGLE", "elements": [{"offset": 10}], "startAngle": 5},
   "mitre angle below 10 degrees", expect_fail=True)
do("styles", "create_mlinestyle",
   {"name": "ACADMCP-BADCAP", "elements": [{"offset": 10}], "startCap": "wobbly"},
   "unknown cap name", expect_fail=True)
do("styles", "create_mlinestyle",
   {"name": "ACADMCP-BADLT", "elements": [{"offset": 10, "linetype": "NOT-LOADED"}]},
   "linetype that is not loaded", expect_fail=True)
do("styles", "modify_mlinestyle",
   {"name": "NO-SUCH-STYLE", "description": "x"},
   "modify a style that does not exist", expect_fail=True)

print("\n== geometry must be the size it claims, not a million units wide ==")
# This block is the one that would have caught the mitre-angle defect without a human looking
# at a PNG. Every property read back correctly while an open 6m wall had a bounding box two
# million units wide, because nothing asserted the SIZE of what was drawn.
for label, args, want_w, want_h in [
    ("closed room 6000x4000, 200 wall",
     {"vertices": room, "style": "ACADMCP-WALL-200", "closed": True}, 6200, 4200),
    ("open run 6000 long, 200 wall",
     {"vertices": [{"x": 0, "y": 20000}, {"x": 6000, "y": 20000}], "style": "ACADMCP-WALL-200"},
     6000, 200),
    ("open run at scale 2 is twice as wide",
     {"vertices": [{"x": 0, "y": 22000}, {"x": 6000, "y": 22000}], "style": "ACADMCP-WALL-200",
      "scale": 2.0}, 6000, 400),
]:
    rr = do("geometry-2d", "draw_mline", args, f"draw for measurement: {label}")
    handle = (rr or {}).get("entity", {}).get("handle") if isinstance(rr, dict) else None
    if not handle:
        check(f"bbox of {label}", False, "no handle returned")
        continue
    bb = do("geometry-2d", "get_bounding_box", {"handle": handle}, f"bbox: {label}")
    box = (bb or {}).get("bbox") or {}
    mn, mx = box.get("min") or {}, box.get("max") or {}
    w = round(mx.get("x", 0) - mn.get("x", 0))
    h = round(mx.get("y", 0) - mn.get("y", 0))
    check(f"{label}: width {want_w}", abs(w - want_w) <= 1, f"got {w}")
    check(f"{label}: height {want_h}", abs(h - want_h) <= 1, f"got {h}")

print("\n== visual check ==")
do("view", "zoom_extents", {})
png = r"C:\tmp\mline-verify.png"
do("files", "export_file", {"path": png, "format": "PNG"}, f"export PNG -> {png}")

ok = sum(1 for _, g in results if g)
print(f"\n==== {ok}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
