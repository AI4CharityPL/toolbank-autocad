# -*- coding: utf-8 -*-
"""Live verification for roadmap 2.3 layer filters.

Three things this has to prove, and only one of them is visible in a return code:

1. The tree is written BACK. Layer filters are read out of Database.LayerFilters as a copy,
   modified, and assigned back. Skip the assignment and every value still reads correctly
   from the in-memory copy while the drawing is untouched. So each filter is created in one
   call and read back in a SEPARATE one.
2. matchCount is real. A property filter expression can be valid, be stored, be listed, and
   select nothing. That is the outcome a caller most needs to see, so the expressions here
   are checked against a layer set built on purpose.
3. The two kinds behave differently. A property filter picks up a layer created afterwards;
   a group filter does not. That difference is the whole reason both exist, and it is
   asserted by creating a layer between two reads.

apply_layer_filter is not tested because it does not exist: LayerFilterTree.Current is
get-only in the managed API and it was withheld rather than shipped as a no-op.
"""
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "styles", "layers")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:170]}"
    if expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:105]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, cond, detail=""):
    results.append((label, bool(cond)))
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + ("" if cond else f"  -> {detail}"))


def filters():
    """Read the tree back in a fresh call - the only way to prove the write reached the drawing."""
    r = do("styles", "list_layer_filters", {}, "list_layer_filters (fresh read)")
    return {f["name"]: f for f in (r or {}).get("filters") or []} if isinstance(r, dict) else {}


print("== fresh drawing with a deliberate layer set ==")
do("files", "new_document", {})
LAYERS = ["A-WALL", "A-WALL-FIRE", "A-DOOR", "S-BEAM", "S-COLUMN", "M-DUCT"]
for name in LAYERS:
    do("layers", "create_layer", {"name": name}, f"create layer {name}")

print("\n== property filter: does the write reach the drawing? ==")
do("styles", "create_layer_filter",
   {"name": "ARCH", "expression": 'NAME=="A-*"'}, "create property filter ARCH")
f = filters()
check("ARCH survived into the drawing, not just the in-memory tree", "ARCH" in f, str(list(f))[:120])
check("ARCH reports kind=property", f.get("ARCH", {}).get("kind") == "property", str(f.get("ARCH"))[:120])
check("ARCH matches the 3 A-* layers", f.get("ARCH", {}).get("matchCount") == 3,
      f"got {f.get('ARCH', {}).get('matchCount')}")

print("\n== group filter: a fixed set ==")
do("styles", "create_layer_group_filter",
   {"name": "STRUCT", "layers": ["S-BEAM", "S-COLUMN"]}, "create group filter STRUCT")
f = filters()
check("STRUCT reports kind=group", f.get("STRUCT", {}).get("kind") == "group", str(f.get("STRUCT"))[:120])
check("STRUCT lists exactly its two layers",
      sorted(f.get("STRUCT", {}).get("layers") or []) == ["S-BEAM", "S-COLUMN"],
      str(f.get("STRUCT", {}).get("layers")))
check("STRUCT matchCount is 2", f.get("STRUCT", {}).get("matchCount") == 2,
      f"got {f.get('STRUCT', {}).get('matchCount')}")

print("\n== the difference between the two kinds, which is why both exist ==")
do("layers", "create_layer", {"name": "A-GLAZING"}, "add a new A-* layer AFTER both filters exist")
f = filters()
check("property filter picked the new layer up on its own (3 -> 4)",
      f.get("ARCH", {}).get("matchCount") == 4, f"got {f.get('ARCH', {}).get('matchCount')}")
check("group filter did NOT change (still 2)",
      f.get("STRUCT", {}).get("matchCount") == 2, f"got {f.get('STRUCT', {}).get('matchCount')}")

print("\n== a valid expression that selects nothing must still be visible as such ==")
do("styles", "create_layer_filter",
   {"name": "TYPO", "expression": 'NAME=="A_WALL*"'}, "create filter with an underscore typo")
f = filters()
check("TYPO was accepted and stored", "TYPO" in f, str(list(f))[:140])
check("TYPO reports matchCount 0, which is the only way to notice",
      f.get("TYPO", {}).get("matchCount") == 0, f"got {f.get('TYPO', {}).get('matchCount')}")

print("\n== nesting ==")
do("styles", "create_layer_filter",
   {"name": "ARCH-FIRE", "expression": 'NAME=="A-*FIRE*"', "parent": "ARCH"},
   "nest a filter under ARCH")
f = filters()
check("nested filter reports its parent", f.get("ARCH-FIRE", {}).get("parent") == "ARCH",
      str(f.get("ARCH-FIRE"))[:120])
check("nested filter matches the one fire layer", f.get("ARCH-FIRE", {}).get("matchCount") == 1,
      f"got {f.get('ARCH-FIRE', {}).get('matchCount')}")

print("\n== arguments that must be refused ==")
do("styles", "create_layer_filter", {"name": "ARCH", "expression": 'NAME=="X*"'},
   "duplicate name without overwrite", expect_fail=True)
do("styles", "create_layer_filter", {"name": "NOEXPR"},
   "property filter with no expression", expect_fail=True)
do("styles", "create_layer_filter", {"name": "BAD", "expression": 'NAME <<>> "junk'},
   "malformed expression", expect_fail=True)
do("styles", "create_layer_group_filter", {"name": "GHOST", "layers": ["NO-SUCH-LAYER"]},
   "group filter naming a layer that does not exist", expect_fail=True)
do("styles", "create_layer_group_filter", {"name": "EMPTY", "layers": []},
   "group filter with no layers", expect_fail=True)
do("styles", "create_layer_filter", {"name": "ORPHAN", "expression": 'NAME=="A-*"', "parent": "NOPE"},
   "nesting under a parent that does not exist", expect_fail=True)
do("styles", "delete_layer_filter", {"name": "NO-SUCH-FILTER"},
   "delete a filter that does not exist", expect_fail=True)

print("\n== delete takes nested filters with it, and says so ==")
before = filters()
r = do("styles", "delete_layer_filter", {"name": "ARCH"}, "delete ARCH (has one nested child)")
if isinstance(r, dict):
    check("delete reports the nested filter it also removed",
          "ARCH-FIRE" in (r.get("alsoDeleted") or []), str(r)[:150])
after = filters()
check("ARCH is gone from a fresh read", "ARCH" not in after, str(list(after))[:140])
check("its nested child is gone too", "ARCH-FIRE" not in after, str(list(after))[:140])
check("unrelated filters survived", "STRUCT" in after and "TYPO" in after, str(list(after))[:140])

print("\n== deleting a filter must not touch the layers themselves ==")
r = do("layers", "list_layers", {}, "list layers after deletions")
names = [x.get("name") for x in (r or {}).get("layers") or []] if isinstance(r, dict) else []
check("every layer still exists", all(n in names for n in LAYERS + ["A-GLAZING"]),
      str(sorted(n for n in names if n != "0"))[:170])

ok = sum(1 for _, g in results if g)
print(f"\n==== {ok}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
