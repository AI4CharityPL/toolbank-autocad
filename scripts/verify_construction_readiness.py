# -*- coding: utf-8 -*-
"""Rule 74, Part B: generic, reusable construction-document readiness check.

NOT hardcoded to one project - operates on the CURRENTLY OPEN AutoCAD document, exactly
like scripts/verify-structural-category.py. Meant to be the new, permanent LAST STEP of any
future build claiming "rysunek wykonawczy" status (rule 74 item 10), replacing reliance on an
agent remembering a 10-item checklist across a long session - the exact failure mode that
produced rule 74 in the first place (rule 73 step 9's own check_overlaps was itself only added
after a user caught the gap live).

Checks, each PASS/FAIL/SKIP:
  - Cross-category geometric overlaps (columns/doors/furniture/plumbing/windows) - reuses the
    battery built in build_apartment_120_test.py / build_dental_clinic_test.py, but SKIPS a pair
    if neither layer has any entities in THIS drawing (an apartment has no A-GLAZ-less dental
    clinic to false-fail on, and vice versa) rather than hardcoding one project's layer set.
  - Material hatching present (>0 hatches).
  - Dimension entities present (>0 on A-ANNO-DIMS).
  - At least one schedule table (A-ANNO-TBLS).
  - Title block, north arrow, scale bar present (A-ANNO-TTLB / A-ANNO-NORT / A-ANNO-SBAR).
  - At least one section line (A-DETL-SECT).
  - Vision sidecar reachable (health-check only here; the actual /v1/architect-review call needs
    an exported image, which is a build-script-specific step - see rule 74 item 9). SKIPPED, not
    FAILED, when unreachable - matches rule 74's own best-effort framing for this one step.

Usage: python scripts/verify_construction_readiness.py [--project-name NAME]
Run against whatever document is currently active/open in AutoCAD.
"""
import json
import os
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\DELL\Dev\autocad-mcp"
sys.path.insert(0, os.path.join(REPO, "scripts"))
from mcpcall import Session  # noqa: E402

CATS = ["files", "layers", "validators", "hatches", "selection"]
S = {c: Session(c) for c in CATS}

project_name = sys.argv[sys.argv.index("--project-name") + 1] if "--project-name" in sys.argv else "(current document)"

results = []  # (label, "PASS"|"FAIL"|"SKIP", detail)


def record(label, ok, detail="", skip=False):
    status = "SKIP" if skip else ("PASS" if ok else "FAIL")
    results.append((label, status, detail))
    print(f"  {status:4s}  {label}" + (f"  -> {detail}" if detail else ""))


print("=" * 70)
print(f"CONSTRUCTION-DOCUMENT READINESS CHECK (rule 74) - {project_name}")
print("=" * 70)

ok, docs = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {docs}")
active = docs.get("active")
print(f"active document: {active}\n")

# ---- which layers actually have entities in THIS drawing? ----
_, layer_list = S["layers"].call("list_layers", {})
present_layers = {l["name"] for l in layer_list.get("layers", [])}


def has_entities(layer, any_space=False):
    if layer not in present_layers:
        return False
    _, r = S["selection"].call("select_by_layer", {"layer": layer, "anySpace": any_space})
    return (r.get("count") or 0) > 0


print("-- 1. Cross-category geometric overlaps (rule 73 step 9 / rule 74 item 1) --")
OVERLAP_PAIRS = [
    (["S-COLS"], ["A-GLAZ"], "columns vs windows"),
    (["S-COLS"], ["A-DOOR"], "columns vs doors"),
    (["S-COLS"], ["A-FURN-BED-RES", "A-FURN-CBT", "A-FURN-KIT", "A-FURN-SFA", "A-FURN-TBL"], "columns vs furniture"),
    (["S-COLS"], ["A-PLMB-WC", "A-PLMB-BSN", "A-PLMB-BT", "A-PLMB-SHW"], "columns vs plumbing fixtures"),
    (["A-DOOR"], ["A-PLMB-WC", "A-PLMB-BSN", "A-PLMB-BT", "A-PLMB-SHW"], "doors vs plumbing fixtures"),
    (["A-DOOR"], ["A-FURN-BED-RES", "A-FURN-CBT", "A-FURN-KIT", "A-FURN-SFA", "A-FURN-TBL"], "doors vs furniture"),
]
for layers_a, layers_b, label in OVERLAP_PAIRS:
    a_present = [l for l in layers_a if has_entities(l)]
    b_present = [l for l in layers_b if has_entities(l)]
    if not a_present or not b_present:
        record(label, True, "no entities on one side - not applicable to this drawing", skip=True)
        continue
    _, r = S["validators"].call("check_overlaps", {"layersA": a_present, "layersB": b_present, "mode": "bbox_intersect"})
    n = len(r.get("overlaps", []))
    record(label, n == 0, f"{n} overlap(s)" if n else "")

