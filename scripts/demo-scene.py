# -*- coding: utf-8 -*-
"""Buduje mala, czytelna scene demonstracyjna na obecnym buildzie i eksportuje PNG.

Cel: obraz do README, ktory pokazuje stan FAKTYCZNY, a nie artefakt sprzed przegladu.
Malo tekstu i duze pomieszczenia - zeby nie odtworzyc problemu nachodzacych etykiet,
ktory dyskwalifikuje stare rendery.
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # scripts/mcpcall.py
from mcpcall import Session, j

S = {}
for c in ("files", "architecture", "layers", "hatches", "callouts", "furniture", "view", "annotations"):
    S[c] = Session(c)

steps = []


def do(cat, tool, args, label=None):
    ok, r = S[cat].call(tool, args)
    label = label or f"{cat}.{tool}"
    steps.append((label, ok))
    print(f"  {'OK  ' if ok else 'FAIL'} {label}" + ("" if ok else f"  -> {str(r)[:150]}"))
    return r if ok else None


# Obrys 12 x 8 m; sciana pionowa x=7000, pozioma y=4000 po prawej i y=4500 po lewej
OUT = [(0, 0), (12000, 0), (12000, 8000), (0, 8000)]

try:
    print("== nowy rysunek ==")
    do("files", "new_document", {})
    do("architecture", "ensure_architectural_layers", {})
    # Styl tekstu dla etykiet pomieszczen tworzy teraz samo define_room (ACADMCP-ROOM,
    # TrueType), bo domyslny SHX nie ma glifu ². Nic tu nie trzeba ustawiac.

    print("\n== sciany ==")
    do("architecture", "draw_walls_chain",
       {"vertices": [{"x": x, "y": y} for x, y in OUT], "thicknessMm": 250, "closed": True},
       "sciany zewnetrzne 250 mm")
    do("architecture", "draw_walls_chain",
       {"vertices": [{"x": 7000, "y": 0}, {"x": 7000, "y": 8000}], "thicknessMm": 120, "closed": False},
       "sciana dzialowa pionowa")
    do("architecture", "draw_walls_chain",
       {"vertices": [{"x": 0, "y": 4500}, {"x": 7000, "y": 4500}], "thicknessMm": 120, "closed": False},
       "sciana dzialowa pozioma lewa")
    do("architecture", "draw_walls_chain",
       {"vertices": [{"x": 7000, "y": 4000}, {"x": 12000, "y": 4000}], "thicknessMm": 120, "closed": False},
       "sciana dzialowa pozioma prawa")

    print("\n== pomieszczenia ==")
    do("architecture", "define_room",
       {"vertices": [{"x": 200, "y": 4700}, {"x": 6800, "y": 4700}, {"x": 6800, "y": 7800}, {"x": 200, "y": 7800}],
        "number": "1.01", "name": "GABINET", "tagTextHeightMm": 300}, "1.01 GABINET")
    do("architecture", "define_room",
       {"vertices": [{"x": 200, "y": 200}, {"x": 6800, "y": 200}, {"x": 6800, "y": 4300}, {"x": 200, "y": 4300}],
        "number": "1.02", "name": "POCZEKALNIA", "tagTextHeightMm": 300}, "1.02 POCZEKALNIA")
    do("architecture", "define_room",
       {"vertices": [{"x": 7200, "y": 4200}, {"x": 11800, "y": 4200}, {"x": 11800, "y": 7800}, {"x": 7200, "y": 7800}],
        "number": "1.03", "name": "ZABIEGOWY", "tagTextHeightMm": 300}, "1.03 ZABIEGOWY")
    do("architecture", "define_room",
       {"vertices": [{"x": 7200, "y": 200}, {"x": 11800, "y": 200}, {"x": 11800, "y": 3800}, {"x": 7200, "y": 3800}],
        "number": "1.04", "name": "SZATNIA", "tagTextHeightMm": 300}, "1.04 SZATNIA")

    print("\n== drzwi i okna ==")
    do("architecture", "insert_door", {"hinge": {"x": 2000, "y": 4500}, "widthMm": 900, "hingeAngleDeg": 0}, "drzwi 1.01/1.02")
    do("architecture", "insert_door", {"hinge": {"x": 7000, "y": 2000}, "widthMm": 900, "hingeAngleDeg": 90}, "drzwi 1.02/1.04")
    do("architecture", "insert_door", {"hinge": {"x": 7000, "y": 6000}, "widthMm": 900, "hingeAngleDeg": 90}, "drzwi 1.02/1.03")
    do("architecture", "insert_window", {"center": {"x": 3500, "y": 0}, "widthMm": 1800}, "okno poludniowe")
    do("architecture", "insert_window", {"center": {"x": 9500, "y": 0}, "widthMm": 1800}, "okno poludniowe 2")
    do("architecture", "insert_window", {"center": {"x": 3500, "y": 8000}, "widthMm": 1800}, "okno polnocne")

    print("\n== kreskowanie posadzki (to jest naprawiony draw_hatch_by_boundary) ==")
    do("hatches", "apply_material_preset_by_point",
       {"seedPoint": {"x": 9500, "y": 2000}, "material": "tile", "scaleMultiplier": 3},
       "posadzka 1.04 - preset 'tile' po punkcie")

    print("\n== opisy arkusza ==")
    do("callouts", "insert_north_arrow", {"position": {"x": 13500, "y": 7000}, "sizeMm": 900, "style": "simple"})
    do("callouts", "insert_scale_bar", {"position": {"x": 0, "y": -1200}, "scaleDenominator": 100})

    print("\n== eksport ==")
    do("view", "zoom_extents", {})
    out = r"C:\Users\DELL\Dev\autocad-mcp\assets\readme\demo-clinic.png"
    do("files", "export_file",
       {"path": out, "format": "PNG", "scope": "Window",
        "window": {"xMin": -1500, "yMin": -2500, "xMax": 15000, "yMax": 9200},
        "widthPx": 2000, "heightPx": 1250}, "PNG 2000x1250")

finally:
    for s in S.values():
        s.close()

ok_n = sum(1 for _, o in steps if o)
print(f"\n-> {ok_n}/{len(steps)}")
for label, o in steps:
    if not o:
        print("   NIEUDANE:", label)
