# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.1 tranche 1 — sweep_curve, loft_curves, draw_helix.

These three are unusual in this project: they can be checked against ARITHMETIC rather than
against another call of the same code. That is a much better kind of evidence, so it is what the
file uses:

* **sweep_curve** — Pappus: the volume is the profile area times the distance travelled by its
  CENTROID. With the profile centred on the path that is the path length, straight or bent alike:
  a circle of radius 10 swept 200 must give 62831.85, and a quarter-arc path must still give area
  times arc length. A tool that scaled, twisted or failed to align the profile lands somewhere
  else while still returning a perfectly good solid.

* **loft_curves** — two identical sections a distance D apart make a prism: area x D. A taper to
  a smaller section must give LESS than that, which is the check that separates a real loft from
  one that quietly extruded the first section.

* **draw_helix** — a constant-radius helix unrolls into a right triangle, so its length is
  sqrt((2 pi r n)^2 + h^2). Nothing about that number comes from AutoCAD, and it earned its keep
  on the first run: a helix asked for 5 turns came back with 300, measuring 75364 against the
  1291.95 the arithmetic called for. Height, Turns and TurnHeight are three views of the same
  geometry and setting one recomputes another, so the assignment order decides the result - and
  a 300-turn helix looks like a perfectly good helix to any check that only asks whether one was
  made.

Volumes are read back with get_volume, not taken from the creating tool's own report.
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


def rel(a, b, tol=0.02):
    """Within tol RELATIVE - solid modellers tessellate, so exact equality is the wrong test."""
    return a is not None and b is not None and b != 0 and abs(a - b) / abs(b) <= tol


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def volume_of(h):
    """Read back with get_volume, not taken from the creating tool's own account."""
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    if not ok or not isinstance(r, dict):
        return None
    for k in ("volume", "value"):
        if r.get(k) is not None:
            return r[k]
    return None


def length_of(h):
    ok, r = S["geometry-2d"].call("get_curve_length", {"handle": h})
    return (r or {}).get("value") if ok and isinstance(r, dict) else None


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
    okp, rp = S["geometry-2d"].call("draw_circle",
                                    {"center": {"x": 0, "y": 9000}, "radius": 5})
    probe = hnd(rp) if okp else None
    ok2, bb = S["geometry-3d"].call("get_3d_bounding_box", {"handle": probe}) if probe else (False, None)
    check("the geometry-2d and geometry-3d sessions are on the SAME drawing",
          bool(ok2), f"probe={probe}; geometry-3d answered {str(bb)[:160]}")
    if probe:
        S["geometry-2d"].call("delete_entities", {"handles": [probe]})


print("== fresh drawing ==")
fresh_drawing()

# ── sweep_curve, against area x length ────────────────────────────────────────
print("\n== sweep_curve: a circle carried along a straight path ==")
R, LEN = 10.0, 200.0
AREA = math.pi * R * R
prof = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 0, "y": 0}, "radius": R},
              label=f"a circle of radius {R}, area {AREA:.3f}"))
path = hnd(do("geometry-2d", "draw_line",
              {"start": {"x": 0, "y": 0}, "end": {"x": 0, "y": LEN}},
              label=f"a straight path {LEN} long"))
# The path runs in Y and the circle lies in XY, so align='path' must turn the profile square to
# it. Without that the sweep would smear the circle along its own plane and the volume would be
# nothing like area x length - which is exactly what the arithmetic below would catch.
r = do("geometry-3d", "sweep_curve", {"profileHandle": prof, "pathHandle": path,
                                      "align": "path"})
sw = hnd(r)
if isinstance(r, dict):
    check("it reports the profile area", rel(r.get("profileArea"), AREA, 1e-6),
          f"{r.get('profileArea')} vs {AREA}")
    check("and the path length", rel(r.get("pathLength"), LEN, 1e-9),
          f"{r.get('pathLength')} vs {LEN}")
vol = volume_of(sw)
# THE check. Nothing about this number comes from AutoCAD.
check(f"PROVEN against arithmetic: the volume is area x length = {AREA * LEN:.1f}",
      rel(vol, AREA * LEN), f"measured {vol} vs computed {AREA * LEN}")

