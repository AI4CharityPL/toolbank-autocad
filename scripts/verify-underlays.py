# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.5 (underlay half) - acad-underlays, 5 tools - plus
acad-lights.create_web_light. Unblocked by the same discovery: real DWF/IES sample files already
exist on this machine under AutoCAD's own install tree, found when the user had none to supply
directly.

  DWF: AutoCAD 2025\\Sample\\Sheet Sets\\*\\*.dwf  (real Sheet Set publish output)
  IES: ProgramData\\Autodesk\\AutoCAD 2025\\R25.0\\plk\\WebFiles\\*.ies

attach_dgn_underlay is NOT tested here - it is withdrawn. Every .dgn on this machine (10 files,
all under UserDataCache\\Template) is a "Seed" file, an empty starting template for EXPORTING a
new DGN rather than real content, and every one refused to load with eInvalidInput regardless of
itemName. attach_dwf_underlay uses the IDENTICAL generic code path and works cleanly against a
real DWF, which is strong evidence the DGN implementation itself is correct and only the
available files are unsuitable - this script asserts attach_dgn_underlay is genuinely absent from
the catalog rather than pretending to test something that cannot be proven.

MEASURED live before this script reached its final form, and worth recording:
  * the first Map2Globe VBA-sample .dwf tried (a very old demo file) failed with eLoadFailed even
    though the code was correct - the newer Sheet Set .dwf files work. Old or unusual DWF variants
    can fail to load; that is a property of the file, not of this tool.
  * create_web_light initially threw eNoDatabase, because WebFile was set BEFORE the light was
    appended to the database - the same "append or PostToDb first" trap already catalogued for
    GeoLocationData.CoordinateSystem (rule 26 section 18), just not yet recorded for Light.WebFile.

Controls, matching the pattern proven out in verify-images.py:
  * attach_dwf_underlay is exercised with TWO DIFFERENT real DWF files under different names, so
    a tool that silently attached the same thing twice would be caught.
  * The SAME file attached twice under one name proves reusedDefinition, and detach_underlay's
    "survives the first removal, dies on the last" branch is exercised for real - the same
    shared-definition design as acad-images.
  * A second, untouched underlay is checked after clip/adjust on the first, so a tool acting
    globally instead of per-entity cannot pass by accident.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "underlays", "lights")}
results = []

