# AutoCAD Vertical Circulation  (acad-verticals)

Stairs (straight, spiral, U/L), ramps, elevators (passenger/bed/goods), escalators, platform lifts and handrails per PN-EN 13374 and WT §54. Bed-lift minimum 160x260 with 1600 kg capacity enforced for hospitals.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `VerticalsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `VerticalsTools.cs` (or split per concern: `VerticalsLines.cs`, `VerticalsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "verticals"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category verticals --regenerate-manifest`
