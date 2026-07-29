// Central tool execution helper — used by all *PluginTools that modify the drawing.
//
// ROOT CAUSE FIX (Phase 7.1): doc.LockDocument() MUST be called from the background
// thread (application context), NOT from inside a UiThreadDispatcher.Run callback.
//
// Why: UiThreadDispatcher.Run posts a callback to the AutoCAD UI thread's
// SynchronizationContext. If LockDocument() is called INSIDE that callback, AutoCAD
// EDU editions trigger a "Educational License Plot Stamp" modal dialog which tries to
// re-enter the message pump — deadlocking because we are already inside a nested
// synchronous UI-thread dispatch.
//
// Correct pattern:
//   1. Background thread: doc reference from Application.DocumentManager (thread-safe read)
//   2. Background thread: doc.LockDocument() — acquires write-access permission.
//      If AutoCAD shows the EDU modal, it fires on the UI thread while THIS background
//      thread waits. User clicks OK, lock is granted, background thread continues.
//   3. UI thread (via UiThreadDispatcher.Run): StartTransaction → work → Commit.
//   4. Background thread: docLock.Dispose() on scope exit.
//
// Rules: 10 (UI thread), 11 (transactions), 26 (API traps).

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcadMcp.Plugin.Tools;

internal static class PluginToolRunner
{
    /// <summary>
    /// Runs a tool that needs to modify the drawing. Acquires the document lock on the
    /// background thread, then executes <paramref name="work"/> on the UI thread inside
    /// a transaction. Handles all error mapping.
    /// </summary>
    public static async Task<ToolDispatchResult> RunWriteAsync(
        string toolKey,
        CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
    {
        try
        {
            // Step 1: get active document — safe from any thread.
            var doc = Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document open in AutoCAD.");

            // Step 2: acquire DocumentLock on the BACKGROUND thread.
            // If AutoCAD EDU shows the "Educational License" modal it fires normally on
            // the UI thread while this background thread simply waits — no deadlock.
            Log.Info($"[{toolKey}] acquiring DocumentLock (background thread tid={Thread.CurrentThread.ManagedThreadId})");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var docLock = doc.LockDocument();
            Log.Info($"[{toolKey}] DocumentLock acquired in {sw.ElapsedMilliseconds}ms — posting to UI thread");
            long lockMs = sw.ElapsedMilliseconds;

            // Step 3: do all DB reads/writes on the UI thread inside a transaction.
            var json = await UiThreadDispatcher.Run(() =>
            {
                Log.Info($"[{toolKey}] on UI thread (tid={Thread.CurrentThread.ManagedThreadId}) — starting transaction");
                long txStart = sw.ElapsedMilliseconds;
                using var tr = doc.TransactionManager.StartTransaction();
                var result = work(doc, doc.Database, tr);
                tr.Commit();
                Log.Info($"[{toolKey}] commit OK total={sw.ElapsedMilliseconds}ms (lock={lockMs}ms tx={sw.ElapsedMilliseconds - txStart}ms)");
                return result;
            }, ct).ConfigureAwait(false);

            // Step 4: docLock disposed by `using` above when RunWriteAsync exits.
            return new ToolDispatchResult(true, json, null);
        }
        catch (OperationCanceledException)
        {
            return new ToolDispatchResult(false, null,
                new ErrorInfo(AcadErrorCode.Timeout, $"Tool '{toolKey}' was cancelled."));
        }
        catch (Exception ex)
        {
            return AcadErrorMapper.Fail(toolKey, ex);
        }
    }

    /// <summary>
    /// Runs a read-only tool. No DocumentLock needed; just posts to the UI thread.
    /// </summary>
    public static async Task<ToolDispatchResult> RunReadAsync(
        string toolKey,
        CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
    {
        try
        {
            var doc = Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document open in AutoCAD.");

            var json = await UiThreadDispatcher.Run(() =>
            {
                using var tr = doc.TransactionManager.StartTransaction();
                var result = work(doc, doc.Database, tr);
                tr.Commit();
                return result;
            }, ct).ConfigureAwait(false);

            return new ToolDispatchResult(true, json, null);
        }
        catch (OperationCanceledException)
        {
            return new ToolDispatchResult(false, null,
                new ErrorInfo(AcadErrorCode.Timeout, $"Tool '{toolKey}' was cancelled."));
        }
        catch (Exception ex)
        {
            return AcadErrorMapper.Fail(toolKey, ex);
        }
    }
}
