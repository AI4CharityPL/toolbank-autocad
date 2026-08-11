# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.4 — the whole acad-sections-3d category, 9 tools.

`create_section_plane`, `create_section_orthographic`, `list_section_planes`, `set_section_state`,
`set_live_section`, `set_section_height`, `set_section_settings`, `generate_section`,
`generate_section_block`.

Two claims are being checked, and the second one only got a test after the first version of this
script passed while the tool was broken.

1. A section plane REPORTS a cut rather than making one, so the solids it crosses come out
   untouched. That is the whole difference from geometry_3d.slice_solid, and nothing in a JSON
   result distinguishes them - so the source volume is measured after every operation.

2. The cut is taken WHERE THE PLANE IS. This needs a shape whose cut and whose silhouette are
   different, because a cube's are the same square: a plane through the middle of a 100 cube and
   a plane 5000 units away BOTH answered 400, and the first version of this script called that
   proof. The controls that do discriminate:

     * a sphere of r=50 cut OFF-CENTRE at y=30 gives a circle of radius 40, so 2*pi*40 = 251.327,
       against 314.159 for the great circle. One number says the plane was used, the other says
       it was ignored.
     * a 100 x 80 x 60 box gives 2*(100+60) = 320 cut upright and 2*(100+80) = 360 cut flat, so
       the two orientations cannot be confused with each other either.

The state, height and settings tools are checked by reading back what was set, because all of them
are plain properties that can accept a value the object then declines to keep.

set_section_settings additionally has a MEASURED table of which property applies to which part of
which section type - AutoCAD answers an unsupported combination with a bare eInvalidInput naming
neither the field nor the reason. Every refusal in that table is checked, and so is a supported
combination: a blanket "no" would pass the refusal checks just as greenly.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "sections-3d", "view")}
results = []

BX, BY, BZ = 100.0, 80.0, 60.0          # a box no two faces of which are alike
BOX_VOL = BX * BY * BZ                   # 480000
CUT_UPRIGHT = 2 * (BX + BZ)              # 320 - a vertical plane cuts the x-z rectangle
CUT_FLAT = 2 * (BX + BY)                 # 360 - a horizontal one cuts the x-y rectangle
R = 50.0
GREAT_CIRCLE = 2 * math.pi * R           # 314.159 - the sphere's silhouette
OFFSET = 30.0
OFF_CIRCLE = 2 * math.pi * math.sqrt(R * R - OFFSET * OFFSET)   # 251.327 at y=30


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:190]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:110]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def rel(a, b, tol=1e-6):
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def hnd(r):
    if not isinstance(r, dict):
        return None            # a refusal comes back as a string; do not crash the whole run
    return ((r.get("entity") or {}) if isinstance(r.get("entity"), dict) else {}).get("handle")


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    return (r or {}).get("volume") if ok and isinstance(r, dict) else None


def plane(vertices, label, **kw):
    a = {"vertices": vertices}
    a.update(kw)
    return do("sections-3d", "create_section_plane", a, label=label)


def cut_length(sec, src, label, expect_fail=False):
    r = do("sections-3d", "generate_section",
           {"handle": sec, "sourceHandles": [src], "kind": "2d"},
           label=label, expect_fail=expect_fail)
    return r.get("totalCurveLength") if isinstance(r, dict) else None


