# No YOLO changes to completed phases

Protect completed phases from drive-by refactors. Forbidden territory rules.

Once a Phase is marked complete in CHANGELOG.md and tagged in git, its existing code is **frozen** unless the user explicitly asks for change in that area.

## Forbidden without user approval

- "While I'm here, let me refactor X" - NO
- "This pattern is older, let me modernize it" - NO
- "I'll switch the logging library" - NO
- "Let me extract this into a new helper" - NO if it touches a frozen Phase
- "Renaming for consistency" - NO

## Allowed without approval

- **Adding new** files in NEW Phase work
- **Adding** new fields to DTOs (additive only, see `02-no-breaking-changes.md`)
- **Bug fixes** with regression tests - explicit and minimal
- **Adding** new tools to existing Categories (each in its own file)
- **Updating documentation** for existing features

## Procedure when refactor IS justified

1. Stop coding.
2. State the case to the user: "I see frozen Phase N has issue Z. I propose change W. Risks: ...."
3. Wait for explicit approval.
4. Once approved, treat the change as a new mini-Phase: own todo, own CHANGELOG entry under "Changed" with migration note.

## Why this rule exists

This system has 30 microservers, a plugin, COM bridge, LISP, source generator, Python sidecar, and 1000+ tools. Local-looking changes have global blast radius.

A 5-minute "improvement" to `Categories/Geometry2D/LineTools.cs` can break:
- The ToolBank manifest sync check
- The architecture test asserting no cross-category refs
- A vision test that screenshotted that exact tool's output
- An auto-design loop fixture that depended on old tool name

Stay narrow. Move forward. Refactor only when explicitly asked.
