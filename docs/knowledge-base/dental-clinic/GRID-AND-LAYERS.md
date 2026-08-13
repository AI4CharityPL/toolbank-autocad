# Dental clinic (gabinet stomatologiczny) — structural grid & layer conventions

## Typical structural grid

A dental clinic is normally a **fit-out inside an existing building shell** (ground-floor
commercial unit, or a floor within an office/residential building), not a purpose-built structure
with its own dedicated large-span grid — much closer to the residential typology's situation than
to the hospital or office typologies, which both assume a dedicated new structure.

- Typical bay: **~3.0-6.0 m** — the same order-of-magnitude default
  `docs/knowledge-base/residential/GRID-AND-LAYERS.md` already uses for the same reason (no large
  clear-span driver like an OR or an open office floor plate; room depths are small and interior
  partitions, not the structural grid, do the actual space-dividing). **No specific project drove
  this figure this session; treat as a placeholder** until a real dental-clinic reference drawing
  is available to derive it from (rule 71 step 2), same caveat residential's own file carries.
- Because the clinic is typically a fit-out, the structural grid itself is often **inherited from
  the host building** (whatever shell it's built into) rather than designed for the clinic — the
  3.0-6.0 m figure above is only relevant when this typology is used to plan a ground-up build.

## This tool bank's own CAD layer key (unchanged, do not fork per typology)

Same AIA-style key as every other typology in this bank (`A-WALL`, `A-ROOM-BNDY`, `A-ROOM-IDEN`,
`A-DOOR`, ...) — rule 02, no breaking changes.

**The gabinet RTG's shielded wall reuses `A-WALL-LEAD` — the same layer the hospital typology
already defined for its own CT/RTG lead shielding.** See
`docs/knowledge-base/hospital/GRID-AND-LAYERS.md` ("Hospital-specific extensions... `A-WALL-LEAD`
(radiation shielding)... see `hospital.walls.lead-shield-on-layer`... in
`validators/architectural/`") for where this layer was first defined. **No new layer name is
invented for the dental clinic's own RTG room** — the shielding concept (a wall that must carry a
lead-equivalent barrier, flagged so a validator can check it) is typology-agnostic, and this bank
already has exactly one layer for it. A future `dental-clinic.walls.lead-shield-on-layer`
validator rule (were one added, alongside a `validators/_standards/dental-clinic-baseline.yaml`
that doesn't exist yet — see AREA-CONVENTION.md) should check `A-WALL-LEAD` the same way
`hospital.walls.lead-shield-on-layer` does, not a new layer.

No dental-clinic-specific layer extension beyond that reuse was found necessary — the typology has
only one shielded-wall case (the point RTG room), unlike hospital's two (`A-WALL-LEAD` for
CT/RTG, `A-WALL-FARA` for MRI's Faraday cage) — a dental clinic has no MRI-equivalent requirement.

## Reading a REAL dental-clinic reference drawing (if supplied)

No real dental-clinic drawing has been supplied to this repo yet. When one is: extract grid,
layer convention, and room program the same way
`docs/knowledge-base/hospital/GRID-AND-LAYERS.md` did for the real outpatient-clinic drawing —
via `acad.layers.list_layers` + `acad.annotations.list_text_by_pattern` — and record the
cross-reference table here. Given the fit-out nature of this typology, pay particular attention
to whether the real drawing's layer key distinguishes the RTG room's shielded wall from ordinary
partitions the way this bank's `A-WALL-LEAD` does, or bundles it into a generic wall layer with
only an annotation/hatch calling out the lead lining — that distinction wasn't testable this
session without a real drawing.

## Sourcing note

Grid spacing is a placeholder reasoned by analogy to the residential typology's own
similarly-unverified figure, not derived from a real reference drawing or a cited structural
requirement — flagged explicitly, same discipline residential's own file uses, rather than
presented with false confidence. The `A-WALL-LEAD` reuse, by contrast, is a direct citation to an
existing, already-defined layer in this bank (`docs/knowledge-base/hospital/GRID-AND-LAYERS.md`),
not a placeholder.
