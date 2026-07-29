using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AcadMcp.Companion.Agent;

/// <summary>Who authored a chat message.</summary>
public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool,
}

/// <summary>Kind of a single content part inside a message.</summary>
public enum ContentKind
{
    Text,
    Image,
    Document,
}

/// <summary>
/// A single piece of message content. Text carries <see cref="Text"/>; binary parts
/// (image/document) carry base64 <see cref="Data"/> and <see cref="MediaType"/>.
/// </summary>
public sealed class ContentPart
{
    public ContentKind Kind { get; init; }
    public string? Text { get; init; }
    public string? Data { get; init; }
    public string? MediaType { get; init; }
    public string? FileName { get; init; }

    public static ContentPart FromText(string text) => new() { Kind = ContentKind.Text, Text = text };

    public static ContentPart FromImage(byte[] bytes, string mediaType, string? fileName = null) => new()
    {
        Kind = ContentKind.Image,
        Data = Convert.ToBase64String(bytes),
        MediaType = mediaType,
        FileName = fileName,
    };

    public static ContentPart FromDocument(byte[] bytes, string mediaType, string? fileName = null) => new()
    {
        Kind = ContentKind.Document,
        Data = Convert.ToBase64String(bytes),
        MediaType = mediaType,
        FileName = fileName,
    };
}

/// <summary>A tool/function call requested by the model.</summary>
public sealed class ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public JsonObject Arguments { get; init; } = new();
}

/// <summary>The result of executing a <see cref="ToolCall"/>, fed back to the model.</summary>
public sealed class ToolResult
{
    public required string ToolCallId { get; init; }
    public required string Content { get; init; }
    public bool IsError { get; init; }

    /// <summary>Tool name. Needed by providers (e.g. Gemini) that match results by name, not id.</summary>
    public string? Name { get; init; }
}

/// <summary>One turn in the conversation.</summary>
public sealed class ChatMessage
{
    public ChatRole Role { get; init; }
    public List<ContentPart> Content { get; init; } = new();
    public List<ToolCall> ToolCalls { get; init; } = new();
    public List<ToolResult> ToolResults { get; init; } = new();

    public static ChatMessage UserText(string text)
        => new() { Role = ChatRole.User, Content = { ContentPart.FromText(text) } };

    public static ChatMessage AssistantText(string text)
        => new() { Role = ChatRole.Assistant, Content = { ContentPart.FromText(text) } };
}

/// <summary>A tool exposed to the model (JSON-schema based).</summary>
public sealed record ToolDefinition(string Name, string Description, JsonObject InputSchema);

/// <summary>Everything a provider needs to produce the next assistant turn.</summary>
public sealed class ChatRequest
{
    public required string Model { get; init; }
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = Array.Empty<ToolDefinition>();
    public int MaxTokens { get; init; } = 4096;
    public double Temperature { get; init; } = 0.2;

    /// <summary>
    /// Max time to wait for the next streamed token before aborting the turn. Guards against a
    /// silently stalled provider stream (the #1 cause of the chat appearing frozen). Default 90 s.
    /// </summary>
    public TimeSpan StreamIdleTimeout { get; init; } = TimeSpan.FromSeconds(90);
}

/// <summary>Why the model stopped producing the turn.</summary>
public enum StopReason
{
    /// <summary>Natural end of the answer.</summary>
    Stop,
    /// <summary>Truncated by the token limit — the answer is incomplete and should be continued.</summary>
    Length,
    /// <summary>Stopped to run tool calls.</summary>
    ToolCalls,
    /// <summary>Unknown / provider-specific.</summary>
    Other,
}

/// <summary>The assistant's reply: accumulated text plus any tool calls it wants to run.</summary>
public sealed class AssistantTurn
{
    public string Text { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; } = new();
    public bool HasToolCalls => ToolCalls.Count > 0;

    /// <summary>Why the turn ended. Drives auto-continuation when the token limit truncated the text.</summary>
    public StopReason StopReason { get; set; } = StopReason.Stop;
}
