# Office — area-measurement convention

## Decision

- **Convention: net internal area (to face-of-finish)**, same as hospital and residential —
  consistent net-area convention across every typology in this bank unless a specific typology's
  own code defines area differently (none found for office).
- **Citation:** PN-ISO 9836 for the general net/gross definition; Rozp. MPiPS BHP §19 ust. 2's
  "2 m² wolnej powierzchni podłogi" is explicitly a *free* floor area (excluding furniture/
  equipment footprint) — narrower than plain net internal area. A validator rule checking a
  private-office room's boundary polygon area is checking net internal area, which is a
  reasonable but not identical proxy for BHP's "free floor" figure; don't conflate the two when
  citing compliance.

## Practical consequence for `define_room`

Same discipline as hospital/residential — inset boundary vertices by half the wall thickness
from centreline. Office interior partitions are typically similar to residential (100-150mm
lightweight) rather than hospital's 200mm default; confirm the actual thickness used per
project.

## Validator interaction — the per-employee floor is NOT directly checkable

BHP §19's core requirement (≥2 m² per employee, ≥13 m³ volume per employee) is a **per-person**
figure, not a per-room minimum. The check-primitive vocabulary
(`docs/engineering-rules/33-validators-rule-format.md` §5) checks a room's own area against a
fixed threshold — it has no way to know how many employees a room is meant to hold, so it cannot
directly enforce "≥2 m² × occupant count" without an occupant-count input the validator engine
doesn't have. This is the same class of gap noted in residential (summing multiple rooms) and
hospital (fire-zone total area) — flagged, not silently worked around.

What IS directly checkable and used in `validators/_standards/office-baseline.yaml`: a **private
office minimum area** derived from this bank's own existing `office` furniture preset (rule 64,
2400×2800mm = 6.72 m²) as a reasonable single-occupant floor, cross-checked against BHP's implied
minimum (1 person × 2 m² free + furniture clearance comfortably exceeds 6.72 m², so the existing
preset footprint is not undersized relative to BHP — it was simply never expressed as a
validator rule before this pass).

## Sourcing note

The distinction between "free floor area" (BHP) and "net internal area" (this bank's area
convention) is this session's own reasoning from the two definitions, not an independently
researched legal clarification — flagged as a judgment call, not a settled citation.
