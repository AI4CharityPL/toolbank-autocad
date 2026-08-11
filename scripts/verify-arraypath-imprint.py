# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.1 tranche 3 — modify.array_path and geometry3d.imprint_edges.

Both are verified against numbers computed outside the tool, because both have a failure mode
that returns a perfectly healthy result:

* **array_path** — spacing along a BEND. A tool that spaces by straight-line distance between
  neighbours produces exactly the count that was asked for and puts every copy on the curve; it
  just bunches them round the outside of the turn. The only thing that catches it is measuring
  the arc length between consecutive copies. Here the path is a quarter circle of radius 200,
  whose length is pi*200/2 = 314.159..., and every gap must be that over (count-1). A CONTROL
  arm on a straight line of the same length shows the two agree where they must.

* **imprint_edges** — an imprint adds EDGES, not material. A tool that quietly cut the solid
  would also report more faces, and only the volume would give it away, so the volume is read
  back independently with get_volume before and after.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "modify", "view")}
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


def rel(a, b, tol=1e-6):
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def at_most(a, tol):
    """abs(a) <= tol, written so that a CORRECT answer of exactly 0 still passes.

    `(a or SENT) <= tol` reads a genuine 0 as missing and fails the check. Three tools have
    already been wrongly marked broken by that."""
    return a is not None and abs(a) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    if not ok or not isinstance(r, dict):
        return None
    for k in ("volume", "value"):
        if r.get(k) is not None:
            return r[k]
    return None


def bbox(h):
    ok, r = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return ((r or {}).get("bbox") or {}) if ok else {}


def centre_of(h):
    """Middle of an entity's bounding box, read back from the drawing."""
    b = bbox(h)
    mn, mx = b.get("min"), b.get("max")
    if not mn or not mx:
        return None
    return ((mn["x"] + mx["x"]) / 2.0, (mn["y"] + mx["y"]) / 2.0)


def span(h):
    """(width, height) of an entity's bounding box.

    This is how a short straight bar's ORIENTATION is measured without asking the tool that
    made it: horizontal is (30, 0), vertical is (0, 30)."""
    b = bbox(h)
    mn, mx = b.get("min"), b.get("max")
    if not mn or not mx:
        return None
    return (mx["x"] - mn["x"], mx["y"] - mn["y"])


def dist_to(h, x, y):
    ok, r = S["geometry-2d"].call("get_distance_to_entity",
                                  {"handle": h, "point": {"x": x, "y": y}})
    return (r or {}).get("value") if ok else None


