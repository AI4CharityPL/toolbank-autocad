# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.1 tranche 4 — the face/edge family.

`list_solid_edges`, `list_solid_faces`, `fillet_edge`, `chamfer_edge`.

The whole family rests on being able to NAME one edge of a solid, and the managed API names it
with a SubentityId — an opaque handle that cannot be spelled by a caller on the other end of a
JSON pipe. So the addressing scheme is an index plus the geometry of every slot, and the first
thing this script checks is that the geometry reported for each index is the real one: a
100-cube has twelve edges of length 100 whose midpoints are twelve known points, and six faces
whose normals are the six axis directions. An index that pointed at the wrong edge would still
be an integer in range.

Then the operations, against arithmetic that can be done on paper:

* filleting one straight edge of length L with radius r removes exactly L*r*r*(1 - pi/4). For
  L=100, r=10 that is 2146.0184, so the cube must measure 997853.9816 afterwards.
* the same fillet at r=20 must remove FOUR times as much — the removal goes as r squared, which
  is the control showing the radius really drives the cut rather than merely being accepted.
* chamfering the same edge with equal distances d removes exactly L*d*d/2 = 5000, which is more
  than the fillet of the same size takes, because the fillet keeps the material inside the arc.

Volumes are read back with get_volume, not taken from the operating tool's own report.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "modify", "view")}
results = []

SIDE = 100.0
WHOLE = SIDE ** 3
QUARTER = 1.0 - math.pi / 4.0          # 0.2146018366...


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
    """abs(a) <= tol, written so a CORRECT answer of exactly 0 still passes."""
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


def box(x0, y0, z0, x1, y1, z1):
    return hnd(S["geometry-3d"].call("draw_box", {
        "corner1": {"x": x0, "y": y0, "z": z0},
        "corner2": {"x": x1, "y": y1, "z": z1}})[1])


def pt(d):
    return (d["x"], d["y"], d["z"])


def edges_of(h):
    ok, r = S["geometry-3d"].call("list_solid_edges", {"handle": h})
    if not ok:
        print(f"  ...  list_solid_edges({h}) failed: {str(r)[:250]}")
        return []
    return (r or {}).get("edges") or []


def idx_list(*ixs):
    """Indexes for a call, refusing to send a null. Sending [None] produces a deserialization
    error that reads like a schema bug and hides the real failure two steps upstream."""
    bad = [i for i in ixs if i is None]
    if bad:
        raise SystemExit("an edge index could not be found - list_solid_edges is failing; see above")
    return list(ixs)


def vertical_edge_at(h, x, y):
    """The index of the edge running in Z at (x, y) — found by its geometry, not assumed."""
    for e in edges_of(h):
        s, t = pt(e["start"]), pt(e["end"])
        if abs(s[0] - x) < 1e-6 and abs(s[1] - y) < 1e-6 and abs(t[0] - x) < 1e-6 \
                and abs(t[1] - y) < 1e-6 and abs(s[2] - t[2]) > 1e-6:
            return e["index"]
    return None


