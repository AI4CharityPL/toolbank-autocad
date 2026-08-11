# -*- coding: utf-8 -*-
"""Live verification for KNOWN-GAPS A4: the vision category, against a running sidecar.

The entry said all nine tools were unverified because they "need the Python sidecar started
and at least one provider API key". Two of those three claims are separable, and this script
covers everything that does NOT need a key:

  vision_health / vision_version   Previously these only ever reported the sidecar unreachable.
                                   Against a live one they must report its version, phase and -
                                   the useful part - which optional backends and API keys are
                                   actually present, so a caller knows what will work before
                                   trying it.
  cross_validate_with_dxf          Pure string comparison. No OCR, no model, no key. There is no
                                   reason this was ever untested.
  everything else                  Needs a backend that is NOT installed here, which makes this
                                   the best possible moment to check the thing that matters
                                   most: that each one REFUSES with a message naming the missing
                                   dependency, rather than crashing, hanging, or returning an
                                   empty result that reads like a successful analysis finding
                                   nothing. An OCR tool that answers "no text found" because
                                   paddleocr is absent is the worst failure in this whole bank.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

V = Session("vision")
results = []


def check(label, cond, detail=""):
    results.append((label, bool(cond)))
    print(f"  {'OK  ' if cond else 'FAIL'} {label}" + ("" if cond else f"  -> {detail}"))


def call(tool, args, label=None):
    ok, r = V.call(tool, args)
    return ok, r, label or tool


print("== the sidecar is reachable and says what it can do ==")
ok, r, _ = call("vision_health", {})
check("vision_health reports ok against a live sidecar",
      ok and (r or {}).get("status") == "ok", str(r)[:150])
check("and reports the base URL it found", bool((r or {}).get("base_url")), str(r)[:150])

ok, r, _ = call("vision_version", {})
deps = (r or {}).get("optional_deps") or {}
keys = (r or {}).get("api_keys") or {}
check("vision_version reports the version", (r or {}).get("version") == "0.2.0", str(r)[:120])
check("it enumerates every optional backend, present or not", len(deps) >= 8, str(deps))
check("it reports API key availability separately from library availability",
      set(keys) >= {"anthropic", "openai"}, str(keys))
print(f"       backends present: {[k for k, v in deps.items() if v] or 'none'}")
print(f"       API keys present: {[k for k, v in keys.items() if v] or 'none'}")

# ─────────────────── the one tool that needs nothing ───────────────────
print("\n== cross_validate_with_dxf needs no model and no key ==")
# The wire names are snake_case here, and `image` below is a nested object rather than a bare
# path. An earlier version of this script guessed camelCase and a flat path; every call came back
# 422 and looked like seven broken tools. Unknown fields being ignored while required ones are
# reported missing is why that read as a category-wide failure - see KNOWN-GAPS C0's sibling
# problem, arguments rather than results.
ok, r, _ = call("cross_validate_with_dxf", {
    "ocr_strings": ["A-101", "SKALA 1:50", "2400", "POM. 1.03"],
    "dxf_strings": ["A-101", "SKALA 1:50", "2400", "POM 1.03"],
})
check("it runs at all", ok, str(r)[:170])
if isinstance(r, dict):
    print(f"       {str(r)[:300]}")
    check("it reports matches", any(k in r for k in ("matched", "matches", "exact")), str(r)[:170])

ok, r, _ = call("cross_validate_with_dxf", {"ocr_strings": ["ONLY-IN-OCR"], "dxf_strings": []})
check("a value present in OCR and absent from the DXF is reported, not swallowed",
      ok and "only-in-ocr" in str(r).lower(), str(r)[:200])
check("and it does NOT claim a match", ok and not (r or {}).get("matched"), str(r)[:200])

ok, r, _ = call("cross_validate_with_dxf", {"ocr_strings": [], "dxf_strings": []})
check("two empty lists are handled without an exception", ok, str(r)[:170])

# ─────────────────── the ones whose backend is missing ───────────────────
print("\n== every tool whose backend is absent must REFUSE, and name what is missing ==")
print("   (an empty 'no text found' from an OCR tool with no OCR installed is the worst")
print("    possible answer, so that is precisely what is asserted against here)")

png = r"C:\tmp\mline-verify.png"   # a real PNG this repo produced earlier
cases = [
    ("ocr_image", {"image": {"path": png}}, ("ocr", "paddle", "easyocr", "tesseract")),
    ("detect_symbols", {"image": {"path": png}, "discipline": "arch"}, ("ultralytics", "yolo", "torch")),
    ("describe_image", {"image": {"path": png}}, ("anthropic", "openai", "api key", "provider", "key")),
    ("classify_drawing", {"image": {"path": png}}, ("anthropic", "openai", "api key", "provider", "key")),
    ("extract_titleblock", {"image": {"path": png}}, ("anthropic", "openai", "api key", "provider", "key", "ocr")),
    ("extract_dimensions", {"image": {"path": png}}, ("ocr", "paddle", "easyocr", "tesseract")),
]
for tool, args, expect_words in cases:
    ok, r, _ = call(tool, args)
    text = str(r).lower()
    named = any(w in text for w in expect_words)
    if ok:
        check(f"{tool}: refused rather than returning a hollow success", False,
              f"SUCCEEDED with no backend installed: {str(r)[:150]}")
    else:
        check(f"{tool}: refuses, and the message names the missing dependency", named,
              f"refused but named nothing useful: {str(r)[:190]}")
        print(f"       {str(r)[:170]}")

print("\n== a file that does not exist must be refused before any backend is consulted ==")
for tool in ("ocr_image", "describe_image", "detect_symbols"):
    ok, r, _ = call(tool, {"image": {"path": r"C:\tmp\no-such-image.png"}})
    check(f"{tool}: missing file named in the error", (not ok) and "no-such-image" in str(r),
          str(r)[:170])

n = sum(1 for _, g in results if g)
print(f"\n==== {n}/{len(results)} ====")
for label, good in results:
    if not good:
        print(f"  FAILED: {label}")