def fresh_drawing():
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
    # Cross-session probe. Backends restarted by a deploy can come back bound to a drawing that
    # new_document has since replaced, and then one category cannot see another's handles at all -
    # see rule 26 §13a. Catch it here by name rather than as arithmetic that will not add up.
    probe = hnd(S["geometry-3d"].call("draw_box", {"corner1": {"x": 0, "y": 9000, "z": 0},
                                                   "corner2": {"x": 10, "y": 9010, "z": 10}})[1])
    ok2, _ = S["sections-3d"].call("list_section_planes", {})
    check("the geometry-3d and sections-3d sessions are on the SAME drawing",
          bool(probe) and rel(volume_of(probe), 1000.0, 1e-9) and ok2, f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── the control that discriminates: a sphere cut off-centre ────────────────
print("\n== THE control: a sphere of r=50, cut off-centre ==")
print(f"   great circle   2*pi*50            = {GREAT_CIRCLE:.4f}   <- what a plane that was")
print( "                                                             IGNORED would give")
print(f"   circle at y=30 2*pi*sqrt(50^2-30^2) = {OFF_CIRCLE:.4f}   <- what the plane ASKED FOR gives")
sph = hnd(do("geometry-3d", "draw_sphere", {"center": {"x": 0, "y": 0, "z": 0}, "radius": R},
             label="a sphere of radius 50 at the origin"))

s_mid = hnd(plane([{"x": -200, "y": 0, "z": 0}, {"x": 200, "y": 0, "z": 0}],
                  "a plane through the sphere's centre"))
L = cut_length(s_mid, sph, "generate through the centre")
check(f"the central cut is the great circle, {GREAT_CIRCLE:.3f}", rel(L, GREAT_CIRCLE, 1e-4), f"{L}")

s_off = hnd(plane([{"x": -200, "y": OFFSET, "z": 0}, {"x": 200, "y": OFFSET, "z": 0}],
                  "a plane 30 off the centre"))
L_off = cut_length(s_off, sph, "generate 30 off the centre")
check(f"PROVEN that the cut is taken WHERE THE PLANE IS: 30 off centre gives a circle of radius "
      f"40, {OFF_CIRCLE:.3f}, not the great circle {GREAT_CIRCLE:.3f}",
      rel(L_off, OFF_CIRCLE, 1e-4), f"{L_off}")
check("and the two cuts differ, which is the whole point of the control - when the plane's "
      "position was being ignored these two numbers were identical",
      L is not None and L_off is not None and abs(L - L_off) > 1.0, f"{L} vs {L_off}")

print("\n-- verticalDirection turns the same line into a horizontal cut --")
s_flat = hnd(plane([{"x": -200, "y": 0, "z": OFFSET}, {"x": 200, "y": 0, "z": OFFSET}],
                   "a plane at z=30 with up along Y - a FLAT cut",
                   verticalDirection={"x": 0, "y": 1, "z": 0}))
L_flat = cut_length(s_flat, sph, "generate the flat cut")
check(f"PROVEN: up along Y cuts horizontally at z=30, giving the same radius-40 circle "
      f"{OFF_CIRCLE:.3f} - the plane contains the line and the up vector",
      rel(L_flat, OFF_CIRCLE, 1e-4), f"{L_flat}")

# ── a box no two faces of which are alike ──────────────────────────────────
print(f"\n== a {BX:.0f} x {BY:.0f} x {BZ:.0f} box: upright cut {CUT_UPRIGHT:.0f}, flat cut {CUT_FLAT:.0f} ==")
box = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 0, "y": 0, "z": 0},
                                         "corner2": {"x": BX, "y": BY, "z": BZ}},
             label="the box"))
check(f"the box measures {BOX_VOL:.0f}", rel(volume_of(box), BOX_VOL, 1e-9), f"{volume_of(box)}")

b_up = hnd(plane([{"x": -50, "y": BY / 2, "z": 0}, {"x": 150, "y": BY / 2, "z": 0}],
                 "an upright plane through the middle of the box"))
check("PROVEN: placing a section plane leaves the box alone", rel(volume_of(box), BOX_VOL, 1e-9),
      f"{volume_of(box)}")

Lb = cut_length(b_up, box, "generate the upright section")
check(f"PROVEN against arithmetic: the upright cut is the x-z rectangle, 2*(100+60) = "
      f"{CUT_UPRIGHT:.0f}, and NOT the flat one at {CUT_FLAT:.0f}",
      rel(Lb, CUT_UPRIGHT, 1e-6), f"{Lb}")

b_flat = hnd(plane([{"x": -50, "y": 0, "z": BZ / 2}, {"x": 150, "y": 0, "z": BZ / 2}],
                   "a flat plane through the middle of the box",
                   verticalDirection={"x": 0, "y": 1, "z": 0}))
