// AutoCAD plugin handlers for the acad-fields category.
//
// A field is not an entity type. It is an MText whose Contents carry an %<\AcVar ...>%
// expression, plus a Field object AutoCAD attaches when it evaluates. So:
//   - creating a field   = create MText with the expression, then Database.EvaluateFields()
//   - detecting a field  = look for "%<\Ac" in the raw Contents
//   - the visible value  = MText.Text (evaluated) vs MText.Contents (raw expression)
// That last distinction is the one to hold on to: Contents is what you wrote, Text is what the
// sheet shows. Reporting Contents as the value would show the caller a code, not a number.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class FieldsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.fields.insert_field_date", InsertDate);
        host.Register("acad.fields.insert_field_filename", InsertFilename);
        host.Register("acad.fields.insert_field_layout_name", InsertLayoutName);
        host.Register("acad.fields.insert_field_object_property", InsertObjectProperty);
        host.Register("acad.fields.insert_field_system_variable", InsertSystemVariable);
        host.Register("acad.fields.insert_field_expression", InsertExpression);
        host.Register("acad.fields.list_fields", ListFields);
        host.Register("acad.fields.update_fields", UpdateFields);
        host.Register("acad.fields.convert_field_to_text", ConvertToText);
        host.Register("acad.fields.get_field_expression", GetExpression);
        host.Register("acad.fields.set_field_evaluation_mode", SetEvalMode);
        host.Register("acad.fields.get_field_evaluation_mode", GetEvalMode);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();
    private static Task<ToolDispatchResult> Run(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);

    private const string FieldMarker = "%<\\Ac";

    private static bool LooksLikeField(string? contents) =>
        !string.IsNullOrEmpty(contents) && contents!.Contains(FieldMarker, StringComparison.Ordinal);

    /// <summary>Place the MText, evaluate it, and report both the expression and the result.</summary>
    private static JsonObject PlaceField(Database db, Transaction tr, Point3d pos, string expression,
                                         double? height, string? layer, string? textStyle,
                                         string? prefix, string? suffix, string kind)
    {
        var contents = (prefix ?? "") + expression + (suffix ?? "");
        var mt = new MText
        {
            Location = pos,
            Contents = contents,
            TextHeight = height is > 0 ? height!.Value : 2.5,
        };
        if (!string.IsNullOrWhiteSpace(textStyle))
            mt.TextStyleId = AcadEnv.ResolveTextStyleOrStandard(db, tr, textStyle);

        var handle = AcadEnv.Persist(db, tr, mt, layer);

        // Evaluate now so the caller sees the resolved value rather than the code. A field that
        // resolves to nothing is almost always a wrong expression, and it should be visible here
        // rather than at plot time.
        try { db.EvaluateFields(); } catch { }

        return Wrap(new
        {
            field = new
            {
                handle = handle.Handle,
                layer = mt.Layer,
                expression = mt.Contents,
                evaluated = SafeText(mt),
                kind,
            }
        });
    }

    private static string SafeText(MText mt)
    {
        try { return mt.Text ?? ""; } catch { return ""; }
    }

    // ─────────── insertion ───────────

    private static Task<ToolDispatchResult> InsertDate(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_date", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldDateArgsDto>(args);
            var fmt = string.IsNullOrWhiteSpace(a.Format) ? "yyyy-MM-dd" : a.Format;
            var expr = $"%<\\AcVar Date \\f \"{fmt}\">%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "date");
        });

    private static Task<ToolDispatchResult> InsertFilename(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_filename", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldFilenameArgsDto>(args);
            // The two flags of AcVar Filename: 1 = include path, 2 = include extension.
            int flags = (a.IncludePath ? 1 : 0) + (a.IncludeExtension ? 2 : 0);
            var expr = $"%<\\AcVar Filename \\f \"%tc1\" \\f {flags}>%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "filename");
        });

    private static Task<ToolDispatchResult> InsertLayoutName(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_layout_name", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldPlacementArgsDto>(args);
            // CTAB is the current tab name; it is what a sheet-number cell should read.
            var expr = "%<\\AcVar CTab>%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "layoutName");
        });

    private static Task<ToolDispatchResult> InsertObjectProperty(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_object_property", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldObjectPropertyArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Property)) throw new ArgumentException("property is required.");

            // Resolve first so a bad handle fails here with a clear message rather than
            // producing a field that silently evaluates to nothing.
            var id = AcadEnv.ResolveHandle(db, a.Handle);
            var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
            var objIdHex = ent.ObjectId.Handle.ToString();

            var fmt = string.IsNullOrWhiteSpace(a.Format) ? "" : $" \\f \"{a.Format}\"";
            var expr = $"%<\\AcObjProp Object(%<\\_ObjId {objIdHex}>%).{a.Property}{fmt}>%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "objectProperty");
        });

    private static Task<ToolDispatchResult> InsertSystemVariable(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_system_variable", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldSysVarArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Variable)) throw new ArgumentException("variable is required.");
            var expr = $"%<\\AcVar {a.Variable.Trim()}>%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "systemVariable");
        });

    private static Task<ToolDispatchResult> InsertExpression(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_expression", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldRawArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Expression)) throw new ArgumentException("expression is required.");
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), a.Expression, a.Height, a.Layer,
                              a.TextStyle, null, null, "raw");
        });

    // ─────────── maintenance ───────────

    private static IEnumerable<(ObjectId Id, MText Mt)> AllFieldTexts(Database db, Transaction tr, OpenMode mode)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not MText probe) continue;
            if (!LooksLikeField(probe.Contents)) continue;
            yield return (id, mode == OpenMode.ForRead ? probe : (MText)tr.GetObject(id, mode));
        }
    }

    private static Task<ToolDispatchResult> ListFields(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.list_fields", args, ct, (doc, db, tr) =>
        {
            var list = new List<object>();
            foreach (var (id, mt) in AllFieldTexts(db, tr, OpenMode.ForRead))
            {
                list.Add(new
                {
                    handle = mt.Handle.ToString(),
                    layer = mt.Layer,
                    expression = mt.Contents,
                    evaluated = SafeText(mt),
                    kind = "unknown",
                });
            }
            return Wrap(new { fields = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> UpdateFields(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.update_fields", args, ct, (doc, db, tr) =>
        {
            var a = Read<UpdateFieldsArgsDto>(args);
            int n = 0;
            if (a.Handles is { Count: > 0 })
            {
                foreach (var h in a.Handles)
                {
                    var obj = tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                    if (obj is MText mt && LooksLikeField(mt.Contents)) n++;
                }
            }
            else
            {
                foreach (var _ in AllFieldTexts(db, tr, OpenMode.ForRead)) n++;
            }
            try { db.EvaluateFields(); } catch (Exception ex) { throw new InvalidOperationException("EvaluateFields failed: " + ex.Message, ex); }
            return Wrap(new { affected = n });
        });

    private static Task<ToolDispatchResult> ConvertToText(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.convert_field_to_text", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldHandleArgsDto>(args);
            var obj = tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (obj is not MText mt)
                throw new ArgumentException($"Handle {a.Handle} is a {obj.GetRXClass().Name}, not an MText.");
            if (!LooksLikeField(mt.Contents))
                throw new ArgumentException(
                    $"MText {a.Handle} carries no field, so there is nothing to freeze. " +
                    "Use list_fields to find the ones that do.");

            try { db.EvaluateFields(); } catch { }
            var frozen = SafeText(mt);
            mt.Contents = frozen;   // one-way, deliberately
            return Wrap(new { affected = 1, handle = a.Handle });
        });

    private static Task<ToolDispatchResult> GetExpression(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.get_field_expression", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldHandleArgsDto>(args);
            var obj = tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            if (obj is not MText mt)
                throw new ArgumentException($"Handle {a.Handle} is a {obj.GetRXClass().Name}, not an MText.");
            return Wrap(new
            {
                field = new
                {
                    handle = a.Handle,
                    layer = mt.Layer,
                    expression = mt.Contents,
                    evaluated = SafeText(mt),
                    kind = LooksLikeField(mt.Contents) ? "field" : "plainText",
                }
            });
        });

    // ─────────── FIELDEVAL ───────────
    // Bitmask: 1 open, 2 save, 4 plot, 8 eTransmit, 16 regen.

    private static JsonObject EvalModeResult(int v) => Wrap(new
    {
        onOpen = (v & 1) != 0,
        onSave = (v & 2) != 0,
        onPlot = (v & 4) != 0,
        onRegen = (v & 16) != 0,
        fieldEval = v,
    });

    private static Task<ToolDispatchResult> SetEvalMode(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.set_field_evaluation_mode", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldEvalModeArgsDto>(args);
            int v = (a.OnOpen ? 1 : 0) | (a.OnSave ? 2 : 0) | (a.OnPlot ? 4 : 0) | (a.OnRegen ? 16 : 0);
            // Int16, like every other AutoCAD sysvar - passing an int throws eInvalidInput, and
            // that lesson has already been paid for once in export_file.
            Application.SetSystemVariable("FIELDEVAL", (short)v);
            return EvalModeResult(Convert.ToInt32(Application.GetSystemVariable("FIELDEVAL")));
        });

    private static Task<ToolDispatchResult> GetEvalMode(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.get_field_evaluation_mode", args, ct, (doc, db, tr) =>
            EvalModeResult(Convert.ToInt32(Application.GetSystemVariable("FIELDEVAL"))));
}