SHEETSETS = r"C:\Program Files\Autodesk\AutoCAD 2025\Sample\Sheet Sets"
DWF1 = os.path.join(SHEETSETS, "Architectural", "IRD Addition.dwf")
DWF2 = os.path.join(SHEETSETS, "Civil", "Civil Sample Sheet Set.dwf")
IES1 = r"C:\ProgramData\Autodesk\AutoCAD 2025\R25.0\plk\WebFiles\sconce.ies"


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:260]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[-140:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


for p, name in [(DWF1, "DWF1"), (DWF2, "DWF2"), (IES1, "IES1")]:
    if not os.path.exists(p):
        raise SystemExit(f"Expected sample file missing: {name} at {p}")

print("== fresh drawing, cross-session probe (rule 26 section 13a) ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

do("underlays", "list_underlays", {}, label="list_underlays on a drawing with none yet is empty")

ok, r = S["underlays"].call("attach_dgn_underlay", {"path": DWF1, "insertionPoint": {"x": 0, "y": 0, "z": 0}})
check("attach_dgn_underlay is genuinely ABSENT from the catalog (withdrawn - see header)",
      (not ok) and ("not found" in str(r).lower() or "UnknownTool" in str(r)), str(r)[:200])

# ── attach: two DIFFERENT real DWF files under different names ──
print("\n== attach_dwf_underlay: two DIFFERENT real files ==")
do("underlays", "attach_dwf_underlay",
   {"path": "C:\\does\\not\\exist.dwf", "insertionPoint": {"x": 0, "y": 0, "z": 0}},
   label="a missing file is refused", expect_fail=True)

r1 = do("underlays", "attach_dwf_underlay",
        {"path": DWF1, "insertionPoint": {"x": 0, "y": 0, "z": 0}, "name": "ird"})
h1 = None
if isinstance(r1, dict):
    u = r1.get("underlay") or {}
    h1 = u.get("handle")
    check("PROVEN kind is reported as dwf", u.get("kind") == "dwf", str(u)[:200])
    check("reusedDefinition is false for the first placement", r1.get("reusedDefinition") is False,
          str(r1)[:200])

r2 = do("underlays", "attach_dwf_underlay",
        {"path": DWF2, "insertionPoint": {"x": 2000, "y": 0, "z": 0}, "name": "civil"})
h2 = None
if isinstance(r2, dict):
    h2 = (r2.get("underlay") or {}).get("handle")

if h1 and h2:
    check("the two DIFFERENT files got DIFFERENT handles", h1 != h2, f"h1={h1} h2={h2}")

do("underlays", "attach_dwf_underlay",
   {"path": DWF2, "insertionPoint": {"x": 5000, "y": 5000, "z": 0}, "name": "ird"},
   label="the same name against a DIFFERENT file is refused", expect_fail=True)

# ── shared definition: same file, same name, second placement ──
h3 = None
if h1:
    print("\n== attach_dwf_underlay again: SAME file, SAME name - must REUSE the definition ==")
    r3 = do("underlays", "attach_dwf_underlay",
            {"path": DWF1, "insertionPoint": {"x": 0, "y": 3000, "z": 0}, "name": "ird"})
    if isinstance(r3, dict):
        h3 = (r3.get("underlay") or {}).get("handle")
        check("PROVEN the definition was REUSED, not duplicated", r3.get("reusedDefinition") is True,
              str(r3)[:200])
    if h1 and h3:
        check("the two placements sharing a definition have different handles", h1 != h3,
              f"h1={h1} h3={h3}")

r = do("underlays", "list_underlays", {})
if isinstance(r, dict):
    check("PROVEN list_underlays enumerates every attached reference",
          r.get("count") == len([x for x in (h1, h2, h3) if x]), f"count={r.get('count')}")

# ── clip: only touch one entity, prove a second is untouched ──
if h2 and isinstance(r2, dict):
    print("\n== clip_underlay: half the local bounds on ONE entity only ==")
    u2 = r2.get("underlay") or {}
    do("underlays", "clip_underlay", {"handle": h2, "points": [{"x": 0, "y": 0}]},
       label="a single clip point is refused", expect_fail=True)

    r = do("underlays", "list_underlays", {})
    u2now = next((i for i in (r.get("underlays") or []) if i.get("handle") == h2), None) \
        if isinstance(r, dict) else None
    if u2now:
        r = do("underlays", "clip_underlay", {
            "handle": h2,
            "points": [{"x": 0, "y": 0}, {"x": 1, "y": 1}],
        }, label="clip to a small rectangle near the origin of local space")
        if isinstance(r, dict):
            check("clipped is true and the local bounds are reported",
                  r.get("clipped") is True and r.get("underlayWidth") is not None
                  and r.get("underlayHeight") is not None, str(r)[:300])
            ea = r.get("extentsAfter") or {}
            eb = r.get("extentsBefore") or {}
            spanX_after = (ea.get("max") or {}).get("x", 0) - (ea.get("min") or {}).get("x", 0)
            spanX_before = (eb.get("max") or {}).get("x", 0) - (eb.get("min") or {}).get("x", 0)
            check("PROVEN clipping shrank the drawing-space extents",
                  spanX_after < spanX_before, f"before={spanX_before} after={spanX_after}")

        r = do("underlays", "clip_underlay", {"handle": h2}, label="omitting points removes the clip")
        if isinstance(r, dict):
            check("PROVEN un-clipping returns clipped to false", r.get("clipped") is False, str(r)[:200])

    # isolation: h1 (a different entity, sharing NOTHING with h2) must be untouched
    if h1:
        r = do("underlays", "list_underlays", {})
        if isinstance(r, dict):
            u1now = next((i for i in (r.get("underlays") or []) if i.get("handle") == h1), None)
            check("PROVEN a DIFFERENT entity (h1) was untouched by clipping h2",
                  u1now is not None and u1now.get("clipped") is False, str(u1now)[:250])

# ── adjust: contrast/fade/monochrome, isolation against a second entity ──
if h1:
    print("\n== set_underlay_adjust ==")
    do("underlays", "set_underlay_adjust", {"handle": h1},
       label="nothing to change is refused", expect_fail=True)
    r = do("underlays", "set_underlay_adjust", {"handle": h1, "contrast": 70, "monochrome": True})
    if isinstance(r, dict):
        before, after = r.get("before") or {}, r.get("after") or {}
        check("PROVEN only contrast and monochrome changed - fade, not given, stayed the same",
              after.get("contrast") == 70 and after.get("monochrome") is True
              and after.get("fade") == before.get("fade"), str(r)[:300])
    if h2:
        r = do("underlays", "list_underlays", {})
        if isinstance(r, dict):
            u2now = next((i for i in (r.get("underlays") or []) if i.get("handle") == h2), None)
            adj2 = (u2now or {}).get("adjust") or {}
            check("PROVEN a DIFFERENT entity (h2) is untouched by adjusting h1",
                  adj2.get("monochrome") is not True, str(adj2)[:200])

# ── detach: shared definition survives the first removal, dies on the last ──
if h1 and h3:
    print("\n== detach_underlay: the shared DWF definition ==")
    r = do("underlays", "detach_underlay", {"handle": h3})
    if isinstance(r, dict):
        check("PROVEN the definition SURVIVED - h1 still uses it", r.get("defRemoved") is False,
              str(r)[:200])
    r = do("underlays", "detach_underlay", {"handle": h1})
    if isinstance(r, dict):
        check("PROVEN the definition was removed on the LAST placement", r.get("defRemoved") is True,
              str(r)[:200])
    do("underlays", "detach_underlay", {"handle": h1},
       label="detaching an already-erased handle is refused", expect_fail=True)

# ── create_web_light ──
print("\n== create_web_light ==")
do("lights", "create_web_light", {"name": "wl_bad", "position": {"x": 0, "y": 0, "z": 0},
                                   "path": "C:\\does\\not\\exist.ies"},
   label="a missing .ies file is refused", expect_fail=True)

r = do("lights", "create_web_light",
       {"name": "wl1", "position": {"x": 0, "y": 0, "z": 0}, "path": IES1, "intensity": 2.0})
if isinstance(r, dict):
    light = r.get("light") or {}
    check("PROVEN webFile reads back matching the resolved path, not merely echoed",
          light.get("webFile") and os.path.normcase(light.get("webFile")) == os.path.normcase(IES1),
          str(light)[:250])

do("lights", "create_point_light", {"name": "pl1", "position": {"x": 500, "y": 500, "z": 0}})
r = do("lights", "list_lights", {})
if isinstance(r, dict):
    wl = next((l for l in (r.get("lights") or []) if l.get("name") == "wl1"), None)
    pl = next((l for l in (r.get("lights") or []) if l.get("name") == "pl1"), None)
    check("PROVEN list_lights reports webFile for the web light and NOT for an ordinary point "
          "light, so the field is genuinely conditional rather than always present",
          wl is not None and wl.get("webFile") and (pl is None or not pl.get("webFile")),
          f"wl={wl} pl={pl}")

do("lights", "delete_light", {"name": "wl1"})
do("lights", "delete_light", {"name": "pl1"})

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
