# -*- coding: utf-8 -*-
"""Live verification for roadmap 3.1 — polyline vertex editing.

Six tools: list_polyline_vertices, polyline_add_vertex, polyline_remove_vertex,
edit_polyline_vertex, set_polyline_width and reverse_curve.

**Numbers alone cannot settle this one.** A bulge that reads back as 1.0 and an arc that is
drawn the wrong way round produce the same JSON, and a width that stores correctly while the
polyline renders hairline looks identical in a result object. So every geometric claim is
checked twice: once by reading the value back through list_polyline_vertices, and once by
putting the polyline on screen and exporting a PNG to look at.

That is this bank's standing rule, and it is the rule that caught a 2,006,000-unit-wide mline
which had passed 35 of 36 data assertions.
"""
import math
import os
import sys

sys.path.insert(0, r"C:\Users\DELL\AppData\Local\Temp\claude\C--Users-DELL-agent-memory\12db232e-b1a1-4ca2-b92e-28c25e2ccd80\scratchpad")
from mcpcall import Session  # noqa: E402

OUT = r"C:\tmp\polyline-verify"
os.makedirs(OUT, exist_ok=True)

S = {c: Session(c) for c in ("files", "geometry-2d", "view")}
results = []


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    # An expected failure must be the failure that was expected. Without this, every
    # `expect_fail` check passes when the tool is not registered at all — which is exactly what
    # happened on the first run of this script: four tools were missing from the plugin and the
    # refusal tests reported OK, because "UnknownTool" is a failure too.
    missing = "UnknownTool" in str(r) or "no tool registered" in str(r)
    if missing:
        good = False
    else:
        good = (not ok) if expect_fail else ok
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:190]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[:110]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def close(a, b, tol=1e-6):
    return abs(a - b) <= tol


def verts(handle):
    ok, r = S["geometry-2d"].call("list_polyline_vertices", {"handle": handle})
    return r if ok else {}


print("== fresh drawing ==")
do("files", "new_document", {})

print("\n== a 4-vertex open polyline to work on ==")
r = do("geometry-2d", "draw_polyline", {
    "vertices": [{"x": 0, "y": 0}, {"x": 100, "y": 0},
                 {"x": 100, "y": 100}, {"x": 0, "y": 100}],
    "closed": False})
h = ((r or {}).get("entity") or {}).get("handle")
check("got a handle back", bool(h), str(r)[:160])

print("\n== list_polyline_vertices ==")
v = verts(h)
check("reports 4 vertices", v.get("count") == 4, str(v)[:200])
check("reports it as open", v.get("closed") is False, str(v)[:200])
check("length is 300 for 100+100+100", close(v.get("length", 0), 300, 1e-6),
      f"got {v.get('length')}")
check("first vertex is at the origin",
      v.get("vertices", [{}])[0].get("point") == [0, 0], str(v.get("vertices", [{}])[0])[:120])
check("every vertex starts with zero bulge",
      all(close(x.get("bulge", 1), 0) for x in v.get("vertices") or []),
      str(v.get("vertices"))[:200])

print("\n== polyline_add_vertex ==")
r = do("geometry-2d", "polyline_add_vertex",
       {"handle": h, "index": 4, "point": {"x": 0, "y": 200}}, label="append at the end")
if isinstance(r, dict):
    check("reports 5 vertices now", r.get("count") == 5, str(r)[:200])
    check("reports the previous count", r.get("before") == 4, str(r)[:200])
v = verts(h)
check("a separate read agrees: 5 vertices", v.get("count") == 5, str(v)[:200])
check("the appended point is where it was asked for",
      v.get("vertices", [])[4].get("point") == [0, 200], str(v.get("vertices", [])[4:])[:160])
check("length grew by 100", close(v.get("length", 0), 400, 1e-6), f"got {v.get('length')}")

r = do("geometry-2d", "polyline_add_vertex",
       {"handle": h, "index": 1, "point": {"x": 50, "y": -50}}, label="insert in the middle")
v = verts(h)
check("inserted at index 1, shifting the rest",
      v.get("vertices", [])[1].get("point") == [50, -50], str(v.get("vertices", [])[:3])[:200])
check("the old index 1 moved to index 2",
      v.get("vertices", [])[2].get("point") == [100, 0], str(v.get("vertices", [])[:4])[:200])

do("geometry-2d", "polyline_add_vertex", {"handle": h, "index": 99, "point": {"x": 0, "y": 0}},
   label="an out-of-range index is refused", expect_fail=True)
r = do("geometry-2d", "polyline_add_vertex", {"handle": h, "index": 0},
       label="a missing point is refused", expect_fail=True)

print("\n== edit_polyline_vertex ==")
r = do("geometry-2d", "edit_polyline_vertex",
       {"handle": h, "index": 1, "point": {"x": 50, "y": -80}}, label="move a vertex")
if isinstance(r, dict):
    check("reports the vertex as it was", (r.get("before") or {}).get("point") == [50, -50],
          str(r.get("before"))[:160])
    check("reports it as it now is", (r.get("vertex") or {}).get("point") == [50, -80],
          str(r.get("vertex"))[:160])

print("\n-- omitted fields are left alone, not reset --")
do("geometry-2d", "edit_polyline_vertex", {"handle": h, "index": 1, "bulge": 0.5},
   label="set a bulge on that vertex")
