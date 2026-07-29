// Length-prefixed JSON framing for the named pipe between Backend and Plugin.
// 4-byte little-endian length + UTF-8 JSON payload. Multi-target compatible (net8.0 + net48).
// See rule 17-pipe-protocol.md.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Shared.Pipe;

/// <summary>
/// Read/write length-prefixed JSON frames over any <see cref="Stream"/>.
/// Used by both Plugin (server side) and Backend (client side); MUST stay binary-identical.
/// </summary>
public static class PipeFraming
{
    /// <summary>Maximum payload size in bytes (16 MiB). Larger frames close the connection.</summary>
    public const int MaxPayloadBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Reads exactly one frame. Returns null when the underlying stream reports a clean EOF
    /// before any byte has been read (peer closed). Throws on partial frames or oversize.
    /// </summary>
    public static async Task<MessageEnvelope?> ReadEnvelopeAsync(Stream stream, CancellationToken ct)
    {
        var lenBuf = new byte[4];
        int read = await ReadAtLeastAsync(stream, lenBuf, 4, allowEof: true, ct).ConfigureAwait(false);
        if (read == 0) return null;
        if (read < 4) throw new IOException("Pipe closed mid-length-prefix");

        int length = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (length <= 0) throw new IOException($"Invalid frame length {length}");
        if (length > MaxPayloadBytes) throw new IOException($"Frame too large: {length} > {MaxPayloadBytes}");

        var payload = new byte[length];
        int got = await ReadAtLeastAsync(stream, payload, length, allowEof: false, ct).ConfigureAwait(false);
        if (got != length) throw new IOException($"Pipe closed mid-payload (got {got} of {length})");

        var env = JsonSerializer.Deserialize<MessageEnvelope>(payload, JsonOptions);
        if (env is null) throw new IOException("Frame deserialized to null envelope");
        return env;
    }

    /// <summary>Writes one frame and flushes. Thread-safety is the caller's responsibility.</summary>
    public static async Task WriteEnvelopeAsync(Stream stream, MessageEnvelope envelope, CancellationToken ct)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        if (bytes.Length > MaxPayloadBytes)
            throw new IOException($"Outbound frame too large: {bytes.Length} > {MaxPayloadBytes}");

        var prefix = new byte[4];
        prefix[0] = (byte)(bytes.Length & 0xFF);
        prefix[1] = (byte)((bytes.Length >> 8) & 0xFF);
        prefix[2] = (byte)((bytes.Length >> 16) & 0xFF);
        prefix[3] = (byte)((bytes.Length >> 24) & 0xFF);

        await stream.WriteAsync(prefix, 0, 4, ct).ConfigureAwait(false);
        await stream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Build an envelope from any DTO by serializing it to <see cref="JsonObject"/>.
    /// </summary>
    public static MessageEnvelope Wrap<T>(MessageKind kind, T payload) where T : class
    {
        var node = JsonSerializer.SerializeToNode(payload, JsonOptions) as JsonObject
                   ?? throw new InvalidOperationException("Payload did not serialize to a JSON object");
        return new MessageEnvelope(kind, node);
    }

    /// <summary>Reverse of <see cref="Wrap{T}"/>: deserialize the envelope's payload to T.</summary>
    public static T? Unwrap<T>(MessageEnvelope envelope) where T : class
    {
        if (envelope.Payload is null) return null;
        return JsonSerializer.Deserialize<T>(envelope.Payload.ToJsonString(JsonOptions), JsonOptions);
    }

    private static async Task<int> ReadAtLeastAsync(Stream stream, byte[] buffer, int count, bool allowEof, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int n = await stream.ReadAsync(buffer, totalRead, count - totalRead, ct).ConfigureAwait(false);
            if (n == 0)
            {
                if (allowEof && totalRead == 0) return 0;
                return totalRead;
            }
            totalRead += n;
        }
        return totalRead;
    }
}
