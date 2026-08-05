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
        host.Register("acad.styles.list_mleaderstyle_properties", ListMLeaderProperties);
        host.Register("acad.styles.list_mleaderstyles", ListMLeaderStyles);
        host.Register("acad.styles.create_mleaderstyle", CreateMLeaderStyle);
        host.Register("acad.styles.modify_mleaderstyle", ModifyMLeaderStyle);
        host.Register("acad.styles.delete_mleaderstyle", DeleteMLeaderStyle);
        host.Register("acad.styles.set_current_mleaderstyle", SetCurrentMLeaderStyle);
        host.Register("acad.styles.list_tablestyle_properties", ListTableProperties);
        host.Register("acad.styles.list_tablestyles", ListTableStyles);
        host.Register("acad.styles.create_tablestyle", CreateTableStyle);
        host.Register("acad.styles.modify_tablestyle", ModifyTableStyle);
        host.Register("acad.styles.delete_tablestyle", DeleteTableStyle);
        host.Register("acad.styles.set_current_tablestyle", SetCurrentTableStyle);

        // Multiline styles live in their own file: they are an ordered list of parallel line
        // elements rather than a bag of scalar properties, so none of the catalogue machinery
        // above applies to them.
        StylesMlinePluginTools.Register(host);

        // Layer filters live in a tree on the Database rather than in a symbol table or a
        // dictionary, and the whole tree has to be assigned back after any change. Separate file
        // so that write-back discipline stays visible instead of buried among style records.
        StylesLayerFilterPluginTools.Register(host);

        // Table cell styles, visual styles and point display: three small groups whose shapes
        // have nothing in common with the style records above.
        StylesMiscPluginTools.Register(host);
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

    // ─────────── multileader styles (roadmap 2.3) ───────────
    //
    // MLeaderStyle objects live in a dictionary rather than a symbol table, so the shape differs
    // from dimension styles even though the tools deliberately do not: a caller who has learned
    // one properties map has learned both.

    private static DBDictionary MLeaderDict(Database db, Transaction tr, OpenMode mode)
        => (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, mode);

    private static List<string> MLeaderNames(Database db, Transaction tr)
    {
        var names = new List<string>();
        foreach (DBDictionaryEntry e in MLeaderDict(db, tr, OpenMode.ForRead)) names.Add(e.Key);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static MLeaderStyle OpenMLeader(Database db, Transaction tr, string name, OpenMode mode, out string key)
    {
        var dict = MLeaderDict(db, tr, OpenMode.ForRead);
        foreach (DBDictionaryEntry e in dict)
        {
            if (!string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase)) continue;
            key = e.Key;
            return (MLeaderStyle)tr.GetObject(e.Value, mode);
        }
        throw new ArgumentException(
            $"No multileader style named '{name}'. Defined: " + string.Join(", ", MLeaderNames(db, tr)) +
            ". Use list_mleaderstyles.");
    }

    private static double GetMl(MLeaderStyle m, string api) => api switch
    {
        "Scale"                   => m.Scale,
        "TextHeight"              => m.TextHeight,
        "ArrowSize"               => m.ArrowSize,
        "LandingGap"              => m.LandingGap,
        "DoglegLength"            => m.DoglegLength,
        "BreakSize"               => m.BreakSize,
        "MaxLeaderSegmentsPoints" => m.MaxLeaderSegmentsPoints,
        "EnableLanding"           => m.EnableLanding ? 1 : 0,
        "EnableDogleg"            => m.EnableDogleg ? 1 : 0,
        "EnableFrameText"         => m.EnableFrameText ? 1 : 0,
        _ => throw new InvalidOperationException($"No reader wired for {api}."),
    };

    private static void SetMl(MLeaderStyle m, string api, double v)
    {
        switch (api)
        {
            case "Scale":                   m.Scale = v; break;
            case "TextHeight":              m.TextHeight = v; break;
            case "ArrowSize":               m.ArrowSize = v; break;
            case "LandingGap":              m.LandingGap = v; break;
            case "DoglegLength":            m.DoglegLength = v; break;
            case "BreakSize":               m.BreakSize = v; break;
            case "MaxLeaderSegmentsPoints": m.MaxLeaderSegmentsPoints = (int)v; break;
            // Booleans arrive as 0/1 so the whole argument stays one map of names to numbers.
            case "EnableLanding":           m.EnableLanding = v != 0; break;
            case "EnableDogleg":            m.EnableDogleg = v != 0; break;
            case "EnableFrameText":         m.EnableFrameText = v != 0; break;
            default: throw new InvalidOperationException($"No writer wired for {api}.");
        }
    }

    private static Dictionary<string, double> ReadMlProps(MLeaderStyle m)
    {
        var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in MLeaderStyleProperties.All) d[p.Name] = GetMl(m, p.ApiName);
        return d;
    }

    private static List<string> ApplyMlProps(MLeaderStyle m, IReadOnlyDictionary<string, double>? props)
    {
        var applied = new List<string>();
        if (props is null) return applied;
        foreach (var kv in props)
        {
            var p = MLeaderStyleProperties.Resolve(kv.Key, kv.Value);
            SetMl(m, p.ApiName, kv.Value);
            applied.Add(p.Name);
        }
        applied.Sort(StringComparer.OrdinalIgnoreCase);
        return applied;
    }

    private static object MlInfo(Database db, string name, MLeaderStyle m) => new
    {
        name,
        isCurrent = db.MLeaderstyle == m.ObjectId,
        properties = ReadMlProps(m),
    };

    private static Task<ToolDispatchResult> ListMLeaderProperties(JsonObject args, CancellationToken ct) =>
        RunR("acad.styles.list_mleaderstyle_properties", args, ct, (doc, db, tr) =>
        {
            var list = MLeaderStyleProperties.All.Select(p => (object)new
            {
                name = p.Name,
                apiName = p.ApiName,
                kind = p.Kind.ToString(),
                description = p.Description,
                min = p.Min,
                max = p.Max,
            }).ToList();
            return Wrap(new { properties = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> ListMLeaderStyles(JsonObject args, CancellationToken ct) =>
        RunR("acad.styles.list_mleaderstyles", args, ct, (doc, db, tr) =>
        {
            var dict = MLeaderDict(db, tr, OpenMode.ForRead);
            var list = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                var m = (MLeaderStyle)tr.GetObject(e.Value, OpenMode.ForRead);
                list.Add(MlInfo(db, e.Key, m));
            }
            return Wrap(new { mleaderStyles = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> CreateMLeaderStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.create_mleaderstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateMLeaderStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            AcadEnv.ValidateSymbolName(a.Name, "MLeaderStyle");

            var dict = MLeaderDict(db, tr, OpenMode.ForWrite);
            foreach (DBDictionaryEntry e in dict)
            {
                if (!string.Equals(e.Key, a.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!a.Overwrite)
                    throw new ArgumentException(
                        $"A multileader style named '{e.Key}' already exists. Pass overwrite:true, or use " +
                        "modify_mleaderstyle to change only some properties.");
                var existing = (MLeaderStyle)tr.GetObject(e.Value, OpenMode.ForWrite);
                var applied0 = ApplyMlProps(existing, a.Properties);
                return Wrap(new { mleaderStyle = MlInfo(db, e.Key, existing), created = false, applied = applied0 });
            }

            var style = new MLeaderStyle();
            var applied = ApplyMlProps(style, a.Properties);
            // PostMLeaderStyleToDb both names the style and adds it to the dictionary; doing it
            // by hand leaves a style the dictionary knows about and MLEADERSTYLE does not.
            style.PostMLeaderStyleToDb(db, a.Name);
            tr.AddNewlyCreatedDBObject(style, true);

            if (a.MakeCurrent) db.MLeaderstyle = style.ObjectId;
            return Wrap(new { mleaderStyle = MlInfo(db, a.Name, style), created = true, applied });
        });

    private static Task<ToolDispatchResult> ModifyMLeaderStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.modify_mleaderstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<ModifyMLeaderStyleArgsDto>(args);
            if (a.Properties is null || a.Properties.Count == 0)
                throw new ArgumentException(
                    "properties: at least one is required. Use list_mleaderstyle_properties for the names.");

            var m = OpenMLeader(db, tr, a.Name, OpenMode.ForWrite, out var key);
            var applied = ApplyMlProps(m, a.Properties);
            return Wrap(new
            {
                mleaderStyle = MlInfo(db, key, m),
                applied,
                note = "Existing multileaders pick this up on the next regen; the stored style is already changed.",
            });
        });

    private static Task<ToolDispatchResult> DeleteMLeaderStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.delete_mleaderstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<MLeaderStyleNameArgsDto>(args);
            var m = OpenMLeader(db, tr, a.Name, OpenMode.ForWrite, out var key);

            if (string.Equals(key, "Standard", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("'Standard' is AutoCAD's built-in multileader style and cannot be deleted.");
            if (db.MLeaderstyle == m.ObjectId)
                throw new ArgumentException(
                    $"'{key}' is the current multileader style. Make another one current first with " +
                    "set_current_mleaderstyle.");

            try { m.Erase(); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot delete '{key}': {ex.Message}. Multileaders still using it must be moved to " +
                    "another style first.");
            }
            return Wrap(new { affected = 1, name = key });
        });

    private static Task<ToolDispatchResult> SetCurrentMLeaderStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.set_current_mleaderstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<MLeaderStyleNameArgsDto>(args);
            var m = OpenMLeader(db, tr, a.Name, OpenMode.ForRead, out var key);
            db.MLeaderstyle = m.ObjectId;
            return Wrap(new { mleaderStyle = MlInfo(db, key, m) });
        });

    // ─────────── table styles (roadmap 2.3) ───────────
    //
    // Third table in the same family. What differs: several properties are per ROW TYPE rather
    // than per style - a table has a title row, a header row and data rows, each with its own
    // text height - so the wire names carry the row (titleTextHeight, dataTextHeight) and the
    // catalogue carries which RowType each addresses.

    private static DBDictionary TableDict(Database db, Transaction tr, OpenMode mode)
        => (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, mode);

    private static List<string> TableStyleNames(Database db, Transaction tr)
    {
        var names = new List<string>();
        foreach (DBDictionaryEntry e in TableDict(db, tr, OpenMode.ForRead)) names.Add(e.Key);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static TableStyle OpenTableStyle(Database db, Transaction tr, string name, OpenMode mode, out string key)
    {
        foreach (DBDictionaryEntry e in TableDict(db, tr, OpenMode.ForRead))
        {
            if (!string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase)) continue;
            key = e.Key;
            return (TableStyle)tr.GetObject(e.Value, mode);
        }
        throw new ArgumentException(
            $"No table style named '{name}'. Defined: " + string.Join(", ", TableStyleNames(db, tr)) +
            ". Use list_tablestyles.");
    }

    private static RowType RowOf(string? name) => name switch
    {
        "TitleRow"  => RowType.TitleRow,
        "HeaderRow" => RowType.HeaderRow,
        "DataRow"   => RowType.DataRow,
        _ => throw new InvalidOperationException($"No row type wired for '{name}'."),
    };

    private static double GetTs(TableStyle t, TableStyleProperty p) => p.ApiName switch
    {
        "HorizontalCellMargin" => t.HorizontalCellMargin,
        "VerticalCellMargin"   => t.VerticalCellMargin,
        "TextHeight"           => t.TextHeight(RowOf(p.RowType)),
        _ => throw new InvalidOperationException($"No reader wired for {p.ApiName}."),
    };

    private static void SetTs(TableStyle t, TableStyleProperty p, double v)
    {
        switch (p.ApiName)
        {
            case "HorizontalCellMargin": t.HorizontalCellMargin = v; break;
            case "VerticalCellMargin":   t.VerticalCellMargin = v; break;
            case "TextHeight":           t.SetTextHeight(v, (int)RowOf(p.RowType)); break;
            default: throw new InvalidOperationException($"No writer wired for {p.ApiName}.");
        }
    }

    private static Dictionary<string, double> ReadTsProps(TableStyle t)
    {
        var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in TableStyleProperties.All) d[p.Name] = GetTs(t, p);
        return d;
    }

    private static List<string> ApplyTsProps(TableStyle t, IReadOnlyDictionary<string, double>? props)
    {
        var applied = new List<string>();
        if (props is null) return applied;
        foreach (var kv in props)
        {
            var p = TableStyleProperties.Resolve(kv.Key, kv.Value);
            SetTs(t, p, kv.Value);
            applied.Add(p.Name);
        }
        applied.Sort(StringComparer.OrdinalIgnoreCase);
        return applied;
    }

    private static object TsInfo(Database db, string name, TableStyle t) => new
    {
        name,
        isCurrent = db.Tablestyle == t.ObjectId,
        properties = ReadTsProps(t),
    };

    private static Task<ToolDispatchResult> ListTableProperties(JsonObject args, CancellationToken ct) =>
        RunR("acad.styles.list_tablestyle_properties", args, ct, (doc, db, tr) =>
        {
            var list = TableStyleProperties.All.Select(p => (object)new
            {
                name = p.Name,
                apiName = p.ApiName,
                rowType = p.RowType,
                kind = p.Kind.ToString(),
                description = p.Description,
                min = p.Min,
                max = p.Max,
            }).ToList();
            return Wrap(new { properties = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> ListTableStyles(JsonObject args, CancellationToken ct) =>
        RunR("acad.styles.list_tablestyles", args, ct, (doc, db, tr) =>
        {
            var list = new List<object>();
            foreach (DBDictionaryEntry e in TableDict(db, tr, OpenMode.ForRead))
            {
                var t = (TableStyle)tr.GetObject(e.Value, OpenMode.ForRead);
                list.Add(TsInfo(db, e.Key, t));
            }
            return Wrap(new { tableStyles = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> CreateTableStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.create_tablestyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateTableStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            AcadEnv.ValidateSymbolName(a.Name, "TableStyle");

            var dict = TableDict(db, tr, OpenMode.ForWrite);
            foreach (DBDictionaryEntry e in dict)
            {
                if (!string.Equals(e.Key, a.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!a.Overwrite)
                    throw new ArgumentException(
                        $"A table style named '{e.Key}' already exists. Pass overwrite:true, or use " +
                        "modify_tablestyle to change only some properties.");
                var existing = (TableStyle)tr.GetObject(e.Value, OpenMode.ForWrite);
                var applied0 = ApplyTsProps(existing, a.Properties);
                return Wrap(new { tableStyle = TsInfo(db, e.Key, existing), created = false, applied = applied0 });
            }

            var style = new TableStyle();

            // Registered before the properties are applied. That order is NOT load-bearing - an
            // earlier version of this comment claimed it fixed an eInvalidInput and it did not:
            // the failure was one property, FlowDirection, which throws on write whether the
            // style is database-resident or not. It is withheld from the catalogue. The order
            // stays because it is the safer of the two, not because it fixed anything.
            //
            // TableStyle has no PostTableStyleToDb; that convenience exists on MLeaderStyle
            // and not here, so the dictionary entry is made by hand. SetAt then
            // AddNewlyCreatedDBObject, in that order: the object must be owned before the
            // transaction is told about it.
            dict.SetAt(a.Name, style);
            tr.AddNewlyCreatedDBObject(style, true);

            var applied = ApplyTsProps(style, a.Properties);

            if (a.MakeCurrent) db.Tablestyle = style.ObjectId;
            return Wrap(new { tableStyle = TsInfo(db, a.Name, style), created = true, applied });
        });

    private static Task<ToolDispatchResult> ModifyTableStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.modify_tablestyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<ModifyTableStyleArgsDto>(args);
            if (a.Properties is null || a.Properties.Count == 0)
                throw new ArgumentException(
                    "properties: at least one is required. Use list_tablestyle_properties for the names.");

            var t = OpenTableStyle(db, tr, a.Name, OpenMode.ForWrite, out var key);
            var applied = ApplyTsProps(t, a.Properties);
            return Wrap(new
            {
                tableStyle = TsInfo(db, key, t),
                applied,
                note = "Existing tables pick this up on the next regen; the stored style is already changed.",
            });
        });

    private static Task<ToolDispatchResult> DeleteTableStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.delete_tablestyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<TableStyleNameArgsDto>(args);
            var t = OpenTableStyle(db, tr, a.Name, OpenMode.ForWrite, out var key);

            if (string.Equals(key, "Standard", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("'Standard' is AutoCAD's built-in table style and cannot be deleted.");
            if (db.Tablestyle == t.ObjectId)
                throw new ArgumentException(
                    $"'{key}' is the current table style. Make another one current first with " +
                    "set_current_tablestyle.");

            try { t.Erase(); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot delete '{key}': {ex.Message}. Tables still using it must be moved to " +
                    "another style first.");
            }
            return Wrap(new { affected = 1, name = key });
        });

    private static Task<ToolDispatchResult> SetCurrentTableStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.styles.set_current_tablestyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<TableStyleNameArgsDto>(args);
            var t = OpenTableStyle(db, tr, a.Name, OpenMode.ForRead, out var key);
            db.Tablestyle = t.ObjectId;
            return Wrap(new { tableStyle = TsInfo(db, key, t) });
        });
}
