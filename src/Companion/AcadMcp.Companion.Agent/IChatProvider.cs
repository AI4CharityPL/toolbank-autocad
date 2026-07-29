using System;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent;

/// <summary>The configured AI vendor.</summary>
public enum ProviderKind
{
    OpenAI,
    Anthropic,
    Gemini,
}

/// <summary>
/// A chat completion provider. Implementations stream assistant text via
/// <paramref name="onTextDelta"/> and return the final turn (text + tool calls).
/// </summary>
public interface IChatProvider
{
    ProviderKind Kind { get; }

    Task<AssistantTurn> SendAsync(
        ChatRequest request,
        Action<string> onTextDelta,
        CancellationToken ct);
}

/// <summary>
/// Optional capability: generate a raster visualization (e.g. a room render) from a text prompt.
/// Implemented by providers that expose an image model (OpenAI, Gemini). Returns PNG/JPEG bytes.
/// </summary>
public interface IImageGenerator
{
    /// <summary>True if the configured model/key can synthesize images.</summary>
    bool CanGenerateImages { get; }

    /// <summary>Generates an image from the prompt. Returns (bytes, mediaType) or throws on failure.</summary>
    Task<(byte[] Bytes, string MediaType)> GenerateImageAsync(string prompt, CancellationToken ct);
}
