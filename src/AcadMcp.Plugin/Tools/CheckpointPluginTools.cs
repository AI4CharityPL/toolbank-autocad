// Plugin handlers for the checkpoint sub-system (Phase 7.0 MVP).
// Four tools: create / restore / list / clear.
//
// Phase 7.0 strategy:
//   - create: records an in-memory LIFO entry ONLY. No AutoCAD command is
//     issued. This avoids every UI-thread trap we hit empirically:
//       * SendStringToExecute("_.UNDO _Mark ") queues a deferred command that
//         drains only after we return from the UiThreadDispatcher callback,
//         leaving AutoCAD in "command active" state; every subsequent
//         doc.LockDocument() from the next tool call wedges the UI thread.
//       * Editor.Command("_.UNDO", "_Mark") runs synchronously, but still
//         toggles the command-active flag across pipe dispatches in a way that
//         caused layer/geometry follow-ups to deadlock after ~2 tool calls.
//     An in-memory record gives the router enough to track the boundary, and
//     the USER can undo through the normal AutoCAD undo stack or open the
//     optional file snapshot if they enable it.
//   - Optional file snapshot (opt-in via fileSnapshot:true) runs in a SEPARATE
//     UiThreadDispatcher.Run - it never overlaps with the create callback.
//   - restore is DEFERRED to Phase 7.1. Current implementation only removes
//     records from the stack (no actual UNDO) - callers should rely on the
//     file snapshot or a manual _.UNDO until 7.1 lands.
//   - list / clear: manage the in-memory stack only.
//   - restore: count how many marks lie between the stack top and the target
//     record, then issue `UNDO _Back` that many times. When even UNDO Back
//     cannot rewind (stack exhausted / different document), we return a
//     "file_fallback" outcome pointing at the saved .dwg so the router can
//     decide whether to OPEN it.
//   - list / clear: manage the in-memory stack only.
//
// IMPORTANT: AutoCAD UNDO marks are per-document. This plugin currently
// tracks a single global stack; opening a different document mid-session
// will make older records unreachable and force the file-snapshot path.
// The stack is intentionally simple - Phase 7.0 validates the loop, we'll
// promote to per-document tracking in Phase 7.1 if needed.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadMcp.Plugin.Tools;

