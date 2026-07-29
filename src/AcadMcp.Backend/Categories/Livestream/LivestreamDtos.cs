using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Categories.Livestream;

public sealed record LivestreamEmptyArgs();

public sealed record PollEventsArgs(
    [property: JsonPropertyName("sinceSeq")] long? SinceSeq = null,
    [property: JsonPropertyName("maxCount")] int? MaxCount = null);

public sealed record LivestreamEventResult(
    [property: JsonPropertyName("seq")]          long Seq,
    [property: JsonPropertyName("kind")]         string Kind,
    [property: JsonPropertyName("documentName")] string DocumentName,
    [property: JsonPropertyName("payload")]      JsonObject Payload,
    [property: JsonPropertyName("timestamp")]    string Timestamp);

public sealed record PollEventsResult(
    [property: JsonPropertyName("events")]       List<LivestreamEventResult> Events,
    [property: JsonPropertyName("nextSeq")]      long NextSeq,
    [property: JsonPropertyName("headSeq")]      long HeadSeq,
    [property: JsonPropertyName("droppedTotal")] long DroppedTotal);

public sealed record LivestreamStatusResult(
    [property: JsonPropertyName("bufferedCount")]   long BufferedCount,
    [property: JsonPropertyName("capacity")]        int Capacity,
    [property: JsonPropertyName("headSeq")]         long HeadSeq,
    [property: JsonPropertyName("droppedTotal")]    long DroppedTotal,
    [property: JsonPropertyName("hookedDocuments")] int HookedDocuments);

public sealed record ClearEventsResult(
    [property: JsonPropertyName("removedEvents")] int RemovedEvents);
