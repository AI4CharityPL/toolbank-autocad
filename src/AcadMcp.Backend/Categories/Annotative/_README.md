# AutoCAD Annotative Scaling  (acad-annotative)

Annotative objects and annotation scales: make text, dimensions, hatches and blocks carry several scale representations so one model annotates correctly at 1:50 and 1:100 without duplicating anything. Covers the drawing scale list, the current annotation scale, per-object scale representations and annotation visibility.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `AnnotativeTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `AnnotativeTools.cs` (or split per concern: `AnnotativeLines.cs`, `AnnotativeCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "annotative"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category annotative --regenerate-manifest`
