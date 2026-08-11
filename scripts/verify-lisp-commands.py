# -*- coding: utf-8 -*-
"""Live verification for the acad-lisp command-context bridge - run_command_sequence. See rule
26 section 15 for why this needed a bridge at all: Editor.Command throws eInvalidInput from the
application context every other tool in this bank dispatches from, and only a genuine
[CommandMethod] AutoCAD's own command processor invokes gets a command context.

run_script_file was built on the same bridge and WITHDRAWN - even a single CIRCLE inside a .scr
drew nothing, in two separately measured attempts (see LispTools.cs). This script confirms it
stays absent from the catalog rather than testing it as working.

SCOPE DECISION, deliberate: this script does NOT test a command sequence that leaves AutoCAD
waiting for an answer it will never get. That is a real, not theoretical, way to freeze the UI
thread behind a prompt with nothing on screen to see it - the exact shape of today's earlier
eNotOpenForWrite incident, just from a different cause. The tool's own description already says
so ("a modal dialog cannot be answered this way at all"), and the argument-validation refusal
(empty tokens) is tested instead - real, but does not touch AutoCAD at all.

TWO LIMITS WERE MEASURED LIVE DURING THE FIRST RUN OF THIS SCRIPT, not guessed in advance, and
both are now REFUSALS rather than silent wrong answers: a second command chained after the first
inside one Editor.Command call is dropped without error (entitiesAdded undercounted by exactly
the missing command), and a command that prompts for object SELECTION - ERASE with both "L" and
"ALL" tested, MOVE also refused by name - completes and reports success while changing nothing.
Both are asserted here as refusals, and each refused call is checked to have changed nothing.

Every count this script asserts is checked TWICE: once from the tool's own self-reported
entitiesBefore/After/Added, and once independently through files.list_documents' entityCount,
which comes from a completely different code path (the document-info builder, not the command
bridge) - so a tool that miscounted its own effect cannot pass both.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "lisp")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:220]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[-120:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def independent_count():
    """Read the (sole, by construction) open document's entity count through
    files.list_documents - a route with no code in common with the command bridge that made
    the changes."""
    ok, r = S["files"].call("list_documents", {})
    if not ok or not isinstance(r, dict):
        return None
    docs = r.get("documents") or []
    if len(docs) != 1:
        print(f"  WARN independent_count: expected exactly 1 open document, found {len(docs)}")
    return docs[-1].get("entityCount") if docs else None


print("== fresh drawing, cross-session probe (rule 26 section 13a) ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

check("independent count starts at 0 on a fresh drawing", independent_count() == 0,
      str(independent_count()))

r = do("lisp", "get_system_variable", {"name": "FILEDIA"}, label="baseline FILEDIA (not assumed)")
filedia_before = r.get("value") if isinstance(r, dict) else None

print("\n== run_command_sequence: refusals that never touch AutoCAD ==")
do("lisp", "run_command_sequence", {"tokens": []},
   label="empty tokens is refused before anything is queued", expect_fail=True)
do("lisp", "run_command_sequence", {},
   label="missing tokens is refused the same way", expect_fail=True)

print("\n== run_command_sequence: one circle, a real command-context call ==")
r = do("lisp", "run_command_sequence",
       {"tokens": ["_.CIRCLE", "0,0", "10"]})
if isinstance(r, dict):
    check("PROVEN entitiesAdded is exactly 1, self-reported by the tool",
          r.get("entitiesBefore") == 0 and r.get("entitiesAfter") == 1
          and r.get("entitiesAdded") == 1, str(r)[:250])
check("PROVEN independently through files.list_documents, a route with no code in common "
      "with the command bridge", independent_count() == 1, str(independent_count()))

print("\n== run_command_sequence: a second chained command is REFUSED, not silently dropped ==")
# MEASURED live, 2026-08-11: a second "_.CIRCLE" chained after the first inside one
# Editor.Command call did not draw a second circle - entitiesAdded read 1, not 2, with no
# error. Rather than ship that silent undercount, the tool now refuses more than one
# command per call outright. This asserts the REFUSAL, and that nothing was drawn as a
# side effect of the refused call.
r = do("lisp", "run_command_sequence",
       {"tokens": ["_.CIRCLE", "50,50", "5", "_.CIRCLE", "100,100", "7"]},
       label="two chained commands in one call is refused", expect_fail=True)
check("and the refusal names the measured cause",
      "dropped" in str(r) or "second command" in str(r), str(r)[:250])
check("PROVEN the refused call drew nothing - count stays at 1",
      independent_count() == 1, str(independent_count()))

print("\n== run_command_sequence: ERASE is REFUSED by name, not silently a no-op ==")
# MEASURED live: ["_.ERASE", "L", ""] and ["_.ERASE", "ALL", ""] both completed and reported
# success while erasing NOTHING - no exception, entitiesAdded correctly read 0 because
# nothing happened, indistinguishable from a legitimate no-op edit. Refused by name instead.
r = do("lisp", "run_command_sequence", {"tokens": ["_.ERASE", "L", ""]},
       label="ERASE is refused - selection-based commands do not work through this route",
       expect_fail=True)
check("and the refusal names the measured cause",
      "selection" in str(r).lower(), str(r)[:250])
check("PROVEN the refused call erased nothing - count stays at 1",
      independent_count() == 1, str(independent_count()))
do("lisp", "run_command_sequence", {"tokens": ["_.MOVE", "L", "", "0,0", "5,5"]},
   label="MOVE is refused too - same selection-based reason", expect_fail=True)

print("\n== run_script_file: WITHDRAWN, not offered - confirming it stays that way ==")
# MEASURED live, 2026-08-11, in two separate attempts: run_script_file ran through the same
# command-context bridge as run_command_sequence, and even a single CIRCLE inside a .scr drew
# nothing (entitiesAdded=0). The obvious cause - SCRIPT opens a file-picker dialog unless
# FILEDIA=0 - was tried, confirmed via a captured FILEDIA baseline that the fix correctly forced
# 0 during the call and restored it after, and the script STILL drew nothing. Withdrawn rather
# than shipped broken; this asserts the tool is genuinely absent, not silently reachable again.
ok, r = S["lisp"].call("run_script_file", {"path": "C:\\does\\not\\exist.scr"})
check("run_script_file is genuinely ABSENT from the catalog (withdrawn, both fix attempts "
      "measured and wrong) - not merely refusing this one call, which do()'s generic "
      "expect_fail path cannot distinguish from a real refusal",
      (not ok) and ("not found" in str(r).lower() or "UnknownTool" in str(r)), str(r)[:200])
check(f"baseline FILEDIA ({filedia_before}) was captured for the record even though the tool "
      "using it is withdrawn", filedia_before is not None, str(filedia_before))

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