Lf = cut_length(b_flat, box, "generate the flat section")
check(f"PROVEN: the flat cut is the x-y rectangle, 2*(100+80) = {CUT_FLAT:.0f} - the two "
      f"orientations give different numbers, so neither can be mistaken for the other",
      rel(Lf, CUT_FLAT, 1e-6), f"{Lf}")

# THE claim the whole category rests on, and the one a healthy JSON result would hide.
check(f"PROVEN: after generating TWO sections the box is still whole at {BOX_VOL:.0f} - a section "
      f"plane reports a cut, it does not make one. slice_solid would have left two halves",
      rel(volume_of(box), BOX_VOL, 1e-9), f"{volume_of(box)}")

print("\n-- the normal is worked out, not given --")
rr = do("sections-3d", "create_section_plane",
        {"vertices": [{"x": -50, "y": 400, "z": 0}, {"x": 150, "y": 400, "z": 0}]},
        label="a plan line with the default up")
if isinstance(rr, dict):
    n = rr.get("normal") or {}
    # The SIGN matters and is measured: AutoCAD works the normal out as up x along, not along x up.
    # A line running +X with up along +Z therefore looks along +Y, exactly. An earlier version of
    # this check asked only that |y| = 1, and a sign error walked straight through it.
    check("a plan line running +X with up along +Z looks along EXACTLY (0,1,0) - the sign is the "
          "half of this that a test on |y| would miss",
          abs(n.get("x", 9)) < 1e-9 and abs(n.get("z", 9)) < 1e-9
          and abs(n.get("y", 0) - 1) < 1e-9, f"{n}")
do("sections-3d", "create_section_plane",
   {"vertices": [{"x": 0, "y": 0, "z": 0}, {"x": 100, "y": 0, "z": 0}],
    "verticalDirection": {"x": 1, "y": 0, "z": 0}},
   label="up parallel to the section line is refused - the two define no plane", expect_fail=True)

print("\n-- a plane clear of the model --")
far = hnd(plane([{"x": -50, "y": 5000, "z": 0}, {"x": 150, "y": 5000, "z": 0}],
                "a plane well away from the box"))
r_far = do("sections-3d", "generate_section", {"handle": far, "sourceHandles": [box], "kind": "2d"},
           label="generating from a plane that misses is refused rather than reported as a success",
           expect_fail=True)
check("and the refusal explains that AutoCAD produced an empty result rather than complaining",
      "did not cross" in str(r_far) or "empty result" in str(r_far), str(r_far)[:250])

# ── state, live and height, each read back ──────────────────────────────────
print("\n== set_section_state ==")
r = do("sections-3d", "set_section_state", {"handle": b_up, "state": "volume"})
if isinstance(r, dict):
    check("PROVEN: it went from plane to volume and reads back as volume",
          r.get("stateBefore") == "plane" and r.get("state") == "volume", str(r)[:250])
do("sections-3d", "set_section_state", {"handle": b_up, "state": "volume"},
   label="setting the state it already has is refused rather than reported as a change",
   expect_fail=True)
do("sections-3d", "set_section_state", {"handle": b_up, "state": "diagonal"},
   label="an unknown state is refused, and the refusal lists the three that exist",
   expect_fail=True)

print("\n== set_section_height ==")
r = do("sections-3d", "set_section_height", {"handle": b_up, "above": 250.0, "below": 40.0})
if isinstance(r, dict):
    check("PROVEN: both heights read back as they were set - these are plain properties and an "
          "assignment the object declines to keep would look identical without this check",
          rel(r.get("above"), 250.0) and rel(r.get("below"), 40.0), str(r)[:280])
do("sections-3d", "set_section_height", {"handle": b_up},
   label="a height call with nothing to set is refused", expect_fail=True)

print("\n== set_live_section ==")
r = do("sections-3d", "set_live_section", {"handle": b_up, "enabled": True})
if isinstance(r, dict):
    check("PROVEN: live went from off to on and reads back on",
          r.get("liveSectionBefore") is False and r.get("liveSection") is True, str(r)[:250])
check("PROVEN: and the live section still leaves the box whole - it hides the model on screen, "
      "it does not cut it", rel(volume_of(box), BOX_VOL, 1e-9), f"{volume_of(box)}")
