# acad-electrical domain traps

Electrical-discipline domain traps — IEC vs ANSI symbol style (rectangle vs zig-zag), NO/NC contact symbols (the slash matters), wire-junction dots vs crossings, ladder-rung numbering, coil-to-contact cross-references, IEC 81346 device tags (-K / -Q / -F / -S / -M), schematic ≠ panel layout, wires connect at symbol terminals not body edges. Read BEFORE adding a tool to acad-electrical or a validator under validators/electrical/.

These are the lessons learned drawing **schematic + ladder-logic + control
panel** content the way an industrial controls / electrical office expects
to receive it. They sit on top of the universal domain rule 35.

## 1. IEC vs ANSI is a SYMBOL STYLE — pick one and document it

Resistor: **IEC** = a rectangle, **ANSI** = a zig-zag. Coil: **IEC** = an
empty rectangle with the device tag inside, **ANSI** = a circle with the
tag inside. Motor: **IEC** = a circle with `M`, **ANSI** = a circle split
into two halves with `M` in the top half. Mixing IEC + ANSI symbols on the
same sheet is a hard fail — the inspector will refuse to sign off.

We DEFAULT to **IEC** (Polish / EU convention) and expose `style: "iec"|"ansi"`
on every symbol-placing tool so an export to a US client can switch in one
place. The `electrical_health` tool reports the chosen default. Validator
`elec.symbol-style.consistent` (Phase 7) will enforce one style per drawing.

## 2. NO vs NC contacts — the SLASH is the whole story

Normally-Open (NO) contact: two short angled lines spreading apart from a
horizontal terminal line. The contact "closes" (bridges) when its
controlling coil energises.

Normally-Closed (NC) contact: same shape, but with a **horizontal slash**
across the angled lines indicating the path is normally bridged. The contact
"opens" (breaks) when its controlling coil energises.

Drawing NO when meaning NC (or vice-versa) is the #1 schematic error
because the symbols look almost identical. `place_contact_no` and
`place_contact_nc` are intentionally TWO different tools — never one tool
with a `kind` flag — so the call site reads unambiguously.

## 3. Wire JUNCTION = solid dot. Wire CROSSING = no dot

Where two wires meet at a T or + and ARE electrically connected, a small
filled circle ("junction dot") sits at the intersection. Where two wires
PASS OVER each other and are NOT connected, NO dot is drawn (and on really
strict drawings the upper wire is "broken" with a small gap).

`draw_wire_junction` MUST be a separate explicit call — agents who call
`draw_wire` twice through the same point and assume "AutoCAD will figure
it out" produce ambiguous schematics. Validator
`elec.wire.no-junction-without-dot` (Phase 7) will catch wires that share
an endpoint without a dot at it.

## 4. Ladder rungs — numbering, position, rails

A ladder diagram has two vertical "power rails" (`L1` left, `N` or `L2`
right) and horizontal "rungs" between them. Conventions:

- rungs are **numbered sequentially** at the LEFT rail (1, 2, 3, …);
- position numbers (where on the rung a contact sits) live ABOVE the rung
  near each device;
- coil sits at the RIGHT end of the rung (just before the right rail);
- contacts live to the LEFT of the coil on the same rung.

`draw_ladder_rails` creates the rails over a Y range; `draw_ladder_rung`
draws ONE horizontal line at a given Y with the rung number text on the
left rail. Rung spacing is uniform (default 30 mm in model space) — agents
who pack rungs unevenly produce a schematic that fails the QA print check.

## 5. Coil → contact cross-reference is mandatory

A coil `-K1` on rung 5 controls contacts elsewhere. The schematic MUST list
those contact locations directly UNDERNEATH the coil symbol:

```
   ( -K1 )       ← coil on rung 5
   ─────────
   12  | 14 | 18   ← contacts of K1 appear on rungs 12, 14, 18
```

`place_coil` accepts an optional `contactRungs: [12, 14, 18]` argument and
emits the cross-reference text below the coil. Agents who omit it leave
the maintenance technician hunting through the entire drawing for the
contacts of K1. Validator `elec.coil.contact-xref-required` (Phase 7).

## 6. Device tags follow IEC 81346 — pick the right prefix letter

Per IEC 81346-2 the prefix letter denotes the device function:

