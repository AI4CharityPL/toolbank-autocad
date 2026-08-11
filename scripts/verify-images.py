# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.5 - acad-images (raster half), 7 tools.

Every check here is aimed at a shape this category can fake:

  * RasterImage.Width/Height are computed from Orientation, not stored - a placement bug
    reads back as a wrong number, not a crash. The fixture is 300x150 px (2:1), so an
    aspect-preserve bug (any ratio other than exactly 0.5) is visible in the first check.
  * Rotating an ASYMMETRIC placement (60 wide, 90 tall) by 90 degrees must SWAP which
    drawing axis is the long one. A square placement could not tell a correct rotation
    from a silently-ignored one; 60x90 can (rule 26 section 14's cube-vs-sphere lesson,
    applied to a 2D swap instead of a 3D cut).
  * attach_image REUSES a definition when the same name points at the same file, so two
    entities can share one RasterImageDef - matching real AutoCAD, where inserting the
    same image twice does not duplicate the source. That makes detach_image's "was this
    the last placement" branch and set_image_path's "every entity that shares this def"
    branch REAL, reachable cases here, not just prose in a description.
  * Every per-handle operation (clip, adjust) is checked against a SECOND, untouched
    image that shares nothing with the first - the isolation control this project has
    been burned by skipping before.
  * A half-size pixel clip must produce a HALF-size drawing extent, computed independently
    of the tool (50 from 100, 25 from 50 - two different numbers, not a round symmetric one).
