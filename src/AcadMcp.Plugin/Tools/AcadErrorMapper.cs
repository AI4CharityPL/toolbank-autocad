// Maps AutoCAD/CLR exceptions to AcadErrorCode + safe message. Never leaks stack traces.
// See rule 12-acad-error-mapping.mdc.

using System;
using AcadMcp.Plugin.Logging;
using AcadMcp.Shared;
using AcadRuntime = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class AcadErrorMapper
{
    public static ToolDispatchResult Fail(string toolKey, Exception ex)
    {
        Log.Error($"Tool '{toolKey}' failed", ex);
        var (code, msg, hint) = Map(ex);
        return new ToolDispatchResult(false, null, new ErrorInfo(code, msg, hint));
    }

    public static (AcadErrorCode Code, string Message, string? Hint) Map(Exception ex)
    {
        switch (ex)
        {
            case ArgumentException ae:
                return (AcadErrorCode.InvalidArgument, ae.Message, "Check argument types and ranges.");
            case OperationCanceledException:
                return (AcadErrorCode.Timeout, "Operation cancelled or timed out.", null);
            case AcadRuntime.Exception are:
                return MapAcadRuntime(are);
            case InvalidOperationException ioe when ioe.Message.IndexOf("active", StringComparison.OrdinalIgnoreCase) >= 0:
                return (AcadErrorCode.NoActiveDocument, ioe.Message, "Open a drawing in AutoCAD before calling this tool.");
            case InvalidOperationException ioe when ioe.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0:
                return (AcadErrorCode.EntityNotFound, ioe.Message, "Pass a current handle from list_entities_in_window or get_entity.");
            default:
                return (AcadErrorCode.AcadException, ex.Message, ex.GetType().Name);
        }
    }

    private static (AcadErrorCode, string, string?) MapAcadRuntime(AcadRuntime.Exception are)
    {
        var s = are.ErrorStatus;
        switch (s)
        {
            case AcadRuntime.ErrorStatus.WasErased:
            case AcadRuntime.ErrorStatus.NullObjectId:
            case AcadRuntime.ErrorStatus.UnknownHandle:
                return (AcadErrorCode.EntityNotFound, are.Message, $"AutoCAD ErrorStatus={s}");
            case AcadRuntime.ErrorStatus.LockViolation:
                return (AcadErrorCode.DocumentLocked, are.Message, "Document is locked by another command - retry shortly.");
            case AcadRuntime.ErrorStatus.InvalidInput:
            case AcadRuntime.ErrorStatus.InvalidIndex:
            case AcadRuntime.ErrorStatus.InvalidKey:
                return (AcadErrorCode.InvalidArgument, are.Message, $"AutoCAD ErrorStatus={s}");
            default:
                return (AcadErrorCode.AcadException, are.Message, $"AutoCAD ErrorStatus={s}");
        }
    }
}
