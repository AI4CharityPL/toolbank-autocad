// Phase 7.1: server-pushed events (entity changes, command lifecycle).
//
// Honest scope note: rule 17-pipe-protocol.mdc describes `kind: "event"` +
// AcadEvent as pushable on the main pipe, and the Phase 7-8 roadmap calls for
// a separate "livestream" pipe plus an "acad-livestream" category. In
// practice, an MCP tool-calling agent has no channel for genuine
// server-initiated push -- MCP is request/response over stdio, so nothing on
// the AI-agent side of this system can receive an unsolicited frame no matter
// which pipe it travels on. What IS real and useful here: entity-change and
// command-lifecycle events are captured as they happen (via real AutoCAD
// Database/Document event hooks, not polling AutoCAD itself) into a bounded
// ring buffer with monotonic sequence numbers, and exposed through
// acad_livestream.poll(sinceSeq) so an agent can cheaply ask "what happened
// since I last checked" instead of re-scanning the whole drawing. This is
// genuinely poll-based, not a raw second named pipe -- building one would add
// transport-layer complexity without changing what an MCP tool-calling agent
// can actually consume, since it still has to poll a tool either way.
//
// Threading: Database.ObjectAppended/Modified/Erased and Document.CommandWillStart/
// CommandEnded fire synchronously on whatever thread is running the transaction
// that triggered them -- for every write tool in this plugin, that's the UI
// thread, inside UiThreadDispatcher.Run. Handlers below do only cheap field
// reads off the DBObject/Document already open in that transaction and append
// to a lock-protected ring buffer -- no further AutoCAD API calls, no
// re-entrant transactions, nothing that could deadlock per rule 10.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadMcp.Plugin.Tools;

internal static class LivestreamPluginTools
{
    private const int Capacity = 2000;

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly object _lock = new();
    private static readonly LinkedList<LivestreamRecord> _buffer = new();
    private static long _nextSeq = 1;
    private static long _droppedTotal;

    private static readonly ConcurrentDictionary<Database, byte> _hookedDatabases = new();
    private static readonly ConcurrentDictionary<Document, byte> _hookedDocuments = new();

    public static void Register(ToolHost host)
    {
        host.Register("acad.livestream.poll",   Poll);
        host.Register("acad.livestream.status", Status);
        host.Register("acad.livestream.clear",  Clear);

        try
        {
            var dm = AcadApp.DocumentManager;
            if (dm is not null)
            {
                dm.DocumentCreated  += (s, e) => { if (e.Document is not null) HookDocument(e.Document); };
                dm.DocumentActivated += (s, e) => { if (e.Document is not null) HookDocument(e.Document); };
                if (dm.MdiActiveDocument is not null) HookDocument(dm.MdiActiveDocument);
            }
        }
        catch (Exception ex) { Logging.Log.Warn($"livestream: initial hook setup failed: {ex.Message}"); }
    }

    private static void HookDocument(Document doc)
    {
        if (!_hookedDocuments.TryAdd(doc, 0)) return;
        try
        {
            doc.CommandWillStart += (s, e) => Push("command_will_start", doc, new JsonObject { ["command"] = e.GlobalCommandName });
            doc.CommandEnded     += (s, e) => Push("command_ended",      doc, new JsonObject { ["command"] = e.GlobalCommandName });
        }
        catch (Exception ex) { Logging.Log.Warn($"livestream: command hook failed for {SafeName(doc)}: {ex.Message}"); }

        if (_hookedDatabases.TryAdd(doc.Database, 0))
        {
            try
            {
                var db = doc.Database;
                db.ObjectAppended += (s, e) => PushEntityEvent("entity_appended", doc, e.DBObject);
                db.ObjectModified += (s, e) => PushEntityEvent("entity_modified", doc, e.DBObject);
                db.ObjectErased   += (s, e) => { if (e.Erased) PushEntityEvent("entity_erased", doc, e.DBObject); };
            }
            catch (Exception ex) { Logging.Log.Warn($"livestream: database hook failed for {SafeName(doc)}: {ex.Message}"); }
        }
    }

    private static void PushEntityEvent(string kind, Document doc, DBObject? obj)
    {
        if (obj is null) return;
        var payload = new JsonObject
        {
            ["handle"] = obj.Handle.ToString(),
            ["dxfType"] = obj.GetType().Name,
        };
        if (obj is Entity ent)
        {
            try { payload["layer"] = ent.Layer; } catch { }
        }
        Push(kind, doc, payload);
    }