internal static class CheckpointPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // LIFO stack: newest record at Count-1.
    private static readonly List<CheckpointRecord> _stack = new();
    private static readonly object _lock = new();

    public static void Register(ToolHost host)
    {
        host.Register("acad.checkpoint.create",  CreateCheckpoint);
        host.Register("acad.checkpoint.restore", RestoreCheckpoint);
        host.Register("acad.checkpoint.list",    ListCheckpoints);
        host.Register("acad.checkpoint.clear",   ClearCheckpoints);
    }

    private static T Read<T>(JsonObject args) =>
        JsonSerializer.Deserialize<T>(args, Opts) ?? throw new ArgumentException($"cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    // ─────────── create ───────────

    private static async Task<ToolDispatchResult> CreateCheckpoint(JsonObject args, CancellationToken ct)
    {
        try
        {
            var a = args.Count > 0 ? Read<CheckpointCreateArgsDto>(args) : new CheckpointCreateArgsDto();
            var id = a.Id;
            if (string.IsNullOrWhiteSpace(id))
                id = "ckpt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");

            // STEP 1: capture document name only. NO AutoCAD command. The
            // boundary is tracked purely in our in-memory stack - see module
            // header for why UNDO mark is deferred to Phase 7.1.
            var markOutcome = await UiThreadDispatcher.Run(() =>
            {
                var doc = AcadEnv.RequireActiveDocument();
                return new { DocName = SafeDocumentName(doc) };
            }, ct).ConfigureAwait(false);

            // STEP 2: optional file snapshot in a SEPARATE UI-thread hop so the
            // snapshot never shares a callback with a (potentially) queued command.
            string? snapshotPath = null;
            if (a.FileSnapshot == true)
            {
                snapshotPath = await UiThreadDispatcher.Run(() =>
                {
                    try
                    {
                        var doc = AcadEnv.RequireActiveDocument();
                        var dir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "AcadMcp", "checkpoints");
                        Directory.CreateDirectory(dir);
                        var path = Path.Combine(dir, id + ".dwg");
                        doc.Database.SaveAs(path, DwgVersion.Current);
                        return (string?)path;
                    }
                    catch (Exception ex)
                    {
                        // File fallback is best-effort - don't fail the whole
                        // checkpoint if disk I/O blew up. UNDO mark already registered.
                        Logging.Log.Warn($"checkpoint file snapshot failed for {id}: {ex.Message}");
                        return (string?)null;
                    }
                }, ct).ConfigureAwait(false);
            }

            var result = new CheckpointRecord(
                Id: id!,
                Label: a.Label,
                CreatedUtc: DateTime.UtcNow,
                DocumentName: markOutcome.DocName,
                SnapshotPath: snapshotPath);
            lock (_lock) { _stack.Add(result); }

            var dto = new CheckpointCreateResultDto(
                Id: result.Id,
                Label: result.Label,
                CreatedUtc: result.CreatedUtc,
                DocumentName: result.DocumentName,
                SnapshotPath: result.SnapshotPath,
                StackDepth: StackDepth());
            return new ToolDispatchResult(true, Wrap(dto), null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.checkpoint.create", ex); }
    }

    // ─────────── restore ───────────

    private static async Task<ToolDispatchResult> RestoreCheckpoint(JsonObject args, CancellationToken ct)
    {
        try
        {
            var a = Read<CheckpointRestoreArgsDto>(args);

            // Decide target record before touching the document.
            int idx;
            CheckpointRecord target;
            lock (_lock)
            {
                idx = FindIndex(a.Id, a.Label);
                if (idx < 0)
                    throw new InvalidOperationException(
                        "checkpoint not found (id or label did not match any record on the active stack).");
                target = _stack[idx];
            }

            var result = await UiThreadDispatcher.Run(() =>
            {
                var doc = AcadEnv.RequireActiveDocument();
                bool sameDoc = string.Equals(SafeDocumentName(doc), target.DocumentName, StringComparison.OrdinalIgnoreCase);

                // Drop target + newer records from the stack regardless of outcome.
                lock (_lock)
                {
                    if (idx < _stack.Count) _stack.RemoveRange(idx, _stack.Count - idx);
                }

                if (!sameDoc)
                {
                    return new CheckpointRestoreOutcome(
                        Id: target.Id,
                        Strategy: "file_fallback",
                        UndoStepsIssued: 0,
                        SnapshotPath: target.SnapshotPath,
                        Message: target.SnapshotPath is null
                            ? "active document differs from checkpoint document and no file snapshot was saved."
                            : "active document differs from checkpoint document - reopen the .dwg snapshot to restore state.");
                }

                // Phase 7.0 MVP: actual UNDO rewind is deferred to 7.1 to avoid
                // the SendStringToExecute / Editor.Command UI-thread deadlock.
                // The router will surface a deferred-restore warning to callers;
                // if the user needs a real rollback they can open the snapshot
                // (if it exists) or hit Ctrl+Z manually.
                return new CheckpointRestoreOutcome(
                    Id: target.Id,
                    Strategy: target.SnapshotPath is not null ? "file_fallback" : "deferred",
                    UndoStepsIssued: 0,
                    SnapshotPath: target.SnapshotPath,
                    Message: target.SnapshotPath is not null
                        ? "Phase 7.0 MVP: automatic UNDO rewind is deferred; reopen the .dwg snapshot for a full rollback."
                        : "Phase 7.0 MVP: automatic UNDO rewind is deferred; use Ctrl+Z in AutoCAD to roll back manually.");
            }, ct).ConfigureAwait(false);

            return new ToolDispatchResult(true, Wrap(result), null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.checkpoint.restore", ex); }
    }

    // ─────────── list ───────────

    private static Task<ToolDispatchResult> ListCheckpoints(JsonObject args, CancellationToken ct)
    {
        List<CheckpointRecord> snapshot;
        lock (_lock) { snapshot = _stack.ToList(); }
        var dto = new CheckpointListResultDto(
            snapshot.Select(r => new CheckpointSummaryDto(r.Id, r.Label, r.CreatedUtc, r.DocumentName, r.SnapshotPath is not null)).ToList(),
            snapshot.Count);
        return Task.FromResult(new ToolDispatchResult(true, Wrap(dto), null));
    }

    // ─────────── clear ───────────

    private static async Task<ToolDispatchResult> ClearCheckpoints(JsonObject args, CancellationToken ct)
    {
        try
        {
            var a = args.Count > 0 ? Read<CheckpointClearArgsDto>(args) : new CheckpointClearArgsDto();
            int removedFiles = 0;
            List<CheckpointRecord> removed;
            lock (_lock)
            {
                removed = _stack.ToList();
                _stack.Clear();
            }

            if (a.DeleteSnapshots == true)
            {
                await Task.Run(() =>
                {
                    foreach (var r in removed)
                    {
                        if (string.IsNullOrEmpty(r.SnapshotPath)) continue;
                        try { if (File.Exists(r.SnapshotPath)) { File.Delete(r.SnapshotPath); removedFiles++; } } catch { }
                    }
                }, ct).ConfigureAwait(false);
            }

            var dto = new CheckpointClearResultDto(removed.Count, removedFiles);
            return new ToolDispatchResult(true, Wrap(dto), null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail("acad.checkpoint.clear", ex); }
    }

    // ─────────── helpers ───────────

    private static int FindIndex(string? id, string? label)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (string.Equals(_stack[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i;
        }
        if (!string.IsNullOrWhiteSpace(label))
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (string.Equals(_stack[i].Label, label, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private static int StackDepth() { lock (_lock) { return _stack.Count; } }

    private static string SafeDocumentName(Document doc)
    {
        try { return doc.Name ?? "<unsaved>"; } catch { return "<unknown>"; }
    }

    private sealed record CheckpointRecord(
        string Id,
        string? Label,
        DateTime CreatedUtc,
        string DocumentName,
        string? SnapshotPath);
}

internal sealed record CheckpointCreateArgsDto(
    [property: JsonPropertyName("id")]           string? Id = null,
    [property: JsonPropertyName("label")]        string? Label = null,
    [property: JsonPropertyName("fileSnapshot")] bool? FileSnapshot = null);

internal sealed record CheckpointCreateResultDto(
    [property: JsonPropertyName("id")]           string Id,
    [property: JsonPropertyName("label")]        string? Label,
    [property: JsonPropertyName("createdUtc")]   DateTime CreatedUtc,
    [property: JsonPropertyName("documentName")] string DocumentName,
    [property: JsonPropertyName("snapshotPath")] string? SnapshotPath,
    [property: JsonPropertyName("stackDepth")]   int StackDepth);

internal sealed record CheckpointRestoreArgsDto(
    [property: JsonPropertyName("id")]    string? Id = null,
    [property: JsonPropertyName("label")] string? Label = null);

internal sealed record CheckpointRestoreOutcome(
    [property: JsonPropertyName("id")]              string Id,
    [property: JsonPropertyName("strategy")]        string Strategy, // "undo_back" | "file_fallback"
    [property: JsonPropertyName("undoStepsIssued")] int UndoStepsIssued,
    [property: JsonPropertyName("snapshotPath")]    string? SnapshotPath,
    [property: JsonPropertyName("message")]         string Message);

internal sealed record CheckpointSummaryDto(
    [property: JsonPropertyName("id")]           string Id,
    [property: JsonPropertyName("label")]        string? Label,
    [property: JsonPropertyName("createdUtc")]   DateTime CreatedUtc,
    [property: JsonPropertyName("documentName")] string DocumentName,
    [property: JsonPropertyName("hasSnapshot")]  bool HasSnapshot);

internal sealed record CheckpointListResultDto(
    [property: JsonPropertyName("checkpoints")] List<CheckpointSummaryDto> Checkpoints,
    [property: JsonPropertyName("stackDepth")]  int StackDepth);

internal sealed record CheckpointClearArgsDto(
    [property: JsonPropertyName("deleteSnapshots")] bool? DeleteSnapshots = null);

internal sealed record CheckpointClearResultDto(
    [property: JsonPropertyName("removedCheckpoints")] int RemovedCheckpoints,
    [property: JsonPropertyName("deletedSnapshotFiles")] int DeletedSnapshotFiles);