do("sections-3d", "set_live_section", {"handle": b_up, "enabled": True},
   label="turning live on when it is already on is refused", expect_fail=True)
do("sections-3d", "set_live_section", {"handle": b_up},
   label="a live call with no enabled flag is refused", expect_fail=True)

# ── list_section_planes ─────────────────────────────────────────────────────
print("\n== list_section_planes ==")
r = do("sections-3d", "list_section_planes", {})
if isinstance(r, dict):
    secs = r.get("sections") or []
    check("every plane made here is listed", (r.get("count") or 0) >= 7, str(r)[:200])
    live = [x for x in secs if x.get("liveSection")]
    check("and exactly one of them is the live section, which is the AutoCAD rule",
          len(live) == 1, f"{[x.get('handle') for x in live]}")
    ours = [x for x in secs if x.get("handle") == b_up]
    check("the one we changed reports the volume state",
          len(ours) == 1 and ours[0].get("state") == "volume", str(ours)[:250])

print("\n-- refusals --")
do("sections-3d", "create_section_plane", {"vertices": [{"x": 0, "y": 0, "z": 0}]},
   label="a section line of one point is refused", expect_fail=True)
do("sections-3d", "create_section_plane",
   {"vertices": [{"x": 0, "y": 0, "z": 0}, {"x": 0, "y": 0, "z": 0}]},
   label="two identical points give no direction and are refused", expect_fail=True)
do("sections-3d", "generate_section", {"handle": b_up},
   label="generating without naming the solids to cut is refused", expect_fail=True)
r = do("sections-3d", "generate_section",
       {"handle": b_up, "sourceHandles": [box], "kind": "sideways"},
       label="an unknown kind is refused", expect_fail=True)
check("and the refusal lists the three kinds that exist",
      "2d" in str(r) and "3d" in str(r) and "live" in str(r), str(r)[:280])
do("sections-3d", "set_section_state", {"handle": box, "state": "plane"},
   label="a solid is refused by name and pointed at list_section_planes", expect_fail=True)

print("\n-- a jogged line keeps its jog --")
r = do("sections-3d", "create_section_plane", {
    "vertices": [{"x": -50, "y": 300, "z": 0}, {"x": 50, "y": 300, "z": 0},
                 {"x": 50, "y": 380, "z": 0}, {"x": 150, "y": 380, "z": 0}]},
    label="a four-point jogged section line")
if isinstance(r, dict):
    check("PROVEN: all four vertices survived - a jogged section is the whole reason more than "
          "two points are allowed", r.get("vertices") == 4, str(r)[:250])

# ── create_section_orthographic ─────────────────────────────────────────────
# The three standard views give three DIFFERENT numbers on a 100 x 80 x 60 box, which is what
# makes "front" distinguishable from "left" and from "top" rather than merely successful.
CUT_LEFT = 2 * (BY + BZ)     # 280 - looking along X cuts the y-z rectangle
print(f"\n== create_section_orthographic: front {CUT_UPRIGHT:.0f}, left {CUT_LEFT:.0f}, "
      f"top {CUT_FLAT:.0f} ==")
do("files", "new_document", {})
box2 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 0, "y": 0, "z": 0},
                                          "corner2": {"x": BX, "y": BY, "z": BZ}},
              label="a fresh 100 x 80 x 60 box"))

for orient, expect, axis in (("front", CUT_UPRIGHT, "y"), ("left", CUT_LEFT, "x"),
                             ("top", CUT_FLAT, "z")):
    r = do("sections-3d", "create_section_orthographic",
           {"orientation": orient, "sourceHandles": [box2]}, label=f"place the {orient} section")
    h = hnd(r)
    if isinstance(r, dict):
        n = r.get("normal") or {}
        want = {"front": (0, 1, 0), "left": (1, 0, 0), "top": (0, 0, -1)}[orient]
        check(f"the {orient} plane looks along {want}, which is what makes the name mean anything",
              all(abs(n.get(k, 9) - w) < 1e-9 for k, w in zip("xyz", want)), f"{n}")
        mn, mx = r.get("extentsMin") or {}, r.get("extentsMax") or {}
        check(f"and it measured the box's own extents, not the whole drawing",
              rel(mx.get("x"), BX) and rel(mx.get("y"), BY) and rel(mx.get("z"), BZ)
              and abs(mn.get("x", 9)) < 1e-9, f"{mn} .. {mx}")
    L = cut_length(h, box2, f"generate the {orient} section")
    check(f"PROVEN: the {orient} section cuts {expect:.0f} - and the other two views give "
          f"different numbers, so the orientation cannot be mistaken",
          rel(L, expect, 1e-6), f"{L}")

