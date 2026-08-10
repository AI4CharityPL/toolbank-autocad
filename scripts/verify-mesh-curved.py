# -*- coding: utf-8 -*-
"""Live verification for roadmap 4.3, third tranche — the curved primitives, and one experiment.

`create_mesh_sphere`, `create_mesh_cone`, `extrude_mesh_face`.

Arithmetic, worked out here rather than taken from the tools:

* a lat/long sphere cage of R rings and S segments is EXACTLY 2 + (R-1)*S vertices and R*S faces
* a mesh cone is a PYRAMID over an n-gon, so its volume is exactly (base area)/3 * height, where
  the base area of a regular n-gon inscribed in r is (n/2)*r*r*sin(2*pi/n) — less than the true
  pi*r*r*h/3, because the flat sides cut the corners off the circle
* both are inscribed polyhedra, so both come out SMALLER than the round shape they are named
  after, and raising the tessellation closes the gap — which is the control that says the
  shortfall is faceting rather than a fault

`extrude_mesh_face` was as much an EXPERIMENT as a tool, and the experiment has ANSWERED: a
hand-built `SubentityId` addresses nothing on a `SubDMesh`. AutoCAD accepts the call, reports
success, and leaves the cage at 8 vertices and 6 faces. `ExtrudeFaces` wants a
`FullSubentityPath[]` and `SubDMesh` exposes no `GetSubentityPathsAt*` family to produce a real
one, unlike `Solid3d`. The tool is withdrawn from the bank and this check keeps the finding
asserted, so nobody spends the same build cycle again.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "geometry-3d", "mesh", "view")}
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


def hnd(r):
    return ((r or {}).get("entity") or {}).get("handle")


def volume_of(h):
    ok, r = S["geometry-3d"].call("get_volume", {"handle": h})
    return (r or {}).get("volume") if ok and isinstance(r, dict) else None


def solid_volume_of_mesh(h):
    return volume_of(hnd(do("mesh", "convert_mesh_to_solid", {"handle": h},
                            label="converted to measure it")))


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
    # The probe below is not decoration. Each category is its own backend process and binds to a
    # document; if the backends are restarted mid-session - killed by a deploy, say - some of them
    # come back bound to a document that new_document has since replaced, and then handles minted
    # by one session are simply "not found" by another. Measured while writing this: the mesh
    # session kept minting 7F, 82, 84 across three consecutive new_document calls, which is a
    # session that never saw any of them. The remedy is a clean AutoCAD start before a run, and
    # this check is what makes the difference visible instead of showing up as arithmetic that
    # will not add up.
    #
    # A warm-up loop was tried here first, on the theory that a session binds late. It did not
    # help and has been removed rather than left in looking useful.
    probe = hnd(S["mesh"].call("create_mesh_box", {"corner1": {"x": 0, "y": 9000, "z": 0},
                                                   "corner2": {"x": 10, "y": 9010, "z": 10}})[1])
    ok2, conv = S["mesh"].call("convert_mesh_to_solid", {"handle": probe, "eraseSource": True})
    ph = ((conv or {}).get("entity") or {}).get("handle") if ok2 else None
    check("the mesh and geometry-3d sessions are on the SAME drawing",
          bool(ph) and rel(volume_of(ph), 1000.0, 1e-9), f"probe={probe} -> {ph}")
    if ph:
        S["geometry-2d"].call("delete_entities", {"handles": [ph]})


print("== fresh drawing ==")
fresh_drawing()

# ── create_mesh_sphere: the cage counts are exact ───────────────────────────
print("\n== create_mesh_sphere: a lat/long cage with counts known in advance ==")
R, SEGS, RINGS = 50.0, 12, 6
r = do("mesh", "create_mesh_sphere", {"center": {"x": 0, "y": 0, "z": 0}, "radius": R,
                                      "segments": SEGS, "rings": RINGS})
sph = hnd(r)
if isinstance(r, dict):
    check(f"PROVEN against arithmetic: 2 poles plus (rings-1)*segments = "
          f"{2 + (RINGS - 1) * SEGS} vertices, and rings*segments = {RINGS * SEGS} faces",
          r.get("vertices") == 2 + (RINGS - 1) * SEGS and r.get("faces") == RINGS * SEGS,
          str(r)[:250])
TRUE_SPHERE = 4.0 / 3.0 * math.pi * R ** 3
v_sph = solid_volume_of_mesh(sph)
check(f"PROVEN: it is a POLYHEDRON inscribed in the sphere, so it holds LESS than the true "
      f"{TRUE_SPHERE:.4f}",
      v_sph is not None and 0 < v_sph < TRUE_SPHERE, f"{v_sph} vs {TRUE_SPHERE}")

print("\n-- THE CONTROL: a finer cage closes the gap --")
r2 = do("mesh", "create_mesh_sphere", {"center": {"x": 200, "y": 0, "z": 0}, "radius": R,
                                       "segments": 32, "rings": 16})
if isinstance(r2, dict):
    check("PROVEN: 32 by 16 gives 2 + 15*32 = 482 vertices and 16*32 = 512 faces",
          r2.get("vertices") == 482 and r2.get("faces") == 512, str(r2)[:250])
v_sph2 = solid_volume_of_mesh(hnd(r2))
check("PROVEN: the finer cage holds MORE and is still under the true sphere - so the shortfall "
      "is the faceting, not a fault",
      v_sph2 is not None and v_sph is not None and v_sph < v_sph2 < TRUE_SPHERE,
      f"coarse {v_sph}, fine {v_sph2}, true {TRUE_SPHERE}")

# ── create_mesh_cone: a pyramid, exactly ────────────────────────────────────
print("\n== create_mesh_cone: a PYRAMID over an n-gon, exactly ==")
CR, CH, CN = 50.0, 100.0, 8
BASE = 0.5 * CN * CR * CR * math.sin(2 * math.pi / CN)
PYRAMID = BASE * CH / 3.0
TRUE_CONE = math.pi * CR * CR * CH / 3.0
r = do("mesh", "create_mesh_cone", {"basePoint": {"x": 400, "y": 0, "z": 0},
                                    "radius": CR, "height": CH, "sides": CN})
cone = hnd(r)
if isinstance(r, dict):
    check(f"PROVEN: the cage is n+1 vertices and n+1 faces - {CN + 1} and {CN + 1} - the base "
          f"plus {CN} triangles",
          r.get("vertices") == CN + 1 and r.get("faces") == CN + 1, str(r)[:250])
    check(f"and it reports BOTH figures: the pyramid it is, {PYRAMID:.4f}, and the cone it is "
          f"not, {TRUE_CONE:.4f}",
          rel(r.get("pyramidVolume"), PYRAMID) and rel(r.get("coneVolume"), TRUE_CONE),
          str(r)[:300])
v_cone = solid_volume_of_mesh(cone)
check(f"PROVEN against arithmetic: exactly one third of base area times height, {PYRAMID:.4f}",
      rel(v_cone, PYRAMID, 1e-6), f"{v_cone}")
check("and that is less than a true cone would hold", v_cone is not None and v_cone < TRUE_CONE,
      f"{v_cone} vs {TRUE_CONE}")

# ── extrude_mesh_face: the experiment ───────────────────────────────────────
print("\n== extrude_mesh_face: can a single mesh face be addressed at all? ==")
box = hnd(do("mesh", "create_mesh_box", {"corner1": {"x": 700, "y": 0, "z": 0},
                                         "corner2": {"x": 800, "y": 100, "z": 100}},
             label="a box mesh, 8 vertices and 6 faces"))
# ANSWERED, and the answer is no. AutoCAD accepted the call, returned success, and left the cage
# at 8 vertices and 6 faces - a hand-built SubentityId addresses nothing on a SubDMesh. The tool
# is therefore WITHDRAWN rather than shipped: it cannot be shown to work, and one that silently
# does nothing while reporting success is worse than an absent one.
r = do("mesh", "extrude_mesh_face", {"handle": box, "faceIndex": 1, "distance": 50.0},
       label="a hand-built face path addresses nothing, and the tool refuses rather than "
             "reporting the success AutoCAD gave it", expect_fail=True)
check("and the refusal says the cage came back unchanged, which is what makes this a finding "
      "rather than a guess",
      "cage is unchanged" in str(r) and "addressed nothing" in str(r), str(r)[:320])

print("\n-- refusals --")
do("mesh", "extrude_mesh_face", {"handle": box, "faceIndex": 99, "distance": 10},
   label="a face index out of range is refused", expect_fail=True)
do("mesh", "extrude_mesh_face", {"handle": box, "distance": 10},
   label="a missing face index is refused", expect_fail=True)
do("mesh", "create_mesh_sphere", {"center": {"x": 0, "y": 0, "z": 0}, "radius": 50, "rings": 1},
   label="a sphere of 1 ring is refused - that is not a sphere", expect_fail=True)
do("mesh", "create_mesh_cone", {"basePoint": {"x": 0, "y": 0, "z": 0}, "radius": 50, "height": 0},
   label="a cone of zero height is refused", expect_fail=True)
ln = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 400}, "end": {"x": 100, "y": 400}},
            label="a line"))
do("mesh", "extrude_mesh_face", {"handle": ln, "faceIndex": 0, "distance": 10},
   label="a line is refused by name", expect_fail=True)

# ── on screen ────────────────────────────────────────────────────────────────
print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "mesh-curved.png")
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -80, "yMin": -80, "xMax": 900, "yMax": 120},
                            "widthPx": 2400, "heightPx": 500})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 4000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print("  -> in plan view: the coarse sphere as a visible 12-sided polygon, the fine one looking")
print("     round, the cone as an octagon, and the box last. The first two side by side are the")
print("     faceting argument made visible.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
