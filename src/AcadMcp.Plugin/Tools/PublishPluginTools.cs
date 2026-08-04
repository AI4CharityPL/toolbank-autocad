// AutoCAD plugin handlers for the acad-publish category.
//
// Named page setups, per docs/engineering-rules/44-page-setups.md. A page setup is a NAMED
// PlotSettings in db.PlotSettingsDictionaryId - not the same thing as a layout's own plot
// configuration, which layouts.configure_plot already handles and which stays as it is.
//
// The hard-won bits of plot configuration live in FilesPluginTools.export_file and are not
// repeated here: bind the device BEFORE asking what media it offers, SetPlotWindowArea before
// SetPlotType(Window), and set plot rotation explicitly because it is inherited otherwise.
// This file needs the first of those; the other two are plot-time concerns.

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

namespace AcadMcp.Plugin.Tools;

internal static class PublishPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.publish.create_page_setup", CreatePageSetup);
        host.Register("acad.publish.list_page_setups", ListPageSetups);
        host.Register("acad.publish.apply_page_setup", ApplyPageSetup);
        host.Register("acad.publish.delete_page_setup", DeletePageSetup);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();
    private static Task<ToolDispatchResult> Run(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);

    // ─────────── helpers ───────────

    private static DBDictionary SetupsDict(Database db, Transaction tr, OpenMode mode)
        => (DBDictionary)tr.GetObject(db.PlotSettingsDictionaryId, mode);

    private static List<string> SetupNames(Database db, Transaction tr)
    {
        var names = new List<string>();
        foreach (DBDictionaryEntry e in SetupsDict(db, tr, OpenMode.ForRead)) names.Add(e.Key);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static string ResolveSetupName(Database db, Transaction tr, string name)
    {
        var dict = SetupsDict(db, tr, OpenMode.ForRead);
        foreach (DBDictionaryEntry e in dict)
            if (string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase)) return e.Key;

        var known = SetupNames(db, tr);
        throw new ArgumentException(
            $"No page setup named '{name}' in this drawing. Defined: " +
            (known.Count == 0 ? "(none)" : string.Join(", ", known)) +
            ". Use list_page_setups, or create_page_setup to define one.");
    }

    private static object Info(PlotSettings ps) => new
    {
        name = ps.PlotSettingsName,
        device = ps.PlotConfigurationName,
        media = ps.CanonicalMediaName,
        plotStyleTable = ps.CurrentStyleSheet,
        plotType = ps.PlotType.ToString(),
        rotation = ps.PlotRotation.ToString(),
        centered = ps.PlotCentered,
        useStandardScale = ps.UseStandardScale,
        stdScaleType = ps.StdScaleType.ToString(),
        plotPaperUnits = ps.PlotPaperUnits.ToString(),
        modelType = ps.ModelType,   // true = a model-space setup, false = a layout setup
    };

    /// <summary>
    /// Bind the device, then the media. Order matters: GetCanonicalMediaNameList throws
    /// eInvalidInput on settings with no device attached, which is the failure that made
    /// export_file's window plotting look like a windowing bug when it was a media-picker bug.
    /// </summary>
    private static void ApplyDeviceAndMedia(PlotSettingsValidator psv, PlotSettings ps,
                                            string? device, string? media)
    {
        if (!string.IsNullOrWhiteSpace(device))
        {
            var devices = psv.GetPlotDeviceList();
            var match = devices.Cast<string>()
                .FirstOrDefault(d => string.Equals(d, device, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                throw new ArgumentException(
                    $"AutoCAD has no plot device '{device}'. Installed: " +
                    string.Join(", ", devices.Cast<string>()) + ".");
            psv.SetPlotConfigurationName(ps, match, null);
        }

        if (!string.IsNullOrWhiteSpace(media))
        {
            var medias = psv.GetCanonicalMediaNameList(ps);
            var match = medias.Cast<string>()
                .FirstOrDefault(m => string.Equals(m, media, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                throw new ArgumentException(
                    $"Device '{ps.PlotConfigurationName}' does not offer media '{media}'. Available: " +
                    string.Join(", ", medias.Cast<string>().Take(40)) +
                    (medias.Count > 40 ? ", ..." : "") + ".");
            psv.SetCanonicalMediaName(ps, match);
        }
    }

    // ─────────── handlers ───────────

    private static Task<ToolDispatchResult> CreatePageSetup(JsonObject args, CancellationToken ct) =>
        Run("acad.publish.create_page_setup", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreatePageSetupArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            AcadEnv.ValidateSymbolName(a.Name, "PageSetup");

            bool fromLayout = !string.IsNullOrWhiteSpace(a.FromLayout);
            bool explicitCfg = !string.IsNullOrWhiteSpace(a.Device) || !string.IsNullOrWhiteSpace(a.Media)
                               || !string.IsNullOrWhiteSpace(a.PlotStyleTable) || a.Rotation is not null;
            if (fromLayout && explicitCfg)
                throw new ArgumentException(
                    "Pass either fromLayout or the explicit device/media/plotStyleTable/rotation arguments, " +
                    "not both. Rule 44: a precedence rule here would be a rule nobody remembers.");

            var dict = SetupsDict(db, tr, OpenMode.ForWrite);
            foreach (DBDictionaryEntry e in dict)
            {
                if (!string.Equals(e.Key, a.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!a.Overwrite)
                    throw new ArgumentException(
                        $"A page setup named '{e.Key}' already exists. Pass overwrite:true to replace it - " +
                        "a firm's standard page setups are not something to redefine by accident.");
                dict.Remove(e.Key);
                break;
            }

            var ps = new PlotSettings(modelType: false);
            var psv = PlotSettingsValidator.Current;

            if (fromLayout)
            {
                var lm = LayoutManager.Current;
                if (!lm.LayoutExists(a.FromLayout))
                    throw new ArgumentException($"Layout '{a.FromLayout}' does not exist.");
                var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.FromLayout), OpenMode.ForRead);
                // CopyFrom takes everything the layout carries, which is the point: this is a
                // snapshot of a configuration someone already got right by hand.
                ps.CopyFrom(layout);
            }
            else
            {
                ApplyDeviceAndMedia(psv, ps, a.Device, a.Media);
                if (!string.IsNullOrWhiteSpace(a.PlotStyleTable))
                    psv.SetCurrentStyleSheet(ps, a.PlotStyleTable);
                if (a.Rotation is int rot)
                {
                    psv.SetPlotRotation(ps, rot switch
                    {
                        0 => PlotRotation.Degrees000,
                        90 => PlotRotation.Degrees090,
                        180 => PlotRotation.Degrees180,
                        270 => PlotRotation.Degrees270,
                        _ => throw new ArgumentException($"rotation must be 0, 90, 180 or 270; got {rot}."),
                    });
                }
                psv.SetPlotCentered(ps, true);
                psv.SetUseStandardScale(ps, true);
                psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
            }

            ps.PlotSettingsName = a.Name;
            ps.AddToPlotSettingsDictionary(db);
            tr.AddNewlyCreatedDBObject(ps, true);

            return Wrap(new { pageSetup = Info(ps) });
        });

    private static Task<ToolDispatchResult> ListPageSetups(JsonObject args, CancellationToken ct) =>
        Run("acad.publish.list_page_setups", args, ct, (doc, db, tr) =>
        {
            var dict = SetupsDict(db, tr, OpenMode.ForRead);
            var list = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                var ps = (PlotSettings)tr.GetObject(e.Value, OpenMode.ForRead);
                list.Add(Info(ps));
            }
            return Wrap(new { pageSetups = list, count = list.Count });
        });

    private static Task<ToolDispatchResult> ApplyPageSetup(JsonObject args, CancellationToken ct) =>
        Run("acad.publish.apply_page_setup", args, ct, (doc, db, tr) =>
        {
            var a = Read<ApplyPageSetupArgsDto>(args);
            bool named = a.Layouts is { Count: > 0 };
            if (named == a.AllLayouts)
                throw new ArgumentException(
                    "Pass either layouts:[...] or allLayouts:true, and not both. Rule 44: there is no " +
                    "'all layouts' default, because applying a page setup to every tab because an " +
                    "argument was omitted is exactly the accident this bank exists to avoid.");

            var key = ResolveSetupName(db, tr, a.Name);
            var dict = SetupsDict(db, tr, OpenMode.ForRead);
            var src = (PlotSettings)tr.GetObject(dict.GetAt(key), OpenMode.ForRead);

            var lm = LayoutManager.Current;
            var targets = new List<string>();
            if (named)
            {
                targets.AddRange(a.Layouts!);
            }
            else
            {
                var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in layoutDict)
                {
                    var lay = (Layout)tr.GetObject(e.Value, OpenMode.ForRead);
                    if (!string.Equals(lay.LayoutName, "Model", StringComparison.OrdinalIgnoreCase))
                        targets.Add(lay.LayoutName);
                }
            }

            // Report per layout rather than an affected count. A page setup naming a device this
            // machine does not have will fail on some layouts and not others, and a partial
            // success has to be visible as one.
            var results = new List<object>();
            int applied = 0;
            foreach (var name in targets)
            {
                try
                {
                    if (!lm.LayoutExists(name))
                    {
                        results.Add(new { layout = name, status = "failed", error = "layout does not exist" });
                        continue;
                    }
                    var layout = (Layout)tr.GetObject(lm.GetLayoutId(name), OpenMode.ForWrite);
                    layout.CopyFrom(src);
                    applied++;
                    results.Add(new { layout = name, status = "applied", error = (string?)null });
                }
                catch (Exception ex)
                {
                    results.Add(new { layout = name, status = "failed", error = ex.Message });
                }
            }

            return Wrap(new { pageSetupName = key, applied, results });
        });

    private static Task<ToolDispatchResult> DeletePageSetup(JsonObject args, CancellationToken ct) =>
        Run("acad.publish.delete_page_setup", args, ct, (doc, db, tr) =>
        {
            var a = Read<PageSetupNameArgsDto>(args);
            var key = ResolveSetupName(db, tr, a.Name);
            var dict = SetupsDict(db, tr, OpenMode.ForWrite);
            dict.Remove(key);
            // Layouts that were configured from it keep their settings: CopyFrom took a copy,
            // it did not create a reference. Said in the result so nobody expects sheets to
            // revert.
            return Wrap(new
            {
                affected = 1,
                name = key,
                note = "Layouts previously configured from this page setup keep their settings - " +
                       "apply_page_setup copies, it does not link.",
            });
        });

    // import_page_setup is WITHHELD. It was written, it ran, and it did not work: the tool
    // reported success and the page setup was simply absent from the target drawing afterwards.
    //
    // The first version called WblockCloneObjects on the DESTINATION with source ids, which is
    // the wrong way round - it is called on the database that owns the objects, with the owner
    // id in the destination. Correcting that direction did not fix it either: the clone still
    // reports no error and nothing lands in PlotSettingsDictionaryId.
    //
    // What it did produce was a post-condition check worth keeping in mind - the tool now
    // verifies the target dictionary instead of echoing back the name it was given, which is
    // how the failure became visible at all. Shipping this before that check would have shipped
    // a tool whose entire output was a restatement of its own argument.
    //
    // Withheld rather than guessed at further. See docs/KNOWN-GAPS.md section B.

}
