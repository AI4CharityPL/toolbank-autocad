# Dental clinic (gabinet stomatologiczny) — area-measurement convention

## Decision

- **Convention: net internal area (to face-of-finish)**, same convention already established for
  hospital and residential in this bank. Nothing found in Rozp. MZ 2019 or Rozp. MZ RTG 2006
  contradicts this — where either regulation gives a numeric floor (the RTG room's **≥8 m² / +4
  m²** figures are the only firmly-cited numbers this typology has, see `STANDARDS.md`), it is
  describing usable room floor area in the same "powierzchnia" sense PN-ISO 9836 and this bank's
  other typologies already treat as net.
- **Citation:** PN-ISO 9836 for the general net/gross definition (reused from
  `docs/knowledge-base/residential/AREA-CONVENTION.md` and
  `docs/knowledge-base/hospital/AREA-CONVENTION.md`); Rozp. MZ RTG 2006 for the RTG-room-specific
  8 m² figure this convention applies to.
- **Gap, recorded honestly:** neither Rozp. MZ 2019 §16 nor Rozp. MZ RTG 2006, as summarized by
  the secondary sources available this session, explicitly states "netto" or "brutto" next to the
  RTG room's 8 m² figure the way the hospital masterplan explicitly labels its own room minimums
  "netto." This file *assumes* net, consistent with this bank's established convention and with
  PN-ISO 9836 being the general Polish default, but that assumption was not independently
  confirmed against primary text for this typology specifically — flagged the same way
  `docs/knowledge-base/residential/AREA-CONVENTION.md` flags its own sourcing gaps.

## Practical consequence for `define_room`

- Typical wall thickness in this typology's construction: **100-150 mm** for interior partitions
  (drywall/lightweight — a dental clinic is normally a build-out inside an existing shell, not new
  structural construction; same figure `docs/knowledge-base/residential/AREA-CONVENTION.md`
  already uses for its own lightweight interior partitions, reused here rather than re-derived).
  The **gabinet RTG's shielded wall is thicker** — lead-lined partition assemblies commonly run
  **150-200 mm** once the lead layer and its board buildup are included, but this bank has no
  single confirmed default thickness for it (see `STANDARDS.md`'s note that the shielding itself
  is a project-specific calculated value, not a universal figure) — confirm the actual
  `draw_wall(thicknessMm=...)` value used for a given project's RTG room rather than assuming.
- `define_room`'s boundary polygon vertices must be drawn **inset by half the wall thickness from
  the wall centreline on every side** — same discipline as every other typology in this bank
  (hospital, residential). For a 120 mm typical interior partition, that's a 60 mm inset; for the
  RTG room's thicker shielded wall, inset by half whatever thickness that project actually used,
  not the 100 mm interior-partition assumption.
- Any validator rule checking a minimum area for this typology (were one to be added to
  `validators/_standards/dental-clinic-baseline.yaml` — none exists yet, see Sourcing note) would
  be checking the boundary polygon's own area, not the flood-fill measurement — get the polygon
  right at construction time, per the hospital typology's own hard-learned lesson
  (`docs/knowledge-base/hospital/AREA-CONVENTION.md`).

## Validator interaction

No `validators/_standards/dental-clinic-baseline.yaml` exists in this repo yet — this typology has
not had any validator rules built for it. If one is added, the RTG room's **≥8 m² / +4 m² per
additional apparatus** figure (Rozp. MZ RTG 2006, **Confirmed** in `STANDARDS.md`) is the only
number in this typology's program with a firm-enough citation to be worth encoding as an
`area_at_least` check; the gabinet-zabiegowy and sterylizacja area figures are industry convention
(**Probable**, not a code minimum) and should not become a hard validator failure the way a
Confirmed figure would — flagging this distinction now so a future rule-authoring pass doesn't
quietly promote a "Probable" convention into a hard requirement.

## Sourcing note

The net-area convention decision follows this bank's existing hospital/residential precedent and
PN-ISO 9836's general definition — not independently re-derived for the dental-clinic typology's
own source regulations this session, since neither explicitly labels its area figures "netto" or
"brutto." That gap is recorded above rather than papered over.
