// Shared helper used by both CategoryServer and RouterServer to dispatch a single
// backend [McpTool] method by reflection. Extracted so the router can proxy
// composite-tool calls without duplicating the reflection / error-handling logic.

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Backend.Sidecar;
using AcadMcp.Shared.Mcp;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Mcp;

/// <summary>
/// Invokes a resolved backend composite tool method, handling parameter binding
/// (CancellationToken, IPluginGateway, IVisionSidecarClient, DTO deserialization)
/// and the typed plugin/vision exceptions surface.
/// </summary>
internal static class ToolInvoker
{
    public readonly record struct InvokeResult(string Text, bool IsError);

    public static async Task<InvokeResult> InvokeAsync(
        ILogger logger,
        McpToolMetadata meta,
        MethodInfo method,
        JsonObject args,
        IPluginGateway? plugin,
        IVisionSidecarClient? vision,
        CancellationToken ct)
    {
        if (meta.RequiresPlugin && plugin is null)
        {
            return new InvokeResult(
                $"Tool '{meta.Name}' requires the AutoCAD plugin gateway, but none is registered.",
                IsError: true);
        }

        // Parameter-driven gateway guard: some tools (notably vision/*) don't set
        // RequiresPlugin but still demand a non-null IPluginGateway / IVisionSidecarClient
        // at runtime. Short-circuit them with a clear error instead of letting the tool
        // body NRE on `gateway.SendAsync` / `sidecar.BaseUrl`.
        foreach (var p in method.GetParameters())
        {
            if (p.ParameterType == typeof(IPluginGateway) && plugin is null)
            {
                return new InvokeResult(
                    $"Tool '{meta.Name}' requires the AutoCAD plugin gateway, but none is registered.",
                    IsError: true);
            }
            if (p.ParameterType == typeof(IVisionSidecarClient) && vision is null)
            {
                return new InvokeResult(
                    $"Tool '{meta.Name}' requires the Vision sidecar, but none is registered.",
                    IsError: true);
            }
        }

        try
        {
            var (callArgs, error) = BuildCallArgs(method, args, ct, plugin, vision);
            if (error is not null) return new InvokeResult(error, IsError: true);

            var ret = method.Invoke(null, callArgs);
            object? actualResult = ret;
            if (ret is Task task)
            {
                await task.ConfigureAwait(false);
                var resProp = task.GetType().GetProperty("Result");
                actualResult = resProp?.GetValue(task);
            }

            string text = actualResult switch
            {
                null => "(no result)",
                string s => s,
                _ => JsonSerializer.SerializeToNode(actualResult, JsonOpts.Pretty)?.ToJsonString(JsonOpts.Pretty) ?? "{}",
            };
            return new InvokeResult(text, IsError: false);
        }
        catch (TargetInvocationException tie)
        {
            var inner = tie.InnerException ?? tie;
            logger.LogError(inner, "Tool '{Name}' threw", meta.Name);
            return new InvokeResult(FormatToolError(inner), IsError: true);
        }
        catch (PluginUnavailableException pux)
        {
            logger.LogWarning(pux, "Plugin unavailable for tool '{Name}'", meta.Name);
            return new InvokeResult("AutoCAD plugin not available: " + pux.Message, IsError: true);
        }
        catch (PluginToolException ptx)
        {
            logger.LogWarning("Plugin returned error for '{Name}': {Code} {Msg}", meta.Name, ptx.Code, ptx.Message);
            return new InvokeResult(ptx.Message, IsError: true);
        }
        catch (VisionUnavailableException vux)
        {
            logger.LogWarning(vux, "Vision sidecar unavailable for tool '{Name}'", meta.Name);
            return new InvokeResult("Vision sidecar not available: " + vux.Message, IsError: true);
        }
        catch (VisionEngineUnavailableException veux)
        {
            logger.LogWarning("Vision engine '{Engine}' unavailable: {Hint}", veux.Engine, veux.InstallHint);
            return new InvokeResult(veux.Message, IsError: true);
        }
        catch (VisionToolException vtx)
        {
            logger.LogWarning("Vision sidecar HTTP {Code} for '{Name}': {Msg}", vtx.StatusCode, meta.Name, vtx.Message);
            return new InvokeResult(vtx.Message, IsError: true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Tool '{Name}' was cancelled", meta.Name);
            return new InvokeResult($"Tool '{meta.Name}' was cancelled.", IsError: true);
        }
        catch (Exception ex)
        {
            // Catch-all, and it has to be here. Everything above catches a SPECIFIC type, so
            // any exception not on that list escaped this method, unwound through
            // CategoryServer.HandleToolsCallAsync and StdioJsonRpcHost.RunAsync, and killed
            // the process - the client got no JSON-RPC response at all and lost the whole
            // session, not just the call.
            //
            // Note TargetInvocationException above does NOT cover this: it only wraps throws
            // from the synchronous part of MethodInfo.Invoke. An async tool that throws after
            // its first await surfaces the original exception type at `await task`, so a plain
            // InvalidOperationException walked straight past every clause.
            //
            // Found live: validators/list_violations and validators/auto_fix_violations both
            // throw InvalidOperationException with a genuinely useful message ("call
            // validate_drawing first"). That message never reached anyone; the server just
            // died with exit code 1.
            //
            // FullToolAuditTests' own header states the invariant this restores: "No tool
            // throws an uncaught exception (ToolInvoker is expected to catch and surface every
            // failure as InvokeResult.IsError)". It could not detect the breach because it
            // calls every tool with EMPTY arguments, and these two fail on session state
            // rather than on arguments.
            logger.LogError(ex, "Tool '{Name}' threw an unhandled {Type}", meta.Name, ex.GetType().Name);
            return new InvokeResult(FormatToolError(ex), IsError: true);
        }
    }

    private static string FormatToolError(Exception ex) => ex switch
    {
        PluginToolException ptx => ptx.Message,
        PluginUnavailableException pux => "AutoCAD plugin not available: " + pux.Message,
        _ => ex.Message,
    };

    private static (object?[] args, string? error) BuildCallArgs(
        MethodInfo method,
        JsonObject input,
        CancellationToken ct,
        IPluginGateway? gateway,
        IVisionSidecarClient? vision)
    {
        var ps = method.GetParameters();
        var argv = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            var p = ps[i];
            if (p.ParameterType == typeof(CancellationToken))
            {
                argv[i] = ct;
                continue;
            }
            if (p.ParameterType == typeof(IPluginGateway))
            {
                argv[i] = gateway;
                continue;
            }
            if (p.ParameterType == typeof(IVisionSidecarClient))
            {
                argv[i] = vision;
                continue;
            }
            // Bind by name if json has a member, otherwise try whole-object binding for record DTOs.
            JsonNode? src = input.TryGetPropertyValue(p.Name ?? "", out var node) ? node : input;
            try
            {
                argv[i] = src is null
                    ? (p.HasDefaultValue ? p.DefaultValue : null)
                    : src.Deserialize(p.ParameterType, JsonOpts.Compact);
            }
            catch (JsonException ex)
            {
                return (Array.Empty<object?>(), $"Failed to deserialize parameter '{p.Name}': {ex.Message}");
            }
        }
        return (argv, null);
    }
}
