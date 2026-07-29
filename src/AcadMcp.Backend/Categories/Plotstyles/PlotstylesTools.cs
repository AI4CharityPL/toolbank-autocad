// acad-plotstyles category. 3 composite tools:
//   * ensure_ctb                — copy CTB from asset/source into AutoCAD Plot Styles dir
//   * apply_plotstyle_to_layout — wraps acad.layouts.configure_plot { plotStyle }
//   * list_plotstyles           — wraps acad.layouts.list_plot_styles + repo preset summary
//
// CTB binary authoring is NOT part of AutoCAD's managed SDK, so ensure_ctb
// performs file-copy on the backend side and delegates enumeration + refresh
// to the plugin. Rule 61 (lineweight policy) documents the colour→mm tiers.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Plotstyles;

public static class PlotstylesTools
{
    private const int T_NORMAL = 20_000;

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ─────────────────────────────────────────────────────────────
    // ensure_ctb
    // ─────────────────────────────────────────────────────────────

    [McpTool("ensure_ctb",
        "Ensure a colour-dependent plot-style (CTB) is installed in AutoCAD's Plot Styles directory. Queries acad.layouts.list_plot_styles to resolve the target directory, then copies from sourcePath (caller override) or the repo asset folder <repo>/assets/plotstyles/<name>. If the CTB already exists and overwrite=false (default), reports existedBefore=true, copied=false. Calls list_plot_styles a second time to verify the refresh picked up the new sheet. Use this before apply_plotstyle_to_layout so the target sheet is guaranteed loaded.",
        "plotstyles",
        Intent = new[]
        {
            "zainstaluj CTB",
            "ensure plot style table",
            "skopiuj CTB do autocad",
            "install HOSPITAL-ISO ctb",
            "wczytaj plotstyle do autocad",
        },
        RequiresPlugin = true)]
    public static async Task<EnsureCtbResult> EnsureCtb(
        IPluginGateway gw, EnsureCtbArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Name))
            throw new ArgumentException("name required (e.g. 'HOSPITAL-ISO.ctb').");

        string name = args.Name.Trim();
        // Accept name with or without extension — default to .ctb
        if (!name.EndsWith(".ctb", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith(".stb", StringComparison.OrdinalIgnoreCase))
            name += ".ctb";

        var notes = new List<string>();

        // 1. ask plugin for the plot-styles directory
        var listBefore = await InvokeListPlotStylesAsync(gw, ct).ConfigureAwait(false);
        string? directory = listBefore["directory"]?.GetValue<string>();
        var namesBefore = (listBefore["names"] as JsonArray)?.ToStringList() ?? new List<string>();
        bool existedBefore = namesBefore.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(directory))
        {
            notes.Add("AutoCAD did not disclose a Plot Styles directory — nothing was copied.");
            return new EnsureCtbResult(name, null, null, existedBefore, false, null, existedBefore, notes);
        }

        string targetPath = Path.Combine(directory!, name);

        // 2. resolve source path (override wins; otherwise repo assets/plotstyles/<name>)
        string? sourceResolved = null;
        if (!string.IsNullOrWhiteSpace(args.SourcePath))
        {
            sourceResolved = Path.GetFullPath(args.SourcePath!);
            if (!File.Exists(sourceResolved))
                throw new FileNotFoundException($"sourcePath does not exist: {sourceResolved}");
        }
        else
        {
            var assetCandidate = Path.Combine(PlotstylesPalette.AssetsDirectory(), name);
            if (File.Exists(assetCandidate)) sourceResolved = assetCandidate;
        }

        // 3. copy decision
        bool copied = false;
        if (existedBefore && !args.Overwrite)
        {
            notes.Add($"'{name}' already present in AutoCAD — leaving untouched (set overwrite=true to replace).");
        }
        else if (sourceResolved is null)
        {
            notes.Add($"No sourcePath and no asset at {Path.Combine(PlotstylesPalette.AssetsDirectory(), name)}; drop the CTB there or pass sourcePath.");
            return new EnsureCtbResult(name, directory, targetPath, existedBefore, false, null, existedBefore, notes);
        }
        else
        {
            try
            {
                File.Copy(sourceResolved, targetPath, overwrite: args.Overwrite);
                copied = true;
                notes.Add($"Copied '{sourceResolved}' -> '{targetPath}' (overwrite={args.Overwrite}).");
            }
            catch (Exception ex)
            {
                notes.Add($"Copy failed: {ex.Message}");
                return new EnsureCtbResult(name, directory, targetPath, existedBefore, false, sourceResolved, existedBefore, notes);
            }
        }

        // 4. ask plugin to refresh + re-enumerate
        var listAfter = await InvokeListPlotStylesAsync(gw, ct).ConfigureAwait(false);
        var namesAfter = (listAfter["names"] as JsonArray)?.ToStringList() ?? new List<string>();
        bool listedAfter = namesAfter.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (copied && !listedAfter)
            notes.Add("Copy succeeded but the refresh did not pick the sheet up — AutoCAD may require a session restart.");

        return new EnsureCtbResult(name, directory, targetPath, existedBefore, copied, sourceResolved, listedAfter, notes);
    }

    // ─────────────────────────────────────────────────────────────
    // apply_plotstyle_to_layout
    // ─────────────────────────────────────────────────────────────

    [McpTool("apply_plotstyle_to_layout",
        "Apply a named plot-style (CTB/STB) to a paperspace layout. Optionally runs ensure_ctb first so the sheet is copied into AutoCAD before being assigned (ensure=true, default). Under the hood dispatches acad.layouts.configure_plot { layoutName, plotStyle }.",
        "plotstyles",
        Intent = new[]
        {
            "przypisz CTB do arkusza",
            "apply plot style to layout",
            "ustaw lineweight na layout",
            "set CTB for paperspace",
            "plotstyle na arkuszu roboczym",
        },
        RequiresPlugin = true)]
    public static async Task<ApplyPlotstyleToLayoutResult> ApplyPlotstyleToLayout(
        IPluginGateway gw, ApplyPlotstyleToLayoutArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.LayoutName))
            throw new ArgumentException("layoutName required.");
        if (string.IsNullOrWhiteSpace(args.Plotstyle))
            throw new ArgumentException("plotstyle required (e.g. 'HOSPITAL-ISO.ctb').");

        var notes = new List<string>();
        EnsureCtbResult? ensureResult = null;

        if (args.Ensure)
        {
            ensureResult = await EnsureCtb(gw,
                new EnsureCtbArgs(args.Plotstyle, args.SourcePath, Overwrite: false), ct).ConfigureAwait(false);
            if (!ensureResult.ListedAfter)
                notes.Add("CTB not in AutoCAD's plot-style list — configure_plot may silently no-op.");
        }

        // Dispatch acad.layouts.configure_plot with just the plotStyle arg set.
        var configureArgs = new JsonObject
        {
            ["layoutName"] = args.LayoutName,
            ["plotStyle"]  = args.Plotstyle,
            ["rotation"]   = 0,
        };
        var cfg = await gw.InvokeAsync("acad.layouts.configure_plot", configureArgs, T_NORMAL, ct).ConfigureAwait(false);
        if (cfg is null) throw new InvalidPluginShapeException("configure_plot returned null");

        bool applied = cfg["layout"] is JsonObject;
        if (!applied) notes.Add("configure_plot did not return a layout object — check plugin logs.");

        return new ApplyPlotstyleToLayoutResult(args.LayoutName, args.Plotstyle, applied, ensureResult, notes);
    }

    // ─────────────────────────────────────────────────────────────
    // list_plotstyles
    // ─────────────────────────────────────────────────────────────

    [McpTool("list_plotstyles",
        "Enumerate all plot-styles currently visible to AutoCAD (CTB + STB). filter='ctb' or 'stb' narrows the returned names. Also returns repo presets (HOSPITAL-ISO, ISO-Standard, monochrome), the AutoCAD Plot Styles directory, and the backend asset directory so the caller can prep ensure_ctb calls.",
        "plotstyles",
        Intent = new[]
        {
            "lista CTB",
            "list plot styles",
            "jakie plotstyle sa dostepne",
            "show available CTB STB",
            "enum plot style table",
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static async Task<ListPlotstylesResult> ListPlotstyles(
        IPluginGateway gw, ListPlotstylesArgs args, CancellationToken ct)
    {
        var resp = await InvokeListPlotStylesAsync(gw, ct).ConfigureAwait(false);
        var all = (resp["names"] as JsonArray)?.ToStringList() ?? new List<string>();
        var ctb = (resp["ctb"]   as JsonArray)?.ToStringList() ?? new List<string>();
        var stb = (resp["stb"]   as JsonArray)?.ToStringList() ?? new List<string>();
        string? dir = resp["directory"]?.GetValue<string>();

        IReadOnlyList<string> filtered = string.Equals(args.Filter, "ctb", StringComparison.OrdinalIgnoreCase)
            ? ctb
            : string.Equals(args.Filter, "stb", StringComparison.OrdinalIgnoreCase)
                ? stb
                : all;

        return new ListPlotstylesResult(
            Names: filtered,
            Ctb: ctb,
            Stb: stb,
            Presets: PlotstylesPalette.DefaultPresets,
            Directory: dir,
            AssetsDir: PlotstylesPalette.AssetsDirectory(),
            Count: filtered.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────

    private static async Task<JsonObject> InvokeListPlotStylesAsync(IPluginGateway gw, CancellationToken ct)
    {
        var resp = await gw.InvokeAsync("acad.layouts.list_plot_styles", new JsonObject(), T_NORMAL, ct).ConfigureAwait(false);
        if (resp is not JsonObject obj)
            throw new InvalidPluginShapeException("list_plot_styles returned null or non-object");
        return obj;
    }
}

internal static class JsonArrayExtensions
{
    public static List<string> ToStringList(this JsonArray arr)
    {
        var list = new List<string>(arr.Count);
        foreach (var n in arr)
        {
            var s = n?.GetValue<string>();
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }
        return list;
    }
}
