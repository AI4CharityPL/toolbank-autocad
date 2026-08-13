# Automotive showroom — area-measurement convention

## Decision

- **Convention: net internal area (to face-of-finish)**, same convention as every other typology
  in this bank (hospital, residential, office). No showroom-specific WT provision was found that
  defines area differently for this typology (see STANDARDS.md — the "Confirmed absent" rows for
  showroom floor-area/glazing minimums mean there is no typology-specific area rule to override
  the general convention with).
- **Citation:** PN-ISO 9836 for the general net/gross definition — same citation residential and
  office already use; not independently re-derived this session beyond confirming no showroom-
  specific override exists.

## Practical consequence for `define_room`

- Same discipline as every other typology — inset `define_room`'s boundary-polygon vertices by
  half the wall thickness from the wall centreline, not at the centreline, so the declared area
  already matches what `audit_all_rooms`'s flood-fill will measure.
- **Wall construction for this typology is a mix of two very different assemblies, unlike
  residential's uniform lightweight partitions — pick the right thickness per wall, not one
  default for the whole plan:**
  - **Exhibition hall perimeter / envelope walls:** a large-span steel-portal-frame hall
    (see GRID-AND-LAYERS.md) typically uses **insulated sandwich panel (płyta warstwowa)
    cladding, ~120-150mm**, hung off the primary steel structure — thinner and lighter than a
    masonry perimeter wall, but still an opaque, insetable wall for `define_room` purposes.
    **Probable** — general steel-hall construction practice found via web research
    (commercecon.pl, pol-met.com), not independently verified against a specific product spec or
    a real showroom drawing.
  - **Glazed street-facing storefront (curtain wall):** the hall's public frontage is typically a
    glass curtain-wall system on an aluminum mullion frame, not a conventional wall — its
    structural sightline depth is much thinner than either the sandwich-panel envelope or a
    residential/office partition (commonly on the order of 50-100mm at the mullion, though this
    varies by system and was not independently verified against a specific manufacturer spec).
    **Probable.** When drawing this as a `define_room` boundary edge, treat it as its own distinct
    wall type rather than reusing the opaque-envelope inset — see GRID-AND-LAYERS.md's note on
    optionally distinguishing it on-layer.
  - **Interior partitions** (sales offices, back-office, WCs): likely lightweight drywall,
    **100-150mm**, same order of magnitude as residential's and office's interior partitions —
    reuse that assumption rather than the hall envelope's thicker figure. **Probable** — not
    independently re-derived this session, inherited from the same reasoning residential/office
    already applied to their own interior partitions.
  - Confirm the actual `draw_wall(thicknessMm=...)` value used for a given project per wall type
    before insetting — don't silently reuse one default across all three assemblies.

## Validator interaction

No area-based validator rule exists yet for this typology (no `validators/_standards/
automotive-showroom-baseline.yaml` file has been created as part of this documentation pass — this
is a pure knowledge-base research task, not a validator-authoring one). Given STANDARDS.md's
"Confirmed absent" findings for showroom floor-area minimums, there is currently no WT/BHP number
to check a hall or room's area against beyond the reused `office` preset minimum for sales/back-
office rooms and the reused rule-63 WC presets — same category of gap already flagged in
residential (apartment-total-area) and office (per-employee floor) for figures that exist but
aren't directly checkable by the current check-primitive vocabulary.

## Sourcing note

The net-area decision itself follows directly from this bank's existing cross-typology convention
(no typology-specific override found). The wall-thickness figures are web-research industry
defaults, not derived from a real showroom reference drawing or a manufacturer spec sheet — flagged
as "Probable" throughout rather than presented as settled, consistent with GRID-AND-LAYERS.md's
same caveat for the structural grid figures.
