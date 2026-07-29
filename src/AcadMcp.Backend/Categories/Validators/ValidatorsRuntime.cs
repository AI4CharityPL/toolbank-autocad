// Process-singleton state for the acad-validators category.
// Holds the rule registry, the standards library, and the most-recent ValidationReport
// (keyed by document path / name to comply with rule 34 §9).

using System.Threading;
using AcadMcp.Backend.Mcp;
using AcadMcp.Backend.Validators;

namespace AcadMcp.Backend.Categories.Validators;

internal static class ValidatorsRuntime
{
    private static readonly object Sync = new();
    private static RuleRegistry? _rules;
    private static StandardLibrary? _standards;

    public static RuleRegistry Rules
    {
        get
        {
            if (_rules is null) lock (Sync) _rules ??= new RuleRegistry();
            return _rules!;
        }
    }

    public static StandardLibrary Standards
    {
        get
        {
            if (_standards is null) lock (Sync) _standards ??= new StandardLibrary();
            return _standards!;
        }
    }

    // Last-report cache keyed by document identifier (rule 34 §9).
    private static string? _lastDocKey;
    private static ValidationReport? _lastReport;

    public static void StoreReport(string docKey, ValidationReport report)
    {
        lock (Sync) { _lastDocKey = docKey; _lastReport = report; }
    }

    public static (string? docKey, ValidationReport? report) GetLastReport()
    {
        lock (Sync) return (_lastDocKey, _lastReport);
    }

    /// <summary>Force re-creation of the rule registry (used after add_validator_rule writes a new YAML).</summary>
    public static void ReloadRules()
    {
        lock (Sync) { _rules = new RuleRegistry(); }
    }
}
