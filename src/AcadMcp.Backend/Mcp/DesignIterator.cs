// Phase 7.0 - the closed design loop driven by acad_design_iterate.
//
// Contract:
//   1. Create an UNDO checkpoint via acad.checkpoint.create (optional file snapshot).
//   2. Execute each plan step by calling the named plugin tool through IPluginGateway.
//      Plan steps use PLUGIN tool names (e.g. "acad.validators.collect_entities",
//      "acad.line.draw"); the optional `category` field is informational only - the
//      router does not spawn category subprocesses in Phase 7.0 MVP.
//   3. Run ValidationEngine against the standard's rule set.
//   4. If violations exist and every rule that fired has a `fix:`, we call
//      acad.validators.apply_fixes and re-validate (up to maxIterations).
//   5. If violations remain after all iterations OR a step throws, we restore the
//      checkpoint and return the accumulated log.
//   6. An audit log is written to %LOCALAPPDATA%\AcadMcp\logs\iterate-<ts>.json.
//
// This is deliberately router-local: all IPC hops go through IPluginGateway, so
// the loop does not need a live AcadMcp.Backend category subprocess to run.
// Cross-category routing (calling `acad-geometry-2d.draw_line`) is deferred to
// Phase 7.1 where we add an RPC proxy.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Validators;
using AcadMcp.Backend.Pipe;
using AcadMcp.Backend.Validators;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Mcp;

public sealed record DesignIterateRequest(
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("plan")] IReadOnlyList<PlanStep> Plan,
    [property: JsonPropertyName("standardId")] string? StandardId,
    [property: JsonPropertyName("maxIterations")] int MaxIterations,
    [property: JsonPropertyName("checkpointLabel")] string? CheckpointLabel);

public sealed record PlanStep(
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("args")] JsonObject? Args);

public sealed record DesignIterateOutcome(
    bool Success,
    string Summary,
    string? CheckpointId,
    int Iterations,
    ValidationReport? FinalReport,
    IReadOnlyList<IterationLog> IterationLogs,
    string AuditLogPath);

public sealed record IterationLog(
    int Iteration,
    IReadOnlyList<StepLog> Steps,
    int Violations,
    int ViolationsFixed,
    bool Aborted,
    string? AbortReason);

public sealed record StepLog(
    string Tool,
    string? Category,
    bool Ok,
    string? Error,
    long ElapsedMs,
    // Full JSON payload returned by the plugin tool on success.
    // Written into the audit log (%LOCALAPPDATA%\AcadMcp\logs\iterate-*.json)
    // so read-only plan steps (doc_summary, collect_entities, ...) are inspectable.
    [property: JsonPropertyName("output")] JsonNode? Output = null);

public sealed class DesignIterator
{
    private const int StepTimeoutMs = 60_000;
    private const int ValidatorTimeoutMs = 120_000;
    private const int CheckpointTimeoutMs = 30_000;

    private readonly ILogger _logger;
    private readonly IPluginGateway _plugin;

    public DesignIterator(ILogger logger, IPluginGateway plugin)
    {
        _logger = logger;
        _plugin = plugin;
    }

