# -*- coding: utf-8 -*-
"""Live verification for roadmap 6.1, second tranche — acad-lights, 6 tools.

Three kinds of light that differ mainly in which fields MEAN anything, so the controls are about
keeping them distinct rather than about arithmetic:

  * All three kinds are created in ONE drawing and then listed. A tool that made the same object
    whatever was asked would pass every single-light check and fail the list, where the three
    types must read back as three DIFFERENT types.
  * The spot cone is 0.3 / 0.6 radians - two different values, neither a default, so a swap or a
    defaulting implementation fails. And an inverted cone (hotspot wider than falloff) must be
    refused, then refused AGAIN when only one angle is changed to a value that would invert it
    against the stored other - which is the case a per-call check would miss.
  * set_light_properties changes one property and the others are asserted unchanged, the same
    neighbour-clobbering guard as acad-materials.
  * Moving the target of a POINT light must be refused: a point light has nothing to aim, and a
    tool that accepted it would be silently writing a field with no meaning.
  * Every light is asymmetric in position - no two share a coordinate - so a mix-up between them
    cannot pass.
"""
import math
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "geometry-3d", "lights")}
results = []

HOT, FALL = 0.3, 0.6          # two different radian values, neither a default


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
        detail = f"  (refused as intended: {str(r)[-105:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def near(a, b, tol=1e-6):
    return a is not None and abs(a - b) <= tol


