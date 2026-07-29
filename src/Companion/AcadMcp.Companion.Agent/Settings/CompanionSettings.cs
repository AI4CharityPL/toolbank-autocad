using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AcadMcp.Companion.Agent.Settings;

/// <summary>
/// Non-secret user preferences for the in-app assistant. API keys are NOT stored here;
/// they live encrypted via <see cref="SecureKeyStore"/>.
/// </summary>
public sealed class CompanionSettings
{
    public ProviderKind Provider { get; set; } = ProviderKind.Anthropic;

    /// <summary>Selected model id per provider. Editable by the user (BYOK + bring-your-own-model).</summary>
    public Dictionary<ProviderKind, string> Models { get; set; } = new()
    {
        // Sensible latest-generation defaults; user can override in Settings.
        [ProviderKind.OpenAI] = "gpt-5.1",
        [ProviderKind.Anthropic] = "claude-opus-4-6",
        [ProviderKind.Gemini] = "gemini-3-pro",
    };

    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.2;

    /// <summary>Max tool-calling round trips per user message before the agent must answer.</summary>
    public int MaxToolIterations { get; set; } = 24;

    /// <summary>
    /// Max seconds to wait for the next streamed token before the turn is aborted as stalled.
    /// This is the guard that prevents the chat from hanging forever on a silent provider stream.
    /// </summary>
    public int StreamIdleTimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Hard ceiling for a single model turn (one request/response, including streaming). On timeout
    /// the orchestrator retries once, then surfaces a clean error instead of freezing.
    /// </summary>
    public int TurnTimeoutSeconds { get; set; } = 240;

    /// <summary>
    /// When true, a planner pass produces a step list first, then executor passes run each step
    /// sequentially (like Cursor's plan/agent split). Off = single agent loop.
    /// </summary>
    public bool PlanMode { get; set; }

    /// <summary>Named pipe the tool-bank server uses to reach AutoCAD.</summary>
    public string PipeName { get; set; } = "acadmcp";

    public string ModelFor(ProviderKind kind)
        => Models.TryGetValue(kind, out var m) && !string.IsNullOrWhiteSpace(m) ? m : DefaultModel(kind);

    public static string DefaultModel(ProviderKind kind) => kind switch
    {
        ProviderKind.OpenAI => "gpt-5.1",
        ProviderKind.Anthropic => "claude-opus-4-6",
        ProviderKind.Gemini => "gemini-3-pro",
        _ => "",
    };

    // ─────────── persistence ───────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SettingsPath =>
        Path.Combine(AppPaths.DataDir, "settings.json");

    public static CompanionSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<CompanionSettings>(json, JsonOpts);
                if (loaded is not null)
                {
                    // Backfill any model keys missing from an older settings file.
                    foreach (ProviderKind k in Enum.GetValues<ProviderKind>())
                        loaded.Models.TryAdd(k, DefaultModel(k));
                    return loaded;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable file -> fall back to defaults.
        }
        return new CompanionSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
    }
}

/// <summary>Shared on-disk locations for the product (per Windows user).</summary>
public static class AppPaths
{
    public const string ProductFolder = "AutoCAD AI";

    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolder);
}
