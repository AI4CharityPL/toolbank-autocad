// AutoCAD plugin handlers for the rest of roadmap 2.3: table cell styles, visual styles and
// point display.
//
// Three unrelated things share a file because each is small and none justifies its own. What
// they do share is that all three are places where the roadmap's wording was looser than the
// API, and the tool names here follow the API rather than the plan:
//
//   set_point_display   The plan said create_point_style + set_point_display_mode. There is no
//                       PointStyle type. Point display is PDMODE and PDSIZE, two system
//                       variables, global to the drawing. Two tools implying a per-style object
//                       would have over-promised; this is one tool that says what it does.
//   create_visual_style Derives from a named VisualStyleType preset. DBVisualStyle's only other
//                       surface is an untyped SetTrait/GetTrait pair with no discoverable
//                       property catalogue, and authoring against that would produce exactly the
//                       "catalogue advertises what the tool refuses" defect class this bank has
//                       already paid for once.

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
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using GI = Autodesk.AutoCAD.GraphicsInterface;

namespace AcadMcp.Plugin.Tools;

internal static class StylesMiscPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.styles.set_table_cell_style", SetTableCellStyle);
        host.Register("acad.styles.list_visual_styles", ListVisualStyles);
        host.Register("acad.styles.create_visual_style", CreateVisualStyle);
        host.Register("acad.styles.set_point_display", SetPointDisplay);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    // ─────────── table cell styles ───────────

    private static readonly Dictionary<string, CellAlignment> Alignments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["topLeft"] = CellAlignment.TopLeft,
            ["topCenter"] = CellAlignment.TopCenter,
            ["topRight"] = CellAlignment.TopRight,
            ["middleLeft"] = CellAlignment.MiddleLeft,
            ["middleCenter"] = CellAlignment.MiddleCenter,
            ["middleRight"] = CellAlignment.MiddleRight,
            ["bottomLeft"] = CellAlignment.BottomLeft,
            ["bottomCenter"] = CellAlignment.BottomCenter,
            ["bottomRight"] = CellAlignment.BottomRight,
        };

    private static TableStyle FindTableStyle(Database db, Transaction tr, string name, OpenMode mode)
    {
        var dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
        foreach (DBDictionaryEntry e in dict)
            if (string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase))
                return (TableStyle)tr.GetObject(e.Value, mode);
        throw new ArgumentException(
            "No table style named '" + name + "'. Use list_tablestyles.");
    }

    // Getters take RowType; setters take an int bit mask of row types. Same asymmetry the
    // existing StylesPluginTools.SetTs already casts around for SetTextHeight.
    private static RowType RowOf(string? name) => (name ?? "").Trim().ToLowerInvariant() switch
    {
        "title" or "titlerow" => RowType.TitleRow,
        "header" or "headerrow" => RowType.HeaderRow,
        "data" or "datarow" => RowType.DataRow,
        _ => throw new ArgumentException(
            "row must be 'title', 'header' or 'data'; got '" + name + "'."),
    };

    private static object CellInfo(TableStyle t, string row, RowType rt) => new
    {
        row,
        // textHeight is reported but NOT settable here - modify_tablestyle already owns it as
        // titleTextHeight / headerTextHeight / dataTextHeight. Reporting it anyway means a caller
        // sees the whole cell state after a change rather than half of it.
        textHeight = t.TextHeight(rt),
        alignment = Alignments.FirstOrDefault(kv => kv.Value == t.Alignment(rt)).Key ?? "unknown",
        colorIndex = t.Color(rt).ColorMethod == ColorMethod.ByAci ? (int?)t.Color(rt).ColorIndex : null,
        backgroundColorNone = t.IsBackgroundColorNone(rt),
        backgroundColorIndex = t.IsBackgroundColorNone(rt) ? null
            : (t.BackgroundColor(rt).ColorMethod == ColorMethod.ByAci
                ? (int?)t.BackgroundColor(rt).ColorIndex : null),
    };

    private static Task<ToolDispatchResult> SetTableCellStyle(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.set_table_cell_style", ct, (doc, db, tr) =>
        {
            var a = Read<SetTableCellStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            var rt = RowOf(a.Row);

            var t = FindTableStyle(db, tr, a.Name, OpenMode.ForWrite);
            var applied = new List<string>();

            if (!string.IsNullOrWhiteSpace(a.Alignment))
            {
                if (!Alignments.TryGetValue(a.Alignment, out var al))
                    throw new ArgumentException(
                        "Unknown alignment '" + a.Alignment + "'. Use one of: " +
                        string.Join(", ", Alignments.Keys) + ".");
                t.SetAlignment(al, (int)rt);
                applied.Add("alignment");
            }

            if (a.ColorIndex is int ci)
            {
                if (ci < 0 || ci > 256) throw new ArgumentException("colorIndex must be 0-256.");
                t.SetColor(Color.FromColorIndex(ColorMethod.ByAci, (short)ci), (int)rt);
                applied.Add("colorIndex");
            }

            // A negative index means "no background", which is a distinct state from any colour
            // and the only way to clear one that was set earlier.
            if (a.BackgroundColorIndex is int bg)
            {
                if (bg < 0) t.SetBackgroundColorNone(true, (int)rt);
                else
                {
                    if (bg > 256) throw new ArgumentException("backgroundColorIndex must be -1..256.");
                    t.SetBackgroundColorNone(false, (int)rt);
                    t.SetBackgroundColor(Color.FromColorIndex(ColorMethod.ByAci, (short)bg), (int)rt);
                }
                applied.Add("backgroundColorIndex");
            }

            if (applied.Count == 0)
                throw new ArgumentException(
                    "Nothing to set. Pass at least one of alignment, colorIndex, " +
                    "backgroundColorIndex. Text height is not here on purpose - modify_tablestyle " +
                    "already owns it as titleTextHeight / headerTextHeight / dataTextHeight, and " +
                    "two tools setting one property is how they drift apart.");

            return Wrap(new { cell = CellInfo(t, (a.Row ?? "").ToLowerInvariant(), rt), applied, tableStyle = t.Name });
        });

    // ─────────── visual styles ───────────

    private static Task<ToolDispatchResult> ListVisualStyles(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.styles.list_visual_styles", ct, (doc, db, tr) =>
        {
            var dict = (DBDictionary)tr.GetObject(db.VisualStyleDictionaryId, OpenMode.ForRead);
            var styles = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                var vs = (DBVisualStyle)tr.GetObject(e.Value, OpenMode.ForRead);

                // Internal-use styles are the ones AutoCAD keeps for its own rendering passes
                // (Dim, Brighten, ColorChange...). They are real entries in this dictionary and
                // hiding them would misreport the drawing, so they are flagged instead.
                styles.Add(new
                {
                    name = e.Key,
                    type = vs.Type.ToString(),
                    description = vs.Description ?? "",
                    internalUseOnly = vs.InternalUseOnly,
                });
            }
            return Wrap(new
            {
                styles,
                count = styles.Count,
                presets = Enum.GetNames(typeof(GI.VisualStyleType)).OrderBy(n => n).ToList(),
            });
        });

    private static Task<ToolDispatchResult> CreateVisualStyle(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.create_visual_style", ct, (doc, db, tr) =>
        {
            var a = Read<CreateVisualStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            AcadEnv.ValidateSymbolName(a.Name, "VisualStyle");
            if (string.IsNullOrWhiteSpace(a.BasedOn))
                throw new ArgumentException(
                    "basedOn is required: a visual style is created from one of AutoCAD's presets. " +
                    "list_visual_styles reports the full set in `presets`; Conceptual, Realistic, " +
                    "Shaded, Hidden, Wireframe2D and Wireframe3D are the everyday ones.");

            if (!Enum.TryParse<GI.VisualStyleType>(a.BasedOn, ignoreCase: true, out var preset))
                throw new ArgumentException(
                    "Unknown preset '" + a.BasedOn + "'. Valid values: " +
                    string.Join(", ", Enum.GetNames(typeof(GI.VisualStyleType)).OrderBy(n => n)) + ".");

            var dict = (DBDictionary)tr.GetObject(db.VisualStyleDictionaryId, OpenMode.ForWrite);
            if (dict.Contains(a.Name))
            {
                if (!a.Overwrite)
                    throw new ArgumentException(
                        "A visual style named '" + a.Name + "' already exists. Pass overwrite:true, " +
                        "or pick another name.");
                dict.Remove(a.Name);
            }

            var vs = new DBVisualStyle { Description = a.Description ?? a.BasedOn, Type = preset };
            dict.SetAt(a.Name, vs);
            tr.AddNewlyCreatedDBObject(vs, true);

            return Wrap(new
            {
                visualStyle = new
                {
                    name = a.Name,
                    type = vs.Type.ToString(),
                    description = vs.Description ?? "",
                    internalUseOnly = vs.InternalUseOnly,
                },
                created = true,
            });
        });

    // ─────────── point display ───────────

    // PDMODE is a bit-coded Int16 and the bits are not intuitive: 0-4 pick the glyph, +32/+64/+96
    // add a surrounding circle, square, or both. Naming them means a caller never has to know
    // that "a dot inside a circle" is 33.
    private static readonly Dictionary<string, short> PointGlyphs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dot"] = 0,
            ["none"] = 1,
            ["plus"] = 2,
            ["cross"] = 3,
            ["tick"] = 4,
        };

    private static Task<ToolDispatchResult> SetPointDisplay(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.set_point_display", ct, (doc, db, tr) =>
        {
            var a = Read<SetPointDisplayArgsDto>(args);

            var before = new
            {
                pdmode = Convert.ToInt16(Application.GetSystemVariable("PDMODE")),
                pdsize = Convert.ToDouble(Application.GetSystemVariable("PDSIZE")),
            };

            short? mode = null;
            if (a.Mode is int explicitMode)
            {
                if (explicitMode < 0 || explicitMode > 99)
                    throw new ArgumentException("mode must be 0-99 (a PDMODE bit code).");
                mode = (short)explicitMode;
            }
            else if (!string.IsNullOrWhiteSpace(a.Glyph))
            {
                if (!PointGlyphs.TryGetValue(a.Glyph, out var g))
                    throw new ArgumentException(
                        "Unknown glyph '" + a.Glyph + "'. Use one of: " +
                        string.Join(", ", PointGlyphs.Keys) + ", optionally with surround.");

                short surround = (a.Surround ?? "none").Trim().ToLowerInvariant() switch
                {
                    "none" => 0,
                    "circle" => 32,
                    "square" => 64,
                    "both" => 96,
                    _ => throw new ArgumentException(
                        "Unknown surround '" + a.Surround + "'. Use none, circle, square or both."),
                };
                mode = (short)(g + surround);
            }

            // Both are Int16/real system variables and SetSystemVariable is not forgiving: passing
            // an int where AutoCAD wants an Int16 raises eInvalidInput with nothing to identify
            // which variable was at fault. See rule 26.
            if (mode is short m) Application.SetSystemVariable("PDMODE", m);
            if (a.Size is double sz) Application.SetSystemVariable("PDSIZE", sz);

            if (mode is null && a.Size is null)
                throw new ArgumentException(
                    "Nothing to set. Pass glyph (with optional surround), or mode as a raw PDMODE " +
                    "value, or size.");

            return Wrap(new
            {
                before,
                after = new
                {
                    pdmode = Convert.ToInt16(Application.GetSystemVariable("PDMODE")),
                    pdsize = Convert.ToDouble(Application.GetSystemVariable("PDSIZE")),
                },
                note = "PDMODE and PDSIZE are drawing-wide. Existing points redraw on the next " +
                       "regen, so an unchanged screen immediately after this call is not a failure.",
            });
        });
}
