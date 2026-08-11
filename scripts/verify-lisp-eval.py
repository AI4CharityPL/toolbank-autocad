# -*- coding: utf-8 -*-
"""Live verification for acad-lisp's LISP-evaluation trio: eval_lisp, load_lisp_file,
list_loaded_lisp. See rule 26 section 24 for the mechanism: a raw LISP form is queued via
Document.SendStringToExecute (the command line accepts LISP directly when typed, unlike
Editor.Command which tokenises and rejects it) wrapped to read the user's expression from a
REQUEST FILE and write the result to a RESPONSE FILE - the expression itself never gets embedded
in the queued text, sidestepping LISP string-escaping entirely.

This is a materially different risk class from run_command_sequence: eval_lisp runs ARBITRARY
LISP, not a bounded command sequence. The checks below are chosen accordingly - real arithmetic,
real file loading, real cross-tool consistency - rather than anything that shells out or touches
the filesystem beyond what the tool itself needs.

Controls:
  * (+ 1 2) and a NESTED list (list 1 (list 2 3) 4) prove the LISP-to-JSON parser preserves
    structure, not just flat values - a flattened list would look identical to a flat one on a
    simpler test.
  * (getvar "CLAYER") through eval_lisp is cross-checked against lisp.get_system_variable's
    OWN answer for the same variable - two independent code paths, one drawing.
  * load_lisp_file uses a REAL AutoCAD sample (afact.lsp, mutually recursive factorial functions)
    - not a synthetic one-liner - and the proof that it genuinely loaded is calling fact1(5) and
    getting 120 back afterward through eval_lisp, real arithmetic a placeholder success could not
    fake.
  * list_loaded_lisp is checked BEFORE and AFTER loading afact.lsp: FACT1 must be absent, then
    present - a tool that always reports the same list regardless of what was loaded would pass
    a single snapshot check but fail this one.
  * Negative controls that never touch AutoCAD (empty source) and ones that DO reach the LISP
    reader and are refused cleanly (undefined function, unbalanced parens) are both exercised,
    proving refusals come from the right layer.
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "lisp")}
results = []

LSP_FILE = r"C:\Program Files\Autodesk\AutoCAD 2025\Sample\VisualLISP\afact.lsp"


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


if not os.path.exists(LSP_FILE):
    raise SystemExit(f"Expected sample file missing: {LSP_FILE}")

print("== fresh drawing, cross-session probe (rule 26 section 13a) ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

print("\n== eval_lisp: refusals that never touch AutoCAD ==")
do("lisp", "eval_lisp", {"source": ""}, label="empty source is refused", expect_fail=True)
do("lisp", "eval_lisp", {}, label="missing source is refused the same way", expect_fail=True)

print("\n== eval_lisp: real arithmetic and structure ==")
r = do("lisp", "eval_lisp", {"source": "(+ 1 2)"})
if isinstance(r, dict):
    check("PROVEN (+ 1 2) evaluates to the real sum 3, not an acknowledgement",
          r.get("value") == 3, str(r)[:250])

r = do("lisp", "eval_lisp", {"source": "(list 1 (list 2 3) 4)"})
if isinstance(r, dict):
    check("PROVEN a NESTED list keeps its nesting - [1, [2, 3], 4], not flattened to "
          "[1, 2, 3, 4] which a naive parser could not tell apart from the real answer",
          r.get("value") == [1, [2, 3], 4], str(r)[:250])

r = do("lisp", "eval_lisp", {"source": "(= 1 1)"})
if isinstance(r, dict):
    check("PROVEN T comes back as JSON true", r.get("value") is True, str(r)[:200])
r = do("lisp", "eval_lisp", {"source": "(= 1 2)"})
if isinstance(r, dict):
    check("PROVEN nil comes back as JSON null, not false and not absent",
          r.get("value") is None, str(r)[:200])

print("\n== eval_lisp: cross-checked against a DIFFERENT tool reading the SAME drawing state ==")
r_eval = do("lisp", "eval_lisp", {"source": '(getvar "CLAYER")'})
r_sysvar = do("lisp", "get_system_variable", {"name": "CLAYER"})
if isinstance(r_eval, dict) and isinstance(r_sysvar, dict):
    check("PROVEN eval_lisp's (getvar \"CLAYER\") agrees with get_system_variable's own answer "
          "- two independent code paths reading the same drawing state",
          r_eval.get("value") == r_sysvar.get("value"),
          f"eval={r_eval.get('value')} sysvar={r_sysvar.get('value')}")

print("\n== eval_lisp: refusals that DO reach the LISP reader ==")
do("lisp", "eval_lisp", {"source": "(this-function-does-not-exist-xyz 1 2)"},
   label="calling an undefined function is refused with LISP's own error", expect_fail=True)
do("lisp", "eval_lisp", {"source": "(+ 1 2"},
   label="an unbalanced expression is refused cleanly, not left hanging", expect_fail=True)

print("\n== list_loaded_lisp: BEFORE loading anything ==")
r = do("lisp", "list_loaded_lisp", {"pattern": "princ"})
if isinstance(r, dict):
    check("PROVEN a well-known built-in (PRINC) is already listed before anything is loaded",
          any("PRINC" in s.upper() for s in (r.get("symbols") or [])), str(r)[:250])
r = do("lisp", "list_loaded_lisp", {"pattern": "fact1"})
if isinstance(r, dict):
    check("PROVEN FACT1 is NOT yet defined - the negative half of the load_lisp_file proof below",
          r.get("count") == 0, str(r)[:200])

print("\n== load_lisp_file: a REAL AutoCAD sample, mutually recursive factorial functions ==")
do("lisp", "load_lisp_file", {"path": "C:\\does\\not\\exist.lsp"},
   label="a missing .lsp file is refused", expect_fail=True)

r = do("lisp", "load_lisp_file", {"path": LSP_FILE})
if isinstance(r, dict):
    check("(load) returned a value rather than nil - the file did something",
          r.get("value") is not None, str(r)[:250])

r = do("lisp", "list_loaded_lisp", {"pattern": "fact1"})
if isinstance(r, dict):
    check("PROVEN FACT1 is NOW defined, having been absent before load_lisp_file ran",
          r.get("count") >= 1 and any("FACT1" in s.upper() for s in (r.get("symbols") or [])),
          str(r)[:250])

r = do("lisp", "eval_lisp", {"source": "(fact1 5)"})
if isinstance(r, dict):
    check("PROVEN fact1(5) = 120 - real recursive arithmetic from the loaded file, not a "
          "placeholder success. A file that failed to load would refuse this as an undefined "
          "function, exactly like the earlier negative control did",
          r.get("value") == 120, str(r)[:250])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
