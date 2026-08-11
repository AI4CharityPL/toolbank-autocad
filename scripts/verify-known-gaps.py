# -*- coding: utf-8 -*-
"""Live verification for the KNOWN-GAPS backlog: A6, A7, A5, A3.

Each of these shipped looking healthy, so each check below targets the specific way it lied
rather than whether the call succeeds.

  A6  The router reported refusals as SUCCESSFUL tool calls whose content happened to start
      with "[router-error]". A client had to string-match to notice. Checked at the JSON-RPC
      level, because the whole point is the isError FLAG, which a normal call wrapper hides.
  A7  acad_undo_checkpoint declared `label` required and accepted its absence.
  A5  Every create_ucs_* reported "*CURRENT" instead of the name it saved, and makeCurrent:false
      did nothing at all.
  A3  set_xref_clip_display took a handle and set a drawing-wide system variable.
"""
import json
import subprocess
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

EXE = r"src\AcadMcp.Backend\bin\Debug\net8.0\AcadMcp.Backend.exe"
results = []


def check(label, cond, detail=""):
    results.append((label, bool(cond)))
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + ("" if cond else f"  -> {detail}"))


def router_call(tool, args):
    """Speak MCP to the router directly. A normal wrapper swallows isError; that flag IS the bug."""
    msgs = [
        {"jsonrpc": "2.0", "id": 1, "method": "initialize",
         "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                    "clientInfo": {"name": "gapcheck", "version": "1"}}},
        {"jsonrpc": "2.0", "id": 2, "method": "tools/call",
         "params": {"name": tool, "arguments": args}},
    ]
    proc = subprocess.run(
        [EXE, "--category", "router"],
        input="\n".join(json.dumps(m) for m in msgs) + "\n",
        capture_output=True, text=True, encoding="utf-8", timeout=120)
    for line in (proc.stdout or "").splitlines():
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            d = json.loads(line)
        except ValueError:
            continue
        if d.get("id") == 2:
            return d.get("result") or {}
    return {}


print("== A6: a router refusal must set isError, not just say so in the text ==")
r = router_call("acad_restore_checkpoint", {})
text = "".join(c.get("text", "") for c in (r.get("content") or []))
check("the refusal text is still there", "[router-error]" in text, text[:140])
check("and isError is now TRUE - a client no longer has to string-match",
      r.get("isError") is True, f"isError={r.get('isError')}  text={text[:110]}")

r = router_call("acad_load_category", {"category": "no-such-category"})
text = "".join(c.get("text", "") for c in (r.get("content") or []))
if "[router-error]" in text:
    check("a second router tool refuses with isError too", r.get("isError") is True,
          f"isError={r.get('isError')}  {text[:110]}")
else:
    check("acad_load_category answered without a router-error marker", True,
          "(nothing to assert here)")

r = router_call("acad_status", {})
check("a SUCCESSFUL call still reports isError false",
      r.get("isError") is False, str(r)[:150])

print("\n== A7: acad_undo_checkpoint declares label required, so it must require it ==")
r = router_call("acad_undo_checkpoint", {})
text = "".join(c.get("text", "") for c in (r.get("content") or []))
check("no label is refused", r.get("isError") is True and "requires 'label'" in text, text[:150])

r = router_call("acad_undo_checkpoint", {"name": "typo-instead-of-label"})
text = "".join(c.get("text", "") for c in (r.get("content") or []))
check("a MISTYPED argument name is refused rather than silently ignored",
      r.get("isError") is True, text[:150])
check("and the message names what was received, so the typo is visible",
      "name" in text, text[:170])

r = router_call("acad_undo_checkpoint", {"label": "before-the-risky-bit"})
text = "".join(c.get("text", "") for c in (r.get("content") or []))
check("a proper label still works", r.get("isError") is False, text[:150])
check("and the label comes back in the result, never '(none)'",
      "before-the-risky-bit" in text and "(none)" not in text, text[:170])

# ─────────── A5 and A3 go through the ordinary session ───────────
S = {c: Session(c) for c in ("files", "ucs", "xrefs")}


def call(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:160]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:95]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


