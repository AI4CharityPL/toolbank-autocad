// AutoCAD plugin handlers for layer filters — roadmap 2.3.
//
// Layer filters are not a symbol table and not a dictionary. They live in a tree hanging off
// Database.LayerFilters, and the whole tree is read out, modified, and assigned back as a unit.
// Forgetting the write-back is the obvious failure here: every property reads correctly on the
// in-memory copy and nothing reaches the drawing, which is exactly the shape of defect this
// bank keeps finding. Every write path below ends in `db.LayerFilters = tree`.
//
// Two kinds share the tree and a caller has to be able to tell them apart:
//
//   PROPERTY filter  carries an expression evaluated against every layer, continuously. A layer
//                    created later that matches the expression joins on its own.
//   GROUP filter     holds a fixed set of layer ids. Nothing joins it without being added.
//
// So list_layer_filters reports `kind`, and reports `matchCount` for both — the number of layers
// each one currently selects. An expression that is syntactically valid and matches nothing is
// the failure a caller most needs to see, and a return code cannot show it.
//
// WITHHELD: apply_layer_filter. The roadmap planned it and there is no managed API for it.
// LayerFilterTree.Current is get-only, there is no LayerFilterManager type, and Database exposes
// only LayerFilters. Which filter the Layer Properties Manager is showing is palette state, not
// something a caller can set from here. A tool named apply_layer_filter that assigned nothing
// would return success and change nothing — the same over-promise as TableStyle.FlowDirection,
// which was withheld for the same reason rather than shipped. list_layer_filters still reports
// isCurrent, because reading it works fine.
//
// For the same reason neither create tool takes makeCurrent: an argument that cannot be honoured
// is worse than an absent one.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.LayerManager;

namespace AcadMcp.Plugin.Tools;

