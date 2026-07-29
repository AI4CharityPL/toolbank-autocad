using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent.Providers;

/// <summary>Google Gemini (Generative Language API) provider with SSE streaming, function calling and image generation.</summary>
public sealed class GeminiProvider : IChatProvider, IImageGenerator
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string ImageModel = "gemini-2.5-flash-image";
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public ProviderKind Kind => ProviderKind.Gemini;
    public bool CanGenerateImages => true;

    public GeminiProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<(byte[] Bytes, string MediaType)> GenerateImageAsync(string prompt, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } } } },
        };
        var url = $"{BaseUrl}{Uri.EscapeDataString(ImageModel)}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await ProviderHttp.EnsureSuccessAsync(resp, "Gemini (image)", ct).ConfigureAwait(false);
        var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var parts = root?["candidates"]?[0]?["content"]?["parts"] as JsonArray;
        if (parts is not null)
        {
            foreach (var p in parts.OfType<JsonObject>())
            {
                var inline = p["inlineData"] as JsonObject ?? p["inline_data"] as JsonObject;
                var data = inline?["data"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(data))
                {
                    var mime = inline?["mimeType"]?.GetValue<string>() ?? inline?["mime_type"]?.GetValue<string>() ?? "image/png";
                    return (Convert.FromBase64String(data!), mime);
                }
            }
        }
        throw new ProviderException("Gemini nie zwróciło danych obrazu.");
    }

    public async Task<AssistantTurn> SendAsync(ChatRequest request, Action<string> onTextDelta, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = request.SystemPrompt } } },
            ["contents"] = BuildContents(request),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = request.Temperature,
                ["maxOutputTokens"] = request.MaxTokens,
            },
        };
        if (request.Tools.Count > 0) body["tools"] = BuildTools(request.Tools);

        var url = $"{BaseUrl}{Uri.EscapeDataString(request.Model)}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(_apiKey)}";
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await ProviderHttp.EnsureSuccessAsync(resp, "Gemini", ct).ConfigureAwait(false);

        var turn = new AssistantTurn();
        var textBuilder = new StringBuilder();

        await foreach (var data in SseStream.ReadDataLinesAsync(resp, request.StreamIdleTimeout, ct))
        {
            JsonObject? chunk;
            try { chunk = JsonNode.Parse(data) as JsonObject; }
            catch { continue; }
            var cand0 = chunk?["candidates"]?[0] as JsonObject;
            var finish = cand0?["finishReason"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(finish))
            {
                turn.StopReason = finish switch
                {
                    "MAX_TOKENS" => StopReason.Length,
                    "STOP" => StopReason.Stop,
                    _ => StopReason.Other,
                };
            }
            var parts = cand0?["content"]?["parts"] as JsonArray;
            if (parts is null) continue;

            foreach (var part in parts)
            {
                if (part is not JsonObject po) continue;
                var text = po["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text)) { textBuilder.Append(text); onTextDelta(text!); }

                if (po["functionCall"] is JsonObject fc)
                {
                    var name = fc["name"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(name)) continue;
                    turn.ToolCalls.Add(new ToolCall
                    {
                        Id = name!,
                        Name = name!,
                        Arguments = fc["args"] as JsonObject is { } a ? a.DeepClone().AsObject() : new JsonObject(),
                    });
                }
            }
        }

        turn.Text = textBuilder.ToString();
        return turn;
    }

    private static JsonArray BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        var decls = new JsonArray();
        foreach (var t in tools)
        {
            decls.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = SanitizeSchema(t.InputSchema),
            });
        }
        return new JsonArray { new JsonObject { ["functionDeclarations"] = decls } };
    }

    private static JsonArray BuildContents(ChatRequest request)
    {
        var arr = new JsonArray();
        foreach (var msg in request.Messages)
        {
            switch (msg.Role)
            {
                case ChatRole.User:
                    arr.Add(new JsonObject { ["role"] = "user", ["parts"] = BuildUserParts(msg) });
                    break;
                case ChatRole.Assistant:
                    arr.Add(new JsonObject { ["role"] = "model", ["parts"] = BuildModelParts(msg) });
                    break;
                case ChatRole.Tool:
                    arr.Add(new JsonObject { ["role"] = "user", ["parts"] = BuildToolResultParts(msg) });
                    break;
            }
        }
        return arr;
    }

    private static JsonArray BuildUserParts(ChatMessage msg)
    {
        var parts = new JsonArray();
        foreach (var p in msg.Content)
        {
            switch (p.Kind)
            {
                case ContentKind.Text:
                    parts.Add(new JsonObject { ["text"] = p.Text ?? "" });
                    break;
                case ContentKind.Image:
                case ContentKind.Document:
                    parts.Add(new JsonObject
                    {
                        ["inlineData"] = new JsonObject { ["mimeType"] = p.MediaType, ["data"] = p.Data },
                    });
                    break;
            }
        }
        return parts;
    }

    private static JsonArray BuildModelParts(ChatMessage msg)
    {
        var parts = new JsonArray();
        var text = ProviderHttp.JoinText(msg);
        if (!string.IsNullOrEmpty(text)) parts.Add(new JsonObject { ["text"] = text });
        foreach (var tc in msg.ToolCalls)
        {
            parts.Add(new JsonObject
            {
                ["functionCall"] = new JsonObject { ["name"] = tc.Name, ["args"] = tc.Arguments.DeepClone() },
            });
        }
        return parts;
    }

    private static JsonArray BuildToolResultParts(ChatMessage msg)
    {
        var parts = new JsonArray();
        foreach (var r in msg.ToolResults)
        {
            parts.Add(new JsonObject
            {
                ["functionResponse"] = new JsonObject
                {
                    ["name"] = r.Name ?? r.ToolCallId,
                    ["response"] = new JsonObject { ["result"] = r.Content },
                },
            });
        }
        return parts;
    }

    /// <summary>
    /// Gemini's function-declaration schema is OpenAPI-flavoured and rejects JSON-schema-only
    /// keywords like <c>additionalProperties</c>. Strip them defensively.
    /// </summary>
    private static JsonNode SanitizeSchema(JsonObject schema)
    {
        var clone = schema.DeepClone().AsObject();
        Strip(clone);
        return clone;

        static void Strip(JsonObject obj)
        {
            obj.Remove("additionalProperties");
            obj.Remove("$schema");
            if (obj["properties"] is JsonObject props)
            {
                foreach (var kv in props)
                {
                    if (kv.Value is JsonObject child) Strip(child);
                }
            }
            if (obj["items"] is JsonObject items) Strip(items);
        }
    }
}
