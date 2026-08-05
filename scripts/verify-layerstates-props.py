# -*- coding: utf-8 -*-
"""Live verification for roadmap 2.4: named layer states beyond save/restore, and drawing
properties.

Two specific ways this could be silently wrong, and both are checked by re-reading state in a
separate call rather than trusting what a write returned:

  LayerStateManager writes outside any transaction opened here, so a call can look healthy
  while nothing reached the drawing.

  Database.SummaryInfo is IMMUTABLE and a fresh DatabaseSummaryInfoBuilder starts EMPTY. A
  write that forgot to seed the builder from the current properties would blank the author
  while setting the title - and report success. That is asserted directly: set one field at a
  time and check the others survive.
"""
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "layers")}
results = []
LAS = r"C:\tmp\acadmcp-state.las"


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:170]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:100]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, cond, detail=""):
    results.append((label, bool(cond)))
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + ("" if cond else f"  -> {detail}"))


def state_names():
    r = do("layers", "list_layer_states", {}, "list_layer_states (fresh read)")
    if not isinstance(r, dict):
        return []
    return r.get("values") or r.get("items") or r.get("names") or []


def props():
    r = do("files", "list_drawing_properties", {}, "list_drawing_properties (fresh read)")
    return (r or {}).get("properties") or {} if isinstance(r, dict) else {}


# ─────────────────── layer states ───────────────────
print("== a drawing with layers to record ==")
do("files", "new_document", {})
for n in ("A-WALL", "A-DOOR", "S-BEAM"):
    do("layers", "create_layer", {"name": n}, f"create layer {n}")

do("layers", "save_layer_state", {"name": "ALL-ON", "description": "everything visible"},
   "save a baseline state")
check("the state is listed", "ALL-ON" in state_names(), "")

print("\n== compare: does restoring change anything? ==")
r = do("layers", "compare_layer_state", {"name": "ALL-ON"})
if isinstance(r, dict):
    check("nothing has changed yet, so the state matches the drawing",
          r.get("matchesCurrentDrawing") is True, str(r)[:150])
    check("it reports the layers it covers", (r.get("layerCount") or 0) >= 4,
          f"layerCount={r.get('layerCount')}")
    check("the description saved earlier came back",
          r.get("description") == "everything visible", str(r.get("description")))

do("layers", "set_layer_state", {"name": "A-DOOR", "off": True}, "turn a layer off")
r = do("layers", "compare_layer_state", {"name": "ALL-ON"})
if isinstance(r, dict):
    check("after changing a layer, the state no longer matches",
          r.get("matchesCurrentDrawing") is False, str(r)[:150])
    check("and the note says restoring WOULD change the drawing",
          "WOULD change" in (r.get("note") or ""), str(r.get("note")))

# Full round trip, because the polarity of this flag was guessed wrong once: the underlying
# CompareLayerStateToDb returns true when they MATCH, and negating it made this tool answer the
# opposite of the truth in the one place that must never be ambiguous.
do("layers", "restore_layer_state", {"name": "ALL-ON"}, "restore the state")
r = do("layers", "compare_layer_state", {"name": "ALL-ON"})
if isinstance(r, dict):
    check("after restoring, it matches again", r.get("matchesCurrentDrawing") is True, str(r)[:150])
    check("and the note says restoring would change nothing",
          "change nothing" in (r.get("note") or ""), str(r.get("note")))

print("\n== description ==")
r = do("layers", "set_layer_state_description",
       {"name": "ALL-ON", "description": "baseline for the architectural plan"})
check("description updated in the reply",
      (r or {}).get("description") == "baseline for the architectural plan", str(r)[:140])
r = do("layers", "compare_layer_state", {"name": "ALL-ON"}, "re-read via a different tool")
check("and it really reached the drawing",
      (r or {}).get("description") == "baseline for the architectural plan", str(r)[:150])

print("\n== export to a .las file ==")
if os.path.exists(LAS):
    os.remove(LAS)
r = do("layers", "export_layer_state", {"name": "ALL-ON", "path": LAS})
if isinstance(r, dict):
    check("the file exists on disk", os.path.exists(LAS), LAS)
    check("and is not empty", (r.get("bytes") or 0) > 0, f"bytes={r.get('bytes')}")

do("layers", "export_layer_state", {"name": "ALL-ON", "path": LAS},
   "export over an existing file without overwrite", expect_fail=True)
do("layers", "export_layer_state", {"name": "ALL-ON", "path": LAS, "overwrite": True},
   "export over it with overwrite")
do("layers", "export_layer_state", {"name": "NO-SUCH-STATE", "path": LAS},
   "export a state that does not exist", expect_fail=True)
do("layers", "export_layer_state", {"name": "ALL-ON", "path": r"C:\no-such-dir\x.las"},
   "export into a directory that does not exist", expect_fail=True)

print("\n== rename, then import the exported file back ==")
do("layers", "rename_layer_state", {"name": "ALL-ON", "newName": "ARCH-BASELINE"})
names = state_names()
check("the old name is gone", "ALL-ON" not in names, str(names))
check("and the new one is there", "ARCH-BASELINE" in names, str(names))

r = do("layers", "import_layer_state", {"name": "ignored", "path": LAS},
       "import the .las back under its original name")
if isinstance(r, dict):
    check("import reports the name that came from the FILE, not from the caller",
          "ALL-ON" in (r.get("imported") or []), str(r)[:170])