def exists(h):
    ok, _ = S["geometry-2d"].call("get_bounding_box", {"handle": h})
    return ok


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — two open drawings put the category
    sessions on different documents and the handles stop meaning the same thing."""
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
    # The probe is made by geometry-3d and then read by geometry-2d and written by modify. Each
    # category is its own backend process; with two drawings open they bind to different ones and
    # the same handle means two different entities.
    probe = hnd(S["geometry-3d"].call("draw_box", {"corner1": {"x": 0, "y": 9000, "z": 0},
                                                   "corner2": {"x": 10, "y": 9010, "z": 10}})[1])
    check("the geometry-3d, geometry-2d and modify sessions are all on the SAME drawing",
          bool(probe) and bool(bbox(probe))
          and S["modify"].call("set_layer", {"handles": [probe], "layer": "0"})[0],
          f"probe={probe}, bbox={bbox(probe) if probe else None}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── array_path along a quarter circle, against arc length ─────────────────────
print("\n== array_path: 5 copies along a quarter circle of radius 200 ==")
R = 200.0
ARC = math.pi * R / 2.0          # 314.15926...
N = 5
GAP = ARC / (N - 1)              # 78.53981...

arc = hnd(do("geometry-2d", "draw_arc", {
    "center": {"x": 0, "y": 0}, "radius": R,
    "startAngleDeg": 0, "endAngleDeg": 90}, label="the quarter-circle path"))
# The thing being arrayed is a short line so its own orientation is readable afterwards.
post = hnd(do("geometry-2d", "draw_line", {"start": {"x": R - 15, "y": 0},
                                           "end": {"x": R + 15, "y": 0}}, label="the item to array"))

r = do("modify", "array_path", {"handles": [post], "pathHandle": arc, "count": N})
ents = [e.get("handle") for e in ((r or {}).get("entities") or [])] if isinstance(r, dict) else []
check(f"{N} copies came back", len(ents) == N, f"{len(ents)}: {ents}")
if isinstance(r, dict):
    check(f"PROVEN against arithmetic: it measured the path at pi*R/2 = {ARC:.4f}",
          rel(r.get("pathLength"), ARC, 1e-6), f"reported {r.get('pathLength')}")
    check(f"PROVEN: the spacing is that over {N - 1} gaps = {GAP:.4f}",
          rel(r.get("spacing"), GAP, 1e-6), f"reported {r.get('spacing')}")
    ds = r.get("distances") or []
    check("PROVEN: the reported distances are exact multiples of the spacing, ends included",
          len(ds) == N and all(at_most(d - i * GAP, 1e-6) for i, d in enumerate(ds)),
          f"{ds}")

# THE check, and the one a straight-line-spacing bug survives: measure where the copies
# actually landed and convert back to arc length.
print("\n-- measured from the drawing, not from the tool's own report --")
angles = []
for h in ents:
    c = centre_of(h)
    if c is None:
        continue
    angles.append(math.degrees(math.atan2(c[1], c[0])) % 360.0)
check("every copy's centre could be measured", len(angles) == N, f"{angles}")
radii = [math.hypot(*c) for c in (centre_of(h) for h in ents) if c]
check("PROVEN: every copy's centre sits ON the arc, at radius 200",
      len(radii) == N and all(rel(x, R, 1e-4) for x in radii),
      f"{[round(x, 4) for x in radii]}")
# The same claim asked of the drawing a second way, through a different tool, in case the
# bounding-box arithmetic above is the thing that is wrong.
on_arc = [dist_to(arc, *c) for c in (centre_of(h) for h in ents) if c]
check("and AutoCAD agrees: the distance from each copy's centre to the arc is zero",
      len(on_arc) == N and all(at_most(d, 1e-4) for d in on_arc),
      f"{[None if d is None else round(d, 6) for d in on_arc]}")
arc_positions = sorted(math.radians(a) * R for a in angles)
gaps = [b - a for a, b in zip(arc_positions, arc_positions[1:])]
check(f"PROVEN: the ARC LENGTH between neighbours is {GAP:.4f} every time - this is what a "
      f"straight-line-spacing bug fails, and only this",
      len(gaps) == N - 1 and all(rel(g, GAP, 1e-4) for g in gaps),
      f"{[round(g, 4) for g in gaps]} vs {GAP:.4f}")
# The control that gives that number meaning: the CHORDS are shorter, and unequal to the arcs.
chords = []
pts = sorted((math.radians(a) for a in angles))
for a, b in zip(pts, pts[1:]):
    chords.append(math.hypot(R * math.cos(b) - R * math.cos(a), R * math.sin(b) - R * math.sin(a)))
check("THE CONTROL: on this bend the straight-line gap is measurably SHORTER than the arc gap, "
      "so the two answers really are distinguishable here",
      all(c < GAP - 0.05 for c in chords), f"chords {[round(c, 4) for c in chords]} vs arc {GAP:.4f}")

print("\n-- alignment is relative: the first copy keeps the source's orientation --")
# The source bar is horizontal and sits at the arc's START, where the tangent is +Y (90 deg).
# Rotating every copy by its own ABSOLUTE tangent angle would stand that first copy on end even
# though it has not moved; relative alignment leaves it exactly as drawn. Orientation is read off
# the bounding box - a 30-long horizontal bar spans (30, 0), a vertical one (0, 30).
def angle_of(h):
    c = centre_of(h)
    return math.degrees(math.atan2(c[1], c[0])) % 360.0 if c else None


placed = sorted((h for h in ents if centre_of(h)), key=angle_of)
if len(placed) == N:
    s_first, s_last = span(placed[0]), span(placed[-1])
    check("PROVEN: the copy at the path start is still horizontal, as the source was drawn",
          s_first is not None and rel(s_first[0], 30.0, 1e-6) and at_most(s_first[1], 1e-6),
          f"spans {s_first}")
    # A quarter circle turns through 90 degrees, so the copy at the far end must be vertical.
    check("PROVEN: and the copy at the far end has turned the 90 degrees the path turned",
          s_last is not None and at_most(s_last[0], 1e-6) and rel(s_last[1], 30.0, 1e-6),
          f"spans {s_last}")
    # The middle copy is at 45 degrees, so both spans are 30/sqrt(2).
    s_mid = span(placed[2])
    diag = 30.0 / math.sqrt(2.0)
    check(f"PROVEN: the middle copy is at 45 degrees, spanning {diag:.4f} each way - the copies "
          f"turn gradually, they are not simply snapped to the two ends",
          s_mid is not None and rel(s_mid[0], diag, 1e-4) and rel(s_mid[1], diag, 1e-4),
          f"spans {s_mid} vs ({diag:.4f}, {diag:.4f})")

print("\n-- alignToPath false leaves every copy parallel to the source --")
post2 = hnd(do("geometry-2d", "draw_line", {"start": {"x": R - 15, "y": 600},
                                            "end": {"x": R + 15, "y": 600}}, label="a second item"))
arc2 = hnd(do("geometry-2d", "draw_arc", {"center": {"x": 0, "y": 600}, "radius": R,
                                          "startAngleDeg": 0, "endAngleDeg": 90}, label="a second path"))
r = do("modify", "array_path", {"handles": [post2], "pathHandle": arc2,
                                "count": 4, "alignToPath": False})
flat = [e.get("handle") for e in ((r or {}).get("entities") or [])] if isinstance(r, dict) else []
spans = [span(h) for h in flat]
check("PROVEN: with the flag off all four copies are still horizontal - so the alignment above "
      "was really the flag doing it, not the copies happening to land that way",
      len(spans) == 4 and all(s and rel(s[0], 30.0, 1e-6) and at_most(s[1], 1e-6) for s in spans),
      f"{spans}")

print("\n-- a straight path, where arc length and straight-line distance are the SAME --")
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 1200},
                                         "end": {"x": 400, "y": 1200}}, label="a straight path"))
dot = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 0, "y": 1200}, "radius": 8},
             label="a circle to array"))
r = do("modify", "array_path", {"handles": [dot], "pathHandle": ln, "count": 5})
straight = [e.get("handle") for e in ((r or {}).get("entities") or [])] if isinstance(r, dict) else []
xs = sorted(centre_of(h)[0] for h in straight if centre_of(h))
check("PROVEN: on a straight path the copies land at 0, 100, 200, 300, 400 - the control that "
      "shows the along-the-curve measurement is not simply a different number everywhere",
      len(xs) == 5 and all(at_most(x - i * 100.0, 1e-6) for i, x in enumerate(xs)),
      f"{[round(x, 4) for x in xs]}")

print("\n-- refusals --")
do("modify", "array_path", {"handles": [dot], "pathHandle": ln, "count": 1},
   label="count 1 is refused, since one copy is the thing you already have", expect_fail=True)
do("modify", "array_path", {"handles": [dot], "pathHandle": ln},
   label="a missing count is refused", expect_fail=True)
r = do("modify", "array_path", {"handles": [ln], "pathHandle": ln, "count": 3},
       label="arraying the path along itself is refused", expect_fail=True)
check("and the refusal says why", "along itself" in str(r), str(r)[:250])
pt = hnd(do("geometry-2d", "draw_point", {"position": {"x": 0, "y": 1400}}, label="a point"))
if pt:
    do("modify", "array_path", {"handles": [dot], "pathHandle": pt, "count": 3},
       label="a point as the path is refused by name, since it is not a curve", expect_fail=True)

# ── imprint_edges, against face/edge counts and conservation of volume ────────
print("\n== imprint_edges: a circle pressed into the top face of a box ==")
b = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 0, "y": 2000, "z": 0},
                                       "corner2": {"x": 200, "y": 2200, "z": 100}},
           label="a 200x200x100 box"))
v_before = volume_of(b)
check("the box measures 4000000", rel(v_before, 200 * 200 * 100), f"{v_before}")

# The circle must LIE ON the top face, z = 100. Everything geometry-2d draws is flat on z=0
# (its args are Point2dDto), so the curve is drawn there and lifted with modify.move.
circ = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 100, "y": 2100},
                                             "radius": 40}, label="a circle, drawn flat"))
do("modify", "move", {"handles": [circ], "from": {"x": 0, "y": 0, "z": 0},
                      "to": {"x": 0, "y": 0, "z": 100}}, label="lifted onto the top face")
r = do("geometry-3d", "imprint_edges", {"solidHandle": b, "curveHandle": circ})
if isinstance(r, dict):
    check("PROVEN: the top face was divided - the face count went up",
          (r.get("faces") or 0) > (r.get("facesBefore") or 0),
          f"{r.get('facesBefore')} -> {r.get('faces')}")
    check("PROVEN: a box has 6 faces, and after imprinting a closed curve on one of them, 7",
          r.get("facesBefore") == 6 and r.get("faces") == 7, str(r)[:250])
    check("PROVEN: and the edge count went up too",
          (r.get("edges") or 0) > (r.get("edgesBefore") or 0),
          f"{r.get('edgesBefore')} -> {r.get('edges')}")
# THE check. More faces alone is exactly what a tool that CUT would also report.
v_after = volume_of(b)
check("MEASURED INDEPENDENTLY with get_volume: the volume did not change, so it imprinted "
      "rather than cut",
      rel(v_after, v_before, 1e-9), f"{v_before} -> {v_after}")

print("\n-- an open curve on a face divides it too --")
b2 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 400, "y": 2000, "z": 0},
                                        "corner2": {"x": 600, "y": 2200, "z": 100}},
            label="a second box"))
v2 = volume_of(b2)
seg = hnd(do("geometry-2d", "draw_line", {"start": {"x": 400, "y": 2100},
                                          "end": {"x": 600, "y": 2100}},
             label="a line, drawn flat"))
do("modify", "move", {"handles": [seg], "from": {"x": 0, "y": 0, "z": 0},
                      "to": {"x": 0, "y": 0, "z": 100}}, label="lifted onto the top face")
r = do("geometry-3d", "imprint_edges", {"solidHandle": b2, "curveHandle": seg,
                                        "eraseSource": True})
if isinstance(r, dict):
    check("PROVEN: the top face became two", r.get("faces") == 7 and r.get("facesBefore") == 6,
          str(r)[:250])
    check("and the curve was consumed as asked", r.get("sourceErased") is True, str(r)[:250])
check("PROVEN: still no change in volume", rel(volume_of(b2), v2, 1e-9), f"{v2} -> {volume_of(b2)}")
check("the erased curve really is gone from the drawing, not merely reported as erased",
      not exists(seg), f"handle {seg} still resolves")

print("\n-- refusals --")
b3 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 800, "y": 2000, "z": 0},
                                        "corner2": {"x": 900, "y": 2100, "z": 100}},
            label="a third box"))
floating = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 850, "y": 2050},
                                                 "radius": 20}, label="a small circle"))
do("modify", "move", {"handles": [floating], "from": {"x": 0, "y": 0, "z": 0},
                      "to": {"x": 0, "y": 0, "z": 400}},
   label="lifted to z=400, well clear of the box")
r = do("geometry-3d", "imprint_edges", {"solidHandle": b3, "curveHandle": floating},
       label="a curve that touches no face is refused", expect_fail=True)
check("and the refusal explains that the curve has to LIE ON a face",
      "lie on" in str(r).lower() or "did not meet" in str(r).lower(), str(r)[:280])
check("PROVEN: the box it refused to imprint is untouched", rel(volume_of(b3), 100 * 100 * 100),
      f"{volume_of(b3)}")
do("geometry-3d", "imprint_edges", {"solidHandle": b3},
   label="a missing curveHandle is refused", expect_fail=True)
do("geometry-3d", "imprint_edges", {"solidHandle": ln, "curveHandle": circ},
   label="a line as the solid is refused by name", expect_fail=True)

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "arraypath-imprint.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -60, "xMax": 960, "yMax": 2300},
                            "widthPx": 1400, "heightPx": 2600})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm in plan view: five bars fanned evenly round a quarter arc, standing radially")
print("     like the spokes of a wheel; above them four bars round a second arc all still")
print("     horizontal; a row of five evenly spaced circles on a straight line; and at the top")
print("     three boxes, the first two carrying a circle and a line drawn on their top faces.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
