// AutoCAD plugin handlers for the acad-styles category — authoring the styles a drawing is
// held to, starting with dimension styles (roadmap 2.3).
//
// What already existed and stays: acad-dimensions owns PLACING dimensions, plus list_dimstyles,
// set_entity_dimstyle and ensure_architectural_dimstyle (which creates one hard-coded ARCH-ISO).
// This category owns AUTHORING - creating a style with chosen properties, changing one,
// duplicating one, deleting one. The split is placing versus defining, not old versus new.
//
// The set of properties a caller may set lives in AcadMcp.Shared.Catalogs.DimStyleProperties,
// not here, so CI can hold the advertised list and the accepted list to each other. A
// properties dictionary is exactly the shape that produced four "the catalogue advertises what
// the tool refuses" defects in an earlier review.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using AcadMcp.Shared.Catalogs;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcadMcp.Plugin.Tools;

internal static class StylesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.styles.list_dimstyle_properties", ListDimStyleProperties);
        host.Register("acad.styles.create_dimstyle", CreateDimStyle);
        host.Register("acad.styles.modify_dimstyle", ModifyDimStyle);
        host.Register("acad.styles.copy_dimstyle", CopyDimStyle);
        host.Register("acad.styles.delete_dimstyle", DeleteDimStyle);
        host.Register("acad.styles.set_current_dimstyle", SetCurrentDimStyle);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();
    private static Task<ToolDispatchResult> Run(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);
    private static Task<ToolDispatchResult> RunR(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunReadAsync(key, ct, work);

    // ─────────── helpers ───────────

    private static DimStyleTable Table(Database db, Transaction tr, OpenMode mode)
        => (DimStyleTable)tr.GetObject(db.DimStyleTableId, mode);

    private static List<string> StyleNames(Database db, Transaction tr)
    {
        var names = new List<string>();
        foreach (ObjectId id in Table(db, tr, OpenMode.ForRead))
            names.Add(((DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static DimStyleTableRecord OpenStyle(Database db, Transaction tr, string name, OpenMode mode)
    {
        var tbl = Table(db, tr, OpenMode.ForRead);
        if (!tbl.Has(name))
            throw new ArgumentException(
                $"No dimension style named '{name}'. Defined: " + string.Join(", ", StyleNames(db, tr)) +
                ". Use dimensions.list_dimstyles.");
        return (DimStyleTableRecord)tr.GetObject(tbl[name], mode);
    }

    /// <summary>Read every property this bank authors back off a style, by its wire name.</summary>
    private static Dictionary<string, double> ReadProps(DimStyleTableRecord rec)
    {
        var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in DimStyleProperties.All) d[p.Name] = GetProp(rec, p.DimVar);
        return d;
    }

    private static double GetProp(DimStyleTableRecord r, string dimVar) => dimVar switch
    {
        "DIMSCALE" => r.Dimscale,
        "DIMTXT"   => r.Dimtxt,
        "DIMASZ"   => r.Dimasz,
        "DIMGAP"   => r.Dimgap,
        "DIMEXE"   => r.Dimexe,
        "DIMEXO"   => r.Dimexo,
        "DIMDLI"   => r.Dimdli,
        "DIMDEC"   => r.Dimdec,
        "DIMLFAC"  => r.Dimlfac,
        "DIMRND"   => r.Dimrnd,
        "DIMZIN"   => r.Dimzin,
        "DIMTAD"   => r.Dimtad,
        "DIMCLRD"  => r.Dimclrd.ColorIndex,
        "DIMCLRE"  => r.Dimclre.ColorIndex,
        "DIMCLRT"  => r.Dimclrt.ColorIndex,
        _ => throw new InvalidOperationException($"No reader wired for {dimVar}."),
    };

    private static void SetProp(DimStyleTableRecord r, string dimVar, double v)
    {
        switch (dimVar)
        {
            case "DIMSCALE": r.Dimscale = v; break;
            case "DIMTXT":   r.Dimtxt = v; break;
            case "DIMASZ":   r.Dimasz = v; break;
            case "DIMGAP":   r.Dimgap = v; break;
            case "DIMEXE":   r.Dimexe = v; break;
            case "DIMEXO":   r.Dimexo = v; break;
            case "DIMDLI":   r.Dimdli = v; break;
            // These are Int16 on the record. Passing an int throws eInvalidInput - the same
            // 16-bit trap that BACKGROUNDPLOT sprang in export_file, and that a bare catch hid
            // for two debug cycles there.
            case "DIMDEC":   r.Dimdec = (short)v; break;
            case "DIMZIN":   r.Dimzin = (short)v; break;
            case "DIMTAD":   r.Dimtad = (short)v; break;
            case "DIMLFAC":  r.Dimlfac = v; break;
            case "DIMRND":   r.Dimrnd = v; break;
            case "DIMCLRD":  r.Dimclrd = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                 Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)v); break;
            case "DIMCLRE":  r.Dimclre = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                 Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)v); break;
            case "DIMCLRT":  r.Dimclrt = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                 Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)v); break;
            default: throw new InvalidOperationException($"No writer wired for {dimVar}.");
        }
    }

    private static List<string> ApplyProps(DimStyleTableRecord rec, IReadOnlyDictionary<string, double>? props)
    {
        var applied = new List<string>();
        if (props is null) return applied;
        foreach (var kv in props)
        {
            // Resolve validates the name AND the range, and throws on either. An unknown
            // property is never skipped: skipping would report success over a style that was
            // not changed in the way the caller asked for.
            var p = DimStyleProperties.Resolve(kv.Key, kv.Value);
            SetProp(rec, p.DimVar, kv.Value);
            applied.Add(p.Name);
        }
        applied.Sort(StringComparer.OrdinalIgnoreCase);
        return applied;
    }

    private static object Info(Database db, Transaction tr, DimStyleTableRecord rec) => new
    {
        name = rec.Name,
        isCurrent = db.Dimstyle == rec.ObjectId,
        properties = ReadProps(rec),
    };

    // ─────────── handlers ───────────

    private static Task<ToolDispatchResult> ListDimStyleProperties(JsonObject args, CancellationToken ct) =>
        RunR("acad.styles.list_dimstyle_properties", args, ct, (doc, db, tr) =>
        {
            var list = DimStyleProperties.All.Select(p => (object)new
            {
                name = p.Name,
                dimVar = p.DimVar,
                kind = p.Kind.ToString(),
                description = p.Description,
                min = p.Min,
                max = p.Max,
            }).ToList();
            return Wrap(new { properties = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> CreateDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.create_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateDimStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            AcadEnv.ValidateSymbolName(a.Name, "DimStyle");

            var tbl = Table(db, tr, OpenMode.ForWrite);
            if (tbl.Has(a.Name))
            {
                if (!a.Overwrite)
                    throw new ArgumentException(
                        $"A dimension style named '{a.Name}' already exists. Pass overwrite:true to change it, " +
                        "or use modify_dimstyle to change only some properties.");
                var existing = (DimStyleTableRecord)tr.GetObject(tbl[a.Name], OpenMode.ForWrite);
                var changed0 = ApplyProps(existing, a.Properties);
                return Wrap(new { dimStyle = Info(db, tr, existing), created = false, applied = changed0 });
            }

            var rec = new DimStyleTableRecord { Name = a.Name };
            var applied = ApplyProps(rec, a.Properties);
            tbl.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);

            if (a.MakeCurrent) db.Dimstyle = rec.ObjectId;
            return Wrap(new { dimStyle = Info(db, tr, rec), created = true, applied });
        });

    private static Task<ToolDispatchResult> ModifyDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.modify_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<ModifyDimStyleArgsDto>(args);
            if (a.Properties is null || a.Properties.Count == 0)
                throw new ArgumentException(
                    "properties: at least one is required. Use list_dimstyle_properties for the names.");

            var rec = OpenStyle(db, tr, a.Name, OpenMode.ForWrite);
            var applied = ApplyProps(rec, a.Properties);

            // Dimensions already placed do NOT redraw from a changed style until the drawing
            // regenerates; the DB values are correct immediately. Said in the result so nobody
            // reads an unchanged screen as a failed call.
            return Wrap(new
            {
                dimStyle = Info(db, tr, rec),
                applied,
                note = "Existing dimensions pick this up on the next regen; the stored style is already changed.",
            });
        });

    private static Task<ToolDispatchResult> CopyDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.copy_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<CopyDimStyleArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.NewName, "DimStyle");

            var src = OpenStyle(db, tr, a.SourceName, OpenMode.ForRead);
            var tbl = Table(db, tr, OpenMode.ForWrite);
            if (tbl.Has(a.NewName))
                throw new ArgumentException($"A dimension style named '{a.NewName}' already exists.");

            var rec = new DimStyleTableRecord { Name = a.NewName };
            // Copy every property this bank knows about, then apply the caller's overrides. The
            // usual reason to copy a style is "the same but at 1:100", so overrides in the same
            // call save a round trip and keep the two changes atomic.
            foreach (var p in DimStyleProperties.All) SetProp(rec, p.DimVar, GetProp(src, p.DimVar));
            var applied = ApplyProps(rec, a.Properties);

            tbl.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            if (a.MakeCurrent) db.Dimstyle = rec.ObjectId;

            return Wrap(new { dimStyle = Info(db, tr, rec), copiedFrom = src.Name, applied });
        });

    private static Task<ToolDispatchResult> DeleteDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.delete_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<DimStyleNameArgsDto>(args);
            var rec = OpenStyle(db, tr, a.Name, OpenMode.ForWrite);

            if (string.Equals(rec.Name, "Standard", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("'Standard' is AutoCAD's built-in dimension style and cannot be deleted.");
            if (db.Dimstyle == rec.ObjectId)
                throw new ArgumentException(
                    $"'{rec.Name}' is the current dimension style. Make another one current first with " +
                    "set_current_dimstyle.");

            // Erase throws eWasOpenForWrite-ish errors if anything still references the style.
            // Let that surface with the style name attached rather than as a bare AutoCAD code.
            try { rec.Erase(); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot delete '{a.Name}': {ex.Message}. Dimensions still using it must be moved to " +
                    "another style first - dimensions.set_entity_dimstyle does that.");
            }

            return Wrap(new { affected = 1, name = a.Name });
        });

    private static Task<ToolDispatchResult> SetCurrentDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.set_current_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<DimStyleNameArgsDto>(args);
            var rec = OpenStyle(db, tr, a.Name, OpenMode.ForRead);
            db.Dimstyle = rec.ObjectId;
            return Wrap(new { dimStyle = Info(db, tr, rec) });
        });
}
