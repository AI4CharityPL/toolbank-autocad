// Wire contracts for the named pipe between Backend processes and the AutoCAD Plugin.
// Frozen surface: per rule 02-no-breaking-changes.md, all changes here must be additive.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AcadMcp.Shared;

/// <summary>
/// Pipe protocol version negotiated at handshake. Bump only on breaking changes.
/// </summary>
public static class PipeProtocol
{
    public const int CurrentVersion = 1;
    public const int MinSupportedVersion = 1;
    public const string PipeName = "acadmcp";
}

/// <summary>Handshake sent by Backend immediately after connecting.</summary>
public sealed record HandshakeRequest(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("protocolVersion")] int ProtocolVersion,
    [property: JsonPropertyName("backendVersion")] string BackendVersion);

/// <summary>Handshake response from Plugin.</summary>
public sealed record HandshakeResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("pluginVersion")] string PluginVersion,
    [property: JsonPropertyName("acadVersion")] string AcadVersion,
    [property: JsonPropertyName("acadVertical")] string? AcadVertical,
    [property: JsonPropertyName("isLT")] bool IsLT,
    [property: JsonPropertyName("negotiatedProtocolVersion")] int NegotiatedProtocolVersion,
    [property: JsonPropertyName("error")] ErrorInfo? Error = null);

/// <summary>A single tool invocation request from Backend to Plugin.</summary>
public sealed record ToolRequest(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("args")] JsonObject Args,
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 30000,
    [property: JsonPropertyName("checkpointBefore")] bool CheckpointBefore = false);

/// <summary>Response to a tool request.</summary>
public sealed record ToolResponse(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] JsonObject? Result = null,
    [property: JsonPropertyName("error")] ErrorInfo? Error = null,
    [property: JsonPropertyName("checkpointId")] string? CheckpointId = null,
    [property: JsonPropertyName("apiVersion")] int ApiVersion = 1);

/// <summary>
/// Generic typed wrapper used inside Backend categories.
/// Adds <see cref="Data"/> deserialized from <see cref="ToolResponse.Result"/>.
/// </summary>
public sealed record ToolResponse<T>(
    bool Ok,
    T? Data,
    ErrorInfo? Error,
    string? CheckpointId = null);

/// <summary>Structured error info. Never expose raw stack traces to the agent.</summary>
public sealed record ErrorInfo(
    [property: JsonPropertyName("code")] AcadErrorCode Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("hint")] string? Hint = null,
    [property: JsonPropertyName("details")] JsonObject? Details = null);

/// <summary>Closed enum of all errors crossing the wire. Append-only.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AcadErrorCode
{
    Unknown = 0,
    NoActiveDocument = 1,
    DocumentLocked = 2,
    InvalidArgument = 3,
    EntityNotFound = 4,
    LayerNotFound = 5,
    BlockNotFound = 6,
    TransactionAborted = 7,
    PluginUnavailable = 8,
    NotSupportedOnLT = 9,
    AcadException = 10,
    Timeout = 11,
    ProtocolMismatch = 12,
    UnknownTool = 13,
    PermissionDenied = 14,
    InternalError = 15,
}

/// <summary>Server-pushed event over pipe (entity changes, command lifecycle).</summary>
public sealed record AcadEvent(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("documentName")] string? DocumentName,
    [property: JsonPropertyName("payload")] JsonObject Payload,
    [property: JsonPropertyName("timestamp")] string Timestamp);

/// <summary>
/// Cancel request sent by Backend to abort an in-flight tool call.
/// Server best-effort cancels the per-request CancellationToken and emits a single
/// <see cref="ToolResponse"/> with <see cref="AcadErrorCode.Timeout"/>.
/// </summary>
public sealed record CancelRequest(
    [property: JsonPropertyName("correlationId")] string CorrelationId);

/// <summary>
/// Discriminator for <see cref="MessageEnvelope.Kind"/>. See rule 17-pipe-protocol.md.
/// Append-only. Adding a new value MUST be tolerated by older peers via UnknownMessageKind error.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageKind
{
    Unknown = 0,
    Handshake = 1,
    HandshakeResponse = 2,
    Tool = 3,
    ToolResponse = 4,
    Cancel = 5,
    Event = 6,
}

/// <summary>
/// Wire envelope. EVERY frame on the pipe is one of these. Payload is the matching DTO
/// for <see cref="Kind"/>: HandshakeRequest, HandshakeResponse, ToolRequest, ToolResponse,
/// CancelRequest, AcadEvent. See rule 17-pipe-protocol.md.
/// </summary>
public sealed record MessageEnvelope(
    [property: JsonPropertyName("kind")] MessageKind Kind,
    [property: JsonPropertyName("payload")] JsonObject Payload);

/// <summary>Stable identifier for an AutoCAD database object.</summary>
public sealed record EntityHandle(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("objectClass")] string ObjectClass,
    [property: JsonPropertyName("layer")] string? Layer = null,
    [property: JsonPropertyName("ownerHandle")] string? OwnerHandle = null);

/// <summary>2D point in current UCS units.</summary>
public sealed record Point2dDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

/// <summary>3D point in current UCS units.</summary>
public sealed record Point3dDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z = 0.0);

/// <summary>3D vector.</summary>
public sealed record Vector3dDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z = 0.0);

/// <summary>Axis-aligned bounding box.</summary>
public sealed record BoundingBoxDto(
    [property: JsonPropertyName("min")] Point3dDto Min,
    [property: JsonPropertyName("max")] Point3dDto Max);

/// <summary>RGB color (0-255 each).</summary>
public sealed record ColorDto(
    [property: JsonPropertyName("r")] int R,
    [property: JsonPropertyName("g")] int G,
    [property: JsonPropertyName("b")] int B,
    [property: JsonPropertyName("aciIndex")] int? AciIndex = null);

/// <summary>Layer descriptor.</summary>
public sealed record LayerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] ColorDto? Color = null,
    [property: JsonPropertyName("lineweightMm")] double? LineweightMm = null,
    [property: JsonPropertyName("linetype")] string? Linetype = null,
    [property: JsonPropertyName("frozen")] bool Frozen = false,
    [property: JsonPropertyName("locked")] bool Locked = false,
    [property: JsonPropertyName("off")] bool Off = false,
    [property: JsonPropertyName("plottable")] bool Plottable = true,
    [property: JsonPropertyName("description")] string? Description = null);

/// <summary>Lightweight document state snapshot returned by acad_status.</summary>
public sealed record DocumentStatusDto(
    [property: JsonPropertyName("alive")] bool Alive,
    [property: JsonPropertyName("acadProductName")] string? AcadProductName = null,
    [property: JsonPropertyName("acadVersion")] string? AcadVersion = null,
    [property: JsonPropertyName("documentName")] string? DocumentName = null,
    [property: JsonPropertyName("activeLayer")] string? ActiveLayer = null,
    [property: JsonPropertyName("entityCount")] int EntityCount = 0,
    [property: JsonPropertyName("isLT")] bool IsLT = false,
    [property: JsonPropertyName("vertical")] string? Vertical = null,
    [property: JsonPropertyName("modeBanner")] string? ModeBanner = null);
