<!--
Keep the sections that apply and delete the rest. A one-line typo fix does not need a
verification section; a new tool does.
-->

## What this changes

<!-- One paragraph. What a reader of the changelog needs, not a restatement of the diff. -->

## Why

<!-- The problem. If this fixes a defect, say what was wrong, not just what is now right. -->

Closes #

---

## Verification

**CI cannot verify the AutoCAD plugin.** Runners have no AutoCAD, so
`AcadMcp.Plugin` and `Companion.Host` are excluded from the build. If this PR touches
either, a green check means nothing about it — say here what you ran against a live
AutoCAD, and paste the result.

- [ ] `pwsh scripts/pre-commit.ps1 -All` passes locally
- [ ] Touches the plugin — verified against live AutoCAD (version + language: ______ )
- [ ] Touches only the server / docs / tooling — CI coverage is sufficient

<!--
For a new or changed tool, paste the actual call and result. And if the change is visual,
attach a screenshot: three of the worst defects in this repository returned a completely
healthy JSON result while drawing the wrong thing. The return code is not the evidence.
-->

```
```

## New or changed tools

<!-- Delete if none. -->

- [ ] Every `[McpTool]` carries `Intent =` with both English and Polish phrasings
- [ ] The MCPBank manifest in `mcpbank-manifests/` was updated to match
- [ ] Argument names in the args record match the `[JsonPropertyName]` actually bound
- [ ] The discovery catalogue and the action tool agree on parameter shape
      <!-- Four defects in one sweep were a catalogue advertising what the tool refused. -->
- [ ] Failure is an error, not a silent fallback to a plausible-looking default

## Checklist

- [ ] `CHANGELOG.md` updated (required by the gate when `src/` changes)
- [ ] No `catch { }` swallowing a signal — two long-lived defects survived exactly that way
- [ ] Behaviour that surprised you is recorded, in `docs/engineering-rules/` if it
      generalises or `docs/KNOWN-GAPS.md` if it does not
