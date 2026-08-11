// AutoCAD plugin entry point. Loaded via NETLOAD.
//
// Lifecycle (rule 16-acad-plugin-lifecycle.md):
//   1. Initialize() runs on AutoCAD's UI thread. We capture SynchronizationContext,
//      register built-in tools, start the named pipe server, write heartbeat file.
//   2. ACADMCP_STATUS / ACADMCP_PING commands are exposed for in-AutoCAD diagnostics.
//   3. Terminate() is called on AutoCAD exit or NETUNLOAD; we drain in-flight work,
//      stop the pipe server (5 s drain), delete heartbeat file, log offline.
//
// All AutoCAD API access goes through UiThreadDispatcher (rule 10).

using System;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Logging;
using AcadMcp.Plugin.Pipe;
using AcadMcp.Plugin.Threading;
using AcadMcp.Plugin.Tools;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadRuntime = Autodesk.AutoCAD.Runtime;

[assembly: AcadRuntime.ExtensionApplication(typeof(AcadMcp.Plugin.PluginEntryPoint))]
[assembly: AcadRuntime.CommandClass(typeof(AcadMcp.Plugin.PluginEntryPoint))]

namespace AcadMcp.Plugin;

public sealed class PluginEntryPoint : AcadRuntime.IExtensionApplication
{
    private static int _initialized;
    private static NamedPipeServer? _pipeServer;
    private static HeartbeatFile? _heartbeat;
    private static ToolHost? _toolHost;
    private static DateTime _startedUtc;
    private static string? _lastError;

    public void Initialize()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
        {
            Log.Warn("Initialize called twice - second call ignored");
            WriteToCommandLine("AcadMcp: already initialized");
            return;
        }

