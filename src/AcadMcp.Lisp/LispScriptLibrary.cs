// Phase 0 stub. Real LISP execution path is implemented when the plugin gains a SendCommand handler.

using System.IO;
using System.Reflection;

namespace AcadMcp.Lisp;

/// <summary>Locates *.lsp / *.scr files copied to the assembly's Scripts/ output folder.</summary>
public static class LispScriptLibrary
{
    public static string ScriptsDirectory
    {
        get
        {
            var asmDir = Path.GetDirectoryName(typeof(LispScriptLibrary).Assembly.Location)
                         ?? Directory.GetCurrentDirectory();
            return Path.Combine(asmDir, "Scripts");
        }
    }

    public static string Resolve(string scriptFileName)
        => Path.Combine(ScriptsDirectory, scriptFileName);

    public static bool Exists(string scriptFileName)
        => File.Exists(Resolve(scriptFileName));
}
