using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent;

/// <summary>
/// Fetches the list of usable model ids for a provider directly from its REST API using the
/// user's own key (BYOK). Falls back to a curated list when offline or when no key is present,
/// so the Settings dropdown is never empty.
/// </summary>
public static class ModelCatalog
{
    public static async Task<IReadOnlyList<string>> ListAsync(
        ProviderKind kind, string? apiKey, HttpClient http, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var live = kind switch
                {
                    ProviderKind.OpenAI => await OpenAiAsync(apiKey!, http, ct).ConfigureAwait(false),
                    ProviderKind.Anthropic => await AnthropicAsync(apiKey!, http, ct).ConfigureAwait(false),
                    ProviderKind.Gemini => await GeminiAsync(apiKey!, http, ct).ConfigureAwait(false),
                    _ => new List<string>(),
                };
                if (live.Count > 0) return live;
            }
            catch
            {
                // Network/auth failure -> curated fallback below.
            }
        }
        return Fallback(kind);
    }

    private static async Task<List<string>> OpenAiAsync(string key, HttpClient http, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var ids = (root?["data"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(o => o["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            // Chat-capable families only; hide embeddings/audio/image/moderation endpoints.
            .Where(id => (id.StartsWith("gpt", StringComparison.OrdinalIgnoreCase)
                          || id.StartsWith("o", StringComparison.OrdinalIgnoreCase)
                          || id.StartsWith("chatgpt", StringComparison.OrdinalIgnoreCase))
                         && !id.Contains("embedding") && !id.Contains("audio")
                         && !id.Contains("realtime") && !id.Contains("image")
                         && !id.Contains("transcribe") && !id.Contains("tts")
                         && !id.Contains("moderation"))
            .Distinct()
            .OrderByDescending(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ids ?? new List<string>();
    }

    private static async Task<List<string>> AnthropicAsync(string key, HttpClient http, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models?limit=1000");
        req.Headers.TryAddWithoutValidation("x-api-key", key);
        req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var ids = (root?["data"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(o => o["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .OrderByDescending(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ids ?? new List<string>();
    }

    private static async Task<List<string>> GeminiAsync(string key, HttpClient http, CancellationToken ct)
    {
        var url = "https://generativelanguage.googleapis.com/v1beta/models?key=" + Uri.EscapeDataString(key);
        using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var ids = (root?["models"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(o => (o["supportedGenerationMethods"] as JsonArray)?
                .Any(m => string.Equals(m?.GetValue<string>(), "generateContent", StringComparison.OrdinalIgnoreCase)) == true)
            .Select(o => o["name"]?.GetValue<string>())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!.StartsWith("models/", StringComparison.Ordinal) ? n.Substring("models/".Length) : n!)
            .Where(n => n.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ids ?? new List<string>();
    }

    /// <summary>Curated latest-generation defaults used when the live list is unavailable.</summary>
    public static IReadOnlyList<string> Fallback(ProviderKind kind) => kind switch
    {
        ProviderKind.OpenAI => new[] { "gpt-5.1", "gpt-5.1-mini", "gpt-5", "gpt-4.1", "o4-mini" },
        ProviderKind.Anthropic => new[] { "claude-opus-4-6", "claude-sonnet-4-5", "claude-3-7-sonnet-latest" },
        ProviderKind.Gemini => new[] { "gemini-3-pro", "gemini-2.5-pro", "gemini-2.5-flash" },
        _ => Array.Empty<string>(),
    };
}
