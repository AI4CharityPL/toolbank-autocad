// acad-parametric: geometric constraints (native -GEOCONSTRAINT via plugin
// Editor.Command) -- Horizontal/Vertical/Parallel/Perpendicular/Coincident/Fix
// plus Tangent/Concentric/Collinear/Equal/Symmetric added Phase 7.3 -- linear
// and aligned dimensional constraints (-DIMCONSTRAINT, point-pair form) added
// Phase 7.3, DELCONSTRAINT cleanup, constraint-entity inventory, dynamic
// BlockReference property get/set, and the P-* layer key (rule 42).
//
// KNOWN DEFECT, found live 2026-07-29, NOT YET FIXED: every constraint-
// APPLICATION tool in this category (all 11 apply_geom_* + both apply_dim_*,
// including the original 6 that predate Phase 7.3) fails against real
// AutoCAD 2025 with Autodesk.AutoCAD.Runtime.Exception: eInvalidInput, thrown
// from Editor.Command itself. This was never caught before because this
// category's test coverage was catalog/shape-level only (tool names, arg
// counts, palette contents) -- nothing exercised these tools against a live
// drawing until this pass. Three independent fix attempts were tried and
// ALL reproduce the identical failure: (1) passing the resolved ObjectId
// directly as the entity-pick answer, (2) resolving to a Point3d on the
// entity's own geometry (ResolvePointOnEntity in ParametricPluginTools.cs)
// instead, and (3) swapping the "._-"/"_.-" command-prefix character order.
// list_constraint_entities, get/set_dynamic_block_property, and
// ensure_parametric_layers are UNAFFECTED (they don't drive Editor.Command
// for GEOCONSTRAINT/DELCONSTRAINT/DIMCONSTRAINT). Root cause is still open --
// candidates not yet tried: the AutoCAD Parametric extension/module may need
// explicit loading (ARXLOAD) before these commands work via automation even
// though they parse fine interactively; the prompt sequence may need a
// trailing "" or "\n" terminator not currently sent; or -GEOCONSTRAINT may
// simply not be scriptable via Editor.Command's object[] mechanism at all in
// this AutoCAD build, requiring a different automation approach (e.g. driving
// the dialog-based GEOMCONSTRAINT variant via COM, or a lower-level ObjectARX
// call) than this file currently attempts. Do NOT re-attempt fixes here
// without live AutoCAD access to inspect the ACTUAL failing prompt -- three
// blind attempts already burned a full build/redeploy/relaunch cycle each
// with no diagnostic detail beyond the bare eInvalidInput error code.
//
// v1/Phase 7.3 limitations, otherwise: dimensional constraints only support
// the point-pair invocation form, not "Object" pick mode. BEDIT-scoped
// constraint authoring and DOF (degrees-of-freedom) reporting remain
// unimplemented -- BEDIT requires entering/exiting the block editor
// transactionally in a way this plugin hasn't verified is deadlock-safe yet
// (same class of risk as the checkpoint UNDO-command deadlock, rule 10), and
// AutoCAD's .NET API does not expose the constraint solver's DOF count
// directly -- computing it would mean reimplementing a chunk of the solver's
// bookkeeping heuristically, which risks being confidently wrong rather than
// honestly absent. Angle dynamic properties accept JSON numbers in degrees
// (rule 42 §5) and the plugin converts to radians internally.

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

    // ─────────── geometric constraints ───────────

    [McpTool("apply_geom_horizontal",
        "Apply a Horizontal geometric constraint to one line-like entity in the current space using AutoCAD transparent -GEOCONSTRAINT. The entity handle must reference a Line, polyline segment, or other object the solver accepts for Horizontal. Runs outside an MCP transaction — AutoCAD owns the command transaction.",
        "parametric",
        Intent = new[]
        {
            "wymus pozioma linie constraint",
            "horizontal geom constraint",
            "GC horizontal",
            "rownolegle do osi X",
            "apply horizontal constraint"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomHorizontal(
        IPluginGateway gw, SingleHandleArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_horizontal", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_vertical",
        "Apply a Vertical geometric constraint to one line-like entity in the current space using transparent -GEOCONSTRAINT. Complements apply_geom_horizontal; do not stack both on the same line unless the office standard requires it.",
        "parametric",
        Intent = new[]
        {
            "wymus pionowa linie",
            "vertical geom constraint",
            "GC vertical",
            "apply vertical constraint",
            "make line vertical parametric"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomVertical(
        IPluginGateway gw, SingleHandleArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_vertical", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_parallel",
        "Apply a Parallel geometric constraint between two curve entities (handles a and b) via transparent -GEOCONSTRAINT. Both entities must live in the same current space; mixed paper-space / block-context picks are undefined — resolve handles from the active viewport context first.",
        "parametric",
        Intent = new[]
        {
            "rownolegle dwie linie constraint",
            "parallel geom constraint",
            "GC parallel",
            "apply parallel constraint",
            "force two lines parallel"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomParallel(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_parallel", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_perpendicular",
        "Apply a Perpendicular geometric constraint between two curves via transparent -GEOCONSTRAINT. Common pitfall: picking two lines that are already parallel to the UCS axes — the solver may report redundant constraints (rule 42 §3).",
        "parametric",
        Intent = new[]
        {
            "prostopadle linie constraint",
            "perpendicular geom constraint",
            "GC perpendicular",
            "apply perpendicular constraint",
            "90 degree constraint two lines"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomPerpendicular(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_perpendicular", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_coincident",
        "Apply a Coincident geometric constraint between two picks (handles a and b) via transparent -GEOCONSTRAINT. Works best on endpoints / points the solver can merge; whole-entity picks may fail depending on AutoCAD build. If the command rejects the pick set, constrain boundary polylines instead of hatch (rule 42 §8).",
        "parametric",
        Intent = new[]
        {
            "polacz punkty coincident",
            "coincident geom constraint",
            "GC coincident",
            "apply coincident constraint",
            "merge endpoints coincident"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomCoincident(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_coincident", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_fix",
        "Apply a Fix geometric constraint to anchor one entity (datum behaviour per rule 42 §2). Call once per sketch for the construction corner — do not Fix every entity or the drawing becomes over-constrained.",
        "parametric",
        Intent = new[]
        {
            "zablokuj punkt odniesienia fix",
            "fix geom constraint",
            "GC fix",
            "anchor sketch fix constraint",
            "datum fix one entity"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomFix(
        IPluginGateway gw, SingleHandleArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_fix", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_tangent",
        "Apply a Tangent geometric constraint between two curve entities (handles a and b) via transparent -GEOCONSTRAINT. Typical use: a line tangent to an arc/circle, or two arcs tangent to each other.",
        "parametric",
        Intent = new[]
        {
            "styczna do okregu constraint",
            "tangent geom constraint",
            "GC tangent",
            "apply tangent constraint",
            "line tangent to arc"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomTangent(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_tangent", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_concentric",
        "Apply a Concentric geometric constraint between two circular/arc entities (handles a and b) via transparent -GEOCONSTRAINT -- forces their centre points to coincide.",
        "parametric",
        Intent = new[]
        {
            "wspolosiowe okregi constraint",
            "concentric geom constraint",
            "GC concentric",
            "apply concentric constraint",
            "same center circles"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomConcentric(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_concentric", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_collinear",
        "Apply a Collinear geometric constraint between two line-like entities (handles a and b) via transparent -GEOCONSTRAINT -- forces them onto the same infinite line.",
        "parametric",
        Intent = new[]
        {
            "wspolliniowe odcinki constraint",
            "collinear geom constraint",
            "GC collinear",
            "apply collinear constraint",
            "two lines on same line"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomCollinear(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_collinear", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_equal",
        "Apply an Equal geometric constraint between two entities of the same kind (handles a and b) via transparent -GEOCONSTRAINT -- two lines get equal length, two circles/arcs get equal radius.",
        "parametric",
        Intent = new[]
        {
            "rowna dlugosc lub promien constraint",
            "equal geom constraint",
            "GC equal",
            "apply equal constraint",
            "same length two lines"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomEqual(
        IPluginGateway gw, TwoHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_equal", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_geom_symmetric",
        "Apply a Symmetric geometric constraint making entities a and b mirror images of each other about symmetryLine, via transparent -GEOCONSTRAINT. All three handles must already exist in the current space.",
        "parametric",
        Intent = new[]
        {
            "symetria wzgledem osi constraint",
            "symmetric geom constraint",
            "GC symmetric",
            "apply symmetric constraint",
            "mirror constraint about line"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyGeomSymmetric(
        IPluginGateway gw, ThreeHandlesArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.geom_symmetric", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    // ─────────── dimensional constraints ───────────

    [McpTool("apply_dim_linear",
        "Apply a Linear dimensional constraint between point1 and point2 via transparent -DIMCONSTRAINT, with its dimension text placed at placementPoint. Unlike geometric constraints, a dimensional constraint carries a numeric value that DRIVES the geometry -- editing it after creation moves whatever it's attached to. Point-pair form only (not the 'Object' pick mode -- see plugin source comment for why).",
        "parametric",
        Intent = new[]
        {
            "wymiar parametryczny liniowy",
            "linear dimensional constraint",
            "DC linear",
            "apply dimensional constraint",
            "parametric distance dimension"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyDimLinear(
        IPluginGateway gw, DimConstraintArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.dim_linear", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("apply_dim_aligned",
        "Apply an Aligned dimensional constraint between point1 and point2 via transparent -DIMCONSTRAINT (dimension line stays parallel to the point1-point2 line, unlike apply_dim_linear which is always horizontal/vertical), with its dimension text placed at placementPoint.",
        "parametric",
        Intent = new[]
        {
            "wymiar parametryczny rownolegly",
            "aligned dimensional constraint",
            "DC aligned",
            "apply aligned dimensional constraint",
            "parametric aligned distance"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> ApplyDimAligned(
        IPluginGateway gw, DimConstraintArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.dim_aligned", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
    }

    [McpTool("delete_entity_constraints",
        "Run transparent -DELCONSTRAINT on one entity handle to strip geometric/dimensional constraints attached to that object. Use before explode-freeze workflows or when rebuilding a sketch (rule 42 §4 — explode orphans constraints differently).",
        "parametric",
        Intent = new[]
        {
            "usun constrainty z obiektu",
            "delete constraints on entity",
            "DELCONSTRAINT handle",
            "strip parametric constraints",
            "remove geometric constraints from line"
        },
        RequiresPlugin = true)]
    public static async Task<SimpleOkResult> DeleteEntityConstraints(
        IPluginGateway gw, SingleHandleArgs args, CancellationToken ct)
    {
        var node = await ParametricProxy.InvokeAsync(gw, "acad.parametric.delete_entity_constraints", ToArgs(args), ct).ConfigureAwait(false);
        return Deserialize<SimpleOkResult>(node);
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
        "Return the 6-layer P-* parametric key, planned Phase-7 block roster, and the dynamic-block angle value policy string. Does not open AutoCAD.",
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
