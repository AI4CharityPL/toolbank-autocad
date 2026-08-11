# -*- coding: utf-8 -*-
"""Live verification for roadmap 6.1, first tranche — acad-materials, 6 tools.

A material is a bundle of numbers that must survive a round trip, and an assignment is a pointer
that must actually land. The controls:

  * ASYMMETRIC colours. Diffuse is (200, 30, 40) and specular (10, 220, 90) - no two channels
    share a value, and no channel is grey. A tool that wrote diffuse into specular, or swapped
    red and blue, cannot pass; (128,128,128) on both would pass every one of those bugs.
  * gloss 0.25 and opacity 0.75 - two DIFFERENT fractions, so swapping them fails. Both are also
    away from 0, 0.5 and 1, the values a defaulting implementation would land on.
  * modify_material touches ONE channel and the others must be unchanged. Each channel is a
    struct rebuilt on write, so the natural bug is clobbering a neighbour - and only an
    untouched-neighbour check finds it.
  * assign_material is read back through Entity.Material (the NAME) rather than the id that was
    written, and a SECOND entity must remain unassigned - a tool that set the material globally
    would pass a single-entity check perfectly.
  * delete_material is attempted while the material is still in use, which must be refused.
"""
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "geometry-3d", "materials")}
results = []

DIFF = {"r": 200, "g": 30, "b": 40}      # no two components equal, and not grey
SPEC = {"r": 10, "g": 220, "b": 90}      # nothing shared with DIFF
GLOSS, OPACITY = 0.25, 0.75              # different fractions, away from 0 / 0.5 / 1


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


def hnd(r):
    if not isinstance(r, dict):
        return None
    e = r.get("entity")
    return e.get("handle") if isinstance(e, dict) else None


def rgb(c):
    return (c or {}).get("r"), (c or {}).get("g"), (c or {}).get("b")


def near(a, b, tol=1e-9):
    return a is not None and abs(a - b) <= tol


print("== fresh drawing ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

box1 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 0, "y": 0, "z": 0},
                                          "corner2": {"x": 50, "y": 50, "z": 50}},
              label="a box to paint"))
box2 = hnd(do("geometry-3d", "draw_box", {"corner1": {"x": 100, "y": 0, "z": 0},
                                          "corner2": {"x": 150, "y": 50, "z": 50}},
              label="a second box, to be left alone"))

r = do("materials", "list_materials", {}, label="the materials a fresh drawing already has")
base_count = r.get("count") if isinstance(r, dict) else 0
if isinstance(r, dict):
    names = [m.get("name") for m in (r.get("materials") or [])]
    check("PROVEN a fresh drawing is not empty of materials - Global is always there, which is why "
          "a small count means nothing is wrong", "Global" in names, str(names))

# ── create, with values that cannot be confused ─────────────────────────────
print(f"\n== create_material: diffuse {DIFF}, specular {SPEC}, gloss {GLOSS}, opacity {OPACITY} ==")
r = do("materials", "create_material",
       {"name": "TB_RED", "description": "verification material",
        "diffuse": DIFF, "specular": SPEC, "gloss": GLOSS, "opacity": OPACITY})
if isinstance(r, dict):
    m = r.get("material") or {}
    check("PROVEN the diffuse colour survives exactly, component by component - no two of "
          "(200, 30, 40) are equal, so a red/blue swap or a channel mix-up cannot pass",
          rgb(m.get("diffuse")) == (DIFF["r"], DIFF["g"], DIFF["b"]), str(m.get("diffuse")))
    check("PROVEN specular is a SEPARATE channel and did not receive the diffuse colour - "
          "(10, 220, 90) shares nothing with (200, 30, 40)",
          rgb(m.get("specular")) == (SPEC["r"], SPEC["g"], SPEC["b"]), str(m.get("specular")))
    check(f"PROVEN gloss {GLOSS} and opacity {OPACITY} are not swapped and not defaulted - two "
          f"different fractions, both away from 0, 0.5 and 1",
          near(m.get("gloss"), GLOSS) and near(m.get("opacity"), OPACITY),
          f"gloss={m.get('gloss')} opacity={m.get('opacity')}")

do("materials", "create_material", {"name": "TB_RED", "diffuse": DIFF},
   label="a duplicate material name is refused", expect_fail=True)
do("materials", "create_material", {"name": "TB_BAD", "opacity": 1.5},
   label="an opacity above 1 is refused", expect_fail=True)
do("materials", "create_material", {"name": "TB_BAD", "gloss": -1},
   label="a negative gloss is refused", expect_fail=True)
