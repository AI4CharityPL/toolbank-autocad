using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AcadMcp.Companion.Agent.Reports;

/// <summary>Extracts the first Markdown table from text and renders it as CSV (RFC 4180).</summary>
public static class MarkdownTable
{
    /// <summary>
    /// Returns CSV for the first GitHub-flavoured Markdown table found in <paramref name="text"/>,
    /// or null if no table is present.
    /// </summary>
    public static string? ToCsv(string text)
    {
        var rows = ExtractRows(text);
        if (rows is null || rows.Count == 0) return null;

        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }
        return sb.ToString();
    }

    public static bool ContainsTable(string text) => ExtractRows(text) is { Count: > 0 };

    private static List<List<string>>? ExtractRows(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var table = new List<List<string>>();
        bool inTable = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            bool looksLikeRow = line.StartsWith('|') && line.Contains('|', StringComparison.Ordinal);

            if (looksLikeRow)
            {
                if (IsSeparator(line)) { inTable = true; continue; }
                table.Add(SplitCells(line));
                inTable = true;
            }
            else if (inTable)
            {
                break; // table ended
            }
        }

        return table.Count > 0 ? table : null;
    }

    private static bool IsSeparator(string line)
    {
        var cells = SplitCells(line);
        return cells.Count > 0 && cells.All(c =>
            c.Length > 0 && c.All(ch => ch is '-' or ':' or ' '));
    }

    private static List<string> SplitCells(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith('|')) trimmed = trimmed.Substring(0, trimmed.Length - 1);
        return trimmed.Split('|').Select(c => c.Trim()).ToList();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
