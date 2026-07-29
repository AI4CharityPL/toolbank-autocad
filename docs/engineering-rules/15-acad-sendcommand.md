# SendStringToExecute is a LAST RESORT

Discipline for SendStringToExecute / SendCommand fallback. Last resort.

`Editor.SendStringToExecute` and command-line scripting (LISP `command`, `.scr`) are AutoCAD's escape hatch for things the .NET API doesn't expose cleanly. They are **inherently fragile**:

- Locale-dependent (`_LINE` vs `_-LINE` vs `LINE` vs Polish `LINIA`)
- Non-transactional (no rollback if half completes)
- Asynchronous in surprising ways (returns before the command finishes)
- Affected by `CMDECHO`, `EXPERT`, dialog-suppression sysvars
- Easy to break by leaving the active command in unexpected state

## Order of preference

1. **First:** look for a `Database`/`Editor`/managed-API method that does the same thing
2. **Second:** look for a COM Automation method (only if `RequiresPlugin = false`)
3. **Third:** SendStringToExecute - only when 1 and 2 are confirmed absent
4. Document WHY 1 and 2 weren't enough in a `// SendCommand justification: ...` comment

## Required pattern when using SendStringToExecute

```csharp
// SendCommand justification: AutoCAD .NET API has no DIM EDIT REPOSITION equivalent (verified ARX 2025).
const string CmdEcho_Off = "_-CMDECHO 0 ";
const string CmdEcho_On  = "_-CMDECHO 1 ";

ed.SendStringToExecute(
    CmdEcho_Off
        + "_DIMOVERRIDE _DIMTOH 1 _DIMTIH 0   "  // double-space terminates each prompt
        + CmdEcho_On,
    activateAppOnError: false,
    wrapUpInactiveDocument: true,
    echoCommand: false);
```

## Hard rules

1. ALWAYS prefix command names with `_` (locale-independent token) AND `-` (suppress dialog version): `_-LINE`, `_-LAYER`, `_-INSERT`.
2. ALWAYS save and restore `CMDECHO`, `FILEDIA`, `EXPERT`, `OSMODE` sysvars if you change them.
3. ALWAYS terminate each prompt explicitly. SPACE = enter. Use double-space at the end to discharge command.
4. NEVER pipe untrusted strings (e.g. user-provided filenames) into a SendStringToExecute. Quote and validate.
5. After SendStringToExecute, you do NOT have a transaction handle on the new entity. To get one, follow up with a query (`SelectImplied` for "last").

## LISP variant

For complex sequences, write a `.lsp` in `src/AcadMcp.Lisp/Scripts/` and load it via `LispScriptLibrary.Resolve(name)` + plugin's `Load` helper. Keep `.lsp` files small, single-purpose, and named after the tool that uses them.

## Audit

Every SendStringToExecute call MUST be listed in `docs/sendcommand-audit.md` with date, tool name, and justification. Pre-commit hook diff-checks new SendStringToExecute calls and warns if absent from audit log.
