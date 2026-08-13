# apartment-120-test

First live proof of `docs/engineering-rules/73-space-planning-method.md` — a ~123.5 m² gross
apartment built ZONE-FIRST (day cluster / buffer corridor / night cluster), not the single-row
"kulfon" pattern every earlier build in this repo used. Built by `scripts/build_apartment_120_test.py`.

## Design (decided before any tool call — rule 73 steps 1-5)

- Envelope: 13000 × 9500mm. `WALL_T` = 150mm uniformly (interior AND exterior — a documented
  simplification for this demo; a real exterior wall would be thicker). `INSET` = 75mm net-internal.
- **DAY zone** (y 0-4350, on the entry + south/daylight facade): Przedpokój (entry, 8.61 m²) |
  Kuchnia (11.97 m²) | Salon z jadalnią (32.13 m²).
- **BUFFER** (y 4350-5700, 1200mm net clear = WT §95 minimum exactly): Korytarz (15.42 m²) — the
  day/night separator rule 73 step 3 calls for.
- **NIGHT zone** (y 5700-9500, on the north facade, reached only via the corridor): Sypialnia
  rodziców (10.77 m²) | Łazienka 1 (6.39 m²) | Sypialnia dziecka 1 (10.59 m²) | Sypialnia dziecka 2
  (10.59 m²) | Łazienka 2 (6.39 m²).
- Every bedroom net footprint checked ≥ 2800×3600mm and kitchen/living checked against their own
  `populate_room` preset minimums (rule 64 §6) BEFORE any wall was drawn (rule 73 step 5).
  Bathrooms land at net 1750×3650mm — comfortably over the 1600×2200mm preset minimum but an
  elongated proportion; a known simplification (see script docstring), not hidden.
- Structural grid (3×2 non-uniform bays via `acad-grids.draw_grid`, 12× HEA140 columns + 1× IPE160
  facade beam) is deliberately independent of the partition layout — the partitions are
  non-load-bearing infill (normal multi-family slab+column construction), per rule 72 §3.
- 9 doors + 5 windows, every opening with its own `insert_lintel`-sized RC lintel and
  `LINTEL_TYPE` tag (14 lintels total).

## Verification results

- `audit_all_rooms(cellMm=50, marginMm=1700, tolerancePct=10)`: 9/9 rooms measured via real
  flood-fill (no raycast fallback). 7/9 rooms flagged `labelMismatch` (8.5-18.5% measured-vs-
  declared) — **root-caused, not a construction defect**: `RoomRegionSolver`'s opening-sealing
  disc (`r = max(widthMm/2 + 1.5*cellMm, 2*cellMm)`) legitimately shrinks the flood-filled area
  near every door/window, worse for door-dense small rooms (the corridor, 6 doors, lost 18.5%;
  the bathrooms, 1 door each, stayed under the 10% tolerance). Full writeup in rule 73's own
  section on this. 0 `leakSuspected`, 0 `emptyOpenings` once `marginMm` was raised past the
  widest opening's own seal radius (1575mm for the 3000mm living-room window) — an earlier pass
  with `marginMm=400` produced a false 0-doors-detected reading on every room, also documented
  in rule 73.
- Rule 60 §1a criterion 18 (public/day zone reachable from entry without crossing a private
  zone): **PASS** — `EXT→0.1→0.2→0.3` stays entirely within the day/entry zone; every night-zone
  door hangs off the corridor (`0.4`), never directly off the entry.
- Criterion 19 (daylight-declared rooms actually sit on an exterior wall with a window):
  **PASS** — windows recorded in rooms `{0.2, 0.3, 0.5, 0.7, 0.8}`, exactly the daylight-required
  set (kitchen, living, all 3 bedrooms). Bathrooms deliberately windowless (mechanical
  ventilation), matching common Polish apartment practice.
- Criterion 20 (built adjacency graph matches this project's own declared table): **PASS** —
  the 9 built door edges match the declared table exactly, no missing or unexpected edges.

## Files

- `Apartment120Test.dwg` — the built drawing.
- `../../scripts/build_apartment_120_test.py` — the build script (re-runnable; starts from
  `new_document`).