        try
        {
            _startedUtc = DateTime.UtcNow;
            Log.PruneOld();
            Log.Info("=== AcadMcp.Plugin Initialize ===");
            Log.Info($"Plugin version: {GetPluginVersion()}");
            Log.Info($"Process: PID={System.Diagnostics.Process.GetCurrentProcess().Id}, runtime={Environment.Version}");

            UiThreadDispatcher.Capture();

            _toolHost = new ToolHost();
            BuiltinTools.Register(_toolHost);
            Geometry2dPluginTools.Register(_toolHost);
            Geometry3dPluginTools.Register(_toolHost);
            SurfacesPluginTools.Register(_toolHost);
            MeshPluginTools.Register(_toolHost);
            Sections3dPluginTools.Register(_toolHost);
            LispPluginTools.Register(_toolHost);
            DataPluginTools.Register(_toolHost);
            DataQueryPluginTools.Register(_toolHost);
            DataLinkPluginTools.Register(_toolHost);
            ViewsPluginTools.Register(_toolHost);
            SelectionExtPluginTools.Register(_toolHost);
            GeoPluginTools.Register(_toolHost);
            MaterialsPluginTools.Register(_toolHost);
            BooleanOpsPluginTools.Register(_toolHost);
            ModifyPluginTools.Register(_toolHost);
            SelectionPluginTools.Register(_toolHost);
            LayersPluginTools.Register(_toolHost);
            DimensionsPluginTools.Register(_toolHost);
            AnnotationsPluginTools.Register(_toolHost);
            BlocksPluginTools.Register(_toolHost);
            LayoutsPluginTools.Register(_toolHost);
            FilesPluginTools.Register(_toolHost);
            XrefsPluginTools.Register(_toolHost);

            // The only COM category in this bank. See docs/engineering-rules/45-sheet-sets-com.md.
            SheetSetsPluginTools.Register(_toolHost);
            ViewportsPluginTools.Register(_toolHost);
            PublishPluginTools.Register(_toolHost);
            StylesPluginTools.Register(_toolHost);
            UcsPluginTools.Register(_toolHost);
            FieldsPluginTools.Register(_toolHost);
            AnnotativePluginTools.Register(_toolHost);
            ValidatorsPluginTools.Register(_toolHost);
            ParametricPluginTools.Register(_toolHost);
            CheckpointPluginTools.Register(_toolHost);
            ViewPluginTools.Register(_toolHost);
            HatchesPluginTools.Register(_toolHost);
            FurniturePluginTools.Register(_toolHost);
            PlumbingPluginTools.Register(_toolHost);
            OpeningsPluginTools.Register(_toolHost);
            SchedulesPluginTools.Register(_toolHost);
            LivestreamPluginTools.Register(_toolHost);
            Log.Info($"Tools registered ({_toolHost.RegisteredTools.Count}): {string.Join(", ", _toolHost.RegisteredTools)}");

            var pipeName = Environment.GetEnvironmentVariable("ACADMCP_PIPE");
            if (string.IsNullOrWhiteSpace(pipeName)) pipeName = PipeProtocol.PipeName;

            _pipeServer = new NamedPipeServer(pipeName!, _toolHost, BuildHandshakeResponse);
            _pipeServer.Start();

            _heartbeat = new HeartbeatFile(pipeName!, GetPluginVersion());

            WriteToCommandLine($"AcadMcp plugin online on \\\\.\\pipe\\{pipeName}  (v{GetPluginVersion()})");
            WriteToCommandLine("AcadMcp commands: ACADMCP_STATUS, ACADMCP_PING");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            Log.Error("Initialize failed", ex);
            WriteToCommandLine($"AcadMcp FAILED to initialize: {ex.Message}");
        }
    }

    public void Terminate()
    {
        try
        {
            Log.Info("=== AcadMcp.Plugin Terminate ===");
            if (_pipeServer is not null)
            {
                try { _pipeServer.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); } catch { }
                _pipeServer.Dispose();
            }
            _heartbeat?.Dispose();
            Log.Info("AcadMcp plugin offline");
        }
        catch (Exception ex)
        {
            try { Log.Error("Terminate failed", ex); } catch { }
        }
    }

    private static HandshakeResponse BuildHandshakeResponse(HandshakeRequest req)
    {
        try
        {
            string acadVersion = "<unknown>";
            string? vertical = null;
            bool isLT = false;
            try
            {
                acadVersion = AcadApp.Version?.ToString() ?? "<unknown>";
                vertical = TryGetVertical();
                isLT = TryDetectLT();
            }
            catch (Exception ex)
            {
                Log.Warn($"Handshake: AutoCAD version probe failed: {ex.Message}");
            }

            return new HandshakeResponse(
                Ok: true,
                PluginVersion: GetPluginVersion(),
                AcadVersion: acadVersion,
                AcadVertical: vertical,
                IsLT: isLT,
                NegotiatedProtocolVersion: PipeProtocol.CurrentVersion);
        }
        catch (Exception ex)
        {
            Log.Error("BuildHandshakeResponse failed", ex);
            return new HandshakeResponse(
                Ok: false,
                PluginVersion: GetPluginVersion(),
                AcadVersion: "<error>",
                AcadVertical: null,
                IsLT: false,
                NegotiatedProtocolVersion: 0,
                Error: new ErrorInfo(AcadErrorCode.InternalError, ex.Message));
        }
    }

    private static string? TryGetVertical()
    {
        try
        {
            var name = AcadApp.Version?.Major switch
            {
                <= 24 => null,
                _ => "vanilla"
            };
            return name;
        }
        catch { return null; }
    }

    private static bool TryDetectLT()
    {
        try
        {
            var product = Assembly.GetEntryAssembly()?.GetName().Name ?? "";
            return product.IndexOf("acadlt", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    private static string GetPluginVersion()
    {
        try
        {
            var asm = typeof(PluginEntryPoint).Assembly;
            var v = asm.GetName().Version;
            return v?.ToString(3) ?? "0.1.0";
        }
        catch { return "0.1.0"; }
    }

    private static void WriteToCommandLine(string msg)
    {
        try
        {
            var doc = AcadApp.DocumentManager?.MdiActiveDocument;
            doc?.Editor?.WriteMessage($"\n{msg}\n");
        }
        catch { }
    }

    [AcadRuntime.CommandMethod("ACADMCP_STATUS")]
    public void StatusCommand()
    {
        try
        {
            var uptime = DateTime.UtcNow - _startedUtc;
            var sessions = _pipeServer?.ActiveSessions ?? -1;
            var maxObserved = _pipeServer?.MaxConcurrentObserved ?? 0;
            var toolCount = _toolHost?.RegisteredTools.Count ?? 0;
            WriteToCommandLine(
                $"AcadMcp v{GetPluginVersion()}  pipe=\\\\.\\pipe\\{_pipeServer?.PipeName ?? "<offline>"}  " +
                $"uptime={uptime:hh\\:mm\\:ss}  active_sessions={sessions}  max_observed={maxObserved}  " +
                $"plugin_tools={toolCount}  last_error={_lastError ?? "(none)"}");
        }
        catch (Exception ex)
        {
            WriteToCommandLine($"AcadMcp STATUS failed: {ex.Message}");
        }
    }

    [AcadRuntime.CommandMethod("ACADMCP_PING")]
    public void PingCommand() => WriteToCommandLine("AcadMcp pong");
}
