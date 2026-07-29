// Minimal MCP-compatible JSON-RPC 2.0 host over stdio.
// MCP spec uses LSP-style framing (Content-Length headers) OR newline-delimited JSON for stdio.
// We support BOTH on input; we emit newline-delimited JSON which is the modern MCP convention.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Mcp;

/// <summary>JSON-RPC 2.0 host. Reads from stdin, writes to stdout. Stderr reserved for logs.</summary>
public sealed class StdioJsonRpcHost
{
    private readonly ILogger _logger;
    private readonly IJsonRpcDispatcher _dispatcher;

    public StdioJsonRpcHost(ILogger logger, IJsonRpcDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await using var outStream = Console.OpenStandardOutput();
        var writer = new StreamWriter(outStream, new UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };
        var inStream = Console.OpenStandardInput();
        var reader = new StreamReader(inStream, Encoding.UTF8);

        _logger.LogDebug("Stdio JSON-RPC host started");

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null) break; // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Support LSP-style header framing transparently
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(line.Substring("Content-Length:".Length).Trim(), out var len))
                {
                    _logger.LogWarning("Malformed Content-Length header: {Line}", line);
                    continue;
                }
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false))) { }
                var buf = new char[len];
                int read = 0;
                while (read < len)
                {
                    int n = await reader.ReadAsync(buf, read, len - read).ConfigureAwait(false);
                    if (n <= 0) break;
                    read += n;
                }
                line = new string(buf, 0, read);
            }

            JsonNode? request;
            try
            {
                request = JsonNode.Parse(line);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse JSON-RPC line");
                await WriteAsync(writer, BuildError(null, -32700, "Parse error: " + ex.Message)).ConfigureAwait(false);
                continue;
            }

            if (request is JsonArray array)
            {
                foreach (var item in array)
                {
                    var response = await _dispatcher.DispatchAsync(item as JsonObject, ct).ConfigureAwait(false);
                    if (response is not null) await WriteAsync(writer, response).ConfigureAwait(false);
                }
            }
            else if (request is JsonObject obj)
            {
                var response = await _dispatcher.DispatchAsync(obj, ct).ConfigureAwait(false);
                if (response is not null) await WriteAsync(writer, response).ConfigureAwait(false);
            }
        }

        _logger.LogDebug("Stdio JSON-RPC host exiting");
    }

    private static async Task WriteAsync(StreamWriter writer, JsonObject response)
    {
        var json = response.ToJsonString(JsonOpts.Compact);
        await writer.WriteLineAsync(json).ConfigureAwait(false);
    }

    public static JsonObject BuildError(JsonNode? id, int code, string message, JsonNode? data = null)
    {
        var err = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        };
        if (data is not null) err["data"] = data.DeepClone();

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = err,
        };
    }

    public static JsonObject BuildResult(JsonNode? id, JsonNode result)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result.DeepClone(),
        };
}

internal static class JsonOpts
{
    // Explicit TypeInfoResolver is required in .NET 8 once the options become
    // read-only (first use) - without it, calls like JsonArray.Add(string) go
    // through the generic JsonSerializer path and throw
    // "JsonSerializerOptions instance must specify a TypeInfoResolver...".
    public static readonly JsonSerializerOptions Compact = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };
}

/// <summary>Plug here whatever wants to handle JSON-RPC requests.</summary>
public interface IJsonRpcDispatcher
{
    Task<JsonObject?> DispatchAsync(JsonObject? request, CancellationToken ct);
}
