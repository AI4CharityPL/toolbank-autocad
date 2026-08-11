# -*- coding: utf-8 -*-
"""Live verification for roadmap 5.3 — acad-geo, 7 tools.

Geographic data fails in one particular way and this whole script is aimed at it: LATITUDE AND
LONGITUDE GET SWAPPED. AutoCAD stores the reference point as (longitude, latitude, altitude), the
reverse of how people say it, and a swap produces coordinates that are perfectly well-formed and
in the wrong hemisphere. So:

  * The test position is DELIBERATELY ASYMMETRIC and unambiguous - Warsaw, latitude 52.23,
    longitude 21.01. Both are positive but they differ by more than 30 degrees, and 52 is not a
    legal longitude for anywhere near Poland, so a swap cannot pass unnoticed. A position like
    (50, 50) would pass a swapped implementation perfectly.
  * The two conversion tools are checked as EXACT INVERSES: a drawing point converted to geo and
    back must return to itself. That is arithmetic the tools cannot fake.
  * A point OFFSET from the design point must produce a DIFFERENT latitude and longitude, and in
    the right direction - moving north must raise the latitude. A conversion that ignored its
    input and returned the drawing origin would pass a single round trip and fail this.
  * Every tool that needs a geographic location is checked BEFORE one exists, so the refusals are
    proved to fire for the right reason rather than as a side effect of something else.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session  # noqa: E402

S = {c: Session(c) for c in ("files", "geo", "geometry-2d")}
results = []

LAT, LON = 52.23, 21.01          # Warsaw. Asymmetric on purpose: 52 is not a plausible longitude here.


def do(cat, tool, args, label=None, expect_fail=False):
    ok, r = S[cat].call(tool, args)
    label = label or tool
    missing = "UnknownTool" in str(r) or "not found in category" in str(r)
    good = False if missing else ((not ok) if expect_fail else ok)
    results.append((label, good))
    detail = "" if good else f"  -> {str(r)[:190]}"
    if missing:
        detail = f"  -> TOOL NOT REGISTERED: {str(r)[:150]}"
    elif expect_fail and not ok:
        detail = f"  (refused as intended: {str(r)[-105:]})"
    print(f"  {'OK  ' if good else 'FAIL'} {label}{detail}")
    return r


def check(label, condition, detail=""):
    results.append((label, bool(condition)))
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + ("" if condition else f"  -> {detail}"))


def near(a, b, tol):
    return a is not None and b is not None and abs(a - b) <= tol


print("== fresh drawing, with NO geographic location yet ==")
do("files", "new_document", {})
ok, r = S["files"].call("list_documents", {})
if not ok:
    raise SystemExit(f"AutoCAD not reachable: {r}")
for d in (r.get("documents") or [])[:-1]:
    S["files"].call("close_document", {"path": d.get("path") or d.get("name"), "save": False})

# Refusals BEFORE a location exists, so they cannot pass as a side effect of something else.
r = do("geo", "get_geographic_location", {},
       label="reading a location before there is one is refused", expect_fail=True)
check("and the refusal says how to create one rather than answering with zeros",
      "set_geographic_location" in str(r), str(r)[:200])
do("geo", "convert_wcs_to_geo", {"point": {"x": 0, "y": 0, "z": 0}},
   label="converting before there is a location is refused", expect_fail=True)
do("geo", "remove_geolocation", {},
   label="removing a location that does not exist is refused", expect_fail=True)
r = do("geo", "list_geo_markers", {}, label="listing markers works even with no location")
check("and it says so rather than leaving empty coordinates to be guessed at",
      isinstance(r, dict) and r.get("hasGeoLocation") is False and r.get("count") == 0, str(r)[:200])

# ── setting the location ────────────────────────────────────────────────────
print(f"\n== set_geographic_location: latitude {LAT}, longitude {LON} - asymmetric on purpose ==")
r = do("geo", "set_geographic_location",
       {"latitude": LAT, "longitude": LON, "designPoint": {"x": 0, "y": 0, "z": 0},
        "horizontalUnits": "Meters"})
if isinstance(r, dict):
    check(f"PROVEN latitude and longitude are NOT swapped: {LAT} comes back as the latitude and "
          f"{LON} as the longitude. A position like (50, 50) would have passed either way, which "
          f"is exactly why this one was chosen",
          near(r.get("latitude"), LAT, 1e-9) and near(r.get("longitude"), LON, 1e-9), str(r)[:280])
    check("it reports this was a new location rather than a replacement",
          r.get("replaced") is False, str(r)[:200])
    loc = r.get("location") or {}
    check("and the north direction is reported as an ANGLE, since it is read-only and derived",
          isinstance(loc.get("northDirectionAngle"), (int, float)), str(loc)[:250])

r = do("geo", "get_geographic_location", {})
check("PROVEN it reads back through a second tool with latitude and longitude the right way round",
      isinstance(r, dict) and near(r.get("latitude"), LAT, 1e-9)
      and near(r.get("longitude"), LON, 1e-9), str(r)[:250])

do("geo", "set_geographic_location", {"latitude": 91, "longitude": 0},
   label="a latitude above 90 is refused", expect_fail=True)
do("geo", "set_geographic_location", {"latitude": 0, "longitude": 181},
   label="a longitude above 180 is refused", expect_fail=True)
do("geo", "set_geographic_location", {"longitude": LON},
   label="a location with no latitude is refused", expect_fail=True)

# ── the conversions, as exact inverses ──────────────────────────────────────
print("\n== convert_wcs_to_geo / convert_geo_to_wcs: exact inverses ==")
r = do("geo", "convert_wcs_to_geo", {"point": {"x": 0, "y": 0, "z": 0}},
       label="the design point itself")
if isinstance(r, dict):
    check(f"PROVEN the design point maps to the position it was given - latitude {LAT}, longitude "
          f"{LON} - and the two are still not swapped",
          near(r.get("latitude"), LAT, 1e-6) and near(r.get("longitude"), LON, 1e-6), str(r)[:250])

# THE control: a point 1000 m NORTH must raise the latitude and barely move the longitude.
r = do("geo", "convert_wcs_to_geo", {"point": {"x": 0, "y": 1000, "z": 0}},
       label="a point 1000 units NORTH of the design point")
if isinstance(r, dict):
    dlat = (r.get("latitude") or 0) - LAT
    dlon = (r.get("longitude") or 0) - LON
    check("PROVEN the conversion uses its input and in the right direction: going north RAISES "
          "the latitude, and by roughly 1000 m worth of it (about 0.009 degrees). A tool that "
          "ignored its argument and returned the design position would have passed the check "
          "above and failed this one",
          dlat > 0.005 and dlat < 0.02 and abs(dlon) < 0.005,
          f"dlat={dlat:.6f} dlon={dlon:.6f}")
    lat_n, lon_n = r.get("latitude"), r.get("longitude")

    r2 = do("geo", "convert_geo_to_wcs", {"latitude": lat_n, "longitude": lon_n},
            label="and back again")
    if isinstance(r2, dict):
        p = r2.get("point") or {}
        check("PROVEN the two tools are exact inverses: converting back returns to (0, 1000) - "
              "arithmetic the tools cannot fake",
              near(p.get("x"), 0, 0.5) and near(p.get("y"), 1000, 0.5), str(p))

# And EAST must raise the longitude, not the latitude - the swap check in the other axis.
r = do("geo", "convert_wcs_to_geo", {"point": {"x": 1000, "y": 0, "z": 0}},
       label="a point 1000 units EAST")
if isinstance(r, dict):
    dlat = (r.get("latitude") or 0) - LAT
    dlon = (r.get("longitude") or 0) - LON
    check("PROVEN the axes are not crossed either: going EAST raises the longitude and leaves the "
          "latitude alone - the mirror of the north check, and a swapped implementation fails one "
          "of the two",
          dlon > 0.005 and abs(dlat) < 0.005, f"dlat={dlat:.6f} dlon={dlon:.6f}")

do("geo", "convert_geo_to_wcs", {"latitude": 95, "longitude": 0},
   label="an impossible latitude is refused", expect_fail=True)
do("geo", "convert_wcs_to_geo", {}, label="converting with no point is refused", expect_fail=True)

# ── markers ─────────────────────────────────────────────────────────────────
print("\n== place_geo_marker / list_geo_markers ==")
r = do("geo", "place_geo_marker", {"latitude": LAT, "longitude": LON, "notes": "site origin"},
       label="a marker placed BY COORDINATES")
if isinstance(r, dict):
    p = r.get("position") or {}
    check("PROVEN placing by coordinates lands at the design point, and the result reports BOTH "
          "the drawing point and the position on Earth",
          near(p.get("x"), 0, 0.5) and near(p.get("y"), 0, 0.5)
          and near(r.get("latitude"), LAT, 1e-6), str(r)[:280])

r = do("geo", "place_geo_marker", {"point": {"x": 0, "y": 1000, "z": 0}, "notes": "north point"},
       label="a marker placed BY DRAWING POINT")
if isinstance(r, dict):
    check("PROVEN placing by point reports where on Earth it landed - north of the first marker",
          (r.get("latitude") or 0) > LAT + 0.005, str(r)[:250])

do("geo", "place_geo_marker", {"point": {"x": 0, "y": 0, "z": 0}, "latitude": LAT, "longitude": LON},
   label="giving BOTH a point and coordinates is refused - they could disagree", expect_fail=True)
do("geo", "place_geo_marker", {"notes": "nowhere"},
   label="giving neither is refused", expect_fail=True)

r = do("geo", "list_geo_markers", {})
if isinstance(r, dict):
    check("both markers are listed with their notes and coordinates",
          r.get("count") == 2 and r.get("hasGeoLocation") is True
          and any(m.get("notes") == "site origin" for m in (r.get("markers") or [])),
          str(r)[:300])

# ── removal, and what it does NOT do ────────────────────────────────────────
print("\n== remove_geolocation ==")
r = do("geo", "remove_geolocation", {})
check("it reports the previous location so it can be put back",
      isinstance(r, dict) and (r.get("previous") or {}).get("designPoint") is not None, str(r)[:250])
do("geo", "get_geographic_location", {}, label="and the location is really gone", expect_fail=True)
do("geo", "convert_wcs_to_geo", {"point": {"x": 0, "y": 0, "z": 0}},
   label="the conversions refuse again", expect_fail=True)

r = do("geo", "list_geo_markers", {})
if isinstance(r, dict):
    check("PROVEN removing the location does NOT delete the markers - they are still there as "
          "entities, now without coordinates, and hasGeoLocation says why rather than leaving "
          "empty fields to be guessed at",
          r.get("count") == 2 and r.get("hasGeoLocation") is False
          and (r.get("markers") or [{}])[0].get("latitude") is None, str(r)[:300])

r = do("geo", "set_geographic_location", {"latitude": LAT, "longitude": LON},
       label="setting a location again")
check("and it reports this one as a replacement of nothing, the previous having been removed",
      isinstance(r, dict) and r.get("replaced") is False, str(r)[:200])
r = do("geo", "list_geo_markers", {})
check("PROVEN the markers get their coordinates back once a location exists again",
      isinstance(r, dict) and r.get("hasGeoLocation") is True
      and (r.get("markers") or [{}])[0].get("latitude") is not None, str(r)[:250])

passed = sum(1 for _, ok in results if ok)
print(f"\n==== {passed}/{len(results)} checks passed ====")
for label, ok in results:
    if not ok:
        print(f"  FAILED: {label}")
raise SystemExit(0 if passed == len(results) else 1)
