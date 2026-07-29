# AutoCAD Furniture Library  (acad-furniture)

Insert and manage parametric furniture blocks for hospitals, offices and residential
interiors. Every block generates on demand (first call creates the `BlockTableRecord`,
subsequent calls insert `BlockReference`s) so the library ships **zero binary DWG
assets** — the whole catalog lives in code and travels with `AcadMcp.Plugin`.

## Tools (10)

| name                        | purpose                                                                             | read-only |
|-----------------------------|-------------------------------------------------------------------------------------|-----------|
| `list_furniture_catalog`    | enumerate built-in blocks (15 fixed + 11 sized families, hospital / office / res)   | yes       |
| `insert_furniture`          | generic insert by fully-qualified block name (e.g. `FURN-DESK-OFF-1600-800`)        |           |
| `insert_bed`                | standard / ICU / bariatric / pediatric / OR-table / labour                          |           |
| `insert_chair`              | office / armchair / stool / exam / wheelchair                                       |           |
| `insert_desk`               | office / reception / nurse-station, configurable width × depth                      |           |
| `insert_cabinet`            | storage / medical / file / wardrobe, configurable width × depth                     |           |
| `insert_sofa`               | lounge / clinical, 2 or 3 seats                                                     |           |
| `insert_table`              | rectangle / round / square / exam, configurable size                                |           |
| `populate_room`             | auto-fill a closed polyline (or bbox) with a preset: ward-room / icu-room / or-room / office / reception / waiting / consult |           |
| `list_furniture_in_model`   | enumerate all `FURN-*` BlockReferences with inv_id / type / note attributes         | yes       |

## Block naming

- **Fixed**: `FURN-<CATEGORY>-<VARIANT>` (e.g. `FURN-BED-ICU`, `FURN-CHAIR-OFF`).
- **Sized**: `FURN-<CATEGORY>-<VARIANT>-<W>-<D>` (e.g. `FURN-DESK-OFF-1600-800`).
  Each unique (W, D) pair produces a distinct cached BTR.

All blocks have **origin at the geometric centre** of the footprint and four
attribute definitions: `INV_ID` (visible), `TYPE`, `ROOM`, `NOTE` (hidden).

## Layer split (AIA-2017)

`A-FURN-BED`, `A-FURN-CHR`, `A-FURN-DSK`, `A-FURN-CBT`, `A-FURN-SFA`,
`A-FURN-TBL`, falling back to `A-FURN`. Callers may pass `layer` to override.

## Typical flows

```text
1. office
   list_furniture_catalog(domainFilter="office")        -> choose items
   insert_desk(position, width=1600, depth=800)
   insert_chair(position, type="office")
   insert_cabinet(position, width=800, depth=450, type="file")
   OR
   populate_room(roomBoundaryHandle, preset="office")

2. ICU room
   populate_room(roomBoundaryHandle, preset="icu-room")
   ... then validators.check_overlaps to catch bed-in-swing conflicts ...

3. After placement
   list_furniture_in_model(layerFilter="A-FURN-BED")    -> inventory for schedules
```

## Conventions

- All tools live in `FurnitureTools.cs`; the plugin implementation is in
  `src/AcadMcp.Plugin/Tools/FurniturePluginTools.cs`.
- Every tool has `Category = "furniture"`; the source generator validates this
  matches the folder.
- Policy, layer/attribute contract, preset clearance per PN-EN 17210 /
  WT-2019: see `.cursor/rules/64-furniture-density-per-room.mdc`.

## How to regenerate the manifest from code

```powershell
dotnet run --project src\AcadMcp.Backend -c Release --no-build -- `
  --category furniture --regenerate-manifest
```