r = do("geometry-2d", "edit_polyline_vertex",
       {"handle": h, "index": 1, "point": {"x": 50, "y": -60}},
       label="move it again WITHOUT mentioning bulge")
v = verts(h)
check("the point moved", v.get("vertices", [])[1].get("point") == [50, -60],
      str(v.get("vertices", [])[1])[:160])
check("and the bulge SURVIVED the move", close(v.get("vertices", [])[1].get("bulge", 0), 0.5),
      f"bulge is now {v.get('vertices', [])[1].get('bulge')} — an omitted field was reset")

do("geometry-2d", "edit_polyline_vertex", {"handle": h, "index": 1},
   label="editing nothing is refused", expect_fail=True)

print("\n== set_polyline_width ==")
r = do("geometry-2d", "set_polyline_width", {"handle": h, "width": 4}, label="whole polyline")
if isinstance(r, dict):
    check("scope says whole polyline", r.get("scope") == "wholePolyline", str(r)[:200])
v = verts(h)
check("every vertex reads width 4",
      all(close(x.get("startWidth", 0), 4) and close(x.get("endWidth", 0), 4)
          for x in v.get("vertices") or []),
      str(v.get("vertices"))[:250])

r = do("geometry-2d", "set_polyline_width", {"handle": h, "width": 12, "segment": 2},
       label="one segment only")
v = verts(h)
check("segment 2 is now 12", close(v.get("vertices", [])[2].get("startWidth", 0), 12),
      str(v.get("vertices", [])[2])[:160])
check("segment 0 is still 4", close(v.get("vertices", [])[0].get("startWidth", 0), 4),
      str(v.get("vertices", [])[0])[:160])

do("geometry-2d", "set_polyline_width", {"handle": h, "width": -1},
   label="a negative width is refused", expect_fail=True)
do("geometry-2d", "set_polyline_width", {"handle": h, "width": 1, "segment": 99},
   label="an out-of-range segment is refused", expect_fail=True)

print("\n== polyline_remove_vertex ==")
before_count = verts(h).get("count")
r = do("geometry-2d", "polyline_remove_vertex", {"handle": h, "index": 1})
if isinstance(r, dict):
    check("reports the vertex it removed",
          (r.get("removed") or {}).get("point") == [50, -60], str(r.get("removed"))[:160])
v = verts(h)
check("one fewer vertex", v.get("count") == before_count - 1,
      f"{before_count} -> {v.get('count')}")
check("the shifted-up vertex is the old index 2",
      v.get("vertices", [])[1].get("point") == [100, 0], str(v.get("vertices", [])[:3])[:200])

print("\n== reverse_curve ==")
v = verts(h)
first_pt, last_pt = v["vertices"][0]["point"], v["vertices"][-1]["point"]
r = do("geometry-2d", "reverse_curve", {"handle": h})
if isinstance(r, dict):
    check("start and end are reported swapped",
          [round(x, 6) for x in (r.get("start") or [])[:2]] == last_pt,
          f"start={r.get('start')}, expected {last_pt}")
v = verts(h)
check("a separate read agrees the order flipped",
      v["vertices"][0]["point"] == last_pt and v["vertices"][-1]["point"] == first_pt,
      f"{v['vertices'][0]['point']} / {v['vertices'][-1]['point']}")

do("geometry-2d", "reverse_curve", {"handle": "ZZZZ"},
   label="an unknown handle is refused", expect_fail=True)

print("\n== a non-polyline is refused BY NAME ==")
r = do("geometry-2d", "draw_circle", {"center": {"x": 400, "y": 0}, "radius": 30})
ch = ((r or {}).get("entity") or {}).get("handle")
r = do("geometry-2d", "list_polyline_vertices", {"handle": ch},
       label="a circle is refused by list_polyline_vertices", expect_fail=True)
check("and the refusal names the type it found", "Circle" in str(r), str(r)[:200])
# Measured: ReverseCurve on a Circle moves nothing - the point a quarter of the way along
# stays put. Rather than report success over a no-op, the tool refuses and says why. That is
# the same standard applied to SetName in the sheet sets category.
r = do("geometry-2d", "reverse_curve", {"handle": ch},
       label="reversing a circle is refused as a no-op", expect_fail=True)
check("and the refusal names the type and explains", "Circle" in str(r) and "no observable" in str(r),
      str(r)[:220])

print("\n== on screen ==")
do("view", "zoom_extents", {})
png = os.path.join(OUT, "polyline-edit.png")
# scope="Window" needs the rectangle in drawing units - it is not inferred from the view.
# The polyline lives in 0..100 x -60..200 and the circle sits at (400,0) r=30.
do("files", "export_file", {"path": png, "format": "png", "scope": "Window",
                            "window": {"xMin": -60, "yMin": -120, "xMax": 470, "yMax": 260},
                            "widthPx": 1400, "heightPx": 900})
check("a PNG was written", os.path.exists(png) and os.path.getsize(png) > 5000,
      f"{png}: {os.path.getsize(png) if os.path.exists(png) else 'missing'} bytes")
print(f"  -> read {png} and confirm: a wide polyline, one segment visibly wider than the rest,")
print("     and no stray arc where the bulged vertex was removed.")

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