    public async Task<DesignIterateOutcome> RunAsync(DesignIterateRequest req, CancellationToken ct)
    {
        if (req.Plan is null || req.Plan.Count == 0)
            throw new ArgumentException("plan must contain at least one step.");

        int maxIter = req.MaxIterations <= 0 ? 3 : Math.Min(req.MaxIterations, 10);
        var logs = new List<IterationLog>();
        string? checkpointId = null;
        ValidationReport? lastReport = null;
        bool aborted = false;
        string? abortReason = null;

        // ── 1. create checkpoint ──
        try
        {
            var ckArgs = new JsonObject();
            var label = req.CheckpointLabel;
            if (string.IsNullOrWhiteSpace(label))
                label = "iter_" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            ckArgs["label"] = label;
            // Leave fileSnapshot unset so acad.checkpoint.create's default (on)
            // applies. The plugin's rollback mechanism is a .dwg snapshot reopen,
            // not an AutoCAD UNDO mark (that approach deadlocked the UI thread --
            // see CheckpointPluginTools.cs header) -- without a snapshot, the
            // "rollback on abort" step below has nothing to restore from and the
            // failed plan's changes would stay on the drawing.

            var ck = await _plugin.InvokeAsync("acad.checkpoint.create", ckArgs, CheckpointTimeoutMs, ct).ConfigureAwait(false);
            checkpointId = (ck as JsonObject)?["id"]?.GetValue<string>();
            _logger.LogInformation("design_iterate checkpoint created id={Id} label={Label}", checkpointId, label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "design_iterate: checkpoint create failed");
            return await FinalizeAsync(req, false, $"checkpoint_create_failed: {ex.Message}", null, 0, null, logs, ct).ConfigureAwait(false);
        }

        // Resolve standard → rule set (optional).
        IReadOnlyList<Rule> rules = Array.Empty<Rule>();
        if (!string.IsNullOrWhiteSpace(req.StandardId))
        {
            if (!ValidatorsRuntime.Standards.TryGet(req.StandardId!, out var std) || std is null)
                return await FinalizeAsync(req, false, $"unknown_standard: {req.StandardId}", checkpointId, 0, null, logs, ct).ConfigureAwait(false);
            rules = std.RuleIds
                .Select(id => ValidatorsRuntime.Rules.TryGet(id, out var rr) ? rr : null)
                .Where(r => r is not null).Cast<Rule>().ToList();
        }

        // ── 2. iteration loop ──
        int iteration = 0;
        for (iteration = 1; iteration <= maxIter; iteration++)
        {
            ct.ThrowIfCancellationRequested();
            var stepLogs = new List<StepLog>();
            bool stepsOk = true;

            // Only execute the plan on iteration 1; later iterations are just re-validate + fix.
            if (iteration == 1)
            {
                foreach (var step in req.Plan)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var stepArgs = step.Args ?? new JsonObject();
                        var result = await _plugin.InvokeAsync(step.Tool, stepArgs, StepTimeoutMs, ct).ConfigureAwait(false);
                        sw.Stop();
                        stepLogs.Add(new StepLog(step.Tool, step.Category, true, null, sw.ElapsedMilliseconds, result));
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        stepLogs.Add(new StepLog(step.Tool, step.Category, false, ex.Message, sw.ElapsedMilliseconds));
                        stepsOk = false;
                        abortReason = $"step_failed: {step.Tool} - {ex.Message}";
                        break;
                    }
                }
            }

            if (!stepsOk)
            {
                logs.Add(new IterationLog(iteration, stepLogs, 0, 0, true, abortReason));
                aborted = true;
                break;
            }

            // Validate (if a standard was requested).
            int violationsNow = 0;
            int fixedNow = 0;
            if (rules.Count > 0)
            {
                var engine = new ValidationEngine(_plugin);
                bool includePaper = rules.Any(r => r.Scope?.InPaperspace ?? false);
                lastReport = await engine.RunAsync(rules, ValidatorsRuntime.Rules.LoadErrors, includePaper, ct).ConfigureAwait(false);
                violationsNow = lastReport.ViolationCount;

                if (violationsNow == 0)
                {
                    logs.Add(new IterationLog(iteration, stepLogs, 0, 0, false, null));
                    break;
                }

                // Attempt auto-fix for every violation that has a fix spec.
                var fixable = lastReport.Violations.Where(v => v.FixAvailable).ToList();
                if (fixable.Count > 0 && iteration < maxIter)
                {
                    try
                    {
                        fixedNow = await ApplyAutoFixesAsync(rules, lastReport, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        abortReason = $"autofix_failed: {ex.Message}";
                        logs.Add(new IterationLog(iteration, stepLogs, violationsNow, 0, true, abortReason));
                        aborted = true;
                        break;
                    }
                }
            }

            logs.Add(new IterationLog(iteration, stepLogs, violationsNow, fixedNow, false, null));

            // Nothing fixable and violations remain -> abort + rollback.
            if (violationsNow > 0 && fixedNow == 0)
            {
                abortReason = $"violations_not_fixable: {violationsNow}";
                aborted = true;
                break;
            }
            if (rules.Count == 0)
            {
                // No standard = no validation loop; we stop after executing the plan once.
                break;
            }
        }

