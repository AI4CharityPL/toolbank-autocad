// AcadMcp.Backend entry point.
// One binary, multiple processes. Each process exposes ONE category over stdio MCP.
// See rule 00-architecture-invariants.md Invariant #1.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using AcadMcp.Backend.Mcp;
using AcadMcp.Backend.Pipe;
using AcadMcp.Backend.Sidecar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = StartupOptions.Parse(args);
        if (options is null) return 2;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            // Logging goes to STDERR so that JSON-RPC stays clean on STDOUT.
            // AddSimpleConsole() writes to stdout by default, which breaks the MCP client's JSON-RPC parser.
            b.AddProvider(new AcadMcp.Backend.Logging.StderrLoggerProvider(
                options.Verbose ? LogLevel.Debug : LogLevel.Information));
        });

        services.AddSingleton(options);
        services.AddSingleton<ToolRegistry>();
        // Router also needs IPluginGateway now (Phase 7.0) for acad_status, checkpoint
        // and design_iterate meta-tools. Non-router categories still need it for their
        // own tool implementations.
        services.AddSingleton<IPluginGateway, PluginGateway>();
        // Vision sidecar client is registered for ALL processes (router included so
        // that acad_call can dispatch vision composites too). Categories that don't
        // need it simply won't ask for it (rule 29 §6).
        services.AddSingleton<IVisionSidecarClient, VisionSidecarClient>();
        services.AddSingleton<ICategoryServer>(sp =>
            options.IsRouter
                ? new RouterServer(sp.GetRequiredService<ILogger<RouterServer>>(),
                                    options,
                                    sp.GetRequiredService<ToolRegistry>(),
                                    sp.GetRequiredService<IPluginGateway>(),
                                    sp.GetRequiredService<IVisionSidecarClient>())
                : new CategoryServer(sp.GetRequiredService<ILogger<CategoryServer>>(),
                                      sp.GetRequiredService<ToolRegistry>(),
                                      options,
                                      sp.GetService<IPluginGateway>(),
                                      sp.GetService<IVisionSidecarClient>()));

        await using var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AcadMcp.Backend");

        logger.LogInformation("AcadMcp.Backend starting. Category={Category} Transport={Transport} Mode={Mode}",
            options.Category, options.Transport, options.IsRouter ? "router" : "category");

        if (options.RegenerateManifest)
        {
            return RunRegenerateManifest(sp, options, logger);
        }

        if (options.PingPlugin)
        {
            return await RunPingPluginAsync(sp, options, logger, cts.Token).ConfigureAwait(false);
        }

        if (options.ValidatorsSelfCheck)
        {
            return RunValidatorsSelfCheck(sp, logger);
        }

        var server = sp.GetRequiredService<ICategoryServer>();
        try
        {
            await server.RunAsync(cts.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error in AcadMcp.Backend");
            return 1;
        }
    }

    private static async Task<int> RunPingPluginAsync(IServiceProvider sp, StartupOptions options, ILogger logger, CancellationToken ct)
    {
        var clientLogger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<PluginPipeClient>();
        await using var client = new PluginPipeClient(
            clientLogger,
            clientId: $"acad-{options.Category}/ping",
            category: options.Category,
            pipeName: options.PipeName);
        try
        {
            logger.LogInformation("Connecting to plugin on \\\\.\\pipe\\{Pipe}...", options.PipeName);
            await client.ConnectAsync(ct).ConfigureAwait(false);
            var hs = client.Handshake!;
            logger.LogInformation("Plugin handshake OK pluginVersion={V} acad={Acad} vertical={Vertical} isLT={LT}",
                hs.PluginVersion, hs.AcadVersion, hs.AcadVertical ?? "<n/a>", hs.IsLT);

            var args = new JsonObject { ["echo"] = "ping" };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await client.CallToolAsync("_echo", args, timeoutMs: 5000, ct).ConfigureAwait(false);
            sw.Stop();
            if (!resp.Ok)
            {
                logger.LogError("Plugin _echo failed: {Code} {Msg}", resp.Error?.Code, resp.Error?.Message);
                return 1;
            }
            logger.LogInformation("Plugin _echo round-trip {Ms} ms - result: {Result}",
                sw.ElapsedMilliseconds, resp.Result?.ToJsonString());

            var statusResp = await client.CallToolAsync("acad_status", new JsonObject(), timeoutMs: 10000, ct).ConfigureAwait(false);
            if (statusResp.Ok)
            {
                logger.LogInformation("Plugin acad_status: {Result}", statusResp.Result?.ToJsonString());
            }
            else
            {
                logger.LogWarning("Plugin acad_status failed: {Code} {Msg}", statusResp.Error?.Code, statusResp.Error?.Message);
            }
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ping plugin failed");
            return 1;
        }
    }

    /// <summary>
    /// Loads the bundled validator <see cref="Validators.RuleRegistry"/> and
    /// <see cref="Validators.StandardLibrary"/> from embedded resources + repo +
    /// user dirs, prints a summary, and exits non-zero on any rule parse error.
    /// Used as a fast smoke test (CI / pre-release) without needing AutoCAD.
    /// </summary>
    private static int RunValidatorsSelfCheck(IServiceProvider sp, ILogger logger)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var registry = new Validators.RuleRegistry(loggerFactory.CreateLogger<Validators.RuleRegistry>());
        var standards = new Validators.StandardLibrary(loggerFactory.CreateLogger<Validators.StandardLibrary>());
        var allStandards = standards.All.ToList();

        logger.LogInformation("Validators self-check: {RuleCount} rule(s), {StandardCount} standard(s)",
            registry.All.Count, allStandards.Count);
        foreach (var r in registry.All)
        {
            logger.LogInformation("  rule    : {Id}  ({Severity}/{Discipline})", r.Id, r.Severity, r.Discipline);
        }
        foreach (var s in allStandards)
        {
            logger.LogInformation("  standard: {Id}  -> {Count} rule(s)", s.Id, s.RuleIds.Count);
        }

        if (registry.LoadErrors.Count > 0)
        {
            foreach (var e in registry.LoadErrors)
            {
                logger.LogError("rule load error: {Error}", e);
            }
            logger.LogError("Validators self-check FAILED: {Count} rule load error(s)", registry.LoadErrors.Count);
            return 1;
        }

        logger.LogInformation("Validators self-check OK.");
        return 0;
    }

    private static int RunRegenerateManifest(IServiceProvider sp, StartupOptions options, ILogger logger)
    {
        var registry = sp.GetRequiredService<ToolRegistry>();
        var tools = registry.ToolsFor(options.Category);
        if (tools.Count == 0)
        {
            logger.LogWarning("No tools found for category '{Category}' - manifest will have empty tools_summary", options.Category);
        }
        var repoRoot = options.RepoRoot ?? Mcp.RepoRootDetector.Detect();
        var changed = Mcp.BankAutoRegister.RegenerateManifest(repoRoot, options.Category, tools, createIfMissing: true);
        logger.LogInformation("Manifest {Status} for acad-{Category} ({Count} tools)",
            changed ? "updated" : "unchanged", options.Category, tools.Count);
        return 0;
    }
}

