# 64. Furniture density / placement policy (acad-furniture)

Furniture category (acad-furniture) policy - block naming, layer split, attribute contract, populate_room presets, clearance budget per PN-EN 17210 / WT 2019. READ BEFORE editing FurniturePluginTools.cs, adding new presets, or calling insert_* from higher-level categories (architecture, planning).

Companion to rule 19 (tool implementation pattern), rule 28 (block / layer traps),
rule 22 (argument shape) and rule 35 (domain-categories design). Enforces consistent
block naming, attribute contract, layer split and minimum clearance per PN-EN 17210
(accessibility) + WT-2019 (Polish technical conditions for hospitals/offices) so
later categories (`acad-schedules`, `acad-architecture`, `populate_room`) get a
predictable world to reason about.

## 1. Drawing unit

**All furniture factories assume the drawing unit is millimetre.** A bed 900×2000 is
literally a polyline 900×2000 at scale 1. Callers MUST call
`files.set_units('mm')` before inserting furniture. Blocks created in an
inches/metres drawing will be visually wrong and later schedule dimensions will
lie.

## 2. Block naming convention (MANDATORY)

All furniture blocks start with the prefix `FURN-`. This prefix is how
`list_furniture_in_model` distinguishes furniture from architecture blocks.

Name shape:
- **Fixed**: `FURN-<CATEGORY>-<VARIANT>` — concrete fixed-size block.
  Examples: `FURN-BED-STD`, `FURN-CHAIR-OFF`, `FURN-SOFA-CLN-3`.
- **Sized**: `FURN-<CATEGORY>-<VARIANT>-<W>-<D>` — parametric; last two tokens
  are integer width/depth in mm. Examples: `FURN-DESK-OFF-1600-800`,
  `FURN-CBT-MED-800-500`, `FURN-TBL-RECT-1200-800`.

Never hand-craft a FURN- block with a different shape. It breaks
`list_furniture_in_model` attribute introspection and `list_furniture_catalog`
enumeration.

### Fixed catalogue (must stay in sync with `s_fixedCatalog`)

| name              | category | domain        | W × D (mm)  | purpose |
|-------------------|----------|---------------|-------------|---------|
| FURN-BED-STD      | bed      | hospital      |  900 × 2000 | ward standard |
| FURN-BED-ICU      | bed      | hospital      | 1000 × 2200 | ICU with head monitor + rails |
| FURN-BED-BARIAT   | bed      | hospital      | 1200 × 2200 | reinforced bariatric |
| FURN-BED-PED      | bed      | hospital      |  700 × 1500 | pediatric |
| FURN-BED-OR       | bed      | hospital      |  550 × 2100 | operating table |
| FURN-BED-LBR      | bed      | hospital      | 1050 × 2300 | labour / delivery |
| FURN-CHAIR-OFF    | chair    | office        |  550 × 550  | swivel |
| FURN-CHAIR-ARM    | chair    | residential   |  800 × 800  | armchair |
| FURN-CHAIR-STL    | chair    | hospital      |  450 × 450  | round stool |
| FURN-CHAIR-EXAM   | chair    | hospital      |  600 × 600  | exam rolling stool |
| FURN-CHAIR-WHL    | chair    | hospital      |  700 × 1100 | wheelchair |
| FURN-SOFA-2       | sofa     | residential   | 1800 × 800  | 2-seat lounge |
| FURN-SOFA-3       | sofa     | residential   | 2200 × 800  | 3-seat lounge |
| FURN-SOFA-CLN-2   | sofa     | hospital      | 1800 × 700  | 2-seat clinic (vinyl) |
| FURN-SOFA-CLN-3   | sofa     | hospital      | 2200 × 700  | 3-seat clinic (vinyl) |

### Sized families (must stay in sync with `s_sizedFamilies`)

| family           | default W × D (mm) | notes |
|------------------|--------------------|-------|
| FURN-DESK-OFF    | 1600 × 800         | office desk + drawer lines |
| FURN-DESK-RCP    | 2400 × 800         | reception L-counter |
| FURN-DESK-NST    | 3000 × 900         | nurse station raised edge |
| FURN-CBT-STR     |  800 × 400         | storage + door-swing arc |
| FURN-CBT-MED     |  900 × 450         | medical, glass-door cross |
| FURN-CBT-FIL     | 1000 × 450         | file drawers |
| FURN-CBT-WDR     | 1200 × 600         | wardrobe + hanger rail |
| FURN-TBL-RECT    | 1200 × 800         | rectangular meeting |
| FURN-TBL-ROUND   | 1200 × 1200        | round (W = D = diameter) |
| FURN-TBL-SQ      | 1000 × 1000        | square |
| FURN-TBL-EXAM    | 1900 × 700         | medical exam + paper-roll slot |

Any new family MUST also extend `BuildSizedBlock` + catalog + this table in the
same commit, or rule-40 (pre-commit gates) will flag it.

## 3. Block origin

Every FURN- block has its **origin at the geometric centre of the footprint**.
Rotations spin around the centre, not a corner. This simplifies populating rooms
(we place the centre, rotate, done) but it also means scripts that assume corner
origin (like copy-paste from AutoDesk sample furniture blocks) must recentre
before comparing.

## 4. Attribute contract

Every FURN- block carries four attribute definitions in this exact order:

| tag    | prompt            | visible? | default             | purpose                      |
|--------|-------------------|----------|---------------------|------------------------------|
| INV_ID | Inventory ID      | visible  | `—` (em dash)       | downstream schedules / asset |
| TYPE   | Type / variant    | hidden   | block name          | schedule grouping            |
| ROOM   | Room code         | hidden   | `""`                | links to room labels         |
| NOTE   | Note              | hidden   | `""`                | free text                    |