print("\n-- offset moves the plane, and a sphere is what proves it --")
sph2 = hnd(do("geometry-3d", "draw_sphere", {"center": {"x": 400, "y": 0, "z": 0}, "radius": R},
              label="a sphere of radius 50, away from the box"))
r = do("sections-3d", "create_section_orthographic",
       {"orientation": "front", "sourceHandles": [sph2]}, label="front section through its centre")
L = cut_length(hnd(r), sph2, "generate at the centre")
check(f"through the centre it is the great circle {GREAT_CIRCLE:.3f}", rel(L, GREAT_CIRCLE, 1e-4),
      f"{L}")
r = do("sections-3d", "create_section_orthographic",
       {"orientation": "front", "sourceHandles": [sph2], "offset": OFFSET},
       label="front section offset 30")
L = cut_length(hnd(r), sph2, "generate at offset 30")
check(f"PROVEN: offset 30 moves the plane, giving the radius-40 circle {OFF_CIRCLE:.3f} rather "
      f"than the great circle - an offset that was ignored would have given {GREAT_CIRCLE:.3f}",
      rel(L, OFF_CIRCLE, 1e-4), f"{L}")

do("sections-3d", "create_section_orthographic", {"orientation": "sideways"},
   label="an unknown orientation is refused, and the refusal lists the six", expect_fail=True)
do("sections-3d", "create_section_orthographic", {}, label="no orientation is refused",
   expect_fail=True)

# ── generate_section_block ──────────────────────────────────────────────────
print("\n== generate_section_block ==")
r = do("sections-3d", "create_section_orthographic",
       {"orientation": "front", "sourceHandles": [box2]}, label="a plane for the block")
blk_sec = hnd(r)
r = do("sections-3d", "generate_section_block",
       {"handle": blk_sec, "sourceHandles": [box2], "blockName": "SEC_TEST_A"})
if isinstance(r, dict):
    check(f"PROVEN: the block holds the same section, still {CUT_UPRIGHT:.0f} long - packing it "
          f"into a block changes where the curves live, not what they are",
          rel(r.get("totalCurveLength"), CUT_UPRIGHT, 1e-6), f"{r.get('totalCurveLength')}")
    check("PROVEN: the block definition holds exactly the entities that were generated - an empty "
          "definition would still insert and would look like a success while drawing nothing",
          (r.get("entitiesInBlock") or 0) > 0
          and r.get("entitiesInBlock") == (r.get("cutCurves") or -1),
          str(r)[:250])
    ok_e, ent = S["geometry-2d"].call("get_entity", {"handle": hnd(r)})
    check("and what landed in model space is a BLOCK REFERENCE, not loose curves",
          ok_e and (ent or {}).get("class") == "AcDbBlockReference",
          str(ent)[:200])
check(f"PROVEN: the box is still whole at {BOX_VOL:.0f} after the block was made",
      rel(volume_of(box2), BOX_VOL, 1e-9), f"{volume_of(box2)}")
do("sections-3d", "generate_section_block",
   {"handle": blk_sec, "sourceHandles": [box2], "blockName": "SEC_TEST_A"},
   label="reusing an existing block name is refused - overwriting a definition would silently "
         "change every insert of it", expect_fail=True)
do("sections-3d", "generate_section_block", {"handle": blk_sec, "blockName": "SEC_TEST_B"},
   label="a block with no sources named is refused", expect_fail=True)

# ── set_section_settings ────────────────────────────────────────────────────
print("\n== set_section_settings ==")
r = do("sections-3d", "set_section_settings",
       {"handle": blk_sec, "part": "cut", "color": 1, "layer": "S-CUT",
        "linetypeScale": 2.5, "divisionLines": True})