print("== fresh drawing ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

r = do("lights", "list_lights", {}, label="a fresh drawing has no lights")
check("PROVEN it starts empty, so a count later is meaningful",
      isinstance(r, dict) and r.get("count") == 0, str(r)[:150])

# ── the three kinds, all in one drawing ─────────────────────────────────────
print("\n== the three kinds must read back as three DIFFERENT types ==")
r = do("lights", "create_point_light",
       {"name": "TB_POINT", "position": {"x": 10, "y": 20, "z": 30}, "intensity": 2.5,
        "color": {"r": 255, "g": 200, "b": 100}})
if isinstance(r, dict):
    l = r.get("light") or {}
    check("the point light reports its type, position and intensity",
          l.get("type") == "PointLight" and near((l.get("position") or {}).get("z"), 30)
          and near(l.get("intensity"), 2.5), str(l)[:250])
    check("PROVEN a point light has NO target - there is nothing to aim, and reporting one would "
          "be inventing a field", l.get("hasTarget") is not True, str(l)[:200])

r = do("lights", "create_spot_light",
       {"name": "TB_SPOT", "position": {"x": 100, "y": 0, "z": 50},
        "target": {"x": 0, "y": 0, "z": 0}, "hotspotAngle": HOT, "falloffAngle": FALL})
if isinstance(r, dict):
    l = r.get("light") or {}
    check(f"PROVEN the cone is {HOT} / {FALL} and not swapped or defaulted - two different values, "
          f"neither one AutoCAD would pick on its own",
          near(l.get("hotspotAngle"), HOT) and near(l.get("falloffAngle"), FALL), str(l)[:250])
    check("and a spot light DOES have a target", l.get("hasTarget") is True, str(l)[:200])

r = do("lights", "create_distant_light",
       {"name": "TB_SUN", "direction": {"x": 0, "y": 0, "z": -1}})
if isinstance(r, dict):
    check("a distant light is created from a direction alone",
          (r.get("light") or {}).get("type") == "DistantLight", str(r.get("light"))[:200])

r = do("lights", "list_lights", {})
if isinstance(r, dict):
    types = sorted((x.get("type") or "") for x in (r.get("lights") or []))
    check("PROVEN the three kinds are genuinely different objects: DistantLight, PointLight and "
          "SpotLight all read back distinctly. A tool that built the same thing whatever was "
          "asked would have passed every check above and failed here",
          r.get("count") == 3 and types == ["DistantLight", "PointLight", "SpotLight"], str(types))
    check("and all three are on by default", r.get("onCount") == 3, str(r)[:200])

# ── the cone guard, including the case a per-call check would miss ──────────
print("\n== the inverted-cone guard ==")
do("lights", "create_spot_light",
   {"name": "TB_BAD", "position": {"x": 0, "y": 0, "z": 10}, "target": {"x": 0, "y": 0, "z": 0},
    "hotspotAngle": 0.9, "falloffAngle": 0.2},
   label="a hotspot wider than the falloff is refused at creation", expect_fail=True)
r = do("lights", "set_light_properties", {"name": "TB_SPOT", "hotspotAngle": 0.9},
       label="and refused when only ONE angle is changed to a value that inverts it", expect_fail=True)
check("PROVEN the guard compares against the STORED other angle, not just the two in this call - "
      "a per-call check would have let this through",
      "falloffAngle" in str(r) and "0.6" in str(r), str(r)[:250])
r = do("lights", "set_light_properties", {"name": "TB_SPOT", "hotspotAngle": 0.5},
       label="a hotspot that still fits inside the stored falloff is accepted")
if isinstance(r, dict):
    l = r.get("light") or {}
    check("and the falloff carried over untouched from what was already set",
          near(l.get("hotspotAngle"), 0.5) and near(l.get("falloffAngle"), FALL), str(l)[:220])

# ── neighbour-clobbering, the same guard as materials ───────────────────────
print("\n== set_light_properties: untouched properties must stay untouched ==")
r = do("lights", "set_light_properties", {"name": "TB_POINT", "intensity": 7.5})
if isinstance(r, dict):
    l = r.get("light") or {}
    check("PROVEN only intensity changed: position, colour and on-state are exactly what they were",
          near(l.get("intensity"), 7.5) and near((l.get("position") or {}).get("z"), 30)
          and (l.get("color") or {}).get("g") == 200 and l.get("on") is True, str(l)[:300])
    check("and the previous intensity is reported so it can be undone",
          near((r.get("before") or {}).get("intensity"), 2.5), str(r.get("before"))[:180])

r = do("lights", "set_light_properties", {"name": "TB_POINT", "on": False})
check("turning it off reads back off, and the intensity survives",
      isinstance(r, dict) and (r.get("light") or {}).get("on") is False
      and near((r.get("light") or {}).get("intensity"), 7.5), str(r.get("light"))[:220])
r = do("lights", "list_lights", {})
check("PROVEN a light that is OFF is still listed - it is still in the drawing - and onCount "
      "reports 2 of 3",
      isinstance(r, dict) and r.get("count") == 3 and r.get("onCount") == 2, str(r)[:200])

# ── the field that means nothing on a point light ───────────────────────────
r = do("lights", "set_light_properties", {"name": "TB_POINT", "target": {"x": 1, "y": 1, "z": 1}},
       label="moving the TARGET of a point light is refused", expect_fail=True)
check("PROVEN it refuses rather than silently writing a field with no meaning, and says why",
      "no target" in str(r).lower() or "nothing to aim" in str(r).lower(), str(r)[:220])

do("lights", "set_light_properties", {"name": "TB_POINT"},
   label="a change with nothing to change is refused", expect_fail=True)
do("lights", "set_light_properties", {"name": "NO_SUCH", "on": True},
   label="an unknown light is refused and points at list_lights", expect_fail=True)
do("lights", "create_point_light", {"name": "TB_POINT", "position": {"x": 0, "y": 0, "z": 0}},
   label="a duplicate light name is refused - lights are addressed by name", expect_fail=True)
do("lights", "create_spot_light", {"name": "TB_NOAIM", "position": {"x": 0, "y": 0, "z": 5}},
   label="a spot light with no target is refused", expect_fail=True)
do("lights", "create_distant_light", {"name": "TB_NODIR"},
   label="a distant light with neither direction nor two points is refused", expect_fail=True)

# ── delete ──────────────────────────────────────────────────────────────────
print("\n== delete_light ==")
r = do("lights", "delete_light", {"name": "TB_SUN"})
check("it reports the previous settings in full, so the light could be recreated",
      isinstance(r, dict) and (r.get("previous") or {}).get("type") == "DistantLight", str(r)[:220])
r = do("lights", "list_lights", {})
if isinstance(r, dict):
    names = sorted(x.get("name") for x in (r.get("lights") or []))
    check("and exactly the other two remain", names == ["TB_POINT", "TB_SPOT"], str(names))
do("lights", "delete_light", {"name": "TB_SUN"},
   label="deleting it twice is refused", expect_fail=True)

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
