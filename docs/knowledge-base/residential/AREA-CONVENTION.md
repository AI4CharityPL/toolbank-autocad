# Residential (multi-family apartment building) — area-measurement convention

## Decision

- **Convention: net internal area (to face-of-finish)**, same convention as hospital and the
  general Polish practice PN-ISO 9836 sets out. WT §94's "25 m² powierzchnia użytkowa" figure is
  a *usable area* (powierzchnia użytkowa) concept, which under PN-ISO 9836 / the Polish usable
  area convention is measured net, to internal wall face — not gross/centreline.
- **Citation:** PN-ISO 9836 for the net/gross definition; WT § 94 for the specific 25 m²
  minimum this convention applies to.

## Practical consequence for `define_room`

- Same discipline as `docs/knowledge-base/hospital/AREA-CONVENTION.md` — draw `define_room`'s
  boundary vertices inset from the wall centreline by half the wall thickness, not at the
  centreline, so the declared area already matches what `audit_all_rooms`'s flood-fill will
  measure.
- Residential interior partition walls are typically thinner than the hospital typology's 200mm
  default (drywall/lightweight partitions can be 100-150mm) — confirm the actual
  `draw_wall(thicknessMm=...)` value used for a given project and inset accordingly; don't
  silently reuse the hospital typology's 100mm inset assumption without checking.

## Validator interaction

Unlike hospital, this typology currently has **no per-room area validator rule** —
`residential.rooms.kitchen-min-area` is the only area-based rule in
`validators/_standards/residential-baseline.yaml`, and it checks the kitchen room's boundary
polygon the same way the hospital rules do. The apartment-total-area requirement (§94, ≥25 m²)
is NOT enforced by a validator rule in this pass — the check-primitive vocabulary
(`docs/engineering-rules/33-validators-rule-format.md` §5) has no "sum of areas across multiple
room entities" primitive, the same limitation that kept this repo from building a fire-zone-area
rule for the hospital typology. Summing an apartment's rooms and checking the total against 25 m²
would need either a new doc-level check primitive or an external script — flagged as a gap, not
silently worked around.

## Sourcing note

The net-area convention decision follows directly from WT §94's own "powierzchnia użytkowa"
terminology and PN-ISO 9836's definition of that term — not independently re-derived this
session beyond confirming the terminology matches.