    private static void Push(string kind, Document doc, JsonObject payload)
    {
        var record = new LivestreamRecord(0, kind, SafeName(doc), payload, DateTime.UtcNow.ToString("O"));
        lock (_lock)
        {
            record = record with { Seq = _nextSeq++ };
            _buffer.AddLast(record);
            while (_buffer.Count > Capacity)
            {
                _buffer.RemoveFirst();
                _droppedTotal++;
            }
        }
    }

    private static string SafeName(Document doc)
    {
        try { return doc.Name ?? "<unsaved>"; } catch { return "<unknown>"; }
    }

    // ─────────── tools ───────────

    private static Task<ToolDispatchResult> Poll(JsonObject args, CancellationToken ct)
    {
        try
        {
            var a = args.Count > 0 ? Read<PollArgsDto>(args) : new PollArgsDto();
            long since = a.SinceSeq ?? 0;
            int maxCount = a.MaxCount is > 0 ? a.MaxCount.Value : 200;

            List<LivestreamRecord> matched;
            long headSeq, dropped;
            lock (_lock)
            {
                matched = _buffer.Where(r => r.Seq > since).Take(maxCount).ToList();
                headSeq = _nextSeq - 1;
                dropped = _droppedTotal;
            }

            var dto = new PollResultDto(
                matched.Select(r => new LivestreamEventDto(r.Seq, r.Kind, r.DocumentName, r.Payload, r.Timestamp)).ToList(),
                NextSeq: matched.Count > 0 ? matched[^1].Seq : since,
                HeadSeq: headSeq,
                DroppedTotal: dropped);
            return Task.FromResult(new ToolDispatchResult(true, Wrap(dto), null));
        }
        catch (Exception ex) { return Task.FromResult(AcadErrorMapper.Fail("acad.livestream.poll", ex)); }
    }

    private static Task<ToolDispatchResult> Status(JsonObject args, CancellationToken ct)
    {
        long count, headSeq, dropped;
        lock (_lock)
        {
            count = _buffer.Count;
            headSeq = _nextSeq - 1;
            dropped = _droppedTotal;
        }
        var dto = new LivestreamStatusDto(count, Capacity, headSeq, dropped, _hookedDocuments.Count);
        return Task.FromResult(new ToolDispatchResult(true, Wrap(dto), null));
    }

    private static Task<ToolDispatchResult> Clear(JsonObject args, CancellationToken ct)
    {
        int removed;
        lock (_lock)
        {
            removed = _buffer.Count;
            _buffer.Clear();
        }
        return Task.FromResult(new ToolDispatchResult(true, Wrap(new { removedEvents = removed }), null));
    }

    private static T Read<T>(JsonObject args) =>
        JsonSerializer.Deserialize<T>(args, Opts) ?? throw new ArgumentException($"cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private sealed record LivestreamRecord(long Seq, string Kind, string DocumentName, JsonObject Payload, string Timestamp);
}

internal sealed record PollArgsDto(
    [property: JsonPropertyName("sinceSeq")] long? SinceSeq = null,
    [property: JsonPropertyName("maxCount")] int? MaxCount = null);

internal sealed record LivestreamEventDto(
    [property: JsonPropertyName("seq")]          long Seq,
    [property: JsonPropertyName("kind")]         string Kind,
    [property: JsonPropertyName("documentName")] string DocumentName,
    [property: JsonPropertyName("payload")]      JsonObject Payload,
    [property: JsonPropertyName("timestamp")]    string Timestamp);

internal sealed record PollResultDto(
    [property: JsonPropertyName("events")]       List<LivestreamEventDto> Events,
    [property: JsonPropertyName("nextSeq")]      long NextSeq,
    [property: JsonPropertyName("headSeq")]      long HeadSeq,
    [property: JsonPropertyName("droppedTotal")] long DroppedTotal);

internal sealed record LivestreamStatusDto(
    [property: JsonPropertyName("bufferedCount")]    long BufferedCount,
    [property: JsonPropertyName("capacity")]         int Capacity,
    [property: JsonPropertyName("headSeq")]          long HeadSeq,
    [property: JsonPropertyName("droppedTotal")]     long DroppedTotal,
    [property: JsonPropertyName("hookedDocuments")]  int HookedDocuments);
