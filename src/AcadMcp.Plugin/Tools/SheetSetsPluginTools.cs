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
// The reads came first on purpose: they needed no save discipline and they unblocked
// fields.insert_field_sheet_set_property, which shipped already and had been dead ever since
// because get_sheet_property did not exist.
//
// The writes then had to establish what "saved" means here, and none of the three answers is what
// the API's shape suggests — the commit is UnlockDb(db, bCommit: true) rather than Save(), a
// sheet's name is composed from number + title rather than stored, and SetTitle("") fails where
// SetTitle(" ") saves as "". All three are recorded in rule 45 §2 and §10, with the measurements.

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
        host.Register("acad.sheetsets.set_sheet_number", SetSheetNumber);
        host.Register("acad.sheetsets.rename_sheet", RenameSheet);
        host.Register("acad.sheetsets.set_sheet_title", SetSheetTitle);
        host.Register("acad.sheetsets.set_sheet_do_not_plot", SetSheetDoNotPlot);
        host.Register("acad.sheetsets.create_subset", CreateSubset);
        host.Register("acad.sheetsets.delete_subset", DeleteSubset);
        host.Register("acad.sheetsets.move_sheet_to_subset", MoveSheetToSubset);
        host.Register("acad.sheetsets.set_sheet_property", SetSheetProperty);
        host.Register("acad.sheetsets.define_custom_property", DefineCustomProperty);
        host.Register("acad.sheetsets.reorder_sheet", ReorderSheet);
        host.Register("acad.sheetsets.remove_sheet", RemoveSheet);
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

    /// <summary>The custom property bag of a sheet, subset or sheet set.</summary>
    /// <remarks>
    /// **Fetched, never cast.** `IAcSmSheet` implements exactly `IAcSmComponent` and
    /// `IAcSmPersist` — it is not itself a property bag, and neither is `IAcSmSheetSet`. The
    /// first version of the read tools tested `x is IAcSmCustomPropertyBag bag` and fell back to
    /// an empty dictionary, so `list_custom_properties` and the `custom` half of
    /// `get_sheet_property` answered "no custom properties" for every sheet set ever passed to
    /// them. Both shipped and both were "verified", because a call that succeeds and returns
    /// nothing looks exactly like a sheet set that has nothing.
    ///
    /// The nearest miss in this bank: the failure was not an exception, a wrong value or a
    /// crash. It was an EMPTY RESULT, which is the hardest kind to notice, and only a fixture
    /// with known properties in it could tell the difference.
    /// </remarks>
    private static IAcSmCustomPropertyBag? BagOf(object owner)
    {
        try
        {
            return owner switch
            {
                IAcSmSheet s => s.GetCustomPropertyBag(),
                IAcSmSheetSet ss => ss.GetCustomPropertyBag(),
                IAcSmSubset sub => sub.GetCustomPropertyBag(),
                _ => null,
            };
        }
        catch (COMException) { return null; }
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
                    var bag = BagOf(found);
                    var custom = bag is null ? new Dictionary<string, string>() : CustomProps(bag);

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
    /// <summary>Find a subset by its bare name or by its full "Parent / Child" path.</summary>
    /// <remarks>
    /// Both forms are accepted because <c>list_subsets</c> reports both, and a caller that read
    /// that output should be able to paste either back. Bare names are matched anywhere in the
    /// tree; a path is matched exactly, which is how two subsets sharing a name are told apart.
    /// The caller releases what comes back.
    /// </remarks>
    private static IAcSmSubset? FindSubset(IAcSmSubset root, string wanted, string prefix = "")
    {
        var en = root.GetSheetEnumerator();
        try
        {
            en.Reset();
            while (true)
            {
                var comp = en.Next();
                if (comp is null) break;
                if (comp is IAcSmSubset nested)
                {
                    var name = nested.GetName();
                    var full = prefix.Length == 0 ? name : prefix + " / " + name;
                    if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(full, wanted, StringComparison.OrdinalIgnoreCase))
                        return nested;   // caller releases

                    var hit = FindSubset(nested, wanted, full);
                    if (hit is not null) { try { Marshal.ReleaseComObject(comp); } catch (Exception) { } return hit; }
                }
                try { Marshal.ReleaseComObject(comp); } catch (Exception) { }
            }
        }
        finally { try { Marshal.ReleaseComObject(en); } catch (Exception) { } }
        return null;
    }

    /// <summary>The subset a caller named, or the sheet set itself when nothing was named.</summary>
    /// <remarks>
    /// The sheet set IS a subset — <c>IAcSmSheetSet</c> carries every member <c>IAcSmSubset</c>
    /// does, including CreateSubset — so "no parent given" resolves to the root rather than being
    /// a separate code path.
    /// </remarks>
    private static IAcSmSubset RequireSubsetOrRoot(IAcSmSheetSet ss, string? wanted, string full)
    {
        if (string.IsNullOrWhiteSpace(wanted)) return ss;
        return FindSubset(ss, wanted!)
            ?? throw new ArgumentException(
                "No subset named '" + wanted + "' in '" + full + "'. Use list_subsets, which " +
                "reports both the bare name and the full 'Parent / Child' path; either is accepted.");
    }

    private static int CountSheets(IAcSmSubset subset)
    {
        var into = new List<object>();
        WalkSheets(subset, "", into);
        return into.Count;
    }

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
                var bag = BagOf(ss);
                var setLevel = bag is null ? new Dictionary<string, string>() : CustomProps(bag);
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

    // ─────────── writes: lock, mutate, save, unlock ───────────

    /// <summary>
    /// The write counterpart of <see cref="WithSheetSet"/>: locks the .DST, mutates, saves, and
    /// unlocks — in that order, with the unlock in a finally.
    /// </summary>
    /// <remarks>
    /// Rule 45 §1 and §2. There is no transaction here and nothing to abort, so a half-finished
    /// edit stays half-finished; the lock is what stops a second writer joining it midway, and
    /// Save() is what stops the whole thing evaporating.
    ///
    /// GetLockStatus is checked FIRST and the owner reported, because "someone else has this open"
    /// is an answer a caller can act on and a raw E_FAIL is not.
    /// </remarks>
    private static JsonObject WithSheetSetWrite(string? path, Func<IAcSmSheetSet, string, JsonObject> work)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is required.");
        var full = Path.GetFullPath(path!);
        if (!File.Exists(full)) throw new ArgumentException("No such sheet set file: " + full);

        IAcSmSheetSetMgr? mgr = null;
        IAcSmDatabase? db = null;
        var weOpenedIt = false;
        var locked = false;

        try
        {
            mgr = new AcSmSheetSetMgr();
            try { db = mgr.FindOpenDatabase(full); } catch (COMException) { db = null; }
            if (db is null)
            {
                foreach (var flag in new[] { true, false })
                {
                    try { db = mgr.OpenDatabase(full, flag); if (db is not null) { weOpenedIt = true; break; } }
                    catch (COMException) { }
                }
            }
            if (db is null)
                throw new InvalidOperationException("AutoCAD would not open the sheet set '" + full + "'.");

            var status = db.GetLockStatus();
            if (status != AcSmLockStatus.AcSmLockStatus_UnLocked
                && status != AcSmLockStatus.AcSmLockStatus_Locked_Local)
            {
                string owner = "", info = "";
                try { db.GetLockOwnerInfo(out owner, out info); } catch (COMException) { }
                throw new InvalidOperationException(
                    "'" + full + "' is locked by someone else (" + status + ")" +
                    (string.IsNullOrWhiteSpace(owner) ? "" : " - owner: " + owner + " " + info) +
                    ". A sheet set is a shared file; nothing was changed.");
            }

            db.LockDb(db);
            locked = true;

            var ss = db.GetSheetSet()
                ?? throw new InvalidOperationException("'" + full + "' carries no sheet set.");

            var result = work(ss, full);

            // Rule 45 §2, and the commit is UnlockDb(db, bCommit: true) - NOT Save().
            //
            // IAcSmDatabase.Save takes an AcSmDSTFiler, and the filer is a serialisation
            // primitive: you Init(pUnk, pDb, bForWrite) it yourself and then drive WriteObject /
            // WriteString / WriteInt by hand. It is how the DST format writes objects, not a
            // "save my changes" button, so calling it here would mean hand-rolling the file
            // format. UnlockDb's second argument is the commit flag and is the caller-level
            // path.
            //
            // Committing HERE rather than in the finally is deliberate. If the commit throws,
            // that exception has to reach the caller: a write tool whose save failed must not
            // report success. The finally below now only releases a lock we still hold, which
            // is the failure path, and there it abandons rather than commits.
            db.UnlockDb(db, true);
            locked = false;

            return result;
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                "The sheet set subsystem refused to write to '" + full + "' (HRESULT 0x" +
                ex.ErrorCode.ToString("X8") + "): " + ex.Message +
                ". Nothing was saved. If the file is open elsewhere or read-only, that is the " +
                "usual cause.", ex);
        }
        finally
        {
            if (locked && db is not null)
            {
                // Reached only when the work or the commit threw, so `locked` is still true.
                // bCommit: false - abandon rather than persist a half-finished edit. The unlock
                // itself must still happen, or the .DST stays locked for the rest of the AutoCAD
                // session and every later call reports it as owned by someone else.
                try { db.UnlockDb(db, false); } catch (COMException) { }
            }
            if (weOpenedIt && mgr is not null && db is not null)
            {
                try { mgr.Close((AcSmDatabase)db); } catch (COMException) { }
            }
            if (db is not null) { try { Marshal.ReleaseComObject(db); } catch (Exception) { } }
            if (mgr is not null) { try { Marshal.ReleaseComObject(mgr); } catch (Exception) { } }
        }
    }

    /// <summary>What to hand SetTitle so that clearing a title actually works.</summary>
    /// <remarks>
    /// Three measurements, and none of them is what the API appears to offer:
    ///
    ///   SetTitle("")   -> E_INVALIDARG, "Value does not fall within the expected range", which
    ///                     names neither the value nor the range.
    ///   SetTitle(" ")  -> accepted. GetTitle() then returns " ".
    ///   reload         -> the saved title is "". The whitespace is trimmed on the way to disk.
    ///
    /// So a title CAN be cleared; "" is simply not the argument that does it. Rather than make
    /// every caller know that, "" is translated to " " here and the result is reported as the ""
    /// that will actually be on disk — otherwise the tool answers " " while the file says "",
    /// which is the in-memory-versus-persisted split this category exists to close.
    /// </remarks>
    private const string BlankTitle = " ";

    private static string TitleToSet(string value) => value.Length == 0 ? BlankTitle : value;

    /// <summary>The title as it will read after a save, not as it reads in memory.</summary>
    private static string PersistedTitle(string inMemory) =>
        string.IsNullOrWhiteSpace(inMemory) ? "" : inMemory;

    private static IAcSmSheet RequireSheet(IAcSmSheetSet ss, string? wanted, string full)
    {
        if (string.IsNullOrWhiteSpace(wanted))
            throw new ArgumentException("sheet is required: the sheet's name or its number.");
        return FindSheet(ss, wanted!)
            ?? throw new ArgumentException(
                "No sheet named or numbered '" + wanted + "' in '" + full + "'. Use list_sheets.");
    }

    private static Task<ToolDispatchResult> SetSheetNumber(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.set_sheet_number", ct, (doc, db, tr) =>
        {
            var a = Read<SheetWriteArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Value))
                throw new ArgumentException("value is required: the new sheet number, e.g. 'A-102'.");
            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var before = sheet.GetNumber();
                    sheet.SetNumber(a.Value);
                    return Wrap(new { path = full, sheet = sheet.GetName(), before, number = sheet.GetNumber() });
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });

    /// <summary>Rename AND renumber a sheet, which is the only way its name can be changed.</summary>
    /// <remarks>
    /// The first version of this called <c>IAcSmSheet.SetName</c> and reported success while
    /// changing nothing — the failure shape this whole sweep exists to remove, and it passed its
    /// own read-back because it re-read the same unchanged value.
    ///
    /// Measured, one variable at a time, against AutoCAD's own sample set:
    ///   * changing ONLY the title moved the reported name "T-01 TITLE SHEET" -> "T-01 PROBE TITLE"
    ///   * SetName("PROBE-NAME") left the name at "T-01 PROBE TITLE"
    /// So the name is composed from number + title and is not stored. There is nothing to set.
    ///
    /// AutoCAD's own command is "Rename &amp; Renumber Sheet" and edits both fields together, so
    /// that is what this is. Doing both under ONE lock also means a caller cannot leave a sheet
    /// renumbered but not retitled because the second call failed.
    /// </remarks>
    private static Task<ToolDispatchResult> RenameSheet(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.rename_sheet", ct, (doc, db, tr) =>
        {
            var a = Read<SheetRenameArgsDto>(args);
            if (a.Number is null && a.Title is null)
                throw new ArgumentException(
                    "At least one of number or title is required. A sheet has no separately " +
                    "stored name: what is displayed is its number and title together, so " +
                    "renaming one means setting one of those.");

            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var beforeNumber = sheet.GetNumber();
                    var beforeTitle = PersistedTitle(sheet.GetTitle());
                    var beforeName = sheet.GetName();

                    if (a.Number is not null) sheet.SetNumber(a.Number);
                    if (a.Title is not null) sheet.SetTitle(TitleToSet(a.Title));

                    return Wrap(new
                    {
                        path = full,
                        before = new { number = beforeNumber, title = beforeTitle, name = beforeName },
                        number = sheet.GetNumber(),
                        title = PersistedTitle(sheet.GetTitle()),
                        name = sheet.GetName(),
                        note = "A sheet's name is composed from its number and title, not stored " +
                               "separately - that is why this tool takes those two rather than a name.",
                    });
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });

    private static Task<ToolDispatchResult> SetSheetTitle(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.set_sheet_title", ct, (doc, db, tr) =>
        {
            var a = Read<SheetWriteArgsDto>(args);
            if (a.Value is null)
                throw new ArgumentException(
                    "value is required: the new sheet title. Pass \"\" to clear it.");
            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var before = PersistedTitle(sheet.GetTitle());
                    sheet.SetTitle(TitleToSet(a.Value));
                    return Wrap(new
                    {
                        path = full,
                        sheet = sheet.GetName(),
                        before,
                        title = PersistedTitle(sheet.GetTitle()),
                    });
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });

    private static Task<ToolDispatchResult> SetSheetDoNotPlot(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.set_sheet_do_not_plot", ct, (doc, db, tr) =>
        {
            var a = Read<SheetFlagArgsDto>(args);
            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var before = sheet.GetDoNotPlot();
                    sheet.SetDoNotPlot(a.DoNotPlot);
                    return Wrap(new
                    {
                        path = full, sheet = sheet.GetName(), before, doNotPlot = sheet.GetDoNotPlot(),
                        note = "The Publisher honours this: a do-not-plot sheet is skipped rather " +
                               "than cancelling the job, which is what an unplottable sheet does.",
                    });
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });

    // ─────────── sheet order and removal ───────────

    private static Task<ToolDispatchResult> ReorderSheet(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.reorder_sheet", ct, (doc, db, tr) =>
        {
            var a = Read<ReorderArgsDto>(args);
            var haveBefore = !string.IsNullOrWhiteSpace(a.Before);
            var haveAfter = !string.IsNullOrWhiteSpace(a.After);
            if (haveBefore == haveAfter)
                throw new ArgumentException(
                    "Exactly one of before or after is required: the sheet this one should sit " +
                    "next to, by name or by number. Giving both, or neither, does not describe a " +
                    "position.");

            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var anchorName = (haveBefore ? a.Before : a.After)!;
                    var anchor = RequireSheet(ss, anchorName, full);
                    try
                    {
                        var number = sheet.GetNumber();
                        if (string.Equals(number, anchor.GetNumber(), StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException(
                                "A sheet cannot be positioned relative to itself.");

                        var owner = sheet.GetOwner() as IAcSmSubset;
                        var anchorOwner = anchor.GetOwner() as IAcSmSubset;
                        try
                        {
                            // Ordering is WITHIN one parent. Moving between parents is a different
                            // operation with different consequences, and conflating them would let
                            // "put A-102 after A-101" quietly relocate a sheet to another subset.
                            var here = owner?.GetName() ?? "";
                            var there = anchorOwner?.GetName() ?? "";
                            if (!string.Equals(here, there, StringComparison.OrdinalIgnoreCase))
                                throw new ArgumentException(
                                    "Sheet '" + sheet.GetName() + "' is in '" + here + "' and '" +
                                    anchor.GetName() + "' is in '" + there + "'. Ordering happens " +
                                    "within one subset; use move_sheet_to_subset to relocate a sheet.");
                            if (owner is null)
                                throw new InvalidOperationException(
                                    "Sheet '" + sheet.GetName() + "' reports no owning subset.");

                            // Same detach-then-insert as a move: InsertComponent adds rather than
                            // re-parents, and inserting a component the owner still holds answers
                            // 0x800288C6 duplicate identifier.
                            owner.RemoveSheet((AcSmSheet)sheet);
                            if (haveBefore) owner.InsertComponent((IAcSmComponent)sheet, (IAcSmComponent)anchor);
                            else owner.InsertComponentAfter((IAcSmComponent)sheet, (IAcSmComponent)anchor);

                            // Checked inside the lock, before the commit. See rule 45 §2.
                            var found = FindSheet(ss, number)
                                ?? throw new InvalidOperationException(
                                    "Sheet '" + number + "' vanished during the reorder, which was " +
                                    "therefore abandoned; nothing was saved.");
                            try { Marshal.ReleaseComObject(found); } catch (Exception) { }

                            var order = new List<object>();
                            WalkSheets(owner, "", order);
                            return Wrap(new
                            {
                                path = full,
                                sheet = sheet.GetName(),
                                number,
                                placed = haveBefore ? "before" : "after",
                                anchor = anchor.GetName(),
                                subset = here,
                                sheetsInSubset = order.Count,
                            });
                        }
                        finally
                        {
                            if (owner is not null) { try { Marshal.ReleaseComObject(owner); } catch (Exception) { } }
                            if (anchorOwner is not null) { try { Marshal.ReleaseComObject(anchorOwner); } catch (Exception) { } }
                        }
                    }
                    finally { try { Marshal.ReleaseComObject(anchor); } catch (Exception) { } }
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });

    /// <summary>Take a sheet out of the set. The layout in the DWG is not touched.</summary>
    /// <remarks>
    /// A sheet is a REFERENCE to a layout in a drawing file. Removing it from the set removes the
    /// reference; the layout, and the drawing, stay exactly as they were. That distinction is the
    /// whole reason this is safe to offer at all, and it is stated in the result rather than left
    /// for the caller to hope about.
    /// </remarks>
    private static Task<ToolDispatchResult> RemoveSheet(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.remove_sheet", ct, (doc, db, tr) =>
        {
            var a = Read<SheetRefArgsDto>(args);
            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                string name, number, subset;
                try
                {
                    name = sheet.GetName();
                    number = sheet.GetNumber();
                    var owner = sheet.GetOwner() as IAcSmSubset
                        ?? throw new InvalidOperationException(
                            "Sheet '" + name + "' reports no owning subset or sheet set.");
                    try
                    {
                        subset = owner.GetName();
                        owner.RemoveSheet((AcSmSheet)sheet);
                    }
                    finally { try { Marshal.ReleaseComObject(owner); } catch (Exception) { } }
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }

                // Confirmed inside the lock: the sheet must be GONE. If it is somehow still
                // present the removal did not take, and reporting success would be a lie - the
                // exception abandons the edit with bCommit:false instead.
                var still = FindSheet(ss, number);
                if (still is not null)
                {
                    try { Marshal.ReleaseComObject(still); } catch (Exception) { }
                    throw new InvalidOperationException(
                        "Sheet '" + number + "' is still in '" + full + "' after RemoveSheet, so " +
                        "the removal was abandoned and nothing was saved.");
                }

                var left = new List<object>();
                WalkSheets(ss, "", left);
                return Wrap(new
                {
                    path = full,
                    removed = name,
                    number,
                    fromSubset = subset,
                    sheetsRemaining = left.Count,
                    note = "The sheet's REFERENCE was removed from the set. The layout it pointed " +
                           "at, and the drawing holding it, are untouched - re-add it with " +
                           "add_sheet if this was a mistake.",
                });
            });
        });

    // ─────────── custom properties ───────────

    /// <summary>The four fields that are NOT custom properties, and what sets each instead.</summary>
    /// <remarks>
    /// `get_sheet_property` reports these under `builtIn` and everything else under `custom`, so a
    /// caller can reasonably try to write one back through the same name. It has to be told no,
    /// and told where to go — silently creating a custom property called "number" alongside the
    /// real one would leave two things named the same and only one of them meaningful.
    /// </remarks>
    private static readonly Dictionary<string, string> BuiltInSheetFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "rename_sheet",
            ["number"] = "set_sheet_number",
            ["title"] = "set_sheet_title",
            ["description"] = "rename_sheet (it carries the sheet's description)",
        };

    /// <summary>Write one custom property into a bag, keeping the flags an existing one already had.</summary>
    /// <remarks>
    /// `SetProperty` takes an <c>AcSmCustomPropertyValue</c>, which is instantiable — it carries a
    /// CoClass attribute. A NEW property needs its flags set explicitly, because `PropertyFlags`
    /// is what tells AutoCAD whether the property belongs to the sheet set or to each sheet; a
    /// value written with `EMPTY` flags is not shown as either. An EXISTING one keeps whatever it
    /// had, so updating a value never silently re-scopes it.
    /// </remarks>
    private static string SetCustomProperty(
        IAcSmCustomPropertyBag bag, string name, string value, PropertyFlags flagsIfNew)
    {
        string before = "";
        AcSmCustomPropertyValue? existing = null;
        try { existing = bag.GetProperty(name); } catch (COMException) { existing = null; }

        var flags = flagsIfNew;
        if (existing is not null)
        {
            try
            {
                before = existing.GetValue()?.ToString() ?? "";
                flags = existing.GetFlags();
            }
            catch (COMException) { }
            finally { try { Marshal.ReleaseComObject(existing); } catch (Exception) { } }
        }

        var pv = new AcSmCustomPropertyValue();
        try
        {
            // InitNew FIRST, with the bag as owner. A freshly co-created AcSmCustomPropertyValue
            // is not yet attached to a database, and SetValue on it throws a bare
            // NullReferenceException from inside the interop layer - no HRESULT, no hint that
            // initialisation is what is missing. Every IAcSmPersist carries InitNew for exactly
            // this reason; the constructor is only half of creating one.
            pv.InitNew((IAcSmPersist)bag);
            pv.SetValue(value);
            pv.SetFlags(flags);
            bag.SetProperty(name, pv);
        }
        finally { try { Marshal.ReleaseComObject(pv); } catch (Exception) { } }
        return before;
    }

    private static string ReadCustomProperty(IAcSmCustomPropertyBag bag, string name)
    {
        AcSmCustomPropertyValue? v = null;
        try { v = bag.GetProperty(name); } catch (COMException) { return ""; }
        try { return v?.GetValue()?.ToString() ?? ""; }
        catch (COMException) { return ""; }
        finally { if (v is not null) { try { Marshal.ReleaseComObject(v); } catch (Exception) { } } }
    }

    private static Task<ToolDispatchResult> SetSheetProperty(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.set_sheet_property", ct, (doc, db, tr) =>
        {
            var a = Read<SetSheetPropertyArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Property))
                throw new ArgumentException("property is required: the custom property's name.");
            if (a.Value is null)
                throw new ArgumentException("value is required.");
            if (BuiltInSheetFields.TryGetValue(a.Property!, out var instead))
                throw new ArgumentException(
                    "'" + a.Property + "' is a built-in sheet field, not a custom property. " +
                    "Use " + instead + ". Writing it here would create a second, meaningless " +
                    "property sharing the name.");

            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var bag = BagOf(sheet)
                        ?? throw new InvalidOperationException(
                            "Sheet '" + sheet.GetName() + "' exposes no custom property bag.");

                    // A sheet's own properties are CUSTOM_SHEET_PROP. Defining one that does not
                    // exist yet is allowed here rather than refused: the SSM lets a sheet carry a
                    // property the set has not declared, and refusing would make this tool unable
                    // to do the thing its name says.
                    var before = SetCustomProperty(bag, a.Property!, a.Value!,
                                                   PropertyFlags.CUSTOM_SHEET_PROP);
                    return Wrap(new
                    {
                        path = full,
                        sheet = sheet.GetName(),
                        property = a.Property,
                        before,
                        value = ReadCustomProperty(bag, a.Property!),
                        created = before.Length == 0,
                    });
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });

    private static Task<ToolDispatchResult> DefineCustomProperty(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.define_custom_property", ct, (doc, db, tr) =>
        {
            var a = Read<DefinePropertyArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: the custom property's name.");

            var scope = (a.Scope ?? "sheetSet").Trim();
            var flags = scope.Equals("sheet", StringComparison.OrdinalIgnoreCase)
                ? PropertyFlags.CUSTOM_SHEET_PROP
                : scope.Equals("sheetSet", StringComparison.OrdinalIgnoreCase)
                    ? PropertyFlags.CUSTOM_SHEETSET_PROP
                    : throw new ArgumentException(
                        "scope must be 'sheetSet' or 'sheet', not '" + scope + "'. 'sheetSet' is " +
                        "one project-wide value; 'sheet' gives every sheet its own.");

            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var bag = BagOf(ss)
                    ?? throw new InvalidOperationException(
                        "'" + full + "' exposes no sheet-set custom property bag.");

                var before = SetCustomProperty(bag, a.Name!, a.DefaultValue ?? "", flags);
                return Wrap(new
                {
                    path = full,
                    name = a.Name,
                    scope,
                    defaultValue = ReadCustomProperty(bag, a.Name!),
                    before,
                    created = before.Length == 0,
                    note = scope.Equals("sheet", StringComparison.OrdinalIgnoreCase)
                        ? "Scope 'sheet' means every sheet carries its own value; set them with " +
                          "set_sheet_property. The value given here is the default."
                        : "Scope 'sheetSet' means one project-wide value, which every title block " +
                          "bound to this property reads.",
                });
            });
        });

    // ─────────── subsets ───────────

    private static Task<ToolDispatchResult> CreateSubset(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.create_subset", ct, (doc, db, tr) =>
        {
            var a = Read<SubsetCreateArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: the new subset's name.");

            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                // A subset nests inside the sheet set OR inside another subset, and both expose
                // CreateSubset because IAcSmSheetSet carries the whole IAcSmSubset surface.
                var parent = RequireSubsetOrRoot(ss, a.Parent, full);
                var parentIsRoot = ReferenceEquals(parent, ss);
                try
                {
                    if (FindSubset(ss, a.Name!) is { } clash)
                    {
                        try
                        {
                            throw new ArgumentException(
                                "'" + full + "' already has a subset named '" + a.Name + "'. " +
                                "Subset names are how move_sheet_to_subset addresses them, so a " +
                                "duplicate would make that ambiguous.");
                        }
                        finally { try { Marshal.ReleaseComObject(clash); } catch (Exception) { } }
                    }

                    var created = parent.CreateSubset(a.Name, a.Description ?? "");
                    try
                    {
                        var parentName = parentIsRoot ? ss.GetName() : parent.GetName();
                        return Wrap(new
                        {
                            path = full,
                            name = created.GetName(),
                            description = created.GetDesc(),
                            parent = parentName,
                            parentIsSheetSet = parentIsRoot,
                            sheetCount = 0,
                        });
                    }
                    finally { try { Marshal.ReleaseComObject(created); } catch (Exception) { } }
                }
                finally { if (!parentIsRoot) { try { Marshal.ReleaseComObject(parent); } catch (Exception) { } } }
            });
        });

    /// <summary>Delete an EMPTY subset. Non-empty is refused rather than guessed at.</summary>
    /// <remarks>
    /// `RemoveSubset` is not documented as to what becomes of the sheets inside — orphaned,
    /// deleted, or moved to the parent — and this bank has been burned twice by guessing at
    /// undocumented behaviour (`IdPair.IsCloned`, `CompareLayerStateToDb`), both times producing a
    /// tool that reported the opposite of the truth. Guessing wrong here would destroy a user's
    /// sheets. So: refuse, say how many sheets are in the way, and name the tool that moves them.
    /// </remarks>
    private static Task<ToolDispatchResult> DeleteSubset(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.delete_subset", ct, (doc, db, tr) =>
        {
            var a = Read<SubsetArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Subset))
                throw new ArgumentException("subset is required: its name or its full path.");

            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var target = FindSubset(ss, a.Subset!)
                    ?? throw new ArgumentException(
                        "No subset named '" + a.Subset + "' in '" + full + "'. Use list_subsets.");
                try
                {
                    var name = target.GetName();
                    var held = CountSheets(target);
                    if (held > 0)
                        throw new ArgumentException(
                            "Subset '" + name + "' still holds " + held + " sheet(s) and was not " +
                            "deleted. What RemoveSubset does with the sheets inside is not " +
                            "documented and has not been measured, and the possibilities include " +
                            "deleting them. Move them out with move_sheet_to_subset first.");

                    // RemoveSubset lives on the OWNER, and it wants the coclass rather than the
                    // interface - the same trap as Close(AcSmDatabase) in rule 45 §10.
                    var owner = target.GetOwner() as IAcSmSubset
                        ?? throw new InvalidOperationException(
                            "Subset '" + name + "' reports no owning subset or sheet set, so " +
                            "there is nothing to remove it from.");
                    try
                    {
                        owner.RemoveSubset((AcSmSubset)target);
                        return Wrap(new
                        {
                            path = full,
                            deleted = name,
                            removedFrom = owner.GetName(),
                            note = "Only empty subsets are deleted. Sheets are never removed by " +
                                   "this tool.",
                        });
                    }
                    finally { try { Marshal.ReleaseComObject(owner); } catch (Exception) { } }
                }
                finally { try { Marshal.ReleaseComObject(target); } catch (Exception) { } }
            });
        });

    private static Task<ToolDispatchResult> MoveSheetToSubset(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.sheetsets.move_sheet_to_subset", ct, (doc, db, tr) =>
        {
            var a = Read<MoveSheetArgsDto>(args);
            return WithSheetSetWrite(a.Path, (ss, full) =>
            {
                var sheet = RequireSheet(ss, a.Sheet, full);
                try
                {
                    var target = RequireSubsetOrRoot(ss, a.Subset, full);
                    var toRoot = ReferenceEquals(target, ss);
                    try
                    {
                        var owner = sheet.GetOwner() as IAcSmSubset;
                        var from = owner?.GetName() ?? "";
                        var to = toRoot ? ss.GetName() : target.GetName();
                        var number = sheet.GetNumber();

                        // Compared by NAME, not by reference: `sheet.GetOwner()` and `target` are
                        // separate runtime-callable wrappers even when they wrap the same COM
                        // object, so ReferenceEquals answers false for a sheet already in place.
                        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                        {
                            if (owner is not null) { try { Marshal.ReleaseComObject(owner); } catch (Exception) { } }
                            throw new ArgumentException(
                                "Sheet '" + sheet.GetName() + "' is already in '" + to + "'. " +
                                "Moving it there again would ask AutoCAD to insert a component it " +
                                "already owns, which fails as a duplicate identifier.");
                        }

                        // InsertComponent ADDS; it does not re-parent. On its own it fails
                        // E_INVALIDARG, and for a sheet already under the target it answers
                        // 0x800288C6 "duplicate identifier" — which is what identified the cause.
                        // A move is therefore: detach from the current owner, then insert.
                        try { owner?.RemoveSheet((AcSmSheet)sheet); }
                        finally { if (owner is not null) { try { Marshal.ReleaseComObject(owner); } catch (Exception) { } } }

                        target.InsertComponent((IAcSmComponent)sheet, null);

                        // Checked INSIDE the lock and BEFORE the commit, because RemoveSheet is not
                        // documented as to whether it detaches a sheet or destroys it. If it
                        // destroyed it, this throws, the finally unlocks with bCommit:false, and the
                        // .DST on disk is untouched. That safety net exists only because the commit
                        // sits on the success path rather than in the finally.
                        var found = FindSheet(ss, number);
                        if (found is null)
                            throw new InvalidOperationException(
                                "Sheet '" + number + "' could not be found after the move, so the " +
                                "move was abandoned and nothing was saved. RemoveSheet appears to " +
                                "destroy a sheet rather than detach it on this AutoCAD build.");
                        try { Marshal.ReleaseComObject(found); } catch (Exception) { }
                        return Wrap(new
                        {
                            path = full,
                            sheet = sheet.GetName(),
                            number = sheet.GetNumber(),
                            from,
                            to,
                            movedToSheetSetRoot = toRoot,
                            note = "The sheet is re-parented, not copied - the set's total sheet " +
                                   "count is unchanged. Omit subset to move it back to the top level.",
                        });
                    }
                    finally { if (!toRoot) { try { Marshal.ReleaseComObject(target); } catch (Exception) { } } }
                }
                finally { try { Marshal.ReleaseComObject(sheet); } catch (Exception) { } }
            });
        });
}
