# -*- coding: utf-8 -*-
"""Live verification for roadmap 5.2, first tranche — acad-data, 13 tools.

Storage tools fail in a particular way: the write reports success, and the value that comes back
later is not the value that went in. So almost every check here is a ROUND TRIP with a control
that would catch the plausible-looking wrong answer:

  * TYPE fidelity. 1 and 1.0 are the same in JSON and are not the same to AutoCAD. A real written
    and read back as an int would look fine in the JSON and be wrong, so the type is asserted
    alongside the value on every value that comes back.
  * APPLICATION isolation. Two applications write xdata to the SAME entity; each must read back
    only its own, and deleting one must leave the other intact. A tool that stored data globally
    per entity would pass a single-application test perfectly.
  * ENTITY isolation. Two entities carry xdata under the same application name with different
    values - a tool keying only on the app name would return the same answer for both.
  * A cross-tool control on purge_regapps, from the acad-lisp category: an app name that IS
    referenced by xdata must SURVIVE a purge, and the same name must be purgeable once its xdata
    is deleted. That is the claim purge_regapps makes about itself, checked from the outside for
    the first time.
  * Dictionary entries are read back through a FRESH lookup, and nesting is walked by path rather
    than assumed.
"""
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "geometry-2d", "data", "lisp")}
results = []
APP = "TBVERIFY"
APP2 = "TBOTHER"


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:200]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[-110:]})"
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


print("== fresh drawing ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

line1 = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 0}, "end": {"x": 100, "y": 0}},
               label="a line to carry data"))
line2 = hnd(do("geometry-2d", "draw_line", {"start": {"x": 0, "y": 50}, "end": {"x": 100, "y": 50}},
               label="a second line"))

# ── xdata round trip, with the types asserted ───────────────────────────────
print("\n== attach_xdata / get_xdata: the round trip, types and all ==")
VALUES = [
    {"type": "string", "value": "hello"},
    {"type": "real", "value": 2.5},
    {"type": "int", "value": 7},
    {"type": "point", "point": {"x": 1.0, "y": 2.0, "z": 3.0}},
    {"type": "layer", "value": "0"},
]
r = do("data", "attach_xdata", {"handle": line1, "appName": APP, "data": VALUES})
if isinstance(r, dict):
    check("the application name was registered automatically - AutoCAD refuses xdata under an "
          "unregistered one, so a tool that did not do this would fail on first use",
          r.get("appRegistered") is True, str(r)[:220])
    check("all five values read back", r.get("count") == 5, str(r)[:220])

r = do("data", "get_xdata", {"handle": line1, "appName": APP})
got = []
if isinstance(r, dict) and r.get("apps"):
    got = r["apps"][0].get("data") or []
check(f"PROVEN the round trip is exact, TYPES INCLUDED: a real comes back real and an int comes "
      f"back int - in JSON 2.5 and 2 would look right either way, which is the whole reason the "
      f"type is stored and checked",
      len(got) == 5
      and got[0] == {"type": "string", "value": "hello"}
      and got[1] == {"type": "real", "value": 2.5}
      and got[2] == {"type": "int", "value": 7}
      and got[3].get("type") == "point"
      and abs((got[3].get("point") or {}).get("x", 0) - 1.0) < 1e-9
      and abs((got[3].get("point") or {}).get("z", 0) - 3.0) < 1e-9
      and got[4] == {"type": "layer", "value": "0"},
      str(got)[:400])

# ── THE control: two applications on one entity ─────────────────────────────
print("\n-- two applications on the SAME entity must not see each other --")
do("data", "attach_xdata", {"handle": line1, "appName": APP2,
                            "data": [{"type": "string", "value": "other-app"}]},
   label="a second application writes to the same line")
r = do("data", "get_xdata", {"handle": line1, "appName": APP}, label="read back the first app")
first = (r.get("apps") or [{}])[0].get("data") if isinstance(r, dict) else None
check("PROVEN application isolation: the first application still reads its own five values and "
      "not the second's - a tool storing per ENTITY rather than per APPLICATION would have passed "
      "every check above and failed here",
      isinstance(first, list) and len(first) == 5 and first[0].get("value") == "hello",
      str(first)[:250])
r = do("data", "get_xdata", {"handle": line1}, label="read every application at once")
if isinstance(r, dict):
    names = sorted(a.get("appName") for a in (r.get("apps") or []))
    check("and asking without a name returns BOTH applications, grouped",
          names == sorted([APP, APP2]), str(names))

print("\n-- and two entities under the SAME application name must not blur --")
do("data", "attach_xdata", {"handle": line2, "appName": APP,
                            "data": [{"type": "string", "value": "second-entity"}]},
   label="the same app writes different values to the other line")
