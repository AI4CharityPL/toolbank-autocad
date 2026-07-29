# AutoCAD Selection  (acad-selection)

TODO: replace - one-paragraph description of the acad-selection category. Lists what kinds of operations live here, which AutoCAD APIs they wrap, and any plugin/COM constraints.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `SelectionTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `SelectionTools.cs` (or split per concern: `SelectionLines.cs`, `SelectionCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "selection"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category selection --regenerate-manifest`
