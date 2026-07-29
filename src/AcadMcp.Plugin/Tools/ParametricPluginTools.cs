// AutoCAD plugin handlers for acad-parametric: geometric constraints via the
// native transparent -GEOCONSTRAINT command (Editor.Command), DELCONSTRAINT
// cleanup, constraint-entity inventory, and dynamic BlockReference property
// get/set. Commands run with NO active transaction (rule 11 — AutoCAD
// commands manage their own transactions).
//
// Rules: 10 (UI thread), 12 (error mapping), 42-parametric-domain-traps.mdc.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class ParametricPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.parametric.geom_horizontal", GeomHorizontal);
        host.Register("acad.parametric.geom_vertical", GeomVertical);
        host.Register("acad.parametric.geom_parallel", GeomParallel);
        host.Register("acad.parametric.geom_perpendicular", GeomPerpendicular);
        host.Register("acad.parametric.geom_coincident", GeomCoincident);
        host.Register("acad.parametric.geom_fix", GeomFix);
        host.Register("acad.parametric.geom_tangent", GeomTangent);
        host.Register("acad.parametric.geom_concentric", GeomConcentric);
        host.Register("acad.parametric.geom_collinear", GeomCollinear);
        host.Register("acad.parametric.geom_symmetric", GeomSymmetric);
        host.Register("acad.parametric.geom_equal", GeomEqual);
        host.Register("acad.parametric.dim_linear", DimLinear);
        host.Register("acad.parametric.dim_aligned", DimAligned);
        host.Register("acad.parametric.delete_entity_constraints", DeleteEntityConstraints);
        host.Register("acad.parametric.list_constraint_entities", ListConstraintEntities);
        host.Register("acad.parametric.get_dynamic_block_properties", GetDynamicBlockProperties);
        host.Register("acad.parametric.set_dynamic_block_property", SetDynamicBlockProperty);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    /// <summary>Resolve handles inside a short transaction, commit, then run
    /// <see cref="Editor.Command"/> outside any transaction.</summary>
    private static Task<ToolDispatchResult> RunEditorCommand(
        string toolKey, JsonObject args, CancellationToken ct, params Func<Database, Transaction, object>[] argFactories)
    {
        return RunEditorCommand(toolKey, args, ct, (db, tr) =>
        {
            var list = new List<object>(capacity: argFactories.Length + 1);
            foreach (var f in argFactories)
                list.Add(f(db, tr)!);
            return list.ToArray();
        });
    }

    private static async Task<ToolDispatchResult> RunEditorCommand(
        string toolKey,
        JsonObject args,
        CancellationToken ct,
        Func<Database, Transaction, object[]> buildCommandArgs)
    {
        try
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document.");
            using var docLock = doc.LockDocument();
            var json = await UiThreadDispatcher.Run(() =>
            {
                var db = doc.Database;
                object[] cmdArgs;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    cmdArgs = buildCommandArgs(db, tr);
                    tr.Commit();
                }
                doc.Editor.Command(cmdArgs);
                return new JsonObject { ["ok"] = true };
            }, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail(toolKey, ex); }
    }

    private static Task<ToolDispatchResult> RunInTransaction(
        string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static async Task<ToolDispatchResult> RunInTransactionAsync(
        string toolKey,
        JsonObject args,
        CancellationToken ct,
        Func<Document, Database, Transaction, Task<JsonObject>> work)
    {
        try
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                      ?? throw new InvalidOperationException("No active document.");
            using var docLock = doc.LockDocument();
            var json = await UiThreadDispatcher.Run(() =>
            {
                var db = doc.Database;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var result = work(doc, db, tr).GetAwaiter().GetResult();
                    tr.Commit();
                    return result;
                }
            }, ct).ConfigureAwait(false);
            return new ToolDispatchResult(true, json, null);
        }
        catch (Exception ex) { return AcadErrorMapper.Fail(toolKey, ex); }
    }

    // ─────────── geometric constraints (-GEOCONSTRAINT) ───────────
    //
    // Verified live 2026-07-29: passing an ObjectId directly as the answer to
    // GEOCONSTRAINT's "Select an object or [2Points]" prompt fails with
    // eInvalidInput -- reproduced on EVERY constraint type including the
    // pre-existing Horizontal/Vertical/Parallel/Perpendicular/Coincident/Fix
    // ones, which had never actually been exercised against real AutoCAD
    // before (their test coverage was catalog/shape-level only). The prompt
    // wants a PICK POINT that resolves to an entity, not the entity's
    // ObjectId directly, so every constraint below now resolves its handle(s)
    // to a real point ON the entity (ResolvePointOnEntity) instead.

    private static Point3d ResolvePointOnEntity(Database db, Transaction tr, string handleStr)
    {
        var id = AcadEnv.ResolveHandle(db, handleStr);
        var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
        if (ent is Curve curve)
        {
            try { return curve.GetPointAtParameter(curve.StartParam); }
            catch { /* fall through to bbox-based fallback below */ }
        }
        var ext = ent.GeometricExtents;
        return new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2.0, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, 0);
    }

    private static Task<ToolDispatchResult> GeomHorizontal(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_horizontal", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Horizontal",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<HandleArgDto>(args).Handle));

    private static Task<ToolDispatchResult> GeomVertical(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_vertical", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Vertical",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<HandleArgDto>(args).Handle));

    private static Task<ToolDispatchResult> GeomParallel(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_parallel", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Parallel",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    private static Task<ToolDispatchResult> GeomPerpendicular(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_perpendicular", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Perpendicular",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    private static Task<ToolDispatchResult> GeomCoincident(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_coincident", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Coincident",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    private static Task<ToolDispatchResult> GeomFix(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_fix", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Fix",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<HandleArgDto>(args).Handle));

    private static Task<ToolDispatchResult> GeomTangent(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_tangent", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Tangent",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    private static Task<ToolDispatchResult> GeomConcentric(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_concentric", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Concentric",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    private static Task<ToolDispatchResult> GeomCollinear(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_collinear", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Collinear",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    private static Task<ToolDispatchResult> GeomEqual(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_equal", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Equal",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<TwoHandlesArgDto>(args).B));

    // Symmetric takes THREE entities: the two symmetric objects plus the line of symmetry.
    private static Task<ToolDispatchResult> GeomSymmetric(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.geom_symmetric", args, ct,
            (_, _) => "_.-GEOCONSTRAINT",
            (_, _) => "_Symmetric",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<SymmetricArgsDto>(args).A),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<SymmetricArgsDto>(args).B),
            (db, tr) => ResolvePointOnEntity(db, tr, Read<SymmetricArgsDto>(args).SymmetryLine));

    // ─────────── dimensional constraints (-DIMCONSTRAINT) ───────────
    //
    // Point-pair form only (subcommand + first extension origin + second extension
    // origin + dimension line location) -- deliberately NOT using the "Object" pick
    // mode, whose prompt sequence differs enough across AutoCAD versions that it
    // would need live verification per version to trust. The point-pair form is the
    // same shape DIMLINEAR/DIMALIGNED have always used, just with a value that
    // drives the geometry instead of a static annotation.

    private static Task<ToolDispatchResult> DimLinear(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.dim_linear", args, ct,
            (_, _) => "_.-DIMCONSTRAINT",
            (_, _) => "_Linear",
            (_, _) => AcadEnv.ToPoint3d(Read<DimConstraintArgsDto>(args).Point1),
            (_, _) => AcadEnv.ToPoint3d(Read<DimConstraintArgsDto>(args).Point2),
            (_, _) => AcadEnv.ToPoint3d(Read<DimConstraintArgsDto>(args).PlacementPoint));

    private static Task<ToolDispatchResult> DimAligned(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.dim_aligned", args, ct,
            (_, _) => "_.-DIMCONSTRAINT",
            (_, _) => "_Aligned",
            (_, _) => AcadEnv.ToPoint3d(Read<DimConstraintArgsDto>(args).Point1),
            (_, _) => AcadEnv.ToPoint3d(Read<DimConstraintArgsDto>(args).Point2),
            (_, _) => AcadEnv.ToPoint3d(Read<DimConstraintArgsDto>(args).PlacementPoint));

    private static Task<ToolDispatchResult> DeleteEntityConstraints(JsonObject args, CancellationToken ct) =>
        RunEditorCommand("acad.parametric.delete_entity_constraints", args, ct,
            (_, _) => "_.-DELCONSTRAINT",
            (db, tr) => ResolvePointOnEntity(db, tr, Read<HandleArgDto>(args).Handle));

    // ─────────── inventory ───────────

    private static Task<ToolDispatchResult> ListConstraintEntities(JsonObject args, CancellationToken ct) =>
        RunInTransaction("acad.parametric.list_constraint_entities", args, ct, (doc, db, tr) =>
        {
            var a = Read<ListConstraintsArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var found = new List<ConstraintEntityInfoDto>();
            foreach (ObjectId id in ms)
            {
                var o = tr.GetObject(id, OpenMode.ForRead);
                var cls = o.GetRXClass().Name;
                if (!cls.Contains("Constraint", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (o is not Entity ent) continue;
                if (!string.IsNullOrEmpty(a.LayerFilter) &&
                    !string.Equals(ent.Layer, a.LayerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                found.Add(new ConstraintEntityInfoDto(
                    ent.Handle.ToString(),
                    cls,
                    ent.Layer));
            }
            return Wrap(new { entities = found, count = found.Count });
        });

    // ─────────── dynamic blocks ───────────

    private static Task<ToolDispatchResult> GetDynamicBlockProperties(JsonObject args, CancellationToken ct) =>
        RunInTransaction("acad.parametric.get_dynamic_block_properties", args, ct, (_, db, tr) =>
        {
            var a = Read<HandleArgDto>(args);
            var id = AcadEnv.ResolveHandle(db, a.Handle);
            if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference br)
                throw new ArgumentException($"handle {a.Handle} is not a BlockReference");
            var list = new List<DynamicPropDto>();
            if (!br.IsDynamicBlock)
                return Wrap(new { isDynamicBlock = false, properties = list });

            foreach (DynamicBlockReferenceProperty p in br.DynamicBlockReferencePropertyCollection)
            {
                var cur = p.Value;
                list.Add(new DynamicPropDto(
                    p.PropertyName,
                    ValueToJsonNode(cur),
                    p.ReadOnly,
                    p.UnitsType.ToString(),
                    cur?.GetType().Name ?? "null"));
            }
            return Wrap(new { isDynamicBlock = true, effectiveBlockName = br.Name, properties = list });
        });

    private static Task<ToolDispatchResult> SetDynamicBlockProperty(JsonObject args, CancellationToken ct) =>
        RunInTransaction("acad.parametric.set_dynamic_block_property", args, ct, (_, db, tr) =>
        {
            var a = Read<SetDynamicBlockPropArgsDto>(args);
            var id = AcadEnv.ResolveHandle(db, a.Handle);
            if (tr.GetObject(id, OpenMode.ForWrite) is not BlockReference br)
                throw new ArgumentException($"handle {a.Handle} is not a BlockReference");
            if (!br.IsDynamicBlock)
                throw new InvalidOperationException("BlockReference is not a dynamic block.");

            DynamicBlockReferenceProperty? hit = null;
            foreach (DynamicBlockReferenceProperty p in br.DynamicBlockReferencePropertyCollection)
            {
                if (string.Equals(p.PropertyName, a.PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    hit = p;
                    break;
                }
            }
            if (hit is null)
                throw new ArgumentException($"No dynamic property named '{a.PropertyName}'.");

            if (hit.ReadOnly)
                throw new InvalidOperationException($"Property '{a.PropertyName}' is read-only.");

            hit.Value = CoerceJsonToPropertyValue(hit, a.Value);
            db.TransactionManager.QueueForGraphicsFlush();
            return Wrap(new { ok = true, propertyName = hit.PropertyName, value = ValueToJsonNode(hit.Value) });
        });

    private static JsonNode? ValueToJsonNode(object? v) => v switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create((double)f),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(v.ToString()),
    };

    private static object CoerceJsonToPropertyValue(DynamicBlockReferenceProperty prop, JsonElement el)
    {
        var cur = prop.Value;
        if (cur is bool)
            return el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.Parse(el.GetString()!),
                JsonValueKind.Number => el.GetInt32() != 0,
                _ => el.GetBoolean(),
            };

        if (cur is int)
            return el.ValueKind == JsonValueKind.String
                ? int.Parse(el.GetString()!, CultureInfo.InvariantCulture)
                : el.GetInt32();

        if (cur is double or float)
        {
            double d = el.ValueKind == JsonValueKind.String
                ? double.Parse(el.GetString()!, CultureInfo.InvariantCulture)
                : el.GetDouble();
            if (UnitsLookAngular(prop.UnitsType))
                return d * (Math.PI / 180.0);
            return d;
        }

        if (cur is string)
            return el.ValueKind == JsonValueKind.String ? el.GetString()! : el.ToString();

        // Current value null / unknown — infer from JSON + units hint.
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return el.GetBoolean();
        if (el.ValueKind == JsonValueKind.String)
            return el.GetString()!;
        if (el.ValueKind == JsonValueKind.Number)
        {
            double d = el.GetDouble();
            if (UnitsLookAngular(prop.UnitsType))
                return d * (Math.PI / 180.0);
            return d;
        }

        throw new ArgumentException($"Cannot coerce JSON {el.ValueKind} to dynamic property '{prop.PropertyName}'.");
    }

    /// <summary>AutoCAD exposes different enum spellings across years; treat
    /// anything containing "angl" (Angular, Angle, …) as radians internally.</summary>
    private static bool UnitsLookAngular(DynamicBlockReferencePropertyUnitsType u) =>
        u.ToString().Contains("angl", StringComparison.OrdinalIgnoreCase);
}

internal sealed record SymmetricArgsDto(
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B,
    [property: JsonPropertyName("symmetryLine")] string SymmetryLine);

internal sealed record DimConstraintArgsDto(
    [property: JsonPropertyName("point1")] Point2dDto Point1,
    [property: JsonPropertyName("point2")] Point2dDto Point2,
    [property: JsonPropertyName("placementPoint")] Point2dDto PlacementPoint);

internal sealed record ListConstraintsArgsDto(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter);

internal sealed record ConstraintEntityInfoDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("className")] string ClassName,
    [property: JsonPropertyName("layer")] string Layer);

internal sealed record DynamicPropDto(
    [property: JsonPropertyName("propertyName")] string PropertyName,
    [property: JsonPropertyName("value")] JsonNode? Value,
    [property: JsonPropertyName("readOnly")] bool ReadOnly,
    [property: JsonPropertyName("unitsType")] string UnitsType,
    [property: JsonPropertyName("clrType")] string ClrType);

internal sealed record SetDynamicBlockPropArgsDto(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("propertyName")] string PropertyName,
    [property: JsonPropertyName("value")] JsonElement Value);
