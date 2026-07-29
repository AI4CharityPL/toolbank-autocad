// Parsed validator rule (one YAML file -> one Rule). See rule 33-validators-rule-format.mdc.

using System.Collections.Generic;

namespace AcadMcp.Backend.Validators;

public enum Severity { Info, Warning, Error }

public enum Discipline { General, Architectural, Mechanical, Electrical, Civil, Mep, Parametric }

public sealed class RuleScope
{
    /// <summary>Canonical entity type names (Line, Polyline, Circle, ...). Empty / null = any.</summary>
    public IReadOnlyList<string>? EntityTypes { get; init; }

    /// <summary>.NET regex against entity.Layer. Empty / null = any.</summary>
    public string? LayerPattern { get; init; }

    /// <summary>Explicit list of layers (case-insensitive). Empty / null = any.</summary>
    public IReadOnlyList<string>? LayerIn { get; init; }

    /// <summary>True = paper-space only, false = model-space only (default).</summary>
    public bool InPaperspace { get; init; } = false;
}

/// <summary>Single check primitive. Type drives evaluator selection in CheckEvaluator.</summary>
public sealed class CheckSpec
{
    public string Type { get; init; } = "";

    /// <summary>Free-form params; consumed by the evaluator for this check Type.</summary>
    public IReadOnlyDictionary<string, object?> Params { get; init; } = new Dictionary<string, object?>();

    /// <summary>Pre-built nested specs (for not / any_of / all_of). Empty otherwise.</summary>
    public IReadOnlyList<CheckSpec> Children { get; init; } = System.Array.Empty<CheckSpec>();
}

/// <summary>Single fix primitive. Type drives operation selection in FixApplier.</summary>
public sealed class FixSpec
{
    public string Type { get; init; } = "";
    public IReadOnlyDictionary<string, object?> Params { get; init; } = new Dictionary<string, object?>();
}

public sealed class Rule
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public Severity Severity { get; init; }
    public Discipline Discipline { get; init; }
    public string Description { get; init; } = "";
    public IReadOnlyList<string> References { get; init; } = System.Array.Empty<string>();
    public RuleScope? Scope { get; init; }
    public IReadOnlyList<CheckSpec> Checks { get; init; } = System.Array.Empty<CheckSpec>();
    public FixSpec? Fix { get; init; }

    /// <summary>Path or resource id where the rule was loaded from. Diagnostic only.</summary>
    public string SourceLocation { get; init; } = "";
}
