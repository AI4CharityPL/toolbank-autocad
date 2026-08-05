// AutoCAD plugin handlers for acad-sheetsets — roadmap 2.1, first tranche.
//
// THE ONLY COM CATEGORY IN THIS BANK. There is no DatabaseServices API for sheet sets; a .DST is
// a file edited through a COM object graph entirely outside the DWG. Every rule this file obeys is
// written down first, in docs/engineering-rules/45-sheet-sets-com.md. The three that shape the
// code most:
//
//   * TAKE A PATH, NEVER HOLD A HANDLE. IAcSmSheetSetMgr.FindOpenDatabase lets each call resolve
//     the .DST itself, so no database is carried across calls. That is why open_sheet_set and
//     close_sheet_set do not exist: every stateful thing this bank has attempted - refedit_*, the
//     plot queue, undo - either failed or had to be withdrawn.
//   * NEVER CALL CloseAll(). It would discard whatever the user has unsaved in the Sheet Set
//     Manager palette. A tool closes only a database it opened itself, by path.
//   * NO COMException REACHES A CALLER UNWRAPPED. An HRESULT names nothing actionable, which is
//     the same problem as the bare eInvalidInput that made create_page_setup undiagnosable for a
//     full diagnostic cycle.
//
// This tranche is read-only on purpose. It needs none of the Save() discipline, and it unblocks
// fields.insert_field_sheet_set_property, which shipped already and has been dead ever since
// because get_sheet_property did not exist.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Interop;

namespace AcadMcp.Plugin.Tools;

