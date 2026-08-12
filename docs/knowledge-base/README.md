# Knowledge base — per-typology grounding for new building projects

This directory is what `71-project-intake-protocol.md` (step 2) reads before any new
building project starts, and what it requires you to fill in when it's missing. It exists
because a from-scratch hospital build in this repo's history invented its entire room program,
structural grid, and area-measurement convention with nothing to ground them in — and the
defects that produced (systematic area mismatches, disconnected zones, a real un-walled gap in
the building envelope) were expensive to find only after ~130 rooms existed. See rule 71 for the
full incident.

## Layout

```
docs/knowledge-base/
  README.md                — this file
  _template/                — the four-file shape every typology follows; copy when adding one
    STANDARDS.md.template
    ROOM-PROGRAM.md.template
    GRID-AND-LAYERS.md.template
    AREA-CONVENTION.md.template
  <typology>/                — one folder per building typology (hospital, residential, office, ...)
    STANDARDS.md            — legal/code citations: which acts and PN-EN/ISO standards apply
    ROOM-PROGRAM.md          — typical room list, typical areas, adjacency notes
    GRID-AND-LAYERS.md       — typical structural grid + CAD layer convention(s) seen in practice
    AREA-CONVENTION.md       — net vs gross area-measurement decision for this typology
```

## Adding a new typology

1. Copy `_template/` into `<typology>/` and fill in all four files. Every legal citation needs
   an actual act/paragraph/standard number — no "per applicable code" placeholders. If you can't
   cite it, you haven't researched it yet.
2. Sourcing, in priority order:
   - **A real reference drawing the user supplied for this typology** — extract grid/program/
     layers via `acad.validators.collect_entities`, per rule 71 step 2 and the method documented
     in `docs/HOSPITAL-2026-GEOMETRY-TARGET.md`. Real geometry beats any secondary source.
   - **Existing project documents already in this repo** for that typology (e.g.
     `docs/HOSPITAL-2026-MASTERPLAN.md` for hospital) — reorganize into the four-file shape
     rather than re-researching from scratch.
   - **Web research** against authoritative sources (the actual statute text — for Poland,
     `isap.sejm.gov.pl` — not a summary blog) when neither of the above exists yet.
3. If the typology maps to new validator rules or catalog presets, add them under
   `validators/_standards/<typology>-baseline.yaml` and `validators/<discipline>/<typology>.*.yaml`
   following the exact format `docs/engineering-rules/33-validators-rule-format.md` requires —
   `validators/_standards/hospital-baseline.yaml` plus its six `validators/architectural/hospital-*.yaml`
   rule files are the direct template. New furniture/fixture presets go in
   `src/AcadMcp.Shared/Catalogs/FurnitureCatalog.cs` / `PlumbingCatalog.cs` following the pattern
   in rules 63/64.
4. Verify every new validator rule live: build one deliberate violation, confirm
   `validate_with_rule` flags it, then confirm a compliant room is NOT flagged. This is not
   optional — see rule 71's incident section for what skipping it costs.

## Existing typologies

| Typology | Status | Primary source |
|---|---|---|
| `hospital/` | Populated | `docs/HOSPITAL-2026-MASTERPLAN.md` (own prior research, reorganized here) |
| `residential/` | Populated | Web research (WT-2019) |
| `office/` | Populated | Web research (WT-2019) + hospital doc's ZL III reference point |

## What this is not

This is not a replacement for a licensed architect's or engineer's sign-off. It's a citation and
grounding layer so this tool bank's output can be checked against a specific, named requirement
instead of a vague sense of what a building "should" look like — the same honesty disclaimer
`docs/HOSPITAL-2026-MASTERPLAN.md` already carries applies here.
