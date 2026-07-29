using System;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent.Providers;

/// <summary>Small shared helpers used by every provider implementation.</summary>
internal static class ProviderHttp
{
    /// <summary>Throws a readable <see cref="ProviderException"/> when the response is not 2xx.</summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage resp, string provider, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        string detail;
        try { detail = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
        catch { detail = "(brak treści)"; }
        if (detail.Length > 800) detail = detail.Substring(0, 800) + "...";
        throw new ProviderException($"{provider} HTTP {(int)resp.StatusCode}: {detail}");
    }

    /// <summary>Parses a (possibly partial/empty) JSON argument string into an object.</summary>
    public static JsonObject ParseArgs(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }

    /// <summary>Concatenates the text content parts of a message.</summary>
    public static string JoinText(ChatMessage msg)
    {
        var sb = new StringBuilder();
        foreach (var p in msg.Content)
        {
            if (p.Kind == ContentKind.Text && !string.IsNullOrEmpty(p.Text)) sb.Append(p.Text);
        }
        return sb.ToString();
    }
}

/// <summary>A vendor API call failed (HTTP error, auth, quota, malformed response).</summary>
public sealed class ProviderException : Exception
{
    public ProviderException(string message) : base(message) { }
}
