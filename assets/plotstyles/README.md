# Plot-style assets (CTB / STB)

This folder hosts **pre-authored** AutoCAD plot-style tables used by
`acad-plotstyles.ensure_ctb`. Binary CTB files are **opt-in**; drop them
here and they will be discovered automatically.

## Canonical presets (rule 61 §3)

- **`HOSPITAL-ISO.ctb`** — primary hospital preset, all colours → BLACK,
  lineweight tiers per rule 61 §2.
- **`ISO-Standard.ctb`** — general architectural, colours 1/2/3 kept
  slightly desaturated for coloured prints.
- **`monochrome.ctb`** — all colours → BLACK regardless of ACI.

## How to install

1. Either author a CTB in AutoCAD (PLOTSTYLE + STYLESMANAGER + edit table)
   and save it here under one of the canonical names, OR copy an existing
   CTB from another machine.
2. From Cursor (or any MCP client), run:
   ```
   acad_call plotstyles ensure_ctb { name: "HOSPITAL-ISO.ctb" }
   ```
3. Verify: `ensure_ctb` returns `copied=true, listedAfter=true` and the
   file is now in `%APPDATA%\Autodesk\AutoCAD 202x\R<ver>\<locale>\Plot Styles\`.

## Colour → lineweight tiers

See rule 61 §2 (`.cursor/rules/61-lineweight-policy.mdc`) and
`src/AcadMcp.Backend/Categories/Plotstyles/PlotstylesPalette.cs`
(`ArchLineweightMm`).

## Git tracking

Binary CTBs are **not** tracked in git by default — they belong to the
asset pipeline, not the source. Add an exception in `.gitignore` (or
commit with `git add -f`) if your team wants to share a preset across
workstations.
