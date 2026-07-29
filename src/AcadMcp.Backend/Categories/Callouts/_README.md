# AutoCAD Callouts & Symbols  (acad-callouts)

Profile callouts (K1 column / K6 elevation profile / K10 stair step), north arrows (simple/compass/ISO-129), scale bars (1:50/1:100/1:200), finish callouts (floor/wall/ceiling codes). Optional per-project for architectural detail depth.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `CalloutsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `CalloutsTools.cs` (or split per concern: `CalloutsLines.cs`, `CalloutsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "callouts"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category callouts --regenerate-manifest`
