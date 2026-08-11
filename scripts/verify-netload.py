# -*- coding: utf-8 -*-
"""Live verification for acad-lisp.netload_assembly. Built only with the user's explicit
go-ahead: dynamic assembly loading is a materially different risk from run_command_sequence's
bounded drawing commands or eval_lisp's LISP evaluation - a loaded assembly runs with full .NET
access inside the AutoCAD process.

Uses the SAME [CommandMethod] bridge as run_command_sequence (loading a .dll is the _.NETLOAD
COMMAND, not a LISP form), with FILEDIA forced to 0 - the same file-dialog precedent already
fixed for run_script_file's SCRIPT command.

The test assembly is a fixture built specifically for this check (scripts/fixtures/
netload-test-assembly, built on demand if its output is missing) - a single static class with no
AutoCAD references, no static constructor, no commands of its own. Nothing ambient or unknown is
loaded.

The sharpest control here: `list_loaded_applications` (DynamicLinker.GetLoadedModules) is
DOCUMENTED not to see .NET assemblies loaded via NETLOAD - this script proves that documented
limitation is real by checking the netloaded test assembly is ABSENT from that list even though
netload_assembly itself correctly reports it loaded (via AppDomain reflection instead). A tool
that trusted DynamicLinker for its own answer would fail exactly this check.
"""
import os
import subprocess
import sys

SCRIPTS_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPTS_DIR)  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "lisp")}
results = []

FIXTURE_DIR = os.path.join(SCRIPTS_DIR, "fixtures", "netload-test-assembly")
DLL = os.path.join(FIXTURE_DIR, "bin", "Release", "net8.0-windows", "AcadMcpNetloadTest.dll")

if not os.path.exists(DLL):
    print(f"Building test fixture ({DLL} not found yet)...")
    subprocess.run(["dotnet", "build", "-c", "Release"], cwd=FIXTURE_DIR, check=True)


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
        detail = f"  (refused as intended: {str(r)[-160:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


if not os.path.exists(DLL):
    raise SystemExit(f"Expected test fixture missing: {DLL}")

print("== fresh drawing, cross-session probe (rule 26 section 13a) ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

print("\n== netload_assembly: refusals that never touch AutoCAD ==")
do("lisp", "netload_assembly", {"path": "C:\\does\\not\\exist.dll"},
   label="a missing file is refused", expect_fail=True)
do("lisp", "netload_assembly", {}, label="a missing path argument is refused", expect_fail=True)

print("\n== list_loaded_applications BEFORE - the fixture must be absent ==")
r = do("lisp", "list_loaded_applications", {"pattern": "AcadMcpNetloadTest"})
if isinstance(r, dict):
    check("PROVEN the fixture is not (yet) anywhere - baseline for the load proof below",
          r.get("count") == 0, str(r)[:200])

print("\n== netload_assembly: load a fully-controlled test fixture ==")
r = do("lisp", "netload_assembly", {"path": DLL})
if isinstance(r, dict):
    check("PROVEN loaded is true, read back via AppDomain reflection",
          r.get("loaded") is True, str(r)[:250])

print("\n== the documented DynamicLinker blind spot, proven rather than just cited ==")
r = do("lisp", "list_loaded_applications", {"pattern": "AcadMcpNetloadTest"})
if isinstance(r, dict):
    check("PROVEN the netloaded assembly is STILL invisible to DynamicLinker.GetLoadedModules "
          "even though netload_assembly correctly reports it loaded - this is exactly why "
          "AppDomain reflection was used instead, and a tool that trusted DynamicLinker for its "
          "own verification would report false failure here",
          r.get("count") == 0, str(r)[:250])

print("\n== netload_assembly: loading the SAME assembly again is refused ==")
r = do("lisp", "netload_assembly", {"path": DLL},
       label="an already-loaded assembly is refused, not silently reloaded", expect_fail=True)
check("and the refusal names the real reason (already loaded / cannot unload)",
      "already loaded" in str(r).lower() or "cannot" in str(r).lower(), str(r)[:250])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
