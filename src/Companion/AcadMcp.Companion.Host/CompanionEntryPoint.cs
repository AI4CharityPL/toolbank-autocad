using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadRuntime = Autodesk.AutoCAD.Runtime;

[assembly: AcadRuntime.ExtensionApplication(typeof(AcadMcp.Companion.Host.CompanionEntryPoint))]
[assembly: AcadRuntime.CommandClass(typeof(AcadMcp.Companion.Host.CompanionEntryPoint))]

namespace AcadMcp.Companion.Host;

/// <summary>
/// NETLOAD entry point for the in-app AI assistant. Registers the ACADAI command which opens
/// the chat palette. This product is independent of the editor integration: it talks to the
/// AutoCAD tool bank through its own out-of-process server.
/// </summary>
public sealed class CompanionEntryPoint : AcadRuntime.IExtensionApplication
{
    public void Initialize()
    {
        CompanionLog.Info("=== CompanionEntryPoint.Initialize ===");
        WriteToCommandLine("Asystent AI gotowy. Wpisz ACADAI, aby otworzyć panel czatu.");
    }

    public void Terminate()
    {
        try
        {
            var vm = ChatPalette.View?.ViewModel;
            vm?.ShutdownAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort cleanup on AutoCAD shutdown.
        }
    }

    [AcadRuntime.CommandMethod("ACADAI", AcadRuntime.CommandFlags.Session)]
    public void ShowAssistant()
    {
        CompanionLog.Info("ACADAI command invoked");
        try
        {
            ChatPalette.Show();
            CompanionLog.Info("ChatPalette.Show completed OK");
            WriteToCommandLine("Asystent AI: panel otwarty. Jeśli go nie widzisz, sprawdź prawą krawędź okna AutoCAD.");
        }
        catch (Exception ex)
        {
            CompanionLog.Error("ACADAI failed to open palette", ex);
            WriteToCommandLine($"Asystent AI: nie udało się otworzyć panelu: {ex.Message}");
            if (ex.InnerException is not null)
                WriteToCommandLine($"  szczegóły: {ex.InnerException.Message}");
        }
    }

    private static void WriteToCommandLine(string msg)
    {
        try
        {
            var doc = AcadApp.DocumentManager?.MdiActiveDocument;
            doc?.Editor?.WriteMessage($"\n{msg}\n");
        }
        catch
        {
            // No active document/editor yet - safe to ignore.
        }
    }
}