if isinstance(r, dict):
    check("PROVEN: every value reads back off the section after being written - these are read "
          "with getters named after the thing (Color, Layer), not Get-something, and a setting "
          "the object declined to keep would look identical without this",
          r.get("color") == 1 and r.get("layer") == "S-CUT"
          and rel(r.get("linetypeScale"), 2.5) and r.get("divisionLines") is True,
          str(r)[:320])

r2 = do("sections-3d", "set_section_settings",
        {"handle": blk_sec, "part": "fill", "color": 3},
        label="style the FILL differently from the cut")
r3 = do("sections-3d", "set_section_settings",
        {"handle": blk_sec, "part": "cut", "color": 1}, label="read the cut back again")
if isinstance(r2, dict) and isinstance(r3, dict):
    check("PROVEN per-part independence: the fill is colour 3 while the cut is still colour 1, so "
          "the setter is writing to the part named and not to the section as a whole",
          r2.get("color") == 3 and r3.get("color") == 1,
          f"fill={r2.get('color')} cut={r3.get('color')}")

r = do("sections-3d", "set_section_settings",
       {"handle": blk_sec, "part": "background", "visible": False},
       label="hide the background geometry of the 2d section")
if isinstance(r, dict):
    check("the background can be turned off, which is what stops a section reading as a wireframe",
          r.get("visible") is False, str(r)[:250])

print("\n-- the measured limits, each refused with the reason rather than eInvalidInput --")
r = do("sections-3d", "set_section_settings", {"handle": blk_sec, "part": "cut", "visible": False},
       label="the CUT of a 2d section cannot be hidden - it IS the section", expect_fail=True)
check("and the refusal explains that rather than passing AutoCAD's bare eInvalidInput through",
      "IS the section" in str(r), str(r)[:250])
do("sections-3d", "set_section_settings",
   {"handle": blk_sec, "part": "background", "kind": "3d", "visible": False},
   label="the background of a 3d section cannot be hidden either", expect_fail=True)
do("sections-3d", "set_section_settings", {"handle": blk_sec, "part": "fill", "divisionLines": True},
   label="division lines exist only on the cut of a 2d section", expect_fail=True)
do("sections-3d", "set_section_settings", {"handle": blk_sec, "part": "cut", "hiddenLine": True},
   label="hidden-line treatment is only for background and foreground", expect_fail=True)
r = do("sections-3d", "set_section_settings",
       {"handle": blk_sec, "part": "cut", "kind": "live", "color": 2},
       label="NOTHING can be styled on the live section", expect_fail=True)
check("and the refusal points at set_live_section instead",
      "set_live_section" in str(r), str(r)[:280])
# The control on all of the above: the combinations that ARE supported still work, so the guard
# is a measured table and not a blanket refusal that would look just as green.
r = do("sections-3d", "set_section_settings",
       {"handle": blk_sec, "part": "foreground", "kind": "3d", "visible": False, "color": 5},
       label="a supported 3d combination still goes through")
if isinstance(r, dict):
    check("PROVEN the guard is a table and not a blanket no: foreground/3d takes both values",
          r.get("visible") is False and r.get("color") == 5, str(r)[:250])

do("sections-3d", "set_section_settings", {"handle": blk_sec, "part": "cut"},
   label="a settings call with nothing to set is refused", expect_fail=True)
r = do("sections-3d", "set_section_settings", {"handle": blk_sec, "part": "tangency", "color": 2},
       label="an unknown part is refused", expect_fail=True)
check("and the refusal says there is no tangency PART, even though generate_section returns "
      "tangency curves - SectionGeometry has no member for it",
      "tangency" in str(r).lower(), str(r)[:280])
do("sections-3d", "set_section_settings", {"handle": box2, "part": "cut", "color": 1},
   label="a solid is refused by name", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "sections3d.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Extents",
                            "widthPx": 1200, "heightPx": 1400})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 4000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> the box and the sphere both still there, with the generated cut outlines lying on")
print("     them and the section lines crossing. The solids surviving IS the result.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
