using System;
using System.IO;
using System.Text;

namespace AcadMcp.Companion.Host;

/// <summary>
/// Lightweight file logger for the in-app assistant. Writes to
/// %LOCALAPPDATA%\AcadMcp\logs\companion-yyyymmdd.log so we can diagnose load/UI issues
/// without a console (none attached inside AutoCAD).
/// </summary>
internal static class CompanionLog
{
    private static readonly object Gate = new();

    private static string LogDir
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(local, "AcadMcp", "logs");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }

    public static void Info(string msg) => Write("INFO ", msg, null);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

    private static void Write(string level, string msg, Exception? ex)
    {
        try
        {
            var stamp = DateTime.UtcNow;
            var line = new StringBuilder()
                .Append(stamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
                .Append(' ').Append(level).Append(' ').Append(msg);
            if (ex is not null)
            {
                line.Append(" | ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
                if (ex.StackTrace is not null)
                    line.Append(" | ").Append(ex.StackTrace.Replace("\r", "").Replace("\n", " | "));
            }
            line.AppendLine();
            var file = Path.Combine(LogDir, $"companion-{stamp:yyyyMMdd}.log");
            lock (Gate) { File.AppendAllText(file, line.ToString(), Encoding.UTF8); }
        }
        catch
        {
            // Never throw from logging.
        }
    }
}
