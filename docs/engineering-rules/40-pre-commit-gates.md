# Pre-commit gate

What scripts/pre-commit.ps1 enforces, what it does NOT enforce, how to extend it.

`scripts/pre-commit.ps1` is the single fast feedback loop before commit. It MUST stay <60 s on a warm cache for the whole repo - any longer and people start `--no-verify` and we lose the gate.

## What pre-commit MUST check (Tier 1)

1. **Engineering rules well-formed** - every `docs/engineering-rules/*.md` is non-empty and starts with a heading.
2. **Manifest hygiene** - `toolbank-manifests/*.json` is valid JSON; no trailing commas; required fields present per `30-toolbank-manifest.md`.
3. **No forbidden patterns** in staged C#:
   - No raw `Marshal.GetActiveObject(` (use `MarshalCompat`).
   - No `Application.DocumentManager.MdiActiveDocument` outside `UiThreadDispatcher` (per `10-acad-ui-thread.md`).
   - No `[McpTool]` without `Intent =` (catch this BEFORE the source generator yells).
   - No `Console.WriteLine` in `AcadMcp.Backend/` (use `ILogger`; stdout is reserved for JSON-RPC frames).
4. **No secrets** - quick regex pass for `(api[_-]?key|password|token)\s*=\s*["'][A-Za-z0-9]{16,}` in staged files.
5. **CHANGELOG touched** if any non-doc file under `src/` changed (per `51-changelog.md`).
6. **Validator YAMLs load** - if any `validators/**/*.yaml` is staged (or `-All`), invoke `AcadMcp.Backend.exe --validators-self-check` and fail on any rule load error. Skipped silently when the binary isn't built (fresh clone). Enforces `33-validators-rule-format.md` and `34-validators-engine-traps.md`.

## What pre-commit MUST NOT do

- Run the full `dotnet build` (too slow). That belongs in CI / Tier 2.
- Run `dotnet test`. Same reason.
- Network calls. The hook must work offline.
- Auto-format / auto-fix. Surprising the user is worse than failing loud.

## Extending the hook

When you add a new check:
1. It MUST be deterministic and offline.
2. It MUST exit 0 / non-zero only - no prompts.
3. It MUST add <2 s to total runtime; if heavier, push it to CI / Tier 3.
4. Write a one-line failure message that names the file and the rule it violates.
5. Update this rule and `04-build-and-test-gates.md` if the gate's contract changes.

## Bypass policy

`git commit --no-verify` is forbidden except for explicit "WIP on feature branch" commits that the author will squash before merge. If you bypass, you owe the codebase a follow-up that re-asserts the gate cleanly. See `04-build-and-test-gates.md` "When to skip a gate".

## CI parity

CI runs `pwsh scripts/pre-commit.ps1` as the first job. If it diverges from the local hook, the hook is wrong - update the hook, not CI.