print("\n-- a curved path gives a DIFFERENT volume, which is the geometry not an error --")
prof2 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 600, "y": 0}, "radius": R},
               label="another circle"))
arcpath = hnd(do("geometry-2d", "draw_arc",
                 {"center": {"x": 600, "y": 0}, "radius": 150,
                  "startAngleDeg": 0, "endAngleDeg": 90}, label="a quarter-arc path"))
r = do("geometry-3d", "sweep_curve", {"profileHandle": prof2, "pathHandle": arcpath,
                                      "align": "path"})
sw2 = hnd(r)
v2 = volume_of(sw2)
arc_len = length_of(arcpath)
# Pappus: the volume is the area times the distance travelled by the profile's CENTROID. An
# earlier version of this check demanded the bent case DIFFER from area x path length, and was
# wrong: the profile is centred on the path, so its centroid travels exactly the path and the
# two agree. Measured 74021.917 against 74022.033 - equal to six significant figures, which is
# the theorem holding, not a coincidence.
check("PROVEN: it has a real volume", v2 is not None and v2 > 0, f"{v2}")
check("PROVEN: on a BENT path it is still area x length, because the centroid rides the path",
      rel(v2, AREA * (arc_len or 0), 0.001),
      f"volume {v2}, area x arc length {AREA * (arc_len or 0)}")

print("\n-- refusals --")
do("geometry-3d", "sweep_curve", {"profileHandle": prof2},
   label="a missing path is refused", expect_fail=True)
r = do("geometry-3d", "sweep_curve", {"profileHandle": arcpath, "pathHandle": arcpath},
       label="sweeping a curve along itself is refused", expect_fail=True)
check("and the refusal says so plainly", "along itself" in str(r), str(r)[:250])
openline = hnd(do("geometry-2d", "draw_line",
                  {"start": {"x": 900, "y": 0}, "end": {"x": 1000, "y": 0}},
                  label="an OPEN curve"))
r = do("geometry-3d", "sweep_curve", {"profileHandle": openline, "pathHandle": path},
       label="an open profile is refused - it would make a surface, not a solid",
       expect_fail=True)
check("and the refusal explains that", "CLOSED" in str(r) or "closed" in str(r), str(r)[:250])
do("geometry-3d", "sweep_curve", {"profileHandle": prof2, "pathHandle": path,
                                  "align": "sideways"},
   label="an unknown align is refused", expect_fail=True)

# ── loft_curves, against area x distance ──────────────────────────────────────
print("\n== loft_curves: two EQUAL sections make a prism ==")
D = 120.0
c1 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 0, "y": 600}, "radius": R},
            label="a circle at z=0"))
c2 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 0, "y": 600}, "radius": R},
            label="an identical one, about to be lifted"))
do("modify", "move", {"handles": [c2], "from": {"x": 0, "y": 600, "z": 0},
                      "to": {"x": 0, "y": 600, "z": D}}, label=f"lifted {D} in z")
r = do("geometry-3d", "loft_curves", {"profileHandles": [c1, c2]})
lf = hnd(r)
vl = volume_of(lf)
check(f"PROVEN against arithmetic: two equal circles {D} apart give area x D = {AREA * D:.1f}",
      rel(vl, AREA * D), f"measured {vl} vs computed {AREA * D}")

print("\n-- a TAPER must give less than the prism --")
c3 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 400, "y": 600}, "radius": R},
            label="a base circle"))
c4 = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 400, "y": 600}, "radius": R / 2},
            label="a HALF-radius top circle"))
do("modify", "move", {"handles": [c4], "from": {"x": 400, "y": 600, "z": 0},
                      "to": {"x": 400, "y": 600, "z": D}}, label=f"lifted {D}")
r = do("geometry-3d", "loft_curves", {"profileHandles": [c3, c4]})
lt = hnd(r)
vt = volume_of(lt)
# A cone frustum: (h/3)(A1 + A2 + sqrt(A1*A2)). Computed here, not asked of AutoCAD.
a1, a2 = AREA, math.pi * (R / 2) ** 2
frustum = (D / 3) * (a1 + a2 + math.sqrt(a1 * a2))
check(f"PROVEN: the taper matches the frustum formula, {frustum:.1f}",
      rel(vt, frustum, 0.03), f"measured {vt} vs computed {frustum}")
