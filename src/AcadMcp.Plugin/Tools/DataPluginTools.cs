// AutoCAD plugin handlers for the acad-data category.
// Registered under "acad.data.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// Extended data and dictionaries are how an application stores its OWN information in a drawing
// and finds it again later. Two mechanisms, and they are not interchangeable:
//
//   XDATA hangs off an ENTITY, is filed under a registered application name, and is capped at
//   16 KB per entity per application. It travels with the entity through copy and explode.
//
//   DICTIONARIES are drawing-wide (or hang off one entity as its extension dictionary) and hold
//   named objects - usually Xrecords. No size cap worth worrying about, and no registered name.
//
// Rule of thumb the descriptions repeat: a few values that belong to one object are xdata; a
// structure, or anything shared, is a dictionary.

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
using Autodesk.AutoCAD.Geometry;
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class DataPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.data.attach_xdata",                AttachXdata);
        host.Register("acad.data.get_xdata",                   GetXdata);
        host.Register("acad.data.delete_xdata",                DeleteXdata);
        host.Register("acad.data.register_app_name",           RegisterAppName);
        host.Register("acad.data.list_registered_apps",        ListRegisteredApps);
        host.Register("acad.data.create_extension_dictionary", CreateExtensionDictionary);
        host.Register("acad.data.list_dictionaries",           ListDictionaries);
        host.Register("acad.data.get_dictionary_entry",        GetDictionaryEntry);
        host.Register("acad.data.set_dictionary_entry",        SetDictionaryEntry);
        host.Register("acad.data.delete_dictionary_entry",     DeleteDictionaryEntry);
        host.Register("acad.data.create_xrecord",              CreateXrecord);
        host.Register("acad.data.read_xrecord",                ReadXrecord);
        host.Register("acad.data.update_xrecord",              UpdateXrecord);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── the value model ───────────

    /// Turns the JSON value model into TypedValues. The `type` is explicit rather than guessed
    /// from the JSON, because 1 and 1.0 are the same in JSON and are NOT the same to AutoCAD:
    /// an integer written where a real is expected reads back as a different type and breaks the
    /// round trip. Making the caller say which it means is the only way that is unambiguous.
    private static TypedValue ToTypedValue(DataValueDto v, bool forXdata)
    {
        var kind = (v.Type ?? "").Trim().ToLowerInvariant();
        switch (kind)
        {
            case "string" or "text":
                return new TypedValue(
                    (int)(forXdata ? DxfCode.ExtendedDataAsciiString : DxfCode.Text),
                    v.Value?.ToString() ?? "");
            case "real" or "double":
                return new TypedValue(
                    (int)(forXdata ? DxfCode.ExtendedDataReal : DxfCode.Real),
                    Convert.ToDouble(v.Value?.ToString(),
                                     System.Globalization.CultureInfo.InvariantCulture));
            case "int" or "integer":
                return forXdata
                    ? new TypedValue((int)DxfCode.ExtendedDataInteger32,
                                     Convert.ToInt32(v.Value?.ToString()))
                    : new TypedValue((int)DxfCode.Int32, Convert.ToInt32(v.Value?.ToString()));
            case "point":
                if (v.Point is null)
                    throw new ArgumentException("A point value needs a `point` with x, y and z.");
                return new TypedValue(
                    (int)(forXdata ? DxfCode.ExtendedDataXCoordinate : DxfCode.XCoordinate),
                    AcadEnv.ToPoint3d(v.Point));
            case "layer":
                return new TypedValue(
                    (int)(forXdata ? DxfCode.ExtendedDataLayerName : DxfCode.LayerName),
                    v.Value?.ToString() ?? "");
            case "handle":
                return new TypedValue(
                    (int)(forXdata ? DxfCode.ExtendedDataHandle : DxfCode.Handle),
                    v.Value?.ToString() ?? "");
            default:
                throw new ArgumentException(
                    "type must be string, real, int, point, layer or handle. Got '" + v.Type +
                    "'. The type is given explicitly rather than guessed because JSON cannot tell " +
                    "1 from 1.0 and AutoCAD very much can - an integer stored where a real was " +
                    "meant reads back as a different type.");
        }
    }

    private static object FromTypedValue(TypedValue tv)
    {
        var code = (DxfCode)tv.TypeCode;
        return code switch
        {
            DxfCode.ExtendedDataAsciiString or DxfCode.Text =>
                new { type = "string", value = tv.Value?.ToString() },
            DxfCode.ExtendedDataReal or DxfCode.Real =>
                new { type = "real", value = (object?)Convert.ToDouble(tv.Value) },
            DxfCode.ExtendedDataInteger16 or DxfCode.Int16 =>
                new { type = "int", value = (object?)Convert.ToInt32(tv.Value) },
            DxfCode.ExtendedDataInteger32 or DxfCode.Int32 =>
                new { type = "int", value = (object?)Convert.ToInt32(tv.Value) },
            DxfCode.ExtendedDataXCoordinate or DxfCode.XCoordinate =>
                new { type = "point", point = AcadEnv.FromPoint3d((Point3d)tv.Value!) },
            DxfCode.ExtendedDataLayerName or DxfCode.LayerName =>
                new { type = "layer", value = tv.Value?.ToString() },
            DxfCode.ExtendedDataHandle or DxfCode.Handle =>
                new { type = "handle", value = tv.Value?.ToString() },
            DxfCode.ExtendedDataRegAppName =>
                new { type = "appName", value = tv.Value?.ToString() },
            _ => new { type = "dxf" + tv.TypeCode, value = tv.Value?.ToString() },
        };
    }

    private static Entity RequireEntity(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required.");
        return (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
    }

    /// Registers the application name if it is not already there. AutoCAD REFUSES xdata filed
    /// under an unregistered name, so every write goes through here first - a caller should not
    /// have to know that registering is a separate step.
    private static bool EnsureRegistered(Database db, Transaction tr, string app)
    {
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (rat.Has(app)) return false;
        rat.UpgradeOpen();
        var rec = new RegAppTableRecord { Name = app };
        rat.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
        return true;
    }

    private static void RequireAppName(string? app)
    {
        if (string.IsNullOrWhiteSpace(app))
            throw new ArgumentException(
                "appName is required. Xdata is filed under an application name so that two " +
                "applications can store data on the same entity without treading on each other - " +
                "use your own name, not ACAD.");
        if (app!.Length > 255)
            throw new ArgumentException("appName is limited to 255 characters.");
    }

    // ─────────── xdata ───────────

    private static Task<ToolDispatchResult> AttachXdata(JsonObject args, CancellationToken ct) =>
        Run("acad.data.attach_xdata", args, ct, (doc, db, tr) =>
        {
            var a = Read<XdataArgsDto>(args);
            RequireAppName(a.AppName);
            if (a.Data is null || a.Data.Count == 0)
                throw new ArgumentException(
                    "data is required and must hold at least one value. To REMOVE this " +
                    "application's xdata, use delete_xdata - writing an empty list would be the " +
                    "same operation wearing the wrong name.");

            var ent = RequireEntity(db, tr, a.Handle, OpenMode.ForWrite);
            var registered = EnsureRegistered(db, tr, a.AppName!);

            // The buffer MUST start with the app name under code 1001; AutoCAD rejects it otherwise.
            var values = new List<TypedValue>
            {
                new((int)DxfCode.ExtendedDataRegAppName, a.AppName!),
            };
            foreach (var v in a.Data) values.Add(ToTypedValue(v, forXdata: true));

            // Xdata already on the entity under OTHER applications must survive. Setting XData
            // replaces only the buffer for the app named in it, but reading first and reporting
            // what else is there is the only way a caller can be sure of that.
            var othersBefore = OtherApps(ent, a.AppName!);

            using (var rb = new ResultBuffer(values.ToArray()))
            {
                try { ent.XData = rb; }
                catch (AcadRt.Exception ex)
                {
                    throw new ArgumentException(
                        "AutoCAD refused the xdata with " + ex.ErrorStatus + ". The commonest " +
                        "cause is exceeding the 16 KB limit that applies per entity per " +
                        "application; a long list of strings reaches it sooner than it looks.");
                }
            }

            // Read back. Xdata that did not take would otherwise look exactly like xdata that did.
            var readBack = ReadXdataFor(ent, a.AppName!);
            if (readBack.Count != a.Data.Count)
                throw new InvalidOperationException(
                    "Wrote " + a.Data.Count + " values but " + readBack.Count + " read back.");

            return Wrap(new
            {
                handle = a.Handle,
                appName = a.AppName,
                count = readBack.Count,
                data = readBack,
                appRegistered = registered,
                otherApps = othersBefore,
                note = "Read back after writing, not echoed from the request. The application name " +
                       "was " + (registered ? "registered here, because" : "already registered;") +
                       " AutoCAD refuses xdata filed under an unregistered name. Xdata belonging " +
                       "to other applications on this entity is untouched - any found is listed " +
                       "above. The limit is 16 KB per entity PER APPLICATION, and it is the one " +
                       "hard cap worth designing around: a structure larger than a few dozen " +
                       "values belongs in a dictionary instead.",
            });
        });

    private static List<string> OtherApps(Entity ent, string exclude)
    {
        var found = new List<string>();
        using var rb = ent.XData;
        if (rb is null) return found;
        foreach (var tv in rb.AsArray())
            if ((DxfCode)tv.TypeCode == DxfCode.ExtendedDataRegAppName)
            {
                var n = tv.Value?.ToString() ?? "";
                if (!string.Equals(n, exclude, StringComparison.OrdinalIgnoreCase)) found.Add(n);
            }
        return found;
    }

    /// Xdata for ONE application. The entity's buffer holds every application's data end to end,
    /// each run introduced by its own 1001 name, so it has to be split rather than returned whole.
    private static List<object> ReadXdataFor(Entity ent, string app)
    {
        var outv = new List<object>();
        using var rb = ent.XData;
        if (rb is null) return outv;
        bool mine = false;
        foreach (var tv in rb.AsArray())
        {
            if ((DxfCode)tv.TypeCode == DxfCode.ExtendedDataRegAppName)
            {
                mine = string.Equals(tv.Value?.ToString(), app, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (mine) outv.Add(FromTypedValue(tv));
        }
        return outv;
    }

    private static Task<ToolDispatchResult> GetXdata(JsonObject args, CancellationToken ct) =>
        Run("acad.data.get_xdata", args, ct, (doc, db, tr) =>
        {
            var a = Read<XdataArgsDto>(args);
            var ent = RequireEntity(db, tr, a.Handle, OpenMode.ForRead);

            using var rb = ent.XData;
            if (rb is null)
                return Wrap(new
                {
                    handle = a.Handle,
                    apps = new List<object>(),
                    count = 0,
                    note = "This entity carries no xdata at all. That is not an error - most " +
                           "entities never have any.",
                });

            // Grouped BY APPLICATION, because that is how it is stored and how it is deleted.
            var apps = new List<object>();
            string current = "";
            var values = new List<object>();
            void Flush()
            {
                if (current.Length > 0)
                    apps.Add(new { appName = current, count = values.Count, data = new List<object>(values) });
                values.Clear();
            }
            foreach (var tv in rb.AsArray())
            {
                if ((DxfCode)tv.TypeCode == DxfCode.ExtendedDataRegAppName)
                {
                    Flush();
                    current = tv.Value?.ToString() ?? "";
                    continue;
                }
                values.Add(FromTypedValue(tv));
            }
            Flush();

            var wanted = string.IsNullOrWhiteSpace(a.AppName)
                ? apps
                : apps.Where(x => string.Equals(
                      x.GetType().GetProperty("appName")!.GetValue(x)?.ToString(),
                      a.AppName, StringComparison.OrdinalIgnoreCase)).ToList();

            return Wrap(new
            {
                handle = a.Handle,
                appName = a.AppName,
                apps = wanted,
                count = wanted.Count,
                note = "Grouped by application, because that is how xdata is stored and how it is " +
                       "deleted - one entity can carry data from several applications at once and " +
                       "they do not interfere. Name an appName to see only one. Types are " +
                       "reported as they are stored, so a value written as a real comes back as a " +
                       "real and not as an int that happens to be whole.",
            });
        });

    private static Task<ToolDispatchResult> DeleteXdata(JsonObject args, CancellationToken ct) =>
        Run("acad.data.delete_xdata", args, ct, (doc, db, tr) =>
        {
            var a = Read<XdataArgsDto>(args);
            RequireAppName(a.AppName);
            var ent = RequireEntity(db, tr, a.Handle, OpenMode.ForWrite);

            var before = ReadXdataFor(ent, a.AppName!);
            if (before.Count == 0)
                throw new ArgumentException(
                    "This entity carries no xdata under '" + a.AppName + "', so there is nothing " +
                    "to delete. get_xdata lists the applications that do have data on it.");

            var others = OtherApps(ent, a.AppName!);

            // A buffer holding ONLY the app name removes that application's xdata. This is the
            // documented mechanism and it looks like a mistake, so it is worth the comment.
            using (var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, a.AppName!)))
            {
                ent.XData = rb;
            }

            var after = ReadXdataFor(ent, a.AppName!);
            if (after.Count != 0)
                throw new InvalidOperationException(
                    "Deletion did not take: " + after.Count + " values still read back under '" +
                    a.AppName + "'.");
            var othersAfter = OtherApps(ent, a.AppName!);

            return Wrap(new
            {
                handle = a.Handle,
                appName = a.AppName,
                deletedCount = before.Count,
                otherAppsBefore = others,
                otherAppsAfter = othersAfter,
                note = "Removed by writing a buffer that holds ONLY the application name, which is " +
                       "AutoCAD's documented way of clearing one application's xdata; it looks " +
                       "like a mistake and is not. Verified by reading back afterwards. Xdata " +
                       "belonging to other applications is untouched, and the before and after " +
                       "lists above are there so that can be checked rather than trusted.",
            });
        });

    private static Task<ToolDispatchResult> RegisterAppName(JsonObject args, CancellationToken ct) =>
        Run("acad.data.register_app_name", args, ct, (doc, db, tr) =>
        {
            var a = Read<XdataArgsDto>(args);
            RequireAppName(a.AppName);

            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(a.AppName!))
                throw new ArgumentException(
                    "'" + a.AppName + "' is already registered in this drawing. Registering is " +
                    "idempotent in effect but this refuses rather than reporting a change that " +
                    "did not happen.");

            EnsureRegistered(db, tr, a.AppName!);

            var rat2 = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!rat2.Has(a.AppName!))
                throw new InvalidOperationException("The name does not read back as registered.");

            return Wrap(new
            {
                appName = a.AppName,
                registered = true,
                note = "Registered. You rarely need to call this yourself: attach_xdata registers " +
                       "the name it is given, because AutoCAD refuses xdata filed under an " +
                       "unregistered one. It is here for the case where the name should exist " +
                       "before any entity carries data under it. An unreferenced name can be " +
                       "removed again with lisp.purge_regapps.",
            });
        });

    private static Task<ToolDispatchResult> ListRegisteredApps(JsonObject args, CancellationToken ct) =>
        Run("acad.data.list_registered_apps", args, ct, (doc, db, tr) =>
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            var names = new List<string>();
            foreach (ObjectId id in rat)
            {
                if (id.IsErased) continue;
                names.Add(((RegAppTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);

            return Wrap(new
            {
                count = names.Count,
                apps = names,
                note = "Every application name registered in this drawing. ACAD is AutoCAD's own " +
                       "and is always present. A name being here does NOT mean any entity carries " +
                       "data under it - registration and use are separate, which is exactly why " +
                       "lisp.purge_regapps has something to do.",
            });
        });

    // ─────────── dictionaries ───────────

    private static DBDictionary RootDictionary(Database db, Transaction tr, string? entityHandle,
                                               string? path, OpenMode mode)
    {
        DBDictionary dict;
        if (!string.IsNullOrWhiteSpace(entityHandle))
        {
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, entityHandle!), OpenMode.ForRead);
            if (ent.ExtensionDictionary.IsNull)
                throw new ArgumentException(
                    "Entity " + entityHandle + " has no extension dictionary. " +
                    "create_extension_dictionary makes one - an entity does not have one until " +
                    "something puts it there.");
            dict = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, mode);
        }
        else
        {
            dict = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, mode);
        }

        // A path walks nested dictionaries, which is how AutoCAD's own data is organised.
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var step in path!.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!dict.Contains(step))
                    throw new ArgumentException(
                        "No dictionary called '" + step + "' on that path. list_dictionaries " +
                        "shows what is there at each level.");
                var next = tr.GetObject(dict.GetAt(step), mode);
                dict = next as DBDictionary
                       ?? throw new ArgumentException(
                           "'" + step + "' is a " + next.GetRXClass().Name + ", not a dictionary, " +
                           "so the path cannot continue through it.");
            }
        }
        return dict;
    }

    private static Task<ToolDispatchResult> CreateExtensionDictionary(JsonObject args, CancellationToken ct) =>
        Run("acad.data.create_extension_dictionary", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            var ent = RequireEntity(db, tr, a.Handle, OpenMode.ForWrite);

            var existed = !ent.ExtensionDictionary.IsNull;
            if (existed)
                throw new ArgumentException(
                    "Entity " + a.Handle + " already has an extension dictionary. Use " +
                    "set_dictionary_entry to put things in it; making a second one is not " +
                    "possible and silently reusing the first would hide that.");

            ent.CreateExtensionDictionary();
            if (ent.ExtensionDictionary.IsNull)
                throw new InvalidOperationException(
                    "CreateExtensionDictionary raised no error but the entity still has none.");

            var dict = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead);
            return Wrap(new
            {
                handle = a.Handle,
                dictionaryHandle = dict.Handle.ToString(),
                entryCount = dict.Count,
                note = "An extension dictionary is a dictionary that belongs to ONE entity and " +
                       "travels with it - copy the entity and the data comes too. That is the " +
                       "difference from the drawing-wide named objects dictionary, and from " +
                       "xdata, which is capped at 16 KB and holds a flat list rather than named " +
                       "entries. Address it afterwards by passing this entity's handle to the " +
                       "dictionary tools.",
            });
        });

    private static Task<ToolDispatchResult> ListDictionaries(JsonObject args, CancellationToken ct) =>
        Run("acad.data.list_dictionaries", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForRead);

            var entries = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                var obj = tr.GetObject(e.Value, OpenMode.ForRead);
                entries.Add(new
                {
                    key = e.Key,
                    objectClass = obj.GetRXClass().Name,
                    isDictionary = obj is DBDictionary,
                    handle = obj.Handle.ToString(),
                });
            }
            entries = entries.OrderBy(x => x.GetType().GetProperty("key")!.GetValue(x)?.ToString(),
                                      StringComparer.OrdinalIgnoreCase).ToList();

            return Wrap(new
            {
                scope = string.IsNullOrWhiteSpace(a.Handle)
                    ? "named objects dictionary" : "extension dictionary of " + a.Handle,
                path = a.Path,
                count = entries.Count,
                entries,
                note = "Without a handle this lists the drawing-wide NAMED OBJECTS dictionary, " +
                       "which is where AutoCAD keeps layouts, groups, plot settings, materials " +
                       "and much else - so a fresh drawing is far from empty and most of what is " +
                       "in here is not yours. With a handle it lists that entity's extension " +
                       "dictionary. `path` walks nested dictionaries with / between the names.",
            });
        });

    private static Task<ToolDispatchResult> GetDictionaryEntry(JsonObject args, CancellationToken ct) =>
        Run("acad.data.get_dictionary_entry", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Key))
                throw new ArgumentException("key is required.");
            var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForRead);
            if (!dict.Contains(a.Key!))
                throw new ArgumentException(
                    "No entry called '" + a.Key + "' here. list_dictionaries shows what is.");

            var obj = tr.GetObject(dict.GetAt(a.Key!), OpenMode.ForRead);
            List<object>? data = null;
            if (obj is Xrecord xr)
            {
                data = new List<object>();
                using var rb = xr.Data;
                if (rb is not null) foreach (var tv in rb.AsArray()) data.Add(FromTypedValue(tv));
            }

            return Wrap(new
            {
                key = a.Key,
                objectClass = obj.GetRXClass().Name,
                handle = obj.Handle.ToString(),
                isDictionary = obj is DBDictionary,
                entryCount = obj is DBDictionary d ? d.Count : (int?)null,
                data,
                note = "An entry can be any database object. When it is an XRECORD - which is what " +
                       "these tools create - its values are decoded above; when it is a nested " +
                       "dictionary the entry count is given instead and `path` reaches inside it. " +
                       "Anything else reports its class so you know what you are looking at " +
                       "rather than getting an empty result.",
            });
        });

    private static Task<ToolDispatchResult> SetDictionaryEntry(JsonObject args, CancellationToken ct) =>
        Run("acad.data.set_dictionary_entry", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Key))
                throw new ArgumentException("key is required.");
            var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForWrite);

            var existed = dict.Contains(a.Key!);
            DBObject obj;
            if (a.Nested == true)
            {
                if (existed)
                    throw new ArgumentException(
                        "'" + a.Key + "' already exists here. Delete it first if it should become " +
                        "a nested dictionary; replacing it silently would discard whatever it holds.");
                obj = new DBDictionary();
            }
            else
            {
                if (a.Data is null || a.Data.Count == 0)
                    throw new ArgumentException(
                        "data is required, or set nested true to create a sub-dictionary instead.");
                var xr = new Xrecord();
                xr.Data = new ResultBuffer(a.Data.Select(v => ToTypedValue(v, forXdata: false)).ToArray());
                obj = xr;
            }

            dict.SetAt(a.Key!, obj);
            tr.AddNewlyCreatedDBObject(obj, true);

            // Read back through a fresh lookup rather than trusting the object just written.
            var dict2 = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForRead);
            if (!dict2.Contains(a.Key!))
                throw new InvalidOperationException("The entry does not read back from the dictionary.");
            var check = tr.GetObject(dict2.GetAt(a.Key!), OpenMode.ForRead);
            int wrote = 0;
            if (check is Xrecord x2)
            {
                using var rb = x2.Data;
                wrote = rb?.AsArray().Length ?? 0;
            }

            return Wrap(new
            {
                key = a.Key,
                handle = check.Handle.ToString(),
                objectClass = check.GetRXClass().Name,
                replaced = existed,
                valueCount = wrote,
                note = "SetAt REPLACES an entry of the same name, so `replaced` above says whether " +
                       "something was overwritten - worth checking, because nothing else would " +
                       "tell you. The entry was read back through a fresh dictionary lookup rather " +
                       "than trusting the object just written. Set nested true to create a " +
                       "sub-dictionary instead of an xrecord.",
            });
        });

    private static Task<ToolDispatchResult> DeleteDictionaryEntry(JsonObject args, CancellationToken ct) =>
        Run("acad.data.delete_dictionary_entry", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Key))
                throw new ArgumentException("key is required.");
            var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForWrite);
            if (!dict.Contains(a.Key!))
                throw new ArgumentException(
                    "No entry called '" + a.Key + "' here, so there is nothing to delete.");

            var target = tr.GetObject(dict.GetAt(a.Key!), OpenMode.ForWrite);
            var wasDict = target is DBDictionary;
            var nestedCount = target is DBDictionary nd ? nd.Count : 0;
            if (wasDict && nestedCount > 0 && a.Force != true)
                throw new ArgumentException(
                    "'" + a.Key + "' is a dictionary holding " + nestedCount + " entries. Deleting " +
                    "it would take them with it, so pass force true if that is what you mean.");

            dict.Remove(a.Key!);
            target.Erase();

            var dict2 = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForRead);
            if (dict2.Contains(a.Key!))
                throw new InvalidOperationException("The entry still reads back after removal.");

            return Wrap(new
            {
                key = a.Key,
                wasDictionary = wasDict,
                nestedEntriesRemoved = wasDict ? nestedCount : 0,
                remaining = dict2.Count,
                note = "Removed from the dictionary AND erased, because removing the name alone " +
                       "would leave the object in the drawing with nothing pointing at it. A " +
                       "nested dictionary that still holds entries is refused unless force is " +
                       "set, since deleting it takes everything inside with it.",
            });
        });

    // ─────────── xrecords ───────────

    private static Task<ToolDispatchResult> CreateXrecord(JsonObject args, CancellationToken ct) =>
        Run("acad.data.create_xrecord", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Key))
                throw new ArgumentException("key is required: the name to file the xrecord under.");
            if (a.Data is null || a.Data.Count == 0)
                throw new ArgumentException("data is required and must hold at least one value.");
            var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForWrite);
            if (dict.Contains(a.Key!))
                throw new ArgumentException(
                    "'" + a.Key + "' already exists here. update_xrecord replaces its contents; " +
                    "this refuses rather than overwriting something you may not have known about.");

            var xr = new Xrecord
            {
                Data = new ResultBuffer(a.Data.Select(v => ToTypedValue(v, forXdata: false)).ToArray()),
                XlateReferences = a.XlateReferences ?? false,
            };
            dict.SetAt(a.Key!, xr);
            tr.AddNewlyCreatedDBObject(xr, true);

            var read = ReadXrecordData(tr, db, a);
            if (read.Count != a.Data.Count)
                throw new InvalidOperationException(
                    "Wrote " + a.Data.Count + " values but " + read.Count + " read back.");

            return Wrap(new
            {
                key = a.Key,
                handle = xr.Handle.ToString(),
                count = read.Count,
                data = read,
                xlateReferences = xr.XlateReferences,
                note = "An xrecord is a named list of typed values living in a dictionary - the " +
                       "way to store a structure that is too big or too shared for xdata, and " +
                       "with no 16 KB cap. Read back after writing rather than echoed. " +
                       "xlateReferences controls whether handles inside are translated when the " +
                       "drawing is bound or inserted elsewhere; leave it false unless the values " +
                       "really are handles that must follow.",
            });
        });

    private static List<object> ReadXrecordData(Transaction tr, Database db, DictArgsDto a)
    {
        var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForRead);
        if (!dict.Contains(a.Key!))
            throw new ArgumentException("No xrecord called '" + a.Key + "' here.");
        var obj = tr.GetObject(dict.GetAt(a.Key!), OpenMode.ForRead);
        if (obj is not Xrecord xr)
            throw new ArgumentException(
                "'" + a.Key + "' is a " + obj.GetRXClass().Name + ", not an xrecord.");
        var outv = new List<object>();
        using var rb = xr.Data;
        if (rb is not null) foreach (var tv in rb.AsArray()) outv.Add(FromTypedValue(tv));
        return outv;
    }

    private static Task<ToolDispatchResult> ReadXrecord(JsonObject args, CancellationToken ct) =>
        Run("acad.data.read_xrecord", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Key))
                throw new ArgumentException("key is required.");
            var data = ReadXrecordData(tr, db, a);
            return Wrap(new
            {
                key = a.Key,
                count = data.Count,
                data,
                note = "Values come back with the type they were stored as, so one written as a " +
                       "real reads back as a real and not as an int that happens to be whole - " +
                       "which is why the type is given explicitly on the way in.",
            });
        });

    private static Task<ToolDispatchResult> UpdateXrecord(JsonObject args, CancellationToken ct) =>
        Run("acad.data.update_xrecord", args, ct, (doc, db, tr) =>
        {
            var a = Read<DictArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Key))
                throw new ArgumentException("key is required.");
            if (a.Data is null || a.Data.Count == 0)
                throw new ArgumentException(
                    "data is required. To remove an xrecord use delete_dictionary_entry.");

            var dict = RootDictionary(db, tr, a.Handle, a.Path, OpenMode.ForRead);
            if (!dict.Contains(a.Key!))
                throw new ArgumentException(
                    "No xrecord called '" + a.Key + "' here. create_xrecord makes one; this only " +
                    "updates an existing one, so a typo in the key cannot quietly create a second.");
            var obj = tr.GetObject(dict.GetAt(a.Key!), OpenMode.ForWrite);
            if (obj is not Xrecord xr)
                throw new ArgumentException(
                    "'" + a.Key + "' is a " + obj.GetRXClass().Name + ", not an xrecord.");

            var before = new List<object>();
            using (var old = xr.Data)
                if (old is not null) foreach (var tv in old.AsArray()) before.Add(FromTypedValue(tv));

            xr.Data = new ResultBuffer(a.Data.Select(v => ToTypedValue(v, forXdata: false)).ToArray());

            var after = ReadXrecordData(tr, db, a);
            if (after.Count != a.Data.Count)
                throw new InvalidOperationException(
                    "Wrote " + a.Data.Count + " values but " + after.Count + " read back.");

            return Wrap(new
            {
                key = a.Key,
                handle = xr.Handle.ToString(),
                countBefore = before.Count,
                count = after.Count,
                data = after,
                note = "The contents are REPLACED, not merged - an xrecord holds one list and " +
                       "writing a new one discards the old, so the previous count is reported " +
                       "above to make that visible. Read back after writing.",
            });
        });
}
