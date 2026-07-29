// Custom logger provider that writes ALL log output to Console.Error (stderr).
//
// WHY THIS EXISTS:
// AcadMcp.Backend communicates via stdio JSON-RPC (MCP protocol). the MCP client
// reads stdout and expects ONLY newline-delimited JSON. Any non-JSON bytes on stdout
// (e.g. "18:02:02 info: AcadMcp...") cause the MCP parser to fail with "Not connected".
//
// AddSimpleConsole() writes to Console.Out (stdout) by default. This provider replaces
// it with a stderr-only logger so the JSON-RPC pipe stays clean.

using System;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Logging;

internal sealed class StderrLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;

    public StderrLoggerProvider(LogLevel minLevel = LogLevel.Information)
        => _minLevel = minLevel;

    public ILogger CreateLogger(string categoryName)
        => new StderrLogger(categoryName, _minLevel);

    public void Dispose() { }
}

internal sealed class StderrLogger : ILogger
{
    private readonly string _shortName;
    private readonly LogLevel _minLevel;

    public StderrLogger(string categoryName, LogLevel minLevel)
    {
        _minLevel = minLevel;
        var parts = categoryName.Split('.');
        _shortName = parts[^1];
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var level = logLevel switch
        {
            LogLevel.Trace       => "TRC",
            LogLevel.Debug       => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning     => "WRN",
            LogLevel.Error       => "ERR",
            LogLevel.Critical    => "CRT",
            _                    => "   ",
        };
        var msg = formatter(state, exception);
        Console.Error.WriteLine($"{DateTime.Now:HH:mm:ss} {level} [{_shortName}] {msg}");
        if (exception is not null)
            Console.Error.WriteLine(exception.ToString());
    }
}
