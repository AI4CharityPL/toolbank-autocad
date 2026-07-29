// Lightweight file logger for the plugin. No System.Console (no console attached inside AutoCAD).
// Writes to %LOCALAPPDATA%\AcadMcp\logs\plugin-yyyymmdd.log with daily rolling, 7-day retention.
// See rule 16-acad-plugin-lifecycle.md.

using System;
using System.IO;
using System.Text;

namespace AcadMcp.Plugin.Logging;

internal static class Log
{
    private static readonly object _gate = new();
    private static string? _logDir;
    private const int RetentionDays = 7;

    public static string LogDir
    {
        get
        {
            if (_logDir is not null) return _logDir;
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDir = Path.Combine(local, "AcadMcp", "logs");
            try { Directory.CreateDirectory(_logDir); } catch { }
            return _logDir;
        }
    }

    public static void Info(string msg) => Write("INFO ", msg, null);
    public static void Warn(string msg) => Write("WARN ", msg, null);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);
    public static void Debug(string msg) => Write("DEBUG", msg, null);

    private static void Write(string level, string msg, Exception? ex)
    {
        try
        {
            var stamp = DateTime.UtcNow;
            var line = new StringBuilder()
                .Append(stamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
                .Append(' ').Append(level)
                .Append(" [").Append(System.Threading.Thread.CurrentThread.ManagedThreadId).Append("] ")
                .Append(msg);
            if (ex is not null)
            {
                line.Append(" | ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
                if (ex.StackTrace is not null) line.Append(" | ").Append(ex.StackTrace.Replace("\r", "").Replace("\n", " | "));
            }
            line.AppendLine();

            var file = Path.Combine(LogDir, $"plugin-{stamp:yyyyMMdd}.log");
            lock (_gate)
            {
                File.AppendAllText(file, line.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    public static void PruneOld()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            foreach (var f in Directory.GetFiles(LogDir, "plugin-*.log"))
            {
                if (File.GetLastWriteTimeUtc(f) < cutoff)
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
        catch
        {
        }
    }
}
