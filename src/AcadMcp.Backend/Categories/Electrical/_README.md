# AutoCAD Electrical (schematic + ladder)  (`acad-electrical`)

High-level electrical-schematic + ladder-logic drafting in 11 tools.
Composes primitives from `acad-geometry-2d`, `acad-layers`, and
`acad-annotations` per rule 35 §2 (no duplicate plugin handlers).

Read **rule 39 (`.cursor/rules/39-electrical-domain-traps.mdc`)** before
adding a tool here. It codifies the IEC/ANSI symbol-style choice, the
NO-vs-NC slash rule, junction-dot semantics, ladder rail+rung numbering,
the coil→contact cross-reference convention, and the IEC 81346 device-tag
prefix lookup.

## Tools (12)

| tool                       | purpose                                                                              |
| -------------------------- | ------------------------------------------------------------------------------------ |
| `ensure_electrical_layers` | idempotently create the 12-layer E-* key with colour + linetype + lineweight         |
| `draw_ladder_rails`        | two vertical L1/N rails with labelled tops on `E-WIRE-PWR`                           |
| `draw_ladder_rung`         | one horizontal rung between rails + sequential rung-number on the LEFT rail          |
| `draw_wire`                | poly-line wire routed to `E-WIRE` / `E-WIRE-PWR` / `E-WIRE-CTRL` by `kind` flag      |
| `draw_wire_junction`       | filled junction dot at a wire intersection (rule 39 §3)                              |
| `place_resistor`           | IEC rectangle or ANSI zig-zag with named terminals `1` / `2`                         |
| `place_contact_no`         | NO contact (NO slash) with terminals `in` / `out`                                    |
| `place_contact_nc`         | NC contact (slash present) with terminals `in` / `out`                               |
| `place_coil`               | IEC rectangle or ANSI circle, optional `tag` + `contactRungs` xref (rule 39 §5)      |
| `place_terminal_block`     | row of N numbered terminals on `E-TERM` with labels on `E-LBL-WIRE`                  |
| `place_device_tag`         | IEC 81346 tag text, prefix validated against `-K/-Q/-F/-S/-B/-M/-T/-G/-X/-W/-H`      |
| `electrical_health`        | read-only metadata: layer key, IEC prefix table, supported styles, planned blocks    |

## Layer key (rule 39 §11)

| layer            | colour | linetype     | weight | content                              |
| ---------------- | ------ | ------------ | ------ | ------------------------------------ |
| `E-WIRE`         | 7      | Continuous   | 0.30   | signal / control wires               |
| `E-WIRE-PWR`     | 1      | Continuous   | 0.50   | power rails L1/L2/L3/N/PE            |
| `E-WIRE-CTRL`    | 4      | Continuous   | 0.25   | low-voltage control wires            |
| `E-SYMBOL`       | 7      | Continuous   | 0.30   | symbol bodies                        |
| `E-TERM`         | 6      | Continuous   | 0.40   | terminal blocks                      |
| `E-LBL-WIRE`     | 2      | Continuous   | 0.18   | wire numbers                         |
| `E-LBL-DEV`      | 2      | Continuous   | 0.18   | device tags (-K1 / -Q1 / -F1)        |
| `E-LBL-RUNG`     | 2      | Continuous   | 0.25   | rung numbers (left rail)             |
| `E-XREF`         | 8      | Continuous   | 0.18   | coil↔contact cross-references        |
| `E-TITLE`        | 7      | Continuous   | 0.50   | title block geometry                 |
| `E-PANEL`        | 7      | Continuous   | 0.50   | panel-layout outlines (Phase 7)      |
| `E-NOTE`         | 2      | Continuous   | 0.18   | schematic notes                      |

## IEC 81346 device-tag prefix letters (rule 39 §6)

| prefix | meaning                                |
| ------ | -------------------------------------- |
| `-K`   | electromechanical relay / contactor    |
| `-Q`   | switch, circuit breaker, motor starter |
| `-F`   | fuse, protective device                |
| `-S`   | manual control (switch, push-button)   |
| `-B`   | sensor (transducer)                    |
| `-M`   | motor                                  |
| `-T`   | transformer                            |
| `-G`   | generator, supply                      |
| `-X`   | terminal block                         |
| `-W`   | wire / cable                           |
| `-H`   | indicator / lamp                       |

`place_device_tag` accepts `K1` / `-K1` / `+CAB1-K1` / `=PWR+CAB1-K1` and
returns the canonical `=FUNC+LOC-PREFIXSEQ` form.

## v1 limitations (consciously deferred)

- Schematic side only — panel-layout tools (`place_din_rail`,
  `place_panel_device_outline`, `route_wireway`) ship in Phase 7 with their
  own paired validators.
- Cross-reference auto-tracking is manual: the agent passes `contactRungs`
  explicitly to `place_coil`. Phase 7 ships an extractor that walks all
  contacts of a tag and back-fills the coil xref.
- Bundled blocks under `blocks/electrical/` (RES_*, COIL_*, CONTACT_*,
  MOTOR_*, FUSE_*, LAMP_*, TRANSFORMER_*, JUNCTION_DOT,
  TERMINAL_BLOCK_*WAY) ship in Phase 7; v1 tools synthesise geometry inline.
- Wire-numbering schemes (sequential per IEC 60204 vs rung-position per
  JIC NFPA 79) are the agent's responsibility — wire-number labels are
  placed via `acad-annotations.add_dbtext` on layer `E-LBL-WIRE`.

## Paired validators (`validators/electrical/`)

- `elec.symbol.on-e-symbol-layer`     — symbol bodies → `E-SYMBOL`
- `elec.wire.on-e-wire-layer`         — signal wires → `E-WIRE`
- `elec.wire.power-on-e-wire-pwr`     — power rails → `E-WIRE-PWR`
- `elec.rung.label-on-e-lbl-rung`     — rung-number labels → `E-LBL-RUNG`
- `elec.tag.device-on-e-lbl-dev`      — device tags → `E-LBL-DEV`

Bundled into the `electrical-baseline` standard
(`validators/_standards/electrical-baseline.yaml`) along with the three
general ISO hygiene rules.

## Regenerate the manifest after editing tools

```powershell
src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe `
    --category electrical --regenerate-manifest
```