        // ── 3. rollback on abort ──
        if (aborted && checkpointId is not null)
        {
            try
            {
                var args = new JsonObject { ["id"] = checkpointId };
                var restoreResult = await _plugin.InvokeAsync("acad.checkpoint.restore", args, CheckpointTimeoutMs, ct).ConfigureAwait(false);
                var strategy = (restoreResult as JsonObject)?["strategy"]?.GetValue<string>() ?? "unknown";
                _logger.LogInformation("design_iterate rolled back to checkpoint {Id} (strategy={Strategy})", checkpointId, strategy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "design_iterate: rollback failed for checkpoint {Id}", checkpointId);
            }
        }

        string summary = aborted
            ? $"aborted after {iteration} iteration(s): {abortReason}"
            : lastReport is null || lastReport.ViolationCount == 0
                ? $"success in {iteration} iteration(s)"
                : $"completed {iteration} iteration(s) with {lastReport.ViolationCount} residual violation(s)";

        return await FinalizeAsync(req, !aborted && (lastReport is null || lastReport.ViolationCount == 0),
            summary, checkpointId, iteration, lastReport, logs, ct).ConfigureAwait(false);
    }

    private async Task<int> ApplyAutoFixesAsync(IReadOnlyList<Rule> rules, ValidationReport report, CancellationToken ct)
    {
        // Build a flat fix list from every violation that has a fixable rule.
        var rulesById = rules.ToDictionary(r => r.Id, r => r, StringComparer.OrdinalIgnoreCase);
        var fixes = new JsonArray();
        foreach (var v in report.Violations)
        {
            if (!v.FixAvailable) continue;
            if (string.IsNullOrWhiteSpace(v.EntityHandle)) continue;
            if (!rulesById.TryGetValue(v.RuleId, out var rule) || rule.Fix is null) continue;

            var parms = new JsonObject();
            foreach (var kv in rule.Fix.Params)
            {
                parms[kv.Key] = JsonValue.Create(kv.Value);
            }

            fixes.Add(new JsonObject
            {
                ["handle"] = v.EntityHandle,
                ["fixType"] = rule.Fix.Type,
                ["params"] = parms,
            });
        }
        if (fixes.Count == 0) return 0;

        var args = new JsonObject { ["fixes"] = fixes };
        var result = await _plugin.InvokeAsync("acad.validators.apply_fixes", args, ValidatorTimeoutMs, ct).ConfigureAwait(false);
        return (result as JsonObject)?["appliedCount"]?.GetValue<int>() ?? 0;
    }

    private static Task<DesignIterateOutcome> FinalizeAsync(
        DesignIterateRequest req, bool success, string summary,
        string? checkpointId, int iteration, ValidationReport? finalReport,
        IReadOnlyList<IterationLog> logs, CancellationToken ct)
    {
        var auditPath = WriteAuditLog(req, success, summary, checkpointId, iteration, finalReport, logs);
        return Task.FromResult(new DesignIterateOutcome(success, summary, checkpointId, iteration, finalReport, logs, auditPath));
    }

    private static string WriteAuditLog(
        DesignIterateRequest req, bool success, string summary,
        string? checkpointId, int iteration, ValidationReport? finalReport,
        IReadOnlyList<IterationLog> logs)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadMcp", "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"iterate-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");
            var doc = new
            {
                task = req.Task,
                standardId = req.StandardId,
                maxIterations = req.MaxIterations,
                success,
                summary,
                checkpointId,
                iterations = iteration,
                plan = req.Plan,
                logs,
                finalReport,
                writtenUtc = DateTime.UtcNow,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }));
            return path;
        }
        catch
        {
            return "<audit-log-unavailable>";
        }
    }
}