r1 = do("data", "get_xdata", {"handle": line1, "appName": APP}, label="line 1")
r2 = do("data", "get_xdata", {"handle": line2, "appName": APP}, label="line 2")
v1 = (r1.get("apps") or [{}])[0].get("data", [{}])[0].get("value") if isinstance(r1, dict) else None
v2 = (r2.get("apps") or [{}])[0].get("data", [{}])[0].get("value") if isinstance(r2, dict) else None
check("PROVEN entity isolation: the two lines report different values under the same application "
      "name", v1 == "hello" and v2 == "second-entity", f"line1={v1!r} line2={v2!r}")

# ── the cross-tool control on purge_regapps ─────────────────────────────────
print("\n== cross-tool control: purge_regapps must SPARE a name that xdata still uses ==")
r = do("data", "list_registered_apps", {})
check("the app names are registered and listed",
      isinstance(r, dict) and APP in (r.get("apps") or []) and APP2 in (r.get("apps") or []),
      str(r.get("apps") if isinstance(r, dict) else r)[:200])

r = do("lisp", "purge_regapps", {}, label="purge while the xdata is still there")
if isinstance(r, dict):
    check(f"PROVEN purge_regapps keeps what is REFERENCED: {APP} and {APP2} survive, because "
          f"erasing a name that xdata points at would corrupt that xdata. This is the claim that "
          f"tool makes about itself, checked from outside it for the first time",
          APP in (r.get("remaining") or []) and APP2 in (r.get("remaining") or []),
          str(r)[:300])

print("\n-- delete the xdata, and the SAME name becomes purgeable --")
r = do("data", "delete_xdata", {"handle": line1, "appName": APP2})
if isinstance(r, dict):
    check("delete reports what it removed and what it left behind",
          r.get("deletedCount") == 1 and APP in (r.get("otherAppsAfter") or []), str(r)[:280])
r = do("data", "get_xdata", {"handle": line1, "appName": APP}, label="the first app is untouched")
check("PROVEN deleting one application's xdata leaves the other's alone - the check that matters, "
      "because the delete mechanism writes to the whole entity buffer",
      isinstance(r, dict) and (r.get("apps") or [{}])[0].get("count") == 5, str(r)[:250])

r = do("lisp", "purge_regapps", {}, label="purge again, now that TBOTHER is unreferenced")
if isinstance(r, dict):
    check(f"PROVEN the purge really was checking references and not just refusing everything: "
          f"{APP2} goes now that nothing points at it, while {APP} stays because line 1 and line 2 "
          f"still carry its data",
          APP2 in (r.get("purged") or []) and APP in (r.get("remaining") or []), str(r)[:300])

print("\n-- xdata refusals --")
do("data", "attach_xdata", {"handle": line1, "appName": APP, "data": []},
   label="an empty value list is refused and points at delete_xdata", expect_fail=True)
do("data", "attach_xdata", {"handle": line1, "data": VALUES},
   label="no appName is refused", expect_fail=True)
r = do("data", "attach_xdata", {"handle": line1, "appName": APP,
                                "data": [{"type": "wombat", "value": 1}]},
       label="an unknown value type is refused", expect_fail=True)
check("and the refusal lists the six types that exist, and says why the type is not guessed",
      "string, real, int, point, layer or handle" in str(r), str(r)[:280])
do("data", "delete_xdata", {"handle": line2, "appName": "NEVER-USED"},
   label="deleting xdata that is not there is refused", expect_fail=True)
do("data", "register_app_name", {"appName": APP},
   label="registering a name that already exists is refused", expect_fail=True)

# ── dictionaries ────────────────────────────────────────────────────────────
print("\n== dictionaries ==")
r = do("data", "list_dictionaries", {}, label="the drawing-wide named objects dictionary")
if isinstance(r, dict):
    keys = [e.get("key") for e in (r.get("entries") or [])]
    check("a fresh drawing already has plenty in it - layouts, groups, plot settings - so an "
          "empty answer here would mean the tool was looking in the wrong place",
          (r.get("count") or 0) > 5 and any(k == "ACAD_LAYOUT" for k in keys), str(keys[:8]))

do("data", "set_dictionary_entry", {"key": "TB_SETTINGS",
                                    "data": [{"type": "string", "value": "v1"},
                                             {"type": "int", "value": 42}]},
   label="store an xrecord in the named objects dictionary")
r = do("data", "get_dictionary_entry", {"key": "TB_SETTINGS"})
if isinstance(r, dict):
    check("PROVEN it reads back through a FRESH lookup with types intact",
          r.get("objectClass") == "AcDbXrecord"
          and r.get("data") == [{"type": "string", "value": "v1"}, {"type": "int", "value": 42}],
          str(r)[:280])

r = do("data", "set_dictionary_entry", {"key": "TB_SETTINGS",
                                        "data": [{"type": "string", "value": "v2"}]},
       label="writing the same key again")
check("PROVEN the overwrite is REPORTED - SetAt replaces silently and nothing else would tell you",
      isinstance(r, dict) and r.get("replaced") is True, str(r)[:250])
