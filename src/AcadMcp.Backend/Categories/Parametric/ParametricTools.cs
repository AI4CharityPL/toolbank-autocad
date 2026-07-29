// acad-parametric: constraint-entity inventory, dynamic BlockReference
// property get/set, and the P-* layer key (rule 42).
//
// Constraint-APPLICATION tools (apply_geom_*, apply_dim_*, delete_entity_
// constraints) were removed from this category's exposed surface on
// 2026-07-29 after live-testing against real AutoCAD 2025 found that every
// one of them -- the 6 that shipped originally plus 5 more geometric
// constraint types and 2 dimensional constraint types added later -- fails
// with Autodesk.AutoCAD.Runtime.Exception: eInvalidInput, thrown from
// Editor.Command itself. Four independent fix attempts were tried and all
// reproduce the identical failure: (1) passing the resolved ObjectId
// directly as the entity-pick answer, (2) resolving to a Point3d on the
// entity's own geometry instead, (3) swapping the "._-"/"_.-" command-prefix
// character order, and (4) using the AutoCAD command name "GEOMCONSTRAINT"
// instead of "GEOCONSTRAINT". None of these narrowed down the actual root
// cause -- Editor.Command rejects the whole command line immediately (~300ms,
// no interactive back-and-forth) with no further diagnostic detail available
// through the .NET API. Rather than ship tools that are advertised in the
// manifest but don't work, they've been pulled from this category's exposed
// surface until the root cause is found and a real fix is verified live.
// The implementation attempts are preserved in
// src/AcadMcp.Plugin/Tools/ParametricPluginTools.cs for whoever picks this
// back up (not wired to the pipe, so they cannot be called even directly).
//
// BEDIT-scoped constraint authoring and DOF (degrees-of-freedom) reporting
// are also not implemented: AutoCAD's .NET API does not expose the
// constraint solver's DOF count directly, and BEDIT entry/exit hasn't been
// verified deadlock-safe (same class of risk as the checkpoint UNDO-command
// deadlock, rule 10). Angle dynamic properties on set_dynamic_block_property
// accept JSON numbers in degrees (rule 42 §5) and the plugin converts to
// radians internally.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Parametric;

public static class ParametricTools
{
    public const string DynamicAnglePolicy =
        "For DynamicBlockReferenceProperty with UnitsType=Angle, pass JSON numbers as degrees; the plugin converts to radians.";

    private static T Deserialize<T>(JsonNode node) where T : class =>
        node.Deserialize<T>(ParametricProxy.Opts)
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");

    private static JsonObject ToArgs<T>(T dto) =>
        JsonSerializer.SerializeToNode(dto, ParametricProxy.Opts) as JsonObject
        ?? throw new InvalidOperationException("Args serialization failed.");

    // ─────────── infrastructure ───────────

    [McpTool("ensure_parametric_layers",
        "Idempotently create the 6-layer parametric sketch key (P-CONSTRUCTION, P-SKETCH, P-CONSTRAINED, P-DYNAMIC, P-PARAM-LBL, P-NOTE) per rule 42 §9 with prescribed ACI colour, Continuous linetype, and lineweight. Existing layers are never overwritten.",
        "parametric",
        Intent = new[]
        {
            "stworz warstwy P- parametric",
            "ensure parametric layer key",
            "warstwy szkicu parametrycznego",
            "setup P-CONSTRAINED layer",
            "parametric drafting layers"
        },
        RequiresPlugin = true)]
    public static async Task<EnsureParametricLayersResult> EnsureParametricLayers(
        IPluginGateway gw, EnsureParametricLayersArgs _, CancellationToken ct)
    {
        var existing = await ParametricProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var outcomes = new List<LayerEnsureOutcome>();
        int created = 0, already = 0;
        foreach (var spec in ParametricPalette.All)
        {
            try
            {
                var did = await ParametricProxy.EnsureLayerAsync(
                    gw, existing, spec.Name, spec.AciColor, spec.Linetype,
                    spec.LineweightMm, spec.Plottable, ct).ConfigureAwait(false);
                if (did) created++; else already++;
                outcomes.Add(new LayerEnsureOutcome(
                    spec.Name, did ? "created" : "already_exists",
                    spec.AciColor, spec.Linetype, spec.LineweightMm));
            }
            catch (Exception ex)
            {
                outcomes.Add(new LayerEnsureOutcome(
                    spec.Name, "failed", spec.AciColor, spec.Linetype, spec.LineweightMm, ex.Message));
            }
        }
        return new EnsureParametricLayersResult(outcomes, created, already);
    }

