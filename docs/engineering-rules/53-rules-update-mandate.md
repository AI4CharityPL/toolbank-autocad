# Rule update mandate (meta-rule)

Meta-rule. The system learns by adding rules. After every mistake, write a rule preventing it.

This is the **most important rule** because it makes the system self-improving.

## Triggers - you MUST add or update a rule when:

1. **You broke an invariant** from any existing rule. Add a stricter rule or an enforcement mechanism.
2. **You misread folder layout** and put a file in the wrong place. Update `01-folder-layout.md` to be more explicit.
3. **You wrote a tool without `Intent` examples** and the source generator caught you. Add an example to `20-mcp-tool-attribute.md`.
4. **You made the same kind of mistake twice in a session.** Add a rule, even if narrow.
5. **You discovered a foot-gun in AutoCAD API** (e.g., method that crashes when called outside transaction). Add to relevant plugin rule.
6. **A test caught a regression** that no rule warned about. Add the rule.
7. **The user pointed out you were doing something wrong.** Always add a rule. The user shouldn't have to repeat themselves.

## Procedure

1. Identify which existing rule should have caught this. If one exists - **strengthen** it (add example, make wording sharper, add enforcement automation).
2. If no existing rule covers the situation - **create** a new `.md` file:
   - Place in `docs/engineering-rules/`
   - Number scheme: `00-09` foundation, `10-19` plugin invariants, `20-29` MCP tool authoring, `30-39` MCPBank manifest, `40-49` Vision sidecar, `50-59` workflow, `60-69` testing, `70-79` deployment, `80-89` performance, `90-99` security
   - YAML frontmatter: `description`, `globs` (optional), `alwaysApply` (boolean)
3. Add an entry under `### Added` in `CHANGELOG.md` describing the new rule and what mistake triggered it.
4. If possible, add automated enforcement (source generator check, NetArchTest, pre-commit hook entry).

## Anti-patterns

- "I'll remember not to do that" - **NO**, you won't, and neither will the next agent. Write the rule.
- "It was a small mistake, doesn't deserve a rule" - **NO**. Small repeated mistakes compound. Rules are cheap.
- Adding a rule with no example. **Bad rules are worse than no rules.** Always include a `### Bad` and `### Good` example.
- Making rules so restrictive that all valid code violates them. Calibrate to the *specific* mistake, not a hypothetical class.

## Audit (every Phase boundary)

Before declaring a Phase done:
- Review `docs/engineering-rules/`. Any rule that hasn't been referenced in the last Phase's commits = candidate for deletion or merging.
- Any pattern of bug fixes in the Phase = candidate for new rule.
- Update the README's status section.

## Example of a well-formed rule update

If during Phase 2 you accidentally call `Database.GetObjectId()` from a non-UI thread and crash AutoCAD:

1. Strengthen `10-acad-ui-thread.md` with a specific example listing `GetObjectId` among forbidden cross-thread methods.
2. Add a Roslyn analyzer in `AcadMcp.SourceGen` that detects calls to `Database.*` from non-`[UiThread]`-marked methods.
3. CHANGELOG entry: `Added: rule strengthening 10-acad-ui-thread.md + analyzer ACAD0010 catching cross-thread Database access (#123)`.

This is how the system gets harder to break over time.
