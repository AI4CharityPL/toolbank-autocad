# AutoCAD plugin: UI thread is sacred

Plugin UI thread invariants. Read before touching anything in src/AcadMcp.Plugin/.

AutoCAD's `Database`, `Editor`, `DocumentManager`, and almost every type in `Autodesk.AutoCAD.*` is **NOT thread-safe**. Touching them from a non-UI thread = silent corruption or instant crash.

## Rule

All AutoCAD API calls in `AcadMcp.Plugin` MUST go through `UiThreadDispatcher.RunAsync(...)` (or `.Run<T>(...)`). The dispatcher posts work to the AutoCAD main thread and awaits completion via a `TaskCompletionSource`.

The pipe server runs on a worker thread. It NEVER calls AutoCAD APIs directly. It only enqueues `IUiWorkItem` instances onto the dispatcher.

## Bad

```csharp
// Pipe handler running on background thread - BOOM
public ToolResponse HandleDrawCircle(ToolRequest req)
{
    var doc = Application.DocumentManager.MdiActiveDocument; // crash on non-UI thread
    using var tx = doc.Database.TransactionManager.StartTransaction();
    // ...
}
```

## Good

```csharp
public Task<ToolResponse> HandleDrawCircleAsync(ToolRequest req, CancellationToken ct)
    => UiThread.RunAsync(() =>
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null) return Failure(AcadErrorCode.NoActiveDocument);
        using var docLock = doc.LockDocument();
        using var tx = doc.Database.TransactionManager.StartTransaction();
        // ...do work...
        tx.Commit();
        return Success(...);
    }, ct);
```

## Forbidden methods on background threads (incomplete list - grows over time)

- `Application.DocumentManager.*`
- `Database.*` (any property, any method)
- `Editor.*`
- `TransactionManager.*`, `Transaction.*`
- `BlockTableRecord.*`, `SymbolTable.*`
- `Document.LockDocument()` (only valid on UI thread)
- `ObjectId.GetObject()` (returns DBObject which is bound to its Database's thread)

## Enforcement (planned)

A Roslyn analyzer `ACAD0010` (Phase 1) will flag any access to `Autodesk.AutoCAD.*` namespace types from methods not marked `[UiThread]` or not inside a `UiThread.RunAsync` lambda.

## Why

This crash is impossible to debug from the agent's logs. The plugin just disappears, AutoCAD freezes for 30 seconds, then the next user action eats all unsaved work. ALWAYS use the dispatcher.