`list_furniture_in_model` reads TYPE and INV_ID; `acad-schedules`
(`generate_furniture_schedule` planned for D8) will pivot on (TYPE, ROOM).
Downstream categories MUST NOT mutate INV_ID after placement — treat it as an
append-only inventory ID.

## 5. Layer split (AIA-2017 + Polish BIP plan convention)

Default layers (applied automatically by `DefaultLayerFor`):

| block prefix   | default layer | color | purpose |
|----------------|---------------|-------|---------|
| FURN-BED       | A-FURN-BED    | 40    | beds — own layer for freezing on clean plans |
| FURN-CHAIR     | A-FURN-CHR    | 42    | chairs |
| FURN-DESK      | A-FURN-DSK    | 42    | desks |
| FURN-CBT       | A-FURN-CBT    | 32    | cabinets |
| FURN-SOFA      | A-FURN-SFA    | 42    | sofas |
| FURN-TBL       | A-FURN-TBL    | 42    | tables |
| any FURN-*     | A-FURN        | 42    | fallback |

Callers passing an explicit `layer` argument override the default. Colors are
recommendations — actual ACI is set at layer creation time by the plotstyle
policy (rule 61, planned for D9).

## 6. `populate_room` presets (MANDATORY preset names)

These are the only preset strings accepted. Alias? Add to the preset switch in
`BuildPopulationPlan` and update this table in the same commit.

| preset     | items placed                                                         | min room (mm) |
|------------|----------------------------------------------------------------------|---------------|
| ward-room  | 2× bed + 2× nightstand (FURN-CBT-STR 400×400) + 1× armchair          | 3200 × 3800 |
| icu-room   | 1× ICU-bed + medical cabinet + visitor armchair                      | 3600 × 4200 |
| or-room    | OR-table + 2× medical cabinets side + anaesthesia cart (cabinet)     | 5000 × 5000 |
| office     | desk (1600×800) + swivel chair + file cabinet                        | 2400 × 2800 |
| reception  | reception desk (2400×800) + 2 swivels + 3-seat clinic sofa           | 3000 × 4500 |
| waiting    | 2× 3-seat clinic sofa face-to-face + round coffee table              | 3000 × 3500 |
| consult    | desk + swivel + armchair + exam table + medical cabinet              | 3500 × 4500 |

If the requested bbox is smaller than the minimum, `populate_room` returns a
warning (but still places what fits — caller decides whether to abort). The
`Warnings` array in `PopulateRoomResult` MUST be checked after every call.

## 7. Clearance budget per PN-EN 17210 / WT-2019

Presets follow (but do not enforce) these minimum clearances:

- **Bed side access** ≥ 900 mm (WT-2019 §95 ust. 3 — shipping hospital bed)
- **Wheelchair turning Ø** ≥ 1500 mm (PN-EN 17210 M.4)
- **Corridor with bed transit** ≥ 2200 mm (WT-2019 §234)
- **Office desk to wall (seated)** ≥ 800 mm (ergonomic)
- **Reception counter overhang clear depth** ≥ 900 mm (PN-EN 17210 U.6, accessibility)

These are **soft constraints inside presets**. `acad-validators`
(`check_clearance` planned for D6) will enforce them on a finished drawing and
flag violations. `populate_room` itself places furniture optimistically; it is
up to the caller to run validators after.

## 8. Scaling and rotation rules

1. `insert_*` tools accept `rotationDeg` (degrees, CCW positive) and — for the
   generic `insert_furniture` — independent `scaleX`/`scaleY`. Non-1.0 scale
   on a FURN- block distorts text/attributes: scale for architectural tweaks
   only, never to fake a different size. Use sized families instead.
2. For sized families, pick exact `widthMm` × `depthMm`; each (W, D) pair
   produces a distinct BTR named `<family>-<W>-<D>`. Reusing identical pairs
   is free (cached after first creation).
3. Orientation of `populate_room` is set via `orientation` arg: `"north"`
   (default, long axis +X), `"east"` (+90°), `"south"` (180°), `"west"`
   (270°). The whole plan rotates around the room centroid.

## 9. When to regenerate manifest

Add / rename a tool in `FurnitureTools.cs`, re-generate with

```powershell
dotnet run --project src\AcadMcp.Backend -c Release --no-build -- `
  --category furniture --regenerate-manifest
```

(see rule 30-mcpbank-manifest.md for rationale). The pre-build
`check-manifests` gate will otherwise fail.

## 10. Interaction with other categories

- **`acad-architecture`** (`draw_wall`, `draw_room`) — place furniture AFTER
  rooms are closed so `populate_room` can bbox-detect the boundary.
- **`acad-openings`** (D5) — door swing arcs live on `A-DOOR-GLAZ`; they will
  clip furniture. Run `validators.check_overlaps` (rule 42) after
  `populate_room` to flag bed-in-swing conflicts.
- **`acad-schedules`** (D8) — will pivot by `(TYPE, ROOM)` attribute;
  DO NOT leave ROOM blank on production drawings (only blank on mock-ups).
- **`acad-hatches`** — do NOT hatch inside furniture blocks. Furniture is a
  symbolic representation, hatch is a material fill; mixing the two breaks
  plot legibility (rule 62 §7).

## 11. Performance budget

| tool                          | target p50 | notes |
|-------------------------------|-----------:|-------|
| list_furniture_catalog        |    < 50 ms | pure in-memory |
| insert_* (single)             |   < 200 ms | BTR creation + BR insert |
| populate_room (≤ 5 items)     |   < 500 ms | includes lazy BTR creation |
| populate_room (10–15 items)   | < 1 500 ms | presets like `or-room` |
| list_furniture_in_model (<500)|   < 300 ms | full model-space scan |

Exceeding budget by 2× => open an issue with reproducer DWG.