print("\n== A5: create_ucs_* must report the name it saved, and honour makeCurrent ==")
call("files", "new_document", {})

r = call("ucs", "create_ucs_origin", {"origin": {"x": 1000, "y": 2000, "z": 0}, "name": "MCP-ORIGIN"})
if isinstance(r, dict):
    u = r.get("ucs") or {}
    check("create_ucs_origin reports the NAME, not '*CURRENT'", u.get("name") == "MCP-ORIGIN", str(u)[:150])
    check("and says what it saved it as", r.get("savedAs") == "MCP-ORIGIN", str(r)[:150])

r = call("ucs", "create_ucs_3point",
         {"origin": {"x": 0, "y": 0, "z": 0}, "xAxisPoint": {"x": 1, "y": 1, "z": 0},
          "yAxisPoint": {"x": -1, "y": 1, "z": 0}, "name": "MCP-ROT"})
if isinstance(r, dict):
    check("the whole family is fixed, not just create_ucs_origin",
          (r.get("ucs") or {}).get("name") == "MCP-ROT", str(r)[:150])

r = call("ucs", "list_ucs", {}, "list_ucs (independent confirmation)")
names = [x.get("name") for x in (r or {}).get("named") or []] if isinstance(r, dict) else []
check("both really are in the UCS table", "MCP-ORIGIN" in names and "MCP-ROT" in names, str(names))

print("\n  makeCurrent:false must actually leave the current UCS alone")
call("ucs", "set_ucs_world", {}, "back to world")
before = call("ucs", "get_current_ucs", {}, "current UCS before")
r = call("ucs", "create_ucs_origin",
         {"origin": {"x": 5000, "y": 5000, "z": 0}, "name": "MCP-SAVED-ONLY", "makeCurrent": False})
if isinstance(r, dict):
    check("the result says it is not current", r.get("isCurrent") is False, str(r)[:150])
after = call("ucs", "get_current_ucs", {}, "current UCS after")
if isinstance(before, dict) and isinstance(after, dict):
    b, a = (before.get("ucs") or {}).get("origin") or {}, (after.get("ucs") or {}).get("origin") or {}
    check("and the drawing's current UCS really did NOT move", b == a, f"{b} vs {a}")

r = call("ucs", "list_ucs", {}, "list_ucs again")
names = [x.get("name") for x in (r or {}).get("named") or []] if isinstance(r, dict) else []
check("but it WAS saved under its name", "MCP-SAVED-ONLY" in names, str(names))

call("ucs", "create_ucs_origin", {"origin": {"x": 1, "y": 1, "z": 0}, "makeCurrent": False},
     "makeCurrent:false with no name would discard it entirely", expect_fail=True)

print("\n== A3: the clip frame tool must not promise a scope it does not have ==")
r = call("xrefs", "set_clip_frame_display", {"mode": "display"})
if isinstance(r, dict):
    check("mode applied", r.get("mode") == "display", str(r)[:150])
    check("the previous mode is reported so it can be put back",
          bool(r.get("before")), str(r)[:150])
    check("and the result states the scope is the DRAWING",
          r.get("scope") == "drawing", str(r)[:150])

r = call("xrefs", "set_clip_frame_display", {"mode": "hidden"}, "switch to hidden")
if isinstance(r, dict):
    check("before now reports the mode set a moment ago", r.get("before") == "display", str(r)[:150])

call("xrefs", "set_clip_frame_display", {"mode": "displayAndPlot"}, "the third state exists")
call("xrefs", "set_clip_frame_display", {"mode": "sometimes"}, "a mode that does not exist",
     expect_fail=True)
call("xrefs", "set_xref_clip_display", {"handle": "1", "visible": True},
     "the old over-promising name is gone", expect_fail=True)

ok = sum(1 for _, g in results if g)
print(f"\n==== {ok}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