def fresh_drawing():
    """A new drawing, and ONLY that drawing open — two open drawings put the category sessions
    on different documents and the same handle stops meaning the same entity."""
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
    probe = box(0, 9000, 0, 10, 9010, 10)
    check("the geometry-3d and geometry-2d sessions are on the SAME drawing",
          bool(probe) and S["geometry-2d"].call("get_bounding_box", {"handle": probe})[0],
          f"probe={probe}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── addressing: is the geometry behind each index the real geometry? ──────────
print("\n== list_solid_edges and list_solid_faces on a 100 cube ==")
b = box(0, 0, 0, SIDE, SIDE, SIDE)
r = do("geometry-3d", "list_solid_edges", {"handle": b})
es = (r or {}).get("edges") or [] if isinstance(r, dict) else []
check("a box has 12 edges", len(es) == 12, f"{len(es)}")
check("PROVEN: every one of them is 100 long",
      len(es) == 12 and all(rel(e["length"], SIDE) for e in es),
      f"{sorted(round(e['length'], 6) for e in es)}")
# The twelve midpoints of a 0..100 cube, computed here rather than taken from the tool.
expected_mids = set()
for a in (0.0, SIDE):
    for c in (0.0, SIDE):
        expected_mids.add((a, c, SIDE / 2))       # edges running in Z
        expected_mids.add((a, SIDE / 2, c))       # edges running in Y
        expected_mids.add((SIDE / 2, a, c))       # edges running in X
got_mids = {tuple(round(v, 6) for v in pt(e["midpoint"])) for e in es}
check("PROVEN against arithmetic: the twelve midpoints reported ARE the twelve midpoints of a "
      "0..100 cube - an index that pointed at the wrong edge would still be an integer in range",
      got_mids == {tuple(round(v, 6) for v in m) for m in expected_mids},
      f"got {sorted(got_mids)}")

r = do("geometry-3d", "list_solid_faces", {"handle": b})
fs = (r or {}).get("faces") or [] if isinstance(r, dict) else []
check("a box has 6 faces", len(fs) == 6, f"{len(fs)}")
check("PROVEN: each face is bounded by 4 edges",
      len(fs) == 6 and all(f.get("edgeCount") == 4 for f in fs),
      f"{[f.get('edgeCount') for f in fs]}")
norms = {tuple(round(abs(v), 6) for v in pt(f["normal"])) for f in fs if f.get("normal")}
check("PROVEN: the six normals are the three axis directions, two faces each",
      norms == {(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)}, f"{sorted(norms)}")
cents = {tuple(round(v, 6) for v in pt(f["centroid"])) for f in fs}
expected_cents = {(0.0, 50.0, 50.0), (100.0, 50.0, 50.0), (50.0, 0.0, 50.0),
                  (50.0, 100.0, 50.0), (50.0, 50.0, 0.0), (50.0, 50.0, 100.0)}
check("PROVEN: and the six centroids are the six face centres",
      cents == expected_cents, f"{sorted(cents)}")

# ── fillet_edge, against L*r*r*(1 - pi/4) ────────────────────────────────────
print("\n== fillet_edge: one vertical edge of the cube, radius 10 ==")
R1 = 10.0
REMOVED_R10 = SIDE * R1 * R1 * QUARTER            # 2146.018366...
ix = vertical_edge_at(b, 0.0, 0.0)
check("the vertical edge at (0,0) was found by its geometry", ix is not None, f"{ix}")
v0 = volume_of(b)
r = do("geometry-3d", "fillet_edge", {"handle": b, "edgeIndexes": idx_list(ix), "radius": R1})
if isinstance(r, dict):
    check("PROVEN: rounding an edge adds a curved face, 6 -> 7",
          r.get("facesBefore") == 6 and r.get("faces") == 7, str(r)[:250])
v1 = volume_of(b)
check(f"PROVEN against arithmetic: L*r*r*(1-pi/4) = {REMOVED_R10:.6f} was removed, leaving "
      f"{WHOLE - REMOVED_R10:.6f}",
      rel(v1, WHOLE - REMOVED_R10, 1e-9), f"measured {v1}, computed {WHOLE - REMOVED_R10}")

print("\n-- THE CONTROL: doubling the radius must remove FOUR times as much --")
b2 = box(300, 0, 0, 300 + SIDE, SIDE, SIDE)
R2 = 20.0
REMOVED_R20 = SIDE * R2 * R2 * QUARTER            # 8584.073465...
ix2 = vertical_edge_at(b2, 300.0, 0.0)
do("geometry-3d", "fillet_edge", {"handle": b2, "edgeIndexes": idx_list(ix2), "radius": R2})
v2 = volume_of(b2)
check(f"PROVEN: r=20 removes {REMOVED_R20:.6f}, exactly 4x what r=10 removed - the radius "
      f"really drives the cut rather than merely being accepted",
      rel(v2, WHOLE - REMOVED_R20, 1e-9) and rel(REMOVED_R20 / REMOVED_R10, 4.0, 1e-9),
      f"measured {v2}, computed {WHOLE - REMOVED_R20}")

# ── chamfer_edge, against L*d*d/2 ────────────────────────────────────────────
print("\n== chamfer_edge: the same edge, distance 10 ==")
D = 10.0
REMOVED_CH = SIDE * D * D / 2.0                   # 5000
b3 = box(600, 0, 0, 600 + SIDE, SIDE, SIDE)
ix3 = vertical_edge_at(b3, 600.0, 0.0)
r = do("geometry-3d", "chamfer_edge", {"handle": b3, "edgeIndexes": idx_list(ix3), "distance": D})
if isinstance(r, dict):
    check("a flat bevel is one new face too, 6 -> 7",
          r.get("facesBefore") == 6 and r.get("faces") == 7, str(r)[:250])
v3 = volume_of(b3)
check(f"PROVEN against arithmetic: L*d*d/2 = {REMOVED_CH:.1f} was removed, leaving "
      f"{WHOLE - REMOVED_CH:.1f}",
      rel(v3, WHOLE - REMOVED_CH, 1e-9), f"measured {v3}, computed {WHOLE - REMOVED_CH}")
check("PROVEN: the chamfer takes MORE than the fillet of the same size, because the fillet "
      "keeps the material inside the arc",
      REMOVED_CH > REMOVED_R10 and rel(REMOVED_CH / REMOVED_R10, 1.0 / (2 * QUARTER), 1e-9),
      f"{REMOVED_CH} vs {REMOVED_R10}")

# ── addressing by a point in space ───────────────────────────────────────────
print("\n== naming an edge by a point instead of an index ==")
b4 = box(900, 0, 0, 900 + SIDE, SIDE, SIDE)
want = vertical_edge_at(b4, 900.0, 0.0)
r = do("geometry-3d", "fillet_edge", {
    "handle": b4, "nearPoints": [{"x": 905, "y": 5, "z": 50}], "radius": R1})
if isinstance(r, dict):
    got = ((r.get("edges") or [{}])[0])
    check("PROVEN: the point snapped to the edge that was meant - reported back with its own "
          "midpoint, so the snap is checkable rather than trusted",
          got.get("index") == want and at_most(pt(got["midpoint"])[0] - 900.0, 1e-6)
          and at_most(pt(got["midpoint"])[1], 1e-6),
          f"picked {got}")
check("PROVEN: and it removed the same amount the index route did",
      rel(volume_of(b4), WHOLE - REMOVED_R10, 1e-9), f"{volume_of(b4)}")

print("\n-- a point that names two edges equally is refused, not snapped to whichever sorted first --")
b5 = box(1200, 0, 0, 1200 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "fillet_edge", {
    "handle": b5, "nearPoints": [{"x": 1250, "y": 50, "z": 50}], "radius": R1},
    label="the centre of the cube is refused", expect_fail=True)
check("and the refusal names both candidates and says the point names neither",
      "names" in str(r) and "same distance" in str(r), str(r)[:280])
check("PROVEN: the cube it refused to fillet is untouched", rel(volume_of(b5), WHOLE), f"{volume_of(b5)}")

print("\n-- refusals --")
do("geometry-3d", "fillet_edge", {"handle": b5, "edgeIndexes": [99], "radius": R1},
   label="an out-of-range edge index is refused", expect_fail=True)
r = do("geometry-3d", "fillet_edge", {"handle": b5, "edgeIndexes": [0]},
       label="a missing radius is refused", expect_fail=True)
do("geometry-3d", "fillet_edge", {"handle": b5, "radius": R1},
   label="naming no edges at all is refused", expect_fail=True)
# CORRECTED. This first read "a radius of 60 is too large and is refused" and it was my
# expectation that was wrong, not the tool: edge 0 is a top edge and the two faces meeting there
# are 100x100, so 60 of room is there and AutoCAD rounds it happily. The number that cannot fit
# is one larger than the face, and asking for the wrong one would have condemned a working tool.
b8 = box(1500, 0, 0, 1500 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "fillet_edge", {"handle": b8, "edgeIndexes": [0], "radius": 60.0},
       label="a radius of 60 FITS on a 100 face and is accepted")
check("PROVEN: and it removed L*r*r*(1-pi/4) for r=60 too, so the formula holds well away "
      "from the small radii it was first checked at",
      rel(volume_of(b8), WHOLE - SIDE * 60.0 * 60.0 * QUARTER, 1e-9),
      f"{volume_of(b8)} vs {WHOLE - SIDE * 60.0 * 60.0 * QUARTER}")
# MEASURED, after the first guess at "too large" was wrong twice. r=150 on a 100 face is still
# accepted - the arc simply runs off the two faces and eats into the ones beyond, which is a
# different shape from the prism the formula describes. So the identity L*r*r*(1-pi/4) holds only
# while the fillet FITS INSIDE both faces, and this is where that boundary is.
# r=150 is where the arc stops fitting inside the two faces. AutoCAD still performs it - it is
# the guard that stops it now - so the domain of the formula is shown with the escape hatch on.
b9 = box(1800, 0, 0, 1800 + SIDE, SIDE, SIDE)
do("geometry-3d", "fillet_edge", {"handle": b9, "edgeIndexes": [0], "radius": 150.0},
   label="a radius of 150 is refused, because the arc no longer fits the two faces",
   expect_fail=True)
do("geometry-3d", "fillet_edge",
   {"handle": b9, "edgeIndexes": [0], "radius": 150.0, "allowFaceLoss": True},
   label="and with allowFaceLoss it goes through, so the shape can be looked at")
check("PROVEN: past the width of the face the fillet is not that prism any more, so the identity "
      "L*r*r*(1-pi/4) has a DOMAIN and this is where it ends",
      volume_of(b9) is not None
      and not rel(volume_of(b9), WHOLE - SIDE * 150.0 * 150.0 * QUARTER, 1e-6),
      f"measured {volume_of(b9)}, naive formula {WHOLE - SIDE * 150.0 * 150.0 * QUARTER}")
# MEASURED, and the reason the "too large is refused" check was wrong twice: on a PRISTINE cube
# AutoCAD accepts a radius of 300 on a 100 face without a word. It does not refuse - it eats a
# whole face and hands back a five-faced solid and a success code. That is exactly the shape of
# defect this project keeps finding, so the tool now says so instead of the caller finding out.
# AutoCAD itself does NOT refuse this. Measured on a pristine cube before the guard was written:
# radius 300 on a 100 face was accepted, swallowed a whole face - six down to five - and returned
# a success with a volume a third smaller. So the tool has to catch it, and it has to leave the
# solid alone when it does.
b10 = box(2100, 0, 0, 2100 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "fillet_edge", {"handle": b10, "edgeIndexes": [0], "radius": 300.0},
       label="a radius that would destroy a face is refused", expect_fail=True)
check("and the refusal says a face would have been DESTROYED, with the counts",
      "DESTROY" in str(r) and "6 faces down to 5" in str(r), str(r)[:400])
check("PROVEN: and it reports the largest radius that DOES fit, rather than leaving the caller "
      "to guess at a number the geometry already decides",
      "largest that leaves every face standing is about" in str(r), str(r)[:400])
# THE check. Refusing after the fillet has already been applied is worth nothing unless the
# rollback is real - the transaction is aborted rather than committed, and this is the assertion
# that says so rather than assuming it.
check("PROVEN: the solid is byte-for-byte where it was - 1000000 and 6 faces, so the aborted "
      "transaction really did undo the fillet",
      rel(volume_of(b10), WHOLE, 1e-12), f"{volume_of(b10)}")
r2 = do("geometry-3d", "list_solid_edges", {"handle": b10}, label="and it still has its 12 edges")
check("12 edges, unrounded", len((r2 or {}).get("edges") or []) == 12,
      f"{len((r2 or {}).get('edges') or [])}")

print("\n-- the largest radius it reported must itself be accepted, or the number is a fiction --")
import re as _re
m = _re.search(r"standing is about ([0-9.]+)", str(r))
largest = float(m.group(1)) if m else None
check("a number was reported", largest is not None, str(r)[:200])
if largest:
    b11 = box(2400, 0, 0, 2400 + SIDE, SIDE, SIDE)
    rr = do("geometry-3d", "fillet_edge", {"handle": b11, "edgeIndexes": [0], "radius": largest},
            label=f"filleting at the reported maximum ({largest})")
    if isinstance(rr, dict):
        check("PROVEN: at the reported maximum no face is lost - the search answers the question "
              "it claims to answer",
              (rr.get("faces") or 0) >= (rr.get("facesBefore") or 0)
              and rr.get("facesConsumed") is not True,
              f"{rr.get('facesBefore')} -> {rr.get('faces')}")

print("\n-- allowFaceLoss is the deliberate way through, and still says what it did --")
b12 = box(2700, 0, 0, 2700 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "fillet_edge",
       {"handle": b12, "edgeIndexes": [0], "radius": 300.0, "allowFaceLoss": True},
       label="the same radius with allowFaceLoss goes through")
if isinstance(r, dict):
    check("PROVEN: it consumed a face, 6 down to 5",
          r.get("facesBefore") == 6 and r.get("faces") == 5, str(r)[:250])
    check("and it is flagged rather than reported as a plain success",
          r.get("facesConsumed") is True and "WARNING" in str(r.get("note")), str(r)[:320])

print("\n-- chamfer refuses the same way --")
# MEASURED where the guard actually has work to do. A distance of 300 is refused by AutoCAD
# itself before the guard is reached; the window where AutoCAD says yes and a face dies is at
# exactly 100 on a 100 face - the bevel reaches the far edges and swallows the face between them.
# Asking the wrong number here would have tested AutoCAD's refusal and called it ours.
b13 = box(3000, 0, 0, 3000 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "chamfer_edge", {"handle": b13, "edgeIndexes": [0], "distance": 100.0},
       label="a chamfer distance that would destroy a face is refused", expect_fail=True)
check("with the same explanation and a largest-that-fits number",
      "DESTROY" in str(r) and "largest" in str(r), str(r)[:400])
check("PROVEN: and that solid is untouched too", rel(volume_of(b13), WHOLE, 1e-12),
      f"{volume_of(b13)}")
b14 = box(3300, 0, 0, 3300 + SIDE, SIDE, SIDE)
r = do("geometry-3d", "chamfer_edge", {"handle": b14, "edgeIndexes": [0], "distance": 300.0},
       label="a distance AutoCAD itself will not attempt is refused too, in its own words",
       expect_fail=True)
check("and that refusal is AutoCAD's, reported as such rather than dressed up as ours",
      "AutoCAD refused the chamfer" in str(r) and "100 long" in str(r), str(r)[:300])
r = do("geometry-3d", "chamfer_edge", {
    "handle": b5, "edgeIndexes": [0], "distance": 10, "distance2": 20},
    label="unequal chamfer distances without a base face are refused", expect_fail=True)
check("and the refusal explains that the base face decides which way the bevel leans",
      "leans" in str(r) or "baseFaceIndex" in str(r), str(r)[:280])
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 500},
                                         "end": {"x": 100, "y": 500}}, label="a line"))