/// <summary>Parsed CLI options.</summary>
public sealed record StartupOptions(
    string Category,
    string Transport,
    bool IsRouter,
    bool Verbose,
    string PipeName,
    bool RegenerateManifest = false,
    string? RepoRoot = null,
    bool PingPlugin = false,
    bool ValidatorsSelfCheck = false)
{
    public static StartupOptions? Parse(string[] args)
    {
        string? category = null;
        string transport = "stdio";
        bool verbose = false;
        bool regenerateManifest = false;
        bool pingPlugin = false;
        bool validatorsSelfCheck = false;
        string? repoRoot = null;
        string pipeName = AcadMcp.Shared.PipeProtocol.PipeName;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--category" when i + 1 < args.Length:
                    category = args[++i]; break;
                case "--transport" when i + 1 < args.Length:
                    transport = args[++i]; break;
                case "--pipe" when i + 1 < args.Length:
                    pipeName = args[++i]; break;
                case "--repo-root" when i + 1 < args.Length:
                    repoRoot = args[++i]; break;
                case "--regenerate-manifest":
                    regenerateManifest = true; break;
                case "--ping-plugin":
                    pingPlugin = true; break;
                case "--validators-self-check":
                    validatorsSelfCheck = true; break;
                case "--verbose":
                    verbose = true; break;
                case "--help" or "-h":
                    PrintHelp();
                    return null;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintHelp();
                    return null;
            }
        }

        // Validators self-check is category-agnostic; default to "validators" if
        // the caller didn't pass --category so the diagnostic stays one-liner-friendly.
        if (validatorsSelfCheck && string.IsNullOrWhiteSpace(category))
        {
            category = "validators";
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            Console.Error.WriteLine("--category <name> is required (use 'router' for the meta-router).");
            PrintHelp();
            return null;
        }

        bool isRouter = string.Equals(category, "router", StringComparison.OrdinalIgnoreCase);
        return new StartupOptions(category!, transport, isRouter, verbose, pipeName, regenerateManifest, repoRoot, pingPlugin, validatorsSelfCheck);
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("AcadMcp.Backend - one MCP host per category");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage: AcadMcp.Backend --category <name> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --transport stdio          Transport (default: stdio)");
        Console.Error.WriteLine("  --pipe <name>              Pipe name (default: acadmcp)");
        Console.Error.WriteLine("  --regenerate-manifest      Update mcpbank-manifests/acad-<category>.json from [McpTool] metadata and exit");
        Console.Error.WriteLine("  --repo-root <path>         Override repo root for --regenerate-manifest");
        Console.Error.WriteLine("  --ping-plugin              Connect to AutoCAD plugin pipe, run handshake + _echo + acad_status, exit");
        Console.Error.WriteLine("  --validators-self-check    Load bundled validator rules + standards (no AutoCAD needed) and exit non-zero on parse errors");
        Console.Error.WriteLine("  --verbose                  Verbose logging");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  AcadMcp.Backend --category router");
        Console.Error.WriteLine("  AcadMcp.Backend --category geometry-2d");
        Console.Error.WriteLine("  AcadMcp.Backend --category geometry-2d --regenerate-manifest");
        Console.Error.WriteLine("  AcadMcp.Backend --category router --ping-plugin");
    }
}
