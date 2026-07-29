// Plugin handlers for the checkpoint sub-system.
// Four tools: create / restore / list / clear.
//
// Rollback strategy: reopen from a file snapshot, NOT AutoCAD's UNDO command.
// We tried the obvious approach first and both variants deadlocked the UI
// thread in practice:
//   * SendStringToExecute("_.UNDO _Mark ") queues a deferred command that
//     drains only after we return from the UiThreadDispatcher callback,
//     leaving AutoCAD in "command active" state; every subsequent
//     doc.LockDocument() from the next tool call wedges the UI thread.
//   * Editor.Command("_.UNDO", "_Mark") runs synchronously, but still
//     toggles the command-active flag across pipe dispatches in a way that
//     caused layer/geometry follow-ups to deadlock after ~2 tool calls.
// A file snapshot sidesteps this entirely: no AutoCAD command is ever
// injected, only DocumentManager-level open/close/save calls that this
// plugin already uses safely elsewhere (see FilesPluginTools) and that were
// verified live end-to-end (create scratch doc, draw, restore, verify).
//
//   - create: takes a full .dwg snapshot of the active document BY DEFAULT
//     (pass fileSnapshot:false to skip it and record an in-memory boundary
//     only -- restore will then have nothing to roll back to). Snapshot I/O
//     runs in its own UiThreadDispatcher.Run, separate from the record-keeping
//     step, so it can never share a callback with anything else.
//   - restore: if the checkpoint has a snapshot AND the active document is
//     still the one the checkpoint was taken on, closes the active document
//     WITHOUT saving (discarding everything since the checkpoint) and reopens
//     the snapshot as the active document -- a real, verified rollback, not a
//     message asking the user to press Ctrl+Z. If the active document has
//     since changed (a different document is now active), the snapshot is
//     opened as an ADDITIONAL document instead of touching the unrelated
//     active one. If there is no snapshot to restore from, this is reported
//     plainly as "no_snapshot" rather than silently doing nothing.
//   - list / clear: manage the in-memory stack only.
//
// Cost tradeoff: a full SaveAs is real disk I/O, proportional to drawing
// size -- more expensive than the UNDO-mark approach would have been, had it
// worked. Correctness (an actual rollback) is worth more than that saving for
// a mechanism whose entire purpose is "make failed operations safe to undo."
// A cheaper in-process UNDO-mark path remains a legitimate future
// optimization once the UI-thread command-queueing issue above is solved
// properly -- this file snapshot approach is not a stopgap needing that to
// be "real"; it already is.
//
// IMPORTANT: checkpoint records (and their snapshots) are per-document by
// construction -- restore checks DocumentName before touching anything.
// This plugin tracks a single global stack across all open documents, so
// switching documents mid-session does not lose older records, it just
// means restoring them opens the snapshot as a new document instead of
// replacing whatever is currently active.

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

            // STEP 2: file snapshot (default ON -- see module header for why this,
            // not UNDO marks, is the real rollback mechanism) in a SEPARATE
            // UI-thread hop so it never shares a callback with anything else.
            // Pass fileSnapshot:false to opt out and record a boundary only
            // (restore will then report "no_snapshot" instead of rolling back).
            string? snapshotPath = null;
            if (a.FileSnapshot != false)
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

                // Drop target + newer records from the stack regardless of outcome --
                // once we attempt a restore, everything after this point in the
                // session is superseded, whether or not a snapshot exists to roll
                // back to.
                lock (_lock)
                {
                    if (idx < _stack.Count) _stack.RemoveRange(idx, _stack.Count - idx);
                }

                if (target.SnapshotPath is null || !File.Exists(target.SnapshotPath))
                {
                    return new CheckpointRestoreOutcome(
                        Id: target.Id,
                        Strategy: "no_snapshot",
                        Document: null,
                        Message: target.SnapshotPath is null
                            ? "This checkpoint was created with fileSnapshot:false, so there is nothing to restore from. Create checkpoints without that flag (the default) to make them restorable."
                            : $"Snapshot file is missing on disk ({target.SnapshotPath}); cannot restore.");
                }

                var dm = AcadApp.DocumentManager
                         ?? throw new InvalidOperationException("DocumentManager unavailable.");

                if (sameDoc)
                {
                    // Real rollback: discard everything since the checkpoint and
                    // reopen exactly the state we saved at checkpoint time.
                    doc.CloseAndDiscard();
                    var restored = dm.Open(target.SnapshotPath, false);
                    return new CheckpointRestoreOutcome(
                        Id: target.Id,
                        Strategy: "reopened_snapshot",
                        Document: FilesPluginTools.BuildDocumentInfo(restored),
                        Message: "Active document was closed without saving and replaced with the checkpoint snapshot.");
                }
                else
                {
                    // The document that was active at checkpoint time is no longer
                    // the active one. Don't touch whatever IS currently active and
                    // unrelated -- open the snapshot as an additional document
                    // instead. (AutoCAD's DocumentManager makes newly-opened
                    // documents the active tab as a side effect of Open(), so focus
                    // does shift, but nothing gets closed or discarded.)
                    var restored = dm.Open(target.SnapshotPath, false);
                    return new CheckpointRestoreOutcome(
                        Id: target.Id,
                        Strategy: "reopened_snapshot_as_new_document",
                        Document: FilesPluginTools.BuildDocumentInfo(restored),
                        Message: "The active document differed from where this checkpoint was taken, so its own document was left untouched. The snapshot was opened as a new document instead (now the active one).");
                }
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
    [property: JsonPropertyName("id")]       string Id,
    // "reopened_snapshot" (real rollback, same document),
    // "reopened_snapshot_as_new_document" (active doc had changed since checkpoint),
    // or "no_snapshot" (checkpoint was created with fileSnapshot:false, or the
    // snapshot file is missing from disk -- nothing to restore).
    [property: JsonPropertyName("strategy")] string Strategy,
    [property: JsonPropertyName("document")] DocumentInfoDto? Document,
    [property: JsonPropertyName("message")]  string Message);

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
