// Phase 7.2 regression: loader + evaluator smoke tests for the 4 new primitives
// (entity_class_equals, text_matches_regex, polyline_closure_within,
// polyline_endpoints_share) and the whole validators/ YAML folder.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcadMcp.Backend.Categories.Validators;
using AcadMcp.Backend.Validators;
using Xunit;

namespace AcadMcp.Tests.Validators;

public class ValidatorsCoreTests
{
    // ─────────── RuleLoader smoke test across the whole validators folder ───────────

    [Fact]
    public void Every_yaml_under_validators_folder_parses()
    {
        var root = LocateValidatorsRoot();
        var errors = new List<string>();
        int loaded = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories))
        {
            // Skip the _standards bundles - they have a different schema.
            if (file.Contains(Path.Combine("validators", "_standards"), StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var rule = RuleLoader.LoadFromYaml(File.ReadAllText(file), file);
                Assert.False(string.IsNullOrWhiteSpace(rule.Id));
                Assert.NotEmpty(rule.Checks);
                loaded++;
            }
            catch (Exception ex)
            {
                errors.Add(file + " -> " + ex.Message);
            }
        }
        Assert.True(errors.Count == 0, "rule load errors:\n" + string.Join("\n", errors));
        Assert.True(loaded >= 20, $"expected at least 20 rules across domains, loaded {loaded}.");
    }

    [Fact]
    public void All_four_new_phase_7_2_rules_are_discoverable()
    {
        var root = LocateValidatorsRoot();
        var ids = Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine("validators", "_standards"), StringComparison.OrdinalIgnoreCase))
            .Select(f => RuleLoader.LoadFromYaml(File.ReadAllText(f), f).Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("mech.thread.arc-not-circle", ids);
        Assert.Contains("elec.tag.iec-81346-format", ids);
        Assert.Contains("civil.parcel.closure-tolerance", ids);
        Assert.Contains("elec.wire.crossing-needs-junction", ids);
    }

    // ─────────── entity_class_equals ───────────

    [Fact]
    public void Entity_class_equals_fires_on_circle_when_arc_expected()
    {
        var check = new CheckSpec
        {
            Type = "entity_class_equals",
            Params = new Dictionary<string, object?> { ["value"] = "AcDbArc" },
        };
        var circle = Snapshot(dxf: "Circle", className: "AcDbCircle");
        var arc = Snapshot(dxf: "Arc", className: "AcDbArc");

        Assert.False(CheckEvaluator.Evaluate(check, circle, new EvalContext()).Pass);
        Assert.True(CheckEvaluator.Evaluate(check, arc, new EvalContext()).Pass);
    }

    [Fact]
    public void Entity_class_equals_skips_when_class_metadata_missing()
    {
        var check = new CheckSpec
        {
            Type = "entity_class_equals",
            Params = new Dictionary<string, object?> { ["value"] = "AcDbArc" },
        };
        var noClass = Snapshot(dxf: "Circle", className: null);
        Assert.True(CheckEvaluator.Evaluate(check, noClass, new EvalContext()).Pass);
    }

    // ─────────── text_matches_regex (entity text + attribute target) ───────────

    [Fact]
    public void Text_matches_regex_rejects_malformed_iec_tag_attribute()
    {
        var check = new CheckSpec
        {
            Type = "text_matches_regex",
            Params = new Dictionary<string, object?>
            {
                ["pattern"] = @"^-[KQFMTXHB]\d+(\.\d+)?$",
                ["attribute"] = "TAG",
            },
        };
        var good = Snapshot(dxf: "BlockReference", attributes: new() { ["TAG"] = "-K12" });
        var bad  = Snapshot(dxf: "BlockReference", attributes: new() { ["TAG"] = "relay1" });
        var missing = Snapshot(dxf: "BlockReference", attributes: new() { ["OTHER"] = "x" });

        Assert.True(CheckEvaluator.Evaluate(check, good, new EvalContext()).Pass);
        Assert.False(CheckEvaluator.Evaluate(check, bad, new EvalContext()).Pass);
        Assert.False(CheckEvaluator.Evaluate(check, missing, new EvalContext()).Pass);
    }

    [Fact]
    public void Text_matches_regex_falls_back_to_text_value_when_no_attribute()
    {
        var check = new CheckSpec
        {
            Type = "text_matches_regex",
            Params = new Dictionary<string, object?> { ["pattern"] = @"^-[KQFMTXHB]\d+$" },
        };
        var good = Snapshot(dxf: "Text", text: "-Q3");
        var bad  = Snapshot(dxf: "Text", text: "Q3");
        Assert.True(CheckEvaluator.Evaluate(check, good, new EvalContext()).Pass);
        Assert.False(CheckEvaluator.Evaluate(check, bad, new EvalContext()).Pass);
    }

    // ─────────── polyline_closure_within ───────────

    [Fact]
    public void Polyline_closure_within_passes_when_gap_below_tolerance()
    {
        var check = new CheckSpec
        {
            Type = "polyline_closure_within",
            Params = new Dictionary<string, object?> { ["tolerance"] = 0.001 },
        };
        var closed = Snapshot(dxf: "Polyline", vertices: new[]
        {
            new[] { 0.0, 0.0, 0.0 },
            new[] { 10.0, 0.0, 0.0 },
            new[] { 10.0, 10.0, 0.0 },
            new[] { 0.0, 0.0, 0.0 },
        });
        var openWide = Snapshot(dxf: "Polyline", vertices: new[]
        {
            new[] { 0.0, 0.0, 0.0 },
            new[] { 10.0, 0.0, 0.0 },
            new[] { 10.0, 10.0, 0.0 },
            new[] { 0.5, 0.0, 0.0 },
        });

        Assert.True(CheckEvaluator.Evaluate(check, closed, new EvalContext()).Pass);
        Assert.False(CheckEvaluator.Evaluate(check, openWide, new EvalContext()).Pass);
    }

    [Fact]
    public void Polyline_closure_within_skips_entities_without_vertex_data()
    {
        var check = new CheckSpec
        {
            Type = "polyline_closure_within",
            Params = new Dictionary<string, object?> { ["tolerance"] = 0.001 },
        };
        var noVerts = Snapshot(dxf: "Polyline", vertices: null);
        Assert.True(CheckEvaluator.Evaluate(check, noVerts, new EvalContext()).Pass);
    }

    // ─────────── polyline_endpoints_share (cross-entity) ───────────

    [Fact]
    public void Polyline_endpoints_share_requires_junction_at_both_ends()
    {
        var wire = Snapshot(dxf: "Polyline", layer: "E-WIRE-CTRL", vertices: new[]
        {
            new[] { 0.0, 0.0, 0.0 },
            new[] { 100.0, 0.0, 0.0 },
        });
        var jctA = Snapshot(dxf: "BlockReference", blockName: "JUNCTION",
            bboxMin: new[] { -1.0, -1.0, 0.0 }, bboxMax: new[] { 1.0, 1.0, 0.0 });
        var jctB = Snapshot(dxf: "BlockReference", blockName: "JUNCTION",
            bboxMin: new[] { 99.0, -1.0, 0.0 }, bboxMax: new[] { 101.0, 1.0, 0.0 });
        var unrelated = Snapshot(dxf: "BlockReference", blockName: "RESISTOR",
            bboxMin: new[] { 50.0, -1.0, 0.0 }, bboxMax: new[] { 52.0, 1.0, 0.0 });

        var check = new CheckSpec
        {
            Type = "polyline_endpoints_share",
            Params = new Dictionary<string, object?>
            {
                ["tolerance"] = 2.5,
                ["block_name"] = "JUNCTION",
                ["require"] = true,
            },
        };

        var ctx = new EvalContext { AllEntities = new[] { wire, jctA, jctB, unrelated } };
        Assert.True(CheckEvaluator.Evaluate(check, wire, ctx).Pass);

        // Remove one endpoint -> must now fail (only 1/2 matched).
        ctx.AllEntities = new[] { wire, jctA, unrelated };
        Assert.False(CheckEvaluator.Evaluate(check, wire, ctx).Pass);
    }

    // ─────────── helpers ───────────

    private static EntitySnapshotDto Snapshot(
        string dxf,
        string? className = null,
        string layer = "0",
        string? text = null,
        Dictionary<string, string>? attributes = null,
        string? blockName = null,
        double[][]? vertices = null,
        double[]? bboxMin = null,
        double[]? bboxMax = null)
    {
        return new EntitySnapshotDto(
            Handle: "0",
            DxfType: dxf,
            ClassName: className,
            Layer: layer,
            ColorAci: null,
            ColorRgb: null,
            Linetype: "",
            LineweightMm: null,
            Length: null,
            Area: null,
            Radius: null,
            TextValue: text,
            TextHeight: null,
            BlockName: blockName,
            Attributes: attributes,
            Vertices: vertices,
            BboxMin: bboxMin ?? new[] { 0.0, 0.0, 0.0 },
            BboxMax: bboxMax ?? new[] { 0.0, 0.0, 0.0 },
            InPaperspace: false);
    }

    private static string LocateValidatorsRoot()
    {
        // Walk up from the test binary directory until we see the *real* validators
        // folder (the one with per-discipline subfolders). The backend embeds these as
        // resources, so there may be other stub `validators` dirs in copy chains; we
        // only accept the one with discipline subfolders.
        var dir = AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var visited = new List<string>();
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            visited.Add(dir);
            var candidate = Path.Combine(dir, "validators");
            if (Directory.Exists(candidate) &&
                Directory.Exists(Path.Combine(candidate, "mechanical")) &&
                Directory.Exists(Path.Combine(candidate, "electrical")))
                return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            "could not locate `validators/` folder. Searched: " + string.Join(" -> ", visited));
    }
}
