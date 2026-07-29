// Public output shapes for the acad-validators category.
// JSON wire-shapes (snake/camel-mix) are defined separately in Categories/Validators/ValidatorsDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadMcp.Backend.Validators;

public sealed record Violation(
    [property: JsonPropertyName("ruleId")]        string RuleId,
    [property: JsonPropertyName("ruleName")]      string RuleName,
    [property: JsonPropertyName("severity")]      string Severity,         // "error" | "warning" | "info"
    [property: JsonPropertyName("discipline")]    string Discipline,       // "general" | "architectural" | ...
    [property: JsonPropertyName("entityHandle")]  string? EntityHandle,    // null for doc-level
    [property: JsonPropertyName("dxfType")]       string? DxfType,
    [property: JsonPropertyName("layer")]         string? Layer,
    [property: JsonPropertyName("expected")]      string Expected,
    [property: JsonPropertyName("observed")]      string Observed,
    [property: JsonPropertyName("message")]       string Message,
    [property: JsonPropertyName("fixAvailable")]  bool FixAvailable);

public sealed record RuleSummary(
    [property: JsonPropertyName("ruleId")]   string RuleId,
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("count")]    int Count);

public sealed record ValidationReport(
    [property: JsonPropertyName("documentName")]  string DocumentName,
    [property: JsonPropertyName("documentPath")]  string? DocumentPath,
    [property: JsonPropertyName("rulesEvaluated")] int RulesEvaluated,
    [property: JsonPropertyName("entitiesScanned")] int EntitiesScanned,
    [property: JsonPropertyName("violationCount")] int ViolationCount,
    [property: JsonPropertyName("errorCount")]    int ErrorCount,
    [property: JsonPropertyName("warningCount")]  int WarningCount,
    [property: JsonPropertyName("infoCount")]     int InfoCount,
    [property: JsonPropertyName("perRule")]       IReadOnlyList<RuleSummary> PerRule,
    [property: JsonPropertyName("violations")]    IReadOnlyList<Violation> Violations,
    [property: JsonPropertyName("loadErrors")]    IReadOnlyList<string> LoadErrors,
    [property: JsonPropertyName("elapsedMs")]     long ElapsedMs);
