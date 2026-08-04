// AutoCAD plugin handlers for the acad-fields category.
//
// A field is not an entity type. It is an MText whose Contents carry an %<\AcVar ...>%
// expression, plus a Field object AutoCAD attaches when it evaluates. So:
//   - creating a field   = create MText with the expression, then Database.EvaluateFields()
//   - detecting a field  = ask the extension dictionary for ACAD_FIELD. Scanning Contents for
//                          the marker does NOT work: once a field evaluates, Contents comes
//                          back resolved and the marker is gone.
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
        host.Register("acad.fields.insert_field_area", InsertArea);
        host.Register("acad.fields.insert_field_formula", InsertFormula);
        host.Register("acad.fields.insert_field_plot_info", InsertPlotInfo);
        host.Register("acad.fields.insert_field_block_attribute", InsertBlockAttribute);
        host.Register("acad.fields.set_field_format", SetFormat);
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

    /// <summary>
    /// The RAW field code, e.g. %&lt;\AcVar CreateDate&gt;%, read from the Field object itself.
    ///
    /// MText.Contents is NOT the expression. For a field that has evaluated, Contents comes back
    /// already resolved, so reading it returns the answer and calls it the question - which made
    /// get_field_expression report the value twice and deliver precisely nothing it promised.
    /// The header of this file already said Contents cannot be trusted for DETECTING a field;
    /// the same is true for reading its code, and that half was missed.
    ///
    /// Field.GetFieldCode is the real source. Returns null when the object carries no Field.
    /// </summary>
    private static string? RawFieldCode(Transaction tr, DBObject obj)
    {
        try
        {
            if (obj.ExtensionDictionary.IsNull) return null;
            var ext = (DBDictionary)tr.GetObject(obj.ExtensionDictionary, OpenMode.ForRead);
            if (!ext.Contains("ACAD_FIELD")) return null;

            var dict = (DBDictionary)tr.GetObject(ext.GetAt("ACAD_FIELD"), OpenMode.ForRead);
            if (!dict.Contains("TEXT")) return null;

            var field = (Field)tr.GetObject(dict.GetAt("TEXT"), OpenMode.ForRead);
            return field.GetFieldCode(FieldCodeFlags.AddMarkers);
        }
        catch { return null; }
    }

    /// <summary>
    /// Whether this object actually carries a Field.
    ///
    /// Scanning MText.Contents for the field marker does NOT work: for a field that evaluates,
    /// Contents comes back already resolved and the marker is gone. Measured - list_fields
    /// returned 0 on a drawing whose fields were plainly visible. The Field hangs off the
    /// entity's extension dictionary under ACAD_FIELD, so ask there. The text scan stays as a
    /// fallback for an expression that has not evaluated yet and still shows its code.
    /// </summary>
    private static bool HasField(Transaction tr, DBObject obj)
    {
        try
        {
            if (!obj.ExtensionDictionary.IsNull)
            {
                var ext = (DBDictionary)tr.GetObject(obj.ExtensionDictionary, OpenMode.ForRead);
                if (ext.Contains("ACAD_FIELD")) return true;
            }
        }
        catch { }
        return obj is MText mt && LooksLikeField(mt.Contents);
    }

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
                expression = RawFieldCode(tr, mt) ?? mt.Contents,
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
            // "Date" is NOT a valid AcVar name - it renders #### (recognised, unevaluable).
            // Measured against live AutoCAD: CreateDate and SaveDate both evaluate. Which one a
            // title block wants differs, so the caller picks; CreateDate is the default because
            // it is the one that does not move every time somebody saves.
            var which = (a.Kind ?? "create").Trim().ToLowerInvariant() switch
            {
                "create" => "CreateDate",
                "save"   => "SaveDate",
                "plot"   => "PlotDate",
                _ => throw new ArgumentException(
                        $"kind must be 'create', 'save' or 'plot' (got '{a.Kind}'). AutoCAD has no "
                        + "plain 'now' date field - it would need re-evaluating on every regen."),
            };
            var expr = $"%<\\AcVar {which} \\f \"{fmt}\">%";
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
            // \_ObjId takes the ObjectId POINTER value as decimal - not the handle, and not the
            // handle converted to decimal. Measured: handle 7F (decimal 127) rendered #### in
            // every form tried; the pointer is a number in the billions. This one substitution
            // is the whole reason the first version of this tool never evaluated.
            var objIdDec = ent.ObjectId.OldIdPtr.ToInt64().ToString(
                System.Globalization.CultureInfo.InvariantCulture);

            var fmt = string.IsNullOrWhiteSpace(a.Format) ? "" : $" \\f \"{a.Format}\"";
            var expr = $"%<\\AcObjProp Object(%<\\_ObjId {objIdDec}>%).{a.Property}{fmt}>%";
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
            if (!HasField(tr, probe)) continue;
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
                    expression = RawFieldCode(tr, mt) ?? mt.Contents,
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
                    if (obj is MText mt2 && HasField(tr, mt2)) n++;
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
            if (!HasField(tr, mt))
                throw new ArgumentException(
                    $"MText {a.Handle} carries no field, so there is nothing to freeze. " +
                    "Use list_fields to find the ones that do.");

            // Evaluate first so the value we freeze is current, not whatever was last rendered.
            try { db.EvaluateFields(); } catch { }
            var frozen = SafeText(mt);

            // ORDER MATTERS, and getting it wrong is worse than not freezing at all.
            // Remove the binding FIRST, write the text LAST. The reverse - set Contents, then
            // erase the Field - leaves a dangling reference that the next EvaluateFields
            // resolves by blanking the MText: measured, the text came back as "" instead of the
            // frozen value. That turns a cosmetic bug into data loss.
            bool bindingRemoved = false;
            if (!mt.ExtensionDictionary.IsNull)
            {
                var ext = (DBDictionary)tr.GetObject(mt.ExtensionDictionary, OpenMode.ForWrite);
                if (ext.Contains("ACAD_FIELD"))
                {
                    try
                    {
                        var fieldObj = tr.GetObject(ext.GetAt("ACAD_FIELD"), OpenMode.ForWrite);
                        if (!fieldObj.IsErased) fieldObj.Erase();
                    }
                    catch { /* the entry still goes, below */ }
                    ext.Remove("ACAD_FIELD");
                    bindingRemoved = true;
                }
            }

            mt.Contents = frozen;

            // Verify rather than assume. This tool has already reported success twice while
            // doing something other than what it claimed.
            if (HasField(tr, mt))
                throw new InvalidOperationException(
                    $"MText {a.Handle} still carries a field after the freeze attempt. Nothing committed.");
            if (string.IsNullOrEmpty(mt.Contents) && !string.IsNullOrEmpty(frozen))
                throw new InvalidOperationException(
                    $"MText {a.Handle} lost its text during the freeze (was '{frozen}'). Nothing committed.");

            return Wrap(new { affected = 1, handle = a.Handle, bindingRemoved, frozenText = frozen });
        });

    // ─────────── closing out roadmap 1.4 ───────────
    //
    // Every expression below was settled by one experiment rather than a guess each: candidate
    // syntaxes placed side by side in a single run, then read back through the (now fixed)
    // get_field_expression. What that measured, on a 6000x4000 mm rectangle:
    //
    //   %<\AcObjProp Object(N).Area>%                        -> 24000000.000000   (mm2, raw)
    //   %<\AcExpr (%<\AcObjProp Object(N).Area>%)/1000000>%  -> 24.000000         (m2)
    //   %<\AcExpr 2+3>%                                      -> 5
    //   %<\AcVar PaperSize>%                                 -> ISO A4 (210.00 x 297.00 mm)
    //   ...\f "%lu2%pr2"                                     -> 24000000.00
    //
    // N is the ObjectId POINTER as decimal, not the handle. AutoCAD stores it as Object(N)
    // directly - the %<\_ObjId N>% wrapper you write is normalised away, which is why a regex
    // hunting for _ObjId inside a stored field finds nothing.

    private static string ObjIdOf(Database db, Transaction tr, string handle)
    {
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle), OpenMode.ForRead);
        return ent.ObjectId.OldIdPtr.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FmtClause(string? format) =>
        string.IsNullOrWhiteSpace(format) ? "" : " \\f \"" + format + "\"";

    private static Task<ToolDispatchResult> InsertArea(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_area", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldAreaArgsDto>(args);
            var oid = ObjIdOf(db, tr, a.Handle);

            // AutoCAD reports Area in square DRAWING units. This bank draws in millimetres, so a
            // raw area field on a room reads 24000000 - technically right and useless on a plan.
            // Do the division inside the field so the number stays live when the room changes.
            var (divisor, defFmt, defSuffix) = (a.Units ?? "m2").Trim().ToLowerInvariant() switch
            {
                "mm2" or "mm" => (1.0, "%lu2%pr0", " mm\u00b2"),
                "cm2" or "cm" => (100.0, "%lu2%pr1", " cm\u00b2"),
                "m2" or "m"   => (1000000.0, "%lu2%pr2", " m\u00b2"),
                "ha"          => (10000000000.0, "%lu2%pr4", " ha"),
                _ => throw new ArgumentException($"Unknown units '{a.Units}'. Known: mm2, cm2, m2, ha."),
            };

            var fmt = FmtClause(a.Format ?? defFmt);
            var expr = divisor == 1.0
                ? "%<\\AcObjProp Object(" + oid + ").Area" + fmt + ">%"
                : "%<\\AcExpr (%<\\AcObjProp Object(" + oid + ").Area>%)/"
                  + divisor.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture)
                  + fmt + ">%";

            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix ?? defSuffix, "area");
        });

    private static Task<ToolDispatchResult> InsertFormula(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_formula", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldFormulaArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Expression))
                throw new ArgumentException("expression is required, e.g. '2+3' or '(12.5*3)/2'.");

            // Passed through unvalidated on purpose: the point of a formula field is arbitrary
            // arithmetic, including nested %<\AcObjProp ...>% references to live geometry. The
            // evaluated result comes back with the handle, so a wrong expression shows up now
            // rather than at plot time, where #### is all anyone sees.
            var expr = "%<\\AcExpr " + a.Expression + FmtClause(a.Format) + ">%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "formula");
        });

    private static readonly string[] PlotInfoVars =
        { "PaperSize", "DeviceName", "PlotScale", "PlotOrientation", "PlotDate", "PlotStyleTable", "LoginName" };

    private static Task<ToolDispatchResult> InsertPlotInfo(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_plot_info", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldPlotInfoArgsDto>(args);
            var match = PlotInfoVars.FirstOrDefault(v => string.Equals(v, a.Info, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                throw new ArgumentException($"Unknown plot info '{a.Info}'. Known: {string.Join(", ", PlotInfoVars)}.");

            // Several of these evaluate to ---- until the layout has the setting: PlotDate before
            // the first plot, PlotStyleTable with no table assigned. That is AutoCAD reporting
            // "not set", not a broken field, and the evaluated value is returned so the caller can
            // tell which case they are in rather than guessing.
            var expr = "%<\\AcVar " + match + FmtClause(a.Format) + ">%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "plotInfo");
        });

    private static Task<ToolDispatchResult> InsertBlockAttribute(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.insert_field_block_attribute", args, ct, (doc, db, tr) =>
        {
            var a = Read<FieldBlockAttributeArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Tag)) throw new ArgumentException("tag is required.");

            var obj = tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            if (obj is not BlockReference br)
                throw new ArgumentException($"Handle {a.Handle} is a {obj.GetRXClass().Name}, not a block reference.");

            var tags = new List<string>();
            var attId = ObjectId.Null;
            foreach (ObjectId id in br.AttributeCollection)
            {
                var att = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);
                tags.Add(att.Tag);
                if (string.Equals(att.Tag, a.Tag, StringComparison.OrdinalIgnoreCase)) { attId = id; break; }
            }
            if (attId.IsNull)
                throw new ArgumentException(
                    "Block " + a.Handle + " has no attribute tagged '" + a.Tag + "'. Tags present: " +
                    (tags.Count == 0 ? "(none)" : string.Join(", ", tags)) + ".");

            var oid = attId.OldIdPtr.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var expr = "%<\\AcObjProp Object(" + oid + ").TextString" + FmtClause(a.Format) + ">%";
            return PlaceField(db, tr, AcadEnv.ToPoint3d(a.Position), expr, a.Height, a.Layer,
                              a.TextStyle, a.Prefix, a.Suffix, "blockAttribute");
        });

    private static Task<ToolDispatchResult> SetFormat(JsonObject args, CancellationToken ct) =>
        Run("acad.fields.set_field_format", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetFieldFormatArgsDto>(args);
            var obj = tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            var code = RawFieldCode(tr, obj)
                ?? throw new ArgumentException(
                       "Handle " + a.Handle + " carries no field, so there is no format to set. " +
                       "Use list_fields to find text that is actually a field.");

            // Edit the field CODE, never the entity contents: contents comes back evaluated, so
            // writing it would replace the field with its own answer and quietly turn a live
            // field into frozen text.
            var stripped = System.Text.RegularExpressions.Regex.Replace(code, "\\s*\\\\f\\s*\"[^\"]*\"", "");
            int close = stripped.LastIndexOf(">%", StringComparison.Ordinal);
            if (close < 0)
                throw new InvalidOperationException(
                    "Field code on " + a.Handle + " is not in a form this tool can edit: " + code);
            var updated = stripped.Insert(close, FmtClause(a.Format));

            var ext = (DBDictionary)tr.GetObject(obj.ExtensionDictionary, OpenMode.ForRead);
            var dict = (DBDictionary)tr.GetObject(ext.GetAt("ACAD_FIELD"), OpenMode.ForRead);
            var field = (Field)tr.GetObject(dict.GetAt("TEXT"), OpenMode.ForWrite);
            field.SetFieldCode(updated);

            // Field.Evaluate on its own re-runs the field but does NOT push the new result into
            // the owning MText, so the code changed and the visible text did not - the tool
            // reported a new format over an unchanged number. db.EvaluateFields is what update_
            // fields uses and what actually refreshes the text.
            try { db.EvaluateFields(); }
            catch (Exception ex) { throw new InvalidOperationException("EvaluateFields failed: " + ex.Message, ex); }

            return Wrap(new
            {
                field = new
                {
                    handle = a.Handle,
                    expression = RawFieldCode(tr, obj) ?? updated,
                    evaluated = obj is MText m ? SafeText(m) : null,
                    format = a.Format,
                }
            });
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
                    expression = RawFieldCode(tr, mt) ?? mt.Contents,
                    evaluated = SafeText(mt),
                    kind = HasField(tr, mt) ? "field" : "plainText",
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
