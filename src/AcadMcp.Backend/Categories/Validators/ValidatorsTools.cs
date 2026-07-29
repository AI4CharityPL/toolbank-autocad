// AutoCAD acad-validators category. 10 tools for the rule-based validation pipeline:
// browse the rule catalogue (list_validators / explain_rule / list_standards), run
// validations (validate_drawing / validate_with_rule / validate_against_standard),
// inspect results (list_violations), apply fixes (auto_fix_violations) and curate
// the rule library (add_validator_rule / reload_validator_rules).
//
// Engine: src/AcadMcp.Backend/Validators/.  Wire DTOs: ValidatorsDtos.cs.
// Plugin handlers: src/AcadMcp.Plugin/Tools/ValidatorsPluginTools.cs ("acad.validators.*").
// Rules: 19, 33-validators-rule-format.mdc, 34-validators-engine-traps.mdc.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Backend.Validators;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Validators;

public static class ValidatorsTools
{
    private const int T_NORMAL = 30_000;
    private const int T_LARGE = 120_000;

    [McpTool("list_validators",
        "List every available validator rule (id, name, severity, discipline, fix-available, description). Optional filters: discipline (general|architectural|mechanical|electrical|civil|mep) and minSeverity (info|warning|error). Pure local query - does not touch AutoCAD.",
        "validators",
        Intent = new[]
        {
            "wylistuj reguly walidacji",
            "list validator rules",
            "show available rules",
            "pokaz dostepne walidatory",
            "what validators are available"
        },
        ReadOnly = true)]
    public static Task<ListValidatorsResult> ListValidators(ListValidatorsArgs args, CancellationToken ct)
    {
        Discipline? d = ParseDisciplineOrNull(args.Discipline);
        Severity? s = ParseSeverityOrNull(args.MinSeverity);
        var rules = ValidatorsRuntime.Rules.Filter(d, s)
            .OrderBy(r => r.Discipline)
            .ThenBy(r => r.Severity, Comparer<Severity>.Create((a, b) => b.CompareTo(a)))
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => new RuleDescriptor(r.Id, r.Name,
                r.Severity.ToString().ToLowerInvariant(),
                r.Discipline.ToString().ToLowerInvariant(),
                r.Fix is not null,
                r.Description))
            .ToList();
        return Task.FromResult(new ListValidatorsResult(rules));
    }

    [McpTool("explain_rule",
        "Return the full definition of one validator rule by id - severity, discipline, description, references, scope, list of checks and the optional fix recipe. Pure local query - does not touch AutoCAD.",
        "validators",
        Intent = new[]
        {
            "pokaz tresc reguly",
            "explain validator rule",
            "what does this rule check",
            "co sprawdza ta regula",
            "rule details"
        },
        ReadOnly = true)]
    public static Task<ExplainRuleResult> ExplainRule(ExplainRuleArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.RuleId))
            throw new ArgumentException("ruleId is required.");
        if (!ValidatorsRuntime.Rules.TryGet(args.RuleId, out var r) || r is null)
            throw new InvalidOperationException($"validator rule '{args.RuleId}' not found.");
        return Task.FromResult(new ExplainRuleResult(
            Id: r.Id,
            Name: r.Name,
            Severity: r.Severity.ToString().ToLowerInvariant(),
            Discipline: r.Discipline.ToString().ToLowerInvariant(),
            Description: r.Description,
            References: r.References,
            HasFix: r.Fix is not null,
            Source: r.SourceLocation,
            Scope: r.Scope is null ? null : new
            {
                entityTypes = r.Scope.EntityTypes,
                layerPattern = r.Scope.LayerPattern,
                layerIn = r.Scope.LayerIn,
                inPaperspace = r.Scope.InPaperspace,
            },
            Checks: r.Checks.Select(c => (object)new { type = c.Type, @params = c.Params, children = c.Children.Select(cc => new { type = cc.Type, @params = cc.Params }).ToList() }).ToList(),
            Fix: r.Fix is null ? null : new { type = r.Fix.Type, @params = r.Fix.Params }));
    }

    [McpTool("list_standards",
        "List every bundled standard preset (id, human name, ordered list of rule ids it expands to). Standards are convenience presets for validate_against_standard. Pure local query.",
        "validators",
        Intent = new[]
        {
            "wylistuj standardy",
            "list cad standards",
            "show standard presets",
            "available standards",
            "jakie standardy sa dostepne"
        },
        ReadOnly = true)]
    public static Task<ListStandardsResult> ListStandards(StandardArgs? _ , CancellationToken ct) =>
        Task.FromResult(new ListStandardsResult(
            ValidatorsRuntime.Standards.All.OrderBy(s => s.Id, StringComparer.Ordinal).ToList()));

    [McpTool("validate_drawing",
        "Run a set of validator rules against the active document and return a structured report (per-rule counts, every violation with handle/dxfType/layer/expected/observed/fixAvailable). Optional filters: ruleIds (explicit list, overrides everything else), discipline, minSeverity, includePaperspace.",
        "validators",
        Intent = new[]
        {
            "zwaliduj rysunek",
            "validate active drawing",
            "run validators on dwg",
            "sprawdz poprawnosc rysunku",
            "check drawing for errors"
        },
        RequiresPlugin = true)]
    public static async Task<ValidationReport> ValidateDrawing(IPluginGateway gw, ValidateArgs args, CancellationToken ct)
    {
        var rules = SelectRules(args.RuleIds, args.Discipline, args.MinSeverity);
        if (rules.Count == 0)
            throw new InvalidOperationException("no validator rules matched the requested filters.");
        var engine = new ValidationEngine(gw);
        var report = await engine.RunAsync(rules, ValidatorsRuntime.Rules.LoadErrors, args.IncludePaperspace, ct).ConfigureAwait(false);
        ValidatorsRuntime.StoreReport(BuildDocKey(report), report);
        return report;
    }

    [McpTool("validate_with_rule",
        "Run exactly one validator rule (by id) against the active document and return a focused report. Throws if the ruleId is unknown.",
        "validators",
        Intent = new[]
        {
            "zwaliduj jedna regula",
            "run a single validator rule",
            "validate against rule",
            "sprawdz ta regule",
            "check one rule only"
        },
        RequiresPlugin = true)]
    public static async Task<ValidationReport> ValidateWithRule(IPluginGateway gw, ValidateOneArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.RuleId))
            throw new ArgumentException("ruleId is required.");
        if (!ValidatorsRuntime.Rules.TryGet(args.RuleId, out var r) || r is null)
            throw new InvalidOperationException($"validator rule '{args.RuleId}' not found.");
        var engine = new ValidationEngine(gw);
        var includePaper = r.Scope?.InPaperspace ?? false;
        var report = await engine.RunAsync(new[] { r }, ValidatorsRuntime.Rules.LoadErrors, includePaper, ct).ConfigureAwait(false);
        ValidatorsRuntime.StoreReport(BuildDocKey(report), report);
        return report;
    }

    [McpTool("validate_against_standard",
        "Resolve a standard id (e.g. 'iso-cad-baseline') to its rule set and run validate_drawing for that bundle. Use list_standards first to discover available presets.",
        "validators",
        Intent = new[]
        {
            "zwaliduj wedlug standardu",
            "run standard preset",
            "validate against standard",
            "sprawdz wg normy",
            "check standard compliance"
        },
        RequiresPlugin = true)]
    public static async Task<ValidationReport> ValidateAgainstStandard(IPluginGateway gw, StandardArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.StandardId))
            throw new ArgumentException("standardId is required.");
        if (!ValidatorsRuntime.Standards.TryGet(args.StandardId, out var std) || std is null)
            throw new InvalidOperationException($"standard '{args.StandardId}' not found.");
        var rules = std.RuleIds
            .Select(id => ValidatorsRuntime.Rules.TryGet(id, out var rr) ? rr : null)
            .Where(r => r is not null)
            .Cast<Rule>()
            .ToList();
        if (rules.Count == 0)
            throw new InvalidOperationException($"standard '{args.StandardId}' references no loaded rules - check rule ids in {std.Id}.");
        var engine = new ValidationEngine(gw);
        bool includePaper = rules.Any(r => r.Scope?.InPaperspace ?? false);
        var report = await engine.RunAsync(rules, ValidatorsRuntime.Rules.LoadErrors, includePaper, ct).ConfigureAwait(false);
        ValidatorsRuntime.StoreReport(BuildDocKey(report), report);
        return report;
    }

    [McpTool("list_violations",
        "Return the most recent ValidationReport produced for the active document. The cache is keyed by document name + path so opening a different drawing returns 'no report yet' (rule 34 §9).",
        "validators",
        Intent = new[]
        {
            "pokaz ostatnie naruszenia",
            "list last violations",
            "what violations did we find",
            "ostatnie bledy walidacji",
            "show last validation report"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static async Task<ValidationReport> ListViolations(IPluginGateway gw, DocSummaryArgs? _, CancellationToken ct)
    {
        // Use plugin doc_summary as the source of truth for current doc identity.
        var doc = await ValidatorsProxy.CallAsync<DocSummaryArgs, DocSummaryDto>(
            gw, "acad.validators.doc_summary", new DocSummaryArgs(), T_NORMAL, ct).ConfigureAwait(false);
        var (key, report) = ValidatorsRuntime.GetLastReport();
        var currentKey = BuildDocKey(doc.DocumentName, doc.DocumentPath);
        if (report is null || !string.Equals(key, currentKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                report is null
                    ? "no validation has been run in this Backend session yet - call validate_drawing first."
                    : $"last cached report is for a different drawing ('{key}'); call validate_drawing on the active document first.");
        }
        return report;
    }

    [McpTool("auto_fix_violations",
        "Apply the fix recipe for every fixable violation in the cached report (or only those for the supplied ruleIds). All fixes run inside a SINGLE plugin transaction (rule 34 §3); failure rolls back the whole batch. Set dryRun=true to preview the planned actions without writing.",
        "validators",
        Intent = new[]
        {
            "napraw bledy walidacji",
            "auto fix violations",
            "apply automatic fixes",
            "wykonaj poprawki automatycznie",
            "fix the report"
        },
        RequiresPlugin = true)]
    public static async Task<AutoFixResult> AutoFixViolations(IPluginGateway gw, AutoFixArgs args, CancellationToken ct)
    {
        var (_, report) = ValidatorsRuntime.GetLastReport();
        if (report is null)
            throw new InvalidOperationException("no cached validation report; call validate_drawing first.");

        IEnumerable<Violation> picked = report.Violations.Where(v => v.FixAvailable && v.EntityHandle is not null);
        if (args.RuleIds is { Length: > 0 })
        {
            var set = new HashSet<string>(args.RuleIds, StringComparer.Ordinal);
            picked = picked.Where(v => set.Contains(v.RuleId));
        }
        var pickedList = picked.ToList();

        var fixes = new List<EntityFixDto>(pickedList.Count);
        foreach (var v in pickedList)
        {
            if (!ValidatorsRuntime.Rules.TryGet(v.RuleId, out var rule) || rule?.Fix is null) continue;
            fixes.Add(new EntityFixDto(v.EntityHandle!, rule.Fix.Type, new Dictionary<string, object?>(rule.Fix.Params)));
        }

        if (args.DryRun)
        {
            var planned = fixes.Select(f => new FixOutcomeDto(
                f.Handle, f.FixType, "manual_only",
                "dry-run: would have requested fix '" + f.FixType + "' on entity " + f.Handle)).ToList();
            return new AutoFixResult(true, fixes.Count, 0, planned);
        }

        if (fixes.Count == 0)
            return new AutoFixResult(false, 0, 0, Array.Empty<FixOutcomeDto>());

        var result = await ValidatorsProxy.CallAsync<ApplyFixesArgs, ApplyFixesResult>(
            gw, "acad.validators.apply_fixes", new ApplyFixesArgs(fixes), T_LARGE, ct).ConfigureAwait(false);

        return new AutoFixResult(false, result.RequestedCount, result.AppliedCount, result.Outcomes);
    }

    [McpTool("add_validator_rule",
        "Persist a brand-new validator rule to the user-rules directory (%LOCALAPPDATA%/AcadMcp/validators/_user/<discipline>/<id>.yaml) and reload the registry. The 'yaml' argument is the full YAML document text. Returns the assigned id and on-disk path.",
        "validators",
        Intent = new[]
        {
            "dodaj nowa regule walidacji",
            "add custom validator rule",
            "register new validator yaml",
            "stworz wlasna regule",
            "create user rule"
        })]
    public static Task<AddRuleResult> AddValidatorRule(AddRuleArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Yaml))
            throw new ArgumentException("yaml body is required.");
        var rule = RuleLoader.LoadFromYaml(args.Yaml, "user-rule:add_validator_rule");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(local, "AcadMcp", "validators", "_user", rule.Discipline.ToString().ToLowerInvariant());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, rule.Id + ".yaml");
        File.WriteAllText(path, args.Yaml);
        ValidatorsRuntime.ReloadRules();
        return Task.FromResult(new AddRuleResult(rule.Id, path));
    }

    [McpTool("check_overlaps",
        "Find pairs of entities whose bounding boxes (or curves, for mode=\"polyline_crosses_polyline\") overlap or intersect. Purely geometric - schema-free, does NOT need a validator rule. " +
        "Intended for the AI visual-review pipeline: e.g. \"which A-DOOR entities actually pierce an A-WALL-* polyline\" or \"which A-ANNO-TEXT labels stack on top of each other\". " +
        "Args: layersA (required, e.g. [\"A-DOOR\"]), layersB (optional, defaults to layersA for self-overlap), mode in { \"bbox_intersect\" (default), \"centroid_in_bbox\", \"polyline_crosses_polyline\" }, tolerance (mm, default 0), optional window rectangle to restrict to a region, maxResults (default 500). " +
        "Result is sorted by severity (critical=2+ curve intersections, major=1 intersection or overlap>10000 sq-mm, minor=smaller overlap) then by overlap area descending. Handles are order-stable across calls.",
        "validators",
        Intent = new[]
        {
            "znajdz nakladajace sie obiekty",
            "check overlaps between layers",
            "find entities that intersect",
            "drzwi przecinajace sciany",
            "doors crossing walls",
            "overlapping labels",
            "text overlaps text",
            "geometric overlap scan",
            "which entities collide",
            "pokaz nakladki"
        },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<CheckOverlapsResult> CheckOverlaps(IPluginGateway gw, CheckOverlapsArgs args, CancellationToken ct)
    {
        if (args.LayersA is null || args.LayersA.Length == 0)
            throw new ArgumentException("layersA is required (non-empty).");
        return ValidatorsProxy.CallAsync<CheckOverlapsArgs, CheckOverlapsResult>(
            gw, "acad.validators.check_overlaps", args, T_LARGE, ct);
    }

    [McpTool("reload_validator_rules",
        "Re-scan embedded resources, <repo>/validators and %LOCALAPPDATA%/AcadMcp/validators for rule YAML files and rebuild the in-process registry. Returns the new total rule count and any load errors.",
        "validators",
        Intent = new[]
        {
            "przeladuj reguly walidacji",
            "reload validator rules",
            "rescan rules folder",
            "odswiez baze regul",
            "refresh validators"
        })]
    public static Task<ReloadResult> ReloadValidatorRules(DocSummaryArgs? _, CancellationToken ct)
    {
        ValidatorsRuntime.ReloadRules();
        return Task.FromResult(new ReloadResult(
            RuleCount: ValidatorsRuntime.Rules.All.Count,
            LoadErrors: ValidatorsRuntime.Rules.LoadErrors));
    }

    // ─────────── helpers ───────────

    private static IReadOnlyList<Rule> SelectRules(string[]? ruleIds, string? discipline, string? minSeverity)
    {
        if (ruleIds is { Length: > 0 })
        {
            var list = new List<Rule>(ruleIds.Length);
            foreach (var id in ruleIds)
            {
                if (ValidatorsRuntime.Rules.TryGet(id, out var r) && r is not null) list.Add(r);
                else throw new InvalidOperationException($"validator rule '{id}' not found.");
            }
            return list;
        }
        var d = ParseDisciplineOrNull(discipline);
        var s = ParseSeverityOrNull(minSeverity);
        return ValidatorsRuntime.Rules.Filter(d, s).ToList();
    }

    private static Discipline? ParseDisciplineOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s!.ToLowerInvariant() switch
        {
            "general" => Discipline.General,
            "architectural" => Discipline.Architectural,
            "mechanical" => Discipline.Mechanical,
            "electrical" => Discipline.Electrical,
            "civil" => Discipline.Civil,
            "mep" => Discipline.Mep,
            "parametric" => Discipline.Parametric,
            _ => throw new ArgumentException($"unknown discipline '{s}'."),
        };

    private static Severity? ParseSeverityOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s!.ToLowerInvariant() switch
        {
            "info" => Severity.Info,
            "warning" => Severity.Warning,
            "error" => Severity.Error,
            _ => throw new ArgumentException($"unknown severity '{s}'."),
        };

    private static string BuildDocKey(ValidationReport r) => BuildDocKey(r.DocumentName, r.DocumentPath);
    private static string BuildDocKey(string name, string? path) => $"{name}||{path ?? ""}";
}

public sealed record ReloadResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("ruleCount")] int RuleCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("loadErrors")] System.Collections.Generic.IReadOnlyList<string> LoadErrors);