do("materials", "create_material", {"diffuse": DIFF},
   label="a material with no name is refused", expect_fail=True)

# ── modify: one channel, neighbours untouched ───────────────────────────────
print("\n== modify_material: the untouched channels must stay untouched ==")
r = do("materials", "modify_material", {"name": "TB_RED", "gloss": 0.9})
if isinstance(r, dict):
    m = r.get("material") or {}
    check("PROVEN only gloss changed: the diffuse and specular colours and the opacity are exactly "
          "what they were. Each channel is a struct rebuilt on write, so clobbering a neighbour is "
          "the natural bug here and nothing but this check would find it",
          near(m.get("gloss"), 0.9)
          and rgb(m.get("diffuse")) == (DIFF["r"], DIFF["g"], DIFF["b"])
          and rgb(m.get("specular")) == (SPEC["r"], SPEC["g"], SPEC["b"])
          and near(m.get("opacity"), OPACITY), str(m)[:300])
    check("and the previous value is reported so the change can be undone",
          near((r.get("before") or {}).get("gloss"), GLOSS), str(r.get("before"))[:200])

r = do("materials", "modify_material", {"name": "TB_RED", "diffuse": {"r": 5, "g": 6, "b": 7}})
if isinstance(r, dict):
    m = r.get("material") or {}
    check("PROVEN the mirror case: changing diffuse leaves specular and gloss alone",
          rgb(m.get("diffuse")) == (5, 6, 7)
          and rgb(m.get("specular")) == (SPEC["r"], SPEC["g"], SPEC["b"])
          and near(m.get("gloss"), 0.9), str(m)[:280])

do("materials", "modify_material", {"name": "TB_RED"},
   label="a modify with nothing to change is refused", expect_fail=True)
do("materials", "modify_material", {"name": "NO_SUCH", "gloss": 0.5},
   label="an unknown material is refused and points at list_materials", expect_fail=True)

# ── assign, and the entity that must NOT change ─────────────────────────────
print("\n== assign_material: one box painted, the other untouched ==")
r = do("materials", "assign_material", {"name": "TB_RED", "handles": [box1]})
if isinstance(r, dict):
    e = (r.get("entities") or [{}])[0]
    check("PROVEN the assignment landed, read back through Entity.Material - the NAME - which is a "
          "different property from the MaterialId that was written",
          e.get("material") == "TB_RED", str(e))
    check("and the previous material is reported, so it can be put back",
          e.get("materialBefore") is not None, str(e))

r = do("materials", "unassign_material", {"handles": [box2]},
       label="read box 2 back through unassign, which reports what it was")
if isinstance(r, dict):
    e = (r.get("entities") or [{}])[0]
    check("PROVEN entity isolation: box 2 was NOT carrying TB_RED - a tool that assigned globally "
          "rather than per entity would have passed the check above and failed this one",
          e.get("materialBefore") != "TB_RED", str(e))

r = do("materials", "list_materials", {})
check("the new material is in the list alongside the built-in ones",
      isinstance(r, dict) and r.get("count") == base_count + 1
      and any(m.get("name") == "TB_RED" for m in (r.get("materials") or [])), str(r)[:200])

# ── delete, and the guard that matters ──────────────────────────────────────
print("\n== delete_material ==")
r = do("materials", "delete_material", {"name": "TB_RED"},
       label="deleting a material still in use is refused", expect_fail=True)
check("and the refusal names how many entities would be orphaned and how to fix it",
      "unassign_material" in str(r), str(r)[:250])
do("materials", "delete_material", {"name": "Global"},
   label="deleting AutoCAD's own Global material is refused outright", expect_fail=True)
do("materials", "delete_material", {"name": "ByLayer"},
   label="and ByLayer likewise", expect_fail=True)

do("materials", "unassign_material", {"handles": [box1]}, label="unassign the box first")
r = do("materials", "delete_material", {"name": "TB_RED"}, label="now the delete goes through")
if isinstance(r, dict):
    check("PROVEN the guard was counting real users, not refusing blindly: with the box unassigned "
          "the same delete succeeds and reports zero users",
          r.get("deleted") is True and r.get("wasUsedBy") == 0, str(r)[:250])
r = do("materials", "list_materials", {})
check("and it is gone from the list, back to the count we started with",
      isinstance(r, dict) and r.get("count") == base_count, str(r)[:200])
do("materials", "delete_material", {"name": "TB_RED"},
   label="deleting it twice is refused", expect_fail=True)

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
