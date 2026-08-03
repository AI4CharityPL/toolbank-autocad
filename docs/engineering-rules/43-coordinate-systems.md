# Coordinate systems — the UCS contract

**Status: decided, binding on every tool that accepts a point.**

Every tool in the bank interprets coordinates in **WCS**. That is the default and it never
changes. A tool may additionally accept an optional `ucs` argument; when present, and only
then, its point arguments are interpreted in that coordinate system and converted to WCS
before anything touches the database.

```jsonc
// unchanged, and still the norm - WCS
{ "start": { "x": 0, "y": 0 }, "end": { "x": 5000, "y": 0 } }

// same tool, points expressed in a named UCS
{ "start": { "x": 0, "y": 0 }, "end": { "x": 5000, "y": 0 }, "ucs": "WALL-EAST" }

// or in whatever UCS is current in the document
{ "start": { "x": 0, "y": 0 }, "end": { "x": 5000, "y": 0 }, "ucs": "current" }
```

## Why this shape and not the other two

**Rejected: UCS as a view-only concept.** Cheapest, fully non-breaking, and useless for the
thing UCS exists for. An agent drawing a wall on a rotated grid, or a section on an inclined
plane, would have to do the trigonometry itself on every call — which is precisely the class of
arithmetic that produces silently wrong drawings.

**Rejected: an ambient current UCS that drawing implicitly follows.** This is how AutoCAD
itself behaves, so it is tempting. It also means the same call produces different geometry
depending on what ran before it. That breaks
[rule 23 — idempotency](23-mcp-tool-idempotency.md), makes a tool call unreadable in isolation,
and makes failures unreproducible: replaying a transcript would not replay the drawing. An
agent that cannot predict what its own call will do is an agent that cannot self-correct.

`ucs: "current"` is available for callers who genuinely want the document's active UCS, but it
is **opt-in and written down in the call**, so the call still says what it does.

## Rules

1. **`ucs` is always optional. Absent means WCS.** No existing call changes meaning, ever.
2. **Conversion happens once, at the plugin boundary**, before the transaction opens. Tool
   bodies work in WCS exclusively — no tool implementation should contain a `Matrix3d`
   transform for this purpose.
3. **Results are always returned in WCS**, whatever `ucs` the caller used for input. A handle,
   a bounding box or a centroid means the same thing to every caller. Tools that want to report
   back in the caller's UCS must do so in a clearly named separate field, never by changing
   what the existing field means.
4. **An unknown UCS name is an error, never a silent fallback to WCS.** Falling back would
   place geometry in the wrong location while reporting success — the exact failure shape this
   sweep spent its time removing.
5. **`ucs` accepts:** a named UCS from the UCS table, the literal `"current"`, or the literal
   `"world"` (equivalent to omitting it).
6. **Z is honoured.** A 2D tool given a UCS whose XY plane is not the world XY plane produces
   geometry on that plane. Tools that genuinely cannot (anything that must be planar in WCS)
   must reject a non-planar UCS with a clear message rather than flattening silently.

## Rollout

The `ucs` argument is added to a tool when that tool is next touched, not in one sweeping
change across 340 DTOs. Because absence means WCS, a tool without it is not broken — it is
simply WCS-only, which is what it is today. `acad-ucs` (the category that creates and manages
the coordinate systems themselves) ships first; drawing tools gain the argument progressively.

See [COVERAGE-ROADMAP.md](../COVERAGE-ROADMAP.md) §1.2.
