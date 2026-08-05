# Build and test gates

Mandatory checks before any commit or pushing changes. Build/test/lint gates.

Before committing ANY change, ALL of the following must pass. Pre-commit hook (`scripts/pre-commit.ps1`) enforces these locally; CI re-runs them.

## Tier 1 - always

```powershell
# 1. Solution builds Release with zero warnings
dotnet build src/AcadMcp.sln -c Release -warnaserror

# 2. Unit tests green
dotnet test src/AcadMcp.sln -c Release --no-build

# 3. Engineering rules and manifests are well-formed
pwsh scripts/pre-commit.ps1
```

## Tier 2 - when touched

| Touched area                                      | Extra check                                                  |
| ------------------------------------------------- | ------------------------------------------------------------ |
| `Categories/<X>/`                                 | `pwsh scripts/check-manifests.ps1` (sync category ↔ manifest) |
| `toolbank-manifests/`                              | `pwsh scripts/check-manifests.ps1`                            |
| `src/AcadMcp.Plugin/`                             | `dotnet build` for both `net48` and `net8.0-windows` targets  |
| `src/AcadMcp.Vision/`                             | `cd src/AcadMcp.Vision && ruff check . && pytest`             |
| `docs/engineering-rules/`                                  | YAML frontmatter linter (run by pre-commit hook)              |
| Any DTO in `Shared/`                              | Read `02-no-breaking-changes.md` first; verify additive only |
| Any new tool                                      | Add fixture and E2E test per `25-mcp-tool-tests.md`          |

## Tier 3 - phase boundary

Before declaring a Phase done:

```powershell
pwsh scripts/package.ps1               # builds all launchers + bundles
pwsh scripts/register-mcps.ps1 -DryRun # validates manifests load into ToolBank
dotnet test --filter Category=ArchTest # NetArchTest invariant tests
```

## Failure handling

- **Build fail:** fix immediately. Do not commit, do not move on.
- **Test fail:** if the test was wrong (rare), fix it AND add a regression test for whatever you broke. If the code was wrong, fix the code.
- **Manifest sync fail:** the category and `toolbank-manifests/acad-<X>.json` are out of step. Update the manifest with new tools or remove deleted ones. Do not commit until in sync.
- **Architecture test fail:** you broke an invariant from `00-architecture-invariants.md`. Stop, re-read that rule, refactor.

## When to skip a gate (almost never)

If you genuinely need to push something with a known failing gate:
1. Get explicit user approval
2. Document the skip in commit message: `[skip-gate: <gate-name>] reason: <why>`
3. Open an immediate follow-up todo to fix it
4. Add to `docs/known-failures.md`

This must be exceptional. Repeating skips = treat as project-blocking incident.
