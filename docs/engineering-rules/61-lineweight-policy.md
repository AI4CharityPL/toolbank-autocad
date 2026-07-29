# 61. Lineweight + plot-style policy (acad-plotstyles)

Lineweight + CTB/STB plot-style policy (ISO / PN-EN ISO 128). READ BEFORE creating or editing a plot-style, calling acad-plotstyles tools, assigning colours to arch/struct/mep drawings, or authoring assets/plotstyles/*.ctb files.

Frozen contract for plotted lineweights, AutoCAD colour → millimetre tiers,
and CTB / STB installation. Paired with rule 35 (category design), rule 60
(architectural fidelity — upcoming), rule 66 (dimension chains), rule 69
(callouts / leaders), rule 70 (sections / elevations).

## §1. Scope

`acad-plotstyles` owns three composites:

1.  `ensure_ctb` — install a CTB from the repo asset folder (or caller-
    supplied `sourcePath`) into AutoCAD's Plot Styles directory.
2.  `apply_plotstyle_to_layout` — assign a named CTB/STB to a paperspace
    layout (delegates to `acad.layouts.configure_plot`).
3.  `list_plotstyles` — enumerate the CTB + STB sheets AutoCAD currently
    sees; also returns the backend asset directory so callers can pre-
    stage files for `ensure_ctb`.

Plot-style *authoring* is NOT in scope — the AutoCAD managed SDK does
not expose public APIs to write CTB binaries, so `ensure_ctb` is strictly
a file-copy + `PlotSettingsValidator.RefreshLists` operation.

## §2. Colour → lineweight tier table (mandatory)

All hospital / architectural drawings shipped from this repo MUST use the
following AutoCAD colour index → plotted mm mapping (`PlotstylesPalette.
ArchLineweightMm`):

| ACI | Colour name | Plotted lineweight | Purpose                              |
|----:|:-----------:|-------------------:|:-------------------------------------|
|  1  | RED         | 0.18 mm            | construction / hidden / axes (thin)  |
|  2  | YELLOW      | 0.25 mm            | door + window frames, fixtures       |
|  3  | GREEN       | 0.35 mm            | walls (interior thick)               |
|  4  | CYAN        | 0.50 mm            | load-bearing / section cuts (thick)  |
|  5  | BLUE        | 0.13 mm            | hidden / phantom (hair)              |
|  6  | MAGENTA     | 0.70 mm            | fire walls REI, heavy seals          |
|  7  | WHITE/BK    | 0.25 mm            | text, general (medium)               |
|  8  | DARK GREY   | 0.13 mm            | hatches (hair)                       |
|  9  | LIGHT GREY  | 0.13 mm            | secondary annotations (hair)         |

Any new CTB added under `assets/plotstyles/` MUST follow this table.
Any drawing that strays from it MUST document the deviation in the
title block notes.

## §3. Canonical CTB presets

The repo ships (optionally) three CTB templates:

- **`HOSPITAL-ISO.ctb`** — primary preset for hospital floor plans:
  implements the §2 tiers literally, all colours map to BLACK with the
  tier's lineweight, background kept white, no screening.
- **`ISO-Standard.ctb`** — general architectural preset: §2 tiers but
  keeps colours 1/2/3 slightly desaturated for coloured prints.
- **`monochrome.ctb`** — same lineweights as HOSPITAL-ISO but forces all
  colours to BLACK regardless of ACI; used for blue-line reprographics.

`PlotstylesPalette.DefaultPresets` lists these three names. The files
themselves are binary and are opt-in (not tracked in git by default).
To enable them:

1.  Place `HOSPITAL-ISO.ctb` / `ISO-Standard.ctb` / `monochrome.ctb`
    under `<repo>/assets/plotstyles/`.
2.  Call `acad-plotstyles.ensure_ctb { name: "HOSPITAL-ISO.ctb" }` — the
    composite will discover the asset, copy it to AutoCAD's Plot Styles
    folder, and verify the refresh picked it up.

## §4. Directory resolution

`acad.layouts.list_plot_styles` (plugin primitive) resolves the plot-
styles directory by probing
`%APPDATA%\Autodesk\AutoCAD *\R*\<locale>\Plot Styles`. The managed SDK
does not surface a direct path API, so the probe is best-effort:

- On hospital workstations with stock AutoCAD 202x installs the probe
  finds the directory on the first try.
- Custom enterprise profiles (roaming profiles, redirected `%APPDATA%`,
  stand-alone installers) may mask the path — `ensure_ctb` reports
  `directory=null` and emits a note when that happens.

Callers MAY override by passing `sourcePath` (which implies "I already
know where things live"); backend-side copy is idempotent under
`overwrite=false`.

## §5. Apply-to-layout contract

`apply_plotstyle_to_layout`:

1.  Calls `ensure_ctb` first when `ensure=true` (the default). This
    guarantees the requested sheet is on disk + refreshed before
    AutoCAD's `SetCurrentStyleSheet` runs.
2.  Dispatches `acad.layouts.configure_plot` with `plotStyle` set and
    `rotation=0`. Layout geometry (paper size, plotter) is not touched.
3.  Records both the `EnsureCtbResult` and the `applied` flag in the
    composite's return payload so callers can see why a refresh was
    skipped.

Set `ensure=false` only when the CTB is known to be pre-installed
(e.g. system-wide HOSPITAL-ISO on an office image).

## §6. Test coverage expectations

Unit tests should at minimum pin:

- All 3 composites declare `category="plotstyles"` and
  `RequiresPlugin=true` (the composites dispatch pipe primitives and
  perform file-I/O that shadows AutoCAD state).
- `PlotstylesPalette.DefaultPresets` contains exactly `HOSPITAL-ISO.ctb`
  + `ISO-Standard.ctb` + `monochrome.ctb`.
- `PlotstylesPalette.ArchLineweightMm` contains exactly the 9 ACI
  indices in §2 and returns the plotted mm matching the table.
- `PlotstylesPalette.AssetsDirectory()` resolves to a path ending in
  `assets\plotstyles` (walks up from the test binary via the
  `AcadMcp.Backend.csproj` marker).

E2E smoke (manual, requires AutoCAD open):

1.  `acad_call sections list_section_lines {}` and
    `acad_call plotstyles list_plotstyles {}` both succeed.
2.  Drop `HOSPITAL-ISO.ctb` under `assets/plotstyles/`, run
    `acad_call plotstyles ensure_ctb { name: "HOSPITAL-ISO.ctb" }`,
    verify `copied=true, listedAfter=true`.
3.  `acad_call plotstyles apply_plotstyle_to_layout { layoutName:
    "Layout1", plotstyle: "HOSPITAL-ISO.ctb" }` returns `applied=true`.
4.  `PLOT` / `PREVIEW` the layout — wall lines should render 0.35 mm,
    section cuts 0.50 mm, text 0.25 mm.
