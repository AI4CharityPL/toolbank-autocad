# AutoCAD Schedules (Tables)  (acad-schedules)

Parametric AutoCAD Table entities in paperspace: door schedule, window schedule, room schedule (number/name/area/floor/wall/ceiling finish), finish legend. Pulls data from acad-openings attributes and room labels, supports update-in-place.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `SchedulesTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `SchedulesTools.cs` (or split per concern: `SchedulesLines.cs`, `SchedulesCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "schedules"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category schedules --regenerate-manifest`
