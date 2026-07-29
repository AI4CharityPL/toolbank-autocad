using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent.Providers;

/// <summary>Anthropic Messages API provider with SSE streaming and tool use.</summary>
public sealed class AnthropicProvider : IChatProvider
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public ProviderKind Kind => ProviderKind.Anthropic;

    public AnthropicProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<AssistantTurn> SendAsync(ChatRequest request, Action<string> onTextDelta, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = request.Temperature,
            ["stream"] = true,
            ["system"] = request.SystemPrompt,
            ["messages"] = BuildMessages(request),
        };
        if (request.Tools.Count > 0) body["tools"] = BuildTools(request.Tools);

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        httpReq.Headers.Add("x-api-key", _apiKey);
        httpReq.Headers.Add("anthropic-version", AnthropicVersion);

        using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await ProviderHttp.EnsureSuccessAsync(resp, "Anthropic", ct).ConfigureAwait(false);

        var turn = new AssistantTurn();
        var textBuilder = new StringBuilder();
        var blocks = new Dictionary<int, BlockBuilder>();

        await foreach (var data in SseStream.ReadDataLinesAsync(resp, request.StreamIdleTimeout, ct))
        {
            JsonObject? evt;
            try { evt = JsonNode.Parse(data) as JsonObject; }
            catch { continue; }
            var type = evt?["type"]?.GetValue<string>();
            switch (type)
            {
                case "content_block_start":
                {
                    int index = evt!["index"]?.GetValue<int>() ?? 0;
                    var cb = evt["content_block"] as JsonObject;
                    var b = new BlockBuilder { Type = cb?["type"]?.GetValue<string>() ?? "text" };
                    if (b.Type == "tool_use")
                    {
                        b.Id = cb?["id"]?.GetValue<string>() ?? "";
                        b.Name = cb?["name"]?.GetValue<string>() ?? "";
                    }
                    blocks[index] = b;
                    break;
                }
                case "content_block_delta":
                {
                    int index = evt!["index"]?.GetValue<int>() ?? 0;
                    var delta = evt["delta"] as JsonObject;
                    var dType = delta?["type"]?.GetValue<string>();
                    if (dType == "text_delta")
                    {
                        var t = delta?["text"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(t)) { textBuilder.Append(t); onTextDelta(t!); }
                    }
                    else if (dType == "input_json_delta" && blocks.TryGetValue(index, out var b))
                    {
                        b.Json.Append(delta?["partial_json"]?.GetValue<string>() ?? "");
                    }
                    break;
                }
                case "message_delta":
                {
                    var stop = evt!["delta"]?["stop_reason"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(stop))
                    {
                        turn.StopReason = stop switch
                        {
                            "max_tokens" => StopReason.Length,
                            "tool_use" => StopReason.ToolCalls,
                            "end_turn" or "stop_sequence" => StopReason.Stop,
                            _ => StopReason.Other,
                        };
                    }
                    break;
                }
            }
        }

        turn.Text = textBuilder.ToString();
        foreach (var b in blocks.Values)
        {
            if (b.Type != "tool_use" || string.IsNullOrEmpty(b.Name)) continue;
            turn.ToolCalls.Add(new ToolCall
            {
                Id = string.IsNullOrEmpty(b.Id) ? Guid.NewGuid().ToString("N") : b.Id,
                Name = b.Name,
                Arguments = ProviderHttp.ParseArgs(b.Json.ToString()),
            });
        }
        return turn;
    }

    private static JsonArray BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        var arr = new JsonArray();
        foreach (var t in tools)
        {
            arr.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["input_schema"] = t.InputSchema.DeepClone(),
            });
        }
        return arr;
    }

    private static JsonArray BuildMessages(ChatRequest request)
    {
        var arr = new JsonArray();
        foreach (var msg in request.Messages)
        {
            switch (msg.Role)
            {
                case ChatRole.User:
                    arr.Add(new JsonObject { ["role"] = "user", ["content"] = BuildUserContent(msg) });
                    break;
                case ChatRole.Assistant:
                    arr.Add(new JsonObject { ["role"] = "assistant", ["content"] = BuildAssistantContent(msg) });
                    break;
                case ChatRole.Tool:
                    arr.Add(new JsonObject { ["role"] = "user", ["content"] = BuildToolResultContent(msg) });
                    break;
            }
        }
        return arr;
    }

    private static JsonArray BuildUserContent(ChatMessage msg)
    {
        var parts = new JsonArray();
        foreach (var p in msg.Content)
        {
            switch (p.Kind)
            {
                case ContentKind.Text:
                    parts.Add(new JsonObject { ["type"] = "text", ["text"] = p.Text ?? "" });
                    break;
                case ContentKind.Image:
                    parts.Add(new JsonObject
                    {
                        ["type"] = "image",
                        ["source"] = new JsonObject
                        {
                            ["type"] = "base64",
                            ["media_type"] = p.MediaType,
                            ["data"] = p.Data,
                        },
                    });
                    break;
                case ContentKind.Document:
                    parts.Add(new JsonObject
                    {
                        ["type"] = "document",
                        ["source"] = new JsonObject
                        {
                            ["type"] = "base64",
                            ["media_type"] = p.MediaType,
                            ["data"] = p.Data,
                        },
                    });
                    break;
            }
        }
        return parts;
    }

    private static JsonArray BuildAssistantContent(ChatMessage msg)
    {
        var parts = new JsonArray();
        var text = ProviderHttp.JoinText(msg);
        if (!string.IsNullOrEmpty(text))
            parts.Add(new JsonObject { ["type"] = "text", ["text"] = text });
        foreach (var tc in msg.ToolCalls)
        {
            parts.Add(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = tc.Id,
                ["name"] = tc.Name,
                ["input"] = tc.Arguments.DeepClone(),
            });
        }
        return parts;
    }

    private static JsonArray BuildToolResultContent(ChatMessage msg)
    {
        var parts = new JsonArray();
        foreach (var r in msg.ToolResults)
        {
            parts.Add(new JsonObject
            {
                ["type"] = "tool_result",
                ["tool_use_id"] = r.ToolCallId,
                ["content"] = r.Content,
                ["is_error"] = r.IsError,
            });
        }
        return parts;
    }

    private sealed class BlockBuilder
    {
        public string Type = "text";
        public string Id = string.Empty;
        public string Name = string.Empty;
        public StringBuilder Json { get; } = new();
    }
}
