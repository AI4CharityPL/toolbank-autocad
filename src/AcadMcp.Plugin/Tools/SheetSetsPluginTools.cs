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
        host.Register("acad.sheetsets.set_sheet_number", SetSheetNumber);
        host.Register("acad.sheetsets.rename_sheet", RenameSheet);
        host.Register("acad.sheetsets.set_sheet_title", SetSheetTitle);
        host.Register("acad.sheetsets.set_sheet_do_not_plot", SetSheetDoNotPlot);
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
}