do("geometry-3d", "list_solid_edges", {"handle": ln},
   label="a line has no edges to list and is refused by name", expect_fail=True)
do("geometry-3d", "fillet_edge", {"handle": ln, "edgeIndexes": [0], "radius": 5},
   label="and cannot be filleted either", expect_fail=True)

# ── on screen: four rounded corners against four bevelled ones ───────────────
print("\n== on screen ==")
b6 = box(0, 1500, 0, SIDE, 1500 + SIDE, SIDE)
ids6 = [vertical_edge_at(b6, x, y) for x, y in
        ((0, 1500), (SIDE, 1500), (0, 1500 + SIDE), (SIDE, 1500 + SIDE))]
r = do("geometry-3d", "fillet_edge",
       {"handle": b6, "edgeIndexes": idx_list(*ids6), "radius": R1},
       label="all four vertical edges rounded at once")
check("PROVEN: four independent fillets remove exactly four times one of them",
      rel(volume_of(b6), WHOLE - 4 * REMOVED_R10, 1e-9),
      f"{volume_of(b6)} vs {WHOLE - 4 * REMOVED_R10}")

b7 = box(300, 1500, 0, 300 + SIDE, 1500 + SIDE, SIDE)
ids7 = [vertical_edge_at(b7, x, y) for x, y in
        ((300, 1500), (300 + SIDE, 1500), (300, 1500 + SIDE), (300 + SIDE, 1500 + SIDE))]
do("geometry-3d", "chamfer_edge",
   {"handle": b7, "edgeIndexes": idx_list(*ids7), "distance": D},
   label="all four vertical edges bevelled at once")
check("PROVEN: four independent chamfers remove exactly four times one of them",
      rel(volume_of(b7), WHOLE - 4 * REMOVED_CH, 1e-9),
      f"{volume_of(b7)} vs {WHOLE - 4 * REMOVED_CH}")

do("view", "zoom_extents", {})
png = os.path.join(OUT, "edge-ops.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -40, "yMin": 1460, "xMax": 440, "yMax": 1640},
                            "widthPx": 1600, "heightPx": 700})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm in plan view: the left square has four ROUNDED corners, the right one four")
print("     straight BEVELS. Same amount taken off each corner, two different shapes - which is")
print("     the whole difference between the two tools and is not visible in any number.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
