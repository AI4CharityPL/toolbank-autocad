# AutoCAD Underlays  (acad-underlays)

Attach, clip and adjust DGN and DWF underlay references (UnderlayReference/DgnReference/DwfReference entities). PDF underlays and importing underlay geometry into real entities are a separate mechanism and not covered here. Layer-level visibility within an underlay and BIND are not exposed in the managed API - confirmed absent, not merely unbuilt.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `UnderlaysTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `UnderlaysTools.cs` (or split per concern: `UnderlaysLines.cs`, `UnderlaysCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "underlays"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category underlays --regenerate-manifest`
