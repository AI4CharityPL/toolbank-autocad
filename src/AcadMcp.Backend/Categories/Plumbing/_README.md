# AutoCAD Plumbing Library  (acad-plumbing)

Insert and manage parametric sanitary-fixture blocks for hospitals, offices and
residential buildings. WT-2019 + PN-EN 17210 accessibility-aware. Zero binary
DWG assets — the whole catalog lives in code and travels with `AcadMcp.Plugin`.

## Tools (9)

| name                        | purpose                                                                         | read-only |
|-----------------------------|---------------------------------------------------------------------------------|-----------|
| `list_plumbing_catalog`     | enumerate 14 catalogue entries with standard ref (PN-EN 997 / 14528 / 14688 / 13407 / 232 / 14527 + PN-EN 17210 accessibility) | yes |
| `insert_plumbing`           | generic insert by fully-qualified block name                                    |           |
| `insert_wc`                 | floor-standing / wall-hung / bidet-combo / accessible                           |           |
| `insert_basin`              | standard / double / accessible (configurable width)                             |           |
| `insert_shower`             | square / rectangle; tray or walk-in barrier-free                                |           |
| `insert_bathtub`            | standard / mini / corner (configurable size)                                    |           |
| `insert_urinal`             | standard / accessible (lower rim)                                               |           |
| `populate_bathroom`         | 6 presets: wc-public / wc-accessible / bathroom-residential / bathroom-hospital-patient / shower-room / wc-block-staff |           |
| `list_plumbing_in_model`    | enumerate `PLMB-*` BlockReferences with INV_ID / TYPE / ACCESSIBLE              | yes       |

## Block naming

- **Fixed**: `PLMB-<CATEGORY>-<VARIANT>` (e.g. `PLMB-WC-ACC`, `PLMB-BSN-STD`).
- **Sized**: `PLMB-<CATEGORY>-<VARIANT>-<W>-<D>` (e.g. `PLMB-SHW-WI-1200-900`,
  `PLMB-BSN-ACC-700-550`).

All blocks have **origin at the geometric centre** and four attributes:
`INV_ID` (visible), `TYPE`, `ACCESSIBLE`, `NOTE` (hidden, editable).

## Layer split (AIA-2017 + Polish BIP plan)

`A-PLMB-WC / -BSN / -SHW / -BT / -UR`, fallback `A-PLMB`. Callers may pass
`layer` to override.

## Typical flows

```text
1. wc accessible room
   populate_bathroom(roomBoundaryHandle, preset="wc-accessible")
   -> placed: PLMB-WC-ACC + PLMB-BSN-ACC-700-550 (room min 1500x1800)
   -> check Warnings for shortfall

2. residential bathroom
   populate_bathroom(roomBoundaryHandle, preset="bathroom-residential")
   -> WC + basin + bathtub (if w >= 1800) OR shower

3. staff WC block (office)
   populate_bathroom(roomBoundaryHandle, preset="wc-block-staff")
   -> 2x WC + 2x basin + 1x urinal
```

## Standards compliance

- **PN-EN 997** — toilets (WCs)
- **PN-EN 14528** — bidets
- **PN-EN 14688** — wash basins
- **PN-EN 232** — bathtubs
- **PN-EN 14527** — shower trays
- **PN-EN 13407** — urinals
- **PN-EN 17210** — accessibility (annex T / U / S / M for WC, basin, shower, manoeuvring)
- **WT-2019 §73 / §79 / §82 / §83 / §86 / §87** — Polish technical conditions

See `.cursor/rules/63-sanitary-fixtures-wt.mdc` for the exact clearance
budget and preset footprint requirements.

## How to regenerate the manifest from code

```powershell
dotnet run --project src\AcadMcp.Backend -c Release --no-build -- `
  --category plumbing --regenerate-manifest
```
