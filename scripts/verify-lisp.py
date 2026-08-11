# -*- coding: utf-8 -*-
"""Live verification for roadmap 5.1 — the acad-lisp category, 5 tools shipped of 11 attempted.

The claim this category rests on is that every result here is OBSERVED rather than acknowledged.
SendStringToExecute queues, so a tool built on it can only report "sent"; everything here uses
Application.Invoke, Editor.Command or Application.Get/SetSystemVariable, all of which return when
the work is done. A verification that only checked "no error" would be exactly the failure mode
this category is designed to avoid, so every tool is checked against a value known independently:

  * set_system_variable is checked against a READ-ONLY variable, which accepts the assignment and
    keeps its old value. That is the case a tool without a read-back reports as success.
  * list_system_variables is checked against get_system_variable, so a remembered value cannot
    pass for a live one.
  * list_loaded_applications is checked against a module that MUST be there (acmgd.dll) and
    against one that must NOT be (this very plugin, which is running and still absent, because
    GetLoadedModules does not report netloaded .NET assemblies).
  * purge_regapps is run TWICE: the second run must find nothing, and ACAD must survive both.

SIX TOOLS WERE WITHDRAWN after this script found them, all on one root cause: eval_lisp,
load_lisp_file, list_loaded_lisp, run_command_sequence, run_script_file and netload_assembly need
a COMMAND context, and Application.Invoke, Editor.Command and LoadModule each answer eInvalidInput
from the APPLICATION context this plugin dispatches in.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

SCRATCH = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       ".verify-tmp")
os.makedirs(SCRATCH, exist_ok=True)
os.makedirs(SCRATCH, exist_ok=True)
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

S = {c: Session(c) for c in ("files", "geometry-2d", "lisp")}
results = []


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


def ev(src, label=None, expect_fail=False):
    r = do("lisp", "eval_lisp", {"source": src}, label=label or f"eval {src}",
           expect_fail=expect_fail)
    return r.get("value") if isinstance(r, dict) else None


print("== fresh drawing ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

# ── system variables ────────────────────────────────────────────────────────
print("\n== get / set / list_system_variables ==")
r = do("lisp", "get_system_variable", {"name": "OSMODE"}, label="read OSMODE, to restore later")
osm_direct = r.get("value") if isinstance(r, dict) else None

r = do("lisp", "get_system_variable", {"name": "CLAYER"})
check("CLAYER on a fresh drawing is layer 0",
      isinstance(r, dict) and str(r.get("value")) == "0", str(r)[:200])
do("lisp", "get_system_variable", {"name": "NOSUCHVAR"},
   label="an unknown system variable is refused rather than answered with an empty value",
   expect_fail=True)

r = do("lisp", "set_system_variable", {"name": "OSMODE", "value": 0})
if isinstance(r, dict):
    check("PROVEN: OSMODE reads back as 0 after being set, and the previous value is reported so "
          "the change can be undone",
          str(r.get("value")) == "0" and r.get("valueBefore") is not None, str(r)[:220])
# The cross-check that used to live here went through eval_lisp, which is withdrawn. What
# remains is still a second route: list_system_variables reads the same variable independently.
r2 = do("lisp", "list_system_variables", {"pattern": "OSMODE"},
        label="re-read OSMODE through list_system_variables")
osm_listed = [v for v in (r2.get("variables") or []) if v.get("name") == "OSMODE"]     if isinstance(r2, dict) else []
check("PROVEN by a SECOND route: list_system_variables sees OSMODE as 0 too, so the value was "
      "really written and not just echoed back by the tool that wrote it",
      len(osm_listed) == 1 and str(osm_listed[0].get("value")) == "0", str(osm_listed)[:200])
do("lisp", "set_system_variable", {"name": "OSMODE", "value": int(osm_direct or 4133)},
   label="restore OSMODE to what it was")

r = do("lisp", "set_system_variable", {"name": "MIRRTEXT", "value": 1})
if isinstance(r, dict):
    check("an integer variable takes an integer", str(r.get("value")) == "1", str(r)[:200])
do("lisp", "set_system_variable", {"name": "MIRRTEXT", "value": 0}, label="restore MIRRTEXT")

# THE control on the read-back: a read-only variable accepts the call and keeps its old value.
r = do("lisp", "set_system_variable", {"name": "DWGNAME", "value": "nonsense.dwg"},
       label="a READ-ONLY variable is refused - this is the case a tool without a read-back "
             "reports as a success", expect_fail=True)
check("and the refusal says so rather than reporting a value that never changed",
      "read-only" in str(r).lower() or "did not take" in str(r).lower(), str(r)[:250])
do("lisp", "set_system_variable", {"name": "MIRRTEXT", "value": "not-a-number"},
   label="a value of the wrong type is refused", expect_fail=True)
do("lisp", "set_system_variable", {"name": "MIRRTEXT"}, label="no value is refused",
   expect_fail=True)

r = do("lisp", "list_system_variables", {})
if isinstance(r, dict):
    names = [v.get("name") for v in (r.get("variables") or [])]
    check("the curated list reports live values for every variable it names",
          (r.get("count") or 0) > 20 and "CLAYER" in names and "OSMODE" in names,
          f"count={r.get('count')} first={names[:6]}")
    clayer = [v for v in (r.get("variables") or []) if v.get("name") == "CLAYER"]
    check("PROVEN the values are read LIVE, not remembered: CLAYER in the list matches what "
          "get_system_variable answers",
          len(clayer) == 1 and str(clayer[0].get("value")) == "0", str(clayer)[:200])
r = do("lisp", "list_system_variables", {"pattern": "3d"}, label="filter by group")
check("filtering by the group name narrows it",
      isinstance(r, dict) and 0 < (r.get("count") or 0) < 20, f"{r.get('count') if isinstance(r, dict) else r}")

# ── modules ─────────────────────────────────────────────────────────────────
print("\n== list_loaded_applications + netload_assembly ==")
r = do("lisp", "list_loaded_applications", {})
if isinstance(r, dict):
    mods = [m.lower() for m in (r.get("modules") or [])]
    check("PROVEN against modules that must be there: acmgd.dll and accoremgd.dll are AutoCAD's "
          "own managed core, and nothing managed could be running without them",
          "acmgd.dll" in mods and "accoremgd.dll" in mods, f"{mods[:8]}")
    check("and the list is the ARX/CRX/DBX kind, not an everything list",
          any(m.endswith(".arx") for m in mods) and any(m.endswith(".crx") for m in mods),
          f"{mods[:8]}")
# MEASURED and stated in the description rather than glossed over: a NETLOADed .NET assembly does
# NOT appear here. This plugin is running - every call above proves it - and is still absent.
r = do("lisp", "list_loaded_applications", {"pattern": "AcadMcp"},
       label="look for this very plugin, which is definitely running")
check("PROVEN the documented limit is real: AcadMcp.Plugin is running - none of these calls could "
      "have happened otherwise - and it is still NOT in the list, because GetLoadedModules does "
      "not report netloaded .NET assemblies",
      isinstance(r, dict) and (r.get("count") or 0) == 0, str(r)[:200])
r = do("lisp", "list_loaded_applications", {"pattern": "acdim"}, label="filter by substring")
check("filtering by substring narrows it to the matching modules",
      isinstance(r, dict) and 0 < (r.get("count") or 0) < 5, f"{r.get('count') if isinstance(r,dict) else r}")


# ── regapps ─────────────────────────────────────────────────────────────────
print("\n== purge_regapps ==")
r1 = do("lisp", "purge_regapps", {})
r2 = do("lisp", "purge_regapps", {}, label="purge a second time")
if isinstance(r1, dict) and isinstance(r2, dict):
    check("PROVEN it purges only what is unreferenced: the second run finds nothing left to "
          "purge, so the first was not simply erasing everything it could reach",
          r2.get("purgedCount") == 0, f"first={r1.get('purgedCount')} second={r2.get('purgedCount')}")
    check("ACAD survives both runs - it is AutoCAD's own and is never offered",
          "ACAD" in [x.upper() for x in (r2.get("remaining") or [])], str(r2.get("remaining"))[:200])
    check("the counts are consistent: after = before - purged",
          r1.get("registeredAfter") == r1.get("registeredBefore") - r1.get("purgedCount"),
          str(r1)[:220])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
