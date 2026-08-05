// AutoCAD plugin handlers for dimension-style OVERRIDES and cross-drawing import — the last of
// roadmap 2.3.
//
// An override is a property set on one dimension ENTITY that differs from the named style it
// carries. The API for it is not an overrule and not a property bag: Dimension.GetDimstyleData()
// hands back a DimStyleTableRecord holding that dimension's EFFECTIVE values, and
// SetDimstyleData() puts a modified one back. So an override is expressed by round-tripping that
// record, and "which properties are overridden" has to be worked out by comparing the effective
// record against the named style's own - AutoCAD does not report the set directly.
//
// That comparison is the whole value of list_dimstyle_overrides. Without it an agent looking at
// a drawing where one dimension renders differently has no way to find out why, short of reading
// every property by hand. It is also the tool most able to lie: reporting "no overrides" for a
// dimension that visibly differs would look exactly like a healthy answer.
//
// The same properties as everywhere else in this category: AcadMcp.Shared.Catalogs.
// DimStyleProperties is the single source of truth, so what create_dimstyle accepts, what
// list_dimstyle_properties advertises and what an override may set cannot drift apart.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared.Catalogs;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcadMcp.Plugin.Tools;

internal static class StylesDimOverridePluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.styles.apply_dimstyle_override", ApplyDimStyleOverride);
        host.Register("acad.styles.list_dimstyle_overrides", ListDimStyleOverrides);
        host.Register("acad.styles.import_dimstyle_from_dwg", ImportDimStyleFromDwg);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Dimension OpenDimension(Database db, Transaction tr, string handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required.");
        if (!long.TryParse(handle, System.Globalization.NumberStyles.HexNumber, null, out var h))
            throw new ArgumentException("handle '" + handle + "' is not a hexadecimal object handle.");

        var id = db.GetObjectId(false, new Handle(h), 0);
        if (id.IsNull || id.IsErased)
            throw new ArgumentException("No object with handle '" + handle + "' in this drawing.");

        var obj = tr.GetObject(id, mode);
        return obj as Dimension
            ?? throw new ArgumentException(
                "Object '" + handle + "' is a " + obj.GetType().Name + ", not a dimension. " +
                "Overrides apply to dimension entities; use modify_dimstyle to change a style.");
    }

    /// <summary>
    /// The properties on which this dimension differs from its named style.
    /// </summary>
    /// <remarks>
    /// Worked out by comparison because AutoCAD does not expose the override set. Both sides are
    /// read through the same catalogue, so a property the bank cannot author is also one it will
    /// never claim is overridden — a comparison over a wider set than create/modify accepts would
    /// report differences nobody could act on.
    /// </remarks>
    private static List<object> DiffAgainstStyle(Database db, Transaction tr, Dimension dim)
    {
        var effective = dim.GetDimstyleData();
        var styleId = dim.DimensionStyle;
        var named = styleId.IsNull ? null : (DimStyleTableRecord)tr.GetObject(styleId, OpenMode.ForRead);

        var diffs = new List<object>();
        foreach (var p in DimStyleProperties.All)
        {
            var mine = StylesPluginTools.GetProp(effective, p.DimVar);
            var theirs = named is null ? mine : StylesPluginTools.GetProp(named, p.DimVar);
            if (Math.Abs(mine - theirs) < 1e-9) continue;
            diffs.Add(new
            {
                name = p.Name,
                dimVar = p.DimVar,
                value = mine,
                styleValue = theirs,
            });
        }
        return diffs;
    }

    private static Task<ToolDispatchResult> ListDimStyleOverrides(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.styles.list_dimstyle_overrides", ct, (doc, db, tr) =>
        {
            var a = Read<DimOverrideQueryArgsDto>(args);
            var dim = OpenDimension(db, tr, a.Handle, OpenMode.ForRead);
            var overrides = DiffAgainstStyle(db, tr, dim);

            return Wrap(new
            {
                handle = a.Handle,
                styleName = dim.DimensionStyleName,
                overrides,
                count = overrides.Count,
                note = overrides.Count == 0
                    ? "This dimension matches its style on every property this bank authors. It may " +
                      "still differ on one outside that set - see list_dimstyle_properties."
                    : null,
            });
        });

    private static Task<ToolDispatchResult> ApplyDimStyleOverride(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.apply_dimstyle_override", ct, (doc, db, tr) =>
        {
            var a = Read<ApplyDimOverrideArgsDto>(args);
            if (a.Properties is null || a.Properties.Count == 0)
                throw new ArgumentException(
                    "properties is required: a map of property name to value. Use " +
                    "list_dimstyle_properties for the names, and list_dimstyle_overrides to see " +
                    "what this dimension already overrides.");

            var dim = OpenDimension(db, tr, a.Handle, OpenMode.ForWrite);

            // Round-trip the EFFECTIVE record rather than building a fresh one. A new
            // DimStyleTableRecord carries AutoCAD's defaults for everything not set, so pushing
            // one back would silently override every property rather than the ones asked for.
            var rec = dim.GetDimstyleData();

            var applied = new List<string>();
            foreach (var kv in a.Properties)
            {
                var p = DimStyleProperties.Resolve(kv.Key, kv.Value);
                StylesPluginTools.SetProp(rec, p.DimVar, kv.Value);
                applied.Add(p.Name);
            }

            dim.SetDimstyleData(rec);
            dim.RecomputeDimensionBlock(true);

            return Wrap(new
            {
                handle = a.Handle,
                styleName = dim.DimensionStyleName,
                applied,
                overrides = DiffAgainstStyle(db, tr, dim),
            });
        });

    private static Task<ToolDispatchResult> ImportDimStyleFromDwg(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.import_dimstyle_from_dwg", ct, (doc, db, tr) =>
        {
            var a = Read<ImportDimStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the drawing to read styles from.");
            if (!File.Exists(a.Path))
                throw new ArgumentException("No such file: " + a.Path);
            if (string.Equals(Path.GetFullPath(a.Path), db.Filename, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Source and destination are the same drawing. Use copy_dimstyle to duplicate a " +
                    "style within one file.");

            // A side database, opened read-only and disposed. This is the mechanism that defeated
            // publish.import_page_setup, so the shape here is deliberate: open, resolve names,
            // clone in ONE WblockCloneObjects call, and report what the id mapping actually says
            // rather than what was requested.
            using var src = new Database(false, true);
            try
            {
                src.ReadDwgFile(a.Path, FileOpenMode.OpenForReadAndAllShare, allowCPConversion: true, password: null);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not read '" + a.Path + "' as a drawing: " + ex.Message, ex);
            }

            var ids = new ObjectIdCollection();
            var requested = new List<string>();
            var missing = new List<string>();

            using (var srcTr = src.TransactionManager.StartTransaction())
            {
                var srcTable = (DimStyleTable)srcTr.GetObject(src.DimStyleTableId, OpenMode.ForRead);

                if (a.Names is { Count: > 0 })
                {
                    foreach (var n in a.Names.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (!srcTable.Has(n)) { missing.Add(n); continue; }
                        ids.Add(srcTable[n]);
                        requested.Add(n);
                    }
                }
                else
                {
                    foreach (ObjectId id in srcTable)
                    {
                        var rec = (DimStyleTableRecord)srcTr.GetObject(id, OpenMode.ForRead);
                        if (string.Equals(rec.Name, "Standard", StringComparison.OrdinalIgnoreCase)) continue;
                        ids.Add(id);
                        requested.Add(rec.Name);
                    }
                }
                srcTr.Commit();
            }

            if (missing.Count > 0)
                throw new ArgumentException(
                    "These styles are not in '" + Path.GetFileName(a.Path) + "': " +
                    string.Join(", ", missing) + ". Omit names to import every non-Standard style.");

            if (ids.Count == 0)
                throw new InvalidOperationException(
                    "'" + Path.GetFileName(a.Path) + "' has no dimension styles to import beyond " +
                    "Standard.");

            // What is here BEFORE the clone. Three outcomes are possible and they are decided by
            // observable state, not by IdPair.IsCloned - whose meaning was guessed once and was
            // wrong: with DuplicateRecordCloning.Replace an existing record IS overwritten while
            // IsCloned reads false, so an honest overwrite reported itself as "skipped". A tool
            // saying it did nothing when it replaced a style is the same lie as one saying it
            // imported when it did nothing, just pointing the other way.
            var destTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            var existedBefore = destTable.Cast<ObjectId>()
                .Select(x => ((DimStyleTableRecord)tr.GetObject(x, OpenMode.ForRead)).Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var mapping = new IdMapping();
            db.WblockCloneObjects(
                ids, db.DimStyleTableId, mapping,
                a.Overwrite ? DuplicateRecordCloning.Replace : DuplicateRecordCloning.Ignore,
                deferTranslation: false);

            var imported = new List<string>();   // was not here at all
            var replaced = new List<string>();   // was here, overwrite:true, definition changed
            var skipped = new List<string>();    // was here, overwrite:false, left alone

            foreach (var name in requested)
            {
                if (!existedBefore.Contains(name)) imported.Add(name);
                else if (a.Overwrite) replaced.Add(name);
                else skipped.Add(name);
            }

            var notes = new List<string>();
            if (skipped.Count > 0)
                notes.Add("Skipped styles already existed here and overwrite was false; their " +
                          "local definition is unchanged and nothing was merged.");
            if (replaced.Count > 0)
                notes.Add("Replaced styles already existed and were overwritten. Dimensions " +
                          "already using them redraw with the new definition on the next regen.");

            return Wrap(new
            {
                source = a.Path,
                requested,
                imported,
                replaced,
                skipped,
                note = notes.Count == 0 ? null : string.Join(" ", notes),
            });
        });
}
