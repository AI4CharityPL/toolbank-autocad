# AutoCAD External References  (acad-xrefs)

Attach, manage, clip and bind external references (XREFs): the mechanism every multi-file project is built on. Covers attach/overlay, reload/unload, bind and insert-bind, path repair, rectangular/polygonal/object clipping, nested xref inspection and per-xref layer overrides.

## Planned tools

> Replace this list with real entries as you implement them. Each item becomes one `[McpTool]` static method in `XrefsTools.cs`.

- [ ] TODO_first_tool   - one-line purpose
- [ ] TODO_second_tool  - one-line purpose
- [ ] TODO_third_tool   - one-line purpose

## Conventions for this category

- All tools live in `XrefsTools.cs` (or split per concern: `XrefsLines.cs`, `XrefsCurves.cs`, ...)
- Every tool MUST follow rules 20-25 (`[McpTool]` shape, naming, args/results, idempotency, category binding, tests)
- `Category = "xrefs"` on every tool; the source generator validates this matches the folder

## How to regenerate the manifest from code

`dotnet run --project src/AcadMcp.Backend -- --category xrefs --regenerate-manifest`