check("PROVEN: and it is LESS than the straight prism, as a taper must be",
      vt is not None and vl is not None and vt < vl, f"taper {vt} vs prism {vl}")

print("\n-- refusals --")
do("geometry-3d", "loft_curves", {"profileHandles": [c1]},
   label="one cross section is refused", expect_fail=True)
r = do("geometry-3d", "loft_curves",
       {"profileHandles": [c1, c2], "guideHandles": [path], "pathHandle": path},
       label="guides AND a path together are refused", expect_fail=True)
check("and the refusal says they are alternatives",
      "alternatives" in str(r), str(r)[:250])

# ── draw_helix, against its own hypotenuse ────────────────────────────────────
print("\n== draw_helix: length is the unrolled right triangle ==")
HR, HH, HT = 40.0, 300.0, 5.0
r = do("geometry-3d", "draw_helix", {"center": {"x": 1200, "y": 0}, "baseRadius": HR,
                                     "height": HH, "turns": HT})
hx = hnd(r)
computed = math.sqrt((2 * math.pi * HR * HT) ** 2 + HH ** 2)
if isinstance(r, dict):
    check("it reports the turns and height it was given",
          rel(r.get("turns"), HT, 1e-9) and rel(r.get("height"), HH, 1e-9), str(r)[:250])
    check("and the turn height is height / turns",
          rel(r.get("turnHeight"), HH / HT, 1e-6), f"{r.get('turnHeight')} vs {HH / HT}")
measured = length_of(hx)
# THE check. sqrt((2*pi*40*5)^2 + 300^2) is arithmetic, not a second opinion from AutoCAD.
check(f"PROVEN against arithmetic: the helix is {computed:.2f} long",
      rel(measured, computed), f"measured {measured} vs computed {computed}")

print("\n-- a taper reports no expectation, because the triangle no longer applies --")
r = do("geometry-3d", "draw_helix", {"center": {"x": 1400, "y": 0}, "baseRadius": HR,
                                     "topRadius": HR / 2, "height": HH, "turns": HT},
       label="a tapering helix")
check("PROVEN: expectedLength is withheld rather than given wrongly",
      isinstance(r, dict) and r.get("expectedLength") is None, str(r)[:250])

print("\n-- refusals --")
do("geometry-3d", "draw_helix", {"center": {"x": 0, "y": 0}, "baseRadius": 10, "height": 100},
   label="a missing turns count is refused", expect_fail=True)
do("geometry-3d", "draw_helix", {"center": {"x": 0, "y": 0}, "baseRadius": 0,
                                 "height": 100, "turns": 3},
   label="a zero base radius is refused", expect_fail=True)
do("geometry-3d", "draw_helix", {"center": {"x": 0, "y": 0}, "baseRadius": 10,
                                 "height": 100, "turns": 0},
   label="zero turns is refused", expect_fail=True)

# ── the helix is a real sweep path ────────────────────────────────────────────
print("\n== and a helix is a PATH: sweep a circle along it to get a spring ==")
tiny = hnd(do("geometry-2d", "draw_circle", {"center": {"x": 1200 + HR, "y": 0}, "radius": 4},
              label="a small circle at the helix start"))
r = do("geometry-3d", "sweep_curve", {"profileHandle": tiny, "pathHandle": hx,
                                      "align": "path"}, label="swept along the helix")
spring = hnd(r) if isinstance(r, dict) else None
vs = volume_of(spring) if spring else None
wire = math.pi * 16 * computed
# Pappus again: a small section carried along a long path encloses about its area times the
# path. This failed with eGeneralModelingFailure while the helix still had 300 turns - the
# wire self-intersected at a turn height of 1 - so it is downstream evidence that the helix
# fix took, not an independent check.
check("PROVEN: the spring's volume is about the wire area times the helix length",
      rel(vs, wire, 0.05), f"measured {vs} vs computed {wire}")

# ── on screen ─────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "sweep-loft.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -100, "yMin": -250, "xMax": 1600, "yMax": 800},
                            "widthPx": 2000, "heightPx": 1200})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> confirm in plan view: a swept tube running up from the origin, a second curving")
print("     round a quarter arc, two lofts near y=600 - one a straight cylinder and one")
print("     tapering - and on the right a helix with a spring swept along it.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
