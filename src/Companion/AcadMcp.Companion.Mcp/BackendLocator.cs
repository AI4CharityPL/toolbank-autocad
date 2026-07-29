using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AcadMcp.Companion.Mcp;

/// <summary>
/// Resolves the path to the bundled AutoCAD tool-bank server executable.
/// Search order: explicit option -> ACADMCP_BACKEND env -> next to this assembly
/// (the deployed bundle layout) -> dev build output under the repo.
/// </summary>
public static class BackendLocator
{
    private const string ExeName = "AcadMcp.Backend.exe";
    private const string DllName = "AcadMcp.Backend.dll";

    public static string? Resolve(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        var env = Environment.GetEnvironmentVariable("ACADMCP_BACKEND");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        // When NETLOAD'd, AppContext.BaseDirectory is AutoCAD's exe dir, NOT the bundle's
        // Contents folder. Probe the directory this assembly was actually loaded from first.
        foreach (var baseDir in CandidateBaseDirs())
        {
            foreach (var candidate in new[] { Path.Combine(baseDir, ExeName), Path.Combine(baseDir, DllName) })
            {
                if (File.Exists(candidate)) return candidate;
            }
        }

        // Dev fallback: walk up to repo root and probe the backend build output.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var probe = Path.Combine(dir.FullName, "src", "AcadMcp.Backend", "bin");
            if (Directory.Exists(probe))
            {
                foreach (var name in new[] { ExeName, DllName })
                {
                    var found = FindFirst(probe, name);
                    if (found is not null) return found;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateBaseDirs()
    {
        string? asmDir = null;
        try
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(loc)) asmDir = Path.GetDirectoryName(loc);
        }
        catch
        {
            // Location may be empty for dynamically loaded assemblies.
        }
        if (!string.IsNullOrEmpty(asmDir)) yield return asmDir!;
        yield return AppContext.BaseDirectory;
    }

    private static string? FindFirst(string root, string fileName)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
                return f;
        }
        catch
        {
            // Ignore inaccessible directories.
        }
        return null;
    }
}