internal static class SheetSetsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.sheetsets.get_sheet_set_info", GetSheetSetInfo);
        host.Register("acad.sheetsets.get_sheet_set_path", GetSheetSetPath);
        host.Register("acad.sheetsets.list_sheets", ListSheets);
        host.Register("acad.sheetsets.list_subsets", ListSubsets);
        host.Register("acad.sheetsets.get_sheet_property", GetSheetProperty);
        host.Register("acad.sheetsets.list_custom_properties", ListCustomProperties);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    // ─────────── the one place COM is entered ───────────

    /// <summary>
    /// Resolve a .DST path, hand the sheet set to <paramref name="work"/>, and release everything
    /// this method acquired — closing the database only if it was not already open.
    /// </summary>
    /// <remarks>
    /// Rule 45 §3 and §5. Every tool goes through here so the lifetime rules exist in exactly one
    /// place: a database ALREADY open (the user has it in the Sheet Set Manager) is used and left
    /// open, because closing it would take their session with it. One this method opened is closed
    /// again, by path, never through CloseAll.
    /// </remarks>
    private static JsonObject WithSheetSet(string? path, Func<IAcSmSheetSet, string, JsonObject> work)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "path is required: the .DST sheet set file. Every tool in this category takes it " +
                "per call - none of them hold a sheet set open between calls.");

        var full = Path.GetFullPath(path!);
        if (!File.Exists(full))
            throw new ArgumentException("No such sheet set file: " + full);
        if (!string.Equals(Path.GetExtension(full), ".dst", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "'" + full + "' is not a .DST file. A sheet set lives in its own file, separate " +
                "from any drawing.");

        IAcSmSheetSetMgr? mgr = null;
        IAcSmDatabase? db = null;
        var weOpenedIt = false;

        try
        {
            mgr = new AcSmSheetSetMgr();

            // FindOpenDatabase THROWS E_FAIL when no sheet set is open, rather than returning
            // null as its signature suggests. That threw before the OpenDatabase attempts below
            // could run, so the per-attempt diagnostics never appeared and every call reported
            // the same opaque outer message. "Not open" is not an error here - it is the normal
            // case for a path nobody has touched yet.
            try { db = mgr.FindOpenDatabase(full); }
            catch (COMException) { db = null; }

            if (db is null)
            {
                // OpenDatabase(String, Boolean) - and the metadata does not say what the flag
                // means. ObjectARX documents it as bFailIfNotFound in one place and differently
                // in another, so both are attempted rather than guessed at, and the failure
                // below reports what each attempt said. Guessing a boolean's polarity has cost
                // this session twice already: IdPair.IsCloned and CompareLayerStateToDb.
                var attempts = new List<string>();
                foreach (var flag in new[] { true, false })
                {
                    try { db = mgr.OpenDatabase(full, flag); if (db is not null) { weOpenedIt = true; break; } }
                    catch (COMException ex) { attempts.Add($"OpenDatabase(flag={flag}) -> 0x{ex.ErrorCode:X8} {ex.Message}"); }
                }
                if (db is null && attempts.Count > 0)
                    throw new InvalidOperationException(
                        "AutoCAD would not open the sheet set '" + full + "'. Both forms of the " +
                        "OpenDatabase flag were tried: " + string.Join(" | ", attempts) +
                        ". The sheet set subsystem usually needs the Sheet Set Manager palette to " +
                        "have been opened once in this AutoCAD session before it will serve COM " +
                        "callers - try SHEETSET (Ctrl+4) and call again.");
            }
            if (db is null)
                throw new InvalidOperationException(
                    "AutoCAD returned no sheet set database for '" + full + "' and raised no error.");

            var ss = db.GetSheetSet()
                ?? throw new InvalidOperationException(
                    "'" + full + "' opened but carries no sheet set. The file may be corrupt.");

            return work(ss, full);
        }
        catch (COMException ex)
        {
            // Rule 45 §6. An HRESULT on its own tells a caller nothing.
            throw new InvalidOperationException(
                "The sheet set subsystem refused to work with '" + full + "' (HRESULT 0x" +
                ex.ErrorCode.ToString("X8") + "): " + ex.Message +
                ". Sheet sets are reached through COM, so this is AutoCAD's own subsystem talking " +
                "rather than the drawing database. If the file is open in another AutoCAD session " +
                "or on a network share that has gone away, that is the usual cause.", ex);
        }
        finally
        {
            // Reverse acquisition order, and only what we opened.
            if (weOpenedIt && mgr is not null && db is not null)
            {
                try { mgr.Close((AcSmDatabase)db); } catch (COMException) { /* nothing useful to add on the way out */ }
            }
            if (db is not null) { try { Marshal.ReleaseComObject(db); } catch (Exception) { } }
            if (mgr is not null) { try { Marshal.ReleaseComObject(mgr); } catch (Exception) { } }
        }
    }

    /// <summary>Walk a subset tree depth-first, yielding every sheet with the subset path it sits under.</summary>
    private static void WalkSheets(IAcSmSubset subset, string prefix, List<object> into)
    {
        var en = subset.GetSheetEnumerator();
        try
        {
            en.Reset();
            while (true)
            {
                var comp = en.Next();
                if (comp is null) break;
                try
                {
                    if (comp is IAcSmSheet sheet)
                    {
                        into.Add(new
                        {
                            name = sheet.GetName(),
                            number = sheet.GetNumber(),
                            title = sheet.GetTitle(),
                            description = sheet.GetDesc(),
                            subset = prefix,
                            doNotPlot = sheet.GetDoNotPlot(),
                        });
                    }
                    else if (comp is IAcSmSubset nested)
                    {
                        var name = nested.GetName();
                        WalkSheets(nested, prefix.Length == 0 ? name : prefix + " / " + name, into);
                    }
                }
                finally { try { Marshal.ReleaseComObject(comp); } catch (Exception) { } }
            }
        }
        finally { try { Marshal.ReleaseComObject(en); } catch (Exception) { } }
    }

    private static void WalkSubsets(IAcSmSubset subset, string prefix, List<object> into)
    {
        var en = subset.GetSheetEnumerator();
        try
        {
            en.Reset();
            while (true)
            {
                var comp = en.Next();
                if (comp is null) break;
                try
                {
                    if (comp is IAcSmSubset nested)
                    {
                        var name = nested.GetName();
                        var full = prefix.Length == 0 ? name : prefix + " / " + name;
                        var sheets = new List<object>();
                        WalkSheets(nested, "", sheets);
                        into.Add(new { name, path = full, sheetCount = sheets.Count });
                        WalkSubsets(nested, full, into);
                    }
                }
                finally { try { Marshal.ReleaseComObject(comp); } catch (Exception) { } }
            }
        }
        finally { try { Marshal.ReleaseComObject(en); } catch (Exception) { } }
    }

    private static Dictionary<string, string> CustomProps(IAcSmCustomPropertyBag bag)
    {
        var into = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var en = bag.GetPropertyEnumerator();
        try
        {
            en.Reset();
            while (true)
            {
                en.Next(out var name, out var value);
                if (string.IsNullOrEmpty(name)) break;
                try { into[name] = value?.GetValue()?.ToString() ?? ""; }
                catch (COMException) { into[name] = "<unreadable>"; }
                finally { if (value is not null) { try { Marshal.ReleaseComObject(value); } catch (Exception) { } } }
            }
        }
        finally { try { Marshal.ReleaseComObject(en); } catch (Exception) { } }
        return into;
    }

    // ─────────── tools ───────────

    private static Task<ToolDispatchResult> GetSheetSetInfo(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.sheetsets.get_sheet_set_info", ct, (doc, db, tr) =>
        {
            var a = Read<SheetSetPathArgsDto>(args);
            return WithSheetSet(a.Path, (ss, full) =>
            {
                var sheets = new List<object>();
                WalkSheets(ss, "", sheets);
                var subsets = new List<object>();
                WalkSubsets(ss, "", subsets);
                return Wrap(new
                {
                    path = full,
                    name = ss.GetName(),
                    description = ss.GetDesc(),
                    sheetCount = sheets.Count,
                    subsetCount = subsets.Count,
                });
            });
        });

    private static Task<ToolDispatchResult> GetSheetSetPath(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.sheetsets.get_sheet_set_path", ct, (doc, db, tr) =>
        {
            var a = Read<SheetSetPathArgsDto>(args);
            return WithSheetSet(a.Path, (ss, full) => Wrap(new
            {
                path = full,
                name = ss.GetName(),
                description = ss.GetDesc(),
                note = "Resolved and readable. Every tool in this category takes this path per " +
                       "call rather than holding the set open, so this is the argument to reuse.",
            }));
        });

    // NOT BUILT, and deliberately: "which sheet set is this drawing a sheet of".
    //
    // IAcSmSheetSetMgr.GetSheetFromLayout answers it, but its parameter is an AcadObject from
    // AXDBLib - a SECOND COM interop assembly. Rule 45 states AcSmComponents.Interop is the only
    // one this plugin references, and pulling in another to serve one convenience lookup is
    // exactly the kind of decision that should be made deliberately rather than discovered in a
    // build error. Recorded as its own item instead.

    private static Task<ToolDispatchResult> ListSheets(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.sheetsets.list_sheets", ct, (doc, db, tr) =>
        {
            var a = Read<SheetSetPathArgsDto>(args);
            return WithSheetSet(a.Path, (ss, full) =>
            {
                var sheets = new List<object>();
                WalkSheets(ss, "", sheets);
                return Wrap(new { path = full, sheets, count = sheets.Count });
            });
        });

    private static Task<ToolDispatchResult> ListSubsets(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.sheetsets.list_subsets", ct, (doc, db, tr) =>
        {
            var a = Read<SheetSetPathArgsDto>(args);
            return WithSheetSet(a.Path, (ss, full) =>
            {
                var subsets = new List<object>();
                WalkSubsets(ss, "", subsets);
                return Wrap(new { path = full, subsets, count = subsets.Count });
            });
        });

    private static Task<ToolDispatchResult> GetSheetProperty(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.sheetsets.get_sheet_property", ct, (doc, db, tr) =>
        {
            var a = Read<SheetPropertyArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Sheet))
                throw new ArgumentException("sheet is required: the sheet's name or its number.");

            return WithSheetSet(a.Path, (ss, full) =>
            {
                var found = FindSheet(ss, a.Sheet!);
                if (found is null)
                {
                    var all = new List<object>();
                    WalkSheets(ss, "", all);
                    throw new ArgumentException(
                        "No sheet named or numbered '" + a.Sheet + "' in '" + full + "'. Present: " +
                        string.Join(", ", all.ConvertAll(x => x.ToString())) + ". Use list_sheets.");
                }

                try
                {
                    var builtIn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["name"] = found.GetName() ?? "",
                        ["number"] = found.GetNumber() ?? "",
                        ["title"] = found.GetTitle() ?? "",
                        ["description"] = found.GetDesc() ?? "",
                    };
                    var custom = found is IAcSmCustomPropertyBag bag
                        ? CustomProps(bag)
                        : new Dictionary<string, string>();

                    if (!string.IsNullOrWhiteSpace(a.Property))
                    {
                        if (builtIn.TryGetValue(a.Property!, out var bv))
                            return Wrap(new { path = full, sheet = builtIn["name"], property = a.Property, value = bv, kind = "builtIn" });
                        if (custom.TryGetValue(a.Property!, out var cv))
                            return Wrap(new { path = full, sheet = builtIn["name"], property = a.Property, value = cv, kind = "custom" });
                        throw new ArgumentException(
                            "Sheet '" + builtIn["name"] + "' has no property '" + a.Property +
                            "'. Built in: " + string.Join(", ", builtIn.Keys) +
                            ". Custom: " + (custom.Count == 0 ? "(none)" : string.Join(", ", custom.Keys)) + ".");
                    }

                    return Wrap(new { path = full, sheet = builtIn["name"], builtIn, custom });
                }
                finally { try { Marshal.ReleaseComObject(found); } catch (Exception) { } }
            });
        });

    /// <summary>Find a sheet by name OR number — a caller thinks in whichever they have to hand.</summary>
    private static IAcSmSheet? FindSheet(IAcSmSubset subset, string wanted)
    {
        var en = subset.GetSheetEnumerator();
        try
        {
            en.Reset();
            while (true)
            {
                var comp = en.Next();
                if (comp is null) break;
                if (comp is IAcSmSheet sheet)
                {
                    if (string.Equals(sheet.GetName(), wanted, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(sheet.GetNumber(), wanted, StringComparison.OrdinalIgnoreCase))
                        return sheet;   // caller releases
                }
                else if (comp is IAcSmSubset nested)
                {
                    var hit = FindSheet(nested, wanted);
                    if (hit is not null) { try { Marshal.ReleaseComObject(comp); } catch (Exception) { } return hit; }
                }
                try { Marshal.ReleaseComObject(comp); } catch (Exception) { }
            }
        }
        finally { try { Marshal.ReleaseComObject(en); } catch (Exception) { } }
        return null;
    }

    private static Task<ToolDispatchResult> ListCustomProperties(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.sheetsets.list_custom_properties", ct, (doc, db, tr) =>
        {
            var a = Read<SheetSetPathArgsDto>(args);
            return WithSheetSet(a.Path, (ss, full) =>
            {
                var setLevel = ss is IAcSmCustomPropertyBag bag
                    ? CustomProps(bag)
                    : new Dictionary<string, string>();
                return Wrap(new
                {
                    path = full,
                    sheetSetProperties = setLevel,
                    count = setLevel.Count,
                    note = "These are the sheet-set-level custom properties. Per-sheet ones are " +
                           "reported by get_sheet_property, because a sheet can override the set.",
                });
            });
        });
}