    // ─────────── inventory ───────────

    [McpTool("list_constraint_entities",
        "Scan model space for database objects whose runtime class name contains 'Constraint' (constraint proxy / glyph entities). Optional layerFilter narrows results. Read-only with respect to geometry — still requires the plugin for DB access.",
        "parametric",
        Intent = new[]
        {
            "lista encji constraint",
            "list constraint proxies",
            "find geom constraints in drawing",
            "inventory parametric constraints",
            "which constraints exist model space"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static async Task<ListConstraintEntitiesResult> ListConstraintEntities(
        IPluginGateway gw, ListConstraintEntitiesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.list_constraint_entities", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<ListConstraintEntitiesResult>(node);
    }

    // ─────────── dynamic blocks ───────────

    [McpTool("get_dynamic_block_properties",
        "Read all DynamicBlockReferenceProperty entries from a BlockReference handle: names, read-only flags, UnitsType, CLR type, and current Value. isDynamicBlock=false returns an empty list — the handle is still a block insert but not dynamic. Use the reference handle, never hard-code anonymous *U block names (rule 42 §6).",
        "parametric",
        Intent = new[]
        {
            "jakie ma wlasciwosci dynamiczny blok",
            "list dynamic block properties",
            "read dynamic visibility distance",
            "get BlockReference parameters",
            "inspect dynamic block handle"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static async Task<GetDynamicBlockPropertiesResult> GetDynamicBlockProperties(
        IPluginGateway gw, GetDynamicBlockPropertiesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.get_dynamic_block_properties", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<GetDynamicBlockPropertiesResult>(node);
    }

    [McpTool("set_dynamic_block_property",
        "Write one DynamicBlockReferenceProperty on a BlockReference by name. Pass JSON booleans as true/false, numbers as JSON numbers. For Angle-typed properties the numeric value is interpreted as degrees and converted to radians in the plugin (see parametric_health.dynamicAnglePolicy). Strings are for lookup / text parameters. Read-only properties throw.",
        "parametric",
        Intent = new[]
        {
            "ustaw visibility dynamic block",
            "set dynamic block distance",
            "zmien parametr bloku dynamicznego",
            "write BlockReference dynamic property",
            "toggle dynamic block visibility state"
        },
        RequiresPlugin = true)]
    public static async Task<SetDynamicBlockPropertyResult> SetDynamicBlockProperty(
        IPluginGateway gw, SetDynamicBlockPropertyArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.set_dynamic_block_property", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SetDynamicBlockPropertyResult>(node);
    }

    // ─────────── introspection ───────────

    [McpTool("parametric_health",
        "Return the 6-layer P-* parametric key and the dynamic-block angle value policy string. Does not open AutoCAD.",
        "parametric",
        Intent = new[]
        {
            "co potrafi parametric",
            "parametric category metadata",
            "P-SKETCH layer list",
            "parametric MCP info",
            "dynamic block angle degrees policy"
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static ParametricHealthResult ParametricHealth(ParametricHealthArgs _)
    {
        var layers = new List<ParametricLayerSpec>();
        foreach (var s in ParametricPalette.All)
            layers.Add(new ParametricLayerSpec(s.Name, s.AciColor, s.Linetype, s.LineweightMm, s.Plottable, s.Purpose));
        return new ParametricHealthResult(
            layers, ParametricPalette.PlannedBlocks, "parametric", "0.1.0", DynamicAnglePolicy);
    }
}
