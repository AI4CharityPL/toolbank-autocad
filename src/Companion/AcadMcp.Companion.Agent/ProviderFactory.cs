using System;
using System.Net.Http;
using AcadMcp.Companion.Agent.Providers;

namespace AcadMcp.Companion.Agent;

/// <summary>Builds the concrete <see cref="IChatProvider"/> for a selected vendor + key.</summary>
public static class ProviderFactory
{
    public static IChatProvider Create(ProviderKind kind, string apiKey, HttpClient http) => kind switch
    {
        ProviderKind.OpenAI => new OpenAiProvider(http, apiKey),
        ProviderKind.Anthropic => new AnthropicProvider(http, apiKey),
        ProviderKind.Gemini => new GeminiProvider(http, apiKey),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nieznany dostawca AI."),
    };

    public static string DisplayName(ProviderKind kind) => kind switch
    {
        ProviderKind.OpenAI => "OpenAI",
        ProviderKind.Anthropic => "Anthropic (Claude)",
        ProviderKind.Gemini => "Google Gemini",
        _ => kind.ToString(),
    };
}
