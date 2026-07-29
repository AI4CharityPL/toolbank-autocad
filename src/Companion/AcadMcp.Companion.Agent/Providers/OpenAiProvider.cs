using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent.Providers;

/// <summary>OpenAI Chat Completions provider with SSE streaming, tool calling and image generation.</summary>
public sealed class OpenAiProvider : IChatProvider, IImageGenerator
{
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";
    private const string ImageEndpoint = "https://api.openai.com/v1/images/generations";
    private const string ImageModel = "gpt-image-1";
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public ProviderKind Kind => ProviderKind.OpenAI;
    public bool CanGenerateImages => true;

    public OpenAiProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<(byte[] Bytes, string MediaType)> GenerateImageAsync(string prompt, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = ImageModel,
            ["prompt"] = prompt,
            ["size"] = "1024x1024",
            ["n"] = 1,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, ImageEndpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await ProviderHttp.EnsureSuccessAsync(resp, "OpenAI (image)", ct).ConfigureAwait(false);
        var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var b64 = root?["data"]?[0]?["b64_json"]?.GetValue<string>();
        if (string.IsNullOrEmpty(b64))
            throw new ProviderException("OpenAI nie zwróciło danych obrazu.");
        return (Convert.FromBase64String(b64!), "image/png");
    }

    public async Task<AssistantTurn> SendAsync(ChatRequest request, Action<string> onTextDelta, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["stream"] = true,
            ["max_completion_tokens"] = request.MaxTokens,
            ["messages"] = BuildMessages(request),
        };
        if (request.Tools.Count > 0)
        {
            body["tools"] = BuildTools(request.Tools);
            body["tool_choice"] = "auto";
        }

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await ProviderHttp.EnsureSuccessAsync(resp, "OpenAI", ct).ConfigureAwait(false);

        var turn = new AssistantTurn();
        var toolAcc = new Dictionary<int, ToolCallBuilder>();
        var textBuilder = new StringBuilder();

        await foreach (var data in SseStream.ReadDataLinesAsync(resp, request.StreamIdleTimeout, ct))
        {
            JsonObject? chunk;
            try { chunk = JsonNode.Parse(data) as JsonObject; }
            catch { continue; }
            var choice0 = chunk?["choices"]?[0] as JsonObject;
            var finish = choice0?["finish_reason"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(finish))
            {
                turn.StopReason = finish switch
                {
                    "length" => StopReason.Length,
                    "tool_calls" or "function_call" => StopReason.ToolCalls,
                    "stop" => StopReason.Stop,
                    _ => StopReason.Other,
                };
            }
            var delta = choice0?["delta"] as JsonObject;
            if (delta is null) continue;

            var content = delta["content"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(content))
            {
                textBuilder.Append(content);
                onTextDelta(content!);
            }

            if (delta["tool_calls"] is JsonArray calls)
            {
                foreach (var call in calls)
                {
                    if (call is not JsonObject co) continue;
                    int index = co["index"]?.GetValue<int>() ?? 0;
                    if (!toolAcc.TryGetValue(index, out var builder))
                    {
                        builder = new ToolCallBuilder();
                        toolAcc[index] = builder;
                    }
                    var id = co["id"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(id)) builder.Id = id!;
                    var fn = co["function"] as JsonObject;
                    var name = fn?["name"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(name)) builder.Name = name!;
                    var argFrag = fn?["arguments"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(argFrag)) builder.Arguments.Append(argFrag);
                }
            }
        }

        turn.Text = textBuilder.ToString();
        foreach (var kv in toolAcc)
        {
            var b = kv.Value;
            if (string.IsNullOrEmpty(b.Name)) continue;
            turn.ToolCalls.Add(new ToolCall
            {
                Id = string.IsNullOrEmpty(b.Id) ? $"call_{kv.Key}" : b.Id,
                Name = b.Name,
                Arguments = ProviderHttp.ParseArgs(b.Arguments.ToString()),
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
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = t.InputSchema.DeepClone(),
                },
            });
        }
        return arr;
    }

    private static JsonArray BuildMessages(ChatRequest request)
    {
        var arr = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
        };

        foreach (var msg in request.Messages)
        {
            switch (msg.Role)
            {
                case ChatRole.User:
                    arr.Add(new JsonObject { ["role"] = "user", ["content"] = BuildUserContent(msg) });
                    break;
                case ChatRole.Assistant:
                    arr.Add(BuildAssistantMessage(msg));
                    break;
                case ChatRole.Tool:
                    foreach (var r in msg.ToolResults)
                    {
                        arr.Add(new JsonObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = r.ToolCallId,
                            ["content"] = r.Content,
                        });
                    }
                    break;
            }
        }
        return arr;
    }

    private static JsonNode BuildUserContent(ChatMessage msg)
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
                        ["type"] = "image_url",
                        ["image_url"] = new JsonObject { ["url"] = $"data:{p.MediaType};base64,{p.Data}" },
                    });
                    break;
                case ContentKind.Document:
                    parts.Add(new JsonObject
                    {
                        ["type"] = "file",
                        ["file"] = new JsonObject
                        {
                            ["filename"] = p.FileName ?? "document.pdf",
                            ["file_data"] = $"data:{p.MediaType};base64,{p.Data}",
                        },
                    });
                    break;
            }
        }
        return parts;
    }

    private static JsonObject BuildAssistantMessage(ChatMessage msg)
    {
        var obj = new JsonObject { ["role"] = "assistant" };
        var text = ProviderHttp.JoinText(msg);
        obj["content"] = string.IsNullOrEmpty(text) ? null : text;
        if (msg.ToolCalls.Count > 0)
        {
            var calls = new JsonArray();
            foreach (var tc in msg.ToolCalls)
            {
                calls.Add(new JsonObject
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Arguments.ToJsonString(),
                    },
                });
            }
            obj["tool_calls"] = calls;
        }
        return obj;
    }

    private sealed class ToolCallBuilder
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public StringBuilder Arguments { get; } = new();
    }
}