internal static class StylesLayerFilterPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.styles.list_layer_filters", ListLayerFilters);
        host.Register("acad.styles.create_layer_filter", CreateLayerFilter);
        host.Register("acad.styles.create_layer_group_filter", CreateLayerGroupFilter);
        host.Register("acad.styles.delete_layer_filter", DeleteLayerFilter);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    // ─────────── tree walking ───────────

    private static IEnumerable<LayerFilter> Walk(LayerFilter node)
    {
        yield return node;
        foreach (LayerFilter child in node.NestedFilters)
            foreach (var d in Walk(child))
                yield return d;
    }

    private static IEnumerable<LayerFilter> AllFilters(LayerFilterTree tree)
        => tree.Root.NestedFilters.Cast<LayerFilter>().SelectMany(Walk);

    private static LayerFilter? Find(LayerFilterTree tree, string name)
        => AllFilters(tree).FirstOrDefault(
            f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>How many layers does this filter currently select?</summary>
    /// <remarks>
    /// Reported for both kinds because a filter that is accepted, stored and listed but matches
    /// nothing is the outcome a caller most needs to see, and no return code can show it. A
    /// mistyped expression like NAME=="A_WALL*" against A-WALL layers is valid and selects zero.
    /// </remarks>
    private static int MatchCount(Database db, Transaction tr, LayerFilter f)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        var n = 0;
        foreach (ObjectId id in lt)
        {
            var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
            if (f.Filter(ltr)) n++;
        }
        return n;
    }

    private static object Info(Database db, Transaction tr, LayerFilterTree tree, LayerFilter f)
    {
        var isGroup = f is LayerGroup;
        List<string>? layers = null;
        if (f is LayerGroup g)
        {
            layers = new List<string>();
            foreach (ObjectId id in g.LayerIds)
            {
                if (id.IsNull || id.IsErased) continue;
                layers.Add(((LayerTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
            }
            layers.Sort(StringComparer.OrdinalIgnoreCase);
        }

        return new
        {
            name = f.Name,
            kind = isGroup ? "group" : "property",
            expression = isGroup ? null : f.FilterExpression,
            layers,
            matchCount = MatchCount(db, tr, f),
            parent = f.Parent?.Name,
            isCurrent = tree.Current is not null
                        && string.Equals(tree.Current.Name, f.Name, StringComparison.OrdinalIgnoreCase),
            allowDelete = f.AllowDelete,
        };
    }

    /// <summary>Resolve the collection a new filter should be added to.</summary>
    private static LayerFilter ResolveParent(LayerFilterTree tree, string? parentName)
    {
        if (string.IsNullOrWhiteSpace(parentName)) return tree.Root;
        var p = Find(tree, parentName)
            ?? throw new ArgumentException(
                "No layer filter named '" + parentName + "' to nest under. Use list_layer_filters, " +
                "or omit parent to add at the top level.");
        if (!p.AllowNested)
            throw new ArgumentException(
                "Layer filter '" + p.Name + "' does not accept nested filters.");
        return p;
    }

    private static void RejectDuplicate(LayerFilterTree tree, string name, bool overwrite)
    {
        var existing = Find(tree, name);
        if (existing is null) return;
        if (!overwrite)
            throw new ArgumentException(
                "A layer filter named '" + existing.Name + "' already exists. Pass overwrite:true " +
                "to replace it, or pick another name.");
        if (!existing.AllowDelete)
            throw new InvalidOperationException(
                "Layer filter '" + existing.Name + "' is built in and cannot be replaced.");
        (existing.Parent ?? tree.Root).NestedFilters.Remove(existing);
    }

    // ─────────── tools ───────────

    private static Task<ToolDispatchResult> ListLayerFilters(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.styles.list_layer_filters", ct, (doc, db, tr) =>
        {
            var tree = db.LayerFilters;
            var filters = AllFilters(tree).Select(f => Info(db, tr, tree, f)).ToList();
            return Wrap(new { filters, count = filters.Count, current = tree.Current?.Name });
        });

    private static Task<ToolDispatchResult> CreateLayerFilter(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.create_layer_filter", ct, (doc, db, tr) =>
        {
            var a = Read<CreateLayerFilterArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            if (string.IsNullOrWhiteSpace(a.Expression))
                throw new ArgumentException(
                    "expression is required for a property filter. Examples: NAME==\"A-*\" , " +
                    "COLOR==\"1\" , NAME==\"A-*\" AND LOCKED==\"False\". Use " +
                    "create_layer_group_filter instead if you want a fixed list of layers.");

            var tree = db.LayerFilters;
            RejectDuplicate(tree, a.Name, a.Overwrite);
            var parent = ResolveParent(tree, a.Parent);

            var filter = new LayerFilter { Name = a.Name };

            // AutoCAD validates the expression on assignment and throws on a syntax error. Let
            // that surface with the expression quoted back, since "eInvalidInput" alone tells a
            // caller nothing about which part of their string was wrong.
            try
            {
                filter.FilterExpression = a.Expression;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD rejected the filter expression " + a.Expression + " - " + ex.Message +
                    ". Property filters use forms like NAME==\"A-*\" or COLOR==\"1\", combined " +
                    "with AND / OR / NOT.", ex);
            }

            parent.NestedFilters.Add(filter);

            // The tree is a copy until it is assigned back. Without this line every value below
            // reads correctly and the drawing is unchanged.
            db.LayerFilters = tree;

            return Wrap(new { filter = Info(db, tr, tree, filter), created = true });
        });

    private static Task<ToolDispatchResult> CreateLayerGroupFilter(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.create_layer_group_filter", ct, (doc, db, tr) =>
        {
            var a = Read<CreateLayerGroupFilterArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            if (a.Layers is null || a.Layers.Count == 0)
                throw new ArgumentException(
                    "layers is required and must name at least one layer. A group filter holds a " +
                    "fixed set; use create_layer_filter for an expression that keeps matching.");

            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var missing = a.Layers.Where(n => !lt.Has(n)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException(
                    "These layers do not exist: " + string.Join(", ", missing) +
                    ". A group filter can only hold layers that are already in the drawing.");

            var tree = db.LayerFilters;
            RejectDuplicate(tree, a.Name, a.Overwrite);
            var parent = ResolveParent(tree, a.Parent);

            var group = new LayerGroup { Name = a.Name };
            foreach (var n in a.Layers.Distinct(StringComparer.OrdinalIgnoreCase))
                group.LayerIds.Add(lt[n]);

            parent.NestedFilters.Add(group);
            db.LayerFilters = tree;

            return Wrap(new { filter = Info(db, tr, tree, group), created = true });
        });

    private static Task<ToolDispatchResult> DeleteLayerFilter(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.delete_layer_filter", ct, (doc, db, tr) =>
        {
            var a = Read<LayerFilterNameArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");

            var tree = db.LayerFilters;
            var f = Find(tree, a.Name)
                ?? throw new ArgumentException(
                    "No layer filter named '" + a.Name + "'. Use list_layer_filters.");

            if (!f.AllowDelete)
                throw new InvalidOperationException(
                    "Layer filter '" + f.Name + "' is built in and cannot be deleted.");

            // Deleting a parent takes its children with it. Say so rather than letting a caller
            // discover it from a filter count that dropped further than they expected.
            var nested = Walk(f).Skip(1).Select(x => x.Name).ToList();

            (f.Parent ?? tree.Root).NestedFilters.Remove(f);
            db.LayerFilters = tree;

            // Always an array, never null-when-empty. The first version omitted the field when
            // nothing cascaded, which meant a caller could not tell "nothing else was removed"
            // from "this build does not report it" - and that ambiguity is precisely what hid a
            // cascade of two child filters behind a bare {name, deleted:true}.
            return Wrap(new
            {
                name = a.Name,
                deleted = true,
                alsoDeleted = nested,
                current = db.LayerFilters.Current?.Name,
            });
        });
}