"""
import os
import struct
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "images")}
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


def near(a, b, tol):
    return a is not None and b is not None and abs(a - b) <= tol


# ── a minimal, self-contained PNG encoder - stdlib only (zlib), no Pillow ──
def make_png(path, width, height, rgb=(200, 80, 40)):
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)  # 8-bit RGB
    row = bytes([0]) + bytes(rgb) * width  # filter byte 0 (none) + solid colour
    raw = row * height
    idat = zlib.compress(raw, 9)
    png = sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


TMP = os.environ.get("TEMP") or os.environ.get("TMP") or "."
IMG1 = os.path.join(TMP, "acadmcp_verify_img1.png")       # 300x150 (2:1) - the shared def
REPATH = os.path.join(TMP, "acadmcp_verify_repath.png")   # 200x100 - the repath target
make_png(IMG1, 300, 150, (200, 80, 40))
make_png(REPATH, 200, 100, (40, 120, 200))

print("== fresh drawing, cross-session probe (rule 26 section 13a) ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

do("images", "list_images", {}, label="list_images on a drawing with none yet is empty, not an error")

# ── attach: aspect-preserving, and the size is PROVEN not assumed ──
print("\n== attach_image: 300x150 px fixture, width=100, height omitted ==")
do("images", "attach_image", {"path": "C:\\does\\not\\exist.png",
                               "insertionPoint": {"x": 0, "y": 0, "z": 0}, "width": 10},
   label="a missing file is refused", expect_fail=True)

r = do("images", "attach_image",
       {"path": IMG1, "insertionPoint": {"x": 0, "y": 0, "z": 0}, "width": 100, "name": "img1"})
h1 = extents1 = None
if isinstance(r, dict):
    img = r.get("image") or {}
    h1 = img.get("handle")
    extents1 = img.get("extents") or {}
    check("PROVEN aspect preserved: 300x150 is exactly 2:1, so width 100 must give height "
          "EXACTLY 50 - not 100 (a square-default bug) and not something else",
          near(img.get("width"), 100, 1e-6) and near(img.get("height"), 50, 1e-6), str(img)[:250])
    check("insertion point reads back where it was placed",
          near((img.get("insertionPoint") or {}).get("x"), 0, 1e-6)
          and near((img.get("insertionPoint") or {}).get("y"), 0, 1e-6), str(img)[:200])
    check("reusedDefinition is false for the first placement",
          r.get("reusedDefinition") is False, str(r)[:200])

do("images", "attach_image",
   {"path": REPATH, "insertionPoint": {"x": 1000, "y": 1000, "z": 0}, "width": 10, "name": "img1"},
   label="the same name against a DIFFERENT file is refused", expect_fail=True)

# ── attach again: SAME name+file (shares the def), explicit non-aspect size, rotated ──
print("\n== attach_image again: same file, EXPLICIT 60x90 (not the 2:1 aspect), rotated 90deg ==")
r = do("images", "attach_image",
       {"path": IMG1, "insertionPoint": {"x": 500, "y": 500, "z": 0},
        "width": 60, "height": 90, "rotationDegrees": 90, "name": "img1"})
h2 = extents2 = None
if isinstance(r, dict):
    img = r.get("image") or {}
    h2 = img.get("handle")
    extents2 = img.get("extents") or {}
    check("PROVEN the definition was REUSED, not duplicated - same name, same file",
          r.get("reusedDefinition") is True, str(r)[:200])
    check("explicit height overrides aspect-preserve: 90, not 60*0.5=30",
          near(img.get("width"), 60, 1e-6) and near(img.get("height"), 90, 1e-6), str(img)[:250])
    spanX = (extents2.get("max") or {}).get("x", 0) - (extents2.get("min") or {}).get("x", 0)
    spanY = (extents2.get("max") or {}).get("y", 0) - (extents2.get("min") or {}).get("y", 0)
    check("PROVEN the 90deg rotation actually rotated the geometry: a 60x90 placement turned "
          "90 degrees must span ~90 in X and ~60 in Y - the SWAPPED numbers, not the original "
          "60/90. A square placement could not have told a real rotation from a no-op",
          near(spanX, 90, 0.5) and near(spanY, 60, 0.5), f"spanX={spanX:.3f} spanY={spanY:.3f}")

check("the two placements have different handles", h1 is not None and h2 is not None and h1 != h2,
      f"h1={h1} h2={h2}")

r = do("images", "list_images", {})
if isinstance(r, dict):
    names = [i.get("name") for i in (r.get("images") or [])]
    check("PROVEN both entities report the SAME shared name, matching AutoCAD's own model where "
          "the definition (not the placement) carries the name",
          r.get("count") == 2 and names == ["img1", "img1"], str(names))

# ── clip: half the pixel size must give half the drawing extent, and NOT touch image 2 ──
print("\n== clip_image: half the pixel bounds on image 1 only ==")
r = do("images", "clip_image", {"handle": h1, "points": [{"x": 0, "y": 0}]},
       label="a single clip point is refused", expect_fail=True)

r = do("images", "clip_image", {"handle": h1, "points": [{"x": 0, "y": 0}, {"x": 150, "y": 75}]})
if isinstance(r, dict):
    check("clipped is true and the pixel bounds reported are the FULL 300x150, not the clip window",
          r.get("clipped") is True and near(r.get("imageWidthPx"), 300, 0.5)
          and near(r.get("imageHeightPx"), 150, 0.5), str(r)[:250])
    ea = r.get("extentsAfter") or {}
    spanX = (ea.get("max") or {}).get("x", 0) - (ea.get("min") or {}).get("x", 0)
    spanY = (ea.get("max") or {}).get("y", 0) - (ea.get("min") or {}).get("y", 0)
    check("PROVEN arithmetic: clipping to HALF the pixel width/height gives HALF the drawing "
          "extent - 50 from the original 100, 25 from the original 50. Two different numbers, "
          "not a round symmetric one that could pass by accident",
          near(spanX, 50, 0.5) and near(spanY, 25, 0.5), f"spanX={spanX:.3f} spanY={spanY:.3f}")

r = do("images", "list_images", {})
if isinstance(r, dict):
    img2now = next((i for i in (r.get("images") or []) if i.get("handle") == h2), None)
    e2 = (img2now or {}).get("extents") or {}
    check("PROVEN image 2 is UNTOUCHED by clipping image 1, despite sharing the same source "
          "definition - clip is per-ENTITY even when the definition is shared",
          near((e2.get("min") or {}).get("x"), (extents2.get("min") or {}).get("x"), 1e-6)
          and near((e2.get("max") or {}).get("y"), (extents2.get("max") or {}).get("y"), 1e-6),
          str(e2)[:250])

r = do("images", "clip_image", {"handle": h1}, label="omitting points REMOVES the clip")
if isinstance(r, dict):
    ea = r.get("extentsAfter") or {}
    spanX = (ea.get("max") or {}).get("x", 0) - (ea.get("min") or {}).get("x", 0)
    spanY = (ea.get("max") or {}).get("y", 0) - (ea.get("min") or {}).get("y", 0)
    check("PROVEN un-clipping returns to the ORIGINAL full size, 100x50 - a round trip, not a "
          "guess that removal happened",
          r.get("clipped") is False and near(spanX, 100, 0.5) and near(spanY, 50, 0.5),
          f"spanX={spanX:.3f} spanY={spanY:.3f}")

# ── adjust: only the given channel changes, and image 2 is untouched ──
print("\n== set_image_adjust ==")
do("images", "set_image_adjust", {"handle": h1}, label="nothing to change is refused", expect_fail=True)
do("images", "set_image_adjust", {"handle": h1, "brightness": 150},
   label="brightness above 100 is refused", expect_fail=True)

r = do("images", "set_image_adjust", {"handle": h1, "brightness": 80, "contrast": 20})
if isinstance(r, dict):
    before, after = r.get("before") or {}, r.get("after") or {}
    check("PROVEN AutoCAD's own defaults were 50/50/0 before this call",
          before.get("brightness") == 50 and before.get("contrast") == 50 and before.get("fade") == 0,
          str(before))
    check("PROVEN only brightness and contrast changed - fade, not given, stayed at 0",
          after.get("brightness") == 80 and after.get("contrast") == 20 and after.get("fade") == 0,
          str(after))

r = do("images", "list_images", {})
if isinstance(r, dict):
    img2now = next((i for i in (r.get("images") or []) if i.get("handle") == h2), None)
    adj2 = (img2now or {}).get("adjust") or {}
    check("PROVEN image 2's adjustment is UNTOUCHED by adjusting image 1",
          adj2.get("brightness") == 50 and adj2.get("contrast") == 50, str(adj2))

# ── frame: drawing-wide, no handle, round trip across calls ──
print("\n== set_image_frame (drawing-wide IMAGEFRAME) ==")
do("images", "set_image_frame", {"frame": 5}, label="an out-of-range frame value is refused", expect_fail=True)
r1 = do("images", "set_image_frame", {"frame": 0})
r2 = do("images", "set_image_frame", {"frame": 2})
if isinstance(r1, dict) and isinstance(r2, dict):
    check("PROVEN it is a real round trip across two calls: the second call's BEFORE is the "
          "first call's AFTER",
          r1.get("after") == 0 and r2.get("before") == 0 and r2.get("after") == 2,
          f"{r1} / {r2}")
do("images", "set_image_frame", {"frame": 1})  # leave it at a sane default

# ── repath: affects EVERY entity sharing the definition, since both do ──
print("\n== set_image_path ==")
do("images", "set_image_path", {"handle": h2, "newPath": "C:\\nope\\nothere.png"},
   label="repathing to a missing file is refused", expect_fail=True)

r = do("images", "set_image_path", {"handle": h2, "newPath": REPATH})
if isinstance(r, dict):
    check("PROVEN loaded is true for a real, readable file",
          r.get("loaded") is True, str(r)[:200])
    affected = set(r.get("affectedHandles") or [])
    check("PROVEN repathing affects BOTH entities, because they share one definition - h1 was "
          "never touched directly but its source moved anyway",
          affected == {h1, h2}, f"affected={affected} expected={{h1,h2}}={{{h1},{h2}}}")

# ── detach: the shared def survives the FIRST removal, and dies on the LAST ──
print("\n== detach_image ==")
r = do("images", "detach_image", {"handle": h2})
if isinstance(r, dict):
    check("PROVEN the definition SURVIVED - image 1 still uses it",
          r.get("defRemoved") is False, str(r)[:200])

r = do("images", "list_images", {})
if isinstance(r, dict):
    check("one image remains after detaching the other", r.get("count") == 1, str(r)[:200])

r = do("images", "detach_image", {"handle": h1})
if isinstance(r, dict):
    check("PROVEN the definition was removed on the LAST placement",
          r.get("defRemoved") is True, str(r)[:200])

do("images", "detach_image", {"handle": h1},
   label="detaching an already-erased handle is refused", expect_fail=True)

r = do("images", "list_images", {})
if isinstance(r, dict):
    check("no images remain", r.get("count") == 0, str(r)[:200])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
