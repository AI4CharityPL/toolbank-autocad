// Orchestrates a validation run.
//
// Flow (rule 34 §1):
//   1. Compute the UNION scope of all selected rules.
//   2. ONE pipe call to acad.validators.collect_entities for that union.
//   3. ONE pipe call to acad.validators.doc_summary.
//   4. Per rule: filter the cached entity snapshot, evaluate each check, emit violations.
//   5. Build ValidationReport.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Validators;
using AcadMcp.Backend.Pipe;

namespace AcadMcp.Backend.Validators;

public sealed class ValidationEngine
{
    private const int T_NORMAL = 30_000;
    private const int T_LARGE  = 120_000;

    private readonly IPluginGateway _plugin;

    public ValidationEngine(IPluginGateway plugin) { _plugin = plugin; }

    public async Task<ValidationReport> RunAsync(
        IReadOnlyList<Rule> rules,
        IReadOnlyList<string> loadErrors,
        bool includePaperspace,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var docSummary = await ValidatorsProxy.CallAsync<DocSummaryArgs, DocSummaryDto>(
            _plugin, "acad.validators.doc_summary", new DocSummaryArgs(), T_NORMAL, ct).ConfigureAwait(false);

        // Bucket rules by space (model/paper) and run a single collect per bucket.
        var modelRules = rules.Where(r => !(r.Scope?.InPaperspace ?? false)).ToList();
        var paperRules = includePaperspace
            ? rules.Where(r => r.Scope?.InPaperspace ?? false).ToList()
            : new List<Rule>();

        var msEntities = modelRules.Count > 0
            ? await CollectAsync(modelRules, inPaperspace: false, ct).ConfigureAwait(false)
            : new List<EntitySnapshotDto>();
        var psEntities = paperRules.Count > 0
            ? await CollectAsync(paperRules, inPaperspace: true, ct).ConfigureAwait(false)
            : new List<EntitySnapshotDto>();

        var ctx = new EvalContext();
        var violations = new List<Violation>();

        foreach (var rule in rules)
        {
            var bucket = (rule.Scope?.InPaperspace ?? false) ? psEntities : msEntities;
            // Populate cross-entity context per rule - predicates like polyline_endpoints_share
            // look up other snapshots in the same space.
            ctx.AllEntities = bucket;

            // Doc-level checks: evaluate each one independently against doc summary.
            foreach (var check in rule.Checks.Where(c => CheckEvaluator.IsDocLevel(c.Type)))
            {
                var oc = CheckEvaluator.EvaluateDoc(check, docSummary, ctx);
                if (oc is null || oc.Pass) continue;
                violations.Add(new Violation(
                    RuleId: rule.Id,
                    RuleName: rule.Name,
                    Severity: rule.Severity.ToString().ToLowerInvariant(),
                    Discipline: rule.Discipline.ToString().ToLowerInvariant(),
                    EntityHandle: null,
                    DxfType: null,
                    Layer: null,
                    Expected: oc.Expected,
                    Observed: oc.Observed,
                    Message: $"[{rule.Id}] {rule.Name}: expected {oc.Expected}, got {oc.Observed}.",
                    FixAvailable: false));
            }

            // Entity-level: filter scope, evaluate, emit on first failed check.
            var entityChecks = rule.Checks.Where(c => !CheckEvaluator.IsDocLevel(c.Type)).ToList();
            if (entityChecks.Count == 0) continue;

            var scoped = ApplyScope(bucket, rule.Scope, ctx);
            foreach (var entity in scoped)
            {
                foreach (var check in entityChecks)
                {
                    var oc = CheckEvaluator.Evaluate(check, entity, ctx);
                    if (oc.Pass) continue;
                    violations.Add(new Violation(
                        RuleId: rule.Id,
                        RuleName: rule.Name,
                        Severity: rule.Severity.ToString().ToLowerInvariant(),
                        Discipline: rule.Discipline.ToString().ToLowerInvariant(),
                        EntityHandle: entity.Handle,
                        DxfType: entity.DxfType,
                        Layer: entity.Layer,
                        Expected: oc.Expected,
                        Observed: oc.Observed,
                        Message: $"entity #{entity.Handle} ({entity.DxfType} on layer '{entity.Layer}') violates {rule.Id}: expected {oc.Expected}, got {oc.Observed}.",
                        FixAvailable: rule.Fix is not null));
                    break; // one violation per (rule, entity) pair
                }
            }
        }

        sw.Stop();

        int errCount = violations.Count(v => v.Severity == "error");
        int warnCount = violations.Count(v => v.Severity == "warning");
        int infoCount = violations.Count(v => v.Severity == "info");

        var perRule = violations
            .GroupBy(v => v.RuleId)
            .Select(g => new RuleSummary(g.Key, g.First().RuleName, g.First().Severity, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        return new ValidationReport(
            DocumentName: docSummary.DocumentName,
            DocumentPath: docSummary.DocumentPath,
            RulesEvaluated: rules.Count,
            EntitiesScanned: msEntities.Count + psEntities.Count,
            ViolationCount: violations.Count,
            ErrorCount: errCount,
            WarningCount: warnCount,
            InfoCount: infoCount,
            PerRule: perRule,
            Violations: violations,
            LoadErrors: loadErrors,
            ElapsedMs: sw.ElapsedMilliseconds);
    }

    private async Task<List<EntitySnapshotDto>> CollectAsync(IReadOnlyList<Rule> rules, bool inPaperspace, CancellationToken ct)
    {
        // Permissive union: if ANY rule has no entity-type filter, request all types.
        var unionTypes = rules.Any(r => r.Scope?.EntityTypes is null || r.Scope.EntityTypes.Count == 0)
            ? null
            : rules.SelectMany(r => r.Scope!.EntityTypes!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var args = new CollectEntitiesArgs(
            EntityTypes: unionTypes,
            LayerPattern: null,         // applied client-side per rule
            LayerIn: null,              // applied client-side per rule
            InPaperspace: inPaperspace);

        var result = await ValidatorsProxy.CallAsync<CollectEntitiesArgs, CollectEntitiesResult>(
            _plugin, "acad.validators.collect_entities", args, T_LARGE, ct).ConfigureAwait(false);
        return result.Entities.ToList();
    }

    private static IEnumerable<EntitySnapshotDto> ApplyScope(IEnumerable<EntitySnapshotDto> all, RuleScope? scope, EvalContext ctx)
    {
        if (scope is null) return all;

        IEnumerable<EntitySnapshotDto> q = all;
        if (scope.EntityTypes is { Count: > 0 })
        {
            var set = new HashSet<string>(scope.EntityTypes, StringComparer.OrdinalIgnoreCase);
            q = q.Where(e => set.Contains(e.DxfType));
        }
        if (scope.LayerIn is { Count: > 0 })
        {
            var set = new HashSet<string>(scope.LayerIn, StringComparer.OrdinalIgnoreCase);
            q = q.Where(e => set.Contains(e.Layer));
        }
        if (!string.IsNullOrWhiteSpace(scope.LayerPattern))
        {
            var rx = ctx.GetRegex(scope.LayerPattern!);
            q = q.Where(e => rx.IsMatch(e.Layer));
        }
        return q;
    }
}
