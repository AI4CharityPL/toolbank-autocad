// COM/ActiveX bridge to AutoCAD. Used when the .NET plugin is unavailable (LT) or as recovery.
// Phase 0 stub - real implementation grows incrementally with categories that opt into ComFallback.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using AcadMcp.Shared;

namespace AcadMcp.ComBridge;

/// <summary>
/// Thin wrapper around AutoCAD's <c>AutoCAD.Application</c> COM ProgID.
/// Activates an existing AutoCAD instance (via Running Object Table) or starts one.
/// All work happens on the calling STA thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ComConnection : IDisposable
{
    private object? _app;
    private bool _disposed;

    public string ProgId { get; }

    public ComConnection(string progId = "AutoCAD.Application")
    {
        ProgId = progId;
    }

    /// <summary>Connect to a running AutoCAD instance via Running Object Table. Returns false if none.</summary>
    public bool TryConnectExisting()
    {
        try
        {
            _app = MarshalCompat.GetActiveObject(ProgId);
            return _app is not null;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>Get a lightweight status snapshot via COM. Used as fallback for acad_status.</summary>
    public DocumentStatusDto Status()
    {
        if (_app is null)
        {
            return new DocumentStatusDto(
                Alive: false,
                ModeBanner: "ComBridge: no AutoCAD COM connection. Plugin path is preferred.");
        }

        try
        {
            string version = (string?)_app.GetType().InvokeMember("Version",
                System.Reflection.BindingFlags.GetProperty, null, _app, null) ?? "?";
            string fullName = (string?)_app.GetType().InvokeMember("FullName",
                System.Reflection.BindingFlags.GetProperty, null, _app, null) ?? "?";

            return new DocumentStatusDto(
                Alive: true,
                AcadProductName: fullName,
                AcadVersion: version,
                ModeBanner: "ComBridge active (limited capability vs. plugin)");
        }
        catch (Exception ex)
        {
            return new DocumentStatusDto(
                Alive: false,
                ModeBanner: $"ComBridge error: {ex.GetType().Name}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_app is not null)
        {
            try { Marshal.ReleaseComObject(_app); } catch { }
            _app = null;
        }
        _disposed = true;
    }
}

/// <summary>
/// Replacement for <c>Marshal.GetActiveObject</c> which was removed in .NET (Core)+. 
/// Uses <c>GetRunningObjectTable</c> + <c>CreateBindCtx</c> via ole32.dll.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class MarshalCompat
{
    public static object GetActiveObject(string progId)
    {
        var clsid = CLSIDFromProgID(progId);
        var moniker = CreateClassMoniker(clsid);
        var rot = GetRunningObjectTable();
        try
        {
            int hr = rot.GetObject(moniker, out object? obj);
            if (hr != 0 || obj is null)
            {
                throw new COMException($"No running instance of {progId} found in ROT (hr=0x{hr:X8})", hr);
            }
            return obj;
        }
        finally
        {
            Marshal.ReleaseComObject(rot);
            Marshal.ReleaseComObject(moniker);
        }
    }

    private static Guid CLSIDFromProgID(string progId)
    {
        int hr = NativeMethods.CLSIDFromProgID(progId, out Guid clsid);
        if (hr != 0) throw new COMException($"CLSIDFromProgID('{progId}') failed", hr);
        return clsid;
    }

    private static IMoniker CreateClassMoniker(Guid clsid)
    {
        int hr = NativeMethods.CreateClassMoniker(ref clsid, out IMoniker mk);
        if (hr != 0) throw new COMException("CreateClassMoniker failed", hr);
        return mk;
    }

    private static IRunningObjectTable GetRunningObjectTable()
    {
        int hr = NativeMethods.GetRunningObjectTable(0, out IRunningObjectTable rot);
        if (hr != 0) throw new COMException("GetRunningObjectTable failed", hr);
        return rot;
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll", PreserveSig = false, CharSet = CharSet.Unicode)]
        public static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

        [DllImport("ole32.dll", PreserveSig = false)]
        public static extern int CreateClassMoniker(ref Guid rclsid, out IMoniker ppmk);

        [DllImport("ole32.dll", PreserveSig = false)]
        public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);
    }
}
