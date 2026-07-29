// Marshals delegates onto the AutoCAD UI thread.
//
// AutoCAD APIs (Database, Document, Editor, Transaction) are NOT thread-safe and MUST be
// invoked from the AutoCAD main UI thread. The plugin captures that thread's
// SynchronizationContext at startup; every tool handler dispatches via Run/RunAsync.
//
// See rule 10-acad-ui-thread.md.

using System;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;

namespace AcadMcp.Plugin.Threading;

internal static class UiThreadDispatcher
{
    private static SynchronizationContext? _ui;
    private static int _uiThreadId;

    /// <summary>Captured once during IExtensionApplication.Initialize on the UI thread.</summary>
    public static void Capture()
    {
        var ctx = SynchronizationContext.Current;
        if (ctx is null)
        {
            Log.Warn("UiThreadDispatcher.Capture: SynchronizationContext.Current is null - dispatch will fall back to inline execution.");
        }
        _ui = ctx;
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        Log.Info($"UiThreadDispatcher captured ctx={ctx?.GetType().Name ?? "<null>"} threadId={_uiThreadId}");
    }

    public static bool IsOnUiThread => Thread.CurrentThread.ManagedThreadId == _uiThreadId;

    /// <summary>Run a sync delegate on the UI thread and return its result.</summary>
    public static Task<T> Run<T>(Func<T> work, CancellationToken ct = default)
    {
        if (work is null) throw new ArgumentNullException(nameof(work));
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (_ui is null || IsOnUiThread)
        {
            try { tcs.SetResult(work()); }
            catch (OperationCanceledException) { tcs.SetCanceled(); }
            catch (Exception ex) { tcs.SetException(ex); }
            return tcs.Task;
        }

        var postedAt = Environment.TickCount64;
        _ui.Post(_ =>
        {
            var enqueueDelayMs = Environment.TickCount64 - postedAt;
            if (enqueueDelayMs > 50)
                Log.Warn($"UiThreadDispatcher: post delivered after {enqueueDelayMs}ms (UI thread was busy)");
            if (ct.IsCancellationRequested) { tcs.TrySetCanceled(ct); return; }
            try { tcs.TrySetResult(work()); }
            catch (OperationCanceledException) { tcs.TrySetCanceled(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, null);

        return tcs.Task;
    }

    /// <summary>Run a sync void delegate on the UI thread.</summary>
    public static Task Run(Action work, CancellationToken ct = default)
        => Run<object?>(() => { work(); return null; }, ct);
}
