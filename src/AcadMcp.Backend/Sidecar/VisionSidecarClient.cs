// HTTP client for the AcadMcp.Vision Python sidecar.
// Discovers the port from %LOCALAPPDATA%\AcadMcp\vision.port (rule 29 §3),
// falls back to ACADMCP_VISION_PORT env or 50062.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Sidecar;

public sealed class VisionSidecarClient : IVisionSidecarClient, IDisposable
{
    private const string DefaultHost = "127.0.0.1";
    private const int DefaultPort = 50062;

    private readonly ILogger<VisionSidecarClient> _logger;
    private readonly HttpClient _http;
    private readonly object _lock = new();
    private DateTimeOffset _lastHealthOk = DateTimeOffset.MinValue;
    private string _baseUrl;

    public VisionSidecarClient(ILogger<VisionSidecarClient> logger)
    {
        _logger = logger;
        _baseUrl = ResolveBaseUrl();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public string BaseUrl => _baseUrl;
    public bool IsHealthy => (DateTimeOffset.UtcNow - _lastHealthOk).TotalSeconds < 30;

    public async Task<JsonNode?> GetJsonAsync(string relativePath, int timeoutMs, CancellationToken ct)
    {
        var url = JoinUrl(_baseUrl, relativePath);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            using var rsp = await _http.GetAsync(url, cts.Token).ConfigureAwait(false);
            return await ProcessResponseAsync(rsp, relativePath).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new VisionUnavailableException(_baseUrl,
                $"Vision sidecar not reachable at {_baseUrl} ({ex.Message}). " +
                "Run scripts\\start-vision.ps1 -EnsureRunning or `acadmcp-vision`.",
                ex);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new VisionToolException(408, $"Vision sidecar GET {relativePath} timed out after {timeoutMs} ms.").Inner(ex);
        }
    }

    public async Task<JsonNode?> PostJsonAsync(string relativePath, JsonNode body, int timeoutMs, CancellationToken ct)
    {
        var url = JoinUrl(_baseUrl, relativePath);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var rsp = await _http.PostAsync(url, content, cts.Token).ConfigureAwait(false);
            return await ProcessResponseAsync(rsp, relativePath).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new VisionUnavailableException(_baseUrl,
                $"Vision sidecar not reachable at {_baseUrl} ({ex.Message}). " +
                "Run scripts\\start-vision.ps1 -EnsureRunning or `acadmcp-vision`.",
                ex);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new VisionToolException(408, $"Vision sidecar POST {relativePath} timed out after {timeoutMs} ms.").Inner(ex);
        }
    }

    private async Task<JsonNode?> ProcessResponseAsync(HttpResponseMessage rsp, string relativePath)
    {
        if (rsp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            var body = await rsp.Content.ReadFromJsonAsync<JsonNode>().ConfigureAwait(false);
            var engine = body?["engine"]?.GetValue<string>() ?? "unknown";
            var hint = body?["install_hint"]?.GetValue<string>() ?? "";
            throw new VisionEngineUnavailableException(engine, hint);
        }
        if (!rsp.IsSuccessStatusCode)
        {
            var text = await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new VisionToolException((int)rsp.StatusCode, text);
        }
        var node = await rsp.Content.ReadFromJsonAsync<JsonNode>().ConfigureAwait(false);
        if (relativePath == "/health") _lastHealthOk = DateTimeOffset.UtcNow;
        return node;
    }

    private static string JoinUrl(string baseUrl, string rel)
    {
        if (string.IsNullOrEmpty(rel) || rel == "/") return baseUrl + "/";
        if (rel.StartsWith('/')) return baseUrl + rel;
        return baseUrl + "/" + rel;
    }

    private string ResolveBaseUrl()
    {
        // 1) Env override.
        var envPort = Environment.GetEnvironmentVariable("ACADMCP_VISION_PORT");
        if (!string.IsNullOrWhiteSpace(envPort) && int.TryParse(envPort, out var ep))
        {
            return $"http://{DefaultHost}:{ep}";
        }
        // 2) Discovery file written by sidecar (rule 29 §3).
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(localAppData))
        {
            var portFile = Path.Combine(localAppData, "AcadMcp", "vision.port");
            if (File.Exists(portFile))
            {
                try
                {
                    var raw = File.ReadAllText(portFile).Trim();
                    if (int.TryParse(raw, out var fp))
                    {
                        return $"http://{DefaultHost}:{fp}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read vision.port file {File}", portFile);
                }
            }
        }
        return $"http://{DefaultHost}:{DefaultPort}";
    }

    public void Dispose() => _http.Dispose();
}

internal static class ExceptionInnerExtensions
{
    public static T Inner<T>(this T ex, Exception inner) where T : Exception
    {
        var f = typeof(Exception).GetField("_innerException",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        f?.SetValue(ex, inner);
        return ex;
    }
}
