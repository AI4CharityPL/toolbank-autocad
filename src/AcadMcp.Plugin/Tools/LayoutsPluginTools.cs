// AutoCAD plugin handlers for the acad-layouts category.
// Registered under "acad.layouts.<verb>"; everything runs on the UI thread.
//
// Rules: 10, 11, 12, 19, 28-acad-blocks-layers-files-traps.mdc.

using System;
using System.Collections.Generic;
using System.IO;
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

internal static class LayoutsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.layouts.list_layouts",        ListLayouts);
        host.Register("acad.layouts.create_layout",       CreateLayout);
        host.Register("acad.layouts.set_current_layout",  SetCurrentLayout);
        host.Register("acad.layouts.delete_layout",       DeleteLayout);
        host.Register("acad.layouts.rename_layout",       RenameLayout);
        host.Register("acad.layouts.create_viewport",     CreateViewport);
        host.Register("acad.layouts.set_viewport_scale",  SetViewportScale);
        host.Register("acad.layouts.configure_plot",      ConfigurePlot);
        host.Register("acad.layouts.get_layout",          GetLayout);
        host.Register("acad.layouts.list_plot_styles",    ListPlotStyles);
        host.Register("acad.layouts.list_paper_sizes",    ListPaperSizes);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Task<ToolDispatchResult> RunRead(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunReadAsync(toolKey, ct, work);

    // Normalise a free-form paper-size label for fuzzy comparison.
    // Lowercase, strip non-alphanumeric characters, collapse whitespace.
    private static string NormalisePaper(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    // Resolve a user-supplied paper size to the exact canonical media name accepted
    // by PlotSettingsValidator.SetPlotConfigurationName. Handles three cases:
    //   1) exact canonical hit (what AutoCAD wants)
    //   2) exact locale name ("ISO A0 (841.00 x 1189.00 MM)" vs
    //      "ISO_full_bleed_A0_(1189.00_x_841.00_MM)")
    //   3) fuzzy hit: user writes "A0" or "ISO A0" or "a0 iso" — match against
    //      canonical + locale normalised (alnum only, lower-cased).
    // Returns null when no match.
    private static string? ResolvePaper(PlotSettingsValidator psv, PlotSettings ps, string requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return null;
        var canonicals = psv.GetCanonicalMediaNameList(ps);
        if (canonicals is null || canonicals.Count == 0) return null;

        var exact = NormalisePaper(requested);
        string? fuzzy = null;
        string? containing = null;

        foreach (var item in canonicals)
        {
            var c = item as string;
            if (string.IsNullOrEmpty(c)) continue;
            if (string.Equals(c, requested, StringComparison.OrdinalIgnoreCase)) return c;

            string locale = string.Empty;
            try { locale = psv.GetLocaleMediaName(ps, c) ?? string.Empty; } catch { }
            if (!string.IsNullOrEmpty(locale)
                && string.Equals(locale, requested, StringComparison.OrdinalIgnoreCase))
                return c;

            var cn = NormalisePaper(c);
            var ln = NormalisePaper(locale);
            if (cn == exact || ln == exact) { fuzzy ??= c; }
            else if (cn.Contains(exact) || ln.Contains(exact)) { containing ??= c; }
        }

        return fuzzy ?? containing;
    }

    // ─────────── helpers ───────────

    private static LayoutInfoDto BuildLayoutInfo(Database db, Transaction tr, Layout layout, string currentName)
    {
        string? plotter = null, paper = null;
        try { plotter = layout.PlotConfigurationName; } catch { }
        try { paper   = layout.CanonicalMediaName;    } catch { }
        return new LayoutInfoDto(
            Name: layout.LayoutName,
            TabOrder: layout.TabOrder,
            IsCurrent: string.Equals(layout.LayoutName, currentName, StringComparison.OrdinalIgnoreCase),
            Plotter: string.IsNullOrEmpty(plotter) ? null : plotter,
            PaperSize: string.IsNullOrEmpty(paper) ? null : paper);
    }

    private static string GetCurrentLayoutName(Database db, Transaction tr)
    {
        try
        {
            var lm = LayoutManager.Current;
            return lm.CurrentLayout ?? "Model";
        }
        catch { return "Model"; }
    }

    // ─────────── list / get ───────────

    private static Task<ToolDispatchResult> ListLayouts(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.list_layouts", args, ct, (doc, db, tr) =>
        {
            var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            var current = GetCurrentLayoutName(db, tr);
            var list = new List<LayoutInfoDto>();
            foreach (DBDictionaryEntry e in dict)
            {
                var layout = (Layout)tr.GetObject(e.Value, OpenMode.ForRead);
                list.Add(BuildLayoutInfo(db, tr, layout, current));
            }
            list.Sort((a, b) => a.TabOrder.CompareTo(b.TabOrder));
            return Wrap(new { layouts = list, current });
        });

    private static Task<ToolDispatchResult> GetLayout(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.get_layout", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayoutNameArgDto>(args);
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.Name)) throw new ArgumentException($"Layout '{a.Name}' does not exist.");
            var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.Name), OpenMode.ForRead);
            return Wrap(new { layout = BuildLayoutInfo(db, tr, layout, GetCurrentLayoutName(db, tr)) });
        });

    // ─────────── lifecycle ───────────

    private static Task<ToolDispatchResult> CreateLayout(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.create_layout", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateLayoutArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.Name, "Layout");
            var lm = LayoutManager.Current;
            if (lm.LayoutExists(a.Name))
                throw new InvalidOperationException($"Layout '{a.Name}' already exists.");
            var id = lm.CreateLayout(a.Name);
            if (a.SetCurrent) lm.CurrentLayout = a.Name;
            var layout = (Layout)tr.GetObject(id, OpenMode.ForRead);
            return Wrap(new { layout = BuildLayoutInfo(db, tr, layout, GetCurrentLayoutName(db, tr)) });
        });

    private static Task<ToolDispatchResult> SetCurrentLayout(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.set_current_layout", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayoutNameArgDto>(args);
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.Name)) throw new ArgumentException($"Layout '{a.Name}' does not exist.");
            lm.CurrentLayout = a.Name;
            var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.Name), OpenMode.ForRead);
            return Wrap(new { layout = BuildLayoutInfo(db, tr, layout, a.Name) });
        });

    private static Task<ToolDispatchResult> DeleteLayout(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.delete_layout", args, ct, (doc, db, tr) =>
        {
            var a = Read<LayoutNameArgDto>(args);
            if (string.Equals(a.Name, "Model", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Model tab cannot be deleted.");
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.Name)) return Wrap(new { affected = 0 });
            int paperCount = 0;
            foreach (DBDictionaryEntry _ in (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead))
                paperCount++;
            // Subtract 1 for Model.
            if (paperCount - 1 <= 1)
                throw new InvalidOperationException("Cannot delete the last paper-space layout.");
            lm.DeleteLayout(a.Name);
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> RenameLayout(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.rename_layout", args, ct, (doc, db, tr) =>
        {
            var a = Read<RenameLayoutArgsDto>(args);
            if (string.Equals(a.OldName, "Model", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Model tab cannot be renamed.");
            AcadEnv.ValidateSymbolName(a.NewName, "Layout");
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.OldName)) throw new ArgumentException($"Layout '{a.OldName}' does not exist.");
            if (lm.LayoutExists(a.NewName)) throw new InvalidOperationException($"Layout '{a.NewName}' already exists.");
            lm.RenameLayout(a.OldName, a.NewName);
            var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.NewName), OpenMode.ForRead);
            return Wrap(new { layout = BuildLayoutInfo(db, tr, layout, GetCurrentLayoutName(db, tr)) });
        });

    // ─────────── viewports ───────────

    private static Task<ToolDispatchResult> CreateViewport(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.create_viewport", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateViewportArgsDto>(args);
            if (a.Width <= 0 || a.Height <= 0) throw new ArgumentException("width and height must be > 0.");
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.LayoutName)) throw new ArgumentException($"Layout '{a.LayoutName}' does not exist.");

            var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.LayoutName), OpenMode.ForRead);
            var paper  = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

            var vp = new Viewport
            {
                CenterPoint = AcadEnv.ToPoint3d(a.Center),
                Width       = a.Width,
                Height      = a.Height,
            };
            if (!string.IsNullOrWhiteSpace(a.Layer))
                vp.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer);
            if (a.Scale > 0)
            {
                vp.CustomScale = a.Scale;
            }
            paper.AppendEntity(vp);
            tr.AddNewlyCreatedDBObject(vp, true);
            vp.On = true;

            return Wrap(new { entity = AcadEnv.ToHandle(vp) });
        });

    private static Task<ToolDispatchResult> SetViewportScale(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.set_viewport_scale", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetViewportScaleArgsDto>(args);
            if (a.Scale <= 0) throw new ArgumentException("scale must be > 0.");
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (ent is not Viewport vp)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not Viewport.");
            vp.CustomScale = a.Scale;
            return Wrap(new { entity = AcadEnv.ToHandle(vp) });
        });

    // ─────────── plot ───────────

    private static Task<ToolDispatchResult> ConfigurePlot(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.configure_plot", args, ct, (doc, db, tr) =>
        {
            var a = Read<ConfigurePlotArgsDto>(args);
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(a.LayoutName)) throw new ArgumentException($"Layout '{a.LayoutName}' does not exist.");
            var layout = (Layout)tr.GetObject(lm.GetLayoutId(a.LayoutName), OpenMode.ForWrite);

            // PlotSettingsValidator lives in DatabaseServices, not PlottingServices, in the modern SDK.
            // Use the singleton .Current — its constructor is private on this vertical.
            var psv = PlotSettingsValidator.Current;
            var ps = new PlotSettings(layout.ModelType);
            ps.CopyFrom(layout);

            // Plotter (device) selection — set first because media list depends on it.
            // Fall back to the layout's current plotter when only paperSize is supplied.
            if (!string.IsNullOrWhiteSpace(a.Plotter))
            {
                // Attach device with a harmless default media so subsequent paper-size
                // resolution runs against the new device's media list.
                try { psv.SetPlotConfigurationName(ps, a.Plotter, null); }
                catch (Exception ex)
                {
                    throw new ArgumentException(
                        $"Plotter '{a.Plotter}' is not registered on this AutoCAD install: {ex.Message}");
                }
            }

            // Paper size — tolerant resolution (canonical | locale | fuzzy "A0").
            if (!string.IsNullOrWhiteSpace(a.PaperSize))
            {
                var resolved = ResolvePaper(psv, ps, a.PaperSize!);
                if (resolved is null)
                {
                    var samples = string.Empty;
                    try
                    {
                        var lst = psv.GetCanonicalMediaNameList(ps);
                        var top = new List<string>();
                        if (lst is not null)
                            foreach (var it in lst)
                            {
                                if (it is string s && !string.IsNullOrEmpty(s)) top.Add(s);
                                if (top.Count >= 6) break;
                            }
                        if (top.Count > 0) samples = " Examples: " + string.Join(", ", top) + ".";
                    }
                    catch { }
                    throw new ArgumentException(
                        $"Paper size '{a.PaperSize}' not recognised for plotter " +
                        $"'{(layout.PlotConfigurationName ?? "<default>")}'." + samples +
                        " Call acad.layouts.list_paper_sizes to see the full list.");
                }

                // Re-attach config with the resolved canonical so PlotSettingsValidator
                // treats plotter + media as one atomic unit (required by the SDK).
                var device = ps.PlotConfigurationName;
                if (string.IsNullOrWhiteSpace(device)) device = layout.PlotConfigurationName;
                psv.SetPlotConfigurationName(ps, device, resolved);
            }

            if (!string.IsNullOrWhiteSpace(a.PlotStyle))
            {
                psv.SetCurrentStyleSheet(ps, a.PlotStyle);
            }
            // Rotation: 0/90/180/270.
            var rot = a.Rotation switch
            {
                0   => PlotRotation.Degrees000,
                90  => PlotRotation.Degrees090,
                180 => PlotRotation.Degrees180,
                270 => PlotRotation.Degrees270,
                _   => throw new ArgumentException("rotation must be 0, 90, 180 or 270."),
            };
            psv.SetPlotRotation(ps, rot);

            layout.CopyFrom(ps);
            return Wrap(new { layout = BuildLayoutInfo(db, tr, layout, GetCurrentLayoutName(db, tr)) });
        });

    // ─────────── paper size enumeration ───────────

    private static Task<ToolDispatchResult> ListPaperSizes(JsonObject args, CancellationToken ct) =>
        RunRead("acad.layouts.list_paper_sizes", args, ct, (doc, db, tr) =>
        {
            var a = Read<ListPaperSizesArgsDto>(args);
            var psv = PlotSettingsValidator.Current;

            // Use the current layout as the baseline for a throwaway PlotSettings.
            var currentLayoutName = GetCurrentLayoutName(db, tr);
            var lm = LayoutManager.Current;
            Layout? curLayout = null;
            try
            {
                if (lm.LayoutExists(currentLayoutName))
                    curLayout = (Layout)tr.GetObject(lm.GetLayoutId(currentLayoutName), OpenMode.ForRead);
            }
            catch { }

            var ps = new PlotSettings(curLayout?.ModelType ?? false);
            if (curLayout is not null) { try { ps.CopyFrom(curLayout); } catch { } }

            // Enumerate devices.
            var plotters = new List<string>();
            try
            {
                var devs = psv.GetPlotDeviceList();
                if (devs is not null)
                    foreach (var item in devs)
                        if (item is string s && !string.IsNullOrWhiteSpace(s))
                            plotters.Add(s);
            }
            catch { }

            // Pick plotter: explicit -> current layout's plotter -> first available.
            string? plotter = a.Plotter;
            if (string.IsNullOrWhiteSpace(plotter))
                plotter = curLayout?.PlotConfigurationName;
            if (string.IsNullOrWhiteSpace(plotter) && plotters.Count > 0)
                plotter = plotters[0];

            if (!string.IsNullOrWhiteSpace(plotter))
            {
                try { psv.SetPlotConfigurationName(ps, plotter, null); }
                catch (Exception ex)
                {
                    throw new ArgumentException(
                        $"Plotter '{plotter}' could not be attached: {ex.Message}. " +
                        $"Available plotters: {(plotters.Count == 0 ? "<none>" : string.Join(", ", plotters))}.");
                }
            }

            var sizes = new List<PaperSizeInfoDto>();
            try
            {
                var canonicals = psv.GetCanonicalMediaNameList(ps);
                if (canonicals is not null)
                {
                    foreach (var item in canonicals)
                    {
                        var c = item as string;
                        if (string.IsNullOrWhiteSpace(c)) continue;
                        string? locale = null;
                        try { locale = psv.GetLocaleMediaName(ps, c); } catch { }
                        sizes.Add(new PaperSizeInfoDto(c!, locale));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"GetCanonicalMediaNameList failed for plotter '{plotter}': {ex.Message}");
            }

            return Wrap(new
            {
                plotters,
                plotter = plotter ?? string.Empty,
                sizes,
                currentLayoutPaper = curLayout?.CanonicalMediaName,
                currentLayoutPlotter = curLayout?.PlotConfigurationName,
            });
        });

    // ─────────── plot styles (CTB/STB) ───────────

    private static Task<ToolDispatchResult> ListPlotStyles(JsonObject args, CancellationToken ct) =>
        Run("acad.layouts.list_plot_styles", args, ct, (doc, db, tr) =>
        {
            var psv = PlotSettingsValidator.Current;
            // PlotSettingsValidator.RefreshLists(PlotSettings) — needs a throwaway target.
            try
            {
                var tmp = new PlotSettings(false);
                psv.RefreshLists(tmp);
                tmp.Dispose();
            }
            catch { /* refresh is best-effort — continue with cached list */ }

            var names = new List<string>();
            var ctb = new List<string>();
            var stb = new List<string>();
            try
            {
                var coll = psv.GetPlotStyleSheetList();
                foreach (string? s in coll)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    names.Add(s!);
                    var ext = Path.GetExtension(s);
                    if (string.Equals(ext, ".ctb", StringComparison.OrdinalIgnoreCase)) ctb.Add(s!);
                    else if (string.Equals(ext, ".stb", StringComparison.OrdinalIgnoreCase)) stb.Add(s!);
                }
            }
            catch (System.Exception ex) { throw new ArgumentException("GetPlotStyleSheetList failed: " + ex.Message); }

            // Plot-styles directory — AutoCAD managed API does not expose it; probe both
            // legacy (%APPDATA%\Autodesk\AutoCAD *\R*\<locale>\Plot Styles) and modern
            // (%APPDATA%\Autodesk\AutoCAD *\R*\<locale>\Plotters\Plot Styles) layouts.
            string? directory = null;
            try
            {
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var autodesk = Path.Combine(appdata, "Autodesk");
                if (Directory.Exists(autodesk))
                {
                    foreach (var acadVer in Directory.EnumerateDirectories(autodesk, "AutoCAD*"))
                    {
                        foreach (var relVer in Directory.EnumerateDirectories(acadVer, "R*"))
                        {
                            foreach (var locale in Directory.EnumerateDirectories(relVer))
                            {
                                // Try AutoCAD 2018+ path first (Plotters\Plot Styles)
                                var nested = Path.Combine(locale, "Plotters", "Plot Styles");
                                if (Directory.Exists(nested)) { directory = nested; break; }
                                // Fallback to legacy path (Plot Styles directly under locale)
                                var legacy = Path.Combine(locale, "Plot Styles");
                                if (Directory.Exists(legacy)) { directory = legacy; break; }
                            }
                            if (!string.IsNullOrWhiteSpace(directory)) break;
                        }
                        if (!string.IsNullOrWhiteSpace(directory)) break;
                    }
                }
            }
            catch { /* ignored */ }

            return Wrap(new { names, ctb, stb, directory, count = names.Count });
        });
}