| prefix | function                               |
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

`place_device_tag` validates the prefix against this list AT WRITE TIME and
fails fast with a hint. Agents who invent prefixes ("-A1" for a contactor)
break the BOM extractor downstream.

## 6a. Tag prefix syntax

The dash (`-`) BEFORE the function letter is part of the IEC 81346 syntax
("product aspect"). A `+` BEFORE the dash denotes location aspect
(`+CAB1-K1` = contactor K1 inside cabinet CAB1). A `=` BEFORE the `+`
denotes function aspect (`=PWR+CAB1-K1`). Tools accept either the short
form (`-K1`) or the fully-qualified form; the parser strips at most one
of each leading aspect marker. Mixed-case prefix letters are coerced to
upper case (the standard requires upper case).

## 7. Wires connect AT TERMINALS, not at symbol body edges

Every symbol-placing tool returns a `terminals: [{name, position}]` list so
the next call (`draw_wire`) can snap to one of those points exactly. A wire
drawn to "wherever the symbol body happens to start" looks correct on
screen but fails any automated continuity / netlist extraction.

`place_resistor` returns `terminals: [{name: "1", ...}, {name: "2", ...}]`.
`place_contact_no` returns `terminals: [{name: "in", ...}, {name: "out", ...}]`.
`place_coil` returns `terminals: [{name: "A1", ...}, {name: "A2", ...}]`
(IEC coil terminal names).

## 8. Schematic ≠ panel layout

A schematic shows the LOGIC of the circuit (drawn for readability, not
physical accuracy). A panel layout shows the PHYSICAL PLACEMENT of devices
inside the cabinet (drawn to scale, top-down view of the back-plane). They
share **device tags** and **nothing else** — different layers, different
scales, often different sheets. v1 ships the schematic side. Panel-layout
tools (`place_din_rail`, `place_panel_device_outline`, `route_wireway`)
are deferred to Phase 7 with their own paired validators.

## 9. Power rails L1 / L2 / L3 / N have specific colours

Polish + IEC convention:

| rail   | wire colour      | layer        | ACI |
| ------ | ---------------- | ------------ | --- |
| L1     | brown            | E-WIRE-PWR   | 1   |
| L2     | black            | E-WIRE-PWR   | 7   |
| L3     | grey             | E-WIRE-PWR   | 8   |
| N      | blue             | E-WIRE-PWR   | 5   |
| PE     | green/yellow     | E-WIRE-PWR   | 3   |

We don't paint individual wires (CAD layers can't carry stripe colours);
we put them all on `E-WIRE-PWR` (a 0.50 mm bold layer, ACI 1) and rely on
LABELS (`L1`, `N`, `PE`) at the rail tops to disambiguate. The
`draw_ladder_rails` tool writes those labels automatically.

## 10. Symbol unit size is a per-DRAWING constant

Every symbol in one drawing shares the same "unit size" — typically 5 mm
(model space). Mixing 5 mm and 8 mm symbols on the same sheet looks
amateurish even when the geometry is technically correct. We expose
`unitSizeMm` on every symbol-placing tool and the office default is 5 mm.
`electrical_health` reports the default so the agent can read it once and
reuse it.

## 11. Layer key (the office standard we ship — IEC + JIC hybrid, 12 layers)

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

`ensure_electrical_layers` creates this key idempotently. Every electrical
draw tool calls it first.

## 12. Bundled blocks under `blocks/electrical/`

Phase-6 v1 ships geometry inline (no DWG dependency). Phase 7 ships the
full IEC + ANSI symbol library as DWG blocks plus the panel-layout tools.

Sanitise filenames before `Path.Combine` (rule 36 §12 carries over).

## 13. Cross-reference with validators

Each electrical draw tool ships with a paired validator rule:

- `place_resistor` / `place_contact_*` / `place_coil` →
  `elec.symbol.on-e-symbol-layer`;
- `draw_wire`        → `elec.wire.on-e-wire-layer`;
- `draw_ladder_rung` → `elec.rung.label-on-e-lbl-rung`;
- `place_device_tag` → `elec.tag.iec-prefix-allowed` (textual format check
  against the IEC 81346 letter set).

When you add a draw tool, add or update the matching YAML under
`validators/electrical/` (rule 35 §8).