print("\n-- 2. Material hatching (rule 62 / rule 74 item 2) --")
_, hatch_r = S["hatches"].call("list_hatches", {})
n_hatches = len(hatch_r.get("hatches", []))
record("material hatches present", n_hatches > 0, f"{n_hatches} hatch(es)")

print("\n-- 3. Dimension chains (rule 66 / rule 74 item 3) --")
n_dims = 0
for layer in present_layers:
    if layer == "A-ANNO-DIMS" or layer.startswith("A-ANNO-DIMS"):
        _, r = S["selection"].call("select_by_layer", {"layer": layer})
        n_dims += r.get("count") or 0
record("dimension entities present", n_dims > 0, f"{n_dims} entit(y/ies) on A-ANNO-DIMS*")

print("\n-- 4. Schedules (rule 65 / rule 74 item 4) --")
# anySpace=True: schedules are legitimately paperspace content (rule 74 item 8's own viewport
# work) since the rule-74 C.4 layoutName fix - select_by_layer defaults to model-space-only
# (acad-selection's own long-standing scope, rule 74 C.4's own follow-up note) and would
# otherwise report a false FAIL for a correctly-built paperspace schedule table.
n_tbls = 0
if "A-ANNO-TBLS" in present_layers:
    _, r = S["selection"].call("select_by_layer", {"layer": "A-ANNO-TBLS", "anySpace": True})
    n_tbls = r.get("count") or 0
record("at least one schedule table", n_tbls > 0, f"{n_tbls} table(s) on A-ANNO-TBLS (any space)")

print("\n-- 5. Callouts: title block, north arrow, scale bar (rule 69 / rule 74 item 5) --")
record("title block present", has_entities("A-ANNO-TTLB", any_space=True))
record("north arrow present", has_entities("A-ANNO-NORT", any_space=True))
record("scale bar present", has_entities("A-ANNO-SBAR", any_space=True))

print("\n-- 6. Section lines (rule 70 / rule 74 item 6) --")
record("at least one section line", has_entities("A-DETL-SECT"))

print("\n-- 7. Load-bearing wall layer in use (rule 74 C.1 / item 7) --")
record("A-WALL-BEAR used somewhere", has_entities("A-WALL-BEAR"),
       "no bearing walls tagged - fine for a fit-out with no structural walls of its own, "
       "otherwise check every exterior/grid-axis wall used bearing=true")

print("\n-- 8. Vision sidecar reachability (rule 74 item 9 - health-check only) --")
port_file = os.path.join(os.environ.get("LOCALAPPDATA", ""), "AcadMcp", "vision.port")
vision_status = "SKIP"
if os.path.exists(port_file):
    port = open(port_file, encoding="utf-8").read().strip()
    try:
        req = urllib.request.Request(f"http://127.0.0.1:{port}/health", headers={"Accept": "application/json"})
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            health = json.loads(resp.read().decode("utf-8"))
        print(f"  PASS  Vision sidecar reachable on port {port}  -> {health}")
        print("        (the actual /v1/architect-review scored call is NOT run here - it needs an")
        print("         exported image, a build-script-specific step; call it separately per rule 74 item 9)")
        vision_status = "PASS"
    except (urllib.error.URLError, OSError) as ex:
        print(f"  SKIP  Vision sidecar port file found ({port}) but not reachable -> {ex}")
else:
    print(f"  SKIP  no port file at {port_file} - sidecar not started this session")
results.append(("vision sidecar reachable", vision_status, ""))

print("\n-- 9. Plot style CTB (rule 61 / rule 74 item 8 - best-effort, external dependency) --")
ctb_dir = os.path.join(REPO, "assets", "plotstyles")
ctb_files = [f for f in os.listdir(ctb_dir) if f.lower().endswith(".ctb")] if os.path.isdir(ctb_dir) else []
if ctb_files:
    print(f"  INFO  {len(ctb_files)} .ctb file(s) available: {ctb_files} - apply via ensure_ctb/apply_plotstyle_to_layout")
else:
    print("  SKIP  no .ctb file under assets/plotstyles/ - document this in the project README, not silent")
results.append(("plot style CTB available", "SKIP" if not ctb_files else "PASS", ""))

print("\n" + "=" * 70)
n_pass = sum(1 for _, s, _ in results if s == "PASS")
n_fail = sum(1 for _, s, _ in results if s == "FAIL")
n_skip = sum(1 for _, s, _ in results if s == "SKIP")
print(f"RESULT: {n_pass} PASS, {n_fail} FAIL, {n_skip} SKIP (of {len(results)} checks)")
if n_fail:
    print("\nFAILING checks:")
    for label, status, detail in results:
        if status == "FAIL":
            print(f"  - {label}" + (f" ({detail})" if detail else ""))
    print("\nNOT construction-document ready. Fix the FAILs above (rule 74).")
else:
    print("\nAll non-SKIP checks pass. SKIPs are either not-applicable-to-this-drawing or")
    print("external-dependency best-effort steps (rule 74 items 8-9) - confirm each SKIP is")
    print("genuinely expected, not a silently-missed step, before calling this project done.")
print("=" * 70)
