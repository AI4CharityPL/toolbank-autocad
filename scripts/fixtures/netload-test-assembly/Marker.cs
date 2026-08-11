namespace AcadMcpNetloadTest;

// Deliberately trivial and inert - no AutoCAD references, no static constructors, no commands.
// Its only purpose is to be a known, fully-controlled quantity that netload_assembly can load
// and this project can then find via .NET reflection on the current process.
public static class Marker
{
    public const string Signature = "acadmcp-netload-verify-2026-08-11";
}
