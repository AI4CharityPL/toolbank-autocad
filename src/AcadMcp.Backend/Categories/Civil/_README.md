# AutoCAD Civil (engineering / survey) — `acad-civil`

High-level civil-engineering / survey drafting tools that turn
`"draw a 6 m road from A to B with stationing every 20 m"` into the right
combination of primitives (CENTER-linetype centreline + two parallel
Continuous edges + perpendicular tick marks + station labels) on the right
layers (`C-ROAD-CNTR`, `C-ROAD-EDGE`, `C-STAT`).

Read **rule 35** (universal domain-category contract) and **rule 38**
(civil-domain traps) BEFORE you change anything in this folder.

## Tools (10)

| tool                       | what it does                                                                                                                  |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `ensure_civil_layers`      | Idempotently create the 12-layer civil key (rule 38 §9). Sub-set flags: `includeRoad`, `includeProperty`, `includeTopo`       |
| `draw_alignment_tangent`   | Single straight road segment as a CENTER-linetype line on `C-ROAD-CNTR`                                                       |
| `draw_alignment_curve`     | Single circular curve segment as an Arc on `C-ROAD-CNTR`. Spirals deferred to Phase 7                                         |
| `draw_road_corridor`       | Centreline polyline + two parallel `C-ROAD-EDGE` Continuous offsets at `widthM/2` (mitred at internal vertices, rule 38 §6)   |
| `place_station_labels`     | Walk the centreline; drop perpendicular tick + station label every `intervalM` in metric `0+020` or US `0+20` form (§1, §7)   |
| `draw_parcel`              | Walk surveyor `(bearing, distance)` legs from a start point; reports closure error vs `kind` tolerance (§3)                   |
| `draw_contour_line`        | Topographic contour polyline routed to `C-TOPO-MAJR` (labelled) or `C-TOPO-MINR` (unlabelled) per `isMajor` (§4)              |
| `place_spot_elevation`     | Cross + signed two-decimal elevation text on `C-TOPO-SPOT` (§5)                                                               |
| `draw_north_arrow`         | Triangle + optional `N` letter on `C-NORTH`, rotated by `trueNorthDegFromPageNorth` (§8)                                      |
| `civil_health`             | ReadOnly diagnostic: layer key + parcel-tolerance presets + supported stationing systems + planned bundled blocks             |

## Layer key (Polish PN + US NCS hybrid, 12 layers — rule 38 §9)

| layer            | colour | linetype     | weight | content                              |
| ---------------- | ------ | ------------ | ------ | ------------------------------------ |
| `C-ROAD-CNTR`    | 4      | CENTER       | 0.30   | road centreline (alignment)          |
| `C-ROAD-EDGE`    | 7      | Continuous   | 0.50   | edge of pavement                     |
| `C-ROAD-LANE`    | 3      | DASHED       | 0.18   | lane lines                           |
| `C-PROP`         | 6      | PHANTOM2     | 0.50   | property / parcel boundary           |
| `C-ESMT`         | 6      | HIDDEN2      | 0.25   | easement                             |
| `C-ROW`          | 6      | PHANTOM      | 0.50   | right of way                         |
| `C-TOPO-MAJR`    | 8      | Continuous   | 0.35   | major contour line                   |
| `C-TOPO-MINR`    | 9      | Continuous   | 0.13   | minor contour line                   |
| `C-TOPO-SPOT`    | 2      | Continuous   | 0.18   | spot elevation marks + labels        |
| `C-STAT`         | 2      | Continuous   | 0.18   | stationing tick marks + labels       |
| `C-ANNO`         | 2      | Continuous   | 0.18   | civil annotations                    |
| `C-NORTH`        | 7      | Continuous   | 0.50   | north arrow                          |

The single source of truth is `CivilPalette.cs`. Mirror **rule 38 §9** if you
change either side.

## Surveyor numerics (`CivilGeometry.cs`)

- `Bearing.Parse("N 45 30 15 E")` accepts ASCII or Unicode (° ′ ″) markers
  and rejects out-of-range minutes / seconds.
- `Bearing.ToVector()` returns the planar unit vector with the correct sign
  per quadrant (rule 38 §2). NEVER convert a bearing with `degrees * π / 180`.
- `CivilStationing.Format(metres, system)` — `metric_km` → `"0+020"`,
  `us_feet` → `"0+20"` (1 station = 100 ft).
- `CivilParcel.Traverse(start, legs, toleranceM)` walks the legs and
  reports closure error in metres, with a boolean `WithinTolerance` so
  `draw_parcel` can mark `closureStatus = "in_tolerance"` /
  `"out_of_tolerance"`.

## Parcel closure tolerances (rule 38 §3, Polish geodetic office values)

| `kind`         | tolerance |
| -------------- | --------- |
| residential    | 0.02 m    |
| commercial     | 0.05 m    |
| agricultural   | 0.20 m    |
| forest         | 0.50 m    |

Override via `toleranceMOverride`.

## v1 limitations (also in the manifest `metadata.v1_limitations`)

1. **Vertical alignments / profile views** ship in **Phase 7**.
2. **Spirals (clothoid transitions)** are not in v1 — only tangents and
   circular curves on horizontal alignments.
3. `draw_road_corridor` mitres internal vertices using the average-normal
   method without an acute-corner self-intersection check; sharper corners
   arrive with spiral support in Phase 7.
4. `draw_north_arrow` synthesises a basic triangle inline; the COMPASS
   variant ships with the **Phase-7** block library under `blocks/civil/`.

## Paired validators

Under `validators/civil/`:

- `civil.road.centerline-on-c-road-cntr`
- `civil.road.centerline-must-be-dashed`
- `civil.road.edge-on-c-road-edge`
- `civil.topo.spot-on-c-topo-spot`
- `civil.parcel.on-c-prop`

Bundled into the new standard `validators/_standards/civil-baseline.yaml`.
Parcel closure tolerance enforcement is performed at-write-time by
`draw_parcel` itself; a post-hoc validator will land once the engine grows
a `polyline_closure_within` check primitive.

## How to regenerate the manifest from code

```powershell
dotnet build src/AcadMcp.Backend -c Release
src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe --category civil --regenerate-manifest
```
