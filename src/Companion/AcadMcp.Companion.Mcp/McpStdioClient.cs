using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Mcp;

/// <summary>
/// Minimal MCP client over stdio. Launches the AutoCAD tool-bank server as a child
/// process and speaks newline-delimited JSON-RPC 2.0 (initialize, tools/list, tools/call).
/// This is the exact same surface the desktop editor integration uses, so the in-app
/// agent inherits the entire tool bank with no re-implementation.
/// </summary>
public sealed class McpStdioClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    private readonly McpClientOptions _options;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly object _writeLock = new();
    private Process? _process;
    private StreamWriter? _stdin;
    private Task? _readLoop;
    private Task? _stderrLoop;
    private long _nextId;
    private volatile bool _disposed;

    private void Log(string msg) { try { _options.Log?.Invoke("[mcp] " + msg); } catch { } }

    public IReadOnlyList<McpToolInfo> Tools { get; private set; } = Array.Empty<McpToolInfo>();
    public string ServerName { get; private set; } = "autocad-tools";
    public bool IsConnected => _process is { HasExited: false } && _stdin is not null;

    public McpStdioClient(McpClientOptions options) => _options = options;

    /// <summary>Spawns the server, performs the MCP handshake and caches the tool list.</summary>
    public async Task ConnectAsync(CancellationToken ct)
    {
        var serverPath = BackendLocator.Resolve(_options.ServerExecutablePath)
            ?? throw new FileNotFoundException(
                "Nie znaleziono serwera narzędzi AutoCAD. Ustaw ścieżkę w ustawieniach lub zmiennej ACADMCP_BACKEND.");

        var psi = new ProcessStartInfo
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
        };

        if (serverPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            psi.ArgumentList.Add(serverPath);
        }
        else
        {
            psi.FileName = serverPath;
        }
        psi.ArgumentList.Add("--category");
        psi.ArgumentList.Add("router");
        psi.ArgumentList.Add("--pipe");
        psi.ArgumentList.Add(_options.PipeName);

        Log($"spawning: {psi.FileName} {string.Join(' ', psi.ArgumentList)}");
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Nie udało się uruchomić serwera narzędzi AutoCAD.");
        Log($"backend pid={_process.Id}");
        _stdin = _process.StandardInput;
        _readLoop = Task.Run(() => ReadLoopAsync(_process.StandardOutput), CancellationToken.None);
        // CRITICAL: backend logs to stderr. If we don't drain it the OS pipe buffer fills (~4KB)
        // and the backend blocks on its next log write -> stops answering JSON-RPC -> tools hang.
        _stderrLoop = Task.Run(() => StderrLoopAsync(_process.StandardError), CancellationToken.None);

        var initParams = new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["clientInfo"] = new JsonObject { ["name"] = "acad-companion", ["version"] = "0.1.0" },
        };
        var initResult = await SendRequestAsync("initialize", initParams, ct).ConfigureAwait(false);
        ServerName = initResult?["serverInfo"]?["name"]?.GetValue<string>() ?? ServerName;
        Log($"initialize OK serverName={ServerName}");

        SendNotification("notifications/initialized", null);

        await RefreshToolsAsync(ct).ConfigureAwait(false);
        Log($"tools/list -> {Tools.Count} tools: {string.Join(", ", Tools.Take(20).Select(t => t.Name))}");
    }

    /// <summary>Re-fetches the tool catalog from the server.</summary>
    public async Task RefreshToolsAsync(CancellationToken ct)
    {
        var listResult = await SendRequestAsync("tools/list", new JsonObject(), ct).ConfigureAwait(false);
        var tools = new List<McpToolInfo>();
        if (listResult?["tools"] is JsonArray arr)
        {
            foreach (var node in arr.OfType<JsonObject>())
            {
                var name = node["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;
                tools.Add(new McpToolInfo(
                    name!,
                    node["description"]?.GetValue<string>(),
                    node["inputSchema"]?.DeepClone() as JsonObject));
            }
        }
        Tools = tools;
    }

    /// <summary>Invokes a tool and flattens its content blocks into a single text string.</summary>
    public async Task<McpCallResult> CallToolAsync(string name, JsonObject arguments, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.CallTimeoutMs);

        var argsPreview = Truncate(arguments.ToJsonString(), 300);
        Log($"call -> {name} args={argsPreview}");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var prms = new JsonObject { ["name"] = name, ["arguments"] = arguments };
        JsonObject? result;
        try
        {
            result = await SendRequestAsync("tools/call", prms, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            Log($"call <- {name} TIMEOUT after {_options.CallTimeoutMs} ms");
            return new McpCallResult($"Narzędzie {name} przekroczyło limit czasu ({_options.CallTimeoutMs} ms).", IsError: true);
        }
        catch (Exception ex)
        {
            Log($"call <- {name} THREW {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        var sb = new StringBuilder();
        if (result?["content"] is JsonArray content)
        {
            foreach (var block in content.OfType<JsonObject>())
            {
                var text = block["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text)) sb.AppendLine(text);
            }
        }
        bool isError = result?["isError"]?.GetValue<bool>() ?? false;
        var outText = sb.ToString().TrimEnd();
        Log($"call <- {name} {(isError ? "ERROR" : "ok")} {sw.ElapsedMilliseconds}ms preview={Truncate(outText, 300)}");
        return new McpCallResult(outText, isError);
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private async Task<JsonObject?> SendRequestAsync(string method, JsonObject? prms, CancellationToken ct)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McpStdioClient));
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (prms is not null) request["params"] = prms;

        WriteLine(request);

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        var response = await tcs.Task.ConfigureAwait(false);

        if (response["error"] is JsonObject err)
        {
            var msg = err["message"]?.GetValue<string>() ?? "unknown error";
            var code = err["code"]?.GetValue<int>() ?? -1;
            throw new McpServerException($"{method} -> [{code}] {msg}");
        }
        return response["result"] as JsonObject;
    }

    private void SendNotification(string method, JsonObject? prms)
    {
        var notification = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
        if (prms is not null) notification["params"] = prms;
        WriteLine(notification);
    }

    private void WriteLine(JsonObject payload)
    {
        var json = payload.ToJsonString(JsonOpts);
        lock (_writeLock)
        {
            _stdin?.WriteLine(json);
            _stdin?.Flush();
        }
    }

    private async Task ReadLoopAsync(StreamReader reader)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonObject? msg;
                try { msg = JsonNode.Parse(line) as JsonObject; }
                catch { continue; }
                if (msg is null) continue;

                if (msg["id"]?.AsValue() is JsonValue idValue &&
                    idValue.TryGetValue<long>(out var id) &&
                    _pending.TryRemove(id, out var tcs))
                {
                    tcs.TrySetResult(msg);
                }
            }
        }
        catch (Exception ex)
        {
            FailAllPending(ex);
        }
        finally
        {
            FailAllPending(new McpServerException("Połączenie z serwerem narzędzi AutoCAD zostało zamknięte."));
        }
    }

    private async Task StderrLoopAsync(StreamReader reader)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (!string.IsNullOrWhiteSpace(line)) Log("stderr: " + line);
            }
        }
        catch
        {
            // Stream closed on process exit; nothing to do.
        }
    }

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs)) tcs.TrySetException(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try { SendNotification("notifications/cancelled", null); } catch { }
        try
        {
            lock (_writeLock) { _stdin?.Dispose(); }
        }
        catch { }

        if (_readLoop is not null)
        {
            try { await _readLoop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { }
        }

        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch { }
        finally
        {
            _process?.Dispose();
        }
    }
}

/// <summary>Raised when the tool-bank server returns a JSON-RPC error or disconnects.</summary>
public sealed class McpServerException : Exception
{
    public McpServerException(string message) : base(message) { }
}
