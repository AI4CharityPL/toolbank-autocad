// Built-in tools shipped with the plugin itself (not via Backend categories).
// Phase 1 minimum:
//   _echo         - end-to-end smoke test (no AutoCAD APIs touched)
//   acad_status   - dispatches onto UI thread, returns DocumentStatusDto
//
// Real domain tools (draw_circle, ...) live in Backend categories and call into the
// plugin via additional registrations as Phase 2+ ships.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadMcp.Plugin.Tools;

internal static class BuiltinTools
{
    public static void Register(ToolHost host)
    {
        host.Register("_echo", EchoAsync);
        host.Register("acad_status", AcadStatusAsync);
    }

    private static Task<ToolDispatchResult> EchoAsync(JsonObject args, CancellationToken ct)
    {
        var echo = args["echo"]?.GetValue<string>() ?? "";
        var result = new JsonObject
        {
            ["echo"] = echo,
            ["pluginPid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
            ["onUiThread"] = UiThreadDispatcher.IsOnUiThread
        };
        return Task.FromResult(new ToolDispatchResult(true, result, null));
    }

    private static async Task<ToolDispatchResult> AcadStatusAsync(JsonObject args, CancellationToken ct)
    {
        try
        {
            var dto = await UiThreadDispatcher.Run(() =>
            {
                var docMgr = AcadApp.DocumentManager;
                var doc = docMgr?.MdiActiveDocument;
                string acadVersion = AcadApp.Version?.ToString() ?? "<unknown>";
                string productName = "AutoCAD";
                try { productName = (string)AcadApp.GetSystemVariable("PRODUCT"); } catch { }

                if (doc is null)
                {
                    return new DocumentStatusDto(
                        Alive: true,
                        AcadProductName: productName,
                        AcadVersion: acadVersion,
                        DocumentName: null,
                        ActiveLayer: null,
                        EntityCount: 0,
                        IsLT: false,
                        Vertical: null,
                        ModeBanner: "no-active-document");
                }

                int entityCount = 0;
                string activeLayer = "<unknown>";
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (var _ in ms) entityCount++;
                    activeLayer = doc.Database.Clayer != ObjectId.Null
                        ? ((LayerTableRecord)tr.GetObject(doc.Database.Clayer, OpenMode.ForRead)).Name
                        : "<none>";
                    tr.Commit();
                }

                return new DocumentStatusDto(
                    Alive: true,
                    AcadProductName: productName,
                    AcadVersion: acadVersion,
                    DocumentName: doc.Name,
                    ActiveLayer: activeLayer,
                    EntityCount: entityCount,
                    IsLT: false,
                    Vertical: null,
                    ModeBanner: "full");
            }, ct).ConfigureAwait(false);

            var node = JsonSerializer.SerializeToNode(dto) as JsonObject ?? new JsonObject();
            return new ToolDispatchResult(true, node, null);
        }
        catch (OperationCanceledException)
        {
            return new ToolDispatchResult(false, null, new ErrorInfo(AcadErrorCode.Timeout, "acad_status cancelled"));
        }
        catch (Exception ex)
        {
            Logging.Log.Error("acad_status failed", ex);
            return new ToolDispatchResult(false, null, new ErrorInfo(AcadErrorCode.AcadException, ex.Message));
        }
    }
}