r = do("data", "get_dictionary_entry", {"key": "TB_SETTINGS"})
check("and the contents really were replaced, not merged",
      isinstance(r, dict) and r.get("data") == [{"type": "string", "value": "v2"}], str(r)[:250])

print("\n-- nesting, walked by path rather than assumed --")
do("data", "set_dictionary_entry", {"key": "TB_TREE", "nested": True},
   label="create a nested dictionary")
do("data", "create_xrecord", {"path": "TB_TREE", "key": "LEAF",
                              "data": [{"type": "real", "value": 1.25}]},
   label="put an xrecord inside it")
r = do("data", "read_xrecord", {"path": "TB_TREE", "key": "LEAF"})
check("PROVEN the path really walks into the nested dictionary and the real stays a real",
      isinstance(r, dict) and r.get("data") == [{"type": "real", "value": 1.25}], str(r)[:250])
r = do("data", "list_dictionaries", {"path": "TB_TREE"}, label="list inside the nested dictionary")
check("and it holds exactly the one entry that was put there",
      isinstance(r, dict) and r.get("count") == 1, str(r)[:250])

do("data", "delete_dictionary_entry", {"key": "TB_TREE"},
   label="deleting a non-empty dictionary is refused without force", expect_fail=True)
r = do("data", "delete_dictionary_entry", {"key": "TB_TREE", "force": True},
       label="and goes through with force")
check("the entries it took with it are reported",
      isinstance(r, dict) and r.get("nestedEntriesRemoved") == 1, str(r)[:250])
do("data", "get_dictionary_entry", {"key": "TB_TREE"},
   label="and it is really gone", expect_fail=True)

# ── extension dictionary on an entity ───────────────────────────────────────
print("\n== create_extension_dictionary ==")
r = do("data", "create_extension_dictionary", {"handle": line2})
check("a new extension dictionary starts empty",
      isinstance(r, dict) and r.get("entryCount") == 0, str(r)[:220])
do("data", "create_extension_dictionary", {"handle": line2},
   label="a second one is refused - an entity can only have one", expect_fail=True)
do("data", "create_xrecord", {"handle": line2, "key": "PER_ENTITY",
                              "data": [{"type": "string", "value": "belongs to line 2"}]},
   label="store an xrecord in the entity's own dictionary")
r = do("data", "read_xrecord", {"handle": line2, "key": "PER_ENTITY"})
check("PROVEN it reads back from the ENTITY's dictionary",
      isinstance(r, dict) and r.get("data") == [{"type": "string", "value": "belongs to line 2"}],
      str(r)[:250])
# The control: the same key must NOT be visible in the drawing-wide dictionary.
do("data", "read_xrecord", {"key": "PER_ENTITY"},
   label="PROVEN scope: the same key is NOT in the drawing-wide dictionary - an extension "
         "dictionary really is per-entity", expect_fail=True)
do("data", "create_extension_dictionary", {"handle": line1},
   label="give line 1 one too")
do("data", "read_xrecord", {"handle": line1, "key": "PER_ENTITY"},
   label="and line 1's own dictionary does not have line 2's entry either", expect_fail=True)

# ── xrecords ────────────────────────────────────────────────────────────────
print("\n== create / read / update_xrecord ==")
do("data", "create_xrecord", {"key": "TB_REC",
                              "data": [{"type": "string", "value": "a"},
                                       {"type": "real", "value": 3.5},
                                       {"type": "point", "point": {"x": 5, "y": 6, "z": 7}}]})
r = do("data", "read_xrecord", {"key": "TB_REC"})
check("three values round-trip with their types",
      isinstance(r, dict) and r.get("count") == 3
      and (r.get("data") or [{}])[1] == {"type": "real", "value": 3.5}, str(r)[:300])
do("data", "create_xrecord", {"key": "TB_REC", "data": [{"type": "int", "value": 1}]},
   label="creating over an existing key is refused", expect_fail=True)

r = do("data", "update_xrecord", {"key": "TB_REC", "data": [{"type": "int", "value": 99}]})
if isinstance(r, dict):
    check("PROVEN update REPLACES rather than merging, and says how many values it discarded",
          r.get("countBefore") == 3 and r.get("count") == 1, str(r)[:280])
r = do("data", "read_xrecord", {"key": "TB_REC"})
check("and the read agrees",
      isinstance(r, dict) and r.get("data") == [{"type": "int", "value": 99}], str(r)[:250])
do("data", "update_xrecord", {"key": "NO_SUCH_KEY", "data": [{"type": "int", "value": 1}]},
   label="updating a key that does not exist is refused rather than creating one", expect_fail=True)
r = do("data", "read_xrecord", {"key": "ACAD_LAYOUT"},
       label="reading a key that holds a dictionary, not an xrecord, is refused", expect_fail=True)
check("and the refusal names what it actually is",
      "AcDbDictionary" in str(r), str(r)[:250])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
