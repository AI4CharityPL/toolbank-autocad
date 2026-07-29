// Liveness file at %LOCALAPPDATA%\AcadMcp\plugin.alive.
// detect-autocad.ps1 and other tooling read this to prove the plugin is loaded without IPC.
// Updated every 30 s while alive; deleted on graceful Terminate.
//
// See rule 16-acad-plugin-lifecycle.md.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using AcadMcp.Plugin.Logging;

namespace AcadMcp.Plugin.Threading;

internal sealed class HeartbeatFile : IDisposable
{
    public static string DefaultPath
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "AcadMcp", "plugin.alive");
        }
    }

    private readonly string _path;
    private readonly Timer _timer;
    private readonly int _processId;
    private readonly DateTime _startedUtc;
    private readonly string _pipeName;
    private readonly string _pluginVersion;

    public HeartbeatFile(string pipeName, string pluginVersion, string? path = null)
    {
        _path = path ?? DefaultPath;
        _pipeName = pipeName;
        _pluginVersion = pluginVersion;
        _processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        _startedUtc = DateTime.UtcNow;

        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); } catch { }
        WriteOnce();
        _timer = new Timer(_ => WriteOnce(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void WriteOnce()
    {
        try
        {
            var payload = new
            {
                processId = _processId,
                pipeName = _pipeName,
                pluginVersion = _pluginVersion,
                startedUtc = _startedUtc.ToString("o"),
                lastTickUtc = DateTime.UtcNow.ToString("o")
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Log.Warn($"HeartbeatFile write failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try { _timer.Dispose(); } catch { }
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
