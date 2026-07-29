// Walks up the directory tree from the current binary to find the repo root.
// Repo root has a `mcpbank-manifests/` folder OR a `.git/` folder.

using System.IO;

namespace AcadMcp.Backend.Mcp;

public static class RepoRootDetector
{
    public static string Detect()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "mcpbank-manifests"))
                || Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return System.Environment.CurrentDirectory;
    }
}
