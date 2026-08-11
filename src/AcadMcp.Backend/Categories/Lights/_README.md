# AutoCAD lights  (acad-lights)

TODO: replace - one-paragraph description of the acad-lights category. Lists what kinds of operations live here, which AutoCAD APIs they wrap, and any plugin/COM constraints.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `LightsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `LightsTools.cs` (or split per concern: `LightsLines.cs`, `LightsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "lights"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category lights --regenerate-manifest`
