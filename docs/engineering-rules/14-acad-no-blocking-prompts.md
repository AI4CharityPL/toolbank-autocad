# No interactive prompts. Ever.

Tools must NEVER trigger AutoCAD's interactive prompts. Headless contract.

The agent expects every tool to either complete or fail with structured error within `timeoutMs`. AutoCAD's interactive prompts (`Editor.GetPoint`, `GetEntity`, `Prompt`, etc.) BLOCK the UI thread waiting for human input that will never come.

## Forbidden methods (incomplete list - extend as we encounter more)

- `Editor.GetPoint`, `Editor.GetEntity`, `Editor.GetSelection` (without filter or in interactive mode)
- `Editor.GetString`, `Editor.GetKeywords`, `Editor.GetCorner`
- `Editor.GetDistance`, `Editor.GetAngle`
- `Editor.PromptForFileOpenFileName`, `PromptForFileSaveFileName`
- `Application.ShowAlertDialog`, `MessageBox.Show`
- Any `OpenFileDialog`/`SaveFileDialog`
- Any modal WinForms/WPF dialog

## Allowed (silent variants)

- `Editor.SelectAll`, `Editor.SelectAllForUndo`
- `Editor.SelectCrossingWindow`, `SelectCrossingPolygon`, `SelectFence`, `SelectImplied` - all programmatic
- `Editor.SendStringToExecute(..., echoCommand: false, activateAppOnError: false, wrapUpInactiveDocument: true)` - careful, see rule 15

## Rule

Tools always take entity selection as **arguments**:

```csharp
public sealed record TrimEntitiesArgs(
    EntityHandle[] Targets,         // explicit handles
    EntityHandle[] CuttingEdges);   // explicit handles
```

NEVER:

```csharp
public sealed record TrimEntitiesArgs();
// then internally: var sel = ed.GetSelection(); // BLOCKS FOREVER
```

## What about commands that fundamentally need user input?

Don't expose them as tools. Instead, compose primitives that already have argumentized inputs (e.g., `select_by_window` + `trim_with_edges`).

If absolutely necessary (e.g., FILEOPENDIALOG is the only way for some weird file format), wrap it in a separate `acad-interactive-*` category that the user opts into knowingly, with explicit warning in the manifest.