names = state_names()
check("both states now exist", "ALL-ON" in names and "ARCH-BASELINE" in names, str(names))

do("layers", "import_layer_state", {"name": "x", "path": LAS},
   "import the same file twice", expect_fail=True)
do("layers", "import_layer_state", {"name": "x", "path": r"C:\tmp\no-such.las"},
   "import a file that does not exist", expect_fail=True)

print("\n== delete, and prove the layers survive ==")
r = do("layers", "list_layers", {}, "layers before deleting a state")
layers_before = {x.get("name") for x in (r or {}).get("layers") or []}
do("layers", "delete_layer_state", {"name": "ARCH-BASELINE"})
names = state_names()
check("the state is gone", "ARCH-BASELINE" not in names, str(names))
r = do("layers", "list_layers", {}, "layers after")
layers_after = {x.get("name") for x in (r or {}).get("layers") or []}
check("every layer survived - a state is a recording, not a container",
      layers_before == layers_after, f"{sorted(layers_before)} vs {sorted(layers_after)}")

do("layers", "delete_layer_state", {"name": "ARCH-BASELINE"},
   "delete it again", expect_fail=True)
do("layers", "rename_layer_state", {"name": "ALL-ON", "newName": "ALL-ON"},
   "rename onto its own name", expect_fail=True)
do("layers", "compare_layer_state", {"name": "GHOST"},
   "compare a state that does not exist", expect_fail=True)

# ─────────────────── drawing properties ───────────────────
print("\n== drawing properties: the builder must be SEEDED, or fields blank each other ==")
do("files", "set_drawing_properties",
   {"title": "Clinic ground floor", "author": "K. Augiewicz", "subject": "A-101"},
   "set three properties at once")
p = props()
check("title stored", p.get("title") == "Clinic ground floor", str(p)[:150])
check("author stored", p.get("author") == "K. Augiewicz", str(p)[:150])
check("subject stored", p.get("subject") == "A-101", str(p)[:150])

# The load-bearing one. If the builder were not seeded from the current properties, setting
# the title alone would wipe the author and subject and still report success.
r = do("files", "set_drawing_properties", {"title": "Clinic first floor"},
       "now set ONLY the title")
if isinstance(r, dict):
    check("applied lists only the title", r.get("applied") == ["title"], str(r.get("applied")))
p = props()
check("title changed", p.get("title") == "Clinic first floor", str(p)[:150])
check("author SURVIVED the single-field write", p.get("author") == "K. Augiewicz", str(p)[:150])
check("subject SURVIVED too", p.get("subject") == "A-101", str(p)[:150])

r = do("files", "set_drawing_properties", {"subject": ""}, "clear one property with an empty string")
p = props()
check("empty string clears it", p.get("subject") == "", str(p)[:150])
check("while the others are untouched",
      p.get("title") == "Clinic first floor" and p.get("author") == "K. Augiewicz", str(p)[:150])

do("files", "set_drawing_properties", {}, "no properties at all", expect_fail=True)

print("\n== custom properties ==")
r = do("files", "set_drawing_custom_property", {"name": "PROJECT-NUMBER", "value": "2026-041"})
check("reported as added", (r or {}).get("action") == "added", str(r)[:130])
p = props()
check("custom property is in the drawing",
      (p.get("custom") or {}).get("PROJECT-NUMBER") == "2026-041", str(p.get("custom")))

r = do("files", "set_drawing_custom_property", {"name": "PROJECT-NUMBER", "value": "2026-042"})
check("second write reports replaced, not added", (r or {}).get("action") == "replaced", str(r)[:130])
check("and the value changed",
      ((props().get("custom")) or {}).get("PROJECT-NUMBER") == "2026-042", "")

do("files", "set_drawing_custom_property", {"name": "CLIENT", "value": "NFZ"}, "add a second one")
p = props()
check("both custom properties coexist",
      len(p.get("custom") or {}) >= 2 and (p.get("custom") or {}).get("CLIENT") == "NFZ",
      str(p.get("custom")))
check("and the standard properties are still intact",
      p.get("title") == "Clinic first floor" and p.get("author") == "K. Augiewicz", str(p)[:150])

r = do("files", "set_drawing_custom_property", {"name": "CLIENT"}, "remove one with value:null")
check("reported as removed", (r or {}).get("action") == "removed", str(r)[:130])
check("and it is gone", "CLIENT" not in (props().get("custom") or {}), str(props().get("custom")))

do("files", "set_drawing_custom_property", {"name": "NOT-THERE"},
   "remove one that does not exist", expect_fail=True)
do("files", "set_drawing_custom_property", {"value": "x"},
   "no name", expect_fail=True)

print("\n== the properties must survive a save/reload round trip ==")
dwg = r"C:\tmp\acadmcp-props.dwg"
if os.path.exists(dwg):
    os.remove(dwg)
do("files", "save_document_as", {"path": dwg}, f"save -> {dwg}")
do("files", "new_document", {}, "switch to a different drawing")
do("files", "open_document", {"path": dwg}, "reopen the saved one")
p = props()
check("title survived the round trip", p.get("title") == "Clinic first floor", str(p)[:150])
check("author survived", p.get("author") == "K. Augiewicz", str(p)[:150])
check("the custom property survived",
      (p.get("custom") or {}).get("PROJECT-NUMBER") == "2026-042", str(p.get("custom")))

ok = sum(1 for _, g in results if g)
print(f"\n==== {ok}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
